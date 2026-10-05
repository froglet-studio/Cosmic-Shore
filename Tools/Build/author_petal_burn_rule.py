#!/usr/bin/env python3
"""Author the PETAL-BURN SWITCH's half of the danger-prism effect asset.   (Docs/ELEMENTAL_ECONOMY.md §4.1)

    python3 Tools/Build/author_petal_burn_rule.py            # write
    python3 Tools/Build/author_petal_burn_rule.py --check    # FAIL on any drift (reads the disk)

VesselElementalDebuffByDangerPrismEffect.asset carries two sizes for one danger contact:
debuffMagnitude (SHIPPED, -0.5 = five petals per element - this script never touches it, it is
Garrett's number) and tunedDebuffMagnitude (TUNED, -0.1 = one petal - the Living Ecology lab's
recommendation). Which one a contact uses is decided per CELL by CellConfigDataSO.PetalBurnRule,
read through the cellData reference this script wires to Runtime Cell Data. The Swarm cell's
PetalBurnRule is authored by author_swarm_fauna.py (PETAL_BURN_RULE); every other cell is silent
and so plays Shipped. check_elemental_economy.py §5 is the gate over the whole arrangement.
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EFFECT_ASSET = os.path.join(ROOT, "Assets/_SO_Assets/Effects/Vessel Prism Effects/"
                                  "VesselElementalDebuffByDangerPrismEffect.asset")
RUNTIME_CELL_DATA = "8d4e8398eedc76c4dadb8604f89b9e1b"   # Assets/_SO_Assets/Cell Data/Runtime Cell Data.asset
TUNED_MAGNITUDE = "-0.1"                                 # one petal per element per contact


def authored(text):
    """The asset with the switch's two lines in place, every other line untouched."""
    text = re.sub(r"^  tunedDebuffMagnitude: .*\n", "", text, flags=re.M)
    text = re.sub(r"^  cellData: .*\n", "", text, flags=re.M)
    text, n = re.subn(r"^(  debuffMagnitude: .*\n)", r"\1  tunedDebuffMagnitude: %s\n" % TUNED_MAGNITUDE,
                      text, count=1, flags=re.M)
    if n != 1:
        raise SystemExit("effect asset: no debuffMagnitude line to anchor on")
    text, n = re.subn(r"^(  cooldown: .*\n)",
                      r"\1  cellData: {fileID: 11400000, guid: %s, type: 2}\n" % RUNTIME_CELL_DATA,
                      text, count=1, flags=re.M)
    if n != 1:
        raise SystemExit("effect asset: no cooldown line to anchor on")
    return text


def main():
    check = "--check" in sys.argv
    with open(EFFECT_ASSET, encoding="utf-8") as fh:
        cur = fh.read()
    want = authored(cur)
    shipped = re.search(r"^  debuffMagnitude: (.*)$", cur, re.M).group(1)
    print(f"  danger-prism burn: shipped {shipped} (untouched), tuned {TUNED_MAGNITUDE}, cellData -> Runtime Cell Data")
    if check:
        if cur != want:
            print("FAIL\n  - differs from what this script authors: " + os.path.relpath(EFFECT_ASSET, ROOT))
            return 1
        print("OK - the danger-prism effect asset carries the petal-burn switch.")
        return 0
    if cur != want:
        with open(EFFECT_ASSET, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(want)
        print("  wrote " + os.path.relpath(EFFECT_ASSET, ROOT))
    else:
        print("  already authored")
    return 0


if __name__ == "__main__":
    sys.exit(main())
