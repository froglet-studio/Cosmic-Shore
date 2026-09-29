#!/usr/bin/env python3
"""Verify that every element of the shared in-game HUD resolves to a rect ON the
canvas, in every scene that instances it.

WHY THIS EXISTS.

A HUD element's position is a chain: the prefab's RectTransform, plus whatever
each scene overrides on it. Both halves are silent when wrong. A scene override
always beats the prefab (`Docs/GAMECANVAS.md`), so one dragged object in one
scene moves that mode's HUD and nothing anywhere reports it; and a dragged object
in the PREFAB moves it in all 25 scenes at once, which no per-scene diff shows
either.

That is not hypothetical. `MinigameJoust_Gameplay.unity` carried
`m_AnchoredPosition.x: -1416.3756` on the in-game toast feed against 2272-2304
everywhere else, which put a 633.6 x 420 panel at x [-1732.8, -1099.2] -- wholly
off a 1920 x 1080 canvas, so Joust's toasts were drawn every match and seen by
nobody. It was found by a human reading YAML, and the audit that found it could
only mark it "probably off-screen, needs a play-test" because the arithmetic had
never been done. The drift itself is gone (the GameCanvas unification dropped it,
2026-09-08), and `gamecanvas_unification_report.py --check` now refuses new
overrides on the 15 migrated scenes -- but nothing asserted the PREFAB's own
value is on-screen, which is the half that fails for every mode simultaneously.

WHAT IT CHECKS. For each element it resolves the full rect chain from the canvas
down, applies each scene's own overrides, and fails when an element's rect does
not intersect the canvas at all. Zero overlap is the assertion because it is
indisputable: an element can legitimately be clipped, hang off an edge, or slide
in from outside, but one that is entirely outside in the SHIPPED state is not a
layout choice. The inside-fraction is printed for every element regardless, so a
near-miss is visible without being fatal.

WHAT IT DOES NOT CHECK, stated rather than implied: it is geometry only. An
element can be perfectly placed and still invisible behind a zero alpha, a
disabled component, a sibling drawn over it, or a missing texture -- none of
which is a rect. `FrogletTools > Diagnostics > Report On-Screen UI` answers those
from a rendered frame, which is the one thing static analysis cannot see.

    python3 Tools/Build/check_hud_onscreen.py
    python3 Tools/Build/check_hud_onscreen.py --verbose
    python3 Tools/Build/check_hud_onscreen.py --self-test

Exit code 0 when clean, 1 when an element resolves off the canvas, 2 on a
usage/setup error.
"""

from __future__ import annotations

import argparse
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CORE = os.path.join("Assets", "_Prefabs", "CORE", "GameCanvas.prefab")
CORE_GUID = "65bf1ed35b752374ca46ae214710e41c"

# The HUD layer whose children are edge-pinned and therefore the ones that can be
# pushed off. Resolved by name so a rename fails loudly rather than silently
# checking nothing.
HUD_LAYER = "MiniGameHUD"

VEC2 = r"\{x: (-?[\d.eE+-]+), y: (-?[\d.eE+-]+)\}"
# The reference resolution the CanvasScaler lays the HUD out against. It is read
# from the scaler and NOT from the canvas root's own RectTransform, because a root
# Canvas's rect is DRIVEN: Unity recomputes it every frame from the scaler, so the
# serialized value is noise -- and so is a scene's override of it. Every scene here
# zeroes that rect (pivot, both anchors, sizeDelta and anchoredPosition all to 0),
# which is Unity's default-override set for a prefab-instance root; applying it
# would resolve the canvas as 0x0 and report every element off-screen.
SCALER_RES = r"m_ReferenceResolution: \{x: (-?[\d.eE+-]+), y: (-?[\d.eE+-]+)\}"
# A rect property a scene can override, mapped to (attribute, component index).
RECT_PROPS = {
    "m_AnchoredPosition.x": ("pos", 0), "m_AnchoredPosition.y": ("pos", 1),
    "m_SizeDelta.x": ("size", 0),       "m_SizeDelta.y": ("size", 1),
    "m_AnchorMin.x": ("amin", 0),       "m_AnchorMin.y": ("amin", 1),
    "m_AnchorMax.x": ("amax", 0),       "m_AnchorMax.y": ("amax", 1),
    "m_Pivot.x": ("pivot", 0),          "m_Pivot.y": ("pivot", 1),
}


