#!/usr/bin/env python3
"""
Authors the OMNI crystal's Shepard-tone geometry.

    python3 Tools/Build/author_omni_crystal_triangles.py [--check]

The omni crystal is an EXPLODED polyhedron: 122 separate plates - 20 triangular
prisms, 90 boxes, 12 pentagonal prisms - one component per face of the solid.
Each family of plates is the shape that stands for one element, and each element
expresses ITS OWN effect on ITS OWN shapes. Mass owns the SHEPARD TONE, so the
tone belongs on the 20 triangles and on nothing else.

SINCE 2026-10-12 (Omni Shepard Lab round 8, the "Depth ratchet"): the tone shells are retired from the
crystal. Slot 1 is ONE opaque prism renderer (`OmniCrystalTriangles.prefab`, now SEATED: the mesh at its
prefab scale is the crystal's own triangles) on `OmniDepthPrismShader` (`OmniDepthPrisms[Inactive].mat`),
driven by `CrystalDepthPrisms` (`CrystalDepthPrismProfile.asset`, authored here too): each prism rides a
column over its own hole by its depth from the camera, faces parting at the top, thickness growing inward,
and the crystal turning; `CrystalDepthPrisms` cuts the body's 20 triangle plates into the holes at runtime.
The mesh's TEXCOORD2 carries (plate centre xyz, plate index). `OriginalMaterialSet.CrystalMaterial1` names
`OmniDepthPrisms`; slots 2-4 are EMPTY since 2026-10-13, when the eight retired tone/rim materials went (the lab
pins their values in its RETIRED constant). Since round 10 the prisms carry their thickness in the mesh and
`CrystalDepthPrismProfile` gives every crystal its own pose and the -0.87 reseat. The history below describes the
looks this one replaced.

Before this, `Crystal.prefab` ran the Shepard shader over FOUR copies of the
WHOLE omni model, so the tone dragged the entire crystal - squares and pentagons
included - through every pulse, and the crystal had no body of its own: the one
static shell (`ActiveMassCrystalMaterial 3`) resolves to alpha 0.02..0.07, a
ghost. What ships now is a BODY, THREE tone shells and a stationary RIM:

    slot 0      the body - the whole omni model, static, on OmniCrystalFresnelShader
                (the elemental crystals' SpreadFresnel look + the Scarab morph path)
    slot 1..3   the Shepard chain, on the TRIANGLES ONLY, falling from outside the
                crystal onto its surface (MassCrystalExport3ExpandedTri, baked), on
                OmniShepardFresnelShader - the body's colour formula and colour pair

and OriginalMaterialSet's CrystalMaterial..CrystalMaterial3 point at those four
per-slot materials, so a TEAM crystal is the same crystal in its domain colours.

THE LOOK is "Breathe" (Omni Shepard Lab round 4, Docs/Studios/OMNI_SHEPARD_LAB.md): each
shell flies in from s = 1 to the surface and back OUT again over one round trip (TONE_TRIP),
the three shells a third of a trip apart (_PhaseOffset 0, 1/3, 2/3); every triangle is half
size at the far end and full size on the surface (PLATE_SCALE_START), plates are 1.25x thick
(PLATE_THICKNESS), and the shading is face-forward. The far end sits at alpha 0.05, so a
breathing shell needs no rim to hide its turn: the stationary rim (once slot 4) was retired
with this look. (Breathe and the rim and their eight materials were retired on 2026-10-13.)

Charge owns the PENTAGONS. Since the Omni Motion Lab round 5 (2026-10-13) its light is CrystalChargeArcs on
OmniCrystalChargeArcs, a plain child of Crystal.prefab (NOT a crystalModels slot - an overlay has no husk to burst and no
team material to clone): chained FLUX ARCS - a pentagon charges, an arc leaps to one 55-125 degrees away, both fade, and
the next leaps on from the sink; only those two pentagons light; the arc is five strands, each one plasma ribbon from a
source corner to a sink corner, fanned onto the corners at each end and running as one coaxial braided bundle between. The Charge crystal (CrystalCharge.prefab, also authored here) gets the
same component as ChargeCrystalArcs in SEAM WALK mode - one bolt walking the seams between its twelve blooms forever -
and its shell's edge crawl is held at zero while it runs. Both draw on CrystalChargeArcsShader (OmniChargeArcs.mat /
ChargeCrystalArcs.mat: the crystal's bright colour, ChargeCrystalMaterial's arc core colour) with one timing profile,
CrystalChargeArcsProfile.asset. CrystalAccentTint paints the omni's from the body, so a team crystal's lit pentagons
wear its domain colour. The edge-crawl overlay that came before (OmniCrystalChargeEdges.prefab: ChargeCrystal.hlsl's
discharge on the pentagons' crease edges) is retired, with its OmniChargeEdges material and shader (the Elemental
Converter moved to the flux arcs too, 2026-10-13).

A team crystal: `ThemeManagerDataContainerSO.GetTeamCrystalMaterial(domain, material)` finds the base-set slot
that holds a model's OWN default material and hands back that slot's domain clone (since 2026-10-13; it was by
model index, which would have put the omni body on a Mass crystal).

WHAT THIS SCRIPT OWNS - the things that must not be typed by hand (the body and
tone materials too; see the materials block below):

 1. THE SHELL SCALE. `MassCrystalExport3ExpandedTri_10-23-25.fbx` is EXACTLY the
    omni model's 20 triangular prisms scaled about the origin - measured 2.241676
    on every one of its 120 vertices (max residual 8.3e-07 against a 1.6172 mesh
    radius, i.e. 5e-07 relative). The shells carry OUTER_REACH / 2.241676 as their
    local scale, so the tone's s = 1 is OUTER_REACH x the crystal's triangles and
    s = 1/OUTER_REACH lands exactly ON them. Re-export either model at a different
    scale and --check fails here rather than in play.

 2. THE TRIANGLE MESH. The shells cannot reference the FBX's own mesh: Unity
    mints an FBX sub-asset's fileID inside the editor by a generator that is
    not reproducible offline (an earlier pass "borrowed" one by renaming the
    node to match another model's recorded id - Unity did not honour it and
    the shells rendered nothing). So this script BAKES the triangle FBX into a
    native Unity Mesh asset, `OmniCrystalTriangles.asset`, whose reference is
    `{fileID: 4300000, guid: <its own .meta>}` - deterministic by construction.
    The FBX stays the source: positions, normals and UVs are read from it and
    converted the way Unity imports this export (`bakeAxisConversion: 0`, so the
    mesh stays in node space; right->left handedness negates x and reverses
    winding; `UnitScaleFactor 100` x `useFileScale` = 1:1). Re-export the FBX and
    re-run this script; --check fails until you do.

If the triangle shells ever render nothing or sit rotated against the body,
the mesh asset's axis conversion is the one thing to check.

 3. THE BODY PREFAB. Time owns the RHOMBI, and its signature is the Time crystal's
    flip wave: the 30 rhombi turn 180 degrees about a diagonal, ring by ring, from
    one vertex to the opposite one (Docs/TIME_CRYSTAL.md). A plate can only turn on a
    bone, so the body is `OmniCrystalBody.prefab` - a FLAT COPY of the shared
    `TrucatedOctahedron.prefab` (which Mine, MazeCrystal and OldCrystalTime keep) with
    the same internal ids, its MeshRenderer re-classed in place as a
    SkinnedMeshRenderer and its MeshFilter dropped. It draws the same FBX mesh; at
    runtime `CrystalFlipWave` (on the Crystal root, `skinRhombi` on) has
    `RhombusSkinBaker` give the rhombi bones and flips them on the Time crystal's own
    profile. Because the ids match, slot 0's overrides and stripped stubs carry over
    with only the source guid changed. The root's CrystalFlipWave lives in the part of
    Crystal.prefab this script preserves, and --check asserts it is there.
"""

import argparse
import hashlib
import json
import math
import os
import re
import struct
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary as F

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

OMNI_FBX = "Assets/_Models/OmniCrystalExport1_8-21-25.fbx"
TRI_FBX = "Assets/_Models/MassCrystalExport3ExpandedTri_10-23-25.fbx"
SHELL_PREFAB = "Assets/_Prefabs/Environment/OmniCrystalTriangles.prefab"
CRYSTAL_PREFAB = "Assets/_Prefabs/Environment/Crystal.prefab"

TRI_MESH_ASSET = "Assets/_Models/OmniCrystalTriangles.asset"
TRI_MESH_GUID = "148e244052c4a0499c66e90863d9d5ee"   # md5("CosmicShore/OmniCrystal/OmniCrystalTriangles.asset")
TRI_MESH_FILE_ID = 4300000                          # a native .asset's main object
SHELL_PREFAB_GUID = "59cbf929dd08bc1e2d4c6d5f1e48b76a"
TRUNC_OCTA_GUID = "a089e5ab0159cc54aa784d6ebf15d2e4"   # TrucatedOctahedron.prefab (whole omni model)
FADE_IN_GUID = "318b4eb62a2693f4e98368eb975997cd"

# ── materials ──
# The BODY wears the omni's own shader (OmniCrystalFresnelShader: SpreadFresnelShader's look,
# transcribed, plus the morph + dissolve only the omni needs). Its two
# materials are clones of the elemental crystals' LimeCrystalFresnelMaterial /
# BlueCrystalFresnelMateriall - same colours and spread - moved onto that shader.
OMNI_SHADER_GUID = "554d96e9b67505208a3fbf0acb165350"           # OmniCrystalFresnelShader.shader
MAT_DIR = "Assets/_Graphics/Materials/CrystalMaterials"
BODY_DONORS = [("OmniCrystalBody", "LimeCrystalFresnelMaterial"),
               ("OmniCrystalBodyInactive", "BlueCrystalFresnelMateriall")]
