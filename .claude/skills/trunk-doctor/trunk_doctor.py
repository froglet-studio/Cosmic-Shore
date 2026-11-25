#!/usr/bin/env python3
"""trunk_doctor.py - is bleeding-edge green, and if not, exactly why and who broke it.

The reader behind /trunk-doctor (.claude/skills/trunk-doctor/SKILL.md). It reads the
"Bleeding-edge guard" workflow's runs on bleeding-edge (.github/workflows/bleeding-edge-guard.yml)
and the "bleeding-edge is broken" tracking issues through the GitHub API, pulls the failing jobs'
logs, and classifies each failure. READ-ONLY: it never pushes, re-runs, comments or edits. The one
exception is `bisect`, which checks commits out into a throwaway git worktree under the cache
directory (never the working tree) and removes it.

    python3 trunk_doctor.py                     # status (default): green or red, the runs, first red, pushes since
    python3 trunk_doctor.py --gate              # exit 0 green, 2 red, 3 unknown; for /ship and other skills
    python3 trunk_doctor.py diagnose            # classify the newest red run, name the push that introduced each finding
    python3 trunk_doctor.py diagnose --run <id> # ... a given run (or --issue <n>: the run a tracking issue names)
    python3 trunk_doctor.py diagnose --log <f>  # classify a saved job log, offline
    python3 trunk_doctor.py history [--pages N] [--diagnose]   # every red streak, as TRUNK_HEALTH.md rows
    python3 trunk_doctor.py bisect --good <sha> --bad <sha> --cmd "<check>"   # first-parent bisect, local
    python3 trunk_doctor.py --self-test         # fixture logs and runs for every failure class; no network

Failure classes (what `diagnose` prints):
    static     a static-validation check failed: which check, which file and line
    generator  an author_*_assets.py --check is red: drift (assets differ) or crash (it could not run)
    compile    the Unity compile: each `error CS` with file and line
    tests      edit-mode tests failed; the project compiled
    infra      runner lost, licence, network, disk, timeout: no code fix, re-run it

The guard's own report job reads a CANCELLED run (superseded by a newer push) as green; this
script never does: a cancelled run has no verdict and is skipped when deciding green or red.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path

REPO = "froglet-studio/Cosmic-Shore"
WORKFLOW = "bleeding-edge-guard.yml"
BRANCH = "bleeding-edge"
ISSUE_LABEL = "ci-bleeding-edge"
HERE = Path(__file__).resolve().parent
FIXTURES = HERE / "fixtures"
ROOT = HERE.parent.parent.parent
CACHE = Path(os.environ.get("XDG_CACHE_HOME", Path.home() / ".cache")) / "trunk_doctor"

EXIT_GREEN, EXIT_RED, EXIT_UNKNOWN = 0, 2, 3


# --------------------------------------------------------------------------------------------
# GitHub API: `gh api` when it is installed and signed in, else the public REST API (the repo is
# public; GITHUB_TOKEN is used when set). Job logs need a signed-in caller either way.
# --------------------------------------------------------------------------------------------
class ApiError(RuntimeError):
    pass


def _gh_ok() -> bool:
    return shutil.which("gh") is not None


def api(path: str, raw: bool = False):
    if _gh_ok():
        r = subprocess.run(["gh", "api", "-X", "GET", path], capture_output=True, text=True)
        if r.returncode == 0:
            return r.stdout if raw else json.loads(r.stdout or "null")
        err = (r.stderr or r.stdout).strip()
        if raw or "Not Found" in err or "HTTP 4" in err:
            raise ApiError(f"gh api {path}: {err[:300]}")
    url = path if path.startswith("http") else f"https://api.github.com/{path}"
    req = urllib.request.Request(url, headers={"Accept": "application/vnd.github+json", "User-Agent": "trunk-doctor"})
    tok = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if tok:
        req.add_header("Authorization", f"Bearer {tok}")
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            body = resp.read().decode("utf-8", "replace")
    except (urllib.error.URLError, TimeoutError, OSError) as e:
        raise ApiError(f"GET {url}: {e}") from e
    return body if raw else json.loads(body or "null")


# --------------------------------------------------------------------------------------------
# Runs and verdicts
# --------------------------------------------------------------------------------------------
PR_RE = re.compile(r"Merge pull request #(\d+) from [\w.-]+/(\S+)|\(#(\d+)\)\s*$")


@dataclass
class Run:
    id: int
    number: int
    sha: str
    status: str
    conclusion: str | None
    created: str
    actor: str
    title: str
    url: str = ""

    @property
    def verdict(self) -> str:
        """green | red | none. Cancelled, skipped and in-progress runs carry no verdict."""
        if self.status != "completed":
            return "none"
        if self.conclusion == "success":
            return "green"
        if self.conclusion in ("failure", "timed_out", "startup_failure"):
            return "red"
        return "none"

    @property
    def pr(self) -> int | None:
        m = PR_RE.search(self.title)
        if not m:
            return None
        return int(m.group(1) or m.group(3))

    @property
    def head_branch(self) -> str | None:
        m = PR_RE.search(self.title)
        return m.group(2) if m and m.group(2) else None

    @staticmethod
    def from_api(d: dict) -> "Run":
        msg = ((d.get("head_commit") or {}).get("message") or d.get("display_title") or "").split("\n")[0]
        return Run(d["id"], d.get("run_number", 0), d["head_sha"], d["status"], d.get("conclusion"),
                   d["created_at"], (d.get("actor") or {}).get("login", "?"), msg, d.get("html_url", ""))


def fetch_runs(pages: int = 1, per_page: int = 50) -> list[Run]:
    out: list[Run] = []
    for p in range(1, pages + 1):
        d = api(f"repos/{REPO}/actions/workflows/{WORKFLOW}/runs?branch={BRANCH}&event=push&per_page={per_page}&page={p}")
        batch = [Run.from_api(r) for r in d.get("workflow_runs", [])]
        out.extend(batch)
        if len(batch) < per_page:
            break
    return out  # newest first


@dataclass
class Trunk:
    state: str                 # green | red | unknown
    newest: Run | None
    first_red: Run | None = None
    last_green: Run | None = None
    since: list[Run] = field(default_factory=list)    # every push from first_red to newest, oldest first
    in_progress: list[Run] = field(default_factory=list)


def analyse(runs: list[Run]) -> Trunk:
    """runs newest first. The state is the newest run WITH a verdict; a red streak runs back to the
    first red after the last green, skipping runs with no verdict (cancelled, in progress)."""
    if not runs:
        return Trunk("unknown", None)
    in_prog = [r for r in runs if r.status != "completed"]
    verdict_runs = [r for r in runs if r.verdict != "none"]
    if not verdict_runs:
        return Trunk("unknown", runs[0], in_progress=in_prog)
    newest = verdict_runs[0]
    if newest.verdict == "green":
        return Trunk("green", newest, last_green=newest, in_progress=in_prog)
    first_red, last_green = newest, None
    for r in verdict_runs[1:]:
        if r.verdict == "red":
            first_red = r
        else:
            last_green = r
            break
    idx = runs.index(first_red)
    since = list(reversed(runs[: idx + 1]))
    return Trunk("red", newest, first_red, last_green, since, in_prog)


# --------------------------------------------------------------------------------------------
# People: a GitHub login or a git name -> the team member (Docs/TEAM.json)
# --------------------------------------------------------------------------------------------
_TEAM: dict[str, str] | None = None


def person(login: str | None) -> str:
    global _TEAM
    if not login:
        return "?"
    if _TEAM is None:
        _TEAM = {}
        try:
            team = json.loads((ROOT / "Docs" / "TEAM.json").read_text())
            for p in team.get("people", []):
                for n in p.get("names", []) + [p["name"]]:
                    _TEAM[n.lower()] = p["name"]
                for e in p.get("emails", []):
                    m = re.match(r"\d+\+([^@]+)@users\.noreply", e)
                    if m:
                        _TEAM[m.group(1).lower()] = p["name"]
                    else:   # a login often equals an email's local part (gradies@gmail.com -> gradies)
                        _TEAM.setdefault(e.split("@")[0].lower(), p["name"])
        except (OSError, ValueError):
            pass
    who = _TEAM.get(login.lower())
    return f"{who} ({login})" if who and who.lower() != login.lower() else (who or login)


_PR_CACHE: dict[int, dict] = {}


def pr_info(n: int) -> dict:
    if n not in _PR_CACHE:
        try:
            d = api(f"repos/{REPO}/pulls/{n}")
            _PR_CACHE[n] = {"author": (d.get("user") or {}).get("login"), "merged_by": (d.get("merged_by") or {}).get("login"),
                            "title": d.get("title", ""), "branch": (d.get("head") or {}).get("ref"), "url": d.get("html_url")}
        except ApiError:
            _PR_CACHE[n] = {}
    return _PR_CACHE[n]


def pr_files(n: int) -> list[str]:
    files: list[str] = []
    for p in range(1, 11):
        try:
            batch = api(f"repos/{REPO}/pulls/{n}/files?per_page=100&page={p}")
        except ApiError:
            break
        files += [f["filename"] for f in batch]
        if len(batch) < 100:
            break
    return files


def commit_files(sha: str) -> list[str]:
    try:
        return [f["filename"] for f in api(f"repos/{REPO}/commits/{sha}").get("files", [])]
    except ApiError:
        return []


def push_line(r: Run, with_pr: bool = True) -> str:
    when = r.created.replace("T", " ")[:16]
    v = {"green": "green", "red": "RED  ", "none": "running" if r.status != "completed" else (r.conclusion or "?")[:9]}[r.verdict]
    pr = r.pr
    who = f"pushed by {person(r.actor)}"
    extra = ""
    if pr and with_pr:
        info = pr_info(pr)
        if info.get("author") and info["author"] != r.actor:
            extra = f", PR by {person(info['author'])}"
        extra += f", {info.get('branch') or r.head_branch or ''}"
    head = f"#{pr}" if pr else r.sha[:10]
    return f"  run {r.number:<4} {v:<9} {when}  {r.sha[:10]}  {head:<6} {who}{extra}"


def duration(a: str, b: str | None = None) -> str:
    t0 = dt.datetime.fromisoformat(a.replace("Z", "+00:00"))
    t1 = dt.datetime.fromisoformat(b.replace("Z", "+00:00")) if b else dt.datetime.now(dt.timezone.utc)
    s = int((t1 - t0).total_seconds())
    d, h, m = s // 86400, s % 86400 // 3600, s % 3600 // 60
    return f"{d}d {h}h {m}m" if d else f"{h}h {m}m"


def open_issue() -> dict | None:
    d = api(f"repos/{REPO}/issues?state=open&labels={ISSUE_LABEL}&per_page=1")
    return d[0] if d else None


# --------------------------------------------------------------------------------------------
# Log classification
# --------------------------------------------------------------------------------------------
TS_RE = re.compile(r"^﻿?\d{4}-\d\d-\d\dT[\d:.]+Z ?")
ECHO = "\x1b[36;1m"      # GitHub prints a step's script in cyan before running it; never a finding
ANSI_RE = re.compile(r"\x1b\[[0-9;]*m")

INFRA = [
    ("licence", re.compile(r"No valid Unity Editor license|'com\.unity\.editor\.headless' was not found|"
                           r"License (?:activation|return) failed|[Ss]erial (?:number|key) is invalid")),
    ("runner lost", re.compile(r"runner has received a shutdown signal|the job failed with no failed step|lost communication with the server|"
                               r"The self-hosted runner .* did not connect|Runner .* (?:went|is) offline")),
    ("timeout", re.compile(r"has exceeded the maximum execution time")),
    ("network", re.compile(r"Could not resolve host|fatal: unable to access|Failed to connect to [\w.]*github|"
                           r"ECONNRESET|ETIMEDOUT|error: RPC failed|TLS handshake timeout|"
                           r"50[234] (?:Bad Gateway|Service Unavailable|Gateway Time)|API rate limit exceeded")),
    ("disk", re.compile(r"No space left on device")),
    ("runner setup", re.compile(r"UNITY_PATH is not (?:set|executable)")),
]
CS_RE = re.compile(r"(?P<file>[A-Za-z0-9_][^():\n]*?\.cs)\((?P<line>\d+),(?P<col>\d+)\): error (?P<code>CS\d+): (?P<msg>.+)")
PATH_RE = re.compile(r"((?:Assets|Packages|Port|Tools|ProjectSettings|Docs|\.github|\.claude)/[^\s:()'\",]+)"
                     r"(?::(\d+)|\((\d+),\d+\))?")
TEST_FAIL_RE = re.compile(r"^\s*(?:Failed\s*:|FAILED\s*:|\[FAIL\]|Failed)\s+([A-Za-z_][\w.`+<>\[\], -]*?)\s*(?:\[\d.*)?$")
EXC_RE = re.compile(r"^(\w+(?:Error|Exception|Exit)): (.+)$")
DRIFT_HINT = re.compile(r"DRIFT|FAIL|differs|mismatch|VALIDATION FAILED|would (?:change|write)|stale|not match|"
                        r"\b\d+ drifted|finding", re.I)


@dataclass
class Finding:
    kind: str           # static | generator | compile | tests | infra
    check: str          # the check (script, generator, CS code, infra cause)
    file: str = ""
    line: int | None = None
    detail: str = ""
    cause: str = ""     # generator: drift | crash | missing-module:<name>; static: false-positive? ...

    @property
    def key(self) -> str:
        if self.kind == "compile":
            return f"compile:{self.file}"
        if self.kind == "generator":
            return f"generator:{self.check}"
        if self.kind == "static":
            return f"static:{self.check}:{self.file}"
        return f"{self.kind}:{self.check}"

    def text(self) -> str:
        loc = f"{self.file}:{self.line}" if self.file and self.line else self.file
        bits = [f"[{self.kind}]", self.check]
        if loc:
            bits.append(f"at {loc}")
        if self.cause:
            bits.append(f"({self.cause})")
        s = " ".join(bits)
        return s + (f"\n      {self.detail}" if self.detail else "")


def clean_lines(text: str) -> list[str]:
    out = []
    for raw in text.splitlines():
        line = TS_RE.sub("", raw)
        out.append(line)
    return out


def split_steps(lines: list[str]) -> list[tuple[str, list[str]]]:
    """[(command, lines)] per `##[group]Run ...` step. The command is the first echoed script line
    that is not a `set -...` or a comment, else the group header."""
    steps: list[tuple[str, list[str]]] = []
    cur_head, cur = "(job setup)", []
    for line in lines:
        if line.startswith("##[group]Run "):
            steps.append((cur_head, cur))
            cur_head, cur = line[len("##[group]Run "):].strip(), []
            continue
        cur.append(line)
    steps.append((cur_head, cur))
    named = []
    for head, body in steps:
        cmds = []
        for b in body:
            if b.startswith(ECHO):
                s = ANSI_RE.sub("", b).strip()
                if s.startswith(("python3 ", "bash ", "python ", "dotnet ", "node ")):
                    cmds.append(s)
                elif s and not s.startswith(("set ", "#")) and not cmds and "=" in s.split(" ")[0]:
                    cmds.append(s)       # a script that starts with an assignment (the Unity step)
        cmd = " ; ".join(cmds) if cmds else head
        named.append((cmd[:200], body))
    return named


def _real(lines: list[str]) -> list[str]:
    return [ANSI_RE.sub("", l) for l in lines if not l.startswith(ECHO)]


def _generator_findings(body: list[str]) -> list[Finding]:
    red = [l.split("RED", 1)[1].strip() for l in body if re.match(r"^\s+RED\s+\S", l)]
    if not red and not any(re.match(r"^generators: ", l) for l in body):
        # The inline `for f in author_*; do python3 $f --check; done` loop (before 2026-11-01) ran
        # under set -e and stopped at the first red one: the last header is the generator that failed.
        heads = [re.match(r"^== (\S+)", l).group(1) for l in body if re.match(r"^== \S+", l)]
        red = heads[-1:]
    sections: dict[str, list[list[str]]] = {}
    cur = None
    for l in body:
        m = re.match(r"^== (\S+)", l)
        if m:
            cur = m.group(1)
            sections.setdefault(cur, []).append([])
            continue
        if re.match(r"^generators: ", l):
            cur = None
        if cur:
            sections[cur][-1].append(l)
    out = []
    for gen in red:
        path = gen.split(" ")[0]
        secs = sections.get(path)
        if not secs:
            out.append(Finding("generator", Path(path).name, path, None, "its output is not in this log", "unknown: read the step"))
            continue
        sec = next((s for s in secs if any("Traceback" in x or DRIFT_HINT.search(x) for x in s)), secs[-1])
        if any("Traceback" in x for x in sec):
            exc = next((EXC_RE.match(x.strip()) for x in reversed(sec) if EXC_RE.match(x.strip())), None)
            msg = exc.group(0) if exc else "Traceback (no exception line)"
            mod = re.search(r"No module named '([\w.]+)'", msg)
            where = next((x.strip() for x in sec if x.strip().startswith('File "')), "")
            wf = re.search(r'File "[^"]*?/((?:Tools|Assets)/[^"]+)", line (\d+)', where) if where else None
            # the deepest frame names the file that raised
            frames = [re.search(r'File "[^"]*?/((?:Tools|Assets)/[^"]+)", line (\d+)', x) for x in sec]
            frames = [f for f in frames if f]
            last = frames[-1] if frames else wf
            out.append(Finding("generator", Path(path).name, last.group(1) if last else path,
                               int(last.group(2)) if last else None, msg,
                               f"crash: missing module {mod.group(1)} on the runner" if mod else "crash"))
        else:
            hits = [x.strip() for x in sec if x.strip() and not x.startswith("note:")]
            detail = "; ".join(hits[:4])
            out.append(Finding("generator", Path(path).name, path, None, detail[:400], "drift"))
    if not red and any("arcade_mode_lib" in l and "FAIL" in l for l in body):
        out.append(Finding("generator", "arcade_mode_lib.py --self-test", "Tools/Build/arcade_mode_lib.py", None, "", "self-test failed"))
    return out


def _static_findings(cmd: str, body: list[str]) -> list[Finding]:
    real = _real(body)
    out: list[Finding] = []
    for i, l in enumerate(real):
        if not l.startswith("##[error]") or "Process completed with exit code" in l:
            continue
        msg = l[len("##[error]"):].strip()
        m = None
        for back in range(0, 4):
            if i - back < 0:
                break
            m = PATH_RE.search(real[i - back])
            if m:
                break
        f = Finding("static", cmd, m.group(1) if m else "", int(m.group(2) or m.group(3)) if m and (m.group(2) or m.group(3)) else None, msg[:300])
        if f.file and not f.file.startswith(("Assets/", "Packages/", "ProjectSettings/")) and "editor-in-runtime" in msg:
            f.cause = "likely checker false positive: this path never reaches a player build"
        out.append(f)
    if not out:
        end = next((i for i, l in enumerate(real) if l.startswith("##[error]Process completed")), len(real))
        tail = [l.strip() for l in real[max(0, end - 15):end] if l.strip()]
        located = [l for l in tail if (m := PATH_RE.search(l)) and (m.group(2) or m.group(3))
                   and not re.search(r"^(ok|PASS)\b", l)]
        hits = located or [l for l in tail if re.search(r"\b(FAIL(ED)?|ERROR|PROBLEMS?|error:?)\b|^FAIL ", l)
                           and not re.search(r"^(ok|PASS)\b", l)]
        for h in hits[:6] or ["(no message: read the step's log)"]:
            m = PATH_RE.search(h)
            out.append(Finding("static", cmd, m.group(1) if m else "", int(m.group(2)) if m and m.group(2) else None, h[:300]))
    return out


def _is_unity_step(cmd: str, body: list[str]) -> bool:
    """The guard's Unity compile step: its compile, test and infra lines are read from the whole log."""
    script = " ".join([cmd] + [l for l in body if l.startswith(ECHO)])
    return any(k in script for k in ("-runTests", "-batchmode", "UNITY_PATH", "set +e"))


