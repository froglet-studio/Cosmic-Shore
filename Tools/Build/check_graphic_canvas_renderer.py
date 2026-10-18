#!/usr/bin/env python3
"""Every first-party UI Graphic must declare [RequireComponent(typeof(CanvasRenderer))].

    python3 Tools/Build/check_graphic_canvas_renderer.py             # fail on a Graphic without it
    python3 Tools/Build/check_graphic_canvas_renderer.py --self-test # prove it fires

`UnityEngine.UI.Graphic` does not itself require a CanvasRenderer: `Image`, `RawImage` and TMP each
declare it on their own class. A custom `Graphic` / `MaskableGraphic` subclass that does not, and is
built in code with `AddComponent<T>()` on a GameObject that has no CanvasRenderer, has none. In the
Unity Editor every access then throws `MissingComponentException` ("There is no 'CanvasRenderer'
attached to ..."). The graphic draws nothing, and when it is a raycast target the throw inside
`GraphicRaycaster` takes every UI press in the scene with it. Prisma adds the renderer on demand, so
the port draws it fine and only the Editor fails.

That is how the first-login spotlight (`SpotlightDimGraphic`) shipped: two Unity playtests showed no
dim and a dead menu while Prisma drew it correctly (`Docs/HomeHub/ARCHITECTURE.md` §8.1). The
Serpent scope's `ScopeRingGraphic` / `ScopeCrosshairGraphic` are built the same way.

A graphic authored onto a prefab gets its CanvasRenderer from the inspector and works without the
attribute, but the attribute costs nothing there, and the rule has no exceptions so a later
AddComponent caller cannot reintroduce the bug.

Scans `Assets/_Scripts` and `Assets/FTUE` for classes deriving directly from `Graphic` or
`MaskableGraphic`, and looks for the attribute in the attribute block above the declaration.
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCAN = ("Assets/_Scripts", "Assets/FTUE")
DECL = re.compile(r"^[ \t]*(?:public|internal)?[ \t]*(?:sealed[ \t]+|abstract[ \t]+|partial[ \t]+)*class[ \t]+(\w+)"
                  r"[ \t]*:[ \t]*(?:UnityEngine\.UI\.)?(?:Maskable)?Graphic\b", re.M)
REQUIRE = re.compile(r"\[\s*RequireComponent\s*\(\s*typeof\s*\(\s*(?:UnityEngine\.)?CanvasRenderer\s*\)")


def attribute_block(text, start):
    """The lines of attributes immediately above the declaration at `start`."""
    lines = text[:start].split("\n")[:-1]
    block = []
    for line in reversed(lines):
        s = line.strip()
        if s.startswith("[") or (block and s.startswith("//") is False and s.endswith("]") and "[" in s):
            block.append(s)
        elif s.startswith("//") or s == "":
            # Doc comments end the attribute block; plain comments between attributes do not.
            if s.startswith("///"):
                break
            continue
        else:
            break
    return "\n".join(block)


def audit(files):
    findings = []
    count = 0
    for name, text in files.items():
        for m in DECL.finditer(text):
            count += 1
            if not REQUIRE.search(attribute_block(text, m.start())):
                line = text[:m.start()].count("\n") + 1
                findings.append(f"{name}:{line}: {m.group(1)} derives from Graphic without "
                                "[RequireComponent(typeof(CanvasRenderer))]")
    return findings, count


def self_test():
    good = ("    /// <summary>x</summary>\n    [RequireComponent(typeof(CanvasRenderer))]\n"
            "    [AddComponentMenu(\"UI/X\", 1)]\n    public class Good : MaskableGraphic\n    {\n    }\n")
    bare = "    /// <summary>x</summary>\n    [AddComponentMenu(\"UI/Y\")]\n    public sealed class Bare : MaskableGraphic, ICanvasRaycastFilter\n    { }\n"
    plain = "    public class Plain : Graphic { }\n"
    # The attribute on an EARLIER class must not cover a later one.
    stray = good + "\n    /// <summary>y</summary>\n    public class Later : MaskableGraphic { }\n"
    not_graphic = "    public class Thing : MonoBehaviour { }\n    public class GraphicHolder : Object { }\n"
    assert audit({"good": good}) == ([], 1), audit({"good": good})
    assert len(audit({"bare": bare})[0]) == 1
    assert len(audit({"plain": plain})[0]) == 1
    assert len(audit({"stray": stray})[0]) == 1
    assert audit({"not": not_graphic}) == ([], 0)
    print("self-test OK (1 clean, 3 missing caught: bare, unattributed, attribute on an earlier class; 0 false hits)")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    files = {}
    for base in SCAN:
        for path in sorted(glob.glob(os.path.join(ROOT, base, "**", "*.cs"), recursive=True)):
            with open(path, encoding="utf-8-sig", errors="ignore") as fh:
                files[os.path.relpath(path, ROOT)] = fh.read()
    findings, count = audit(files)
    if findings:
        print("FAIL: UI Graphics that can be built without a CanvasRenderer:")
        for f in findings:
            print("  - " + f)
        return 1
    print(f"OK: {count} first-party Graphic subclasses, every one requires its CanvasRenderer.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
