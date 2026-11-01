#!/usr/bin/env python3
"""Author the render-parity scenes: the scenes Unity and Amoebius both render, frame for frame.

    python3 Tools/Build/author_render_parity_scene.py            # writes the scene and its grid config
    python3 Tools/Build/author_render_parity_scene.py --check    # drift gate: exits 1 if disk differs

Writes Assets/_Scenes/Game_TestDesign/RenderParity.unity (+ .meta, fixed guid) and
Assets/Resources/RenderParityGridConfig.asset (+ .meta). The scene is deterministic by construction:

  - the HyperSea skybox and the game scenes' fog / ambient settings (no scene light, as the game's scenes);
  - the game's PostProcessing volume prefab (the bloom / tonemapping the camera shows) and the camera
    with the game's URP camera data (post-processing on, no AA on the camera: the URP asset's MSAA);
  - PrismManagers (the prism singletons) and the ThemeManager, as the prism-grid test scene has them;
  - a PrismGridExplosionHarness laying a 9x5x9 Jade prism grid on a warm-up frame and firing the Dolphin's
    blast (Ruby) at take frame 150: prisms, shields, the blast shell and the burst, all from the game's code;
  - twelve GyroidBranch spindles in an arc (the flora limbs' shader);
  - the Manta hull at rest (its mesh and its four hull materials, no vessel components);
  - the RenderParityTake: 30 warm-up frames, 300 take frames at 60 fps, 1280x720, a slow arc of a camera.

A second scene, the FLIGHT TAKE (Assets/_Scenes/Game_TestDesign/RenderParityFlight.unity), is the footage highlight
reels are cut from: four hulls racing through each other's prism trails (RenderParityFlight: closed-form paths of the
frame index, each laying its domain's trail through PrismTrailBuilder.LayOne), the camera chasing the lead Manta.
90 warm-up frames (the trails exist by take frame 0), 300 take frames at 60 fps, 1280x720.

Garrett (2026-10-29): "make a tool in unity that will use the unity recorder in a constructed scene ... then
create the exact same scene and video in amoebius ... your loss function will be based on sampling pixel
discrepancies." Unity records it with FrogletTools > Amoebius > Render Parity (RenderParityRecorder.cs);
Amoebius with Port/tools/render_parity.py, which also scores the two against each other.
"""
import argparse
import glob
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCENE = "Assets/_Scenes/Game_TestDesign/RenderParity.unity"
CONFIG = "Assets/Resources/RenderParityGridConfig.asset"
SCENE_GUID = "7c2e1a9d4b5f4e0c8a1d2f3e4b5c6d80"
CONFIG_GUID = "7c2e1a9d4b5f4e0c8a1d2f3e4b5c6d81"

# Assets the scene references (guids from their .meta files; checked at write time).
SKYBOX_MAT = "b9e5d3c7f0a2468135792bde4c6a8f01"            # Assets/_Graphics/Skyboxes/HyperSeaSkybox.mat
URP_CAMERA_DATA = "a79441f348de89743a2939f4d699eac1"       # UniversalAdditionalCameraData
POSTPROCESSING_PREFAB = "7fec532a729a8b648a8de2399d1b918a"  # Assets/_Prefabs/Environment/PostProcessing.prefab
PRISM_MANAGERS_PREFAB = "d2f1f48a5b5d5d14299a56a777d3971c"  # Assets/_Prefabs/Environment/PrismManagers.prefab
THEME_MANAGER_SCRIPT = "e31df9cb6c21bb24095af6f1262f40f5"
# StandaloneSceneMarker: without it AppManager's boot chain replaces any scene with Authentication
# after the splash, in Unity and in Amoebius alike (the prism-grid test scene carries one).
STANDALONE_MARKER_SCRIPT = "9c4a7f2e1b6d48c3a5e0f7d29b81c634"
THEME_DATA = "d45a23e6bd2da304988606fba6c97628"
HARNESS_SCRIPT = "073aad2d2c4c494f9ae322d3d37f8ebc"         # PrismGridExplosionHarness.cs
GRID_CONFIG_SCRIPT = "5cd132ed964743619915a3b1038018e2"     # PrismGridTestConfigSO.cs
TAKE_SCRIPT = "7c2e1a9d4b5f4e0c8a1d2f3e4b5c6d70"            # RenderParityTake.cs
FLIGHT_SCRIPT = "d6df4250342f4fc681e5d3b796e7a326"          # RenderParityFlight.cs
FLIGHT_SCENE = "Assets/_Scenes/Game_TestDesign/RenderParityFlight.unity"
FLIGHT_SCENE_GUID = "7c2e1a9d4b5f4e0c8a1d2f3e4b5c6d82"
GYROID_BRANCH_PREFAB = "932f1e7dbf4a4c04824d09eb4510cafc"   # Assets/_Prefabs/FloraAndFauna/Spindles/GyroidBranch.prefab
DOLPHIN_PRISM_PREFAB = ("4563009547826722997", "4e280247faa1d154fa25d1e92df405dc")
DOLPHIN_BLAST_PREFAB = ("3479271500403630839", "4a855af160021d241b8c1ac70cf2d792")
MANTA_MESH = ("322997229772504633", "77a38b23d33b195469fe407e2207bbdf")   # Manta_shapekey_rigged.fbx
MANTA_MATERIALS = ["539a8c65974bf0b48aae77d83884c13b", "54e78ce17120fe641904833c11bc1210", "923d366546007394ea6c8aaff6a06863", "0c127fffc48780b4d93a10811a37ae8a"]

