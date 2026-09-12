#!/usr/bin/env python3
"""Verify that every `EnumName.Member` reference in first-party C# names a member the
enum actually declares.

WHY THIS EXISTS, and why the compiler is not enough here.

The out-of-editor syntax gate (a `dotnet build` over the changed files with no Unity
assemblies) can only report errors Roslyn is still willing to bind. A file whose BASE
TYPE lives elsewhere in the `Assembly-CSharp` monolith — every MonoBehaviour and every
ScriptableObject in this project — fails to bind its class body at all, so a wrong enum
member inside a serialized field's DEFAULT is reported as nothing:

    [SerializeField] CombatHitClass hitClass = CombatHitClass.Missile;   // renamed away

That shipped once (2026-09, `CombatHitClass.Missile` -> `MissileDirect`): the gate came
back clean over the very file that carried it, and the Editor found it on the next
compile. Stubbing Unity's attributes does not fix it — measured; only the unresolved
base type matters — and stubbing the monolith is not a thing anyone will maintain.

So renames are checked TEXTUALLY, which needs no compiler and cannot be degraded by a
missing type. Cheap enough to run on every commit (~0.3 s over ~1,900 files).

Limits, stated rather than implied: this understands `EnumName.Member`. It does not see
a member reached through a `using static`, an alias, or a variable of the enum's type,
and it does not type-check anything else. It is a targeted guard for the one failure
mode above, not a substitute for compiling.

    python3 Tools/Build/check_enum_member_references.py
"""

import os
import re
import sys

# Tools/Build/<this file> -> Tools/Build -> Tools -> the repository root.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts")

# `public enum Name` / `public enum Name : byte`, then members up to the closing brace.
ENUM_RE = re.compile(
    r"\benum\s+(\w+)\s*(?::\s*\w+\s*)?\{(.*?)\}", re.S)
# Members every enum has from System.Enum / System.Object. A reference to one of these
# is correct code, not a stale member name.
INHERITED_MEMBERS = {
    "ToString", "Equals", "GetHashCode", "GetType", "CompareTo", "HasFlag",
    "Parse", "TryParse", "GetName", "GetNames", "GetValues", "IsDefined",
    "Format", "GetUnderlyingType", "ToObject",
}

ATTR_RE = re.compile(r"\[[^\]]*\]")
LEADING_IDENT_RE = re.compile(r"^\s*([A-Za-z_]\w*)")


def enum_members(body: str) -> "set[str]":
    """Members of one enum body.

    Split on COMMAS, not on lines: a single-line `enum Phase { A, B, C }` is common in
    this codebase, and a per-line parse silently captures only the first member — which
    then reports every other member of that enum as missing. That false-positive flood
    is what makes a gate get switched off, so it matters more than the true positives.
    """
    out = set()
    for chunk in ATTR_RE.sub(" ", body).split(","):
        m = LEADING_IDENT_RE.match(chunk)
        if m:
            out.add(m.group(1))
    return out


def strip_comments_and_strings(src: str) -> str:
    """Blank out things that look like code and are not.

    Deliberately keeps XML doc comments' `<see cref="Enum.Member"/>` OUT of scope: a
    stale cref is a documentation bug, not a compile error, and flagging it here would
    make the gate noisy enough to be ignored. Ordinary `//` and `/* */` comments go too,
    for the same reason.
    """
    out = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c == '"':
            # string literal (verbatim or not) - skip to its end
            if i >= 1 and src[i - 1] == '@':
                j = i + 1
                while j < n:
                    if src[j] == '"':
                        if j + 1 < n and src[j + 1] == '"':
                            j += 2
                            continue
                        break
                    j += 1
                i = j + 1
            else:
                j = i + 1
                while j < n and src[j] != '"':
                    j += 2 if src[j] == '\\' else 1
                i = j + 1
            out.append(" ")
            continue
        if c == "'":
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == '\\' else 1
            i = j + 1
            out.append(" ")
            continue
        if c == "/" and i + 1 < n and src[i + 1] == "/":
            j = src.find("\n", i)
            i = n if j < 0 else j
            out.append(" ")
            continue
        if c == "/" and i + 1 < n and src[i + 1] == "*":
            j = src.find("*/", i + 2)
            i = n if j < 0 else j + 2
            out.append(" ")
            continue
        out.append(c)
        i += 1
    return "".join(out)


def cs_files():
    for base, _dirs, names in os.walk(SCRIPTS):
        for name in names:
            if name.endswith(".cs"):
                yield os.path.join(base, name)


