#!/usr/bin/env python3
"""
Authors the Grizzly TRIGGER BOMB's own projectile and wires it onto the Grizzly.
See Assets/_Scripts/Controller/Vessel/R_VesselActions/GRIZZLY_TRIGGER_BOMBS.md.

Run from the repo root:  python3 Tools/Build/author_grizzly_bomb_assets.py [--check]

WHAT IT AUTHORS
  1. Assets/_Prefabs/Projectile/GrizzlyBomb.prefab - GrizzlyShell.prefab (the charged cannon's
     round, read off disk every run) plus the bomb's look:
       - a SPIKE CROWN child ("Spikes": the core's fresnel material on a MeshFilter whose mesh
         GrizzlyBombVisual generates at Awake - twelve cones, retracted in flight, snapped out
         when the bomb is armed) so the bomb reads as a sea mine rather than a ball,
       - a camera-facing additive GLOW quad ("Halo", glow1_ADD) the visual breathes with,
       - a camera-facing additive RING quad ("Ring", ring_ADD) that pings outward in flight and
         collapses onto the bomb when it is armed,
       - a plasma COMET (TrailRenderer on the Astro League ball's CosmicShore/BallTrail
         material, tinted per shot through a property block),
       - SWEPT VESSEL DETECTION on the Projectile, so a hull the bomb crosses between fixed
         steps still stops it (GrizzlyTriggerBombExecutor freezes it on Projectile.VesselStruck),
       - its OWN impact container (GrizzlyBombProjectileImpactContainer, all four lists empty):
         a bomb touches nothing - it flies through prisms (lighting them, GrizzlyBombVisual),
         never stops on one, and goes off only on the trigger. GrizzlyShell's container is the
         Sparrow full-auto's (prism damage, a detonate end effect), which is what made the bombs
         carve and blow on contact,
       - GrizzlyBombVisual on the root, wired to the core, the spikes, the halo, the ring and the comet,
       - the core casts no shadow (a glowing bomb does not).
     The cannon keeps GrizzlyShell untouched: the bombs used to share it, which is why they
     looked like cannon shells.
  2. Grizzly.prefab - the bombs' OWN pool: a PoolManager (prefab = GrizzlyBomb), a
     ProjectileFactory over it, and a second Gun on the cannon's muzzle GameObject using that
     factory; GrizzlyTriggerBombExecutor.gun is pointed at that Gun. Inserted once (idempotent),
     validated every run.
  3. GrizzlyBombVisual.cs.meta.
  4. Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/
     GrizzlyBombProjectileImpactContainer.asset (+meta).

Deterministic: guids are md5("CosmicShore/<name>") (arcade_mode_lib.guid), new prefab-local
fileIDs are fixed constants checked for collisions, and the whole result is validated in memory
before anything is written.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

ROOT = lib.ROOT
SHELL = "Assets/_Prefabs/Projectile/GrizzlyShell.prefab"
BOMB = "Assets/_Prefabs/Projectile/GrizzlyBomb.prefab"
GRIZZLY = "Assets/_Prefabs/Spacevessels/Grizzly.prefab"
VISUAL_CS = "Assets/_Scripts/Controller/Projectiles/GrizzlyBombVisual.cs"

CONTAINER = ("Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/"
             "GrizzlyBombProjectileImpactContainer.asset")

G_BOMB = lib.guid("prefab/GrizzlyBomb")
G_VISUAL = lib.guid("script/GrizzlyBombVisual")
G_CONTAINER = lib.guid("asset/GrizzlyBombProjectileImpactContainer")

EXISTING = {
    "GrizzlyShell":     lib.existing_guid(SHELL),
    "Glow1Add":         "e653836c30661fe419b8992e230ca189",   # Epic Toon FX glow1_ADD (URP particles, additive)
    "RingAdd":          "35b072e7a7dfe0f429bf5123cfc9a433",   # Epic Toon FX ring_ADD (same shader, a ring sprite)
    "BallTrail":        "68ceadd522d84c4aa57c8edef427c129",   # Resources/BallTrail.mat (CosmicShore/BallTrail comet)
    "CoreMaterial":     "4109669bc21cfa844bd19bbbb8697e6f",   # DangerProjectileMaterial (SpreadFresnelShader) - the shell's core
    "ShellContainer":   "c876b418c4188d546996eaabb11d0f26",   # SparrowFullAutoProjectileImpactContainer
    "ImpactContainerSO": lib.existing_guid(
        "Assets/_Scripts/Controller/ImpactEffects/Containers/ProjectileImpactorDataContainerSO.cs"),
    "PoolManager":      "b5daed71d21246db9fdb15a9d495e1ec",
    "ProjectileFactory": "a9b3080b23e64bba93ece7a3dece3463",
    "Gun":              "35a4e5dfc7064254cb27fc78eefb05a0",
    "TriggerBombExecutor": lib.existing_guid(
        "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GrizzlyTriggerBombExecutor.cs"),
}

# ── The shell's own objects (read off GrizzlyShell.prefab; asserted below) ──
SHELL_ROOT_GO = "7877928849292311340"
SHELL_ROOT_TF = "714786179197644117"
SHELL_CORE_MR = "7231651320737456340"
SHELL_PROJECTILE = "7632677803272750685"
SHELL_IMPACTOR = "4476692679679402743"

# ── New objects in the bomb prefab ──
B_TRAIL = "5130000000000000001"
B_VISUAL = "5130000000000000002"
B_HALO_GO = "5130000000000000010"
B_HALO_TF = "5130000000000000011"
B_HALO_MF = "5130000000000000012"
B_HALO_MR = "5130000000000000013"
B_SPIKES_GO = "5130000000000000020"
B_SPIKES_TF = "5130000000000000021"
B_SPIKES_MF = "5130000000000000022"
B_SPIKES_MR = "5130000000000000023"
B_RING_GO = "5130000000000000030"
B_RING_TF = "5130000000000000031"
B_RING_MF = "5130000000000000032"
B_RING_MR = "5130000000000000033"

# ── New objects on Grizzly.prefab ──
GZ_ROOT_GO = "6417075533431866457"
GZ_GUN_GO = "3907232763667441001"      # the charged cannon's muzzle
GZ_CANNON_GUN = "2353136872146328024"
GZ_EXECUTOR = "777000000000000200"
GZ_POOL = "777000000000000410"
GZ_FACTORY = "777000000000000411"
GZ_GUN = "777000000000000412"

TRAIL_SECONDS = 0.4


def renderer_tail(materials_block: str, cast_shadows: int) -> str:
    """The body of a Renderer after m_Enabled - Unity's defaults, shadows as asked."""
    return f"""  m_CastShadows: {cast_shadows}
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 0
  m_LightProbeUsage: 0
  m_ReflectionProbeUsage: 0
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
{materials_block}  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
"""


