#!/usr/bin/env python3
"""
Wire the body-anchored EROSION dither (one wipe per face) into ExplodingBlockGraph.

The exploding prism's fade-out must not be a function of the view: a screen- or
world-anchored dither crawls across flying, tumbling debris. PrismErosionFade (in
PrismOcclusionCorridor.hlsl) sweeps ONE jagged erosion front across each face of the
debris cube, anchored to UV0 — a mesh attribute no vertex animation (flight, spin,
scale) can move, so the front is glued to the face under any motion and any camera.
(The previous body-position anchoring broke under the per-face shatter spin: fragments
migrated across dominant-axis boundaries as pieces rotated and the wipe jumped frames.)

The splice sits BETWEEN the explosion clock and the occlusion corridor:

  BEFORE:  PrismExplosionClock.Opacity ------------------------> PrismOcclusionFade.BaseAlpha
  AFTER:   PrismExplosionClock.Opacity -> EROSION.BaseOpacity
           UV (channel 0) --------------> EROSION.UV
           Tangent Vector (object) -----> EROSION.Tangent       (per-PIECE wipe identity)
           Prop[Velocity] --------------> EROSION.Velocity      (per-PRISM wipe identity)
           EROSION.Survival (0..1) -----> PrismOcclusionFade.BaseAlpha

THE TANGENT IS THE PIECE (2026-09-16). UV0 says WHERE ON a face a fragment sits and
nothing about WHICH face: every one of the shipped debris cube's 24 wedges carries the
bit-identical UV triangle, so one wipe was serving the whole prism in its PRE-explosion
body frame and the pieces all peeled in lockstep after they had hinged apart. The
object-space tangent IS each wedge's hinge axis (RotateFacesAlongAxis rotates about it),
is distinct per piece on every debris mesh the game ships, and — unlike the normal — is
NOT written by this graph's vertex stage, so it cannot crawl. That last property is the
one a future edit could silently take away, so it is ASSERTED here: an edge into
VertexDescription.Tangent turns the block from an identity pass-through of the mesh
attribute into an animated value, and the wipe would start crawling with nothing else
in the graph changing.

So the erosion owns the FADE (angle-free) while the corridor keeps owning OCCLUSION (a
view effect by definition); Survival is fractional only in the narrow fringe leading
the front, which the corridor stage renders as screen-door speckle (soft-hard-soft).
Live prisms on this graph are exact pass-throughs: with no explosion stamped the clock
hands _Opacity through, and the erosion's >=1 / <=0 early-outs return 1 or 0 untouched.

MIGRATES the earlier body-position wiring in place: an erosion node with the old
7-slot signature is stripped (node, slots, its Object-space Position feeder, edges)
with the clock->corridor edge restored, then the graph is wired fresh — so one run
upgrades any prior state, and re-running is a validated no-op.

Out-of-editor ShaderGraph JSON synthesis per /asset-surgery: parse everything, clone
same-file donors (the UV node clones from ExplosionGraph, which this repo ships),
rebuild in memory, assert every invariant, only then write.
Usage: [--check] validates without writing (exit 1 if not wired to the CURRENT shape).
"""

import json
import os
import sys
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPH = "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph"
UV_DONOR = "Assets/_Graphics/Materials/Graphs/ExplosionGraph.shadergraph"
TANGENT_DONOR = "Assets/_Graphics/Materials/Graphs/PrismGraphs/Subgraphs/RotateFacesAlongAxis.shadersubgraph"

# GUID of PrismOcclusionCorridor.hlsl (pinned by its committed .meta).
HLSL_GUID = "bf8e2c1fa76142c89ba03b2e1ae46201"
FUNCTION_NAME = "PrismErosionFade"
CORRIDOR_FUNCTION = "PrismOcclusionFade"
CORRIDOR_BASEALPHA_SLOT = 3
CLOCK_FUNCTION = "PrismExplosionClock"

# (integer slot id, display name, "Vector1"|"Vector3", is_output) — ids MUST match the
# HLSL parameter order. UV is declared float3 in the HLSL so the UV node's Vector4
# output truncates onto it without an adapter node; the function reads .xy.
CF_SLOTS = [
    (0, "UV", "Vector3", False),
    (1, "Tangent", "Vector3", False),
    (2, "Velocity", "Vector3", False),
    (3, "BaseOpacity", "Vector1", False),
    (4, "Survival", "Vector1", True),
]
# Slot ids of the inputs whose feeder nodes exist ONLY to serve this function, so a
# migration from an older signature must take them with it (see strip_old_erosion).
EROSION_BASEOPACITY_SLOT = 3
EROSION_SURVIVAL_SLOT = 4


