#!/usr/bin/env python3
"""
Authors the Urchin vessel's ScriptableObject assets.

Idempotent and re-runnable: GUIDs are derived from a stable name
(md5("cosmic-shore/urchin/<name>")), so re-running produces byte-identical output and
never mints a second copy of an asset. Retuning is one edit here plus a re-run, rather
than N hand edits that drift apart.

The generator is the source; the .asset files are the build. Keep it committed.

Run from the repo root:
    python3 Tools/Build/author_urchin_assets.py            # write
    python3 Tools/Build/author_urchin_assets.py --check    # validate only, write nothing
"""

import glob
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
os.chdir(ROOT)

CHECK = "--check" in sys.argv


def guid_for(name: str) -> str:
    return hashlib.md5(f"cosmic-shore/urchin/{name}".encode()).hexdigest()


# ---------------------------------------------------------------- guid resolution
def script_guids():
    out = {}
    for m in glob.glob("Assets/_Scripts/**/*.cs.meta", recursive=True):
        for line in open(m, encoding="utf-8", errors="ignore"):
            if line.startswith("guid: "):
                out[os.path.basename(m)[:-8]] = line.strip()[6:]
                break
    return out


def asset_guids():
    out = {}
    for m in glob.glob("Assets/**/*.asset.meta", recursive=True):
        for line in open(m, encoding="utf-8", errors="ignore"):
            if line.startswith("guid: "):
                out[os.path.basename(m)[:-11]] = line.strip()[6:]
                break
    return out


S = script_guids()
A = asset_guids()


def sref(cls):
    if cls not in S:
        sys.exit(f"FATAL: no script guid for {cls}")
    return S[cls]


def aref(name):
    """A reference to an EXISTING asset. Fails loudly rather than emitting fileID 0,
    which Unity imports silently as an empty slot."""
    if name not in A:
        sys.exit(f"FATAL: no asset guid for {name} - it must exist before it can be referenced")
    return f"{{fileID: 11400000, guid: {A[name]}, type: 2}}"


def nref(name):
    """A reference to an asset THIS generator authors."""
    return f"{{fileID: 11400000, guid: {guid_for(name)}, type: 2}}"


