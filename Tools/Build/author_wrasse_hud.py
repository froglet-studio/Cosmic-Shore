#!/usr/bin/env python3
"""Author the Wrasse's own HUD: WrasseHUDVariant.prefab (forked from the Sparrow's) and its swap onto Wrasse.prefab.

The Wrasse (a Sparrow clone) flew SparrowHUDVariant under its own WrasseHUDController until 2026-11-13.
`sparrow_hud_fork.fork` writes the variant (every fileID kept), with `WrasseHUDView` and the icon kit's cards
(Tools/Build/icon_kit/specs/wrasse.py), each level-5 card the binding's upgradedSprite. The counts stay the controller's.

    python3 Tools/Build/author_wrasse_hud.py           # write
    python3 Tools/Build/author_wrasse_hud.py --check   # exit 1 on drift
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sparrow_hud_fork import fork  # noqa: E402

CARDS = {
    1: ("Wrasse_KiteRam", "Wrasse_CrystalSteal"),            # Charge
    2: ("Wrasse_MassCluster", "Wrasse_ShieldedClusters"),    # Mass
    3: ("Wrasse_SpaceBlast", "Wrasse_ShieldBreaker"),        # Space
    4: ("Wrasse_SavedBoosts", "Wrasse_Tailwind"),            # Time
}

if __name__ == "__main__":
    sys.exit(fork("Wrasse", "Assets/_Scripts/UI/View/WrasseHUDView.cs", "Assets/_Graphics/Icons/AbilityIcons/Wrasse",
                  CARDS, "Assets/_Prefabs/Spacevessels/Wrasse.prefab"))
