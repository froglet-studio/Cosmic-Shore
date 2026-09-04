#!/usr/bin/env python3
"""
Wire the SUPER-SHIELD DEFLECTION JIGGLE clock into every graph a live prism can render with.

Docs/PRISM_ANIMATION.md §5 C14. A super-shielded prism is fully invulnerable, so until now
every hit on one was visually silent: sparks fired, the mass did not move, and the deflection
read as the shot having missed. This wires the deflection itself — a per-face wobble that
precesses, nutates and settles, evaluated entirely on the GPU from the shader clock and three
stamped initial conditions (Prism.AbsorbSuperShieldHit -> PrismSuperShieldJiggle.Stamp).

It is a per-instance STAMP rather than a per-frame global because it is §1 ANIMATION: a pure
function of the clock and what was known at the hit. Contrast PrismOcclusionFade and
PrismDestructionSight on these same graphs, which are view-dependent and therefore §4.7
globals.

Coverage is the same census as the flight clock and the occlusion corridor, for the same
reason: a live prism can render with BlockGraph OR ExplodingBlockGraph. Super-shielded mass
binds the team block material (BlockGraph) or, when the prism is transparent,
TransparentPrismMaterial — which rests on ExplodingBlockGraph. Wiring one graph leaves a
hole. SuctionGraph is excluded: it renders mass being consumed, and consumed mass is by
definition not deflecting a hit.

What it adds to each graph:

  properties (all three HYBRID PER INSTANCE — hlslDeclarationOverride 3 — because they are
  per-prism initial conditions):
      _JiggleStartTime  float   clock value at the hit
      _JiggleDuration   float   seconds of wobble; 0 = unstamped = identity
      _JiggleParams     float3  (peak tilt radians, precession rad/s, nutation rad/s)

  nodes:
      Property x3                        -> the three above
      Property                           -> the existing unexposed _PrismClock global
      PrismJiggleClock (Custom Function) -> PrismClockAnimation.hlsl
      NormalVector (object space)        -> ONLY on a graph whose VertexDescription.Normal
                                            is unfed (BlockGraph). Where something already
                                            drives it (ExplodingBlockGraph's shatter
                                            rotation), that feeder is retargeted instead.

  edges (the splice — the rotation goes in FRONT of the grow scale, exactly where
  ExplodingBlockGraph already puts its shatter rotation, so grow / explosion offset / flight
  offset all still apply on top):

      BEFORE:  <vertex source> ------------------------> GrowMultiply.A
               <normal source | nothing> --------------> VertexDescription.Normal
      AFTER:   <vertex source> -> PrismJiggleClock.Position
               <normal source | NormalVector(object)> -> PrismJiggleClock.Normal
               PrismJiggleClock.OutPosition -----------> GrowMultiply.A
               PrismJiggleClock.OutNormal -------------> VertexDescription.Normal

The grow Multiply — the node whose B input is PrismGrowScale.Scale — is the anchor on both
graphs, which is what makes one splice rule cover both.

Out-of-editor ShaderGraph JSON synthesis per the /asset-surgery protocol: parse the whole
file, clone same-file donors so the schema is exact by construction, rebuild in memory,
assert every invariant, and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0. That also
makes this the resolver for a .shadergraph merge conflict — take one side whole, re-run every
wirer, confirm each reports "already wired".

Usage:  python3 Tools/Shaders/wire_prism_jiggle_clock.py [--check]
        --check validates without writing (exit 1 if not wired).
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

# GUID of Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl, pinned by its
# committed .meta so this reference can never drift.
HLSL_GUID = "e3f9a1c27b8d4e05b6a4c9d1f0527a83"
FUNCTION_NAME = "PrismJiggleClock"

CLOCK_PROP_NAME = "PrismClock"  # the existing unexposed global, reused as the Clock feed
GROW_FUNCTION = "PrismGrowScale"  # its Scale output identifies the grow Multiply

# (display name, reference name, "Vector1"|"Vector3")
JIGGLE_PROPS = [
    ("JiggleStartTime", "_JiggleStartTime", "Vector1"),
    ("JiggleDuration", "_JiggleDuration", "Vector1"),
    ("JiggleParams", "_JiggleParams", "Vector3"),
]

# (integer slot id, display name, "Vector1"|"Vector3", is_output) — the integer ids MUST
# match the HLSL parameter order (every input first, then every output).
CF_SLOTS = [
    (0, "Clock", "Vector1", False),
    (1, "StartTime", "Vector1", False),
    (2, "Duration", "Vector1", False),
    (3, "Params", "Vector3", False),
    (4, "Position", "Vector3", False),
    (5, "Normal", "Vector3", False),
    (6, "OutPosition", "Vector3", True),
    (7, "OutNormal", "Vector3", True),
]

VERTEX_NORMAL_BLOCK = "VertexDescription.Normal"

OBJECT_SPACE = 0  # UnityEditor.ShaderGraph.CoordinateSpace.Object


# ---------------------------------------------------------------------------
# parse / serialize
# ---------------------------------------------------------------------------

def load_docs(path):
    """.shadergraph is CONCATENATED JSON documents, not one document."""
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


def new_oid():
    return uuid.uuid4().hex


def find_graph(docs):
    return next(d for d in docs if "GraphData" in d.get("m_Type", ""))


def index(docs):
    return {d["m_ObjectId"]: d for d in docs if "m_ObjectId" in d}


def find_property(docs, name):
    for d in docs:
        if d.get("m_Name") == name and "ShaderProperty" in d.get("m_Type", ""):
            return d
    return None


def find_block(docs, descriptor):
    for d in docs:
        if d.get("m_SerializedDescriptor") == descriptor:
            return d
    return None


def find_node_by_type(docs, type_fragment):
    for d in docs:
        if type_fragment in d.get("m_Type", ""):
            return d
    return None


def find_cf(docs, fn):
    for d in docs:
        if d.get("m_FunctionName") == fn:
            return d
    return None


def edge_sources(graph):
    """(input node, input slot) -> (output node, output slot) for every edge."""
    return {
        (e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]):
            (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])
        for e in graph["m_Edges"]
    }


def find_grow_multiply(docs, graph, idx):
    """The grow Multiply — the node PrismGrowScale.Scale feeds — plus the integer id of
    its OTHER (position) input. That is the anchor of the vertex chain on both graphs and
    the edge the jiggle splices in FRONT of.

    Returning the other input, not the one Scale feeds, is the whole point: retargeting
    Scale's own edge would cut the grow bloom out of the chain."""
    grow = find_cf(docs, GROW_FUNCTION)
    assert grow, f"{GROW_FUNCTION} not found — this graph has not had the clock wired"
    scale_slot = next(idx[s["m_Id"]]["m_Id"] for s in grow["m_Slots"]
                      if idx[s["m_Id"]].get("m_DisplayName") == "Scale")
    for e in graph["m_Edges"]:
        o, i = e["m_OutputSlot"], e["m_InputSlot"]
        if o["m_Node"]["m_Id"] == grow["m_ObjectId"] and o["m_SlotId"] == scale_slot:
            node = idx[i["m_Node"]["m_Id"]]
            assert "MultiplyNode" in node.get("m_Type", ""), \
                "PrismGrowScale.Scale no longer feeds a Multiply — the vertex chain changed"
            inputs = sorted(idx[s["m_Id"]]["m_Id"] for s in node["m_Slots"]
                            if idx[s["m_Id"]]["m_SlotType"] == 0)
            assert len(inputs) == 2, "the grow Multiply does not have exactly 2 inputs"
            other = [sid for sid in inputs if sid != i["m_SlotId"]]
            assert len(other) == 1, "PrismGrowScale.Scale feeds an input that is not one of two"
            return node, other[0]
    raise AssertionError("PrismGrowScale.Scale feeds nothing")


