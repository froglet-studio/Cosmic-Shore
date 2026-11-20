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

THE SHIFT LINE (2026-11-16, BRITTLESTARTIME.md section 0, Docs/BrittleStarTime/SHIFT_LINE_SPEC.md). On top of the race:
let-go gates before the rings Speed Mode cannot hold (BrittleStarTimeLine), the AI's race coach (BrittleStarTimeCoach),
the local pilot's heat (BrittleStarTimeHeat), laps per rung (2/3/3/4), toasts 410-419 and eight empty FMOD slots. This
generator writes the controller's new fields, the toasts, the card and preview text, and the hull AI policy's mode
variant that stands its own Speed law down in this mode (the coach is the one writer of the wish).

THE DONOR IS THRESHER TIME'S SCENE. The clone swaps the controller script, the scoring rule asset, the AI
approach (sized to Speed Mode's circle), the detection clamp and the Netcode ids.
"""
import os
import re
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

# ── The Shift Line ────────────────────────────────────────────────────────────────────────────────────────────────
# The environment prism Waystation's walls and Grizzly Time's kegs are laid from (SpawnablePrism.prefab).
SPAWNABLE_PRISM = "ed9defc56162b4b4588e61c20984b6d9"
SPAWNABLE_PRISM_FILE_ID = 4563009547826722997
HEAT_SOUNDS = ("flatOutThreadEvent", "cleanShiftEvent", "streakCalledEvent", "lapCompleteEvent", "bestLapEvent",
               "cleanLapEvent", "missedMouthEvent", "speedDarkEvent")
SHIFT_LINE_FIELDS = (
    "  coachOn: 1\n"
    "  heatOn: 1\n"
    "  flatOutFlarePunch: 2.4\n"
    "  cleanShiftFlarePunch: 1.8\n"
    "  flareSeconds: 0.45\n"
    "  chaseStagger: 0.06\n"
    "  maxFlares: 24\n"
    + "".join(f"  {k}:\n" + lib.event_yaml(f"{lib.SCENES_DIR}/MinigameBrittleStarTime.unity", k) for k in HEAT_SOUNDS)
    + f"  postPrefab: {{fileID: {SPAWNABLE_PRISM_FILE_ID}, guid: {SPAWNABLE_PRISM}, type: 3}}\n"
    "  postRegrowSeconds: 1.5\n"
    "  gateLightSeconds: 1.2\n")

# Toasts 410-419 (GameToastSituation, the band reserved for this mode among the round-5 sessions). The tokens are the
# toast system's positional {0}/{1}; every word is here, none in code.
TOASTS = (
    (410, "Light Speed Mode and keep it lit - six times cruise. Let go only when the nose is far off the next ring", dict(idle=0)),
    (411, "{0} carried in a row", {}),
    (412, "Lap {0} - {1} s", {}),
    (413, "Lap {0} - {1} s - best lap", {}),
    (414, "Lap {0} carried end to end", {}),
    (415, "Flat out - through the heart at full Speed", {}),
    (416, "Clean shift - let go at the posts and coasted in", {}),
    (417, "Hairpins: let go of Speed Mode at the posts, coast in, light it again on the way out", {}),
    (418, "Corkscrew: you start at Time 5 - roll and pull to hold Speed Mode through the coil", {}),
    (419, "Speed Mode is dark - light it to race", {}),
)

# The hull AI policy's mode variant (AIBoostPolicySO.ForMode): its straight law never engages (align 0 deg, 999 s of
# runway), so in Brittle Star Time the race coach is the one writer of the Speed wish. The hull's own policy is untouched.
HULL_POLICY = "Assets/_SO_Assets/AI Boost Policies/BrittleStarAIBoostPolicy.asset"
VARIANT = "Assets/Resources/AIBoostPolicies/BrittleStarAIBoostPolicy_BrittleStarTime.asset"
VARIANT_GUID = lib.guid("asset/BrittleStarAIBoostPolicy_BrittleStarTime")


def variant_text() -> str:
    hull = htr.read_or_empty(HULL_POLICY)
    assert "m_Name: BrittleStarAIBoostPolicy\n" in hull, "the hull's AI policy asset moved or was renamed"
    text = hull.replace("m_Name: BrittleStarAIBoostPolicy\n", "m_Name: BrittleStarAIBoostPolicy_BrittleStarTime\n")
    text = re.sub(r"^  alignDegrees: .*$", "  alignDegrees: 0", text, flags=re.M)
    text = re.sub(r"^  engageRunwaySeconds: .*$", "  engageRunwaySeconds: 999", text, flags=re.M)
    return text


def shift_line_errors() -> "list[str]":
    errors = []
    course = htr.read_or_empty(f"{MODE_DIR}/BrittleStarTimeCourse.cs")
    m = re.search(r"LapsByIntensity = \{ (\d+), (\d+), (\d+), (\d+) \};", course)
    if not m or [int(x) for x in m.groups()] != [2, 3, 6, 6]:
        errors.append("BrittleStarTimeCourse.LapsByIntensity is not { 2, 3, 6, 6 } (BRITTLESTARTIME.md section 0, the climbing ladder of 2026-11-17)")
    toasts = htr.read_or_empty("Assets/_Scripts/Data/Enums/GameToastSituation.cs")
    for sid, _, _ in TOASTS:
        hits = re.findall(rf"\b(\w+) = {sid},", toasts)
        if len(hits) != 1 or not hits[0].startswith("BrittleStarTime"):
            errors.append(f"GameToastSituation {sid} is not exactly one BrittleStarTime member ({hits})")
    cs = "".join(htr.read_or_empty(f"{MODE_DIR}/{f}") for f in os.listdir(os.path.join(htr.ROOT, MODE_DIR))
                 if f.startswith("BrittleStarTimeController") and f.endswith(".cs"))
    for k in HEAT_SOUNDS:
        if f"FMODUnity.EventReference {k};" not in cs:
            errors.append(f"BrittleStarTimeController has no FMODUnity.EventReference {k}")
    for k in ("coachOn", "heatOn", "postPrefab", "postRegrowSeconds", "gateLightSeconds", "flatOutFlarePunch",
              "cleanShiftFlarePunch", "flareSeconds", "chaseStagger", "maxFlares"):
        if not re.search(rf"\b{k}\b", cs):
            errors.append(f"BrittleStarTimeController has no field {k}")
    if re.search(r"\bvoid Update\s*\(", cs):
        errors.append("a BrittleStarTimeController partial declares Update: it would hide GateRaceController.Update")
    v = variant_text()
    if "  engageRunwaySeconds: 999\n" not in v or "  alignDegrees: 0\n" not in v:
        errors.append("the AI policy variant's straight law can engage")
    for i in range(1, 5):
        if os.path.exists(os.path.join(htr.ROOT, VARIANT.replace(".asset", f"_I{i}.asset"))):
            errors.append(f"an _I{i} AI policy variant would win over the coach's stand-down variant")
    return errors


htr.author(dict(
    member="BrittleStarTime", display="Brittle Star Time", hull="BrittleStar", class_id=16,
    scripts={
        "BrittleStarTimeController":         f"{MODE_DIR}/BrittleStarTimeController.cs",
        "BrittleStarTimeController.Coach":   f"{MODE_DIR}/BrittleStarTimeController.Coach.cs",
        "BrittleStarTimeController.Heat":    f"{MODE_DIR}/BrittleStarTimeController.Heat.cs",
        "BrittleStarTimeController.Line":    f"{MODE_DIR}/BrittleStarTimeController.Line.cs",
        "BrittleStarTimeCourse":             f"{MODE_DIR}/BrittleStarTimeCourse.cs",
        "BrittleStarTimeCourseSource":       f"{MODE_DIR}/BrittleStarTimeCourseSource.cs",
        "BrittleStarTimeLine":               f"{MODE_DIR}/BrittleStarTimeLine.cs",
        "BrittleStarTimeCoach":              f"{MODE_DIR}/BrittleStarTimeCoach.cs",
        "BrittleStarTimeHeat":               f"{MODE_DIR}/BrittleStarTimeHeat.cs",
        "BrittleStarTimeCourseTests":        "Assets/_Scripts/Tests/Editor/BrittleStarTimeCourseTests.cs",
        "BrittleStarTimeLineTests":          "Assets/_Scripts/Tests/Editor/BrittleStarTimeLineTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/BRITTLESTARTIME.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), commit=135, lead=150, through=100, speed=4600),
    # An AI Brittle Star flies Speed Mode (BrittleStarSpeedModeAIPolicySO) on a ~145 u circle: Slingshot's
    # ratios (commit 2.2x, lead 2.5x, through 1.7x) on that circle. The clamp covers twice Time 10's 360 u/s.
    new_fields=FIELDS.format(rule="__RULE__", commit=320, lead=360, through=250, speed=1000),
    new_field_lines=SHIFT_LINE_FIELDS,
    max_speed=360,
    # The I1 race (two laps of eight); I2-I4 fly 3/3/4 laps of their shorter shapes (BrittleStarTimeCourseSource).
    target=16, laps=2, target_key="brittleStarTimeGateTarget", target_after="serpentTimeGateTarget",
    # 4 gates behind x 0.45 = 1.8 levels; the Brittle Star's Time raises Speed Mode x6 -> x8.
    comeback=0.45,
    description="""Brittle Stars only, against the clock. Speed Mode runs you at six times
    cruise - but it turns wide. Most rings you take flat out; where two posts stand before a
    ring, let go of Speed Mode there and coast in. Gearbox, Feather slalom, Coast-in hairpins,
    Corkscrew - one question of Speed Mode per intensity, more laps as the shapes tighten.
    Fastest domain home wins.""",
    preview_objective="Hold Speed Mode flat out; let go at the posts; thread every ring",
    preview_notes="OPEN-ENDED, shell-only: four course shapes cut for the Brittle Star (BrittleStarTimeCourse), laid as rings at match start, so a preview has no gates. Let-go posts stand before the rings Speed Mode cannot hold (BrittleStarTimeLine).",
    extra_toasts=TOASTS,
    extra_assets=[(VARIANT, VARIANT_GUID, variant_text())],
    extra_errors=shift_line_errors(),
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