MAT_BODY_EXPLODING = "383f21e8586fd7243a19e1d0f26110d0"  # CrystalMaterial: the spent husk animates
                                                         # CrystalGraph's _velocity, which the
                                                         # Fresnel shaders do not carry

# The TONE shells are on OmniShepardFresnelShader: the BODY's colour formula on the body's own
# colour pair (so a triangle in flight is the body's triangle, see-through, and can never drift to a
# different lime), with ShepardGraph's motion and alpha transcribed. ShepardGraph-style band:
# s sweeps _Start -> _Stop (falling when _Start > _Stop), the mesh is scaled by s about the origin,
# Alpha = (1.05 - s) * _Opacity. The shell prefab is scaled so s = 1 is OUTER_REACH x the crystal's
# own triangles and s = 1/OUTER_REACH is ON them, and the three bands tile that range - so the
# triangles fall in from outside the crystal and land on its surface, brightening as they arrive
# (alpha 0.05 at the outer edge, 1.05 - 1/OUTER_REACH at the surface).
OUTER_REACH = 2.0

# The "Breathe" look (Omni Shepard Lab round 4, shipped 2026-10-10..12) and the stationary rim are RETIRED with
# their eight OmniShepardTriangles* materials (2026-10-13, refactor row F2): no crystal or material set names them,
# and the lab pins their values in Docs/Studios/OmniShepardLab.html's RETIRED constant.

def mat_guid(name):
    return hashlib.md5(f"CosmicShore/OmniCrystal/{name}.mat".encode()).hexdigest()


# THE LOOK since the Omni Shepard Lab round 7 (decision 2026-10-12): "Depth ratchet". The tone shells
# are retired from the crystal; ONE opaque prism renderer fills the body's 20 triangle holes, each prism
# riding its column by its depth from the camera (CrystalDepthPrisms + OmniDepthPrismShader). The lab's
# recipe: {depth 2 (ratchet), depthOpen -0.88, depthSeal 1, faceSplit 0.5, thickAnchor 1 (inward),
# thickness 8 (omni; the Mass crystal 6.25), turn 15 - dropped 2026-10-12, the crystal holds still} on top of start 1 / stop 0.5 / sizeStart 0.5.
DEPTH_SHADER = "Assets/_Graphics/Materials/Shaders/OmniDepthPrismShader.shader"
DEPTH_SHADER_GUID = hashlib.md5(b"CosmicShore/OmniCrystal/OmniDepthPrismShader.shader").hexdigest()
DEPTH_NAME = "OmniDepthPrisms"
MAT_DEPTH = mat_guid(DEPTH_NAME)
MAT_DEPTH_INACTIVE = mat_guid(DEPTH_NAME + "Inactive")
# Omni Motion Lab round 3 (shipped with round 5, 2026-10-13): taller columns and bigger prisms at the top, so the
# Mass that the horizon seal had made subtle stands out again (it was OUTER_REACH 2 / 0.5).
DEPTH_REACH = 3                          # the top of a column, x the prism's seated radius
DEPTH_PLATE_SCALE_START = 0.6            # a prism's size at the top of its column, x its seated size
DEPTH_THICKNESS = 8                      # the omni's pick (thickness grows INWARD, so a sealed prism is flush)
DEPTH_THICKNESS_INWARD = 1
# The thickness is BAKED INTO the prism mesh (thicken()) and the materials ship _Thickness 1, so the shader's
# vertex stage is the identity on a sealed prism: CrystalHullFusion moves this mesh's vertices on the CPU and
# draws them on the crystal's own material, and a thickness left in the shader stretched every moved vertex
# from a plate centre it had left behind (x5.25 on the Mass, measured 2026-10-13). The lab still reads the
# thickness: omni_shepard_lab_assets.py measures it off the shipped mesh.
MATERIAL_THICKNESS = 1
DEPTH_FACE_SPLIT = 0.5                   # the faces part by half a plate radius at the top of the column
DEPTH_PRISMS_GUID = hashlib.md5(b"CosmicShore/Crystals/CrystalDepthPrisms.cs").hexdigest()
DEPTH_PROFILE_SO_GUID = hashlib.md5(b"CosmicShore/Crystals/CrystalDepthPrismProfileSO.cs").hexdigest()
DEPTH_RATCHET_GUID = hashlib.md5(b"CosmicShore/Utility/DepthRatchet.cs").hexdigest()
DEPTH_PROFILE = "Assets/_SO_Assets/Environment/CrystalDepthPrismProfile.asset"
DEPTH_PROFILE_GUID = hashlib.md5(b"CosmicShore/Crystals/CrystalDepthPrismProfile.asset").hexdigest()
# CrystalDepthPrismProfileSO's fields, as the lab picked them (the SO's own defaults match).
# The free turn (15 deg/s about a 25-degree axis) was dropped 2026-10-12: the crystal holds still in game.
# Round 10 (2026-10-13): reseat -0.87 (the far-range bound: -0.8 was seen once the camera moves) and a random pose
# per crystal (orientationJitter 1, seeded from the spawn position by CrystalDepthPrisms).
# Omni Motion Lab rounds 1 and 3 (shipped with round 5, 2026-10-13): seal at the camera's horizon (every prism the camera
# sees is flush, so none clips into the squares), rise while hidden (a prism climbs only while its column top is behind
# the body, so every hidden prism stands out and none is seen rising), a later descent (open -0.25) and the reseat
# pushed back to -0.97 now that it is a fallback. The Mass crystal shares this profile.
DEPTH_PROFILE_VALUES = [("openDepth", -0.25), ("sealDepth", 1), ("profilePower", 1), ("reseatDepth", -0.97),
                        ("reseatFadeSeconds", 0.6), ("sealAtHorizon", 1), ("horizonMargin", 0.05),
                        ("riseWhileHidden", 1), ("riseRate", 1.5), ("occluderRadius", 0.936),
                        ("orientationJitter", 1)]


MAT_BODY = mat_guid("OmniCrystalBody")
MAT_BODY_INACTIVE = mat_guid("OmniCrystalBodyInactive")

CHARGE_DONOR = "ChargeCrystalMaterial"                   # the charge crystal's own material: the arcs' core colour
ACCENT_TINT_GUID = "599565cac0fff6c38bbe40a3fcc53207"    # CrystalAccentTint.cs
TRUNC_OCTA_PREFAB = "Assets/_Prefabs/Environment/TrucatedOctahedron.prefab"
BODY_PREFAB = "Assets/_Prefabs/Environment/OmniCrystalBody.prefab"
BODY_PREFAB_GUID = hashlib.md5(b"CosmicShore/OmniCrystal/OmniCrystalBody.prefab").hexdigest()
FLIP_WAVE_GUID = "c1b251f1ac8f4a15b1bc5681c1d6bcc5"          # CrystalFlipWave.cs
FLIP_PROFILE_GUID = "7ae6c13f991929b01f383003cb5d9277"       # TimeCrystalFlipWaveProfile.asset
# ── the Charge light (Omni Motion Lab round 5) ──
ARCS_GUID = hashlib.md5(b"CosmicShore/Crystals/CrystalChargeArcs.cs").hexdigest()
ARCS_PROFILE_SO_GUID = hashlib.md5(b"CosmicShore/Crystals/CrystalChargeArcsProfileSO.cs").hexdigest()
ARCS_CORE_GUID = hashlib.md5(b"CosmicShore/Utility/ChargeArcs.cs").hexdigest()
SQUARE_TURN_FIT_GUID = hashlib.md5(b"CosmicShore/Utility/SquareTurnFit.cs").hexdigest()   # the square turns' fit, shared
ARCS_SHADER = "Assets/_Graphics/Materials/Shaders/CrystalChargeArcsShader.shader"
ARCS_SHADER_GUID = hashlib.md5(b"CosmicShore/Crystals/CrystalChargeArcsShader.shader").hexdigest()
ARCS_PROFILE = "Assets/_SO_Assets/Environment/CrystalChargeArcsProfile.asset"
ARCS_PROFILE_GUID = hashlib.md5(b"CosmicShore/Crystals/CrystalChargeArcsProfile.asset").hexdigest()
OMNI_ARCS_MAT = "OmniChargeArcs"
CHARGE_ARCS_MAT = "ChargeCrystalArcs"
ARCS_SEAM_WALK, ARCS_FLUX = 1, 2                          # CrystalChargeArcs.Effect
# CrystalChargeArcsProfileSO's fields, as the lab picked them (its JT and FX_T, round 4; the SO's defaults match).
ARCS_PROFILE_VALUES = [("rate", 1), ("boltIntensity", 1), ("glowIntensity", 1),
                       ("seamStrikeSeconds", 0.07), ("seamDwellMinSeconds", 0.1), ("seamDwellMaxSeconds", 0.28),
                       ("seamFadeSeconds", 0.85), ("seamPetalGlow", 0.35), ("fluxGapSeconds", 0.9), ("fluxChargeSeconds", 0.45),
                       ("fluxStrikeSeconds", 0.06), ("fluxHoldSeconds", 0.45), ("fluxFadeSeconds", 0.45), ("fluxFork", 0.3),
                       ("fluxMinDegrees", 55), ("fluxMaxDegrees", 125), ("fluxBundle", 0.016), ("fluxTurns", 1), ("strikeProtractionFrom", 0.15), ("strikeProtractionFull", 0.45), ("coreWidth", 0.012), ("writheSpeed", 3), ("flickerSpeed", 4)]
# The omni's light: a plain child of Crystal.prefab (GameObject, Transform, MeshFilter, MeshRenderer, CrystalChargeArcs,
# CrystalAccentTint). The Charge crystal's: a plain child of CrystalCharge.prefab (no tint: its body has no _BrightColor).
OMNI_ARCS_IDS = (3086241560104200061, 3086241560104200062, 3086241560104200063, 3086241560104200064,
                 3086241560104200065, 3086241560104200066)
