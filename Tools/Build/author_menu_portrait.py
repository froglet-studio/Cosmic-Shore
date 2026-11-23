#!/usr/bin/env python3
"""
Portrait menus for Menu_Main (Docs/MENU_REDESIGN_B.md section 138, the picked Rev 63 B; Docs/UI_STYLE_GUIDE.md section 8,
D6). Every device shows the PC UI; when a phone is held upright in the menus the canvas switches to a 1080 x 1920 reference
matched to the width (AdaptiveCanvasScaler.hasPortraitLayout) and every PortraitRect under it takes its upright pose.

This script owns Menu_Main's side of that:
    the canvas         AdaptiveCanvasScaler: hasPortraitLayout 1, portraitReferenceResolution 1080 x 1920
    the rotation       MenuOrientation on the MainMenuController's GameObject: portrait only while the menu is Ready
    the turn           OrientationGlide + its own CanvasGroup on the canvas root: turn, then glide - every posed piece
                       starts where the turn left it and glides to its new place (Shombith, 2026-11-08: "Turn then glide",
                       Docs/MENU_REDESIGN_B.md section 154); the same on GameCanvas, gliding its own children, which also lays out upright, for a crew seat played
                       upright (the Goby's Pistol: ScreenOrientationPolicy, CrewOrientation)
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
GAME_CANVAS = "Assets/_Prefabs/CORE/GameCanvas.prefab"
PORTRAIT_GUID = K.meta_guid("Assets/_Scripts/UI/PortraitRect.cs")
SCALER_GUID = K.meta_guid("Assets/_Scripts/UI/AdaptiveCanvasScaler.cs")
ORIENTATION_GUID = K.meta_guid("Assets/_Scripts/UI/MenuOrientation.cs")
MENU_CONTROLLER_GUID = K.meta_guid("Assets/_Scripts/System/MainMenuController.cs")
GLIDE_GUID = K.meta_guid("Assets/_Scripts/UI/OrientationGlide.cs")
GLIDE_HOLD_FRAMES = 2
GLIDE_SECONDS = 0.35
PORTRAIT_REFERENCE = (1080, 1920)

# Screens and windows with no upright pose yet: their PC layout fitted to the width. Home, Arcade, Configure (with the
# Arena and Maelstrom configure windows, the same views) and Hangar are posed by their own generators (Rev 63 B).
SCENE_FALLBACK = ["ArkScreen", "PortScreen", "ProfileScreen", "MissionScreenModal", "ToyboxScreenModal",
                  "ArenaScreenModal", "ToyboxGameConfigureModal", "ProfileModal", "LeaderboardConfigureModal",
                  "PlayerDataSelectModal"]
# Scene pieces posed here (their generators patch, they do not re-emit them). The nav band: across the top upright as on
# PC (author_menu_topnav.py measured the row to fit 1080), so its pose is its own rect; posed, it glides on a turn
# with the rest (OrientationGlide, Docs/MENU_REDESIGN_B.md section 154; Shombith 2026-11-08: "give the nav band a pose too").
_tspec = importlib.util.spec_from_file_location("author_menu_topnav", os.path.join(HERE, "author_menu_topnav.py"))
TOPNAV = importlib.util.module_from_spec(_tspec)
_tspec.loader.exec_module(TOPNAV)
SCENE_POSED = {"NavBar": K.rect((0, 1), (1, 1), (0, -TOPNAV.BAND_H / 2), (0, TOPNAV.BAND_H))}

# Screens whose own generators write their fallback on their root (they re-emit it): Codex, Port, Replays, Credits and
# the hub window strip. SettingsModal is not re-emitted by its generator, so this script patches its prefab's root.
PREFAB_FALLBACK = ["Assets/_Prefabs/UI Elements/Panels/SettingsModal.prefab"]


def unique_go(p, name):
    gos = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!1 ") and f"  m_Name: {name}\n" in d]
    assert len(gos) == 1, f"Menu_Main: expected one {name!r}, found {len(gos)}"
    return gos[0]


def mark(p, go, pose=None):
    p.remove_comp(go, str(p.ctx.fid(f"{go}#portrait")))
    p.add_comp(go, "portrait", 114, K.portrait_rect(pose=pose) if pose else K.portrait_rect(pc_block=True))


def prefab_text(path):
    """The fallback on a prefab's root (re-added every run)."""
    p = Patch(open(os.path.join(REPO, path), encoding="utf-8").read(), os.path.basename(path) + "/Portrait")
    roots = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!224 ") and "  m_Father: {fileID: 0}\n" in d]
    assert len(roots) == 1, f"{path}: one root rect expected"
    mark(p, re.search(r"m_GameObject: \{fileID: (-?\d+)\}", p.doc(roots[0])).group(1))
    return p.text()


