#!/usr/bin/env python3
"""
Authors every serialized asset The Bends game mode needs (GameModes.Bends = 42).

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_bends_assets.py [--check]

--check validates without writing (CI / pre-commit use).

WHAT THIS MODE IS. The Bends is the Dolphin-only debuff duel: no guns, one weapon (the crystal
blast), and the only thing that scores is catching an OPPOSING pilot in it. See
Assets/_Scripts/Controller/Arcade/BENDS.md.

THE ARENA IS RAMPAGE'S, ON PURPOSE AND READ-ONLY. The Dolphin banks blast energy only by
skimming and discharges it only on a crystal, so the mode needs exactly what Rampage already
authored: a cactus forest thick enough to charge in, and a scarce crystal supply worth racing
for. Cloning that scene and REFERENCING its four per-intensity cell configs (rather than forking
them) is the CLAUDE.md rule "the Cell owns the environment - minigames don't build parallel
systems" applied to a whole arena. The two modes differ in what you aim the cone AT, which is a
scoring rule, not a world.

THE ONE EDIT THAT REACHES OUTSIDE THE MODE - and it is the load-bearing wiring for the whole
feature - is section 3: the Dolphin's conic blast container gets its vessel effects. It shipped
EMPTY, so until now the Dolphin's blast has passed through enemy pilots doing nothing at all
while destroying every prism around them. That is a platform gap, not a mode setting: a weapon
that engulfs a ship should do something to it. The debuff lands everywhere; only this mode's
scoring rule pays for it (PointsForCombatHit is 0 in every other rule), which is the same
"counted everywhere, scored in one place" split Dog Fight established for gunnery.
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
    "BendsController":          guid("script/BendsController"),
    "BendsObjectiveProvider":   guid("script/BendsObjectiveProvider"),
    "BendsPointTurnMonitor":    guid("script/BendsPointTurnMonitor"),
    "BendsScoringRuleSO":       guid("script/BendsScoringRuleSO"),
    "CombatPointTurnMonitorBase": guid("script/CombatPointTurnMonitorBase"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameBends":            guid("asset/ArcadeGameBends"),
    "BendsScoringRule":           guid("asset/BendsScoringRule"),
    "MinigameBends.unity":        guid("asset/MinigameBends.unity"),
    "VesselCombatHitByCrystalBlast": guid("asset/VesselCombatHitByCrystalBlast"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    # script types
    "SO_ArcadeGame":                    "fe040efad3307fb449b6b72ad15362da",
    "ExplosionImpactorDataContainerSO": "841db4ce66da4384a711272307733e0f",
    "VesselCombatHitByExplosionEffectSO": "22cfb97278f955c7cfdfaf0ec3e9fef2",
    # donor scene scripts to swap out
    "RampageController":       "e11ff862e6844a89a951292673243625",
    "RampagePrismTurnMonitor": "694b571734fe4a55a57f6cc672c7fcc2",
    "RampageScoringRule":      "7d1bfbd4091c4a12a12c730553bf293a",
    # shared content
    "Vessel_Dolphin":          "c0f30e9f09616874780edc0a375ce686",
    # the debuff the blast already knows how to apply - authored, and until now unwired
    "CavitationDebuffEffect":  "1587fc1489104b19b23b3f545657df3d",
    # the SOAP channel a landed vessel-vs-vessel hit travels on
    "Event_CombatHitStats":    "45fb43a0cddb5cc3cc4dc448df994152",
    # the Dolphin's conic blast container (edited in place, not minted)
    "ConicBlastContainer":     "05e8092ca85764e488182ce15e2e6d4c",
    # arcade card art - shared with Rampage and Dog Fight, the other pure-aggression party games
    "IconActive":         "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":       "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground":     "587d2203114c8004c9985d0112c89585",
    "PreviewClip":        "4396864d799a6154bb82e5346ac0093b",
}

PREVIEW_FILEID = 241334157148977051

# The Cell component's fileID in the donor scene - the controller's arenaCell reference.
DONOR_CELL_FILEID = 1700000065

# The bend target - the race metric. The 25%/50% milestone rungs are FRACTIONS of this (so 15
# and 30), and moving it moves the whole progress ladder. Kept in sync with
# EndConditionOverridesSO.DefaultBendsPointTarget.
BENDS_POINT_TARGET = 60

# What one bend is worth. 10 rather than 1 so the target reads as a count of REAL EVENTS (60 =
# six clean hits) instead of a number that needs dividing, and so a blast that catches two
# enemies is visibly a big moment on the HUD.
BEND_POINTS = 10

# The comeback strength, and it is a FUNCTION OF THE TARGET - `bonusLevels = deficit x rate` -
# so a rate only means anything next to the scale of deficits the mode produces. A
# quarter-of-target deficit here is 15 points, and the platform rule of thumb (Rampage, Dog
# Fight) is that this should buy several whole element levels: 15 x 0.4 = 6.
#
# It matters more in this mode than in any other, because the thing a bend TAKES is element
# levels: a player who is losing is, by construction, also debuffed. The comeback buff is what
# stops that becoming a spiral, and all four elements rise together per the platform law
# (CLAUDE.md / ElementalComebackSystem: equal-elements), so this dial is the whole surface.
COMEBACK_RATE = 0.4

# How long a bend lasts and how deep it cuts. These are the ALREADY-AUTHORED values on the
# cavitation debuff asset (-0.5 on every element, decaying over 4s); they are restated here only
# so the doc and the tuning live together. This script does not rewrite that asset - it wires it.
BEND_MAGNITUDE = -0.5
BEND_DURATION = 4

# Anti-double-count window, and it MUST match the debuff effect's own cooldown (1s). The blast
# is a cone that GROWS through its victim over many frames, so both the debuff and the scoring
# effect need a per-victim window or one detonation pays - and debuffs - every frame it overlaps.
SAME_VICTIM_COOLDOWN = 1

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


# ── 1. .cs.meta for the new scripts ──────────────────────────────────────────
SCRIPT_PATHS = {
    "BendsController":        "Assets/_Scripts/Controller/Arcade/BendsController.cs",
    "BendsObjectiveProvider": "Assets/_Scripts/Controller/Arcade/BendsObjectiveProvider.cs",
    "BendsScoringRuleSO":     "Assets/_Scripts/Controller/Arcade/Scoring/BendsScoringRuleSO.cs",
    "BendsPointTurnMonitor":  "Assets/_Scripts/Controller/Arcade/TurnMonitors/BendsPointTurnMonitor.cs",
    "CombatPointTurnMonitorBase":
        "Assets/_Scripts/Controller/Arcade/TurnMonitors/CombatPointTurnMonitorBase.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))


# ── 2. The scoring effect: "an opposing pilot was caught in this blast" ──────
#
# hitClass 2 = CombatHitClass.Debuff. Same script as the Sparrow's missile-blast reporter,
# authored differently - which is the point of the class living on the ASSET rather than being
# inferred at runtime.
#
# requireDebuffableVictim is the flag this mode added, and it exists so the score and the effect
# cannot disagree. An elementally immune pilot (ResourceSystem.IsElementallyImmune) takes no
# element drain from the blast - ApplyElementalEffect drops negative magnitudes while immune - so
# scoring their attacker would pay for something that provably did not happen. Off for a missile
# (a rocket that hits you hit you, whatever your immunity state); on here, because the whole
# event being scored IS the drain.
#
# requireOwningMachine is the OTHER flag this mode added, and it is a networking fix rather than
# a design choice. A crystal collection resolves server-side and
# NetworkCrystalManager.ReplayVesselCrystalEffects then re-runs the vessel effects on the OWNING
# client, so a client's single blast genuinely exists on two machines - unlike a Sparrow rocket,
# which is a pooled local object that only ever exists on one. Without this the server would
# credit its own copy AND accept the client's forwarded RPC for the same bend, and the
# VesselCombatHitLatch cannot help because it is per-machine.
emit("Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByCrystalBlast.asset",
     HEADER_FOR(EXISTING["VesselCombatHitByExplosionEffectSO"], "VesselCombatHitByCrystalBlast") +
     f"""  hitClass: 2
  onCombatHitLanded: {{fileID: 11400000, guid: {EXISTING['Event_CombatHitStats']}, type: 2}}
  sameVictimCooldownSeconds: {SAME_VICTIM_COOLDOWN}
  requireDebuffableVictim: 1
  requireOwningMachine: 1