def load_docs(path):
    text = open(path, encoding="utf-8").read()
    decoder = json.JSONDecoder()
    docs, i, n = [], 0, len(text)
    while i < n:
        while i < n and text[i] in " \t\r\n":
            i += 1
        if i >= n:
            break
        obj, i = decoder.raw_decode(text, i)
        docs.append(obj)
    return docs


def dump_docs(docs):
    return "\n\n".join(json.dumps(d, indent=4) for d in docs) + "\n"


def find_graph(docs):
    return next(d for d in docs if "GraphData" in d.get("m_Type", ""))


def index(docs):
    return {d["m_ObjectId"]: d for d in docs if "m_ObjectId" in d}


def cf_by_function(idx, graph, fn):
    for r in graph["m_Nodes"]:
        node = idx[r["m_Id"]]
        if node.get("m_FunctionName") == fn:
            return node
    return None


def node_output_slot(idx, node, display_name=None):
    outs = []
    for s in node.get("m_Slots", []):
        sd = idx[s["m_Id"]]
        if sd.get("m_SlotType") == 1 and (display_name is None or sd.get("m_DisplayName") == display_name):
            outs.append(sd["m_Id"])
    assert len(outs) == 1, f"expected one output slot ({display_name}) on {node.get('m_Name')}, found {len(outs)}"
    return outs[0]


def property_node(idx, graph, prop_name):
    for r in graph["m_Nodes"]:
        node = idx[r["m_Id"]]
        if node.get("m_Type", "").endswith("PropertyNode"):
            prop = idx.get(node["m_Property"]["m_Id"])
            if prop is not None and prop.get("m_Name") == prop_name:
                return node
    return None


def make_slot(donor, slot_id, display_name, is_output):
    s = json.loads(json.dumps(donor))
    s["m_ObjectId"] = uuid.uuid4().hex
    s["m_Id"] = slot_id
    s["m_DisplayName"] = display_name
    s["m_ShaderOutputName"] = display_name
    s["m_SlotType"] = 1 if is_output else 0
    s["m_StageCapability"] = 3
    if isinstance(s.get("m_Value"), dict):
        s["m_Value"] = {"x": 0.0, "y": 0.0, "z": 0.0}
        s["m_DefaultValue"] = {"x": 0.0, "y": 0.0, "z": 0.0}
    else:
        s["m_Value"] = 0.0
        s["m_DefaultValue"] = 0.0
    return s


def edge(out_node, out_slot, in_node, in_slot):
    return {
        "m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
        "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot},
    }


