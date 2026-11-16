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

THE 2026-11-17 UPGRADE (Docs/Thornline/UPGRADE_SPEC.md): every seat one sling from the start thorn (the controller is
an IPlayerSpawnLine), the AI plan (ThornlineAIPlan: lanes, snipes, crystal shots, the stall rescue) and its nose lead by
difficulty, the sight, rival glow and the beats (toasts 420-426, nine empty FMOD slots). Their serialized fields are
the mode's own field lines (NEW_FIELD_LINES below), appended to the committed controller block once and then checked
verbatim; the C# defaults and ThornlineAIPlan's rows are checked against the same numbers.

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

# ── The 2026-11-17 upgrade's fields, in ThornlineController.cs declaration order (all in that one file). ──
AI_NOSE_LEAD = (2.5, 3, 8, 16)            # ThornlineAIPlan.NoseLeadByDifficulty (run.sh --ai measured)
AI_NOSE_FOLLOW = (-1, -1, -1, -1)         # ThornlineAIPlan.NoseFollowByDifficulty (the fleet lag)
AI_NOSE_LEAD_MAX_EXTRA = 10               # ThornlineAIPlan.NoseLeadMaxExtraDegrees
TOASTS = (                                # (field, GameToastSituation member, value, template)
    ("hintToast", "ThornlineHint", 420, "Sling into the lit sight on the next thorn"),
    ("snipeToast", "ThornlineSnipe", 421, "Snipe! Two rings, one sling"),
    ("crystalResetToast", "ThornlineCrystalReset", 422, "Reset - the sling is ready"),
    ("crystalTakenToast", "ThornlineCrystalTaken", 423, "{0} took the crystal"),
    ("caughtShortToast", "ThornlineCaughtShort", 424, "Caught short on a trail - aim beside it"),
    ("passedToast", "ThornlinePassed", 425, "Past {0}"),
    ("overtakenToast", "ThornlineOvertaken", 426, "{0} slung past you"),
)
SOUNDS = ("snipeEvent", "crystalResetEvent", "crystalTakenEvent", "cleanDockEvent", "caughtShortEvent",
          "sightReadyEvent", "passedEvent", "overtakenEvent", "thornRegrowEvent")
EMPTY_EVENT = "    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n"
FEEDBACK_SCALARS = (("caughtShortToastsPerRace", 2), ("passToastSeconds", 1.5))
SIGHT_SCALARS = (("sightReloadingScale", 0.35), ("rivalThornGlow", 0.35))


def _yaml_num(v):
    return str(int(v)) if float(v) == int(v) else str(v)


def _yaml_list(name, values):
    return f"  {name}:\n" + "".join(f"  - {_yaml_num(v)}\n" for v in values)


