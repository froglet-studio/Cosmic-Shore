#!/usr/bin/env python3
"""
Authors every serialized asset Spearline needs (GameModes.Spearline, the id READ from GameModes.cs; reserved 96 with
hint toast 171 in Docs/ElementalLoop/GAME_KIT_ROSTER.md). The shared work is hull_time_race.author; this file is the
Swordfish's spec.

Run from the repo root:  py -3 Tools/Build/author_spearline_assets.py [--check]

WHAT THIS MODE IS. The Swordfish's TIME card (Garrett, 2026-11-15: "make all the missing games you can"), upgraded in
place by the 2026-11-16 fun pass (SPEARLINE.md, Docs/Spearline/FUN_PASS.md). The race is Headlong's circuit
(HeadlongCircuit, cut for this airframe's 840 u/s ramp and its turn) with Spearline's own mouths (96 / 84 / 74 / 66),
five laps, and the gentlest rings of each lap carry SEALS: panes of prisms in the mouth (SpearlineSeals). A sealed ring
counts only through a HOLE of at least 8 prisms (the connected run of gone prisms the crossing sits in); the hull alone
punches 3-7, the hull plus one stroke of the bill INTO the pane 8-19. From I2 every seal is ARMOURED: a shielded sector
(120 / 180 / 240 degrees, its side a hash of the ring) that only cracks, so the pilot reads where the plain face is,
crosses it and swings into it. Each closed pass wears that pilot's need there by 2, so nobody is trapped. The AI reads
the seal the same way (SpearlineSeals.Read) and strokes through its shipped sword hand (SwordfishSwordAutopilot.Coach).
Tests: SpearlineSealsTests (Tools/Build/spearline_harness); timing: Tools/Build/spearline_race_model.py.

THE DONOR IS THORNLINE'S SCENE (barren race cell, equatorial spawn ring, a plain GateRaceController block). The clone
swaps the controller script, the scoring rule asset, the AI numbers, the detection clamp and the Netcode ids, and adds
the seal prefab; the card's Vessels list clamps every seat to the Swordfish. Nothing about the vessel changes. The
controller's fun-pass fields (the AI's read, the feedback, the three FMOD slots) are not written into the scene: they
ship at their C# defaults, the sound slots empty, as Redline's do.
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
LAPS = 5            # SpearlineCourseSource.DefaultLaps; 3 until the 2026-11-16 fun pass (50-70 s in the race model)
SEAL_FIELDS = f"""  laps: {LAPS}
  sealPrefab: {{fileID: 4563009547826722997, guid: {SPAWNABLE_PRISM}, type: 3}}
  sealRegrowPerFrame: 40
  sealClearance: 60