def validate(docs, expect_wired):
    idx = index(docs)
    graph = find_graph(docs)

    ids = [d["m_ObjectId"] for d in docs if "m_ObjectId" in d]
    assert len(ids) == len(set(ids)), "duplicate m_ObjectId"

    slot_ids = {}
    for ref in graph["m_Nodes"]:
        assert ref["m_Id"] in idx, f"m_Nodes references missing {ref['m_Id']}"
        node = idx[ref["m_Id"]]
        ints = set()
        for s in node.get("m_Slots", []):
            assert s["m_Id"] in idx, f"node {ref['m_Id']} slot {s['m_Id']} missing"
            sd = idx[s["m_Id"]]
            assert sd["m_Id"] not in ints, f"duplicate integer slot id on node {ref['m_Id']}"
            ints.add(sd["m_Id"])
        slot_ids[ref["m_Id"]] = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in node.get("m_Slots", [])}

    feeders = {}
    for e in graph["m_Edges"]:
        o, i = e["m_OutputSlot"], e["m_InputSlot"]
        for end, label in ((o, "output"), (i, "input")):
            nid = end["m_Node"]["m_Id"]
            assert nid in slot_ids, f"edge {label} node {nid} not registered"
            assert end["m_SlotId"] in slot_ids[nid], f"edge {label} slot {end['m_SlotId']} missing on {nid}"
        assert slot_ids[o["m_Node"]["m_Id"]][o["m_SlotId"]]["m_SlotType"] == 1, "edge output not an output slot"
        assert slot_ids[i["m_Node"]["m_Id"]][i["m_SlotId"]]["m_SlotType"] == 0, "edge input not an input slot"
        key = (i["m_Node"]["m_Id"], i["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
    for key, count in feeders.items():
        assert count == 1, f"input slot {key} has {count} feeders (must be exactly 1)"

    if not expect_wired:
        return

    erosion = cf_by_function(idx, graph, FUNCTION_NAME)
    assert erosion is not None, "PrismErosionFade custom function node missing"
    assert erosion["m_FunctionSource"] == HLSL_GUID, "erosion points at the wrong HLSL asset"
    er_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in erosion["m_Slots"]}
    assert set(er_slots) == {s[0] for s in CF_SLOTS}, \
        "erosion slot ids do not match the CURRENT HLSL signature (old wiring? re-run without --check to migrate)"
    for slot_id, name, _kind, is_output in CF_SLOTS:
        assert er_slots[slot_id]["m_DisplayName"] == name, f"erosion slot {slot_id} name drifted"
        assert er_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"erosion slot {slot_id} direction wrong"

    sources = {}
    for e in graph["m_Edges"]:
        sources[(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])] = \
            (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])

    corridor = cf_by_function(idx, graph, CORRIDOR_FUNCTION)
    clock = cf_by_function(idx, graph, CLOCK_FUNCTION)
    assert corridor is not None and clock is not None, "corridor / explosion clock node missing"

    src = sources.get((erosion["m_ObjectId"], EROSION_BASEOPACITY_SLOT))
    assert src is not None, "erosion BaseOpacity unconnected"
    assert src[0] == clock["m_ObjectId"] and src[1] == node_output_slot(idx, clock, "Opacity"), \
        "erosion BaseOpacity is not fed by the clock's Opacity output"

    vel = property_node(idx, graph, "Velocity")
    assert vel is not None, "Velocity property node missing"
    assert sources.get((erosion["m_ObjectId"], 2)) == (vel["m_ObjectId"], node_output_slot(idx, vel)), \
        "erosion Velocity is not fed by Prop[Velocity]"

    uv_src = sources.get((erosion["m_ObjectId"], 0))
    assert uv_src is not None, "erosion UV unconnected"
    uv_node = idx[uv_src[0]]
    assert uv_node.get("m_Type", "").endswith("UVNode"), "erosion UV is not fed by a UV node"
    assert uv_node.get("m_OutputChannel", 0) == 0, \
        "erosion UV must read channel 0 — channel 1 is the face-centroid feed"

    tan_src = sources.get((erosion["m_ObjectId"], 1))
    assert tan_src is not None, "erosion Tangent unconnected"
    tan_node = idx[tan_src[0]]
    assert tan_node.get("m_Type", "").endswith("TangentVectorNode"), \
        "erosion Tangent is not fed by a Tangent Vector node"
    assert tan_node.get("m_Space") == 0, \
        "erosion Tangent must be OBJECT space — a world-space tangent turns with the prism"

    # THE GUARD (see the module docstring). The tangent is a stable per-PIECE identity
    # only while this graph leaves VertexDescription.Tangent unconnected, which makes the
    # block an identity pass-through of the mesh attribute. Wire it and the fragment's
    # tangent becomes animated, the hashed wipe direction becomes a function of time, and
    # the erosion crawls — with nothing else in the graph looking different.
    for ref in graph["m_Nodes"]:
        block = idx[ref["m_Id"]]
        if not block.get("m_Type", "").endswith("BlockNode"):
            continue
        if block.get("m_Name") != "VertexDescription.Tangent":
            continue
        for bs in block.get("m_Slots", []):
            assert (block["m_ObjectId"], idx[bs["m_Id"]]["m_Id"]) not in sources, \
                "VertexDescription.Tangent is wired — that animates the erosion's per-piece " \
                "anchor and the wipe will crawl; use a different identity or re-anchor"

    assert sources.get((corridor["m_ObjectId"], CORRIDOR_BASEALPHA_SLOT)) == \
        (erosion["m_ObjectId"], EROSION_SURVIVAL_SLOT), \
        "corridor BaseAlpha is not fed by erosion Survival"


