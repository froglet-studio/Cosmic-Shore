#!/usr/bin/env python3
"""Publish a browser Toybox WebGL build to Cloudflare R2 (Docs/WEBGL_TOYBOX.md section 5).

Two steps, usable together (the toybox-publish workflow) or alone:

  fetch-uba   Download the latest successful build of the Unity Build Automation WebGL Toybox
              target as a zip.
                  python3 Tools/Build/publish_toybox.py fetch-uba --out toybox.zip
              Needs UBA_API_KEY, UBA_ORG_ID, UBA_PROJECT_ID, UBA_TOYBOX_TARGET in the environment.
              Prints the build number and commit; writes them to --meta-out as JSON if given.

  upload      Upload a built WebGL folder (the one holding Build/) to R2 under
              <prefix>/builds/<version>/, then write <prefix>/latest.json, which /play reads.
                  python3 Tools/Build/publish_toybox.py upload --build-dir Builds/WebGLToybox --version 42
              Needs R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET (and boto3).
              --dry-run prints every object with its headers and the manifest, needs nothing,
              and uploads nothing.

  serve       Serve a built WebGL folder on http://localhost with the same headers, to play a
              local build before uploading it (the build's own index.html, the FrogletToybox template).
                  python3 Tools/Build/publish_toybox.py serve --build-dir Builds/WebGLToybox
              Browsers decode Brotli on localhost even without HTTPS. Needs nothing installed.

  check       Read a published root the way www.froglet.games/play will, and say whether it will load:
              latest.json, then each build file's Content-Encoding, Content-Type and CORS header.
                  python3 Tools/Build/publish_toybox.py check --root https://play.froglet.games/toybox
              Exit 0 = /play can load it. Run it after every first upload to a new host.

Why the headers matter: the build is compressed (Brotli or gzip) with Unity's decompression
fallback off, so the browser can only read it if each file arrives with Content-Encoding set,
the right Content-Type (application/wasm for the code, or the browser refuses to stream-compile
it), and no re-compression on the way (Cache-Control: no-transform). Hashed build files never
change, so they are cached for a year; latest.json is never cached, so a new build is live as
soon as it is written. latest.json goes up LAST, so a page never points at files that are still
uploading, and a rollback is rewriting latest.json to an older version folder.
"""
import argparse
import functools
import http.server
import json
import mimetypes
import os
import sys
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
    ".bank": "application/octet-stream",   # FMOD banks in StreamingAssets
    ".symbols.json": "application/json",
}
ENCODINGS = {".br": "br", ".gz": "gzip"}
IMMUTABLE = "public, max-age=31536000, immutable, no-transform"
NO_CACHE = "no-cache, no-transform"

UBA_API = "https://build-api.cloud.unity3d.com/api/v1"


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


def find_build_files(build_dir):
    """The four files createUnityInstance needs, as paths relative to build_dir."""
    bdir = os.path.join(build_dir, "Build")
    if not os.path.isdir(bdir):
        raise SystemExit(f"{build_dir}: no Build/ folder - point --build-dir at the WebGL output folder.")
    names = sorted(os.listdir(bdir))

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


def plan_upload(build_dir, prefix, version):
    """Every object to write, in order, plus the manifest. latest.json is last."""
    files = find_build_files(build_dir)
    version_root = f"builds/{version}"
    objects, total = [], 0
    for sub in ("Build", "StreamingAssets"):
        root = os.path.join(build_dir, sub)
        if not os.path.isdir(root):
            continue
        for dirpath, _, filenames in os.walk(root):
            for fn in sorted(filenames):
                path = os.path.join(dirpath, fn)
                rel = os.path.relpath(path, build_dir).replace(os.sep, "/")
                ctype, enc = headers_for(fn)
                size = os.path.getsize(path)
                total += size
                objects.append({
                    "path": path,
                    "key": f"{prefix}/{version_root}/{rel}",
                    "ContentType": ctype,
                    "ContentEncoding": enc,
                    "CacheControl": IMMUTABLE,
                    "bytes": size,
                })
    manifest = {
        "version": str(version),
        "loader": f"{version_root}/{files['loader']}",
        "framework": f"{version_root}/{files['framework']}",
        "code": f"{version_root}/{files['code']}",
        "data": f"{version_root}/{files['data']}",
        "streamingAssets": f"{version_root}/StreamingAssets",
        "bytes": total,
    }
    return objects, manifest


