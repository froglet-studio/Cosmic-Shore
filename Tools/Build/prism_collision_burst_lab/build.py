#!/usr/bin/env python3
"""
Build Docs/Studios/PrismCollisionBurstLab.html — the COLLISION BURST LAB (round 2: the prism's interaction
detail levels — destruction and stealing) — from the shipped sources.

The lab is a READER of the game (`/labmaker` §2.1): every number and every line of shader math it runs
is stamped in here from the file the game ships, never typed into the page.

  burst math     Assets/_Graphics/Materials/Graphs/PrismCollisionBurst.hlsl  -> GLSL, MECHANICALLY
                 (HLSL spelling -> GLSL spelling only; the page runs the shipped functions, and
                 Tools/Build/prism_collision_burst_lab/parity.py holds it to the C++ execution of
                 the same file)
  explosion fade Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl (PrismErosionFade and
                 its helpers) -> GLSL, mechanically
  explosion map  RotateFacesAlongAxis.shadersubgraph — its three constants are READ from the graph and
                 asserted; the closed form the page writes is held to the graph's own dataflow
                 evaluation (Tools/Shaders/verify_prism_face_pivot.py's evaluator) by parity.py
  explosion dials ExplodingBlockMaterial.mat (_ExplosiveRotation, _ExplosiveSpead, _Spread),
                 PrismExplosion.prefab (minSpeed, maxSpeed, dangerDetonationMultiplier),
                 PrismExplosion.DefaultDuration
  explosion mesh Assets/_Models/Testing/Prism.asset (the 72 vertices, decoded from the mesh bytes)
  the grade      Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl (PrismColorLerp_float, the steal's
                 colour lerp) -> GLSL, mechanically; MaterialPropertyAnimator.DefaultTransitionSeconds
  the cut         Assets/_Scripts/Utility/PrismSierpinskiTiling.cs — its depth cap; the page's port of the
                 recursion is held to the C# (compiled with the .NET SDK) triangle for triangle by parity.py
  detail config  Assets/Resources/PrismInteractionLodConfig.asset (the detail levels' lines, the shape, the
                 shed weights) and Assets/Resources/PrismMotionConfig.asset (the motion ladder)
  burst look     Assets/_Graphics/Materials/PrismCollisionBurstMaterial.mat
  domain colours Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset (Inside/OutsideBlockColor —
                 the Plain tier pair TryGetPrismKindColors hands both the burst and the explosion)

    python3 Tools/Build/prism_collision_burst_lab/build.py           # write the page
    python3 Tools/Build/prism_collision_burst_lab/build.py --check   # exit 1 if the page is stale
"""

import json
import os
import re
import struct
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "lab.src.html")
OUT = os.path.join(ROOT, "Docs/Studios/PrismCollisionBurstLab.html")

GRAPHS = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs")
BURST_HLSL = os.path.join(GRAPHS, "PrismCollisionBurst.hlsl")
CORRIDOR_HLSL = os.path.join(GRAPHS, "PrismOcclusionCorridor.hlsl")
SUBGRAPH = os.path.join(GRAPHS, "PrismGraphs/Subgraphs/RotateFacesAlongAxis.shadersubgraph")
TANGENT_SLIDER = os.path.join(GRAPHS, "PrismGraphs/Subgraphs/TangentSlider.shadersubgraph")
EXPLODING_MAT = os.path.join(ROOT, "Assets/_Graphics/Materials/BlockMaterials/ExplodingBlockMaterial.mat")
EXPLOSION_PREFAB = os.path.join(ROOT, "Assets/_Prefabs/Trails/Prisms With Pools/PrismExplosion.prefab")
EXPLOSION_CS = os.path.join(ROOT, "Assets/_Scripts/Utility/Effects/PrismExplosion.cs")
PRISM_MESH = os.path.join(ROOT, "Assets/_Models/Testing/Prism.asset")
CONFIG = os.path.join(ROOT, "Assets/Resources/PrismMotionConfig.asset")
LOD_CONFIG = os.path.join(ROOT, "Assets/Resources/PrismInteractionLodConfig.asset")
CLOCK_HLSL = os.path.join(GRAPHS, "PrismClockAnimation.hlsl")
TILING_CS = os.path.join(ROOT, "Assets/_Scripts/Utility/PrismSierpinskiTiling.cs")
ANIMATOR_CS = os.path.join(ROOT, "Assets/_Scripts/Controller/Environment/Prisms/MaterialPropertyAnimator.cs")
BURST_MAT = os.path.join(ROOT, "Assets/_Graphics/Materials/PrismCollisionBurstMaterial.mat")
COLORSET = os.path.join(ROOT, "Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset")
MOTION_CS = os.path.join(ROOT, "Assets/_Scripts/Controller/Prisms/PrismMotion.cs")


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