HEAD = """  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
"""

TRAIL = f"""--- !u!96 &{B_TRAIL}
TrailRenderer:
  serializedVersion: 3
{HEAD}  m_GameObject: {{fileID: {SHELL_ROOT_GO}}}
  m_Enabled: 1
""" + renderer_tail(f"  m_Materials:\n  - {{fileID: 2100000, guid: {EXISTING['BallTrail']}, type: 2}}\n", 0) + f"""  m_Time: {TRAIL_SECONDS}
  m_PreviewTimeScale: 1
  m_Parameters:
    serializedVersion: 3
    widthMultiplier: 1
    widthCurve:
      serializedVersion: 2
      m_Curve:
      - serializedVersion: 3
        time: 0
        value: 1
        inSlope: 0
        outSlope: 0
        tangentMode: 0
        weightedMode: 0
        inWeight: 0.33333334
        outWeight: 0.33333334
      - serializedVersion: 3
        time: 1
        value: 0
        inSlope: -1
        outSlope: -1
        tangentMode: 0
        weightedMode: 0
        inWeight: 0.33333334
        outWeight: 0.33333334
      m_PreInfinity: 2
      m_PostInfinity: 2
      m_RotationOrder: 4
    colorGradient:
      serializedVersion: 2
      key0: {{r: 1, g: 1, b: 1, a: 1}}
      key1: {{r: 1, g: 1, b: 1, a: 0}}
      key2: {{r: 0, g: 0, b: 0, a: 0}}
      key3: {{r: 0, g: 0, b: 0, a: 0}}
      key4: {{r: 0, g: 0, b: 0, a: 0}}
      key5: {{r: 0, g: 0, b: 0, a: 0}}
      key6: {{r: 0, g: 0, b: 0, a: 0}}
      key7: {{r: 0, g: 0, b: 0, a: 0}}
      ctime0: 0
      ctime1: 65535
      ctime2: 0
      ctime3: 0
      ctime4: 0
      ctime5: 0
      ctime6: 0
      ctime7: 0
      atime0: 0
      atime1: 65535
      atime2: 0
      atime3: 0
      atime4: 0
      atime5: 0
      atime6: 0
      atime7: 0
      m_Mode: 0
      m_ColorSpace: 0
      m_NumColorKeys: 2
      m_NumAlphaKeys: 2
    numCornerVertices: 2
    numCapVertices: 4
    alignment: 0
    textureMode: 0
    textureScale: {{x: 1, y: 1}}
    shadowBias: 0.5
    generateLightingData: 0
  m_MinVertexDistance: 0.3
  m_MaskInteraction: 0
  m_Autodestruct: 0
  m_Emitting: 0
  m_ApplyActiveColorSpace: 1
"""

