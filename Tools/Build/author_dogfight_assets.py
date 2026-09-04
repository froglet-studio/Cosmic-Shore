#!/usr/bin/env python3
"""
Authors every serialized asset the Dog Fight game mode needs (GameModes.DogFight = 41).

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_dogfight_assets.py [--check]

--check validates without writing (CI / pre-commit use).

The arena geometry and the PhaseThresholds are IMPORTED from boneyard_budget.py - which
mirrors SpawnableBoneyard.cs's loops exactly - so the wreck field and the phase ladder that
sits above it can never drift apart behind a stale constant here.

Beyond the mode's own assets this script does one thing that reaches OUTSIDE the mode, and it
is the load-bearing wiring for the whole feature: it hangs the combat-hit effects on the
Sparrow's own weapon containers and gives the skyburst's conic blast an explosion container it
never had. Without those three edits the mode has a scoring rule and nothing that raises it.

See Assets/_Scripts/Controller/Arcade/DOGFIGHT.md for what these numbers mean.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import boneyard_budget as budget  # noqa: E402


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


INTENSITIES = [1, 2, 3, 4]
# Every intensity flies the Boneyard. Intensity 4 briefly flew Scurry's SpawnableAtlantis
# instead; the playtest read after that was that the Boneyard itself is right at every level and
# what was wrong was the SPREAD between levels, so 4 came back to the Boneyard and the ladder in
# boneyard_budget.INTENSITIES was widened from a 1.9x span to 3.8x.
BONEYARD_INTENSITIES = INTENSITIES

# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
# Only scripts an ASSET points at need a deterministic GUID authored here; the rest of the
# branch's new .cs files get theirs from Unity on import like any other script.
G_SCRIPT = {
    "SpawnableBoneyard":                    guid("script/SpawnableBoneyard"),
    "DogFightController":                   guid("script/DogFightController"),
    "DogFightPointTurnMonitor":             guid("script/DogFightPointTurnMonitor"),
    "DogFightScoringRuleSO":                guid("script/DogFightScoringRuleSO"),
    "ScriptableEventCombatHitStats":        guid("script/ScriptableEventCombatHitStats"),
    "VesselCombatHitByProjectileEffectSO":  guid("script/VesselCombatHitByProjectileEffectSO"),
    "VesselCombatHitByExplosionEffectSO":   guid("script/VesselCombatHitByExplosionEffectSO"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameDogFight":            guid("asset/ArcadeGameDogFight"),
    "DogFightScoringRule":           guid("asset/DogFightScoringRule"),
    "MinigameDogFight.unity":        guid("asset/MinigameDogFight.unity"),
    "Event_CombatHitStats":          guid("asset/Event_CombatHitStats"),
    "VesselCombatHitByBullet":       guid("asset/VesselCombatHitByBullet"),
    "VesselCombatHitByMissile":      guid("asset/VesselCombatHitByMissile"),
    "VesselCombatHitByMissileBlast": guid("asset/VesselCombatHitByMissileBlast"),
    "SkyBurstExplosionContainer":    guid("asset/SkyBurstExplosionImpactorDataContainer"),
}
for _i in INTENSITIES:
    if _i in BONEYARD_INTENSITIES:
        G_ASSET[f"SpawnableBoneyard{_i}.prefab"] = guid(f"asset/SpawnableBoneyard{_i}.prefab")
    G_ASSET[f"BoneyardCellConfig{_i}"] = guid(f"asset/BoneyardCellConfig{_i}")
    G_ASSET[f"BoneyardSpawnProfile{_i}"] = guid(f"asset/BoneyardSpawnProfile{_i}")
    G_ASSET[f"BoneyardScavenger{_i}"] = guid(f"asset/BoneyardScavenger{_i}")

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    # script types
    "SO_ArcadeGame":                    "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO":                 "01f934d50526431a9392a6ceca1dc33d",
    "SpawnProfileSO":                   "e8d8aa5d835249798a256e18f2f7d912",
    "FaunaConfigurationSO":             "c778cfbe4dfc4c5c8401e40c17802311",
    "ExplosionImpactorDataContainerSO": "841db4ce66da4384a711272307733e0f",
    # donor scene scripts to swap out
    "RampageController":       "e11ff862e6844a89a951292673243625",
    "RampagePrismTurnMonitor": "694b571734fe4a55a57f6cc672c7fcc2",
    "RampageCellConfig":       "c6959b0e548d4f26bdde820ca48ac26e",
    "RampageScoringRule":      "7d1bfbd4091c4a12a12c730553bf293a",
    # shared content
    "Prism_prefab":       "ed9defc56162b4b4588e61c20984b6d9",
    "Membrane_prefab":    "6e330f85972faf843b8a128e7166f7b5",
    "Cytoplasm_prefab":   "9cacd903fcf4643459f5f14ac811bb20",
    "CellIcon":           "6aa1c06e11b265744a5f9fa8858ac72a",
    "Vessel_Sparrow":     "7b7053dd065edb54baa3b831b90f4985",
    # arcade card art - shared with Rampage, the other pure-aggression party game
    "IconActive":         "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":       "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground":     "587d2203114c8004c9985d0112c89585",
    "PreviewClip":        "4396864d799a6154bb82e5346ac0093b",
    # the scavengers
    "QuadFishPrefab":     "19615ed0c903b1041973d70593d4b0a3",
    # the cell's CellRuntimeDataSO - the spawn ring resolves its Cell through this
    "RuntimeCellData":    "8d4e8398eedc76c4dadb8604f89b9e1b",
}

QUADFISH_FILEID = 4652232322436628206      # the LightFauna component inside the prefab
# Per-element sibling configs, reused verbatim (read-only species identity assets) so the
# scavengers spread across the full elemental palette - one base prefab, four identities.
QUADFISH_PALETTE = ["4053ff006892420d8ca5efa51365570c", "5697aa8685514f2ca9b9de9638fce1a1",
                    "3107bcc776d54a74b110b883f10fba61", "414bce89d4dc495f87bbccfb02e5b847"]

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
PREVIEW_FILEID = 241334157148977051

# The point target - the race metric. The 25%/50% milestone rungs are fractions of this (so 30
# and 60), and moving it moves the whole progress ladder. Kept in sync with
# EndConditionOverridesSO.DefaultDogFightPointTarget.
DOGFIGHT_POINT_TARGET = 90

# The comeback strength, and it is a FUNCTION OF THE TARGET - `bonusLevels = deficit x rate`, so
# a rate is only meaningful next to the scale of the deficits the mode produces. This one shipped
# at 0.004, which was scaled for the original 500-point target and never rescaled when the target
# became 120: a whole rocket behind (50 points) bought 0.2 of a level, i.e. nothing.
#
# 0.12 puts it back on the same footing as the other party games (Rampage: a quarter-of-target
# deficit is worth ~5 levels): one rocket behind = 6 levels, a shutout saturates at the level-10
# sustained ceiling. This is what makes the trailing side's MASS - which stretches the Sparrow's
# fired prisms - visibly larger, per the playtest ask.
#
# All four elements rise together. That is the platform law (CLAUDE.md / ElementalComebackSystem:
# "ALL FOUR elements rise EQUALLY... equal-elements is the law"), so the dial below is the whole
# tuning surface; a Mass-only weighting would be a fundamentals change, not a mode setting.
COMEBACK_RATE = 0.12

# Players spawn on a shell OUTSIDE the wreck field (radius 520) and inside the membrane (1200),
# so every pilot's opening move is to fly in. The cell has no nucleus, so the ring has nothing
# to measure off and needs this floor.
SPAWN_RING_RADIUS = 700

# The cell must SENSE mass out to the whole arena so the scavengers can find a player's trail
# anywhere in it; the membrane visual is 1200, so sensing that far costs no visual change.
SENSE_RADIUS = 1200

# The scene's NetworkCrystalManager spawns the OMNI crystal. Dog Fight runs it EXACTLY as every
# other mode does (PeelTheCage, freestyle: crystalCountMode 0, one crystal, spawnOnClientReady) - it
# is a platform fundamental, not mode furniture, and a mode that switches it off is a mode where
# a whole crystal economy silently does nothing.
#
# It was briefly zeroed here after a playtest read the big faceted sphere as a bouncable
# objective; that was reverted on request. What was actually wrong is the SPAWN RADIUS, fixed
# properly below rather than by deleting the crystal.
# FOUR of them, Scurry-style. One crystal in a 520-unit arena is a needle nobody detours for;
# four means there is usually one worth breaking off toward, which is the point of having them at
# all in a mode where crystals score nothing. Kept on FixedCount rather than Scurry's
# PlayerCountPlusExtra (which would put NINE in a full lobby) - the arena wants a handful, not a
# field. Every one of them still needs OMNI_SPAWN_RADIUS below to not stack on the origin.
OMNI_CRYSTAL_COUNT = 4

# The ball the omni crystal is drawn from, in place of the nucleus radius this cell hasn't got.
# Inside the wreck field (r=520) so it is genuinely hidden among the hulks rather than floating
# in the open, and comfortably inside the spawn shell (700) so it is never behind a player's back
# at the countdown.
OMNI_SPAWN_RADIUS = 420

# The scavenger population. DELIBERATELY LIGHT - see DOGFIGHT.md: this arena already carries
# 12.7k-24k prisms of cover and four Sparrows' worth of projectile and AOE traffic, and every
# fauna body prism is a MOVER that re-buckets in the spatial index each frame. Atmosphere and a
# crystal source, not a second heavy system.
SCAVENGER_SEED = [60, 80, 100, 120]
SCAVENGER_CAP = [150, 200, 250, 300]

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


def meta(g: str, folder: bool = False) -> str:
    if folder:
        return (f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n"
                f"  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def asset_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def prefab_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nPrefabImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


files: "dict[str, str]" = {}


def emit(rel: str, content: str):
    files[rel] = content


def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


# ── 1. .cs.meta for the scripts assets point at ──────────────────────────────
SCRIPT_PATHS = {
    "SpawnableBoneyard":       "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableBoneyard.cs",
    "DogFightController":      "Assets/_Scripts/Controller/Arcade/DogFightController.cs",
    "DogFightPointTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/DogFightPointTurnMonitor.cs",
    "DogFightScoringRuleSO":   "Assets/_Scripts/Controller/Arcade/Scoring/DogFightScoringRuleSO.cs",
    "ScriptableEventCombatHitStats":
        "Assets/_Scripts/ScriptableObjects/SOAP/ScriptableCombatHitStats/ScriptableEventCombatHitStats.cs",
    "VesselCombatHitByProjectileEffectSO":
        "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Projectile Effects/VesselCombatHitByProjectileEffectSO.cs",
    "VesselCombatHitByExplosionEffectSO":
        "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Explosion Effects/VesselCombatHitByExplosionEffectSO.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))


# ── 2. The combat-hit SOAP channel + its wiring on GameDataSO ────────────────
# The two impact effects publish a landed hit here; StatsManager (which already holds this
# GameDataSO in every scene) subscribes in code and turns it into IRoundStats. A ScriptableEvent
# rather than a static event, per the SOAP policy - and on GameDataSO rather than on a
# serialized field of StatsManager, so no scene needs a new wire.
emit("Assets/_SO_Assets/Event Channels/Event_CombatHitStats.asset",
     HEADER_FOR(G_SCRIPT["ScriptableEventCombatHitStats"], "Event_CombatHitStats") +
     "  CategoryIndex: 0\n  Description: Raised when a vessel lands a shot on an OPPOSING\n"
     "    vessel - a direct projectile hit or an opponent caught in a blast. Carries the\n"
     "    shooter, the victim and the weapon class; StatsManager arbitrates who may record it\n"
     "    (projectiles are local objects, so only the machine that fired one sees the hit).\n"
     # No _debugValue key: SOAP omits it for STRUCT payloads (see
     # Event Channels/Prisms/EventOnPrismDestroyed.asset, the same shape). Writing an empty
     # scalar there would ask Unity to deserialize a struct from nothing.
     "  _debugLogEnabled: 0\n")
emit("Assets/_SO_Assets/Event Channels/Event_CombatHitStats.asset.meta",
     asset_meta(G_ASSET["Event_CombatHitStats"]))

GAMEDATA_PATH = "Assets/_SO_Assets/Game Data/Runtime GameData.asset"
gamedata = read(GAMEDATA_PATH)
if "OnCombatHitLanded:" not in gamedata:
    anchor = re.search(r"^  OnPlayerPairInitialized: \{[^}]*\}\n", gamedata, re.M)
    assert anchor, "OnPlayerPairInitialized not found in the runtime GameData SO"
    gamedata = gamedata.replace(
        anchor.group(0),
        anchor.group(0) +
        f"  OnCombatHitLanded: {{fileID: 11400000, guid: {G_ASSET['Event_CombatHitStats']}, type: 2}}\n", 1)
emit(GAMEDATA_PATH, gamedata)


# ── 3. The three combat-hit effects ──────────────────────────────────────────
# hitClass: 0 = Bullet, 1 = Missile.
#
# The cooldown is ONE number shared by all three on purpose: the projectile effect and the
# explosion effect claim the SAME VesselCombatHitLatch window, which is what stops a clean
# centre-punch (a skyburst detonates on its own direct hit) from paying twice for one rocket.
# Give them different windows and that guarantee evaporates.
SAME_VICTIM_COOLDOWN = 0.5

emit("Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByBullet.asset",
     HEADER_FOR(G_SCRIPT["VesselCombatHitByProjectileEffectSO"], "VesselCombatHitByBullet") +
     f"""  vesselTypesToImpact: []
  hitClass: 0
  onCombatHitLanded: {{fileID: 11400000, guid: {G_ASSET['Event_CombatHitStats']}, type: 2}}
  sameVictimCooldownSeconds: 0.05
