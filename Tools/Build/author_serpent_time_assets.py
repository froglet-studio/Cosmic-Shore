#!/usr/bin/env python3
"""
Authors every serialized asset Serpent Time needs (GameModes.SerpentTime, the id READ from GameModes.cs).
The shared work is hull_time_race.author; this file is the Serpent's spec.

Run from the repo root:  python3 Tools/Build/author_serpent_time_assets.py [--check]

WHAT THIS MODE IS. The Serpent's TIME card (it had none: Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md §2).
Thresher Time's shape - a lapped circuit on the shared HeadlongCircuit solver - cut by SerpentTimeCourse
against the Serpent's Solid Fuel Pellets: four in the tank, each a 3 s burn that stacks (160 u/s on
one, 460 on four), one back every ~3.6 s. See Assets/_Scripts/Controller/Arcade/SERPENTTIME.md.

THE DONOR IS THRESHER TIME'S SCENE (barren race cell, equatorial spawn ring, a `laps` field). The clone
swaps the controller script, the scoring rule asset, the AI approach, the detection clamp and the
Netcode ids; the card's Vessels list clamps every seat to the Serpent.
"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/SerpentTime"
FIELDS = """  rule: {{fileID: 11400000, guid: {rule}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: 2
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

htr.author(dict(
    member="SerpentTime", display="Serpent Time", hull="Serpent", class_id=lib.VESSEL_CLASS_ID["Serpent"],
    scripts={
        "SerpentTimeController":   f"{MODE_DIR}/SerpentTimeController.cs",
        "SerpentTimeCourse":       f"{MODE_DIR}/SerpentTimeCourse.cs",
        "SerpentTimeCourseSource": f"{MODE_DIR}/SerpentTimeCourseSource.cs",
        "SerpentTimeCourseTests":  "Assets/_Scripts/Tests/Editor/SerpentTimeCourseTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/SERPENTTIME.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), commit=135, lead=150, through=100, speed=4600),
    # The AI burns pellets (ConsumeBoostAction's AI path), so it arrives at up to 460 u/s on a ~100 u
    # circle: the approach sits outside 2x that circle, Switchback's numbers. The clamp covers twice 460.
    new_fields=FIELDS.format(rule="__RULE__", commit=260, lead=300, through=220, speed=1400),
    max_speed=460,
    target=16, laps=2, target_key="serpentTimeGateTarget", target_after="scarabRallyGateTarget",
    # A quarter-of-race deficit (4 gates) x 0.45 = 1.8 element levels - Thresher Time's number on the
    # same 16-gate race. The Serpent's Time lengthens a pellet's burn (3 s -> 4.8 s).
    comeback=0.45,
    description="""Serpents only, against the clock. Your speed is your fuel: tap to burn
    a pellet, tap again to stack another on top - four in the tank, each one faster.
    Burn them all on a straight or keep one lit through the corners, and thread the
    rings at speed. Two laps. Fastest domain home wins.""",
    preview_objective="Burn your pellets - stack them on the straights",
    preview_notes="OPEN-ENDED, shell-only: Headlong''s solver cut for the Serpent, laid as rings at match start, so a preview has no gates. It teaches the fuel pellets.",
    build_after="MinigameScarabRally",
))
