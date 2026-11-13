#!/usr/bin/env python3
"""Author the Honeyguide's own HUD: HoneyguideHUDVariant.prefab (forked from the Sparrow's) and its swap onto
Honeyguide.prefab.

The Honeyguide (a Sparrow clone) flew SparrowHUDVariant under the Sparrow's own SparrowHUDController until 2026-11-13,
so its round-1 icons (specs/honeyguide.py) never showed. `sparrow_hud_fork.fork` writes the variant (every fileID kept)
with `HoneyguideHUDView` and the kit's cards, each level-5 card the binding's upgradedSprite, and turns the hull's
SparrowHUDController into the fleet's base VesselHUDController on the same component.

    python3 Tools/Build/author_honeyguide_hud.py           # write
    python3 Tools/Build/author_honeyguide_hud.py --check   # exit 1 on drift
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sparrow_hud_fork import fork  # noqa: E402

CARDS = {
    1: ("Honeyguide_Flit", "Honeyguide_Relentless"),       # Charge
    2: ("Honeyguide_Hive", "Honeyguide_HiveChain"),        # Mass
    3: ("Honeyguide_Peck", "Honeyguide_PiercingPecks"),    # Space
    4: ("Honeyguide_Slip", "Honeyguide_Slipstream"),       # Time
}

if __name__ == "__main__":
    sys.exit(fork("Honeyguide", "Assets/_Scripts/UI/View/HoneyguideHUDView.cs",
                  "Assets/_Graphics/Icons/AbilityIcons/Honeyguide", CARDS, "Assets/_Prefabs/Spacevessels/Honeyguide.prefab"))