""")
emit("Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByBullet.asset.meta",
     asset_meta(G_ASSET["VesselCombatHitByBullet"]))

emit("Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByMissile.asset",
     HEADER_FOR(G_SCRIPT["VesselCombatHitByProjectileEffectSO"], "VesselCombatHitByMissile") +
     f"""  vesselTypesToImpact: []
  hitClass: 1
  onCombatHitLanded: {{fileID: 11400000, guid: {G_ASSET['Event_CombatHitStats']}, type: 2}}
  sameVictimCooldownSeconds: {SAME_VICTIM_COOLDOWN}
""")
emit("Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByMissile.asset.meta",
     asset_meta(G_ASSET["VesselCombatHitByMissile"]))

emit("Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByMissileBlast.asset",
     HEADER_FOR(G_SCRIPT["VesselCombatHitByExplosionEffectSO"], "VesselCombatHitByMissileBlast") +
     f"""  hitClass: 1
  onCombatHitLanded: {{fileID: 11400000, guid: {G_ASSET['Event_CombatHitStats']}, type: 2}}
  sameVictimCooldownSeconds: {SAME_VICTIM_COOLDOWN}
""")
emit("Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByMissileBlast.asset.meta",
     asset_meta(G_ASSET["VesselCombatHitByMissileBlast"]))


# ── 4. Wire them onto the Sparrow's weapons ─────────────────────────────────
#
# BULLETS: append to the full-auto container's existing projectileShipEffects (which already
# spins and shrinks the victim's skimmer) - the scoring effect is additive and changes nothing
# about how a bullet already feels.
FULLAUTO_PATH = ("Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/"
                 "SparrowFullAutoProjectileImpactContainer.asset")
fullauto = read(FULLAUTO_PATH)
bullet_ref = f"  - {{fileID: 11400000, guid: {G_ASSET['VesselCombatHitByBullet']}, type: 2}}\n"
if bullet_ref not in fullauto:
    m = re.search(r"^  projectileShipEffects:\n((?:  - \{[^}]*\}\n)*)", fullauto, re.M)
    assert m, "projectileShipEffects block not found on the full-auto container"
    fullauto = fullauto.replace(m.group(0), m.group(0) + bullet_ref, 1)
emit(FULLAUTO_PATH, fullauto)

# TURRET-STANCE PRISM SHOTS: the Sparrow's second fire mode, and it scores the same 1 point a
# bullet does. It is the same weapon class - a single direct projectile hit - just a different
# round, and its container already carries the SAME two victim effects the full-auto one does
# (spin + skimmer shrink), so the scoring effect slots in identically.
#
# It matters more here than the symmetry suggests: MASS stretches the fired prisms
# (SPARROW_TURRET_STANCE.md), so this is the fire mode the comeback buff visibly feeds - a
# trailing pilot's rounds get bigger, and now those bigger rounds are also worth something.
PRISMSHOT_PATH = ("Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/"
                  "SparrowPrismProjectileImpactContainer.asset")
prismshot = read(PRISMSHOT_PATH)
if bullet_ref not in prismshot:
    m = re.search(r"^  projectileShipEffects:\n((?:  - \{[^}]*\}\n)*)", prismshot, re.M)
    assert m, "projectileShipEffects block not found on the prism-projectile container"
    prismshot = prismshot.replace(m.group(0), m.group(0) + bullet_ref, 1)
emit(PRISMSHOT_PATH, prismshot)

# ── 4b. THE TURRET'S MUZZLES — the reason turret shots did nothing ──────────
#
# The Sparrow carries TWO pairs of gun transforms, one per fire mode, and they had drifted 13.8
# units apart:
#
#     FullAutoActionExecutor/Guns       (bullets)        LeftGun/RightGun at z =  1.30
#     FullAutoBlockActionExecutor/Guns  (turret prisms)  LeftGun/RightGun at z = 15.13
#
# A shot is BORN at its muzzle, so every turret round spawned 15 units ahead of the nose and the
# first 15 units of its path simply did not exist. In a wreck-field dogfight — a mode built
# specifically for close passes — the enemy is routinely inside that gap, so the round appeared
# already past them and hit nothing. No damage, and therefore no points, no matter how correctly
# the scoring was wired. (Playtest: "shooting with the turrets does not do any damage. Maybe
# because the point of origin of bullets for the sparrow is too far away from the model" —
# exactly right.)
#
# Both pairs are bare Transforms (no renderer, no VFX, no children), so this is purely where the
# shot starts. They are moved onto the bullets' position, which is also the documented rule for
# this weapon: SPARROW_TURRET_STANCE.md — "a turret shot IS a bullet — you just see a prism
# flying". Range is unaffected: the executor computes `anchor = muzzle + forward * range`, so
# moving the muzzle back moves the anchor back with it and the path length is identical.
SPARROW_PREFAB = "Assets/_Prefabs/Spacevessels/Sparrow.prefab"
sparrow = read(SPARROW_PREFAB)
TURRET_MUZZLES = (
    ("  m_LocalPosition: {x: 3, y: 0.4, z: 15.13}\n",
     "  m_LocalPosition: {x: 3.2, y: 0.4, z: 1.3}\n"),
    ("  m_LocalPosition: {x: -3, y: 0.4, z: 15.13}\n",
     "  m_LocalPosition: {x: -3.2, y: 0.4, z: 1.3}\n"),
)
for old_pos, new_pos in TURRET_MUZZLES:
    if old_pos in sparrow:
        assert sparrow.count(old_pos) == 1, f"turret muzzle position {old_pos!r} is not unique"
        sparrow = sparrow.replace(old_pos, new_pos, 1)
emit(SPARROW_PREFAB, sparrow)

# MISSILES, direct strike: same, on the skyburst container.
SKYBURST_PATH = ("Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/"
                 "SparrowSkyBurstProjectileImpactContainer.asset")
skyburst = read(SKYBURST_PATH)
missile_ref = f"  - {{fileID: 11400000, guid: {G_ASSET['VesselCombatHitByMissile']}, type: 2}}\n"
if missile_ref not in skyburst:
    m = re.search(r"^  projectileShipEffects:\n((?:  - \{[^}]*\}\n)*)", skyburst, re.M)
    assert m, "projectileShipEffects block not found on the skyburst container"
    skyburst = skyburst.replace(m.group(0), m.group(0) + missile_ref, 1)
emit(SKYBURST_PATH, skyburst)

# MISSILES, blast: the conic skyburst's ExplosionImpactor shipped with NO container at all
# (`explosionImpactorDataContainer: {fileID: 0}`), so AcceptImpactee returned immediately for
# every vessel it engulfed - a Sparrow rocket has never done anything to a pilot it did not hit
# dead-on. This is the container it was missing.
#
# Scoped to the CONIC prefab deliberately. The same detonation also spawns the shared
# AOEExplosion.prefab, which the Manta's crystal path uses too; hanging a "missile hit" on that
# would label a crystal blast as gunnery in every mode. The conic burst is the big one (scale
# 100-170) and is Sparrow-only, so it is the honest place for this.
emit("Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/"
     "SkyBurstExplosionImpactorDataContainer.asset",
     HEADER_FOR(EXISTING["ExplosionImpactorDataContainerSO"],
                "SkyBurstExplosionImpactorDataContainer") +
     f"""  vesselExplosionEffects:
  - {{fileID: 11400000, guid: {G_ASSET['VesselCombatHitByMissileBlast']}, type: 2}}
  explosionPrismEffects: []