CHECKED_GUIDS = {
    SKYBOX_MAT: "Assets/_Graphics/Skyboxes/HyperSeaSkybox.mat",
    POSTPROCESSING_PREFAB: "Assets/_Prefabs/Environment/PostProcessing.prefab",
    PRISM_MANAGERS_PREFAB: "Assets/_Prefabs/Environment/PrismManagers.prefab",
    HARNESS_SCRIPT: "Assets/_Scripts/Utility/Tools/PrismGridExplosionHarness.cs",
    GRID_CONFIG_SCRIPT: "Assets/_Scripts/ScriptableObjects/PrismGridTestConfigSO.cs",
    TAKE_SCRIPT: "Assets/_Scripts/Utility/Parity/RenderParityTake.cs",
    FLIGHT_SCRIPT: "Assets/_Scripts/Utility/Parity/RenderParityFlight.cs",
    STANDALONE_MARKER_SCRIPT: "Assets/_Scripts/Utility/StandaloneSceneMarker.cs",
    GYROID_BRANCH_PREFAB: "Assets/_Prefabs/FloraAndFauna/Spindles/GyroidBranch.prefab",
    DOLPHIN_PRISM_PREFAB[1]: "Assets/_Prefabs/Trails/Prisms With Pools/Dolphin Prism.prefab",
    DOLPHIN_BLAST_PREFAB[1]: "Assets/_Prefabs/Projectile/AOEExplosion.prefab",
    MANTA_MESH[1]: "Assets/_Models/Vessel Models/Manta_shapekey_rigged.fbx",
}

# The take
FPS, WARMUP, FRAMES, W, H = 60, 30, 300, 1280, 720
KEYFRAMES = [  # (frame, position, lookAt): a slow arc round the grid, the Manta and the gyroids in view
    (0, (-300, 110, -330), (0, 0, 0)),
    (150, (-90, 70, -360), (0, 10, 0)),
    (299, (190, 40, -320), (10, 0, 0)),
]
EVENTS = [(-28, "spawn"), (150, "explode")]
GRID_COUNTS, GRID_GAPS = (9, 5, 9), (30, 30, 30)
MANTA_POSE = ((-110, 40, -150), (0, 0.7071068, 0, 0.7071068), (1, 1, 1))   # position, rotation (facing +x), scale
GYROIDS = [((70 + 14 * i, -30 + 9 * (i % 4), -100 - 10 * (i % 3)), (0, 0.3826834 * (i % 2), 0, 0.9238795 if i % 2 else 1.0), (1, 1, 1)) for i in range(12)]


