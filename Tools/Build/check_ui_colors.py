#!/usr/bin/env python3
"""Hold the style-B UI to Garrett's colour rules (Docs/UI_COLOR_RULES.md, 2026-10-24).

The rules this gate can read from the assets:
  1. Elements carry no fixed colour anywhere in the UI. No kit component draws in an element token
     (UIColorToken ElementCharge / ElementMass / ElementSpace / ElementTime = 13-16), no Image or text
     is authored in an element's literal colour, and no Image runs a retired ElementRim_* material
     (the comet of an element's light round a panel, which also read as the selection).
  2. No KitButton uses a retired element variant (Mass / Space / Time = 7-9).
  3. The in-match ability lockup paints no element colour and runs no lava-lamp motion
     (AbilityLockupStyle.asset elementColour / lavaMotion = 0).

What it cannot read (meaning: gold only for spending, lime only for a categorically good call to action,
fire only for danger, the selection blue only on what is actually selected) is held by each screen's
generator and by review against Docs/UI_COLOR_RULES.md.

Scanned: every prefab under Assets/_Prefabs/UI Elements, GameCanvas.prefab, and the scenes the style-B
UI lives in. A READER: it writes nothing.

Usage:
    check_ui_colors.py [--self-test]
"""

import glob
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCENES = ("Assets/_Scenes/Menu_Main.unity", "Assets/_Scenes/Authentication.unity", "Assets/_Scenes/Bootstrap.unity",
          "Assets/_Scenes/Multiplayer Scenes/Maelstrom.unity", "Assets/_Scenes/Game_TestDesign/UIKitTestScene.unity")
LOCKUP = "Assets/Resources/AbilityLockupStyle.asset"
KIT_BUTTON = "36c1a95b6e2ce113095054da8aaa40ab"          # KitButton.cs
ELEMENT_TOKENS = {13: "ElementCharge", 14: "ElementMass", 15: "ElementSpace", 16: "ElementTime"}
RETIRED_VARIANTS = {7: "Mass", 8: "Space", 9: "Time"}
# As Unity serializes them (UITheme's element colours).
ELEMENT_COLOURS = {"Charge": ("1", "0.827451", "0.3019608"), "Mass": ("0.49803922", "0.65882355", "1"),
                   "Space": ("0.6901961", "0.4862745", "1"), "Time": ("0.37254903", "0.9411765", "0.78431374")}


def _lattice_guid(key):
    import hashlib
    return hashlib.md5(f"CosmicShore/MenuB/lattice/{key}".encode()).hexdigest()


RIM_GUIDS = {_lattice_guid(f"rim_{e.lower()}_mat"): e for e in ("Charge", "Mass", "Space", "Time", "Omni")}
TOKEN_FIELD = re.compile(r"^\s+(\w*[tT]oken): (1[3-6])$", re.M)
TOKEN_OVERRIDE = re.compile(r"propertyPath: ([\w.\[\]]*[tT]oken)\n\s+value: (1[3-6])\n")
VARIANT_OVERRIDE = re.compile(r"target: \{fileID: -?\d+, guid: \w+, type: 3\}\n\s+propertyPath: variant\n\s+value: ([789])\n")
COLOUR = {e: re.compile(r"\{r: %s, g: %s, b: %s, a: [\d.]+\}" % tuple(re.escape(x) for x in c))
          for e, c in ELEMENT_COLOURS.items()}
DOC = re.compile(r"^--- !u!114 &-?\d+\nMonoBehaviour:\n((?:(?!--- ).*\n)*)", re.M)


def scanned():
    paths = glob.glob(os.path.join(ROOT, "Assets/_Prefabs/UI Elements/**/*.prefab"), recursive=True)
    paths.append(os.path.join(ROOT, "Assets/_Prefabs/CORE/GameCanvas.prefab"))
    paths += [os.path.join(ROOT, s) for s in SCENES]
    out = {}
    for p in paths:
        if os.path.exists(p):
            out[os.path.relpath(p, ROOT)] = open(p, encoding="utf-8", errors="replace").read()
    lockup = os.path.join(ROOT, LOCKUP)
    out[LOCKUP] = open(lockup, encoding="utf-8").read() if os.path.exists(lockup) else ""
    return out


