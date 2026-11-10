#!/usr/bin/env python3
"""
Authors every serialized asset Brittle Star Time needs (GameModes.BrittleStarTime, the id READ from
GameModes.cs). The shared work is hull_time_race.author; this file is the Brittle Star's spec.

Run from the repo root:  python3 Tools/Build/author_brittle_star_time_assets.py [--check]

WHAT THIS MODE IS. The Brittle Star's TIME card: four intensities, four MOVEMENT questions about Speed Mode and the
turn (BrittleStarTimeCourse; Shombith, 2026-11-06): I1 Gearbox (hold Speed or shift down), I2 Feather slalom (tap A
for a speed between), I3 Coast-in hairpins (let go early, coast in), I4 Corkscrew (roll to hold Speed - the hull starts
I4 at Time 5, the card's StartingElements row, where Speed Roll is live). See
Assets/_Scripts/Controller/Arcade/BRITTLESTARTIME.md.

THE DONOR IS THRESHER TIME'S SCENE. The clone swaps the controller script, the scoring rule asset, the AI
approach (sized to Speed Mode's circle), the detection clamp and the Netcode ids.
"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/BrittleStarTime"
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
    member="BrittleStarTime", display="Brittle Star Time", hull="BrittleStar", class_id=16,
    scripts={
        "BrittleStarTimeController":   f"{MODE_DIR}/BrittleStarTimeController.cs",
        "BrittleStarTimeCourse":       f"{MODE_DIR}/BrittleStarTimeCourse.cs",
        "BrittleStarTimeCourseSource": f"{MODE_DIR}/BrittleStarTimeCourseSource.cs",
        "BrittleStarTimeCourseTests":  "Assets/_Scripts/Tests/Editor/BrittleStarTimeCourseTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/BRITTLESTARTIME.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), commit=135, lead=150, through=100, speed=4600),
    # An AI Brittle Star flies Speed Mode (BrittleStarSpeedModeAIPolicySO) on a ~145 u circle: Slingshot's
    # ratios (commit 2.2x, lead 2.5x, through 1.7x) on that circle. The clamp covers twice Time 10's 360 u/s.
    new_fields=FIELDS.format(rule="__RULE__", commit=320, lead=360, through=250, speed=1000),
    max_speed=360,
    target=16, laps=2, target_key="brittleStarTimeGateTarget", target_after="serpentTimeGateTarget",
    # 4 gates behind x 0.45 = 1.8 levels; the Brittle Star's Time raises Speed Mode x6 -> x8.
    comeback=0.45,
    description="""Brittle Stars only, against the clock. Speed Mode runs you at six times
    cruise - but it turns wide. Every intensity asks one question of it. Gearbox - hold
    Speed or shift down. Feather slalom - tap it in and out to weave. Coast-in hairpins -
    let go early and coast into the turn. Corkscrew - start at Time 5 and roll to hold
    Speed through the coil. Two laps. Fastest domain home wins.""",
    preview_objective="One question of Speed Mode per intensity - shift, feather, coast, roll",
    preview_notes="OPEN-ENDED, shell-only: four course shapes cut for the Brittle Star (BrittleStarTimeCourse), laid as rings at match start, so a preview has no gates. It teaches Speed Mode and the turn.",
    # I4 Corkscrew starts the hull at Time 5 (0.5): Speed Roll unlocks at level 5 (BrittleStar.asset), and Speed Mode
    # runs x7 there. Every other rung starts at rest.
    card_extra="""  StartingElements:
  - Class: 16
    Intensity: 4
    Levels:
      Mass: 0
      Charge: 0
      Space: 0
      Time: 0.5
""",
    build_after="MinigameSerpentTime",
))
