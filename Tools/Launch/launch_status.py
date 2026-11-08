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
    show      print the board from a status.json (a local file, or the branch when no path is given)
    self-test prove the validator accepts good records and refuses bad ones, and publish end to end against a
              throwaway local remote (a lost race, a retry, a refusal, and nothing left behind in the caller's repo)

A record never holds a secret. Fields are an allow-list, every value is a short plain string, and a value that
looks like a credential (a long or random-looking token, a key in a known format, an Authorization header, a URL
with a query string or a user:password@, a private key or its base64 body, split up or not) is refused, so a
workflow that passes the wrong variable fails loudly instead of publishing it to a public branch.

publish never checks anything out and never creates a branch or a worktree in the caller's repo. It builds the
commit with git plumbing (a private temporary index, hash-object, write-tree, commit-tree) on top of the fetched
branch tip and pushes that commit straight to refs/heads/launch-status. A push that loses a race is refused as a
non-fast-forward; publish then re-fetches, rebuilds on the new tip and tries again.

Stdlib only; runs on any GitHub-hosted runner and any collaborator's machine.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import json
import math
import os
import random
import re
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

# All of these are used with fullmatch: `$` under re.match also matches before a trailing newline.
ID_RE = re.compile(r"[a-z0-9][a-z0-9-]{1,62}")
SHA_RE = re.compile(r"[0-9a-f]{7,40}")
VERSION_RE = re.compile(r"[0-9A-Za-z][0-9A-Za-z.+_-]{0,39}")
URL_RE = re.compile(r"https://[A-Za-z0-9.-]+(/[A-Za-z0-9._~%/+-]*)?(#[A-Za-z0-9._-]*)?")
DETAIL_KEY_RE = re.compile(r"[a-z0-9_]{1,40}")
TIME_RE = re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z")

# Values that look like secrets are refused outright. A false positive costs a re-run with a shorter summary;
# a false negative publishes a credential on a public branch. Each rule names what it caught, never the value.
SECRET_PATTERNS = [
    ("a private key or certificate block",
     re.compile(r"-----BEGIN [A-Z0-9 ]*-----|BEGIN [A-Z0-9 ]*PRIVATE KEY|PRIVATE KEY-----|PRIVATE KEY BLOCK")),
    ("a base64 key body", re.compile(r"\bMII[A-Za-z0-9+/]{5,}|b3BlbnNzaC1rZXktdjE")),
    ("a GitHub token", re.compile(r"\b(?:gh[pousr]|github_pat)_[A-Za-z0-9_]{20,}")),
    ("an AWS access key id", re.compile(r"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b")),
    ("a Google API key", re.compile(r"\bAIza[0-9A-Za-z_-]{30,}")),
    ("a Slack token", re.compile(r"\bxox[abposr]-[A-Za-z0-9-]{10,}")),
    ("an API secret key", re.compile(r"\b[sr]k[-_](?:live_|test_|ant-|proj-)?[A-Za-z0-9_-]{20,}")),
    ("a webhook URL", re.compile(r"hooks\.slack\.com/(?:services|workflows)/|discord(?:app)?\.com/api/webhooks/",
                                 re.I)),
    ("a JWT", re.compile(r"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.")),
    ("credentials in a URL (user:password@host)", re.compile(r"[A-Za-z][A-Za-z0-9+.-]*://[^/\s?#@]*@")),
    ("an Authorization header", re.compile(r"(?i)\b(?:proxy-)?authorization\s*[:=]")),
    ("a bearer or basic credential", re.compile(
        r"(?i:\bbearer\s+)(?=\S*[0-9._~+/=-])\S{8,}|\bBasic\s+(?=\S*[0-9=+/])[A-Za-z0-9+/]{11,}")),
    ("a credential assignment", re.compile(
        r"(?i)\b(?:x-api-key|x-auth-token|api[-_]?key|api[-_]?token|private[-_]?key|client[-_]?secret|secret[-_]?key"
        r"|access[-_]?token|refresh[-_]?token|auth[-_]?token|password|passwd|pwd|secret|token)\s*[:=]\s*\S")),
    ("a signed URL", re.compile(
        r"(?i)[?&](?:X-Amz-|X-Goog-|sig=|signature=|token=|key=|access_token=|code=|sv=|se=|sp=|expires=|policy="
        r"|key-pair-id=)")),
    ("a URL with a query string", re.compile(r"(?i)\bhttps?://[^\s?#]*\?\S")),
    ("a licence serial", re.compile(r"\b[A-Z0-9]{2}(?:-[A-Z0-9]{4}){5}\b")),
    ("an app-specific password", re.compile(r"(?<![-\w])[a-z]{4}-[a-z]{4}-[a-z]{4}-[a-z]{4}(?![-\w])")),
    ("a 32-character hex key", re.compile(r"(?<![0-9A-Za-z])[0-9a-fA-F]{32}(?![0-9A-Za-z])")),
]
# A run is what is left between any two characters a token cannot hold (space, '/', '.', ':', quotes ...).
_RUN_SPLIT = re.compile(r"[^A-Za-z0-9+=_-]+")
# Readable pieces: lower case, Capitalised, ALL CAPS, a number, a word ending in a number (Windows64). Kept strict on
# purpose: a short piece of a random token takes one of these shapes only by chance, so loosening this is what lets
# a token split into pieces through.
_READABLE = re.compile(r"[a-z]+|[A-Z][a-z]+|[A-Z]+|[0-9]+|[A-Z]?[a-z]{3,}[0-9]+")
# A whole identifier (only ever tested on a piece of 12+ characters, where a random piece rarely takes its shape):
# CamelCase humps and short acronyms, optionally starting lower-case and ending in a number (OriginalColorSetSO,
# enableCrashReportAPI, DontDestroyOnLoad). The miss rates this costs are measured in the contract doc.
_IDENT = re.compile(r"[a-z]*(?:[A-Z][a-z]+|[A-Z]{2,4}(?=[A-Z][a-z]|[0-9]|$))+[0-9]*")
_CAMEL = re.compile(r"(?:[A-Z][a-z]+|[0-9]+|[-_])+")
_HEXISH = re.compile(r"[0-9a-f]+|[0-9A-F]+")
_PIECES = re.compile(r"[^A-Za-z0-9]+")


def _noisy(run: str) -> bool:
    """Has a digit, an upper-case and a lower-case letter: how a random token looks."""
    return any(c.isdigit() for c in run) and any(c.isupper() for c in run) and any(c.islower() for c in run)


def _unreadable(piece: str) -> bool:
    """An alphanumeric piece that is not a word, a number, hex (a short sha, a uuid group) or a long identifier."""
    if _READABLE.fullmatch(piece) or _HEXISH.fullmatch(piece):
        return False
    return not (len(piece) >= 12 and _IDENT.fullmatch(piece))


def _token_reason(value: str) -> str | None:
    """The shape rules: a long token, a random-looking one, or a base64 body broken up by spaces or slashes."""
    for run in _RUN_SPLIT.split(value):
        if len(run) >= 48:
            return "a long unbroken token"
        if len(run) >= 20 and _noisy(run) and not _CAMEL.fullmatch(run):
            return "a random-looking token"
        for alnum in re.findall(r"[A-Za-z0-9]{24,}", run):
            digits = sum(c.isdigit() for c in alnum)
            if len(alnum) / 5 <= digits <= len(alnum) * 4 / 5 and not re.fullmatch(r"[0-9a-f]{40}", alnum):
                return "a random-looking token"
    # A whole word: a token often carries '/', '+', '=' or '.', which split it into runs too short for the rules
    # above. A URL path or a file path splits into readable pieces; a token splits into unreadable ones.
    for word in value.split():
        word = word.strip("\"'`()[]{}<>,;:!?*").rstrip(".")
        if len(word) < 16 or not re.fullmatch(r"[A-Za-z0-9+/=_.-]+", word):
            continue
        noise = sum(len(p) for p in _PIECES.split(word) if len(p) >= 2 and _unreadable(p))
        if noise >= 14:
            return "a random-looking token"
    # A base64 body cut into pieces: unreadable pieces in a row, broken only by two readable words in a row.
    noise = digits = readable_streak = 0
    for piece in _PIECES.split(value):
        if len(piece) < 3:
            continue
        if not _unreadable(piece):
            readable_streak += 1
            if readable_streak >= 2:
                noise = digits = 0
            continue
        readable_streak = 0
        noise += len(piece)
        digits += sum(c.isdigit() for c in piece)
        if noise >= 32 and digits >= 2:
            return "a base64 body split into pieces"
    return None


def secret_reason(value: str) -> str | None:
    """What kind of credential `value` looks like, or None. Never echoes the value."""
    for name, pattern in SECRET_PATTERNS:
        if pattern.search(value):
            return name
    return _token_reason(value)


def scrub(text: str, limit: int = 300) -> str:
    """Git output made safe to print: credentials in URLs and anything that looks like a token are masked."""
    text = re.sub(r"://[^/\s@]*@", "://***@", text)
    return " ".join("***" if secret_reason(word) else word for word in text.split())[:limit]


FIELDS = {
    # name: required
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
_LINE_BREAKS = "\n\r\x0b\x0c\x1c\x1d\x1e\x85\u2028\u2029"


def now_iso() -> str:
    return _dt.datetime.now(_dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def _check_text(name: str, value, errors: list[str], limit: int = MAX_TEXT) -> None:
    if not isinstance(value, str):
        errors.append(f"{name}: must be a string")
        return
    if len(value) > limit:
        errors.append(f"{name}: longer than {limit} characters")
    if any(c in value for c in _LINE_BREAKS):
        errors.append(f"{name}: must be one line")


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
    if "schema" in record and not (type(record["schema"]) is int and record["schema"] == SCHEMA):
        errors.append(f"schema: must be {SCHEMA}")
    if "id" in record and not (isinstance(record["id"], str) and ID_RE.fullmatch(record["id"])):
        errors.append("id: lower-case letters, digits and '-', 2-63 characters")
    for key, allowed in (("platform", PLATFORMS), ("kind", KINDS), ("result", RESULTS), ("channel", CHANNELS)):
        if key in record and record[key] is not None and record[key] not in allowed:
            errors.append(f"{key}: must be one of {', '.join(allowed)}")
    if "commit" in record and record["commit"] is not None:
        if not (isinstance(record["commit"], str) and SHA_RE.fullmatch(record["commit"])):
            errors.append("commit: a 7-40 character lower-case hex commit sha")
    for key in ("version", "build", "rollback_to"):
        if key in record and record[key] is not None:
            if not (isinstance(record[key], str) and VERSION_RE.fullmatch(record[key])):
                errors.append(f"{key}: a short version-like string (letters, digits, . + _ -)")
    for key in ("run_url", "link"):
        if key in record and record[key] is not None:
            value = record[key]
            if not (isinstance(value, str) and URL_RE.fullmatch(value)):
                errors.append(f"{key}: an https URL with no user, port or query string")
    for key in ("source", "summary"):
        if key in record and record[key] is not None:
            _check_text(key, record[key], errors)
    if "updated_at" in record:
        value = record["updated_at"]
        if not (isinstance(value, str) and TIME_RE.fullmatch(value)):
            errors.append("updated_at: ISO-8601 UTC to the second, like 2026-11-05T20:00:00Z")
        else:
            try:
                _dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
            except ValueError:
                errors.append("updated_at: not a real date and time")
    details = record.get("details")
    if details is not None:
        if not isinstance(details, dict):
            errors.append("details: an object of short strings")
        else:
            if len(details) > MAX_DETAILS:
                errors.append(f"details: at most {MAX_DETAILS} entries")
            for dk, dv in details.items():
                if not (isinstance(dk, str) and DETAIL_KEY_RE.fullmatch(dk)):
                    errors.append(f"details.{dk!r}: keys are lower_snake_case, up to 40 characters")
                if isinstance(dv, bool) or isinstance(dv, int):
                    continue
                if isinstance(dv, float):
                    if not math.isfinite(dv):
                        errors.append(f"details.{dk}: a finite number")
                    continue
                _check_text(f"details.{dk}", dv, errors, limit=160)
    # Every string anywhere in the record is scanned for credentials, whatever field it arrived in.
    strings = [(k, v) for k, v in record.items() if isinstance(v, str)]
    if isinstance(details, dict):
        strings += [(f"details.{k}", v) for k, v in details.items() if isinstance(v, str)]
        strings += [(f"details key {k[:12]!r}", k) for k in details if isinstance(k, str)]
    for name, value in strings:
        reason = secret_reason(value)
        if reason:
            errors.append(f"{name}: looks like {reason}; refusing to publish it")
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
                raise SystemExit("--detail wants key=value")
            k, v = item.split("=", 1)
            details[k.strip()] = v.strip()
        record["details"] = details
    return record


def board_of(components: dict) -> dict:
    return {"schema": SCHEMA, "generated_at": now_iso(), "components": dict(sorted(components.items()))}


# ---------------------------------------------------------------------------------------------- git plumbing

class GitError(RuntimeError):
    pass


def _git(args: list[str], cwd: Path, check: bool = True, env: dict | None = None,
         data: bytes | None = None) -> subprocess.CompletedProcess:
    """Run git; output is decoded. A failure raises GitError with scrubbed stderr (never the raw text)."""
    proc = subprocess.run(["git", *args], cwd=cwd, input=data, capture_output=True, env=env)
    out = subprocess.CompletedProcess(proc.args, proc.returncode, proc.stdout.decode("utf-8", "replace"),
                                      proc.stderr.decode("utf-8", "replace"))
    if check and out.returncode != 0:
        raise GitError(f"git {args[0]} failed: {scrub(out.stderr)}")
    return out


# Failures no retry can fix: stop at once instead of sleeping through five attempts.
FATAL_GIT = ("does not appear to be a git repository", "Repository not found", "Authentication failed",
             "could not read Username", "Permission to", "403", "denied", "protected branch",
             "No such remote", "not a git repository")

README = ("# launch-status\n\nData only. Written by `Tools/Launch/launch_status.py publish` from the launch pipelines "
          "and read by the Launch Console. The contract is `Docs/LAUNCH/STATUS_CONTRACT.md` on bleeding-edge. "
          "Never merge this branch, never commit to it by hand.\n")


def _build_commit(record: dict, repo: Path, parent: str | None) -> str:
    """The next status commit on top of `parent` (None: the branch's first commit), built without touching the
    caller's index, working tree, HEAD or branches. Objects land in the repo's object store, unreferenced until
    pushed; git gc collects them."""
    cid = record["id"]
    components: dict = {}
    history = ""
    files: dict[str, str] = {}
    if parent:
        listing = _git(["ls-tree", "--name-only", parent, "components/"], repo).stdout.split()
        for path in listing:
            stem = path[len("components/"):-len(".json")] if path.endswith(".json") else ""
            if not stem or stem == cid:
                continue
            blob = _git(["cat-file", "blob", f"{parent}:{path}"], repo, check=False)
            try:
                components[stem] = json.loads(blob.stdout)
            except json.JSONDecodeError:
                continue
        old = _git(["cat-file", "blob", f"{parent}:history/{cid}.jsonl"], repo, check=False)
        if old.returncode == 0:
            history = old.stdout if old.stdout.endswith("\n") or not old.stdout else old.stdout + "\n"
    else:
        files["README.md"] = README
    components[cid] = record
    files[f"components/{cid}.json"] = json.dumps(record, indent=2, sort_keys=True) + "\n"
    files[f"history/{cid}.jsonl"] = history + json.dumps(record, sort_keys=True) + "\n"
    files["status.json"] = json.dumps(board_of(components), indent=2, sort_keys=True) + "\n"

    with tempfile.TemporaryDirectory(prefix="launch-status-index-") as tmp:
        env = {**os.environ, "GIT_INDEX_FILE": str(Path(tmp) / "index"),
               "GIT_AUTHOR_NAME": "launch-status", "GIT_AUTHOR_EMAIL": "launch-status@users.noreply.github.com",
               "GIT_COMMITTER_NAME": "launch-status",
               "GIT_COMMITTER_EMAIL": "launch-status@users.noreply.github.com"}
        # The private index is never a checkout, so the caller's sparse patterns cannot apply to it; the -c flags
        # make sure of it (a sparse or sparse-index caller repo must still rebuild every component).
        dense = ["-c", "core.sparseCheckout=false", "-c", "core.sparseCheckoutCone=false", "-c", "index.sparse=false"]
        _git([*dense, "read-tree", parent] if parent else [*dense, "read-tree", "--empty"], repo, env=env)
        for path, text in files.items():
            blob = _git(["hash-object", "-w", "--stdin"], repo, env=env, data=text.encode("utf-8")).stdout.strip()
            _git([*dense, "update-index", "--add", "--cacheinfo", f"100644,{blob},{path}"], repo, env=env)
        tree = _git([*dense, "write-tree"], repo, env=env).stdout.strip()
        cmd = ["commit-tree", tree, "-m", f"status({cid}): {record['result']}"]
        if parent:
            cmd += ["-p", parent]
        return _git(cmd, repo, env=env).stdout.strip()


def publish(record: dict, repo: Path, remote: str, attempts: int = 5, sleep=time.sleep, before_push=None) -> str:
    """Publish one record and return the pushed commit. Refuses an invalid record before touching git.

    `before_push(attempt)` is a test seam: the self-test uses it to make another publisher win the race."""
    errors = validate(record)
    if errors:
        raise SystemExit("refusing to publish:\n  " + "\n  ".join(errors))
    cid = record["id"]
    last = "no attempt made"
    for attempt in range(1, attempts + 1):
        if attempt > 1:
            sleep(min(2 ** (attempt - 1), 16) + random.uniform(0, 1))
        fetched = _git(["fetch", "-q", "--no-tags", remote, f"refs/heads/{BRANCH}"], repo, check=False)
        if fetched.returncode == 0:
            parent = _git(["rev-parse", "--verify", "-q", "FETCH_HEAD^{commit}"], repo).stdout.strip()
        elif "couldn't find remote ref" in fetched.stderr:
            parent = None  # the first record ever: start the orphan branch
        elif any(k in fetched.stderr for k in FATAL_GIT):
            raise SystemExit(f"cannot fetch {BRANCH} from {remote}: {scrub(fetched.stderr)}")
        else:
            last = f"fetch failed: {scrub(fetched.stderr)}"
            print(f"attempt {attempt}: {last}", file=sys.stderr)
            continue
        commit = _build_commit(record, repo, parent)
        if before_push:
            before_push(attempt)
        pushed = _git(["push", "-q", remote, f"{commit}:refs/heads/{BRANCH}"], repo, check=False)
        if pushed.returncode == 0:
            print(f"published {cid} = {record['result']} to {BRANCH} ({commit[:10]})")
            return commit
        if any(k in pushed.stderr for k in FATAL_GIT):
            raise SystemExit(f"push to {BRANCH} refused: {scrub(pushed.stderr)}")
        last = f"push refused: {scrub(pushed.stderr)}"
        print(f"attempt {attempt}: {last} (another publisher? re-fetching)", file=sys.stderr)
    raise SystemExit(f"could not publish {cid} to {BRANCH} after {attempts} attempts; last: {last}")


# ---------------------------------------------------------------------------------------------- the board

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
        except (KeyError, ValueError, AttributeError):
            pass
        build = r.get("version") or "-"
        if r.get("build"):
            build = f"{build} ({r['build']})"
        rows.append(f"{r.get('platform', '?'):8} {cid:22} {r.get('channel') or '-':11} {r.get('result', '?'):9} "
                    f"{build:>16} {(r.get('commit') or '-')[:9]:9} {age:>4}  {r.get('source', '')}")
        for key in ("summary", "run_url", "link"):
            if r.get(key):
                rows.append(f"{'':9}{key}: {r[key]}")
        if r.get("details"):
            rows.append(f"{'':9}details: " + ", ".join(f"{k}={v}" for k, v in sorted(r["details"].items())))
    return "\n".join(rows) if rows else "(no components published yet)"


# ---------------------------------------------------------------------------------------------- self-test

GOOD = {"schema": 1, "id": "web-staging", "platform": "web", "kind": "channel", "channel": "staging",
        "result": "success", "updated_at": "2026-11-05T20:00:00Z", "source": "Publish browser Toybox",
        "version": "3", "commit": "1ac6f87ae0", "run_url": "https://github.com/o/r/actions/runs/1",
        "details": {"bytes": 119802073, "root": "toybox-staging"}}

# Records that must be ACCEPTED: the first real records, and text a summary plausibly carries. These keep the
# secret rules from refusing ordinary launch facts.
GOOD_CASES = {
    "the base record": GOOD,
    "a run link in a summary": {**GOOD, "summary": "Static checks green, see "
                                "https://github.com/froglet-studio/Cosmic-Shore/actions/runs/42095529959"},
    "a full commit sha in a summary": {**GOOD, "summary": "rolled back to 1ac6f87ae0c3b2d4e5f60718293a4b5c6d7e8f90"},
    "a date and a version": {**GOOD, "summary": "Hand upload 2026-10-22 on the r2.dev host; commit unknown",
                             "link": "https://www.froglet.games/play", "details": {"event_date": "2026-10-22"}},
    "words about auth": {**GOOD, "summary": "Authorization failed for the upload; PR 1695 waits on the unity-ci "
                                            "licence secrets (UNITY_LICENSE, UNITY_EMAIL)"},
    "CamelCase identifiers": {**GOOD, "summary": "MultiplayerDomainGamesController2 and "
                                                 "UnityPipelineReleaseGuard in Assets/_Scripts/Editor/Build"},
    "a build number": {**GOOD, "build": "412826", "version": "0.2.0",
                       "summary": "No Unity compile in CI since 2026-10-06; last success 2026-10-06T14:03:11Z"},
}

# Records that must be REFUSED. Each one is a shape a credential reaches a record in.
BAD_CASES = {
    "unknown field": {**GOOD, "token": "x"},
    "missing result": {k: v for k, v in GOOD.items() if k != "result"},
    "bad platform": {**GOOD, "platform": "macos"},
    "signed url": {**GOOD, "run_url": "https://r2.dev/x?X-Amz-Signature=abc"},
    "long token in summary": {**GOOD, "summary": "deploy ok " + "A" * 60},
    "github token in details": {**GOOD, "details": {"note": "ghp_" + "a" * 36}},
    "private key": {**GOOD, "summary": "-----BEGIN RSA PRIVATE KEY-----"},
    "jwt": {**GOOD, "summary": "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.sig"},
    "bad sha": {**GOOD, "commit": "not-a-sha"},
    "multi-line": {**GOOD, "summary": "a\nb"},
    "bad id": {**GOOD, "id": "Web Staging"},
    # added 2026-11-05 (ship-deep review)
    "user:pass@ in link": {**GOOD, "link": "https://user:pass@example.com/x"},
    "user:pass@ in summary": {**GOOD, "summary": "cloned https://x-access-token:abc123@github.com/o/r"},
    "token@ in a url": {**GOOD, "summary": "remote https://ghtoken123@github.com/o/r"},
    "authorization header": {**GOOD, "summary": "curl -H 'Authorization: token abc'"},
    "bearer credential": {**GOOD, "summary": "sent Bearer 4f9.Qx-7aa_b"},
    "basic credential": {**GOOD, "summary": "header Basic dXNlcjpwYXNzd29yZA=="},
    "credential assignment": {**GOOD, "details": {"note": "password=hunter2"}},
    "pem body split by spaces": {**GOOD, "summary": "key Qm9iIGlz IGEgc2Vj cmV0IGtl eSBmb3Ig dGhpcyBy ZXBv"},
    "pem body split by slashes": {**GOOD, "summary": "Qm9iIGlz/IGEgc2Vj/cmV0IGtl/eSBmb3Ig/dGhpcyBy"},
    "pem body after a stripped header": {**GOOD, "summary": "MIIEvQIBADANBgkqhkiG9w0BAQEFAASC"},
    "openssh key body": {**GOOD, "summary": "b3BlbnNzaC1rZXktdjEAAAAABG5vbmU"},
    "pem header without dashes": {**GOOD, "summary": "BEGIN OPENSSH PRIVATE KEY"},
    "token followed by a period": {**GOOD, "summary": "token was " + "Zx81Kq0LmPa9TfR2Wv7Yb3Nc5Hd6Jg8Ms1Ep4Ur7Ty0Wq" + "."},
    "random token in a version": {**GOOD, "build": "Zx81Kq0LmPa9TfR2Wv7Y"},
    "random token in a link path": {**GOOD, "link": "https://example.com/s/Zx81Kq0LmPa9TfR2Wv7Yb3"},
    "slack webhook link": {**GOOD, "link": "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXX"},
    "32-hex api key": {**GOOD, "summary": "steam key 0123456789ABCDEF0123456789ABCDEF"},
    "unity serial": {**GOOD, "summary": "serial SC-AB12-CD34-EF56-GH78-IJ90"},
    "aws key id": {**GOOD, "details": {"k": "AKIAABCDEFGHIJKLMNOP"}},
    "google api key": {**GOOD, "summary": "AIzaSyA1b2C3d4E5f6G7h8I9j0KlMnOpQrStUv"},
    "url query in summary": {**GOOD, "summary": "see https://example.com/x?id=1"},
    "trailing newline in id": {**GOOD, "id": "web-staging\n"},
    "trailing newline in url": {**GOOD, "run_url": "https://github.com/o/r/actions/runs/1\n"},
    "trailing newline in detail key": {**GOOD, "details": {"note\n": "x"}},
    "unicode line separator": {**GOOD, "summary": "a\u2028b"},
    "updated_at not utc": {**GOOD, "updated_at": "2026-11-05T20:00:00+02:00"},
    "updated_at date only": {**GOOD, "updated_at": "2026-11-05"},
    "schema true": {**GOOD, "schema": True},
    "nan detail": {**GOOD, "details": {"n": float("nan")}},
}


def _self_test_publish() -> list[str]:
    """Publish end to end against a throwaway bare remote: first record, a lost race and its retry, a refused
    record, a fatal remote; then prove nothing was left behind in the caller's repo."""
    problems: list[str] = []
    noop = lambda _s: None  # noqa: E731
    saved = {k: os.environ.get(k) for k in ("GIT_CONFIG_GLOBAL", "GIT_CONFIG_NOSYSTEM")}
    os.environ["GIT_CONFIG_GLOBAL"] = os.devnull  # the caller's own git config must not change the outcome
    os.environ["GIT_CONFIG_NOSYSTEM"] = "1"
    try:
        with tempfile.TemporaryDirectory(prefix="launch-status-selftest-") as tmp:
            root = Path(tmp)
            remote = root / "remote.git"
            _git(["init", "-q", "--bare", str(remote)], root)
            callers = []
            for name in ("a", "b"):
                path = root / name
                _git(["-c", "init.defaultBranch=main", "init", "-q", str(path)], root)
                _git(["remote", "add", "origin", str(remote)], path)
                callers.append(path)
            a, b = callers

            publish(GOOD, a, "origin", sleep=noop)
            ios = {**GOOD, "id": "ios-prerelease", "platform": "ios", "channel": "prerelease", "version": "0.2.0"}
            web_failed = {**GOOD, "result": "failure", "summary": "second web record"}
            raced = []

            def race(attempt: int) -> None:
                if attempt == 1:  # another publisher wins between our fetch and our push
                    publish(ios, b, "origin", sleep=noop)
                    raced.append(attempt)

            sleeps: list = []
            publish(web_failed, a, "origin", sleep=sleeps.append, before_push=race)
            if not raced or len(sleeps) != 1:
                problems.append(f"the race did not happen as staged (raced={raced}, retries={len(sleeps)})")

            def remote_file(path: str) -> str:
                return _git(["show", f"refs/heads/{BRANCH}:{path}"], remote).stdout

            board = json.loads(remote_file("status.json"))
            if sorted(board["components"]) != ["ios-prerelease", "web-staging"]:
                problems.append(f"lost a record in the race: board has {sorted(board['components'])}")
            elif board["components"]["web-staging"]["result"] != "failure":
                problems.append("the retried record did not replace the older one")
            if len(remote_file("history/web-staging.jsonl").splitlines()) != 2:
                problems.append("history/web-staging.jsonl should hold 2 records")
            if "README.md" not in _git(["ls-tree", "--name-only", f"refs/heads/{BRANCH}"], remote).stdout:
                problems.append("the first commit did not write the README")
            tip = _git(["rev-parse", f"refs/heads/{BRANCH}"], remote).stdout.strip()
            if _git(["rev-list", "--count", tip], remote).stdout.strip() != "3":
                problems.append("expected 3 linear status commits")
            if _git(["rev-list", "--max-parents=0", tip], remote).stdout.split() != \
                    _git(["rev-list", "--reverse", tip], remote).stdout.split()[:1]:
                problems.append("the branch should have exactly one root commit")

            try:
                publish({**GOOD, "summary": "Authorization: Bearer abc.def-123"}, a, "origin", sleep=noop)
                problems.append("publish accepted a record carrying an Authorization header")
            except SystemExit:
                pass
            fatal_sleeps: list = []
            try:
                publish(GOOD, a, str(root / "missing.git"), sleep=fatal_sleeps.append)
                problems.append("publish to a missing remote succeeded")
            except SystemExit:
                if fatal_sleeps:
                    problems.append("a fatal remote error was retried instead of stopping at once")
            if _git(["rev-parse", f"refs/heads/{BRANCH}"], remote).stdout.strip() != tip:
                problems.append("a refused publish still moved the branch")

            # A SPARSE caller (cone mode, sparse index; a CI job that checked out only Tools/Launch) must still
            # rebuild every component: its patterns must not reach the commit publish builds.
            sparse = root / "sparse"
            _git(["-c", "init.defaultBranch=main", "init", "-q", str(sparse)], root)
            for rel in ("Tools/Launch/x.txt", "components/other.txt", "history/other.txt", "Assets/y.txt"):
                (sparse / rel).parent.mkdir(parents=True, exist_ok=True)
                (sparse / rel).write_text("x\n")
            _git(["add", "-A"], sparse)
            _git(["-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "-m", "base"], sparse)
            _git(["sparse-checkout", "set", "--cone", "--sparse-index", "Tools/Launch"], sparse)
            _git(["remote", "add", "origin", str(remote)], sparse)
            heads_before = {sparse: _git(["for-each-ref", "--format=%(refname)", "refs/heads"], sparse).stdout.split()}
            android = {**GOOD, "id": "android-prerelease", "platform": "android", "channel": "prerelease"}
            publish(android, sparse, "origin", sleep=noop)
            board = json.loads(remote_file("status.json"))
            if sorted(board["components"]) != ["android-prerelease", "ios-prerelease", "web-staging"]:
                problems.append(f"a sparse caller dropped components: board has {sorted(board['components'])}")
            files = set(_git(["ls-tree", "-r", "--name-only", f"refs/heads/{BRANCH}"], remote).stdout.split())
            expected = {"README.md", "status.json", "components/web-staging.json", "components/ios-prerelease.json",
                        "components/android-prerelease.json", "history/web-staging.jsonl",
                        "history/ios-prerelease.jsonl", "history/android-prerelease.jsonl"}
            if files != expected:
                problems.append(f"a sparse caller changed the branch's files: {sorted(files ^ expected)}")
            callers.append(sparse)

            for caller in callers:
                heads = _git(["for-each-ref", "--format=%(refname)", "refs/heads"], caller).stdout.split()
                if heads != heads_before.get(caller, []):
                    problems.append(f"publish left local branches behind: {heads}")
                worktrees = _git(["worktree", "list", "--porcelain"], caller).stdout.count("worktree ")
                if worktrees != 1:
                    problems.append(f"publish left {worktrees - 1} worktree(s) behind")
                if (caller / ".git" / "shallow").exists():
                    problems.append("publish made the caller's repo shallow")
                status = _git(["status", "--porcelain", "--untracked-files=all"], caller).stdout
                if status.strip():
                    problems.append(f"publish touched the caller's working tree: {status.strip()[:80]}")
    finally:
        for k, v in saved.items():
            if v is None:
                os.environ.pop(k, None)
            else:
                os.environ[k] = v
    return problems


def self_test() -> int:
    wrongly_refused = {name: validate(rec) for name, rec in GOOD_CASES.items() if validate(rec)}
    wrongly_accepted = [name for name, rec in BAD_CASES.items() if not validate(rec)]
    failed = False
    if wrongly_refused:
        failed = True
        print("SELF-TEST FAILED: refused records it must accept:")
        for name, errs in wrongly_refused.items():
            print(f"  {name}: {'; '.join(errs)}")
    if wrongly_accepted:
        failed = True
        print("SELF-TEST FAILED: accepted records it must refuse: " + ", ".join(wrongly_accepted))
    board = board_of({"web-staging": GOOD})
    if board["components"]["web-staging"]["result"] != "success" or "web-staging" not in show(board):
        failed = True
        print("SELF-TEST FAILED: the board does not show a published record")
    problems = _self_test_publish()
    if problems:
        failed = True
        print("SELF-TEST FAILED: publish end to end:\n  " + "\n  ".join(problems))
    if failed:
        return 1
    print(f"self-test OK: {len(GOOD_CASES)} good records accepted, {len(BAD_CASES)} bad records refused; "
          "publish end to end (first record, lost race + retry, refusal, fatal remote) left no branch, worktree "
          "or file behind")
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
            here = Path.cwd()
            fetched = _git(["fetch", "-q", "--no-tags", args.remote, f"refs/heads/{BRANCH}"], here, check=False)
            if fetched.returncode != 0:
                print(f"cannot read {BRANCH} from {args.remote}: {scrub(fetched.stderr)}", file=sys.stderr)
                return 1
            board = json.loads(_git(["show", "FETCH_HEAD:status.json"], here).stdout)
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
    try:
        publish(record, Path(args.repo), args.remote)
    except GitError as exc:
        print(f"publish failed: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
