#!/usr/bin/env python3
"""
Wire the object-anchored EROSION dither into ExplodingBlockGraph.

The exploding prism's fade-out must not be a function of the view: a screen- or
world-anchored dither crawls across flying, tumbling debris, so the dissolve reads as
an image effect instead of the prism breaking up. PrismErosionFade (in
PrismOcclusionCorridor.hlsl) carves the prism into object-space Voronoi chunks that
shrink toward their own centres and vanish, each at its own hashed phase — anchored to
the debris body (the flight translation is undone inside the function), so no camera
angle and no motion can slide it.

The splice sits BETWEEN the explosion clock and the occlusion corridor:

  BEFORE:  PrismExplosionClock.Opacity ------------------------> PrismOcclusionFade.BaseAlpha
  AFTER:   PrismExplosionClock.Opacity -> EROSION.BaseOpacity
           Position(Object) ------------> EROSION.PositionOS
           Prop[Velocity] --------------> EROSION.Velocity      (flight undo + per-prism entropy)
           Prop[PrismClock] ------------> EROSION.Clock
           Prop[ExplodeStartTime] ------> EROSION.StartTime
           Prop[ExplodeDuration] ------> EROSION.Duration
           EROSION.Survival (1|0) ------> PrismOcclusionFade.BaseAlpha

So the erosion owns the FADE (angle-free) while the corridor keeps owning OCCLUSION (a
view effect by definition); a fading chunk inside the corridor composes the two screen
doors in coverage. Live prisms on this graph are exact pass-throughs: with no explosion
stamped the clock hands _Opacity through, and the erosion's >=1 / <=0 early-outs return
1 or 0 untouched (MazeDanger solid, cloak-invisible TransparentPrismMaterial gone).

Every property node fanned out from here already exists in the graph — the tool adds
ONE Custom Function node, ONE Object-space Position node, retargets ONE edge, and adds
six. BlockGraph is deliberately NOT touched: nothing on it explodes.

Out-of-editor ShaderGraph JSON synthesis per /asset-surgery: parse everything, clone
same-file donors, rebuild in memory, assert every invariant, only then write.
Idempotent — re-run after a graph revert to repair. Usage: [--check] validates without
writing (exit 1 if not wired).
"""

import json
import os
import sys
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPH = "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph"

# GUID of PrismOcclusionCorridor.hlsl (pinned by its committed .meta) — the erosion
# lives beside the corridor because it IS prism transparency, same file, same hashes.
HLSL_GUID = "bf8e2c1fa76142c89ba03b2e1ae46201"
FUNCTION_NAME = "PrismErosionFade"
CORRIDOR_FUNCTION = "PrismOcclusionFade"
CORRIDOR_BASEALPHA_SLOT = 3
CLOCK_FUNCTION = "PrismExplosionClock"

# (integer slot id, display name, "Vector1"|"Vector3", is_output) — ids MUST match the
# HLSL parameter order (inputs first, then outputs).
CF_SLOTS = [
    (0, "PositionOS", "Vector3", False),
    (1, "Velocity", "Vector3", False),
    (2, "Clock", "Vector1", False),
    (3, "StartTime", "Vector1", False),
    (4, "Duration", "Vector1", False),
    (5, "BaseOpacity", "Vector1", False),
    (6, "Survival", "Vector1", True),
]

# The property NODES the erosion fans out from (property name -> erosion input slot).
PROPERTY_FEEDS = {
    "Velocity": 1,
    "PrismClock": 2,
    "ExplodeStartTime": 3,
    "ExplodeDuration": 4,
}

COORDINATE_SPACE_OBJECT = 0


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


def cf_by_function(docs, idx, graph, fn):
    for r in graph["m_Nodes"]:
        node = idx[r["m_Id"]]
        if node.get("m_FunctionName") == fn:
            return node
    return None


