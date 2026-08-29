#!/usr/bin/env python3
"""
Catch Release-player-build compile breaks that are INVISIBLE in the Editor.

Why this exists
---------------
The bleeding-edge build failed with:

    LoadInsightsRuntime.cs(27,40): error CS0246: The type or namespace name
    'MonoBehaviour' could not be found
    -> Error building Player because scripts had compiler errors

The file guarded ALL its usings - including `using UnityEngine;` - behind
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`, but declared
`public class LoadInsightsRuntime : MonoBehaviour` OUTSIDE that guard. In the
Editor and in Development builds the symbol is defined, so it compiles and looks
correct. In a Release player build the using is stripped while the base-class
reference survives, and the type stops resolving.

Nothing in the normal loop catches this: the Editor always defines UNITY_EDITOR,
so you can work all day without seeing it. Only a Release player build fails, and
by then it is the automated build that breaks. This script reproduces the
Release preprocessor cheaply, in plain Python, with no Unity install required -
so it can run on every PR.

Checks
------
A. A type declared OUTSIDE all guards whose base list needs a `using` that is
   INSIDE a guard. (The exact bug above.)
B. A runtime file (not under an Editor/ folder, not an editor/test asmdef)
   touching the `UnityEditor` namespace outside a `#if UNITY_EDITOR` guard.
   UnityEditor does not exist in a player build, so this fails the same way.

Usage
-----
    python3 Tools/Build/check_conditional_compilation.py [--root Assets]

Exit code 0 = clean, 1 = violations found (fails CI).
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys

# Base types that give away which namespace an unguarded declaration depends on.
BASE_TYPE_NAMESPACES = {
    "UnityEngine": [
        "MonoBehaviour", "ScriptableObject", "StateMachineBehaviour",
        "PropertyAttribute", "AssetBundle",
    ],
    "UnityEditor": [
        "Editor", "EditorWindow", "PropertyDrawer", "ScriptableWizard",
        "AssetPostprocessor", "AssetModificationProcessor", "DecoratorDrawer",
    ],
    "Unity.Netcode": ["NetworkBehaviour"],
    "UnityEngine.UI": ["Graphic", "MaskableGraphic", "Selectable"],
    "UnityEngine.EventSystems": ["UIBehaviour"],
}

DECL_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:public|internal|private|protected|sealed|abstract|partial|static|\s)*"
    r"(?:class|struct)\s+\w+\s*(?:<[^>]*>)?\s*:\s*(?P<bases>[^{;]+)"
)
USING_RE = re.compile(r"^\s*using\s+(?:static\s+)?(?P<ns>[A-Za-z_][\w.]*)\s*;")
# `SomeAlias = UnityEditor.Foo;` still pulls the namespace in.
USING_ALIAS_RE = re.compile(r"^\s*using\s+\w+\s*=\s*(?P<ns>[A-Za-z_][\w.]*)")

UNITY_EDITOR_TOKEN_RE = re.compile(r"\bUnityEditor\b")

# Guards that make a region editor-or-dev-only. A region under any of these is
# absent from a Release player build.
EDITOR_ONLY_GUARD_RE = re.compile(r"\bUNITY_EDITOR\b")

# `#if false` / `#if 0` excludes a region from EVERY configuration, so code inside
# it can never reach a player build and is not a hazard.
ALWAYS_FALSE_GUARD_RE = re.compile(r"^#(?:el)?if\s+(?:false|0)\s*$")


def region_excluded_from_player(guard_stack: list[str]) -> bool:
    """True if the enclosing guards keep this region out of a Release player build."""
    return any(EDITOR_ONLY_GUARD_RE.search(g) or ALWAYS_FALSE_GUARD_RE.match(g)
               for g in guard_stack)


def strip_noise(text: str) -> str:
    """Remove block comments, line comments and string literals.

    Keeps line structure intact so reported line numbers stay accurate.
    """
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                i += 1
        elif c == "/" and i + 1 < n and text[i + 1] == "*":
            i += 2
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                if text[i] == "\n":
                    out.append("\n")
                i += 1
            i += 2
        elif c in "\"'":
            quote = c
            verbatim = i > 0 and text[i - 1] == "@"
            i += 1
            while i < n:
                if text[i] == "\\" and not verbatim:
                    i += 2
                    continue
                if text[i] == quote:
                    i += 1
                    break
                if text[i] == "\n":
                    out.append("\n")
                i += 1
        else:
            out.append(c)
            i += 1
    return "".join(out)


def analyse(path: str, text: str, runtime: bool) -> list[dict]:
    """Return violations for one file."""
    lines = strip_noise(text).split("\n")

    guard_stack: list[str] = []
    guarded_usings: dict[str, int] = {}   # namespace -> line it was guarded on
    unguarded_usings: set[str] = set()
    violations: list[dict] = []

    for idx, raw in enumerate(lines, start=1):
        s = raw.lstrip("\ufeff").strip()

        if s.startswith("#if"):
            guard_stack.append(s)
            continue
        if s.startswith("#elif"):
            if guard_stack:
                guard_stack[-1] = s
            continue
        if s.startswith("#else"):
            continue
        if s.startswith("#endif"):
            if guard_stack:
                guard_stack.pop()
            continue

        inside_guard = bool(guard_stack)

        # --- Check B: runtime file touching UnityEditor outside a UNITY_EDITOR guard ---
        # Runs BEFORE the `using` handling below, because `using UnityEditor;` is the
        # single most common form of this bug and must not be skipped as "just a using".
        if runtime and UNITY_EDITOR_TOKEN_RE.search(s):
            if not region_excluded_from_player(guard_stack):
                violations.append({
                    "file": path, "line": idx, "check": "B",
                    "message": (
                        "runtime script references 'UnityEditor' outside a "
                        "'#if UNITY_EDITOR' guard. The UnityEditor assembly does not "
                        "exist in a player build, so this breaks Release (CS0246). "
                        "Wrap it in '#if UNITY_EDITOR', or move the file under an "
                        "Editor/ folder."
                    ),
                })

        m = USING_RE.match(s) or USING_ALIAS_RE.match(s)
        if m:
            ns = m.group("ns")
            if inside_guard:
                guarded_usings.setdefault(ns, idx)
            else:
                unguarded_usings.add(ns)
            continue

        # --- Check A: unguarded declaration depending on a guarded using ---
        if not inside_guard:
            d = DECL_RE.match(s)
            if d:
                bases = [b.strip().split("<")[0].split(".")[-1]
                         for b in d.group("bases").split(",")]
                for ns, types in BASE_TYPE_NAMESPACES.items():
                    if ns in unguarded_usings:
                        continue          # using is available unconditionally
                    if ns not in guarded_usings:
                        continue          # not guarded either; fully qualified, or N/A
                    hit = next((t for t in bases if t in types), None)
                    if hit:
                        violations.append({
                            "file": path, "line": idx, "check": "A",
                            "message": (
                                f"type declared outside any #if guard derives from "
                                f"'{hit}', but 'using {ns};' is guarded "
                                f"(line {guarded_usings[ns]}). In a Release player "
                                f"build the using is stripped and '{hit}' will not "
                                f"resolve (CS0246). Move 'using {ns};' outside the guard."
                            ),
                        })

    return violations


def is_runtime_file(path: str, editor_asmdef_dirs: list[str]) -> bool:
    """True if this file compiles into the player (not editor-only)."""
    parts = path.replace("\\", "/").split("/")
    if "Editor" in parts:
        return False
    norm = path.replace("\\", "/")
    return not any(norm.startswith(d) for d in editor_asmdef_dirs)


def find_editor_asmdef_dirs(root: str) -> list[str]:
    """Directories owned by an asmdef that is editor-only or a test assembly."""
    dirs = []
    for cur, _, files in os.walk(root):
        for fn in files:
            if not fn.endswith(".asmdef"):
                continue
            try:
                with open(os.path.join(cur, fn), encoding="utf-8") as fh:
                    data = json.load(fh)
            except Exception:
                continue
            platforms = data.get("includePlatforms") or []
            is_editor_only = platforms == ["Editor"]
            is_test = bool(data.get("defineConstraints")) and any(
                "UNITY_INCLUDE_TESTS" in c for c in data["defineConstraints"]
            )
            if is_editor_only or is_test:
                dirs.append(cur.replace("\\", "/").rstrip("/") + "/")
    return dirs


# --------------------------------------------------------------------------
# Self-test
#
# A checker that silently stops detecting is worse than no checker. During
# development this one no-opped TWICE (a `continue` that skipped check B, then a
# UTF-8 BOM that hid `#if` on line 1), and both times it still reported "OK".
# These fixtures run in CI so that can't happen again unnoticed.
# --------------------------------------------------------------------------
SELF_TESTS = [
    # (name, relative path, source, runtime?, expected check or None)
    ("A: guarded using + unguarded MonoBehaviour decl", "Runtime/A_bad.cs",
     "#if UNITY_EDITOR || DEVELOPMENT_BUILD\nusing UnityEngine;\n#endif\n"
     "public class A : MonoBehaviour\n{\n#if UNITY_EDITOR\n    int x;\n#endif\n}\n", True, "A"),
    ("A: using outside guard (the correct layout)", "Runtime/A_good.cs",
     "using UnityEngine;\n#if UNITY_EDITOR\nusing System;\n#endif\n"
     "public class A2 : MonoBehaviour\n{\n}\n", True, None),
    ("B: unguarded 'using UnityEditor' in runtime file", "Runtime/B_bad.cs",
     "using UnityEngine;\nusing UnityEditor;\npublic class B : MonoBehaviour { }\n", True, "B"),
    ("B: 'using UnityEditor' properly guarded", "Runtime/B_good.cs",
     "using UnityEngine;\n#if UNITY_EDITOR\nusing UnityEditor;\n#endif\n"
     "public class B2 : MonoBehaviour { }\n", True, None),
    ("B: whole file behind '#if false' is inert", "Runtime/B_false.cs",
     "#if false\nusing UnityEditor;\npublic class B3 : Editor { }\n#endif\n", True, None),
    ("B: BOM'd '#if UNITY_EDITOR' on line 1 still counts as a guard", "Runtime/B_bom.cs",
     "﻿#if UNITY_EDITOR\nusing UnityEditor;\npublic class B4 : Editor { }\n#endif\n", True, None),
    ("B: editor-only file is exempt", "Editor/B_editor.cs",
     "using UnityEngine;\nusing UnityEditor;\npublic class B5 : EditorWindow { }\n", False, None),
    ("B: 'UnityEditor' only in a comment or string is not a reference", "Runtime/B_noise.cs",
     "using UnityEngine;\n// once called UnityEditor.AssetDatabase\n"
     "public class B6 : MonoBehaviour { string s = \"UnityEditor\"; }\n", True, None),
]


def run_self_test() -> int:
    failures = 0
    for name, path, src, runtime, expected in SELF_TESTS:
        got = analyse(path, src, runtime)
        codes = sorted({v["check"] for v in got})
        ok = (codes == [expected]) if expected else (codes == [])
        print(f"  {'PASS' if ok else 'FAIL'}  {name}")
        if not ok:
            failures += 1
            print(f"        expected {expected or 'no violation'}, got {codes or 'none'}")
    print(f"\nself-test: {len(SELF_TESTS) - failures}/{len(SELF_TESTS)} passed")
    return 1 if failures else 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", default="Assets", help="directory to scan (default: Assets)")
    ap.add_argument("--self-test", action="store_true",
                    help="verify the checker still detects its own fixtures, then exit")
    args = ap.parse_args()

    if args.self_test:
        return run_self_test()

    if not os.path.isdir(args.root):
        print(f"error: no such directory: {args.root}", file=sys.stderr)
        return 2

    editor_dirs = find_editor_asmdef_dirs(args.root)

    violations: list[dict] = []
    scanned = 0
    for cur, dirnames, files in os.walk(args.root):
        # Third-party code is not ours to police.
        dirnames[:] = [d for d in dirnames if d not in
                       ("PackageCache", "Plugins", "PlayFabSDK", "Wwise",
                        "NiceVibrations", "Unity Assests")]
        for fn in files:
            if not fn.endswith(".cs"):
                continue
            p = os.path.join(cur, fn).replace("\\", "/")
            try:
                with open(p, encoding="utf-8-sig", errors="replace") as fh:
                    text = fh.read()
            except OSError:
                continue
            scanned += 1
            if "#if" not in text:
                continue      # no conditional compilation: cannot exhibit either bug
            violations.extend(analyse(p, text, is_runtime_file(p, editor_dirs)))

    if not violations:
        print(f"conditional-compilation check: OK ({scanned} files scanned)")
        return 0

    print(f"conditional-compilation check: {len(violations)} violation(s) "
          f"({scanned} files scanned)\n")
    for v in violations:
        # GitHub Actions renders this as an inline annotation on the PR.
        print(f"::error file={v['file']},line={v['line']}::{v['message']}")
        print(f"  {v['file']}:{v['line']}  [check {v['check']}]\n    {v['message']}\n")
    print("See Docs/CONDITIONAL_COMPILATION.md for the rule and the fix.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