CHARGE_CRYSTAL_PREFAB = "Assets/_Prefabs/Environment/CrystalCharge.prefab"
CHARGE_CRYSTAL_ROOT_TR = 6588436222817790493
CHARGE_CRYSTAL_SHELL_TR, CHARGE_CRYSTAL_SHELL_MF, CHARGE_CRYSTAL_SHELL_MR = (2842750437815966002, 2842750437815966003,
                                                                            2842750437815966004)
CHARGE_ARCS_IDS = (2842750437815966011, 2842750437815966012, 2842750437815966013, 2842750437815966014,
                   2842750437815966015, None)
CRYSTAL_ROOT_TR = 5535990081244205889


def _donor(name):
    return open(os.path.join(ROOT, MAT_DIR, name + ".mat")).read()


def _sub1(text, pattern, repl, label):
    out, n = re.subn(pattern, repl, text, count=1, flags=re.M)
    if n != 1:
        raise SystemExit(f"material clone: {label} not found exactly once")
    return out


def depth_material(name, body_name, body_text, thickness, top_height, split_unit):
    """A depth-prism material: the body material (same colours, same _Spread) moved onto
    OmniDepthPrismShader, plus the prism's shape dials. OPAQUE (queue Geometry), like the body."""
    t = _sub1(body_text, rf"^  m_Name: {re.escape(body_name)}$", f"  m_Name: {name}", name + " m_Name")
    t = _sub1(t, r"^  m_Shader: \{fileID: 4800000, guid: \w+, type: 3\}",
              f"  m_Shader: {{fileID: 4800000, guid: {DEPTH_SHADER_GUID}, type: 3}}", name + " m_Shader")
    floats = (f"    - _DepthDriven: 0\n    - _FaceSplit: {DEPTH_FACE_SPLIT}\n"
              f"    - _PlateScaleStart: {DEPTH_PLATE_SCALE_START}\n    - _Reach: {DEPTH_REACH}\n"
              f"    - _SplitUnit: {split_unit}\n    - _Thickness: {thickness}\n"
              f"    - _ThicknessInward: {DEPTH_THICKNESS_INWARD}\n    - _TopHeight: {top_height}\n")
    t = _sub1(t, r"^    m_Floats:\n", lambda m: m.group(0) + floats, name + " m_Floats")
    # The Charge strike glow (OmniDepthPrismShader's _StrikeColor): ChargeCrystalMaterial's arc core colour, so a bolt
    # through a Mass column lights the prism in the bolt's own colour. Kept in Unity's sorted order.
    strike = f"    - _StrikeColor: {_saved(_donor(CHARGE_DONOR), '_ArcCoreColor', 'c')}\n"

    def with_strike(m):
        rows = [r + "\n" for r in m.group(1).splitlines() if not r.startswith("    - _StrikeColor:")] + [strike]
        return "    m_Colors:\n" + "".join(sorted(rows, key=lambda r: r.split(":")[0]))
    return _sub1(t, r"^    m_Colors:\n((?:    - .*\n)+)", with_strike, name + " m_Colors")


def material_texts():
    out = {}
    for name, donor in BODY_DONORS:
        t = _sub1(_donor(donor), rf"^  m_Name: {re.escape(donor)}$", f"  m_Name: {name}", name + " m_Name")
        t = _sub1(t, r"^  m_Shader: \{fileID: [-0-9]+, guid: \w+,\s*\n?\s*type: 3\}",
                  f"  m_Shader: {{fileID: 4800000, guid: {OMNI_SHADER_GUID}, type: 3}}", name + " m_Shader")
        out[name] = t
    body, body_inactive = out["OmniCrystalBody"], out["OmniCrystalBodyInactive"]
    out[OMNI_ARCS_MAT] = arcs_material_text(OMNI_ARCS_MAT, _saved(body, "_BrightColor", "c"))
    out[CHARGE_ARCS_MAT] = arcs_material_text(CHARGE_ARCS_MAT, _saved(_donor(CHARGE_DONOR), "_BrightCrystalColor", "c"))
    top_h, split = prism_shape(bake_tri_mesh()[0])
    out[DEPTH_NAME] = depth_material(DEPTH_NAME, "OmniCrystalBody", body, MATERIAL_THICKNESS, top_h, split)
    out[DEPTH_NAME + "Inactive"] = depth_material(DEPTH_NAME + "Inactive", "OmniCrystalBodyInactive", body_inactive,
                                                  MATERIAL_THICKNESS, top_h, split)
    return {f"{MAT_DIR}/{n}.mat": t for n, t in out.items()}


def _saved(text, name, kind):
    """One saved property's serialized value text out of a material, exactly as written."""
    if kind == "f":
        m = re.findall(rf"^    - {re.escape(name)}: (\S+)$", text, re.M)
    else:
        m = re.findall(rf"^    - {re.escape(name)}: (\{{[^}}]*\}})$", text, re.M)
    if len(m) != 1:
        raise SystemExit(f"material clone: {name} found {len(m)}x in its source material")
    return m[0]


def arcs_material_text(name, bright):
    """A CrystalChargeArcsShader material: the crystal's bright colour (the lit faces' tint) and ChargeCrystalMaterial's
    arc core colour (the bolts), at full intensity. Cloned from ChargeCrystalMaterial for its header, with its saved
    properties replaced by exactly the new shader's set, so no stale property rides along."""
    t = _sub1(_donor(CHARGE_DONOR), rf"^  m_Name: {CHARGE_DONOR}$", f"  m_Name: {name}", name + " m_Name")
    t = _sub1(t, r"^  m_Shader: \{fileID: 4800000, guid: \w+, type: 3\}",
              f"  m_Shader: {{fileID: 4800000, guid: {ARCS_SHADER_GUID}, type: 3}}", name + " m_Shader")
    core = _saved(_donor(CHARGE_DONOR), "_ArcCoreColor", "c")
    t = _sub1(t, r"^    m_Floats:\n(?:    - .*\n)+", lambda _: "    m_Floats:\n    - _BoltIntensity: 1\n    - _GlowIntensity: 1\n",
              name + " m_Floats")
    t = _sub1(t, r"^    m_Colors:\n(?:    - .*\n)+",
              lambda _: f"    m_Colors:\n    - _ArcCoreColor: {core}\n    - _BrightColor: {bright}\n", name + " m_Colors")
    return t


def material_meta(name):
    return (f"fileFormatVersion: 2\nguid: {mat_guid(name)}\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n")


# Stable ids inside OmniCrystalTriangles.prefab.
SHELL_GO, SHELL_TR, SHELL_MF, SHELL_MR, SHELL_FADE, SHELL_DEPTH = (
    3086241560104200011, 3086241560104200012, 3086241560104200013,
    3086241560104200014, 3086241560104200015, 3086241560104200016)
# Ids inside TrucatedOctahedron.prefab, for the body instance.
OCTA_GO, OCTA_TR, OCTA_MR = 2448508127246415652, 1114034561007818392, 2073129193669181770
# OmniCrystalBody.prefab's own addition: the Space squares driver (round 9).
BODY_SQUARES = 2346576036425758006

# THE SPACE SQUARES since round 9 (decision 2026-10-12): the artist's three shape keys on the body's 60 square
# plates (CrystalsRaw_V2.blend, OmniCrystal.001, Action.008 - baked to JSON by bake_omni_space_squares.py),
# written as anchors (the moving control points, in Unity's space) + per-key offsets + the frame weights.
# CrystalSpaceSquares adds them to the body's runtime twin as blend shapes and plays them.
SQUARES_JSON = "Docs/Studios/data/omni_space_squares.json"
SQUARES_ASSET = "Assets/_SO_Assets/Environment/OmniSpaceSquares.asset"
SQUARES_ASSET_GUID = hashlib.md5(b"CosmicShore/Crystals/OmniSpaceSquares.asset").hexdigest()
SQUARES_SO_GUID = hashlib.md5(b"CosmicShore/Crystals/CrystalSpaceSquaresSO.cs").hexdigest()
SQUARES_DRIVER_GUID = hashlib.md5(b"CosmicShore/Crystals/CrystalSpaceSquares.cs").hexdigest()
SQUARES_PLAYBACK_SPEED = 1               # the art file's own 25 fps: a 4.8 s loop
SQUARES_MATCH = 1e-4                     # CrystalSpaceSquaresBaker.MatchTolerance, x the body's radius

# THE CADENCE since the Omni Motion Lab round 5 (decision 2026-10-13): the Space squares and the Time rhombi take turns
# (pentagon turn, wave, triangle turn, wave, a gap between each) so they never move at once; each square turn is ONE
# rigid turn that hops just clear of its neighbours (CrystalSpaceSquares fits it to the art file's own keys at runtime);
# and the body's squares, rhombi and pentagons are drawn 10% thicker. OmniCrystalCadence on the Crystal root is the
# clock; CrystalFlipWave (same root) and CrystalSpaceSquares (the body) ask it what to play.
CADENCE_ASSET = "Assets/_SO_Assets/Environment/OmniCrystalCadence.asset"
CADENCE_ASSET_GUID = hashlib.md5(b"CosmicShore/Crystals/OmniCrystalCadence.asset").hexdigest()
CADENCE_SO_GUID = hashlib.md5(b"CosmicShore/Crystals/OmniCrystalCadenceSO.cs").hexdigest()
CADENCE_GUID = hashlib.md5(b"CosmicShore/Crystals/OmniCrystalCadence.cs").hexdigest()
OMNI_CADENCE_GUID = hashlib.md5(b"CosmicShore/Utility/OmniCadence.cs").hexdigest()
FLIP_PROFILE = "Assets/_SO_Assets/Environment/TimeCrystalFlipWaveProfile.asset"
CADENCE_COMPONENT = 3086241560104200051                     # OmniCrystalCadence on the Crystal root
FLIP_WAVE_COMPONENT = 5535990081244205930                   # CrystalFlipWave on the Crystal root (preserved)
SQUARES_PENTAGON_SECONDS = round(1.56 / 0.9, 3)             # the art file's 1.56 s, slowed 10% (round 4)
CADENCE_VALUES = [("pentagonTurnSeconds", SQUARES_PENTAGON_SECONDS), ("triangleTurnSeconds", 2.4), ("gapSeconds", 0.15),
                  ("waveActiveSeconds", None), ("waveSpeed", 1), ("pentagonLift", 0.14), ("triangleLift", 1.5),
                  ("triangleSide", 1.5), ("framesPerTurn", 24), ("plateThickness", 1.1)]

