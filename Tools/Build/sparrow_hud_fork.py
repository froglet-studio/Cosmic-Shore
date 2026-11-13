#!/usr/bin/env python3
"""Fork the Sparrow's (or the Rhino's) HUD prefab into a hull's own (shared by author_orca_hud.py, author_wrasse_hud.py,
author_jacks_hud.py, ...).

Several hulls were cloned from the Sparrow and still nest SparrowHUDVariant, so their cards drew the Sparrow's rocket
bay, stance hull and afterburner parts. `fork()` writes `<Hull>HUDVariant.prefab` from the Sparrow's prefab with
every fileID kept (so the hull prefab's nested instance and its overrides still resolve), renamed, its view swapped for
the hull's own `VesselHUDView` subclass, the four element icons given the icon kit's pictures at full alpha with each
level-5 card as the binding's `upgradedSprite`, and the Sparrow's part children switched off; then it moves the hull
prefab's nested instance onto the new variant. Deterministic guids; `--check` reports drift and writes nothing.

`RHINO` is the same fork from RhinoHUDVariant (the Jacks were a Rhino clone): its view's generated katana and reach blade
replaced the cards' own icons on every hull nesting it. Its gauges (Charge, Time) are kept, and its view's own fields are
dropped with the view.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HUD_DIR = os.path.join(ROOT, "Assets/_Prefabs/UI Elements/VesselHUD")
SPARROW = os.path.join(HUD_DIR, "SparrowHUDVariant.prefab")
SPARROW_GUID = "f0a24806a0485254389f97d4c93f7615"
SPARROW_VIEW_GUID = "297f560e4f6d4fec848862cfd2c41581"
VIEW_ID = "7983934951929746178"
# element -> the icon Image's fileID in the Sparrow's prefab (Charge, Mass, Space, Time)
ICON_IDS = {1: "3855639277037614910", 2: "6611959372928416812", 3: "4990196459091587280", 4: "3258612490553136050"}
SPARROW_PARTS = ("StanceRounds", "StanceHull", "AfterburnerHull", "AfterburnerJets")

SPARROW_SRC = dict(path=SPARROW, name="SparrowHUDVariant", guid=SPARROW_GUID, view_guid=SPARROW_VIEW_GUID,
                   view_id=VIEW_ID, icon_ids=ICON_IDS, parts=SPARROW_PARTS, keep_gauges=False)
RHINO = dict(path=os.path.join(HUD_DIR, "RhinoHUDVariant.prefab"), name="RhinoHUDVariant",
             guid="2656b477a4af7304180e65f2848a84b3", view_guid="d6e5753c294347a1966bc06a0e2e0424",
             view_id="5161366343835098938",
             icon_ids={1: "7720000000000003015", 2: "7720000000000003025", 3: "7720000000000003035", 4: "7720000000000003045"},
             parts=("SpoolPrism", "SpoolGlance", "SpoolWard", "SpoolHull"),   # the afterimage parts left with the sword (2026-11-13)
             keep_gauges=True)


def guid_for(hull, name):
    """The Orca's guids were minted as CosmicShore/OrcaHUD/<name>; every hull keeps that scheme."""
    return hashlib.md5(f"CosmicShore/{hull}HUD/{name}".encode()).hexdigest()


def sprite_ref(icons_dir, name):
    with open(os.path.join(ROOT, icons_dir, name + ".png.meta")) as f:
        guid = re.search(r"^guid: ([0-9a-f]{32})", f.read(), re.M).group(1)
    return f"{{fileID: 21300000, guid: {guid}, type: 3}}"


def _docs(text):
    parts = re.split(r"(?m)^(--- !u!\d+ &\d+.*)$", text)
    return parts[0], list(zip(parts[1::2], parts[2::2]))


