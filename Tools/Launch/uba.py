#!/usr/bin/env python3
"""Unity Build Automation (UBA) from GitHub Actions: build one player target at one commit, wait, download.

The launch pipelines (`.github/workflows/release-builds.yml`, Docs/LAUNCH/PIPELINES.md) build every player on
Unity's builders and drive them from Actions through UBA's REST API (https://build-api.cloud.unity3d.com/api/v1).

    python3 Tools/Launch/uba.py plan --platforms windows,android,ios,web       # validate a platform list (JSON out)
    python3 Tools/Launch/uba.py start --platform windows --commit <sha>         # queue a build, print its number
    python3 Tools/Launch/uba.py wait --platform windows --build 42 [--timeout-min 150]
    python3 Tools/Launch/uba.py verify --platform windows --build 42 --commit <sha>   # succeeded, at that commit
    python3 Tools/Launch/uba.py download --platform windows --build 42 --commit <sha> --out windows.zip
    python3 Tools/Launch/uba.py run --platform windows --commit <sha> --out windows.zip   # all three
    python3 Tools/Launch/uba.py self-test

Environment (the `uba-builds` GitHub Environment, or any shell):
    UBA_API_KEY      the API key (Unity Cloud > Build Automation > Settings > API key), a secret
    UBA_ORG_ID       the organisation id
    UBA_PROJECT_ID   the project id
    UBA_<PLATFORM>_TARGET   the build target id for each platform: UBA_WINDOWS_TARGET, UBA_ANDROID_TARGET,
                     UBA_IOS_TARGET, UBA_WEB_TARGET (the web pipeline's own UBA_TOYBOX_TARGET is separate)

Refusals, each a non-zero exit with one plain sentence:
    a build that ends failure / canceled / unknown, or does not finish in time (2)
    a finished build whose built commit is not the commit asked for (3) - never ship another commit's player
    a missing setting, an unknown platform, or an API answer without what it must carry (2)

Nothing secret is printed. UBA's download links are signed URLs, so no URL is ever printed with its query string
(only scheme, host and path), and an HTTP failure prints its status code and that path, never the response body.
`--github-output` writes plain outputs (build number, commit) for a workflow step. Stdlib only.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import re
import shutil
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

API = "https://build-api.cloud.unity3d.com/api/v1"
PLATFORMS = ("windows", "android", "ios", "web")
TERMINAL_OK = ("success",)
TERMINAL_BAD = ("failure", "canceled", "cancelled", "unknown")
RUNNING = ("created", "queued", "sentToBuilder", "started", "restarted")
SHA_RE = re.compile(r"^[0-9a-f]{40}$")
ID_RE = re.compile(r"^[A-Za-z0-9_.-]{1,128}$")


class UbaError(Exception):
    def __init__(self, message: str, code: int = 2):
        super().__init__(message)
        self.message = message
        self.code = code


def safe_url(url: str) -> str:
    """scheme://host/path - never the query string or fragment, where a signed URL keeps its signature."""
    p = urllib.parse.urlsplit(url)
    return f"{p.scheme}://{p.hostname or ''}{p.path}"


# ───────────────────────────── transport (replaced by a fake in the self-test) ─────────────────────────────

