#!/usr/bin/env python3
"""Author the Milestone 2 VOYAGE CLASSES asset (Assets/Resources/Voyages/VoyageClasses.asset).

The seven voyage classes (Guardian, Protector, Destroyer, Cultivator, Hunter, Shepherd, Pathfinder; Shombith,
2026-11-24: "class names ok"), the vessels each holds, and the classes each kind of voyage wants - the table the
Frogma rounds draw (Docs/MenuRedesignB/rounds/r135_voyages_classes.py FLEET and EFF, its "Best" column), which the
Vessels section (VoyageVesselsPage, Rev 136 E / Rev 138) and the Ark seats' class tags read. A class is a label: it
never changes a vessel's kit. The section's words are the SO's own field defaults and are left for a person to edit;
this script writes only the tables, so a person's word edits survive a re-run.

    python3 Tools/Build/author_voyage_classes.py            write the asset
    python3 Tools/Build/author_voyage_classes.py --check    report drift, write nothing (exit 1 on drift)
    python3 Tools/Build/author_voyage_classes.py --self-test the checks fire on planted defects
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSET = ROOT / "Assets/Resources/Voyages/VoyageClasses.asset"
SCRIPT_META = ROOT / "Assets/_Scripts/ScriptableObjects/Voyages/VoyageClassesSO.cs.meta"
ENUM = ROOT / "Assets/_Scripts/Data/Enums/VesselClassType.cs"
ASSET_GUID = "5b0f3c7e2a9d4e6f8c1a2b3d4e5f6a7b"

# VoyageClass values (Assets/_Scripts/Data/Voyages/VoyageClass.cs).
CLASS = {"Guardian": 1, "Protector": 2, "Destroyer": 3, "Cultivator": 4, "Hunter": 5, "Shepherd": 6, "Pathfinder": 7}

# The class, what it is for (Rev 135 ROLE), and its vessels in floor order (Rev 135 FLEET, as VesselClassType names).
FLEET = [
    ("Guardian", "Holds a place: walls and shells around the Ark.", ["Serpent", "Grizzly", "Shrike"]),
    ("Protector", "Takes the hits meant for the Ark.", ["Scarab", "Orca", "Thresher"]),
    ("Destroyer", "Breaks mass, hulls and whatever is in the way.", ["Sparrow", "Dolphin", "Swordfish", "Rhino"]),
    ("Cultivator", "Grows and gathers: crystal, mass, a living wall.", ["Squirrel", "Urchin", "TigerMoth", "Goby"]),
    ("Hunter", "Finds one target and runs it down.", ["Manta", "BrittleStar", "Honeyguide"]),
    ("Shepherd", "Moves fauna and crowds where it wants them.", ["Angler", "Jacks", "Starling", "JewelWasp"]),
    ("Pathfinder", "Fast, reads the sea, gets there first.", ["Meerkat", "Stoat", "Wrasse"]),
]

# The classes each kind of voyage wants (the giver's ask in the Voyage Codex; Rev 135 EFF's 3s), by job template id.
# The Crystal Run (job.raid.crystals) is left out on purpose: it stays as Milestone 1 built it (Shombith, 2026-11-24).
WANTS = [
    ("job.salvage.drift", ["Cultivator", "Pathfinder", "Protector"]),
    ("escort", ["Protector", "Guardian", "Destroyer"]),
    ("job.bounty.crew", ["Hunter", "Pathfinder", "Destroyer"]),
    ("job.defence.aid", ["Guardian", "Cultivator", "Protector"]),
]


def vessel_values():
    text = ENUM.read_text()
    return {m.group(1): int(m.group(2)) for m in re.finditer(r"^\s*(\w+)\s*=\s*(-?\d+)\s*,", text, re.M)}


def validate(fleet, wants, values):
    errors = []
    seen = {}
    for name, line, vessels in fleet:
        if name not in CLASS:
            errors.append(f"unknown class {name}")
        if not line:
            errors.append(f"{name} has no line")
        for v in vessels:
            if v not in values:
                errors.append(f"{name}: {v} is not a VesselClassType")
            if v in seen:
                errors.append(f"{v} is in both {seen[v]} and {name}")
            seen[v] = name
    if [n for n, _, _ in fleet] != list(CLASS):
        errors.append("classes are not the seven in VoyageClass order")
    for job, wanted in wants:
        if len(wanted) != 3 or len(set(wanted)) != 3:
            errors.append(f"{job} must want three different classes")
        for c in wanted:
            if c not in CLASS:
                errors.append(f"{job}: unknown class {c}")
        if job == "job.raid.crystals":
            errors.append("the Crystal Run must stay unchanged: it wants no class")
    return errors


def yaml_str(text):
    """A string as Unity writes it: single-quoted when plain YAML would misread it (a ': ' inside, as two lines here)."""
    return "'" + text.replace("'", "''") + "'" if (": " in text or text[:1] in "'\"&*!|>%@`#{[") else text


def script_guid():
    m = re.search(r"guid: (\w+)", SCRIPT_META.read_text())
    return m.group(1)


def words_block(existing):
    """The section's words as they stand in the asset (a person's edits), else nothing (the SO's defaults apply)."""
    if not existing:
        return ""
    m = re.search(r"^  seatLine:.*\Z", existing, re.M | re.S)
    return m.group(0) if m else ""


def render(values, existing=None):
    out = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {script_guid()}, type: 3}}",
        "  m_Name: VoyageClasses",
        "  m_EditorClassIdentifier:",
        "  classes:",
    ]
    for name, line, vessels in FLEET:
        out += [f"  - Class: {CLASS[name]}", f"    Name: {name}", f"    Line: {yaml_str(line)}", "    Vessels:"]
        out += [f"    - {values[v]}" for v in vessels]
    out.append("  wants:")
    for job, wanted in WANTS:
        out += [f"  - Job: {job}", "    Wanted:"]
        out += [f"    - {CLASS[c]}" for c in wanted]
    words = words_block(existing)
    text = "\n".join(out) + "\n"
    return text + (words if words.endswith("\n") or not words else words + "\n")


