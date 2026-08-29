#!/usr/bin/env python3
"""
Self-test for validate_project.py.

The editor-in-runtime check is the only one allowed to block a promotion, so
its preprocessor handling has to be right in both directions: a missed guard
blocks a perfectly good build, and a missed leak lets a broken one through to
UGS. Every case below came from something actually present in this repository
or from a mistake that is easy to make while fixing one.

Run: python3 Tools/CI/test_validate_project.py
"""

import importlib.util
import os
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("v", HERE / "validate_project.py")
v = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v)


# (name, source, should_be_flagged)
EDITOR_CASES = [
    ("plain runtime leak",
     "using UnityEditor;\nclass A {}\n", True),
    ("guarded",
     "#if UNITY_EDITOR\nusing UnityEditor;\n#endif\n", False),
    # Several files in this repo carry a BOM. Reading as plain utf-8 leaves it
    # on line 1 and hides the guard, which flagged two good files as broken.
    ("BOM then guard",
     "﻿#if UNITY_EDITOR\nusing UnityEditor;\n#endif\n", False),
    ("disabled with #if false",
     "#if false\nusing UnityEditor;\n#endif\n", False),
    ("disabled with a trailing comment",
     "#if false // UNITY_EDITOR\nusing UnityEditor;\n#endif\n", False),
    # An OR arm can still reach a player, so this is not editor-only.
    ("OR with a player symbol",
     "#if UNITY_EDITOR || UNITY_STANDALONE\nusing UnityEditor;\n#endif\n", True),
    ("AND chain is still editor-only",
     "#if UNITY_EDITOR && UNITY_ANDROID\nusing UnityEditor;\n#endif\n", False),
    ("the else arm is player code",
     "#if UNITY_EDITOR\nint a;\n#else\nusing UnityEditor;\n#endif\n", True),
    ("nested inside a guard",
     "#if UNITY_EDITOR\n#if UNITY_ANDROID\nusing UnityEditor;\n#endif\n#endif\n", False),
    ("guard closed before the use",
     "#if UNITY_EDITOR\nint a;\n#endif\nusing UnityEditor;\n", True),
    ("commented out",
     "// using UnityEditor;\n", False),
    ("unrelated platform guard",
     "#if UNITY_ANDROID\nusing UnityEditor;\n#endif\n", True),
]

MONO_CASES = [
    ("name matches",
     "Thing.cs", "public class Thing : MonoBehaviour {}\n", False),
    ("name does not match",
     "Thing.cs", "public class Other : MonoBehaviour {}\n", True),
    # A primary type plus helpers added via AddComponent is legal and common
    # here, so multi-MonoBehaviour files are deliberately not flagged.
    ("helper alongside the primary",
     "Thing.cs", "public class Thing : MonoBehaviour {}\n"
                 "class Helper : MonoBehaviour {}\n", False),
    ("not a MonoBehaviour",
     "Thing.cs", "public class Other : ScriptableObject {}\n", False),
]


def _write(src: str, name: str = "Thing.cs") -> tuple[Path, list[str]]:
    d = Path(tempfile.mkdtemp())
    (d / "Assets").mkdir()
    (d / "Assets" / name).write_text(src, encoding="utf-8")
    return d, [f"Assets/{name}"]


def main() -> int:
    failures = 0

    print("editor-in-runtime")
    for name, src, expected in EDITOR_CASES:
        root, files = _write(src)
        got = bool(v.check_editor_in_runtime(root, files))
        ok = got == expected
        failures += not ok
        print(f"  {'PASS' if ok else 'FAIL'}  {name:34} flagged={got} expected={expected}")

    print("\nmonobehaviour-name")
    for name, fname, src, expected in MONO_CASES:
        root, files = _write(src, fname)
        got = bool(v.check_monobehaviour_names(root, files))
        ok = got == expected
        failures += not ok
        print(f"  {'PASS' if ok else 'FAIL'}  {name:34} flagged={got} expected={expected}")

    total = len(EDITOR_CASES) + len(MONO_CASES)
    print(f"\n{total - failures}/{total} passed")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
