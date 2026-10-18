"""The Angler's icon and card-image ids, shared by author_angler_icons.py (which draws them) and
author_angler_assets.py (which wires them). Kept dependency-free so the asset generator's --check runs
where numpy / Pillow are not installed (CI's generator sweep)."""
import hashlib

CARD_ACTIVE, CARD_INACTIVE = "Angler.png", "Angler_Inactive.png"


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/AnglerIcons/{name}".encode()).hexdigest()
