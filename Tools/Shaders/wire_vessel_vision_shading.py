#!/usr/bin/env python3
"""
Wire the VESSEL VISION BAND into the vessel hull graph (Docs/VESSEL_VISION.md).

Every vessel is progressively re-shaded into a flat, cel-banded silhouette in its own DOMAIN
colour as a function of its distance from the camera drawing it. The whole distance test runs in
the fragment stage (VesselVisionShading.hlsl) off three global uniforms plus one per-vessel
property, so there is no per-vessel per-frame CPU anywhere in the law.

ONE graph is wired, and that is not a shortcut — VesselGraph.shadergraph is what every hull
surface of every vessel in the fleet is painted with. VesselCustomization's three material roles
(Body = BlueBaseVesselMaterial, Domain = the per-domain ship material, Window =
ScreenVesselMaterial) are all VesselGraph materials, as are the engine/accent/lime variants. The
non-VesselGraph materials a vessel carries are its skimmer crackle overlay, its jet particles, the
Rhino's sword tracer and the trail viewer — none of which is hull, and none of which should wear a
pilot-identification mark.

The splice sits at the very end of the colour chain, on SurfaceDescription.BaseColor:

  BEFORE:  Multiply(Blend x ColorMultiplier) -----------------> SurfaceDescription.BaseColor
  AFTER:   Multiply(Blend x ColorMultiplier) --> VISION.BaseColor
           Position(World)      [new] ---------> VISION.PositionWS
           Normal Vector(World) [new] ---------> VISION.NormalWS
           Property(_VesselVisionTint) [new] --> VISION.Tint
           VISION.Color ---------------------------------------> SurfaceDescription.BaseColor

It MUST be last. _ColorMultiplier is a per-material brightness the vessels author from 1 to 5, and
the Echo Sight drives it live; folding the mark in before that multiply would make a pilot's
identification colour a function of which engine cowling it landed on and of whether a Dolphin
happened to be holding a trigger. The mark carries its own gain instead, so the same domain reads
the same brightness on every surface of every ship.

_VesselVisionTint is EXPOSED (m_GeneratePropertyBlock), which is load-bearing twice over: an
unexposed ShaderGraph property is declared outside UnityPerMaterial, so a MaterialPropertyBlock
could not reach it, and Material.HasColor could never see it — which is the trap
PrismOcclusionDiagnostics records for the corridor's own globals, and the reason the runtime can
census wired materials here but not there. Its default is (0,0,0,0) and its ALPHA is a marker
rather than an opacity: an object nobody stamped is not a vessel, and the shader declines rather
than guessing. That is what keeps the law off BlueOrangeProjectileMaterial, which also wears this
graph.

Out-of-editor ShaderGraph JSON synthesis per /asset-surgery: parse everything, clone same-file
donors where they exist, rebuild in memory, assert every invariant, only then write — and validate
again from disk afterwards. Idempotent: re-run after a graph revert to repair.

Usage:  python3 Tools/Shaders/wire_vessel_vision_shading.py [--check]
        --check validates without writing, and exits non-zero if the graph is unwired.
"""

import json
import os
import sys
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

GRAPH = "Assets/_Graphics/Materials/Graphs/VesselGraph.shadergraph"

# The graph carries no CustomFunctionNode of its own, so the node shell is cloned from a graph
# that does. Same external-donor pattern the corridor and back-face wirers use.
CF_DONOR = "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph"
CF_DONOR_FUNCTION = "PrismOcclusionFade"

HLSL_GUID = "6862450db5b346df96c3355ca0543f93"   # VesselVisionShading.hlsl.meta
FUNCTION_NAME = "VesselVisionShade"

TINT_PROPERTY_NAME = "VesselVisionTint"
TINT_REFERENCE_NAME = "_VesselVisionTint"

BASECOLOR_BLOCK = "SurfaceDescription.BaseColor"

# (slot id, display name, kind, is output) — MUST match VesselVisionShade_float's parameter order.
CF_SLOTS = [
    (0, "PositionWS", "Vector3", False),
    (1, "NormalWS", "Vector3", False),
    (2, "Tint", "Vector4", False),
    (3, "BaseColor", "Vector3", False),
    (4, "Color", "Vector3", True),
]

COORDINATE_SPACE_WORLD = 2
COLOR_MODE_HDR = 1


# ---------------------------------------------------------------- io

def load_docs(path):
    """ShaderGraph files are a stream of concatenated JSON objects, not one document."""
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


def find_block(docs, descriptor):
    for d in docs:
        if d.get("m_SerializedDescriptor") == descriptor:
            return d
    return None


