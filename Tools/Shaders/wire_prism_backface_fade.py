#!/usr/bin/env python3
"""
Wire the BACK-FACE alpha separation into every corridor-wired prism graph.

Prisms render two-sided (`_Cull: 0`), so the layered dither beat's usual second surface
is a prism's OWN interior, seen through the holes its front face just punched and sitting
at nearly the same corridor alpha. PrismBackFaceFade (in PrismOcclusionCorridor.hlsl)
sharpens the alpha of far-facing surfaces (alpha^power), pushing the interior out of the
gradient band while the exterior is still dissolving — so only one dithered layer is live
at a time and there is nothing left to interfere with. This is the one fix that removes
the interference rather than scrambling it.

The splice sits AFTER the corridor, on its Alpha output:

  BEFORE:  PrismOcclusionFade.Alpha ---------------------> SurfaceDescription.Alpha
  AFTER:   PrismOcclusionFade.Alpha -> BACKFACE.BaseAlpha
           Position(World) ----------> BACKFACE.PositionWS   (the corridor's own node, reused)
           Normal(World) ------------> BACKFACE.NormalWS     (new)
           BACKFACE.Alpha -----------------------------> SurfaceDescription.Alpha

It MUST be after the corridor: in the corridor's gradient band the graph's own alpha is 1
and only the corridor's fade is fractional, so sharpening earlier would square a 1 and do
nothing. The corridor's ClipThreshold output is untouched and still compares against this
sharpened alpha — which is exactly "the interior clips out sooner".

Facing is computed from the world NORMAL rather than SV_IsFrontFace: Shader Graph only
exposes that semantic through an Is Front Face node, and this project has none to
donor-clone, while NormalVector nodes are everywhere. The interpolated normal is the
geometric outward normal on both sides of a two-sided draw, so dot(N, camera - pos) < 0
identifies the far side. Same answer, ordinary data, no new varying.

Out-of-editor ShaderGraph JSON synthesis per /asset-surgery: parse everything, clone
same-file donors, rebuild in memory, assert every invariant, only then write. Idempotent —
re-run after a graph revert to repair. Usage: [--check] validates without writing.
"""

import json
import os
import sys
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPHS = [
    "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
    "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
]
# Graphs that ship a NormalVector node to clone. Both targets lack one, so the donor is
# external — same pattern the corridor wirer uses for its Position node.
NORMAL_DONOR = "Assets/_Graphics/Materials/Graphs/CageGraph.shadergraph"

HLSL_GUID = "bf8e2c1fa76142c89ba03b2e1ae46201"
FUNCTION_NAME = "PrismBackFaceFade"
CORRIDOR_FUNCTION = "PrismOcclusionFade"
CORRIDOR_ALPHA_SLOT = 4          # PrismOcclusionFade's Alpha output
ALPHA_BLOCK = "SurfaceDescription.Alpha"

CF_SLOTS = [
    (0, "PositionWS", "Vector3", False),
    (1, "NormalWS", "Vector3", False),
    (2, "BaseAlpha", "Vector1", False),
    (3, "Alpha", "Vector1", True),
]

COORDINATE_SPACE_WORLD = 2


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


def find_block(docs, descriptor):
    for d in docs:
        if d.get("m_SerializedDescriptor") == descriptor:
            return d
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

    back = cf_by_function(idx, graph, FUNCTION_NAME)
    assert back is not None, "PrismBackFaceFade custom function node missing"
    assert back["m_FunctionSource"] == HLSL_GUID, "back-face node points at the wrong HLSL asset"
    bslots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in back["m_Slots"]}
    assert set(bslots) == {s[0] for s in CF_SLOTS}, "back-face slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CF_SLOTS:
        assert bslots[slot_id]["m_DisplayName"] == name, f"back-face slot {slot_id} name drifted"
        assert bslots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"back-face slot {slot_id} direction wrong"

    sources = {}
    for e in graph["m_Edges"]:
        sources[(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])] = \
            (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])

    corridor = cf_by_function(idx, graph, CORRIDOR_FUNCTION)
    assert corridor is not None, "corridor node missing"

    src = sources.get((back["m_ObjectId"], 2))
    assert src == (corridor["m_ObjectId"], CORRIDOR_ALPHA_SLOT), \
        "back-face BaseAlpha is not fed by the corridor's Alpha output (it MUST sit after the corridor)"

    pos_src = sources.get((back["m_ObjectId"], 0))
    assert pos_src is not None, "back-face PositionWS unconnected"
    assert idx[pos_src[0]].get("m_Type", "").endswith("PositionNode"), "PositionWS not fed by a Position node"
    assert idx[pos_src[0]].get("m_Space") == COORDINATE_SPACE_WORLD, "PositionWS node is not WORLD space"

    nrm_src = sources.get((back["m_ObjectId"], 1))
    assert nrm_src is not None, "back-face NormalWS unconnected"
    assert idx[nrm_src[0]].get("m_Type", "").endswith("NormalVectorNode"), "NormalWS not fed by a NormalVector node"
    assert idx[nrm_src[0]].get("m_Space") == COORDINATE_SPACE_WORLD, "NormalWS node is not WORLD space"

    alpha_block = find_block(docs, ALPHA_BLOCK)
    assert alpha_block is not None, "SurfaceDescription.Alpha block missing"
    assert sources.get((alpha_block["m_ObjectId"], 0)) == (back["m_ObjectId"], 3), \
        "SurfaceDescription.Alpha is not fed by the back-face node's Alpha"