# Crystal.prefab: the four child slots, in crystalModels order. The stripped
# Transform/GameObject ids are PRESERVED across this rewrite so the root's
# m_Children list and the crystalModels model references stay valid.
#
# The BODY is slot 0, and that is load-bearing: every consumer that wants "the crystal's shape"
# reads crystalModels[0] (ElementCrystalModelBuilder, SpawnMatrixToy's element visual), and the
# Scarab's crystal->ball forge (ScarabCrystalMorph.AdoptShells) builds its morph mesh from shell 0
# and folds every shell that draws the same mesh - the whole-model body, here.
SLOTS = [
    # (PrefabInstance id, stripped Transform id, stripped GameObject id, name)
    (302729943389656567,  812437664975045487,  2722806873126380243, "OmniCrystalBody"),
    (693643822389642704,  492451356381860680,  2907786364588143348, DEPTH_NAME),
    # Slots 2 and 3, the second and third breathing tone shells (3428511433788108504 and 5888802945109814545),
    # were retired with the Depth ratchet (2026-10-12): one prism renderer draws every column. Slot 4, the
    # stationary rim (3086241560104200021/22/23), went with the Breathe look before it.
]

# The Charge light is a plain child of Crystal.prefab, deliberately NOT a crystalModels slot (see the module docstring;
# OMNI_ARCS_IDS). The edge-crawl overlay instance it replaced (3086241560104200041/42/43, from
# OmniCrystalChargeEdges.prefab) was retired 2026-10-13.

# Per slot: (default, inactive). Slot 0 is the body; slot 1 the depth prisms.
SLOT_MATERIALS = [(MAT_BODY, MAT_BODY_INACTIVE), (MAT_DEPTH, MAT_DEPTH_INACTIVE)]


# ── measurement ──────────────────────────────────────────────────────────────

def _load(path):
    nodes, _, _ = F.read(os.path.join(ROOT, path))
    objs = [n for n in nodes if n.name == "Objects"][0]
    geo = [c for c in objs.children if c.name == "Geometry"][0]
    flat = geo.first("Vertices").props[0][1]
    pvi = geo.first("PolygonVertexIndex").props[0][1]
    verts = [(flat[i], flat[i + 1], flat[i + 2]) for i in range(0, len(flat), 3)]
    faces, cur = [], []
    for idx in pvi:
        if idx < 0:
            cur.append(-idx - 1)
            faces.append(tuple(cur))
            cur = []
        else:
            cur.append(idx)
    return verts, faces