def read(path: str) -> str:
    with open(os.path.join(ROOT, path), encoding="utf-8", errors="replace") as fh:
        return fh.read()


def unwrap(text: str) -> str:
    """Join flow maps Unity wrapped across two lines.

    `- target: {fileID: N, guid: g,\\n    type: 3}` is the shipped shape in scene
    YAML, and a one-line regex reads a 1,774-override instance as clean -- the
    trap `gamecanvas_unification_report.py` records paying for.
    """
    return re.sub(r",\s*\n\s+(type: \d\})", r", \1", text)


def parse_objects(text: str) -> "dict[str, dict]":
    """fileID -> {cls, body, stripped} for every object in a prefab/scene."""
    out = {}
    for chunk in re.split(r"\n--- ", text):
        m = re.match(r"!u!(\d+) &(-?\d+)(.*?)\n", chunk)
        if m:
            out[m.group(2)] = dict(cls=m.group(1), body=chunk,
                                   stripped="stripped" in m.group(3))
    return out


def vec(body: str, key: str, default):
    m = re.search(r"m_%s: %s" % (key, VEC2), body)
    return [float(m.group(1)), float(m.group(2))] if m else list(default)


class Node:
    __slots__ = ("fid", "name", "active", "pos", "size", "amin", "amax", "pivot",
                 "children", "resolved")

    def __init__(self, fid, name, active, pos, size, amin, amax, pivot):
        self.fid, self.name, self.active = fid, name, active
        self.pos, self.size, self.amin, self.amax, self.pivot = pos, size, amin, amax, pivot
        self.children, self.resolved = [], None


def rect_of(node: Node, parent_w: float, parent_h: float,
            parent_x: float, parent_y: float):
    """Unity's RectTransform resolution, one formula for both anchored and stretched.

    width = (amax-amin)*parentW + sizeDelta; the pivot sits at the anchor
    reference plus anchoredPosition; the left edge is that minus pivot*width.
    With amin == amax this reduces to `width = sizeDelta`, which is the anchored
    case -- writing the two branches separately is how they drift apart.
    """
    w = (node.amax[0] - node.amin[0]) * parent_w + node.size[0]
    h = (node.amax[1] - node.amin[1]) * parent_h + node.size[1]
    px = (node.amin[0] + (node.amax[0] - node.amin[0]) * node.pivot[0]) * parent_w + node.pos[0]
    py = (node.amin[1] + (node.amax[1] - node.amin[1]) * node.pivot[1]) * parent_h + node.pos[1]
    x = parent_x + px - node.pivot[0] * w
    y = parent_y + py - node.pivot[1] * h
    return x, y, w, h


