#!/usr/bin/env python3
"""
Author the colour-set catalog the Profile's Colour sets tab picks from, and wire it into the theme container
(Docs/MENU_REDESIGN_B.md section 123; Rev 51 A, picked by Shombith 2026-11-05).

    Assets/_SO_Assets/Color Palettes/ColorSetCatalog.asset    ColorSetCatalogSO: one entry per set (id, palette,
                                                               the name and line the Profile shows, unlocked)
    Assets/_SO_Assets/ThemeManagerDataContainer.asset         Catalog -> that asset

The catalog's WORDS are authored in the asset (a person edits them in the inspector), so this script writes the asset
only when it does not exist yet and otherwise STANDS DOWN on its words. What it always validates is the structure the
game depends on: each id is present once, names an existing SO_ColorSet by guid, and the container points at the
catalog. Every set is unlocked for now (Shombith, 2026-11-05: quests are not planned yet).

    python3 Tools/Build/author_colour_set_catalog.py              # write (first run) / validate
    python3 Tools/Build/author_colour_set_catalog.py --check      # fail on a missing or broken catalog or wiring
    python3 Tools/Build/author_colour_set_catalog.py --self-test  # planted defects must all be caught
"""
import argparse
import hashlib
import json
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PALETTES = "Assets/_SO_Assets/Color Palettes"
CATALOG = f"{PALETTES}/ColorSetCatalog.asset"
CONTAINER = "Assets/_SO_Assets/ThemeManagerDataContainer.asset"
SCRIPT = "Assets/_Scripts/ScriptableObjects/ColorSetCatalogSO.cs"
CATALOG_GUID = hashlib.md5(b"CosmicShore/ColorSetCatalog").hexdigest()

# (id, palette asset, the name the Profile shows, its line, the vision it is made for). The words seed the asset once;
# edit them in Unity. VISION is ColourVision (None 0, Protanopia 1, Deuteranopia 2, Tritanopia 3, Achromatopsia 4,
# LowVision 5, Contrast 6). Every set, a vision set included, is picked in the Profile's colour-set rail; Settings >
# General > Colour sets is a breadcrumb that opens the rail on the first set made for a vision.
# PIGMENTS: Jade, Ruby, Gold, Blue, rim over face, from colour_sets.json (Photic has no pigments there: named by eye).
SETS = (
    ("nocturne", "NocturneColorSetSO", "Nocturne", "Every prism a lantern: a glowing rim over a deep jewel face.", 0),
    ("photic", "PhoticColorSetSO", "Photic", "The original sunlit sea: one bright hue per domain.", 0),
    ("dartfrog", "DartFrogColorSetSO", "Dart Frog", "Frog warning colours over their dark grounds: mint over bronze, bumblebee over coal.", 0),
    ("nacre", "NacreColorSetSO", "Nacre", "Light, not bright: soft bodies in each family's own colour under a sheen.", 0),
    # The /colour-set skill's worked examples, taken into the game 2026-11-11 (Garrett: "the artifact lab had some
    # great new color sets").
    ("blueringedoctopus", "BlueRingedOctopusColorSetSO", "Blue-ringed Octopus",
     "Sand skin, electric rings, black outlines: an octopus flashing its warning.", 0),
    ("neonnoir", "NeonNoirColorSetSO", "Neon Noir",
     "Rain and neon: every rim a neon tube, every face a wet street reflecting the sign across from it.", 0),
    ("ukiyoe", "UkiyoEColorSetSO", "Ukiyo-e", "Woodblock prints: every face its rim's own pigment, printed deeper.", 0),
    ("dusk", "DuskColorSetSO", "Dusk", "The hush after sunset: muted rims over dusky faces, plum the clearest colour.", 0),
    ("contrast", "ContrastColorSetSO", "Contrast",
     "Every domain as far from every other as the rules allow, for anyone who wants the board to read at a glance.", 6),
    ("protanopia", "ProtanopiaColorSetSO", "Protanopia",
     "Made for eyes without red cones (protanopia and protanomaly): the domains told apart on blue-yellow and light.", 1),
    ("deuteranopia", "DeuteranopiaColorSetSO", "Deuteranopia",
     "Made for eyes without green cones (deuteranopia and deuteranomaly): the domains told apart on blue-yellow and light.", 2),
    ("tritanopia", "TritanopiaColorSetSO", "Tritanopia",
     "Made for eyes without blue cones (tritanopia): the domains told apart on red-teal and light.", 3),
    ("achromatopsia", "AchromatopsiaColorSetSO", "Achromatopsia",
     "Made for eyes that see no colour: every domain has its own brightness, rim and face.", 4),
    ("lowvision", "LowVisionColorSetSO", "Low Vision",
     "Made for low vision and cataract: bright rims over dark faces, the domains apart in brightness first.", 5),
)
VISION = {s[0]: s[4] for s in SETS}
JSON_KEY = {"dartfrog": "dart_frog", "lowvision": "low_vision", "blueringedoctopus": "blue_ringed_octopus",
            "neonnoir": "neon_noir", "ukiyoe": "ukiyo_e"}   # catalog id -> colour_sets.json key, where they differ

