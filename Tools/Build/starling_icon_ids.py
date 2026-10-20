"""The Starlings' ability-row icon ids, shared by author_starling_icons.py (which draws them) and author_starling_assets.py
(which wires them). Kept dependency-free so the asset generator's --check runs where numpy / Pillow are not installed."""
import hashlib

# One icon per element of the four-icon row (charge, mass, space, time): the button on it picks that element's card.
ICONS = ("Starling_Charge.png", "Starling_Mass.png", "Starling_Space.png", "Starling_Time.png")


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/StarlingIcons/{name}".encode()).hexdigest()
