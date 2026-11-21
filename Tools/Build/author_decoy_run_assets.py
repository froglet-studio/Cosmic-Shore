#!/usr/bin/env python3
"""
Authors every serialized asset Decoy Run needs (GameModes.DecoyRun, the id READ from GameModes.cs).
The shared work is hull_time_race.author; this file is the Jacks' spec.

Run from the repo root:  python3 Tools/Build/author_decoy_run_assets.py [--check]

WHAT THIS MODE IS. The Jacks' TIME card (Garrett, 2026-11-07: every vessel gets a Time game that suits it). The Jacks
fly at the Rhino rig's plain 50 u/s, so the race is their herd-and-travel cycle: swap to the jacks, run them on the board
until one has laid the decoy past the ring and their midpoint sits behind it, take off (the jump is a teleport and threads
nothing) and fly to the decoy (that flight threads the ring). I1 doors, I2 skewers (two rings on one zip), I3 skewers on
rolled flats, I4 a skewer and a hairpin with Time 5 from the start (the second decoy). The course and its tests:
DecoyRunCourse, DecoyRunCourseTests (Tools/Build/decoy_run_harness/run.sh). Doc: DECOY_RUN.md.

THE DONOR IS THRESHER TIME'S SCENE (barren race cell, a plain GateRaceController block). The clone swaps the controller
script, the scoring rule asset, the AI numbers, the detection clamp (a travel flies at up to 900 u/s) and the Netcode ids,
and adds the chalk prefab; the card's Vessels list clamps every seat to the Jacks, and its StartingElements give the I4
racers Time 5.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/DecoyRun"
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

# The chalk is the environment prism, super-shielded so the jacks' net never drags it.
SPAWNABLE_PRISM_PATH = "Assets/_Prefabs/Trails/SpawnablePrism.prefab"
SPAWNABLE_PRISM = lib.existing_guid(SPAWNABLE_PRISM_PATH) \
    if os.path.exists(os.path.join(lib.ROOT, SPAWNABLE_PRISM_PATH)) else None
CHALK_FIELDS = """  chalkPrefab: {{fileID: 4563009547826722997, guid: {prism}, type: 3}}
  aiGroupWaitSeconds: 2.5
"""


def _errors():
    errs = []
    course = open(os.path.join(lib.ROOT, f"{MODE_DIR}/DecoyRunCourse.cs"), encoding="utf-8").read()
    source = open(os.path.join(lib.ROOT, "Assets/_Scripts/Controller/Arcade/Racing/RaceCourseSource.cs"),
                  encoding="utf-8").read()
    a = re.search(r"DefaultInnerRadius = ([0-9.]+)f;", course)
    b = re.search(r"DefaultInnerRadiusFallback = ([0-9.]+)f;", source)
    if not (a and b and float(a.group(1)) == float(b.group(1))):
        errs.append("DecoyRunCourse.DefaultInnerRadius no longer equals RaceCourseSource.DefaultInnerRadiusFallback")
    if not re.search(r"StartTimeLevel = new\[\] \{ 0, 0, 0, 5 \}", course):
        errs.append("DecoyRunCourse no longer cuts I4 for Time 5 - the card's StartingElements row below must follow it")
    if SPAWNABLE_PRISM is None:
        errs.append("SpawnablePrism.prefab not found - the chalk has no prefab")
    return errs


assert SPAWNABLE_PRISM, "SpawnablePrism.prefab not found"
JACKS = lib.VESSEL_CLASS_ID["Jacks"]
htr.author(dict(
    member="DecoyRun", display="Decoy Run", hull="Jacks", class_id=JACKS,
    scripts={
        "DecoyRunController":   f"{MODE_DIR}/DecoyRunController.cs",
        "DecoyRunCourse":       f"{MODE_DIR}/DecoyRunCourse.cs",
        "DecoyRunCourseSource": f"{MODE_DIR}/DecoyRunCourseSource.cs",
        "DecoyRunCourseTests":  "Assets/_Scripts/Tests/Editor/DecoyRunCourseTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/DECOY_RUN.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), laps="  laps: 2\n",
                               commit=135, lead=150, through=100, speed=4600),
    # The AI's cycles are its coach's (DecoyRunController.TryNextCycle); the approach numbers only shape the slow flight
    # between them. The clamp covers a 900 u/s travel at 60 fps twice, with room.
    new_fields=FIELDS.format(rule="__RULE__", laps="", commit=135, lead=150, through=100, speed=2400)
    + CHALK_FIELDS.format(prism=SPAWNABLE_PRISM),
    max_speed=1000,
    # 12 rings flown once: 12 doors, 6 skewers, 6 tilted skewers, 4 knots.
    target=12, laps=1, target_key="decoyRunGateTarget", target_after="thornlineGateTarget",
    # Time only matters at 5 here (the second decoy), so the rate is a level a ring: five rings down hands a trailer
    # the second decoy, which lets one cycle take a ring more.
    comeback=1.0,
    description="""Jacks only, and barely any flying - herding. Swap to the jacks, run
    them past the ring and drop a decoy beyond it, centre them behind it, take
    off and fly to the decoy. Line two rings up and one flight takes both. On the
    hard levels each pair lies on its own tilted plane, so roll onto it first. The
    hardest level gives a second decoy for a hairpin. First team to put a pilot
    through the last ring takes it.""",
    preview_objective="Herd the jacks past the ring, take off, fly to the decoy",
    preview_notes="OPEN-ENDED, shell-only: the rings and the chalk lanes (DecoyRunCourse) are laid at match start, so a preview has none. It teaches the herd, the decoy, the take-off and the travel.",
    build_after="MinigameThornline",
    extra_errors=_errors(),
    card_extra=f"""  StartingElements:
  - Class: {JACKS}
    Intensity: 4
    Levels:
      Mass: 0
      Charge: 0
      Space: 0
      Time: 0.5
""",
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