def classify(text: str, job: str = "") -> list[Finding]:
    """Every finding in one failed job's log."""
    lines = clean_lines(text)
    real_all = _real(lines)
    findings: list[Finding] = []

    cs = {}
    for l in real_all:
        m = CS_RE.search(l)
        if m:
            f = m.group("file").strip().replace("\\", "/")
            cs.setdefault((f, int(m.group("line")), m.group("code")), m.group("msg").strip())
    for (f, ln, code), msg in cs.items():
        findings.append(Finding("compile", code, f, ln, msg[:300]))

    if not cs and any("Scripts have compiler errors" in l for l in real_all):
        findings.append(Finding("compile", "compile errors", detail="Unity: 'Scripts have compiler errors'; the job log lists "
                                "no `error CS` line - read unity-editmode.log in the run's editmode-results artifact"))

    tests_failed = any("Edit-mode tests failed" in l for l in real_all if l.startswith("##[error]")) or \
        any("One or more tests failed" in l for l in real_all)
    if tests_failed and not any(f.kind == "compile" for f in findings):
        names = []
        for l in real_all:
            m = TEST_FAIL_RE.match(l)
            if m and m.group(1) not in names:
                names.append(m.group(1))
        for n in names or ["(test names: read editmode-results.xml in the run's artifact)"]:
            findings.append(Finding("tests", n))

    for cmd, body in split_steps(lines):
        if not any(l.startswith("##[error]Process completed with exit code") for l in body):
            continue
        if "check_generators" in cmd or any(re.match(r"^generators: \d+ RED", l) for l in body) or \
                ("author_" in (s := " ".join(l for l in body if l.startswith(ECHO))) and "--check" in s):
            findings += _generator_findings(body)
        elif _is_unity_step(cmd, body):
            continue  # the Unity step: compile/tests handled above, infra below
        else:
            findings += _static_findings(cmd, body)

    if not any(f.kind in ("compile", "tests", "generator", "static") for f in findings):
        for cause, rx in INFRA:
            hit = next((l for l in real_all if rx.search(l)), None)
            if hit:
                findings.append(Finding("infra", cause, detail=hit.strip()[:300]))
        if not findings and any("did not compile" in l for l in real_all if l.startswith("##[error]")):
            # The guard says "did not compile" whenever there is no results XML. With no `error CS`
            # line that is NOT a compile error: Unity never got that far (#1025 was a licence).
            findings.append(Finding("infra", "unity did not start", detail="the guard reported 'did not compile' but the log has no `error CS` line"))
    if not findings:
        findings.append(Finding("unknown", job or "job", detail="no known failure pattern in the log; read it"))
    return findings


