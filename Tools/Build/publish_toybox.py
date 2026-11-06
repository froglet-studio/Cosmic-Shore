#!/usr/bin/env python3
"""Publish a browser Toybox WebGL build to Cloudflare R2 (Docs/WEBGL_TOYBOX.md section 5).

The pipeline is stage -> promote -> (rollback). A build goes to the STAGING root first, is played there, and
reaches www.froglet.games/play only when someone promotes it (the toybox-publish workflow puts that step behind
the `toybox-production` environment's reviewers). Two roots in one bucket:

    toybox-staging/latest.json             what the staging page loads
    toybox-staging/builds/<v>/...          every staged build (immutable)
    toybox/latest.json                     what /play loads (production)
    toybox/builds/<v>/...                  every promoted build (immutable)
    <root>/builds/<v>/manifest.json        what the build is: version, commit, built_at, UBA build number, and
                                           every file with its size, sha256, content type and encoding

  fetch-uba   Download the Unity Build Automation WebGL Toybox build OF ONE COMMIT as a zip (never "the newest"):
                  python3 Tools/Build/publish_toybox.py fetch-uba --commit <sha> --out toybox.zip --meta-out uba.json
              --build <n> names a UBA build instead; --start-if-missing asks UBA to build that commit and waits.
              Needs UBA_API_KEY, UBA_ORG_ID, UBA_PROJECT_ID, UBA_TOYBOX_TARGET in the environment.

  upload      Upload a built WebGL folder (the one holding Build/) to <root>/builds/<version>/, write its
              manifest.json, then point <root>/latest.json at it. --root staging is the pipeline's root;
              --root production is the hand path (a local build straight to /play).
                  python3 Tools/Build/publish_toybox.py upload --root staging --build-dir Builds/WebGLToybox \
                      --version 42-1ac6f87 --commit 1ac6f87ae0...
              --dry-run prints every object with its headers and the manifest, needs nothing, uploads nothing.

  promote     Copy a staged build into the production root (server side, R2 to R2) and point toybox/latest.json
              at it, recording the version it replaces as `previous`:
                  python3 Tools/Build/publish_toybox.py promote --from-staging 42-1ac6f87
              Refuses a version that is not staged, or not the one the staging page serves.

  rollback    Point toybox/latest.json back at `previous` (or --to <v>), after checking every file in that
              version's manifest is on R2 with the right size and headers. Refuses otherwise.

  list        Every version under a root (or both) with its commit and build date; marks LIVE and previous.

  adopt       Write the manifest.json a build uploaded before manifests existed never got (builds/1, builds/2),
              read from what is on R2, so it can be a rollback target. Changes no pointer.

  serve       Serve a built WebGL folder on http://localhost with the same headers, to play a
              local build before uploading it (the build's own index.html, the FrogletToybox template).
                  python3 Tools/Build/publish_toybox.py serve --build-dir Builds/WebGLToybox
              Browsers decode Brotli on localhost even without HTTPS. Needs nothing installed.

  check       Read a published root the way www.froglet.games/play will, and say whether it will load:
              latest.json, then each build file's Content-Encoding, Content-Type and CORS header.
                  python3 Tools/Build/publish_toybox.py check --root https://play.froglet.games/toybox
              Exit 0 = /play can load it. Run it after every first upload to a new host.

  self-test   Offline proof with a fake R2: upload -> promote -> rollback, and the negative controls (a rollback
              to a version with a missing file, a promote of an unknown version, an overwrite of an immutable
              build, a UBA build of the wrong commit, and a signed URL in an error, which must be redacted).

Why promote COPIES instead of sharing one builds/ folder: the production root stays self-contained (the /play
page, `check` and the manifests all read toybox/ only), the staging root can be wiped or moved to another bucket
without breaking /play, and a copy is server side, so the bytes that go live are byte for byte the bytes that were
played on staging (the copy is verified against the manifest before the pointer moves). The cost is a second copy
of each promoted build (~115 MB) in R2.

Why the headers matter: the build is compressed (Brotli or gzip) with Unity's decompression
fallback off, so the browser can only read it if each file arrives with Content-Encoding set,
the right Content-Type (application/wasm for the code, or the browser refuses to stream-compile
it), and no re-compression on the way (Cache-Control: no-transform). Hashed build files never
change, so they are cached for a year; latest.json is never cached, so a new build is live as
soon as it is written. latest.json goes up LAST, so a page never points at files that are still
uploading, and a rollback is rewriting latest.json to an older version folder.

Nothing here prints a response body, a URL with a query string or a credential: errors are a status code and
a short reason (redact()), because these logs are public and a UBA download link is a signed URL.
"""
import argparse
import contextlib
import datetime as _dt
import functools
import hashlib
import http.server
import io
import json
import mimetypes
import os
import re
import sys
import tempfile
import time
import urllib.error
import urllib.request

CONTENT_TYPES = {
    ".wasm": "application/wasm",
    ".js": "application/javascript",
    ".data": "application/octet-stream",
    ".json": "application/json",
    ".html": "text/html; charset=utf-8",
    ".css": "text/css",
    ".png": "image/png",
    ".jpg": "image/jpeg",
    ".ico": "image/x-icon",
    ".txt": "text/plain; charset=utf-8",
    ".bank": "application/octet-stream",   # FMOD banks in StreamingAssets
    ".symbols.json": "application/json",
}
ENCODINGS = {".br": "br", ".gz": "gzip"}
IMMUTABLE = "public, max-age=31536000, immutable, no-transform"
NO_CACHE = "no-cache, no-transform"

ROOTS = {"staging": "toybox-staging", "production": "toybox"}
BUILD_MANIFEST_TXT = "build_manifest.txt"   # written by WebToyboxBuild next to the player
MANIFEST_SCHEMA = 1

UBA_API = "https://build-api.cloud.unity3d.com/api/v1"
UBA_ENV = ("UBA_API_KEY", "UBA_ORG_ID", "UBA_PROJECT_ID", "UBA_TOYBOX_TARGET")
R2_ENV = ("R2_ACCOUNT_ID", "R2_ACCESS_KEY_ID", "R2_SECRET_ACCESS_KEY", "R2_BUCKET")

VERSION_RE = re.compile(r"^[0-9A-Za-z][0-9A-Za-z.+_-]{0,39}$")   # the launch-status contract's version rule
SHA_RE = re.compile(r"^[0-9a-f]{7,40}$")

# Swapped by the self-test; everything that touches the network goes through these.
_urlopen = urllib.request.urlopen
_sleep = time.sleep


# ──────────────────────────────────────────────
#  Redaction: what an error may say
# ──────────────────────────────────────────────

_URL_WITH_QUERY = re.compile(r"(https?://[^\s?#'\"<>]+)\?[^\s'\"<>]*")
_LONG_TOKEN = re.compile(r"[A-Za-z0-9+/=_%-]{40,}")


def redact(text, limit=300):
    """A foreign message (an exception, a library error) made safe for a public log: URLs lose their query
    string (a signed URL is a credential), the values of every secret this tool reads become ***, long
    unbroken tokens become <redacted>, and the whole is cut short."""
    text = str(text)
    for name in UBA_ENV + R2_ENV:
        value = os.environ.get(name)
        if value and len(value) >= 4:
            text = text.replace(value, "***")
    text = _URL_WITH_QUERY.sub(r"\1?<redacted>", text)
    text = _LONG_TOKEN.sub("<redacted>", text)
    text = " ".join(text.split())
    return text if len(text) <= limit else text[: limit - 3] + "..."