""")
emit("Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/"
     "SkyBurstExplosionImpactorDataContainer.asset.meta",
     asset_meta(G_ASSET["SkyBurstExplosionContainer"]))

CONIC_PATH = "Assets/_Prefabs/Projectile/AOEConicSkyBurst.prefab"
conic = read(CONIC_PATH)
assert conic.count("  explosionImpactorDataContainer: ") == 1, \
    "expected exactly one ExplosionImpactor on AOEConicSkyBurst"
conic = re.sub(r"^  explosionImpactorDataContainer: \{fileID: 0\}$",
               f"  explosionImpactorDataContainer: {{fileID: 11400000, "
               f"guid: {G_ASSET['SkyBurstExplosionContainer']}, type: 2}}",
               conic, count=1, flags=re.M)
emit(CONIC_PATH, conic)


# ── 5. SpawnableBoneyard prefabs - ONE VARIANT PER INTENSITY ────────────────
# Same script, same seed; only the cover counts and the density knob differ, and
# BuildParameterHash keeps their generation caches distinct. The arena RADIUS never varies -
# the spawn shell and the silhouette are defined against it.
#
# spawnClearRadius is 0 on every variant, and that is deliberate rather than an oversight:
# players spawn on a shell at r=700, well outside the wreck field, so there is nothing to
# clear - and Emit's clearance rejection is the one term that would stop boneyard_budget.py
# from being an exact mirror of the C#.
for i in BONEYARD_INTENSITIES:
    density, hulks, spires, frames, overpasses = budget.INTENSITIES[i - 1]
    emit(f"Assets/_Prefabs/Spawnables/SpawnableBoneyard{i}.prefab", f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &5260000000000501
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 5260000000000502}}
  - component: {{fileID: 5260000000000503}}
  m_Layer: 0
  m_Name: SpawnableBoneyard
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &5260000000000502
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000501}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &5260000000000503
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000501}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableBoneyard']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: {budget.SEED}
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: {i}
  prism: {{fileID: {PRISM_FILEID}, guid: {EXISTING['Prism_prefab']}, type: 3}}
  density: {density}
  spawnClearRadius: 0
  spawnClearPoints: []
  arenaRadius: {budget.ARENA_RADIUS}
  crustDepth: {budget.CRUST_DEPTH}
  hulkCount: {hulks}
  spireCount: {spires}
  frameCount: {frames}
  overpassCount: {overpasses}
  debrisFields: {budget.DEBRIS_FIELDS}
  fieldRadiusFraction: {budget.FIELD_RADIUS_FRACTION}
  coreClearRadius: {budget.CORE_CLEAR_RADIUS}
  driftFraction: {budget.DRIFT_FRACTION}
  driftHeight: {budget.DRIFT_HEIGHT}
""")
    emit(f"Assets/_Prefabs/Spawnables/SpawnableBoneyard{i}.prefab.meta",
         prefab_meta(G_ASSET[f"SpawnableBoneyard{i}.prefab"]))