def build_variant(hull, view_guid, icons_dir, cards, src=SPARROW_SRC):
    """cards: element (1..4) -> (base sprite name, level-5 sprite name)."""
    with open(src["path"]) as f:
        head, docs = _docs(f.read())
    parts, icon_ids = src["parts"], src["icon_ids"]
    off_gos = set()
    for h, b in docs:
        if h.startswith("--- !u!1 &") and re.search(r"^  m_Name: (%s)$" % "|".join(parts), b, re.M):
            off_gos.add(h.split("&")[1].split()[0])
    assert len(off_gos) == len(parts), f"{src['name']} parts not found: {off_gos}"
    icon_sprite = {icon_ids[el]: base for el, (base, _) in cards.items()}
    out = [head]
    for h, b in docs:
        fid = h.split("&")[1].split()[0]
        if fid == src["view_id"]:
            if src["keep_gauges"]:
                gauges = dict(re.findall(r"- element: (\d)\n    icon: \{fileID: \d+\}\n    upgradedSprite: [^\n]*\n"
                                         r"    gauge: (\{fileID: \d+\})", b))
                m = re.search(r"(?s)^(.*?  m_EditorClassIdentifier: *\n)(  highlights: \[\]\n)", b)
            else:
                gauges = {}
                m = re.search(r"(?s)^(.*?  m_EditorClassIdentifier: *\n)(  highlights:\n.*?)(  abilityIcons:)", b)
            assert m, "view block shape changed"
            b = m.group(1).replace(f"guid: {src['view_guid']}", f"guid: {view_guid}") + m.group(2) + "  abilityIcons:\n"
            for el in sorted(cards):
                b += (f"  - element: {el}\n    icon: {{fileID: {icon_ids[el]}}}\n"
                      f"    upgradedSprite: {sprite_ref(icons_dir, cards[el][1])}\n"
                      f"    gauge: {gauges.get(str(el), '{fileID: 0}')}\n")
            b += "  upgradeHighlightScale: 1.15\n  upgradePunchScale: 1.35\n  upgradePunchDuration: 0.35\n"
        elif fid in icon_sprite:
            # the source's card may be a sprite-less container (the Sparrow's) or carry its own hull's sprite (the Rhino's
            # plough cards since 2026-11-13): either way the fork's card is the hull's own picture at full alpha
            b, n1 = re.subn(r"(?m)^  m_Color: \{r: 1, g: 1, b: 1, a: [\d.]+\}$", "  m_Color: {r: 1, g: 1, b: 1, a: 1}", b)
            b, n2 = re.subn(r"(?m)^  m_Sprite: \{[^\n]*\}$", f"  m_Sprite: {sprite_ref(icons_dir, icon_sprite[fid])}", b)
            assert n1 == 1 and n2 == 1, f"icon {fid} shape changed"
        elif fid in off_gos:
            b = re.sub(r"(?m)^  m_IsActive: 1$", "  m_IsActive: 0", b)
        elif h.startswith("--- !u!1 &"):
            b = re.sub(r"(?m)^  m_Name: %s$" % src["name"], f"  m_Name: {hull}HUDVariant", b)
        out.append(h + b)
    return "".join(out)


def prefab_meta(guid):
    return (f"fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n"
            f"  assetBundleName: \n  assetBundleVariant: \n")


def script_meta(guid):
    return (f"fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n"
            f"  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n"
            f"  assetBundleName: \n  assetBundleVariant: \n")


BASE_CONTROLLER_GUID = "2134c837bdb00594d80fc211aa9d0c12"   # VesselHUDController
SPARROW_CONTROLLER_GUID = "e1938f2b097540e7abd1e4210d446545"


def base_controller(text):
    """A hull that still runs the Sparrow's HUD CONTROLLER (it reads the Sparrow's view and guns) gets the fleet's base
    VesselHUDController on the same component (same fileID, so every reference to it holds), keeping its baseView."""
    head, docs = _docs(text)
    out = [head]
    for h, b in docs:
        if f"guid: {SPARROW_CONTROLLER_GUID}" in b:
            b = b.replace(f"guid: {SPARROW_CONTROLLER_GUID}", f"guid: {BASE_CONTROLLER_GUID}")
            keep, drop = [], False
            for line in b.split("\n"):
                if re.match(r"^  (view|fireGunExecutor|barrelRollController|gunSprayAccuracy|stationaryModeChanged|"
                            r"onInputEventBlocked):", line):
                    drop = True
                elif re.match(r"^  \S", line) or line == "":
                    drop = False
                if not drop:
                    keep.append(line)
            b = "\n".join(keep)
        out.append(h + b)
    return "".join(out)


def swap_vessel(vessel_prefab, hull, hud_guid, src=SPARROW_SRC, view_guid=None):
    with open(os.path.join(ROOT, vessel_prefab)) as f:
        text = f.read()
    text = base_controller(text)
    text = text.replace(f"guid: {src['guid']}", f"guid: {hud_guid}")
    if view_guid:
        # a stripped stub of the nested view still names the SOURCE's view script; the fork's view is the hull's own
        text = text.replace(f"m_Script: {{fileID: 11500000, guid: {src['view_guid']}, type: 3}}",
                            f"m_Script: {{fileID: 11500000, guid: {view_guid}, type: 3}}")
    text = re.sub(r"(?m)^(      value: )%s$" % src["name"], rf"\g<1>{hull}HUDVariant", text)
    assert src["guid"] not in text
    return text


def fork(hull, view_cs, icons_dir, cards, vessel_prefab, argv=None, src=SPARROW_SRC):
    """Write (or with --check, compare) the hull's HUD variant, its metas and the vessel prefab swap. Returns an exit code."""
    argv = sys.argv if argv is None else argv
    check = "--check" in argv
    hud = os.path.join(HUD_DIR, f"{hull}HUDVariant.prefab")
    hud_guid, view_guid = guid_for(hull, f"{hull}HUDVariant.prefab"), guid_for(hull, os.path.basename(view_cs))
    want = {
        hud: build_variant(hull, view_guid, icons_dir, cards, src),
        hud + ".meta": prefab_meta(hud_guid),
        os.path.join(ROOT, view_cs) + ".meta": script_meta(view_guid),
        os.path.join(ROOT, vessel_prefab): swap_vessel(vessel_prefab, hull, hud_guid, src, view_guid),
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
    tool = f"author_{hull.lower()}_hud"
    if check:
        print(f"{tool}: " + ("ok" if not drift else "DRIFT " + ", ".join(drift)))
        return 1 if drift else 0
    print(f"{tool}: wrote " + (", ".join(drift) if drift else "nothing (up to date)"))
    return 0