def build_tree(prefab_rel: str) -> "tuple[Node, dict[str, Node]]":
    """The canvas root and a fileID -> Node index, including nested-prefab roots.

    A nested prefab instance contributes ONE node: its root RectTransform, whose
    values are the inner prefab's own, overridden by the outer instance's
    modification block. That is the object a scene later addresses through the
    STRIPPED transform, so the stripped id is what the index is keyed on.
    """
    text = unwrap(read(prefab_rel))
    objs = parse_objects(text)
    index: "dict[str, Node]" = {}

    def go_of(tid):
        m = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", objs[tid]["body"])
        return m.group(1) if m else None

    def plain_node(tid):
        b = objs[tid]["body"]
        go = go_of(tid)
        name, active = "?", True
        if go and go in objs:
            n = re.search(r"m_Name: (.*)", objs[go]["body"])
            a = re.search(r"m_IsActive: (\d)", objs[go]["body"])
            name = n.group(1).strip() if n else "?"
            active = (a.group(1) == "1") if a else True
        return Node(tid, name, active,
                    vec(b, "AnchoredPosition", (0, 0)), vec(b, "SizeDelta", (0, 0)),
                    vec(b, "AnchorMin", (0, 0)), vec(b, "AnchorMax", (0, 0)),
                    vec(b, "Pivot", (0.5, 0.5)))

    # Nested instances: stripped transform id -> the node its inner prefab + the
    # outer modification block resolve to.
    nested: "dict[str, Node]" = {}
    for chunk in re.split(r"\n--- ", text):
        m = re.match(r"!u!1001 &(-?\d+)\nPrefabInstance:", chunk)
        if not m:
            continue
        inst_id = m.group(1)
        src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: (\w+)", chunk)
        stripped = [k for k, v in objs.items()
                    if v["stripped"] and v["cls"] in ("4", "224")
                    and f"m_PrefabInstance: {{fileID: {inst_id}}}" in v["body"]]
        if not src or not stripped:
            continue
        inner_path = path_for_guid(src.group(1))
        node = Node(stripped[0], "[nested]", True, [0, 0], [0, 0], [0, 0], [0, 0], [0.5, 0.5])
        inner_root_id = None
        if inner_path:
            itext = unwrap(read(inner_path))
            iobjs = parse_objects(itext)
            for k, v in iobjs.items():
                if v["cls"] in ("4", "224") and re.search(r"m_Father: \{fileID: 0\}", v["body"]):
                    inner_root_id = k
                    b = v["body"]
                    node.pos = vec(b, "AnchoredPosition", (0, 0))
                    node.size = vec(b, "SizeDelta", (0, 0))
                    node.amin = vec(b, "AnchorMin", (0, 0))
                    node.amax = vec(b, "AnchorMax", (0, 0))
                    node.pivot = vec(b, "Pivot", (0.5, 0.5))
                    break
        for tgt, prop, val in re.findall(
                r"- target: \{fileID: (-?\d+), guid: \w+, type: 3\}\s*\n\s*propertyPath: (\S+)"
                r"\s*\n\s*value: (\S*)", chunk):
            if prop == "m_Name" and val:
                node.name = val
            if inner_root_id and tgt == inner_root_id and prop in RECT_PROPS:
                attr, i = RECT_PROPS[prop]
                getattr(node, attr)[i] = float(val)
        nested[stripped[0]] = node
        index[stripped[0]] = node

    roots = []
    for tid, o in objs.items():
        if o["cls"] not in ("4", "224") or o["stripped"]:
            continue
        node = plain_node(tid)
        index[tid] = node
        if re.search(r"m_Father: \{fileID: 0\}", o["body"]):
            roots.append(node)
    for tid, o in objs.items():
        if o["cls"] not in ("4", "224") or o["stripped"] or tid not in index:
            continue
        kids = re.search(r"m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", o["body"])
        if kids:
            for k in re.findall(r"fileID: (-?\d+)", kids.group(1)):
                if k in index:
                    index[tid].children.append(index[k])
    if len(roots) != 1:
        raise SystemExit(f"error: expected exactly one root transform in {prefab_rel}, "
                         f"found {len(roots)}")
    res = re.search(SCALER_RES, text)
    if not res:
        raise SystemExit(f"error: no CanvasScaler reference resolution in {prefab_rel}. "
                         f"Without it there is no canvas to measure against, and a "
                         f"guessed one would make this gate agree with nothing.")
    canvas = (0.0, 0.0, float(res.group(1)), float(res.group(2)))
    return roots[0], index, canvas


_GUID_INDEX = None