# ---------------------------------------------------------------------------
# builders (every one clones a donor of the same type)
# ---------------------------------------------------------------------------

def make_per_instance_property(donor, name, reference):
    """Clone a donor ShaderProperty of the right type and re-key it as a fresh
    Hybrid-Per-Instance property. The donor is always an existing clock property, so the
    exposure flags are already correct and are asserted afterwards."""
    p = json.loads(json.dumps(donor))
    p["m_ObjectId"] = new_oid()
    p["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
    p["m_Name"] = name
    p["m_RefNameGeneratedByDisplayName"] = name
    p["m_DefaultReferenceName"] = reference
    p["m_OverrideReferenceName"] = ""
    p["m_GeneratePropertyBlock"] = True
    p["overrideHLSLDeclaration"] = True
    p["hlslDeclarationOverride"] = 3  # HybridPerInstance
    p["m_Hidden"] = False
    # Authored default = the settled/unstamped state: duration 0 is what makes the HLSL
    # return identity, so every existing material renders byte-identically.
    if isinstance(p.get("m_Value"), dict):
        p["m_Value"] = {"x": 0.0, "y": 0.0, "z": 0.0}
    else:
        p["m_Value"] = 0.0
    return p


def make_slot(donor, slot_id, display_name, is_output):
    s = json.loads(json.dumps(donor))
    s["m_ObjectId"] = new_oid()
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


def make_property_node(donor_property_node, donor_slot, property_oid, label, x, y):
    node = json.loads(json.dumps(donor_property_node))
    node["m_ObjectId"] = new_oid()
    node["m_Property"] = {"m_Id": property_oid}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 200.0, "height": 36.0})
    slot = make_slot(donor_slot, 0, label, True)
    slot["m_ShaderOutputName"] = "Out"
    node["m_Slots"] = [{"m_Id": slot["m_ObjectId"]}]
    return node, [slot]


