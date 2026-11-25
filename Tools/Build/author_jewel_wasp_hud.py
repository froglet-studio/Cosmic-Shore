#!/usr/bin/env python3
"""Author the Jewel Wasp's own HUD: JewelWaspHUDVariant.prefab (forked from the Tiger Moth's) and its swap onto
JewelWasp.prefab.

The wasp was cloned from the Tiger Moth and nested ButterflyHUDVariant, whose view drew the Moth's scale dust, drop and
mass keys over every card. `sparrow_hud_fork.fork` with `BUTTERFLY` writes the variant (every fileID kept, every Moth part
switched off, the wing-energy gauge dropped), with `JewelWaspHUDView` and the icon kit's cards
(Tools/Build/icon_kit/specs/jewelwasp.py), each level-5 card the binding's upgradedSprite, and the two cards the view
swaps in while the wasp rides a creature (Suction on Space, Dash on Time).

    python3 Tools/Build/author_jewel_wasp_hud.py           # write
    python3 Tools/Build/author_jewel_wasp_hud.py --check   # exit 1 on drift
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sparrow_hud_fork import BUTTERFLY, fork  # noqa: E402

CARDS = {
    1: ("JewelWasp_Attack", "JewelWasp_Burst"),      # Charge
    2: ("JewelWasp_Root", "JewelWasp_Overgrowth"),   # Mass
    3: ("JewelWasp_Sting", "JewelWasp_Maw"),         # Space
    4: ("JewelWasp_Swim", "JewelWasp_Restless"),     # Time
}
IN_HOST = {"spaceInHostIcon": "JewelWasp_Suction", "timeInHostIcon": "JewelWasp_Dash"}

if __name__ == "__main__":
    sys.exit(fork("JewelWasp", "Assets/_Scripts/UI/View/JewelWaspHUDView.cs", "Assets/_Graphics/Icons/AbilityIcons/JewelWasp",
                  CARDS, "Assets/_Prefabs/Spacevessels/JewelWasp.prefab", src=BUTTERFLY, view_fields=IN_HOST))
