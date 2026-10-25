#!/usr/bin/env python3
"""
Every press the main menu offers is a pad stop (Docs/MENU_REDESIGN_B.md §65; Yash, 2026-10-25: "make sure all the
buttons and all the places where it's clickable can be navigated using thumbsticks").

Reads Menu_Main.unity and every prefab it reaches (placed in the scene, or referenced by a field and instantiated at
runtime) and fails on:

  1. an interactable Selectable whose navigation is None - the D-pad / left stick can never select it;
  2. a pointer-only press: a GameObject with a PointerClick / PointerDown EventTrigger, or a script that handles
     IPointerClickHandler / IPointerDownHandler, and no Selectable (a prefab instance's Selectable is looked up in
     its source prefab).

A control that is meant to stay pointer-only is listed in ALLOWED with the reason the pad does not need it (the
pad has the same action another way). A new entry needs that reason; "the pad cannot reach it" is not one.

    python3 Tools/Build/check_menu_pad_reach.py              # report; exit 1 on a finding
    python3 Tools/Build/check_menu_pad_reach.py --self-test  # planted defects must be caught

A READER: it writes nothing.
"""

import argparse
import collections
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SCENE = "Assets/_Scenes/Menu_Main.unity"
EVENT_TRIGGER = "d0b148fe25e99eb48b9724523833bab1"
HDR = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?[ \t]*$", re.M)

# (file, object path suffix) -> why the pad does not need it.
ALLOWED = {
    ("Assets/_Scenes/Menu_Main.unity", "Safe Area/NavBar/NavBarButtons/LeftArrrow"):
        "the pointer's twin of LT on the top tab bar (§64); the pad presses LT itself",
    ("Assets/_Scenes/Menu_Main.unity", "Safe Area/NavBar/NavBarButtons/RightArrow"):
        "the pointer's twin of RT on the top tab bar (§64); the pad presses RT itself",
    ("Assets/_Scenes/Menu_Main.unity", "Safe Area/HubWindowNavHost/LeftArrow"):
        "the pointer's twin of LT on the hub strip (§65); the pad presses LT itself",
    ("Assets/_Scenes/Menu_Main.unity", "Safe Area/HubWindowNavHost/RightArrow"):
        "the pointer's twin of RT on the hub strip (§65); the pad presses RT itself",
    ("Assets/_Scenes/Menu_Main.unity", "Safe Area/Screens/HomeScreen/Button"):
        "Home's full-screen double-tap-to-fly surface; the pad enters freestyle with Select",
    ("Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureView.prefab", "ConfigureView/Preview/Focus"):
        "the preview's invisible tap surface; FLY, a pad stop, does the same",
    ("Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureArenaView.prefab", "ConfigureArenaView/Preview/Focus"):
        "the preview's invisible tap surface; FLY, a pad stop, does the same",
    ("Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureView.prefab", "ConfigureView/FlyingHint/StopButton"):
        "shown only while the sticks fly the preview; the pad stops with START",
    ("Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureArenaView.prefab", "ConfigureArenaView/FlyingHint/StopButton"):
        "shown only while the sticks fly the preview; the pad stops with START",
    ("Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureView.prefab", "ConfigureView/FlyingHint/FullScreenButton"):
        "shown only while the sticks fly the preview, which the pad cannot navigate out of",
    ("Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/ConfigureArenaView.prefab", "ConfigureArenaView/FlyingHint/FullScreenButton"):
        "shown only while the sticks fly the preview, which the pad cannot navigate out of",
    ("Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/VesselInfoCard.prefab", "VesselInfoCard"):
        "the card's tap-outside backdrop; the pad closes it with B or its X",
    ("Assets/_Prefabs/UI Elements/Main Menu Screens/Toybox/ToyboxView.prefab", "ShuffleButton"):
        "the shuffle card's own SHUFFLE button; the card, a pad stop, shuffles exactly the same",
    ("Assets/_Prefabs/UI Elements/Panels/CreditsModal.prefab", "CreditsModal/Content/Window/Scrollbar"):
        "the right stick scrolls the credits; a focusable bar would steal focus from Close",
}


