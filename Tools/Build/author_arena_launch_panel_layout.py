#!/usr/bin/env python3
"""
Author the Arena launch window's VESSEL PICKER layout in Menu_Main: the SELECT VESSEL button
belongs to the carousel, under the hull it confirms - never on top of the Play button.

    python3 Tools/Build/author_arena_launch_panel_layout.py            # write the scene
    python3 Tools/Build/author_arena_launch_panel_layout.py --check    # fail if the scene drifted

WHAT IT FIXES (measured on the authored scene, 2026-09-15):

  `SelectVesselButton` was authored as a CLONE of `Play Button` - same parent
  (`ConfigurationDetailView`), same anchors (0.690..0.998 x 0.056..0.156), same pivot, same
  (0, -22) offset - with the CONFIRM plate sprite in place of START GAME. So it sat EXACTLY on the
  Play button, and because `ArenaLaunchPanel.ShowVessel` hides it the moment the pilot confirms,
  what the pilot saw was "a strange button over the play button that goes away when clicked".
  Nothing about it was wrong in code; the rect was a copy that never got moved.

  It now lives INSIDE the carousel (`ControlsDescription/Content`, beside `VesselIcon`,
  `PrevButton` and `NextButton`), centred under the vessel icon at the CONFIRM plate's native
  272x72 - the same fixed-pixel idiom the two arrows already use (30x30 at +-271 from the centre),
  and the resolution rule Docs/HomeHub/ARCHITECTURE.md section 5.3 records (a 272x72 plate
  stretched to 326x92 is upscaled on every display). The picker is therefore self-contained:
  icon, arrows, confirm - and Start, which stays dead until confirm has been pressed, stands
  alone in its corner.

  `--check` also PROVES the two rects are disjoint by solving both against the canvas's own
  reference resolution, so a future clone-and-forget cannot pass.
"""
import argparse
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_toybox_layout import Scene, fmt, sub1, set_rect  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCENE = os.path.join(ROOT, "Assets", "_Scenes", "Menu_Main.unity")

MODAL = "ArenaGameConfigureModal"
CONFIRM_SPRITE_SIZE = (272.0, 72.0)     # Button (3).png - the CONFIRM plate, drawn at native size
GAP_UNDER_ICON = 8.0                    # px between the icon's bottom edge and the plate's top


# ---- rect solving ------------------------------------------------------------------------------

def vec2(body, key):
    m = re.search(r"^  %s: \{x: ([-\d.e]+), y: ([-\d.e]+)\}" % re.escape(key), body, re.M)
    if not m:
        raise SystemExit(f"no {key} in rect")
    return float(m.group(1)), float(m.group(2))


def reference_resolution(sc):
    for fid, (h, b) in sc.docs.items():
        m = re.search(r"^  m_ReferenceResolution: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", b, re.M)
        if m:
            return float(m.group(1)), float(m.group(2))
    raise SystemExit("no CanvasScaler with a reference resolution in the scene")


def solve_rect(sc, go, root_size):
    """(left, bottom, width, height) of a GameObject's rect in canvas pixels, walking its
    RectTransform chain from the canvas root. A stretch parent with no size of its own (the
    canvas root) takes the reference resolution."""
    chain = []
    tf = sc.go_tf[go]
    while tf:
        chain.append(tf)
        tf = sc.tf_parent.get(tf)
    chain.reverse()
    left, bottom, w, h = 0.0, 0.0, root_size[0], root_size[1]
    for i, t in enumerate(chain):
        b = sc.body(t)
        amin, amax = vec2(b, "m_AnchorMin"), vec2(b, "m_AnchorMax")
        ap, sd, pv = vec2(b, "m_AnchoredPosition"), vec2(b, "m_SizeDelta"), vec2(b, "m_Pivot")
        if i == 0:
            continue  # the canvas root: its size is the canvas's, not its own anchors'
        nw = (amax[0] - amin[0]) * w + sd[0]
        nh = (amax[1] - amin[1]) * h + sd[1]
        ref_x = amin[0] * w + pv[0] * (amax[0] - amin[0]) * w
        ref_y = amin[1] * h + pv[1] * (amax[1] - amin[1]) * h
        px, py = ref_x + ap[0], ref_y + ap[1]
        left, bottom = left + px - pv[0] * nw, bottom + py - pv[1] * nh
        w, h = nw, nh
    return left, bottom, w, h


def overlaps(a, b):
    return not (a[0] + a[2] <= b[0] or b[0] + b[2] <= a[0] or a[1] + a[3] <= b[1] or b[1] + b[3] <= a[1])


# ---- the layout --------------------------------------------------------------------------------