"""
GATES_PER_LAP = 8   # HeadlongCircuitSettings.ForIntensity's GateCount


# The fun pass's toasts (ids reserved 400-409 for it; 403-409 spare) and its sound slots.
FUN_TOASTS = (("SpearlineSealHeld", 400), ("SpearlineCleanStreak", 401), ("SpearlineArmourHint", 402))
SOUND_SLOTS = ("sealOpenedEvent", "sealHeldEvent", "cleanStreakEvent")


def _errors():
    errs = []
    root = lib.ROOT
    enum = open(os.path.join(root, "Assets/_Scripts/Data/Enums/GameModes.cs"), encoding="utf-8").read()
    if not re.search(rf"^\s*Spearline\s*=\s*{RESERVED_ID}\s*,", enum, re.M):
        errs.append(f"GameModes.Spearline is not the roster's reserved {RESERVED_ID}")
    toasts = open(os.path.join(root, "Assets/_Scripts/Data/Enums/GameToastSituation.cs"), encoding="utf-8").read()
    if f"SpearlineHint = {HINT_TOAST}," not in toasts:
        errs.append(f"GameToastSituation.SpearlineHint is not the roster's reserved {HINT_TOAST}")
    for member, value in FUN_TOASTS:
        if f"{member} = {value}," not in toasts:
            errs.append(f"GameToastSituation.{member} is not {value} (the fun pass's reserved 400-409)")
        if len(re.findall(rf"= {value},", toasts)) != 1:
            errs.append(f"GameToastSituation value {value} is used more than once")
    src = open(os.path.join(root, f"{MODE_DIR}/SpearlineCourseSource.cs"), encoding="utf-8").read()
    if f"DefaultLaps = {LAPS};" not in src:
        errs.append(f"SpearlineCourseSource.DefaultLaps is not {LAPS}")
    circuit = open(os.path.join(root, "Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs"), encoding="utf-8").read()
    if f"GateCount = {GATES_PER_LAP}," not in circuit:
        errs.append(f"HeadlongCircuitSettings.ForIntensity no longer lays {GATES_PER_LAP} rings a lap")
    ctl = "".join(open(os.path.join(root, f"{MODE_DIR}/{f}"), encoding="utf-8").read()
                  for f in ("SpearlineController.cs", "SpearlineController.Coach.cs", "SpearlineController.Feedback.cs"))
    # The LOCKED mode rule: the mode lays seals, judges rings and coaches its AI's input; it never touches the
    # vessel's kit. The AI's strokes go through the shipped sword hand (SwordfishSwordAutopilot.Coach).
    for banned in ("PerformShipControllerActions", "InputStatus", "ElementalScaling", "IsUpgradeActive"):
        if banned in ctl:
            errs.append(f"SpearlineController* mentions {banned} - a mode never changes the vessel")
    # Every sound is an exposed FMOD slot, shipped empty (never a path in code).
    for event in SOUND_SLOTS:
        if not re.search(rf"\[SerializeField\] FMODUnity\.EventReference {event};", ctl):
            errs.append(f"SpearlineController has no empty FMOD slot {event}")
    if "PlayOneShot(" in ctl or "event:/" in ctl:
        errs.append("SpearlineController names an FMOD event path in code")
    hand = open(os.path.join(root, "Assets/_Scripts/Controller/AI/Rhino/SwordfishSwordAutopilot.cs"), encoding="utf-8").read()
    if "public ISwordfishSwordCoach Coach" not in hand or "Coach.TryHold(" not in hand:
        errs.append("SwordfishSwordAutopilot has no Coach seam - Spearline's AI cannot stroke through a seal")
    return errs


htr.author(dict(
    member="Spearline", display="Spearline", hull="Swordfish", class_id=lib.VESSEL_CLASS_ID["Swordfish"],
    scripts={
        "SpearlineController":   f"{MODE_DIR}/SpearlineController.cs",
        "SpearlineController.Coach":    f"{MODE_DIR}/SpearlineController.Coach.cs",
        "SpearlineController.Feedback": f"{MODE_DIR}/SpearlineController.Feedback.cs",
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
    description="""Swordfish only. Five laps of the circuit, and the gentle rings are sealed
    with panes of prisms. A sealed ring counts only through a hole - your hull alone punches
    too small a one, so swing the bill into the pane as you go through. From level two part
    of every pane is shielded and only cracks - cross the bright face and swing into it.
    First team to put a pilot over the line wins.""",
    preview_objective="Swing the bill into each sealed ring",
    preview_notes="The circuit (HeadlongCircuit) and its seals are laid at match start from the rings, so a preview shows the shell. It teaches the seal - a ring that counts only through a hole you cut - and from I2 the armour, a shielded face to read and avoid.",
    build_after="MinigameThornline",
    extra_toasts=[(HINT_TOAST, "Swing the bill into the pane as you go through - a sealed ring counts only through a hole you cut",
                   dict(domain_names=0)),
                  (400, "The seal held - cut a hole of your own, then come back through it", dict(domain_names=0)),
                  (401, "{0} seals cut clean", dict(domain_names=0)),
                  (402, "Shields only crack - cross the bright face and swing into it", dict(domain_names=0))],
    extra_errors=_errors(),
))
