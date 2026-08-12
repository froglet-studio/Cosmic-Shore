#!/usr/bin/env python3
"""
Wire the ballistic FLIGHT clock into every shader graph a LIVE prism can render with.

Docs/PRISM_ANIMATION.md §5 C5. The Sparrow's Turret Stance fires prisms; before this,
their flight was a per-frame CPU transform write, which is exactly what the clock-material
law forbids. After it, the prism's entity transform is FINAL at the flight's end point
from the stamp and the vertex stage walks the visual in from the muzzle off three
per-instance properties — zero CPU writes between the stamp and the anchor.

Coverage is the same census as the occlusion corridor, for the same reason: a live prism
can render with BlockGraph OR ExplodingBlockGraph (TransparentPrismMaterial and
MazeDangerBlockMateral rest on the latter), so wiring one graph leaves a hole. SuctionGraph
is excluded — it renders mass being consumed, never mass in flight.

What it adds to each graph:

  properties (all three HYBRID PER INSTANCE — hlslDeclarationOverride 3 — because they
  are per-prism initial conditions, unlike the corridor's per-frame globals):
      _FlightStartTime  float   clock value at the muzzle
      _FlightDuration   float   seconds of flight; 0 = unstamped = render at the transform
      _FlightVelocity   float3  WORLD-space muzzle velocity (units/s)

  nodes:
      Property x3                         -> the three above
      PrismFlightClock (Custom Function)  -> PrismClockAnimation.hlsl
      Add                                 -> splices the offset into the vertex position

  edges (the splice — whatever fed VertexDescription.Position is RETARGETED into the new
  Add's A input, so the graph's existing vertex chain still applies and the flight only
  translates it; that is the grow Multiply on BlockGraph and the explosion Add on
  ExplodingBlockGraph):
      BEFORE:  <vertex source> ---------------------> VertexDescription.Position
      AFTER:   <vertex source> -> Add.A
               PrismFlightClock.ObjectOffset -> Add.B
               Add.Out ---------------------------> VertexDescription.Position

Out-of-editor ShaderGraph JSON synthesis per the /asset-surgery protocol: parse the whole
file, clone same-file (or cross-file) donors so the schema is exact by construction,
rebuild in memory, assert every invariant, and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0.

Usage:  python3 Tools/Shaders/wire_prism_flight_clock.py [--check]
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
# BlockGraph contains no AddNode to clone; ExplodingBlockGraph's explosion-offset Add is
# the same ShaderGraph version, so the schema is exact by construction.
ADD_DONOR = "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph"

# GUID of Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl, pinned by its
# committed .meta so this reference can never drift.
HLSL_GUID = "e3f9a1c27b8d4e05b6a4c9d1f0527a83"
FUNCTION_NAME = "PrismFlightClock"

CLOCK_PROP_NAME = "PrismClock"  # the existing unexposed global, reused as the Clock feed

# (display name, reference name, "Vector1"|"Vector3")
FLIGHT_PROPS = [
    ("FlightStartTime", "_FlightStartTime", "Vector1"),
    ("FlightDuration", "_FlightDuration", "Vector1"),
    ("FlightVelocity", "_FlightVelocity", "Vector3"),
]

# (integer slot id, display name, "Vector1"|"Vector3", is_output) — the integer ids MUST
# match the HLSL parameter order (every input first, then every output).
CF_SLOTS = [
    (0, "Clock", "Vector1", False),
    (1, "StartTime", "Vector1", False),
    (2, "Duration", "Vector1", False),
    (3, "Velocity", "Vector3", False),
    (4, "ObjectOffset", "Vector3", True),
]

VERTEX_POSITION_BLOCK = "VertexDescription.Position"

# ---- stage 2: flight-corrected camera distance (BlockGraph only) ------------
# The distance-driven look (Prism Sub Graph's spread chain) read
# SqrDistanceSubGraph = dot(pivot - camera, .), and a flying prism's pivot is
# parked at the flight END POINT - so a turret shot rendered its full-range
# spread from frame one. PrismFlightSqrDistance re-measures from the DISPLACED
# pivot; unstamped (Duration 0) it reduces to the old value exactly, so every
# other prism renders identically and the retired subgraph node is removed.
SQRDIST_FUNCTION = "PrismFlightSqrDistance"
SQRDIST_SUBGRAPH_GUID = "9a1c93633fd560847bde596dbf7353ab"  # SqrDistanceSubGraph.shadersubgraph
NODE_DONOR_FILE = "Assets/_Graphics/Materials/Graphs/SqrDistanceSubGraph.shadersubgraph"

SQRDIST_CF_SLOTS = [
    (0, "Clock", "Vector1", False),
    (1, "StartTime", "Vector1", False),
    (2, "Duration", "Vector1", False),
    (3, "Velocity", "Vector3", False),
    (4, "ObjectPosition", "Vector3", False),
    (5, "CameraPosition", "Vector3", False),
    (6, "SqrDistance", "Vector1", True),
]


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


# ---------------------------------------------------------------------------
# builders (every one clones a donor of the same type)
# ---------------------------------------------------------------------------

def make_per_instance_property(donor, name, reference):
    """Clone a donor ShaderProperty of the right type and re-key it as a fresh
    Hybrid-Per-Instance property. The donor is always an existing clock property, so
    the exposure flags are already correct and are asserted afterwards."""
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
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 232.0, "height": 350.0})
    slots = []
    for slot_id, name, kind, is_output in CF_SLOTS:
        donor = donor_slot_v3 if kind == "Vector3" else donor_slot_v1
        slots.append(make_slot(donor, slot_id, name, is_output))
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def make_add_node(donor_add, donor_add_slots, x, y):
    node = json.loads(json.dumps(donor_add))
    node["m_ObjectId"] = new_oid()
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 208.0, "height": 302.0})
    slots = []
    for donor_slot in donor_add_slots:
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
        cd = idx[cat["m_Id"]]
        for child in cd["m_ChildObjectList"]:
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
        for end, label in ((o, "output"), (i, "input")):
            nid = end["m_Node"]["m_Id"]
            assert nid in slot_ids, f"edge {label} node {nid} not registered in m_Nodes"
            assert end["m_SlotId"] in slot_ids[nid], f"edge {label} slot {end['m_SlotId']} missing on {nid}"
        assert slot_ids[o["m_Node"]["m_Id"]][o["m_SlotId"]]["m_SlotType"] == 1, "edge output is not an output slot"
        assert slot_ids[i["m_Node"]["m_Id"]][i["m_SlotId"]]["m_SlotType"] == 0, "edge input is not an input slot"
        key = (i["m_Node"]["m_Id"], i["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
    for key, count in feeders.items():
        assert count == 1, f"input slot {key} has {count} feeders (must be exactly 1)"

    if not expect_wired:
        return

    for name, reference, _kind in FLIGHT_PROPS:
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

    cf = next((idx[r["m_Id"]] for r in graph["m_Nodes"]
               if idx[r["m_Id"]].get("m_FunctionName") == FUNCTION_NAME), None)
    assert cf is not None, f"{FUNCTION_NAME} custom function node missing"
    assert cf["m_FunctionSource"] == HLSL_GUID, "custom function points at the wrong HLSL asset"
    cf_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
    assert set(cf_slots) == {s[0] for s in CF_SLOTS}, "custom function slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CF_SLOTS:
        assert cf_slots[slot_id]["m_DisplayName"] == name, f"slot {slot_id} name drifted"
        assert cf_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"slot {slot_id} direction wrong"

    fed = {(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]) for e in graph["m_Edges"]}
    for slot_id, name, _kind, is_output in CF_SLOTS:
        if not is_output:
            assert (cf["m_ObjectId"], slot_id) in fed, f"custom function input '{name}' is unconnected"

    pos_block = find_block(docs, VERTEX_POSITION_BLOCK)
    assert pos_block, "VertexDescription.Position block missing"
    sources = {}
    for e in graph["m_Edges"]:
        sources[(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])] = (
            e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])

    pos_src = sources.get((pos_block["m_ObjectId"], 0))
    assert pos_src is not None, "VertexDescription.Position is unconnected"
    add = idx[pos_src[0]]
    assert "AddNode" in add.get("m_Type", ""), \
        "VertexDescription.Position is not fed by the flight Add node"
    assert pos_src[1] == 2, "the Add node's Out slot is not the one feeding Position"

    # The graph's ORIGINAL vertex source was RETARGETED into Add.A, not dropped and not
    # duplicated — so the existing vertex chain (grow bloom, shatter rotation, explosion
    # offset) still applies and the flight only translates the result.
    assert (add["m_ObjectId"], 0) in sources, \
        "Add.A is unconnected — the graph's original vertex source was dropped"
    a_src = sources[(add["m_ObjectId"], 0)]
    assert a_src[0] not in (add["m_ObjectId"], cf["m_ObjectId"]), "Add.A is fed by the splice itself"
    assert sources.get((add["m_ObjectId"], 1)) == (cf["m_ObjectId"], 4), \
        "Add.B is not fed by PrismFlightClock.ObjectOffset"


def find_subgraph_node(docs, guid):
    for d in docs:
        if "SubGraphNode" in d.get("m_Type", "") and guid in json.dumps(d.get("m_SubGraph", "")) + d.get("m_SerializedSubGraph", ""):
            return d
    return None


def validate_sqrdist(docs):
    """Stage-2 invariants for a graph that carries the distance-spread chain."""
    idx = index(docs)
    graph = find_graph(docs)

    old = find_subgraph_node(docs, SQRDIST_SUBGRAPH_GUID)
    assert old is None, "the retired SqrDistanceSubGraph node is still present"

    cf = find_cf(docs, SQRDIST_FUNCTION)
    assert cf is not None, f"{SQRDIST_FUNCTION} custom function node missing"
    assert cf["m_FunctionSource"] == HLSL_GUID, f"{SQRDIST_FUNCTION} points at the wrong HLSL asset"
    cf_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
    assert set(cf_slots) == {s[0] for s in SQRDIST_CF_SLOTS}, \
        f"{SQRDIST_FUNCTION} slot ids do not match the HLSL signature"

    fed = {(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]) for e in graph["m_Edges"]}
    for slot_id, name, _kind, is_output in SQRDIST_CF_SLOTS:
        if not is_output:
            assert (cf["m_ObjectId"], slot_id) in fed, f"{SQRDIST_FUNCTION} input '{name}' is unconnected"

    # Its output must actually carry the distance into the prism subgraph.
    assert any(e["m_OutputSlot"]["m_Node"]["m_Id"] == cf["m_ObjectId"] and e["m_OutputSlot"]["m_SlotId"] == 6
               for e in graph["m_Edges"]), f"{SQRDIST_FUNCTION}.SqrDistance feeds nothing"


# ---------------------------------------------------------------------------
# the edit
# ---------------------------------------------------------------------------

def already_wired(docs):
    return find_cf(docs, FUNCTION_NAME) is not None


def wire(path, add_donor_docs):
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
    donor_cf = find_cf(docs, "PrismGrowScale")
    assert donor_cf, "no PrismGrowScale CustomFunctionNode donor"

    cf_slot_docs = [idx[s["m_Id"]] for s in donor_cf["m_Slots"]]
    donor_slot_v1 = next(s for s in cf_slot_docs if "Vector1MaterialSlot" in s["m_Type"])
    donor_slot_v3 = next(s for s in cf_slot_docs if "Vector3MaterialSlot" in s["m_Type"])

    donor_add = find_node_by_type(add_donor_docs, "ShaderGraph.AddNode")
    assert donor_add, "no AddNode donor in the donor graph"
    add_donor_idx = index(add_donor_docs)
    donor_add_slots = [add_donor_idx[s["m_Id"]] for s in donor_add["m_Slots"]]
    assert len(donor_add_slots) == 3, "AddNode donor does not have 3 slots"

    new_docs = []

    # ---- properties -------------------------------------------------------
    prop_oids = {}
    for name, reference, kind in FLIGHT_PROPS:
        donor = grow_frac if kind == "Vector3" else grow_start
        p = make_per_instance_property(donor, name, reference)
        prop_oids[name] = p["m_ObjectId"]
        new_docs.append(p)
        graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
        # Blackboard: the category the clock properties already live in.
        host = next(idx[c["m_Id"]] for c in graph["m_CategoryData"]
                    if any(ch["m_Id"] == grow_start["m_ObjectId"]
                           for ch in idx[c["m_Id"]]["m_ChildObjectList"]))
        host["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})

    # ---- property nodes ---------------------------------------------------
    base_x, base_y = -2600.0, 1900.0
    node_oids = {}
    for i, (name, _ref, kind) in enumerate(FLIGHT_PROPS):
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

    # ---- the Add that splices the offset into the vertex position ---------
    add, add_slots = make_add_node(donor_add, donor_add_slots, base_x + 640.0, base_y)
    new_docs.append(add)
    new_docs.extend(add_slots)
    graph["m_Nodes"].append({"m_Id": add["m_ObjectId"]})

    # ---- edges ------------------------------------------------------------
    pos_block = find_block(docs, VERTEX_POSITION_BLOCK)
    assert pos_block, "VertexDescription.Position block missing"

    # Retarget whatever fed Position into Add.A. Exactly one edge feeds it (asserted
    # by validate()), so this is a single rewrite, never a drop.
    retargeted = 0
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == pos_block["m_ObjectId"] and i["m_SlotId"] == 0:
            i["m_Node"]["m_Id"] = add["m_ObjectId"]
            i["m_SlotId"] = 0
            retargeted += 1
    assert retargeted == 1, f"expected exactly 1 edge into Position, retargeted {retargeted}"

    graph["m_Edges"].extend([
        edge(clock_node["m_ObjectId"], 0, cf["m_ObjectId"], 0),
        edge(node_oids["FlightStartTime"], 0, cf["m_ObjectId"], 1),
        edge(node_oids["FlightDuration"], 0, cf["m_ObjectId"], 2),
        edge(node_oids["FlightVelocity"], 0, cf["m_ObjectId"], 3),
        edge(cf["m_ObjectId"], 4, add["m_ObjectId"], 1),
        edge(add["m_ObjectId"], 2, pos_block["m_ObjectId"], 0),
    ])

    docs.extend(new_docs)
    validate(docs, expect_wired=True)

    open(os.path.join(REPO, path), "w", encoding="utf-8").write(dump_docs(docs))
    print(f"  {os.path.basename(path)}: wired "
          f"(+{len(FLIGHT_PROPS)} properties, +{len(new_docs)} objects)")
    return True


def wire_sqrdistance(path):
    """Stage 2, applied only to graphs that carry the distance-spread chain:
    replace the pivot-based SqrDistanceSubGraph feed with PrismFlightSqrDistance
    (the same distance, measured from the flight-displaced pivot)."""
    docs = load_docs(os.path.join(REPO, path))
    validate(docs, expect_wired=False)

    if find_cf(docs, SQRDIST_FUNCTION) is not None:
        validate_sqrdist(docs)
        print(f"  {os.path.basename(path)}: distance stage already wired")
        return False

    old = find_subgraph_node(docs, SQRDIST_SUBGRAPH_GUID)
    if old is None:
        print(f"  {os.path.basename(path)}: no distance-spread chain — stage 2 not applicable")
        return False

    graph = find_graph(docs)
    idx = index(docs)

    flight_cf = find_cf(docs, FUNCTION_NAME)
    assert flight_cf, "stage 1 (PrismFlightClock) must be wired first"

    # The property nodes feeding the flight CF's four inputs are reused verbatim —
    # a property node's output legally fans out to any number of inputs.
    feeders = {}
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == flight_cf["m_ObjectId"]:
            feeders[i["m_SlotId"]] = e["m_OutputSlot"]
    for slot in (0, 1, 2, 3):
        assert slot in feeders, f"flight CF input {slot} has no feeder"

    # The one edge the old subgraph drives — capture its destination, then retarget.
    out_edges = [e for e in graph["m_Edges"]
                 if e["m_OutputSlot"]["m_Node"]["m_Id"] == old["m_ObjectId"]]
    assert len(out_edges) == 1, f"expected the SqrDistance subgraph to feed exactly 1 input, found {len(out_edges)}"
    dest = out_edges[0]["m_InputSlot"]

    # Cross-file donors for Object/Camera nodes (BlockGraph has neither; the
    # retired subgraph itself does, same serialization version by construction).
    donor_docs = load_docs(os.path.join(REPO, NODE_DONOR_FILE))
    donor_idx = index(donor_docs)

    def clone_node_with_slots(type_fragment, x, y):
        donor = find_node_by_type(donor_docs, type_fragment)
        assert donor, f"no {type_fragment} donor in {NODE_DONOR_FILE}"
        node = json.loads(json.dumps(donor))
        node["m_ObjectId"] = new_oid()
        node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 208.0, "height": 302.0})
        slots = []
        old_to_new = {}
        for ref in donor["m_Slots"]:
            s = json.loads(json.dumps(donor_idx[ref["m_Id"]]))
            old_to_new[s["m_ObjectId"]] = s["m_ObjectId"] = new_oid()
            slots.append(s)
        node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
        return node, slots

    new_docs = []
    base_x, base_y = -2600.0, 2400.0

    object_node, object_slots = clone_node_with_slots("ShaderGraph.ObjectNode", base_x, base_y)
    camera_node, camera_slots = clone_node_with_slots("ShaderGraph.CameraNode", base_x, base_y + 160.0)
    for node, slots in ((object_node, object_slots), (camera_node, camera_slots)):
        new_docs.append(node)
        new_docs.extend(slots)
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    # The CF, donor-cloned from the flight CF in this same graph.
    flight_slot_docs = [idx[s["m_Id"]] for s in flight_cf["m_Slots"]]
    donor_slot_v1 = next(s for s in flight_slot_docs if "Vector1MaterialSlot" in s["m_Type"])
    donor_slot_v3 = next(s for s in flight_slot_docs if "Vector3MaterialSlot" in s["m_Type"])
    cf = json.loads(json.dumps(flight_cf))
    cf["m_ObjectId"] = new_oid()
    cf["m_Name"] = f"{SQRDIST_FUNCTION} (Custom Function)"
    cf["m_FunctionName"] = SQRDIST_FUNCTION
    cf["m_DrawState"]["m_Position"].update({"x": base_x + 320.0, "y": base_y, "width": 232.0, "height": 350.0})
    cf_slots = []
    for slot_id, name, kind, is_output in SQRDIST_CF_SLOTS:
        donor = donor_slot_v3 if kind == "Vector3" else donor_slot_v1
        cf_slots.append(make_slot(donor, slot_id, name, is_output))
    cf["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in cf_slots]
    new_docs.append(cf)
    new_docs.extend(cf_slots)
    graph["m_Nodes"].append({"m_Id": cf["m_ObjectId"]})

    # Retarget the subgraph's one edge onto the CF output, feed the CF's inputs.
    out_edges[0]["m_OutputSlot"] = {"m_Node": {"m_Id": cf["m_ObjectId"]}, "m_SlotId": 6}
    for slot in (0, 1, 2, 3):
        graph["m_Edges"].append({"m_OutputSlot": dict(feeders[slot]),
                                 "m_InputSlot": {"m_Node": {"m_Id": cf["m_ObjectId"]}, "m_SlotId": slot}})
    graph["m_Edges"].append(edge(object_node["m_ObjectId"], 0, cf["m_ObjectId"], 4))
    graph["m_Edges"].append(edge(camera_node["m_ObjectId"], 0, cf["m_ObjectId"], 5))

    # Retire the subgraph node: drop it from m_Nodes and delete its documents.
    graph["m_Nodes"] = [r for r in graph["m_Nodes"] if r["m_Id"] != old["m_ObjectId"]]
    dead = {old["m_ObjectId"]} | {s["m_Id"] for s in old.get("m_Slots", [])}
    docs = [d for d in docs if d.get("m_ObjectId") not in dead]

    docs.extend(new_docs)
    validate(docs, expect_wired=True)
    validate_sqrdist(docs)

    open(os.path.join(REPO, path), "w", encoding="utf-8").write(dump_docs(docs))
    print(f"  {os.path.basename(path)}: distance stage wired (+{len(new_docs)} objects, "
          f"SqrDistanceSubGraph node retired)")
    return True


def main():
    check_only = "--check" in sys.argv
    add_donor_docs = load_docs(os.path.join(REPO, ADD_DONOR))

    if check_only:
        ok = True
        for path in GRAPHS:
            docs = load_docs(os.path.join(REPO, path))
            try:
                validate(docs, expect_wired=True)
                # Stage 2 is only applicable where the distance-spread chain exists:
                # a leftover retired subgraph OR a wired CF both trigger validation.
                if find_cf(docs, SQRDIST_FUNCTION) is not None or \
                   find_subgraph_node(docs, SQRDIST_SUBGRAPH_GUID) is not None:
                    validate_sqrdist(docs)
                print(f"  {os.path.basename(path)}: OK")
            except AssertionError as exc:
                print(f"  {os.path.basename(path)}: NOT WIRED — {exc}")
                ok = False
        return 0 if ok else 1

    changed = False
    for path in GRAPHS:
        changed |= wire(path, add_donor_docs)
    for path in GRAPHS:
        changed |= wire_sqrdistance(path)
    print("done" if changed else "nothing to do")
    return 0


if __name__ == "__main__":
    sys.exit(main())