def http_reason(e):
    """'HTTP 403 Forbidden' or the error's class: never its body, never its URL."""
    if isinstance(e, urllib.error.HTTPError):
        return f"HTTP {e.code} {redact(e.reason or '', 60)}".strip()
    if isinstance(e, urllib.error.URLError):
        return f"network error ({type(e.reason).__name__})"
    return type(e).__name__


def _s3_code(e):
    resp = getattr(e, "response", None) or {}
    err = resp.get("Error", {}) if isinstance(resp, dict) else {}
    status = (resp.get("ResponseMetadata", {}) if isinstance(resp, dict) else {}).get("HTTPStatusCode")
    return str(err.get("Code") or ""), status


def s3_reason(e):
    code, status = _s3_code(e)
    if code or status:
        return f"{code or 'error'} (HTTP {status or '?'})"
    return f"{type(e).__name__}: {redact(e, 120)}"


def _is_missing(e):
    code, status = _s3_code(e)
    return code in ("404", "NoSuchKey", "NotFound") or status == 404


def now_iso():
    return _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


# ──────────────────────────────────────────────
#  Headers and the build folder
# ──────────────────────────────────────────────

def headers_for(name):
    """(content_type, content_encoding or None) for a published file name."""
    base, enc = name, None
    for ext, e in ENCODINGS.items():
        if base.endswith(ext):
            base, enc = base[: -len(ext)], e
            break
    if base.endswith(".unityweb"):
        # Unity's legacy compressed suffix: the compression is not in the name. Refuse rather
        # than guess, because a wrong Content-Encoding is a page that never loads.
        raise SystemExit(f"{name}: .unityweb files carry no compression in their name. Build with "
                         "'Name Files As Hashes' and Brotli or Gzip (the toybox settings) instead.")
    ctype = None
    for ext, t in sorted(CONTENT_TYPES.items(), key=lambda kv: -len(kv[0])):
        if base.endswith(ext):
            ctype = t
            break
    return ctype or mimetypes.guess_type(base)[0] or "application/octet-stream", enc


def pick_build_files(names):
    """The four files createUnityInstance needs, from the file names in a Build/ folder."""
    names = sorted(names)

    def one(pred, what):
        hits = [n for n in names if pred(n)]
        if len(hits) != 1:
            raise SystemExit(f"Build/: expected exactly one {what}, found {hits or 'none'}")
        return "Build/" + hits[0]

    strip = lambda n: n[:-3] if n.endswith(".br") or n.endswith(".gz") else n
    return {
        "loader": one(lambda n: n.endswith(".loader.js"), "*.loader.js"),
        "framework": one(lambda n: strip(n).endswith(".framework.js"), "*.framework.js[.br|.gz]"),
        "code": one(lambda n: strip(n).endswith(".wasm"), "*.wasm[.br|.gz]"),
        "data": one(lambda n: strip(n).endswith(".data"), "*.data[.br|.gz]"),
    }


def find_build_files(build_dir):
    """The four files createUnityInstance needs, as paths relative to build_dir."""
    bdir = os.path.join(build_dir, "Build")
    if not os.path.isdir(bdir):
        raise SystemExit(f"{build_dir}: no Build/ folder - point --build-dir at the WebGL output folder.")
    return pick_build_files(os.listdir(bdir))


def _sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def read_build_manifest_txt(build_dir):
    """key=value pairs from the build_manifest.txt WebToyboxBuild writes (empty when the build has none:
    a UBA build made through PreExport does not run WebToyboxBuild.Run, so it carries none)."""
    path = os.path.join(build_dir, BUILD_MANIFEST_TXT)
    out = {}
    if os.path.isfile(path):
        with open(path, encoding="utf-8", errors="replace") as fh:
            for line in fh:
                if "=" in line:
                    k, v = line.rstrip("\n").split("=", 1)
                    out[k.strip()] = v.strip()
    return out


def plan_upload(build_dir, prefix, version):
    """Every object to write, in order, plus the pointer latest.json will carry. Paths in the pointer and in
    the manifest are relative to the root, so the same manifest is right under staging and production."""
    files = find_build_files(build_dir)
    version_root = f"builds/{version}"
    objects, total = [], 0
    paths = []
    for sub in ("Build", "StreamingAssets"):
        root = os.path.join(build_dir, sub)
        if not os.path.isdir(root):
            continue
        for dirpath, dirnames, filenames in os.walk(root):
            dirnames.sort()
            for fn in sorted(filenames):
                paths.append(os.path.join(dirpath, fn))
    if os.path.isfile(os.path.join(build_dir, BUILD_MANIFEST_TXT)):
        paths.append(os.path.join(build_dir, BUILD_MANIFEST_TXT))
    for path in paths:
        rel = os.path.relpath(path, build_dir).replace(os.sep, "/")
        ctype, enc = headers_for(os.path.basename(path))
        size = os.path.getsize(path)
        total += size
        objects.append({
            "path": path,
            "rel": rel,
            "key": f"{prefix}/{version_root}/{rel}",
            "ContentType": ctype,
            "ContentEncoding": enc,
            "CacheControl": IMMUTABLE,
            "bytes": size,
        })
    pointer = {
        "version": str(version),
        "loader": f"{version_root}/{files['loader']}",
        "framework": f"{version_root}/{files['framework']}",
        "code": f"{version_root}/{files['code']}",
        "data": f"{version_root}/{files['data']}",
        "streamingAssets": f"{version_root}/StreamingAssets",
        "bytes": total,
    }
    return objects, pointer


def make_manifest(version, objects, pointer, commit, built_at, uba_build=None, tag=None):
    return {
        "schema": MANIFEST_SCHEMA,
        "version": str(version),
        "commit": commit,
        "built_at": built_at,
        "uba_build": uba_build,
        "tag": tag,
        "pointer": pointer,
        "files": [{
            "path": o["rel"],
            "bytes": o["bytes"],
            "sha256": o.get("sha256") or _sha256_file(o["path"]),
            "content_type": o["ContentType"],
            "content_encoding": o["ContentEncoding"],
        } for o in objects],
    }


def latest_from(manifest, previous):
    """The latest.json a root serves for this manifest: the pointer /play reads (version + four paths +
    streamingAssets + bytes), plus who the build is and what was live before it."""
    v = manifest["version"]
    return {
        **manifest["pointer"],
        "version": v,
        "commit": manifest.get("commit") or "",
        "built_at": manifest.get("built_at") or "",
        "manifest": f"builds/{v}/manifest.json",
        "previous": previous,
        "published_at": now_iso(),
    }


def _same_files(a, b):
    key = lambda m: sorted((f["path"], f["bytes"], f["sha256"]) for f in m.get("files", []))
    return key(a) == key(b)


# ──────────────────────────────────────────────
#  R2 (S3 API)
# ──────────────────────────────────────────────

