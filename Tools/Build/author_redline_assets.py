#!/usr/bin/env python3
"""
Authors every serialized asset the Redline game mode needs; the mode id is READ from
GameModes.cs rather than hardcoded (Headlong's moved 48 -> 49 once; Bloomrush's 45 -> 52).

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_redline_assets.py [--check]

--check validates without writing (CI / pre-commit use).

WHAT THIS MODE IS. Redline is the Manta-only CIRCUIT race: a closed loop of switch rings cut
through the cell, flown in LAPS by every pilot, and the first DOMAIN whose LEAD RUNNER threads
the last gate of the last lap wins. Every corner is cut against the Manta's FULL-BOOST turn
radius - the 237u circle it holds with both triggers flat - so a corner is one question: how
much Soar is it worth? See Assets/_Scripts/Controller/Arcade/REDLINE.md.

THE DONOR IS HEADLONG, and that is the point rather than a shortcut. Headlong is already a lapped
gate race in the barren race cell with an EQUATORIAL spawn ring (gate 0 on its pole - the
fairness rule), on the shared GateRaceController with a RaceGateTurnMonitor, a
RaceGateObjectiveProvider and the generic GateRaceScoringRuleSO. The clone swaps FOUR things and
inherits the rest:

  1. the controller script (RedlineController), keeping Headlong's `laps` field.
  2. its scoring rule ASSET (a second asset on the same GateRaceScoringRuleSO script).
  3. the AI approach numbers and the plausible-speed clamp: a Manta arrives at 720-936 u/s
     against a Dolphin's 347 (the numbers Headlong inherited), so the commit/lead/through
     distances are sized to the Manta's 237u full-boost circle, and maxPlausibleSpeed is
     raised so a Time-10 Manta's frame step is never rejected as a teleport (at 400 the 30 fps
     step of a 936 u/s vessel - 31.2u - sits within a unit of the rejection line).
  4. the arcade card's Vessels list: Manta, not Rhino.

THE CIRCUIT GENERATOR IS SHARED, NOT CLONED. RedlineCourse supplies the Manta's settings to
HeadlongCircuit.Generate; the only change to the solver is an absolute corner floor field.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
G_SCRIPT = {
    "RedlineController":  guid("script/RedlineController"),
    "RedlineCourse":      guid("script/RedlineCourse"),
    "RedlineCourseTests": guid("script/RedlineCourseTests"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameRedline":     guid("asset/ArcadeGameRedline"),
    "RedlineScoringRule":    guid("asset/RedlineScoringRule"),
    "MinigameRedline.unity": guid("asset/MinigameRedline.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    "SO_ArcadeGame":          "fe040efad3307fb449b6b72ad15362da",
    # the shared gate-race scoring rule SCRIPT (one class, one asset per mode)
    "GateRaceScoringRuleSO":  "349cc0c9402590262de23356775d43cc",
    # donor scene wiring to swap out - Headlong's controller and rule, minted by ITS generator
    "HeadlongController":     hashlib.md5(b"CosmicShore/script/HeadlongController").hexdigest(),
    "HeadlongScoringRule":    hashlib.md5(b"CosmicShore/asset/HeadlongScoringRule").hexdigest(),
    # shared content
    "Vessel_Manta":           "b0e6ec5495dbfb6419332830d585f364",
    # arcade card art - shared with the other pure-race cards
    "IconActive":             "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":           "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground":         "587d2203114c8004c9985d0112c89585",
}

# ── Tuning ───────────────────────────────────────────────────────────────────

# The RACE LENGTH in gate threadings - LAPS x RINGS. One number: the controller divides by LAPS
# to size the circuit and the turn monitor asks the controller for the target, so the finish
# line and the course cannot be different lengths. Kept in sync with
# EndConditionOverridesSO.DefaultRedlineGateTarget.
LAPS = 3
RINGS_PER_LAP = 8                 # RedlineCourse.ForIntensity's GateCount
GATE_TARGET = LAPS * RINGS_PER_LAP

# The circuit's base circle. A regular octagon at 820 has 628u legs, so one lap is ~5k units
# and the race ~15k: a Manta that holds Soar covers it in about 21 s at 720 u/s; one that
# gives it up at every corner spends two seconds winning each one back.
BASE_RADIUS = 820

# AI approach geometry, sized to the Manta rather than inherited from a Dolphin at 347 u/s:
# the commit distance sits comfortably outside the 237u full-boost circle so a bot that is
# still lining up has room to turn, the lead is about two circles back along the axis, and
# the through point is far enough past the mouth that the bot does not swing early.
AI_COMMIT_DISTANCE = 420
AI_APPROACH_LEAD = 480
AI_THROUGH_DISTANCE = 320

# Detection clamp: a single frame's motion longer than this x dt x 2 + 5 is treated as a
# respawn. A Time-10 Manta at 936 u/s steps 31.2u per 30 fps frame; the inherited 400 rejects
# that within a unit. 1400 leaves a knockback from a crystal blast inside the clamp too.
MAX_PLAUSIBLE_SPEED = 1400

# The comeback strength, and it is a FUNCTION OF THE TARGET - `bonusLevels = deficit x rate` -
# so a rate only means anything next to the scale of deficits the mode produces (the trap
# DOGFIGHT.md, BENDS.md, WILDLIFE_LIBERATION.md, SWITCHBACK.md and HEADLONG.md each record).
# At 0.35: six gates behind (a quarter of the race) buys 2.1 element levels - and on THIS hull
# a Time level is speed outright (Soar's map multiplier), so the buff lands on the mode's axis.
COMEBACK_RATE = 0.35


_HEADER_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: __GUID__, type: 3}
  m_Name: __NAME__
  m_EditorClassIdentifier:
"""


