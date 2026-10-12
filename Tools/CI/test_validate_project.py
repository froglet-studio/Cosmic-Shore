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
    # A Shader Graph reader in Port/ carried the serialized type name as data
    # and turned the bleeding-edge guard red (2026-10-08). A literal is not a
    # reference; the token must survive stripping the literals to count.
    ("inside a string literal",
     'string t = "UnityEditor.ShaderGraph.GraphData";\n', False),
    ("inside a verbatim string literal",
     'string t = @"UnityEditor.ShaderGraph.GraphData";\n', False),
    ("escaped quote inside the literal",
     'string t = "say \\"UnityEditor\\" here";\n', False),
    ("a literal beside a real reference",
     'var w = UnityEditor.EditorWindow.focusedWindow; string t = "UnityEditor";\n', True),
    # The regex that first stripped literals hid a real reference in each of
    # the next three shapes (review of 8c6952942, reproduced end to end): an
    # interpolation hole is code, a verbatim string ends at the first lone
    # quote whatever precedes it, and a quote inside a char literal opens no
    # string. Each must still flag.
    ("reference inside an interpolation hole",
     'Debug.Log($"playing={UnityEditor.EditorApplication.isPlaying}");\n', True),
    ("verbatim string ending in a backslash",
     'var p = @"C:\\dir\\" + UnityEditor.AssetDatabase.GetAssetPath(x) + "y";\n', True),
    ("after a quote char literal",
     "if (c == '\"') UnityEditor.Selection.activeObject = o; var s = \"z\";\n", True),
    ("hole in a verbatim interpolated string",
     'var p = $@"C:\\{UnityEditor.EditorApplication.applicationPath}\\x";\n', True),
    ("quote inside a block comment",
     'int a = 1; /* " */ UnityEditor.Selection.activeObject = null;\n', True),
    # ...while the text around those holes is still data.
    ("text and nested literals of an interpolated string",
     'var s = $"{d["UnityEditor"]} {{UnityEditor}} {x:F2}";\n', False),
    ("doubled quote inside a verbatim literal",
     'var s = @"say ""UnityEditor"" here";\n', False),
    ("escaped quote char literal before a literal",
     "char q = '\\''; var s = \"UnityEditor\";\n", False),
    ("trailing comment",
     "int a = 1; // see UnityEditor.AssetDatabase\n", False),
]

# (relative path, should be skipped by collect)
EXCLUDED_PATH_CASES = [
    ("Assets/_Scripts/Thing.cs", False),
    ("Assets/Plugins/FMOD/src/fmod.cs", True),
    # Prisma is its own solution and CI; never part of a Unity player build.
    ("Port/src/CosmicShore.Content/Shaders/ShaderGraphAsset.cs", True),
    ("Port/tests/CosmicShore.Tests/ShaderGraphCompilerTests.cs", True),
    # Only the top-level Port/ is the port; a folder merely NAMED Port under
    # Assets is still player code.
    ("Assets/_Scripts/Port/Thing.cs", False),
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

    print("\nexcluded paths")
    for rel, expected in EXCLUDED_PATH_CASES:
        got = v.is_excluded(rel)
        ok = got == expected
        failures += not ok
        print(f"  {'PASS' if ok else 'FAIL'}  {rel:58} excluded={got} expected={expected}")

    total = len(EDITOR_CASES) + len(MONO_CASES) + len(EXCLUDED_PATH_CASES)
    print(f"\n{total - failures}/{total} passed")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
