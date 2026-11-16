#!/usr/bin/env python3
"""Author the Jacks' own HUD: JacksHUDVariant.prefab (forked from the Rhino's) and its swap onto Jacks.prefab.

The Jacks were a Rhino clone and nested RhinoHUDVariant under their own JacksHUDController. After the Rhino's rework
(2026-11-12) that variant's view builds the Rhino's katana into the Charge card and its reach blade into the Space card,
hiding the card's own icon, so the Jacks' Danger Share and Wide cards drew the Rhino's sword. `sparrow_hud_fork.fork`
with `RHINO` writes the variant (every fileID kept, the Charge and Time gauges kept), with `JacksHUDView` and the icon
kit's cards (Tools/Build/icon_kit/specs/jacks.py), each level-5 card the binding's upgradedSprite.

    python3 Tools/Build/author_jacks_hud.py           # write
    python3 Tools/Build/author_jacks_hud.py --check   # exit 1 on drift
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sparrow_hud_fork import RHINO, fork  # noqa: E402

CARDS = {
    1: ("Jacks_DangerShare", "Jacks_Attack"),       # Charge
    2: ("Jacks_TheCall", "Jacks_MassClusters"),     # Mass
    3: ("Jacks_Wide", "Jacks_EatShields"),          # Space
    4: ("Jacks_Decoys", "Jacks_SecondDecoy"),       # Time
}

if __name__ == "__main__":
    sys.exit(fork("Jacks", "Assets/_Scripts/UI/View/JacksHUDView.cs", "Assets/_Graphics/Icons/AbilityIcons/Jacks", CARDS,
                  "Assets/_Prefabs/Spacevessels/Jacks.prefab", src=RHINO))