def scaler_of(p, label):
    found = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!114 ") and f"guid: {SCALER_GUID}, type: 3}}" in d]
    assert len(found) == 1, f"{label}: expected one AdaptiveCanvasScaler, found {len(found)}"
    return found[0]


def switch_on(p, label="Menu_Main"):
    found = scaler_of(p, label)
    d = re.sub(r"  hasPortraitLayout: \d\n  portraitReferenceResolution: \{x: [-\d.]+, y: [-\d.]+\}\n", "", p.doc(found))
    assert "  safeZoneMaxAspect: " in d, f"{label}: the AdaptiveCanvasScaler body changed shape"
    fields = "  hasPortraitLayout: 1\n  portraitReferenceResolution: {x: %d, y: %d}\n" % PORTRAIT_REFERENCE
    d = re.sub(r"(  safeZoneMaxAspect: [^\n]*\n)", lambda m: m.group(1) + fields, d, count=1)
    p.replace(found, d)


def canvas_group(ctx, go):
    return ("CanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Alpha: 1\n"
            "  m_Interactable: 1\n  m_BlocksRaycasts: 1\n  m_IgnoreParentGroups: 0\n")


def glide(p, label, direct_children=False):
    """The turn's glide on the canvas root, with a CanvasGroup only it drives (both re-added every run; the fade it
    replaced is removed)."""
    go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", p.doc(scaler_of(p, label))).group(1)
    others = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!225 ") and f"  m_GameObject: {{fileID: {go}}}\n" in d
              and f != str(p.ctx.fid(f"{go}#orientationGroup"))]
    assert not others, f"{label}: the canvas root already has a CanvasGroup; the fade needs one nothing else drives"
    for key in ("orientationFade", "orientationGlide", "orientationGroup"):
        p.remove_comp(go, str(p.ctx.fid(f"{go}#{key}")))
    group = p.add_comp(go, "orientationGroup", 225, canvas_group)
    p.add_comp(go, "orientationGlide", 114, lambda ctx, g: K.mono(
        g, GLIDE_GUID, f"  group: {{fileID: {group}}}\n  holdFrames: {GLIDE_HOLD_FRAMES}\n  glideSeconds: {GLIDE_SECONDS}\n"
                       f"  ease: 1\n  glideDirectChildren: {1 if direct_children else 0}\n"))


def game_canvas_text(text_):
    p = Patch(text_, "GameCanvas/Portrait")
    switch_on(p, "GameCanvas")
    glide(p, "GameCanvas", direct_children=True)
    return p.text()


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
    for name, pose in SCENE_POSED.items():
        mark(p, unique_go(p, name), pose)
    switch_on(p)
    glide(p, "Menu_Main")
    orientation(p)
    return p.text()


def validate(text_):
    assert text_.count(f"guid: {PORTRAIT_GUID}, type: 3}}") >= len(SCENE_FALLBACK) + len(SCENE_POSED), \
        "Menu_Main: a fallback screen lost its PortraitRect"
    assert "  hasPortraitLayout: 1\n" in text_, "Menu_Main: the canvas has no portrait layout"
    assert f"guid: {ORIENTATION_GUID}, type: 3}}" in text_, "Menu_Main: no MenuOrientation (portrait would never unlock)"
    assert f"guid: {GLIDE_GUID}, type: 3}}" in text_, "Menu_Main: no OrientationGlide (a turn would cut)"
    for m in re.finditer(r"^--- !u!114 &(-?\d+)\n(.*?)(?=^--- |\Z)", text_, re.S | re.M):
        if any(f"guid: {g}" in m.group(2) for g in (PORTRAIT_GUID, ORIENTATION_GUID, GLIDE_GUID)):
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
    b = open(os.path.join(REPO, GAME_CANVAS), encoding="utf-8").read()
    gc = game_canvas_text(b)
    assert "  hasPortraitLayout: 1\n" in gc and f"guid: {GLIDE_GUID}, type: 3}}" in gc, "GameCanvas: no upright layout or glide"
    outs[GAME_CANVAS] = (b, gc)
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
