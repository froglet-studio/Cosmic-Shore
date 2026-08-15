#!/usr/bin/env python3
"""
Wire the SHIELD MORPH clock into every shader graph a live prism can render with.

Docs/PRISM_ANIMATION.md §5 B4. The octahedron shield's engage bloom and its shatter
overlay (and the stellated super-shield's twin pair) were the last per-frame CPU prism
animation: PrismOctahedronShieldManager ticked every morphing shield and each one
REBUILT A MESH per frame, on the un-batched GameObject renderer. After this, both are
pure functions of the shader clock evaluated on the cache-SHARED settled shield mesh —
so a shielded prism never leaves the instanced path and same-size shields stay in one
batch through the whole animation.

Coverage is the same census as the flight clock and the occlusion corridor, for the
same reason: a live prism can render with BlockGraph OR ExplodingBlockGraph
(TransparentPrismMaterial and MazeDangerBlockMateral rest on the latter), so wiring
one graph leaves a hole. SuctionGraph is excluded — it renders mass being consumed,
which is never shielded.

What it adds to each graph:

  properties (all four HYBRID PER INSTANCE — hlslDeclarationOverride 3 — per-prism
  initial conditions, exactly like the grow/flight stamps):
      _ShieldMorphStartTime  float  clock value at the transition's start
      _ShieldMorphDuration   float  seconds; 0 = unstamped = render the mesh as authored
      _ShieldMorphDirection  float  >= 0 engage bloom, < 0 shatter
      _ShieldMorphOffset     float  shatter fly-out distance in LOCAL units (bloom: 0)

  nodes:
      Property x4 + a PrismClock property node
      UV (channel UV1)                    -> per-vertex FACE CENTROID, baked by
                                             Octahedron/StellatedOctahedronMeshGenerator
      Normal Vector (OBJECT space)        -> the flat per-face normal (shatter fly-out axis)
      PrismShieldMorph (Custom Function)  -> PrismClockAnimation.hlsl

  edges (the splice — the Prism Sub Graph's Out_Vector3, i.e. the RAW object-space
  vertex position at the head of the vertex chain, is retargeted through the morph so
  everything downstream — the explosion's face rotation, the grow scale, the explosion
  offset, the ballistic flight — applies to the MORPHED shape rather than fighting it):
      BEFORE:  PrismSubGraph.Out_Vector3 ------------------> <first vertex consumer>
      AFTER:   PrismSubGraph.Out_Vector3 -> PrismShieldMorph.Position
               UV1 -> .FaceCentroid, NormalOS -> .Normal, clock + 4 props -> the rest
               PrismShieldMorph.MorphedPosition ----------> <first vertex consumer>

Out-of-editor ShaderGraph JSON synthesis per the /asset-surgery protocol: parse the whole
file, clone same-file (or cross-file) donors so the schema is exact by construction,
rebuild in memory, assert every invariant, and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0.

Usage:  python3 Tools/Shaders/wire_prism_shield_morph.py [--check]
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
# BlockGraph carries no UVNode to clone; ExplodingBlockGraph's is the same ShaderGraph
# serialization version, so the schema is exact by construction (the same cross-file
# donor pattern wire_prism_flight_clock.py uses for its AddNode).
UV_DONOR = "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph"

# GUID of Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl, pinned by its
# committed .meta so this reference can never drift.
HLSL_GUID = "e3f9a1c27b8d4e05b6a4c9d1f0527a83"
FUNCTION_NAME = "PrismShieldMorph"

CLOCK_PROP_NAME = "PrismClock"  # the existing unexposed global, reused as the Clock feed

# GUID of Prism Sub Graph — the node whose Out_Vector3 is the head of the vertex chain
# in BOTH graphs. Matched by GUID, not by display name, so a rename cannot silently
# splice the morph into the wrong place.
PRISM_SUBGRAPH_GUID = "b93c63013c15a264f81ca731f29b9191"
PRISM_SUBGRAPH_POSITION_SLOT = 1  # Out_Vector3

# ShaderGraph CoordinateSpace enum: Object = 0 (World is 2 — the value the existing
# Normal Vector donors carry, since they feed the world-space back-face fade).
COORDINATE_SPACE_OBJECT = 0
# ShaderGraph UVChannel enum: UV0 = 0, UV1 = 1. Must match
# OctahedronMeshGenerator.FaceCentroidUVChannel.
UV_CHANNEL_UV1 = 1

# (display name, reference name)
SHIELD_PROPS = [
    ("ShieldMorphStartTime", "_ShieldMorphStartTime"),
    ("ShieldMorphDuration", "_ShieldMorphDuration"),
    ("ShieldMorphDirection", "_ShieldMorphDirection"),
    ("ShieldMorphOffset", "_ShieldMorphOffset"),
]

# (integer slot id, display name, "Vector1"|"Vector3", is_output) — the integer ids MUST
# match the HLSL parameter order (every input first, then every output).
CF_SLOTS = [
    (0, "Clock", "Vector1", False),
    (1, "StartTime", "Vector1", False),
    (2, "Duration", "Vector1", False),
    (3, "Direction", "Vector1", False),
    (4, "ShatterOffset", "Vector1", False),
    (5, "Position", "Vector3", False),
    (6, "Normal", "Vector3", False),
    (7, "FaceCentroid", "Vector3", False),
    (8, "MorphedPosition", "Vector3", True),
]

CF_OUTPUT_SLOT = 8


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


def find_prism_subgraph(docs):
    for d in docs:
        if "SubGraphNode" in d.get("m_Type", "") and \
                PRISM_SUBGRAPH_GUID in str(d.get("m_SerializedSubGraph", "")):
            return d
    return None


# ---------------------------------------------------------------------------
# builders (every one clones a donor of the same type)
# ---------------------------------------------------------------------------

def make_per_instance_property(donor, name, reference):
    """Clone a donor Vector1 ShaderProperty and re-key it as a fresh Hybrid-Per-Instance
    property. The donor is always an existing clock property, so the exposure flags are
    already correct — and they are asserted afterwards anyway."""
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
    # Settled default: Duration 0 means "unstamped" in PrismShieldMorph_float, so an
    # untouched material renders the mesh exactly as authored.
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
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 232.0, "height": 460.0})
    slots = []
    for slot_id, name, kind, is_output in CF_SLOTS:
        donor = donor_slot_v3 if kind == "Vector3" else donor_slot_v1
        slots.append(make_slot(donor, slot_id, name, is_output))
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def clone_simple_node(donor, donor_idx, x, y, **overrides):
    """Clone a zero-input node (UV / Normal Vector) with fresh ids for it and its slots."""
    node = json.loads(json.dumps(donor))
    node["m_ObjectId"] = new_oid()
    node["m_Group"] = {"m_Id": ""}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 208.0, "height": 302.0})
    node.update(overrides)
    slots = []
    for ref in donor["m_Slots"]:
        s = json.loads(json.dumps(donor_idx[ref["m_Id"]]))
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

    for name, reference in SHIELD_PROPS:
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
    assert cf["m_FunctionSource"] == HLSL_GUID, "custom function points at the wrong HLSL asset"
    cf_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
    assert set(cf_slots) == {s[0] for s in CF_SLOTS}, "custom function slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CF_SLOTS:
        assert cf_slots[slot_id]["m_DisplayName"] == name, f"slot {slot_id} name drifted"
        assert cf_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"slot {slot_id} direction wrong"

    sources = {}
    for e in graph["m_Edges"]:
        sources[(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])] = (
            e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])

    for slot_id, name, _kind, is_output in CF_SLOTS:
        if not is_output:
            assert (cf["m_ObjectId"], slot_id) in sources, f"custom function input '{name}' is unconnected"

    # The morph sits between the Prism Sub Graph's object-space position and whatever
    # consumed it: Position IS that output, and the old consumer now reads the morph.
    sub = find_prism_subgraph(docs)
    assert sub is not None, "Prism Sub Graph node not found — the splice anchor is gone"
    assert sources[(cf["m_ObjectId"], 5)] == (sub["m_ObjectId"], PRISM_SUBGRAPH_POSITION_SLOT), \
        "PrismShieldMorph.Position is not fed by Prism Sub Graph.Out_Vector3"

    consumers = [e for e in graph["m_Edges"]
                 if e["m_OutputSlot"]["m_Node"]["m_Id"] == cf["m_ObjectId"]
                 and e["m_OutputSlot"]["m_SlotId"] == CF_OUTPUT_SLOT]
    assert len(consumers) == 1, \
        f"PrismShieldMorph.MorphedPosition feeds {len(consumers)} inputs (expected exactly 1 — the retargeted vertex chain)"
    assert consumers[0]["m_InputSlot"]["m_Node"]["m_Id"] != cf["m_ObjectId"], "the morph feeds itself"

    # Out_Vector3 must ONLY feed the morph now — a surviving direct edge would mean the
    # un-morphed position is still driving part of the vertex chain.
    direct = [e for e in graph["m_Edges"]
              if e["m_OutputSlot"]["m_Node"]["m_Id"] == sub["m_ObjectId"]
              and e["m_OutputSlot"]["m_SlotId"] == PRISM_SUBGRAPH_POSITION_SLOT]
    assert len(direct) == 1 and direct[0]["m_InputSlot"]["m_Node"]["m_Id"] == cf["m_ObjectId"], \
        "Prism Sub Graph.Out_Vector3 still feeds something other than the morph"

    # The two per-vertex attribute feeds — the whole reason this can run on the SHARED
    # settled mesh instead of a per-prism rebuild.
    uv_node = idx[sources[(cf["m_ObjectId"], 7)][0]]
    assert "UVNode" in uv_node.get("m_Type", ""), "FaceCentroid is not fed by a UV node"
    assert uv_node.get("m_OutputChannel") == UV_CHANNEL_UV1, \
        "the FaceCentroid UV node is not reading UV1 (must match FaceCentroidUVChannel)"
    nrm_node = idx[sources[(cf["m_ObjectId"], 6)][0]]
    assert "NormalVectorNode" in nrm_node.get("m_Type", ""), "Normal is not fed by a Normal Vector node"
    assert nrm_node.get("m_Space") == COORDINATE_SPACE_OBJECT, \
        "the shatter Normal Vector node is not in OBJECT space (the morph is object-space arithmetic)"


# ---------------------------------------------------------------------------
# the edit
# ---------------------------------------------------------------------------

def already_wired(docs):
    return find_cf(docs, FUNCTION_NAME) is not None


def wire(path, uv_donor_docs):
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
    assert grow_start, \
        "GrowStartTime not found — this graph has not had the clock properties wired"

    donor_property_node = find_node_by_type(docs, "ShaderGraph.PropertyNode")
    assert donor_property_node, "no PropertyNode donor"
    donor_cf = find_cf(docs, "PrismGrowScale")
    assert donor_cf, "no PrismGrowScale CustomFunctionNode donor"

    cf_slot_docs = [idx[s["m_Id"]] for s in donor_cf["m_Slots"]]
    donor_slot_v1 = next(s for s in cf_slot_docs if "Vector1MaterialSlot" in s["m_Type"])
    donor_slot_v3 = next(s for s in cf_slot_docs if "Vector3MaterialSlot" in s["m_Type"])

    donor_normal = find_node_by_type(docs, "ShaderGraph.NormalVectorNode")
    assert donor_normal, "no NormalVectorNode donor in this graph"
    donor_uv = find_node_by_type(docs, "ShaderGraph.UVNode") or \
        find_node_by_type(uv_donor_docs, "ShaderGraph.UVNode")
    assert donor_uv, "no UVNode donor in this graph or the cross-file donor"
    uv_idx = idx if find_node_by_type(docs, "ShaderGraph.UVNode") else index(uv_donor_docs)

    sub = find_prism_subgraph(docs)
    assert sub, "Prism Sub Graph node not found — nothing to splice into"
    sub_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in sub["m_Slots"]}
    assert PRISM_SUBGRAPH_POSITION_SLOT in sub_slots, "Prism Sub Graph has no slot 1"
    assert sub_slots[PRISM_SUBGRAPH_POSITION_SLOT]["m_DisplayName"] == "Out_Vector3", \
        "Prism Sub Graph slot 1 is no longer Out_Vector3 — re-derive the splice anchor"

    new_docs = []

    # ---- properties -------------------------------------------------------
    prop_oids = {}
    for name, reference in SHIELD_PROPS:
        p = make_per_instance_property(grow_start, name, reference)
        prop_oids[name] = p["m_ObjectId"]
        new_docs.append(p)
        graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
        # Blackboard: the category the clock properties already live in.
        host = next(idx[c["m_Id"]] for c in graph["m_CategoryData"]
                    if any(ch["m_Id"] == grow_start["m_ObjectId"]
                           for ch in idx[c["m_Id"]]["m_ChildObjectList"]))
        host["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})

    # ---- property nodes ---------------------------------------------------
    base_x, base_y = -3200.0, 2700.0
    node_oids = {}
    for i, (name, _ref) in enumerate(SHIELD_PROPS):
        node, slots = make_property_node(donor_property_node, donor_slot_v1, prop_oids[name],
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

    # ---- per-vertex attribute feeds ---------------------------------------
    uv_node, uv_slots = clone_simple_node(donor_uv, uv_idx, base_x, base_y + 360.0,
                                          m_OutputChannel=UV_CHANNEL_UV1)
    normal_node, normal_slots = clone_simple_node(donor_normal, idx, base_x, base_y + 520.0,
                                                  m_Space=COORDINATE_SPACE_OBJECT)
    for node, slots in ((uv_node, uv_slots), (normal_node, normal_slots)):
        new_docs.append(node)
        new_docs.extend(slots)
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    # ---- the custom function ---------------------------------------------
    cf, cf_slots = make_custom_function_node(donor_cf, donor_slot_v1, donor_slot_v3,
                                             base_x + 340.0, base_y)
    new_docs.append(cf)
    new_docs.extend(cf_slots)
    graph["m_Nodes"].append({"m_Id": cf["m_ObjectId"]})

    # ---- the splice -------------------------------------------------------
    # Retarget every consumer of Out_Vector3 onto the morph's output, then feed the
    # morph from Out_Vector3. Exactly one consumer exists (asserted), so this is a
    # single rewrite — never a drop, never a duplicate.
    retargeted = 0
    for e in graph["m_Edges"]:
        o = e["m_OutputSlot"]
        if o["m_Node"]["m_Id"] == sub["m_ObjectId"] and o["m_SlotId"] == PRISM_SUBGRAPH_POSITION_SLOT:
            o["m_Node"]["m_Id"] = cf["m_ObjectId"]
            o["m_SlotId"] = CF_OUTPUT_SLOT
            retargeted += 1
    assert retargeted == 1, \
        f"expected exactly 1 consumer of Prism Sub Graph.Out_Vector3, retargeted {retargeted}"

    graph["m_Edges"].extend([
        edge(clock_node["m_ObjectId"], 0, cf["m_ObjectId"], 0),
        edge(node_oids["ShieldMorphStartTime"], 0, cf["m_ObjectId"], 1),
        edge(node_oids["ShieldMorphDuration"], 0, cf["m_ObjectId"], 2),
        edge(node_oids["ShieldMorphDirection"], 0, cf["m_ObjectId"], 3),
        edge(node_oids["ShieldMorphOffset"], 0, cf["m_ObjectId"], 4),
        edge(sub["m_ObjectId"], PRISM_SUBGRAPH_POSITION_SLOT, cf["m_ObjectId"], 5),
        edge(normal_node["m_ObjectId"], 0, cf["m_ObjectId"], 6),
        edge(uv_node["m_ObjectId"], 0, cf["m_ObjectId"], 7),
    ])

    docs.extend(new_docs)
    validate(docs, expect_wired=True)

    open(os.path.join(REPO, path), "w", encoding="utf-8").write(dump_docs(docs))
    print(f"  {os.path.basename(path)}: wired "
          f"(+{len(SHIELD_PROPS)} properties, +{len(new_docs)} objects)")
    return True


def main():
    check_only = "--check" in sys.argv
    uv_donor_docs = load_docs(os.path.join(REPO, UV_DONOR))

    if check_only:
        ok = True
        for path in GRAPHS:
            docs = load_docs(os.path.join(REPO, path))
            try:
                validate(docs, expect_wired=True)
                print(f"  {os.path.basename(path)}: OK")
            except AssertionError as exc:
                print(f"  {os.path.basename(path)}: NOT WIRED — {exc}")
                ok = False
        return 0 if ok else 1

    changed = False
    for path in GRAPHS:
        changed |= wire(path, uv_donor_docs)
    print("done" if changed else "nothing to do")
    return 0


if __name__ == "__main__":
    sys.exit(main())