class Transport:
    def request(self, method: str, url: str, headers: dict, body: bytes | None) -> tuple[int, bytes]:
        req = urllib.request.Request(url, data=body, method=method, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                return r.status, r.read()
        except urllib.error.HTTPError as e:
            return e.code, b""   # the body is dropped on purpose: it can echo a request or a link

    def download(self, url: str, dest: Path) -> int:
        req = urllib.request.Request(url, headers={"User-Agent": "cosmic-shore-launch"})
        try:
            with urllib.request.urlopen(req, timeout=600) as r, dest.open("wb") as out:
                shutil.copyfileobj(r, out, 1 << 20)
                return r.status
        except urllib.error.HTTPError as e:
            return e.code


class Uba:
    def __init__(self, env: dict, transport: Transport | None = None, sleep=time.sleep, clock=time.monotonic):
        missing = [k for k in ("UBA_API_KEY", "UBA_ORG_ID", "UBA_PROJECT_ID") if not env.get(k)]
        if missing:
            raise UbaError("missing " + ", ".join(missing) + " (secrets of the uba-builds Environment; "
                           "Docs/LAUNCH/PIPELINES.md lists them)")
        for k in ("UBA_ORG_ID", "UBA_PROJECT_ID"):
            if not ID_RE.match(env[k]):
                raise UbaError(f"{k} is not a plain id (letters, digits, '.', '_', '-')")
        self.env = env
        self.http = transport or Transport()
        self.sleep = sleep
        self.clock = clock

    def target(self, platform: str) -> str:
        if platform not in PLATFORMS:
            raise UbaError(f"'{platform}' is not a launch platform ({', '.join(PLATFORMS)}; macOS is out)")
        name = f"UBA_{platform.upper()}_TARGET"
        value = self.env.get(name, "")
        if not value:
            raise UbaError(f"missing {name}: the UBA build target id for {platform} (a variable of the uba-builds "
                           f"Environment; create the target first, Docs/LAUNCH/PIPELINES.md)")
        if not ID_RE.match(value):
            raise UbaError(f"{name} is not a plain build target id")
        return value

    def _url(self, platform: str, suffix: str = "") -> str:
        return (f"{API}/orgs/{self.env['UBA_ORG_ID']}/projects/{self.env['UBA_PROJECT_ID']}"
                f"/buildtargets/{self.target(platform)}/builds{suffix}")

    def _call(self, method: str, url: str, payload: dict | None = None):
        # UBA's v1 API takes the API key itself as the Basic credential (as Tools/Build/publish_toybox.py does).
        headers = {"Authorization": "Basic " + self.env["UBA_API_KEY"], "Content-Type": "application/json",
                   "User-Agent": "cosmic-shore-launch"}
        body = json.dumps(payload).encode() if payload is not None else None
        status, raw = self.http.request(method, url, headers, body)
        if status == 401 or status == 403:
            raise UbaError(f"UBA refused the credentials (HTTP {status}) for {safe_url(url)}: check UBA_API_KEY "
                           f"and that the key's user can build this project")
        if status == 404:
            raise UbaError(f"UBA has no such org, project, target or build (HTTP 404) at {safe_url(url)}")
        if status >= 300:
            raise UbaError(f"UBA answered HTTP {status} for {method} {safe_url(url)}")
        try:
            return json.loads(raw.decode() or "null")
        except ValueError:
            raise UbaError(f"UBA answered {method} {safe_url(url)} with something that is not JSON")

    # ── the three steps ──

    def start(self, platform: str, commit: str, label: str = "") -> int:
        if not SHA_RE.match(commit):
            raise UbaError("--commit must be a full 40-character commit sha")
        payload = {"clean": True, "delay": 0, "commit": commit}
        if label:
            payload["label"] = label[:100]
        data = self._call("POST", self._url(platform), payload)
        first = data[0] if isinstance(data, list) and data else data
        if not isinstance(first, dict):
            raise UbaError("UBA's answer to starting a build holds no build")
        if first.get("error"):
            # UBA's own error sentence (e.g. "a build is already in progress"); it carries no credential.
            raise UbaError("UBA did not start the build: " + " ".join(str(first["error"]).split())[:200])
        number = first.get("build")
        if not isinstance(number, int):
            raise UbaError("UBA's answer to starting a build holds no build number")
        return number

    def get(self, platform: str, build: int) -> dict:
        data = self._call("GET", self._url(platform, f"/{int(build)}"))
        if not isinstance(data, dict) or "buildStatus" not in data:
            raise UbaError(f"UBA's answer for build {build} holds no buildStatus")
        return data

    def wait(self, platform: str, build: int, timeout_min: float = 150, interval_s: float = 60,
             log=print) -> dict:
        deadline = self.clock() + timeout_min * 60
        last = None
        while True:
            data = self.get(platform, build)
            status = data["buildStatus"]
            if status != last:
                log(f"{platform} build {build}: {status}")
                last = status
            if status in TERMINAL_OK:
                return data
            if status in TERMINAL_BAD:
                raise UbaError(f"{platform} build {build} ended '{status}'. Read its log in Unity Cloud > Build "
                               f"Automation > the {platform} target > build {build}.")
            if status not in RUNNING:
                raise UbaError(f"{platform} build {build} is in a state this tool does not know: '{status}'")
            if self.clock() >= deadline:
                raise UbaError(f"{platform} build {build} did not finish within {timeout_min:g} minutes "
                               f"(last state '{status}'); it is still running on UBA")
            self.sleep(interval_s)

    def verify(self, platform: str, build: int, commit: str) -> dict:
        """The build, if it succeeded AND built exactly `commit`; otherwise a refusal."""
        if not SHA_RE.match(commit or ""):
            raise UbaError("--commit must be a full 40-character commit sha")
        data = self.get(platform, build)
        if data["buildStatus"] != "success":
            raise UbaError(f"{platform} build {build} is '{data['buildStatus']}', not success; nothing to download")
        built = str(data.get("lastBuiltRevision") or "").lower()
        if not SHA_RE.match(built):
            raise UbaError(f"{platform} build {build} does not say which commit it built; refusing to ship it", 3)
        if built != commit.lower():
            raise UbaError(f"{platform} build {build} built {built[:12]}, not {commit[:12]}: refusing an artifact "
                           f"for a different commit", 3)
        return data

    def download(self, platform: str, build: int, commit: str, dest: Path, artifact_key: str = "") -> dict:
        data = self.verify(platform, build, commit)
        built = str(data["lastBuiltRevision"]).lower()
        links = data.get("links") or {}
        href = ""
        if artifact_key:
            for art in links.get("artifacts") or []:
                if isinstance(art, dict) and art.get("key") == artifact_key:
                    files = art.get("files") or []
                    href = (files[0] or {}).get("href", "") if files else ""
                    break
            if not href:
                raise UbaError(f"{platform} build {build} has no artifact '{artifact_key}'")
        else:
            href = ((links.get("download_primary") or {}).get("href")) or ""
            if not href:
                raise UbaError(f"{platform} build {build} has no primary download")
        if not href.startswith("https://"):
            raise UbaError(f"{platform} build {build}'s download link is not https; refusing it")
        status = self.http.download(href, dest)
        if status >= 300:
            dest.unlink(missing_ok=True)
            raise UbaError(f"downloading {platform} build {build} failed (HTTP {status}) from {safe_url(href)}")
        size = dest.stat().st_size if dest.exists() else 0
        if size == 0:
            raise UbaError(f"downloading {platform} build {build} produced an empty file")
        return {"platform": platform, "build": build, "commit": built, "bytes": size,
                "file": dest.name}


def plan(platforms: str) -> list[str]:
    names = [p.strip().lower() for p in platforms.replace(" ", ",").split(",") if p.strip()]
    if not names:
        raise UbaError("no platform named; use some of " + ", ".join(PLATFORMS))
    bad = [p for p in names if p not in PLATFORMS]
    if bad:
        raise UbaError(f"not a launch platform: {', '.join(sorted(set(bad)))[:80]} (use {', '.join(PLATFORMS)}; "
                       f"macOS and Amoebius are out of the launch)")
    return [p for p in PLATFORMS if p in names]


def _write_outputs(path: str, values: dict) -> None:
    if not path:
        return
    with open(path, "a") as fh:
        for k, v in values.items():
            fh.write(f"{k}={v}\n")


# ───────────────────────────── self-test (fake HTTP; negative controls) ─────────────────────────────

class FakeTransport(Transport):
    """Answers from a script: a list of build states for GET, a build for POST, bytes for downloads."""

    def __init__(self, states: list[str], built: str, start_answer=None, start_status: int = 202,
                 download_status: int = 200, artifacts=None):
        self.states = list(states)
        self.built = built
        self.start_answer = start_answer
        self.start_status = start_status
        self.download_status = download_status
        self.artifacts = artifacts or []
        self.calls: list[tuple[str, str]] = []

    def request(self, method, url, headers, body):
        self.calls.append((method, url))
        assert headers["Authorization"] == "Basic fake-key"
        if method == "POST":
            sent = json.loads(body)
            assert SHA_RE.match(sent["commit"])
            answer = self.start_answer if self.start_answer is not None else [{"build": 42, "buildStatus": "queued"}]
            return self.start_status, json.dumps(answer).encode()
        state = self.states.pop(0) if len(self.states) > 1 else self.states[0]
        return 200, json.dumps({
            "build": 42, "buildStatus": state, "lastBuiltRevision": self.built,
            "links": {"download_primary": {"href": "https://storage.example/b/42.zip?X-Amz-Signature=SIGSECRET"},
                      "artifacts": self.artifacts}}).encode()

    def download(self, url, dest):
        self.calls.append(("DOWNLOAD", url))
        if self.download_status == 200:
            dest.write_bytes(b"PK\x03\x04 fake player")
        return self.download_status


def _self_test() -> int:
    import contextlib
    import io
    checks: list[tuple[str, bool]] = []

    def check(name, ok):
        checks.append((name, bool(ok)))

    sha = "a" * 40
    other = "b" * 40
    env = {"UBA_API_KEY": "fake-key", "UBA_ORG_ID": "froglet", "UBA_PROJECT_ID": "cosmic-shore",
           "UBA_WINDOWS_TARGET": "windows-release", "UBA_ANDROID_TARGET": "android-release"}

    def expect(name, fn, needle, code=None):
        try:
            fn()
        except UbaError as e:
            check(name, needle in e.message and (code is None or e.code == code))
            return e
        check(name, False)

    clock = {"t": 0.0}
    sleep = lambda s: clock.__setitem__("t", clock["t"] + s)  # noqa: E731
    now = lambda: clock["t"]  # noqa: E731
    tmp = Path(tempfile.mkdtemp(prefix="uba-test-"))
    out = io.StringIO()
    try:
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(out):
            # The happy path: start, three polls, download the right commit.
            fake = FakeTransport(["queued", "started", "success"], sha)
            u = Uba(env, fake, sleep, now)
            n = u.start("windows", sha, "rc-1")
            data = u.wait("windows", n, timeout_min=10, interval_s=60)
            meta = u.download("windows", n, sha, tmp / "w.zip")
            check("start returns the build number", n == 42)
            check("wait returns on success", data["buildStatus"] == "success")
            check("download writes the file and reports the commit", meta["bytes"] > 0 and meta["commit"] == sha)
            check("the start call names the target", any(m == "POST" and "/buildtargets/windows-release/builds"
                                                          in url for m, url in fake.calls))

            # Negative controls.
            expect("a failed build exits non-zero", lambda: Uba(env, FakeTransport(["started", "failure"], sha),
                                                                sleep, now).wait("windows", 42), "ended 'failure'")
            expect("a canceled build exits non-zero", lambda: Uba(env, FakeTransport(["canceled"], sha),
                                                                  sleep, now).wait("windows", 42), "ended 'canceled'")
            expect("a build that never finishes times out", lambda: Uba(env, FakeTransport(["started"], sha),
                                                                        sleep, now).wait("windows", 42, 5, 60),
                   "did not finish within 5 minutes")
            expect("an unknown state is refused", lambda: Uba(env, FakeTransport(["weird"], sha), sleep, now)
                   .wait("windows", 42), "does not know")
            e = expect("an artifact for a different commit is refused", lambda: Uba(
                env, FakeTransport(["success"], other), sleep, now).download("windows", 42, sha, tmp / "x.zip"),
                "refusing an artifact for a different commit", 3)
            check("... and nothing was downloaded", not (tmp / "x.zip").exists())
            expect("a build with no commit is refused", lambda: Uba(env, FakeTransport(["success"], ""), sleep, now)
                   .download("windows", 42, sha, tmp / "y.zip"), "does not say which commit", 3)
            expect("verify refuses a build of another commit", lambda: Uba(env, FakeTransport(["success"], other),
                                                                           sleep, now).verify("windows", 42, sha),
                   "different commit", 3)
            check("verify accepts the right commit", Uba(env, FakeTransport(["success"], sha), sleep, now)
                  .verify("windows", 42, sha)["buildStatus"] == "success")
            expect("downloading an unfinished build is refused", lambda: Uba(env, FakeTransport(["started"], sha),
                                                                             sleep, now)
                   .download("windows", 42, sha, tmp / "z.zip"), "not success")
            e = expect("a failed download names the path, not the signed query", lambda: Uba(
                env, FakeTransport(["success"], sha, download_status=403), sleep, now)
                .download("windows", 42, sha, tmp / "d.zip"), "HTTP 403")
            check("... and the signature never appears", e is not None and "SIGSECRET" not in e.message
                  and "X-Amz" not in e.message)
            expect("a missing artifact key is refused", lambda: Uba(env, FakeTransport(["success"], sha), sleep, now)
                   .download("windows", 42, sha, tmp / "k.zip", "xcode"), "no artifact 'xcode'")
            ok_art = FakeTransport(["success"], sha, artifacts=[{"key": "xcode", "files": [
                {"href": "https://storage.example/x.zip?sig=SIGSECRET"}]}])
            check("a named artifact downloads", Uba(env, ok_art, sleep, now)
                  .download("windows", 42, sha, tmp / "a.zip", "xcode")["bytes"] > 0)
            expect("UBA's own start error is reported", lambda: Uba(env, FakeTransport(
                ["queued"], sha, start_answer=[{"error": "Build already in progress"}]), sleep, now)
                .start("windows", sha), "already in progress")
            expect("refused credentials say so", lambda: Uba(env, FakeTransport(["queued"], sha, start_status=401),
                                                             sleep, now).start("windows", sha), "refused the credentials")
            expect("a short commit is refused", lambda: Uba(env, FakeTransport(["queued"], sha), sleep, now)
                   .start("windows", "abc123"), "40-character")
            expect("a missing secret is named", lambda: Uba({**env, "UBA_API_KEY": ""}), "UBA_API_KEY")
            expect("a missing target is named", lambda: Uba(env, FakeTransport(["queued"], sha), sleep, now)
                   .start("ios", sha), "UBA_IOS_TARGET")
            expect("an unknown platform is refused", lambda: plan("windows,macos"), "macos")
            expect("an empty platform list is refused", lambda: plan(" , "), "no platform")
            check("platforms come back in a fixed order", plan("web, windows,ios") == ["windows", "ios", "web"])
            check("safe_url drops the query", safe_url("https://h.example/p/a.zip?X-Amz-Signature=s#f")
                  == "https://h.example/p/a.zip")
            rc = main(["plan", "--platforms", "windows,playstation"])
            check("plan on the command line exits non-zero for a bad platform", rc != 0)
        check("no output ever carried the signed query or the key",
              "SIGSECRET" not in out.getvalue() and "fake-key" not in out.getvalue())
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    for name, ok in checks:
        print(("ok   " if ok else "FAIL ") + name)
    failed = [n for n, ok in checks if not ok]
    if failed:
        print(f"SELF-TEST FAILED: {len(failed)} of {len(checks)} checks")
        return 1
    print(f"self-test OK: {len(checks)} checks, including a failed build, a canceled build, a timeout and an "
          f"artifact for a different commit (all refused)")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("plan")
    p.add_argument("--platforms", required=True)
    p.add_argument("--github-output", default="")
    for name in ("start", "wait", "verify", "download", "run"):
        s = sub.add_parser(name)
        s.add_argument("--platform", required=True, choices=PLATFORMS)
        if name in ("start", "verify", "download", "run"):
            s.add_argument("--commit", required=True)
        if name in ("wait", "verify", "download"):
            s.add_argument("--build", required=True, type=int)
        if name in ("start", "run"):
            s.add_argument("--label", default="")
        if name in ("wait", "run"):
            s.add_argument("--timeout-min", type=float, default=150)
            s.add_argument("--interval-s", type=float, default=60)
        if name in ("download", "run"):
            s.add_argument("--out", required=True)
            s.add_argument("--artifact-key", default="", help="a named artifact instead of the primary download")
        s.add_argument("--github-output", default="")
    sub.add_parser("self-test")
    args = ap.parse_args(argv)
    err = "::error::" if os.environ.get("GITHUB_ACTIONS") == "true" else "ERROR: "
    try:
        if args.cmd == "self-test":
            return _self_test()
        if args.cmd == "plan":
            names = plan(args.platforms)
            print(json.dumps(names))
            _write_outputs(args.github_output, {"platforms": json.dumps(names),
                                                **{p: str(p in names).lower() for p in PLATFORMS}})
            return 0
        u = Uba(dict(os.environ))
        if args.cmd == "start":
            n = u.start(args.platform, args.commit, args.label)
            print(f"{args.platform}: queued UBA build {n} at {args.commit[:12]}")
            _write_outputs(args.github_output, {"build": n})
            return 0
        if args.cmd == "wait":
            u.wait(args.platform, args.build, args.timeout_min, args.interval_s)
            return 0
        if args.cmd == "verify":
            u.verify(args.platform, args.build, args.commit)
            print(f"{args.platform}: build {args.build} succeeded and built {args.commit[:12]}")
            return 0
        if args.cmd == "download":
            meta = u.download(args.platform, args.build, args.commit, Path(args.out), args.artifact_key)
            print(f"{args.platform}: build {meta['build']} at {meta['commit'][:12]}, {meta['bytes']} bytes")
            _write_outputs(args.github_output, {"build": meta["build"], "bytes": meta["bytes"]})
            return 0
        n = u.start(args.platform, args.commit, args.label)
        print(f"{args.platform}: queued UBA build {n} at {args.commit[:12]}")
        _write_outputs(args.github_output, {"build": n})
        u.wait(args.platform, n, args.timeout_min, args.interval_s)
        meta = u.download(args.platform, n, args.commit, Path(args.out), args.artifact_key)
        print(f"{args.platform}: build {n} at {meta['commit'][:12]}, {meta['bytes']} bytes")
        _write_outputs(args.github_output, {"bytes": meta["bytes"]})
        return 0
    except UbaError as e:
        print(f"{err}{e.message}", file=sys.stderr)
        return e.code


if __name__ == "__main__":
    sys.exit(main())