def node_output_slot(idx, node, display_name=None):
    """The (single, or named) output slot's INTEGER id on a node."""
    outs = []
    for s in node.get("m_Slots", []):
        sd = idx[s["m_Id"]]
        if sd.get("m_SlotType") == 1 and (display_name is None or sd.get("m_DisplayName") == display_name):
            outs.append(sd["m_Id"])
    assert len(outs) == 1, f"expected one output slot ({display_name}) on {node.get('m_Name')}, found {len(outs)}"
    return outs[0]


def property_node(docs, idx, graph, prop_name):
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

    erosion = cf_by_function(docs, idx, graph, FUNCTION_NAME)
    assert erosion is not None, "PrismErosionFade custom function node missing"
    assert erosion["m_FunctionSource"] == HLSL_GUID, "erosion points at the wrong HLSL asset"
    er_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in erosion["m_Slots"]}
    assert set(er_slots) == {s[0] for s in CF_SLOTS}, "erosion slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CF_SLOTS:
        assert er_slots[slot_id]["m_DisplayName"] == name, f"erosion slot {slot_id} name drifted"
        assert er_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"erosion slot {slot_id} direction wrong"

    sources = {}
    for e in graph["m_Edges"]:
        sources[(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])] = \
            (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])

    # every erosion input is fed, and by what the design says feeds it
    corridor = cf_by_function(docs, idx, graph, CORRIDOR_FUNCTION)
    clock = cf_by_function(docs, idx, graph, CLOCK_FUNCTION)
    assert corridor is not None and clock is not None, "corridor / explosion clock node missing"

    src = sources.get((erosion["m_ObjectId"], 5))
    assert src is not None, "erosion BaseOpacity unconnected"
    assert src[0] == clock["m_ObjectId"], "erosion BaseOpacity is not fed by the explosion clock"
    assert idx and src[1] == node_output_slot(idx, clock, "Opacity"), \
        "erosion BaseOpacity is not fed by the clock's Opacity output"

    for prop_name, slot in PROPERTY_FEEDS.items():
        pn = property_node(docs, idx, graph, prop_name)
        assert pn is not None, f"property node {prop_name} missing"
        assert sources.get((erosion["m_ObjectId"], slot)) == (pn["m_ObjectId"], node_output_slot(idx, pn)), \
            f"erosion input {slot} is not fed by Prop[{prop_name}]"

    pos_src = sources.get((erosion["m_ObjectId"], 0))
    assert pos_src is not None, "erosion PositionOS unconnected"
    pos_node = idx[pos_src[0]]
    assert pos_node.get("m_Type", "").endswith("PositionNode"), "erosion PositionOS not fed by a Position node"
    assert pos_node.get("m_Space") == COORDINATE_SPACE_OBJECT, "erosion Position node is not OBJECT space"

    # and the corridor's BaseAlpha now reads the erosion's Survival
    assert sources.get((corridor["m_ObjectId"], CORRIDOR_BASEALPHA_SLOT)) == \
        (erosion["m_ObjectId"], 6), "corridor BaseAlpha is not fed by erosion Survival"


