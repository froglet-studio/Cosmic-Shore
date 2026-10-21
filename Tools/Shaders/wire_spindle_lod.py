#!/usr/bin/env python3
"""
Generate the two spindle LOD shader graphs (and their template materials) FROM SpindleGraph.

    python3 Tools/Shaders/wire_spindle_lod.py            # write graphs, metas, template materials
    python3 Tools/Shaders/wire_spindle_lod.py --check    # fail if anything on disk drifted
    python3 Tools/Shaders/wire_spindle_lod.py --self-test

WHY TWO NEW GRAPHS AND NOT A KEYWORD ON SPINDLEGRAPH. SpindleGraph is the look Garrett wants KEPT up
close, and it is worn by five materials of which only one is a spindle (BranchingMembrane and the
Sparrow's exhaust projectile ride it too). The LOD therefore never touches it: the near tier IS
SpindleGraph, byte for byte. The mid and far tiers are SpindleGraph's own JSON with the LOD splice
added, regenerated from it on every run, so a later edit to the lace reaches both tiers by
re-running this script (and `--check` fails until somebody does - the drift gate).

  SpindleLodMidGraph   SpindleGraph + SpindleLodVertex between Position and SpindleSway (the
                       strands converge, the thread holds a visible width) + SpindleLodSurface
                       between the lace and the BaseColor / Alpha blocks (the lace closes into the
                       thread on the convergence schedule).
  SpindleLodFarGraph   SpindleGraph + the same vertex splice + SpindleLodThread feeding BaseColor /
                       Alpha straight from the colours and the death clock. The Voronoi chain is
                       left in the file but no block reads it, and Shader Graph generates code only
                       for what reaches a block, so the far thread pays nothing for the lace.

Both add two exposed per-material floats, `_SpindleLodGirth` and `_SpindleLodStrand` (default 0 =
pass-through), which SpindleLodSystem copies onto each runtime LOD material from the measured
profile. The bands and look are GLOBALS declared in SpindleLod.hlsl, not graph properties.

SpindleDepthGraph (Docs/SPINDLE_LOD.md §8) is the third: SpindleGraph + SpindleDepthVertex between Position
and SpindleSway + SpindleDepthGlow between the lace and the BaseColor / Alpha blocks (a body's alpha fades
out on the thread's band; a ring's is untouched). It carries the
overt depth effect onto every spindle-material surface that is NOT the braid - the creatures' own models
(form 0, bloom) and the switch rings (form 1, facets) - with three per-material floats,
`_SpindleLodStrength`, `_SpindleDepthReach` and `_SpindleDepthForm` (all 0 by default = pass-through).

TEMPLATE MATERIALS (SpindleLodMid.mat / SpindleLodFar.mat / SpindleDepth.mat) exist for the BUILD, not for looks: a
material created only at runtime does not stop the build from stripping the shader variants it
needs (`_ALPHATEST_ON`, `_SURFACE_TYPE_TRANSPARENT` are shader_feature keywords on a graph with
material override, and instancing variants are stripped unless some material asset enables them).
The profile asset references both, so the build keeps exactly the variants the runtime clones use.
Each template is SpindleMaterial with its shader swapped and the two LOD floats added.
"""

import copy
import hashlib
import json
import os
import re
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wire_prism_spindle_death_clock import (  # noqa: E402
    load_docs, dump_docs, find_graph, index, find_property, find_cf, make_slot, edge,
)
from wire_spindle_sway import (  # noqa: E402
    make_exposed_float, make_slot_v3, find_node, property_node_for, acyclic,
)

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPH_DIR = "Assets/_Graphics/Materials/Graphs"
SOURCE = f"{GRAPH_DIR}/SpindleGraph.shadergraph"
HLSL = f"{GRAPH_DIR}/SpindleLod.hlsl"
SOURCE_MATERIAL = "Assets/_Graphics/Materials/SpindleMaterial.mat"
MATERIAL_DIR = "Assets/_Graphics/Materials"
SHADERGRAPH_SHADER_FILE_ID = -6465566751694194690
SOURCE_GUID = "545a7b2ee58b90744bdb1c9e265a09f4"
UV_DONOR = "Assets/_Graphics/Materials/Lifeform_World_Shader Graphs/CreatureTextureGraph.shadergraph"
NORMAL_DONOR = f"{GRAPH_DIR}/TimeCrystalGraph.shadergraph"
VIEW_DONOR = f"{GRAPH_DIR}/ShepardGraph.shadergraph"

SPACE_OBJECT = 0
SPACE_WORLD = 2


def guid_for(name):
    return hashlib.md5(f"CosmicShore/SpindleLod/{name}".encode()).hexdigest()