def find_under(sc, root_go, name):
    hits = [g for g in sc.subtree(root_go) if sc.names.get(g) == name]
    if len(hits) != 1:
        raise SystemExit(f"{name}: expected exactly one under {sc.names.get(root_go)}, found {len(hits)}")
    return hits[0]


def reparent(sc, go, new_parent_go, why):
    tf, new_tf = sc.go_tf[go], sc.go_tf[new_parent_go]
    old_tf = sc.tf_parent.get(tf)
    if old_tf == new_tf:
        return
    # the child's own m_Father
    b = sub1(sc.body(tf), r"^  m_Father: \{fileID: -?\d+\}$", f"  m_Father: {{fileID: {new_tf}}}")
    sc.set_body(tf, b, f"{why}: parent -> {sc.names.get(new_parent_go)}")
    # out of the old parent's child list
    if old_tf:
        ob = sc.body(old_tf)
        ob2 = re.sub(r"^  - \{fileID: %s\}\n" % tf, "", ob, count=1, flags=re.M)
        sc.set_body(old_tf, ob2, f"{why}: leave {sc.names.get(sc.tf_go.get(old_tf))}'s children")
    # onto the END of the new parent's child list (drawn last = on top of its siblings)
    nb = sc.body(new_tf)
    if not re.search(r"^  - \{fileID: %s\}$" % tf, nb, re.M):
        if re.search(r"^  m_Children: \[\]$", nb, re.M):
            nb2 = sub1(nb, r"^  m_Children: \[\]$", f"  m_Children:\n  - {{fileID: {tf}}}")
        else:
            nb2 = re.sub(r"(^  m_Children:\n(?:  - \{fileID: -?\d+\}\n)*)",
                         lambda m: m.group(1) + f"  - {{fileID: {tf}}}\n", nb, count=1, flags=re.M)
        sc.set_body(new_tf, nb2, f"{why}: join {sc.names.get(new_parent_go)}'s children")
    sc.rebuild_maps()


def author(sc):
    ref = reference_resolution(sc)
    modal = sc.find_root(MODAL)
    if not modal:
        raise SystemExit(f"{MODAL} not found in Menu_Main")
    select = find_under(sc, modal, "SelectVesselButton")
    play = find_under(sc, modal, "Play Button")
    icon = find_under(sc, modal, "VesselIcon")
    content = sc.tf_go[sc.tf_parent[sc.go_tf[icon]]]        # the carousel: VesselIcon's parent

    reparent(sc, select, content, "SelectVesselButton")

    # Centre-anchored, native size, hung a small gap under the icon's bottom edge. The icon is
    # anchor-stretched inside the carousel, so its bottom is read off the solved rects rather
    # than assumed.
    _, c_bottom, _, c_h = solve_rect(sc, content, ref)
    _, i_bottom, _, _ = solve_rect(sc, icon, ref)
    icon_bottom_from_centre = i_bottom - (c_bottom + c_h / 2.0)
    top = icon_bottom_from_centre - GAP_UNDER_ICON
    set_rect(sc, select, (0.5, 0.5), (0.5, 0.5), pos=(0.0, round(top, 2)), size=CONFIRM_SPRITE_SIZE,
             pivot=(0.5, 1.0), why="SelectVesselButton")
    sc.rebuild_maps()

    # Prove the picker's button and Start are disjoint on the reference canvas.
    r_select, r_play = solve_rect(sc, select, ref), solve_rect(sc, play, ref)
    if overlaps(r_select, r_play):
        raise SystemExit(f"SelectVesselButton {r_select} overlaps Play Button {r_play}")
    r_content = solve_rect(sc, content, ref)
    if (r_select[0] < r_content[0] or r_select[0] + r_select[2] > r_content[0] + r_content[2]
            or r_select[1] < r_content[1] or r_select[1] + r_select[3] > r_content[1] + r_content[3]):
        raise SystemExit(f"SelectVesselButton {r_select} leaves the carousel {r_content}")
    return r_select, r_play


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    sc = Scene(SCENE)
    r_select, r_play = author(sc)

    if not sc.changes:
        print(f"OK  arena picker layout matches (select {tuple(round(v, 1) for v in r_select)}, "
              f"play {tuple(round(v, 1) for v in r_play)}, disjoint)")
        return 0
    if args.check:
        print(f"FAIL: Menu_Main is {len(sc.changes)} edit(s) away from the authored arena picker layout:",
              file=sys.stderr)
        for c in sc.changes[:40]:
            print(f"  - {c}", file=sys.stderr)
        return 1
    sc.write()
    print(f"wrote {os.path.relpath(SCENE, ROOT)}: {len(sc.changes)} edit(s)")
    for c in sc.changes:
        print(f"  - {c}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
