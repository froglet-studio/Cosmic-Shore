#!/usr/bin/env python3
"""
Authors the Grizzly TRIGGER BOMB's own projectile and wires it onto the Grizzly.
See Assets/_Scripts/Controller/Vessel/R_VesselActions/GRIZZLY_TRIGGER_BOMBS.md.

Run from the repo root:  python3 Tools/Build/author_grizzly_bomb_assets.py [--check]

WHAT IT AUTHORS
  1. Assets/_Prefabs/Projectile/GrizzlyBomb.prefab - GrizzlyShell.prefab (the charged cannon's
     round, read off disk every run) plus the bomb's look:
       - a camera-facing additive GLOW quad ("Halo", glow1_ADD) the visual breathes with the fuse,
       - a short streak (TrailRenderer, Unity's Default-Line material so the per-shot domain
         gradient needs no material instance),
       - GrizzlyBombVisual on the root, wired to the core, the halo and the streak,
       - the core casts no shadow (a glowing bomb does not).
     The cannon keeps GrizzlyShell untouched: the bombs used to share it, which is why they
     looked like cannon shells.
  2. Grizzly.prefab - the bombs' OWN pool: a PoolManager (prefab = GrizzlyBomb), a
     ProjectileFactory over it, and a second Gun on the cannon's muzzle GameObject using that
     factory; GrizzlyTriggerBombExecutor.gun is pointed at that Gun. Inserted once (idempotent),
     validated every run.
  3. GrizzlyBombVisual.cs.meta.

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

G_BOMB = lib.guid("prefab/GrizzlyBomb")
G_VISUAL = lib.guid("script/GrizzlyBombVisual")

EXISTING = {
    "GrizzlyShell":     lib.existing_guid(SHELL),
    "Glow1Add":         "e653836c30661fe419b8992e230ca189",   # Epic Toon FX glow1_ADD (URP particles, additive)
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

# ── New objects in the bomb prefab ──
B_TRAIL = "5130000000000000001"
B_VISUAL = "5130000000000000002"
B_HALO_GO = "5130000000000000010"
B_HALO_TF = "5130000000000000011"
B_HALO_MF = "5130000000000000012"
B_HALO_MR = "5130000000000000013"

# ── New objects on Grizzly.prefab ──
GZ_ROOT_GO = "6417075533431866457"
GZ_GUN_GO = "3907232763667441001"      # the charged cannon's muzzle
GZ_CANNON_GUN = "2353136872146328024"
GZ_EXECUTOR = "777000000000000200"
GZ_POOL = "777000000000000410"
GZ_FACTORY = "777000000000000411"
GZ_GUN = "777000000000000412"

TRAIL_SECONDS = 0.3


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
""" + renderer_tail("  m_Materials:\n  - {fileID: 10306, guid: 0000000000000000f000000000000000, type: 0}\n", 0) + f"""  m_Time: {TRAIL_SECONDS}
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
  halo: {{fileID: {B_HALO_TF}}}
  haloRenderer: {{fileID: {B_HALO_MR}}}
  trail: {{fileID: {B_TRAIL}}}
  trailWidthFactor: 0.7
  pulseHzAtLaunch: 2.5
  pulseHzAtFuseEnd: 11
  haloScale: 3
  pulseAmplitude: 0.22
  armedHaloScale: 5
  armedPulseHz: 14
  rimIntensityPeak: 6
  rimIntensityTrough: 1.5
  sparkColor: {{r: 1, g: 0.92, b: 0.7, a: 1}}
  sparkMix: 0.45
  coreDarkness: 0.12
"""

HALO = f"""--- !u!1 &{B_HALO_GO}
GameObject:
{HEAD}  serializedVersion: 6
  m_Component:
  - component: {{fileID: {B_HALO_TF}}}
  - component: {{fileID: {B_HALO_MF}}}
  - component: {{fileID: {B_HALO_MR}}}
  m_Layer: 12
  m_Name: Halo
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{B_HALO_TF}
Transform:
{HEAD}  m_GameObject: {{fileID: {B_HALO_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 3, y: 3, z: 3}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {SHELL_ROOT_TF}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{B_HALO_MF}
MeshFilter:
{HEAD}  m_GameObject: {{fileID: {B_HALO_GO}}}
  m_Mesh: {{fileID: 10210, guid: 0000000000000000e000000000000000, type: 0}}
--- !u!23 &{B_HALO_MR}
MeshRenderer:
{HEAD}  m_GameObject: {{fileID: {B_HALO_GO}}}
  m_Enabled: 1
""" + renderer_tail(f"  m_Materials:\n  - {{fileID: 2100000, guid: {EXISTING['Glow1Add']}, type: 2}}\n", 0) \
    + "  m_AdditionalVertexStreams: {fileID: 0}\n"


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
                        f"  m_Children:\n  - {{fileID: {B_HALO_TF}}}\n  m_Father: {{fileID: 0}}\n", 1)
    # The core: a glowing bomb casts no shadow.
    core = re.search(rf"--- !u!23 &{SHELL_CORE_MR}\nMeshRenderer:\n(?:(?!--- ).*\n)*", bomb).group(0)
    assert core.count("  m_CastShadows: 1\n") == 1, "core renderer shadow flag not found"
    bomb = bomb.replace(core, core.replace("  m_CastShadows: 1\n", "  m_CastShadows: 0\n"), 1)
    if not bomb.endswith("\n"):
        bomb += "\n"
    return bomb + TRAIL + VISUAL + HALO


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


g = lib.Generator(mode_id=0, mode_name="GrizzlyBomb")
g.script_meta(VISUAL_CS, G_VISUAL)
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

# The visual's serialized field names must be the script's.
_visual_src = g.read(VISUAL_CS)
for field in re.findall(r"^  (\w+): ", VISUAL.split("m_EditorClassIdentifier:", 1)[1].split("\n", 1)[1], re.M):
    if not re.search(rf"\b{field}\b\s*(=|;)", _visual_src):
        errors.append(f"GrizzlyBombVisual has no serialized field '{field}' (the prefab would carry a dead key)")

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

g.finish(errors, referenced=EXISTING, minted=[G_BOMB, G_VISUAL])