def strip_old_erosion(docs, graph, idx):
    """Remove a previous-signature erosion node, its slots, every feeder node that
    existed ONLY to serve it, and every edge touching them; restore the clock ->
    corridor BaseAlpha edge. Returns the docs list (filtered)."""
    erosion = cf_by_function(idx, graph, FUNCTION_NAME)
    if erosion is None:
        return docs, False
    er_slots = {idx[s["m_Id"]]["m_Id"] for s in erosion["m_Slots"]}
    if er_slots == {s[0] for s in CF_SLOTS}:
        return docs, False  # already the current shape

    doomed_nodes = {erosion["m_ObjectId"]}
    # Feeders that served the erosion and NOTHING else are orphaned with it — the old
    # Object-space Position node (the pre-2026-08-11 anchoring) and, migrating from the
    # 4-slot shape, that shape's own UV node. The test is "every outgoing edge of this
    # node lands on the erosion", never the node's TYPE: the graph's other UV node
    # (channel 1) feeds the face-centroid consumers and must survive, and guessing by
    # type is exactly how a shared feeder gets deleted.
    consumers = {}
    for e in graph["m_Edges"]:
        consumers.setdefault(e["m_OutputSlot"]["m_Node"]["m_Id"], set()).add(
            e["m_InputSlot"]["m_Node"]["m_Id"])
    for e in graph["m_Edges"]:
        if e["m_InputSlot"]["m_Node"]["m_Id"] != erosion["m_ObjectId"]:
            continue
        fid = e["m_OutputSlot"]["m_Node"]["m_Id"]
        feeder = idx.get(fid)
        if feeder is None or feeder.get("m_Type", "").endswith("PropertyNode"):
            continue  # a property node is shared by construction
        if consumers.get(fid) == {erosion["m_ObjectId"]}:
            doomed_nodes.add(fid)

    doomed_docs = set(doomed_nodes)
    for nid in doomed_nodes:
        for s in idx[nid].get("m_Slots", []):
            doomed_docs.add(s["m_Id"])

    corridor = cf_by_function(idx, graph, CORRIDOR_FUNCTION)
    clock = cf_by_function(idx, graph, CLOCK_FUNCTION)

    graph["m_Edges"] = [e for e in graph["m_Edges"]
                        if e["m_InputSlot"]["m_Node"]["m_Id"] not in doomed_nodes
                        and e["m_OutputSlot"]["m_Node"]["m_Id"] not in doomed_nodes]
    graph["m_Nodes"] = [r for r in graph["m_Nodes"] if r["m_Id"] not in doomed_nodes]
    docs = [d for d in docs if d.get("m_ObjectId") not in doomed_docs]

    # Restore the pre-erosion feed so the fresh-wire path sees the canonical shape.
    graph["m_Edges"].append(edge(clock["m_ObjectId"], node_output_slot(index(docs), clock, "Opacity"),
                                 corridor["m_ObjectId"], CORRIDOR_BASEALPHA_SLOT))
    return docs, True