class R2:
    """The few S3 calls this tool makes, each turning a failure into a short, redacted reason."""

    def __init__(self, s3, bucket):
        self.s3, self.bucket = s3, bucket

    def _call(self, what, fn, **kw):
        try:
            return fn(Bucket=self.bucket, **kw)
        except Exception as e:  # noqa: BLE001 - boto3's errors and the fake's share only `.response`
            if _is_missing(e):
                raise KeyError(kw.get("Key") or kw.get("Prefix")) from None
            raise SystemExit(f"R2 {what} failed: {s3_reason(e)}") from None

    def head(self, key):
        try:
            h = self._call("head", self.s3.head_object, Key=key)
        except KeyError:
            return None
        return {"bytes": h.get("ContentLength"), "content_type": h.get("ContentType"),
                "content_encoding": h.get("ContentEncoding") or None, "cache_control": h.get("CacheControl")}

    def get_bytes(self, key):
        try:
            return self._call("get", self.s3.get_object, Key=key)["Body"].read()
        except KeyError:
            return None

    def get_json(self, key):
        raw = self.get_bytes(key)
        if raw is None:
            return None
        try:
            return json.loads(raw.decode())
        except (UnicodeDecodeError, json.JSONDecodeError):
            raise SystemExit(f"{key} is not valid JSON") from None

    def put_json(self, key, obj, cache):
        self._call("put", self.s3.put_object, Key=key, Body=json.dumps(obj, indent=1).encode(),
                   ContentType="application/json", CacheControl=cache)

    def upload(self, path, key, ctype, enc, cache):
        extra = {"ContentType": ctype, "CacheControl": cache}
        if enc:
            extra["ContentEncoding"] = enc
        try:
            self.s3.upload_file(path, self.bucket, key, ExtraArgs=extra)
        except Exception as e:  # noqa: BLE001
            raise SystemExit(f"R2 upload of {key} failed: {s3_reason(e)}") from None

    def copy(self, src, dst, ctype, enc, cache):
        # REPLACE with the manifest's headers rather than trusting a provider's COPY semantics, so the
        # production object carries exactly the encoding and type the browser needs.
        kw = {"Key": dst, "CopySource": {"Bucket": self.bucket, "Key": src}, "MetadataDirective": "REPLACE",
              "ContentType": ctype, "CacheControl": cache}
        if enc:
            kw["ContentEncoding"] = enc
        self._call("copy", self.s3.copy_object, **kw)

    def keys(self, prefix):
        out, token = [], None
        while True:
            kw = {"Prefix": prefix}
            if token:
                kw["ContinuationToken"] = token
            page = self._call("list", self.s3.list_objects_v2, **kw)
            out += [o["Key"] for o in page.get("Contents", [])]
            if not page.get("IsTruncated"):
                return out
            token = page.get("NextContinuationToken")

    def versions(self, prefix):
        out, token = [], None
        while True:
            kw = {"Prefix": f"{prefix}/builds/", "Delimiter": "/"}
            if token:
                kw["ContinuationToken"] = token
            page = self._call("list", self.s3.list_objects_v2, **kw)
            out += [p["Prefix"][len(prefix) + len("/builds/"):].strip("/") for p in page.get("CommonPrefixes", [])]
            if not page.get("IsTruncated"):
                return out
            token = page.get("NextContinuationToken")


def r2_from_env():
    try:
        import boto3  # noqa: PLC0415 - only the real calls need it
    except ImportError:
        raise SystemExit("boto3 is required: pip install boto3") from None
    env = {k: os.environ.get(k) for k in R2_ENV}
    missing = [k for k, v in env.items() if not v]
    if missing:
        raise SystemExit("missing environment: " + ", ".join(missing))
    s3 = boto3.client(
        "s3",
        # R2_ENDPOINT overrides the account endpoint (a local S3 server for testing).
        endpoint_url=os.environ.get("R2_ENDPOINT") or f"https://{env['R2_ACCOUNT_ID']}.r2.cloudflarestorage.com",
        aws_access_key_id=env["R2_ACCESS_KEY_ID"],
        aws_secret_access_key=env["R2_SECRET_ACCESS_KEY"],
        region_name="auto",
    )
    return R2(s3, env["R2_BUCKET"])


def verify_files(r2, prefix, manifest):
    """Every file the manifest names is on R2 under <prefix>/builds/<v>/ with its size, type and encoding, and
    the pointer's four files are among them. Returns a list of problems (empty = the build can be served)."""
    v = manifest["version"]
    problems = []
    listed = {f["path"] for f in manifest.get("files", [])}
    for key in ("loader", "framework", "code", "data"):
        rel = manifest["pointer"][key][len(f"builds/{v}/"):]
        if rel not in listed:
            problems.append(f"the manifest does not list its own {key} ({rel})")
    for f in manifest.get("files", []):
        h = r2.head(f"{prefix}/builds/{v}/{f['path']}")
        if h is None:
            problems.append(f"missing: {f['path']}")
            continue
        if h["bytes"] != f["bytes"]:
            problems.append(f"size differs: {f['path']} ({h['bytes']} on R2, {f['bytes']} in the manifest)")
        if (h["content_type"] or "").split(";")[0] != (f["content_type"] or "").split(";")[0]:
            problems.append(f"content type differs: {f['path']} ({h['content_type']})")
        if (h["content_encoding"] or None) != (f["content_encoding"] or None):
            problems.append(f"content encoding differs: {f['path']} ({h['content_encoding']})")
    return problems


def _refuse(what, problems):
    shown = "\n  ".join(problems[:12]) + (f"\n  ... and {len(problems) - 12} more" if len(problems) > 12 else "")
    raise SystemExit(f"refusing {what}:\n  {shown}")


def _write_result(path, result):
    if path:
        with open(path, "w") as fh:
            json.dump(result, fh)


# ──────────────────────────────────────────────
#  upload / promote / rollback / list / adopt
# ──────────────────────────────────────────────

def stage_build(r2, build_dir, prefix, version, commit, built_at, uba_build=None, tag=None):
    """Upload a build folder to <prefix>/builds/<version>/ (immutable), its manifest, then <prefix>/latest.json."""
    objects, pointer = plan_upload(build_dir, prefix, version)
    for o in objects:
        o["sha256"] = _sha256_file(o["path"])
    manifest = make_manifest(version, objects, pointer, commit, built_at, uba_build, tag)
    mkey = f"{prefix}/builds/{version}/manifest.json"
    existing = r2.get_json(mkey)
    if existing is not None:
        if not _same_files(existing, manifest):
            raise SystemExit(f"refusing upload: {prefix}/builds/{version}/ already holds a different build "
                             "(builds are immutable). Use a new --version.")
        print(f"{prefix}/builds/{version}/ already holds this exact build; not uploading it again")
        manifest = existing
    else:
        for o in objects:
            r2.upload(o["path"], o["key"], o["ContentType"], o["ContentEncoding"], o["CacheControl"])
            print("uploaded", o["key"])
        r2.put_json(mkey, manifest, IMMUTABLE)
        print("wrote", mkey)
    problems = verify_files(r2, prefix, manifest)
    if problems:
        _refuse(f"to point {prefix}/latest.json at {version}", problems)
    current = r2.get_json(f"{prefix}/latest.json")
    previous = None
    if current:
        previous = current.get("version") if current.get("version") != version else current.get("previous")
    latest = latest_from(manifest, previous)
    r2.put_json(f"{prefix}/latest.json", latest, NO_CACHE)
    print(f"published {prefix}/latest.json -> {version} ({pointer['bytes'] / 1048576:.0f} MB, "
          f"commit {(commit or '?')[:10]}, previous {previous or 'none'})")
    return {"version": version, "commit": commit, "previous": previous, "bytes": pointer["bytes"], "root": prefix}


