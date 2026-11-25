#!/usr/bin/env python3
"""
Authors the BLAST LOOK's assets and wires the Grizzly's blast to it.
See Assets/_Scripts/Controller/Projectiles/BlastLook/BLAST_LOOK.md.

Run from the repo root:  python3 Tools/Build/author_blast_look_assets.py [--check]
Run it BEFORE author_grizzly_bomb_assets.py, which dresses the bomb in two of these materials.

WHAT IT AUTHORS
  1. The metas of the look's scripts (BlastLookSO, BlastLookSystem, BlastLookMath, BlastLookMeshes),
     its doc and its folder, and of its two shaders (CosmicShore/BlastFacet, CosmicShore/BlastShell).
  2. Five materials in Assets/_Graphics/Materials/BlastLook/:
       BlastCoreFacet   - BlastFacet, heat from the property block: the white-hot core
       BlastShardFacet  - BlastFacet, heat from uv0.x: the casing shards and embers (one mesh a blast)
       BlastShell       - BlastShell: the shock front
       GrizzlyBombFacet - BlastFacet with _Spread 1 (the old fresnel shader's push, so the bomb keeps
                          its shipped size): the bomb's core and spikes
       GrizzlyBombShell - BlastShell, thinner rim: the bomb's ping and in-pull
     Their colours are white and black only (the identity and the absence of light): every colour a
     blast or a bomb shows is written at runtime from the worn colour set (COLOR_TRACE.md).
  3. Assets/_SO_Assets/Effects/BlastLook/GrizzlyBlastLook.asset - the Grizzly's look (BlastLookSO).
  4. AOEGrizzlyExplosion.prefab's AOEExplosion.blastLook -> GrizzlyBlastLook (the trigger bomb's blast,
     the Orca crew's bomb blast and every other user of that prefab).

Deterministic: guids are md5("CosmicShore/<name>") (arcade_mode_lib.guid); validated in memory first.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

ROOT = lib.ROOT
CODE_DIR = "Assets/_Scripts/Controller/Projectiles/BlastLook"
SHADER_DIR = "Assets/_Graphics/Materials/Graphs"
MAT_DIR = "Assets/_Graphics/Materials/BlastLook"
SO_DIR = "Assets/_SO_Assets/Effects/BlastLook"
LOOK_ASSET = f"{SO_DIR}/GrizzlyBlastLook.asset"
AOE_PREFAB = "Assets/_Prefabs/Projectile/AOEGrizzlyExplosion.prefab"
AOE_CS = "Assets/_Scripts/Controller/Projectiles/AOEExplosion.cs"
LOOK_CS = f"{CODE_DIR}/BlastLookSO.cs"

SCRIPTS = ("BlastLookSO", "BlastLookSystem", "BlastLookMath", "BlastLookMeshes")
G_SCRIPT = {name: lib.guid(f"script/{name}") for name in SCRIPTS}
G_SHADER = {name: lib.guid(f"shader/{name}") for name in ("BlastFacet", "BlastShell")}
MATERIALS = ("BlastCoreFacet", "BlastShardFacet", "BlastShell", "GrizzlyBombFacet", "GrizzlyBombShell")
G_MAT = {name: lib.guid(f"material/{name}") for name in MATERIALS}
G_LOOK = lib.guid("asset/GrizzlyBlastLook")

EXISTING = {
    "AOEExplosion": lib.existing_guid(AOE_CS),
    "AOEGrizzlyExplosion": lib.existing_guid(AOE_PREFAB),
}

AOE_BLOCK = "3479271500403630839"   # AOEGrizzlyExplosion's AOEExplosion component

WHITE = "{r: 1, g: 1, b: 1, a: 1}"
BLACK = "{r: 0, g: 0, b: 0, a: 1}"


def shader_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nShaderImporter:\n  externalObjects: {{}}\n"
            f"  defaultTextures: []\n  nonModifiableTextures: []\n  preprocessorOverride: 0\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def material_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 2100000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def material(name: str, shader: str, floats: dict, colours: dict, queue: int = -1) -> str:
    float_lines = "".join(f"    - {k}: {lib.num(v)}\n" for k, v in sorted(floats.items()))
    colour_lines = "".join(f"    - {k}: {v}\n" for k, v in sorted(colours.items()))
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {G_SHADER[shader]}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {queue}
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
{float_lines}    m_Colors:
{colour_lines}  m_BuildTextureStacks: []
  m_AllowLocalizedText: 1
"""


