#!/usr/bin/env python3
"""check_orthographic_views.py - no orthographic view bows its picture or blooms its 2D marks by default.

Garrett, 2026-10-22: "make sure anyone else using orthographic view never applies a panini or bloom to 2d assets by
default". The rule has two halves, and this gate holds the parts a file can show:

    lens      Assets/_Scripts/Utility/OrthographicLens.cs installs itself at load and zeroes the volumes' Panini for every
              orthographic Game camera. It must keep its [RuntimeInitializeOnLoadMethod] hook, its beginCameraRendering
              subscription and its PaniniProjection zeroing, or every orthographic view is bowed again.
    marks     every runtime script that makes a camera orthographic (`.orthographic = true`, or a variable) draws its 2D
              marks through an UnprocessedOverlay (Assets/_Scripts/Utility/UnprocessedOverlay.cs), so the view's bloom and
              tonemapper never reach them; or says why it has none with a `// ortho-marks: <reason>` comment.

    python3 Tools/Build/check_orthographic_views.py              # the shipped scripts
    python3 Tools/Build/check_orthographic_views.py --self-test  # each check fires on a broken copy (negative controls)

A READER: it writes nothing.
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts")
LENS = os.path.join(SCRIPTS, "Utility", "OrthographicLens.cs")
OVERLAY = os.path.join(SCRIPTS, "Utility", "UnprocessedOverlay.cs")

MAKES_ORTHO = re.compile(r"\.orthographic\s*=(?!\s*false\b)[^;=]+;")
EXEMPT = re.compile(r"//\s*ortho-marks:\s*\S")
LENS_NEEDS = {
    "load hook": re.compile(r"\[RuntimeInitializeOnLoadMethod"),
    "beginCameraRendering subscription": re.compile(r"beginCameraRendering\s*\+=\s*\w+"),
    "Panini zeroed": re.compile(r"GetComponent<PaniniProjection>\(\)[\s\S]*distance\.value\s*=\s*0f"),
}


def read(path):
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read()


def runtime_scripts():
    for d, _, files in os.walk(SCRIPTS):
        parts = d.replace("\\", "/").split("/")
        if "Editor" in parts or "Tests" in parts:
            continue
        for f in files:
            if f.endswith(".cs"):
                yield os.path.join(d, f)


def strip_comments(src):
    src = re.sub(r"/\*[\s\S]*?\*/", "", src)
    return re.sub(r"//[^\n]*", "", src)


def lens_errors(src):
    return [f"OrthographicLens.cs: missing its {name}" for name, rx in LENS_NEEDS.items() if not rx.search(src)]


def marks_errors(path, src):
    if os.path.abspath(path) == os.path.abspath(OVERLAY):
        return []
    code = strip_comments(src)
    if not MAKES_ORTHO.search(code):
        return []
    if "UnprocessedOverlay" in code or EXEMPT.search(src):
        return []
    rel = os.path.relpath(path, ROOT)
    return [f"{rel}: makes a camera orthographic but draws no marks through an UnprocessedOverlay "
            "(or add `// ortho-marks: <why it has none>`)"]


def run():
    errors = []
    if not os.path.exists(LENS):
        errors.append("Assets/_Scripts/Utility/OrthographicLens.cs is missing")
    else:
        errors += lens_errors(read(LENS))
    checked = 0
    for path in runtime_scripts():
        src = read(path)
        if MAKES_ORTHO.search(strip_comments(src)):
            checked += 1
        errors += marks_errors(path, src)
    return errors, checked


def self_test():
    ok = True
    lens = read(LENS)
    for name, rx in LENS_NEEDS.items():
        broken = rx.sub("REMOVED", lens)
        if not any(name in e for e in lens_errors(broken)):
            print(f"self-test FAILED: removing the lens's {name} was not caught")
            ok = False
    fake = os.path.join(SCRIPTS, "Fake", "FakeView.cs")
    bare = "class V { void A(Camera c) { c.orthographic = true; } }"
    if not marks_errors(fake, bare):
        print("self-test FAILED: an orthographic view with no overlay was not caught")
        ok = False
    if marks_errors(fake, bare + " // ortho-marks: draws no marks"):
        print("self-test FAILED: the ortho-marks exemption was not honoured")
        ok = False
    if marks_errors(fake, bare + " UnprocessedOverlay _m;"):
        print("self-test FAILED: a view with an UnprocessedOverlay was flagged")
        ok = False
    if marks_errors(fake, "class V { void A(Camera c) { c.orthographic = false; } }"):
        print("self-test FAILED: turning a camera perspective was flagged")
        ok = False
    if marks_errors(fake, "class V { /* c.orthographic = true; */ }"):
        print("self-test FAILED: a commented-out assignment was flagged")
        ok = False
    print("check_orthographic_views self-test: " + ("OK" if ok else "FAILED"))
    return 0 if ok else 1


def main():
    if "--self-test" in sys.argv:
        return self_test()
    errors, checked = run()
    for e in errors:
        print("  " + e)
    print(f"orthographic views: {len(errors)} problem(s) ({checked} script(s) make a camera orthographic)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
