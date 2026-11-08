#!/usr/bin/env python3
"""Every launch workflow pins what it runs: each third-party `uses:` names a full 40-character commit sha.

    python3 Tools/Launch/check_action_pins.py check [files...]   default: .github/workflows/{launch-,release}*.y*ml
    python3 Tools/Launch/check_action_pins.py self-test          the negative control: unpinned shapes it must find

A tag or a branch (`actions/checkout@v4`, `@main`) can be moved by whoever controls the action, so a launch
workflow that holds a store or signing secret would run whatever that tag points at tomorrow. Rules:
- `owner/repo@<40 hex>` and `owner/repo/path@<40 hex>` (a reusable workflow too) pass; a short sha, a tag, a branch,
  an upper-case sha or no `@` at all fail;
- `docker://image@sha256:<64 hex>` passes; a docker image by tag fails;
- local actions and workflows (`./...`) pass: they are this repo's own code at the same commit;
- quoted values, flow mappings (`- {uses: x@y}`) and trailing comments are read; commented-out lines are not.

Stdlib only.
"""
from __future__ import annotations

import re
import sys
import tempfile
from pathlib import Path

DEFAULT_GLOBS = ("launch-*.yml", "launch-*.yaml", "release*.yml", "release*.yaml")
USES_RE = re.compile(r"""(?:^\s*(?:-\s+)?|[{,]\s*)uses\s*:\s*(?P<q>["']?)(?P<ref>[^"'\s,}#]*)(?P=q)""")
SHA_PIN = re.compile(r"[^@\s]+@[0-9a-f]{40}")
DOCKER_PIN = re.compile(r"docker://[^@\s]+@sha256:[0-9a-f]{64}")


def problems_in(text: str, name: str) -> list[str]:
    found = []
    for lineno, line in enumerate(text.splitlines(), 1):
        if line.lstrip().startswith("#"):
            continue
        for m in USES_RE.finditer(line):
            ref = m.group("ref")
            if ref.startswith("./"):
                continue
            if ref.startswith("docker://"):
                if not DOCKER_PIN.fullmatch(ref):
                    found.append(f"{name}:{lineno}: {ref or '(empty)'} - pin a docker image by @sha256 digest")
                continue
            if not SHA_PIN.fullmatch(ref):
                found.append(f"{name}:{lineno}: {ref or '(empty)'} - pin to a full 40-character commit sha "
                             "(with the version in a comment)")
    return found


def check(paths: list[Path]) -> list[str]:
    found = []
    for path in paths:
        found += problems_in(path.read_text(encoding="utf-8"), str(path))
    return found


def default_paths(root: Path) -> list[Path]:
    workflows = root / ".github" / "workflows"
    return sorted({p for pattern in DEFAULT_GLOBS for p in workflows.glob(pattern)})


def self_test() -> int:
    sha = "11bd71901bbe5b1630ceea73d27597364c9af683"
    digest = "a" * 64
    good = f"""
jobs:
  a:
    steps:
      - uses: actions/checkout@{sha} # v4.2.2
      - uses: "actions/setup-python@{sha}"
      - uses: './.github/actions/local'
      - {{ name: x, uses: actions/cache@{sha} }}
      - uses: docker://alpine@sha256:{digest}
      # - uses: actions/checkout@v4   (commented out)
  b:
    uses: org/repo/.github/workflows/build.yml@{sha}
  c:
    uses: ./.github/workflows/local.yml
"""
    bad = {
        "a tag": "      - uses: actions/checkout@v4",
        "a quoted tag": '      - uses: "actions/checkout@v4.2.2"',
        "a single-quoted branch": "      - uses: 'actions/checkout@main'",
        "a short sha": "      - uses: actions/checkout@11bd719",
        "an upper-case sha": f"      - uses: actions/checkout@{sha.upper()}",
        "a sha with a suffix": f"      - uses: actions/checkout@{sha}x",
        "no ref": "      - uses: actions/checkout",
        "a reusable workflow on a branch": "    uses: org/repo/.github/workflows/build.yml@main",
        "a flow mapping": "      - { name: x, uses: actions/cache@v4 }",
        "a docker tag": "      - uses: docker://alpine:3.20",
        "a docker digest that is short": "      - uses: docker://alpine@sha256:abc",
        "spaces before the colon": "      - uses : actions/checkout@v4",
        "a trailing comment hiding nothing": "      - uses: actions/checkout@v4 # " + sha,
    }
    failed = []
    if problems_in(good, "good.yml"):
        failed.append("refused pinned shapes: " + "; ".join(problems_in(good, "good.yml")))
    for name, line in bad.items():
        if not problems_in(f"jobs:\n  a:\n    steps:\n{line}\n", name):
            failed.append(f"accepted {name}")
    with tempfile.TemporaryDirectory() as tmp:  # the file walk finds .yaml as well as .yml
        wf = Path(tmp) / ".github" / "workflows"
        wf.mkdir(parents=True)
        (wf / "launch-x.yaml").write_text("steps:\n  - uses: actions/checkout@v4\n")
        (wf / "other.yml").write_text("steps:\n  - uses: actions/checkout@v4\n")
        paths = default_paths(Path(tmp))
        if [p.name for p in paths] != ["launch-x.yaml"] or len(check(paths)) != 1:
            failed.append("the default file set is not launch-*/release* .yml and .yaml only")
    if failed:
        print("SELF-TEST FAILED: " + "\n  ".join(failed))
        return 1
    print(f"self-test OK: pinned shapes accepted, {len(bad)} unpinned shapes found")
    return 0


def main(argv: list[str]) -> int:
    if not argv or argv[0] not in ("check", "self-test"):
        print(__doc__)
        return 2
    if argv[0] == "self-test":
        return self_test()
    paths = [Path(a) for a in argv[1:]] or default_paths(Path("."))
    found = check(paths)
    for line in found:
        print(f"::error::{line}")
    if found:
        print(f"{len(found)} unpinned action reference(s) in launch workflows.")
        return 1
    print(f"All {len(paths)} launch workflow file(s) pin their actions to a commit.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