PIGMENTS = {
    "nocturne": ("Lagoon over Plum", "Orchid over Deep Teal", "Honey over Violet", "Cornflower over Spruce"),
    "photic": ("Cyan over Deep cyan", "Violet over Deep violet", "Amber over Umber", "Periwinkle over Indigo"),
    "dartfrog": ("Mint over Bronze", "Orchid over Petrol", "Bumblebee over Coal", "Azure over Cobalt"),
}


def json_pigments():
    """Jade, Ruby, Gold, Blue as 'rim over face' for every catalog set colour_sets.json defines (the vision sets)."""
    with open(os.path.join(REPO, "Docs", "VesselModels", "colour_sets.json"), encoding="utf-8") as fh:
        defs = json.load(fh)["sets"]
    out = {}
    for sid, *_ in SETS:
        s = defs.get(JSON_KEY.get(sid, sid))
        if sid not in PIGMENTS and s:
            out[sid] = tuple("%s over %s" % (s["domains"][d]["rim"]["name"], s["domains"][d]["face"]["name"])
                             for d in ("jade", "ruby", "gold", "blue"))
    return out


PIGMENTS.update(json_pigments())


def pigment_lines(sid):
    return "    pigments:\n" + "".join(f"    - {yaml_str(p)}\n" for p in PIGMENTS[sid])


def entry_text(sid, guid, name, line):
    return (f"  - id: {sid}\n    colorSet: {ref(guid)}\n    displayName: {yaml_str(name)}\n"
            f"    line: {yaml_str(line)}\n    unlocked: 1\n" + pigment_lines(sid) + f"    vision: {VISION[sid]}\n")


def add_pigments(text):
    """Migration (section 126): an entry without pigments gets them after its `unlocked` line, and (2026-11-05) an
    entry without a vision gets its own at its end. Stands down per entry once present, so a person's edits in the
    inspector are kept."""
    out, blocks = [], re.split(r"(?=^  - id: )", text, flags=re.M)
    for b in blocks:
        m = re.match(r"  - id: (\S+)\n", b)
        if m and m.group(1) in PIGMENTS and "    pigments:" not in b:
            b = re.sub(r"(    unlocked: \d+\n)", lambda x: x.group(1) + pigment_lines(m.group(1)), b, count=1)
        if m and m.group(1) in VISION and "    vision:" not in b:
            b = b.rstrip("\n") + "\n" + f"    vision: {VISION[m.group(1)]}\n"
        out.append(b)
    return "".join(out)


