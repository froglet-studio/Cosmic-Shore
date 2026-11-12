#!/usr/bin/env python3
"""Author the Manta spec-remake asset set (Sting / Kabloom / the map re-cut).

One-shot generator in the author_salvo_assets.py family: deterministic guids
(md5 of a stable name), idempotent (re-runs rewrite the same bytes), --check
compares and exits nonzero on drift. It owns:

  * AOEMantaBloom.prefab           - the bomb bloom (flat copy of AOEExplosion.prefab,
                                     same internal fileIDs by convention, own guid, plus the
                                     Manta bloom effect container the base prefab lacks)
  * MantaBombDebuffByExplosionEffect.asset  - Mass+Space debuff (04/20/2026 rule: overtakers
                                     never touch Time; Charge stays the victim's own economy)
  * MantaBloomExplosionImpactorDataContainer.asset
  * MantaStingConfig.asset
  * MantaStingSkimPrismEffect.asset / MantaStingPlantBombVesselEffect.asset
  * MantaKabloomByCrystalEffect.asset
  * MantaDetonateByPrismEffect.asset - hull hits a prism: every planted bomb goes off (2026-11-07 overhaul)
  * MantaDecoyByCrystalEffect.asset  - a collected crystal leaves a decoy (the Mass ability)
  * MantaStingSkimmerImpactorDataContainer.asset  - REWRITES the renamed
                                     MantaOvercharge... container in place (guid preserved,
                                     so Manta.prefab's SkimmerImpactor override never moves)
  * Resources/ElementalAbilityMaps/Manta.asset    - the spec map re-cut

The Bloomrush mode set lives in author_bloomrush_assets.py, not here.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK = "--check" in sys.argv


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/MantaRemake/{name}".encode()).hexdigest()


# ── Script guids (owned by the .cs.meta files, read here as constants) ────────
SCRIPT = {
    "MantaStingConfigSO":              "74a5f56c62b268ee03e09ae3a9e8811b",
    "MantaStingSkimPrismEffectSO":     "2dcf021070794c32ad8fedb2fc9c5ede",
    "MantaStingPlantBombVesselEffectSO": "93535b5ea9fb9381d319896eae6b11f8",
    "MantaKabloomByCrystalEffectSO":   "4de6a983ee9f3d43306090bb86b07bf2",
    "MantaPlantBombByLifeformJoustEffectSO": "99e1d40fff3139f2a58cf225fdea541b",
    "MantaDetonateByPrismEffectSO":    "d005686f0e6a48e7835888c602e7a0a7",
    "MantaDecoyByCrystalEffectSO":     "13045f1c8b2741c4a33864a1803978f3",
    "VesselElementalDebuffByExplosionEffectSO": "c2e449f647cc48e9be66d67fbc166a33",
    "ExplosionImpactorDataContainerSO": "841db4ce66da4384a711272307733e0f",
    "SkimmerImpactorDataContainerSO":  "07132fee89b7c9d4ea24a34ffcf1d9f3",  # resolved below
    "VesselImpactorDataContainerSO":   "fde7d8c2dacf45f99e46799ffeea15e6",
    "ElementalAbilityMapSO":           "4b3f2a8c8f344355b16a4d4296ae3f98",
}

# ── Existing asset guids (referenced, never re-minted) ───────────────────────
AOE_EXPLOSION_PREFAB_GUID = "4a855af160021d241b8c1ac70cf2d792"
AOE_EXPLOSION_COMPONENT_FILEID = 3479271500403630839
AOE_FLOWER_PREFAB_GUID = "f5ecfb9923155a84c860975d559b2714"
AOE_FLOWER_COMPONENT_FILEID = 6875622972764638977
COMBAT_HIT_BY_BLAST_GUID = "fb13e2c96ac07b6d086178c042518dcf"
# SkimmerHapticsByPrismEffect is deliberately NOT in the Sting container: the Manta's skim has no haptic
# (Caleb, 2026-11-11). A pulse per entry into the 20-40 unit sphere held the pad at a steady hum in dense
# prisms; the pad now speaks only on a hull collision and a bomb plant (Docs/HAPTICS.md, the Manta row).
STING_CONTAINER_GUID = "911a107d002544e47bc70cef1714a1a6"  # the renamed overcharge container

# ── Minted guids for the new assets ──────────────────────────────────────────
BLOOM_PREFAB_GUID = guid_for("AOEMantaBloom.prefab")
BOMB_DEBUFF_GUID = guid_for("MantaBombDebuffByExplosionEffect.asset")
BLOOM_CONTAINER_GUID = guid_for("MantaBloomExplosionImpactorDataContainer.asset")
STING_CONFIG_GUID = guid_for("MantaStingConfig.asset")
SKIM_EFFECT_GUID = guid_for("MantaStingSkimPrismEffect.asset")
PLANT_EFFECT_GUID = guid_for("MantaStingPlantBombVesselEffect.asset")
KABLOOM_EFFECT_GUID = guid_for("MantaKabloomByCrystalEffect.asset")
JOUST_PLANT_EFFECT_GUID = guid_for("MantaPlantBombByLifeformJoustEffect.asset")
DETONATE_EFFECT_GUID = guid_for("MantaDetonateByPrismEffect.asset")
DECOY_EFFECT_GUID = guid_for("MantaDecoyByCrystalEffect.asset")

# The decoy is built from the real crystal prefabs: the omni one is referenced here, the four elemental
# ones come from Resources/ElementalCrystalSet at runtime. The morph is the fleet's danger-block hemisphere.
OMNI_CRYSTAL_PREFAB_GUID = "54802b89e00a0ed4281025fa5e770811"       # Crystal.prefab
OMNI_CRYSTAL_ROOT_FILEID = 5535990081244205914
DANGER_BLOCKS_PREFAB_GUID = "c3ab658461bdc764ea9f7da47252c0ba"      # AOEDangerBlocks.prefab
DANGER_BLOCKS_COMPONENT_FILEID = 3631957846213860199

SO_HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier:
"""

ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