PRIMARY_ORDER = ["compile", "generator", "static", "tests", "infra", "unknown"]


def primary(findings: list[Finding]) -> str:
    kinds = {f.kind for f in findings}
    return next(k for k in PRIMARY_ORDER if k in kinds)


def advice(findings: list[Finding]) -> list[str]:
    """What /trunk-doctor §3 decides from, per finding class. Guidance; the session decides."""
    out = []
    kinds = {f.kind for f in findings}
    for f in findings:
        if f.kind == "generator" and f.cause.startswith("crash: missing module"):
            out.append(f"FIX FORWARD: {f.check} cannot run on the runner ({f.detail}). The check is not wrong; the "
                       "runner lacks a dependency. Give the guard's validate job that dependency (a setup step), never "
                       "a skip in the generator. Reproduce with a Python that lacks it.")
        elif f.kind == "generator" and f.cause == "drift":
            out.append(f"{f.check}: drift. If the introducing PR changed the GENERATOR and simply did not re-run it, fix "
                       "forward by re-running it (`python3 Tools/Build/" + f.check + "`) and committing its output. If it "
                       "changed the ASSETS by hand (or a schema the generator has not taken), the generator's owner "
                       "decides: revert the PR.")
        elif f.kind == "generator":
            out.append(f"{f.check}: crashed ({f.detail}). Read the traceback; fix forward only if it is a one-line import/path fix.")
        elif f.kind == "static" and "false positive" in f.cause:
            out.append(f"{f.check} flags {f.file}: {f.cause}. A scoped, explained checker fix (as #1069 did for Port/), "
                       "with a negative control proving the real case still fails. Never a threshold or a skip.")
    if "compile" in kinds:
        cs = [f for f in findings if f.kind == "compile"]
        files = {f.file for f in cs}
        simple = all(f.check in ("CS0246", "CS0234", "CS0103", "CS0117", "CS0176", "CS1061") for f in cs)
        if len(files) <= 2 and simple:
            out.append(f"COMPILE: {len(cs)} error(s) in {len(files)} file(s), missing-name class. Fix forward if the "
                       "missing type/member/using is obvious from the introducing PR; otherwise revert it.")
        else:
            out.append(f"COMPILE: {len(cs)} error(s) in {len(files)} file(s). Revert the introducing PR unless the fix is one line.")
    if "static" in kinds and not any("false positive" in f.cause for f in findings if f.kind == "static"):
        out.append("STATIC: fix forward when it is one of the guard's mechanical classes (editor API in player code, "
                   "a missing scene in the build settings, a .meta); else revert.")
    if "tests" in kinds:
        out.append("TESTS: the project compiled. Revert the PR that turned them red, or fix the test only when the "
                   "test itself is stale. Never change gameplay code to make an assertion pass.")
    if kinds == {"infra"}:
        out.append("INFRA: no code change. Re-run the failed jobs (`gh run rerun <id> --failed`) and record the note.")
    merged: list[str] = []
    for a in out:                      # one line per cause: three generators missing numpy is one fix
        generic = re.sub(r"^FIX FORWARD: \S+ cannot run", "FIX FORWARD: <generator> cannot run", a)
        if generic != a and any(m.startswith("FIX FORWARD: ") and generic.split("(", 1)[1] == m.split("(", 1)[1] for m in merged):
            i = next(i for i, m in enumerate(merged) if m.split("(", 1)[1] == generic.split("(", 1)[1])
            merged[i] = merged[i].replace(" cannot run", ", " + a.split()[2] + " cannot run", 1)
            continue
        merged.append(a)
    return merged