def _components(verts, faces):
    parent = list(range(len(verts)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for f in faces:
        for i in range(len(f)):
            ra, rb = find(f[i]), find(f[(i + 1) % len(f)])
            if ra != rb:
                parent[ra] = rb
    out = defaultdict(list)
    for fi, f in enumerate(faces):
        out[find(f[0])].append(fi)
    return out


def _triangular_prisms(verts, faces):
    """The plates that are triangular prisms: 2 triangles + 3 quads, 6 vertices."""
    out = []
    for _, fl in _components(verts, faces).items():
        sizes = Counter(len(faces[fi]) for fi in fl)
        if sizes.get(3) == 2 and sizes.get(4) == 3:
            out.append([verts[v] for v in sorted({v for fi in fl for v in faces[fi]})])
    return out


def measure():
    """The shell scale, re-derived from the two shipped models. Fails loud on drift."""
    ov, of = _load(OMNI_FBX)
    tv, tf = _load(TRI_FBX)

    omni_tris = _triangular_prisms(ov, of)
    tri_tris = _triangular_prisms(tv, tf)
    if len(omni_tris) != 20:
        raise SystemExit(f"{OMNI_FBX}: expected 20 triangular prisms, found {len(omni_tris)}")
    if len(tri_tris) != 20 or len(tv) != 120:
        raise SystemExit(f"{TRI_FBX}: expected 20 triangular prisms / 120 verts, "
                         f"found {len(tri_tris)} / {len(tv)}")

    omni_pts = [p for prism in omni_tris for p in prism]
    tri_pts = [p for prism in tri_tris for p in prism]

    def norm(v):
        return math.dist((0.0, 0.0, 0.0), v)

    # Least-squares uniform scale over the direction-matched vertex pairs.
    num = den = 0.0
    worst_cos = 0.0
    pairs = []
    for t in tri_pts:
        rt = norm(t)
        best = max(omni_pts, key=lambda o: sum(a * b for a, b in zip(t, o)) / (rt * norm(o)))
        cos = sum(a * b for a, b in zip(t, best)) / (rt * norm(best))
        worst_cos = max(worst_cos, 1.0 - cos)
        num += rt * norm(best)
        den += norm(best) ** 2
        pairs.append((t, best))
    scale = num / den
    residual = max(math.dist(t, tuple(c * scale for c in o)) for t, o in pairs)

    if worst_cos > 1e-9:
        raise SystemExit("the triangle model's prisms no longer point the same way as the omni "
                         f"model's (worst 1-cos {worst_cos:.3e}) - it is not the same 20 plates")
    if residual > 1e-4:
        raise SystemExit(f"the triangle model is not a UNIFORM scale of the omni triangles "
                         f"(max residual {residual:.3e}) - re-derive the shell scale by hand")
    return scale, residual


# ── triangle mesh asset ──────────────────────────────────────────────────────

def _layer(geo, name, data, index):
    layer = geo.first(name)
    assert layer.first("MappingInformationType").props[0][1] == b"ByPolygonVertex", name
    assert layer.first("ReferenceInformationType").props[0][1] == b"IndexToDirect", name
    return layer.first(data).props[0][1], layer.first(index).props[0][1]


def bake_tri_mesh():
    """The triangle FBX as Unity imports it, flattened to one vertex per polygon corner."""
    verts, tangents, indices = bake_prism_mesh(TRI_FBX, 20)
    assert len(verts) == 360 and len(indices) == 3 * 160, (len(verts), len(indices))
    return verts, tangents, indices


def bake_prism_mesh(fbx, plates_expected):
    """An FBX of disjoint triangular prisms as Unity imports it, flattened to one vertex per polygon corner,
    each vertex carrying its plate's centre and index: (position, normal, uv, (cx, cy, cz, plate))."""
    nodes, _, _ = F.read(os.path.join(ROOT, fbx))
    objs = [n for n in nodes if n.name == "Objects"][0]
    geo = [c for c in objs.children if c.name == "Geometry"][0]
    flat = geo.first("Vertices").props[0][1]
    pvi = geo.first("PolygonVertexIndex").props[0][1]
    normals, n_index = _layer(geo, "LayerElementNormal", "Normals", "NormalsIndex")
    if geo.first("LayerElementUV") is not None:
        uvs, uv_index = _layer(geo, "LayerElementUV", "UV", "UVIndex")
    else:
        uvs, uv_index = [0.0, 0.0], None

    # Each plate's centre: the plates are disjoint prisms, so a plate is a connected component of
    # control points, and its centre the mean of its DISTINCT corners (in Unity's space, x negated).
    parent = list(range(len(flat) // 3))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    cur = []
    for raw in pvi:
        cp = -raw - 1 if raw < 0 else raw
        if cur:
            parent[find(cp)] = find(cur[0])
        cur.append(cp)
        if raw < 0:
            cur = []
    members = defaultdict(list)
    for cp in range(len(parent)):
        members[find(cp)].append(cp)
    centre = {}
    # Plate index: plates in order of their first control point, so the index is stable across runs.
    for index, group in enumerate(sorted(members.values(), key=min)):
        c = [sum(flat[3 * cp + k] for cp in group) / len(group) for k in range(3)]
        for cp in group:
            centre[cp] = (-c[0], c[1], c[2], float(index))
    assert len(members) == plates_expected and all(len(g) == 6 for g in members.values()), \
        f"{fbx}: expected {plates_expected} triangular prisms of 6 corners, got {sorted(len(g) for g in members.values())}"

    verts, indices, corner, poly = [], [], 0, []
    for raw in pvi:
        last = raw < 0
        poly.append((-raw - 1 if last else raw, corner))
        corner += 1
        if not last:
            continue
        base = len(verts)
        for cp, c in poly:
            x, y, z = flat[3 * cp: 3 * cp + 3]
            n = n_index[c]
            nx, ny, nz = normals[3 * n: 3 * n + 3]
            u = uv_index[c] if uv_index is not None else 0
            # Right-handed FBX -> left-handed Unity: negate x (positions AND normals).
            verts.append([(-x, y, z), (-nx, ny, nz), (uvs[2 * u], uvs[2 * u + 1]), centre[cp]])
        # Fan-triangulate (every polygon here is a convex tri or quad), and reverse
        # the winding, because the handedness flip mirrored it.
        for k in range(1, len(poly) - 1):
            indices += [base, base + k + 1, base + k]
        poly = []

    # Per-face tangent from the UV gradient (the graph's Tangent block passes it through).
    tangents = [None] * len(verts)
    for t in range(0, len(indices), 3):
        i0, i1, i2 = indices[t:t + 3]
        (p0, n0, w0, _), (p1, _, w1, _), (p2, _, w2, _) = verts[i0], verts[i1], verts[i2]
        e1 = [p1[k] - p0[k] for k in range(3)]
        e2 = [p2[k] - p0[k] for k in range(3)]
        du1, dv1, du2, dv2 = w1[0] - w0[0], w1[1] - w0[1], w2[0] - w0[0], w2[1] - w0[1]
        det = du1 * dv2 - du2 * dv1
        tan = [(dv2 * e1[k] - dv1 * e2[k]) for k in range(3)] if abs(det) > 1e-12 else e1
        # Gram-Schmidt against the normal, then normalize.
        d = sum(tan[k] * n0[k] for k in range(3))
        tan = [tan[k] - n0[k] * d for k in range(3)]
        m = math.sqrt(sum(c * c for c in tan)) or 1.0
        for i in (i0, i1, i2):
            if tangents[i] is None:
                tangents[i] = (tan[0] / m, tan[1] / m, tan[2] / m, 1.0)

    assert all(t is not None for t in tangents)
    return verts, tangents, indices


def prism_shape(verts):
    """(top height, plate radius) of a baked prism mesh, in mesh units: how far each prism's top face sits
    above its centre along its column, and its mean corner distance from its centre (the face split's unit).
    Both are the same for every plate of these meshes; fails loud if they ever are not."""
    by_plate = defaultdict(list)
    for p, _n, _uv, c in verts:
        by_plate[int(c[3])].append((p, c))
    tops, radii = [], []
    for rows in by_plate.values():
        c = rows[0][1][:3]
        r = math.sqrt(sum(x * x for x in c))
        d = [x / r for x in c]
        tops.append(max(sum((p[k] - c[k]) * d[k] for k in range(3)) for p, _ in rows))
        radii.append(sum(math.dist(p, c) for p, _ in rows) / len(rows))
    for name, vals in (("top height", tops), ("plate radius", radii)):
        if max(vals) - min(vals) > 1e-4 * max(vals):
            raise SystemExit(f"prism {name} differs between plates ({min(vals)}..{max(vals)}); "
                             "OmniDepthPrismShader carries ONE value per material")
    return round(sum(tops) / len(tops), 6), round(sum(radii) / len(radii), 6)


def thicken(verts, thickness):
    """A baked prism mesh with each prism stretched along its column to `thickness`, INWARD from its top face -
    OmniDepthPrismShader's _Thickness term (with _ThicknessInward 1), applied once here instead of every frame:
    local += dir * (dot(local, dir) - top) * (thickness - 1). The top face does not move, so a sealed prism stays
    flush with the body; normals are unchanged (the shader never turned them either). The top height is the RAW
    mesh's, the same for every plate (prism_shape fails loud otherwise)."""
    # The shader parts each face along its own normal with no sign test, which is right only while every
    # normal faces out of its prism (measured 2026-10-13: min n.(v - c) is 0.038 on the omni, 0.018 on the Mass).
    for p, n, _uv, c in verts:
        if sum(n[k] * (p[k] - c[k]) for k in range(3)) <= 0:
            raise SystemExit(f"prism normal {n} at {p} faces INTO its prism (centre {c[:3]}): OmniDepthPrismShader's "
                             "face split would part that face inward")
    if thickness == 1:
        return verts
    top, _ = prism_shape(verts)
    out = []
    for p, n, uv, c in verts:
        r = math.sqrt(sum(x * x for x in c[:3]))
        d = [x / r for x in c[:3]]
        local = [p[k] - c[k] for k in range(3)]
        along = (sum(local[k] * d[k] for k in range(3)) - top) * (thickness - 1)
        out.append([tuple(p[k] + d[k] * along for k in range(3)), n, uv, c])
    return out


def tri_mesh_text():
    verts, tangents, indices = bake_tri_mesh()
    return prism_mesh_text(thicken(verts, DEPTH_THICKNESS), tangents, indices, "OmniCrystalTriangles")


def prism_mesh_text(verts, tangents, indices, name):
    vbuf = bytearray()
    for (p, n, uv, c), t in zip(verts, tangents):
        # stride 64: Prism.asset's layout, then TEXCOORD2 = (plate centre xyz, plate index) - what
        # OmniDepthPrismShader and CrystalDepthPrisms read (OmniShepardFresnelShader reads the xyz).
        vbuf += struct.pack("<3f3f4f2f4f", *p, *n, *t, *uv, *c)
    ibuf = struct.pack("<%dH" % len(indices), *indices)
    lo = [min(v[0][k] for v in verts) for k in range(3)]
    hi = [max(v[0][k] for v in verts) for k in range(3)]
    ctr = [(lo[k] + hi[k]) / 2 for k in range(3)]
    ext = [(hi[k] - lo[k]) / 2 for k in range(3)]

    def v3(a):
        return "{x: %s, y: %s, z: %s}" % tuple(repr(float(struct.unpack("<f", struct.pack("<f", c))[0])) for c in a)

    # Channel table: position, normal, tangent, colour, uv0..7, blend weights, blend indices.
    # Channel 6 is uv2 = TEXCOORD2, the plate centre and index.
    used = {0: (0, 3), 1: (12, 3), 2: (24, 4), 4: (40, 2), 6: (48, 4)}
    channels = "".join(
        "    - stream: 0\n      offset: %d\n      format: 0\n      dimension: %d\n"
        % used.get(i, (0, 0)) for i in range(14))
    donor = open(os.path.join(ROOT, "Assets/_Models/Testing/Prism.asset")).read()
    head = donor[:donor.index("  m_SubMeshes:")].replace("m_Name: Prism", f"m_Name: {name}")
    tail = donor[donor.index("  m_CompressedMesh:"):]
    tail = re.sub(r"  m_LocalAABB:\n    m_Center: .*\n    m_Extent: .*\n",
                  f"  m_LocalAABB:\n    m_Center: {v3(ctr)}\n    m_Extent: {v3(ext)}\n", tail)
    return (head
            + "  m_SubMeshes:\n  - serializedVersion: 2\n    firstByte: 0\n"
            + f"    indexCount: {len(indices)}\n    topology: 0\n    baseVertex: 0\n"
            + f"    firstVertex: 0\n    vertexCount: {len(verts)}\n    localAABB:\n"
            + f"      m_Center: {v3(ctr)}\n      m_Extent: {v3(ext)}\n"
            + donor[donor.index("  m_Shapes:"):donor.index("  m_IndexBuffer:")]
            + f"  m_IndexBuffer: {ibuf.hex()}\n"
            + "  m_VertexData:\n    serializedVersion: 3\n"
            + f"    m_VertexCount: {len(verts)}\n    m_Channels:\n{channels}"
            + f"    m_DataSize: {len(vbuf)}\n    _typelessdata: {vbuf.hex()}\n"
            + tail)


TRI_MESH_META = f"""fileFormatVersion: 2
guid: {TRI_MESH_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 4300000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


# ── prefab authoring ─────────────────────────────────────────────────────────

def shell_prefab_text(scale):
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{SHELL_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {SHELL_TR}}}
  - component: {{fileID: {SHELL_MF}}}
  - component: {{fileID: {SHELL_MR}}}
  - component: {{fileID: {SHELL_FADE}}}
  - component: {{fileID: {SHELL_DEPTH}}}
  m_Layer: 0
  m_Name: OmniCrystalTriangles
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{SHELL_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHELL_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: {scale}, y: {scale}, z: {scale}}}
  m_ConstrainProportionsScale: 1
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{SHELL_MF}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHELL_GO}}}
  m_Mesh: {{fileID: {TRI_MESH_FILE_ID}, guid: {TRI_MESH_GUID}, type: 2}}
--- !u!23 &{SHELL_MR}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHELL_GO}}}
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
  - {{fileID: 2100000, guid: {MAT_DEPTH}, type: 2}}
  m_StaticBatchInfo:
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
--- !u!114 &{SHELL_FADE}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHELL_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {FADE_IN_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  fadeInRate: 0
{depth_prisms_doc(SHELL_DEPTH, SHELL_GO, True)}"""


def depth_prisms_doc(fid, go, cut_body_holes):
    """The CrystalDepthPrisms component on a prism renderer (omni: cuts the body's triangle holes)."""
    return (f"--- !u!114 &{fid}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
            f"  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n"
            f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n"
            f"  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {DEPTH_PRISMS_GUID}, type: 3}}\n"
            f"  m_Name: \n  m_EditorClassIdentifier: \n"
            f"  profile: {{fileID: 11400000, guid: {DEPTH_PROFILE_GUID}, type: 2}}\n"
            f"  cutBodyHoles: {1 if cut_body_holes else 0}\n")


def depth_profile_text():
    fields = "".join(f"  {k}: {v}\n" for k, v in DEPTH_PROFILE_VALUES)
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n"
            "  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {DEPTH_PROFILE_SO_GUID}, type: 3}}\n"
            "  m_Name: CrystalDepthPrismProfile\n  m_EditorClassIdentifier: \n" + fields)


DEPTH_PROFILE_META = f"""fileFormatVersion: 2
guid: {DEPTH_PROFILE_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def script_meta(guid):
    return (f"fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n  externalObjects: {{}}\n"
            "  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            "  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


DEPTH_SHADER_META = f"""fileFormatVersion: 2
guid: {DEPTH_SHADER_GUID}
ShaderImporter:
  externalObjects: {{}}
  defaultTextures: []
  nonModifiableTextures: []
  preprocessorOverride: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def body_mesh_ref():
    """The omni model's mesh reference exactly as the body prefab records it (Unity minted that fileID
    on import, so it is READ, never computed - see the docstring's note on FBX sub-asset ids)."""
    refs = re.findall(r"^  m_Mesh: (\{fileID: -?\d+, guid: \w+, type: 3\})$",
                      open(os.path.join(ROOT, TRUNC_OCTA_PREFAB)).read(), re.M)
    if len(refs) != 1:
        raise SystemExit(f"{TRUNC_OCTA_PREFAB}: expected one MeshFilter, found {len(refs)}")
    return refs[0]


def arcs_profile_text():
    fields = "".join(f"  {k}: {v}\n" for k, v in ARCS_PROFILE_VALUES)
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n"
            "  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {ARCS_PROFILE_SO_GUID}, type: 3}}\n"
            "  m_Name: CrystalChargeArcsProfile\n  m_EditorClassIdentifier: \n" + fields)


ARCS_PROFILE_META = f"""fileFormatVersion: 2
guid: {ARCS_PROFILE_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

ARCS_SHADER_META = f"""fileFormatVersion: 2
guid: {ARCS_SHADER_GUID}
ShaderImporter:
  externalObjects: {{}}
  defaultTextures: []
  nonModifiableTextures: []
  preprocessorOverride: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def arcs_child_docs(ids, name, layer, father, source_mesh, material, effect, silence):
    """A Charge light child, as documents embedded in its crystal's prefab: a GameObject at the crystal's own pose
    (identity, scale 1, so it shares the model's space) with an empty MeshFilter (CrystalChargeArcs fills it each frame),
    an additive renderer with no shadows or probes, CrystalChargeArcs and - on the omni - CrystalAccentTint."""
    go, tr, mf, mr, arcs, tint = ids
    components = [tr, mf, mr, arcs] + ([tint] if tint else [])

    def head(kind, cls, fid):
        return (f"--- !u!{kind} &{fid}\n{cls}:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
                f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n")

    def behaviour(fid, guid, fields):
        return (head(114, "MonoBehaviour", fid) + f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
                f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n{fields}")

    out = (head(1, "GameObject", go) + "  serializedVersion: 6\n  m_Component:\n"
           + "".join(f"  - component: {{fileID: {c}}}\n" for c in components)
           + f"  m_Layer: {layer}\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n"
           "  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
           + head(4, "Transform", tr) + f"  m_GameObject: {{fileID: {go}}}\n  serializedVersion: 2\n"
           "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
           "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 1\n  m_Children: []\n"
           f"  m_Father: {{fileID: {father}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
           + head(33, "MeshFilter", mf) + f"  m_GameObject: {{fileID: {go}}}\n  m_Mesh: {{fileID: 0}}\n"
           + head(23, "MeshRenderer", mr) + f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n"
           "  m_CastShadows: 0\n  m_ReceiveShadows: 0\n  m_DynamicOccludee: 1\n  m_StaticShadowCaster: 0\n"
           "  m_MotionVectors: 2\n  m_LightProbeUsage: 0\n  m_ReflectionProbeUsage: 0\n  m_RayTracingMode: 0\n"
           "  m_RayTraceProcedural: 0\n  m_RayTracingAccelStructBuildFlagsOverride: 0\n"
           "  m_RayTracingAccelStructBuildFlags: 1\n  m_SmallMeshCulling: 1\n  m_RenderingLayerMask: 1\n"
           "  m_RendererPriority: 0\n  m_Materials:\n"
           f"  - {{fileID: 2100000, guid: {material}, type: 2}}\n"
           "  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n  m_StaticBatchRoot: {fileID: 0}\n"
           "  m_ProbeAnchor: {fileID: 0}\n  m_LightProbeVolumeOverride: {fileID: 0}\n  m_ScaleInLightmap: 1\n"
           "  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n  m_IgnoreNormalsForChartDetection: 0\n  m_ImportantGI: 0\n"
           "  m_StitchLightmapSeams: 1\n  m_SelectedEditorRenderState: 3\n  m_MinimumChartSize: 4\n"
           "  m_AutoUVMaxDistance: 0.5\n  m_AutoUVMaxAngle: 89\n  m_LightmapParameters: {fileID: 0}\n"
           "  m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder: 0\n  m_AdditionalVertexStreams: {fileID: 0}\n"
           + behaviour(arcs, ARCS_GUID, f"  effect: {effect}\n  profile: {{fileID: 11400000, guid: {ARCS_PROFILE_GUID}, type: 2}}\n"
                                        f"  sourceMesh: {source_mesh}\n  silenceCrawlOn: {{fileID: {silence}}}\n"))
    if tint:
        out += behaviour(tint, ACCENT_TINT_GUID, "  crystal: {fileID: 0}\n")
    return out


def _without_docs(text, ids):
    for fid in ids:
        if fid:
            text = re.sub(rf"^--- !u!\d+ &{fid}\n(?:(?!--- ).*\n)*", "", text, flags=re.M)
    return text


def build_charge_crystal_prefab(existing):
    """CrystalCharge.prefab with its seam-walk child: the shell's own mesh and space, the shell's crawl silenced."""
    t = _without_docs(existing, CHARGE_ARCS_IDS)
    mesh = re.findall(rf"^--- !u!33 &{CHARGE_CRYSTAL_SHELL_MF}\n(?:(?!--- ).*\n)*?  m_Mesh: (\{{[^}}]*\}})$", t, re.M)
    if len(mesh) != 1:
        raise SystemExit(f"{CHARGE_CRYSTAL_PREFAB}: the shell's MeshFilter not found")
    entry = f"  - {{fileID: {CHARGE_ARCS_IDS[1]}}}\n"
    shell_entry = f"  - {{fileID: {CHARGE_CRYSTAL_SHELL_TR}}}\n"
    if entry not in t:
        if t.count(shell_entry) != 1:
            raise SystemExit(f"{CHARGE_CRYSTAL_PREFAB}: the root's child list (the shell) not found exactly once")
        t = t.replace(shell_entry, shell_entry + entry)
    return t + arcs_child_docs(CHARGE_ARCS_IDS, "ChargeCrystalArcs", 0, CHARGE_CRYSTAL_ROOT_TR, mesh[0],
                               mat_guid(CHARGE_ARCS_MAT), ARCS_SEAM_WALK, CHARGE_CRYSTAL_SHELL_MR)


def body_prefab_text():
    """OmniCrystalBody.prefab: TrucatedOctahedron's GameObject, Transform and FadeIn under the SAME ids,
    with its MeshRenderer re-classed in place as a SkinnedMeshRenderer (id kept, so slot 0's material
    overrides still land) and no MeshFilter. No bones are authored: RhombusSkinBaker adds them at runtime,
    and until it does the renderer draws the mesh unskinned - the same body, at rest. The AABB is the
    mesh's own box widened by a rhombus's reach, so a turning plate is never culled."""
    octa = open(os.path.join(ROOT, TRUNC_OCTA_PREFAB)).read()
    fade = re.search(r"^--- !u!114 &2346576036425758005\n.*\Z", octa, re.S | re.M)
    if not fade or FADE_IN_GUID not in fade.group(0):
        raise SystemExit(f"{TRUNC_OCTA_PREFAB}: its FadeIn is no longer the last document - re-read the donor")
    verts, _ = _load(OMNI_FBX)
    half = max(max(abs(c) for c in v) for v in verts)
    extent = round(half * 1.25, 4)   # + a rhombus's reach (0.31 of the shell radius), rounded up
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{OCTA_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {OCTA_TR}}}
  - component: {{fileID: {OCTA_MR}}}
  - component: {{fileID: 2346576036425758005}}
  - component: {{fileID: {BODY_SQUARES}}}
  m_Layer: 0
  m_Name: OmniCrystalBody
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{OCTA_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {OCTA_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!137 &{OCTA_MR}
SkinnedMeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {OCTA_GO}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 3
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_BODY_EXPLODING}, type: 2}}
  m_StaticBatchInfo:
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
  serializedVersion: 2
  m_Quality: 0
  m_UpdateWhenOffscreen: 0
  m_SkinnedMotionVectors: 1
  m_Mesh: {body_mesh_ref()}
  m_Bones: []
  m_BlendShapeWeights: []
  m_RootBone: {{fileID: 0}}
  m_AABB:
    m_Center: {{x: 0, y: 0, z: 0}}
    m_Extent: {{x: {extent}, y: {extent}, z: {extent}}}
  m_DirtyAABB: 0
""" + fade.group(0) + body_squares_doc()


def _f32(c):
    """A float as Unity holds it (float32), written exactly."""
    return repr(float(struct.unpack("<f", struct.pack("<f", c))[0]))


def _v3(a):
    return "{x: %s, y: %s, z: %s}" % tuple(_f32(c) for c in a)


def space_squares():
    """The squares, mapped onto the body FBX's control points: (anchors, body radius, keys, weights, fps).
    The .blend carries the model at another scale (the triangle export's); the axes are the FBX's, so a basis
    point divided by the radius ratio IS a control point. Anchors and offsets come out in Unity's space (x
    negated, as for the prism plates). Refuses a bake that does not land on the body vertex for vertex, and
    anchors the runtime could confuse with each other or with a still vertex."""
    d = json.load(open(os.path.join(ROOT, SQUARES_JSON)))
    cps, _ = _load(OMNI_FBX)
    basis = [d["basis"][i:i + 3] for i in range(0, len(d["basis"]), 3)]
    rf = max(math.dist((0, 0, 0), p) for p in cps)
    sc = max(math.dist((0, 0, 0), p) for p in basis) / rf
    grid = defaultdict(list)
    for i, p in enumerate(cps):
        grid[tuple(round(c * 50) for c in p)].append(i)

    def nearest(q):
        key = tuple(round(c * 50) for c in q)
        cand = [j for dx in (-1, 0, 1) for dy in (-1, 0, 1) for dz in (-1, 0, 1)
                for j in grid.get((key[0] + dx, key[1] + dy, key[2] + dz), [])]
        return min(cand, key=lambda j: math.dist(cps[j], q))
    to_cp = {}
    for k in d["keys"]:
        for v in k["verts"]:
            q = [c / sc for c in basis[v]]
            j = nearest(q)
            if math.dist(cps[j], q) > 1e-4:
                raise SystemExit(f"{SQUARES_JSON}: basis vertex {v} lands {math.dist(cps[j], q):.2e} from the body FBX")
            to_cp[v] = j
    if len(set(to_cp.values())) != len(to_cp):
        raise SystemExit(f"{SQUARES_JSON}: two moving vertices land on one body control point")
    order = sorted(set(to_cp.values()))
    slot = {cp: a for a, cp in enumerate(order)}
    tol = SQUARES_MATCH * rf
    moving = set(order)
    for cp in order:
        for j in range(len(cps)):
            if j != cp and math.dist(cps[j], cps[cp]) < 10 * tol:
                raise SystemExit(f"body control points {cp} and {j} sit within {10 * tol:.1e}: the runtime match is ambiguous"
                                 + ("" if j in moving else " (and one of them holds still)"))
    anchors = [(-cps[cp][0], cps[cp][1], cps[cp][2]) for cp in order]
    names = [k["name"] for k in d["keys"]]
    keys = []
    for k in d["keys"]:
        offs = [(0.0, 0.0, 0.0)] * len(order)
        for n, v in enumerate(k["verts"]):
            dx, dy, dz = (c / sc for c in k["delta"][3 * n:3 * n + 3])
            offs[slot[to_cp[v]]] = (-dx, dy, dz)
        stacked = -1 if k["relative"] == "Basis" else names.index(k["relative"])
        keys.append((k["name"], stacked, offs))
    weights = [w for row in d["weights"] for w in row]
    return anchors, rf, keys, weights, d["fps"]


def space_squares_text():
    anchors, radius, keys, weights, fps = space_squares()
    out = ["%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
           "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
           "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n"
           "  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
           f"  m_Script: {{fileID: 11500000, guid: {SQUARES_SO_GUID}, type: 3}}\n"
           "  m_Name: OmniSpaceSquares\n  m_EditorClassIdentifier: \n  anchors:\n"]
    out += [f"  - {_v3(a)}\n" for a in anchors]
    out.append(f"  bodyRadius: {_f32(radius)}\n  keys:\n")
    for name, stacked, offs in keys:
        out.append(f"  - name: {name}\n    stackedOn: {stacked}\n    deltas:\n")
        out += [f"    - {_v3(o)}\n" for o in offs]
    out.append("  weights:\n")
    out += [f"  - {_f32(w)}\n" for w in weights]
    out.append(f"  framesPerSecond: {fps}\n  playbackSpeed: {SQUARES_PLAYBACK_SPEED}\n")
    return "".join(out)


SQUARES_ASSET_META = f"""fileFormatVersion: 2
guid: {SQUARES_ASSET_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def body_squares_doc():
    """The CrystalSpaceSquares driver on the body."""
    return (f"--- !u!114 &{BODY_SQUARES}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
            f"  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n"
            f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {OCTA_GO}}}\n  m_Enabled: 1\n"
            f"  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {SQUARES_DRIVER_GUID}, type: 3}}\n"
            f"  m_Name: \n  m_EditorClassIdentifier: \n"
            f"  squares: {{fileID: 11400000, guid: {SQUARES_ASSET_GUID}, type: 2}}\n")


BODY_META = f"""fileFormatVersion: 2
guid: {BODY_PREFAB_GUID}
PrefabImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


FLIP_WAVE_DOC = re.compile(r"^--- !u!114 &(-?\d+)\nMonoBehaviour:\n(?:  .*\n)*?  m_Script: \{fileID: 11500000, guid: "
                           + FLIP_WAVE_GUID + r", type: 3\}\n((?:  .*\n)*)", re.M)


def check_flip_wave(crystal_text):
    """The root's CrystalFlipWave sits in the part of Crystal.prefab this script preserves; hold it to the
    body it has to drive."""
    m = FLIP_WAVE_DOC.search(crystal_text)
    problems = []
    if not m:
        return [f"{CRYSTAL_PREFAB}: the root has no CrystalFlipWave - the rhombi will hold still"]
    body = m.group(2)
    if f"  model: {{fileID: {SLOTS[0][1]}}}\n" not in body:
        problems.append("CrystalFlipWave.model is not the body's stripped Transform")
    if "  skinRhombi: 1\n" not in body:
        problems.append("CrystalFlipWave.skinRhombi is off - the body's rhombi have no bones to turn")
    if f"guid: {FLIP_PROFILE_GUID}" not in body:
        problems.append("CrystalFlipWave.profile is not TimeCrystalFlipWaveProfile")
    if f"  - component: {{fileID: {m.group(1)}}}\n" not in crystal_text:
        problems.append("CrystalFlipWave is not in the root's m_Component list")
    return [f"{CRYSTAL_PREFAB}: {p}" for p in problems]


SHELL_META = f"""fileFormatVersion: 2
guid: {SHELL_PREFAB_GUID}
PrefabImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def _instance_block(inst_id, source_guid, target_go, target_tr, target_mr, name, material_guid):
    def mod(target, path, value="", ref="{fileID: 0}"):
        return (f"    - target: {{fileID: {target}, guid: {source_guid},\n"
                f"        type: 3}}\n"
                f"      propertyPath: {path}\n"
                f"      value: {value}\n"
                f"      objectReference: {ref}\n")

    mods = "".join(
        [mod(target_tr, f"m_LocalPosition.{a}", "0") for a in "xyz"]
        + [mod(target_tr, "m_LocalRotation.w", "1")]
        + [mod(target_tr, f"m_LocalRotation.{a}", "0") for a in "xyz"]
        + [mod(target_tr, f"m_LocalEulerAnglesHint.{a}", "0") for a in "xyz"]
        + [mod(target_mr, "m_Materials.Array.size", "1")]
        + [mod(target_mr, "'m_Materials.Array.data[0]'", "",
               f"{{fileID: 2100000, guid: {material_guid}, type: 2}}")]
        + [mod(target_go, "m_Name", name)]
    )
    return f"""--- !u!1001 &{inst_id}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 5535990081244205889}}
    m_Modifications:
{mods}    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {source_guid}, type: 3}}
"""


def _stripped_blocks(inst_id, source_guid, stripped_tr, stripped_go, target_tr, target_go):
    return f"""--- !u!4 &{stripped_tr} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: {target_tr}, guid: {source_guid},
    type: 3}}
  m_PrefabInstance: {{fileID: {inst_id}}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!1 &{stripped_go} stripped
GameObject:
  m_CorrespondingSourceObject: {{fileID: {target_go}, guid: {source_guid},
    type: 3}}
  m_PrefabInstance: {{fileID: {inst_id}}}
  m_PrefabAsset: {{fileID: 0}}
"""


def wave_active_seconds():
    """Seconds a Time wave's rings move: the flip profile's loop less its lead-in (the profile's own contract is that
    every ring lands inside the loop, so this is the span from the lead ring's start to the last ring's landing)."""
    text = open(os.path.join(ROOT, FLIP_PROFILE)).read()
    head = {k: float(v) for k, v in re.findall(r"^  (loopSeconds|firstRingStartSeconds): (\S+)$", text, re.M)}
    if len(head) != 2:
        raise SystemExit(f"{FLIP_PROFILE}: loopSeconds / firstRingStartSeconds not found")
    return round(head["loopSeconds"] - head["firstRingStartSeconds"], 4)


def cadence_text():
    values = [(k, wave_active_seconds() if v is None else v) for k, v in CADENCE_VALUES]
    fields = "".join(f"  {k}: {v}\n" for k, v in values)
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n"
            "  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {CADENCE_SO_GUID}, type: 3}}\n"
            "  m_Name: OmniCrystalCadence\n  m_EditorClassIdentifier: \n" + fields)