def main() -> int:
    files = sorted(cs_files())
    sources = {}
    for path in files:
        with open(path, encoding="utf-8", errors="replace") as fh:
            sources[path] = fh.read()

    # 1. Collect every first-party enum and its members.
    members: "dict[str, set[str]]" = {}
    ambiguous: "set[str]" = set()
    declared_in: "dict[str, str]" = {}
    for path, src in sources.items():
        for m in ENUM_RE.finditer(strip_comments_and_strings(src)):
            name, body = m.group(1), m.group(2)
            found = enum_members(body)
            if name in members and found != members[name]:
                # Two different enums share a name - a reference is unresolvable
                # textually, so stand down on that name rather than guess.
                ambiguous.add(name)
                continue
            members[name] = found
            declared_in[name] = os.path.relpath(path, ROOT)

    for name in ambiguous:
        members.pop(name, None)

    # 2. Check every `EnumName.Member` reference against it.
    ref_re = re.compile(r"\b(" + "|".join(sorted(map(re.escape, members))) + r")\.(\w+)")
    bad = []
    for path, src in sources.items():
        code = strip_comments_and_strings(src)
        for m in ref_re.finditer(code):
            enum_name, member = m.group(1), m.group(2)
            if member in members[enum_name] or member in INHERITED_MEMBERS:
                continue

            # A DOT before the enum name means this is not the enum. Two shapes hit
            # this and both are ordinary code, not defects:
            #   Slider.Direction.LeftToRight   - a nested type on some other type
            #   line.Direction.magnitude       - a property that happens to share a
            #                                    name with one of our enums
            # Only a reference that STARTS a qualified chain can be reasoned about
            # textually, so anything else is out of scope by construction.
            prev = code[:m.start()].rstrip()
            if prev.endswith("."):
                continue

            # camelCase names are instance members, never members of an enum in this
            # codebase (the convention is PascalCase with explicit values).
            if member[:1].islower():
                continue

            line = code.count("\n", 0, m.start()) + 1
            bad.append((os.path.relpath(path, ROOT), line, enum_name, member,
                        declared_in[enum_name]))

    if bad:
        print("enum-member reference check: FAILED\n")
        for path, line, enum_name, member, decl in bad:
            print(f"  {path}({line}): '{enum_name}' has no member '{member}'")
            print(f"      declared in {decl}: "
                  f"{', '.join(sorted(members[enum_name])) or '(none)'}")
        return 1

    skipped = f", {len(ambiguous)} ambiguous name(s) skipped" if ambiguous else ""
    print(f"enum-member reference check: OK "
          f"({len(members)} enums, {len(files)} files scanned{skipped})")
    return 0


def self_test() -> int:
    """Negative control: a gate nobody has watched FAIL is a gate nobody should trust.

    Synthesizes the exact shape that escaped in 2026-09 - a renamed member used as a
    serialized field's default inside a ScriptableObject - plus the three shapes that
    must NOT fire, and checks the classifier reports each correctly. Runs against the
    parsing/classification logic directly, so it needs no files on disk.
    """
    body = "Bullet = 0, MissileDirect = 1, Debuff = 2"
    got = enum_members(body)
    checks = [
        ("single-line enum body parses every member",
         got == {"Bullet", "MissileDirect", "Debuff"}, got),
        ("a one-line enum is not truncated to its first member",
         len(got) == 3, got),
    ]

    # The classifier, exercised through the same predicates main() uses.
    def classify(code: str, enum_name: str, valid: "set[str]"):
        rx = re.compile(r"\b" + re.escape(enum_name) + r"\.(\w+)")
        hits = []
        for m in rx.finditer(strip_comments_and_strings(code)):
            member = m.group(1)
            if member in valid or member in INHERITED_MEMBERS:
                continue
            prev = strip_comments_and_strings(code)[:m.start()].rstrip()
            if prev.endswith(".") or member[:1].islower():
                continue
            hits.append(member)
        return hits

    valid = {"Bullet", "MissileDirect", "Debuff"}
    checks += [
        ("the escaped bug is caught",
         classify("[SerializeField] CombatHitClass h = CombatHitClass.Missile;",
                  "CombatHitClass", valid) == ["Missile"], None),
        ("a nested type on another type is not a defect",
         classify("s.direction = Slider.Direction.LeftToRight;",
                  "Direction", {"Left", "Right"}) == [], None),
        ("an instance member on a same-named property is not a defect",
         classify("var x = line.Direction.magnitude;",
                  "Direction", {"Left", "Right"}) == [], None),
        ("System.Enum's own members are not defects",
         classify("var s = CombatHitClass.ToString();",
                  "CombatHitClass", valid) == [], None),
        ("a reference inside a comment is not a defect",
         classify("// see CombatHitClass.Missile for the old name",
                  "CombatHitClass", valid) == [], None),
        ("a reference inside a string is not a defect",
         classify('var s = "CombatHitClass.Missile";',
                  "CombatHitClass", valid) == [], None),
    ]

    failed = [(name, extra) for name, ok, extra in checks if not ok]
    for name, ok, _extra in checks:
        print(f"  {'PASS' if ok else 'FAIL'}  {name}")
    if failed:
        for name, extra in failed:
            print(f"\nself-test FAILED: {name}" + (f" (got {extra})" if extra else ""))
        return 1
    print(f"\nself-test: OK ({len(checks)} checks)")
    return 0


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    sys.exit(main())