# --------------------------------------------------------------------------------------------
# Logs of a run
# --------------------------------------------------------------------------------------------
_JOBS: dict[int, list[dict]] = {}


def run_jobs(run_id: int) -> list[dict]:
    if run_id not in _JOBS:
        _JOBS[run_id] = api(f"repos/{REPO}/actions/runs/{run_id}/jobs?per_page=50").get("jobs", [])
    return _JOBS[run_id]


def run_logs(run_id: int) -> list[tuple[str, str]]:
    """[(job name, log text)] for every failed job of a run. Cached under CACHE."""
    jobs = run_jobs(run_id)
    out = []
    for j in jobs:
        if j.get("conclusion") not in ("failure", "timed_out"):
            continue
        p = CACHE / f"{run_id}-{j['id']}.log"
        if p.exists():
            text = p.read_text(errors="replace")
        else:
            try:
                text = api(f"repos/{REPO}/actions/jobs/{j['id']}/logs", raw=True)
            except ApiError as e:
                text = f"(log unavailable: {e})"
            if not text.startswith("(log unavailable"):
                CACHE.mkdir(parents=True, exist_ok=True)
                p.write_text(text)
        if not text.startswith("(log unavailable") and not any(
                s.get("conclusion") == "failure" for s in j.get("steps", [])):
            text += "\n##[error]trunk-doctor: the job failed with no failed step (the log stops: the runner was lost or the job killed)\n"
        out.append((j["name"], text))
    return out