def make_custom_function_node(donor_cf, donor_slot_v1, donor_slot_v3, x, y):
    node = json.loads(json.dumps(donor_cf))
    node["m_ObjectId"] = new_oid()
    node["m_Name"] = f"{FUNCTION_NAME} (Custom Function)"
    node["m_FunctionName"] = FUNCTION_NAME
    node["m_FunctionSource"] = HLSL_GUID
    node["m_SourceType"] = 0
    node["m_FunctionBody"] = "Enter function body here..."
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 232.0, "height": 470.0})
    slots = []
    for slot_id, name, kind, is_output in CF_SLOTS:
        donor = donor_slot_v3 if kind == "Vector3" else donor_slot_v1
        slots.append(make_slot(donor, slot_id, name, is_output))
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def make_object_normal_node(donor_normal_node, donor_slots, x, y):
    """Clone the graph's own (world-space) NormalVectorNode and re-space it to OBJECT.
    Same-file donor, so the slot type and serializer version are exact by construction."""
    node = json.loads(json.dumps(donor_normal_node))
    node["m_ObjectId"] = new_oid()
    node["m_Space"] = OBJECT_SPACE
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y})
    slots = []
    for donor_slot in donor_slots:
        s = json.loads(json.dumps(donor_slot))
        s["m_ObjectId"] = new_oid()
        slots.append(s)
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def edge(out_node, out_slot, in_node, in_slot):
    return {
        "m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
        "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot},
    }


# ---------------------------------------------------------------------------
# validation
# ---------------------------------------------------------------------------

