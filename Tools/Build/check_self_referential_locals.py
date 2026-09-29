#!/usr/bin/env python3
"""Flag a C# local declaration whose own initializer references the local being declared.

    int seed = cableSeed != 0 ? cableSeed : (seed != 0 ? seed : DefaultSeed);
                                             ^^^^ binds to the LOCAL, not the field

A local's scope starts at its declarator, not after the `=`, so the inner name can never
reach an inherited or outer variable of the same name. The compiler says CS0165 ("use of
unassigned local variable") when the local is unassigned at that point - and says NOTHING
AT ALL when the initializer happens to be well-defined, which is the worse case: the code
then silently reads the local's default instead of the field it was reaching for.

Why this is a gate and not a review note: it is one of the two error classes that reached
the editor on this branch, and it is invisible to every check the repo can run offline.
An out-of-editor `mcs`/Roslyn pass cannot bind the body of a class whose base type lives in
the Assembly-CSharp monolith (Docs/ASSEMBLY_SPLIT.md, CLAUDE.md), and no Unity managed
assemblies exist in CI, so there is no compiler here to ask. This check needs no types at
all: a declarator naming itself in its own initializer is a defect on syntax alone.

Deliberately NOT flagged (each is legal and common):
  * a member access - `var node = node.Next` reaching a FIELD is still the bug, but
    `.name` as a member of something else is not, so a dotted occurrence never counts;
  * a named argument - `Foo(name: 1)`;
  * a sibling declarator - `int x = 0, y = x;` is fine, so each declarator is compared
    only against its OWN initializer;
  * a re-assignment - `a = () => a();` is the standard self-referencing-lambda idiom and
    is not a declaration.

Usage:
    check_self_referential_locals.py [--all] [--self-test] [paths...]

Default scope is the files changed against the base ref (origin/bleeding-edge, else
bleeding-edge, else HEAD). Unlike check_using_directives.py, `--all` is CLEAN over the
whole tree (0 findings across 1,849 files) and is safe to wire into CI - that gate has to
guess whether an unqualified name is first-party, and this one needs no such guess.

Measured while writing it, and each is a self-test case now: a first cut produced ~100
findings, every one a false positive of exactly three shapes - an optional parameter
default (`void F(int n = 1)`, whose "initializer" runs into the method body), a keyword in
the type position (`else offset = offset / d;` is a re-assignment), and an object
initializer naming a property (`new Foo { options = development }`). The gate is only worth
running because those are excluded; a noisy gate is one nobody reads.
"""

import argparse
import os
import re
import subprocess
import sys

# A local declaration STATEMENT: a type token and a name, at a statement boundary.
# The boundary is what separates it from an optional parameter default (`int n = 1`),
# which is the single biggest source of false matches - `void F(int n = 1) { ... n ... }`
# otherwise reads as a declaration whose initializer runs into the method body.
DECL = re.compile(
    r"""(?:^|(?<=[;{}]))\s*                 # statement boundary
        (var|[A-Za-z_][\w.]*(?:\s*<[^;{}()]*>)?(?:\s*\[\s*\])?)   # 1: the type token
        \s+
        ([A-Za-z_]\w*)                      # 2: the declared name
        \s*=(?![=>])                        # a real assignment: not ==, not =>
    """,
    re.VERBOSE | re.MULTILINE,
)

# A statement keyword in the type position means this is a re-assignment, not a
# declaration - `else offset = offset / d;` is the shape that proved it.
STATEMENT_KEYWORDS = {
    "else", "do", "try", "finally", "unchecked", "checked", "lock", "unsafe", "fixed",
    "return", "throw", "yield", "case", "default", "break", "continue", "goto",
}

KEYWORDS = {
    "return", "case", "else", "new", "out", "ref", "in", "is", "as", "await",
    "if", "while", "for", "foreach", "switch", "do", "throw", "yield", "using",
    "get", "set", "public", "private", "protected", "internal", "static", "readonly",
    "const", "override", "virtual", "abstract", "sealed", "partial", "class", "struct",
}


def strip_noise(text):
    """Blank out string/char literals and comments so their contents never match."""
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c == "/" and i + 1 < n and text[i + 1] == "/":
            j = text.find("\n", i)
            j = n if j < 0 else j
            out.append(" " * (j - i))
            i = j
        elif c == "/" and i + 1 < n and text[i + 1] == "*":
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append("".join(ch if ch == "\n" else " " for ch in text[i:j]))
            i = j
        elif c in "\"'":
            quote, j = c, i + 1
            while j < n:
                if text[j] == "\\":
                    j += 2
                    continue
                if text[j] == quote:
                    j += 1
                    break
                j += 1
            out.append("".join(ch if ch == "\n" else " " for ch in text[i:j]))
            i = j
        else:
            out.append(c)
            i += 1
    return "".join(out)


def initializer_span(text, pos):
    """Read the initializer starting at `pos` (just past `=`), to its `;` at depth 0.

    Returns None when the span leaves the declaration before terminating - an unmatched
    `)` or `}` means we were inside a parameter list or an object initializer, not a
    local declaration. That rejection is what keeps parameter defaults out."""
    depth = 0
    for i in range(pos, len(text)):
        c = text[i]
        if c in "([{":
            depth += 1
        elif c in ")]}":
            depth -= 1
            if depth < 0:
                return None          # ran out of the enclosing construct
        elif c == ";" and depth == 0:
            return text[pos:i]
    return None