CADENCE_ASSET_META = f"""fileFormatVersion: 2
guid: {CADENCE_ASSET_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

CADENCE_DOC = re.compile(rf"^--- !u!114 &{CADENCE_COMPONENT}\nMonoBehaviour:\n(?:  .*\n)+", re.M)


def with_cadence(head):
    """The Crystal root's OmniCrystalCadence, authored into the part of Crystal.prefab this script otherwise preserves:
    listed right after CrystalFlipWave on the root and serialized after it."""
    doc = (f"--- !u!114 &{CADENCE_COMPONENT}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
           "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
           "  m_GameObject: {fileID: 5535990081244205914}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
           f"  m_Script: {{fileID: 11500000, guid: {CADENCE_GUID}, type: 3}}\n"
           "  m_Name: \n  m_EditorClassIdentifier: \n"
           f"  settings: {{fileID: 11400000, guid: {CADENCE_ASSET_GUID}, type: 2}}\n")
    head = CADENCE_DOC.sub("", head)
    entry = f"  - component: {{fileID: {CADENCE_COMPONENT}}}\n"
    flip_entry = f"  - component: {{fileID: {FLIP_WAVE_COMPONENT}}}\n"
    if entry not in head:
        if head.count(flip_entry) != 1:
            raise SystemExit(f"{CRYSTAL_PREFAB}: the root's CrystalFlipWave entry not found exactly once")
        head = head.replace(flip_entry, flip_entry + entry)
    if not head.endswith("\n"):
        raise SystemExit(f"{CRYSTAL_PREFAB}: unexpected head")
    return head + doc


def crystal_children_text():
    """Crystal.prefab's children: the crystalModels slots' PrefabInstances in order, then the Charge light (a plain
    child, not a slot)."""
    out = []
    for i, (inst, stripped_tr, stripped_go, name) in enumerate(SLOTS):
        body = i == 0
        guid = BODY_PREFAB_GUID if body else SHELL_PREFAB_GUID
        t_go, t_tr, t_mr = ((OCTA_GO, OCTA_TR, OCTA_MR) if body
                            else (SHELL_GO, SHELL_TR, SHELL_MR))
        mat = SLOT_MATERIALS[i][0]
        out.append(_instance_block(inst, guid, t_go, t_tr, t_mr, name, mat))
        out.append(_stripped_blocks(inst, guid, stripped_tr, stripped_go, t_tr, t_go))
    out.append(arcs_child_docs(OMNI_ARCS_IDS, "OmniCrystalChargeArcs", 0, CRYSTAL_ROOT_TR, body_mesh_ref(),
                               mat_guid(OMNI_ARCS_MAT), ARCS_FLUX, 0))
    return "".join(out)


def crystal_models_text():
    """The Crystal component's crystalModels list - the body and the depth prisms.

    Every slot explodes on CrystalMaterial: the spent husk animates CrystalGraph's _velocity, which
    neither Fresnel shader carries, and a tone material on a husk would play its band, not burst."""
    rows = []
    for i, (_, _, stripped_go, _) in enumerate(SLOTS):
        default, inactive = SLOT_MATERIALS[i]
        exploding = MAT_BODY_EXPLODING
        rows.append(
            f"  - model: {{fileID: {stripped_go}}}\n"
            f"    defaultMaterial: {{fileID: 2100000, guid: {default}, type: 2}}\n"
            f"    explodingMaterial: {{fileID: 2100000, guid: {exploding}, type: 2}}\n"
            f"    inactiveMaterial: {{fileID: 2100000, guid: {inactive}, type: 2}}\n"
            f"    spaceCrystalAnimator: {{fileID: 0}}\n")
    return "  crystalModels:\n" + "".join(rows)


# ── team crystals ──
# A domain-owned crystal (Skim Race track crystals via CrystalManager, the Dolphin's TeamCrystal)
# swaps each model to ThemeManagerDataContainerSO.GetTeamCrystalMaterial(domain, slot), and
# ThemeManager builds those by cloning the BASE set's CrystalMaterial..CrystalMaterial4 and painting
# the domain pair (_BrightColor = BrightCrystalColor, _DarkColor = DullCrystalColor). So the base set
# must point at exactly the omni's per-slot materials, or a team omni wears some other geometry's
# bands - which is what the Mass-era set did (a band on the body, inward bands on the triangles).
BASE_MATERIAL_SET = "Assets/_SO_Assets/MaterialSets/OriginalMaterialSet.asset"
BASE_SET_FIELDS = ["CrystalMaterial", "CrystalMaterial1", "CrystalMaterial2", "CrystalMaterial3",
                   "CrystalMaterial4"]


def build_base_material_set(existing):
    """The base set names the omni's per-slot materials and nothing else: slots past the crystal's models are EMPTY
    (since 2026-10-13; they named the retired breathing tone and rim materials, which ThemeManager cloned for every
    domain although no crystal drew them)."""
    t = existing
    slots = [f"{{fileID: 2100000, guid: {default}, type: 2}}" for default, _ in SLOT_MATERIALS]
    slots += ["{fileID: 0}"] * (len(BASE_SET_FIELDS) - len(slots))
    for field, ref in zip(BASE_SET_FIELDS, slots):
        line = f"  {field}: {ref}"
        t, n = re.subn(rf"^  {field}: \{{fileID: \d+(?:, guid: \w+,\s*\n?\s*type: 2)?\}}$", line, t,
                       count=1, flags=re.M)
        if n == 0:
            # A field the asset has never serialized (CrystalMaterial4) goes after its predecessor.
            prev = BASE_SET_FIELDS[BASE_SET_FIELDS.index(field) - 1]
            t, n = re.subn(rf"(^  {prev}: \{{[^}}]*\}}\n)", lambda m: m.group(1) + line + "\n", t,
                           count=1, flags=re.M)
        if n != 1:
            raise SystemExit(f"{BASE_MATERIAL_SET}: could not author {field}")
    return t


CHILDREN_START = re.compile(r"^--- !u!1001 &", re.M)
MODELS_BLOCK = re.compile(r"^  crystalModels:\n(?:  - model:.*?\n(?:    .*\n)*)+", re.M)


def build_crystal_prefab(existing):
    head = existing[:CHILDREN_START.search(existing).start()]
    head, n = MODELS_BLOCK.subn(crystal_models_text(), head)
    children = "".join(f"  - {{fileID: {tr}}}\n" for _, tr, _, _ in SLOTS) + f"  - {{fileID: {OMNI_ARCS_IDS[1]}}}\n"
    head, m = re.subn(r"(^  m_Children:\n)(?:  - \{fileID: \d+\}\n){3,6}", lambda x: x.group(1) + children,
                      head, count=1, flags=re.M)
    if m != 1:
        raise SystemExit(f"{CRYSTAL_PREFAB}: root m_Children (3-6 entries) not found")
    if n != 1:
        raise SystemExit(f"{CRYSTAL_PREFAB}: expected exactly one crystalModels block, found {n}")
    return with_cadence(head) + crystal_children_text()


# ── entry point ──────────────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true",
                    help="verify the shipped assets match what this script would author")
    args = ap.parse_args()

    scale, residual = measure()
    shell_scale = round(1.0 / scale, 9)
    print(f"omni triangles -> triangle model: uniform scale {scale:.9f} "
          f"(max residual {residual:.3e})")
    print(f"prism localScale: {shell_scale}  (the mesh SEATED on the crystal's triangles; the top of a column "
          f"is {DEPTH_REACH}x out)")
    top_h, split = prism_shape(bake_tri_mesh()[0])
    print(f"depth prisms: thickness {DEPTH_THICKNESS} inward, baked into the mesh (top face {top_h} above the plate centre), "
          f"face split {DEPTH_FACE_SPLIT} x plate radius {split}; profile {dict(DEPTH_PROFILE_VALUES)}")

    want = {
        TRI_MESH_ASSET: tri_mesh_text(),
        TRI_MESH_ASSET + ".meta": TRI_MESH_META,
        SHELL_PREFAB: shell_prefab_text(shell_scale),
        SHELL_PREFAB + ".meta": SHELL_META,
        BODY_PREFAB: body_prefab_text(),
        BODY_PREFAB + ".meta": BODY_META,
        DEPTH_PROFILE: depth_profile_text(),
        DEPTH_PROFILE + ".meta": DEPTH_PROFILE_META,
        SQUARES_ASSET: space_squares_text(),
        SQUARES_ASSET + ".meta": SQUARES_ASSET_META,
        "Assets/_Scripts/Controller/Environment/Crystals/CrystalSpaceSquares.cs.meta": script_meta(SQUARES_DRIVER_GUID),
        "Assets/_Scripts/Controller/Environment/Crystals/CrystalSpaceSquaresSO.cs.meta": script_meta(SQUARES_SO_GUID),
        DEPTH_SHADER + ".meta": DEPTH_SHADER_META,
        "Assets/_Scripts/Controller/Environment/Crystals/CrystalDepthPrisms.cs.meta": script_meta(DEPTH_PRISMS_GUID),
        "Assets/_Scripts/Controller/Environment/Crystals/CrystalDepthPrismProfileSO.cs.meta": script_meta(DEPTH_PROFILE_SO_GUID),
        "Assets/_Scripts/Utility/DepthRatchet.cs.meta": script_meta(DEPTH_RATCHET_GUID),
        CADENCE_ASSET: cadence_text(),
        CADENCE_ASSET + ".meta": CADENCE_ASSET_META,
        "Assets/_Scripts/Controller/Environment/Crystals/OmniCrystalCadenceSO.cs.meta": script_meta(CADENCE_SO_GUID),
        "Assets/_Scripts/Controller/Environment/Crystals/OmniCrystalCadence.cs.meta": script_meta(CADENCE_GUID),
        "Assets/_Scripts/Utility/OmniCadence.cs.meta": script_meta(OMNI_CADENCE_GUID),
        ARCS_PROFILE: arcs_profile_text(),
        ARCS_PROFILE + ".meta": ARCS_PROFILE_META,
        ARCS_SHADER + ".meta": ARCS_SHADER_META,
        "Assets/_Scripts/Controller/Environment/Crystals/CrystalChargeArcs.cs.meta": script_meta(ARCS_GUID),
        "Assets/_Scripts/Controller/Environment/Crystals/CrystalChargeArcsProfileSO.cs.meta": script_meta(ARCS_PROFILE_SO_GUID),
        "Assets/_Scripts/Utility/ChargeArcs.cs.meta": script_meta(ARCS_CORE_GUID),
        "Assets/_Scripts/Utility/SquareTurnFit.cs.meta": script_meta(SQUARE_TURN_FIT_GUID),
    }
    for path, text in material_texts().items():
        want[path] = text
        want[path + ".meta"] = material_meta(os.path.basename(path)[:-4])
    crystal_path = os.path.join(ROOT, CRYSTAL_PREFAB)
    want[CRYSTAL_PREFAB] = build_crystal_prefab(open(crystal_path).read())
    want[CHARGE_CRYSTAL_PREFAB] = build_charge_crystal_prefab(open(os.path.join(ROOT, CHARGE_CRYSTAL_PREFAB)).read())
    want[BASE_MATERIAL_SET] = build_base_material_set(open(os.path.join(ROOT, BASE_MATERIAL_SET)).read())

    failures = []
    for rel, text in want.items():
        path = os.path.join(ROOT, rel)
        have = open(path).read() if os.path.exists(path) else None
        if have == text:
            print(f"  ok      {rel}")
            continue
        if args.check:
            failures.append(rel)
            print(f"  DRIFT   {rel}")
        else:
            open(path, "w").write(text)
            print(f"  wrote   {rel}")

    if failures:
        raise SystemExit("author_omni_crystal_triangles: " + ", ".join(failures) +
                         " differ from what this script authors. Run it without --check.")
    broken = check_flip_wave(want[CRYSTAL_PREFAB])
    if broken:
        raise SystemExit("author_omni_crystal_triangles: " + "; ".join(broken))
    print("  ok      Crystal.prefab root carries CrystalFlipWave (skinRhombi, Time profile)")


if __name__ == "__main__":
    main()