META = f"""fileFormatVersion: 2
guid: {ASSET_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def self_test(values):
    bad = [("Guardian", "x", ["Serpent"]), ("Protector", "x", ["Serpent"])] + FLEET[2:]
    assert any("in both" in e for e in validate(bad, WANTS, values)), "a vessel in two classes must fail"
    assert any("not a VesselClassType" in e for e in validate([("Guardian", "x", ["Nope"])] + FLEET[1:], WANTS, values))
    assert any("Crystal Run" in e for e in validate(FLEET, WANTS + [("job.raid.crystals", ["Hunter", "Guardian", "Shepherd"])], values))
    assert not validate(FLEET, WANTS, values), validate(FLEET, WANTS, values)
    kept = render(values, render(values) + "  seatLine: edited\n")
    assert kept.endswith("  seatLine: edited\n"), "a person's word edits must survive a re-run"
    assert "Line: 'Holds a place: walls" in render(values), "a line with ': ' must be quoted"
    print("self-test ok")


def main():
    values = vessel_values()
    if "--self-test" in sys.argv:
        self_test(values)
        return 0
    errors = validate(FLEET, WANTS, values)
    if errors:
        print("\n".join("FAIL " + e for e in errors))
        return 1
    existing = ASSET.read_text() if ASSET.exists() else None
    text = render(values, existing)
    meta_path = ASSET.with_suffix(".asset.meta")
    if "--check" in sys.argv:
        drift = existing != text or not meta_path.exists() or meta_path.read_text() != META
        print("drift: re-run author_voyage_classes.py" if drift else "VoyageClasses.asset up to date")
        return 1 if drift else 0
    ASSET.write_text(text)
    meta_path.write_text(META)
    print(f"wrote {ASSET.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