NEW_FIELD_LINES = (
    _yaml_list("aiNoseLeadByDifficulty", AI_NOSE_LEAD)
    + _yaml_list("aiNoseFollowByDifficulty", AI_NOSE_FOLLOW)
    + f"  aiNoseLeadMaxExtraDegrees: {AI_NOSE_LEAD_MAX_EXTRA}\n"
    + "".join(f"  {field}: {value}\n" for field, _, value, _ in TOASTS)
    + "".join(f"  {k}: {_yaml_num(v)}\n" for k, v in FEEDBACK_SCALARS)
    + "".join(f"  {k}:\n{EMPTY_EVENT}" for k in SOUNDS)
    + "".join(f"  {k}: {_yaml_num(v)}\n" for k, v in SIGHT_SCALARS))


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

    ctrl = open(os.path.join(lib.ROOT, f"{MODE_DIR}/ThornlineController.cs"), encoding="utf-8").read()
    plan = open(os.path.join(lib.ROOT, f"{MODE_DIR}/ThornlineAIPlan.cs"), encoding="utf-8").read()
    enum = open(os.path.join(lib.ROOT, "Assets/_Scripts/Data/Enums/GameToastSituation.cs"), encoding="utf-8").read()

    def cs_floats(src, name):
        m = re.search(rf"{name} = \{{([^}}]*)\}};", src)
        return None if not m else [float(v.strip().rstrip("f")) for v in m.group(1).split(",")]

    for src, label, name, want in ((ctrl, "ThornlineController", "aiNoseLeadByDifficulty", AI_NOSE_LEAD),
                                   (ctrl, "ThornlineController", "aiNoseFollowByDifficulty", AI_NOSE_FOLLOW),
                                   (plan, "ThornlineAIPlan", "NoseLeadByDifficulty", AI_NOSE_LEAD),
                                   (plan, "ThornlineAIPlan", "NoseFollowByDifficulty", AI_NOSE_FOLLOW)):
        got = cs_floats(src, name)
        if got != [float(w) for w in want]:
            errs.append(f"{label}.{name} is {got}, the scene authors {list(want)}")
    for src, label, name in ((ctrl, "ThornlineController", "aiNoseLeadMaxExtraDegrees"),
                             (plan, "ThornlineAIPlan", "NoseLeadMaxExtraDegrees")):
        m = re.search(rf"{name} = ([0-9.]+)f;", src)
        if not m or float(m.group(1)) != AI_NOSE_LEAD_MAX_EXTRA:
            errs.append(f"{label}.{name} is not {AI_NOSE_LEAD_MAX_EXTRA}")
    for k, v in FEEDBACK_SCALARS + SIGHT_SCALARS:
        d = lib.cs_field_default(ctrl, k)
        if d is None or float(d) != float(v):
            errs.append(f"ThornlineController.{k} defaults to {d}, the scene authors {v}")
    ids = [int(v) for v in re.findall(r"=\s*(\d+),", enum)]
    for field, member, value, _ in TOASTS:
        m = re.search(rf"^\s*{member}\s*=\s*(\d+),", enum, re.M)
        if not m or int(m.group(1)) != value:
            errs.append(f"GameToastSituation.{member} is not {value}")
        elif ids.count(value) != 1:
            errs.append(f"GameToastSituation value {value} is used {ids.count(value)} times")
        if not re.search(rf"GameToastSituation {field} = GameToastSituation\.{member};", ctrl):
            errs.append(f"ThornlineController.{field} does not default to GameToastSituation.{member}")
    for k in SOUNDS:
        if not re.search(rf"FMODUnity\.EventReference {k};", ctrl):
            errs.append(f"ThornlineController.{k} is not an exposed EventReference")
    # A regrown thorn must not hold the arena-ready gate (the /arcadegame Serpent Time rule).
    if "slot.Prism = Lay(slot.Thorn, forReveal: false);" not in ctrl or "CompletesAsLiveRibbon = true" not in ctrl:
        errs.append("ThornlineController no longer regrows a thorn as a live ribbon (forReveal: false + CompletesAsLiveRibbon)")
    if "IPlayerSpawnLine" not in ctrl:
        errs.append("ThornlineController is no longer the IPlayerSpawnLine: the equal start is gone")
    harness = os.path.join(lib.ROOT, "Tools/Build/thornline_course_harness/run.sh")
    if not os.path.exists(harness) or "--ai" not in open(harness, encoding="utf-8").read():
        errs.append("thornline_course_harness/run.sh has no --ai race replica")
    return errs


# SPAWNABLE_PRISM missing is reported by _errors() above (it used to be an assert here, which aborted every check
# below it: CLAUDE.md, "a spent one-shot must stand down").
htr.author(dict(
    member="Thornline", display="Thornline", hull="Shrike", class_id=lib.VESSEL_CLASS_ID["Shrike"],
    scripts={
        "ThornlineController":   f"{MODE_DIR}/ThornlineController.cs",
        "ThornlineController.AI": f"{MODE_DIR}/ThornlineController.AI.cs",
        "ThornlineController.Feedback": f"{MODE_DIR}/ThornlineController.Feedback.cs",
        "ThornlineAIPlan":       f"{MODE_DIR}/ThornlineAIPlan.cs",
        "ThornlineAIPlanTests":  "Assets/_Scripts/Tests/Editor/ThornlineAIPlanTests.cs",
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
    new_field_lines=NEW_FIELD_LINES,
    extra_toasts=[(value, template, dict(domain_names=0 if "{0}" not in template else 1))
                  for _, _, value, template in TOASTS],
    max_speed=1600,
    # 16 rings, an OPEN chain of thorns flown once.
    target=16, laps=1, target_key="thornlineGateTarget", target_after="lanternRunGateTarget",
    # A quarter-of-race deficit (4 rings) x 0.35 = 1.4 element levels; Time shortens the sling's cooldown 6 -> 2.5 s,
    # so the buff lands on the race's own clock.
    comeback=0.35,
    description="""Shrikes only, and no flying - slinging. Everyone starts one sling from the
    first thorn. Dock on a thorn, aim into the lit sight on the next, and sling through the
    ring between - one second at any distance, then wait out the reload. Super-shielded
    thorns reload faster. Aim through a Time crystal and the sling is ready the moment you
    dock, and on the hard levels snipe a far thorn through two rings at once. First team to
    put a pilot through the last ring takes it.""",
    preview_objective="Dock, aim at the next thorn, sling through the ring",
    preview_notes="OPEN-ENDED, shell-only: the thorn chain (ThornlineCourse) is laid at match start from the rings, so a preview has no thorns. It teaches the sling, the reload and the snipe.",
    build_after="MinigameBrittleStarTime",
    extra_errors=_errors(),
    # Thresher Time's nest fields, which the donor grew after Thornline was cloned (ThornlineController has none).
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
""".format(prism=SPAWNABLE_PRISM),
))