HLSL_GUID = guid_for("SpindleLod.hlsl")
INCORPOREAL_HLSL = f"{GRAPH_DIR}/IncorporealDepth.hlsl"
INCORPOREAL_GUID = guid_for("IncorporealDepth.hlsl")
TIERS = {
    "mid": dict(graph="SpindleLodMidGraph", material="SpindleLodMid"),
    "far": dict(graph="SpindleLodFarGraph", material="SpindleLodFar"),
    "depth": dict(graph="SpindleDepthGraph", material="SpindleDepth"),
}
for t in TIERS.values():
    t["graph_guid"] = guid_for(t["graph"])
    t["material_guid"] = guid_for(t["material"])

LOD_PROPS = [("SpindleLodGirth", "_SpindleLodGirth"), ("SpindleLodStrand", "_SpindleLodStrand"),
             ("SpindleLodStrength", "_SpindleLodStrength")]
DEPTH_PROPS = [("SpindleLodStrength", "_SpindleLodStrength"), ("SpindleDepthReach", "_SpindleDepthReach"),
               ("SpindleDepthForm", "_SpindleDepthForm")]


def props_for(tier):
    return DEPTH_PROPS if tier == "depth" else LOD_PROPS

# (slot id, name, kind, is_output) - order IS the HLSL signature.
VERTEX_SLOTS = [
    (0, "PositionOS", "v3", False), (1, "NormalOS", "v3", False), (2, "UV", "v3", False),
    (3, "ToAxis", "v3", False), (4, "Radial", "v3", False), (5, "Splay", "v3", False),
    (6, "Girth", "v1", False), (7, "StrandRadius", "v1", False), (8, "Strength", "v1", False),
    (9, "Phase", "v1", False), (10, "Clock", "v1", False), (11, "Out", "v3", True),
]
SURFACE_SLOTS = [
    (0, "LaceColor", "v3", False), (1, "LaceAlpha", "v1", False), (2, "Dull", "v3", False),
    (3, "Bright", "v3", False), (4, "DeathState", "v1", False), (5, "NormalWS", "v3", False),
    (6, "ViewWS", "v3", False), (7, "UV", "v3", False), (8, "Girth", "v1", False),
    (9, "StrandRadius", "v1", False), (10, "Strength", "v1", False), (11, "Phase", "v1", False),
    (12, "Clock", "v1", False), (13, "Color", "v3", True), (14, "Alpha", "v1", True),
]
THREAD_SLOTS = [
    (0, "Dull", "v3", False), (1, "Bright", "v3", False), (2, "DeathState", "v1", False),
    (3, "NormalWS", "v3", False), (4, "ViewWS", "v3", False), (5, "UV", "v3", False),
    (6, "Girth", "v1", False), (7, "StrandRadius", "v1", False), (8, "Strength", "v1", False),
    (9, "Phase", "v1", False), (10, "Clock", "v1", False), (11, "Color", "v3", True), (12, "Alpha", "v1", True),
]
DEPTH_VERTEX_SLOTS = [
    (0, "PositionOS", "v3", False), (1, "NormalOS", "v3", False), (2, "UV", "v3", False),
    (3, "Reach", "v1", False), (4, "Form", "v1", False), (5, "Strength", "v1", False),
    (6, "Phase", "v1", False), (7, "Clock", "v1", False), (8, "Out", "v3", True),
]
DEPTH_GLOW_SLOTS = [
    (0, "Color", "v3", False), (1, "Alpha", "v1", False), (2, "PositionWS", "v3", False), (3, "UV", "v3", False),
    (4, "Reach", "v1", False), (5, "Form", "v1", False), (6, "Strength", "v1", False),
    (7, "Phase", "v1", False), (8, "Clock", "v1", False), (9, "OutColor", "v3", True), (10, "OutAlpha", "v1", True),
]
FUNCTIONS = {"SpindleLodVertex": VERTEX_SLOTS, "SpindleLodSurface": SURFACE_SLOTS, "SpindleLodThread": THREAD_SLOTS,
             "SpindleDepthVertex": DEPTH_VERTEX_SLOTS, "SpindleDepthGlow": DEPTH_GLOW_SLOTS}


def vertex_fn(tier):
    return "SpindleDepthVertex" if tier == "depth" else "SpindleLodVertex"


def fragment_fn(tier):
    return {"mid": "SpindleLodSurface", "far": "SpindleLodThread", "depth": "SpindleDepthGlow"}[tier]


def _oid(seed):
    """Deterministic object ids, so a re-run reproduces the file byte for byte (the --check)."""
    return hashlib.md5(f"CosmicShore/SpindleLod/oid/{seed}".encode()).hexdigest()


