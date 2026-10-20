"""The Krill's ability-row icon ids, shared by author_krill_icons.py (which draws them) and author_krill_assets.py
(which wires them). Kept dependency-free so the asset generator's --check runs where numpy / Pillow are not installed."""
import hashlib

# One icon per element of the four-icon row (charge, mass, space, time): the button on it picks that element's card.
ICONS = ("Krill_Charge.png", "Krill_Mass.png", "Krill_Space.png", "Krill_Time.png")


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/KrillIcons/{name}".encode()).hexdigest()