def main():
    check_only = "--check" in sys.argv
    path = os.path.join(REPO, GRAPH)
    docs = load_docs(path)
    graph = find_graph(docs)
    idx = index(docs)

    if cf_by_function(docs, idx, graph, FUNCTION_NAME) is not None:
        validate(docs, expect_wired=True)
        print(f"  {os.path.basename(GRAPH)}: already wired (validated).")
        return 0
    if check_only:
        print(f"  {os.path.basename(GRAPH)}: NOT wired.", file=sys.stderr)
        return 1

    validate(docs, expect_wired=False)

    # ---- donors (all same-file) ----
    corridor = cf_by_function(docs, idx, graph, CORRIDOR_FUNCTION)
    clock = cf_by_function(docs, idx, graph, CLOCK_FUNCTION)
    assert corridor is not None, "corridor node missing — run wire_prism_occlusion_corridor.py first"
    assert clock is not None, "explosion clock node missing"
    donor_slot_v3 = next(idx[s["m_Id"]] for s in corridor["m_Slots"]
                         if "Vector3MaterialSlot" in idx[s["m_Id"]]["m_Type"])
    donor_slot_v1 = next(idx[s["m_Id"]] for s in corridor["m_Slots"]
                         if "Vector1MaterialSlot" in idx[s["m_Id"]]["m_Type"])
    donor_pos = next(idx[r["m_Id"]] for r in graph["m_Nodes"]
                     if idx[r["m_Id"]].get("m_Type", "").endswith("PositionNode"))

    new_docs = []

    # ---- the Object-space Position node (clone the corridor's World one) ----
    pos_node = json.loads(json.dumps(donor_pos))
    pos_node["m_ObjectId"] = uuid.uuid4().hex
    pos_node["m_Space"] = COORDINATE_SPACE_OBJECT
    pos_node["m_DrawState"]["m_Position"].update({"x": -1500.0, "y": 2150.0})
    pos_slot = json.loads(json.dumps(idx[donor_pos["m_Slots"][0]["m_Id"]]))
    pos_slot["m_ObjectId"] = uuid.uuid4().hex
    pos_node["m_Slots"] = [{"m_Id": pos_slot["m_ObjectId"]}]
    new_docs += [pos_node, pos_slot]

    # ---- the erosion custom function node ----
    er_node = json.loads(json.dumps(corridor))
    er_node["m_ObjectId"] = uuid.uuid4().hex
    er_node["m_Name"] = f"{FUNCTION_NAME} (Custom Function)"
    er_node["m_FunctionName"] = FUNCTION_NAME
    er_node["m_FunctionSource"] = HLSL_GUID
    er_node["m_SourceType"] = 0
    er_node["m_DrawState"]["m_Position"].update({"x": -1180.0, "y": 2100.0, "width": 232.0, "height": 380.0})
    er_slots = []
    for slot_id, name, kind, is_output in CF_SLOTS:
        donor = donor_slot_v3 if kind == "Vector3" else donor_slot_v1
        er_slots.append(make_slot(donor, slot_id, name, is_output))
    er_node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in er_slots]
    new_docs += [er_node] + er_slots

    for node in (pos_node, er_node):
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    # ---- edges ----
    # Retarget the clock->corridor BaseAlpha edge into the erosion's BaseOpacity.
    retargeted = 0
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == corridor["m_ObjectId"] and i["m_SlotId"] == CORRIDOR_BASEALPHA_SLOT:
            assert e["m_OutputSlot"]["m_Node"]["m_Id"] == clock["m_ObjectId"], \
                "corridor BaseAlpha is not fed by the explosion clock — graph shape drifted, refusing"
            e["m_InputSlot"] = {"m_Node": {"m_Id": er_node["m_ObjectId"]}, "m_SlotId": 5}
            retargeted += 1
    assert retargeted == 1, f"expected exactly one BaseAlpha feeder, retargeted {retargeted}"

    graph["m_Edges"].append(edge(pos_node["m_ObjectId"], pos_slot["m_Id"], er_node["m_ObjectId"], 0))
    for prop_name, slot in PROPERTY_FEEDS.items():
        pn = property_node(docs, idx, graph, prop_name)
        assert pn is not None, f"no property node for {prop_name} in the graph"
        graph["m_Edges"].append(edge(pn["m_ObjectId"], node_output_slot(idx, pn), er_node["m_ObjectId"], slot))
    graph["m_Edges"].append(edge(er_node["m_ObjectId"], 6, corridor["m_ObjectId"], CORRIDOR_BASEALPHA_SLOT))

    docs += new_docs
    validate(docs, expect_wired=True)   # nothing written yet

    open(path, "w", encoding="utf-8").write(dump_docs(docs))
    validate(load_docs(path), expect_wired=True)   # re-read from disk and re-check
    print(f"  {os.path.basename(GRAPH)}: wired and validated "
          f"(+2 nodes, +{len(new_docs) - 2} slots, 6 new edges, 1 retargeted).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