def docs(text):
    ms = list(HDR.finditer(text))
    for i, m in enumerate(ms):
        yield int(m.group(1)), m.group(2), bool(m.group(3)), text[m.start(): ms[i + 1].start() if i + 1 < len(ms) else len(text)]


def guid_index():
    g2p = {}
    for root, _, names in os.walk(os.path.join(REPO, "Assets")):
        for n in names:
            if n.endswith(".meta"):
                p = os.path.join(root, n)
                with open(p, errors="ignore") as fh:
                    m = re.search(r"^guid: (\w+)", fh.read(300), re.M)
                if m:
                    g2p[m.group(1)] = os.path.relpath(p[:-5], REPO)
    return g2p


def pointer_scripts(g2p):
    """Runtime scripts that take a pointer press without being a Selectable themselves."""
    out = set()
    for g, p in g2p.items():
        if not p.endswith(".cs") or "/Editor/" in p:
            continue
        with open(os.path.join(REPO, p), errors="ignore") as fh:
            src = fh.read()
        if re.search(r"\bIPointer(Click|Down)Handler\b", src) and \
                not re.search(r"class \w+\s*:\s*(KitSelectableBase|Button|Selectable|Toggle|Slider|Scrollbar)\b", src):
            out.add(g)
    return out


def reachable_files(g2p, texts):
    seen, todo = [], [SCENE]
    while todo:
        f = todo.pop()
        if f in seen or f not in texts:
            continue
        seen.append(f)
        for g in set(re.findall(r"guid: (\w+), type: 3\}", texts[f])):
            p = g2p.get(g)
            if p and p.endswith(".prefab"):
                todo.append(p)
    return seen


class File:
    def __init__(self, text):
        self.names, self.father, self.rt_go, self.go_rt = {}, {}, {}, {}
        self.comps = collections.defaultdict(list)
        self.stripped_src = {}
        for cls, fid, stripped, d in docs(text):
            if cls == 1:
                if stripped:
                    m = re.search(r"m_CorrespondingSourceObject: \{fileID: (-?\d+), guid: (\w+)", d)
                    if m:
                        self.stripped_src[fid] = (m.group(1), m.group(2))
                m = re.search(r"m_Name: (.*)", d)
                self.names[fid] = m.group(1).strip() if m else "?"
            elif cls in (4, 224) and not stripped:
                g = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", d)
                fa = re.search(r"m_Father: \{fileID: (-?\d+)\}", d)
                if g:
                    self.rt_go[fid], self.go_rt[g.group(1)] = g.group(1), fid
                if fa:
                    self.father[fid] = fa.group(1)
            elif cls == 114 and not stripped:
                g = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", d)
                s = re.search(r"m_Script: \{fileID: \d+, guid: (\w+)", d)
                if g:
                    self.comps[g.group(1)].append((s.group(1) if s else "", d))

    def path(self, go):
        parts, rt = [self.names.get(go, "?")], self.go_rt.get(go)
        for _ in range(12):
            rt = self.father.get(rt)
            if not rt or rt not in self.rt_go:
                break
            parts.append(self.names.get(self.rt_go[rt], "?"))
        return "/".join(reversed(parts))


def has_selectable(comps):
    return any("  m_Navigation:" in d for _, d in comps)