PREFAB_META = """fileFormatVersion: 2
guid: {guid}
PrefabImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def so_asset(script_key: str, name: str, body: str) -> str:
    return SO_HEADER.format(script=SCRIPT[script_key], name=name) + body


def resolve_skimmer_container_script_guid() -> str:
    meta = os.path.join(
        ROOT, "Assets/_Scripts/Controller/ImpactEffects/Containers/SkimmerImpactorDataContainerSO.cs.meta")
    for line in open(meta):
        if line.startswith("guid:"):
            return line.split()[1]
    raise SystemExit("SkimmerImpactorDataContainerSO.cs.meta guid not found")


SCRIPT["SkimmerImpactorDataContainerSO"] = resolve_skimmer_container_script_guid()

FILES = {}

# ── AOEMantaBloom.prefab: flat copy + the bloom container ────────────────────
def build_bloom_prefab() -> str:
    src = open(os.path.join(ROOT, "Assets/_Prefabs/Projectile/AOEExplosion.prefab")).read()
    out = src.replace("m_Name: AOEExplosion", "m_Name: AOEMantaBloom")
    # The bloom is the destructive sphere wearing a different effect container. The donor now
    # authors one of its own (the skyburst's 20-point blast report), and Unity wraps a long
    # reference over two lines, so the swap is a REGEX over the whole key rather than a literal
    # match on what the donor happened to say the day this was written.
    pattern = re.compile(
        r"^  explosionImpactorDataContainer: \{fileID: \d+, guid: [0-9a-f]+,?\s*\n?\s*type: 2\}\n",
        re.M)
    assert pattern.search(out), (
        "AOEExplosion.prefab no longer authors an explosionImpactorDataContainer - the bloom "
        "is a copy of that prefab and cannot re-point what is not there. Re-derive.")
    out = pattern.sub(
        "  explosionImpactorDataContainer: {fileID: 11400000, guid: %s, type: 2}\n"
        % BLOOM_CONTAINER_GUID, out, count=1)
    assert out.count("explosionImpactorDataContainer") == 1
    return out


FILES["Assets/_Prefabs/Projectile/AOEMantaBloom.prefab"] = (build_bloom_prefab(), PREFAB_META.format(guid=BLOOM_PREFAB_GUID))

# ── Effect + container assets ────────────────────────────────────────────────
# OWNED ELSEWHERE: `debuffMagnitude` belongs to the fleet's combat drain table
# (Tools/Build/author_combat_debuff_magnitudes.py), which derives every drain from the price
# Broadside puts on its verb at TEN POINTS TO THE PETAL, per element - a bloom is a 12-point
# Debuff, so -0.12 on each of the two elements it touches (the four-element cone takes the same
# -0.12 on each of four). This generator authors the whole file, so it must
# READ that value back rather than restating it: two generators owning one field means whichever
# ran last wins, and the loser's --check reports a drift belonging to nobody's change. The
# consequence, stated: this generator's --check can no longer catch a hand-edit of that one
# field - the drain table's --check is what does, which is where the value comes from. The
# fallback is the derived value itself, so a first author (no file on disk) still lands right.
_BOMB_DEBUFF_ASSET = "Assets/_SO_Assets/Effects/Vessel Explosion Effects/MantaBombDebuffByExplosionEffect.asset"


def _live_bomb_debuff_magnitude(default="-1"):
    try:
        with open(os.path.join(ROOT, _BOMB_DEBUFF_ASSET), "r", encoding="utf-8") as fh:
            m = re.search(r"^\s*debuffMagnitude:\s*(-?[\d.]+)\s*$", fh.read(), re.M)
        return m.group(1) if m else default
    except OSError:
        return default


FILES[_BOMB_DEBUFF_ASSET] = (
    so_asset("VesselElementalDebuffByExplosionEffectSO", "MantaBombDebuffByExplosionEffect",
             "  debuffMagnitude: %s\n" % _live_bomb_debuff_magnitude() +
             "  debuffDuration: 4\n"
             # Charge does NOT scale the bloom's petals (Garrett, 2026-11-10: a petal is a whole unit, too coarse a
             # dial to tune). It scaled them x1 -> x2 from 2026-11-07; Charge now sets the fuse (fuseScale below).
             "  magnitudeAtFullCharge: 1\n"
             "  cooldown: 1\n"
             "  elements:\n"
             "  - 2\n"
             "  - 3\n"),
    ASSET_META.format(guid=BOMB_DEBUFF_GUID))

FILES["Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/MantaBloomExplosionImpactorDataContainer.asset"] = (
    so_asset("ExplosionImpactorDataContainerSO", "MantaBloomExplosionImpactorDataContainer",
             "  vesselExplosionEffects:\n"
             f"  - {{fileID: 11400000, guid: {BOMB_DEBUFF_GUID}, type: 2}}\n"
             f"  - {{fileID: 11400000, guid: {COMBAT_HIT_BY_BLAST_GUID}, type: 2}}\n"
             "  explosionPrismEffects: []\n"
             "  explosionCrystalEffects: []\n"),
    ASSET_META.format(guid=BLOOM_CONTAINER_GUID))

FILES["Assets/_SO_Assets/VesselActions/Manta/MantaStingConfig.asset"] = (
    so_asset("MantaStingConfigSO", "MantaStingConfig",
             # The 2026-11-07 overhaul (Garrett): skimming gathers ENERGY (one bomb's worth = 1), the bombs share
             # ONE fuse that every plant resets, a hull hit on a prism sets every bomb off and loses the energy,
             # the omni crystal sets them all off big, and a collected crystal leaves a decoy.
             "  energyCapacity: 1\n"
             "  energyPerSkim: 0.34\n"
             "  perPrismEnergyCooldown: 1.5\n"
             "  skimPrimesExplosive: 1\n"
             "  explosiveLift: 0.55\n"
             "  explosiveLiftFadeSeconds: 0.25\n"
             "  fuseSeconds: 20\n"
             # CHARGE -> the fuse (Garrett, 2026-11-10): a plant resets the shared fuse to fuseSeconds x this, read
             # at the planter's Charge: 20 s at rest, 30 s at Charge 10, floored at half.
             "  fuseScale:\n"
             "    Enabled: 1\n"
             "    Value: 1\n"
             "    Min: 1\n"
             "    Max: 1.5\n"
             "    element: 1\n"
             "    UseFloor: 1\n"
             "    Floor: 0.5\n"
             "  plantSpeedMargin: 0\n"
             "  aoePrefabs:\n"
             f"  - {{fileID: {AOE_EXPLOSION_COMPONENT_FILEID}, guid: {BLOOM_PREFAB_GUID}, type: 3}}\n"
             "  bloomMaterial: {fileID: 0}\n"
             "  fuseBlastScale: 70\n"
             "  kabloomBlastScale: 140\n"
             "  omniScalePerExtraBomb: 0.35\n"
             "  omniScaleMultiplierMax: 3\n"
             "  bloomGrowSeconds: 2.5\n"
             "  omniBloomGrowSeconds: 4\n"
             "  kabloomSelfBlastScale: 140\n"
             "  contagionRadiusFraction: 1\n"
             # Juice (playtest 2026-08-26). The cascade stagger is the payoff beat; the marker
             # is how a pilot reads a fuse without reading a number. Audio slots stay EMPTY -
             # an unwired EventReference is silent by design and is the audio owner's to fill.
             "  cascadeStaggerSeconds: 0.09\n"
             "  cascadeMaxSeconds: 1.2\n"
             f"  omniCrystalPrefab: {{fileID: {OMNI_CRYSTAL_ROOT_FILEID}, guid: {OMNI_CRYSTAL_PREFAB_GUID}, type: 3}}\n"
             "  decoyLifetimeSeconds: 45\n"
             "  decoyLifetimeAtFullMass: 2\n"
             "  minDecoyLifetimeMultiplier: 0.5\n"
             "  decoyMorphPrefabs:\n"
             f"  - {{fileID: {DANGER_BLOCKS_COMPONENT_FILEID}, guid: {DANGER_BLOCKS_PREFAB_GUID}, type: 3}}\n"
             "  decoyMorphScale: 60\n"
             "  decoyTriggerRadiusScale: 1\n"
             "  showFuseMarker: 1\n"
             "  markerRadius: 14\n"
             "  markerCalmColor: {r: 0.35, g: 0.8, b: 1, a: 1}\n"
             "  markerCriticalColor: {r: 1, g: 0.35, b: 0.1, a: 1}\n"
             "  markerCriticalSeconds: 6\n"
             "  markerCalmPulseHz: 0.9\n"
             "  markerCriticalPulseHz: 5\n"
             "  markerFadeSeconds: 0.22\n"),
    ASSET_META.format(guid=STING_CONFIG_GUID))

FILES["Assets/_SO_Assets/Effects/Vessel Prism Effects/MantaDetonateByPrismEffect.asset"] = (
    so_asset("MantaDetonateByPrismEffectSO", "MantaDetonateByPrismEffect", ""),
    ASSET_META.format(guid=DETONATE_EFFECT_GUID))

FILES["Assets/_SO_Assets/Effects/Vessel Crystal Effects/MantaDecoyByCrystalEffect.asset"] = (
    so_asset("MantaDecoyByCrystalEffectSO", "MantaDecoyByCrystalEffect",
             "  cooldownSeconds: 0.15\n"),
    ASSET_META.format(guid=DECOY_EFFECT_GUID))

FILES["Assets/_SO_Assets/Effects/Skimmer Prism Effects/MantaStingSkimPrismEffect.asset"] = (
    so_asset("MantaStingSkimPrismEffectSO", "MantaStingSkimPrismEffect", ""),
    ASSET_META.format(guid=SKIM_EFFECT_GUID))

FILES["Assets/_SO_Assets/Effects/Vessel Skimmer Effects/MantaStingPlantBombVesselEffect.asset"] = (
    so_asset("MantaStingPlantBombVesselEffectSO", "MantaStingPlantBombVesselEffect", ""),
    ASSET_META.format(guid=PLANT_EFFECT_GUID))

FILES["Assets/_SO_Assets/Effects/Vessel Crystal Effects/MantaKabloomByCrystalEffect.asset"] = (
    so_asset("MantaKabloomByCrystalEffectSO", "MantaKabloomByCrystalEffect",
             f"  stingConfig: {{fileID: 11400000, guid: {STING_CONFIG_GUID}, type: 2}}\n"
             "  selfBlastPrefabs:\n"
             f"  - {{fileID: {AOE_EXPLOSION_COMPONENT_FILEID}, guid: {BLOOM_PREFAB_GUID}, type: 3}}\n"
             f"  - {{fileID: {AOE_FLOWER_COMPONENT_FILEID}, guid: {AOE_FLOWER_PREFAB_GUID}, type: 3}}\n"
             "  kabloomCooldown: 0.15\n"),
    ASSET_META.format(guid=KABLOOM_EFFECT_GUID))

FILES["Assets/_SO_Assets/Effects/Vessel Crystal Effects/MantaPlantBombByLifeformJoustEffect.asset"] = (
    so_asset("MantaPlantBombByLifeformJoustEffectSO", "MantaPlantBombByLifeformJoustEffect",
             "  plantOnOwnDomain: 0\n"),
    ASSET_META.format(guid=JOUST_PLANT_EFFECT_GUID))

# The Sting skimmer container REPLACES the overcharge one at its own guid, so the
# nested SkimmerImpactor override inside Manta.prefab keeps resolving untouched.
FILES["Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/MantaStingSkimmerImpactorDataContainer.asset"] = (
    so_asset("SkimmerImpactorDataContainerSO", "MantaStingSkimmerImpactorDataContainer",
             "  vesselSkimmerEffectsSO:\n"
             f"  - {{fileID: 11400000, guid: {PLANT_EFFECT_GUID}, type: 2}}\n"
             "  skimmerPrismEffectsSO:\n"
             f"  - {{fileID: 11400000, guid: {SKIM_EFFECT_GUID}, type: 2}}\n"
             "  skimmerCrystalEffectsSO: []\n"),
    ASSET_META.format(guid=STING_CONTAINER_GUID))

# The vessel container is REWRITTEN at its own guid for the same reason: the prefab's
# vesselImpactorDataContainerSO reference keeps resolving untouched. Its crystal slot
# swaps the old kit's resource-scaled MantaVesselExplosionByCrystalEffect (which would
# double-blast beside Kabloom, scaled off a resource slot the remake repurposed as the
# bomb bay) for the Kabloom effect; the four prism effects are the fleet-shared set and
# are carried through unchanged.
VESSEL_CONTAINER_GUID = "fed903592808c9b49b28dc65e94825fc"
SPHERE_BLAST_MORPH_GUID = "c62807b520fd4bbb8569dd7b68924156"  # SphereBlastCrystalMorphByCrystal.asset
FILES["Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/MantaImpactorDataContainer.asset"] = (
    so_asset("VesselImpactorDataContainerSO", "MantaImpactorDataContainer",
             "  vesselPrismEffects:\n"
             "  - {fileID: 11400000, guid: 78b37cebc40f2b7458b5958004216de5, type: 2}\n"
             "  - {fileID: 11400000, guid: ca2d1be3880b82941a0465a1f7e45280, type: 2}\n"
             "  - {fileID: 11400000, guid: 4da62d4eefe03594ba35027dd537aff0, type: 2}\n"
             "  - {fileID: 11400000, guid: c7ccaca885824b24b716b12148d77ce1, type: 2}\n"
             # The Manta's risk: its hull on a prism sets off every bomb it planted.
             f"  - {{fileID: 11400000, guid: {DETONATE_EFFECT_GUID}, type: 2}}\n"
             "  vesselCrystalEffects:\n"
             f"  - {{fileID: 11400000, guid: {KABLOOM_EFFECT_GUID}, type: 2}}\n"
             # The Mass 5 omni decoy (the bay checks the upgrade).
             f"  - {{fileID: 11400000, guid: {DECOY_EFFECT_GUID}, type: 2}}\n"
             # The JOUST surface: hull reaches a living lifeform's heart and PLANTS on it.
             # Deliberately NOT the Squirrel's withering joust — a kill would destroy the
             # target the bomb is riding, which is the opposite of this vessel.
             "  vesselLifeformCrystalEffects:\n"
             f"  - {{fileID: 11400000, guid: {JOUST_PLANT_EFFECT_GUID}, type: 2}}\n"
             # The base Mass ability: every elemental crystal leaves an elemental decoy.
             "  vesselMassCrystalEffects:\n"
             f"  - {{fileID: 11400000, guid: {DECOY_EFFECT_GUID}, type: 2}}\n"
             "  vesselChargeCrystalEffects:\n"
             f"  - {{fileID: 11400000, guid: {DECOY_EFFECT_GUID}, type: 2}}\n"
             "  vesselSpaceCrystalEffects:\n"
             f"  - {{fileID: 11400000, guid: {DECOY_EFFECT_GUID}, type: 2}}\n"
             "  vesselTimeCrystalEffects:\n"
             f"  - {{fileID: 11400000, guid: {DECOY_EFFECT_GUID}, type: 2}}\n"
             "  vesselSkimmerEffects: []\n"
             # The omni-crystal retirement: the crystal becomes the Kabloom self-blast sphere
             # instead of the shared husk spray (R_VesselActions/SPHERE_BLAST_CRYSTAL_MORPH.md;
             # one asset shared with the Butterfly and the Rhino).
             f"  omniCrystalRetirement: {{fileID: 11400000, guid: {SPHERE_BLAST_MORPH_GUID}, type: 2}}\n"),
    ASSET_META.format(guid=VESSEL_CONTAINER_GUID))

# ── The map re-cut (the design record; multipliers pinned where a dedicated
#    authored field carries the scaling — the no-double-dip rule) ─────────────
MAP_BODY = """  vesselClass: 1
  entries:
  - Element: 1
    AbilityLabel: Sting
    AbilityDescription: Skimming a rival plants a bomb that costs one bomb of energy. Every
      bomb shares ONE fuse, reset by each plant, so they all go off together. Charge sets how
      long the fuse runs when a plant resets it (MantaStingConfig.asset fuseSeconds x fuseScale,
      20 s at rest to 30 s at level 10).
    Input: 0
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Contagion
    UpgradeDescription: Anything caught in a bomb's detonation is itself bombed, free -
      one good route cascades through a whole pack. Snapshotted per bomb at plant time.
  - Element: 2
    AbilityLabel: Decoy
    AbilityDescription: An elemental crystal the Manta collects leaves a decoy where it was,
      with its own element's effect frozen as the tell. A rival who takes it gets a cluster of
      danger prisms across their course. Mass lengthens how long a decoy stands
      (MantaStingConfig.asset decoyLifetimeAtFullMass, 2x at level 10).
    Input: 0
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Omni Decoy
    UpgradeDescription: The omni crystal leaves a decoy too, an omni crystal with one of its
      four effects frozen. One elemental and one omni decoy at a time; a new one pops the old.
  - Element: 3
    AbilityLabel: Skim
    AbilityDescription: Skimming gathers the energy that plants bombs, and turns rival prisms
      explosive - destroyed, they take their prismscape neighbours with them. Hitting a prism
      loses all the energy. Space widens the skimmer (Manta.prefab Skimmer scale, 20 to 40).
    Input: 0
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Shield Breaker
    UpgradeDescription: Skimming a shielded rival prism pops its shield and makes it explosive.
  - Element: 4
    AbilityLabel: Yawstery
    AbilityDescription: 'Hard flat turns on the turn pair. Time makes the Manta faster:
      cruise speed (VesselTransformer.ThrottleScalerMultiplier, x1.5 at level 10) and the
      soaring speed (BoostSpeedMultiplier, x1.3 at level 10). Speed shapes the trail, never
      an element: tall at a crawl, long and thin at Soar, and a Yawstery bends both rails.'
    Input: 12
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Soar
    UpgradeDescription: Boosting unlocks. The analog turn-boost and the boost button both stay
      dark until Time 5 (MantaSoarBoostAction.asset, MantaAnalogTurnBoostExecutor.soarUnlockedBy).