VISUAL = f"""--- !u!114 &{B_VISUAL}
MonoBehaviour:
{HEAD}  m_GameObject: {{fileID: {SHELL_ROOT_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_VISUAL}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  core: {{fileID: {SHELL_CORE_MR}}}
  spikes: {{fileID: {B_SPIKES_TF}}}
  spikesFilter: {{fileID: {B_SPIKES_MF}}}
  spikesRenderer: {{fileID: {B_SPIKES_MR}}}
  halo: {{fileID: {B_HALO_TF}}}
  haloRenderer: {{fileID: {B_HALO_MR}}}
  ring: {{fileID: {B_RING_TF}}}
  ringRenderer: {{fileID: {B_RING_MR}}}
  trail: {{fileID: {B_TRAIL}}}
  trailWidthFactor: 1.4
  trailBraidSpread: 0.7
  trailIntensity: 1.2
  pulseHz: 4
  haloScale: 3
  pulseAmplitude: 0.22
  spikesFlightScale: 0.8
  spikesArmedScale: 1.05
  spikesDeploySeconds: 0.22
  spinDegreesPerSecond: 240
  armedSpinDegreesPerSecond: 45
  ringPingHz: 1.6
  ringMinScale: 1.4
  ringMaxScale: 7
  armedRingHz: 3.5
  armedRingMaxScale: 6
  ringIntensity: 0.9
  armedHaloScale: 5
  armedPulseHz: 14
  armFlash: 1.2
  rimIntensityPeak: 6
  rimIntensityTrough: 1.5
  sparkColor: {{r: 1, g: 0.92, b: 0.7, a: 1}}
  sparkMix: 0.45
  coreDarkness: 0.12
  litRadiusFactor: 1.5
  litWakeSeconds: 0.3
"""

def child(go: str, tf: str, mf: str, mr: str, name: str, scale: float, mesh: str, material_guid: str) -> str:
    """A rendering child of the bomb's root: GameObject + Transform + MeshFilter + MeshRenderer."""
    return f"""--- !u!1 &{go}
GameObject:
{HEAD}  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tf}}}
  - component: {{fileID: {mf}}}
  - component: {{fileID: {mr}}}
  m_Layer: 12
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tf}
Transform:
{HEAD}  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: {scale:g}, y: {scale:g}, z: {scale:g}}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {SHELL_ROOT_TF}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{mf}
MeshFilter:
{HEAD}  m_GameObject: {{fileID: {go}}}
  m_Mesh: {mesh}
--- !u!23 &{mr}
MeshRenderer:
{HEAD}  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
""" + renderer_tail(f"  m_Materials:\n  - {{fileID: 2100000, guid: {material_guid}, type: 2}}\n", 0) \
        + "  m_AdditionalVertexStreams: {fileID: 0}\n"