def check_ran(run_id: int, f: Finding) -> bool:
    """Did this run actually run the check behind `f`? A red run that does not show a finding
    proves nothing when the check was SKIPPED behind an earlier failure (the guard's generator step
    has no `if: !cancelled()`, so a failing static check skips it; a failing validate job skips
    the Unity compile)."""
    jobs = run_jobs(run_id)
    if f.kind in ("compile", "tests"):
        return any(j["name"].startswith("Unity") and j.get("conclusion") in ("success", "failure") for j in jobs)
    val = next((j for j in jobs if j["name"] == "Static validation"), None)
    if not val or val.get("conclusion") == "success":
        return bool(val)
    text = "\n".join(t for n, t in run_logs(run_id) if n == "Static validation")
    if f.kind == "generator":
        # the sweep's summary line means it ran every generator that existed then; a generator that
        # did not exist yet was not broken yet
        return f"== Tools/Build/{f.check}" in text or bool(re.search(r"^\S* ?generators: ", text, re.M))
    scripts = [c.split()[1] for c in f.check.split(" ; ") if len(c.split()) > 1]
    return any(sc in l for sc in scripts for l in text.splitlines() if ECHO in l)


def diagnose_run(run_id: int) -> list[Finding]:
    findings: list[Finding] = []
    for name, text in run_logs(run_id):
        if text.startswith("(log unavailable"):
            findings.append(Finding("unknown", name, detail=text))
        else:
            findings += classify(text, name)
    return findings


def runs_around(run: Run, pages: int = 6) -> list[Run]:
    """Guard runs on bleeding-edge from a little after `run` back to the last green before it."""
    created = run.created[:10]
    start = (dt.date.fromisoformat(created) - dt.timedelta(days=10)).isoformat()
    end = (dt.date.fromisoformat(created) + dt.timedelta(days=1)).isoformat()
    out: list[Run] = []
    for p in range(1, pages + 1):
        d = api(f"repos/{REPO}/actions/workflows/{WORKFLOW}/runs?branch={BRANCH}&event=push&per_page=100&page={p}"
                f"&created={start}..{end}")
        batch = [Run.from_api(r) for r in d.get("workflow_runs", [])]
        out += batch
        if len(batch) < 100:
            break
    return out


def attribute(target: Run, runs: list[Run], findings: list[Finding], trace: bool = True) -> list[str]:
    """For each finding, the push that introduced it: trace back through the red runs before
    `target` to the earliest that still shows it, then name the pushes between that run and the
    verdict run before it, narrowed by which pushes touched the files involved."""
    lines: list[str] = []
    idx = runs.index(target) if target in runs else 0
    older = runs[idx + 1:]
    by_key: dict[str, list[Finding]] = {}
    for f in findings:
        by_key.setdefault(f.key, []).append(f)
    for key, fs in by_key.items():
        f = fs[0]
        if f.kind in ("infra", "unknown"):
            continue
        first_seen = target
        prev_verdict = None
        hidden = []
        for r in older:
            if r.verdict == "none":
                continue
            if r.verdict == "green" or not trace:
                prev_verdict = r
                break
            keys = {g.key for g in diagnose_run(r.id)}
            if key in keys:
                first_seen = r
            elif not check_ran(r.id, f):
                hidden.append(r)          # skipped behind another failure: no evidence either way
            else:
                prev_verdict = r
                break
        # The candidates below run from first_seen back to prev_verdict, so the hidden runs' pushes are
        # suspects with the rest; the line printed next says why.
        a = runs.index(first_seen)
        b = runs.index(prev_verdict) if prev_verdict in runs else len(runs)
        candidates = list(reversed(runs[a:b]))           # oldest first: the pushes no verdict separated
        lines.append(f"- {f.check}{' at ' + f.file if f.file else ''}: first red in run {first_seen.number} "
                     f"({first_seen.created[:16].replace('T', ' ')}); last verdict before it: "
                     + (f"run {prev_verdict.number} {prev_verdict.verdict} {prev_verdict.sha[:10]}" if prev_verdict else "older than the runs read")
                     + (f"; runs {', '.join(str(h.number) for h in hidden)} never ran this check (skipped behind another "
                        "failure), so their pushes are suspects too" if hidden else ""))
        involved = {g.file for g in fs if g.file} | ({f"Tools/Build/{f.check}"} if f.kind == "generator" else set())
        stems = {Path(p).stem.replace("author_", "").replace("_assets", "") for p in involved}
        touched = []
        for r in candidates:
            files = pr_files(r.pr) if r.pr else commit_files(r.sha)
            hit = sorted((x for x in files if x in involved or any(s and s.lower() in x.lower() for s in stems)),
                         key=lambda x: (not x.startswith("Tools/"), not x.endswith((".py", ".cs")), x))
            touched.append((r, hit))
        named = [t for t in touched if t[1]]
        for r, hit in touched:
            mark = "<= touched " + ", ".join(hit[:4]) + (" ..." if len(hit) > 4 else "") if hit else ""
            lines.append(push_line(r) + (f"   {mark}" if mark else ""))
        if len(candidates) == 1:
            lines.append(f"  INTRODUCED BY: {_name(candidates[0])}")
        elif len(named) == 1:
            lines.append(f"  INTRODUCED BY (the one push in range that touched it): {_name(named[0][0])}")
        else:
            lines.append(f"  {len(candidates)} pushes share this range with no verdict between them: bisect "
                         f"(`trunk_doctor.py bisect --good {prev_verdict.sha[:10] if prev_verdict else '<sha>'} "
                         f"--bad {first_seen.sha[:10]} --cmd '<the failing check>'`)")
    return lines


def _name(r: Run) -> str:
    if r.pr:
        info = pr_info(r.pr)
        by = info.get("author")
        return (f"#{r.pr} ({info.get('branch') or r.head_branch}) by {person(by)}, merged by {person(info.get('merged_by') or r.actor)}"
                f", {r.sha[:10]}  {info.get('url') or ''}")
    return f"{r.sha[:10]} pushed by {person(r.actor)}: {r.title}"


