#!/usr/bin/env python3
"""Elements have no colour; the four domain colours are TEAM identity only (Garrett, 2026-10-24, LOCKED).

    python3 Tools/Build/check_element_colour.py              # exit 1 if the rule is broken
    python3 Tools/Build/check_element_colour.py --self-test  # prove it fires

"we need to make sure we never conflate the domain colors with elemental identity. jade is not time. gold is not
charge. mass is not blue. space is not ruby. and when you do that it confuses players ... we need to set a rule that
we explicitly do not do this."

What the gate holds today:
  1. Resources/AbilityLockupStyle.asset serialises `elementColour: 0` - the rev 3 lava-lamp colouring of the
     ability cards (Charge gold, Mass blue, Space violet, Time teal) is OFF. The SO's C# default is still true, so
     an asset that drops the line silently turns it back on; the line must be present and zero.
  2. No ability-icon sprite under Assets/_Graphics/Icons/AbilityIcons/ carries a baked colour (icons are pure
     white; the HUD tints by state and by TEAM, never by element).
  3. The known element->colour maps that remain in code are LISTED, not failed - each has an owner to resolve
     them with (Docs/claude/VESSEL_HUD_AND_ELEMENTS.md, "Elements have no colour"). Add a site here when you find
     one; remove it when it is gone.
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
STYLE = os.path.join(ROOT, "Assets", "Resources", "AbilityLockupStyle.asset")
ICONS = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "AbilityIcons")
KNOWN_SITES = [
    ("Assets/_Scripts/Editor/Codex/CodexHarvester.cs", "Element.Charge => new Color", "Shombith (Codex)"),
    ("Assets/_Scripts/Controller/Vessel/Crew/KiteNavigatorConfigSO.cs", "elementColors", "the Wrasse & Grouper crew (Kite Navigator)"),
    ("Assets/_Scripts/ScriptableObjects/AbilityLockupStyleSO.cs", "chargeColour", "Shombith (lockup rev 3; OFF on the asset)"),
]


def check_style(path=STYLE):
    text = open(path, encoding="utf-8").read()
    m = re.search(r"^\s*elementColour:\s*(\d+)\s*$", text, re.M)
    if not m:
        return ["AbilityLockupStyle.asset does not serialise elementColour (the C# default is ON): add `elementColour: 0`"]
    if m.group(1) != "0":
        return [f"AbilityLockupStyle.asset elementColour: {m.group(1)} - the cards colour by element; set it to 0"]
    return []


def check_icons(folder=ICONS):
    try:
        import numpy as np
        from PIL import Image
    except ImportError:
        return []
    bad = []
    for png in sorted(glob.glob(os.path.join(folder, "*", "*.png"))):
        a = np.asarray(Image.open(png).convert("RGBA"))
        lit = a[..., 3] > 25
        if lit.any() and (a[lit][:, :3] < 250).any():
            bad.append(f"{os.path.relpath(png, ROOT)} carries a baked colour (icons are pure white; the HUD tints)")
    return bad


def list_sites():
    out = []
    for rel, needle, owner in KNOWN_SITES:
        p = os.path.join(ROOT, rel)
        if os.path.exists(p) and needle in open(p, encoding="utf-8", errors="ignore").read():
            out.append(f"  listed: {rel} ({needle}) - owner: {owner}")
    return out


def self_test():
    import tempfile
    ok = True
    with tempfile.TemporaryDirectory() as td:
        p = os.path.join(td, "s.asset")
        open(p, "w").write("MonoBehaviour:\n  plateWidth: 104\n")
        ok &= bool(check_style(p))
        open(p, "w").write("MonoBehaviour:\n  elementColour: 1\n")
        ok &= bool(check_style(p))
        open(p, "w").write("MonoBehaviour:\n  elementColour: 0\n")
        ok &= not check_style(p)
        try:
            from PIL import Image
            os.makedirs(os.path.join(td, "Hull"))
            Image.new("RGBA", (8, 8), (0, 200, 255, 255)).save(os.path.join(td, "Hull", "tinted.png"))
            ok &= bool(check_icons(td))
            Image.new("RGBA", (8, 8), (255, 255, 255, 255)).save(os.path.join(td, "Hull", "tinted.png"))
            ok &= not check_icons(td)
        except ImportError:
            pass
    print("self-test " + ("ok" if ok else "FAILED"))
    return 0 if ok else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    problems = check_style() + check_icons()
    for p in problems:
        print("FAIL " + p)
    for line in list_sites():
        print(line)
    print(f"element-colour check: {len(problems)} problem(s)")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