def path_for_guid(guid: str):
    global _GUID_INDEX
    if _GUID_INDEX is None:
        _GUID_INDEX = {}
        for d, _, fs in os.walk(os.path.join(ROOT, "Assets")):
            for f in fs:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(d, f)
                try:
                    with open(p, encoding="utf-8", errors="replace") as fh:
                        head = fh.read(400)
                except OSError:
                    continue
                m = re.search(r"^guid: (\w+)", head, re.M)
                if m:
                    _GUID_INDEX[m.group(1)] = os.path.relpath(p[:-5], ROOT)
    return _GUID_INDEX.get(guid)


def scene_overrides(scene_text: str) -> "dict[str, dict[str, float]]":
    """fileID -> {rect property: value} for the canvas instance in one scene."""
    out: "dict[str, dict[str, float]]" = {}
    for tgt, guid, prop, val in re.findall(
            r"- target: \{fileID: (-?\d+), guid: (\w+), type: 3\}\s*\n\s*propertyPath: (\S+)"
            r"\s*\n\s*value: (\S*)", unwrap(scene_text)):
        if prop in RECT_PROPS and val not in ("",):
            out.setdefault(tgt, {})[prop] = float(val)
    return out


def evaluate(root: Node, index: "dict[str, Node]", overrides, hud_layer: str, canvas_rect):
    """(canvas rect, [(name, rect, inside fraction)]) with overrides applied."""
    saved = {}
    for fid, props in overrides.items():
        node = index.get(fid)
        if not node or node is root:      # the root's rect is driven; see SCALER_RES
            continue
        for prop, val in props.items():
            attr, i = RECT_PROPS[prop]
            saved.setdefault((fid, attr, i), getattr(node, attr)[i])
            getattr(node, attr)[i] = val
    try:
        cx, cy, cw, ch = canvas_rect
        hud = next((n for n in walk(root) if n.name == hud_layer), None)
        if hud is None:
            raise SystemExit(f"error: no '{hud_layer}' under the canvas root - has it "
                             f"been renamed? This gate checks nothing without it.")
        # Place the HUD layer, then measure its direct children against the canvas.
        hx, hy, hw, hh = place(root, hud, cx, cy, cw, ch)
        rows = []
        for child in hud.children:
            if not child.active:
                continue
            r = rect_of(child, hw, hh, hx, hy)
            rows.append((child.name, r, overlap_fraction(r, (cx, cy, cw, ch))))
        return (cx, cy, cw, ch), rows
    finally:
        for (fid, attr, i), val in saved.items():
            getattr(index[fid], attr)[i] = val


def walk(node: Node):
    yield node
    for c in node.children:
        yield from walk(c)


def place(root: Node, target: Node, cx, cy, cw, ch):
    """Resolve `target`'s rect by descending from the canvas root."""
    def rec(node, pw, ph, px, py):
        if node is target:
            return rect_of(node, pw, ph, px, py)
        x, y, w, h = rect_of(node, pw, ph, px, py) if node is not root else (cx, cy, cw, ch)
        for c in node.children:
            got = rec(c, w, h, x, y)
            if got:
                return got
        return None
    return rec(root, 0, 0, 0, 0)


def overlap_fraction(r, canvas) -> float:
    """How much of `r` lies on the canvas, in [0, 1].

    A rect with zero width or height is measured as a POINT or a SEGMENT rather
    than as area, and that is not a special case to be tidied away: a serialized
    size of 0 is what a RUNTIME-SIZED element looks like on disk. Three ship in
    this canvas - `GoalStack` carries a VerticalLayoutGroup plus a
    ContentSizeFitter that computes its height from the rows it is given, and
    `CountdownTimer` and `Pip` are bare anchor points whose children hold the
    geometry. Dividing by a zero area would score all three at 0% and fail them,
    which is how a gate earns a reputation for crying wolf and gets switched off.
    """
    x, y, w, h = r
    cx, cy, cw, ch = canvas
    ox = max(0.0, min(x + w, cx + cw) - max(x, cx))
    oy = max(0.0, min(y + h, cy + ch) - max(y, cy))
    if w > 0 and h > 0:
        return ox * oy / (w * h)
    # Degenerate on one or both axes: the surviving extent must be on the canvas.
    on_x = (ox / w) if w > 0 else (cx <= x <= cx + cw)
    on_y = (oy / h) if h > 0 else (cy <= y <= cy + ch)
    return float(on_x) * float(on_y)


