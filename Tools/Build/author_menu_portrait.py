#!/usr/bin/env python3
"""
Portrait menus for Menu_Main (Docs/MENU_REDESIGN_B.md section 138, the picked Rev 63 B; Docs/UI_STYLE_GUIDE.md section 8,
D6). Every device shows the PC UI; when a phone is held upright in the menus the canvas switches to a 1080 x 1920 reference
matched to the width (AdaptiveCanvasScaler.hasPortraitLayout) and every PortraitRect under it takes its upright pose.

This script owns Menu_Main's side of that:
    the canvas         AdaptiveCanvasScaler: hasPortraitLayout 1, portraitReferenceResolution 1080 x 1920
    the rotation       MenuOrientation on the MainMenuController's GameObject: portrait only while the menu is Ready
    the fallback       every screen and window with no upright pose (all but Home, Arcade, Configure and Hangar, whose
                       generators write their poses) is its PC layout fitted to the width (PortraitRect drawAtPcScale):
                       the scene's own objects here, SettingsModal's prefab root here, and Codex, Port, Replays, Credits
                       and the hub window strip in their own generators (they re-emit their roots).

Each component is removed and re-added every run, so a re-run is a no-op (--check). The owning generators' --check stays
clean: they patch, they do not re-emit these objects.

    python3 Tools/Build/author_menu_portrait.py            # write
    python3 Tools/Build/author_menu_portrait.py --check    # fail if Menu_Main drifted
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_ingame_hud", os.path.join(HERE, "author_ingame_hud.py"))
HUD = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(HUD)
K, Patch = HUD.K, HUD.Patch
REPO = K.REPO

MENU = "Assets/_Scenes/Menu_Main.unity"
PORTRAIT_GUID = K.meta_guid("Assets/_Scripts/UI/PortraitRect.cs")
SCALER_GUID = K.meta_guid("Assets/_Scripts/UI/AdaptiveCanvasScaler.cs")
ORIENTATION_GUID = K.meta_guid("Assets/_Scripts/UI/MenuOrientation.cs")
MENU_CONTROLLER_GUID = K.meta_guid("Assets/_Scripts/System/MainMenuController.cs")
PORTRAIT_REFERENCE = (1080, 1920)

# Screens and windows with no upright pose yet: their PC layout fitted to the width. Home, Arcade, Configure (with the
# Arena and Maelstrom configure windows, the same views) and Hangar are posed by their own generators (Rev 63 B).
SCENE_FALLBACK = ["ArkScreen", "PortScreen", "ProfileScreen", "MissionScreenModal", "ToyboxScreenModal",
                  "ArenaScreenModal", "ToyboxGameConfigureModal", "ProfileModal", "LeaderboardConfigureModal",
                  "PlayerDataSelectModal"]
# Screens whose own generators write their fallback on their root (they re-emit it): Codex, Port, Replays, Credits and
# the hub window strip. SettingsModal is not re-emitted by its generator, so this script patches its prefab's root.
PREFAB_FALLBACK = ["Assets/_Prefabs/UI Elements/Panels/SettingsModal.prefab"]


def unique_go(p, name):
    gos = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!1 ") and f"  m_Name: {name}\n" in d]
    assert len(gos) == 1, f"Menu_Main: expected one {name!r}, found {len(gos)}"
    return gos[0]


def mark(p, go):
    p.remove_comp(go, str(p.ctx.fid(f"{go}#portrait")))
    p.add_comp(go, "portrait", 114, K.portrait_rect(pc_block=True))


def prefab_text(path):
    """The fallback on a prefab's root (re-added every run)."""
    p = Patch(open(os.path.join(REPO, path), encoding="utf-8").read(), os.path.basename(path) + "/Portrait")
    roots = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!224 ") and "  m_Father: {fileID: 0}\n" in d]
    assert len(roots) == 1, f"{path}: one root rect expected"
    mark(p, re.search(r"m_GameObject: \{fileID: (-?\d+)\}", p.doc(roots[0])).group(1))
    return p.text()


def switch_on(p):
    found = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!114 ") and f"guid: {SCALER_GUID}, type: 3}}" in d]
    assert len(found) == 1, f"Menu_Main: expected one AdaptiveCanvasScaler, found {len(found)}"
    d = re.sub(r"  hasPortraitLayout: \d\n  portraitReferenceResolution: \{x: [-\d.]+, y: [-\d.]+\}\n", "", p.doc(found[0]))
    assert "  safeZoneMaxAspect: " in d, "Menu_Main: the AdaptiveCanvasScaler body changed shape"
    fields = "  hasPortraitLayout: 1\n  portraitReferenceResolution: {x: %d, y: %d}\n" % PORTRAIT_REFERENCE
    d = re.sub(r"(  safeZoneMaxAspect: [^\n]*\n)", lambda m: m.group(1) + fields, d, count=1)
    p.replace(found[0], d)


def orientation(p):
    menus = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!114 ") and f"guid: {MENU_CONTROLLER_GUID}, type: 3}}" in d]
    assert len(menus) == 1, f"Menu_Main: expected one MainMenuController, found {len(menus)}"
    go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", p.doc(menus[0])).group(1)
    p.remove_comp(go, str(p.ctx.fid(f"{go}#orientation")))
    p.add_comp(go, "orientation", 114, lambda ctx, g: K.mono(g, ORIENTATION_GUID, f"  menu: {{fileID: {menus[0]}}}\n"))


def scene_text(text_):
    p = Patch(text_, "Menu_Main/Portrait")
    for name in SCENE_FALLBACK:
        mark(p, unique_go(p, name))
    switch_on(p)
    orientation(p)
    return p.text()


def validate(text_):
    assert text_.count(f"guid: {PORTRAIT_GUID}, type: 3}}") >= len(SCENE_FALLBACK), \
        "Menu_Main: a fallback screen lost its PortraitRect"
    assert "  hasPortraitLayout: 1\n" in text_, "Menu_Main: the canvas has no portrait layout"
    assert f"guid: {ORIENTATION_GUID}, type: 3}}" in text_, "Menu_Main: no MenuOrientation (portrait would never unlock)"
    for m in re.finditer(r"^--- !u!114 &(-?\d+)\n(.*?)(?=^--- |\Z)", text_, re.S | re.M):
        if f"guid: {PORTRAIT_GUID}" in m.group(2) or f"guid: {ORIENTATION_GUID}" in m.group(2):
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", m.group(2)).group(1)
            stripped = re.search(r"^--- !u!1 &%s stripped\n" % go, text_, re.M)
            listed = re.search(r"^--- !u!1 &%s\n(?:.*\n)*?  - component: \{fileID: %s\}" % (go, m.group(1)), text_, re.M)
            added = f"addedObject: {{fileID: {m.group(1)}}}" in text_
            assert (stripped and added) or listed, f"Menu_Main: component {m.group(1)} is not attached to its GameObject"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    path = os.path.join(REPO, MENU)
    before = open(path, encoding="utf-8").read()
    out = scene_text(before)
    validate(out)
    outs = {MENU: (before, out)}
    for pf in PREFAB_FALLBACK:
        b = open(os.path.join(REPO, pf), encoding="utf-8").read()
        outs[pf] = (b, prefab_text(pf))
    drifted = [k for k, (b, o) in outs.items() if b != o]
    if args.check:
        for k in drifted:
            print(f"DRIFT: {k}")
        print("OK: no drift" if not drifted else f"FAIL: {len(drifted)} file(s) drifted")
        return 1 if drifted else 0
    for k in drifted:
        with open(os.path.join(REPO, k), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(outs[k][1])
        print(f"wrote {k}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
