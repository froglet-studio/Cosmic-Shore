#!/usr/bin/env python3
"""
The phone layout of Menu_Main, for the screens that have no phone board yet (Docs/UI_STYLE_GUIDE.md section 8, D6;
Docs/MENU_REDESIGN_B.md sections 127-129).

The phone scale switches a whole canvas at once (AdaptiveCanvasScaler.hasPhoneLayout). Menu_Main holds every menu
screen, and only Home, Arcade, Configure and Hangar have approved phone boards. Every other screen gets a PhoneRect with
`drawAtPcScale`: on a phone it is drawn exactly as the 1920 x 1080 layout shrunk onto the phone draws it today (its
KitTexts keep their PC sizes), so Menu_Main can switch to the phone layout without one of them breaking. A screen moves
off this list when its phone board is approved and its own generator writes its phone layout.

    Menu_Main.unity objects       the screens, windows and overlays the scene itself owns
    prefab roots                  the screens that are prefab instances in the scene (their prefab's root carries it)

The PhoneRect is added last on each object and re-added every run, so a re-run is a no-op (--check). Each owning
generator's --check stays clean (they patch, they do not re-emit these objects); gates.sh runs all of them. A screen whose
generator re-emits its root (Codex, Port, Replays, Credits, the hub window strip) carries the fallback in that generator.

    python3 Tools/Build/author_menu_phone.py            # write
    python3 Tools/Build/author_menu_phone.py --check    # fail if any output drifted
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

# Scene objects of Menu_Main with no phone board yet, by their (unique) name.
SCENE_FALLBACK = [
    "Hangar Screen", "ArkScreen", "PortScreen", "ProfileScreen",
    "FriendsDrawerHost", "ToastNotificationContainer", "ToggleGameMenuButton", "Game UI",
    "ArcadeScreenModal", "MissionScreenModal", "ToyboxScreenModal", "ArenaScreenModal",
    "ToyboxGameConfigureModal", "ArenaGameConfigureModal", "MaelstromGameConfigurationModal",
    "ProfileModal", "LeaderboardConfigureModal", "PlayerDataSelectModal", "InitializingScreen", "SceneTransitionModal",
]

# Screens that are prefab instances directly under a container that is not itself a fallback block.
PREFAB_FALLBACK = [
    "Assets/_Prefabs/UI Elements/Panels/SettingsModal.prefab",
    "Assets/_Prefabs/ArcadeGameConfigureModal.prefab",
    "Assets/_Prefabs/UI Elements/Quest/InGameInstructionSet.prefab",
]

# Scene objects of Menu_Main that HAVE a phone layout and no generator of their own to write it (the nav band is
# patched in place by author_menu_topnav.py, which keeps other generators' components on it). R55 (section 129): the
# band is the rail down the left, 96 wide, its tabs stacked Home first.
RAIL_W, TAB_H = 96, 56
SCENE_PHONE = {
    "NavBar": dict(pose=K.rect((0, 0), (0, 1), (RAIL_W / 2, 0), (RAIL_W, 0))),
    "NavBarButtons": dict(pose=K.STRETCH, stack=dict(origin=(0, -10), item=(RAIL_W, TAB_H), spacing=2,
                                                     order=["HomeLink", "CodexLink", "HangarLink", "ArkLink", "PortLink"])),
}
PHONE_GUID = K.meta_guid("Assets/_Scripts/UI/PhoneRect.cs")


def mark(p, go):
    """Add (or re-add) the fallback PhoneRect last on `go`."""
    p.remove_comp(go, str(p.ctx.fid(f"{go}#phone")))
    p.add_comp(go, "phone", 114, K.phone_rect(pc_block=True))


def scene_text(text_):
    p = Patch(text_, "Menu_Main/Phone")
    for name in list(SCENE_FALLBACK) + list(SCENE_PHONE):
        gos = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!1 ") and f"  m_Name: {name}\n" in d]
        assert len(gos) == 1, f"Menu_Main: expected one {name!r}, found {len(gos)}"
        if name in SCENE_PHONE:
            p.remove_comp(gos[0], str(p.ctx.fid(f"{gos[0]}#phone")))
            p.add_comp(gos[0], "phone", 114, K.phone_rect(**SCENE_PHONE[name]))
        else:
            mark(p, gos[0])
    return p.text()


def prefab_text(text_, key):
    p = Patch(text_, key)
    roots = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!224 ") and "  m_Father: {fileID: 0}\n" in d]
    assert len(roots) == 1, f"{key}: one root rect expected"
    go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", p.doc(roots[0])).group(1)
    mark(p, go)
    return p.text()


def outputs():
    out = {MENU: scene_text(open(os.path.join(REPO, MENU), encoding="utf-8").read())}
    for path in PREFAB_FALLBACK:
        out[path] = prefab_text(open(os.path.join(REPO, path), encoding="utf-8").read(), os.path.basename(path) + "/Phone")
    return out


def validate(out):
    for path, text_ in out.items():
        n = text_.count(f"guid: {PHONE_GUID}, type: 3}}")
        want = len(SCENE_FALLBACK) + len(SCENE_PHONE) if path == MENU else 1
        assert n >= want, f"{path}: {n} PhoneRects, expected at least {want}"
        for m in re.finditer(r"^--- !u!114 &(-?\d+)\n(.*?)(?=^--- |\Z)", text_, re.S | re.M):
            body = m.group(2)
            if f"guid: {PHONE_GUID}" in body:
                go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body).group(1)
                assert re.search(r"^--- !u!1 &%s\n(?:.*\n)*?  - component: \{fileID: %s\}" % (go, m.group(1)), text_, re.M), \
                    f"{path}: PhoneRect {m.group(1)} is not in its GameObject's component list"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    out = outputs()
    validate(out)
    drifted = [path for path, text_ in out.items() if open(os.path.join(REPO, path), encoding="utf-8").read() != text_]
    if args.check:
        for path in drifted:
            print(f"DRIFT: {path}")
        print("OK: no drift" if not drifted else f"FAIL: {len(drifted)} file(s) drifted")
        return 1 if drifted else 0
    for path in drifted:
        with open(os.path.join(REPO, path), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(out[path])
        print(f"wrote {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