# ------------------------------------------------------------------------------------------- HLSL -> GLSL

def strip_comments(src):
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    src = re.sub(r"//[^\n]*", "", src)
    return src


def hlsl_to_glsl(src):
    """HLSL spelling -> GLSL ES 3.0 spelling. Mechanical only: types, three intrinsics, static const."""
    out = strip_comments(src)
    out = re.sub(r"\bfloat4\b", "vec4", out)
    out = re.sub(r"\bfloat3\b", "vec3", out)
    out = re.sub(r"\bfloat2\b", "vec2", out)
    out = re.sub(r"\bfrac\(", "fract(", out)
    out = re.sub(r"\blerp\(", "mix(", out)
    out = re.sub(r"\bstatic const\b", "const", out)
    # x += y on a vector with a scalar right side is legal GLSL; nothing else to spell.
    out = "\n".join(l.rstrip() for l in out.splitlines() if l.strip())
    assert all(ord(c) < 128 for c in out), "non-ASCII survived the comment strip (WebGL rejects it)"
    for bad in ("float3", "float2", "frac(", "lerp(", "static "):
        assert bad not in out, f"untranslated '{bad}'"
    return out


def burst_glsl():
    src = read(BURST_HLSL)
    for name in ("void PrismBurstPose(", "float PrismBurstProgress(", "float PrismBurstDissolveValue(",
                 "bool PrismBurstClipped(", "float PrismBurstNoise(", "void PrismBurstVoronoi("):
        assert name in src, f"{name} missing from {BURST_HLSL}"
    return hlsl_to_glsl(src)


def extract_function(src, signature):
    i = src.index(signature)
    j = src.index("{", i)
    depth, k = 0, j
    while True:
        if src[k] == "{":
            depth += 1
        elif src[k] == "}":
            depth -= 1
            if depth == 0:
                return src[i:k + 1]
        k += 1


def grade_glsl():
    return hlsl_to_glsl(extract_function(read(CLOCK_HLSL), "void PrismColorLerp_float("))


def erosion_glsl():
    src = read(CORRIDOR_HLSL)
    parts = []
    for const in ("PRISM_EROSION_WIGGLE", "PRISM_EROSION_WIGGLE_FREQ", "PRISM_EROSION_END_MARGIN",
                  "PRISM_EROSION_FRINGE", "PRISM_EROSION_CDF_LO", "PRISM_EROSION_CDF_HI"):
        m = re.search(r"static const float " + const + r" = [^;]+;", src)
        assert m, f"{const} missing from {CORRIDOR_HLSL}"
        parts.append(m.group(0))
    for sig in ("float PrismOcclusionSafeThreshold(", "float3 PrismOcclusionHash3(", "float PrismOcclusionHash1(",
                "void PrismErosionFade_float("):
        parts.append(extract_function(src, sig))
    return hlsl_to_glsl("\n".join(parts))


# ------------------------------------------------------------------------------- the explosion's subgraph

def load_docs(path):
    text, dec, docs, i = read(path), json.JSONDecoder(), [], 0
    while i < len(text):
        while i < len(text) and text[i] in " \t\r\n":
            i += 1
        if i >= len(text):
            break
        obj, i = dec.raw_decode(text, i)
        docs.append(obj)
    return docs