def add_entries(text, palette_guids):
    """Migration (2026-11-05): a set in SETS the catalog does not have yet is added at its place in SETS order (before
    the next SETS entry the catalog has, else at the end; 2026-11-08, so Nacre sits beside the other looks and not
    after the vision sets). Stands down once every set is present."""
    ids = set(re.findall(r"^  - id: (\S+)$", text, re.M))
    if all(s[0] in ids for s in SETS):
        return text
    text = text.rstrip("\n") + "\n"
    for i, (sid, _, name, line, _) in enumerate(SETS):
        if sid in ids:
            continue
        entry = entry_text(sid, palette_guids[sid], name, line)
        after = next((s[0] for s in SETS[i + 1:] if s[0] in ids), None)
        m = re.search(r"^  - id: %s$" % re.escape(after), text, re.M) if after else None
        text = text[:m.start()] + entry + text[m.start():] if m else text + entry
        ids.add(sid)
    return text


def read(path):
    with open(os.path.join(REPO, path), encoding="utf-8", newline="") as f:
        return f.read()


def write(path, text):
    with open(os.path.join(REPO, path), "w", encoding="utf-8", newline="") as f:
        f.write(text)


def meta_guid(path):
    m = re.search(r"^guid: ([0-9a-f]{32})", read(path + ".meta"), re.M)
    return m.group(1) if m else None


def ref(guid):
    return f"{{fileID: 11400000, guid: {guid}, type: 2}}"


def yaml_str(s):
    return "'" + s.replace("'", "''") + "'" if re.search(r"[:#{}\[\],&*!|>'\"%@`]", s) else s


def catalog_text(script_guid, palette_guids):
    entries = "".join(entry_text(sid, palette_guids[sid], name, line) for sid, _, name, line, _ in SETS)
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n  m_Name: ColorSetCatalog\n"
            "  m_EditorClassIdentifier: \n  entries:\n" + entries)