# The flight take: (domain, origin at the flight's first frame, velocity, sway amplitude, frequency Hz, phase cycles).
# Domains: 1 Jade, 2 Ruby, 3 Blue, 4 Gold. The lead (the chased Manta) is first; the rivals run ahead of it and
# sway across its line, so it flies through their trails.
FLIGHT_WARMUP, FLIGHT_START = 90, -90
FLYERS = [
    (1, (0, 0, -100), (0, 0, 70), (6, 3, 0), (0.23, 0.31, 0), (0, 0.25, 0)),
    (2, (14, 4, -60), (0, 0, 68), (10, 5, 0), (0.17, 0.27, 0), (0.4, 0.1, 0)),
    (3, (-12, -5, -40), (1, 0, 71), (8, 6, 0), (0.29, 0.19, 0), (0.7, 0.6, 0)),
    (4, (4, 10, -20), (-0.5, -0.3, 69.5), (12, 4, 0), (0.13, 0.37, 0), (0.2, 0.8, 0)),
]
FLIGHT_KEYFRAMES = [(0, (5, 3.5, -8), (5, 0, 35)), (299, (5, 3.5, -8), (5, 0, 35))]   # the warm-up pose only: the chase takes over at 0
FLIGHT_GYROIDS = [((-90 if i % 2 else 90, -20 + 12 * (i % 3), 40 + 60 * i), (0, 0.3826834 * (i % 2), 0, 0.9238795 if i % 2 else 1.0), (1, 1, 1)) for i in range(6)]


def root_transform_id(prefab_rel):
    s = open(os.path.join(ROOT, prefab_rel)).read()
    for m in re.finditer(r"--- !u!4 &(\d+)\nTransform:(.*?)(?=\n--- |\Z)", s, re.S):
        if "m_Father: {fileID: 0}" in m.group(2):
            return m.group(1)
    raise SystemExit(f"no root transform in {prefab_rel}")


def check_guids():
    bad = []
    for guid, rel in CHECKED_GUIDS.items():
        meta = os.path.join(ROOT, rel + ".meta")
        if not os.path.exists(meta) or f"guid: {guid}" not in open(meta).read():
            bad.append(f"{rel} is not guid {guid}")
    # A guid this script names must belong to ONE asset: the flight script's first guid was the Parity folder's,
    # Unity re-assigned both on import and the scene's component loaded as a missing script (2026-11-01).
    owned = list(CHECKED_GUIDS) + [SCENE_GUID, CONFIG_GUID, FLIGHT_SCENE_GUID]
    seen = {}
    for meta in glob.glob(os.path.join(ROOT, "Assets", "**", "*.meta"), recursive=True):
        m = re.search(r"^guid: (\w+)", open(meta, errors="ignore").read(), re.M)
        if m and m.group(1) in owned:
            seen.setdefault(m.group(1), []).append(os.path.relpath(meta, ROOT))
    bad += [f"guid {g} is shared by {', '.join(v)}" for g, v in seen.items() if len(v) > 1]
    if bad:
        raise SystemExit("asset guids drifted:\n  " + "\n  ".join(bad))


def f(v):
    return repr(float(v)) if isinstance(v, float) else str(v)


def vec3(v):
    return "{x: %s, y: %s, z: %s}" % (f(v[0]), f(v[1]), f(v[2]))


def quat(q):
    return "{x: %s, y: %s, z: %s, w: %s}" % (f(q[0]), f(q[1]), f(q[2]), f(q[3]))


class Ids:
    def __init__(self):
        self.n = 1000

    def next(self):
        self.n += 1
        return self.n


def header():
    return """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 10
  m_Fog: 0
  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0, g: 0, b: 0, a: 1}
  m_AmbientEquatorColor: {r: 0.114, g: 0.125, b: 0.133, a: 1}
  m_AmbientGroundColor: {r: 0.047, g: 0.043, b: 0.035, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 0
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 2100000, guid: SKYBOX_GUID, type: 2}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 13
  m_BakeOnSceneLoad: 0
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 2
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 1
    m_PVRFilteringGaussRadiusAO: 1
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
""".replace("SKYBOX_GUID", SKYBOX_MAT)


def yaml_name(name):
    """Unity quotes a name a YAML reader would misparse ('[ThemeManager]' is a flow sequence bare)."""
    return f"'{name}'" if name[:1] in "[{'\"&*!|>%@`" or name != name.strip() else name