# ── 6. Scoring rule ──────────────────────────────────────────────────────────
# metric 8 = ScoringMetric.CombatPoints. Golf: the winning domain's pilots carry a finish time,
# everyone else a sentinel, so lower is better. The hit VALUES live here rather than in the
# metric reader - see DogFightScoringRuleSO's class summary.
emit("Assets/_SO_Assets/Scoring Rules/DogFightScoringRule.asset",
     HEADER_FOR(G_SCRIPT["DogFightScoringRuleSO"], "DogFightScoringRule") +
     "  metric: 8\n  golfRules: 1\n  bulletPoints: 1\n  missilePoints: 50\n")
emit("Assets/_SO_Assets/Scoring Rules/DogFightScoringRule.asset.meta",
     asset_meta(G_ASSET["DogFightScoringRule"]))


# ── 7. Arcade game config ────────────────────────────────────────────────────
# SPARROW ONLY: a single entry in Vessels is what drives all three enforcement layers
# (GameDataSO.SyncFromArcadeGame's launcher clamp, ServerPlayerVesselInitializer's server-side
# spawn clamp, and the AI clamp in ServerPlayerVesselInitializerWithAI).
#
# MinDomainsAllowed is 2, and that is a RULE rather than a preference: teammates cannot damage
# each other (Projectile.DisallowImpactOnVessel refuses own-domain contact), so a lobby that
# launched with everyone on one domain would have no legal targets at all.
emit("Assets/_SO_Assets/Games/ArcadeGameDogFight.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameDogFight") + f"""  Mode: 41
  IsMultiplayer: 1
  DisplayName: Dog Fight
  Description: Sparrows only, in the wreck of a world that already lost. Guns score
    one, rockets score fifty, and the Boneyard is full of places to disappear into -
    hollow hulks, leaning spires, a reactor nobody should loiter near. First domain
    to the point target takes it.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  PreviewClip: {{fileID: {PREVIEW_FILEID}, guid: {EXISTING['PreviewClip']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameDogFight
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
emit("Assets/_SO_Assets/Games/ArcadeGameDogFight.asset.meta",
     asset_meta(G_ASSET["ArcadeGameDogFight"]))


# ── 8. Cell configs + spawn profiles + the scavengers ───────────────────────
#
# One CellConfigDataSO PER INTENSITY, because PhaseThresholds must ride ITS OWN baseline (the
# arena runs 12.7k - 24.1k prisms), and one SpawnProfileSO per intensity so the scavenger
# population tracks the amount of wreckage there is to pick over.
#
# NO NUCLEUS by design: the Boneyard's centre is a STRUCTURE (the reactor), not a nucleus, so
# there is no territorial claim to contest - Dog Fight scores gunnery and nothing else, and a
# node-control zone would be a second, silent objective nobody is playing for.
FOLDER = "Assets/_SO_Assets/Cell Configs/Boneyard Cell"
emit(FOLDER + ".meta", meta(guid("folder/BoneyardCell"), folder=True))

palette = "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in QUADFISH_PALETTE)

for i in INTENSITIES:
    row = budget.all_intensities()[i - 1]
    seed_pop, cap_pop = SCAVENGER_SEED[i - 1], SCAVENGER_CAP[i - 1]

    emit(f"{FOLDER}/Boneyard Scavenger {i}.asset",
         HEADER_FOR(EXISTING["FaunaConfigurationSO"], f"Boneyard Scavenger {i}") +
         f"""  FaunaPrefab: {{fileID: {QUADFISH_FILEID}, guid: {EXISTING['QuadFishPrefab']}, type: 3}}
  InitialSpawnCount: {seed_pop}
  PopulationSize: {seed_pop}
  SpawnProbability: 1
  FeedsPerOffspring: 20
  OffspringPerBirth: 1
  ReproductionCooldownSeconds: 12
  MaxLivePopulation: {cap_pop}
  ReleaseTier: 0
  BandInnerRadius: 0
  BandOuterRadius: 0
  CenterFocusBias: 0
  Element: 0
  Variant:
    Enabled: 0
  SpreadElements: 1
  ElementPalette:
{palette}""")
    emit(f"{FOLDER}/Boneyard Scavenger {i}.asset.meta",
         asset_meta(G_ASSET[f"BoneyardScavenger{i}"]))

    emit(f"{FOLDER}/Boneyard Spawn Profile {i}.asset",
         HEADER_FOR(EXISTING["SpawnProfileSO"], f"Boneyard Spawn Profile {i}") + f"""  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  SupportedFloras: []
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 4
  InitialFaunaReleaseTier: 0
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 30
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 0
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 0
  HerbivoreSpawnPointCount: 0
  HerbivoreSpawnRadius: 0
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 0
  SupportedFaunas:
  - {{fileID: 11400000, guid: {G_ASSET[f'BoneyardScavenger{i}']}, type: 2}}