def HEADER_FOR(script_guid: str, name: str) -> str:
    return _HEADER_TMPL.replace("__GUID__", script_guid).replace("__NAME__", name)


def meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def asset_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


files = {}


def emit(rel, content):
    files[rel] = content


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


# ── 1. .cs.meta for the new scripts ──────────────────────────────────────────
SCRIPT_PATHS = {
    "RedlineController":  "Assets/_Scripts/Controller/Arcade/Redline/RedlineController.cs",
    "RedlineCourse":      "Assets/_Scripts/Controller/Arcade/Redline/RedlineCourse.cs",
    "RedlineCourseTests": "Assets/_Scripts/Tests/Editor/RedlineCourseTests.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))

# The mode doc is an ASSET too - without a .meta Unity mints a fresh GUID on every machine.
emit("Assets/_Scripts/Controller/Arcade/REDLINE.md.meta",
     f"fileFormatVersion: 2\nguid: {guid('doc/REDLINE.md')}\nTextScriptImporter:\n"
     f"  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

emit("Assets/_Scripts/Controller/Arcade/Redline.meta",
     f"fileFormatVersion: 2\nguid: {guid('folder/Redline')}\nfolderAsset: yes\n"
     f"DefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n"
     f"  assetBundleVariant:\n")


# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 9 = ScoringMetric.SwitchesThreaded, REUSED rather than added, exactly as Headlong
# reused it: the metric, the BestByDomain fold, the goal-stack row and the objective icon all
# come for free (CLAUDE.md: keyed on the METRIC, never on the game mode). Golf: the winning
# domain's pilots carry a finish time, everyone else a sentinel, so lower is better.
emit("Assets/_SO_Assets/Scoring Rules/RedlineScoringRule.asset",
     HEADER_FOR(EXISTING["GateRaceScoringRuleSO"], "RedlineScoringRule") +
     "  metric: 9\n  golfRules: 1\n")
emit("Assets/_SO_Assets/Scoring Rules/RedlineScoringRule.asset.meta",
     asset_meta(G_ASSET["RedlineScoringRule"]))


# ── Mode id: READ, never hardcoded ───────────────────────────────────────────
_ENUM = read("Assets/_Scripts/Data/Enums/GameModes.cs")
_m = re.search(r"^\s*Redline\s*=\s*(\d+)\s*,", _ENUM, re.M)
assert _m, "GameModes.Redline not found - has the member been renamed?"
MODE_ID = int(_m.group(1))


# ── 3. Arcade game config ────────────────────────────────────────────────────
# MANTA ONLY: a single entry in Vessels drives all three enforcement layers (the launcher clamp,
# the server-side spawn clamp, and the AI clamp).
#
# MinPlayersAllowed 2 / MinDomainsAllowed 2 because a race needs a rival: with one domain the
# objective is reached the moment anyone finishes and there is nobody to have beaten.
emit("Assets/_SO_Assets/Games/ArcadeGameRedline.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameRedline") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Redline
  Description: Mantas only, flat out. Both triggers buried is four times cruise and a
    turning circle you can hold through most of the lap - but the corners that will not
    take it are cut to the metre, and easing a trigger to make one costs you two seconds
    of winding the Soar back up. Laps, until somebody stops lifting.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameRedline
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Manta']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  CallToActionTargetType: 404
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameRedline.asset.meta",
     asset_meta(G_ASSET["ArcadeGameRedline"]))


# ── 4. Scene: clone MinigameHeadlong, swap the controller ────────────────────
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameHeadlong.unity")

# 4a. controller script swap
scene, n = re.subn(EXISTING["HeadlongController"], G_SCRIPT["RedlineController"], scene)
assert n == 1, f"controller guid appeared {n} times in the donor scene"

# 4b. its serialized field block: the rule asset, the AI numbers and the speed clamp move; the
# shell, the lap count and the course seed stay.
OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['HeadlongScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: 3
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: 260
  aiApproachLead: 300
  aiThroughDistance: 220
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: 400
  reportResyncSeconds: 3
"""
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['RedlineScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: {LAPS}
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: {AI_COMMIT_DISTANCE}
  aiApproachLead: {AI_APPROACH_LEAD}
  aiThroughDistance: {AI_THROUGH_DISTANCE}
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: {MAX_PLAUSIBLE_SPEED}
  reportResyncSeconds: 3
"""
assert scene.count(OLD_FIELDS) == 1, "controller field block not found in donor scene"
scene = scene.replace(OLD_FIELDS, NEW_FIELDS)

# 4c. Everything ELSE the donor authored is already what this mode wants, stated rather than
# left as an absence:
#   - the CELL is the barren race cell (one config) - intensity is the COURSE here too.
#   - the SPAWN RING is EQUATORIAL at 150 outside the nucleus: gate 0 is rotated onto its POLE,
#     and only a point on the axis is equidistant from every pilot.
#   - the COMEBACK source is 8 (SwitchesThreaded) and useGolfRules 1 - same metric, same
#     direction, same kind of race.
#   - the AI roster's vesselClass is irrelevant: the card's Vessels list clamps every AI to the
#     Manta (ServerPlayerVesselInitializerWithAI -> GameDataSO.ClampVesselToGame).
for probe, why in ((r"^  spawnFormation: 1$", "equatorial spawn ring"),
                   (r"^  differenceSource: 8$", "comeback reads SwitchesThreaded"),
                   (r"^  useGolfRules: 1$", "comeback golf direction"),
                   (r"^  cellTypeChoiceOptions: 0$", "single race cell")):
    assert re.search(probe, scene, re.M), f"donor no longer provides: {why}"

emit("Assets/_Scenes/Multiplayer Scenes/MinigameRedline.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameRedline.unity.meta",
     scene_meta(G_ASSET["MinigameRedline.unity"]))


# ── 5. Register the card in BOTH rosters ────────────────────────────────────
# Docs/HomeHub/ARCHITECTURE.md §3.1: OrganicRematchGames is the MASTER (the injected list a
# guest resolves the host's card through), and ArcadeGames is the Arcade grid's
# rosterOverride — "the master minus the arena cards". A card registered in the master alone
# launches for a guest and is invisible in the arcade, which is exactly how Redline shipped
# the first time (and how Breakwater / Skein / Bloomrush arrived from their own branches).
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameRedline']}, type: 2}}\n"
for LIST_PATH in ("Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset",
                  "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset"):
    games = read(LIST_PATH)
    if entry not in games:
        assert games.endswith("\n")
        games = games + entry
    assert games.count(entry) == 1, f"the Redline card is listed more than once in {LIST_PATH}"
    emit(LIST_PATH, games)


# ── 6. Always-unlocked so the card is clickable on a fresh account ──────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(rf"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - {MODE_ID}\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", rf"\g<1>  - {MODE_ID}\n",
                      prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 7. Build settings ───────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameRedline.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameBloomrush\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Bloomrush scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameRedline.unity\n"
                          f"    guid: {G_ASSET['MinigameRedline.unity']}\n")
emit(BUILD_PATH, build)


# ── 8. End-game condition target ────────────────────────────────────────────
# SET rather than insert-if-absent, so a re-run after a retune actually moves the number the
# game reads (the Dog Fight generator's lesson). Inserted after the Headlong keys, matching the
# C# declaration order.
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("headlongGateTarget", "redlineGateTarget"),
                          ("headlongGateTargetBuild", "redlineGateTargetBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0), f"  {new_key}: {GATE_TARGET}\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH} - run author_headlong_assets.py first"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {GATE_TARGET}\n", 1)