def slot_value(docs, node_prefix, slot_id):
    idx = {d["m_ObjectId"]: d for d in docs if "m_ObjectId" in d}
    node = next(d for d in docs if d.get("m_ObjectId", "").startswith(node_prefix))
    for ref in node["m_Slots"]:
        s = idx[ref["m_Id"]]
        if s["m_Id"] == slot_id:
            v = s["m_Value"]
            return v["x"] if "x" in v else v["e00"]
    raise KeyError(node_prefix)


def subgraph_constants():
    """The three constants the explosion's closed form needs, read from the shipped graphs and asserted
    against the form the page writes. A graph edit that moves one fails the build, never the page."""
    docs = load_docs(SUBGRAPH)
    slider = load_docs(TANGENT_SLIDER)
    pivot_bias = slot_value(docs, "782733", 0)     # Subtract A: (-0.5) - SpreadValue
    slide_bias = slot_value(docs, "499421", 0)     # Add A:      0.5 + (SpreadValue + Amount*ExplosiveSpread)
    slider_gain = slot_value(slider, "5baaa9", 1)  # TangentSlider: Position + SpreadValue * Tangent * gain
    assert (pivot_bias, slide_bias, slider_gain) == (-0.5, 0.5, 0.5), \
        f"RotateFacesAlongAxis constants moved: {(pivot_bias, slide_bias, slider_gain)} — re-derive the closed form"
    return {"pivotBias": pivot_bias, "slideBias": slide_bias, "sliderGain": slider_gain}


# --------------------------------------------------------------------------------------------- the assets

def yaml_float(text, key):
    m = re.search(r"^\s*-?\s*" + re.escape(key) + r":\s*([-0-9.eE]+)\s*$", text, re.M)
    assert m, f"{key} missing"
    return float(m.group(1))


def yaml_color(text, key):
    m = re.search(re.escape(key) + r": \{r: ([-0-9.eE]+), g: ([-0-9.eE]+), b: ([-0-9.eE]+)(?:,\s*a: ([-0-9.eE]+))?\}", text)
    assert m, f"{key} missing"
    return [float(m.group(1)), float(m.group(2)), float(m.group(3))]