# --------------------------------------------------------------------------------------------
# Commands
# --------------------------------------------------------------------------------------------
def cmd_status(args) -> int:
    try:
        runs = fetch_runs(pages=args.pages)
        t = analyse(runs)
        # a red streak may start beyond the first page
        p = args.pages
        while t.state == "red" and t.last_green is None and p < 8 and len(runs) >= 50 * p:
            p += 1
            runs = fetch_runs(pages=p)
            t = analyse(runs)
    except ApiError as e:
        print(f"bleeding-edge: UNKNOWN - the GitHub API could not be read ({e}).")
        print(f"Look by hand: https://github.com/{REPO}/actions/workflows/{WORKFLOW}?query=branch%3A{BRANCH}")
        return EXIT_UNKNOWN
    try:
        issue = open_issue()
    except ApiError:
        issue = None

    if args.gate:
        return gate(t, issue, args)

    if t.state == "unknown":
        print("bleeding-edge: UNKNOWN - no guard run on bleeding-edge has a verdict yet.")
        return EXIT_UNKNOWN
    head = t.newest
    if t.state == "green":
        print(f"bleeding-edge: GREEN - run {head.number} on {head.sha[:10]} ({head.created[:16].replace('T', ' ')} UTC) passed.")
    else:
        print(f"bleeding-edge: RED since {t.first_red.created[:16].replace('T', ' ')} UTC "
              f"({duration(t.first_red.created)}), {len(t.since)} push(es) since the first red one.")
    if t.in_progress:
        print(f"  in progress: run {t.in_progress[0].number} on {t.in_progress[0].sha[:10]} (its verdict is not in yet)")
    if issue:
        print(f"Tracking issue: #{issue['number']} open since {issue['created_at'][:16].replace('T', ' ')} "
              f"({issue['comments']} comments) {issue['html_url']}")
    elif t.state == "red":
        print("Tracking issue: NONE open although the trunk is red. The guard's report job closes it on a CANCELLED run "
              "(a newer push superseded it) as if the trunk had recovered; read the runs, not the issue.")
    else:
        print("Tracking issue: none open.")

    print(f"\nLatest guard runs on {BRANCH} (newest first; cancelled = superseded, no verdict):")
    for r in runs[: args.show]:
        print(push_line(r, with_pr=False) + f"  {r.title[:70]}")
    if t.state == "red":
        print(f"\nFirst red:  {_name(t.first_red)}  {t.first_red.url}")
        if t.last_green:
            print(f"Last green: run {t.last_green.number}, {t.last_green.sha[:10]} "
                  f"({t.last_green.created[:16].replace('T', ' ')} UTC)  {t.last_green.url}")
        print(f"\nEvery push since the first red one (oldest first), {len(t.since)}:")
        for r in t.since:
            print(push_line(r))
        people: dict[str, int] = {}
        for r in t.since[1:]:
            people[person(r.actor)] = people.get(person(r.actor), 0) + 1
        if people:
            print("Pushed onto the red trunk: " + ", ".join(f"{k} {v}" for k, v in sorted(people.items(), key=lambda x: -x[1])))
        print("\nNext: python3 .claude/skills/trunk-doctor/trunk_doctor.py diagnose")
    if args.json:
        print(json.dumps({"state": t.state, "newest": head.__dict__, "first_red": t.first_red.__dict__ if t.first_red else None,
                          "last_green": t.last_green.__dict__ if t.last_green else None,
                          "since": [r.__dict__ for r in t.since], "issue": issue and issue["number"]}, indent=1))
    return EXIT_GREEN if t.state == "green" else EXIT_RED


def _current_branch() -> str:
    r = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], capture_output=True, text=True, cwd=ROOT)
    return r.stdout.strip()


def gate(t: Trunk, issue: dict | None, args) -> int:
    if t.state == "green":
        print(f"trunk gate: GREEN (run {t.newest.number}, {t.newest.sha[:10]}). Go ahead.")
        return EXIT_GREEN
    if t.state == "unknown":
        print("trunk gate: UNKNOWN - no verdict could be read. Look at the guard runs before pushing.")
        return EXIT_UNKNOWN
    branch = args.branch or _current_branch()
    since = f"red since {t.first_red.created[:16].replace('T', ' ')} UTC ({duration(t.first_red.created)}), first red: {_name(t.first_red)}"
    if args.fix or branch.startswith("trunk-doctor/"):
        print(f"trunk gate: RED, {since}.\n  '{branch}' is the fix, so it goes ahead.")
        return EXIT_GREEN
    print(f"trunk gate: RED - bleeding-edge is broken, {since}.")
    if issue:
        print(f"  tracking issue #{issue['number']}: {issue['html_url']}")
    print("  Do not push or merge more work onto a red trunk. Run /trunk-doctor to get it green first, or pass\n"
          "  --fix if this change IS the fix (a trunk-doctor/* branch is recognised by itself).")
    return EXIT_RED


def cmd_diagnose(args) -> int:
    if args.log:
        fs = classify(Path(args.log).read_text(errors="replace"), Path(args.log).name)
        _print_diag(fs)
        return 0
    try:
        if args.issue:
            body = api(f"repos/{REPO}/issues/{args.issue}").get("body") or ""
            m = re.search(r"/actions/runs/(\d+)", body)
            if not m:
                print(f"#{args.issue} names no run.")
                return 1
            run_id = int(m.group(1))
        elif args.run:
            run_id = args.run
        else:
            t = analyse(fetch_runs(pages=2))
            if t.state != "red":
                print(f"bleeding-edge is {t.state.upper()}: nothing to diagnose. (--run <id> or --issue <n> for a past one)")
                return 0
            run_id = t.newest.id
        run = Run.from_api(api(f"repos/{REPO}/actions/runs/{run_id}"))
    except ApiError as e:
        print(f"could not read the run: {e}")
        return EXIT_UNKNOWN
    print(f"Run {run.number} ({run.id}) on {run.sha[:10]}, {run.created[:16].replace('T', ' ')} UTC, {run.conclusion}: {run.title}")
    print(f"  {run.url}")
    fs = diagnose_run(run.id)
    _print_diag(fs)
    if args.no_attribute:
        return 0
    print("\nWho introduced it:")
    try:
        runs = runs_around(run)
        for line in attribute(run, runs, fs, trace=not args.no_trace):
            print(line)
    except ApiError as e:
        print(f"  could not read the runs around it: {e}")
    return 0


def _print_diag(fs: list[Finding]) -> None:
    print(f"\nFailure class: {primary(fs).upper()}  ({len(fs)} finding(s))")
    for f in fs:
        print("  " + f.text())
    adv = advice(fs)
    if adv:
        print("\nDecide (SKILL.md section 3):")
        for a in adv:
            print("  - " + a)


def streaks(runs: list[Run]) -> list[dict]:
    """Every red streak in `runs` (newest first), oldest first."""
    out, cur = [], None
    for r in reversed(runs):
        if r.verdict == "red":
            if cur is None:
                cur = {"first": r, "runs": [r], "pushes": [r], "end": None}
            else:
                cur["runs"].append(r)
                cur["pushes"].append(r)
        elif r.verdict == "green":
            if cur:
                cur["end"] = r
                out.append(cur)
                cur = None
        elif cur:
            cur["pushes"].append(r)
    if cur:
        out.append(cur)
    return out