def validate(docs, expect_wired):
    """Rebuild the object model and assert every invariant. Raises on failure."""
    idx = index(docs)
    graph = find_graph(docs)

    ids = [d["m_ObjectId"] for d in docs if "m_ObjectId" in d]
    assert len(ids) == len(set(ids)), "duplicate m_ObjectId"

    for ref in graph["m_Nodes"]:
        assert ref["m_Id"] in idx, f"m_Nodes references missing {ref['m_Id']}"
    for ref in graph["m_Properties"]:
        assert ref["m_Id"] in idx, f"m_Properties references missing {ref['m_Id']}"
    for cat in graph["m_CategoryData"]:
        for child in idx[cat["m_Id"]]["m_ChildObjectList"]:
            assert child["m_Id"] in idx, "category child missing"

    slot_ids = {}
    for ref in graph["m_Nodes"]:
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
        for end, lbl in ((o, "output"), (i, "input")):
            nid = end["m_Node"]["m_Id"]
            assert nid in slot_ids, f"edge {lbl} node {nid} not registered in m_Nodes"
            assert end["m_SlotId"] in slot_ids[nid], f"edge {lbl} slot {end['m_SlotId']} missing on {nid}"
        assert slot_ids[o["m_Node"]["m_Id"]][o["m_SlotId"]]["m_SlotType"] == 1, "edge output is not an output slot"
        assert slot_ids[i["m_Node"]["m_Id"]][i["m_SlotId"]]["m_SlotType"] == 0, "edge input is not an input slot"
        key = (i["m_Node"]["m_Id"], i["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
    for key, count in feeders.items():
        assert count == 1, f"input slot {key} has {count} feeders (must be exactly 1)"

    if not expect_wired:
        return

    for name, reference, _kind in JIGGLE_PROPS:
        p = find_property(docs, name)
        assert p is not None, f"property {name} missing"
        assert p["m_DefaultReferenceName"] == reference, f"{name} reference name wrong"
        assert p["m_GeneratePropertyBlock"] is True, f"{name} must be EXPOSED"
        assert p["overrideHLSLDeclaration"] is True, f"{name} must override the HLSL declaration"
        assert p["hlslDeclarationOverride"] == 3, \
            f"{name} must be Hybrid Per Instance (3) or DOTS cannot write it per prism"
        assert any(r["m_Id"] == p["m_ObjectId"] for r in graph["m_Properties"]), f"{name} not in m_Properties"
        assert any(
            any(c["m_Id"] == p["m_ObjectId"] for c in idx[cat["m_Id"]]["m_ChildObjectList"])
            for cat in graph["m_CategoryData"]
        ), f"{name} not in any blackboard category"

    cf = find_cf(docs, FUNCTION_NAME)
    assert cf is not None, f"{FUNCTION_NAME} custom function node missing"
    assert any(r["m_Id"] == cf["m_ObjectId"] for r in graph["m_Nodes"]), \
        f"{FUNCTION_NAME} not registered in m_Nodes"
    assert cf["m_FunctionSource"] == HLSL_GUID, "custom function points at the wrong HLSL asset"
    cf_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
    assert set(cf_slots) == {s[0] for s in CF_SLOTS}, "custom function slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CF_SLOTS:
        assert cf_slots[slot_id]["m_DisplayName"] == name, f"slot {slot_id} name drifted"
        assert cf_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"slot {slot_id} direction wrong"

    sources = edge_sources(graph)
    fed = set(sources)
    for slot_id, name, _kind, is_output in CF_SLOTS:
        if not is_output:
            assert (cf["m_ObjectId"], slot_id) in fed, f"custom function input '{name}' is unconnected"

    # --- the position splice: the jiggle sits in FRONT of the grow scale ---
    mul, mul_a = find_grow_multiply(docs, graph, idx)
    a_src = sources.get((mul["m_ObjectId"], mul_a))
    assert a_src == (cf["m_ObjectId"], 6), \
        "the grow Multiply's A input is not fed by PrismJiggleClock.OutPosition"
    # ...and the graph's ORIGINAL vertex source was RETARGETED into it, not dropped, so the
    # subgraph/spread/shatter chain that used to feed the Multiply still applies.
    pos_in = sources.get((cf["m_ObjectId"], 4))
    assert pos_in is not None, "PrismJiggleClock.Position is unconnected — the original vertex source was dropped"
    assert pos_in[0] != cf["m_ObjectId"], "PrismJiggleClock.Position is fed by the splice itself"

    # --- the normal splice ---
    nrm_block = find_block(docs, VERTEX_NORMAL_BLOCK)
    assert nrm_block, "VertexDescription.Normal block missing"
    nrm_src = sources.get((nrm_block["m_ObjectId"], 0))
    assert nrm_src == (cf["m_ObjectId"], 7), \
        "VertexDescription.Normal is not fed by PrismJiggleClock.OutNormal"
    nrm_in = sources.get((cf["m_ObjectId"], 5))
    assert nrm_in is not None, "PrismJiggleClock.Normal is unconnected"
    assert nrm_in[0] != cf["m_ObjectId"], "PrismJiggleClock.Normal is fed by the splice itself"
    # Whatever feeds it must be OBJECT space: the vertex Normal block's slot is object space
    # (m_Space 0), so a world-space normal here silently rotates the wrong basis.
    nrm_feeder = idx[nrm_in[0]]
    if "NormalVectorNode" in nrm_feeder.get("m_Type", ""):
        assert nrm_feeder.get("m_Space") == OBJECT_SPACE, \
            "PrismJiggleClock.Normal is fed by a NON-object-space NormalVector node"


# ---------------------------------------------------------------------------
# the edit
# ---------------------------------------------------------------------------

def already_wired(docs):
    return find_cf(docs, FUNCTION_NAME) is not None


def wire(path):
    docs = load_docs(os.path.join(REPO, path))
    validate(docs, expect_wired=False)

    if already_wired(docs):
        validate(docs, expect_wired=True)
        print(f"  {os.path.basename(path)}: already wired")
        return False

    graph = find_graph(docs)
    idx = index(docs)

    # ---- donors, all schema-exact by construction -------------------------
    clock_prop = find_property(docs, CLOCK_PROP_NAME)
    assert clock_prop, f"{CLOCK_PROP_NAME} property not found — wire the clock properties first"
    grow_start = find_property(docs, "GrowStartTime")
    grow_frac = find_property(docs, "GrowStartFrac")
    assert grow_start and grow_frac, \
        "GrowStartTime/GrowStartFrac not found — this graph has not had the clock properties wired"

    donor_property_node = find_node_by_type(docs, "ShaderGraph.PropertyNode")
    assert donor_property_node, "no PropertyNode donor"
    donor_cf = find_cf(docs, GROW_FUNCTION)
    assert donor_cf, f"no {GROW_FUNCTION} CustomFunctionNode donor"

    cf_slot_docs = [idx[s["m_Id"]] for s in donor_cf["m_Slots"]]
    donor_slot_v1 = next(s for s in cf_slot_docs if "Vector1MaterialSlot" in s["m_Type"])
    donor_slot_v3 = next(s for s in cf_slot_docs if "Vector3MaterialSlot" in s["m_Type"])

    new_docs = []

    # ---- properties -------------------------------------------------------
    prop_oids = {}
    host = next(idx[c["m_Id"]] for c in graph["m_CategoryData"]
                if any(ch["m_Id"] == grow_start["m_ObjectId"]
                       for ch in idx[c["m_Id"]]["m_ChildObjectList"]))
    for name, reference, kind in JIGGLE_PROPS:
        donor = grow_frac if kind == "Vector3" else grow_start
        p = make_per_instance_property(donor, name, reference)
        prop_oids[name] = p["m_ObjectId"]
        new_docs.append(p)
        graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
        host["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})

    # ---- property nodes ---------------------------------------------------
    base_x, base_y = -2600.0, 2400.0
    node_oids = {}
    for i, (name, _ref, kind) in enumerate(JIGGLE_PROPS):
        donor_slot = donor_slot_v3 if kind == "Vector3" else donor_slot_v1
        node, slots = make_property_node(donor_property_node, donor_slot, prop_oids[name],
                                         name, base_x, base_y + i * 80.0)
        node_oids[name] = node["m_ObjectId"]
        new_docs.append(node)
        new_docs.extend(slots)
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    clock_node, clock_slots = make_property_node(donor_property_node, donor_slot_v1,
                                                 clock_prop["m_ObjectId"], CLOCK_PROP_NAME,
                                                 base_x, base_y - 80.0)
    new_docs.append(clock_node)
    new_docs.extend(clock_slots)
    graph["m_Nodes"].append({"m_Id": clock_node["m_ObjectId"]})

    # ---- the custom function ---------------------------------------------
    cf, cf_slots = make_custom_function_node(donor_cf, donor_slot_v1, donor_slot_v3,
                                             base_x + 320.0, base_y)
    new_docs.append(cf)
    new_docs.extend(cf_slots)
    graph["m_Nodes"].append({"m_Id": cf["m_ObjectId"]})

    # ---- the position splice ---------------------------------------------
    # Retarget whatever fed the grow Multiply's A input into PrismJiggleClock.Position.
    # Exactly one edge feeds it (asserted by validate()), so this is a single rewrite.
    mul, mul_a = find_grow_multiply(docs, graph, idx)
    retargeted = 0
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == mul["m_ObjectId"] and i["m_SlotId"] == mul_a:
            i["m_Node"]["m_Id"] = cf["m_ObjectId"]
            i["m_SlotId"] = 4
            retargeted += 1
    assert retargeted == 1, f"expected exactly 1 edge into the grow Multiply's A, retargeted {retargeted}"

    # ---- the normal splice ------------------------------------------------
    # Where something already drives VertexDescription.Normal (ExplodingBlockGraph's shatter
    # rotation), retarget it so the jiggle composes on top. Where nothing does (BlockGraph),
    # mint an OBJECT-space NormalVector node — the block's slot is object space, and the
    # graph's existing NormalVector node is WORLD space (it feeds the back-face fade in the
    # fragment stage), so it cannot be reused as-is.
    nrm_block = find_block(docs, VERTEX_NORMAL_BLOCK)
    assert nrm_block, "VertexDescription.Normal block missing"
    normal_retargeted = 0
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == nrm_block["m_ObjectId"] and i["m_SlotId"] == 0:
            i["m_Node"]["m_Id"] = cf["m_ObjectId"]
            i["m_SlotId"] = 5
            normal_retargeted += 1
    assert normal_retargeted <= 1, "VertexDescription.Normal had more than one feeder"

    if normal_retargeted == 0:
        donor_normal = find_node_by_type(docs, "ShaderGraph.NormalVectorNode")
        assert donor_normal, "no NormalVectorNode donor in this graph"
        donor_normal_slots = [idx[s["m_Id"]] for s in donor_normal["m_Slots"]]
        nrm_node, nrm_slots = make_object_normal_node(donor_normal, donor_normal_slots,
                                                      base_x, base_y + 260.0)
        new_docs.append(nrm_node)
        new_docs.extend(nrm_slots)
        graph["m_Nodes"].append({"m_Id": nrm_node["m_ObjectId"]})
        normal_out_slot = next(idx_s["m_Id"] for idx_s in nrm_slots if idx_s["m_SlotType"] == 1)
        graph["m_Edges"].append(edge(nrm_node["m_ObjectId"], normal_out_slot, cf["m_ObjectId"], 5))

    # ---- remaining edges --------------------------------------------------
    graph["m_Edges"].extend([
        edge(clock_node["m_ObjectId"], 0, cf["m_ObjectId"], 0),
        edge(node_oids["JiggleStartTime"], 0, cf["m_ObjectId"], 1),
        edge(node_oids["JiggleDuration"], 0, cf["m_ObjectId"], 2),
        edge(node_oids["JiggleParams"], 0, cf["m_ObjectId"], 3),
        edge(cf["m_ObjectId"], 6, mul["m_ObjectId"], mul_a),
        edge(cf["m_ObjectId"], 7, nrm_block["m_ObjectId"], 0),
    ])

    docs.extend(new_docs)
    validate(docs, expect_wired=True)

    open(os.path.join(REPO, path), "w", encoding="utf-8").write(dump_docs(docs))
    print(f"  {os.path.basename(path)}: wired "
          f"(+{len(JIGGLE_PROPS)} properties, +{len(new_docs)} objects, "
          f"normal {'retargeted' if normal_retargeted else 'minted'})")
    return True


def check(path):
    docs = load_docs(os.path.join(REPO, path))
    validate(docs, expect_wired=False)
    if not already_wired(docs):
        print(f"  {os.path.basename(path)}: NOT wired")
        return False
    validate(docs, expect_wired=True)
    print(f"  {os.path.basename(path)}: wired ✅")
    return True


def main():
    check_only = "--check" in sys.argv
    print(f"{'Checking' if check_only else 'Wiring'} the super-shield jiggle clock "
          f"({FUNCTION_NAME}) into {len(GRAPHS)} graphs:")
    ok = True
    for path in GRAPHS:
        if check_only:
            ok &= check(path)
        else:
            wire(path)
    if check_only and not ok:
        print("NOT fully wired — run without --check.")
        return 1
    print("done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