def run(verbose: bool = False) -> int:
    root, index, canvas_rect = build_tree(CORE)
    scenes = [s for s in sorted(glob.glob(os.path.join(ROOT, "Assets", "_Scenes", "**", "*.unity"),
                                          recursive=True))
              if CORE_GUID in open(s, encoding="utf-8", errors="replace").read()]
    if not scenes:
        print(f"error: no scene instances {CORE} - this gate cannot be clean and "
              f"empty at the same time.", file=sys.stderr)
        return 2

    failures = []
    print(f"{len(scenes)} scene(s) instance {CORE}\n")
    for scene in scenes:
        text = open(scene, encoding="utf-8", errors="replace").read()
        canvas, rows = evaluate(root, index, scene_overrides(text), HUD_LAYER,
                                canvas_rect)
        bad = [r for r in rows if r[2] <= 0.0]
        name = os.path.relpath(scene, ROOT)
        if verbose or bad:
            print(f"  {'FAIL' if bad else 'OK  '} {os.path.basename(scene)}"
                  f"   canvas {canvas[2]:.0f}x{canvas[3]:.0f}")
            for nm, (x, y, w, h), frac in rows:
                mark = "  <-- OFF THE CANVAS" if frac <= 0.0 else (
                    "  (runtime-sized)" if w == 0 or h == 0 else "")
                print(f"        {nm:32} x[{x:8.1f},{x+w:8.1f}] y[{y:8.1f},{y+h:8.1f}]"
                      f"  {frac*100:5.1f}% on{mark}")
        for nm, r, frac in bad:
            failures.append(f"{name}: '{nm}' resolves to x[{r[0]:.1f},{r[0]+r[2]:.1f}] "
                            f"y[{r[1]:.1f},{r[1]+r[3]:.1f}], entirely outside the "
                            f"{canvas[2]:.0f}x{canvas[3]:.0f} canvas")

    if failures:
        print(f"\n{len(failures)} HUD element(s) off the canvas:", file=sys.stderr)
        for f in failures:
            print(f"  {f}", file=sys.stderr)
        print("\nFix the PREFAB when every scene fails together; delete the scene's "
              "override when one scene differs - never re-author the same number into "
              "the scene, which is how the drift got there (Docs/GAMECANVAS.md).",
              file=sys.stderr)
        return 1

    total = sum(1 for _ in walk(root))
    print(f"OK: every active {HUD_LAYER} element resolves onto the canvas in all "
          f"{len(scenes)} scene(s).")
    return 0