def main():
    check_only = "--check" in sys.argv
    path = os.path.join(REPO, GRAPH)
    docs = load_docs(path)
    graph = find_graph(docs)
    idx = index(docs)

    existing = cf_by_function(idx, graph, FUNCTION_NAME)
    if existing is not None and \
            {idx[s["m_Id"]]["m_Id"] for s in existing["m_Slots"]} == {s[0] for s in CF_SLOTS}:
        validate(docs, expect_wired=True)
        print(f"  {os.path.basename(GRAPH)}: already wired (validated).")
        return 0
    if check_only:
        print(f"  {os.path.basename(GRAPH)}: NOT wired to the current shape.", file=sys.stderr)
        return 1

    docs, migrated = strip_old_erosion(docs, graph, idx)
    idx = index(docs)
    validate(docs, expect_wired=False)

    # ---- donors ----
    corridor = cf_by_function(idx, graph, CORRIDOR_FUNCTION)
    clock = cf_by_function(idx, graph, CLOCK_FUNCTION)
    assert corridor is not None, "corridor node missing — run wire_prism_occlusion_corridor.py first"
    assert clock is not None, "explosion clock node missing"
    donor_slot_v3 = next(idx[s["m_Id"]] for s in corridor["m_Slots"]
                         if "Vector3MaterialSlot" in idx[s["m_Id"]]["m_Type"])
    donor_slot_v1 = next(idx[s["m_Id"]] for s in corridor["m_Slots"]
                         if "Vector1MaterialSlot" in idx[s["m_Id"]]["m_Type"])

    uv_docs = load_docs(os.path.join(REPO, UV_DONOR))
    uv_idx = index(uv_docs)
    donor_uv = next(d for d in uv_docs if d.get("m_Type", "").endswith("UVNode"))
    assert donor_uv.get("m_OutputChannel", 0) == 0, "UV donor is not channel 0"
    donor_uv_slot = uv_idx[donor_uv["m_Slots"][0]["m_Id"]]

    tan_docs = load_docs(os.path.join(REPO, TANGENT_DONOR))
    tan_idx = index(tan_docs)
    donor_tan = next(d for d in tan_docs
                     if d.get("m_Type", "").endswith("TangentVectorNode") and d.get("m_Space") == 0)
    donor_tan_slot = tan_idx[donor_tan["m_Slots"][0]["m_Id"]]

    new_docs = []

    uv_node = json.loads(json.dumps(donor_uv))
    uv_node["m_ObjectId"] = uuid.uuid4().hex
    uv_node["m_DrawState"]["m_Position"].update({"x": -1500.0, "y": 2150.0})
    uv_slot = json.loads(json.dumps(donor_uv_slot))
    uv_slot["m_ObjectId"] = uuid.uuid4().hex
    uv_node["m_Slots"] = [{"m_Id": uv_slot["m_ObjectId"]}]
    new_docs += [uv_node, uv_slot]

    tan_node = json.loads(json.dumps(donor_tan))
    tan_node["m_ObjectId"] = uuid.uuid4().hex
    tan_node["m_Group"] = {"m_Id": ""}
    tan_node["m_DrawState"]["m_Position"].update({"x": -1500.0, "y": 2500.0})
    tan_slot = json.loads(json.dumps(donor_tan_slot))
    tan_slot["m_ObjectId"] = uuid.uuid4().hex
    tan_node["m_Slots"] = [{"m_Id": tan_slot["m_ObjectId"]}]
    new_docs += [tan_node, tan_slot]

    er_node = json.loads(json.dumps(corridor))
    er_node["m_ObjectId"] = uuid.uuid4().hex
    er_node["m_Name"] = f"{FUNCTION_NAME} (Custom Function)"
    er_node["m_FunctionName"] = FUNCTION_NAME
    er_node["m_FunctionSource"] = HLSL_GUID
    er_node["m_SourceType"] = 0
    er_node["m_DrawState"]["m_Position"].update({"x": -1180.0, "y": 2100.0, "width": 232.0, "height": 300.0})
    er_slots = []
    for slot_id, name, kind, is_output in CF_SLOTS:
        donor = donor_slot_v3 if kind == "Vector3" else donor_slot_v1
        er_slots.append(make_slot(donor, slot_id, name, is_output))
    er_node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in er_slots]
    new_docs += [er_node] + er_slots

    for node in (uv_node, tan_node, er_node):
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    # ---- edges: retarget clock -> corridor.BaseAlpha into the erosion ----
    retargeted = 0
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == corridor["m_ObjectId"] and i["m_SlotId"] == CORRIDOR_BASEALPHA_SLOT:
            assert e["m_OutputSlot"]["m_Node"]["m_Id"] == clock["m_ObjectId"], \
                "corridor BaseAlpha is not fed by the explosion clock — graph shape drifted, refusing"
            e["m_InputSlot"] = {"m_Node": {"m_Id": er_node["m_ObjectId"]},
                                "m_SlotId": EROSION_BASEOPACITY_SLOT}
            retargeted += 1
    assert retargeted == 1, f"expected exactly one BaseAlpha feeder, retargeted {retargeted}"

    vel = property_node(idx, graph, "Velocity")
    assert vel is not None, "no Velocity property node in the graph"
    graph["m_Edges"] += [
        edge(uv_node["m_ObjectId"], uv_slot["m_Id"], er_node["m_ObjectId"], 0),
        edge(tan_node["m_ObjectId"], tan_slot["m_Id"], er_node["m_ObjectId"], 1),
        edge(vel["m_ObjectId"], node_output_slot(idx, vel), er_node["m_ObjectId"], 2),
        edge(er_node["m_ObjectId"], EROSION_SURVIVAL_SLOT,
             corridor["m_ObjectId"], CORRIDOR_BASEALPHA_SLOT),
    ]

    docs += new_docs
    validate(docs, expect_wired=True)   # nothing written yet

    open(path, "w", encoding="utf-8").write(dump_docs(docs))
    validate(load_docs(path), expect_wired=True)
    print(f"  {os.path.basename(GRAPH)}: wired and validated "
          f"({'migrated from an older signature, ' if migrated else ''}"
          f"+3 nodes, +{len(new_docs) - 3} slots, 4 new edges, 1 retargeted).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