def _clone(doc, seed):
    d = copy.deepcopy(doc)
    d["m_ObjectId"] = _oid(seed)
    if "m_Group" in d:
        d["m_Group"] = {"m_Id": ""}
    return d


def _place(node, x, y, w=232.0, h=200.0):
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": w, "height": h})


def _block(docs, name):
    b = find_node(docs, name, "BlockNode")
    assert b is not None, f"SpindleGraph lost its {name} block"
    return b


def _block_slot(idx, block):
    return idx[block["m_Slots"][0]["m_Id"]]["m_Id"]


def _feeder(graph, node_oid, slot_id):
    for e in graph["m_Edges"]:
        if e["m_InputSlot"]["m_Node"]["m_Id"] == node_oid and e["m_InputSlot"]["m_SlotId"] == slot_id:
            return e
    return None


def _cf(donor_cf, donor_v1, donor_v3, fn, seed, x, y):
    node = _clone(donor_cf, f"{seed}/{fn}")
    node["m_Name"] = f"{fn} (Custom Function)"
    node["m_FunctionName"] = fn
    node["m_FunctionSource"] = HLSL_GUID
    node["m_SourceType"] = 0
    node["m_FunctionBody"] = "Enter function body here..."
    _place(node, x, y, 260.0, 60.0 + 40.0 * len(FUNCTIONS[fn]))
    slots = []
    for slot_id, name, kind, is_output in FUNCTIONS[fn]:
        s = make_slot_v3(donor_v3, slot_id, name, is_output) if kind == "v3" else make_slot(donor_v1, slot_id, name, is_output)
        s["m_ObjectId"] = _oid(f"{seed}/{fn}/slot{slot_id}")
        slots.append(s)
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def _simple_node(donor_docs, type_name, seed, x, y, **fields):
    didx = index(donor_docs)
    donor = next(d for d in donor_docs if d.get("m_Type") == f"UnityEditor.ShaderGraph.{type_name}")
    node = _clone(donor, seed)
    node.update(fields)
    _place(node, x, y, 208.0, 120.0)
    slot = copy.deepcopy(didx[donor["m_Slots"][0]["m_Id"]])
    slot["m_ObjectId"] = _oid(f"{seed}/slot")
    node["m_Slots"] = [{"m_Id": slot["m_ObjectId"]}]
    return node, [slot]


def _property_node(donor_pn, donor_slot, prop_oid, label, seed, x, y):
    node = _clone(donor_pn, seed)
    node["m_Property"] = {"m_Id": prop_oid}
    _place(node, x, y, 200.0, 36.0)
    slot = copy.deepcopy(donor_slot)
    slot["m_ObjectId"] = _oid(f"{seed}/slot")
    slot["m_DisplayName"] = label
    node["m_Slots"] = [{"m_Id": slot["m_ObjectId"]}]
    return node, [slot]