def cf_by_function(idx, graph, fn):
    for r in graph["m_Nodes"]:
        node = idx[r["m_Id"]]
        if node.get("m_FunctionName") == fn:
            return node
    return None


def nodes_of_type(idx, graph, suffix):
    return [idx[r["m_Id"]] for r in graph["m_Nodes"]
            if idx[r["m_Id"]].get("m_Type", "").endswith(suffix)]


def clone(doc):
    c = json.loads(json.dumps(doc))
    c["m_ObjectId"] = uuid.uuid4().hex
    return c


def edge(out_node, out_slot, in_node, in_slot):
    return {
        "m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
        "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot},
    }


def make_slot(donor, slot_id, display_name, is_output):
    s = clone(donor)
    s["m_Id"] = slot_id
    s["m_DisplayName"] = display_name
    s["m_ShaderOutputName"] = display_name
    s["m_SlotType"] = 1 if is_output else 0
    s["m_StageCapability"] = 3
    s["m_Hidden"] = False
    zero = {"x": 0.0, "y": 0.0, "z": 0.0}
    if "Vector4" in s["m_Type"]:
        zero = {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}
    s["m_Value"] = dict(zero)
    s["m_DefaultValue"] = dict(zero)
    return s


# ---------------------------------------------------------------- validation

def validate(docs, expect_wired):
    idx = index(docs)
    graph = find_graph(docs)

    ids = [d["m_ObjectId"] for d in docs if "m_ObjectId" in d]
    assert len(ids) == len(set(ids)), "duplicate m_ObjectId"

    # Structural invariants that must hold whether or not the splice is in: every node reference
    # resolves, every slot resolves, every edge runs output -> input, and no input slot has two
    # feeders (ShaderGraph shows the second one and silently drops it).
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

    # Every property in m_Properties resolves and is listed in exactly one category, or the
    # blackboard renders an empty row and the property is unreachable from the UI.
    prop_ids = [p["m_Id"] for p in graph["m_Properties"]]
    assert len(prop_ids) == len(set(prop_ids)), "duplicate entry in m_Properties"
    listed = []
    for c in graph["m_CategoryData"]:
        listed += [child["m_Id"] for child in idx[c["m_Id"]]["m_ChildObjectList"]]
    for pid in prop_ids:
        assert pid in idx, f"m_Properties references missing {pid}"
        assert listed.count(pid) == 1, f"property {pid} appears {listed.count(pid)} times in categories"

    if not expect_wired:
        return

    vision = cf_by_function(idx, graph, FUNCTION_NAME)
    assert vision is not None, "VesselVisionShade custom function node missing"
    assert vision["m_FunctionSource"] == HLSL_GUID, "vision node points at the wrong HLSL asset"
    assert vision["m_SourceType"] == 0, "vision node is not in File source mode"

    vslots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in vision["m_Slots"]}
    assert set(vslots) == {s[0] for s in CF_SLOTS}, "vision slot ids do not match the HLSL signature"
    for slot_id, name, kind, is_output in CF_SLOTS:
        assert vslots[slot_id]["m_DisplayName"] == name, f"vision slot {slot_id} name drifted"
        assert vslots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"vision slot {slot_id} direction wrong"
        assert kind in vslots[slot_id]["m_Type"], f"vision slot {slot_id} is not a {kind} slot"

    sources = {}
    for e in graph["m_Edges"]:
        sources[(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])] = \
            (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])

    pos_src = sources.get((vision["m_ObjectId"], 0))
    assert pos_src is not None, "vision PositionWS unconnected"
    assert idx[pos_src[0]].get("m_Type", "").endswith("PositionNode"), "PositionWS not fed by a Position node"
    assert idx[pos_src[0]].get("m_Space") == COORDINATE_SPACE_WORLD, "PositionWS node is not WORLD space"

    nrm_src = sources.get((vision["m_ObjectId"], 1))
    assert nrm_src is not None, "vision NormalWS unconnected"
    assert idx[nrm_src[0]].get("m_Type", "").endswith("NormalVectorNode"), "NormalWS not fed by a NormalVector node"
    assert idx[nrm_src[0]].get("m_Space") == COORDINATE_SPACE_WORLD, "NormalWS node is not WORLD space"

    tint_src = sources.get((vision["m_ObjectId"], 2))
    assert tint_src is not None, "vision Tint unconnected"
    tint_node = idx[tint_src[0]]
    assert tint_node.get("m_Type", "").endswith("PropertyNode"), "Tint not fed by a Property node"
    tint_prop = idx[tint_node["m_Property"]["m_Id"]]
    assert tint_prop["m_DefaultReferenceName"] == TINT_REFERENCE_NAME, \
        f"Tint property reference name is {tint_prop['m_DefaultReferenceName']}, not {TINT_REFERENCE_NAME}"
    assert tint_prop["m_GeneratePropertyBlock"] is True, \
        (f"{TINT_REFERENCE_NAME} is not EXPOSED — an unexposed property lands outside "
         "UnityPerMaterial, so no MaterialPropertyBlock can reach it and no material can be "
         "censused for it. The whole per-vessel channel dies silently.")
    assert tint_prop["m_Hidden"] is False, f"{TINT_REFERENCE_NAME} is hidden from the blackboard"
    assert tint_prop["m_Value"]["a"] == 0.0, \
        (f"{TINT_REFERENCE_NAME} default alpha must be 0 — alpha is the 'this object is a vessel' "
         "marker, and a non-zero default would mark every object wearing this graph.")

    base_src = sources.get((vision["m_ObjectId"], 3))
    assert base_src is not None, "vision BaseColor unconnected"
    assert idx[base_src[0]].get("m_Type", "").endswith("MultiplyNode"), \
        ("vision BaseColor is not fed by the graph's final Multiply — the splice MUST sit after "
         "_ColorMultiplier, or the mark's brightness becomes a function of which cowling it "
         "landed on")

    block = find_block(docs, BASECOLOR_BLOCK)
    assert block is not None, f"{BASECOLOR_BLOCK} block missing"
    assert sources.get((block["m_ObjectId"], 0)) == (vision["m_ObjectId"], 4), \
        f"{BASECOLOR_BLOCK} is not fed by the vision node's Color output"


