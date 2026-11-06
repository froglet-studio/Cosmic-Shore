#!/usr/bin/env python3
"""Google Play from GitHub Actions: upload an App Bundle to the INTERNAL track, promote it, stage it, halt it.

Uses the Google Play Developer API (androidpublisher v3) with a service account. Stdlib plus the `openssl` CLI
(which every GitHub-hosted runner ships) to sign the service account's token request, so no Google client
library is installed into a job that holds the key.

    python3 Tools/Launch/play.py upload  --aab app.aab [--release-name "0.3.0 (rc-1)"]       # internal track only
    python3 Tools/Launch/play.py promote --version-code 412826 --to-track alpha               # a closed track
    python3 Tools/Launch/play.py promote --version-code 412826 --to-track production --user-fraction 0.1
    python3 Tools/Launch/play.py rollout --track production --user-fraction 0.5               # widen a staged rollout
    python3 Tools/Launch/play.py halt    --track production                                    # stop a staged rollout
    python3 Tools/Launch/play.py resume  --track production
    python3 Tools/Launch/play.py status  --track production
    python3 Tools/Launch/play.py self-test

Environment:
    PLAY_SERVICE_ACCOUNT_JSON  the service account's JSON key (a secret of play-prerelease / android-production)
    PLAY_PACKAGE               the application id (default com.FrogletGames.CosmicShore)

Rules this tool keeps, whatever it is asked:
    `upload` goes to the internal track and nowhere else.
    `promote` never targets internal, and production needs --user-fraction (a staged rollout); 1.0 is a full
    release and is refused unless --allow-full is passed.
    A version code is promoted only if it is on the internal track already (built and tested by this pipeline).
    Every change is one edit, committed at the end or deleted on any failure, so a half-done change never lands.

Nothing secret is printed: the key never leaves this process except as a signed token request to Google, and an
HTTP failure prints its status code and the API path, never the response body. Release rollback on Play: a
completed release cannot be withdrawn; halt the staged rollout, then ship a NEW build of the earlier tag (its
version code is higher, Docs/ANDROID_BUILD.md) - Docs/LAUNCH/PIPELINES.md has the steps.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

API = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications"
UPLOAD_API = "https://androidpublisher.googleapis.com/upload/androidpublisher/v3/applications"
SCOPE = "https://www.googleapis.com/auth/androidpublisher"
DEFAULT_PACKAGE = "com.FrogletGames.CosmicShore"
PKG_RE = re.compile(r"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$")
TRACK_RE = re.compile(r"^[a-z][a-z0-9:_-]{0,63}$")


class PlayError(Exception):
    def __init__(self, message: str, code: int = 2):
        super().__init__(message)
        self.message = message
        self.code = code


class Transport:
    def request(self, method: str, url: str, headers: dict, body: bytes | None) -> tuple[int, bytes]:
        req = urllib.request.Request(url, data=body, method=method, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=600) as r:
                return r.status, r.read()
        except urllib.error.HTTPError as e:
            try:
                # Keep only Google's machine-readable reason (e.g. "apkUpgradeVersionConflict"), never the text.
                reason = json.loads(e.read().decode())["error"]["errors"][0]["reason"]
            except Exception:
                reason = ""
            return e.code, json.dumps({"reason": reason if re.match(r"^[A-Za-z]{1,60}$", reason) else ""}).encode()


def openssl_sign(pem: str, data: bytes) -> bytes:
    """RS256 with the openssl CLI; the key goes through a 0600 temp file that is removed at once."""
    fd, path = tempfile.mkstemp(prefix="play-key-", suffix=".pem")
    try:
        with os.fdopen(fd, "w") as fh:
            fh.write(pem)
        proc = subprocess.run(["openssl", "dgst", "-sha256", "-sign", path], input=data, capture_output=True)
        if proc.returncode != 0:
            raise PlayError("openssl could not sign with the service account key (is it the JSON key's "
                            "private_key?)")
        return proc.stdout
    finally:
        os.unlink(path)


def _b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


class Play:
    def __init__(self, key_json: str, package: str, transport: Transport | None = None, signer=openssl_sign,
                 clock=time.time):
        if not key_json:
            raise PlayError("missing PLAY_SERVICE_ACCOUNT_JSON (a secret of the play-prerelease or "
                            "android-production Environment; Docs/LAUNCH/PIPELINES.md)")
        try:
            key = json.loads(key_json)
            self.email, self.pem = key["client_email"], key["private_key"]
            self.token_uri = key.get("token_uri", "https://oauth2.googleapis.com/token")
        except (ValueError, KeyError, TypeError):
            raise PlayError("PLAY_SERVICE_ACCOUNT_JSON is not a service account JSON key "
                            "(client_email and private_key are required)")
        if not self.token_uri.startswith("https://oauth2.googleapis.com/"):
            raise PlayError("the service account's token_uri is not Google's; refusing to send a signed request "
                            "anywhere else")
        if not PKG_RE.match(package):
            raise PlayError("PLAY_PACKAGE is not an application id")
        self.package = package
        self.http = transport or Transport()
        self.signer = signer
        self.clock = clock
        self._token = ""

    def token(self) -> str:
        if self._token:
            return self._token
        now = int(self.clock())
        header = _b64url(json.dumps({"alg": "RS256", "typ": "JWT"}).encode())
        claims = _b64url(json.dumps({"iss": self.email, "scope": SCOPE, "aud": self.token_uri,
                                     "iat": now, "exp": now + 3600}).encode())
        signing_input = f"{header}.{claims}".encode()
        jwt = f"{header}.{claims}.{_b64url(self.signer(self.pem, signing_input))}"
        body = urllib.parse.urlencode({"grant_type": "urn:ietf:params:oauth:grant-type:jwt-bearer",
                                       "assertion": jwt}).encode()
        status, raw = self.http.request("POST", self.token_uri,
                                        {"Content-Type": "application/x-www-form-urlencoded"}, body)
        if status != 200:
            raise PlayError(f"Google refused the service account (HTTP {status}); check the key and that the "
                            f"account is invited in Play Console with release rights")
        try:
            self._token = json.loads(raw)["access_token"]
        except (ValueError, KeyError):
            raise PlayError("Google's token answer holds no access_token")
        return self._token

    def _call(self, method: str, url: str, payload=None, raw_body: bytes | None = None,
              content_type: str = "application/json"):
        headers = {"Authorization": f"Bearer {self.token()}", "Content-Type": content_type}
        body = raw_body if raw_body is not None else (json.dumps(payload).encode() if payload is not None else None)
        status, raw = self.http.request(method, url, headers, body)
        path = urllib.parse.urlsplit(url).path
        if status >= 300:
            try:
                reason = json.loads(raw).get("reason", "")
            except ValueError:
                reason = ""
            raise PlayError(f"Play answered HTTP {status} for {method} {path}" + (f" ({reason})" if reason else ""))
        return json.loads(raw.decode() or "{}") if raw else {}

    # ── edits ──

    def _edit(self, work):
        """Run `work(edit_id)` inside one edit; commit it, or delete it on any failure."""
        base = f"{API}/{self.package}/edits"
        edit = self._call("POST", base, {})
        edit_id = edit.get("id")
        if not isinstance(edit_id, str) or not re.match(r"^[A-Za-z0-9_-]{1,128}$", edit_id):
            raise PlayError("Play's answer to opening an edit holds no edit id")
        try:
            result = work(edit_id)
            self._call("POST", f"{base}/{edit_id}:commit")
            return result
        except BaseException:
            try:
                self._call("DELETE", f"{base}/{edit_id}")
            except PlayError:
                pass
            raise

    def _track(self, edit_id: str, track: str) -> dict:
        return self._call("GET", f"{API}/{self.package}/edits/{edit_id}/tracks/{track}")

    def _put_track(self, edit_id: str, track: str, releases: list) -> dict:
        return self._call("PUT", f"{API}/{self.package}/edits/{edit_id}/tracks/{track}",
                          {"track": track, "releases": releases})

    # ── commands ──

    def upload(self, aab: Path, release_name: str = "", track: str = "internal") -> int:
        if track != "internal":
            raise PlayError(f"upload goes to the internal track only (asked for '{track}'); promotion is "
                            f"release-promote.yml's job, behind the android-production Environment")
        if not aab.is_file() or aab.suffix != ".aab":
            raise PlayError(f"{aab.name} is not an .aab file")

        def work(edit_id):
            res = self._call("POST", f"{UPLOAD_API}/{self.package}/edits/{edit_id}/bundles?uploadType=media",
                             raw_body=aab.read_bytes(), content_type="application/octet-stream")
            code = res.get("versionCode")
            if not isinstance(code, int):
                raise PlayError("Play's answer to the upload holds no versionCode")
            release = {"versionCodes": [str(code)], "status": "completed"}
            if release_name:
                release["name"] = release_name[:50]
            self._put_track(edit_id, "internal", [release])
            return code
        return self._edit(work)

    def promote(self, version_code: int, to_track: str, user_fraction: float | None, allow_full: bool = False,
                release_name: str = "") -> dict:
        if to_track == "internal":
            raise PlayError("promote never targets the internal track; upload does")
        if not TRACK_RE.match(to_track):
            raise PlayError(f"'{to_track[:40]}' is not a Play track name")
        if to_track == "production" and user_fraction is None:
            raise PlayError("production needs --user-fraction (a staged rollout, e.g. 0.05)")
        if user_fraction is not None and not (0 < user_fraction <= 1):
            raise PlayError("--user-fraction is a share of users between 0 and 1, e.g. 0.05")
        if user_fraction == 1 and to_track == "production" and not allow_full:
            raise PlayError("a 1.0 production rollout is a full release; pass --allow-full to mean it")

        def work(edit_id):
            internal = self._track(edit_id, "internal")
            codes = {c for r in internal.get("releases", []) for c in r.get("versionCodes", [])}
            if str(version_code) not in codes:
                raise PlayError(f"version code {version_code} is not on the internal track; only a build this "
                                f"pipeline uploaded and the team tested is promoted", 3)
            release = {"versionCodes": [str(version_code)]}
            if user_fraction is None or user_fraction == 1:
                release["status"] = "completed"
            else:
                release["status"] = "inProgress"
                release["userFraction"] = user_fraction
            if release_name:
                release["name"] = release_name[:50]
            self._put_track(edit_id, to_track, [release])
            return release
        return self._edit(work)

    def _change_live(self, track: str, change) -> dict:
        def work(edit_id):
            current = self._track(edit_id, track)
            releases = current.get("releases") or []
            staged = [r for r in releases if r.get("status") in ("inProgress", "halted")]
            if not staged:
                raise PlayError(f"the {track} track has no staged rollout to change (a completed release cannot be "
                                f"halted or narrowed on Play)")
            target = staged[0]
            change(target)
            self._put_track(edit_id, track, [target])
            return target
        return self._edit(work)

    def rollout(self, track: str, user_fraction: float, allow_full: bool = False) -> dict:
        if not (0 < user_fraction <= 1):
            raise PlayError("--user-fraction is a share of users between 0 and 1")
        if user_fraction == 1 and track == "production" and not allow_full:
            raise PlayError("a 1.0 production rollout is a full release; pass --allow-full to mean it")

        def change(r):
            if r.get("status") != "inProgress":
                raise PlayError("the staged rollout is halted; resume it first")
            if user_fraction == 1:
                r["status"] = "completed"
                r.pop("userFraction", None)
            else:
                r["userFraction"] = user_fraction
        return self._change_live(track, change)

    def halt(self, track: str) -> dict:
        def change(r):
            r["status"] = "halted"
        return self._change_live(track, change)

    def resume(self, track: str) -> dict:
        def change(r):
            if r.get("status") != "halted":
                raise PlayError("the staged rollout is not halted")
            r["status"] = "inProgress"
        return self._change_live(track, change)

    def status(self, track: str) -> list:
        def work(edit_id):
            return self._track(edit_id, track).get("releases") or []
        # A read-only edit is deleted, never committed.
        base = f"{API}/{self.package}/edits"
        edit_id = self._call("POST", base, {}).get("id", "")
        try:
            return work(edit_id)
        finally:
            try:
                self._call("DELETE", f"{base}/{edit_id}")
            except PlayError:
                pass


# ───────────────────────────── self-test ─────────────────────────────

class FakePlay(Transport):
    def __init__(self, tracks: dict | None = None, fail: dict | None = None):
        self.tracks = tracks or {}
        self.fail = fail or {}
        self.calls: list[tuple[str, str]] = []
        self.committed: list[str] = []
        self.deleted: list[str] = []
        self.pending: dict = {}

    def request(self, method, url, headers, body):
        path = urllib.parse.urlsplit(url).path
        self.calls.append((method, path))
        for (m, needle), status in self.fail.items():
            if m == method and needle in url:
                return status, json.dumps({"reason": "testFailure"}).encode()
        if url.startswith("https://oauth2.googleapis.com/"):
            assert b"assertion=" in body
            return 200, json.dumps({"access_token": "fake-access-token"}).encode()
        assert headers["Authorization"] == "Bearer fake-access-token"
        if method == "POST" and path.endswith("/edits"):
            self.pending = json.loads(json.dumps(self.tracks))
            return 200, json.dumps({"id": "edit1"}).encode()
        if method == "POST" and path.endswith(":commit"):
            self.tracks = self.pending
            self.committed.append(path)
            return 200, b"{}"
        if method == "DELETE":
            self.deleted.append(path)
            return 204, b""
        if "/bundles" in path:
            assert headers["Content-Type"] == "application/octet-stream"
            return 200, json.dumps({"versionCode": 500001}).encode()
        track = path.rsplit("/", 1)[1]
        if method == "GET":
            return 200, json.dumps({"track": track, "releases": self.pending.get(track, [])}).encode()
        if method == "PUT":
            self.pending[track] = json.loads(body)["releases"]
            return 200, body
        return 500, b""


def _self_test() -> int:
    checks: list[tuple[str, bool]] = []

    def check(name, ok):
        checks.append((name, bool(ok)))

    def expect(name, fn, needle, code=None):
        try:
            fn()
        except PlayError as e:
            check(name, needle in e.message and (code is None or e.code == code))
            return e
        check(name, False)

    key = json.dumps({"client_email": "ci@froglet.iam.gserviceaccount.com",
                      "private_key": "-----BEGIN PRIVATE KEY-----\nFAKE\n-----END PRIVATE KEY-----\n",
                      "token_uri": "https://oauth2.googleapis.com/token"})
    fake_sign = lambda pem, data: b"signature"  # noqa: E731

    def mk(tracks=None, fail=None):
        t = FakePlay(tracks, fail)
        return Play(key, DEFAULT_PACKAGE, t, fake_sign), t

    tmp = Path(tempfile.mkdtemp(prefix="play-test-"))
    try:
        aab = tmp / "app.aab"
        aab.write_bytes(b"PK fake bundle")

        p, t = mk({"internal": []})
        code = p.upload(aab, "0.3.0 (rc-1)")
        check("upload returns the version code", code == 500001)
        check("upload puts it on internal, completed", t.tracks["internal"][0]["versionCodes"] == ["500001"]
              and t.tracks["internal"][0]["status"] == "completed")
        check("upload commits its edit once", len(t.committed) == 1)
        expect("upload refuses any track but internal", lambda: mk()[0].upload(aab, track="production"),
               "internal track only")
        expect("upload refuses a file that is not an .aab", lambda: mk()[0].upload(tmp / "nope.apk"), "not an .aab")

        p, t = mk({"internal": []}, fail={("PUT", "/tracks/internal"): 403})
        expect("a failure mid-edit is reported with its status, not its body",
               lambda: p.upload(aab), "HTTP 403")
        check("... and the edit is deleted, not committed", t.deleted and not t.committed)

        internal = {"internal": [{"versionCodes": ["500001"], "status": "completed"}]}
        p, t = mk(internal)
        r = p.promote(500001, "production", 0.05)
        check("promote to production is a staged rollout", r["status"] == "inProgress" and r["userFraction"] == 0.05
              and t.tracks["production"][0]["versionCodes"] == ["500001"])
        p, t = mk(internal)
        check("promote to a closed track completes there", p.promote(500001, "alpha", None)["status"] == "completed")
        expect("promote refuses a version code that is not on internal",
               lambda: mk(internal)[0].promote(999, "alpha", None), "not on the internal track", 3)
        expect("promote refuses the internal track", lambda: mk(internal)[0].promote(500001, "internal", None),
               "never targets the internal track")
        expect("production without a fraction is refused", lambda: mk(internal)[0].promote(500001, "production", None),
               "needs --user-fraction")
        expect("a full production rollout needs --allow-full",
               lambda: mk(internal)[0].promote(500001, "production", 1.0), "--allow-full")
        expect("a fraction above 1 is refused", lambda: mk(internal)[0].promote(500001, "production", 1.5),
               "between 0 and 1")

        staged = {"production": [{"versionCodes": ["500001"], "status": "inProgress", "userFraction": 0.05}]}
        p, t = mk(staged)
        p.halt("production")
        check("halt stops the staged rollout", t.tracks["production"][0]["status"] == "halted")
        p.resume("production")
        check("resume restarts it", t.tracks["production"][0]["status"] == "inProgress")
        p.rollout("production", 0.5)
        check("rollout widens it", t.tracks["production"][0]["userFraction"] == 0.5)
        expect("halting a track with no staged rollout says why",
               lambda: mk({"production": [{"versionCodes": ["1"], "status": "completed"}]})[0].halt("production"),
               "no staged rollout")
        p, t = mk(staged)
        p.status("production")
        check("status reads without committing", not t.committed and t.deleted)

        expect("a key that is not a service account is refused", lambda: Play('{"x": 1}', DEFAULT_PACKAGE),
               "not a service account")
        expect("a missing key is named", lambda: Play("", DEFAULT_PACKAGE), "PLAY_SERVICE_ACCOUNT_JSON")
        expect("a token_uri that is not Google's is refused", lambda: Play(json.dumps(
            {"client_email": "a", "private_key": "b", "token_uri": "https://evil.example/token"}), DEFAULT_PACKAGE),
            "not Google's")
        p, t = mk(fail={("POST", "oauth2.googleapis.com"): 400})
        expect("a refused service account says so", lambda: p.upload(aab), "refused the service account")
        rc = main(["promote", "--version-code", "1", "--to-track", "internal"])
        check("the command line refuses promoting to internal before reading any key", rc == 2)

        # The real signer: a throwaway RSA key, signed with openssl and verified with its public half.
        if shutil.which("openssl"):
            kp = tmp / "k.pem"
            subprocess.run(["openssl", "genpkey", "-algorithm", "RSA", "-pkeyopt", "rsa_keygen_bits:2048",
                            "-out", str(kp)], check=True, capture_output=True)
            subprocess.run(["openssl", "pkey", "-in", str(kp), "-pubout", "-out", str(tmp / "pub.pem")],
                           check=True, capture_output=True)
            sig = openssl_sign(kp.read_text(), b"header.claims")
            (tmp / "sig").write_bytes(sig)
            v = subprocess.run(["openssl", "dgst", "-sha256", "-verify", str(tmp / "pub.pem"), "-signature",
                                str(tmp / "sig")], input=b"header.claims", capture_output=True)
            check("openssl_sign makes an RS256 signature its public key verifies", v.returncode == 0)
            leftovers = list(Path(tempfile.gettempdir()).glob("play-key-*.pem"))
            check("openssl_sign leaves no key file behind", not leftovers)
            expect("openssl_sign refuses a key that is not a key", lambda: openssl_sign("not a key", b"x"),
                   "could not sign")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    for name, ok in checks:
        print(("ok   " if ok else "FAIL ") + name)
    failed = [n for n, ok in checks if not ok]
    if failed:
        print(f"SELF-TEST FAILED: {len(failed)} of {len(checks)} checks")
        return 1
    print(f"self-test OK: {len(checks)} checks, including uploads to any track but internal, a promotion of a "
          f"version not on internal and a failed edit (all refused, the edit deleted)")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    u = sub.add_parser("upload")
    u.add_argument("--aab", required=True)
    u.add_argument("--release-name", default="")
    u.add_argument("--github-output", default="")
    p = sub.add_parser("promote")
    p.add_argument("--version-code", required=True, type=int)
    p.add_argument("--to-track", required=True)
    p.add_argument("--user-fraction", type=float)
    p.add_argument("--allow-full", action="store_true")
    p.add_argument("--release-name", default="")
    r = sub.add_parser("rollout")
    r.add_argument("--track", default="production")
    r.add_argument("--user-fraction", required=True, type=float)
    r.add_argument("--allow-full", action="store_true")
    for name in ("halt", "resume", "status"):
        s = sub.add_parser(name)
        s.add_argument("--track", default="production")
    sub.add_parser("self-test")
    args = ap.parse_args(argv)
    err = "::error::" if os.environ.get("GITHUB_ACTIONS") == "true" else "ERROR: "
    try:
        if args.cmd == "self-test":
            return _self_test()
        if args.cmd == "promote" and args.to_track == "internal":
            raise PlayError("promote never targets the internal track; upload does")
        track = getattr(args, "track", None) or getattr(args, "to_track", None)
        if track and not TRACK_RE.match(track):
            raise PlayError("not a Play track name")
        play = Play(os.environ.get("PLAY_SERVICE_ACCOUNT_JSON", ""), os.environ.get("PLAY_PACKAGE") or DEFAULT_PACKAGE)
        if args.cmd == "upload":
            code = play.upload(Path(args.aab), args.release_name)
            print(f"uploaded version code {code} to the internal track")
            if args.github_output:
                with open(args.github_output, "a") as fh:
                    fh.write(f"version_code={code}\n")
        elif args.cmd == "promote":
            rel = play.promote(args.version_code, args.to_track, args.user_fraction, args.allow_full,
                               args.release_name)
            print(f"version code {args.version_code} -> {args.to_track}: {rel['status']}"
                  + (f" at {rel['userFraction']:.0%}" if "userFraction" in rel else ""))
        elif args.cmd == "rollout":
            rel = play.rollout(args.track, args.user_fraction, args.allow_full)
            print(f"{args.track}: {rel['status']}" + (f" at {rel['userFraction']:.0%}" if "userFraction" in rel else ""))
        elif args.cmd == "halt":
            play.halt(args.track)
            print(f"{args.track}: staged rollout halted")
        elif args.cmd == "resume":
            play.resume(args.track)
            print(f"{args.track}: staged rollout resumed")
        else:
            for rel in play.status(args.track):
                print(f"{args.track}: {','.join(rel.get('versionCodes', []))} {rel.get('status')} "
                      f"{rel.get('userFraction', '')}")
        return 0
    except PlayError as e:
        print(f"{err}{e.message}", file=sys.stderr)
        return e.code


if __name__ == "__main__":
    sys.exit(main())