def build(tier):
    docs = load_docs(os.path.join(REPO, SOURCE))
    graph = find_graph(docs)
    idx = index(docs)
    seed = TIERS[tier]["graph"]

    assert find_cf(docs, "SpindleLodVertex") is None and find_cf(docs, "SpindleDepthVertex") is None, \
        "SpindleGraph itself carries a spindle LOD / depth splice - it must not"
    sway = find_cf(docs, "SpindleSway")
    death = find_cf(docs, "PrismDeathClock")
    assert sway is not None and death is not None, "SpindleGraph lost its sway or death clock"

    phase = find_property(docs, "Phase")
    clock = find_property(docs, "PrismClock")
    dull = find_property(docs, "DullColor")
    bright = find_property(docs, "BrightColor")
    assert phase and dull and bright and clock, "SpindleGraph lost Phase / PrismClock / DullColor / BrightColor"
    phase_pn = property_node_for(docs, phase["m_ObjectId"])
    dull_pn = property_node_for(docs, dull["m_ObjectId"])
    donor_v1 = idx[phase_pn["m_Slots"][0]["m_Id"]]
    donor_color_slot = idx[dull_pn["m_Slots"][0]["m_Id"]]
    pos_edge = _feeder(graph, sway["m_ObjectId"], 0)
    assert pos_edge is not None, "SpindleSway.PositionOS is unfed"
    pos_node = idx[pos_edge["m_OutputSlot"]["m_Node"]["m_Id"]]
    donor_v3 = idx[pos_node["m_Slots"][0]["m_Id"]]

    host = next(idx[c["m_Id"]] for c in graph["m_CategoryData"]
                if any(ch["m_Id"] == phase["m_ObjectId"] for ch in idx[c["m_Id"]]["m_ChildObjectList"]))

    new_docs = []

    def add(nodes_and_slots):
        node, slots = nodes_and_slots
        new_docs.append(node)
        new_docs.extend(slots)
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})
        return node

    prop_oids = {}
    for name, reference in props_for(tier):
        p = make_exposed_float(phase, name, reference, 0.0)
        p["m_ObjectId"] = _oid(f"{seed}/prop/{name}")
        p["m_Guid"] = {"m_GuidSerialized": str(uuid.UUID(_oid(f"{seed}/propguid/{name}")))}
        prop_oids[name] = p["m_ObjectId"]
        new_docs.append(p)
        graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
        host["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})

    bx, by = -3300.0, 1900.0
    uv_docs = load_docs(os.path.join(REPO, UV_DONOR))
    uv1 = add(_simple_node(uv_docs, "UVNode", f"{seed}/uv1", bx, by, m_OutputChannel=1))
    uv2 = add(_simple_node(uv_docs, "UVNode", f"{seed}/uv2", bx, by + 140.0, m_OutputChannel=2))
    uv3 = add(_simple_node(uv_docs, "UVNode", f"{seed}/uv3", bx, by + 600.0, m_OutputChannel=3))

    if tier == "depth":
        return _build_depth(docs, graph, idx, seed, add, new_docs, prop_oids, phase_pn, donor_v1, donor_v3,
                            pos_node, pos_edge, sway, death, phase, clock, uv_docs)

    def lod_props(x, y, tag):
        g = add(_property_node(phase_pn, donor_v1, prop_oids["SpindleLodGirth"], "SpindleLodGirth", f"{seed}/{tag}/girth", x, y))
        s = add(_property_node(phase_pn, donor_v1, prop_oids["SpindleLodStrand"], "SpindleLodStrand", f"{seed}/{tag}/strand", x, y + 60.0))
        k = add(_property_node(phase_pn, donor_v1, prop_oids["SpindleLodStrength"], "SpindleLodStrength", f"{seed}/{tag}/strength", x, y + 120.0))
        return g, s, k

    vg, vs, vk = lod_props(bx, by + 300.0, "v")
    normal_docs = load_docs(os.path.join(REPO, NORMAL_DONOR))
    view_docs = load_docs(os.path.join(REPO, VIEW_DONOR))
    nrm_os = add(_simple_node(normal_docs, "NormalVectorNode", f"{seed}/normalOS", bx, by - 300.0, m_Space=SPACE_OBJECT))
    uv0 = add(_simple_node(uv_docs, "UVNode", f"{seed}/uv0", bx, by - 160.0, m_OutputChannel=0))
    vphase = add(_property_node(phase_pn, donor_v1, phase["m_ObjectId"], "Phase", f"{seed}/v/phase", bx, by + 420.0))
    vclock = add(_property_node(phase_pn, donor_v1, clock["m_ObjectId"], "PrismClock", f"{seed}/v/clock", bx, by + 480.0))
    vcf = add(_cf(find_cf(docs, "PrismDeathClock"), donor_v1, donor_v3, "SpindleLodVertex", seed, bx + 420.0, by + 60.0))

    # Position -> SpindleLodVertex -> SpindleSway.PositionOS (the old direct edge is replaced).
    graph["m_Edges"].remove(pos_edge)
    for src, slot in ((pos_node, 0), (nrm_os, 1), (uv0, 2), (uv1, 3), (uv2, 4), (uv3, 5), (vg, 6), (vs, 7),
                      (vk, 8), (vphase, 9), (vclock, 10)):
        graph["m_Edges"].append(edge(src["m_ObjectId"], 0, vcf["m_ObjectId"], slot))
    graph["m_Edges"].append(edge(vcf["m_ObjectId"], 11, sway["m_ObjectId"], 0))

    # Fragment.
    fx, fy = -3300.0, 2600.0
    nrm = add(_simple_node(normal_docs, "NormalVectorNode", f"{seed}/normal", fx, fy, m_Space=SPACE_WORLD))
    view = add(_simple_node(view_docs, "ViewDirectionNode", f"{seed}/view", fx, fy + 140.0, m_Space=SPACE_WORLD))
    dpn = add(_property_node(dull_pn, donor_color_slot, dull["m_ObjectId"], "DullColor", f"{seed}/dull", fx, fy + 300.0))
    bpn = add(_property_node(dull_pn, donor_color_slot, bright["m_ObjectId"], "BrightColor", f"{seed}/bright", fx, fy + 360.0))
    fg, fs, fk = lod_props(fx, fy + 440.0, "f")
    fuv0 = add(_simple_node(uv_docs, "UVNode", f"{seed}/fuv0", fx, fy + 560.0, m_OutputChannel=0))
    fphase = add(_property_node(phase_pn, donor_v1, phase["m_ObjectId"], "Phase", f"{seed}/f/phase", fx, fy + 700.0))
    fclock = add(_property_node(phase_pn, donor_v1, clock["m_ObjectId"], "PrismClock", f"{seed}/f/clock", fx, fy + 760.0))

    base_block = _block(docs, "SurfaceDescription.BaseColor")
    alpha_block = _block(docs, "SurfaceDescription.Alpha")
    base_slot = _block_slot(idx, base_block)
    alpha_slot = _block_slot(idx, alpha_block)
    base_edge = _feeder(graph, base_block["m_ObjectId"], base_slot)
    alpha_edge = _feeder(graph, alpha_block["m_ObjectId"], alpha_slot)
    assert base_edge and alpha_edge, "SpindleGraph's BaseColor / Alpha blocks are unfed"
    graph["m_Edges"].remove(base_edge)
    graph["m_Edges"].remove(alpha_edge)

    if tier == "mid":
        scf = add(_cf(death, donor_v1, donor_v3, "SpindleLodSurface", seed, fx + 460.0, fy))
        feeds = [((base_edge["m_OutputSlot"]["m_Node"]["m_Id"], base_edge["m_OutputSlot"]["m_SlotId"]), 0),
                 ((alpha_edge["m_OutputSlot"]["m_Node"]["m_Id"], alpha_edge["m_OutputSlot"]["m_SlotId"]), 1),
                 ((dpn["m_ObjectId"], 0), 2), ((bpn["m_ObjectId"], 0), 3),
                 ((death["m_ObjectId"], 5), 4), ((nrm["m_ObjectId"], 0), 5), ((view["m_ObjectId"], 0), 6),
                 ((fuv0["m_ObjectId"], 0), 7), ((fg["m_ObjectId"], 0), 8), ((fs["m_ObjectId"], 0), 9),
                 ((fk["m_ObjectId"], 0), 10), ((fphase["m_ObjectId"], 0), 11), ((fclock["m_ObjectId"], 0), 12)]
        color_out, alpha_out = 13, 14
    else:
        scf = add(_cf(death, donor_v1, donor_v3, "SpindleLodThread", seed, fx + 460.0, fy))
        feeds = [((dpn["m_ObjectId"], 0), 0), ((bpn["m_ObjectId"], 0), 1), ((death["m_ObjectId"], 5), 2),
                 ((nrm["m_ObjectId"], 0), 3), ((view["m_ObjectId"], 0), 4), ((fuv0["m_ObjectId"], 0), 5),
                 ((fg["m_ObjectId"], 0), 6), ((fs["m_ObjectId"], 0), 7), ((fk["m_ObjectId"], 0), 8),
                 ((fphase["m_ObjectId"], 0), 9), ((fclock["m_ObjectId"], 0), 10)]
        color_out, alpha_out = 11, 12
    for (src, src_slot), dst_slot in feeds:
        graph["m_Edges"].append(edge(src, src_slot, scf["m_ObjectId"], dst_slot))
    graph["m_Edges"].append(edge(scf["m_ObjectId"], color_out, base_block["m_ObjectId"], base_slot))
    graph["m_Edges"].append(edge(scf["m_ObjectId"], alpha_out, alpha_block["m_ObjectId"], alpha_slot))

    docs.extend(new_docs)
    return docs


def _build_depth(docs, graph, idx, seed, add, new_docs, prop_oids, phase_pn, donor_v1, donor_v3,
                 pos_node, pos_edge, sway, death, phase, clock, uv_docs):
    """SpindleGraph + SpindleDepthVertex before the sway + SpindleDepthGlow on the lace colour."""
    bx, by = -3300.0, 1900.0

    def depth_props(x, y, tag):
        return [add(_property_node(phase_pn, donor_v1, prop_oids[name], name, f"{seed}/{tag}/{name}", x, y + 60.0 * k))
                for k, (name, _ref) in enumerate(DEPTH_PROPS)]

    normal_docs = load_docs(os.path.join(REPO, NORMAL_DONOR))
    nrm_os = add(_simple_node(normal_docs, "NormalVectorNode", f"{seed}/normalOS", bx, by - 300.0, m_Space=SPACE_OBJECT))
    uv0 = add(_simple_node(uv_docs, "UVNode", f"{seed}/uv0", bx, by - 160.0, m_OutputChannel=0))
    vk, vreach, vform = depth_props(bx, by + 300.0, "v")
    vphase = add(_property_node(phase_pn, donor_v1, phase["m_ObjectId"], "Phase", f"{seed}/v/phase", bx, by + 420.0))
    vclock = add(_property_node(phase_pn, donor_v1, clock["m_ObjectId"], "PrismClock", f"{seed}/v/clock", bx, by + 480.0))
    vcf = add(_cf(death, donor_v1, donor_v3, "SpindleDepthVertex", seed, bx + 420.0, by + 60.0))

    # Position -> SpindleDepthVertex -> SpindleSway.PositionOS (the old direct edge is replaced).
    graph["m_Edges"].remove(pos_edge)
    for src, slot in ((pos_node, 0), (nrm_os, 1), (uv0, 2), (vreach, 3), (vform, 4), (vk, 5),
                      (vphase, 6), (vclock, 7)):
        graph["m_Edges"].append(edge(src["m_ObjectId"], 0, vcf["m_ObjectId"], slot))
    graph["m_Edges"].append(edge(vcf["m_ObjectId"], 8, sway["m_ObjectId"], 0))

    # Fragment: the lace colour and alpha -> SpindleDepthGlow -> BaseColor / Alpha.
    fx, fy = -3300.0, 2600.0
    pos_ws = add(_simple_node(docs, "PositionNode", f"{seed}/positionWS", fx, fy, m_Space=SPACE_WORLD))
    fuv0 = add(_simple_node(uv_docs, "UVNode", f"{seed}/fuv0", fx, fy + 140.0, m_OutputChannel=0))
    fk, freach, fform = depth_props(fx, fy + 300.0, "f")
    fphase = add(_property_node(phase_pn, donor_v1, phase["m_ObjectId"], "Phase", f"{seed}/f/phase", fx, fy + 500.0))
    fclock = add(_property_node(phase_pn, donor_v1, clock["m_ObjectId"], "PrismClock", f"{seed}/f/clock", fx, fy + 560.0))
    base_block = _block(docs, "SurfaceDescription.BaseColor")
    alpha_block = _block(docs, "SurfaceDescription.Alpha")
    base_slot = _block_slot(idx, base_block)
    alpha_slot = _block_slot(idx, alpha_block)
    base_edge = _feeder(graph, base_block["m_ObjectId"], base_slot)
    alpha_edge = _feeder(graph, alpha_block["m_ObjectId"], alpha_slot)
    assert base_edge and alpha_edge, "SpindleGraph's BaseColor / Alpha blocks are unfed"
    graph["m_Edges"].remove(base_edge)
    graph["m_Edges"].remove(alpha_edge)
    gcf = add(_cf(death, donor_v1, donor_v3, "SpindleDepthGlow", seed, fx + 460.0, fy))
    feeds = [((base_edge["m_OutputSlot"]["m_Node"]["m_Id"], base_edge["m_OutputSlot"]["m_SlotId"]), 0),
             ((alpha_edge["m_OutputSlot"]["m_Node"]["m_Id"], alpha_edge["m_OutputSlot"]["m_SlotId"]), 1),
             ((pos_ws["m_ObjectId"], 0), 2), ((fuv0["m_ObjectId"], 0), 3), ((freach["m_ObjectId"], 0), 4),
             ((fform["m_ObjectId"], 0), 5), ((fk["m_ObjectId"], 0), 6), ((fphase["m_ObjectId"], 0), 7),
             ((fclock["m_ObjectId"], 0), 8)]
    for (src, src_slot), dst_slot in feeds:
        graph["m_Edges"].append(edge(src, src_slot, gcf["m_ObjectId"], dst_slot))
    graph["m_Edges"].append(edge(gcf["m_ObjectId"], 9, base_block["m_ObjectId"], base_slot))
    graph["m_Edges"].append(edge(gcf["m_ObjectId"], 10, alpha_block["m_ObjectId"], alpha_slot))

    docs.extend(new_docs)
    return docs


def validate(docs, tier):
    errs = []
    idx = index(docs)
    graph = find_graph(docs)
    ids = [d["m_ObjectId"] for d in docs if "m_ObjectId" in d]
    if len(ids) != len(set(ids)):
        errs.append("duplicate m_ObjectId")
    for ref in graph["m_Nodes"] + graph["m_Properties"]:
        if ref["m_Id"] not in idx:
            errs.append(f"dangling reference {ref['m_Id']}")
    slot_ids = {}
    for ref in graph["m_Nodes"]:
        node = idx.get(ref["m_Id"])
        if node is None:
            continue
        slot_ids[ref["m_Id"]] = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in node.get("m_Slots", []) if s["m_Id"] in idx}
    feeders = {}
    for e in graph["m_Edges"]:
        o, i = e["m_OutputSlot"], e["m_InputSlot"]
        on, inn = o["m_Node"]["m_Id"], i["m_Node"]["m_Id"]
        if on not in slot_ids or o["m_SlotId"] not in slot_ids[on]:
            errs.append(f"edge from a missing slot {on}:{o['m_SlotId']}")
            continue
        if inn not in slot_ids or i["m_SlotId"] not in slot_ids[inn]:
            errs.append(f"edge into a missing slot {inn}:{i['m_SlotId']}")
            continue
        if slot_ids[on][o["m_SlotId"]]["m_SlotType"] != 1 or slot_ids[inn][i["m_SlotId"]]["m_SlotType"] != 0:
            errs.append("edge direction wrong")
        feeders[(inn, i["m_SlotId"])] = feeders.get((inn, i["m_SlotId"]), 0) + 1
    if any(c != 1 for c in feeders.values()):
        errs.append("an input slot has more than one feeder")
    if not acyclic(graph):
        errs.append("edge cycle (the graph would import magenta)")

    fns = [vertex_fn(tier), fragment_fn(tier)]
    for fn in fns:
        cf = find_cf(docs, fn)
        if cf is None:
            errs.append(f"{fn} missing")
            continue
        if cf["m_FunctionSource"] != HLSL_GUID:
            errs.append(f"{fn} points at the wrong HLSL")
        got = sorted((idx[s["m_Id"]]["m_Id"], idx[s["m_Id"]]["m_DisplayName"], idx[s["m_Id"]]["m_SlotType"]) for s in cf["m_Slots"])
        want = sorted((sid, name, 1 if out else 0) for sid, name, _k, out in FUNCTIONS[fn])
        if got != want:
            errs.append(f"{fn} slots do not match the HLSL signature")
        for sid, name, _k, out in FUNCTIONS[fn]:
            if not out and feeders.get((cf["m_ObjectId"], sid)) != 1:
                errs.append(f"{fn}.{name} is unfed")
    # The blocks read the LOD (or depth) function, and SpindleSway reads the converged position.
    blocks = (("SurfaceDescription.BaseColor", "Color"), ("SurfaceDescription.Alpha", "Alpha"))
    for block_name, out_name in blocks:
        block = find_node(docs, block_name, "BlockNode")
        e = _feeder(graph, block["m_ObjectId"], _block_slot(idx, block))
        src = idx.get(e["m_OutputSlot"]["m_Node"]["m_Id"]) if e else None
        if not src or src.get("m_FunctionName") != fns[1]:
            errs.append(f"{block_name} is not fed by {fns[1]}.{out_name}")
    sway = find_cf(docs, "SpindleSway")
    e = _feeder(graph, sway["m_ObjectId"], 0)
    if not e or idx[e["m_OutputSlot"]["m_Node"]["m_Id"]].get("m_FunctionName") != fns[0]:
        errs.append(f"SpindleSway.PositionOS is not fed by {fns[0]}")
    for _name, reference in props_for(tier):
        p = find_property(docs, reference)
        if p is None or p["m_DefaultReferenceName"] != reference or p["m_Value"] != 0.0 or p["overrideHLSLDeclaration"]:
            errs.append(f"{reference} must be an exposed UnityPerMaterial float defaulting to 0")
    return errs