QUAD = "{fileID: 10210, guid: 0000000000000000e000000000000000, type: 0}"
# The spike mesh is generated by GrizzlyBombVisual at Awake (shared by every bomb), so the
# filter is authored empty - the prefab shows a bare core in the editor, the game a sea mine.
SPIKES = child(B_SPIKES_GO, B_SPIKES_TF, B_SPIKES_MF, B_SPIKES_MR, "Spikes", 0.8, "{fileID: 0}",
               EXISTING["CoreMaterial"])
HALO = child(B_HALO_GO, B_HALO_TF, B_HALO_MF, B_HALO_MR, "Halo", 3, QUAD, EXISTING["Glow1Add"])
RING = child(B_RING_GO, B_RING_TF, B_RING_MF, B_RING_MR, "Ring", 1.4, QUAD, EXISTING["RingAdd"])


def build_bomb(shell: str) -> str:
    for probe, why in ((f"--- !u!1 &{SHELL_ROOT_GO}\n", "root GameObject"),
                       (f"--- !u!4 &{SHELL_ROOT_TF}\n", "root Transform"),
                       (f"--- !u!23 &{SHELL_CORE_MR}\n", "core MeshRenderer"),
                       (f"--- !u!114 &{SHELL_PROJECTILE}\n", "Projectile component"),
                       ("  m_Name: GrizzlyShell\n", "name"),
                       ("  m_Children: []\n  m_Father: {fileID: 0}\n", "childless root transform")):
        assert shell.count(probe) == 1, f"GrizzlyShell.prefab no longer has its {why}"
    bomb = shell.replace("  m_Name: GrizzlyShell\n", "  m_Name: GrizzlyBomb\n")
    # Register the new components on the root and the halo as its child.
    last_component = re.search(r"(  m_Component:\n(?:  - component: \{fileID: \d+\}\n)+)", bomb).group(1)
    bomb = bomb.replace(last_component, last_component +
                        f"  - component: {{fileID: {B_TRAIL}}}\n  - component: {{fileID: {B_VISUAL}}}\n", 1)
    bomb = bomb.replace("  m_Children: []\n  m_Father: {fileID: 0}\n",
                        f"  m_Children:\n  - {{fileID: {B_SPIKES_TF}}}\n  - {{fileID: {B_HALO_TF}}}\n"
                        f"  - {{fileID: {B_RING_TF}}}\n  m_Father: {{fileID: 0}}\n", 1)
    # The core: a glowing bomb casts no shadow.
    core = re.search(rf"--- !u!23 &{SHELL_CORE_MR}\nMeshRenderer:\n(?:(?!--- ).*\n)*", bomb).group(0)
    assert core.count("  m_CastShadows: 1\n") == 1, "core renderer shadow flag not found"
    assert f"guid: {EXISTING['CoreMaterial']}, type: 2}}" in core, \
        "GrizzlyShell's core no longer wears DangerProjectileMaterial - the spikes would not match it"
    bomb = bomb.replace(core, core.replace("  m_CastShadows: 1\n", "  m_CastShadows: 0\n"), 1)
    # Its own impact container: a bomb touches nothing (see the module doc).
    impactor = re.search(rf"--- !u!114 &{SHELL_IMPACTOR}\nMonoBehaviour:\n(?:(?!--- ).*\n)*", bomb).group(0)
    shell_ref = re.search(r"  projectileImpactorDataContainer: \{fileID: 11400000, guid: (\w+),\s+type: 2\}\n",
                          impactor)
    assert shell_ref and shell_ref.group(1) == EXISTING["ShellContainer"], \
        "GrizzlyShell's impactor no longer names the Sparrow full-auto container"
    bomb = bomb.replace(impactor, impactor.replace(
        shell_ref.group(0),
        f"  projectileImpactorDataContainer: {{fileID: 11400000, guid: {G_CONTAINER}, type: 2}}\n"), 1)
    # Swept VESSEL detection: a hull the bomb crosses between fixed steps still stops it.
    projectile = block(bomb, SHELL_PROJECTILE)
    assert "sweptVesselDetection" not in projectile and projectile.count("  sweptPrismDetection: 1\n") == 1, \
        "GrizzlyShell's Projectile detection flags changed - re-derive the bomb's"
    bomb = bomb.replace(projectile, projectile.replace(
        "  sweptPrismDetection: 1\n", "  sweptPrismDetection: 1\n  sweptVesselDetection: 1\n"), 1)
    if not bomb.endswith("\n"):
        bomb += "\n"
    return bomb + TRAIL + VISUAL + SPIKES + HALO + RING


