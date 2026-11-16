#!/usr/bin/env python3
"""
Authors every serialized asset Spearline needs (GameModes.Spearline, the id READ from GameModes.cs; reserved 96 with
hint toast 171 in Docs/ElementalLoop/GAME_KIT_ROSTER.md). The shared work is hull_time_race.author; this file is the
Swordfish's spec.

Run from the repo root:  python3 Tools/Build/author_spearline_assets.py [--check]

WHAT THIS MODE IS. The Swordfish's TIME card (Garrett, 2026-11-15: "make all the missing games you can"). The
Swordfish's Time ability is Quickdraw, the sword's RATE (swipe recovery 0.35 -> 0.14 s, energize hold 1 -> 0.4 s,
x1 -> x2.5 across Time 0-10). The race is Headlong's circuit (HeadlongCircuit, cut for this airframe's 840 u/s ramp and
its turn), three laps, and the rings carry SEALS: panes of prisms in the mouth (SpearlineSeals). A sealed ring counts
only through a hole, and the hull alone rams a lane narrower than the hole, so each seal asks for a swing of the bill
on the way through; the faster the sword swings, the less speed a seal costs. I1 seals every other ring and one stroke
opens it, I2 seals every ring and wants a stroke to each side, I3 shields every second seal (two strokes in one place),
I4 shields them all and wants the bill stood up too. Doc: SPEARLINE.md; tests: SpearlineSealsTests.

THE DONOR IS THORNLINE'S SCENE (barren race cell, equatorial spawn ring, a plain GateRaceController block). The clone
swaps the controller script, the scoring rule asset, the AI numbers, the detection clamp and the Netcode ids, and adds
the seal prefab; the card's Vessels list clamps every seat to the Swordfish. Nothing about the vessel changes.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/Spearline"
RESERVED_ID, HINT_TOAST = 96, 171
FIELDS = """  rule: {{fileID: 11400000, guid: {rule}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: {commit}
  aiApproachLead: {lead}
  aiThroughDistance: {through}
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: {speed}
  reportResyncSeconds: 3
"""
SPAWNABLE_PRISM_PATH = "Assets/_Prefabs/Trails/SpawnablePrism.prefab"
SPAWNABLE_PRISM = lib.existing_guid(SPAWNABLE_PRISM_PATH)
THORN_FIELDS = f"""  thornPrefab: {{fileID: 4563009547826722997, guid: {SPAWNABLE_PRISM}, type: 3}}
  thornRegrowSeconds: 3
  crystalRegrowSeconds: 5
  aiTakesSnipes: 1
"""
SEAL_FIELDS = f"""  laps: 3
  sealPrefab: {{fileID: 4563009547826722997, guid: {SPAWNABLE_PRISM}, type: 3}}
  sealRegrowPerFrame: 40
  sealClearance: 60
"""
LAPS = 3
GATES_PER_LAP = 8   # HeadlongCircuitSettings.ForIntensity's GateCount


def _errors():
    errs = []
    root = lib.ROOT
    enum = open(os.path.join(root, "Assets/_Scripts/Data/Enums/GameModes.cs"), encoding="utf-8").read()
    if not re.search(rf"^\s*Spearline\s*=\s*{RESERVED_ID}\s*,", enum, re.M):
        errs.append(f"GameModes.Spearline is not the roster's reserved {RESERVED_ID}")
    toasts = open(os.path.join(root, "Assets/_Scripts/Data/Enums/GameToastSituation.cs"), encoding="utf-8").read()
    if f"SpearlineHint = {HINT_TOAST}," not in toasts:
        errs.append(f"GameToastSituation.SpearlineHint is not the roster's reserved {HINT_TOAST}")
    src = open(os.path.join(root, f"{MODE_DIR}/SpearlineCourseSource.cs"), encoding="utf-8").read()
    if f"DefaultLaps = {LAPS};" not in src:
        errs.append(f"SpearlineCourseSource.DefaultLaps is not {LAPS}")
    circuit = open(os.path.join(root, "Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs"), encoding="utf-8").read()
    if f"GateCount = {GATES_PER_LAP}," not in circuit:
        errs.append(f"HeadlongCircuitSettings.ForIntensity no longer lays {GATES_PER_LAP} rings a lap")
    ctl = open(os.path.join(root, f"{MODE_DIR}/SpearlineController.cs"), encoding="utf-8").read()
    # The LOCKED mode rule: the mode lays seals and judges rings; it never touches the vessel's kit.
    for banned in ("PerformShipControllerActions", "InputStatus", "ElementalScaling", "IsUpgradeActive"):
        if banned in ctl:
            errs.append(f"SpearlineController mentions {banned} - a mode never changes the vessel")
    return errs


htr.author(dict(
    member="Spearline", display="Spearline", hull="Swordfish", class_id=lib.VESSEL_CLASS_ID["Swordfish"],
    scripts={
        "SpearlineController":   f"{MODE_DIR}/SpearlineController.cs",
        "SpearlineSeals":        f"{MODE_DIR}/SpearlineSeals.cs",
        "SpearlineCourseSource": f"{MODE_DIR}/SpearlineCourseSource.cs",
        "SpearlineSealsTests":   "Assets/_Scripts/Tests/Editor/SpearlineSealsTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/SPEARLINE.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThornline.unity",
    donor_controller_guid=lib.guid("script/ThornlineController"),
    donor_rule_guid=lib.guid("asset/ThornlineScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThornlineScoringRule"), commit=300, lead=340, through=240,
                               speed=4000) + THORN_FIELDS,
    # Headlong's steering numbers (the same circuit and airframe); the clamp covers the 840 u/s ramp twice over.
    new_fields=FIELDS.format(rule="__RULE__", commit=260, lead=300, through=220, speed=2000) + SEAL_FIELDS,
    max_speed=840,
    target=GATES_PER_LAP * LAPS, laps=LAPS, target_key="spearlineGateTarget", target_after="thornlineGateTarget",
    # A quarter-of-race deficit (6 rings) x 0.2 = 1.2 element levels; Time speeds the sword, so the buff lands on the
    # very thing the seals ask for.
    comeback=0.2,
    description="""Swordfish only. Three laps of the circuit, and the rings are sealed with
    panes of prisms. A sealed ring counts only through a hole - your hull alone punches too
    small a one, so swing the bill as you go through. The hard levels shield the seals and want
    more strokes. Time swings the sword faster. First team to put a pilot over the line wins.""",
    preview_objective="Swing the bill through each sealed ring",
    preview_notes="The circuit (HeadlongCircuit) and its seals are laid at match start from the rings, so a preview shows the shell. It teaches the seal - a ring that counts only through a hole you cut.",
    build_after="MinigameThornline",
    extra_toasts=[(HINT_TOAST, "Swing the bill as you go through - a sealed ring counts only through a hole you cut",
                   dict(domain_names=0))],
    extra_errors=_errors(),
))
