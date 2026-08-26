#!/usr/bin/env python3
"""Author the Manta spec-remake asset set (Sting / Kabloom / Wake Rings / the map re-cut).

One-shot generator in the author_salvo_assets.py family: deterministic guids
(md5 of a stable name), idempotent (re-runs rewrite the same bytes), --check
compares and exits nonzero on drift. It owns:

  * AOEMantaBloom.prefab           - the bomb bloom (flat copy of AOEExplosion.prefab,
                                     same internal fileIDs by convention, own guid, plus the
                                     Manta bloom effect container the base prefab lacks)
  * MantaBombDebuffByExplosionEffect.asset  - Mass+Space debuff (04/20/2026 rule: overtakers
                                     never touch Time; Charge stays the victim's own economy)
  * MantaBloomExplosionImpactorDataContainer.asset
  * MantaStingConfig.asset / MantaWakeRingConfig.asset
  * MantaStingSkimPrismEffect.asset / MantaStingPlantBombVesselEffect.asset
  * MantaKabloomByCrystalEffect.asset
  * MantaStingSkimmerImpactorDataContainer.asset  - REWRITES the renamed
                                     MantaOvercharge... container in place (guid preserved,
                                     so Manta.prefab's SkimmerImpactor override never moves)
  * Resources/ElementalAbilityMaps/Manta.asset    - the spec map re-cut

The Bloomrush mode set lives in author_bloomrush_assets.py, not here.
"""
import hashlib
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK = "--check" in sys.argv


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/MantaRemake/{name}".encode()).hexdigest()


