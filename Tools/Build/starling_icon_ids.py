"""The Starlings' ability-row icon ids, shared by author_starling_icons.py (which draws them) and author_starling_assets.py
(which wires them). Kept dependency-free so the asset generator's --check runs where numpy / Pillow are not installed."""
import hashlib

# One icon per element of the four-icon row (charge, mass, space, time): the button on it picks that element's card.
ICONS = ("Starling_Charge.png", "Starling_Mass.png", "Starling_Space.png", "Starling_Time.png")

# The objective card's glyphs drawn here (Elemental crystals, Opposing vessels, Opposing mass), and the game's own omnicrystal
# icon for the Omnicrystal phase (Assets/_Graphics/ElementIcons/OmniCrystal_Active.png, the omni card's emblem).
OBJECTIVE_ICONS = ("Starling_ObjCrystals.png", "Starling_ObjVessels.png", "Starling_ObjMass.png")
OMNI_SPRITE_GUID = "4206d9fa3d98d9c4cbe20d38fddaadce"


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/StarlingIcons/{name}".encode()).hexdigest()
