#!/usr/bin/env python3
"""Launch status: one small record per pipeline, gate or deploy, kept on the `launch-status` branch.

Every launch pipeline (a build, an upload to a pre-release channel, a promotion, a rollback, a UGS deploy, a
gate) ends by publishing ONE record describing what it just did. The Launch Console (the FrogletTools editor
window and the claude.ai artifact) reads the merged `status.json` from that branch and nothing else, so every
figure on the console says where it came from (`source`, `run_url`) and how old it is (`updated_at`).

The contract is `Docs/LAUNCH/STATUS_CONTRACT.md`. In short:

    launch-status branch (orphan, data only, never merged anywhere)
      status.json                 every component's latest record, rebuilt on each publish
      components/<id>.json        one component's latest record
      history/<id>.jsonl          every record that component ever published, newest last

Subcommands:
    publish   write one record and push it (retries on a race with another publisher)
    validate  check a record file (or stdin) against the contract, no git
    show      print the board from a status.json (local file, or the branch with --from-branch)
    self-test prove the validator accepts good records and refuses bad ones (the negative control)

A record never holds a secret. Fields are an allow-list, every value is a short plain string, and a value that
looks like a credential (a long token, a URL with a query string, a private key header) is refused, so a
workflow that passes the wrong variable fails loudly instead of publishing it to a public branch.

Stdlib only; runs on any GitHub-hosted runner and any collaborator's machine.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

SCHEMA = 1
BRANCH = "launch-status"

# The components the console knows how to place. A new pipeline adds its id here and to the contract doc.
PLATFORMS = ("windows", "ios", "android", "web", "liveops", "backend", "quality", "release")
KINDS = ("channel", "gate", "deploy", "build")
CHANNELS = ("prerelease", "production", "staging", "development")
RESULTS = ("success", "failure", "running", "skipped", "cancelled")

ID_RE = re.compile(r"^[a-z0-9][a-z0-9-]{1,62}$")
SHA_RE = re.compile(r"^[0-9a-f]{7,40}$")
VERSION_RE = re.compile(r"^[0-9A-Za-z][0-9A-Za-z.+_-]{0,39}$")
URL_RE = re.compile(r"^https://[A-Za-z0-9.-]+(/[A-Za-z0-9._~%/+-]*)?(#[A-Za-z0-9._-]*)?$")

# Values that look like secrets are refused outright. A false positive costs a re-run with a shorter summary;
# a false negative publishes a credential on a public branch.
SECRET_PATTERNS = [
    re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----"),
    re.compile(r"\b(?:ghp|gho|ghs|ghu|github_pat)_[A-Za-z0-9_]{20,}"),
    re.compile(r"\bAKIA[0-9A-Z]{16}\b"),
    re.compile(r"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\."),  # a JWT
    re.compile(r"[A-Za-z0-9+/=_-]{48,}"),  # any long unbroken token
    re.compile(r"[?&](?:X-Amz-|sig=|signature=|token=|key=|access_token=)", re.I),
]

FIELDS = {
    # name: (required, validator description)
    "schema": True,
    "id": True,
    "platform": True,
    "kind": True,
    "result": True,
    "updated_at": True,
    "source": True,
    "channel": False,
    "version": False,
    "build": False,
    "commit": False,
    "summary": False,
    "run_url": False,
    "link": False,
    "rollback_to": False,
    "details": False,
}
MAX_TEXT = 280
MAX_DETAILS = 24


def now_iso() -> str:
    return _dt.datetime.now(_dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def _looks_secret(value: str) -> bool:
    return any(p.search(value) for p in SECRET_PATTERNS)


def _check_text(name: str, value, errors: list[str], limit: int = MAX_TEXT) -> None:
    if not isinstance(value, str):
        errors.append(f"{name}: must be a string")
        return
    if len(value) > limit:
        errors.append(f"{name}: longer than {limit} characters")
    if "\n" in value or "\r" in value:
        errors.append(f"{name}: must be one line")
    if _looks_secret(value):
        errors.append(f"{name}: looks like a credential or a signed URL; refusing to publish it")


def validate(record: dict) -> list[str]:
    """Every problem with a record, as plain sentences. An empty list means it may be published."""
    errors: list[str] = []
    if not isinstance(record, dict):
        return ["the record must be a JSON object"]
    for key in record:
        if key not in FIELDS:
            errors.append(f"{key}: not a field of the contract (allowed: {', '.join(FIELDS)})")
    for key, required in FIELDS.items():
        if required and key not in record:
            errors.append(f"{key}: required")
    if record.get("schema") not in (None, SCHEMA):
        errors.append(f"schema: must be {SCHEMA}")
    if "id" in record and not (isinstance(record["id"], str) and ID_RE.match(record["id"])):
        errors.append("id: lower-case letters, digits and '-', 2-63 characters")
    for key, allowed in (("platform", PLATFORMS), ("kind", KINDS), ("result", RESULTS), ("channel", CHANNELS)):
        if key in record and record[key] is not None and record[key] not in allowed:
            errors.append(f"{key}: must be one of {', '.join(allowed)}")
    if "commit" in record and record["commit"] is not None:
        if not (isinstance(record["commit"], str) and SHA_RE.match(record["commit"])):
            errors.append("commit: a 7-40 character hex commit sha")
    for key in ("version", "build", "rollback_to"):
        if key in record and record[key] is not None:
            if not (isinstance(record[key], str) and VERSION_RE.match(record[key])):
                errors.append(f"{key}: a short version-like string (letters, digits, . + _ -)")
    for key in ("run_url", "link"):
        if key in record and record[key] is not None:
            value = record[key]
            if not (isinstance(value, str) and URL_RE.match(value)):
                errors.append(f"{key}: an https URL with no query string")
    for key in ("source", "summary"):
        if key in record and record[key] is not None:
            _check_text(key, record[key], errors)
    if "updated_at" in record:
        try:
            _dt.datetime.fromisoformat(str(record["updated_at"]).replace("Z", "+00:00"))
        except ValueError:
            errors.append("updated_at: an ISO-8601 UTC time")
    details = record.get("details")
    if details is not None:
        if not isinstance(details, dict):
            errors.append("details: an object of short strings")
        else:
            if len(details) > MAX_DETAILS:
                errors.append(f"details: at most {MAX_DETAILS} entries")
            for dk, dv in details.items():
                if not (isinstance(dk, str) and re.match(r"^[a-z0-9_]{1,40}$", dk)):
                    errors.append(f"details.{dk}: keys are lower_snake_case, up to 40 characters")
                if isinstance(dv, (int, float, bool)):
                    continue
                _check_text(f"details.{dk}", dv, errors, limit=160)
    return errors


def build_record(args: argparse.Namespace) -> dict:
    record: dict = {
        "schema": SCHEMA,
        "id": args.id,
        "platform": args.platform,
        "kind": args.kind,
        "result": args.result,
        "updated_at": now_iso(),
        "source": args.source or os.environ.get("GITHUB_WORKFLOW") or "manual",
    }
    for key in ("channel", "version", "build", "commit", "summary", "link", "rollback_to"):
        value = getattr(args, key)
        if value:
            record[key] = value
    run_url = args.run_url
    if not run_url and os.environ.get("GITHUB_RUN_ID") and os.environ.get("GITHUB_REPOSITORY"):
        server = os.environ.get("GITHUB_SERVER_URL", "https://github.com")
        run_url = f"{server}/{os.environ['GITHUB_REPOSITORY']}/actions/runs/{os.environ['GITHUB_RUN_ID']}"
    if run_url:
        record["run_url"] = run_url
    if args.detail:
        details = {}
        for item in args.detail:
            if "=" not in item:
                raise SystemExit(f"--detail wants key=value, got {item!r}")
            k, v = item.split("=", 1)
            details[k.strip()] = v.strip()
        record["details"] = details
    return record


def merge_board(components_dir: Path) -> dict:
    components = {}
    for path in sorted(components_dir.glob("*.json")):
        try:
            components[path.stem] = json.loads(path.read_text())
        except json.JSONDecodeError:
            continue
    return {"schema": SCHEMA, "generated_at": now_iso(), "components": components}


def _git(args: list[str], cwd: Path, check: bool = True) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *args], cwd=cwd, check=check, text=True, capture_output=True)


def publish(record: dict, repo: Path, remote: str, push: bool, attempts: int = 5) -> Path:
    """Write the record onto the status branch in a throwaway worktree and push. Returns the worktree path when
    not pushing (a dry run), so a caller or a test can read what would have been published."""
    errors = validate(record)
    if errors:
        raise SystemExit("refusing to publish:\n  " + "\n  ".join(errors))
    for attempt in range(1, attempts + 1):
        work = Path(tempfile.mkdtemp(prefix="launch-status-"))
        try:
            fetched = _git(["fetch", "-q", "--depth=50", remote, BRANCH], cwd=repo, check=False).returncode == 0
            _git(["worktree", "add", "-q", "--detach", str(work)], cwd=repo)
            if fetched:
                _git(["checkout", "-q", "-B", BRANCH, "FETCH_HEAD"], cwd=work)
            else:
                _git(["checkout", "-q", "--orphan", BRANCH], cwd=work)
                _git(["rm", "-rq", "--cached", "."], cwd=work, check=False)
                for child in work.iterdir():
                    if child.name == ".git":
                        continue
                    shutil.rmtree(child) if child.is_dir() else child.unlink()
                (work / "README.md").write_text(
                    "# launch-status\n\nData only. Written by `Tools/Launch/launch_status.py publish` from the launch "
                    "pipelines and read by the Launch Console. The contract is `Docs/LAUNCH/STATUS_CONTRACT.md` on "
                    "bleeding-edge. Never merge this branch.\n")
            (work / "components").mkdir(exist_ok=True)
            (work / "history").mkdir(exist_ok=True)
            cid = record["id"]
            (work / "components" / f"{cid}.json").write_text(json.dumps(record, indent=2, sort_keys=True) + "\n")
            with (work / "history" / f"{cid}.jsonl").open("a") as fh:
                fh.write(json.dumps(record, sort_keys=True) + "\n")
            board = merge_board(work / "components")
            (work / "status.json").write_text(json.dumps(board, indent=2, sort_keys=True) + "\n")
            _git(["add", "-A"], cwd=work)
            _git(["-c", "user.name=launch-status", "-c", "user.email=launch-status@users.noreply.github.com",
                  "commit", "-q", "-m", f"status({cid}): {record['result']}"], cwd=work)
            if not push:
                return work
            pushed = _git(["push", "-q", remote, f"HEAD:refs/heads/{BRANCH}"], cwd=work, check=False)
            if pushed.returncode == 0:
                print(f"published {cid} = {record['result']} to {BRANCH}")
                return work
            print(f"push attempt {attempt} refused (another publisher?): {pushed.stderr.strip()[:200]}",
                  file=sys.stderr)
        finally:
            if push:
                _git(["worktree", "remove", "--force", str(work)], cwd=repo, check=False)
                shutil.rmtree(work, ignore_errors=True)
        time.sleep(2 ** attempt)
    raise SystemExit(f"could not push to {BRANCH} after {attempts} attempts")


def show(board: dict) -> str:
    rows = []
    comps = board.get("components", {})
    for cid in sorted(comps, key=lambda c: (comps[c].get("platform", ""), c)):
        r = comps[cid]
        age = ""
        try:
            then = _dt.datetime.fromisoformat(r["updated_at"].replace("Z", "+00:00"))
            hours = (_dt.datetime.now(_dt.timezone.utc) - then).total_seconds() / 3600
            age = f"{hours:.0f}h" if hours < 48 else f"{hours / 24:.0f}d"
        except (KeyError, ValueError):
            pass
        rows.append(f"{r.get('platform', '?'):8} {cid:28} {r.get('result', '?'):9} "
                    f"{r.get('version', '-'):>10} {(r.get('commit') or '-')[:9]:9} {age:>4}  {r.get('source', '')}")
    return "\n".join(rows) if rows else "(no components published yet)"


def self_test() -> int:
    good = {"schema": 1, "id": "web-staging", "platform": "web", "kind": "channel", "channel": "staging",
            "result": "success", "updated_at": "2026-11-05T20:00:00Z", "source": "Publish browser Toybox",
            "version": "3", "commit": "1ac6f87ae0", "run_url": "https://github.com/o/r/actions/runs/1",
            "details": {"bytes": 119802073, "root": "toybox-staging"}}
    assert validate(good) == [], validate(good)
    bad_cases = {
        "unknown field": {**good, "token": "x"},
        "missing result": {k: v for k, v in good.items() if k != "result"},
        "bad platform": {**good, "platform": "macos"},
        "signed url": {**good, "run_url": "https://r2.dev/x?X-Amz-Signature=abc"},
        "long token in summary": {**good, "summary": "deploy ok " + "A" * 60},
        "github token in details": {**good, "details": {"note": "ghp_" + "a" * 36}},
        "private key": {**good, "summary": "-----BEGIN RSA PRIVATE KEY-----"},
        "jwt": {**good, "summary": "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.sig"},
        "bad sha": {**good, "commit": "not-a-sha"},
        "multi-line": {**good, "summary": "a\nb"},
        "bad id": {**good, "id": "Web Staging"},
    }
    failures = [name for name, rec in bad_cases.items() if not validate(rec)]
    if failures:
        print("SELF-TEST FAILED: accepted records it must refuse: " + ", ".join(failures))
        return 1
    with tempfile.TemporaryDirectory() as tmp:
        comps = Path(tmp) / "components"
        comps.mkdir()
        (comps / "web-staging.json").write_text(json.dumps(good))
        board = merge_board(comps)
        assert board["components"]["web-staging"]["result"] == "success"
        assert "web-staging" in show(board)
    print(f"self-test OK: 1 good record accepted, {len(bad_cases)} bad records refused")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("publish", help="publish one record to the launch-status branch")
    p.add_argument("--id", required=True)
    p.add_argument("--platform", required=True, choices=PLATFORMS)
    p.add_argument("--kind", required=True, choices=KINDS)
    p.add_argument("--result", required=True, choices=RESULTS)
    p.add_argument("--channel", choices=CHANNELS)
    p.add_argument("--version")
    p.add_argument("--build")
    p.add_argument("--commit", default=os.environ.get("GITHUB_SHA"))
    p.add_argument("--summary")
    p.add_argument("--link", help="a dashboard or store page for this component (https, no query)")
    p.add_argument("--rollback-to", dest="rollback_to")
    p.add_argument("--run-url")
    p.add_argument("--source")
    p.add_argument("--detail", action="append", help="key=value, repeatable")
    p.add_argument("--remote", default="origin")
    p.add_argument("--repo", default=".")
    p.add_argument("--dry-run", action="store_true", help="validate and print the record; do not push")

    v = sub.add_parser("validate", help="validate a record file (or - for stdin)")
    v.add_argument("path")

    s = sub.add_parser("show", help="print the board")
    s.add_argument("path", nargs="?", help="a status.json; default: the launch-status branch")
    s.add_argument("--remote", default="origin")

    sub.add_parser("self-test")
    args = ap.parse_args(argv)

    if args.cmd == "self-test":
        return self_test()
    if args.cmd == "validate":
        text = sys.stdin.read() if args.path == "-" else Path(args.path).read_text()
        errors = validate(json.loads(text))
        print("OK" if not errors else "INVALID:\n  " + "\n  ".join(errors))
        return 0 if not errors else 1
    if args.cmd == "show":
        if args.path:
            board = json.loads(Path(args.path).read_text())
        else:
            subprocess.run(["git", "fetch", "-q", args.remote, BRANCH], check=True)
            board = json.loads(subprocess.run(["git", "show", f"FETCH_HEAD:status.json"], check=True, text=True,
                                              capture_output=True).stdout)
        print(show(board))
        return 0
    record = build_record(args)
    errors = validate(record)
    if errors:
        print("refusing to publish:\n  " + "\n  ".join(errors), file=sys.stderr)
        return 1
    if args.dry_run:
        print(json.dumps(record, indent=2, sort_keys=True))
        return 0
    publish(record, Path(args.repo), args.remote, push=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