FACET_COLOURS = {"_BrightColor": WHITE, "_DarkColor": BLACK, "_HotColor": WHITE}
SHELL_COLOURS = {"_BrightColor": WHITE, "_HotColor": WHITE}

MATERIAL_TEXT = {
    "BlastCoreFacet": material("BlastCoreFacet", "BlastFacet",
                               {"_Heat": 1, "_HeatFromVertex": 0, "_HeatIntensity": 9, "_FacetPower": 1.5,
                                "_Spread": 0}, FACET_COLOURS),
    "BlastShardFacet": material("BlastShardFacet", "BlastFacet",
                                {"_Heat": 0, "_HeatFromVertex": 1, "_HeatIntensity": 6, "_FacetPower": 1.3,
                                 "_Spread": 0}, FACET_COLOURS),
    "BlastShell": material("BlastShell", "BlastShell",
                           {"_Heat": 1, "_Opacity": 1, "_Intensity": 3.2, "_RimPower": 2.5, "_FaceFill": 0.04,
                            "_Coverage": 0.2}, SHELL_COLOURS),
    "GrizzlyBombFacet": material("GrizzlyBombFacet", "BlastFacet",
                                 {"_Heat": 0, "_HeatFromVertex": 0, "_HeatIntensity": 5, "_FacetPower": 1.5,
                                  "_Spread": 1}, FACET_COLOURS),
    "GrizzlyBombShell": material("GrizzlyBombShell", "BlastShell",
                                 {"_Heat": 0, "_Opacity": 1, "_Intensity": 2.4, "_RimPower": 3.5, "_FaceFill": 0,
                                  "_Coverage": 0.15}, SHELL_COLOURS),
}

# The Grizzly's look. Every key is a BlastLookSO field (validated below).
LOOK_FIELDS = f"""  coreMaterial: {{fileID: 2100000, guid: {G_MAT['BlastCoreFacet']}, type: 2}}
  shardMaterial: {{fileID: 2100000, guid: {G_MAT['BlastShardFacet']}, type: 2}}
  shellMaterial: {{fileID: 2100000, guid: {G_MAT['BlastShell']}, type: 2}}
  bodyShare: 0.16
  hotWhiteness: 0.78
  coreRadiusShare: 0.075
  coreRadiusClamp: {{x: 1.5, y: 8}}
  coreGrowSeconds: 0.05
  coreHoldSeconds: 0.1
  coreFadeSeconds: 0.65
  coreHeatIntensity: 9
  replaceRippleSphere: 1
  shellHeatSeconds: 0.18
  shellOpacity: 0.9
  shellFadePower: 1.4
  shellIntensity: 3.2
  pointLight: 1
  lightRangeShare: 1.1
  lightIntensity: 14
  lightSeconds: 0.22
  shardCount: 34
  shardSizeShare: {{x: 0.008, y: 0.04}}
  shardSizeClamp: {{x: 0.35, y: 4.5}}
  shardReachShare: {{x: 0.12, y: 0.6}}
  shardDrag: 2.6
  shardLife: {{x: 2.2, y: 3.6}}
  shardCool: {{x: 0.35, y: 0.9}}
  shardSpin: 7
  shardSpinDrag: 0.6
  emberCount: 80
  emberSizeShare: {{x: 0.0015, y: 0.005}}
  emberSizeClamp: {{x: 0.12, y: 0.55}}
  emberReachShare: {{x: 0.3, y: 0.95}}
  emberDrag: 3.4
  emberLife: {{x: 1.1, y: 2.4}}
  emberCool: {{x: 0.2, y: 0.5}}
  emberStreakSeconds: 0.025
  shrinkShare: 0.35
  shardHeatIntensity: 6
"""

LOOK_TEXT = lib.header_for(G_SCRIPT["BlastLookSO"], "GrizzlyBlastLook") + LOOK_FIELDS


def wire_aoe(prefab: str) -> str:
    m = re.search(rf"--- !u!114 &{AOE_BLOCK}\nMonoBehaviour:\n(?:(?!--- ).*\n)*", prefab)
    assert m, "AOEGrizzlyExplosion's AOEExplosion block not found"
    blk = m.group(0)
    assert f"guid: {EXISTING['AOEExplosion']}, type: 3}}" in blk, "block &AOE is no longer an AOEExplosion"
    want = f"  blastLook: {{fileID: 11400000, guid: {G_LOOK}, type: 2}}\n"
    new = re.sub(r"  blastLook: \{[^\n]*\}\n", "", blk)
    new = new + want if new.endswith("\n") else new + "\n" + want
    return prefab.replace(blk, new, 1)