""")
    emit(f"{FOLDER}/Boneyard Spawn Profile {i}.asset.meta",
         asset_meta(G_ASSET[f"BoneyardSpawnProfile{i}"]))

    density, hulks, spires, frames, overpasses = budget.INTENSITIES[i - 1]

    # Every intensity is the SAME arena at a different density - one recipe, four settings of its
    # dials. Nothing about the silhouette, the radius or the family mix changes with intensity;
    # only how much wreckage there is to hide behind.
    env_ref = (f"{{fileID: 5260000000000503, "
               f"guid: {G_ASSET[f'SpawnableBoneyard{i}.prefab']}, type: 3}}")
    description = (
        f"The Dog Fight arena at intensity {i} - {row['total']} prisms of wreckage\n"
        f"    inside r={budget.ARENA_RADIUS:.0f}: {hulks} hollow hulks to hide in, {spires} leaning spires,\n"
        f"    {frames} girder cages, {overpasses} broken overpasses, and {row['danger']} danger traps on the torn\n"
        "    ends and around the reactor, scattered into debris fields with an open centre.\n"
        "    Cover, never an objective - shooting it scores nothing. NO NUCLEUS by design.\n"
        "    PhaseThresholds ride THIS intensity's own baseline; regenerate with\n"
        "    Tools/Build/author_dogfight_assets.py after any change rather than hand-editing.")
    th = budget.phase_thresholds(row["total"], row["volume"])
    thresholds = f"""  PhaseThresholds:
    RestlessEnter: {th['RestlessEnter']}
    RestlessExit: {th['RestlessExit']}
    FrenzyEnter: {th['FrenzyEnter']}
    FrenzyExit: {th['FrenzyExit']}
    RestlessEnterVolume: {th['RestlessEnterVolume']}
    RestlessExitVolume: {th['RestlessExitVolume']}
    FrenzyEnterVolume: {th['FrenzyEnterVolume']}
    FrenzyExitVolume: {th['FrenzyExitVolume']}
"""

    emit(f"{FOLDER}/Boneyard Cell Config {i}.asset",
         HEADER_FOR(EXISTING["CellConfigDataSO"], f"Boneyard Cell Config {i}") +
         f"""  CellName: Boneyard
  Description: {description}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['Membrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'BoneyardSpawnProfile{i}']}, type: 2}}
  EnvironmentPrefab: {env_ref}
  EnvironmentIntensity: {i}
  SenseRadiusOverride: {SENSE_RADIUS}
""" + thresholds)
    emit(f"{FOLDER}/Boneyard Cell Config {i}.asset.meta",
         asset_meta(G_ASSET[f"BoneyardCellConfig{i}"]))


# ── 9. Scene: clone MinigameRampage, swap the mode-specific wiring ───────────
#
# ONE-SHOT, AND THE DONOR HAS MOVED ON. This script cloned the Rampage scene once; Dog Fight's
# scene exists and is committed, so its output is landed and nothing here needs to run again.
# It can no longer run: the Rampage rework deleted RampageController.arenaCell /
# aiRetargetSeconds and replaced the donor's single Cell config with four per-intensity ones,
# so the asserts below fire on a donor that is simply a different scene now. That is the
# expected end state of a migration generator, not a break to repair - if Dog Fight ever needs
# re-authoring, re-point it at a current donor rather than trying to satisfy these asserts.
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity")

# 9a. turn monitor script swap (field set is identical - base TurnMonitor fields only)
scene, n = re.subn(EXISTING["RampagePrismTurnMonitor"], G_SCRIPT["DogFightPointTurnMonitor"], scene)
assert n == 1, f"turn monitor guid appeared {n} times"

# 9b. controller script swap + its serialized field block
scene, n = re.subn(EXISTING["RampageController"], G_SCRIPT["DogFightController"], scene)
assert n == 1, f"controller guid appeared {n} times"

OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['RampageScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  aiRetargetSeconds: 1.5
"""
NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['DogFightScoringRule']}, type: 2}}
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
assert OLD_FIELDS in scene, "controller field block not found in donor scene"
scene = scene.replace(OLD_FIELDS, NEW_FIELDS)

