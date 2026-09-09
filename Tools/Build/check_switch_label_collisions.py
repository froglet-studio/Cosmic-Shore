#!/usr/bin/env python3
"""Fail when two members of a first-party enum share a VALUE and both are used as
switch labels.

WHY THIS EXISTS
---------------
Two parallel game-mode branches each took the next free `GameModes` id, so a merge
produced `Hijack = 46` and `Tollway = 46` in one enum. C# allows duplicate enum
values (they are aliases), so nothing about the enum itself is wrong -- but every
`switch` that handles both is then a hard error:

    switch statement   ->  CS0152  "the switch statement contains multiple cases
                                    with the label value '46'"
    switch expression  ->  CS8510  "the pattern is unreachable; it has already
                                    been handled by a previous arm"

Neither is visible to a SYNTAX-only Roslyn pass (which is all an out-of-editor
compile can do for a file whose base type lives in the Assembly-CSharp monolith --
see CLAUDE.md, "Renaming or removing an ENUM MEMBER"), and neither is visible to
`check_enum_member_references.py`, which only asks whether a NAME exists. The
failure surfaces for the first time in the Unity console, on somebody else's
machine, after the merge has been pushed.

WHAT IT CHECKS
--------------
For every enum declared under Assets/_Scripts:
  * group its members by resolved integer value (following `A = B` aliases);
  * for any value carrying 2+ DISTINCT names, ask whether at least two of those
    names are used anywhere as a switch label (`case Enum.Name:` or an
    `Enum.Name =>` / `Enum.Name or ...` pattern arm).

Deliberate scope: an alias pair that nothing switches on is legal and common
(`Any = -1`, `Random = 0`, `None = 0`, `All = ~0`), so it is NOT reported. What
is reported is the pair that a switch actually has to tell apart.

    python3 Tools/Build/check_switch_label_collisions.py             # gate
    python3 Tools/Build/check_switch_label_collisions.py --self-test # negative control
"""
import os
import re
import sys

ROOT = os.path.join("Assets", "_Scripts")

ENUM_RE = re.compile(r'\benum\s+([A-Za-z_]\w*)\s*(?::\s*[\w\.]+\s*)?\{', re.M)
MEMBER_RE = re.compile(r'^\s*(?:\[[^\]]*\]\s*)*([A-Za-z_]\w*)\s*(?:=\s*([^,\n]+?))?\s*,?\s*(?://.*)?$')


def strip_comments(src):
    src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
    return "\n".join(re.sub(r'//.*$', '', ln) for ln in src.split("\n"))


def parse_enum_body(body):
    """Return {member: value} with `A = B` aliases and implicit increments resolved."""
    values, nxt = {}, 0
    for raw in body.split(","):
        line = raw.strip()
        if not line:
            continue
        m = MEMBER_RE.match(line if "\n" not in line else line.split("\n")[-1].strip())
        if not m:
            m = MEMBER_RE.match(line.split("\n")[-1].strip())
        if not m:
            continue
        name, expr = m.group(1), m.group(2)
        if expr is None:
            values[name] = nxt
            nxt += 1
            continue
        expr = expr.strip()
        try:
            values[name] = int(expr, 0)
        except ValueError:
            if expr in values:                      # `A = B` alias
                values[name] = values[expr]
            else:
                continue                            # computed/flag expression: skip
        nxt = values[name] + 1
    return values


def collect_enums(files):
    enums = {}
    for path in files:
        src = strip_comments(open(path, encoding="utf-8", errors="replace").read())
        for m in ENUM_RE.finditer(src):
            name = m.group(1)
            depth, i = 1, m.end()
            while i < len(src) and depth:
                if src[i] == '{':
                    depth += 1
                elif src[i] == '}':
                    depth -= 1
                i += 1
            body = src[m.end():i - 1]
            if name in enums:                       # two enums share a name: can't disambiguate
                enums[name] = None
                continue
            enums[name] = (path, parse_enum_body(body))
    return {k: v for k, v in enums.items() if v}


def collect_switch_labels(files):
    """{enum: {member: [(path, line)]}} for members used as a switch label."""
    case_re = re.compile(r'\bcase\s+([A-Z]\w*)\.([A-Za-z_]\w*)\s*(?::|\s+when\b)')
    arm_re = re.compile(r'(?:^|[\s(,]|=>\s*)([A-Z]\w*)\.([A-Za-z_]\w*)\s*(?:=>|\bor\b)')
    used = {}
    for path in files:
        for n, ln in enumerate(open(path, encoding="utf-8", errors="replace"), 1):
            s = ln.strip()
            if s.startswith("//") or s.startswith("///") or s.startswith("*"):
                continue
            for rx in (case_re, arm_re):
                for enum, member in rx.findall(ln):
                    used.setdefault(enum, {}).setdefault(member, []).append((path, n))
    return used


def run(files):
    enums = collect_enums(files)
    used = collect_switch_labels(files)
    problems = []
    for enum, (decl_path, members) in sorted(enums.items()):
        by_value = {}
        for name, val in members.items():
            by_value.setdefault(val, []).append(name)
        for val, names in sorted(by_value.items()):
            if len(names) < 2:
                continue
            switched = [n for n in names if n in used.get(enum, {})]
            if len(switched) < 2:
                continue
            sites = []
            for n in switched:
                sites += [f"{p}:{l} ({enum}.{n})" for p, l in used[enum][n][:3]]
            problems.append((enum, decl_path, val, switched, sites))
    return enums, problems


def main():
    self_test = "--self-test" in sys.argv
    files = []
    for d, _, fs in os.walk(ROOT):
        for f in fs:
            if f.endswith(".cs"):
                files.append(os.path.join(d, f))

    if self_test:
        import tempfile
        ok = True
        cases = [
            # (source, should_fire)
            ("enum E { A = 1, B = 1 }\nvoid F(E e){ switch(e){ case E.A: break; case E.B: break; } }", True),
            ("enum E { A = 1, B = 1 }\nint F(E e) => e switch { E.A => 0, E.B => 1 };", True),
            # alias nobody switches on -- legal, must NOT fire
            ("enum E { None = 0, Random = 0, A = 1 }\nint F(E e) => e switch { E.A => 0, _ => 1 };", False),
            # only ONE of the pair is switched -- legal, must NOT fire
            ("enum E { A = 1, B = 1 }\nint F(E e) => e switch { E.A => 0, _ => 1 };", False),
            # distinct values -- must NOT fire
            ("enum E { A = 1, B = 2 }\nint F(E e) => e switch { E.A => 0, E.B => 1 };", False),
        ]
        for i, (src, should) in enumerate(cases):
            with tempfile.TemporaryDirectory() as td:
                p = os.path.join(td, f"T{i}.cs")
                open(p, "w").write(src)
                _, probs = run([p])
                fired = bool(probs)
                mark = "ok " if fired == should else "FAIL"
                if fired != should:
                    ok = False
                print(f"  [{mark}] case {i}: expected {'a hit' if should else 'no hit'}, got {'a hit' if fired else 'no hit'}")
        print("self-test: OK" if ok else "self-test: FAILED")
        return 0 if ok else 1

    enums, problems = run(files)
    for enum, decl_path, val, names, sites in problems:
        print(f"COLLISION  {enum} value {val} is shared by {', '.join(names)}  ({decl_path})")
        for s in sites:
            print(f"    switched at {s}")
    if problems:
        print(f"\nswitch-label collision check: {len(problems)} collision(s) -- these are CS0152 / CS8510 in Unity.")
        return 1
    print(f"switch-label collision check: OK ({len(enums)} enums, {len(files)} files scanned)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
