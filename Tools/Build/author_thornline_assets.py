#!/usr/bin/env python3
"""
Authors every serialized asset Thornline needs (GameModes.Thornline, the id READ from GameModes.cs).
The shared work is hull_time_race.author; this file is the Shrike's spec.

Run from the repo root:  python3 Tools/Build/author_thornline_assets.py [--check]

WHAT THIS MODE IS. The Shrike's TIME card (Garrett, 2026-11-07: every vessel gets a Time game that suits it). The
Shrike cruises at 18 u/s, so the race is its Void Sling: a chain of THORNS (one big block each) with a ring at the
middle of every leg - dock, aim at the next thorn, sling (one second at any distance), reload docked. I2 adds plain
thorns (the slower x1.6 reload) and Time crystals on the rim lines, I3 three snipes through two rings, I4 the
smallest thorns and the longest legs. The course and its tests: ThornlineCourse, ThornlineCourseTests
(Tools/Build/thornline_course_harness/run.sh). Doc: THORNLINE.md.

THE DONOR IS THRESHER TIME'S SCENE (barren race cell, equatorial spawn ring, a plain GateRaceController block). The
clone swaps the controller script, the scoring rule asset, the AI numbers, the detection clamp (a sling flies at up
to 1,600 u/s) and the Netcode ids, and adds the thorn prefab; the card's Vessels list clamps every seat to the Shrike.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/Thornline"
FIELDS = """  rule: {{fileID: 11400000, guid: {rule}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
{laps}  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: {commit}
  aiApproachLead: {lead}
  aiThroughDistance: {through}
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: {speed}
  reportResyncSeconds: 3
"""

# The thorns are the environment prism, the one every arena lays its mass from.
SPAWNABLE_PRISM_PATH = "Assets/_Prefabs/Trails/SpawnablePrism.prefab"
SPAWNABLE_PRISM = lib.existing_guid(SPAWNABLE_PRISM_PATH) \
    if os.path.exists(os.path.join(lib.ROOT, SPAWNABLE_PRISM_PATH)) else None
THORN_FIELDS = """  thornPrefab: {{fileID: 4563009547826722997, guid: {prism}, type: 3}}
  thornRegrowSeconds: 3
  crystalRegrowSeconds: 5
  aiTakesSnipes: 1
"""


def _errors():
    errs = []
    course = open(os.path.join(lib.ROOT, f"{MODE_DIR}/ThornlineCourse.cs"), encoding="utf-8").read()
    source = open(os.path.join(lib.ROOT, "Assets/_Scripts/Controller/Arcade/Racing/RaceCourseSource.cs"),
                  encoding="utf-8").read()
    a = re.search(r"DefaultInnerRadius = ([0-9.]+)f;", course)
    b = re.search(r"DefaultInnerRadiusFallback = ([0-9.]+)f;", source)
    if not (a and b and float(a.group(1)) == float(b.group(1))):
        errs.append("ThornlineCourse.DefaultInnerRadius no longer equals RaceCourseSource.DefaultInnerRadiusFallback")
    if SPAWNABLE_PRISM is None:
        errs.append("SpawnablePrism.prefab not found - the thorns have no prefab")
    return errs


assert SPAWNABLE_PRISM, "SpawnablePrism.prefab not found"
htr.author(dict(
    member="Thornline", display="Thornline", hull="Shrike", class_id=lib.VESSEL_CLASS_ID["Shrike"],
    scripts={
        "ThornlineController":   f"{MODE_DIR}/ThornlineController.cs",
        "ThornlineCourse":       f"{MODE_DIR}/ThornlineCourse.cs",
        "ThornlineCourseSource": f"{MODE_DIR}/ThornlineCourseSource.cs",
        "ThornlineCourseTests":  "Assets/_Scripts/Tests/Editor/ThornlineCourseTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/THORNLINE.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), laps="  laps: 2\n",
                               commit=135, lead=150, through=100, speed=4600),
    # The AI aims at THORNS (ThornlineController.TryOverrideAim), so the approach numbers only shape the steering
    # toward a thorn; they are the sling's scale, not a turn circle. The clamp covers a 1,600 u sling at 60 fps twice.
    new_fields=FIELDS.format(rule="__RULE__", laps="", commit=300, lead=340, through=240, speed=4000)
    + THORN_FIELDS.format(prism=SPAWNABLE_PRISM),
    max_speed=1600,
    # 16 rings, an OPEN chain of thorns flown once.
    target=16, laps=1, target_key="thornlineGateTarget", target_after="lanternRunGateTarget",
    # A quarter-of-race deficit (4 rings) x 0.35 = 1.4 element levels; Time shortens the sling's cooldown 6 -> 2.5 s,
    # so the buff lands on the race's own clock.
    comeback=0.35,
    description="""Shrikes only, and no flying - slinging. Dock on a thorn, aim at the next,
    and sling through the ring between - one second at any distance, then wait out the
    reload. Super-shielded thorns reload faster. Aim at a thorn's rim to take the Time
    crystal on the line, and on the hard levels snipe a far thorn through two rings at
    once. First team to put a pilot through the last ring takes it.""",
    preview_objective="Dock, aim at the next thorn, sling through the ring",
    preview_notes="OPEN-ENDED, shell-only: the thorn chain (ThornlineCourse) is laid at match start from the rings, so a preview has no thorns. It teaches the sling, the reload and the snipe.",
    build_after="MinigameBrittleStarTime",
    extra_errors=_errors(),
))