# ---------------------------------------------------------------- wiring

def wire(check_only):
    path = os.path.join(REPO, GRAPH)
    docs = load_docs(path)
    graph = find_graph(docs)
    idx = index(docs)

    if cf_by_function(idx, graph, FUNCTION_NAME) is not None:
        validate(docs, expect_wired=True)
        return False, f"{os.path.basename(GRAPH)}: already wired (validated)."
    if check_only:
        return False, None

    validate(docs, expect_wired=False)

    # ---- donors -------------------------------------------------------------
    donor_docs = load_docs(os.path.join(REPO, CF_DONOR))
    donor_idx = index(donor_docs)
    donor_cf = cf_by_function(donor_idx, find_graph(donor_docs), CF_DONOR_FUNCTION)
    assert donor_cf is not None, f"{CF_DONOR}: no {CF_DONOR_FUNCTION} node to clone"
    donor_slot_v3 = next(donor_idx[s["m_Id"]] for s in donor_cf["m_Slots"]
                         if "Vector3MaterialSlot" in donor_idx[s["m_Id"]]["m_Type"])

    donor_slot_v4 = next(d for d in docs if d.get("m_Type", "").endswith("Vector4MaterialSlot"))

    donor_position = next(n for n in nodes_of_type(idx, graph, "PositionNode"))
    donor_normal = next(n for n in nodes_of_type(idx, graph, "NormalVectorNode"))
    donor_prop_node = next(n for n in nodes_of_type(idx, graph, "PropertyNode"))
    donor_prop = idx[donor_prop_node["m_Property"]["m_Id"]]
    assert "ColorShaderProperty" in donor_prop["m_Type"], "expected a Color property to clone"

    new_docs = []

    # ---- world Position -----------------------------------------------------
    position_node = clone(donor_position)
    position_node["m_Space"] = COORDINATE_SPACE_WORLD
    position_node["m_DrawState"]["m_Position"].update({"x": 300.0, "y": 900.0})
    position_slot = clone(idx[donor_position["m_Slots"][0]["m_Id"]])
    position_node["m_Slots"] = [{"m_Id": position_slot["m_ObjectId"]}]
    new_docs += [position_node, position_slot]

    # ---- world Normal -------------------------------------------------------
    normal_node = clone(donor_normal)
    normal_node["m_Space"] = COORDINATE_SPACE_WORLD
    normal_node["m_DrawState"]["m_Position"].update({"x": 300.0, "y": 990.0})
    normal_slot = clone(idx[donor_normal["m_Slots"][0]["m_Id"]])
    normal_node["m_Slots"] = [{"m_Id": normal_slot["m_ObjectId"]}]
    new_docs += [normal_node, normal_slot]

    # ---- the exposed tint property + its node --------------------------------
    tint_prop = clone(donor_prop)
    tint_prop["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
    tint_prop["m_Name"] = TINT_PROPERTY_NAME
    tint_prop["m_RefNameGeneratedByDisplayName"] = TINT_PROPERTY_NAME
    tint_prop["m_DefaultReferenceName"] = TINT_REFERENCE_NAME
    tint_prop["m_OverrideReferenceName"] = ""
    tint_prop["m_GeneratePropertyBlock"] = True
    tint_prop["m_Hidden"] = False
    tint_prop["isMainColor"] = False
    tint_prop["m_ColorMode"] = COLOR_MODE_HDR
    # Alpha 0 is the "no domain published for this object" sentinel the shader gates on.
    tint_prop["m_Value"] = {"r": 0.0, "g": 0.0, "b": 0.0, "a": 0.0}

    tint_node = clone(donor_prop_node)
    tint_node["m_Property"] = {"m_Id": tint_prop["m_ObjectId"]}
    tint_node["m_DrawState"]["m_Position"].update({"x": 300.0, "y": 1080.0, "width": 140.0, "height": 34.0})
    tint_slot = clone(donor_slot_v4)
    tint_slot["m_Id"] = 0
    tint_slot["m_DisplayName"] = TINT_PROPERTY_NAME
    tint_slot["m_ShaderOutputName"] = "Out"
    tint_slot["m_SlotType"] = 1
    tint_node["m_Slots"] = [{"m_Id": tint_slot["m_ObjectId"]}]
    new_docs += [tint_prop, tint_node, tint_slot]

    graph["m_Properties"].append({"m_Id": tint_prop["m_ObjectId"]})
    category = idx[graph["m_CategoryData"][0]["m_Id"]]
    category["m_ChildObjectList"].append({"m_Id": tint_prop["m_ObjectId"]})

    # ---- the custom function node -------------------------------------------
    vision_node = clone(donor_cf)
    vision_node["m_Name"] = f"{FUNCTION_NAME} (Custom Function)"
    vision_node["m_FunctionName"] = FUNCTION_NAME
    vision_node["m_FunctionSource"] = HLSL_GUID
    vision_node["m_SourceType"] = 0
    vision_node["m_Group"] = {"m_Id": ""}
    vision_node["m_DrawState"]["m_Position"].update(
        {"x": 620.0, "y": 900.0, "width": 232.0, "height": 300.0})
    vslots = []
    for slot_id, name, kind, is_output in CF_SLOTS:
        donor = donor_slot_v4 if kind == "Vector4" else donor_slot_v3
        vslots.append(make_slot(donor, slot_id, name, is_output))
    vision_node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in vslots]
    new_docs += [vision_node] + vslots

    for node in (position_node, normal_node, tint_node, vision_node):
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    # ---- retarget the BaseColor feeder into the vision node -------------------
    block = find_block(docs, BASECOLOR_BLOCK)
    assert block is not None, f"{GRAPH}: no {BASECOLOR_BLOCK} block"
    retargeted = 0
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == block["m_ObjectId"]:
            feeder = idx[e["m_OutputSlot"]["m_Node"]["m_Id"]]
            assert feeder.get("m_Type", "").endswith("MultiplyNode"), \
                (f"{BASECOLOR_BLOCK} is fed by {feeder.get('m_Type')}, not the graph's final "
                 "Multiply — graph shape drifted, refusing to splice")
            e["m_InputSlot"] = {"m_Node": {"m_Id": vision_node["m_ObjectId"]}, "m_SlotId": 3}
            retargeted += 1
    assert retargeted == 1, f"{GRAPH}: expected one BaseColor feeder, retargeted {retargeted}"

    graph["m_Edges"] += [
        edge(position_node["m_ObjectId"], position_slot["m_Id"], vision_node["m_ObjectId"], 0),
        edge(normal_node["m_ObjectId"], normal_slot["m_Id"], vision_node["m_ObjectId"], 1),
        edge(tint_node["m_ObjectId"], tint_slot["m_Id"], vision_node["m_ObjectId"], 2),
        edge(vision_node["m_ObjectId"], 4, block["m_ObjectId"], 0),
    ]

    docs += new_docs
    validate(docs, expect_wired=True)          # nothing written yet

    open(path, "w", encoding="utf-8").write(dump_docs(docs))
    validate(load_docs(path), expect_wired=True)
    return True, (f"{os.path.basename(GRAPH)}: wired and validated "
                  f"(+4 nodes, +1 exposed property, 4 new edges, 1 retargeted).")


def main():
    check_only = "--check" in sys.argv
    _changed, message = wire(check_only)
    if message is None:
        print(f"  {os.path.basename(GRAPH)}: NOT wired.", file=sys.stderr)
        return 1
    print("  " + message)
    return 0


if __name__ == "__main__":
    sys.exit(main())
