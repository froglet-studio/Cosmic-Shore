#!/usr/bin/env python3
"""
Authors every serialized asset Slip Line needs (GameModes.SlipLine, the id READ from GameModes.cs).
The shared work is hull_time_race.author; this file is the Honeyguide's spec.

Run from the repo root:  python3 Tools/Build/author_slip_line_assets.py [--check]

WHAT THIS MODE IS. The Honeyguide's TIME card (Garrett, 2026-11-07: every vessel gets a Time game that suits it). The
Honeyguide cruises at 60 u/s; a SLIP down a prism trail of a colour the battery holds zips it along at 480 u/s for one
charge, and the Ratel seat makes the charges. The race lays RAILS (one prism trail each, in Ruby, Jade and Gold) along
the legs between rings: I1 three rails a leg (any charge rides), I2 one coloured rail a leg (half are stubs not worth a
charge), I3 rails that lean off the line, I4 chains (two rails, one ride for two rings) with the smallest rings. The
course and its tests: SlipLineCourse, SlipLineCourseTests (Tools/Build/slip_line_harness/run.sh). Doc: SLIP_LINE.md.

THE DONOR IS THRESHER TIME'S SCENE (barren race cell, a plain GateRaceController block). The clone swaps the controller
script, the scoring rule asset, the detection clamp (a zip flies at 480 u/s) and the Netcode ids, and adds the rail
prefab; the card's Vessels list clamps every seat to the Honeyguide.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/SlipLine"
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

# The rails are the environment prism, super-shielded so no hull or Peck breaks a rail mid-race.
SPAWNABLE_PRISM_PATH = "Assets/_Prefabs/Trails/SpawnablePrism.prefab"
SPAWNABLE_PRISM = lib.existing_guid(SPAWNABLE_PRISM_PATH) \
    if os.path.exists(os.path.join(lib.ROOT, SPAWNABLE_PRISM_PATH)) else None
RAIL_FIELDS = """  railPrefab: {{fileID: 4563009547826722997, guid: {prism}, type: 3}}
  aiRailLead: 40
"""


def _errors():
    errs = []
    course = open(os.path.join(lib.ROOT, f"{MODE_DIR}/SlipLineCourse.cs"), encoding="utf-8").read()
    source = open(os.path.join(lib.ROOT, "Assets/_Scripts/Controller/Arcade/Racing/RaceCourseSource.cs"),
                  encoding="utf-8").read()
    a = re.search(r"DefaultInnerRadius = ([0-9.]+)f;", course)
    b = re.search(r"DefaultInnerRadiusFallback = ([0-9.]+)f;", source)
    if not (a and b and float(a.group(1)) == float(b.group(1))):
        errs.append("SlipLineCourse.DefaultInnerRadius no longer equals RaceCourseSource.DefaultInnerRadiusFallback")
    if not re.search(r"RingCount = 16,", course):
        errs.append("SlipLineCourse no longer cuts 16 rings - the target below must follow it")
    if SPAWNABLE_PRISM is None:
        errs.append("SpawnablePrism.prefab not found - the rails have no prefab")
    return errs


assert SPAWNABLE_PRISM, "SpawnablePrism.prefab not found"
HONEYGUIDE = lib.VESSEL_CLASS_ID["Honeyguide"]
htr.author(dict(
    member="SlipLine", display="Slip Line", hull="Honeyguide", class_id=HONEYGUIDE,
    scripts={
        "SlipLineController":   f"{MODE_DIR}/SlipLineController.cs",
        "SlipLineCourse":       f"{MODE_DIR}/SlipLineCourse.cs",
        "SlipLineCourseSource": f"{MODE_DIR}/SlipLineCourseSource.cs",
        "SlipLineCourseTests":  "Assets/_Scripts/Tests/Editor/SlipLineCourseTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/SLIP_LINE.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), laps="  laps: 2\n",
                               commit=135, lead=150, through=100, speed=4600),
    # The AI's rides are its coach's (SlipLineController steers it onto the rail it chose; the Honeyguide's own slip
    # spends the charge). The clamp covers a 480 u/s zip plus its exit boost twice, with room.
    new_fields=FIELDS.format(rule="__RULE__", laps="", commit=135, lead=150, through=100, speed=1400)
    + RAIL_FIELDS.format(prism=SPAWNABLE_PRISM),
    max_speed=700,
    # 16 rings flown once: about 100 s at Hard, 7 or so rides.
    target=16, laps=1, target_key="slipLineGateTarget", target_after="perchRunGateTarget",
    # The Time element is the Slip, which the Honeyguide always has; a level does not change a ride. A quarter-level a
    # ring keeps the comeback alive by the gate's measure.
    comeback=0.25,
    description="""Honeyguide only, and the rails are the race. Touch a rail
    of a colour your battery holds with your nose along it and you zip down it
    eight times faster than you fly - one charge a ride. The Ratel makes the
    charges, so spend them well - stubs are not worth one, leaning rails need a
    turn out to meet, and on the hardest level one ride chains into the next.
    First team to put a pilot through the last ring takes it.""",
    preview_objective="Ride the rails through the rings - one charge a ride",
    preview_notes="OPEN-ENDED, shell-only: the rings and the rails (SlipLineCourse) are laid at match start, so a preview has none. It teaches the battery and the slip.",
    build_after="MinigamePerchRun",
    extra_errors=_errors(),
    # Thresher Time's nest fields, which the donor grew after the hull-time-race clone was written.
    donor_tail="""  nestPrefab: {{fileID: 4563009547826722997, guid: {prism}, type: 3}}
  creditRadiusByIntensity:
  - 320
  - 280
  - 240
  - 200
  regrowSecondsByIntensity:
  - 3
  - 3
  - 2.5
  - 2
  nestBurstPerFrame: 48
  nestBurstSpeed: 40
  nestRegrowPerFrame: 40
  crystalEveryNthTarget: 3
""".format(prism=lib.existing_guid("Assets/_Prefabs/Trails/SpawnablePrism.prefab")),
))