def run_self_test() -> int:
    """Fixtures for the rect maths and for the gate's own verdict.

    The maths cases are hand-computed; the verdict case replays the SHIPPED Joust
    drift (-1416.3756) against the real prefab and requires the gate to reject it,
    so what is proven is that this tool would have caught the bug it was written
    for -- not merely that it runs.
    """
    fails = 0

    def check(label, got, want, tol=1e-6):
        nonlocal fails
        ok = all(abs(a - b) <= tol for a, b in zip(got, want))
        print(f"  {'PASS' if ok else 'FAIL'}  {label}")
        if not ok:
            fails += 1
            print(f"        got {tuple(round(v, 4) for v in got)}, want {want}")

    n = Node("x", "n", True, [316.8, 210], [633.6, 420], [0, 0], [0, 0], [0.5, 0.5])
    check("anchored bottom-left, pivot centre -> flush in the corner",
          rect_of(n, 1920, 1080, 0, 0), (0, 0, 633.6, 420))
    n = Node("x", "n", True, [0, 0], [0, 0], [0, 0], [1, 1], [0.5, 0.5])
    check("full stretch -> exactly the parent",
          rect_of(n, 1920, 1080, 0, 0), (0, 0, 1920, 1080))
    n = Node("x", "n", True, [0, 0], [-40, -40], [0, 0], [1, 1], [0.5, 0.5])
    check("stretch with negative sizeDelta -> inset on all sides",
          rect_of(n, 1920, 1080, 0, 0), (20, 20, 1880, 1040))
    n = Node("x", "n", True, [-30, 30], [120, 45], [1, 0], [1, 0], [1, 0])
    check("anchored bottom-right, pivot bottom-right",
          rect_of(n, 1920, 1080, 0, 0), (1770, 30, 120, 45))
    n = Node("x", "n", True, [960, 540], [1920, 1080], [0, 0], [0, 0], [0.5, 0.5])
    check("the canvas root's own convention", rect_of(n, 0, 0, 0, 0), (0, 0, 1920, 1080))

    canvas = (0, 0, 1920, 1080)
    for label, r, want in [
            ("fully inside is 100%", (0, 0, 633.6, 420), 1.0),
            ("the shipped Joust drift is 0%", (-1732.8, -673, 633.6, 420), 0.0),
            ("straddling the left edge is a fraction", (-316.8, 0, 633.6, 420), 0.5),
            ("a runtime-sized element is scored on POSITION", (16, 1028, 400, 0), 1.0),
            ("a bare anchor point on the canvas passes", (960, 840, 0, 0), 1.0),
            ("a bare anchor point off the canvas fails", (-500, 840, 0, 0), 0.0),
            ("a runtime-sized element off the canvas fails", (16, 1600, 400, 0), 0.0)]:
        got = overlap_fraction(r, canvas)
        ok = abs(got - want) < 1e-6
        print(f"  {'PASS' if ok else 'FAIL'}  {label}")
        if not ok:
            fails += 1
            print(f"        got {got}, want {want}")

    # The verdict fixture: re-inject the real drift and require a rejection.
    try:
        root, index, canvas_rect = build_tree(CORE)
        feed = next((n for n in index.values() if n.name == "NotificationUI"), None)
        if feed is None:
            print("  FAIL  the shipped drift is rejected (no NotificationUI found)")
            fails += 1
        else:
            _, clean = evaluate(root, index, {}, HUD_LAYER, canvas_rect)
            _, drift = evaluate(root, index,
                                {feed.fid: {"m_AnchoredPosition.x": -1416.3756,
                                            "m_AnchoredPosition.y": -463.0}}, HUD_LAYER,
                                canvas_rect)
            clean_ok = all(f > 0 for _, _, f in clean)
            drift_bad = any(f <= 0 for nm, _, f in drift if nm == "NotificationUI")
            for label, ok in (("the shipped prefab passes", clean_ok),
                              ("the shipped Joust drift is rejected", drift_bad)):
                print(f"  {'PASS' if ok else 'FAIL'}  {label}")
                if not ok:
                    fails += 1
    except SystemExit as exc:
        print(f"  FAIL  verdict fixture could not run: {exc}")
        fails += 1

    print(f"\nself-test: {'all passed' if not fails else str(fails) + ' FAILED'}")
    return 1 if fails else 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--verbose", action="store_true",
                    help="print every element's resolved rect, not just the failures")
    ap.add_argument("--check", action="store_true",
                    help="accepted for symmetry with the other Tools/Build gates; "
                         "this tool only ever checks")
    ap.add_argument("--self-test", action="store_true",
                    help="verify the rect maths and the verdict, then exit")
    args = ap.parse_args()
    return run_self_test() if args.self_test else run(args.verbose)


if __name__ == "__main__":
    sys.exit(main())