emit(END_PATH, endcond)

# The goal-stack row and the launch panel's objective icon are keyed on metric 9, which
# author_switchback_assets.py already authored. Nothing to do here, and deliberately so.


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

if 0.25 * GATE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(
        f"comeback rate {COMEBACK_RATE} is dead against target {GATE_TARGET}: a "
        f"quarter-of-target deficit buys {0.25 * GATE_TARGET * COMEBACK_RATE:.2f} element "
        f"levels (< 1). Rescale the rate with the target.")

if GATE_TARGET % LAPS != 0:
    errors.append(f"race target {GATE_TARGET} is not a whole number of {LAPS} laps")

_course = read("Assets/_Scripts/Controller/Arcade/Redline/RedlineCourse.cs")
m = re.search(r"GateCount\s*=\s*(\d+),", _course)
if not m or int(m.group(1)) != RINGS_PER_LAP:
    errors.append(f"RedlineCourse.ForIntensity GateCount is not {RINGS_PER_LAP}")
m = re.search(r"BaseRadius\s*=\s*(\d+)f,", _course)
if not m or int(m.group(1)) != BASE_RADIUS:
    errors.append(f"RedlineCourse.ForIntensity BaseRadius is not {BASE_RADIUS}")

_end = read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultRedlineGateTarget\s*=\s*(\d+);", _end)
if not m or int(m.group(1)) != GATE_TARGET:
    errors.append(f"EndConditionOverridesSO.DefaultRedlineGateTarget is not {GATE_TARGET}")