""")
emit("Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByCrystalBlast.asset.meta",
     asset_meta(G_ASSET["VesselCombatHitByCrystalBlast"]))


# ── 3. Wire the Dolphin's conic blast - THE load-bearing edit ────────────────
#
# AOEConicExplosionImpactorDataContainer is the effect container on AOEConicExplosion.prefab,
# the Dolphin's crystal blast, and it shipped with vesselExplosionEffects EMPTY. So the blast
# has always destroyed every prism it engulfed and done NOTHING to a pilot standing in the same
# volume. Two sibling effects now hang here, dispatched from the one contact:
#
#   1. the cavitation debuff (already authored, never wired) - the ELEMENTAL expression of "the
#      blast weakens you", per the platform law that elementals are the single buff/debuff
#      system;
#   2. the scoring report - counted in every mode, paid for only by BendsScoringRuleSO.
#
# The order is not load-bearing (neither reads the other's result) but is written debuff-first
# so the file reads as "what happens, then what it is worth".
#
# SCOPED TO THE CONIC PREFAB deliberately, exactly as Dog Fight scoped its missile reporter: the
# same shape of edit on the shared AOEExplosion.prefab would label every vessel's crystal blast
# a bend in every mode.
emit("Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/"
     "AOEConicExplosionImpactorDataContainer.asset",
     HEADER_FOR(EXISTING["ExplosionImpactorDataContainerSO"],
                "AOEConicExplosionImpactorDataContainer") +
     f"""  vesselExplosionEffects:
  - {{fileID: 11400000, guid: {EXISTING['CavitationDebuffEffect']}, type: 2}}
  - {{fileID: 11400000, guid: {G_ASSET['VesselCombatHitByCrystalBlast']}, type: 2}}
  explosionPrismEffects: []