def hlsl_errors():
    text = open(os.path.join(REPO, HLSL)).read()
    errs = []
    for fn, slots in FUNCTIONS.items():
        m = re.search(rf"void {fn}_float\(([^)]*)\)", text)
        if not m:
            errs.append(f"{HLSL}: {fn}_float missing")
            continue
        params = [p.split()[-1] for p in m.group(1).replace("\n", " ").split(",")]
        want = [name for _sid, name, _k, _o in slots]
        if params != want:
            errs.append(f"{HLSL}: {fn}_float parameters {params} != graph slots {want}")
    # The .meta (and so the guid every LOD graph's Custom Function points at) is one of this
    # script's own outputs, so --check's drift test is what holds it.
    return errs


GRAPH_META = """fileFormatVersion: 2
guid: {guid}
ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 2
  userData:
  assetBundleName:
  assetBundleVariant:
  script: {{fileID: 11500000, guid: 625f186215c104763be7675aa2d941aa, type: 3}}
"""

HLSL_META = """fileFormatVersion: 2
guid: {guid}
ShaderIncludeImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

MATERIAL_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def material_text(tier):
    text = open(os.path.join(REPO, SOURCE_MATERIAL)).read()
    name = TIERS[tier]["material"]
    text = text.replace("m_Name: SpindleMaterial", f"m_Name: {name}", 1)
    text = re.sub(r"m_Shader: \{fileID: -6465566751694194690, guid: 545a7b2ee58b90744bdb1c9e265a09f4,\n    type: 3\}",
                  f"m_Shader: {{fileID: {SHADERGRAPH_SHADER_FILE_ID}, guid: {TIERS[tier]['graph_guid']},\n    type: 3}}",
                  text, count=1)
    assert TIERS[tier]["graph_guid"] in text, "SpindleMaterial's shader line changed shape"
    # Add the two LOD floats in Unity's sorted position (m_Floats is alphabetical).
    floats = re.search(r"    m_Floats:\n((?:    - .*\n)+)", text)
    rows = floats.group(1).splitlines(keepends=True)
    for _n, reference in props_for(tier):
        rows.append(f"    - {reference}: 0\n")
    rows.sort(key=lambda r: r.strip()[2:].split(":")[0])
    text = text[:floats.start(1)] + "".join(rows) + text[floats.end(1):]
    assert "m_EnableInstancingVariants: 1" in text, "the far template must enable instancing variants"
    if tier == "depth":
        # The creatures and rings draw through their own renderers, never instanced.
        text = text.replace("m_EnableInstancingVariants: 1", "m_EnableInstancingVariants: 0", 1)
    return text


def outputs():
    files = {HLSL + ".meta": HLSL_META.format(guid=HLSL_GUID),
             INCORPOREAL_HLSL + ".meta": HLSL_META.format(guid=INCORPOREAL_GUID)}
    errs = hlsl_errors()
    for tier, t in TIERS.items():
        docs = build(tier)
        errs += [f"{t['graph']}: {e}" for e in validate(docs, tier)]
        files[f"{GRAPH_DIR}/{t['graph']}.shadergraph"] = dump_docs(docs)
        files[f"{GRAPH_DIR}/{t['graph']}.shadergraph.meta"] = GRAPH_META.format(guid=t["graph_guid"])
        files[f"{MATERIAL_DIR}/{t['material']}.mat"] = material_text(tier)
        files[f"{MATERIAL_DIR}/{t['material']}.mat.meta"] = MATERIAL_META.format(guid=t["material_guid"])
    return files, errs


def self_test():
    failures = 0
    for tier in TIERS:
        docs = build(tier)
        assert not validate(docs, tier), f"{tier}: the shipped construction must validate"
        graph = find_graph(docs)
        # 1. dropping the edge into SpindleSway must be caught
        broken = copy.deepcopy(docs)
        g = find_graph(broken)
        sway = find_cf(broken, "SpindleSway")
        g["m_Edges"] = [e for e in g["m_Edges"] if not (e["m_InputSlot"]["m_Node"]["m_Id"] == sway["m_ObjectId"] and e["m_InputSlot"]["m_SlotId"] == 0)]
        failures += not any("SpindleSway" in e for e in validate(broken, tier))
        # 2. a doubled feeder must be caught
        broken = copy.deepcopy(docs)
        g = find_graph(broken)
        g["m_Edges"].append(copy.deepcopy(g["m_Edges"][-1]))
        failures += not any("more than one feeder" in e for e in validate(broken, tier))
        # 3. a slot renamed away from the HLSL signature must be caught
        broken = copy.deepcopy(docs)
        bidx = index(broken)
        cf = find_cf(broken, vertex_fn(tier))
        bidx[cf["m_Slots"][1]["m_Id"]]["m_DisplayName"] = "Wrong"
        failures += not any("signature" in e for e in validate(broken, tier))
        del graph
    print("self-test:", f"OK ({3 * len(TIERS)} negative controls fire)" if failures == 0 else f"FAILED ({failures})")
    return 1 if failures else 0


def main():
    if "--self-test" in sys.argv:
        return self_test()
    check = "--check" in sys.argv
    files, errs = outputs()
    if errs:
        for e in errs:
            print("ERROR:", e)
        return 1
    drift = []
    for rel, text in files.items():
        path = os.path.join(REPO, rel)
        on_disk = open(path, encoding="utf-8").read() if os.path.exists(path) else None
        if on_disk != text:
            drift.append(rel)
            if not check:
                with open(path, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(text)
    if check:
        if drift:
            print("DRIFT (re-run Tools/Shaders/wire_spindle_lod.py):", ", ".join(drift))
            return 1
        print("spindle LOD graphs and templates: OK (in step with SpindleGraph)")
        return 0
    print("wrote:" if drift else "no change:", ", ".join(drift) if drift else "all files current")
    return 0


if __name__ == "__main__":
    sys.exit(main())