def wire(rel_path, check_only):
    path = os.path.join(REPO, rel_path)
    docs = load_docs(path)
    graph = find_graph(docs)
    idx = index(docs)

    if cf_by_function(idx, graph, FUNCTION_NAME) is not None:
        validate(docs, expect_wired=True)
        return False, f"{os.path.basename(rel_path)}: already wired (validated)."
    if check_only:
        return False, None

    validate(docs, expect_wired=False)

    corridor = cf_by_function(idx, graph, CORRIDOR_FUNCTION)
    assert corridor is not None, (
        f"{rel_path}: no corridor node — run wire_prism_occlusion_corridor.py first")
    donor_slot_v3 = next(idx[s["m_Id"]] for s in corridor["m_Slots"]
                         if "Vector3MaterialSlot" in idx[s["m_Id"]]["m_Type"])
    donor_slot_v1 = next(idx[s["m_Id"]] for s in corridor["m_Slots"]
                         if "Vector1MaterialSlot" in idx[s["m_Id"]]["m_Type"])

    # The corridor's own World Position node — fan it out rather than adding a second.
    position_node = next(idx[r["m_Id"]] for r in graph["m_Nodes"]
                         if idx[r["m_Id"]].get("m_Type", "").endswith("PositionNode")
                         and idx[r["m_Id"]].get("m_Space") == COORDINATE_SPACE_WORLD)
    position_slot = idx[position_node["m_Slots"][0]["m_Id"]]["m_Id"]

    nrm_docs = load_docs(os.path.join(REPO, NORMAL_DONOR))
    nrm_idx = index(nrm_docs)
    donor_nrm = next(d for d in nrm_docs if d.get("m_Type", "").endswith("NormalVectorNode"))
    donor_nrm_slot = nrm_idx[donor_nrm["m_Slots"][0]["m_Id"]]

    new_docs = []

    normal_node = json.loads(json.dumps(donor_nrm))
    normal_node["m_ObjectId"] = uuid.uuid4().hex
    normal_node["m_Space"] = COORDINATE_SPACE_WORLD
    normal_node["m_DrawState"]["m_Position"].update({"x": -1500.0, "y": 2400.0})
    normal_slot = json.loads(json.dumps(donor_nrm_slot))
    normal_slot["m_ObjectId"] = uuid.uuid4().hex
    normal_node["m_Slots"] = [{"m_Id": normal_slot["m_ObjectId"]}]
    new_docs += [normal_node, normal_slot]

    back_node = json.loads(json.dumps(corridor))
    back_node["m_ObjectId"] = uuid.uuid4().hex
    back_node["m_Name"] = f"{FUNCTION_NAME} (Custom Function)"
    back_node["m_FunctionName"] = FUNCTION_NAME
    back_node["m_FunctionSource"] = HLSL_GUID
    back_node["m_SourceType"] = 0
    back_node["m_DrawState"]["m_Position"].update({"x": -880.0, "y": 2350.0, "width": 232.0, "height": 250.0})
    bslots = []
    for slot_id, name, kind, is_output in CF_SLOTS:
        donor = donor_slot_v3 if kind == "Vector3" else donor_slot_v1
        bslots.append(make_slot(donor, slot_id, name, is_output))
    back_node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in bslots]
    new_docs += [back_node] + bslots

    for node in (normal_node, back_node):
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    # Retarget corridor.Alpha -> SurfaceDescription.Alpha into the back-face node.
    alpha_block = find_block(docs, ALPHA_BLOCK)
    assert alpha_block is not None, f"{rel_path}: no {ALPHA_BLOCK} block"
    retargeted = 0
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == alpha_block["m_ObjectId"]:
            assert e["m_OutputSlot"]["m_Node"]["m_Id"] == corridor["m_ObjectId"] and \
                   e["m_OutputSlot"]["m_SlotId"] == CORRIDOR_ALPHA_SLOT, \
                   "SurfaceDescription.Alpha is not fed by the corridor — graph shape drifted, refusing"
            e["m_InputSlot"] = {"m_Node": {"m_Id": back_node["m_ObjectId"]}, "m_SlotId": 2}
            retargeted += 1
    assert retargeted == 1, f"{rel_path}: expected one Alpha feeder, retargeted {retargeted}"

    graph["m_Edges"] += [
        edge(position_node["m_ObjectId"], position_slot, back_node["m_ObjectId"], 0),
        edge(normal_node["m_ObjectId"], normal_slot["m_Id"], back_node["m_ObjectId"], 1),
        edge(back_node["m_ObjectId"], 3, alpha_block["m_ObjectId"], 0),
    ]

    docs += new_docs
    validate(docs, expect_wired=True)   # nothing written yet

    open(path, "w", encoding="utf-8").write(dump_docs(docs))
    validate(load_docs(path), expect_wired=True)
    return True, (f"{os.path.basename(rel_path)}: wired and validated "
                  f"(+2 nodes, +{len(new_docs) - 2} slots, 3 new edges, 1 retargeted).")


def main():
    check_only = "--check" in sys.argv
    unwired = []
    for rel in GRAPHS:
        _changed, message = wire(rel, check_only)
        if message is None:
            unwired.append(rel)
            print(f"  {os.path.basename(rel)}: NOT wired.", file=sys.stderr)
        else:
            print("  " + message)
    return 1 if unwired else 0


if __name__ == "__main__":
    sys.exit(main())
