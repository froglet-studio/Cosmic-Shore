#!/usr/bin/env python3
"""
Authors every serialized asset the Headlong game mode needs (GameModes.Headlong = 48).

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_headlong_assets.py [--check]

--check validates without writing (CI / pre-commit use).

WHAT THIS MODE IS. Headlong is the Rhino-only CIRCUIT race: a closed loop of switch rings cut
through the cell, flown in LAPS by every pilot, and the first DOMAIN whose LEAD RUNNER threads
the last gate of the last lap wins. Every corner is cut against the Rhino's FLAT-OUT turn radius
- the tightest circle it can fly WITHOUT dropping the ramp boost - so a corner is a decision
rather than a chore. See Assets/_Scripts/Controller/Arcade/HEADLONG.md.

THE DONOR IS SWITCHBACK, and that is the point rather than a shortcut. Switchback is already a
gate race in a barren race cell with an EQUATORIAL spawn ring, which is precisely the fairness
rule this mode needs too (gate 0 sits on that ring's pole). Since the Headlong branch the two
share a controller base, a ring, a turn monitor, an objective provider and a scoring rule CLASS -
so the clone swaps three things and inherits the rest:

  1. the controller script (HeadlongController), and its field block: the scoring rule asset,
     firstGateDistance dropped (a circuit has no first leg to place), and `laps` added.
  2. NOTHING about the cell, the spawn ring or the comeback source - all four are already what
     this mode wants, including differenceSource 8 (SwitchesThreaded), which is the same metric.
  3. the arcade card's Vessels list: Rhino, not Dolphin.

THE SCORING RULE IS A SECOND ASSET, NOT A SECOND CLASS. GateRaceScoringRuleSO is entirely
generic - golf-timed, folded per domain by the LEAD RUNNER - so Headlong points a new asset at
the same script. A subclass would only have been somewhere for the two to drift apart.
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
    "HeadlongController":  guid("script/HeadlongController"),
    "HeadlongCircuit":     guid("script/HeadlongCircuit"),
    "HeadlongCircuitTests": guid("script/HeadlongCircuitTests"),
    # Shared with Switchback, introduced on this branch. Minted here because this script is the
    # one that creates them; Switchback's generator references them by name and never mints.
    "GateRaceController":  guid("script/GateRaceController"),
    "RaceCourseGeometry":  guid("script/RaceCourseGeometry"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameHeadlong":    guid("asset/ArcadeGameHeadlong"),
    "HeadlongScoringRule":   guid("asset/HeadlongScoringRule"),
    "MinigameHeadlong.unity": guid("asset/MinigameHeadlong.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    "SO_ArcadeGame":          "fe040efad3307fb449b6b72ad15362da",
    # the shared gate-race scoring rule SCRIPT (one class, one asset per mode)
    "GateRaceScoringRuleSO":  "349cc0c9402590262de23356775d43cc",
    # donor scene wiring to swap out
    "SwitchbackController":   "232714002ed12bc27251e8ac09358ff0",
    "SwitchbackScoringRule":  "b67719b27d5ca31d65b90aec65674945",
    # shared content
    "Vessel_Rhino":           "ec97e344adb08f847a8f7649ab79088e",
    # arcade card art - shared with the other pure-race cards
    "IconActive":             "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":           "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground":         "587d2203114c8004c9985d0112c89585",
}

# ── Tuning ───────────────────────────────────────────────────────────────────

# The RACE LENGTH in gate threadings - LAPS x RINGS. One number: the controller divides by LAPS
# to size the circuit and the turn monitor asks the controller for the target, so the finish
# line and the course cannot be different lengths. Kept in sync with
# EndConditionOverridesSO.DefaultHeadlongGateTarget.
LAPS = 3
RINGS_PER_LAP = 8                 # HeadlongCircuitSettings.ForIntensity's GateCount
GATE_TARGET = LAPS * RINGS_PER_LAP

# The circuit's base circle. A regular octagon at 800 has 612u legs, so one lap is ~4.9k units
# and the race is ~14.7k. A Rhino that holds the ramp boost covers that at up to 910 u/s; one
# that keeps losing it crawls at 60. That spread IS the mode, and it is why the race is short.
BASE_RADIUS = 800

# The comeback strength, and it is a FUNCTION OF THE TARGET - `bonusLevels = deficit x rate` -
# so a rate only means anything next to the scale of deficits the mode produces. This is the trap
# DOGFIGHT.md, BENDS.md, WILDLIFE_LIBERATION.md and SWITCHBACK.md have each recorded
# independently, so the assert below fails the build rather than trusting the number.
#
# At 0.35: six gates behind (a quarter of the race) buys 2.1 element levels. Measured on the LEAD
# RUNNER, so a trailing domain is genuinely behind on the same circuit.
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
    "HeadlongController":   "Assets/_Scripts/Controller/Arcade/Headlong/HeadlongController.cs",
    "HeadlongCircuit":      "Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs",
    "HeadlongCircuitTests": "Assets/_Scripts/Tests/Editor/HeadlongCircuitTests.cs",
    "GateRaceController":   "Assets/_Scripts/Controller/Arcade/Racing/GateRaceController.cs",
    "RaceCourseGeometry":   "Assets/_Scripts/Controller/Arcade/Racing/RaceCourseGeometry.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))

# The mode doc is an ASSET too - without a .meta Unity mints a fresh GUID on every machine.
emit("Assets/_Scripts/Controller/Arcade/HEADLONG.md.meta",
     f"fileFormatVersion: 2\nguid: {guid('doc/HEADLONG.md')}\nTextScriptImporter:\n"
     f"  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

for folder in ("Headlong", "Racing"):
    emit(f"Assets/_Scripts/Controller/Arcade/{folder}.meta",
         f"fileFormatVersion: 2\nguid: {guid('folder/' + folder)}\nfolderAsset: yes\n"
         f"DefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n"
         f"  assetBundleVariant:\n")


# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 9 = ScoringMetric.SwitchesThreaded, REUSED rather than added: a lapped circuit's gates
# are threaded in order exactly as an open chain's are, so the metric, the BestByDomain fold,
# the goal-stack row and the objective icon all come for free (CLAUDE.md: keyed on the METRIC,
# never on the game mode). Golf: the winning domain's pilots carry a finish time, everyone else
# a sentinel, so lower is better.
emit("Assets/_SO_Assets/Scoring Rules/HeadlongScoringRule.asset",
     HEADER_FOR(EXISTING["GateRaceScoringRuleSO"], "HeadlongScoringRule") +
     "  metric: 9\n  golfRules: 1\n")
emit("Assets/_SO_Assets/Scoring Rules/HeadlongScoringRule.asset.meta",
     asset_meta(G_ASSET["HeadlongScoringRule"]))


# ── 3. Arcade game config ────────────────────────────────────────────────────
# RHINO ONLY: a single entry in Vessels drives all three enforcement layers (the launcher clamp,
# the server-side spawn clamp, and the AI clamp).
#
# MinPlayersAllowed 2 / MinDomainsAllowed 2 because a race needs a rival: with one domain the
# objective is reached the moment anyone finishes and there is nobody to have beaten.
emit("Assets/_SO_Assets/Games/ArcadeGameHeadlong.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameHeadlong") + f"""  Mode: 48
  IsMultiplayer: 1
  DisplayName: Headlong
  Description: Rhinos only, on a circuit that never lets go. Hold full throttle dead
    straight and the ramp winds you up to fifteen times cruise - but the boost dies the
    moment you steer, and every corner is cut just wide enough to take flat out if your
    hands are quiet enough. Laps until somebody's nerve breaks.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameHeadlong
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Rhino']}, type: 2}}
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
emit("Assets/_SO_Assets/Games/ArcadeGameHeadlong.asset.meta",
     asset_meta(G_ASSET["ArcadeGameHeadlong"]))


