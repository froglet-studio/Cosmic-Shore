#!/usr/bin/env python3
"""
Authors every serialized asset Brittle Star Time needs (GameModes.BrittleStarTime, the id READ from
GameModes.cs). The shared work is hull_time_race.author; this file is the Brittle Star's spec.

Run from the repo root:  python3 Tools/Build/author_brittle_star_time_assets.py [--check]

WHAT THIS MODE IS. The Brittle Star's TIME card (it had none). Thresher Time's shape cut by
BrittleStarTimeCourse against Speed Mode: x6 cruise (270 u/s) on a turn that barely grows with speed,
a ~145 u circle, so each level adds corners Speed Mode cannot hold (0 / 1 / 2 / 3 a lap). See
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
    description="""Brittle Stars only, against the clock. Speed Mode lights all five arms
    and runs you at six times cruise - but it turns wide. Hold it through the corners
    you can, drop back to Cruise for the ones you cannot, and thread the rings. Two
    laps. Fastest domain home wins.""",
    preview_objective="Speed Mode down the straights, Cruise for the tight corners",
    preview_notes="OPEN-ENDED, shell-only: Headlong''s solver cut for the Brittle Star, laid as rings at match start, so a preview has no gates. It teaches Speed Mode.",
    build_after="MinigameSerpentTime",
))
