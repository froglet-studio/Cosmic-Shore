#!/usr/bin/env python3
"""Flag a three-or-more-value Mathf.Min / Mathf.Max call in runtime C#.

    float m = Mathf.Max(Mathf.Abs(u), Mathf.Abs(r), 1e-4f);
              ^^^^^^^^^ UnityEngine.Mathf has no 3-argument overload: this binds to
                        Max(params float[]) and ALLOCATES a float[] on every call

UnityEngine.Mathf declares Min/Max for two values and for `params float[]` / `params int[]`,
nothing in between. So a third argument silently turns a free comparison into a heap
allocation - per call, in whatever loop it sits in. Measured 2026-10-06: the Skim Race AI's
planner made 840 of them per frame with two AI seats (36 KB, half the frame's garbage),
from five such calls in its rollout steering. Nothing flags it: it compiles, it is correct,
and the offline simulator's Unity shim defined a 3-argument overload Unity does not have, so
the simulator never allocated at all.

The fix is CosmicShore.Utility.MathfNoAlloc.Min/Max(a, b, c), which returns the params form's
answer bit for bit. Nested two-value calls are fine too.

Scope: every .cs under Assets/_Scripts except Editor/ and Tests/ folders (editor tooling may
allocate). The whole scope was cleaned when this gate was written, so it runs over all of it
by default and must stay at 0.

Usage:
    check_mathf_params_alloc.py [--self-test] [paths...]
"""

import argparse
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts")
CALL = re.compile(r"\bMathf\s*\.\s*(Min|Max)\s*\(")
# A generic argument list, e.g. GetComponent<Foo>() or Dictionary<int, float>: its commas are
# not call arguments. Collapsed before counting. `?` and `:` are excluded so a ternary
# comparison (a < b ? x : y) is never mistaken for one.
GENERIC = re.compile(r"(\w)\s*<[\w\s,.\[\]]*>")


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


def find_offenders(text):
    """(line, method, argCount, snippet) for every Mathf.Min/Max call with 3+ arguments."""
    clean = strip_noise(text)
    prev = None
    while prev != clean:  # nested generics collapse from the inside out
        prev, clean = clean, GENERIC.sub(lambda m: m.group(1) + " " * (len(m.group(0)) - 1), clean)
    found = []
    for m in CALL.finditer(clean):
        depth, args, j = 1, 1, m.end()
        while depth and j < len(clean):
            c = clean[j]
            if c in "([{":
                depth += 1
            elif c in ")]}":
                depth -= 1
            elif c == "," and depth == 1:
                args += 1
            j += 1
        if args >= 3:
            line = clean.count("\n", 0, m.start()) + 1
            snippet = " ".join(text[m.start():j].split())
            found.append((line, m.group(1), args, snippet))
    return found


def runtime_files():
    for dirpath, dirnames, filenames in os.walk(SCRIPTS):
        dirnames[:] = [d for d in dirnames if d not in ("Editor", "Tests")]
        for f in filenames:
            if f.endswith(".cs"):
                yield os.path.join(dirpath, f)


def self_test():
    flag = [
        "float m = Mathf.Max(a, b, c);",
        "float m = Mathf.Min(a,\n    Foo(b, c),\n    d);",
        "int i = Mathf.Max(1, 2, 3, 4);",
        "x = Bar(Mathf.Min(a, b, c));",
        "x = Mathf . Max (a, b, c);",
        "float m = Mathf.Max(GetComponent<Foo>().x, b, c);",
    ]
    keep = [
        "float m = Mathf.Max(a, b);",
        "float m = Mathf.Max(Foo(a, b), c);",
        "float m = Mathf.Max(Mathf.Max(a, b), c);",
        "// Mathf.Max(a, b, c)",
        "/* Mathf.Min(a, b, c) */",
        'string s = "Mathf.Max(a, b, c)";',
        "float m = Mathf.Max(a, new Vector2(b, c).x);",
        "float m = Mathf.Max(a, Get<int, float>(b));",
        "float m = Mathf.Max(a, Lookup<Dictionary<int, float>>(b));",
        "float m = Mathf.Max(a < b ? a : b, c);",
        "float m = MathfNoAlloc.Max(a, b, c);",
        "float m = math.max(a, math.max(b, c));",
    ]
    bad = 0
    for src in flag:
        if not find_offenders(src):
            bad += 1
            print(f"self-test: NOT flagged but should be: {src!r}")
    for src in keep:
        if find_offenders(src):
            bad += 1
            print(f"self-test: flagged but should not be: {src!r}")
    print("self-test: " + ("OK" if not bad else f"{bad} FAILURE(S)"))
    return 1 if bad else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("paths", nargs="*")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()

    files = args.paths or sorted(runtime_files())
    bad = 0
    for path in files:
        try:
            text = open(path, encoding="utf-8-sig").read()
        except (OSError, UnicodeDecodeError):
            continue
        for line, method, count, snippet in find_offenders(text):
            bad += 1
            rel = os.path.relpath(path, ROOT)
            print(f"{rel}({line}): Mathf.{method} with {count} values allocates a params array - "
                  f"use MathfNoAlloc.{method} or nest two-value calls")
            print(f"    {snippet[:110]}")
    if bad:
        print(f"\nmathf-params-alloc check: {bad} FAILURE(S)")
        return 1
    print(f"mathf-params-alloc check: OK ({len(files)} files)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