def promote(r2, version):
    """Copy staging's build <version> into production and point toybox/latest.json at it."""
    stage, prod = ROOTS["staging"], ROOTS["production"]
    s_manifest = r2.get_json(f"{stage}/builds/{version}/manifest.json")
    if s_manifest is None:
        raise SystemExit(f"refusing promote: {version} is not staged (no {stage}/builds/{version}/manifest.json). "
                         f"Staged versions: {', '.join(r2.versions(stage)) or 'none'}")
    s_latest = r2.get_json(f"{stage}/latest.json") or {}
    if s_latest.get("version") != version:
        raise SystemExit(f"refusing promote: the staging page serves {s_latest.get('version') or 'nothing'}, not "
                         f"{version}. Promote what was played on staging, or stage {version} first.")
    problems = verify_files(r2, stage, s_manifest)
    if problems:
        _refuse(f"promote of {version}: the staged build is incomplete", problems)

    p_manifest = r2.get_json(f"{prod}/builds/{version}/manifest.json")
    if p_manifest is not None and not _same_files(p_manifest, s_manifest):
        raise SystemExit(f"refusing promote: {prod}/builds/{version}/ already holds a different build "
                         "(builds are immutable).")
    if p_manifest is None:
        for f in s_manifest["files"]:
            src, dst = f"{stage}/builds/{version}/{f['path']}", f"{prod}/builds/{version}/{f['path']}"
            r2.copy(src, dst, f["content_type"], f["content_encoding"], IMMUTABLE)
            print("copied", dst)
        r2.put_json(f"{prod}/builds/{version}/manifest.json", s_manifest, IMMUTABLE)
    problems = verify_files(r2, prod, s_manifest)
    if problems:
        _refuse(f"to point {prod}/latest.json at {version}: the copy is incomplete", problems)

    current = r2.get_json(f"{prod}/latest.json") or {}
    if current.get("version") == version:
        print(f"{version} is already live on {prod}/latest.json; nothing to change")
        return {"version": version, "commit": s_manifest.get("commit"), "previous": current.get("previous"),
                "changed": False}
    previous = current.get("version")
    r2.put_json(f"{prod}/latest.json", latest_from(s_manifest, previous), NO_CACHE)
    print(f"promoted {version} (commit {(s_manifest.get('commit') or '?')[:10]}) to {prod}/latest.json; "
          f"previous {previous or 'none'}")
    return {"version": version, "commit": s_manifest.get("commit"), "previous": previous, "changed": True}


def rollback(r2, to=None):
    """Point toybox/latest.json at `previous` (or `to`), once every file of that build is verified on R2."""
    prod = ROOTS["production"]
    current = r2.get_json(f"{prod}/latest.json")
    if not current:
        raise SystemExit(f"refusing rollback: {prod}/latest.json does not exist")
    target = to or current.get("previous")
    if not target:
        raise SystemExit("refusing rollback: latest.json records no previous version; name one with --to "
                         "(see `list --root production`)")
    if target == current.get("version"):
        raise SystemExit(f"refusing rollback: {target} is already live")
    manifest = r2.get_json(f"{prod}/builds/{target}/manifest.json")
    if manifest is None:
        raise SystemExit(f"refusing rollback: {prod}/builds/{target}/ has no manifest.json, so its files cannot be "
                         f"verified. If it was uploaded by hand before manifests existed, `adopt --version {target}` "
                         "records one.")
    problems = verify_files(r2, prod, manifest)
    if problems:
        _refuse(f"rollback to {target}: its build is not all on R2", problems)
    r2.put_json(f"{prod}/latest.json", latest_from(manifest, current.get("version")), NO_CACHE)
    print(f"rolled back {prod}/latest.json: {current.get('version')} -> {target} "
          f"(commit {(manifest.get('commit') or '?')[:10]})")
    return {"version": target, "commit": manifest.get("commit"), "previous": current.get("version")}


def list_versions(r2, which):
    lines = []
    for name in which:
        prefix = ROOTS[name]
        latest = r2.get_json(f"{prefix}/latest.json") or {}
        lines.append(f"{name} ({prefix}/): live {latest.get('version') or 'none'}, "
                     f"previous {latest.get('previous') or 'none'}")
        rows = []
        for v in r2.versions(prefix):
            m = r2.get_json(f"{prefix}/builds/{v}/manifest.json")
            mark = "LIVE" if v == latest.get("version") else ("prev" if v == latest.get("previous") else "")
            if m is None:
                rows.append(("", v, f"  {mark:4}  {v:24}  (no manifest: `adopt` it to make it a rollback target)"))
                continue
            rows.append((m.get("built_at") or "", v,
                         f"  {mark:4}  {v:24}  commit {(m.get('commit') or '?')[:10]:10}  built "
                         f"{m.get('built_at') or '?':20}  {m['pointer'].get('bytes', 0) / 1048576:5.0f} MB  "
                         f"{len(m.get('files', []))} files"))
        lines += [r[2] for r in sorted(rows)] or ["  (no builds)"]
    return "\n".join(lines)


def adopt(r2, prefix, version, commit="", built_at=""):
    """Record a manifest for a build that was uploaded without one, from what R2 holds."""
    base = f"{prefix}/builds/{version}/"
    if r2.get_json(base + "manifest.json") is not None:
        raise SystemExit(f"refusing adopt: {base}manifest.json already exists")
    keys = sorted(k for k in r2.keys(base) if not k.endswith("/manifest.json"))
    if not keys:
        raise SystemExit(f"refusing adopt: nothing under {base}")
    rels = [k[len(base):] for k in keys]
    picked = pick_build_files([r[len("Build/"):] for r in rels if r.startswith("Build/") and "/" not in r[6:]])
    files, total = [], 0
    for key, rel in zip(keys, rels):
        h = r2.head(key)
        body = r2.get_bytes(key) or b""
        files.append({"path": rel, "bytes": h["bytes"], "sha256": hashlib.sha256(body).hexdigest(),
                      "content_type": h["content_type"], "content_encoding": h["content_encoding"]})
        total += h["bytes"] or 0
        print("hashed", key)
    root = f"builds/{version}"
    manifest = {
        "schema": MANIFEST_SCHEMA, "version": version, "commit": commit, "built_at": built_at,
        "uba_build": None, "tag": None, "adopted_at": now_iso(),
        "pointer": {"version": version, **{k: f"{root}/{v}" for k, v in picked.items()},
                    "streamingAssets": f"{root}/StreamingAssets", "bytes": total},
        "files": files,
    }
    r2.put_json(base + "manifest.json", manifest, IMMUTABLE)
    print(f"wrote {base}manifest.json ({len(files)} files)")
    return manifest