def problems(files):
    bad = []
    for path, text in sorted(files.items()):
        if path == LOCKUP:
            for field in ("elementColour", "lavaMotion"):
                if f"  {field}: 0\n" not in text:
                    bad.append(f"{path}: {field} is not 0 - the ability lockup paints element colours / lava motion again")
            continue
        for m in TOKEN_FIELD.finditer(text):
            bad.append(f"{path}: {m.group(1)} draws in {ELEMENT_TOKENS[int(m.group(2))]}")
        for m in TOKEN_OVERRIDE.finditer(text):
            bad.append(f"{path}: an override sets {m.group(1)} to {ELEMENT_TOKENS[int(m.group(2))]}")
        for guid, element in RIM_GUIDS.items():
            if f"guid: {guid}" in text:
                bad.append(f"{path}: an Image runs the retired ElementRim_{element} material")
        for element, pat in COLOUR.items():
            n = len(pat.findall(text))
            if n:
                bad.append(f"{path}: {n} colour(s) authored in {element}'s literal element colour")
        for m in DOC.finditer(text):
            body = m.group(1)
            if f"guid: {KIT_BUTTON}" not in body:
                continue
            v = re.search(r"^  variant: (\d+)$", body, re.M)
            if v and int(v.group(1)) in RETIRED_VARIANTS:
                bad.append(f"{path}: a KitButton uses the retired {RETIRED_VARIANTS[int(v.group(1))]} variant")
        for m in VARIANT_OVERRIDE.finditer(text):
            bad.append(f"{path}: an override sets a button to the retired {RETIRED_VARIANTS[int(m.group(1))]} variant")
    return bad


def self_test(files):
    some = next(p for p in files if p.endswith(".prefab") and f"guid: {KIT_BUTTON}" in files[p])
    cases = [
        ("a kit token drawing in Mass", lambda f: f.__setitem__(some, f[some] + "  colorToken: 14\n")),
        ("an override setting a token to Time", lambda f: f.__setitem__(some, f[some] +
            "    - target: {fileID: 1, guid: 0, type: 3}\n      propertyPath: m_Token\n      value: 16\n")),
        ("the Omni rim back on a panel", lambda f: f.__setitem__(some, f[some] +
            f"  m_Material: {{fileID: 2100000, guid: {_lattice_guid('rim_omni_mat')}, type: 2}}\n")),
        ("an Image authored in Charge's colour", lambda f: f.__setitem__(some, f[some] +
            "  m_Color: {r: 1, g: 0.827451, b: 0.3019608, a: 1}\n")),
        ("a KitButton on the retired Space variant", lambda f: f.__setitem__(some, re.sub(
            r"(m_Script: \{fileID: 11500000, guid: %s, type: 3\}\n(?:(?!--- ).*\n)*?  variant: )\d+" % KIT_BUTTON,
            r"\g<1>8", f[some], count=1))),
        ("a scene override putting a button on Mass", lambda f: f.__setitem__(some, f[some] +
            "    - target: {fileID: 5, guid: 36c1a95b6e2ce113095054da8aaa40ab, type: 3}\n      propertyPath: variant\n      value: 7\n")),
        ("the lockup's lava lamp back on", lambda f: f.__setitem__(LOCKUP, f[LOCKUP].replace("  lavaMotion: 0\n", "  lavaMotion: 1\n"))),
    ]
    ok = True
    for label, mutate in cases:
        planted = dict(files)
        mutate(planted)
        found = problems(planted)
        if found:
            print(f"  caught: {label} ({found[0]})")
        else:
            print(f"  MISSED: {label}")
            ok = False
    print("OK: every planted defect is caught" if ok else "FAIL: a planted defect was not caught")
    return ok


def main():
    files = scanned()
    if "--self-test" in sys.argv[1:]:
        base = problems(files)
        if base:
            print("FAIL: the shipped UI already breaks the rules, so the self-test cannot isolate its plants:")
            print("\n".join(f"  {b}" for b in base))
            sys.exit(1)
        sys.exit(0 if self_test(files) else 1)
    bad = problems(files)
    for b in bad:
        print(f"FAIL: {b}")
    if bad:
        sys.exit(f"{len(bad)} colour-rule problem(s) - see Docs/UI_COLOR_RULES.md")
    print(f"OK: {len(files)} UI files follow the colour rules (no element colours, rims or element variants)")


if __name__ == "__main__":
    main()