def catalog_meta():
    return (f"fileFormatVersion: 2\nguid: {CATALOG_GUID}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def wire_container(text):
    """The container with Catalog -> the catalog, inserted right after its ColorSet line (idempotent)."""
    line = f"  Catalog: {ref(CATALOG_GUID)}"
    nl = "\r\n" if "\r\n" in text else "\n"
    if re.search(r"^  Catalog: .*$", text, re.M):
        return re.sub(r"^  Catalog: .*$", line, text, count=1, flags=re.M)
    m = re.search(r"^  ColorSet: .*$", text, re.M)
    if not m:
        raise SystemExit(f"{CONTAINER}: no ColorSet line to wire the catalog beside")
    return text[:m.end()] + nl + line + text[m.end():]


def validate(catalog, container, palette_guids, script_guid):
    errors = []
    if f"guid: {script_guid}, type: 3" not in catalog:
        errors.append(f"{CATALOG}: m_Script is not ColorSetCatalogSO ({script_guid})")
    ids = re.findall(r"^  - id: (\S+)$", catalog, re.M)
    for sid, asset, _, _, vision in SETS:
        if ids.count(sid) != 1:
            errors.append(f"{CATALOG}: id '{sid}' appears {ids.count(sid)} times (want 1)")
            continue
        block = catalog.split(f"  - id: {sid}\n", 1)[1].split("  - id:", 1)[0]
        if len(re.findall(r"^    - ", block, re.M)) != 4:
            errors.append(f"{CATALOG}: '{sid}' needs four pigment lines (Jade, Ruby, Gold, Blue)")
        m = re.search(r"colorSet: \{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}", block)
        if not m or m.group(1) != palette_guids[sid]:
            errors.append(f"{CATALOG}: '{sid}' does not point at {asset}.asset ({palette_guids[sid]})")
        v = re.search(r"^    vision: (\d+)$", block, re.M)
        if not v or int(v.group(1)) != vision:
            errors.append(f"{CATALOG}: '{sid}' must be made for vision {vision} (ColourVision)")
    visions = [int(v) for v in re.findall(r"^    vision: (\d+)$", catalog, re.M) if int(v)]
    for v in sorted(set(visions)):
        if visions.count(v) > 1:
            errors.append(f"{CATALOG}: {visions.count(v)} sets are made for vision {v}; one set per vision")
    if f"  Catalog: {ref(CATALOG_GUID)}" not in container:
        errors.append(f"{CONTAINER}: Catalog is not wired to {CATALOG} ({CATALOG_GUID})")
    return errors


def inputs():
    script_guid = meta_guid(SCRIPT)
    palette_guids = {sid: meta_guid(f"{PALETTES}/{asset}.asset") for sid, asset, *_ in SETS}
    missing = [sid for sid, g in palette_guids.items() if not g]
    if not script_guid or missing:
        raise SystemExit(f"missing guid: script={script_guid} palettes={missing}")
    return script_guid, palette_guids


def self_test():
    script_guid, guids = inputs()
    good_cat = catalog_text(script_guid, guids)
    good_con = wire_container("MonoBehaviour:\n  BaseMaterialSet: {fileID: 0}\n  ColorSet: {fileID: 0}\n")
    assert not validate(good_cat, good_con, guids, script_guid), validate(good_cat, good_con, guids, script_guid)
    assert wire_container(good_con) == good_con, "wiring is not idempotent"
    planted = {
        "a set pointing at another palette": (good_cat.replace(guids["dartfrog"], guids["photic"], 1), good_con),
        "a duplicated id": (good_cat.replace("  - id: photic", "  - id: nocturne"), good_con),
        "the wrong script": (good_cat.replace(script_guid, "0" * 32), good_con),
        "an unwired container": (good_cat, "MonoBehaviour:\n  ColorSet: {fileID: 0}\n"),
        "a set missing a pigment": (good_cat.replace("    - Mint over Bronze\n", "", 1), good_con),
        "a vision set made for the wrong vision": (good_cat.replace("    vision: 1\n", "    vision: 2\n", 1), good_con),
        "two sets made for one vision": (good_cat.replace("    vision: 3\n", "    vision: 2\n", 1), good_con),
    }
    old_shape = re.sub(r"^    vision: \d+\n", "", good_cat.split("  - id: contrast", 1)[0], flags=re.M)
    old_shape = re.sub(r"^  - id: nacre\n(?:    .*\n)+", "", old_shape, flags=re.M)   # a set added mid-catalog
    assert add_entries(add_pigments(old_shape), guids) == good_cat, "the migration does not reach the authored catalog"
    for name, (cat, con) in planted.items():
        assert validate(cat, con, guids, script_guid), f"self-test: '{name}' was not caught"
    print(f"author_colour_set_catalog --self-test: ok ({len(planted)} planted defects caught)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        self_test()
        return
    script_guid, guids = inputs()
    exists = os.path.exists(os.path.join(REPO, CATALOG))
    if not a.check:
        if not exists:
            write(CATALOG, catalog_text(script_guid, guids))
            write(CATALOG + ".meta", catalog_meta())
            print(f"wrote {CATALOG}")
        else:
            cat = read(CATALOG)
            migrated = add_entries(add_pigments(cat), guids)
            if migrated != cat:
                write(CATALOG, migrated)
                print(f"added pigments, visions or sets to {CATALOG}")
            else:
                print(f"{CATALOG} exists: its words stay as authored (validating only)")
        con = read(CONTAINER)
        wired = wire_container(con)
        if wired != con:
            write(CONTAINER, wired)
            print(f"wired {CONTAINER} Catalog")
    if not os.path.exists(os.path.join(REPO, CATALOG)):
        sys.exit(f"author_colour_set_catalog --check: {CATALOG} is missing; run without --check")
    if meta_guid(CATALOG) != CATALOG_GUID:
        sys.exit(f"author_colour_set_catalog --check: {CATALOG}.meta guid is not {CATALOG_GUID}")
    errors = validate(read(CATALOG), read(CONTAINER), guids, script_guid)
    if errors:
        sys.exit("author_colour_set_catalog: " + "; ".join(errors))
    print(f"author_colour_set_catalog{' --check' if a.check else ''}: ok ({len(SETS)} sets, container wired)")


if __name__ == "__main__":
    main()