def summary(fs: list[Finding]) -> str:
    """One short cause for a TRUNK_HEALTH.md row."""
    k = primary(fs)
    if k == "unknown" and any("log unavailable" in f.detail for f in fs):
        return "log expired (GitHub keeps 90 days)"
    parts = []
    for f in fs:
        if f.kind != k:
            continue
        if k == "generator":
            p = f.check + (" (crash: " + f.cause.split("module ")[1].split(" ")[0] + " missing)" if "missing module" in f.cause else f" ({f.cause.split(':')[0]})")
        elif k == "static":
            script = sorted({Path(c.split()[1]).name for c in f.check.split(" ; ") if len(c.split()) > 1})
            what = f"{f.file}:{f.line}" if f.file and f.line else (f.file or f.detail[:70])
            p = f"{'/'.join(script[:2])}{'...' if len(script) > 2 else ''}: {what}"
        elif k == "compile":
            p = f"{f.check} {Path(f.file).name}:{f.line}" if f.file else f.check
        else:
            p = f.check
        if p not in parts:
            parts.append(p)
    return f"{k}: " + "; ".join(parts[:3]) + (f" (+{len(parts) - 3})" if len(parts) > 3 else "")


def cmd_history(args) -> int:
    runs = fetch_runs(pages=args.pages, per_page=100)
    ss = streaks(runs)
    print(f"{len(ss)} red streak(s) in the {len(runs)} newest guard runs on {BRANCH} "
          f"({runs[-1].created[:10]} to {runs[0].created[:10]}).\n")
    print("| Went red (UTC) | Back green | Red for | First red push | Pushed by | Pushes while red | Cause |")
    print("|---|---|---|---|---|---|---|")
    for s in ss:
        f, e = s["first"], s["end"]
        cause = ""
        if args.diagnose:
            cause = summary(diagnose_run(f.id))
            last = s["runs"][-1]
            if last is not f:
                end_cause = summary(diagnose_run(last.id))
                if end_cause != cause:
                    cause += f"; at the end: {end_cause}"
        pr = f"#{f.pr}" if f.pr else f.sha[:10]
        print(f"| {f.created[:16].replace('T', ' ')} | {e.created[:16].replace('T', ' ') if e else 'still red'} | "
              f"{duration(f.created, e.created if e else None)} | {pr} | {person(f.actor)} | {len(s['pushes']) - 1} | {cause} |")
    return 0


def cmd_bisect(args) -> int:
    """First-parent bisect of bleeding-edge between --good and --bad, running --cmd in a throwaway
    worktree at each midpoint (exit 0 = good). Needs the commits locally (git fetch origin bleeding-edge)."""
    revs = subprocess.run(["git", "rev-list", "--first-parent", "--reverse", f"{args.good}..{args.bad}"],
                          capture_output=True, text=True, cwd=ROOT)
    if revs.returncode != 0:
        print(revs.stderr.strip() or "rev-list failed; fetch the range first (git fetch --deepen / --unshallow)")
        return 1
    commits = revs.stdout.split()
    if not commits:
        print("no commits between good and bad")
        return 1
    CACHE.mkdir(parents=True, exist_ok=True)
    wt = Path(tempfile.mkdtemp(prefix="bisect-", dir=CACHE))
    subprocess.run(["git", "worktree", "add", "--detach", str(wt), commits[-1]], cwd=ROOT, capture_output=True)
    try:
        lo, hi = 0, len(commits) - 1      # commits[hi] is known bad
        while lo < hi:
            mid = (lo + hi) // 2
            subprocess.run(["git", "checkout", "-q", "--detach", commits[mid]], cwd=wt, check=True)
            ok = subprocess.run(args.cmd, shell=True, cwd=wt, capture_output=True).returncode == 0
            print(f"  {commits[mid][:10]} {'good' if ok else 'BAD'}")
            if ok:
                lo = mid + 1
            else:
                hi = mid
        bad = commits[lo]
        msg = subprocess.run(["git", "log", "-1", "--format=%h %an %s", bad], capture_output=True, text=True, cwd=ROOT).stdout.strip()
        print(f"first bad push on {BRANCH}: {msg}")
    finally:
        subprocess.run(["git", "worktree", "remove", "--force", str(wt)], cwd=ROOT, capture_output=True)
        shutil.rmtree(wt, ignore_errors=True)
    return 0


# --------------------------------------------------------------------------------------------
# Self-test: fixture logs (real logs from the incidents named, trimmed; two synthetic) and runs
# --------------------------------------------------------------------------------------------
def _fake_runs(spec: str) -> list[Run]:
    """spec newest first, one letter per run: g green, r red, c cancelled, p in progress."""
    out = []
    for i, ch in enumerate(spec):
        status, concl = {"g": ("completed", "success"), "r": ("completed", "failure"),
                         "c": ("completed", "cancelled"), "p": ("in_progress", None)}[ch]
        n = 100 - i
        out.append(Run(n, n, f"{n:040x}", status, concl, f"2026-11-{10 + (n % 10):02d}T10:{n % 60:02d}:00Z",
                       "Shombith03", f"Merge pull request #{n} from froglet-studio/claude/x-{n}"))
    return out