# ── 4. Scene: clone MinigameSwitchback, swap the controller ──────────────────
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameSwitchback.unity")

# 4a. controller script swap
scene, n = re.subn(EXISTING["SwitchbackController"], G_SCRIPT["HeadlongController"], scene)
assert n == 1, f"controller guid appeared {n} times in the donor scene"

# 4b. its serialized field block. firstGateDistance goes (a closed circuit has no first leg to
# place - gate 0 is rotated onto the spawn pole instead); `laps` arrives.
OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['SwitchbackScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  firstGateDistance: 620
"""
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['HeadlongScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: {LAPS}
"""
assert scene.count(OLD_FIELDS) == 1, "controller field block not found in donor scene"
scene = scene.replace(OLD_FIELDS, NEW_FIELDS)

# 4c. Everything ELSE the donor authored is already what this mode wants, and that is worth
# stating rather than leaving as an absence:
#   - the CELL is the barren race cell (one config, cellTypeChoiceOptions 0) - intensity is the
#     COURSE here too, so an IntensityWise ladder would make it mean two things at once.
#   - the SPAWN RING is EQUATORIAL at 150 outside the nucleus, which is the fairness rule: gate 0
#     is rotated onto that ring's POLE, and only a point on the axis is equidistant from every
#     pilot.
#   - the COMEBACK source is 8 (SwitchesThreaded) and useGolfRules 1 - the same metric and the
#     same direction, because it is the same kind of race.
for probe, why in ((r"^  spawnFormation: 1$", "equatorial spawn ring"),
                   (r"^  differenceSource: 8$", "comeback reads SwitchesThreaded"),
                   (r"^  useGolfRules: 1$", "comeback golf direction"),
                   (r"^  cellTypeChoiceOptions: 0$", "single race cell")):
    assert re.search(probe, scene, re.M), f"donor no longer provides: {why}"

emit("Assets/_Scenes/Multiplayer Scenes/MinigameHeadlong.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameHeadlong.unity.meta",
     scene_meta(G_ASSET["MinigameHeadlong.unity"]))


# ── 5. Register the card in the party-games list ─────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameHeadlong']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
assert games.count(entry) == 1, "the Headlong card is listed more than once in OrganicRematchGames"
emit(LIST_PATH, games)


# ── 6. Always-unlocked so the card is clickable on a fresh account ──────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 48\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 48\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 7. Build settings ───────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameHeadlong.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameSwitchback\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Switchback scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameHeadlong.unity\n"
                          f"    guid: {G_ASSET['MinigameHeadlong.unity']}\n")