def cmd_upload(args):
    objects, manifest = plan_upload(args.build_dir, args.prefix.strip("/"), args.version)
    latest_key = f"{args.prefix.strip('/')}/latest.json"
    if args.dry_run:
        for o in objects:
            print(f"{o['key']}  type={o['ContentType']}  encoding={o['ContentEncoding']}  "
                  f"cache={o['CacheControl']}  {o['bytes']} B")
        print(f"{latest_key}  type=application/json  cache={NO_CACHE}  (last)")
        print(json.dumps(manifest, indent=2))
        return 0

    try:
        import boto3  # noqa: PLC0415 - only the real upload needs it
    except ImportError:
        raise SystemExit("boto3 is required for a real upload: pip install boto3")
    env = {k: os.environ.get(k) for k in ("R2_ACCOUNT_ID", "R2_ACCESS_KEY_ID", "R2_SECRET_ACCESS_KEY", "R2_BUCKET")}
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
    for o in objects:
        extra = {"ContentType": o["ContentType"], "CacheControl": o["CacheControl"]}
        if o["ContentEncoding"]:
            extra["ContentEncoding"] = o["ContentEncoding"]
        s3.upload_file(o["path"], env["R2_BUCKET"], o["key"], ExtraArgs=extra)
        print("uploaded", o["key"])
    s3.put_object(Bucket=env["R2_BUCKET"], Key=latest_key, Body=json.dumps(manifest).encode(),
                  ContentType="application/json", CacheControl=NO_CACHE)
    print("published", latest_key, "->", manifest["version"], f"({manifest['bytes'] / 1048576:.0f} MB)")
    return 0


def uba_get(url, key):
    req = urllib.request.Request(url, headers={
        # UBA's v1 API takes the API key itself as the Basic credential.
        "Authorization": "Basic " + key,
        "Content-Type": "application/json",
    })
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())


def cmd_fetch_uba(args):
    env = {k: os.environ.get(k) for k in ("UBA_API_KEY", "UBA_ORG_ID", "UBA_PROJECT_ID", "UBA_TOYBOX_TARGET")}
    missing = [k for k, v in env.items() if not v]
    if missing:
        raise SystemExit("missing environment: " + ", ".join(missing))
    url = (f"{UBA_API}/orgs/{env['UBA_ORG_ID']}/projects/{env['UBA_PROJECT_ID']}"
           f"/buildtargets/{env['UBA_TOYBOX_TARGET']}/builds?buildStatus=success&per_page=1")
    builds = uba_get(url, env["UBA_API_KEY"])
    if not builds:
        raise SystemExit("no successful build of the WebGL Toybox target yet")
    b = builds[0]
    try:
        href = b["links"]["download_primary"]["href"]
    except (KeyError, TypeError):
        raise SystemExit("unexpected UBA response (no links.download_primary.href):\n"
                         + json.dumps(b, indent=2)[:4000])
    meta = {"build": b.get("build"), "commit": b.get("lastBuiltRevision") or "", "finished": b.get("finished")}
    print(f"UBA build #{meta['build']} at {meta['commit'][:10]} - downloading")
    urllib.request.urlretrieve(href, args.out)
    if args.meta_out:
        with open(args.meta_out, "w") as f:
            json.dump(meta, f)
    print("saved", args.out)
    return 0


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
        with urllib.request.urlopen(req, timeout=30) as r:
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
        with urllib.request.urlopen(req, timeout=30) as r:
            manifest = json.loads(r.read().decode())
            acao = r.headers.get("Access-Control-Allow-Origin")
    except Exception as e:  # noqa: BLE001 - every failure here means the same thing to the reader
        raise SystemExit(f"  FAIL  cannot read latest.json: {e}")
    report(acao in (args.origin, "*"), "CORS allows " + args.origin, f"Access-Control-Allow-Origin: {acao}")
    for key in ("loader", "framework", "code", "data"):
        if key not in manifest:
            report(False, f"latest.json names a {key}")
    print(f"  build {manifest.get('version')}, {manifest.get('bytes', 0) / 1048576:.0f} MB")

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


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    u = sub.add_parser("upload")
    u.add_argument("--build-dir", required=True)
    u.add_argument("--version", required=True, help="folder name under builds/, e.g. the UBA build number")
    u.add_argument("--prefix", default="toybox")
    u.add_argument("--dry-run", action="store_true")
    f = sub.add_parser("fetch-uba")
    f.add_argument("--out", required=True)
    f.add_argument("--meta-out")
    s = sub.add_parser("serve")
    s.add_argument("--build-dir", required=True)
    s.add_argument("--port", type=int, default=8000)
    c = sub.add_parser("check")
    c.add_argument("--root", required=True, help="the build_root /play uses, e.g. https://play.froglet.games/toybox")
    c.add_argument("--origin", default="https://www.froglet.games")
    args = ap.parse_args()
    return {"upload": cmd_upload, "fetch-uba": cmd_fetch_uba, "serve": cmd_serve, "check": cmd_check}[args.cmd](args)


if __name__ == "__main__":
    sys.exit(main())