_ctrl = read("Assets/_Scripts/Controller/Arcade/Redline/RedlineController.cs")
m = re.search(r"int laps\s*=\s*(\d+);", _ctrl)
if not m or int(m.group(1)) != LAPS:
    errors.append(f"RedlineController.laps default is not {LAPS}")

for field, want in (("courseOuterRadius", "1080"), ("courseInnerRadiusFallback", "480")):
    if f"  {field}: {want}\n" not in NEW_FIELDS:
        errors.append(f"scene shell {field} is not {want}")
_tests = read("Assets/_Scripts/Tests/Editor/RedlineCourseTests.cs")
for const, want in (("Inner", "480"), ("Outer", "1080")):
    m = re.search(rf"const float {const} = ([0-9.]+)f;", _tests)
    if not m or float(m.group(1)) != float(want):
        errors.append(f"RedlineCourseTests.{const} is not {want}")

# The speed clamp must clear a Time-10 Manta at 30 fps with the clamp's own margin removed:
# maxStep = speed x dt x 2 + 5 must exceed 936 x dt.
if MAX_PLAUSIBLE_SPEED * 2 <= 936:
    errors.append("maxPlausibleSpeed does not clear a Time-10 Manta")

# GUID hygiene: nothing this script mints may collide with an asset it does not own.
all_new = set(G_SCRIPT.values()) | set(G_ASSET.values()) | {guid("doc/REDLINE.md"), guid("folder/Redline")}
owned_metas = {os.path.join(ROOT, p) for p in files if p.endswith(".meta")}
existing_guids = set()
for dirpath, _dirs, fnames in os.walk(os.path.join(ROOT, "Assets")):
    for fn in fnames:
        if not fn.endswith(".meta"):
            continue
        full = os.path.join(dirpath, fn)
        if full in owned_metas:
            continue
        try:
            with open(full, encoding="utf-8", errors="ignore") as fh:
                m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
            if m:
                existing_guids.add(m.group(1))
        except OSError:
            pass
for g in all_new:
    if g in existing_guids:
        errors.append(f"minted GUID {g} collides with an asset this script does not own")
for name, g in EXISTING.items():
    if g not in existing_guids:
        errors.append(f"referenced GUID for {name} ({g}) does not resolve to any asset")
for k, p in SCRIPT_PATHS.items():
    if not os.path.exists(os.path.join(ROOT, p)):
        errors.append(f"script {p} does not exist")

if errors:
    print("VALIDATION FAILED:")
    for e in errors:
        print("  -", e)
    sys.exit(1)

# ══ WRITE / CHECK ═══════════════════════════════════════════════════════════
if CHECK_ONLY:
    differing = []
    for rel, content in sorted(files.items()):
        full = os.path.join(ROOT, rel)
        if not os.path.exists(full):
            differing.append(rel + "  (missing)")
            continue
        with open(full, encoding="utf-8") as fh:
            if fh.read() != content:
                differing.append(rel)
    if differing:
        print(f"--check: {len(differing)} file(s) differ from the authored output:")
        for d in differing:
            print("  -", d)
        sys.exit(1)
    print(f"--check: OK ({len(files)} files match)")
    sys.exit(0)

for rel, content in sorted(files.items()):
    full = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8") as fh:
        fh.write(content)
    print("wrote", rel)
print(f"\nRedline: {len(files)} files authored. Race = {LAPS} laps x {RINGS_PER_LAP} rings "
      f"= {GATE_TARGET} gates.")
