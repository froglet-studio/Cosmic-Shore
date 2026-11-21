#!/usr/bin/env python3
"""Author the Orca's own HUD: OrcaHUDVariant.prefab (forked from the Sparrow's) and its swap onto Orca.prefab.

The Orca flew SparrowHUDVariant under its own OrcaHUDController until 2026-11-13. `sparrow_hud_fork.fork` writes the
variant (every fileID kept), with `OrcaHUDView` and the icon kit's cards (Tools/Build/icon_kit/specs/orca.py), each
level-5 card the binding's upgradedSprite.

    python3 Tools/Build/author_orca_hud.py           # write
    python3 Tools/Build/author_orca_hud.py --check   # exit 1 on drift
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sparrow_hud_fork import fork  # noqa: E402

CARDS = {
    1: ("Orca_FlownMissiles", "Orca_Rearm"),        # Charge
    2: ("Orca_DangerShield", "Orca_Bulwark"),       # Mass
    3: ("Orca_Burst", "Orca_ChainBurst"),           # Space
    4: ("Orca_BoostTank", "Orca_Afterburner"),      # Time
}

if __name__ == "__main__":
    sys.exit(fork("Orca", "Assets/_Scripts/UI/View/OrcaHUDView.cs", "Assets/_Graphics/Icons/AbilityIcons/Orca", CARDS,
                  "Assets/_Prefabs/Spacevessels/Orca.prefab"))