def block(text: str, file_id: str) -> str:
    m = re.search(rf"--- !u!\d+ &{file_id}\n(?:(?!--- ).*\n)*", text)
    assert m, f"block &{file_id} not found"
    return m.group(0)


def wire_grizzly(prefab: str) -> str:
    if f"&{GZ_POOL}\n" in prefab:
        return prefab   # already wired (validated below)
    for fid in (GZ_ROOT_GO, GZ_GUN_GO, GZ_CANNON_GUN, GZ_EXECUTOR):
        assert f"&{fid}\n" in prefab, f"Grizzly.prefab no longer has &{fid}"
    pool = f"""--- !u!114 &{GZ_POOL}
MonoBehaviour:
{HEAD}  m_GameObject: {{fileID: {GZ_ROOT_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {EXISTING['PoolManager']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  prefab: {{fileID: {SHELL_PROJECTILE}, guid: {G_BOMB}, type: 3}}
  defaultCapacity: 4
  maxSize: 12
  enableBufferMaintenance: 1
  bufferSizeTarget: 4
  maxInstantiateRate: 5
  baseInstantiateRate: 2
  maxAddsPerFrame: 2
"""
    factory = f"""--- !u!114 &{GZ_FACTORY}
MonoBehaviour:
{HEAD}  m_GameObject: {{fileID: {GZ_ROOT_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {EXISTING['ProjectileFactory']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  poolEntries:
  - type: 0
    poolManager: {{fileID: {GZ_POOL}}}
"""
    gun = f"""--- !u!114 &{GZ_GUN}
MonoBehaviour:
{HEAD}  m_GameObject: {{fileID: {GZ_GUN_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {EXISTING['Gun']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  firePeriod: 0.2
  sideLength: 2
  projectileFactory: {{fileID: {GZ_FACTORY}}}
"""
    # Component lists.
    root_go = block(prefab, GZ_ROOT_GO)
    prefab = prefab.replace(root_go, re.sub(
        r"(  m_Component:\n(?:  - component: \{fileID: \d+\}\n)+)",
        lambda m: m.group(1) + f"  - component: {{fileID: {GZ_POOL}}}\n  - component: {{fileID: {GZ_FACTORY}}}\n",
        root_go, count=1), 1)
    gun_go = block(prefab, GZ_GUN_GO)
    prefab = prefab.replace(gun_go, re.sub(
        r"(  m_Component:\n(?:  - component: \{fileID: \d+\}\n)+)",
        lambda m: m.group(1) + f"  - component: {{fileID: {GZ_GUN}}}\n", gun_go, count=1), 1)
    # The executor fires the bombs' own gun, not the cannon's.
    executor = block(prefab, GZ_EXECUTOR)
    assert executor.count(f"  gun: {{fileID: {GZ_CANNON_GUN}}}\n") == 1, "executor gun wire not found"
    prefab = prefab.replace(executor, executor.replace(
        f"  gun: {{fileID: {GZ_CANNON_GUN}}}\n", f"  gun: {{fileID: {GZ_GUN}}}\n"), 1)
    if not prefab.endswith("\n"):
        prefab += "\n"
    return prefab + pool + factory + gun


CONTAINER_ASSET = (lib.header_for(EXISTING["ImpactContainerSO"], "GrizzlyBombProjectileImpactContainer")
                   + "  projectileShipEffects: []\n"
                   + "  projectilePrismEffects: []\n"
                   + "  projectileMineEffects: []\n"
                   + "  projectileEndEffects: []\n")

