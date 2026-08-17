#!/usr/bin/env python3
"""
Authors every serialized asset the Salvo game mode needs (GameModes.Salvo = 42).

Salvo is the Sparrow-only demolition race in the Boneyard - Dog Fight's inverse: there the
wreckage is cover and shooting it scores nothing; here tearing it apart IS the score
(ScoringMetric.PrismsDestroyed, the Rampage/Ribcage metric). The arena, the cell configs, the
spawn profiles and the scavengers are Dog Fight's Boneyard assets REUSED VERBATIM - the cell is
per-arena, not per-mode, and forking it would be the parallel-system mistake. What this script
authors is only what is genuinely Salvo's:

  - the arcade card + scoring rule
  - the scene (cloned from MinigameDogFight, mode wiring swapped)
  - the crystal ABUNDANCE (PlayerCountPlusExtra + 5, Scurry's shape, against Rampage's
    scarcity) - crystals are the Sparrow's missile economy here
  - the registrations (game list, progression, build settings, end-condition target)

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_salvo_assets.py [--check]

--check validates without writing (CI / pre-commit use).

See Assets/_Scripts/Controller/Arcade/SALVO.md for what these numbers mean.
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
    "SalvoController":       guid("script/SalvoController"),
    "SalvoPrismTurnMonitor": guid("script/SalvoPrismTurnMonitor"),
    "SalvoScoringRuleSO":    guid("script/SalvoScoringRuleSO"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameSalvo":      guid("asset/ArcadeGameSalvo"),
    "SalvoScoringRule":     guid("asset/SalvoScoringRule"),
    "MinigameSalvo.unity":  guid("asset/MinigameSalvo.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    # script types
    "SO_ArcadeGame":    "fe040efad3307fb449b6b72ad15362da",
    # donor scene wiring to swap out (all minted deterministically by
    # author_dogfight_assets.py, so they are recomputable here)
    "DogFightController":       guid("script/DogFightController"),
    "DogFightPointTurnMonitor": guid("script/DogFightPointTurnMonitor"),
    "DogFightScoringRule":      guid("asset/DogFightScoringRule"),
    # the omni crystal's collection channel - the crystal prefab raises it server-side with
    # the collector's name; StatsManager and (now) SalvoController subscribe to it
    "EventOnCrystalCollected": "3664bc230593b734aa52dd67e3caa21c",
    # shared content
    "Vessel_Sparrow": "7b7053dd065edb54baa3b831b90f4985",
    # arcade card art - shared with Rampage and Dog Fight, the other aggression party games
    "IconActive":     "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":   "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground": "587d2203114c8004c9985d0112c89585",
    "PreviewClip":    "4396864d799a6154bb82e5346ac0093b",
}

PREVIEW_FILEID = 241334157148977051

# The prism target - the race metric. Lower than Rampage's 2000 because the Sparrow's salvos
# are crystal-rationed: a skyburst costs half the missile tank, the tank does not regenerate,
# and the only refuel is an omni crystal - so destruction comes in bought bursts rather than a
# Dolphin's continuous graze-and-blast loop. Kept in sync with
# EndConditionOverridesSO.DefaultSalvoPrismTarget.
SALVO_PRISM_TARGET = 1500

# The comeback strength - a FUNCTION OF THE TARGET (`bonusLevels = deficit x rate`; see the
# Dog Fight generator for the drift story this guards against). At 0.013 a quarter-of-target
# deficit (375 prisms) buys ~4.9 element levels - the same footing as Rampage's 0.01 against
# 2000. All four elements rise together (equal-elements is the law); Mass is the one a Sparrow
# feels through its guns, since it stretches the fired prisms AND their hit sphere.
COMEBACK_RATE = 0.013

# The crystal economy - the mode's whole rhythm, and its reason to play together.
#
# ABUNDANCE, not Rampage's scarcity: CrystalCountMode.PlayerCountPlusExtra (1) with +5, the
# Scurry shape - 7 crystals in a 2-player lobby, 9 in a full one. Every omni crystal collected
# fully reloads the collector's missile tank (the platform crystal effect the Sparrow already
# carries) AND - the mode's own rule - the missile bays of every pilot on the collector's
# domain (SalvoController.RefuelDomainMissiles_ClientRpc). A wingman flying the crystal line
# keeps the strikers firing.
EXTRA_CRYSTALS_BEYOND_PLAYERS = 5

# The donor's authored anchorless spawn ball (the Boneyard has no nucleus); kept verbatim so
# the crystals hide among the hulks instead of stacking on the arena centre.
OMNI_SPAWN_RADIUS = 420

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


files: "dict[str, str]" = {}


def emit(rel: str, content: str):
    files[rel] = content


def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


# ── 1. .cs.meta for the scripts assets/scenes point at ───────────────────────
SCRIPT_PATHS = {
    "SalvoController":       "Assets/_Scripts/Controller/Arcade/SalvoController.cs",
    "SalvoPrismTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/SalvoPrismTurnMonitor.cs",
    "SalvoScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/Scoring/SalvoScoringRuleSO.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))


# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 5 = ScoringMetric.PrismsDestroyed. Golf: the winning domain's pilots carry a finish
# time, everyone else a remaining-prisms sentinel, so lower is better - identical to Rampage
# (the class inherits RampageScoringRuleSO and overrides only the reveal label).
emit("Assets/_SO_Assets/Scoring Rules/SalvoScoringRule.asset",
     HEADER_FOR(G_SCRIPT["SalvoScoringRuleSO"], "SalvoScoringRule") +
     "  metric: 5\n  golfRules: 1\n")
emit("Assets/_SO_Assets/Scoring Rules/SalvoScoringRule.asset.meta",
     asset_meta(G_ASSET["SalvoScoringRule"]))


# ── 3. Arcade game config ────────────────────────────────────────────────────
# SPARROW ONLY: a single entry in Vessels drives all three enforcement layers (the launcher
# clamp, the server-side spawn clamp, and the AI clamp).
#
# MinDomainsAllowed 2 because a race needs a rival: destruction sums per DOMAIN, and a lobby
# that launched with everyone on one colour would be a co-op timer with no opponent.
emit("Assets/_SO_Assets/Games/ArcadeGameSalvo.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameSalvo") + f"""  Mode: 42
  IsMultiplayer: 1
  DisplayName: Salvo
  Description: Sparrows only, and this time the Boneyard is the target. Guns chip,
    rockets level whole hulks, and every omni crystal you grab reloads the missile
    bays of your WHOLE team - so somebody fly the crystal line while somebody rains
    the salvos. First domain to tear down the prism target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  PreviewClip: {{fileID: {PREVIEW_FILEID}, guid: {EXISTING['PreviewClip']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameSalvo
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Sparrow']}, type: 2}}
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
emit("Assets/_SO_Assets/Games/ArcadeGameSalvo.asset.meta",
     asset_meta(G_ASSET["ArcadeGameSalvo"]))


# ── 4. Scene: clone MinigameDogFight, swap the mode-specific wiring ──────────
# The donor already IS the Boneyard scene this mode wants - Sparrow AI templates, spawn sphere
# at r=700, IntensityWise Boneyard cell configs, noNucleusSpawnRadius 420 - so the clone only
# swaps the mode identity: controller, turn monitor, rule, and the crystal economy.
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity")

# 4a. turn monitor script swap (field set is identical - base TurnMonitor fields only)
scene, n = re.subn(EXISTING["DogFightPointTurnMonitor"], G_SCRIPT["SalvoPrismTurnMonitor"], scene)
assert n == 1, f"turn monitor guid appeared {n} times"

# 4b. controller script swap + its serialized field block. Salvo has no milestone sampler and
# no AI dogfighter loop (the platform-default AI seeks the crystals, which here ARE its ammo
# line), so those fields go; in their place the controller is wired to the omni crystal's
# collection channel for the wingman reload.
scene, n = re.subn(EXISTING["DogFightController"], G_SCRIPT["SalvoController"], scene)
assert n == 1, f"controller guid appeared {n} times"

OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['DogFightScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  firstMilestoneFraction: 0.25
  secondMilestoneFraction: 0.5
  progressSampleSeconds: 0.5
  elementalCrystalCount: 14
  crystalScatterRadius: 400
  crystalScatterSeed: 41
  aiRetargetSeconds: 1.5
  aiLeadSeconds: 0.6
  aiBreakOffDistance: 120
  aiExtendDistanceMultiplier: 3
  aiMaxExtendSeconds: 4
"""
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['SalvoScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  onOmniCrystalCollected: {{fileID: 11400000, guid: {EXISTING['EventOnCrystalCollected']}, type: 2}}
  missileResourceIndex: 0
  elementalCrystalCount: 14
  crystalScatterRadius: 400
  crystalScatterSeed: 42
"""
assert OLD_FIELDS in scene, "controller field block not found in donor scene"
scene = scene.replace(OLD_FIELDS, NEW_FIELDS)

# 4c. THE CRYSTAL ABUNDANCE - the one gameplay dial this mode moves on the donor.
# PlayerCountPlusExtra (+5): the Scurry shape, because in Salvo the omni crystal is the
# missile economy - a Sparrow with an empty tank and no crystal in reach is a pilot with
# nothing to do. noNucleusSpawnRadius stays 420 (this cell has no nucleus; without the
# fallback every crystal stacks on the arena's exact centre).
OLD_CRYSTALS = ("  noNucleusSpawnRadius: 420\n  crystalCountMode: 0\n"
                "  fixedCrystalCount: 4\n  extraCrystalsToSpawnBeyondPlayerCount: 0\n")
NEW_CRYSTALS = (f"  noNucleusSpawnRadius: {OMNI_SPAWN_RADIUS}\n  crystalCountMode: 1\n"
                f"  fixedCrystalCount: 4\n"
                f"  extraCrystalsToSpawnBeyondPlayerCount: {EXTRA_CRYSTALS_BEYOND_PLAYERS}\n")
assert OLD_CRYSTALS in scene, "donor crystal-count block not found"
scene = scene.replace(OLD_CRYSTALS, NEW_CRYSTALS, 1)

emit("Assets/_Scenes/Multiplayer Scenes/MinigameSalvo.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameSalvo.unity.meta",
     scene_meta(G_ASSET["MinigameSalvo.unity"]))


# ── 5. Register the card in the party-games list ────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameSalvo']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
emit(LIST_PATH, games)


# ── 6. Always-unlocked so the card is clickable on a fresh account ──────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 42\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 42\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 7. Build settings ───────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameSalvo.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameDogFight\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Dog Fight scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameSalvo.unity\n"
                          f"    guid: {G_ASSET['MinigameSalvo.unity']}\n")
emit(BUILD_PATH, build)


# ── 8. End-game condition target ────────────────────────────────────────────
# SET semantics, not add-if-absent (the Dog Fight generator's lesson: an insert-only key
# left the asset on a stale number after a target retune).
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("dogFightPointTarget", "salvoPrismTarget"),
                          ("dogFightPointTargetBuild", "salvoPrismTargetBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0),
                                  f"  {new_key}: {SALVO_PRISM_TARGET}\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH} - run author_dogfight_assets.py first"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {SALVO_PRISM_TARGET}\n", 1)
emit(END_PATH, endcond)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

all_new = list(G_SCRIPT.values()) + list(G_ASSET.values())
if len(set(all_new)) != len(all_new):
    errors.append("minted GUID collision within this script")

# .meta files THIS script owns are excluded from the collision sweep - otherwise a second run
# flags its own (byte-identical) output as a collision and the script stops being idempotent.
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

# the scene must no longer mention the donor's mode-specific guids
sc = files["Assets/_Scenes/Multiplayer Scenes/MinigameSalvo.unity"]
for name in ("DogFightController", "DogFightPointTurnMonitor", "DogFightScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("SalvoController", "SalvoPrismTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if G_ASSET["SalvoScoringRule"] not in sc:
    errors.append("cloned scene missing the scoring rule reference")
if EXISTING["EventOnCrystalCollected"] not in sc:
    errors.append("cloned scene does not wire the omni crystal collection channel onto the "
                  "controller - the wingman reload (the mode's reason to play together) would "
                  "never fire")
if "  cellTypeChoiceOptions: 1\n" not in sc:
    errors.append("scene Cell is not on CellTypeChoiceOptions.IntensityWise")
if sc.count("vesselClass: 11") != 4:
    errors.append("scene does not author 4 Sparrow AI templates")
if "  crystalCountMode: 1\n" not in sc:
    errors.append("scene is not on CrystalCountMode.PlayerCountPlusExtra - the crystal "
                  "abundance IS the missile economy here")
if f"  extraCrystalsToSpawnBeyondPlayerCount: {EXTRA_CRYSTALS_BEYOND_PLAYERS}\n" not in sc:
    errors.append("scene does not author the extra-crystal abundance")
if f"  noNucleusSpawnRadius: {OMNI_SPAWN_RADIUS}\n" not in sc:
    errors.append("scene lost noNucleusSpawnRadius - this cell has no nucleus, so every omni "
                  "crystal would stack on the arena's exact centre")
if "  missileResourceIndex: 0\n" not in sc:
    errors.append("scene does not author the missile resource index for the wingman reload")

# Sparrow-only must be a SINGLE entry, or the clamps let another hull through
arcade = files["Assets/_SO_Assets/Games/ArcadeGameSalvo.asset"]
vessels = re.search(r"^  Vessels:\n((?:  - .*\n)*)", arcade, re.M)
if not vessels or vessels.group(1).count("- {fileID") != 1:
    errors.append("ArcadeGameSalvo must author EXACTLY ONE vessel (Sparrow)")
elif EXISTING["Vessel_Sparrow"] not in vessels.group(1):
    errors.append("ArcadeGameSalvo's single vessel is not Sparrow")
if "MinDomainsAllowed: 2" not in arcade:
    errors.append("ArcadeGameSalvo must require at least TWO domains - a one-domain lobby is "
                  "a race with no rival")

# The comeback rate only means anything relative to the TARGET (see the Dog Fight generator's
# drift story). A quarter-of-target deficit must buy at least one whole element level.
_quarter_deficit_levels = (SALVO_PRISM_TARGET * 0.25) * COMEBACK_RATE
if _quarter_deficit_levels < 1.0:
    errors.append(f"ComebackRatePerScoreDeficit {COMEBACK_RATE} is too small for a "
                  f"{SALVO_PRISM_TARGET}-prism target: a quarter-of-target deficit buys only "
                  f"{_quarter_deficit_levels:.2f} element levels, which is invisible")


# serialized MonoBehaviour keys must exist on the C# class (asset-surgery §3)
def cs_fields(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as fh:
        src = fh.read()
    out = set()
    TYPE = r"[\w<>,\[\]\?\.]+"
    MODS = (r"(?:(?:public|protected|private|internal|static|const|readonly|new|virtual|"
            r"override|abstract|sealed|partial)\s+)+")
    for m in re.finditer(MODS + TYPE + r"\s+(\w+)\s*(?:=|;|\{|=>|\()", src):
        out.add(m.group(1))
    for m in re.finditer(r"\[SerializeField[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
                         r"(?:(?:public|protected|private|internal)\s+)?"
                         + TYPE + r"\s+(\w+)\s*(?:=|;)", src):
        out.add(m.group(1))
    for m in re.finditer(r"^\s{4,}" + TYPE + r"\s+(\w+)\s*\{\s*get;", src, re.M):
        out.add(m.group(1))
    return out


SO_BASE = {"Mode", "IsMultiplayer", "DisplayName", "Description", "IconActive", "IconInactive",
           "CardBackground", "PreviewClip", "GolfScoring", "SceneName"}
CHECKS = [
    ("Assets/_SO_Assets/Games/ArcadeGameSalvo.asset",
     "Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs"),
    ("Assets/_SO_Assets/Scoring Rules/SalvoScoringRule.asset",
     "Assets/_Scripts/Controller/Arcade/Scoring/SalvoScoringRuleSO.cs"),
]
for asset_path, cs_path in CHECKS:
    keys = set(re.findall(r"^  (\w+):", files[asset_path], re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
        "m_EditorClassIdentifier"}
    known = cs_fields(cs_path) | SO_BASE
    for extra in ("Assets/_Scripts/ScriptableObjects/SO_Game.cs",
                  "Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs",
                  "Assets/_Scripts/Controller/Arcade/Scoring/RampageScoringRuleSO.cs"):
        if os.path.exists(os.path.join(ROOT, extra)):
            known |= cs_fields(extra)
    unknown = keys - known
    if unknown:
        errors.append(f"{os.path.basename(asset_path)}: keys not found on "
                      f"{os.path.basename(cs_path)}: {sorted(unknown)}")

# every serialized key the scene's controller block authors must exist on SalvoController.cs
CONTROLLER_CS = "Assets/_Scripts/Controller/Arcade/SalvoController.cs"
controller_keys = {"rule", "arenaCell", "onOmniCrystalCollected", "missileResourceIndex",
                   "elementalCrystalCount", "crystalScatterRadius", "crystalScatterSeed"}
missing = controller_keys - cs_fields(CONTROLLER_CS)
if missing:
    errors.append(f"SalvoController.cs is missing serialized fields the scene authors: "
                  f"{sorted(missing)}")

# the C# default target and this script's must agree, or the tool window's "(default)" lies
endcond_cs = read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultSalvoPrismTarget = (\d+);", endcond_cs)
if not m:
    errors.append("EndConditionOverridesSO.cs has no DefaultSalvoPrismTarget")
elif int(m.group(1)) != SALVO_PRISM_TARGET:
    errors.append(f"DefaultSalvoPrismTarget ({m.group(1)}) != this script's "
                  f"SALVO_PRISM_TARGET ({SALVO_PRISM_TARGET}) - the two must move together")

# GameModes.Salvo must exist with the value this card authors
gamemodes_cs = read("Assets/_Scripts/Data/Enums/GameModes.cs")
if not re.search(r"^\s*Salvo = 42,", gamemodes_cs, re.M):
    errors.append("GameModes.cs has no 'Salvo = 42' - the card would launch nothing")

if errors:
    print("VALIDATION FAILED - nothing written:")
    for e in errors:
        print("  x", e)
    sys.exit(1)

print(f"Validation passed ({len(files)} files).")
for rel in sorted(files):
    print("  ", rel)

if CHECK_ONLY:
    print("\n--check: no files written.")
    sys.exit(0)

for rel, content in files.items():
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(content)
print(f"\nWrote {len(files)} files.")