emit(BUILD_PATH, build)


# ── 8. End-game condition target ────────────────────────────────────────────
# SET rather than insert-if-absent, so a re-run after a retune actually moves the number the
# game reads (the Dog Fight generator's lesson).
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("switchbackGateTarget", "headlongGateTarget"),
                          ("switchbackGateTargetBuild", "headlongGateTargetBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0), f"  {new_key}: {GATE_TARGET}\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH} - run author_switchback_assets.py first"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {GATE_TARGET}\n", 1)
emit(END_PATH, endcond)

# The goal-stack row and the launch panel's objective icon are keyed on metric 9, which
# author_switchback_assets.py already authored. Nothing to do here, and deliberately so: a
# second write of the same row is how two generators come to disagree about one glyph.


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

# The comeback rate is meaningless without the target next to it - see COMEBACK_RATE.
if 0.25 * GATE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(
        f"comeback rate {COMEBACK_RATE} is dead against target {GATE_TARGET}: a "
        f"quarter-of-target deficit buys {0.25 * GATE_TARGET * COMEBACK_RATE:.2f} element "
        f"levels (< 1). Rescale the rate with the target.")

# The race length must be a whole number of laps, or the last lap ends mid-circuit and the
# course the controller lays is not the course the monitor counts.
if GATE_TARGET % LAPS != 0:
    errors.append(f"race target {GATE_TARGET} is not a whole number of {LAPS} laps")

# The C# and this script must agree about the circuit's size and the default target.
_circuit = read("Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs")
m = re.search(r"GateCount\s*=\s*(\d+),", _circuit)
if not m or int(m.group(1)) != RINGS_PER_LAP:
    errors.append(f"HeadlongCircuitSettings.GateCount is not {RINGS_PER_LAP}")
m = re.search(r"BaseRadius\s*=\s*(\d+)f,", _circuit)
if not m or int(m.group(1)) != BASE_RADIUS:
    errors.append(f"HeadlongCircuitSettings.BaseRadius is not {BASE_RADIUS}")

_end = read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultHeadlongGateTarget\s*=\s*(\d+);", _end)
if not m or int(m.group(1)) != GATE_TARGET:
    errors.append(f"EndConditionOverridesSO.DefaultHeadlongGateTarget is not {GATE_TARGET}")

# The controller's authored lap count must match this script's, or the circuit and the finish
# line are sized from two different numbers.
_ctrl = read("Assets/_Scripts/Controller/Arcade/Headlong/HeadlongController.cs")
m = re.search(r"int laps\s*=\s*(\d+);", _ctrl)
if not m or int(m.group(1)) != LAPS:
    errors.append(f"HeadlongController.laps default is not {LAPS}")

# The shell the scene authors, the shell the tests sweep, and the C# defaults are three copies
# of two numbers. Tie them here (the SHELL check Switchback's generator records).
for field, want in (("courseOuterRadius", "1080"), ("courseInnerRadiusFallback", "480")):
    if f"  {field}: {want}\n" not in NEW_FIELDS:
        errors.append(f"scene shell {field} is not {want}")
_tests = read("Assets/_Scripts/Tests/Editor/HeadlongCircuitTests.cs")
for const, want in (("Inner", "480"), ("Outer", "1080")):
    m = re.search(rf"const float {const} = ([0-9.]+)f;", _tests)
    if not m or float(m.group(1)) != float(want):
        errors.append(f"HeadlongCircuitTests.{const} is not {want}")

# GUID hygiene: nothing this script mints may collide with an asset it does not own.
all_new = set(G_SCRIPT.values()) | set(G_ASSET.values()) | {guid("doc/HEADLONG.md")} | \
          {guid("folder/Headlong"), guid("folder/Racing")}
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
print(f"\nHeadlong: {len(files)} files authored. Race = {LAPS} laps x {RINGS_PER_LAP} rings "
      f"= {GATE_TARGET} gates.")