def self_test() -> int:
    fails = []

    def check(name, cond, got=""):
        print(f"  {'ok  ' if cond else 'FAIL'}  {name}" + ("" if cond else f"   got: {got}"))
        if not cond:
            fails.append(name)

    print("classification (fixtures/):")
    expect = {
        "static_port_false_positive_1037.log": ("static", lambda fs: any(
            f.file == "Port/tests/CosmicShore.Tests/ShaderGraphCompilerTests.cs" and f.line == 178 and "false positive" in f.cause for f in fs)),
        "generator_drift_1767.log": ("generator", lambda fs: {f.check for f in fs} == {"author_payload_assets.py", "author_thresher_assets.py"}
                                     and all(f.cause == "drift" for f in fs)),
        "generator_crash_numpy_1897.log": ("generator", lambda fs: {f.check for f in fs} >= {"author_corona_assets.py", "author_flatline_assets.py"}
                                           and all("missing module numpy" in f.cause for f in fs)
                                           and any(f.file == "Tools/Build/flatline_budget.py" and f.line == 29 for f in fs)),
        "compile_870.log": ("compile", lambda fs: [(f.file, f.line, f.check) for f in fs if f.kind == "compile"]
                            == [("Assets/_Scripts/UI/Elements/ArcadeLobbyList.cs", 591, "CS0176")]),
        "tests_synthetic.log": ("tests", lambda fs: {f.check for f in fs} == {"CosmicShore.Tests.SoapCoreTests.RaiseInvokesEveryListener",
                                                                           "CosmicShore.Tests.GameModesTests.EveryValueIsUnique"}),
        "infra_licence_1025.log": ("infra", lambda fs: [f.check for f in fs] == ["licence"]),
        "infra_runner_lost_synthetic.log": ("infra", lambda fs: [f.check for f in fs] == ["runner lost"]),
        "generator_inline_loop_meerkat_1631.log": ("generator", lambda fs: [(f.check, f.cause) for f in fs]
                                                   == [("author_meerkat_assets.py", "drift")]),
        "static_console_logging_932.log": ("static", lambda fs: any(
            f.file == "Assets/_Scripts/Utility/AITraining/Runner/TrainingSessionRunner.cs" and f.line == 710 for f in fs)),
    }
    for name, (kind, ok) in expect.items():
        fs = classify((FIXTURES / name).read_text(errors="replace"), name)
        got = primary(fs)
        check(f"{name} -> {kind}", got == kind and ok(fs), f"{got}: " + " | ".join(f.text() for f in fs))
    # Negative controls on the classifier: a clean log and the guard's own script text are not findings.
    clean = "2026-11-12T00:00:00Z ##[group]Run bash Tools/CI/check_generators.sh\n" \
            "2026-11-12T00:00:01Z \x1b[36;1mecho \"::error::The project did not compile.\"\x1b[0m\n" \
            "2026-11-12T00:00:02Z generators: OK (76 checked, arcade_mode_lib self-test OK)\n"
    fs = classify(clean)
    check("a clean log has no code finding", primary(fs) == "unknown", primary(fs))
    check("the infra override never hides a real compile error",
          primary(classify((FIXTURES / "compile_870.log").read_text() + "\nNo valid Unity Editor license found.\n")) == "compile")

    print("trunk state (fixture runs, newest first):")
    t = analyse(_fake_runs("ggrg"))
    check("green trunk reports green", t.state == "green" and t.first_red is None, t.state)
    t = analyse(_fake_runs("cgr"))
    check("a cancelled newest run is no verdict: green underneath stays green", t.state == "green", t.state)
    t = analyse(_fake_runs("prcrrgg"))
    check("red streak: state red", t.state == "red", t.state)
    check("red streak: first red skips the cancelled run", t.first_red and t.first_red.number == 96, t.first_red and t.first_red.number)
    check("red streak: last green found", t.last_green and t.last_green.number == 95, t.last_green and t.last_green.number)
    check("red streak: every push since counted, cancelled included", len(t.since) == 5, len(t.since))
    check("red streak: in-progress run listed", len(t.in_progress) == 1, len(t.in_progress))
    ss = streaks(_fake_runs("rrgcrrgg"))
    check("history: two streaks, oldest first, the open one last", len(ss) == 2 and ss[0]["end"] and ss[1]["end"] is None,
          [(s['first'].number, s['end'] and s['end'].number) for s in ss])

    print("attribution evidence (check_ran):")
    global run_logs
    real_run_logs = run_logs
    logs = {1: (FIXTURES / "static_console_logging_932.log").read_text(),
            2: (FIXTURES / "generator_crash_numpy_1897.log").read_text()}
    _JOBS.update({1: [{"name": "Static validation", "conclusion": "failure"}, {"name": "Unity compile", "conclusion": "skipped"}],
                  2: [{"name": "Static validation", "conclusion": "failure"}],
                  3: [{"name": "Static validation", "conclusion": "success"}, {"name": "Unity compile", "conclusion": "failure"}]})
    run_logs = lambda rid: [("Static validation", logs[rid])] if rid in logs else []  # noqa: E731
    try:
        gen = Finding("generator", "author_regatta_assets.py")
        check("a generator step skipped behind a static failure is no evidence", not check_ran(1, gen))
        check("a finished generator sweep is evidence, even for one that did not exist yet", check_ran(2, gen))
        check("a skipped Unity job never compiled", not check_ran(1, Finding("compile", "CS0246", "a.cs")))
        check("a failed Unity job did compile", check_ran(3, Finding("compile", "CS0246", "a.cs")))
        check("a passing validate job ran every static check", check_ran(3, Finding("static", "python3 Tools/x.py")))
    finally:
        run_logs = real_run_logs
        for k in (1, 2, 3):
            _JOBS.pop(k, None)

    print("gate:")
    ns = argparse.Namespace(branch="claude/some-feature", fix=False)
    import contextlib
    import io
    with contextlib.redirect_stdout(io.StringIO()):
        g = gate(analyse(_fake_runs("ggr")), None, ns)
        r = gate(analyse(_fake_runs("rrg")), None, ns)
        rf = gate(analyse(_fake_runs("rrg")), None, argparse.Namespace(branch="trunk-doctor/2026-11-12-numpy", fix=False))
        rx = gate(analyse(_fake_runs("rrg")), None, argparse.Namespace(branch="claude/x", fix=True))
        u = gate(analyse(_fake_runs("cc")), None, ns)
    check("--gate exits 0 on a green trunk (negative control)", g == 0, g)
    check("--gate exits 2 on a red trunk", r == 2, r)
    check("--gate lets a trunk-doctor/* branch through", rf == 0, rf)
    check("--gate --fix lets the fix through", rx == 0, rx)
    check("--gate exits 3 when no run has a verdict", u == 3, u)

    print("titles:")
    check("PR number from a merge title", Run(1, 1, "a", "completed", "success", "", "x",
                                              "Merge pull request #1913 from froglet-studio/claude/ecstatic-curie-fjt6ep").pr == 1913)
    check("PR number from a squash title", Run(1, 1, "a", "completed", "success", "", "x", "Rhino: triple plough (#1862)").pr == 1862)

    print(f"\nself-test: {'OK' if not fails else f'{len(fails)} FAILED'}")
    return 0 if not fails else 1


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--gate", action="store_true", help="exit 0 green, 2 red, 3 unknown")
    ap.add_argument("--fix", action="store_true", help="with --gate: this change IS the trunk fix")
    ap.add_argument("--branch", help="with --gate: the branch being pushed (default: the current one)")
    ap.add_argument("--pages", type=int, default=1)
    ap.add_argument("--show", type=int, default=12, help="status: how many recent runs to list")
    ap.add_argument("--json", action="store_true")
    sub = ap.add_subparsers(dest="cmd")
    sub.add_parser("status")
    d = sub.add_parser("diagnose")
    d.add_argument("--run", type=int)
    d.add_argument("--issue", type=int)
    d.add_argument("--log")
    d.add_argument("--no-trace", action="store_true", help="do not walk back through earlier red runs")
    d.add_argument("--no-attribute", action="store_true")
    h = sub.add_parser("history")
    h.add_argument("--pages", type=int, default=3)
    h.add_argument("--diagnose", action="store_true")
    b = sub.add_parser("bisect")
    b.add_argument("--good", required=True)
    b.add_argument("--bad", required=True)
    b.add_argument("--cmd", required=True)
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    if args.cmd == "diagnose":
        return cmd_diagnose(args)
    if args.cmd == "history":
        return cmd_history(args)
    if args.cmd == "bisect":
        return cmd_bisect(args)
    return cmd_status(args)


if __name__ == "__main__":
    sys.exit(main())
