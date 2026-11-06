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
    "ToastNotificationContainer", "ToggleGameMenuButton", "Game UI", "InitializingScreen", "SceneTransitionModal",
]

# Screens that are prefab instances directly under a container that is not itself a fallback block.
PREFAB_FALLBACK = [
    "Assets/_Prefabs/UI Elements/Quest/InGameInstructionSet.prefab",
]

# Screens that HAD the fallback and now carry their own phone layout (their generators write it): the marker this tool
# left on them is taken off, so a screen never carries both.
SCENE_RETIRED = ["ArenaGameConfigureModal", "MaelstromGameConfigurationModal", "Hangar Screen",
                 "MissionScreenModal", "ToyboxScreenModal", "ArenaScreenModal", "ArkScreen", "PortScreen",
                 "ProfileScreen", "ProfileModal", "PlayerDataSelectModal", "ToyboxGameConfigureModal",
                 "LeaderboardConfigureModal", "HomeScreen", "ArcadeScreenModal"]
PREFAB_RETIRED = ["Assets/_Prefabs/ArcadeGameConfigureModal.prefab", "Assets/_Prefabs/UI Elements/Panels/SettingsModal.prefab"]

# Scene objects of Menu_Main that HAVE a phone layout and no generator of their own to write it (the nav band is
# patched in place by author_menu_topnav.py, which keeps other generators' components on it). R55 (section 129): the
# band is the rail down the RIGHT, 96 wide, its tabs in the PC band's order: Codex, Hangar, Home, Ark, Port (Shombith,
# 2026-11-06: "the order will be codex, hangar, home ark and port just like in the PC version do not change order").
# The band's LT / RT arrows leave the canvas on a phone (author_menu_topnav.py writes their PhoneRect).
RAIL_W, TAB_H = 96, 56
SCENE_PHONE = {
    "NavBar": dict(pose=K.rect((1, 0), (1, 1), (-RAIL_W / 2, 0), (RAIL_W, 0))),
    # The rail is on the RIGHT (Shombith, 2026-11-06: "Put codex hangar etc on the right side"). Every screen's phone
    # layout is authored for a rail on the left (content from x 112, 16 from the right); its containers move 96 left on
    # a phone, so each screen's content runs from 16 to 112 short of the right edge, clear of the rail.
    "Screens": dict(pose=K.rect((0, 0), (1, 1), (-RAIL_W, 0), (0, 0))),
    "ModalWindows": dict(pose=K.rect((0, 0), (1, 1), (-RAIL_W, 0), (0, 0))),
    "NavBarButtons": dict(pose=K.STRETCH, stack=dict(origin=(0, -10), item=(RAIL_W, TAB_H), spacing=2,
                                                     order=["CodexLink", "HangarLink", "HomeLink", "ArkLink", "PortLink"])),
}
PHONE_GUID = K.meta_guid("Assets/_Scripts/UI/PhoneRect.cs")


# Fallback objects that cover the whole canvas on PC (overlays, covers, the freestyle HUD): on a phone they are the PC
# layout shrunk onto the whole canvas. Every other fallback is a window under the PC nav band, which is the rail on a
# phone: its layout less the band fills the region right of the rail (K.phone_window_block).
FULL_CANVAS = {"ToastNotificationContainer", "ToggleGameMenuButton", "Game UI", "InitializingScreen", "SceneTransitionModal",
               "InGameInstructionSet.prefab"}


# Full-canvas covers inside ModalWindows (moved 96 left on a phone): their region is widened back over the whole canvas.
IN_SHIFTED = {"InitializingScreen", "SceneTransitionModal"}


def mark(p, go, name):
    """Add (or re-add) the fallback PhoneRect last on `go`."""
    p.remove_comp(go, str(p.ctx.fid(f"{go}#phone")))
    if name in IN_SHIFTED:
        fn = K.phone_rect(pc_block=True, insets=(RAIL_W, 0, -RAIL_W, 0))
    elif name == "ToastNotificationContainer":
        fn = K.phone_rect(pc_block=True, insets=(0, 0, RAIL_W, 0))   # the toasts (top right) clear the rail
    elif name in FULL_CANVAS:
        fn = K.phone_rect(pc_block=True)
    else:
        fn = K.phone_window_block()
    p.add_comp(go, "phone", 114, fn)