def game_object(gid, name, components, tag="Untagged", layer=0):
    comps = "".join(f"  - component: {{fileID: {c}}}\n" for c in components)
    return f"""--- !u!1 &{gid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{comps}  m_Layer: {layer}
  m_Name: {yaml_name(name)}
  m_TagString: {tag}
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""


def transform(tid, gid, pos=(0, 0, 0), rot=(0, 0, 0, 1), scale=(1, 1, 1)):
    return f"""--- !u!4 &{tid}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  serializedVersion: 2
  m_LocalRotation: {quat(rot)}
  m_LocalPosition: {vec3(pos)}
  m_LocalScale: {vec3(scale)}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""


def camera(cid, gid):
    return f"""--- !u!20 &{cid}
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 1
  m_BackGroundColor: {{r: 0, g: 0, b: 0, a: 1}}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {{x: 2, y: 11}}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {{x: 36, y: 24}}
  m_LensShift: {{x: 0, y: 0}}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 10000
  field of view: 60
  orthographic: 0
  orthographic size: 5
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {{fileID: 0}}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 1
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
"""


def urp_camera_data(cid, gid):
    return f"""--- !u!114 &{cid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {URP_CAMERA_DATA}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  m_RenderShadows: 0
  m_RequiresDepthTextureOption: 2
  m_RequiresOpaqueTextureOption: 2
  m_CameraType: 0
  m_Cameras: []
  m_RendererIndex: -1
  m_VolumeLayerMask:
    serializedVersion: 2
    m_Bits: 1
  m_VolumeTrigger: {{fileID: 0}}
  m_VolumeFrameworkUpdateModeOption: 2
  m_RenderPostProcessing: 1
  m_Antialiasing: 0
  m_AntialiasingQuality: 2
  m_StopNaN: 0
  m_Dithering: 0
  m_ClearDepth: 1
  m_AllowXRRendering: 1
  m_AllowHDROutput: 1
  m_UseScreenCoordOverride: 0
  m_ScreenSizeOverride: {{x: 0, y: 0, z: 0, w: 0}}
  m_ScreenCoordScaleBias: {{x: 0, y: 0, z: 0, w: 0}}
  m_RequiresDepthTexture: 0
  m_RequiresColorTexture: 0
  m_Version: 2
  m_TaaSettings:
    m_Quality: 3
    m_FrameInfluence: 0.1
    m_JitterScale: 1
    m_MipBias: 0
    m_VarianceClampScale: 0.9
    m_ContrastAdaptiveSharpening: 0
"""


def audio_listener(cid, gid):
    return f"""--- !u!81 &{cid}
AudioListener:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Enabled: 1
"""


def mono(cid, gid, script_guid, body):
    return f"""--- !u!114 &{cid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
{body}"""


def mesh_filter(cid, gid, mesh):
    return f"""--- !u!33 &{cid}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Mesh: {{fileID: {mesh[0]}, guid: {mesh[1]}, type: 3}}
"""


def mesh_renderer(cid, gid, materials):
    mats = "".join(f"  - {{fileID: 2100000, guid: {m}, type: 2}}\n" for m in materials)
    return f"""--- !u!23 &{cid}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gid}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
{mats}  m_StaticBatchInfo:
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
  m_AdditionalVertexStreams: {{fileID: 0}}
"""


def prefab_instance(pid, tid, guid, root_tid, pos=(0, 0, 0), rot=(0, 0, 0, 1), scale=(1, 1, 1), name=None):
    mods = []

    def mod(target, path, value):
        mods.append(f"""    - target: {{fileID: {target}, guid: {guid}, type: 3}}
      propertyPath: {path}
      value: {f(value)}
      objectReference: {{fileID: 0}}
""")
    for axis, v in zip("xyz", pos): mod(root_tid, f"m_LocalPosition.{axis}", v)
    for axis, v in zip("xyzw", rot): mod(root_tid, f"m_LocalRotation.{axis}", v)
    for axis, v in zip("xyz", scale): mod(root_tid, f"m_LocalScale.{axis}", v)
    for axis in "xyz": mod(root_tid, f"m_LocalEulerAnglesHint.{axis}", 0)
    body = "".join(mods)
    return f"""--- !u!1001 &{pid}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
{body}    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}
--- !u!4 &{tid} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: {root_tid}, guid: {guid}, type: 3}}
  m_PrefabInstance: {{fileID: {pid}}}
  m_PrefabAsset: {{fileID: 0}}
"""