g = lib.Generator(mode_id=0, mode_name="BlastLook")
g.folder_meta(CODE_DIR)
g.folder_meta(MAT_DIR)
g.folder_meta(SO_DIR)
for name in SCRIPTS:
    g.script_meta(f"{CODE_DIR}/{name}.cs", G_SCRIPT[name])
g.text_meta(f"{CODE_DIR}/BLAST_LOOK.md")
for name, sg in G_SHADER.items():
    rel = f"{SHADER_DIR}/{name}.shader"
    assert os.path.exists(os.path.join(ROOT, rel)), f"{rel} does not exist"
    g.emit(rel + ".meta", shader_meta(sg))
for name in MATERIALS:
    g.emit(f"{MAT_DIR}/{name}.mat", MATERIAL_TEXT[name])
    g.emit(f"{MAT_DIR}/{name}.mat.meta", material_meta(G_MAT[name]))
g.emit_asset(LOOK_ASSET, G_LOOK, LOOK_TEXT)
aoe = wire_aoe(g.read(AOE_PREFAB))
g.emit(AOE_PREFAB, aoe)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
errors = []

# Every key the look asset carries is a field of BlastLookSO, and every field is carried.
_look_src = g.read(LOOK_CS)
_declared = set(re.findall(r"^\s+(?:\[[^\]]*\]\s*)*(?:Material|float|int|bool|Vector2)\s+(\w+)\s*(?:=|;)",
                           _look_src, re.M))
_authored = set(re.findall(r"^  (\w+): ", LOOK_FIELDS, re.M))
for key in sorted(_authored - _declared):
    errors.append(f"GrizzlyBlastLook.asset carries '{key}', which BlastLookSO does not declare")
for key in sorted(_declared - _authored):
    errors.append(f"BlastLookSO declares '{key}', which GrizzlyBlastLook.asset does not author")

# Every material property the materials set is declared by their shader.
for name, text in MATERIAL_TEXT.items():
    shader = re.search(r"guid: (\w+), type: 3", text).group(1)
    shader_name = next(k for k, v in G_SHADER.items() if v == shader)
    src = g.read(f"{SHADER_DIR}/{shader_name}.shader")
    props = set(re.findall(r"^\s+(?:\[\w+\]\s*)*(_\w+)\s*\(", src, re.M))
    for key in re.findall(r"^    - (_\w+): ", text, re.M):
        if key not in props:
            errors.append(f"{name}.mat sets {key}, which {shader_name}.shader does not declare")
    # Only the identity and the absence of light are baked: the colour set paints the rest at runtime.
    for colour in re.findall(r"^    - _\w+: (\{r:[^}]*\})", text, re.M):
        if colour not in (WHITE, BLACK):
            errors.append(f"{name}.mat bakes the colour {colour}: only white and black may be baked (COLOR_TRACE.md)")

# The prefab names the look, and AOEExplosion has the field it fills.
if f"  blastLook: {{fileID: 11400000, guid: {G_LOOK}, type: 2}}\n" not in aoe:
    errors.append("AOEGrizzlyExplosion.prefab does not name GrizzlyBlastLook")
if not re.search(r"BlastLookSO\s+blastLook\s*;", g.read(AOE_CS)):
    errors.append("AOEExplosion has no serialized field 'blastLook'")
if not re.search(r"Shader \"CosmicShore/BlastFacet\"", g.read(f"{SHADER_DIR}/BlastFacet.shader")):
    errors.append("BlastFacet.shader is not named CosmicShore/BlastFacet")
if not re.search(r"Shader \"CosmicShore/BlastShell\"", g.read(f"{SHADER_DIR}/BlastShell.shader")):
    errors.append("BlastShell.shader is not named CosmicShore/BlastShell")

minted = list(G_SCRIPT.values()) + list(G_SHADER.values()) + list(G_MAT.values()) + [G_LOOK] + [
    lib.guid(f"folder/{d}") for d in (CODE_DIR, MAT_DIR, SO_DIR)] + [lib.guid(f"text/{CODE_DIR}/BLAST_LOOK.md")]
g.finish(errors, referenced=EXISTING, minted=minted)