def _resolve_identity(args, build_dir):
    """The commit and build time this upload records: the flags (from UBA) first, then build_manifest.txt."""
    txt = read_build_manifest_txt(build_dir)
    txt_commit = txt.get("commit", "")
    txt_commit = txt_commit.lower() if SHA_RE.match(txt_commit.lower() or "-") else ""
    commit = (args.commit or "").lower()
    if commit and not SHA_RE.match(commit):
        raise SystemExit(f"--commit {args.commit!r} is not a commit sha")
    if commit and txt_commit and not (commit.startswith(txt_commit) or txt_commit.startswith(commit)):
        raise SystemExit(f"refusing upload: --commit {commit[:10]} but the build's {BUILD_MANIFEST_TXT} says "
                         f"{txt_commit[:10]}")
    commit = commit or txt_commit
    if not commit:
        raise SystemExit("refusing upload: no commit for this build. Pass --commit <sha> (the commit it was built "
                         f"from); {BUILD_MANIFEST_TXT} has none.")
    built_at = args.built_at or txt.get("builtAtUtc") or ""
    if not built_at:
        newest = max(os.path.getmtime(os.path.join(dp, f))
                     for dp, _, fs in os.walk(os.path.join(build_dir, "Build")) for f in fs)
        built_at = _dt.datetime.fromtimestamp(newest, _dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    return commit, built_at


def _check_version(v):
    if not VERSION_RE.match(v or ""):
        raise SystemExit(f"version {v!r} is not a plain version (letters, digits, . + _ -; at most 40)")
    return v


def cmd_upload(args):
    _check_version(args.version)
    prefix = (args.prefix or ROOTS[args.root]).strip("/")
    objects, pointer = plan_upload(args.build_dir, prefix, args.version)
    if args.dry_run:
        for o in objects:
            print(f"{o['key']}  type={o['ContentType']}  encoding={o['ContentEncoding']}  "
                  f"cache={o['CacheControl']}  {o['bytes']} B")
        print(f"{prefix}/builds/{args.version}/manifest.json  type=application/json  cache={IMMUTABLE}")
        print(f"{prefix}/latest.json  type=application/json  cache={NO_CACHE}  (last)")
        print(json.dumps(pointer, indent=2))
        return 0
    commit, built_at = _resolve_identity(args, args.build_dir)
    result = stage_build(r2_from_env(), args.build_dir, prefix, args.version, commit, built_at,
                         uba_build=args.uba_build, tag=args.tag)
    _write_result(args.result_out, result)
    return 0


def cmd_promote(args):
    _write_result(args.result_out, promote(r2_from_env(), _check_version(args.from_staging)))
    return 0


def cmd_rollback(args):
    _write_result(args.result_out, rollback(r2_from_env(), _check_version(args.to) if args.to else None))
    return 0


def cmd_list(args):
    print(list_versions(r2_from_env(), ["staging", "production"] if args.root == "both" else [args.root]))
    return 0


def cmd_adopt(args):
    adopt(r2_from_env(), ROOTS[args.root], _check_version(args.version), (args.commit or "").lower(),
          args.built_at or "")
    return 0


# ──────────────────────────────────────────────
#  Unity Build Automation
# ──────────────────────────────────────────────

def uba_request(url, key, what, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method="POST" if data else "GET", headers={
        # UBA's v1 API takes the API key itself as the Basic credential.
        "Authorization": "Basic " + key,
        "Content-Type": "application/json",
    })
    try:
        with _urlopen(req, timeout=60) as r:
            raw = r.read()
    except Exception as e:  # noqa: BLE001 - every failure is reported the same, redacted way
        raise SystemExit(f"UBA API {what} failed: {http_reason(e)}") from None
    try:
        return json.loads(raw.decode())
    except (UnicodeDecodeError, json.JSONDecodeError):
        raise SystemExit(f"UBA API {what}: the response is not JSON ({len(raw)} bytes, not shown)") from None


def _uba_env():
    env = {k: os.environ.get(k) for k in UBA_ENV}
    missing = [k for k, v in env.items() if not v]
    if missing:
        raise SystemExit("missing environment: " + ", ".join(missing))
    base = (f"{UBA_API}/orgs/{env['UBA_ORG_ID']}/projects/{env['UBA_PROJECT_ID']}"
            f"/buildtargets/{env['UBA_TOYBOX_TARGET']}/builds")
    return base, env["UBA_API_KEY"]


def _rev(b):
    return str((b or {}).get("lastBuiltRevision") or "").lower() if isinstance(b, dict) else ""


def uba_successful_builds(base, key, pages=4):
    out = []
    for page in range(1, pages + 1):
        builds = uba_request(f"{base}?buildStatus=success&per_page=25&page={page}", key, "list builds")
        if not isinstance(builds, list):
            raise SystemExit(f"UBA API list builds: expected a list, got {type(builds).__name__} (not shown)")
        out += [b for b in builds if isinstance(b, dict)]
        if len(builds) < 25:
            break
    return out


def uba_pick(builds, commit=None, number=None):
    """The build of exactly this commit (or this build number); None when there is none. Never 'the newest'."""
    if number is not None:
        hits = [b for b in builds if str(b.get("build")) == str(number)]
    else:
        hits = [b for b in builds if commit and _rev(b).startswith(commit)]
    return max(hits, key=lambda b: int(b.get("build") or 0)) if hits else None


def uba_start_and_wait(base, key, commit, wait_minutes):
    """Ask UBA to build `commit` on the toybox target and wait for it. The `commit` field of UBA's start-build
    body is not yet verified against the live API, so the caller re-checks the finished build's revision."""
    started = uba_request(base, key, "start build", body={"clean": False, "delay": 0, "commit": commit})
    first = started[0] if isinstance(started, list) and started else started
    number = first.get("build") if isinstance(first, dict) else None
    if number is None:
        raise SystemExit("UBA API start build: no build number in the response (not shown)")
    print(f"started UBA build #{number} for {commit[:10]}; waiting up to {wait_minutes} min")
    deadline = time.monotonic() + wait_minutes * 60
    while True:
        b = uba_request(f"{base}/{number}", key, f"poll build #{number}")
        status = (b or {}).get("buildStatus") if isinstance(b, dict) else None
        if status == "success":
            return b
        if status in ("failure", "canceled", "cancelled", "unknown"):
            raise SystemExit(f"UBA build #{number} ended: {status}")
        if time.monotonic() > deadline:
            raise SystemExit(f"UBA build #{number} still {status} after {wait_minutes} min")
        _sleep(60)


def _download(href, out):
    try:
        with _urlopen(urllib.request.Request(href), timeout=600) as r, open(out, "wb") as fh:
            for chunk in iter(lambda: r.read(1 << 20), b""):
                fh.write(chunk)
    except Exception as e:  # noqa: BLE001 - the URL is signed: never let it reach the log
        with contextlib.suppress(OSError):
            os.remove(out)
        raise SystemExit(f"download of the UBA build failed: {http_reason(e)}") from None


def fetch_uba(commit, number, out, start_if_missing=False, wait_minutes=120):
    base, key = _uba_env()
    builds = uba_successful_builds(base, key)
    b = uba_pick(builds, commit=commit, number=number)
    if b is None and number is None and start_if_missing:
        b = uba_start_and_wait(base, key, commit, wait_minutes)
    if b is None:
        what = f"build #{number}" if number is not None else f"build of commit {commit[:10]}"
        raise SystemExit(f"no successful {what} of the WebGL Toybox target among the last {len(builds)} successful "
                         "builds. Start one in UBA (or pass --start-if-missing) and run again.")
    if commit and not _rev(b).startswith(commit):
        raise SystemExit(f"refusing: UBA build #{b.get('build')} is of {_rev(b)[:10] or 'an unknown commit'}, "
                         f"not {commit[:10]}")
    href = ((b.get("links") or {}).get("download_primary") or {}).get("href") if isinstance(b.get("links"), dict) \
        else None
    if not href:
        raise SystemExit(f"UBA build #{b.get('build')} has no primary download (links: "
                         f"{', '.join(sorted((b.get('links') or {}).keys())) if isinstance(b.get('links'), dict) else 'none'})")
    meta = {"build": b.get("build"), "commit": _rev(b), "finished": b.get("finished") or ""}
    print(f"UBA build #{meta['build']} at {meta['commit'][:10]} - downloading")
    _download(href, out)
    print("saved", out)
    return meta


def cmd_fetch_uba(args):
    if not args.commit and args.build is None:
        raise SystemExit("name the build: --commit <sha> (the commit to stage) or --build <n>")
    commit = (args.commit or "").lower()
    if commit and not SHA_RE.match(commit):
        raise SystemExit(f"--commit {args.commit!r} is not a commit sha (7 to 40 hex characters)")
    meta = fetch_uba(commit, args.build, args.out, args.start_if_missing, args.wait_minutes)
    _write_result(args.meta_out, meta)
    return 0


# ──────────────────────────────────────────────
#  serve / check
# ──────────────────────────────────────────────

class _ToyboxHandler(http.server.SimpleHTTPRequestHandler):
    """Serves a build folder with the headers a real host gives it (headers_for)."""

    def guess_type(self, path):
        ctype, _ = headers_for(os.path.basename(path)) if not path.endswith(".unityweb") else (None, None)
        return ctype or super().guess_type(path)

    def end_headers(self):
        name = os.path.basename(self.path.split("?", 1)[0])
        if not name.endswith(".unityweb"):
            _, enc = headers_for(name)
            if enc:
                self.send_header("Content-Encoding", enc)
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


def cmd_serve(args):
    if not os.path.isfile(os.path.join(args.build_dir, "index.html")):
        raise SystemExit(f"{args.build_dir}: no index.html - point --build-dir at the WebGL output folder.")
    find_build_files(args.build_dir)  # the same refusals as an upload: one of each, no .unityweb
    handler = functools.partial(_ToyboxHandler, directory=args.build_dir)
    server = http.server.ThreadingHTTPServer(("127.0.0.1", args.port), handler)
    print(f"Serving {args.build_dir} at http://localhost:{args.port}/  (Ctrl+C to stop)")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    return 0


_USER_AGENT = "Mozilla/5.0 (publish_toybox.py check)"


def _head(url, origin):
    """Status and lower-cased headers of a request the way a browser on `origin` makes it."""
    # HEAD, not a ranged GET: R2 leaves Content-Encoding off a 206 partial response, so a ranged
    # probe reports a missing encoding that a browser's full download does carry.
    req = urllib.request.Request(url, method="HEAD", headers={
        "Origin": origin,
        "Accept-Encoding": "gzip, deflate, br",
        "User-Agent": _USER_AGENT,
    })
    try:
        with _urlopen(req, timeout=30) as r:
            return r.status, {k.lower(): v for k, v in r.headers.items()}, r.read()
    except urllib.error.HTTPError as e:
        return e.code, {k.lower(): v for k, v in e.headers.items()}, b""


def cmd_check(args):
    root = args.root.rstrip("/")
    problems = []

    def report(ok, what, detail=""):
        print(("  ok    " if ok else "  FAIL  ") + what + (f"  ({detail})" if detail else ""))
        if not ok:
            problems.append(what)

    print(f"{root}/latest.json")
    # Cloudflare refuses Python's default User-Agent (HTTP 403), so every request here names itself.
    req = urllib.request.Request(f"{root}/latest.json", headers={
        "Origin": args.origin, "Cache-Control": "no-cache", "User-Agent": _USER_AGENT})
    try:
        with _urlopen(req, timeout=30) as r:
            manifest = json.loads(r.read().decode())
            acao = r.headers.get("Access-Control-Allow-Origin")
    except Exception as e:  # noqa: BLE001 - every failure here means the same thing to the reader
        raise SystemExit(f"  FAIL  cannot read latest.json: {http_reason(e)}") from None
    report(acao in (args.origin, "*"), "CORS allows " + args.origin, f"Access-Control-Allow-Origin: {acao}")
    for key in ("loader", "framework", "code", "data"):
        if key not in manifest:
            report(False, f"latest.json names a {key}")
    print(f"  build {manifest.get('version')}, {manifest.get('bytes', 0) / 1048576:.0f} MB, "
          f"commit {(manifest.get('commit') or 'not recorded')[:10]}")
    if args.expect_version:
        report(manifest.get("version") == args.expect_version, f"serves version {args.expect_version}",
               f"serves {manifest.get('version')}")

    for key in ("loader", "framework", "code", "data"):
        if key not in manifest:
            continue
        url = f"{root}/{manifest[key]}"
        status, h, _ = _head(url, args.origin)
        print(f"{key}: {manifest[key]}")
        report(status in (200, 206), "reachable", f"HTTP {status}")
        name = manifest[key].rsplit("/", 1)[-1]
        want_type, want_enc = headers_for(name)
        got_enc = h.get("content-encoding")
        if want_enc:
            report(got_enc == want_enc, f"Content-Encoding: {want_enc}", f"got {got_enc or 'none'}")
        if key == "code":
            got_type = (h.get("content-type") or "").split(";")[0]
            report(got_type == "application/wasm", "Content-Type: application/wasm", f"got {got_type or 'none'}")
        if key != "loader":   # the loader is a <script>; the rest are fetched, so they need CORS
            got = h.get("access-control-allow-origin")
            report(got in (args.origin, "*"), "CORS", f"Access-Control-Allow-Origin: {got}")

    if problems:
        print(f"\n{len(problems)} problem(s): /play will not load this build until they are fixed "
              "(Docs/WEBGL_TOYBOX.md section 5).")
        return 1
    print("\nAll good: /play can load this build.")
    return 0


# ──────────────────────────────────────────────
#  self-test (offline: a fake R2 and a fake UBA)
# ──────────────────────────────────────────────

class _FakeS3Error(Exception):
    def __init__(self, code, status):
        super().__init__(f"An error occurred ({code})")
        self.response = {"Error": {"Code": code}, "ResponseMetadata": {"HTTPStatusCode": status}}


class _FakeS3:
    """The S3 calls R2 makes, in memory: objects are (bytes, headers)."""

    def __init__(self):
        self.objects = {}

    def _get(self, Key):
        if Key not in self.objects:
            raise _FakeS3Error("NoSuchKey" if True else "", 404)
        return self.objects[Key]

    def put_object(self, Bucket, Key, Body, ContentType, CacheControl, ContentEncoding=None):
        self.objects[Key] = (bytes(Body), {"ContentType": ContentType, "CacheControl": CacheControl,
                                           "ContentEncoding": ContentEncoding})

    def upload_file(self, Filename, Bucket, Key, ExtraArgs):
        with open(Filename, "rb") as fh:
            self.objects[Key] = (fh.read(), dict(ExtraArgs))

    def head_object(self, Bucket, Key):
        body, h = self._get(Key)
        return {"ContentLength": len(body), **{k: v for k, v in h.items() if v}}

    def get_object(self, Bucket, Key):
        return {"Body": io.BytesIO(self._get(Key)[0])}

    def copy_object(self, Bucket, Key, CopySource, MetadataDirective, ContentType, CacheControl,
                    ContentEncoding=None):
        body, _ = self._get(CopySource["Key"])
        self.objects[Key] = (body, {"ContentType": ContentType, "CacheControl": CacheControl,
                                    "ContentEncoding": ContentEncoding})

    def list_objects_v2(self, Bucket, Prefix, Delimiter=None, ContinuationToken=None):
        keys = sorted(k for k in self.objects if k.startswith(Prefix))
        if not Delimiter:
            return {"Contents": [{"Key": k, "Size": len(self.objects[k][0])} for k in keys], "IsTruncated": False}
        prefixes = sorted({Prefix + k[len(Prefix):].split(Delimiter, 1)[0] + Delimiter
                           for k in keys if Delimiter in k[len(Prefix):]})
        return {"CommonPrefixes": [{"Prefix": p} for p in prefixes], "IsTruncated": False}


def _fake_build(root, tag, with_txt_commit=None):
    os.makedirs(os.path.join(root, "Build"))
    os.makedirs(os.path.join(root, "StreamingAssets"))
    for name, body in ((f"{tag}.loader.js", b"loader"), (f"{tag}.framework.js.br", b"fw" + tag.encode()),
                       (f"{tag}.wasm.br", b"wasm" * 50 + tag.encode()), (f"{tag}.data.br", b"data" + tag.encode())):
        with open(os.path.join(root, "Build", name), "wb") as fh:
            fh.write(body)
    with open(os.path.join(root, "StreamingAssets", "Master.bank"), "wb") as fh:
        fh.write(b"bank" + tag.encode())
    if with_txt_commit:
        with open(os.path.join(root, BUILD_MANIFEST_TXT), "w") as fh:
            fh.write(f"version=0.1\ncommit={with_txt_commit}\nbuiltAtUtc=2026-11-05T10:00:00Z\n")
    return root


class _FakeResponse(io.BytesIO):
    def __enter__(self):
        return self

    def __exit__(self, *a):
        self.close()


def self_test():
    global _urlopen, _sleep
    checks = []

    def ok(name, cond):
        checks.append((name, bool(cond)))
        if not cond:
            print("FAIL", name)

    def refuses(name, fn, must_say=None):
        try:
            fn()
        except SystemExit as e:
            msg = str(e.code if e.code is not None else "")
            ok(name, must_say is None or must_say in msg)
            return msg
        ok(name + " (did not refuse)", False)
        return ""

    s3 = _FakeS3()
    r2 = R2(s3, "fake-bucket")
    stage, prod = ROOTS["staging"], ROOTS["production"]
    sha1, sha2 = "1" * 40, "2" * 40
    quiet = io.StringIO()
    with tempfile.TemporaryDirectory() as tmp, contextlib.redirect_stdout(quiet):
        b1 = _fake_build(os.path.join(tmp, "b1"), "aaa", with_txt_commit=sha1)
        b2 = _fake_build(os.path.join(tmp, "b2"), "bbb")

        # upload -> staging, with a manifest and a commit in latest.json
        stage_build(r2, b1, stage, "11-1111111", sha1, "2026-11-05T10:00:00Z", uba_build=11, tag="web-v0.1.0")
        m1 = r2.get_json(f"{stage}/builds/11-1111111/manifest.json")
        latest = r2.get_json(f"{stage}/latest.json")
        ok("staged manifest lists every file with sha256",
           m1 and len(m1["files"]) == 6 and all(len(f["sha256"]) == 64 for f in m1["files"]))
        ok("staged manifest uploads build_manifest.txt", any(f["path"] == BUILD_MANIFEST_TXT for f in m1["files"]))
        ok("staging latest.json carries commit, built_at, manifest",
           latest["commit"] == sha1 and latest["built_at"] and latest["manifest"].endswith("manifest.json"))
        ok("wasm is stored with its headers",
           s3.objects[f"{stage}/{latest['code']}"][1]["ContentEncoding"] == "br"
           and s3.objects[f"{stage}/{latest['code']}"][1]["ContentType"] == "application/wasm")
        ok("nothing reached production by staging", not any(k.startswith(prod + "/") for k in s3.objects))

        # negative: an immutable version cannot be overwritten with different bytes
        refuses("upload over an immutable build refuses",
                lambda: stage_build(r2, b2, stage, "11-1111111", sha2, "x"), "immutable")

        # negative: promote of an unknown version
        refuses("promote of an unknown version refuses", lambda: promote(r2, "99-9999999"), "not staged")
        ok("refused promote left production empty", r2.get_json(f"{prod}/latest.json") is None)

        # promote 1, then stage + promote 2
        promote(r2, "11-1111111")
        live = r2.get_json(f"{prod}/latest.json")
        ok("promote points production at the staged build", live["version"] == "11-1111111"
           and live["previous"] is None and live["code"] == latest["code"])
        ok("promote copied every file with its headers", not verify_files(r2, prod, m1))
        stage_build(r2, b2, stage, "12-2222222", sha2, "2026-11-05T11:00:00Z", uba_build=12)
        refuses("promote of a staged version the staging page no longer serves refuses",
                lambda: promote(r2, "11-1111111"), "staging page serves")
        promote(r2, "12-2222222")
        live = r2.get_json(f"{prod}/latest.json")
        ok("second promote records previous", live["version"] == "12-2222222" and live["previous"] == "11-1111111")

        # rollback to previous, then to a named version
        rollback(r2)
        live = r2.get_json(f"{prod}/latest.json")
        ok("rollback returns to previous", live["version"] == "11-1111111" and live["previous"] == "12-2222222"
           and live["code"] == m1["pointer"]["code"])
        rollback(r2, to="12-2222222")
        ok("rollback --to names a version", r2.get_json(f"{prod}/latest.json")["version"] == "12-2222222")
        refuses("rollback to the live version refuses", lambda: rollback(r2, to="12-2222222"), "already live")

        # negative: a version with a missing file
        del s3.objects[f"{prod}/builds/11-1111111/StreamingAssets/Master.bank"]
        refuses("rollback to a version with a missing file refuses", lambda: rollback(r2, to="11-1111111"),
                "missing: StreamingAssets/Master.bank")
        ok("refused rollback left production unchanged",
           r2.get_json(f"{prod}/latest.json")["version"] == "12-2222222")

        # a hand-uploaded build without a manifest: refused, then adopted
        for key in [k for k in s3.objects if k.startswith(f"{stage}/builds/11-1111111/Build/")]:
            s3.objects[key.replace(f"{stage}/builds/11-1111111/", f"{prod}/builds/2/")] = s3.objects[key]
        refuses("rollback to a build with no manifest refuses", lambda: rollback(r2, to="2"), "adopt")
        adopt(r2, prod, "2")
        rollback(r2, to="2")
        ok("an adopted build is a rollback target", r2.get_json(f"{prod}/latest.json")["version"] == "2")
        listing = list_versions(r2, ["staging", "production"])
        ok("list shows versions, commits and the live one",
           "LIVE  2 " in listing and "12-2222222" in listing and sha2[:10] in listing)

        # identity: a --commit that contradicts build_manifest.txt refuses
        class A:
            commit, built_at = sha2, ""
        refuses("a commit that contradicts build_manifest.txt refuses", lambda: _resolve_identity(A, b1),
                "build_manifest.txt says")

    # UBA: pick by commit, never the newest; and redact everything
    os.environ.setdefault("UBA_API_KEY", "fake-uba-key-0123456789")
    for k in UBA_ENV[1:]:
        os.environ.setdefault(k, "fake")
    signed = "https://storage.example.com/dl/toybox.zip?X-Amz-Signature=deadbeefcafe&X-Amz-Credential=AKIAFAKE"
    builds = [{"build": 9, "lastBuiltRevision": "c" * 40, "links": {"download_primary": {"href": signed}}},
              {"build": 7, "lastBuiltRevision": "a" * 40, "links": {"download_primary": {"href": signed}}}]
    ok("UBA pick takes the build of the commit, not the newest", uba_pick(builds, commit="aaaaaaa")["build"] == 7)
    ok("UBA pick of an unknown commit finds none", uba_pick(builds, commit="bbbbbbb") is None)

    def fake_urlopen(req, timeout=0):
        url = req.full_url if hasattr(req, "full_url") else str(req)
        if "?X-Amz" in url:   # the signed download: fail the way S3 does, body and all
            raise urllib.error.HTTPError(url, 403, "Forbidden", {},
                                         io.BytesIO(b"<Error>SignatureDoesNotMatch " + url.encode() + b"</Error>"))
        if "buildStatus=success" in url:
            return _FakeResponse(json.dumps([{"build": 5, "lastBuiltRevision": "e" * 40,
                                              "links": {"weird": {"href": signed}}, "secretish": signed}]).encode())
        raise urllib.error.HTTPError(url, 500, "Server Error", {}, io.BytesIO(signed.encode()))

    saved = (_urlopen, _sleep)
    _urlopen, _sleep = fake_urlopen, (lambda s: None)
    try:
        out_buf, err_buf = io.StringIO(), io.StringIO()
        with tempfile.TemporaryDirectory() as tmp, contextlib.redirect_stdout(out_buf), \
                contextlib.redirect_stderr(err_buf):
            msgs = [
                refuses("fetch-uba of a commit UBA never built refuses",
                        lambda: fetch_uba("bbbbbbb", None, os.path.join(tmp, "z")), "no successful build of commit"),
                refuses("fetch-uba with an unexpected response shape refuses",
                        lambda: fetch_uba("eeeeeee", None, os.path.join(tmp, "z")), "no primary download"),
                refuses("a failed signed download refuses", lambda: _download(signed, os.path.join(tmp, "z")),
                        "HTTP 403"),
                refuses("a failed start-build refuses",
                        lambda: fetch_uba("bbbbbbb", None, os.path.join(tmp, "z"), start_if_missing=True), "HTTP 500"),
                redact("boom " + signed + " key fake-uba-key-0123456789"),
                s3_reason(_FakeS3Error("AccessDenied", 403)),
            ]
        everything = "\n".join(msgs) + out_buf.getvalue() + err_buf.getvalue()
        ok("no signed URL, signature, credential or response body in any message",
           "X-Amz" not in everything and "deadbeef" not in everything and "AKIAFAKE" not in everything
           and "SignatureDoesNotMatch" not in everything and "fake-uba-key" not in everything
           and "secretish" not in everything)
        ok("redact keeps the host and path", "https://storage.example.com/dl/toybox.zip?<redacted>" in msgs[4])
        ok("an S3 error is a code and a status", msgs[5] == "AccessDenied (HTTP 403)")
    finally:
        _urlopen, _sleep = saved

    failed = [n for n, good in checks if not good]
    if failed:
        print(f"SELF-TEST FAILED: {len(failed)} of {len(checks)} checks: " + "; ".join(failed))
        return 1
    print(f"self-test OK: {len(checks)} checks (upload -> promote -> rollback, adopt, list; refusals: unknown "
          "version, stale staging, immutable overwrite, missing file, missing manifest, wrong commit; "
          "redaction of signed URLs, bodies and keys)")
    return 0


def cmd_self_test(_args):
    return self_test()


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    u = sub.add_parser("upload")
    u.add_argument("--build-dir", required=True)
    u.add_argument("--version", required=True, help="folder name under builds/, e.g. <UBA build>-<sha7>")
    u.add_argument("--root", choices=sorted(ROOTS), required=True,
                   help="staging (the pipeline) or production (a hand upload straight to /play)")
    u.add_argument("--prefix", help="an explicit object prefix instead of --root's (testing)")
    u.add_argument("--commit", help="the commit the build came from (default: its build_manifest.txt)")
    u.add_argument("--built-at", help="when it was built, ISO-8601 (default: build_manifest.txt, else file times)")
    u.add_argument("--uba-build", type=int)
    u.add_argument("--tag")
    u.add_argument("--result-out", help="write what was published as JSON (for the workflow's status record)")
    u.add_argument("--dry-run", action="store_true")
    p = sub.add_parser("promote")
    p.add_argument("--from-staging", required=True, metavar="VERSION")
    p.add_argument("--result-out")
    r = sub.add_parser("rollback")
    r.add_argument("--to", metavar="VERSION", help="default: the `previous` production latest.json records")
    r.add_argument("--result-out")
    ls = sub.add_parser("list")
    ls.add_argument("--root", choices=sorted(ROOTS) + ["both"], default="both")
    a = sub.add_parser("adopt")
    a.add_argument("--version", required=True)
    a.add_argument("--root", choices=sorted(ROOTS), default="production")
    a.add_argument("--commit", default="")
    a.add_argument("--built-at", default="")
    f = sub.add_parser("fetch-uba")
    f.add_argument("--out", required=True)
    f.add_argument("--commit", help="the commit whose build to fetch")
    f.add_argument("--build", type=int, help="a UBA build number instead of a commit")
    f.add_argument("--start-if-missing", action="store_true", help="ask UBA to build --commit if it has not")
    f.add_argument("--wait-minutes", type=int, default=120)
    f.add_argument("--meta-out")
    s = sub.add_parser("serve")
    s.add_argument("--build-dir", required=True)
    s.add_argument("--port", type=int, default=8000)
    c = sub.add_parser("check")
    c.add_argument("--root", required=True, help="the build_root /play uses, e.g. https://play.froglet.games/toybox")
    c.add_argument("--origin", default="https://www.froglet.games")
    c.add_argument("--expect-version", help="fail unless latest.json serves this version")
    sub.add_parser("self-test")
    args = ap.parse_args()
    handlers = {"upload": cmd_upload, "promote": cmd_promote, "rollback": cmd_rollback, "list": cmd_list,
                "adopt": cmd_adopt, "fetch-uba": cmd_fetch_uba, "serve": cmd_serve, "check": cmd_check,
                "self-test": cmd_self_test}
    try:
        return handlers[args.cmd](args)
    except SystemExit:
        raise
    except Exception as e:  # noqa: BLE001 - an unexpected error is still reported without its secrets
        print(f"error: {type(e).__name__}: {redact(e)}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
