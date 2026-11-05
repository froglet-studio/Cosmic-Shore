#!/usr/bin/env python3
"""Release notes for a tag: every pull request merged since the last release tag, grouped by area.

Reads git only, so it works offline on any checkout with history and tags. With a GitHub token it may also
read each pull request's title and author from the API (`--enrich`), and falls back to the commit message
when the API says no.

    python3 Tools/Launch/release_notes.py check-tag rc-0.3.0-1          # is this a release tag? prints its version
    python3 Tools/Launch/release_notes.py notes --tag v0.3.0 --out notes.md [--json notes.json] [--require-prs]
    python3 Tools/Launch/release_notes.py identity --tag v0.3.0 --sha <sha> --build-number 17 --out build-identity.json
    python3 Tools/Launch/release_notes.py self-test

Release tags (the only names the release workflow accepts):
    v<major>.<minor>.<patch>[-<pre>]   a release, e.g. v0.3.0, v0.3.0-beta.2           version = without the v
    rc-<label>                         a release candidate, e.g. rc-0.3.0-1, rc-launchtest-1   version = the tag
                                       (<label>: lower-case letters and digits, joined by '.' or '-')

The range starts at the nearest earlier tag matching `--match` that is an ancestor of the new tag: for a final
release (v1.2.3) the previous final release, so its notes cover every candidate since; for a candidate or a
pre-release the nearest `v*` or `rc-*` tag. If there is none it falls back to `--fallback-match` (default `internal/*`, the weekly dated tag on
bleeding-edge) and says so in the notes. `--from <ref>` names the start outright.

A pull request is found two ways on the first-parent line of the range: a merge commit whose subject is
"Merge pull request #N from ..." (its title is the first line of the body), and a squash merge whose subject
ends "(#N)". Commits that are neither are counted, not listed.

Areas come from the title's conventional-commit type and scope first (`feat(ui): ...` is UI, `ci:` is Build & CI),
then from the paths the pull request changed (AREA_PATHS below; edit it there). The areas are UI, Vessels,
Multiplayer, Live ops / UGS, Build & CI, Ecology, AI, Docs and Other.

Nothing secret is read or printed: the token is sent only to api.github.com and an API failure prints the
status code, never the response body. Stdlib only.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import fnmatch
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
from pathlib import Path

AREAS = ("UI", "Vessels", "Multiplayer", "Live ops / UGS", "Build & CI", "Ecology", "AI", "Docs", "Other")

V_TAG_RE = re.compile(r"^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?$")
RC_TAG_RE = re.compile(r"^rc-[a-z0-9]+(?:[.-][a-z0-9]+)*$")
DEFAULT_MATCH = ("v*", "rc-*")
FINAL_MATCH = ("v*", "!v*-*")  # a final release counts from the previous final release, not the last candidate


def default_match(tag: str) -> tuple[str, ...]:
    """The previous-release patterns for a tag: v1.2.3 looks back to the last v<x.y.z> with no suffix; a candidate
    or a pre-release (rc-..., v1.2.3-beta.1) looks back to the nearest release tag of any kind."""
    m = V_TAG_RE.match(tag)
    return FINAL_MATCH if m and not m.group(4) else DEFAULT_MATCH
DEFAULT_FALLBACK = ("internal/*",)

MERGE_RE = re.compile(r"^Merge pull request #(\d+) from (\S+)")
SQUASH_RE = re.compile(r"^(?P<title>.*\S)\s+\(#(?P<num>\d+)\)\s*$")
CONVENTIONAL_RE = re.compile(r"^(?P<type>[a-z]+)(?:\((?P<scope>[^)]+)\))?!?:\s*(?P<summary>.+)$")

# Conventional-commit scopes (any word of the scope) and types that decide an area outright.
SCOPE_AREAS = {
    "UI": ("ui", "menu", "menus", "hud", "frogma", "style", "toybox-ui", "screen", "screens", "lava-lamp",
           "settings", "scoreboard", "canvas", "gamecanvas", "ftue", "dialogue", "icons", "ability-icon"),
    "Vessels": ("vessel", "vessels", "sparrow", "manta", "dolphin", "squirrel", "serpent", "urchin", "rhino",
                "butterfly", "stoat", "wrasse", "grouper", "goby", "pistol", "angler", "tigermoth", "thresher",
                "brittlestar", "honeyguide", "ratel", "shrike", "orca", "jacks", "starlings", "fulmen", "crew",
                "hull", "vessel-model", "crystal-morph", "abilities", "ability", "elements", "elemental", "prism",
                "prisms"),
    "Multiplayer": ("multiplayer", "mp", "netcode", "network", "party", "lobby", "friends", "matchmaking",
                    "invite", "vivox", "session", "sessions"),
    "Live ops / UGS": ("ugs", "cloud-code", "cloudcode", "cloud-save", "cloudsave", "liveops", "live-ops",
                       "backend", "auth", "economy", "flags", "leaderboard", "leaderboards", "analytics"),
    "Build & CI": ("ci", "build", "builds", "launch", "release", "steam", "ios", "android", "web", "webgl",
                   "toybox", "testflight", "play", "pipeline", "workflows", "guard", "ship"),
    "Ecology": ("ecology", "flora", "fauna", "cell", "cells", "hypersea", "crystal", "crystals", "bioform",
                "bioforms", "swarm"),
    "AI": ("ai", "compute", "learner", "learning", "autopilot", "trainer", "regatta-balance", "vessel-ai"),
    "Docs": ("docs", "doc", "readme"),
}
TYPE_AREAS = {"ci": "Build & CI", "build": "Build & CI", "docs": "Docs"}

# Path prefixes (or fnmatch patterns when they hold a '*') to areas, checked in order; the first match wins.
AREA_PATHS = (
    (".github/", "Build & CI"),
    ("Tools/Launch/", "Build & CI"),
    ("Tools/CI/", "Build & CI"),
    ("Tools/Steam/", "Build & CI"),
    ("Tools/iOS/", "Build & CI"),
    ("Tools/Build/build_*", "Build & CI"),
    ("Tools/Build/publish_*", "Build & CI"),
    ("Assets/_Scripts/Editor/Build/", "Build & CI"),
    ("ProjectSettings/", "Build & CI"),
    ("Packages/", "Build & CI"),
    ("Tools/UGS/", "Live ops / UGS"),
    ("Assets/_Scripts/System/CloudData/", "Live ops / UGS"),
    ("Assets/_Scripts/System/Economy/", "Live ops / UGS"),
    ("Assets/_Scripts/System/UGS*", "Live ops / UGS"),
    ("Assets/_Scripts/System/Authentication*", "Live ops / UGS"),
    ("Assets/_Scripts/Controller/Multiplayer/", "Multiplayer"),
    ("Assets/_Scripts/Controller/Party/", "Multiplayer"),
    ("Assets/_Scripts/System/Friends*", "Multiplayer"),
    ("Docs/MULTIPLAYER*", "Multiplayer"),
    ("Assets/_Scripts/UI/", "UI"),
    ("Assets/_Prefabs/UI*", "UI"),
    ("Assets/_Graphics/Icons/", "UI"),
    ("Assets/_Scenes/Menu_Main*", "UI"),
    ("Assets/FTUE/", "UI"),
    ("Docs/MenuRedesignB/", "UI"),
    ("Docs/UI/", "UI"),
    ("Assets/_Scripts/Controller/Environment/", "Ecology"),
    ("Assets/_Prefabs/FloraAndFauna/", "Ecology"),
    ("Assets/_Models/Fauna/", "Ecology"),
    ("Docs/ECOSYSTEM*", "Ecology"),
    ("Tools/Build/fauna_kit/", "Ecology"),
    ("Assets/_Scripts/Controller/AI/", "AI"),
    ("Tools/Compute/", "AI"),
    ("Docs/AI_SYSTEM/", "AI"),
    ("Docs/Compute/", "AI"),
    ("Assets/_Scripts/Controller/Vessel/", "Vessels"),
    ("Assets/_Scripts/Controller/Prisms/", "Vessels"),
    ("Assets/_Models/Vessel Models/", "Vessels"),
    ("Assets/_Prefabs/Spacevessels/", "Vessels"),
    ("Assets/Resources/ElementalAbilityMaps/", "Vessels"),
    ("Tools/Build/vessel_model/", "Vessels"),
    ("Docs/VesselModels/", "Vessels"),
    ("Docs/", "Docs"),
    ("*.md", "Docs"),
)
DOCS_WEIGHT = 0.25  # a docs file beside code rarely says what a pull request is about


class ReleaseError(SystemExit):
    """A clear, user-facing failure. Exits with code 2 and its message."""

    def __init__(self, message: str):
        super().__init__(2)
        self.message = message


# ───────────────────────────── tags ─────────────────────────────

def tag_version(tag: str) -> str:
    """The version a release tag names, or a ReleaseError saying what a release tag looks like."""
    if V_TAG_RE.match(tag):
        return tag[1:]
    if RC_TAG_RE.match(tag):
        return tag
    raise ReleaseError(
        f"'{tag}' is not a release tag. A release tag is v<major>.<minor>.<patch>[-<pre>] (v0.3.0, v0.3.0-beta.2) "
        f"or rc-<label> with lower-case letters and digits joined by '.' or '-' (rc-0.3.0-1, rc-launchtest-1).")


def _git(repo: Path, *args: str, check: bool = True) -> str:
    proc = subprocess.run(["git", "-C", str(repo), *args], text=True, capture_output=True)
    if check and proc.returncode != 0:
        raise ReleaseError(f"git {' '.join(args[:3])} failed: {proc.stderr.strip()[:300]}")
    return proc.stdout


def resolve_commit(repo: Path, ref: str, what: str) -> str:
    proc = subprocess.run(["git", "-C", str(repo), "rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}"],
                          text=True, capture_output=True)
    if proc.returncode != 0 or not proc.stdout.strip():
        raise ReleaseError(f"{what} '{ref}' does not exist in this checkout (fetch tags and full history: "
                           f"git fetch --tags --unshallow, or actions/checkout with fetch-depth: 0).")
    return proc.stdout.strip()


def previous_tag(repo: Path, tag: str, match: tuple[str, ...], fallback: tuple[str, ...]) -> tuple[str, bool]:
    """The nearest earlier tag matching `match` (then `fallback`) that is an ancestor of `tag`.
    Returns (tag, used_fallback)."""
    tag_commit = resolve_commit(repo, tag, "tag")
    parents = _git(repo, "rev-list", "--parents", "-n", "1", tag_commit).split()[1:]
    if not parents:
        raise ReleaseError(f"'{tag}' points at a root commit; there is nothing before it to compare with.")
    for patterns, used_fallback in ((match, False), (fallback, True)):
        if not patterns:
            continue
        args = ["describe", "--tags", "--abbrev=0"]
        for p in patterns:
            args += ["--exclude", p[1:]] if p.startswith("!") else ["--match", p]
        proc = subprocess.run(["git", "-C", str(repo), *args, f"{tag_commit}^"], text=True, capture_output=True)
        found = proc.stdout.strip()
        if proc.returncode == 0 and found and found != tag:
            return found, used_fallback
    raise ReleaseError(
        f"No earlier tag before '{tag}' matches {', '.join(match)}"
        + (f" or the fallback {', '.join(fallback)}" if fallback else "")
        + ". Name the start of the range with --from <tag or sha>.")


# ───────────────────────────── pull requests ─────────────────────────────

def first_parent_commits(repo: Path, start: str, end: str) -> list[dict]:
    out = _git(repo, "log", "--first-parent", "--format=%H%x1f%P%x1f%s%x1f%b%x1e", f"{start}..{end}")
    commits = []
    for chunk in out.split("\x1e"):
        chunk = chunk.strip("\n")
        if not chunk:
            continue
        sha, parents, subject, body = (chunk.split("\x1f") + ["", "", ""])[:4]
        commits.append({"sha": sha, "parents": parents.split(), "subject": subject.strip(), "body": body.strip()})
    return commits


def changed_files(repo: Path, commit: dict) -> list[str]:
    if not commit["parents"]:
        return []
    out = _git(repo, "diff", "--name-only", "--no-renames", commit["parents"][0], commit["sha"], check=False)
    return [line for line in out.splitlines() if line]


def find_pull_requests(repo: Path, start: str, end: str) -> tuple[list[dict], int]:
    """(pull requests newest first, how many first-parent commits were not pull requests)."""
    prs: list[dict] = []
    seen: set[int] = set()
    other = 0
    for c in first_parent_commits(repo, start, end):
        m = MERGE_RE.match(c["subject"])
        if m and len(c["parents"]) > 1:
            number, branch = int(m.group(1)), m.group(2)
            title = next((ln.strip() for ln in c["body"].splitlines() if ln.strip()), "") or branch
        else:
            s = SQUASH_RE.match(c["subject"])
            if not s:
                other += 1
                continue
            number, branch, title = int(s.group("num")), "", s.group("title")
        if number in seen:
            continue
        seen.add(number)
        prs.append({"number": number, "title": title, "branch": branch, "commit": c["sha"],
                    "files": changed_files(repo, c), "author": ""})
    return prs, other


def path_area(path: str) -> str:
    for pattern, area in AREA_PATHS:
        if "*" in pattern:
            if fnmatch.fnmatch(path, pattern) or fnmatch.fnmatch(path, pattern + "/*"):
                return area
        elif path.startswith(pattern):
            return area
    return "Other"


def classify(title: str, files: list[str]) -> str:
    m = CONVENTIONAL_RE.match(title.strip())
    if m:
        scope = (m.group("scope") or "").lower()
        words = [w for w in re.split(r"[,/ ]+", scope) if w]
        for word in words:
            for area, scopes in SCOPE_AREAS.items():
                if word in scopes:
                    return area
        kind = m.group("type")
        if kind in TYPE_AREAS:
            return TYPE_AREAS[kind]
    scores: dict[str, float] = {}
    for f in files:
        area = path_area(f)
        if area == "Other":
            continue
        scores[area] = scores.get(area, 0.0) + (DOCS_WEIGHT if area == "Docs" else 1.0)
    if not scores:
        return "Other"
    best = max(scores.values())
    return next(a for a in AREAS if scores.get(a) == best)


# ───────────────────────────── GitHub enrichment (optional) ─────────────────────────────

def _api_get(url: str, token: str, opener=urllib.request.urlopen) -> dict:
    req = urllib.request.Request(url, headers={
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "cosmic-shore-release-notes",
    })
    with opener(req, timeout=20) as r:
        return json.loads(r.read().decode())


def enrich(prs: list[dict], repo_slug: str, token: str, opener=urllib.request.urlopen, limit: int = 300) -> str:
    """Fill each pull request's title and author from the API. Returns one line saying what happened.
    Never prints a response body: an error says only its status code."""
    if not re.match(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", repo_slug or ""):
        return "enrichment skipped: no owner/repo"
    done = failures = 0
    for pr in prs[:limit]:
        url = f"https://api.github.com/repos/{repo_slug}/pulls/{pr['number']}"
        try:
            data = _api_get(url, token, opener)
        except urllib.error.HTTPError as e:
            failures += 1
            print(f"enrichment: #{pr['number']} -> HTTP {e.code}; keeping the commit message title", file=sys.stderr)
        except (urllib.error.URLError, TimeoutError, ValueError, OSError) as e:
            failures += 1
            print(f"enrichment: #{pr['number']} -> {type(e).__name__}; keeping the commit message title",
                  file=sys.stderr)
        else:
            title = data.get("title")
            if isinstance(title, str) and title.strip():
                pr["title"] = " ".join(title.split())
            login = (data.get("user") or {}).get("login")
            if isinstance(login, str) and re.match(r"^[A-Za-z0-9-]+(\[bot\])?$", login):
                pr["author"] = login
            done += 1
        if failures >= 3:
            return f"enrichment stopped after 3 failures ({done} enriched); titles come from commit messages"
    return f"enriched {done} of {len(prs)} pull requests from the GitHub API"


# ───────────────────────────── output ─────────────────────────────

def _md_escape(text: str) -> str:
    # Keep a title from opening a heading, a table or an HTML tag, and from @-mentioning anyone.
    text = " ".join(text.split())
    return re.sub(r"([<>|`*_\[\]])", r"\\\1", text).replace("@", "@​")


def build_notes(repo: Path, tag: str, start: str | None, match: tuple[str, ...], fallback: tuple[str, ...],
                repo_slug: str = "", token: str = "", enrich_mode: str = "auto", opener=urllib.request.urlopen) -> dict:
    version = tag_version(tag)
    tag_commit = resolve_commit(repo, tag, "tag")
    used_fallback = False
    if start:
        resolve_commit(repo, start, "--from")
    else:
        start, used_fallback = previous_tag(repo, tag, match, fallback)
    start_commit = resolve_commit(repo, start, "start")
    prs, other = find_pull_requests(repo, start_commit, tag_commit)
    enrichment = "enrichment off"
    if enrich_mode == "on" or (enrich_mode == "auto" and token):
        enrichment = enrich(prs, repo_slug, token, opener) if token else "enrichment skipped: no GITHUB_TOKEN"
    for pr in prs:
        pr["area"] = classify(pr["title"], pr["files"])
    return {"tag": tag, "version": version, "commit": tag_commit, "from": start, "from_commit": start_commit,
            "from_is_fallback": used_fallback, "pull_requests": prs, "other_commits": other,
            "commits": int(_git(repo, "rev-list", "--count", "--first-parent", f"{start_commit}..{tag_commit}")),
            "enrichment": enrichment, "repo": repo_slug}


def render_markdown(notes: dict) -> str:
    prs = notes["pull_requests"]
    slug = notes.get("repo") or ""
    lines = [f"# Cosmic Shore {notes['tag']}", "",
             "Draft notes from `Tools/Launch/release_notes.py`. Read and edit them before this release is published.",
             "", "| | |", "|---|---|",
             f"| Tag | `{notes['tag']}` |",
             f"| Version | `{notes['version']}` |",
             f"| Commit | `{notes['commit'][:12]}` |",
             f"| Since | `{notes['from']}` (`{notes['from_commit'][:12]}`)"
             + (" - no earlier release tag, so the range starts at the newest fallback tag" if notes["from_is_fallback"]
                else "") + " |",
             f"| Pull requests | {len(prs)} |",
             f"| Other commits on the first-parent line | {notes['other_commits']} |", ""]
    if not prs:
        lines += [f"**No pull requests were merged between `{notes['from']}` and `{notes['tag']}`.**", ""]
        return "\n".join(lines)
    for area in AREAS:
        group = [p for p in prs if p["area"] == area]
        if not group:
            continue
        lines += [f"## {area}", ""]
        for p in sorted(group, key=lambda p: p["number"]):
            ref = f"#{p['number']}"
            by = f" (by {p['author']})" if p.get("author") else ""
            lines.append(f"- {_md_escape(p['title'])} ({ref}){by}")
        lines.append("")
    if slug:
        lines += [f"Full comparison: https://github.com/{slug}/compare/{notes['from_commit'][:12]}...{notes['tag']}", ""]
    return "\n".join(lines)


def notes_json(notes: dict) -> dict:
    out = {k: v for k, v in notes.items() if k != "pull_requests"}
    out["pull_requests"] = [{k: p[k] for k in ("number", "title", "author", "area", "commit")}
                            for p in notes["pull_requests"]]
    out["by_area"] = {a: sum(1 for p in notes["pull_requests"] if p["area"] == a) for a in AREAS}
    return out


def build_identity(tag: str, sha: str, build_number: str, run_url: str = "") -> dict:
    version = tag_version(tag)
    if not re.match(r"^[0-9a-f]{40}$", sha):
        raise ReleaseError(f"--sha must be a full 40-character commit sha (got '{sha[:60]}').")
    if not re.match(r"^[1-9]\d{0,9}$", str(build_number)):
        raise ReleaseError(f"--build-number must be a positive integer (got '{str(build_number)[:20]}').")
    identity = {"schema": 1, "product": "Cosmic Shore", "tag": tag, "version": version, "commit": sha,
                "build_number": int(build_number),
                "created_at": _dt.datetime.now(_dt.timezone.utc).replace(microsecond=0).isoformat()
                .replace("+00:00", "Z")}
    if run_url:
        if not re.match(r"^https://[A-Za-z0-9.-]+/[A-Za-z0-9._~/-]*$", run_url):
            raise ReleaseError("--run-url must be an https URL with no query string.")
        identity["run_url"] = run_url
    return identity


# ───────────────────────────── self-test ─────────────────────────────

def _self_test() -> int:
    env = {**os.environ, "GIT_AUTHOR_NAME": "t", "GIT_AUTHOR_EMAIL": "t@example.com", "GIT_COMMITTER_NAME": "t",
           "GIT_COMMITTER_EMAIL": "t@example.com", "GIT_CONFIG_GLOBAL": os.devnull, "GIT_CONFIG_NOSYSTEM": "1"}
    checks: list[tuple[str, bool]] = []

    def check(name: str, ok: bool) -> None:
        checks.append((name, bool(ok)))

    def expect_error(name: str, fn, needle: str) -> None:
        try:
            fn()
        except ReleaseError as e:
            check(name, needle in e.message)
            return
        check(name, False)

    tmp = Path(tempfile.mkdtemp(prefix="release-notes-test-"))
    try:
        def git(*args: str) -> str:
            return subprocess.run(["git", "-C", str(tmp), "-c", "commit.gpgsign=false", "-c", "tag.gpgsign=false",
                                   *args], check=True, text=True, capture_output=True, env=env).stdout

        def write(path: str, text: str) -> None:
            p = tmp / path
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(text)

        def merge_pr(branch: str, number: int, title: str, files: dict[str, str]) -> None:
            git("checkout", "-q", "-b", branch)
            for path, text in files.items():
                write(path, text)
            git("add", "-A")
            git("commit", "-q", "-m", f"work on {branch}")
            git("checkout", "-q", "main")
            git("merge", "-q", "--no-ff", branch, "-m",
                f"Merge pull request #{number} from froglet/{branch}\n\n{title}")

        git("init", "-q", "-b", "main")
        write("README.md", "root\n")
        git("add", "-A")
        git("commit", "-q", "-m", "root")
        git("commit", "-q", "--allow-empty", "-m", "second")
        git("tag", "internal/2026-10-30")
        git("commit", "-q", "--allow-empty", "-m", "third")

        # No release tag yet: the fallback (the weekly internal tag) starts the range, and says so.
        merge_pr("first", 5, "fix(party): invite flow", {"Assets/_Scripts/Controller/Party/Invite.cs": "x"})
        git("tag", "rc-first-1")
        n0 = build_notes(tmp, "rc-first-1", None, DEFAULT_MATCH, DEFAULT_FALLBACK)
        check("fallback to internal/* when no release tag exists", n0["from"] == "internal/2026-10-30"
              and n0["from_is_fallback"] and "no earlier release tag" in render_markdown(n0))
        check("party scope is Multiplayer", n0["pull_requests"][0]["area"] == "Multiplayer")
        expect_error("no earlier tag and no fallback fails clearly",
                     lambda: build_notes(tmp, "rc-first-1", None, DEFAULT_MATCH, ()), "--from")
        git("commit", "-q", "--allow-empty", "-m", "release prep")
        git("tag", "v0.1.0")

        merge_pr("ui-menu", 10, "feat(ui): new arcade menu", {"Assets/_Scripts/UI/Menu.cs": "x"})
        merge_pr("claude/steam", 13, "claude/steam-workflow", {".github/workflows/x.yml": "x", "Docs/x.md": "y"})
        merge_pr("docs-only", 12, "Write the Steam runbook", {"Docs/STEAM.md": "x"})
        merge_pr("vessel-paths", 14, "Make the Sparrow boost readable",
                 {"Assets/_Scripts/Controller/Vessel/Sparrow.cs": "x", "Docs/VesselModels/S.md": "y",
                  "Docs/a.md": "a", "Docs/b.md": "b", "Docs/c.md": "c"})
        merge_pr("odd", 15, "Tidy up", {"Assets/Random/thing.txt": "x"})
        write("Assets/_Scripts/Controller/Environment/Flora.cs", "x")
        git("add", "-A")
        git("commit", "-q", "-m", "feat(flora): grow backwards (#11)")
        git("commit", "-q", "--allow-empty", "-m", "chore: bump version")
        git("tag", "rc-test-1")

        n1 = build_notes(tmp, "rc-test-1", None, DEFAULT_MATCH, DEFAULT_FALLBACK)
        areas = {p["number"]: p["area"] for p in n1["pull_requests"]}
        check("previous tag is v0.1.0 (not the fallback)", n1["from"] == "v0.1.0" and not n1["from_is_fallback"])
        check("six pull requests found (merge commits + squash)", sorted(areas) == [10, 11, 12, 13, 14, 15])
        check("one other commit counted", n1["other_commits"] == 1)
        check("scope ui -> UI", areas.get(10) == "UI")
        check("scope flora -> Ecology (squash merge)", areas.get(11) == "Ecology")
        check("docs-only paths -> Docs", areas.get(12) == "Docs")
        check("workflow path beats a docs file -> Build & CI", areas.get(13) == "Build & CI")
        check("one code file beats four docs -> Vessels", areas.get(14) == "Vessels")
        check("unknown paths -> Other", areas.get(15) == "Other")
        md = render_markdown({**n1, "repo": "froglet/game"})
        check("markdown lists every pull request", all(f"(#{n})" in md for n in areas))
        check("markdown groups by area", md.index("## UI") < md.index("## Build & CI") < md.index("## Other"))
        check("markdown links the comparison", "compare/" in md)

        # A range with no pull requests says so, and --require-prs turns that into a failure.
        git("commit", "-q", "--allow-empty", "-m", "hotfix without a pull request")
        git("tag", "rc-empty-1")
        n2 = build_notes(tmp, "rc-empty-1", None, DEFAULT_MATCH, DEFAULT_FALLBACK)
        check("empty range: previous tag is rc-test-1", n2["from"] == "rc-test-1")
        check("empty range: no pull requests, and the notes say so",
              not n2["pull_requests"] and "No pull requests were merged" in render_markdown(n2))
        out = tmp.parent / f"{tmp.name}-notes.md"
        rc = main(["notes", "--repo", str(tmp), "--tag", "rc-empty-1", "--out", str(out), "--require-prs", "--enrich", "off"])
        check("empty range with --require-prs exits non-zero", rc != 0)
        out.unlink(missing_ok=True)

        # A final release looks back past the candidates to the previous final release.
        merge_pr("final", 16, "feat(ugs): flags document", {"Tools/UGS/CloudCode/flags.js": "x"})
        git("tag", "v0.2.0")
        n5 = build_notes(tmp, "v0.2.0", None, default_match("v0.2.0"), DEFAULT_FALLBACK)
        check("final release counts from the previous final release (past rc tags)",
              n5["from"] == "v0.1.0" and len(n5["pull_requests"]) == 7)
        check("ugs scope -> Live ops / UGS", {p["number"]: p["area"] for p in n5["pull_requests"]}[16] == "Live ops / UGS")
        check("a candidate counts from the nearest release tag of any kind",
              default_match("rc-0.3.0-1") == DEFAULT_MATCH and default_match("v0.3.0-beta.1") == DEFAULT_MATCH)

        # Malformed and missing tags fail clearly.
        for bad in ("v1.2", "rc-", "rc-Launch_Test", "release-1", "v01.2.3", "rc-a..b"):
            expect_error(f"malformed tag {bad!r} refused", lambda b=bad: tag_version(b), "is not a release tag")
        check("v tag version strips the v", tag_version("v0.3.0-beta.2") == "0.3.0-beta.2")
        check("rc tag version is the tag", tag_version("rc-launchtest-1") == "rc-launchtest-1")
        expect_error("missing tag fails clearly", lambda: build_notes(tmp, "rc-nope-1", None, DEFAULT_MATCH, ()),
                     "does not exist")
        expect_error("--from that does not exist fails clearly",
                     lambda: build_notes(tmp, "rc-test-1", "no-such-ref", DEFAULT_MATCH, ()), "does not exist")

        # Enrichment: titles and authors from the API; a failure prints a status code, never a body.
        class FakeResponse:
            def __init__(self, payload: dict):
                self.payload = payload

            def __enter__(self):
                return self

            def __exit__(self, *a):
                return False

            def read(self):
                return json.dumps(self.payload).encode()

        def good_opener(req, timeout=0):
            num = int(req.full_url.rsplit("/", 1)[1])
            assert req.get_header("Authorization") == "Bearer fake-token-for-test"
            return FakeResponse({"title": f"API title {num}", "user": {"login": "someone"}})

        n3 = build_notes(tmp, "rc-test-1", None, DEFAULT_MATCH, DEFAULT_FALLBACK, "froglet/game",
                         "fake-token-for-test", "auto", good_opener)
        check("enrichment fills titles and authors",
              all(p["title"].startswith("API title") and p["author"] == "someone" for p in n3["pull_requests"]))
        check("enriched author shows without an @-mention", "(by someone)" in render_markdown(n3)
              and "@someone" not in render_markdown(n3))

        secret_body = b'{"message":"nope","url":"https://x.example/a?token=SECRETSECRET"}'

        def bad_opener(req, timeout=0):
            raise urllib.error.HTTPError(req.full_url, 403, "Forbidden", {}, __import__("io").BytesIO(secret_body))

        import contextlib
        import io
        err = io.StringIO()
        with contextlib.redirect_stderr(err):
            n4 = build_notes(tmp, "rc-test-1", None, DEFAULT_MATCH, DEFAULT_FALLBACK, "froglet/game",
                             "fake-token-for-test", "auto", bad_opener)
        check("enrichment failure keeps commit titles", n4["pull_requests"][0]["title"] != "")
        check("enrichment stops after 3 failures", "stopped after 3 failures" in n4["enrichment"])
        check("enrichment failure never prints the body or the token",
              "SECRET" not in err.getvalue() and "fake-token" not in err.getvalue() and "HTTP 403" in err.getvalue())

        # A title cannot @-mention or inject markup.
        check("titles are escaped", _md_escape("<b>@all | x</b>") == "\\<b\\>@​all \\| x\\</b\\>")

        # Build identity.
        ident = build_identity("v0.3.0", "a" * 40, "17", "https://github.com/o/r/actions/runs/1")
        check("identity carries tag, version, sha and build number",
              ident["version"] == "0.3.0" and ident["build_number"] == 17 and ident["commit"] == "a" * 40)
        expect_error("identity refuses a short sha", lambda: build_identity("v0.3.0", "abc", "1"), "40-character")
        expect_error("identity refuses a bad build number", lambda: build_identity("v0.3.0", "a" * 40, "0"),
                     "positive integer")
        expect_error("identity refuses a run url with a query", lambda: build_identity(
            "v0.3.0", "a" * 40, "1", "https://x/y?sig=1"), "no query string")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    failed = [name for name, ok in checks if not ok]
    for name, ok in checks:
        print(("ok   " if ok else "FAIL ") + name)
    if failed:
        print(f"SELF-TEST FAILED: {len(failed)} of {len(checks)} checks")
        return 1
    print(f"self-test OK: {len(checks)} checks, including malformed tags, a range with no pull requests, "
          f"a missing tag and an API failure that must not leak its body")
    return 0


# ───────────────────────────── CLI ─────────────────────────────

def _err() -> str:
    """An error line's prefix: a GitHub Actions annotation in a workflow, plain text elsewhere."""
    return "::error::" if os.environ.get("GITHUB_ACTIONS") == "true" else "ERROR: "


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    c = sub.add_parser("check-tag", help="is this a release tag? prints its version")
    c.add_argument("tag")

    n = sub.add_parser("notes", help="write the release notes for a tag")
    n.add_argument("--tag", required=True)
    n.add_argument("--from", dest="start", help="start of the range (default: the previous release tag)")
    n.add_argument("--match", action="append",
                   help="previous release tag pattern, '!pattern' to exclude (repeatable; default: v* and rc-* for a "
                        "candidate, v* without a '-' suffix for a final release)")
    n.add_argument("--fallback-match", action="append",
                   help="used when no tag matches --match (repeatable; default internal/*; 'none' to disable)")
    n.add_argument("--repo", default=".")
    n.add_argument("--github-repo", default=os.environ.get("GITHUB_REPOSITORY", ""), help="owner/repo")
    n.add_argument("--enrich", choices=("auto", "on", "off"), default="auto",
                   help="read titles and authors from the GitHub API (auto: when GITHUB_TOKEN is set)")
    n.add_argument("--out", help="the Markdown file (default: stdout)")
    n.add_argument("--json", help="also write a JSON summary here")
    n.add_argument("--require-prs", action="store_true", help="fail when the range holds no pull request")

    i = sub.add_parser("identity", help="write build-identity.json")
    i.add_argument("--tag", required=True)
    i.add_argument("--sha", required=True)
    i.add_argument("--build-number", required=True)
    i.add_argument("--run-url", default="")
    i.add_argument("--out", required=True)

    sub.add_parser("self-test")
    args = ap.parse_args(argv)

    try:
        if args.cmd == "self-test":
            return _self_test()
        if args.cmd == "check-tag":
            print(tag_version(args.tag))
            return 0
        if args.cmd == "identity":
            ident = build_identity(args.tag, args.sha, args.build_number, args.run_url)
            Path(args.out).write_text(json.dumps(ident, indent=2) + "\n")
            print(json.dumps(ident, indent=2))
            return 0
        match = tuple(args.match or default_match(args.tag))
        fallback = tuple(args.fallback_match or DEFAULT_FALLBACK)
        if fallback == ("none",):
            fallback = ()
        notes = build_notes(Path(args.repo), args.tag, args.start, match, fallback, args.github_repo,
                            os.environ.get("GITHUB_TOKEN", ""), args.enrich)
        md = render_markdown(notes)
        if args.out:
            Path(args.out).write_text(md)
        else:
            print(md)
        if args.json:
            Path(args.json).write_text(json.dumps(notes_json(notes), indent=2) + "\n")
        print(f"{notes['tag']}: {len(notes['pull_requests'])} pull request(s) since {notes['from']} "
              f"({notes['commits']} first-parent commits; {notes['enrichment']})", file=sys.stderr)
        if args.require_prs and not notes["pull_requests"]:
            print(f"{_err()}no pull requests were merged between {notes['from']} and {notes['tag']}. A release with "
                  f"nothing in it is almost always the wrong tag; re-run with allow_empty if it is intended.",
                  file=sys.stderr)
            return 3
        return 0
    except ReleaseError as e:
        print(f"{_err()}{e.message}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