# Menu_Main's canvas: every screen now has a phone layout or the fallback, so it switches (Shombith, 2026-11-05).
SCALER_GUID = K.meta_guid("Assets/_Scripts/UI/AdaptiveCanvasScaler.cs")
PHONE_FIELDS = "  hasPhoneLayout: 1\n" + K.PHONE_REFERENCE_YAML


def switch_on(p):
    found = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!114 ") and f"guid: {SCALER_GUID}, type: 3}}" in d]
    assert len(found) == 1, f"Menu_Main: expected one AdaptiveCanvasScaler, found {len(found)}"
    doc = p.doc(found[0])
    doc = re.sub(r"  hasPhoneLayout: \d\n  phoneReferenceResolution: \{x: [-\d.]+, y: [-\d.]+\}\n", "", doc)
    assert "  safeZoneMaxAspect: " in doc, "Menu_Main: the AdaptiveCanvasScaler body changed shape"
    doc = re.sub(r"(  safeZoneMaxAspect: [^\n]*\n)", lambda m: m.group(1) + PHONE_FIELDS, doc, count=1)
    p.replace(found[0], doc)


def scene_text(text_):
    p = Patch(text_, "Menu_Main/Phone")
    for name in list(SCENE_FALLBACK) + list(SCENE_PHONE):
        gos = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!1 ") and f"  m_Name: {name}\n" in d]
        assert len(gos) == 1, f"Menu_Main: expected one {name!r}, found {len(gos)}"
        if name in SCENE_PHONE:
            p.remove_comp(gos[0], str(p.ctx.fid(f"{gos[0]}#phone")))
            p.add_comp(gos[0], "phone", 114, K.phone_rect(**SCENE_PHONE[name]))
        else:
            mark(p, gos[0], name)
    for name in SCENE_RETIRED:
        gos = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!1 ") and f"  m_Name: {name}\n" in d]
        assert len(gos) == 1, f"Menu_Main: expected one {name!r}, found {len(gos)}"
        p.remove_comp(gos[0], str(p.ctx.fid(f"{gos[0]}#phone")))
    switch_on(p)
    return p.text()


def prefab_text(text_, key, retired=False):
    p = Patch(text_, key)
    roots = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!224 ") and "  m_Father: {fileID: 0}\n" in d]
    assert len(roots) == 1, f"{key}: one root rect expected"
    go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", p.doc(roots[0])).group(1)
    if retired:
        p.remove_comp(go, str(p.ctx.fid(f"{go}#phone")))
    else:
        mark(p, go, key.rsplit("/Phone", 1)[0])
    return p.text()


def outputs():
    out = {MENU: scene_text(open(os.path.join(REPO, MENU), encoding="utf-8").read())}
    for path in PREFAB_FALLBACK:
        out[path] = prefab_text(open(os.path.join(REPO, path), encoding="utf-8").read(), os.path.basename(path) + "/Phone")
    for path in PREFAB_RETIRED:
        out[path] = prefab_text(open(os.path.join(REPO, path), encoding="utf-8").read(), os.path.basename(path) + "/Phone",
                                retired=True)
    return out


def pc_block_names(text_):
    """The names of Menu_Main's objects that carry the PC-scale fallback (drawAtPcScale: 1)."""
    gos = {m.group(1): m.group(2) for m in re.finditer(r"^--- !u!1 &(-?\d+)\n(?:(?!--- ).*\n)*?  m_Name: (.*)\n", text_, re.M)}
    out = []
    for m in re.finditer(r"^--- !u!114 &-?\d+\n((?:(?!--- ).*\n)*)", text_, re.M):
        body = m.group(1)
        if f"guid: {PHONE_GUID}" in body and "  drawAtPcScale: 1\n" in body:
            out.append(gos.get(re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body).group(1), "?"))
    return sorted(out)


def validate(out):
    # A screen that leaves the fallback must lose its marker: a stale one draws its new phone layout shrunk (HomeScreen
    # and ArcadeScreenModal shipped that way on 2026-11-06 - Home's hubs a third of their size, its chip off screen).
    stale = sorted(set(pc_block_names(out[MENU])) - set(SCENE_FALLBACK))
    assert not stale, f"Menu_Main: {stale} still draw at the PC scale but are not on the fallback list (add them to SCENE_RETIRED)"
    for path, text_ in out.items():
        n = text_.count(f"guid: {PHONE_GUID}, type: 3}}")
        if path in PREFAB_RETIRED:
            continue
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