g = lib.Generator(mode_id=0, mode_name="GrizzlyBomb")
g.script_meta(VISUAL_CS, G_VISUAL)
g.emit_asset(CONTAINER, G_CONTAINER, CONTAINER_ASSET)
bomb = build_bomb(g.read(SHELL))
g.emit_prefab(BOMB, G_BOMB, bomb)
grizzly = wire_grizzly(g.read(GRIZZLY))
g.emit(GRIZZLY, grizzly)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
errors = []


def ids_ok(name: str, text: str):
    ids = re.findall(r"^--- !u!\d+ &(\d+)", text, re.M)
    dupes = sorted({i for i in ids if ids.count(i) > 1})
    if dupes:
        errors.append(f"{name}: duplicate fileIDs {dupes}")
    known = set(ids)
    dangling = sorted({r for r in re.findall(r"\{fileID: (\d+)\}", text) if r != "0" and r not in known})
    if dangling:
        errors.append(f"{name}: dangling local references {dangling}")


ids_ok("GrizzlyBomb.prefab", bomb)
ids_ok("Grizzly.prefab", grizzly)

# The bomb stops on a hull only if the sweep sees the hull.
if "  sweptVesselDetection: 1\n" not in block(bomb, SHELL_PROJECTILE):
    errors.append("GrizzlyBomb's Projectile does not sweep for vessels - a fast bomb would fly through a hull")
_projectile_src = g.read("Assets/_Scripts/Controller/Projectiles/Projectile.cs")
for field in ("sweptPrismDetection", "sweptVesselDetection"):
    if not re.search(rf"\b{field}\s*=", _projectile_src):
        errors.append(f"Projectile has no serialized field '{field}'")

# The visual's serialized field names must be the script's.
_visual_src = g.read(VISUAL_CS)
for field in re.findall(r"^  (\w+): ", VISUAL.split("m_EditorClassIdentifier:", 1)[1].split("\n", 1)[1], re.M):
    if not re.search(rf"\b{field}\b\s*(=|;)", _visual_src):
        errors.append(f"GrizzlyBombVisual has no serialized field '{field}' (the prefab would carry a dead key)")

# A bomb touches nothing: its impactor names the bombs' own container, and that container is empty.
if f"guid: {G_CONTAINER}, type: 2}}" not in block(bomb, SHELL_IMPACTOR):
    errors.append("GrizzlyBomb's ProjectileImpactor does not use GrizzlyBombProjectileImpactContainer")
for key in ("projectileShipEffects", "projectilePrismEffects", "projectileMineEffects", "projectileEndEffects"):
    if f"  {key}: []\n" not in CONTAINER_ASSET:
        errors.append(f"GrizzlyBombProjectileImpactContainer.{key} is not empty - the bomb would act on contact")
_container_src = g.read("Assets/_Scripts/Controller/ImpactEffects/Containers/ProjectileImpactorDataContainerSO.cs")
for key in ("projectileShipEffects", "projectilePrismEffects", "projectileMineEffects", "projectileEndEffects"):
    if not re.search(rf"\b{key}\s*;", _container_src):
        errors.append(f"ProjectileImpactorDataContainerSO has no serialized field '{key}'")

# The executor fires the bombs' own gun, the gun uses the bombs' own factory, the pool holds the bomb.
if f"  gun: {{fileID: {GZ_GUN}}}\n" not in block(grizzly, GZ_EXECUTOR):
    errors.append("GrizzlyTriggerBombExecutor does not fire the bombs' own Gun")
if f"projectileFactory: {{fileID: {GZ_FACTORY}}}" not in block(grizzly, GZ_GUN):
    errors.append("the bombs' Gun does not use the bombs' ProjectileFactory")
if f"prefab: {{fileID: {SHELL_PROJECTILE}, guid: {G_BOMB}, type: 3}}" not in block(grizzly, GZ_POOL):
    errors.append("the bombs' pool does not hold GrizzlyBomb.prefab's Projectile")
# The cannon is untouched: its gun still uses the original factory.
if "projectileFactory: {fileID: 9100000000000001016}" not in block(grizzly, GZ_CANNON_GUN):
    errors.append("the charged cannon's Gun no longer uses its own factory")

g.finish(errors, referenced=EXISTING, minted=[G_BOMB, G_VISUAL, G_CONTAINER])