""")


# ── 4. Scoring rule ──────────────────────────────────────────────────────────
# metric 8 = ScoringMetric.CombatPoints - the same field Dog Fight races on, because both modes
# score vessel-vs-vessel hits and only the WEIGHTING differs. Golf: the winning domain's pilots
# carry a finish time, everyone else a sentinel, so lower is better.
emit("Assets/_SO_Assets/Scoring Rules/BendsScoringRule.asset",
     HEADER_FOR(G_SCRIPT["BendsScoringRuleSO"], "BendsScoringRule") +
     f"  metric: 8\n  golfRules: 1\n  bendPoints: {BEND_POINTS}\n  gunneryPoints: 0\n")
emit("Assets/_SO_Assets/Scoring Rules/BendsScoringRule.asset.meta",
     asset_meta(G_ASSET["BendsScoringRule"]))


# ── 5. Arcade game config ────────────────────────────────────────────────────
# DOLPHIN ONLY: a single entry in Vessels is what drives all three enforcement layers
# (GameDataSO.SyncFromArcadeGame's launcher clamp, ServerPlayerVesselInitializer's server-side
# spawn clamp, and the AI clamp in ServerPlayerVesselInitializerWithAI).
#
# MinPlayersAllowed 2 / MinDomainsAllowed 2 is a RULE rather than a preference, and a stricter
# one than Rampage's: teammates cannot be caught in each other's blasts
# (ExplosionImpactor.AcceptImpactee declines own-domain vessels), so a lobby that launched solo
# or all on one domain would have nothing legal to score against at all. Rampage can be played
# solo because its target is the forest; this mode's target is a person.
emit("Assets/_SO_Assets/Games/ArcadeGameBends.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameBends") + f"""  Mode: 42
  IsMultiplayer: 1
  DisplayName: The Bends
  Description: Dolphins only, in the cactus forest, with no guns anywhere. Graze the
    thicket to charge your jaws, race a rival to the crystal, and put the cone on a
    PILOT instead of the trees - a hit strips every element they have and leaves them
    four seconds of being worse at all of it. First domain to the bend target takes it.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  PreviewClip: {{fileID: {PREVIEW_FILEID}, guid: {EXISTING['PreviewClip']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameBends
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Dolphin']}, type: 2}}
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
emit("Assets/_SO_Assets/Games/ArcadeGameBends.asset.meta",
     asset_meta(G_ASSET["ArcadeGameBends"]))


# ── 6. Scene: clone MinigameRampage, swap the mode-specific wiring ───────────
#
# Only two things change, and that is the whole argument for cloning rather than authoring: the
# CONTROLLER and the TURN MONITOR. Everything else the donor authored is exactly what this mode
# wants and is inherited verbatim, references intact -
#
#   • the four per-intensity cactus-forest cell configs (IntensityWise), which also carry the
#     intensity ladder: crystals get SCARCER and wildlife heavier as intensity rises. In Rampage
#     that ladder tunes how contested cashing out is; here it does the same job for the same
#     reason, because it is the same vessel economy;
#   • the crystal counts (2x players / players / players-1 / exactly 1);
#   • the cell-relative spawn ring, all pilots facing the cell;
#   • the four AI templates, already vesselClass 2 (Dolphin).
#
# A scene that shares a donor's arena has to be checked against the donor MOVING, which is why
# the asserts below are exact-match rather than fuzzy: if Rampage re-authors its controller
# block, this fails loudly instead of silently producing a scene with a half-wired controller.
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity")

# 6a. turn monitor script swap. The field set is identical (base TurnMonitor fields only), so
# the swap is the guid and nothing else - both monitors read their target from
# EndConditionOverridesSO rather than from a serialized field, per the /EndGameConditions rule.
scene, n = re.subn(EXISTING["RampagePrismTurnMonitor"], G_SCRIPT["BendsPointTurnMonitor"], scene)
assert n == 1, f"turn monitor guid appeared {n} times"

# 6b. controller script swap + its serialized field block
scene, n = re.subn(EXISTING["RampageController"], G_SCRIPT["BendsController"], scene)
assert n == 1, f"controller guid appeared {n} times"

OLD_FIELDS = f"  rule: {{fileID: 11400000, guid: {EXISTING['RampageScoringRule']}, type: 2}}\n"
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['BendsScoringRule']}, type: 2}}
  arenaCell: {{fileID: {DONOR_CELL_FILEID}}}
  firstMilestoneFraction: 0.25
  secondMilestoneFraction: 0.5
  progressSampleSeconds: 0.5
  aiAimRetargetSeconds: 1.25
  aiAimLeadSeconds: 0.35
  aiAimMaxRange: 900
