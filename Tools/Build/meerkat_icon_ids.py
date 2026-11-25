"""The Meerkat's ability-row icon ids, shared by the icon kit (Tools/Build/icon_kit/specs/meerkat.py, which draws them) and
author_meerkat_assets.py (which wires them into MeerkatHUDVariant). Kept dependency-free so the asset generator's --check
runs where numpy / Pillow are not installed (CI's generator sweep)."""
import hashlib

HULL = "Meerkat"
# The LOCKED row order, charge -> mass -> space -> time (Garrett's kit, Resources/ElementalAbilityMaps/Meerkat.asset).
ICONS = ("Meerkat_SentinelMode", "Meerkat_ExitPortal", "Meerkat_Soliton", "Meerkat_FasterRide")
# The omni crystal card (VesselHUDView.omniAbilitySprite): the Soliton Network an omni crystal grows.
OMNI = "Meerkat_SolitonNetwork"
# Each card's level-5 picture (the binding's upgradedSprite, swapped in when the upgrade unlocks; roster audit 2026-11-13),
# in the same order: Six Sentinels, Danger Portal, Steal, Boost Out.
UPGRADED = ("Meerkat_SixSentinels", "Meerkat_DangerPortal", "Meerkat_Steal", "Meerkat_BoostOut")


def guid_for(name: str) -> str:
    """The icon kit's guid for this hull's sprite (icon_kit/author.py guid_for(HULL, name))."""
    return hashlib.md5(f"CosmicShore/IconKit/{HULL}/{name}".encode()).hexdigest()
