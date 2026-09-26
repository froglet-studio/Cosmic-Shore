#!/usr/bin/env python3
"""A rest scale cached before the thing that OWNS the layout has run is a stale rest scale.

`AbilityLockupView` re-homes every vessel's ability button into the fleet row and normalises its
`localScale` to 1 - which happens long after `Awake`. So a component that caches
`transform.localScale` in `Awake` / `Start` / `OnEnable` caches whatever the PREFAB authored, and
then writes that stale value back on its next release or disable.

That shipped: every Squirrel ability button is authored at 0.7 and carries
`AbilityButtonPressJuice`, whose `Awake` captured 0.7 and whose `OnDisable` wrote it back on every
hide of the HUD. Four of the row's five cards sat at 0.7 beside the one card with no juice on it -
which reads on screen as that ONE card being oversized, because four wrong cards agree with each
other.

This gate fails on any UI component that captures a scale into a FIELD during an early Unity
lifecycle method. The fix is always the same shape: capture lazily (at the moment you are about to
animate) so you only ever restore a value you took yourself, and let the layout owner hand you the
new rest outright.

    python3 Tools/Build/check_rest_scale_capture.py --check
    python3 Tools/Build/check_rest_scale_capture.py --self-test
"""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
assert (ROOT / "Assets").is_dir(), f"ROOT is not the project root: {ROOT}"

# UI only, and deliberately so: the rule is about a LAYOUT OWNER writing a scale after Awake, and
# the only such owners are UI. `Skimmer` and `ShieldSkimmerScaleDriver` capture an authored shape in
# Awake too, and there it is exactly right - nothing re-homes a skimmer into a row. Widening this to
# the vessel tree would make every hit an exemption, which trains people to stop reading the gate.
SCAN_DIRS = ["Assets/_Scripts/UI"]

# The Unity entry points that run BEFORE a layout owner has had a chance to place anything.
EARLY = ("Awake", "OnEnable", "Start")

# A field assignment (or expression-bodied method) whose right-hand side reads a scale.
SCALE_READ = re.compile(r"\.localScale\b")
# `_field = ...` / `this._field = ...` - a capture INTO state, as opposed to a local.
FIELD_ASSIGN = re.compile(r"(?:^|[^\w.])(?:this\.)?(_\w+|\w+Scale)\s*=\s*[^=]")

# Reviewed exemptions: "file::member" -> why it is safe.
ALLOWED = {}


def method_bodies(src: str):
    """Yield (name, body) for each method whose body we can brace-match, plus expression bodies."""
    for m in re.finditer(r"\b(?:void|private void|protected void|public void)?\s*\b(\w+)\s*\(\s*\)\s*(=>|\{)", src):
        name, kind = m.group(1), m.group(2)
        if kind == "=>":
            end = src.find(";", m.end())
            yield name, src[m.end():end if end > 0 else m.end()]
            continue
        depth, i = 0, m.end() - 1
        while i < len(src):
            if src[i] == "{":
                depth += 1
            elif src[i] == "}":
                depth -= 1
                if depth == 0:
                    break
            i += 1
        yield name, src[m.end():i]


def findings_for(path: Path, src: str):
    out = []
    for name, body in method_bodies(src):
        if name not in EARLY:
            continue
        for line in body.splitlines():
            line = line.split("//", 1)[0]
            if not SCALE_READ.search(line):
                continue
            if not FIELD_ASSIGN.search(line):
                continue
            key = f"{path.name}::{name}"
            if key in ALLOWED:
                continue
            out.append((key, line.strip()))
    return out


def scan():
    findings = []
    for d in SCAN_DIRS:
        for p in sorted((ROOT / d).rglob("*.cs")):
            findings += [(p, k, l) for k, l in findings_for(p, p.read_text(encoding="utf-8", errors="replace"))]
    return findings


def self_test():
    cases = [
        # (label, source, expect a finding)
        ("the shipped bug", "class A { void Awake() => _restScale = transform.localScale; }", True),
        ("braced Awake", "class A { void Awake() { _rest = transform.localScale; } }", True),
        ("OnEnable capture", "class A { void OnEnable() { _restScale = rectTransform.localScale; } }", True),
        ("Start capture", "class A { void Start() { this._restScale = target.localScale; } }", True),
        ("lazy capture (the fix)", "class A { void EnsureRest() { _restScale = transform.localScale; } }", False),
        ("a local, not a field", "class A { void Awake() { Vector3 s = transform.localScale; } }", False),
        ("WRITING a scale in Awake", "class A { void Awake() { transform.localScale = Vector3.one; } }", False),
        ("commented out", "class A { void Awake() { // _restScale = transform.localScale;\n } }", False),
    ]
    bad = 0
    for label, src, expect in cases:
        got = bool(findings_for(Path("Case.cs"), src))
        ok = got == expect
        bad += not ok
        print(f"  {'ok ' if ok else 'FAIL'}  {label}: expected {'a finding' if expect else 'clean'}, got {'a finding' if got else 'clean'}")
    return bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        bad = self_test()
        print("self-test: all cases behaved" if not bad else f"self-test: {bad} case(s) misbehaved")
        return 0 if not bad else 1

    findings = scan()
    for p, key, line in findings:
        print(f"{p.relative_to(ROOT)}: {key} caches a scale before any layout owner has run")
        print(f"    {line}")
    if findings:
        print(f"\n{len(findings)} early scale capture(s). Capture lazily instead, so the component "
              f"only ever restores a scale it took itself.")
        return 1
    print(f"clean: no early scale capture in {', '.join(SCAN_DIRS)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
