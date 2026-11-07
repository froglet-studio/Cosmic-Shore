#!/usr/bin/env python3
"""
Authors every serialized asset Perch Run needs (GameModes.PerchRun, the id READ from GameModes.cs).
The shared work is hull_time_race.author; this file is the Goby's spec.

Run from the repo root:  python3 Tools/Build/author_perch_run_assets.py [--check]

WHAT THIS MODE IS. The Goby's TIME card (Garrett, 2026-11-07: every vessel gets a Time game that suits it). The Goby
cruises slowly and DARTS: hold to charge a length (140-600 u over 0.8 s), let go, and it flies there in one second,
stopping on the first block on its line. A dart costs the same second however far it goes, so the race is how few darts
a course takes: I1 open rings, I2 hairpins (a full charge overshoots out of reach of the next ring: judge the length),
I3 coral blockers (a dart at the centre stops on the head: take the rim), I4 doubles (one line, two rings) with the
smallest mouths. The course and its tests: PerchRunCourse, PerchRunCourseTests (Tools/Build/perch_run_harness/run.sh).
Doc: PERCH_RUN.md.

THE DONOR IS THRESHER TIME'S SCENE (barren race cell, a plain GateRaceController block). The clone swaps the controller
script, the scoring rule asset, the detection clamp (a dart flies at up to 600 u/s) and the Netcode ids, and adds the
coral prefab; the card's Vessels list clamps every seat to the Goby.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/PerchRun"
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

# The coral is the environment prism, super-shielded so nothing breaks a head mid-race.
SPAWNABLE_PRISM_PATH = "Assets/_Prefabs/Trails/SpawnablePrism.prefab"
SPAWNABLE_PRISM = lib.existing_guid(SPAWNABLE_PRISM_PATH) \
    if os.path.exists(os.path.join(lib.ROOT, SPAWNABLE_PRISM_PATH)) else None
CORAL_FIELDS = """  coralPrefab: {{fileID: 4563009547826722997, guid: {prism}, type: 3}}
"""


def _errors():
    errs = []
    course = open(os.path.join(lib.ROOT, f"{MODE_DIR}/PerchRunCourse.cs"), encoding="utf-8").read()
    source = open(os.path.join(lib.ROOT, "Assets/_Scripts/Controller/Arcade/Racing/RaceCourseSource.cs"),
                  encoding="utf-8").read()
    a = re.search(r"DefaultInnerRadius = ([0-9.]+)f;", course)
    b = re.search(r"DefaultInnerRadiusFallback = ([0-9.]+)f;", source)
    if not (a and b and float(a.group(1)) == float(b.group(1))):
        errs.append("PerchRunCourse.DefaultInnerRadius no longer equals RaceCourseSource.DefaultInnerRadiusFallback")
    if not re.search(r"RingCount = 16,", course):
        errs.append("PerchRunCourse no longer cuts 16 rings - the target below must follow it")
    if SPAWNABLE_PRISM is None:
        errs.append("SpawnablePrism.prefab not found - the coral has no prefab")
    return errs


assert SPAWNABLE_PRISM, "SpawnablePrism.prefab not found"
GOBY = lib.VESSEL_CLASS_ID["Goby"]
htr.author(dict(
    member="PerchRun", display="Perch Run", hull="Goby", class_id=GOBY,
    scripts={
        "PerchRunController":   f"{MODE_DIR}/PerchRunController.cs",
        "PerchRunCourse":       f"{MODE_DIR}/PerchRunCourse.cs",
        "PerchRunCourseSource": f"{MODE_DIR}/PerchRunCourseSource.cs",
        "PerchRunCourseTests":  "Assets/_Scripts/Tests/Editor/PerchRunCourseTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/PERCH_RUN.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), laps="  laps: 2\n",
                               commit=135, lead=150, through=100, speed=4600),
    # The AI's darts are its coach's (PerchRunController.TryRaceAim, through the Goby's own AI); the approach numbers
    # are the platform autopilot's, which the Goby's AI crew switches off. The clamp covers a 600 u/s dart twice, with room.
    new_fields=FIELDS.format(rule="__RULE__", laps="", commit=135, lead=150, through=100, speed=1400)
    + CORAL_FIELDS.format(prism=SPAWNABLE_PRISM),
    max_speed=700,
    # 16 rings flown once: about 16 darts at a second plus a 1.2 s reload each.
    target=16, laps=1, target_key="perchRunGateTarget", target_after="decoyRunGateTarget",
    # Time below 5 does nothing to the dart (it shortens the Pistol's jump cooldown); Time 5 phases the dart through
    # coral. A quarter-level a ring keeps the comeback alive by the gate's measure without handing a trailer Time 5.
    comeback=0.25,
    description="""Goby only, and every ring is a dart. Hold to charge, let go,
    and the Goby flies that far in one second, so the fewer darts the faster.
    Watch the reach ring - it shows where you will stop. Hairpins punish a full
    charge, coral stops a dart aimed at the centre (take the rim), and on the
    hardest level two rings on one line fall to a single dart. First team to put
    a pilot through the last ring takes it.""",
    preview_objective="Dart through the rings - judge each dart's length and line",
    preview_notes="OPEN-ENDED, shell-only: the rings and the coral (PerchRunCourse) are laid at match start, so a preview has none. It teaches the charge, the reach ring and the dart.",
    build_after="MinigameDecoyRun",
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