def findings(texts, g2p, pointer):
    out = []
    parsed = {}

    def file(p):
        if p not in parsed and p in texts:
            parsed[p] = File(texts[p])
        return parsed.get(p)

    for f in reachable_files(g2p, texts):
        # A prefab instance can take a control out of navigation by override, with nothing on the prefab to show it.
        for cls, _, _, d in docs(texts[f]):
            if cls == 1001 and re.search(r"propertyPath: m_Navigation\.m_Mode\n\s+value: 0\n", d):
                name = re.search(r"propertyPath: m_Name\n\s+value: (.*)\n", d)
                out.append((f, name.group(1).strip() if name else "a prefab instance",
                            "navigation set to None by an instance override"))
        fl = file(f)
        for go, comps in fl.comps.items():
            where = fl.path(go)
            for _, d in comps:
                if re.search(r"  m_Navigation:\n    m_Mode: 0\n", d) and "  m_Interactable: 1\n" in d:
                    out.append((f, where, "out of pad navigation (Navigation None)"))
            if has_selectable(comps):
                continue
            presses = [g for g, d in comps if (g == EVENT_TRIGGER and re.search(r"- eventID: (2|4)\n", d)) or g in pointer]
            if not presses:
                continue
            src = fl.stripped_src.get(go)
            if src:   # a prefab instance: its Selectable may live in the source prefab
                sf = file(g2p.get(src[1], ""))
                if sf and has_selectable(sf.comps.get(src[0], [])):
                    continue
            out.append((f, where, "a pointer press with no Selectable"))
    return [(f, w, why) for f, w, why in out
            if not any(f == af and (w == aw or w.endswith("/" + aw)) for af, aw in ALLOWED)]


def load():
    g2p = guid_index()
    texts = {}
    for p in set(g2p.values()) | {SCENE}:
        if p.endswith((".prefab", ".unity")) and p.startswith(("Assets/_Scenes/Menu_Main", "Assets/_Prefabs/")):
            with open(os.path.join(REPO, p), errors="ignore") as fh:
                texts[p] = fh.read()
    return texts, g2p


def self_test(texts, g2p, pointer):
    assert not findings(texts, g2p, pointer), "self-test needs a clean tree"
    star = "Assets/_Prefabs/UI Elements/Main Menu Screens/Arcade/ArcadeGameCard.prefab"
    plants = {
        "the Arcade card's star leaves pad navigation": (star, _star_off),
        "a scene override takes a control out of navigation": (SCENE, lambda t: t.replace(
            "    m_Modifications:\n", "    m_Modifications:\n    - target: {fileID: 0}\n      propertyPath: m_Navigation.m_Mode\n"
            "      value: 0\n      objectReference: {fileID: 0}\n", 1)),
        "a tab on the top bar loses its Selectable": ("Assets/_Prefabs/UI Elements/Kit/KitTopNavTab.prefab",
                                                     lambda t: t.replace("  m_Navigation:", "  m_Navigatio_:")),
    }
    ok = True
    for label, (path, plant) in plants.items():
        bad = dict(texts)
        bad[path] = plant(texts[path])
        assert bad[path] != texts[path], f"plant did not apply: {label}"
        if findings(bad, g2p, pointer):
            print(f"  caught: {label}")
        else:
            print(f"  MISSED: {label}")
            ok = False
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def _star_off(t):
    """Set the FavoriteStar Button's navigation to None (the first Button whose GameObject is FavoriteStar)."""
    go = re.search(r"--- !u!1 &(-?\d+)\nGameObject:\n(?:.*\n)*?  m_Name: FavoriteStar\n", t).group(1)
    for _, fid, _, d in docs(t):
        if f"m_GameObject: {{fileID: {go}}}" in d and "  m_Navigation:\n    m_Mode: 3\n" in d:
            return t.replace(d, d.replace("  m_Navigation:\n    m_Mode: 3\n", "  m_Navigation:\n    m_Mode: 0\n", 1), 1)
    raise AssertionError("FavoriteStar Button not found")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    texts, g2p = load()
    pointer = pointer_scripts(g2p)
    if a.self_test:
        return 0 if self_test(texts, g2p, pointer) else 1
    bad = findings(texts, g2p, pointer)
    for f, where, why in bad:
        print(f"FAIL {f}: {where} - {why}")
    n = len(reachable_files(g2p, texts))
    print(f"menu pad reach: {'OK' if not bad else f'{len(bad)} PROBLEM(S)'} ({n} files, {len(ALLOWED)} allowed pointer-only)")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
