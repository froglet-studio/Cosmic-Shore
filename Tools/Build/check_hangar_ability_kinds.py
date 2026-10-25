#!/usr/bin/env python3
"""
Check the Hangar's ability kinds (Docs/MENU_REDESIGN_B.md section 46): every ability the Abilities tab shows gets
its own move on the stage instead of a shot for everything (the owner, 2026-10-17). The kind is read from the
ability's authored name and description by HangarAbilityKinds (Assets/_Scripts/UI/Elements/Hangar/
HangarAbilityKind.cs). This script reads that file's two rule tables AS SHIPPED, runs the same algorithm over
every Assets/Resources/ElementalAbilityMaps/*.asset, prints the table and asserts:

  - every designed ability gets a kind (no designed ability falls to Passive);
  - the fleet shows real variety (at least seven different kinds);
  - the spot checks the owner can recognise (Afterburner boosts, Pulsefire shoots, Scope senses, Fold phases,
    Seed Wall builds, Dig In settles, Field Dipole pulls, Wrecking Ball strikes, Kabloom bursts...).

    python3 Tools/Build/check_hangar_ability_kinds.py              # table + checks
    python3 Tools/Build/check_hangar_ability_kinds.py --self-test  # planted defects must all be caught
"""
import argparse
import glob
import os
import re
import sys

import yaml

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
SOURCE = os.path.join(REPO, "Assets/_Scripts/UI/Elements/Hangar/HangarAbilityKind.cs")
MAPS = os.path.join(REPO, "Assets/Resources/ElementalAbilityMaps/*.asset")
ELEMENTS = {1: "Charge", 2: "Mass", 3: "Space", 4: "Time"}

SPOT = {
    ("Sparrow", "Pulsefire Cannons"): "Shot", ("Sparrow", "Afterburner"): "Boost", ("Sparrow", "Turret Stance"): "Stance",
    ("Sparrow", "Skyburst Rockets"): "Shot", ("Serpent", "Scope"): "Sense", ("Serpent", "Seed Wall"): "Build",
    ("Serpent", "Sniper Shot"): "Shot", ("Butterfly", "Fold"): "Phase", ("Grizzly", "Dig In"): "Stance",
    ("Grizzly", "Rush"): "Boost", ("Stoat", "Field Dipole"): "Pull", ("Thresher", "Wrecking Ball"): "Strike",
    ("Thresher", "Mass Mode"): "Stance", ("Manta", "Kabloom"): "Burst", ("Urchin", "Slip"): "Phase",
    ("Urchin", "Track Projector"): "Build", ("Dolphin", "Echo Sight"): "Sense", ("Squirrel", "Boost Ring"): "Build",
    ("Butterfly", "Mass / Dust Mode"): "Stance", ("Stoat", "Mass / Time Mode"): "Stance",
    ("BrittleStar", "Charge Mode"): "Strike", ("BrittleStar", "Space Mode"): "Shot", ("BrittleStar", "Speed Mode"): "Boost",
}


def parse_rules(src, table):
    block = re.search(table + r" =\s*\{(.*?)\n        \};", src, re.S)
    assert block, f"{table} not found in HangarAbilityKind.cs"
    rules = []
    for kind, words in re.findall(r"\(HangarAbilityKind\.(\w+), new\[\] \{ (.*?) \}\)", block.group(1)):
        rules.append((kind, re.findall(r'"((?:[^"\\]|\\.)*)"', words)))
    assert rules, f"{table} is empty"
    return rules


def window(src):
    return int(re.search(r"DescriptionWindow = (\d+);", src).group(1))


def matches(text, word):
    if not word[0].isalpha():
        return word in text
    return re.search(r"\b" + re.escape(word) + r"(?:s|es)?\b", text, re.I) is not None


def classify(name_rules, desc_rules, win, name, desc):
    for rules, text in ((name_rules, name), (desc_rules, (desc or "")[:win])):
        if not text:
            continue
        for kind, words in rules:
            if any(matches(text, w) for w in words):
                return kind
    return "Passive"


def abilities():
    out = []
    for path in sorted(glob.glob(MAPS)):
        text = open(path, encoding="utf-8").read()
        body = yaml.safe_load(re.sub(r"^%.*\n|^--- .*\n", "", text, flags=re.M))
        vessel = os.path.basename(path)[:-6]
        for e in body["MonoBehaviour"].get("entries", []) or []:
            label = (e.get("AbilityLabel") or "").strip()
            out.append((vessel, ELEMENTS.get(e.get("Element"), "?"), label, e.get("AbilityDescription") or ""))
    return out


def check(src, quiet=False):
    name_rules, desc_rules, win = parse_rules(src, "NameRules"), parse_rules(src, "DescriptionRules"), window(src)
    rows = [(v, el, lab, classify(name_rules, desc_rules, win, lab, d)) for v, el, lab, d in abilities()]
    if not quiet:
        for v, el, lab, k in rows:
            print(f"  {v:12s} {el:7s} {lab[:30]:30s} -> {k}")
    designed = [r for r in rows if r[2] and not r[2].startswith("(open")]
    passive = [f"{v}/{lab}" for v, _, lab, k in designed if k == "Passive"]
    assert not passive, f"designed abilities with no kind (the stage would only glow): {passive}"
    kinds = {k for *_, k in designed}
    assert len(kinds) >= 7, f"only {len(kinds)} kinds across the fleet ({sorted(kinds)}) - the stage would look the same"
    got = {(v, lab): k for v, _, lab, k in rows}
    for key, want in SPOT.items():
        assert key in got, f"{key[0]} no longer has {key[1]!r} (update SPOT)"
        assert got[key] == want, f"{key[0]} {key[1]} reads as {got[key]}, not {want}"
    return rows, kinds


def self_test(src):
    plants = {
        "Boost read before Build (Boost Ring would boost)": lambda s: s.replace(
            '            (HangarAbilityKind.Build, new[] { "trail", "wall", "slab"',
            '            (HangarAbilityKind.Boost, new[] { "ring" }),\n            (HangarAbilityKind.Build, new[] { "trail", "wall", "slab"', 1),
        "the toggle word dropped (Thresher's Mass Mode stops settling)": lambda s: s.replace('"toggle", ', "", 1),
        "the two-mode '/' dropped (Mass / Dust Mode would build)": lambda s: s.replace('{ "/", "stance"', '{ "stance"', 1),
        "the description no longer read (Charge Mode falls to Passive)": lambda s: s.replace(
            "DescriptionWindow = 240;", "DescriptionWindow = 0;", 1),
    }
    ok = True
    for label, plant in plants.items():
        bad = plant(src)
        assert bad != src, f"plant did not apply: {label}"
        try:
            check(bad, quiet=True)
            print(f"  MISSED: {label}")
            ok = False
        except AssertionError as e:
            print(f"  caught: {label} ({e})")
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    src = open(SOURCE, encoding="utf-8").read()
    rows, kinds = check(src, quiet=args.self_test)
    print(f"OK: {len(rows)} abilities, {len(kinds)} kinds ({', '.join(sorted(kinds))}); every spot check holds")
    if args.self_test:
        return 0 if self_test(src) else 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