"""
FILES["Assets/Resources/ElementalAbilityMaps/Manta.asset"] = (
    so_asset("ElementalAbilityMapSO", "Manta", MAP_BODY), None)  # meta already exists


def unparseable_assets():
    """Every .asset this generator writes must PARSE, not only match. The byte comparison below cannot see a broken
    file it wrote itself: an unquoted 'faster:' in the Yawstery text left Manta.asset unreadable to Unity
    (E-251530c87b) while --check stayed green. Skipped, not failed, where PyYAML is not installed."""
    try:
        import yaml
    except ImportError:
        return []
    errors = []
    for rel, (content, _meta) in FILES.items():
        if not rel.endswith(".asset"):
            continue
        try:
            yaml.safe_load(content.split("--- !u!114 &11400000\n", 1)[-1])
        except yaml.YAMLError as e:
            errors.append(f"{rel}: {str(e).splitlines()[0]}")
    return errors


def main() -> int:
    broken = unparseable_assets()
    if broken:
        print("DOES NOT PARSE (nothing written):\n  " + "\n  ".join(broken))
        return 1
    drift = []
    for rel, (content, meta) in FILES.items():
        path = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        targets = [(path, content)]
        if meta is not None:
            targets.append((path + ".meta", meta))
        for p, want in targets:
            have = open(p).read() if os.path.exists(p) else None
            if have != want:
                drift.append(p)
                if not CHECK:
                    open(p, "w").write(want)
    if CHECK:
        if drift:
            print("DRIFT:\n  " + "\n  ".join(drift))
            return 1
        print("check clean: every authored file matches the generator")
        return 0
    print(f"wrote {len(drift)} file(s)" if drift else "idempotent: nothing to write")
    return 0


if __name__ == "__main__":
    sys.exit(main())
