#!/usr/bin/env python3
"""Author the Orca's own HUD: OrcaHUDVariant.prefab (from the Sparrow's) and its swap onto Orca.prefab.

The Orca flew SparrowHUDVariant under its own OrcaHUDController until 2026-11-13, so its four cards drew the Sparrow's
rocket bay, stance hull and afterburner parts. This writes:

1. `OrcaHUDVariant.prefab`, the Sparrow's HUD prefab with every fileID kept (so Orca.prefab's nested instance and its
   overrides still resolve), renamed, its view swapped for `OrcaHUDView`, the four element icons given the kit's
   pictures (Tools/Build/icon_kit/specs/orca.py) at full alpha with each level-5 card as the binding's
   `upgradedSprite`, and the Sparrow's part children (StanceRounds, StanceHull, AfterburnerHull, AfterburnerJets)
   switched off.
2. `Orca.prefab`: the nested HUD instance's source guid and its name override move to the Orca's variant.

Idempotent and deterministic (fixed guids).

    python3 Tools/Build/author_orca_hud.py           # write
    python3 Tools/Build/author_orca_hud.py --check   # exit 1 on drift
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HUD_DIR = os.path.join(ROOT, "Assets/_Prefabs/UI Elements/VesselHUD")
SPARROW = os.path.join(HUD_DIR, "SparrowHUDVariant.prefab")
SPARROW_GUID = "f0a24806a0485254389f97d4c93f7615"
ORCA_HUD = os.path.join(HUD_DIR, "OrcaHUDVariant.prefab")
ORCA_PREFAB = os.path.join(ROOT, "Assets/_Prefabs/Spacevessels/Orca.prefab")
VIEW_CS = os.path.join(ROOT, "Assets/_Scripts/UI/View/OrcaHUDView.cs")
ICONS = os.path.join(ROOT, "Assets/_Graphics/Icons/AbilityIcons/Orca")


def guid_for(name):
    return hashlib.md5(f"CosmicShore/OrcaHUD/{name}".encode()).hexdigest()


ORCA_HUD_GUID = guid_for("OrcaHUDVariant.prefab")
VIEW_GUID = guid_for("OrcaHUDView.cs")
VIEW_ID = "7983934951929746178"
# element -> (icon Image fileID in the Sparrow's prefab, base card, level-5 card)
CARDS = {
    1: ("3855639277037614910", "Orca_FlownMissiles", "Orca_Rearm"),        # Charge
    2: ("6611959372928416812", "Orca_DangerShield", "Orca_Bulwark"),       # Mass
    3: ("4990196459091587280", "Orca_Burst", "Orca_ChainBurst"),           # Space
    4: ("3258612490553136050", "Orca_BoostTank", "Orca_Afterburner"),      # Time
}
SPARROW_PARTS = ("StanceRounds", "StanceHull", "AfterburnerHull", "AfterburnerJets")


def sprite_guid(name):
    with open(os.path.join(ICONS, name + ".png.meta")) as f:
        return re.search(r"^guid: ([0-9a-f]{32})", f.read(), re.M).group(1)


def sprite_ref(name):
    return f"{{fileID: 21300000, guid: {sprite_guid(name)}, type: 3}}"


def blocks(text):
    """[(header, body)] for every YAML document after the file header."""
    parts = re.split(r"(?m)^(--- !u!\d+ &\d+.*)$", text)
    return parts[0], list(zip(parts[1::2], parts[2::2]))


def build_variant():
    with open(SPARROW) as f:
        text = f.read()
    head, docs = blocks(text)
    off_gos = set()
    for h, b in docs:
        if h.startswith("--- !u!1 &") and re.search(r"^  m_Name: (%s)$" % "|".join(SPARROW_PARTS), b, re.M):
            off_gos.add(h.split("&")[1].split()[0])
    assert len(off_gos) == len(SPARROW_PARTS), f"Sparrow parts not found: {off_gos}"
    icon_ids = {v[0]: v[1] for v in CARDS.values()}
    out = [head]
    for h, b in docs:
        fid = h.split("&")[1].split()[0]
        if fid == VIEW_ID:
            m = re.search(r"(?s)^(.*?  m_EditorClassIdentifier: *\n)(  highlights:\n.*?)(  abilityIcons:)", b)
            assert m, "view block shape changed"
            b = m.group(1).replace("guid: 297f560e4f6d4fec848862cfd2c41581", f"guid: {VIEW_GUID}") + m.group(2)
            b += "  abilityIcons:\n"
            for el, (iid, base, up) in CARDS.items():
                b += (f"  - element: {el}\n    icon: {{fileID: {iid}}}\n    upgradedSprite: {sprite_ref(up)}\n"
                      f"    gauge: {{fileID: 0}}\n")
            b += "  upgradeHighlightScale: 1.15\n  upgradePunchScale: 1.35\n  upgradePunchDuration: 0.35\n"
        elif fid in icon_ids:
            b, n1 = re.subn(r"(?m)^  m_Color: \{r: 1, g: 1, b: 1, a: 0\}$", "  m_Color: {r: 1, g: 1, b: 1, a: 1}", b)
            b, n2 = re.subn(r"(?m)^  m_Sprite: \{fileID: 0\}$", f"  m_Sprite: {sprite_ref(icon_ids[fid])}", b)
            assert n1 == 1 and n2 == 1, f"icon {fid} shape changed"
        elif fid in off_gos:
            b = re.sub(r"(?m)^  m_IsActive: 1$", "  m_IsActive: 0", b)
        elif h.startswith("--- !u!1 &"):
            b = re.sub(r"(?m)^  m_Name: SparrowHUDVariant$", "  m_Name: OrcaHUDVariant", b)
        out.append(h + b)
    return "".join(out)


def meta(guid, kind="PrefabImporter"):
    return (f"fileFormatVersion: 2\nguid: {guid}\n{kind}:\n  externalObjects: {{}}\n  userData: \n"
            f"  assetBundleName: \n  assetBundleVariant: \n")


def script_meta(guid):
    return (f"fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n"
            f"  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n"
            f"  assetBundleName: \n  assetBundleVariant: \n")


def build_orca_prefab():
    with open(ORCA_PREFAB) as f:
        text = f.read()
    text = text.replace(f"guid: {SPARROW_GUID}", f"guid: {ORCA_HUD_GUID}")
    text = re.sub(r"(?m)^(      value: )SparrowHUDVariant$", r"\1OrcaHUDVariant", text)
    assert SPARROW_GUID not in text
    return text


def main():
    check = "--check" in sys.argv
    want = {
        ORCA_HUD: build_variant(),
        ORCA_HUD + ".meta": meta(ORCA_HUD_GUID),
        VIEW_CS + ".meta": script_meta(VIEW_GUID),
        ORCA_PREFAB: build_orca_prefab(),
    }
    drift = []
    for path, content in want.items():
        have = open(path).read() if os.path.exists(path) else None
        if have == content:
            continue
        drift.append(os.path.relpath(path, ROOT))
        if not check:
            with open(path, "w") as f:
                f.write(content)
    if check:
        print("author_orca_hud: " + ("ok" if not drift else "DRIFT " + ", ".join(drift)))
        return 1 if drift else 0
    print("author_orca_hud: wrote " + (", ".join(drift) if drift else "nothing (up to date)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