# 9c. Cell: swap the donor's single config for the FOUR per-intensity configs and flip the
# choice mode to IntensityWise - the platform's own way to vary a cell by intensity.
OLD_CELL = f"""  CellConfigs:
  - {{fileID: 11400000, guid: {EXISTING['RampageCellConfig']}, type: 2}}
  cellTypeChoiceOptions: 0
"""
NEW_CELL = "  CellConfigs:\n" + "".join(
    f"  - {{fileID: 11400000, guid: {G_ASSET[f'BoneyardCellConfig{i}']}, type: 2}}\n"
    for i in INTENSITIES) + "  cellTypeChoiceOptions: 1\n"
assert OLD_CELL in scene, "donor Cell config block not found"
scene = scene.replace(OLD_CELL, NEW_CELL)

# 9d. Spawn on a SPHERE outside the wreck field. The donor's four authored transforms sit at
# +/-50 - dead centre of the arena, which here is inside the reactor. Switch to the computed
# cell spawn ring (CellSpawnFormation, all facing the cell) with a radius FLOOR, because this
# cell has no nucleus for the ring to measure off.
#
# spawnFormation 0 = Symmetric: spread over a SPHERE rather than a horizontal circle. A
# dogfight arena has no meaningful "up" - the crust is a bowl, not a floor with a ceiling - so
# there is no pole to be unfair about, and a spherical spread means the opening merge comes
# from every direction instead of everyone converging on one plane.
OLD_SPAWN = """  playerSpawnPoints:
  - {fileID: 1468661147}
  - {fileID: 1074736317}
  - {fileID: 1323644424}
  - {fileID: 1564881929}
  preSpawnDelayMs: 200
"""
NEW_SPAWN = f"""  playerSpawnPoints:
  - {{fileID: 1468661147}}
  - {{fileID: 1074736317}}
  - {{fileID: 1323644424}}
  - {{fileID: 1564881929}}
  arrangeSpawnPointsAroundCell: 1
  spawnDistanceOutsideNucleus: 40
  spawnFormation: 0
  spawnRingRadiusFloor: {SPAWN_RING_RADIUS}
  cellData: {{fileID: 11400000, guid: {EXISTING['RuntimeCellData']}, type: 2}}
  preSpawnDelayMs: 200
"""
assert OLD_SPAWN in scene, "donor spawn-point block not found"
scene = scene.replace(OLD_SPAWN, NEW_SPAWN)

# 9e. THE OMNI CRYSTAL SPAWNS NORMALLY - it just stops spawning at the origin.
#
# The donor's settings are already the platform-normal ones (crystalCountMode 0, one crystal,
# spawnOnClientReady), identical to PeelTheCage, and they are kept verbatim. What the donor does NOT
# author is `noNucleusSpawnRadius`, and in THIS cell that is the whole problem:
# CrystalManager.GetAnchorlessSpawnRadius resolves the cell's NUCLEUS radius FIRST (the crystal
# volume and the nucleus are coupled platform-wide and no scene may override that - see
# Docs/ECOSYSTEM.md 27.6), the Boneyard has no nucleus by design, so it fell through to the
# crystal's own SphereRadius - a few units - and every spawn landed on the arena's exact centre.
# `noNucleusSpawnRadius` is the FALLBACK that exists for exactly this case: a cell with no core. A large faceted sphere pinned to the middle
# of a gunnery arena reads as THE objective, which is how it got mistaken for a ball.
#
# Authoring the radius is the fix the field exists for: the crystal now draws from a ball that
# covers the wreck field, so it hides among the hulks and moves somewhere new on every respawn -
# a thing you go and find, not a monument in the middle of the map.
OLD_CRYSTALS = "  crystalCountMode: 0\n  fixedCrystalCount: 1\n"
NEW_CRYSTALS = (f"  noNucleusSpawnRadius: {OMNI_SPAWN_RADIUS}\n"
                f"  crystalCountMode: 0\n  fixedCrystalCount: {OMNI_CRYSTAL_COUNT}\n")
assert OLD_CRYSTALS in scene, "donor crystal-count block not found"
scene = scene.replace(OLD_CRYSTALS, NEW_CRYSTALS, 1)

# 9f. SPARROW-ONLY, third layer: the AI templates. ServerPlayerVesselInitializerWithAI clamps
# these through GameDataSO.ClampVesselToGame anyway, but authoring the right class here means
# the scene is honest on its own and the clamp never has to fire. 3 = Rhino (Rampage's donor
# value), 11 = Sparrow.
scene, n = re.subn(r"^  - vesselClass: 3$", "  - vesselClass: 11", scene, flags=re.M)
assert n == 4, f"expected 4 AI vessel templates, patched {n}"

emit("Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity.meta",
     scene_meta(G_ASSET["MinigameDogFight.unity"]))


# ── 10. Register the card in the party-games list ───────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameDogFight']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
emit(LIST_PATH, games)


# ── 11. Always-unlocked so the card is clickable on a fresh account ─────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 41\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 41\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 12. Build settings ──────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameDogFight.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameRampage\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Rampage scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity\n"
                          f"    guid: {G_ASSET['MinigameDogFight.unity']}\n")
emit(BUILD_PATH, build)


# ── 13. End-game condition target ───────────────────────────────────────────
# The shared overrides asset is what FrogletTools > Game Modes > End Game Conditions edits. A
# missing key would silently fall back to the C# field initializer, so author both the live and
# the build-baseline value explicitly.
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("wildlifeKillTarget", "dogFightPointTarget"),
                          ("wildlifeKillTargetBuild", "dogFightPointTargetBuild")):
    # SET, don't just add. An earlier version only inserted the key when it was absent, so
    # re-running after a target change left the asset on the OLD number while the C# default and
    # every doc said the new one - and the asset is what the game actually reads.
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0),
                                  f"  {new_key}: {DOGFIGHT_POINT_TARGET}\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH} - run author_wildlife_liberation_assets.py first"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {DOGFIGHT_POINT_TARGET}\n", 1)
emit(END_PATH, endcond)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