"""
assert scene.count(OLD_FIELDS) == 1, "controller field block not found in donor scene"
scene = scene.replace(OLD_FIELDS, NEW_FIELDS)

emit("Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity.meta",
     scene_meta(G_ASSET["MinigameBends.unity"]))


# ── 7. Register the card in the party-games list ─────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameBends']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
emit(LIST_PATH, games)


# ── 8. Always-unlocked so the card is clickable on a fresh account ──────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 42\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 42\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 9. Build settings ───────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameBends.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameRampage\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Rampage scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity\n"
                          f"    guid: {G_ASSET['MinigameBends.unity']}\n")
emit(BUILD_PATH, build)


# ── 10. End-game condition target ───────────────────────────────────────────
# The shared overrides asset is what FrogletTools > Game Modes > End Game Conditions edits. A
# missing key would fall back to the C# field initializer, so author both the live and the
# build-baseline value explicitly - and SET them rather than only inserting when absent, so a
# re-run after a target change actually moves the number the game reads.
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("dogFightPointTarget", "bendsPointTarget"),
                          ("dogFightPointTargetBuild", "bendsPointTargetBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0), f"  {new_key}: {BENDS_POINT_TARGET}\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH} - run author_dogfight_assets.py first"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {BENDS_POINT_TARGET}\n", 1)
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

# every script this script mints a meta for must actually exist
for k, p in SCRIPT_PATHS.items():
    if not os.path.exists(os.path.join(ROOT, p)):
        errors.append(f"script {p} does not exist")

# the cloned scene must no longer mention the donor's mode-specific guids, and must mention ours
sc = files["Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity"]
for name in ("RampageController", "RampagePrismTurnMonitor", "RampageScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("BendsController", "BendsPointTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if G_ASSET["BendsScoringRule"] not in sc:
    errors.append("cloned scene missing the scoring rule reference")
# the arena is INHERITED - if the donor's forest configs stopped coming through, the clone is
# an empty cell and the Dolphin has nothing to skim.
if sc.count("  cellTypeChoiceOptions: 1\n") < 1:
    errors.append("cloned scene lost the IntensityWise cell selection")
if sc.count("  - vesselClass: 2\n") != 4:
    errors.append("cloned scene does not carry 4 Dolphin AI templates")

# the blast wiring is the whole feature - assert both effects landed on the container
cont = files["Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/"
             "AOEConicExplosionImpactorDataContainer.asset"]
for name, g in (("cavitation debuff", EXISTING["CavitationDebuffEffect"]),
                ("combat-hit report", G_ASSET["VesselCombatHitByCrystalBlast"])):
    if g not in cont:
        errors.append(f"conic blast container missing the {name}")

if errors:
    print("VALIDATION FAILED:")
    for e in errors:
        print("  -", e)
    sys.exit(1)

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