# ── Script guids (owned by the .cs.meta files, read here as constants) ────────
SCRIPT = {
    "MantaStingConfigSO":              "74a5f56c62b268ee03e09ae3a9e8811b",
    "MantaWakeRingConfigSO":           "d76ff5670a77723418bac011be0d429d",
    "MantaStingSkimPrismEffectSO":     "2dcf021070794c32ad8fedb2fc9c5ede",
    "MantaStingPlantBombVesselEffectSO": "93535b5ea9fb9381d319896eae6b11f8",
    "MantaKabloomByCrystalEffectSO":   "4de6a983ee9f3d43306090bb86b07bf2",
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
SKIMMER_HAPTICS_GUID = "dcce08b9351b97c4eb275508001a5b8e"
STING_CONTAINER_GUID = "911a107d002544e47bc70cef1714a1a6"  # the renamed overcharge container

# ── Minted guids for the new assets ──────────────────────────────────────────
BLOOM_PREFAB_GUID = guid_for("AOEMantaBloom.prefab")
BOMB_DEBUFF_GUID = guid_for("MantaBombDebuffByExplosionEffect.asset")
BLOOM_CONTAINER_GUID = guid_for("MantaBloomExplosionImpactorDataContainer.asset")
STING_CONFIG_GUID = guid_for("MantaStingConfig.asset")
WAKE_CONFIG_GUID = guid_for("MantaWakeRingConfig.asset")
SKIM_EFFECT_GUID = guid_for("MantaStingSkimPrismEffect.asset")
PLANT_EFFECT_GUID = guid_for("MantaStingPlantBombVesselEffect.asset")
KABLOOM_EFFECT_GUID = guid_for("MantaKabloomByCrystalEffect.asset")

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
    stale = ("  explosionShipEffectsSO: []\n"
             "  explosionPrismEffectsSO: []\n")
    assert stale in out, "AOEExplosion.prefab ExplosionImpactor stale keys moved - re-derive"
    out = out.replace(
        stale,
        "  explosionImpactorDataContainer: {fileID: 11400000, guid: %s, type: 2}\n"
        % BLOOM_CONTAINER_GUID)
    assert out.count("explosionImpactorDataContainer") == 1
    return out


FILES["Assets/_Prefabs/Projectile/AOEMantaBloom.prefab"] = (build_bloom_prefab(), PREFAB_META.format(guid=BLOOM_PREFAB_GUID))

# ── Effect + container assets ────────────────────────────────────────────────
FILES["Assets/_SO_Assets/Effects/Vessel Explosion Effects/MantaBombDebuffByExplosionEffect.asset"] = (
    so_asset("VesselElementalDebuffByExplosionEffectSO", "MantaBombDebuffByExplosionEffect",
             "  debuffMagnitude: -0.5\n"
             "  debuffDuration: 4\n"
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
             "  baseCapacity: 3\n"
             "  capacityPerChargeLevel: 0.2\n"
             "  minCapacity: 1\n"
             "  maxCapacity: 5\n"
             "  chargePerSkim: 0.34\n"
             "  chargeRateAtFullCharge: 2\n"
             "  minChargeRateMultiplier: 0.25\n"
             "  perPrismChargeCooldown: 1.5\n"
             "  fuseSeconds: 25\n"
             "  plantSpeedMargin: 0\n"
             "  knockOffGraceSeconds: 1\n"
             "  ownFreshTrailGraceSeconds: 6\n"
             "  aoePrefabs:\n"
             f"  - {{fileID: {AOE_EXPLOSION_COMPONENT_FILEID}, guid: {BLOOM_PREFAB_GUID}, type: 3}}\n"
             "  bloomMaterial: {fileID: 0}\n"
             "  fuseBlastScale: 70\n"
             "  kabloomBlastScale: 140\n"
             "  kabloomSelfBlastScale: 140\n"
             "  blastScaleAtFullSpace: 1.6\n"
             "  minBlastScaleMultiplier: 0.5\n"
             "  contagionRadiusFraction: 1\n"),
    ASSET_META.format(guid=STING_CONFIG_GUID))

FILES["Assets/_SO_Assets/VesselActions/Manta/MantaWakeRingConfig.asset"] = (
    so_asset("MantaWakeRingConfigSO", "MantaWakeRingConfig",
             "  segments: 8\n"
             "  ringRadius: 18\n"
             "  prismScale: {x: 10, y: 1.5, z: 4}\n"
             "  behindOffset: 30\n"
             "  spawnPeriodSeconds: 8\n"
             "  spawnPeriodAtTime5: 4\n"
             "  surgeSpeed: 60\n"
             "  surgeSpeedAtTime5: 90\n"
             "  surgeSeconds: 1.5\n"
             "  perVesselRideCooldown: 3\n"
             "  retireBelowPrismFraction: 0.5\n"),
    ASSET_META.format(guid=WAKE_CONFIG_GUID))

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

# The Sting skimmer container REPLACES the overcharge one at its own guid, so the
# nested SkimmerImpactor override inside Manta.prefab keeps resolving untouched.
FILES["Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/MantaStingSkimmerImpactorDataContainer.asset"] = (
    so_asset("SkimmerImpactorDataContainerSO", "MantaStingSkimmerImpactorDataContainer",
             "  vesselSkimmerEffectsSO:\n"
             f"  - {{fileID: 11400000, guid: {PLANT_EFFECT_GUID}, type: 2}}\n"
             "  skimmerPrismEffectsSO:\n"
             f"  - {{fileID: 11400000, guid: {SKIM_EFFECT_GUID}, type: 2}}\n"
             f"  - {{fileID: 11400000, guid: {SKIMMER_HAPTICS_GUID}, type: 2}}\n"
             "  skimmerCrystalEffectsSO: []\n"),
    ASSET_META.format(guid=STING_CONTAINER_GUID))

# The vessel container is REWRITTEN at its own guid for the same reason: the prefab's
# vesselImpactorDataContainerSO reference keeps resolving untouched. Its crystal slot
# swaps the old kit's resource-scaled MantaVesselExplosionByCrystalEffect (which would
# double-blast beside Kabloom, scaled off a resource slot the remake repurposed as the
# bomb bay) for the Kabloom effect; the four prism effects are the fleet-shared set and
# are carried through unchanged.
VESSEL_CONTAINER_GUID = "fed903592808c9b49b28dc65e94825fc"
FILES["Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/MantaImpactorDataContainer.asset"] = (
    so_asset("VesselImpactorDataContainerSO", "MantaImpactorDataContainer",
             "  vesselPrismEffects:\n"
             "  - {fileID: 11400000, guid: 78b37cebc40f2b7458b5958004216de5, type: 2}\n"
             "  - {fileID: 11400000, guid: ca2d1be3880b82941a0465a1f7e45280, type: 2}\n"
             "  - {fileID: 11400000, guid: 4da62d4eefe03594ba35027dd537aff0, type: 2}\n"
             "  - {fileID: 11400000, guid: c7ccaca885824b24b716b12148d77ce1, type: 2}\n"
             "  vesselCrystalEffects:\n"
             f"  - {{fileID: 11400000, guid: {KABLOOM_EFFECT_GUID}, type: 2}}\n"
             "  vesselMassCrystalEffects: []\n"
             "  vesselChargeCrystalEffects: []\n"
             "  vesselSpaceCrystalEffects: []\n"
             "  vesselTimeCrystalEffects: []\n"
             "  vesselSkimmerEffects: []\n"),
    ASSET_META.format(guid=VESSEL_CONTAINER_GUID))

# ── The map re-cut (the design record; multipliers pinned where a dedicated
#    authored field carries the scaling — the no-double-dip rule) ─────────────
MAP_BODY = """  vesselClass: 1
  entries:
  - Element: 1
    AbilityLabel: Sting
    AbilityDescription: Charge raises the bomb bay's capacity and its skim-charge rate.
      Both authored on MantaStingConfig.asset (capacityPerChargeLevel, chargeRateAtFullCharge);
      the map multiplier is pinned to 1 so one element never drives a parameter twice.
    Input: 0
    MultiplierAtFullLevel: 1
    MinMultiplier: 1
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Contagion
    UpgradeDescription: Anything caught in a bomb's detonation is itself bombed, free -
      one good route cascades through a whole pack. Snapshotted per bomb at plant time.
  - Element: 2
    AbilityLabel: Yastri
    AbilityDescription: Mass grows the trail's prism volume (VesselPrismController.trailVolume
      on Manta.prefab, 1x to 2.5x). The turn itself is deliberately unscaled - Yastri's element
      shapes what the turn LEAVES, and the map multiplier is pinned to 1.
    Input: 12
    MultiplierAtFullLevel: 1
    MinMultiplier: 1
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Shielded Turn Trails
    UpgradeDescription: Prisms laid during a hard Yastri turn come out shielded - a bank
      leaves a defensible wall behind you. Regular shield only, per-spawn snapshot.
  - Element: 3
    AbilityLabel: Kabloom
    AbilityDescription: Space widens every bomb bloom (MantaStingConfig.asset,
      blastScaleAtFullSpace 1.6x at level 10; the map multiplier is pinned to 1).
    Input: 0
    MultiplierAtFullLevel: 1
    MinMultiplier: 1
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: No Friendly Fire
    UpgradeDescription: Blooms stop catching allies and allied prisms, so the Manta can
      detonate freely inside a team fight. Snapshotted per bomb at plant time.
  - Element: 4
    AbilityLabel: Soar
    AbilityDescription: Time raises the maximum soaring speed - THIS multiplier is the
      authoring home, read fleet-wide by VesselTransformer.CurrentBoostAmount while boosting.
    Input: 13
    MultiplierAtFullLevel: 1.3
    MinMultiplier: 0.7
    UnlockLevel: 5
    RelockBelowLevel: 4
    LatchPolicy: 0
    UpgradeLabel: Wake Highway
    UpgradeDescription: Soar's wake rings come twice as often, surge harder, and any
      own-domain vessel can ride them - a highway the team can follow.
"""
FILES["Assets/Resources/ElementalAbilityMaps/Manta.asset"] = (
    so_asset("ElementalAbilityMapSO", "Manta", MAP_BODY), None)  # meta already exists


def main() -> int:
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
