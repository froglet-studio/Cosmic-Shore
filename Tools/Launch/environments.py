#!/usr/bin/env python3
"""Check that the GitHub Environments a launch workflow needs exist, and that production ones need a reviewer.

A job that names an Environment which does not exist makes GitHub CREATE it, empty and unprotected. So every
launch workflow runs this first, in a job that names no Environment, and stops before any job that would:

    python3 Tools/Launch/environments.py check uba-builds steam-prerelease android-production:reviewers
    python3 Tools/Launch/environments.py self-test

`name:reviewers` also requires a "Required reviewers" protection rule with at least one reviewer, which is what
keeps a production step behind a person (Docs/LAUNCH/PIPELINES.md). Reads
GET /repos/{owner}/{repo}/environments/{name} with GITHUB_TOKEN (the job needs `actions: read`); a token that
cannot read them fails closed. Prints names and rule types only; never a response body. Stdlib only.
"""
from __future__ import annotations

import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

NAME_RE = re.compile(r"^[A-Za-z0-9_.-]{1,255}$")


def fetch(repo: str, name: str, token: str) -> tuple[int, dict]:
    url = f"https://api.github.com/repos/{repo}/environments/{urllib.parse.quote(name)}"
    req = urllib.request.Request(url, headers={"Authorization": f"Bearer {token}",
                                               "Accept": "application/vnd.github+json",
                                               "X-GitHub-Api-Version": "2022-11-28",
                                               "User-Agent": "cosmic-shore-launch"})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return e.code, {}
    except (urllib.error.URLError, TimeoutError, ValueError):
        return 0, {}


def check(needs: list[str], repo: str, token: str, getter=fetch) -> list[str]:
    """Every problem, one sentence each. Empty means every Environment is there and protected as asked."""
    problems = []
    for need in needs:
        name, _, rule = need.partition(":")
        if not NAME_RE.match(name) or rule not in ("", "reviewers"):
            problems.append(f"'{need[:60]}' is not an Environment name (with an optional ':reviewers')")
            continue
        status, env = getter(repo, name, token)
        if status == 404:
            problems.append(f"the '{name}' Environment does not exist. Create it (Settings > Environments > New "
                            f"environment"
                            + (", with Required reviewers" if rule else "")
                            + ") and add its secrets: Docs/LAUNCH/PIPELINES.md lists them.")
        elif status != 200:
            problems.append(f"could not read the '{name}' Environment (HTTP {status or 'no answer'}); the job needs "
                            f"permissions: actions: read. Failing closed.")
        elif rule == "reviewers":
            rules = env.get("protection_rules") or []
            reviewers = [r for r in rules if r.get("type") == "required_reviewers" and r.get("reviewers")]
            if not reviewers:
                problems.append(f"the '{name}' Environment has no Required reviewers rule. A production step runs "
                                f"only behind a person: add one (Settings > Environments > {name}).")
    return problems


def _self_test() -> int:
    table = {
        "uba-builds": (200, {"protection_rules": []}),
        "android-production": (200, {"protection_rules": [{"type": "required_reviewers",
                                                           "reviewers": [{"type": "User"}]}]}),
        "windows-production": (200, {"protection_rules": [{"type": "wait_timer", "wait_timer": 5}]}),
        "secret-env": (403, {}),
    }
    getter = lambda repo, name, token: table.get(name, (404, {}))  # noqa: E731
    results = [
        ("existing Environments pass", check(["uba-builds", "android-production:reviewers"], "o/r", "t", getter) == []),
        ("a missing Environment is named", any("'steam-prerelease' Environment does not exist" in p
                                               for p in check(["steam-prerelease"], "o/r", "t", getter))),
        ("a production Environment without reviewers is refused", any("no Required reviewers" in p for p in check(
            ["windows-production:reviewers"], "o/r", "t", getter))),
        ("an unreadable Environment fails closed", any("Failing closed" in p for p in check(
            ["secret-env"], "o/r", "t", getter))),
        ("a bad name is refused", len(check(["bad name:x"], "o/r", "t", getter)) == 1),
        ("several problems are all listed", len(check(["a", "b", "uba-builds"], "o/r", "t", getter)) == 2),
    ]
    for name, ok in results:
        print(("ok   " if ok else "FAIL ") + name)
    if not all(ok for _, ok in results):
        print("SELF-TEST FAILED")
        return 1
    print(f"self-test OK: {len(results)} checks, including a missing Environment, one without reviewers and one "
          f"that cannot be read (all refused)")
    return 0


def main(argv: list[str] | None = None) -> int:
    argv = sys.argv[1:] if argv is None else argv
    if argv[:1] == ["self-test"]:
        return _self_test()
    if argv[:1] != ["check"] or len(argv) < 2:
        print(__doc__)
        return 2
    repo = os.environ.get("GITHUB_REPOSITORY", "")
    token = os.environ.get("GITHUB_TOKEN", "")
    err = "::error::" if os.environ.get("GITHUB_ACTIONS") == "true" else "ERROR: "
    if not re.match(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", repo) or not token:
        print(f"{err}GITHUB_REPOSITORY and GITHUB_TOKEN are needed to read the Environments", file=sys.stderr)
        return 2
    problems = check(argv[1:], repo, token)
    for p in problems:
        print(f"{err}{p}", file=sys.stderr)
    if problems:
        print(f"{len(problems)} Environment problem(s); nothing that needs them will run.", file=sys.stderr)
        return 1
    print("Environments ready: " + ", ".join(argv[1:]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