def first_declarator(init):
    """Truncate at the first comma OUTSIDE brackets - `int x = 0, y = x;` declares two.

    Only ever SHORTENS the initializer, so a misread can cost a detection but can never
    invent one. That is the right direction for a gate."""
    depth = 0
    for i, c in enumerate(init):
        if c in "([{":
            depth += 1
        elif c in ")]}":
            depth -= 1
        elif c == "," and depth <= 0:
            return init[:i]
    return init


def find_offenders(text):
    """Yield (line_no, declared_name, initializer) for each self-referential declarator."""
    clean = strip_noise(text)
    hits = []
    for m in DECL.finditer(clean):
        type_token, name = m.group(1).strip(), m.group(2)
        if name in KEYWORDS or type_token in STATEMENT_KEYWORDS:
            continue
        raw = initializer_span(clean, m.end())
        if raw is None:
            continue
        init = first_declarator(raw)
        # The name standing alone, READ: not a member access, not a named argument, and
        # not an assignment target (`new Foo { options = x }` names Foo's property, not us).
        reads = [o for o in re.finditer(r"(?<![.\w])" + re.escape(name) + r"\b(?!\s*:)", init)
                 if not re.match(r"\s*=(?![=>])", init[o.end():])]
        if not reads:
            continue
        # A lambda that REBINDS the name as its own parameter is a different variable.
        if re.search(r"(?<![.\w])" + re.escape(name) + r"\s*=>", init):
            continue
        hits.append((clean.count("\n", 0, m.start(2)) + 1, name, init.strip()))
    return hits


def changed_files(explicit):
    if explicit:
        return [p for p in explicit if p.endswith(".cs")]
    base = None
    for ref in ("origin/bleeding-edge", "bleeding-edge"):
        if subprocess.run(["git", "rev-parse", "--verify", "-q", ref],
                          capture_output=True).returncode == 0:
            base = ref
            break
    if base is None:
        return []
    merge = subprocess.run(["git", "merge-base", "HEAD", base],
                           capture_output=True, text=True)
    ref = merge.stdout.strip() or base
    out = subprocess.run(["git", "diff", "--name-only", "--diff-filter=ACMR", f"{ref}..HEAD"],
                         capture_output=True, text=True).stdout.split()
    live = subprocess.run(["git", "diff", "--name-only", "--diff-filter=ACMR", "HEAD"],
                          capture_output=True, text=True).stdout.split()
    return sorted({p for p in out + live if p.endswith(".cs") and os.path.exists(p)})


def all_files():
    found = []
    for root, _, names in os.walk("Assets/_Scripts"):
        found += [os.path.join(root, n) for n in names if n.endswith(".cs")]
    return sorted(found)


def self_test():
    cases = [
        # (source, should_fire, label)
        ("int seed = cableSeed != 0 ? cableSeed : (seed != 0 ? seed : DefaultSeed);",
         True, "the shipped bug"),
        ("int x = x + 1;", True, "bare self-reference"),
        ("var node = node.Next;", True, "self-reference through a member access on ITSELF"),
        ("int x = 0, y = x;", False, "sibling declarator"),
        ("var seed = other.seed;", False, "member access on something else"),
        ("Foo f = Make(seed: seed);", False, "named argument, different local"),
        ("Func<int,int> f = x => x + 1;", False, "lambda parameter"),
        ('string s = "int s = s;";', False, "inside a string literal"),
        ("// int s = s;", False, "inside a comment"),
        ("a = () => a();", False, "re-assignment, not a declaration"),
        # The three shapes that false-positived against the real tree before the
        # keyword/assignment-target rules landed. Kept so they cannot come back.
        ("else offset = offset / d * Mathf.Clamp(d, inner, outer);",
         False, "Flora.cs: `else` is not a type - re-assignment"),
        ("else t0 = math.max(t0, t);",
         False, "ShieldShellMath.cs: same shape, one statement"),
        ("var options = new BuildPlayerOptions { scenes = s, options = development };",
         False, "BuildPipeline.cs: object-initializer property, not a read"),
    ]
    bad = 0
    for src, want, label in cases:
        got = bool(find_offenders(src))
        mark = "ok " if got == want else "FAIL"
        if got != want:
            bad += 1
        print(f"  [{mark}] fires={got!s:5} want={want!s:5}  {label}")
    print("self-test: " + ("OK" if not bad else f"{bad} FAILURE(S)"))
    return 1 if bad else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("paths", nargs="*")
    ap.add_argument("--all", action="store_true", help="scan all of Assets/_Scripts")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    files = all_files() if args.all else changed_files(args.paths)
    bad = 0
    for path in files:
        try:
            text = open(path, encoding="utf-8-sig").read()
        except (OSError, UnicodeDecodeError):
            continue
        for line, name, init in find_offenders(text):
            bad += 1
            print(f"{path}({line}): local '{name}' is referenced by its own initializer")
            print(f"    {name} = {init[:100]}")
    if bad:
        print(f"\nself-referential-local check: {bad} FAILURE(S)")
        return 1
    print(f"self-referential-local check: OK ({len(files)} files)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