def prism_mesh():
    t = read(PRISM_MESH)
    data = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]+)", t).group(1))
    count = int(re.search(r"m_VertexCount: (\d+)", t).group(1))
    stride = len(data) // count
    assert stride == 48, f"Prism.asset vertex layout changed (stride {stride}); re-read its channels"
    verts = []
    for i in range(count):
        px, py, pz, nx, ny, nz, tx, ty, tz, tw, u, v = struct.unpack_from("<3f3f4f2f", data, i * stride)
        verts.append([px, py, pz, nx, ny, nz, tx, ty, tz, u, v])
    idx = bytes.fromhex(re.search(r"m_IndexBuffer: ([0-9a-f]+)", t).group(1))
    indices = list(struct.unpack("<%dH" % (len(idx) // 2), idx))
    assert indices == list(range(count)), "Prism.asset is no longer the unwelded 72-vertex prism"
    return [round(x, 6) for vtx in verts for x in vtx]


def domain_colors():
    t = read(COLORSET)
    out = {}
    for key in ("Jade", "Ruby", "Gold", "Blue"):
        block = re.search(key + r"Colors:\n((?:    .*\n)+)", t).group(1)
        out[key.lower()] = {"bright": yaml_color(block, "InsideBlockColor"), "dark": yaml_color(block, "OutsideBlockColor")}
    return out


def config():
    motion, lod = read(CONFIG), read(LOD_CONFIG)
    out = {k: yaml_float(motion, k) for k in ("showcaseMaxMovers", "boxMaxMovers")}
    for k in ("maxLiveBursts", "maxLiveTriangles", "crowdedFill", "packedFill", "shedMinScreen", "shedFineScreen", "shedFinestScreen", "shedSubdivision", "shedFineSubdivision",
              "shedFinestSubdivision", "bloomWeight", "unravelWeight", "flakeWeight", "shedInflate", "spread",
              "openSeconds", "spinPerSpeed", "bloomTurns", "unravelTurns", "ribbonsPerFace", "shardsPerFaceEdge",
              "dissolveGrain"):
        out[k] = yaml_float(lod, k)
    m = re.search(r"^  depthMinScreen:\n((?:  - [-0-9.eE]+\n)+)", lod, re.M)
    assert m, "depthMinScreen missing from PrismInteractionLodConfig.asset"
    out["depthMinScreen"] = [float(x) for x in re.findall(r"- ([-0-9.eE]+)", m.group(1))]
    out["sierpinskiMaxDepth"] = int(re.search(r"public const int MaxDepth = (\d+);", read(TILING_CS)).group(1))
    cs = read(ANIMATOR_CS)
    out["gradeSeconds"] = float(re.search(r"public const float DefaultTransitionSeconds = ([0-9.]+)f;", cs).group(1))
    return out


def burst_look():
    t = read(BURST_MAT)
    return {
        "fresnelPower": yaml_float(t, "_FresnelPower"),
        "innerGlow": yaml_float(t, "_InnerGlow"),
        "noiseScale": yaml_float(t, "_DissolveNoiseScale"),
    }


def explosion():
    mat, prefab, cs = read(EXPLODING_MAT), read(EXPLOSION_PREFAB), read(EXPLOSION_CS)
    duration = float(re.search(r"internal const float DefaultDuration = ([0-9.]+)f;", cs).group(1))
    return {
        "rotation": yaml_float(mat, "_ExplosiveRotation"),
        # The material spells it _ExplosiveSpead; it is the graph's ExplosiveSpread.
        "spreadPerAmount": yaml_float(mat, "_ExplosiveSpead"),
        "spreadValue": yaml_color(mat, "_Spread"),
        "minSpeed": yaml_float(prefab, "minSpeed"),
        "maxSpeed": yaml_float(prefab, "maxSpeed"),
        "dangerDetonation": yaml_float(prefab, "dangerDetonationMultiplier"),
        "duration": duration,
    }


def motion_constants():
    cs = read(MOTION_CS)
    rest = float(re.search(r"public const float DebrisRestitution = ([0-9.]+)f / ([0-9.]+)f;", cs).group(1)) / \
        float(re.search(r"public const float DebrisRestitution = ([0-9.]+)f / ([0-9.]+)f;", cs).group(2))
    limit = float(re.search(r"public const float DebrisSpeedLimit = ([0-9.]+)f;", cs).group(1))
    return {"debrisRestitution": rest, "debrisSpeedLimit": limit}


def build():
    data = {
        "config": config(),
        "look": burst_look(),
        "explosion": explosion(),
        "graph": subgraph_constants(),
        "motion": motion_constants(),
        "overshoot": float(re.search(r"#define PRISM_BURST_DISSOLVE_OVERSHOOT ([0-9.]+)", read(BURST_HLSL)).group(1)),
        "domains": domain_colors(),
        "prismMesh": prism_mesh(),
        "sources": {
            "burst": "Assets/_Graphics/Materials/Graphs/PrismCollisionBurst.hlsl",
            "erosion": "Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl (PrismErosionFade)",
            "grade": "Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl (PrismColorLerp_float)",
            "explosionMap": "RotateFacesAlongAxis.shadersubgraph (closed form, parity-gated)",
        },
    }
    page = read(SRC)
    for marker, value in (("/*@@DATA@@*/null", json.dumps(data, separators=(",", ":"))),
                          ("/*@@GLSL_BURST@@*/", burst_glsl()),
                          ("/*@@GLSL_EROSION@@*/", erosion_glsl()),
                          ("/*@@GLSL_GRADE@@*/", grade_glsl())):
        assert page.count(marker) == 1, f"marker {marker} must appear exactly once in {SRC}"
        page = page.replace(marker, value)
    return page


def main():
    page = build()
    if "--check" in sys.argv:
        current = read(OUT) if os.path.exists(OUT) else ""
        if current != page:
            print(f"STALE: {os.path.relpath(OUT, ROOT)} differs from a fresh build — run build.py")
            return 1
        print(f"OK: {os.path.relpath(OUT, ROOT)} is the build of the shipped sources")
        return 0
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(page)
    print(f"wrote {os.path.relpath(OUT, ROOT)} ({len(page) // 1024} KB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
