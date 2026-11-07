#!/usr/bin/env python3
"""
Authors every serialized asset Breach needs (GameModes.Breach, the id READ from GameModes.cs).
The shared work is hull_time_race.author; this file is the Orca's spec.

Run from the repo root:  python3 Tools/Build/author_breach_assets.py [--check]

WHAT THIS MODE IS. The Orca's TIME card (Garrett, 2026-11-07: every vessel gets a Time game that suits it). The Orca
cruises at 30 u/s; the helm's BURN is five times that (115 u/s) from a 3 s tank that refills at a quarter of real time,
and a burning hull turns at a fifth of its cruising sweep. The race is where in each leg to burn: I1 gentle bends (burn
the straights), I2 hard turns on short legs (a burn into one misses the ring), I3 danger walls standing before some rings
with a dog-leg round them (a burn into one is a dead stop), I4 both with the smallest rings. The course and its tests:
BreachCourse, BreachCourseTests (Tools/Build/breach_harness/run.sh). Doc: BREACH.md.

THE DONOR IS THRESHER TIME'S SCENE (barren race cell, a plain GateRaceController block). The clone swaps the controller
script, the scoring rule asset, the detection clamp and the Netcode ids, and adds the wall prefab; the card's Vessels
list clamps every seat to the Orca.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/Breach"
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

# The walls are the environment prism in the Blue danger domain: a hull that flies into one is stopped dead, and a
# gunner can shoot a breach through it (the wall regrows).
SPAWNABLE_PRISM_PATH = "Assets/_Prefabs/Trails/SpawnablePrism.prefab"
SPAWNABLE_PRISM = lib.existing_guid(SPAWNABLE_PRISM_PATH) \
    if os.path.exists(os.path.join(lib.ROOT, SPAWNABLE_PRISM_PATH)) else None
WALL_FIELDS = """  wallPrefab: {{fileID: 4563009547826722997, guid: {prism}, type: 3}}
  wallRegrowSeconds: 6
  wallRegrowPerFrame: 40
"""


def _errors():
    errs = []
    course = open(os.path.join(lib.ROOT, f"{MODE_DIR}/BreachCourse.cs"), encoding="utf-8").read()
    source = open(os.path.join(lib.ROOT, "Assets/_Scripts/Controller/Arcade/Racing/RaceCourseSource.cs"),
                  encoding="utf-8").read()
    a = re.search(r"DefaultInnerRadius = ([0-9.]+)f;", course)
    b = re.search(r"DefaultInnerRadiusFallback = ([0-9.]+)f;", source)
    if not (a and b and float(a.group(1)) == float(b.group(1))):
        errs.append("BreachCourse.DefaultInnerRadius no longer equals RaceCourseSource.DefaultInnerRadiusFallback")
    if not re.search(r"RingCount = 16,", course):
        errs.append("BreachCourse no longer cuts 16 rings - the target below must follow it")
    if SPAWNABLE_PRISM is None:
        errs.append("SpawnablePrism.prefab not found - the walls have no prefab")
    return errs


assert SPAWNABLE_PRISM, "SpawnablePrism.prefab not found"
ORCA = lib.VESSEL_CLASS_ID["Orca"]
htr.author(dict(
    member="Breach", display="Breach", hull="Orca", class_id=ORCA,
    scripts={
        "BreachController":   f"{MODE_DIR}/BreachController.cs",
        "BreachCourse":       f"{MODE_DIR}/BreachCourse.cs",
        "BreachCourseSource": f"{MODE_DIR}/BreachCourseSource.cs",
        "BreachCourseTests":  "Assets/_Scripts/Tests/Editor/BreachCourseTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/BREACH.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), laps="  laps: 2\n",
                               commit=135, lead=150, through=100, speed=4600),
    # The AI's burns are its coach's (BreachController.HelmBurns; the Orca's own helm spends the tank). The clamp covers
    # a 115 u/s burn twice, with room.
    new_fields=FIELDS.format(rule="__RULE__", laps="", commit=135, lead=150, through=100, speed=500)
    + WALL_FIELDS.format(prism=SPAWNABLE_PRISM),
    max_speed=250,
    # 16 rings flown once: about 110-140 s for the coached AI.
    target=16, laps=1, target_key="breachGateTarget", target_after="slipLineGateTarget",
    # The Time element does not change the burn (the tank is the crew's kit); a quarter-level a ring keeps the
    # comeback alive by the gate's measure.
    comeback=0.25,
    description="""Orca only, and the burn is the race. The helm's burn is
    five times your cruise, but a burning Orca turns wide and the tank refills
    slowly. Burn the straights and coast the turns - a burn into a hard turn
    misses the ring, and on the higher levels danger walls stand before some
    rings, rounded on the inside of the turn, and stop a hull dead.
    First team to put a pilot through the last ring takes it.""",
    preview_objective="Burn the straights, coast the turns",
    preview_notes="OPEN-ENDED, shell-only: the rings and the walls (BreachCourse) are laid at match start, so a preview has none. It teaches the burn and the tank.",
    build_after="MinigameSlipLine",
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
