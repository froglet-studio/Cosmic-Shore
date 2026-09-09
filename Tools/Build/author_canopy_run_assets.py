#!/usr/bin/env python3
"""
Authors every serialized asset Canopy Run needs (GameModes.CanopyRun = 45): the Gibbon-only
brachiation race. Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"),
so re-running produces byte-identical output. Validates the whole result in memory, then writes.

Run from the repo root:  python3 Tools/Build/author_canopy_run_assets.py [--check]

--check validates without writing and reports any file that differs from the authored output.

WHAT THIS MODE IS. Skim Race's domain crystal race, played by a vessel that SWINGS. The donor
scene (MinigameSkimRace.unity) is cloned as text and exactly three things change:

  1. the controller script - SkimRaceController -> CanopyRunController (a subclass adding no
     behaviour; the class is the scene's identity and where any Canopy-Run-only rule lands);
  2. the track script - SpawnableWaypointTrack -> SpawnableCanopyTrack (a subclass that grows
     BOUGHS alternately left/right of the SAME waypoint line and a prism HOOP around every crystal
     spot, instead of a skimmable ribbon), with its new fields authored EXPLICITLY here - Unity
     fills an absent field from the class initializer with no log, which is how a tuned number
     ships as a default;
  3. the crystal manager's anchor sets - RE-AUTHORED ONTO THE TRACK WAYPOINTS. The donor's own
     per-intensity crystal sets differ from its waypoints (81/81/46u mean offsets; at intensity 3
     there are 14 anchors against 28 waypoints), which never mattered for a ribbon you skim along
     and matters completely for a hoop laid AROUND the crystal. One dataset, asserted equal, and
     the spawn jitter shrunk so a crystal always lands inside its hoop.

The AI templates' vesselClass swap (6 -> 13) is hygiene: what actually picks the AI hull is the
card's single Vessels entry (ServerPlayerVesselInitializerWithAI.PickAIVesselType reads it), which
is also what locks the launcher and the server-side spawn clamp to the Gibbon.

Geometry is derived from the Gibbon's BEAT (see Assets/_Scripts/Controller/Vessel/R_VesselActions/GIBBON.md):
latch-to-latch at 150-250 u/s on the swing-rate-floored line is 0.6-1.3 s, i.e. ~180u along the
line (~360u same-side), and the resting aim yaw at race speed lands an abeam anchor ~80u out.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv


def guid(name: str) -> str:
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


G_SCRIPT = {
    "CanopyRunController":  guid("script/CanopyRunController"),
    "SpawnableCanopyTrack": guid("script/SpawnableCanopyTrack"),
}
G_ASSET = {
    "ArcadeGameCanopyRun":     guid("asset/ArcadeGameCanopyRun"),
    "MinigameCanopyRun.unity": guid("asset/MinigameCanopyRun.unity"),
}
EXISTING = {
    "SO_ArcadeGame":          "fe040efad3307fb449b6b72ad15362da",
    "SkimRaceController":     "7c31e7782a274137a50ffa78479c4a35",
    "SpawnableWaypointTrack": "528998d6a59dd7c4b9b1f0290145ef51",
    "SkimRaceScoringRule":    "63bafccfd69743c086ad1bdc7623cc4c",
    "Vessel_Gibbon":          "0f8b832bb0854eb3bfe15bcc826e25c2",   # SO_Class_Spider.asset (Class 13, Name Gibbon)
    # card art - Skim Race's, the sibling race
    "IconActive":     "9842120c8e297c64d8e6e9bed82feeb5",
    "IconInactive":   "d7304af54e4930545a2bafd4c4b193f1",
    "CardBackground": "bd7a8cdc016c46b48ac0e048f42cf948",
}

MODE_ID = 45
GIBBON_CLASS = 13
SQUIRREL_CLASS = 6

# ── Canopy geometry (from the beat) ─────────────────────────────────────────
BOUGH_SPACING = 180.0          # along the line; alternating sides -> ~360u same-side
BOUGH_LATERAL_OFFSET = 80.0    # the resting aim yaw at race speed lands here
BOUGH_PRISM_COUNT = 3
BOUGH_PRISM_SPACING = 12.0
BOUGH_PRISM_SCALE = (6.0, 6.0, 6.0)
BOUGH_VERTICAL_STAGGER = 0.0
HOOP_PRISM_COUNT = 8
HOOP_RADIUS = 34.0
HOOP_PRISM_SCALE = (4.0, 4.0, 9.0)
GUIDE_SPACING = 40.0
GUIDE_PRISM_SCALE = (1.5, 1.5, 6.0)

# The crystal's spawn jitter around its anchor, and the radius a crystal renders at. The hoop must
# clear both or a jittered crystal spawns inside the hoop wall. 35 was the donor's value.
CRYSTAL_JITTER = 12.0
CRYSTAL_RADIUS = 15.0
HOOP_CLEARANCE_MARGIN = 5.0

# Comeback: bonusLevels = deficit x rate. Targets are waypoints x laps per intensity (auto-calc,
# canopyRunCrystalCount 0) - the smallest is what the assert below checks against.
COMEBACK_RATE = 0.5
LAPS_PER_INTENSITY = [3, 3, 2, 2]   # inherited verbatim from the donor's turn monitor

# Collider budget ceiling per intensity (every canopy prism is super-shielded: one stellated
# octahedron each). Peel the Cage runs 10-20k; this mode is two orders of magnitude lighter.
PRISM_BUDGET_CEILING = 2000

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
            f"  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def asset_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


files: "dict[str, str]" = {}


def emit(rel: str, content: str):
    files[rel] = content


def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


def v3(t):
    return "{x: %g, y: %g, z: %g}" % t


# ── 1. .cs.meta for the new scripts ──────────────────────────────────────────
SCRIPT_PATHS = {
    "CanopyRunController":  "Assets/_Scripts/Controller/Arcade/CanopyRunController.cs",
    "SpawnableCanopyTrack": "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableCanopyTrack.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))


# ── 2. Arcade game card ─────────────────────────────────────────────────────
# GIBBON ONLY: the single Vessels entry drives all three enforcement layers (launcher clamp,
# server-side spawn clamp, AI hull pick). Solo play is AI backfill, like Skim Race.
emit("Assets/_SO_Assets/Games/ArcadeGameCanopyRun.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameCanopyRun") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Canopy Run
  Description: Gibbons only. Two arms, two triggers - cast a line at a bough, let the
    snap turn you, reel to build speed, and let go where you want to fly. The boughs
    alternate left and right of the line and every crystal sits inside a hoop, so the
    rhythm IS the racing line. First domain to collect the crystals wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameCanopyRun
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Gibbon']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 1
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  CallToActionTargetType: 0
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameCanopyRun.asset.meta", asset_meta(G_ASSET["ArcadeGameCanopyRun"]))


# ── 3. Scene: clone MinigameSkimRace, swap the mode-specific wiring ─────────
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameSkimRace.unity")

# 3a. controller script swap (the serialized field block is inherited verbatim: same fields)
scene, n = re.subn(EXISTING["SkimRaceController"], G_SCRIPT["CanopyRunController"], scene)
assert n == 1, f"controller guid appeared {n} times"

# 3b. track script swap + the canopy field block, authored explicitly
scene, n = re.subn(EXISTING["SpawnableWaypointTrack"], G_SCRIPT["SpawnableCanopyTrack"], scene)
assert n == 1, f"track guid appeared {n} times"
OLD_TRACK_TAIL = "  previewIntensityLevel: 0\n"
NEW_TRACK_TAIL = f"""  previewIntensityLevel: 0
  boughPrism: {{fileID: 0}}
  boughSpacing: {BOUGH_SPACING:g}
  boughLateralOffset: {BOUGH_LATERAL_OFFSET:g}
  boughPrismCount: {BOUGH_PRISM_COUNT}
  boughPrismSpacing: {BOUGH_PRISM_SPACING:g}
  boughPrismScale: {v3(BOUGH_PRISM_SCALE)}
  boughVerticalStagger: {BOUGH_VERTICAL_STAGGER:g}
  hoopPrismCount: {HOOP_PRISM_COUNT}
  hoopRadius: {HOOP_RADIUS:g}
  hoopPrismScale: {v3(HOOP_PRISM_SCALE)}
  layGuideVine: 1
  guideSpacing: {GUIDE_SPACING:g}
  guidePrismScale: {v3(GUIDE_PRISM_SCALE)}