def build_scene():
    ids = Ids()
    out = [header()]
    # Main Camera
    cam_go, cam_t, cam_c, cam_al, cam_urp = (ids.next() for _ in range(5))
    out.append(game_object(cam_go, "Main Camera", [cam_t, cam_c, cam_al, cam_urp], tag="MainCamera"))
    out.append(transform(cam_t, cam_go, pos=KEYFRAMES[0][1]))
    out.append(camera(cam_c, cam_go))
    out.append(audio_listener(cam_al, cam_go))
    out.append(urp_camera_data(cam_urp, cam_go))
    # A second, disabled camera for the grid rig's own framing so it never moves the take's camera.
    rig_go, rig_t, rig_c = (ids.next() for _ in range(3))
    out.append(game_object(rig_go, "GridRigCamera (disabled)", [rig_t, rig_c]))
    out.append(transform(rig_t, rig_go, pos=(0, 0, -600)))
    out.append(camera(rig_c, rig_go).replace("  m_Enabled: 1\n  serializedVersion: 2\n  m_ClearFlags", "  m_Enabled: 0\n  serializedVersion: 2\n  m_ClearFlags"))
    # PostProcessing and PrismManagers prefabs
    instance_roots = []
    for guid, prefab in ((POSTPROCESSING_PREFAB, "Assets/_Prefabs/Environment/PostProcessing.prefab"), (PRISM_MANAGERS_PREFAB, "Assets/_Prefabs/Environment/PrismManagers.prefab")):
        pid, tid = ids.next(), ids.next()
        out.append(prefab_instance(pid, tid, guid, root_transform_id(prefab)))
        instance_roots.append(tid)
    # Standalone marker: this scene boots nothing, it is measured
    sa_go, sa_t, sa_c = (ids.next() for _ in range(3))
    out.append(game_object(sa_go, "[StandaloneScene]", [sa_t, sa_c]))
    out.append(transform(sa_t, sa_go))
    out.append(mono(sa_c, sa_go, STANDALONE_MARKER_SCRIPT, ""))
    # ThemeManager
    tm_go, tm_t, tm_c = (ids.next() for _ in range(3))
    out.append(game_object(tm_go, "[ThemeManager]", [tm_t, tm_c]))
    out.append(transform(tm_t, tm_go))
    out.append(mono(tm_c, tm_go, THEME_MANAGER_SCRIPT, f"  _dataContainer: {{fileID: 11400000, guid: {THEME_DATA}, type: 2}}\n"))
    # The prism grid rig
    gr_go, gr_t, gr_c = (ids.next() for _ in range(3))
    out.append(game_object(gr_go, "[RenderParityGrid]", [gr_t, gr_c]))
    out.append(transform(gr_t, gr_go))
    out.append(mono(gr_c, gr_go, HARNESS_SCRIPT, f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}\n  viewCamera: {{fileID: {rig_c}}}\n"))
    # The Manta hull at rest
    mh_go, mh_t, mh_f, mh_r = (ids.next() for _ in range(4))
    out.append(game_object(mh_go, "Manta Hull", [mh_t, mh_f, mh_r]))
    out.append(transform(mh_t, mh_go, *MANTA_POSE))
    out.append(mesh_filter(mh_f, mh_go, MANTA_MESH))
    out.append(mesh_renderer(mh_r, mh_go, MANTA_MATERIALS))
    # Gyroid branches
    gyroid_root = root_transform_id("Assets/_Prefabs/FloraAndFauna/Spindles/GyroidBranch.prefab")
    for pos, rot, scale in GYROIDS:
        pid, tid = ids.next(), ids.next()
        out.append(prefab_instance(pid, tid, GYROID_BRANCH_PREFAB, gyroid_root, pos, rot, scale))
        instance_roots.append(tid)
    # The take
    tk_go, tk_t, tk_c = (ids.next() for _ in range(3))
    out.append(game_object(tk_go, "[RenderParityTake]", [tk_t, tk_c]))
    out.append(transform(tk_t, tk_go))
    kf = "".join(f"  - frame: {fr}\n    position: {vec3(p)}\n    lookAt: {vec3(l)}\n" for fr, p, l in KEYFRAMES)
    ev = "".join(f"  - frame: {fr}\n    kind: {k}\n" for fr, k in EVENTS)
    body = (f"  fps: {FPS}\n  warmupFrames: {WARMUP}\n  takeFrames: {FRAMES}\n  width: {W}\n  height: {H}\n"
            f"  viewCamera: {{fileID: {cam_c}}}\n  harness: {{fileID: {gr_c}}}\n  keyframes:\n{kf}  events:\n{ev}")
    out.append(mono(tk_c, tk_go, TAKE_SCRIPT, body))
    # Scene roots, in order
    roots = [sa_t, cam_t, rig_t] + instance_roots[:2] + [tm_t, gr_t, mh_t] + instance_roots[2:] + [tk_t]
    out.append("--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n" + "".join(f"  - {{fileID: {r}}}\n" for r in roots))
    return "".join(out)


def build_flight_scene():
    ids = Ids()
    out = [header()]
    cam_go, cam_t, cam_c, cam_al, cam_urp = (ids.next() for _ in range(5))
    out.append(game_object(cam_go, "Main Camera", [cam_t, cam_c, cam_al, cam_urp], tag="MainCamera"))
    out.append(transform(cam_t, cam_go, pos=FLIGHT_KEYFRAMES[0][1]))
    out.append(camera(cam_c, cam_go))
    out.append(audio_listener(cam_al, cam_go))
    out.append(urp_camera_data(cam_urp, cam_go))
    instance_roots = []
    for guid, prefab in ((POSTPROCESSING_PREFAB, "Assets/_Prefabs/Environment/PostProcessing.prefab"), (PRISM_MANAGERS_PREFAB, "Assets/_Prefabs/Environment/PrismManagers.prefab")):
        pid, tid = ids.next(), ids.next()
        out.append(prefab_instance(pid, tid, guid, root_transform_id(prefab)))
        instance_roots.append(tid)
    sa_go, sa_t, sa_c = (ids.next() for _ in range(3))
    out.append(game_object(sa_go, "[StandaloneScene]", [sa_t, sa_c]))
    out.append(transform(sa_t, sa_go))
    out.append(mono(sa_c, sa_go, STANDALONE_MARKER_SCRIPT, ""))
    tm_go, tm_t, tm_c = (ids.next() for _ in range(3))
    out.append(game_object(tm_go, "[ThemeManager]", [tm_t, tm_c]))
    out.append(transform(tm_t, tm_go))
    out.append(mono(tm_c, tm_go, THEME_MANAGER_SCRIPT, f"  _dataContainer: {{fileID: 11400000, guid: {THEME_DATA}, type: 2}}\n"))
    # The hulls: the Manta's mesh and hull materials, posed by the flight every frame
    hulls = []
    names = {1: "Jade", 2: "Ruby", 3: "Blue", 4: "Gold"}
    for i, (dom, origin, *_rest) in enumerate(FLYERS):
        h_go, h_t, h_f, h_r = (ids.next() for _ in range(4))
        out.append(game_object(h_go, f"Hull {i} ({names[dom]})", [h_t, h_f, h_r]))
        out.append(transform(h_t, h_go, origin))
        out.append(mesh_filter(h_f, h_go, MANTA_MESH))
        out.append(mesh_renderer(h_r, h_go, MANTA_MATERIALS))
        hulls.append(h_t)
    gyroid_root = root_transform_id("Assets/_Prefabs/FloraAndFauna/Spindles/GyroidBranch.prefab")
    for pos, rot, scale in FLIGHT_GYROIDS:
        pid, tid = ids.next(), ids.next()
        out.append(prefab_instance(pid, tid, GYROID_BRANCH_PREFAB, gyroid_root, pos, rot, scale))
        instance_roots.append(tid)
    # The take (no harness, no events) and the flight
    tk_go, tk_t, tk_c, fl_c = (ids.next() for _ in range(4))
    out.append(game_object(tk_go, "[RenderParityTake]", [tk_t, tk_c, fl_c]))
    out.append(transform(tk_t, tk_go))
    kf = "".join(f"  - frame: {fr}\n    position: {vec3(p)}\n    lookAt: {vec3(l)}\n" for fr, p, l in FLIGHT_KEYFRAMES)
    body = (f"  fps: {FPS}\n  warmupFrames: {FLIGHT_WARMUP}\n  takeFrames: {FRAMES}\n  width: {W}\n  height: {H}\n"
            f"  viewCamera: {{fileID: {cam_c}}}\n  harness: {{fileID: 0}}\n  keyframes:\n{kf}  events: []\n")
    out.append(mono(tk_c, tk_go, TAKE_SCRIPT, body))
    fly = "".join(
        f"  - hull: {{fileID: {hulls[i]}}}\n    domain: {dom}\n    origin: {vec3(o)}\n    velocity: {vec3(v)}\n"
        f"    swayAmplitude: {vec3(a)}\n    swayFrequency: {vec3(fq)}\n    swayPhase: {vec3(ph)}\n"
        for i, (dom, o, v, a, fq, ph) in enumerate(FLYERS))
    body = (f"  take: {{fileID: {tk_c}}}\n  startFrame: {FLIGHT_START}\n"
            f"  prismPrefab: {{fileID: {DOLPHIN_PRISM_PREFAB[0]}, guid: {DOLPHIN_PRISM_PREFAB[1]}, type: 3}}\n"
            f"  layEveryFrames: 2\n  layBehind: 6\n  prismScale: {{x: 3, y: 1.5, z: 8}}\n  flyers:\n{fly}"
            f"  chase: 0\n  chaseOffset: {{x: 0, y: 3.5, z: -13}}\n  lookAhead: 30\n")
    out.append(mono(fl_c, tk_go, FLIGHT_SCRIPT, body))
    roots = [sa_t, cam_t] + instance_roots[:2] + [tm_t] + hulls + instance_roots[2:] + [tk_t]
    out.append("--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n" + "".join(f"  - {{fileID: {r}}}\n" for r in roots))
    return "".join(out)


def build_config():
    return f"""%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: {GRID_CONFIG_SCRIPT}, type: 3}}
  m_Name: RenderParityGridConfig
  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.ScriptableObjects.PrismGridTestConfigSO
  defaultCounts: {{x: {GRID_COUNTS[0]}, y: {GRID_COUNTS[1]}, z: {GRID_COUNTS[2]}}}
  defaultGaps: {{x: {GRID_GAPS[0]}, y: {GRID_GAPS[1]}, z: {GRID_GAPS[2]}}}
  maxTotalPrisms: 2000
  prismsPerFrame: 2000
  prismPrefab: {{fileID: {DOLPHIN_PRISM_PREFAB[0]}, guid: {DOLPHIN_PRISM_PREFAB[1]}, type: 3}}
  gridDomain: 1
  prismScale: {{x: 0, y: 0, z: 0}}
  clearSeconds: 0.35
  explosionPrefab: {{fileID: {DOLPHIN_BLAST_PREFAB[0]}, guid: {DOLPHIN_BLAST_PREFAB[1]}, type: 3}}
  maxScale: 260
  fitBlastToLattice: 0
  explosionSpeed: 0
  explosionDomain: 2
  overrideMaterial: {{fileID: 0}}
  liftSafetyThrottles: 1
  nearDistance: 50
  farDistanceMultiplier: 1.5
  defaultZoom: 0.6
"""


def meta(guid, kind):
    if kind == "scene":
        return f"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    return f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args(argv)
    check_guids()
    files = {
        SCENE: build_scene(), SCENE + ".meta": meta(SCENE_GUID, "scene"),
        CONFIG: build_config(), CONFIG + ".meta": meta(CONFIG_GUID, "asset"),
        FLIGHT_SCENE: build_flight_scene(), FLIGHT_SCENE + ".meta": meta(FLIGHT_SCENE_GUID, "scene"),
    }
    drift = []
    for rel, text in files.items():
        path = os.path.join(ROOT, rel)
        if a.check:
            if not os.path.exists(path) or open(path).read() != text:
                drift.append(rel)
        else:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            open(path, "w").write(text)
            print(f"wrote {rel} ({len(text)} bytes)")
    if a.check:
        if drift:
            print("DRIFT: " + ", ".join(drift))
            return 1
        print("render parity scene: no drift")
    return 0


if __name__ == "__main__":
    sys.exit(main())