HEADER = """%YAML 1.1
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

META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

EFFECTS = "Assets/_SO_Assets/Effects"
ACTIONS = "Assets/_SO_Assets/VesselActions/Urchin"

# ---------------------------------------------------------------- the assets
ASSETS = []


def add(path, name, script_cls, body):
    ASSETS.append((path, name, sref(script_cls), body))


# --- the three effects that close the chain-reaction loop ---------------------
# Order in the container is [Embed, Steal, ChainFire] and it is load-bearing; see
# ProjectileChainFirePrismEffectSO's class doc.
add(f"{EFFECTS}/Projectile Prism Effects/ProjectileStealPrismEffect.asset",
    "ProjectileStealPrismEffect", "ProjectileStealPrismEffectSO", "")

add(f"{EFFECTS}/Projectile Prism Effects/ProjectileEmbedPrismEffect.asset",
    "ProjectileEmbedPrismEffect", "ProjectileEmbedPrismEffectSO",
    "  dwellSeconds: 3.75\n  fadeSeconds: 0.35\n")

add(f"{EFFECTS}/Projectile Prism Effects/ProjectileChainFirePrismEffect.asset",
    "ProjectileChainFirePrismEffect", "ProjectileChainFirePrismEffectSO",
    "  requireDomainChange: 1\n")

# --- the spike's own impact container ----------------------------------------
# projectileEndEffects is NOT optional: MoveProjectileAsync deliberately does not call
# ReturnToFactory, so a spike that expires without hitting anything would leak its pool
# slot permanently - the exact defect that drained the 2023 build's 1,500-deep pool.
add(f"{EFFECTS}/Effect Containers/Projectile Containers/UrchinSpikeProjectileImpactContainer.asset",
    "UrchinSpikeProjectileImpactContainer", "ProjectileImpactorDataContainerSO",
    "  projectileShipEffects: []\n"
    "  projectilePrismEffects:\n"
    f"  - {nref('ProjectileEmbedPrismEffect')}\n"
    f"  - {nref('ProjectileStealPrismEffect')}\n"
    f"  - {nref('ProjectileChainFirePrismEffect')}\n"
    "  projectileMineEffects: []\n"
    "  projectileEndEffects:\n"
    f"  - {aref('DetonateSparrowProjectileEndEffect')}\n")

# --- the vessel's own impact container ---------------------------------------
# VesselDamagePrismEffect is safe to list here ONLY because VesselDamagePrismEffectSO
# now declines while IsAttached. Before that guard, an attaching vessel destroyed the
# prism it latched onto and the Urchin had to omit damage entirely.
add(f"{EFFECTS}/Effect Containers/VesselContainers/UrchinImpactorDataContainer.asset",
    "UrchinImpactorDataContainer", "VesselImpactorDataContainerSO",
    "  vesselPrismEffects:\n"
    f"  - {aref('VesselHapticsByPrismEffect')}\n"
    f"  - {aref('VesselAttachPrismEffect')}\n"
    f"  - {aref('VesselDamagePrismEffect')}\n"
    f"  - {aref('VesselElementalDebuffByDangerPrismEffect')}\n"
    "  vesselCrystalEffects:\n"
    f"  - {aref('VesselHapticsByCrystalEffect')}\n")

# --- the three ability assets -------------------------------------------------
# SPACE: the aimed volley. Costs ammo, chains by default.
add(f"{ACTIONS}/UrchinSpikeVolleyAction.asset", "UrchinSpikeVolleyAction", "UrchinSpikeActionSO",
    "  firingPattern: 2\n"          # ConcentricRings - the shotgun
    "  repeatWhileHeld: 1\n"
    "  barrageSpikeCount: 36\n"
    "  ringCount: 3\n"
    "  spikesPerRing: 3\n"          # PER MUZZLE - both guns fire, so 2x this
    "  coneHalfAngleDegrees: 9\n"
    "  centerSpike: 1\n"
    "  ammoIndex: 0\n"
    "  ammoCost: 0.15\n"
    "  firingRate: 3\n"
    "  projectileSpeed: 60\n"
    "  projectileTime: 2\n"
    "  projectileScale: 1\n"
    "  generationsAtRestingCharge: 1\n"
    "  generationsAtFullCharge: 4\n"
    "  generationRangeFalloff: 0.75\n"
    "  chainsOnChargeUpgrade: 0\n")

# Depth ladder, worst case per SEEDED hit (every child finding fresh enemy mass), from
# FireSpherical's own formula points = 2*(energy+3) with children at energy-1:
#     depth 1 ->      8 spikes
#     depth 2 ->     90 spikes
#     depth 3 ->  1,092 spikes
#     depth 4 -> 15,302 spikes      <- shipped ceiling, at Charge 10
# Every spike is a live trigger collider, so the ladder is a collider-budget decision, not
# a feel one. Depth 2 shipped first as the affordable call; round 6 ("dial up the recursive
# explosions") took it to 4 on BOTH triggers and raised ChainReactionBudget.VolleysPerFrame
# 4 -> 6 to pay for it - that per-frame ceiling, not the depth, is what bounds frame cost
# (<= 6 x 14 = 84 chain spikes/frame). The real-world count is far below the worst case
# because a converted prism stops accepting spikes, but the budget survives the worst case.

# CHARGE: the free omni barrage. "Fire free spikes in all directions that steal blocks,
# and even other player's trails" - so ammoCost 0, and no chain until Overcharge.
add(f"{ACTIONS}/UrchinSpikeBarrageAction.asset", "UrchinSpikeBarrageAction", "UrchinSpikeActionSO",
    "  firingPattern: 1\n"
    "  repeatWhileHeld: 0\n"
    "  ammoIndex: 0\n"
    "  ammoCost: 0\n"
    "  firingRate: 1\n"
    "  projectileSpeed: 40\n"
    "  projectileTime: 2\n"
    "  projectileScale: 1\n"
    "  generationsAtRestingCharge: 1\n"
    "  generationsAtFullCharge: 4\n"
    "  generationRangeFalloff: 0.75\n"
    "  chainsOnChargeUpgrade: 1\n")

# TIME: detach + ghost.
add(f"{ACTIONS}/UrchinSlipAction.asset", "UrchinSlipAction", "UrchinSlipActionSO",
    "  ghostSecondsAtRestingTime: 0.6\n"
    "  ghostSecondsAtFullTime: 1.6\n"
    "  detachImpulse: 0\n")


# ---------------------------------------------------------------- validate, then write
def serialized_fields(cs_path):
    """Field names Unity would serialize from a C# file, including its base classes'
    within the same file set. Attribute stripping is anchored to the line start so a
    `float[] foo` declaration is not mangled into `floatfoo`."""
    src = open(cs_path, encoding="utf-8").read()
    fields = set()
    for line in src.splitlines():
        line = re.sub(r"^\s*(?:\[[^\]\n]*\]\s*)+", "", line.strip())
        m = re.match(
            r"(?:public|protected internal|internal|private)?\s*"
            r"(?:static\s+|readonly\s+|const\s+)*"
            r"[\w<>,\[\]\.\?]+\s+(\w+)\s*(?:=|;)", line)
        if m and "(" not in line.split(m.group(1))[0]:
            fields.add(m.group(1))
    return fields


def validate():
    ok = True
    g2cs = {}
    for m in glob.glob("Assets/_Scripts/**/*.cs.meta", recursive=True):
        for line in open(m, encoding="utf-8", errors="ignore"):
            if line.startswith("guid: "):
                g2cs[line.strip()[6:]] = m[:-5]
                break

    seen = {}
    for path, name, script, body in ASSETS:
        g = guid_for(name)
        if g in seen:
            print(f"  FAIL {name}: guid collides with {seen[g]}")
            ok = False
        seen[g] = name

        cs = g2cs.get(script)
        if not cs:
            print(f"  FAIL {name}: m_Script guid {script} resolves to no .cs")
            ok = False
            continue

        fields = serialized_fields(cs)
        # include base-class fields (one level is enough for these SOs)
        base = re.search(r"class\s+\w+\s*:\s*(\w+)", open(cs, encoding="utf-8").read())
        if base:
            for other in glob.glob(f"Assets/_Scripts/**/{base.group(1)}.cs", recursive=True):
                fields |= serialized_fields(other)

        keys = [m.group(1) for m in re.finditer(r"^  (\w+):", body, re.M)]
        unknown = [k for k in keys if k not in fields]
        if unknown:
            print(f"  FAIL {name}: keys not serialized by {os.path.basename(cs)}: {unknown}")
            ok = False
        else:
            print(f"  ok   {name}  ({len(keys)} keys vs {os.path.basename(cs)})")
    return ok


print("Validating authored YAML against the C# it claims to configure...")
if not validate():
    sys.exit("VALIDATION FAILED - nothing written.")

if CHECK:
    print("\n--check: validation passed, no files written.")
    sys.exit(0)

written = 0
for path, name, script, body in ASSETS:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "w", encoding="utf-8").write(HEADER.format(script=script, name=name) + body)
    open(path + ".meta", "w", encoding="utf-8").write(META.format(guid=guid_for(name)))
    written += 1
    print(f"  wrote {path}")

# The elemental ability map is hand-authored (it is prose, not tuning) but still needs
# its .meta, and the guid must be stable across re-runs like everything else here.
MAP = "Assets/Resources/ElementalAbilityMaps/Urchin.asset"
if os.path.exists(MAP) and not os.path.exists(MAP + ".meta"):
    open(MAP + ".meta", "w", encoding="utf-8").write(META.format(guid=guid_for("ElementalAbilityMap")))
    print(f"  wrote {MAP}.meta")

print(f"\n{written} assets authored.")