"""
assert scene.count(OLD_TRACK_TAIL) == 1, "track block tail not found exactly once in the donor scene"
scene = scene.replace(OLD_TRACK_TAIL, NEW_TRACK_TAIL)

# 3c. crystal anchors := the track waypoints, per intensity (one dataset)
wp_match = re.search(r"^  waypoints:\n((?:  - positions:\n(?:    - \{x: [^}]*\}\n)+)+)  useSplinePerIntensity:", scene, re.M)
assert wp_match, "waypoint sets not found in the track block"
waypoint_block = wp_match.group(1)
sets = re.findall(r"  - positions:\n((?:    - \{x: [^}]*\}\n)+)", waypoint_block)
assert len(sets) == 4, f"expected 4 waypoint sets, found {len(sets)}"
waypoint_counts = [len(re.findall(r"^    - \{x:", s_, re.M)) for s_ in sets]

cm = re.search(r"^  listOfCrystalPositions:\n(?:  - positions:\n(?:    - \{x: [^}]*\}\n)+)+  anchorJitterRadius: (\d+)\n", scene, re.M)
assert cm, "crystal manager anchor block not found"
scene = scene.replace(cm.group(0), "  listOfCrystalPositions:\n" + waypoint_block + f"  anchorJitterRadius: {CRYSTAL_JITTER:g}\n", 1)

# 3d. AI templates: hygiene (the card's single vessel is what picks the AI hull)
scene, n = re.subn(rf"^  - vesselClass: {SQUIRREL_CLASS}$", f"  - vesselClass: {GIBBON_CLASS}", scene, flags=re.M)
assert n == 4, f"expected 4 AI templates, rewrote {n}"

emit("Assets/_Scenes/Multiplayer Scenes/MinigameCanopyRun.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameCanopyRun.unity.meta", scene_meta(G_ASSET["MinigameCanopyRun.unity"]))


# ── 4. Registration ────────────────────────────────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameCanopyRun']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
emit(LIST_PATH, games)

PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(rf"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - {MODE_ID}\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", rf"\g<1>  - {MODE_ID}\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)

BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameCanopyRun.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameSkimRace\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Skim Race scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameCanopyRun.unity\n"
                          f"    guid: {G_ASSET['MinigameCanopyRun.unity']}\n")
emit(BUILD_PATH, build)

# End condition keys: 0 = auto-calc (waypoints x laps), the Skim Race model. SET, not insert-if-absent.
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("hexRaceCrystalCount", "canopyRunCrystalCount"),
                          ("hexRaceCrystalCountBuild", "canopyRunCrystalCountBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0), f"  {new_key}: 0\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH}"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: 0\n", 1)
emit(END_PATH, endcond)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

# The hoop must clear the crystal's whole spawn volume.
if HOOP_RADIUS < CRYSTAL_JITTER + CRYSTAL_RADIUS + HOOP_CLEARANCE_MARGIN:
    errors.append(f"hoopRadius {HOOP_RADIUS} < jitter {CRYSTAL_JITTER} + crystal {CRYSTAL_RADIUS} + margin {HOOP_CLEARANCE_MARGIN}: a crystal can spawn in the hoop wall")

# Comeback rate vs the SMALLEST target (a quarter-of-target deficit must buy a whole level).
targets = [c * l for c, l in zip(waypoint_counts, LAPS_PER_INTENSITY)]
if 0.25 * min(targets) * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against the smallest target {min(targets)}")

# Collider budget: mirror SpawnableCanopyTrack's layout counts from the waypoint geometry.
def polyline_length(set_text):
    pts = [tuple(float(v) for v in re.findall(r"[xyz]: (-?[\d.]+)", ln)) for ln in set_text.strip().splitlines()]
    return sum(((pts[i][0] - pts[(i + 1) % len(pts)][0]) ** 2 + (pts[i][1] - pts[(i + 1) % len(pts)][1]) ** 2
                + (pts[i][2] - pts[(i + 1) % len(pts)][2]) ** 2) ** 0.5 for i in range(len(pts)))

budget_lines = []
for i, s_ in enumerate(sets):
    length = polyline_length(s_)
    boughs = max(2, round(length / BOUGH_SPACING)) * BOUGH_PRISM_COUNT
    hoops = waypoint_counts[i] * HOOP_PRISM_COUNT
    guide = max(2, round(length / GUIDE_SPACING))
    total = boughs + hoops + guide
    budget_lines.append(f"intensity {i + 1}: line ~{length:.0f}u -> {boughs} bough + {hoops} hoop + {guide} guide = {total} prisms (all super-shielded), crystal target {targets[i]}")
    if total > PRISM_BUDGET_CEILING:
        errors.append(f"intensity {i + 1} lays {total} prisms > ceiling {PRISM_BUDGET_CEILING}")

all_new = list(G_SCRIPT.values()) + list(G_ASSET.values())
if len(set(all_new)) != len(all_new):
    errors.append("minted GUID collision within this script")

owned_metas = {os.path.normpath(os.path.join(ROOT, rel)) for rel in files if rel.endswith(".meta")}
existing_guids = set()
for dirpath, _, filenames in os.walk(os.path.join(ROOT, "Assets")):
    for fn in filenames:
        if not fn.endswith(".meta"):
            continue
        full = os.path.normpath(os.path.join(dirpath, fn))
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

sc = files["Assets/_Scenes/Multiplayer Scenes/MinigameCanopyRun.unity"]
for name in ("SkimRaceController", "SpawnableWaypointTrack"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("CanopyRunController", "SpawnableCanopyTrack"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if EXISTING["SkimRaceScoringRule"] not in sc:
    errors.append("cloned scene lost the (reused) Skim Race scoring rule")
if sc.count(f"  - vesselClass: {GIBBON_CLASS}\n") != 4:
    errors.append("cloned scene does not carry 4 Gibbon AI templates")
if sc.count("  listOfCrystalPositions:\n" + waypoint_block) != 1:
    errors.append("crystal anchors are not the track waypoints")
if f"  anchorJitterRadius: {CRYSTAL_JITTER:g}\n" not in sc:
    errors.append("crystal jitter not re-authored")

# Key parity: every canopy key the scene carries must be a serialized field of the C# class.
def cs_fields(rel):
    src = read(rel)
    return set(re.findall(r"\[SerializeField[^\]]*\]\s*(?:protected |private |public )?[\w<>\[\],.]+\s+(\w+)\s*(?:=|;)", src))
canopy_fields = cs_fields(SCRIPT_PATHS["SpawnableCanopyTrack"])
for key in re.findall(r"^  (\w+):", NEW_TRACK_TAIL, re.M):
    if key != "previewIntensityLevel" and key not in canopy_fields:
        errors.append(f"scene key '{key}' is not a [SerializeField] on SpawnableCanopyTrack")
card_fields = set(re.findall(r"public\s+[\w<>\[\],.]+\s+(\w+)\s*(?:=|;)", read("Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs")))
card_fields |= set(re.findall(r"public\s+[\w<>\[\],.]+\s+(\w+)\s*(?:=|;)", read("Assets/_Scripts/ScriptableObjects/SO_Game.cs")))
for key in re.findall(r"^  (\w+):", files["Assets/_SO_Assets/Games/ArcadeGameCanopyRun.asset"], re.M):
    if key.startswith("m_"):
        continue
    if key not in card_fields:
        errors.append(f"card key '{key}' is not a field on SO_ArcadeGame/SO_Game")

if errors:
    print("VALIDATION FAILED:")
    for e in errors:
        print("  -", e)
    sys.exit(1)

print("Collider budget:")
for line in budget_lines:
    print("  ", line)

if CHECK_ONLY:
    changed = []
    for rel, content in files.items():
        full = os.path.join(ROOT, rel)
        if not os.path.exists(full) or read(rel) != content:
            changed.append(rel)
    if changed:
        print(f"--check: {len(changed)} file(s) differ from the authored output:")
        for c in sorted(changed):
            print("  -", c)
        sys.exit(1)
    print(f"--check: OK, {len(files)} file(s) match.")
    sys.exit(0)

for rel, content in files.items():
    full = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(content)
print(f"Wrote {len(files)} file(s).")
for rel in sorted(files):
    print("  ", rel)