all_new = list(G_SCRIPT.values()) + list(G_ASSET.values()) + [guid("folder/BoneyardCell")]
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
for g in QUADFISH_PALETTE:
    if g not in existing_guids:
        errors.append(f"QuadFish element-palette GUID {g} does not resolve to any asset")

# the scene must no longer mention the donor's mode-specific guids
sc = files["Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity"]
for name in ("RampageController", "RampagePrismTurnMonitor", "RampageCellConfig", "RampageScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("DogFightController", "DogFightPointTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if G_ASSET["DogFightScoringRule"] not in sc:
    errors.append("cloned scene missing the scoring rule reference")
for i in INTENSITIES:
    if G_ASSET[f"BoneyardCellConfig{i}"] not in sc:
        errors.append(f"cloned scene missing cell config {i}")
if "  cellTypeChoiceOptions: 1\n" not in sc:
    errors.append("scene Cell is not on CellTypeChoiceOptions.IntensityWise - "
                  "the per-intensity configs would never be selected")
if "vesselClass: 3" in sc:
    errors.append("scene still authors a non-Sparrow AI vessel class")
if f"  fixedCrystalCount: {OMNI_CRYSTAL_COUNT}\n" not in sc:
    errors.append("scene does not spawn the omni crystal - crystals are a platform fundamental, "
                  "not mode furniture")
if f"  noNucleusSpawnRadius: {OMNI_SPAWN_RADIUS}\n" not in sc:
    errors.append("scene does not author noNucleusSpawnRadius - this cell has no nucleus, so "
                  "the omni crystal would fall back to its own SphereRadius and spawn on the "
                  "arena's exact centre")
if sc.count("vesselClass: 11") != 4:
    errors.append("scene does not author 4 Sparrow AI templates")

# GameDataSO must carry the combat-hit channel, or nothing scores
if f"OnCombatHitLanded: {{fileID: 11400000, guid: {G_ASSET['Event_CombatHitStats']}" not in files[GAMEDATA_PATH]:
    errors.append("Runtime GameData is missing the OnCombatHitLanded channel - no hit would score")

# The three weapon wirings ARE the feature. Each is checked against the file this run will
# actually write, so a donor whose shape changed upstream fails here rather than at play time.
if G_ASSET["VesselCombatHitByBullet"] not in files[FULLAUTO_PATH]:
    errors.append("the full-auto container does not carry the bullet scoring effect - "
                  "bullet hits would never score")
if G_ASSET["VesselCombatHitByBullet"] not in files[PRISMSHOT_PATH]:
    errors.append("the prism-projectile container does not carry the bullet scoring effect - "
                  "the Sparrow's turret stance would be worth nothing")

# The turret's muzzles must sit where the BULLETS' do. Wiring the scoring effect is worthless if
# the shot is born past the target, which is exactly what shipped once (see section 4b).
_sparrow = files[SPARROW_PREFAB]
_bullet_muzzles = _sparrow.count("  m_LocalPosition: {x: 3.2, y: 0.4, z: 1.3}\n") + \
                  _sparrow.count("  m_LocalPosition: {x: -3.2, y: 0.4, z: 1.3}\n")
if _bullet_muzzles != 4:
    errors.append(f"the Sparrow should carry FOUR gun transforms on the bullets' position "
                  f"(two bullet + two turret); found {_bullet_muzzles}. The turret's muzzles "
                  "have drifted forward again - a turret shot is born past its target and "
                  "hits nothing at close range")
if "z: 15.13}" in _sparrow:
    errors.append("a Sparrow gun transform is still at z=15.13 - the far-forward turret muzzle "
                  "that made close-range turret fire do nothing")
if G_ASSET["VesselCombatHitByMissile"] not in files[SKYBURST_PATH]:
    errors.append("the skyburst container does not carry the missile scoring effect - "
                  "direct rocket hits would never score")
if G_ASSET["SkyBurstExplosionContainer"] not in files[CONIC_PATH]:
    errors.append("AOEConicSkyBurst has no explosion container - a rocket's BLAST would never "
                  "score, which is most of what a rocket does")
if "explosionImpactorDataContainer: {fileID: 0}" in files[CONIC_PATH]:
    errors.append("AOEConicSkyBurst still has a null explosion container")

# The two effects that share a latch window must AGREE on it, or a direct rocket strike scores
# twice (once for the hit, once for its own blast).
direct = files["Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByMissile.asset"]
blast = files["Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByMissileBlast.asset"]


def cooldown_of(body):
    m = re.search(r"^  sameVictimCooldownSeconds: ([\d.]+)", body, re.M)
    return float(m.group(1)) if m else None


if cooldown_of(direct) != cooldown_of(blast) or not cooldown_of(direct):
    errors.append("the missile direct-hit and blast effects must share one non-zero "
                  "sameVictimCooldownSeconds - they claim the same latch window")

# Sparrow-only must be a SINGLE entry, or the clamps let another hull through
arcade = files["Assets/_SO_Assets/Games/ArcadeGameDogFight.asset"]
vessels = re.search(r"^  Vessels:\n((?:  - .*\n)*)", arcade, re.M)
if not vessels or vessels.group(1).count("- {fileID") != 1:
    errors.append("ArcadeGameDogFight must author EXACTLY ONE vessel (Sparrow)")
elif EXISTING["Vessel_Sparrow"] not in vessels.group(1):
    errors.append("ArcadeGameDogFight's single vessel is not Sparrow")
if "MinDomainsAllowed: 2" not in arcade:
    errors.append("ArcadeGameDogFight must require at least TWO domains - teammates cannot "
                  "damage each other, so a one-domain lobby has no legal targets")

# The comeback rate only means anything relative to the TARGET, and the two have already drifted
# apart once (the rate was scaled for a 500-point target and left behind when it became 120).
# A quarter-of-target deficit must be worth at least a whole element level, or the buff is
# arithmetic nobody can feel - which is a silently dead system, not a tuning choice.
_quarter_deficit_levels = (DOGFIGHT_POINT_TARGET * 0.25) * COMEBACK_RATE
if _quarter_deficit_levels < 1.0:
    errors.append(f"ComebackRatePerScoreDeficit {COMEBACK_RATE} is too small for a "
                  f"{DOGFIGHT_POINT_TARGET}-point target: a quarter-of-target deficit buys only "
                  f"{_quarter_deficit_levels:.2f} element levels, which is invisible")


# serialized MonoBehaviour keys must exist on the C# class (asset-surgery §3)
def cs_fields(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as fh:
        src = fh.read()
    out = set()
    TYPE = r"[\w<>,\[\]\?\.]+"
    # The modifier group must span every combination that appears in this codebase, in any
    # order - the copy of this helper in author_wildlife_liberation_assets.py already learned
    # that lesson once for `static`/`const`/`readonly` and reported a real field as MISSING.
    # `virtual`/`override`/`abstract` are added here for the same reason.
    MODS = (r"(?:(?:public|protected|private|internal|static|const|readonly|new|virtual|"
            r"override|abstract|sealed|partial)\s+)+")
    for m in re.finditer(MODS + TYPE + r"\s+(\w+)\s*(?:=|;|\{|=>|\()", src):
        out.add(m.group(1))
    for m in re.finditer(r"\[SerializeField[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
                         r"(?:(?:public|protected|private|internal)\s+)?"
                         + TYPE + r"\s+(\w+)\s*(?:=|;)", src):
        out.add(m.group(1))
    # INTERFACE members carry no access modifier at all (`int CombatPoints { get; set; }`), so
    # the modifier-anchored patterns above cannot see them. IRoundStats is an interface and is
    # exactly what the stat checks below read, so without this branch every one of them
    # reports a false MISSING.
    for m in re.finditer(r"^\s{4,}" + TYPE + r"\s+(\w+)\s*\{\s*get;", src, re.M):
        out.add(m.group(1))
    return out


CHECKS = [
    (f"{FOLDER}/Boneyard Cell Config {i}.asset",
     "Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs") for i in INTENSITIES
] + [
    (f"{FOLDER}/Boneyard Spawn Profile {i}.asset",
     "Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs") for i in INTENSITIES
] + [
    (f"{FOLDER}/Boneyard Scavenger {i}.asset",
     "Assets/_Scripts/Utility/DataContainers/FaunaConfigurationSO.cs") for i in INTENSITIES
] + [
    ("Assets/_SO_Assets/Games/ArcadeGameDogFight.asset",
     "Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs"),
    ("Assets/_SO_Assets/Scoring Rules/DogFightScoringRule.asset",
     "Assets/_Scripts/Controller/Arcade/Scoring/DogFightScoringRuleSO.cs"),
    ("Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByBullet.asset",
     "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Projectile Effects/VesselCombatHitByProjectileEffectSO.cs"),
    ("Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByMissileBlast.asset",
     "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Explosion Effects/VesselCombatHitByExplosionEffectSO.cs"),
]

BONEYARD_CS = "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableBoneyard.cs"
for field in ("arenaRadius", "crustDepth", "hulkCount", "spireCount", "frameCount", "overpassCount"):
    if field not in cs_fields(BONEYARD_CS):
        errors.append(f"SpawnableBoneyard.cs has no '{field}' field - the per-intensity prefab "
                      f"variants would all build the same arena")
for stat in ("BulletHitsLanded", "MissileHitsLanded", "CombatPoints"):
    if stat not in cs_fields("Assets/_Scripts/Data/Enums/IRoundStats.cs"):
        errors.append(f"IRoundStats.cs has no '{stat}' - gunnery would have nowhere to record")
if "OnCombatHitLanded" not in cs_fields("Assets/_Scripts/Utility/DataContainers/GameDataSO.cs"):
    errors.append("GameDataSO.cs has no 'OnCombatHitLanded' channel - no hit would score")
if "PointsForCombatHit" not in cs_fields("Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs"):
    errors.append("ScoringRuleSO.cs has no 'PointsForCombatHit' - a landed hit would be worth "
                  "nothing in every mode including this one")

SO_BASE = {"CellName", "Description", "Icon", "Difficulty", "CellEndGameScore", "Mode",
           "IsMultiplayer", "DisplayName", "IconActive", "IconInactive", "CardBackground",
           "PreviewClip", "GolfScoring", "SceneName"}
for asset_path, cs_path in CHECKS:
    keys = set(re.findall(r"^  (\w+):", files[asset_path], re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
        "m_EditorClassIdentifier"}
    known = cs_fields(cs_path) | SO_BASE
    for extra in ("Assets/_Scripts/ScriptableObjects/SO_Game.cs",
                  "Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs",
                  "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Abstract Effect Types/VesselProjectileEffectSO.cs"):
        if os.path.exists(os.path.join(ROOT, extra)):
            known |= cs_fields(extra)
    unknown = keys - known
    if unknown:
        errors.append(f"{os.path.basename(asset_path)}: keys not found on "
                      f"{os.path.basename(cs_path)}: {sorted(unknown)}")

# every intensity flies its OWN Boneyard variant and carries its OWN ladder
for _i in INTENSITIES:
    _cfg = files[f"{FOLDER}/Boneyard Cell Config {_i}.asset"]
    if G_ASSET[f"SpawnableBoneyard{_i}.prefab"] not in _cfg:
        errors.append(f"intensity {_i} does not reference its own Boneyard prefab variant")
    if "PhaseThresholds:" not in _cfg:
        errors.append(f"intensity {_i} authors no PhaseThresholds - it would fall back to the "
                      "default ladder instead of riding its own measured baseline")

# LEVELS MUST BE SPREAD. The playtest ask was that picking an intensity should change the match,
# so each step up must add a MEANINGFUL amount of cover, not a rounding error. 25% per step is
# the floor; the shipped ladder runs +78 / +54 / +40%.
_rows = budget.all_intensities()
for _i in range(1, len(_rows)):
    _lo, _hi = _rows[_i - 1]["total"], _rows[_i]["total"]
    if _hi < _lo * 1.25:
        errors.append(f"intensity {_i + 1} ({_hi:,} prisms) is less than 25% denser than "
                      f"intensity {_i} ({_lo:,}) - the levels are not spread apart")

# the prefab variants must actually differ, or "intensity" is a lie
prefab_bodies = [files[f"Assets/_Prefabs/Spawnables/SpawnableBoneyard{i}.prefab"] for i in BONEYARD_INTENSITIES]
if len(set(prefab_bodies)) != len(prefab_bodies):
    errors.append("two Boneyard prefab variants are byte-identical - intensity would do nothing")

# thresholds must rise with the baseline, or a denser arena would sit in a lower phase
prev = -1
for i in BONEYARD_INTENSITIES:
    body = files[f"{FOLDER}/Boneyard Cell Config {i}.asset"]
    enter = int(re.search(r"^    RestlessEnter: (\d+)", body, re.M).group(1))
    if enter <= prev:
        errors.append(f"intensity {i}'s RestlessEnter ({enter}) does not exceed intensity "
                      f"{i - 1}'s ({prev}) - the phase ladder is not riding its own baseline")
    prev = enter

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
