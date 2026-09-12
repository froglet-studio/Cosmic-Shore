#!/usr/bin/env python3
"""
Wire whole-prism GPU suction into every graph a live prism can render with.

Docs/PRISM_ANIMATION.md §5 C9. Cell.RequestCellSwap used to scale a retiring-world
root transform. Companion render entities ignore parent scale (§3.8 #1), so the
old world stood at full size then vanished at the drain. This splice lets a live
prism stamp PrismSuctionClock + a WORLD-space _Location and lerp its WHOLE mesh
toward that point — true convergence on the cell centre, not C8's in-place grow
collapse, and not SuctionGraph's SequentialFaceConverger (per-face consumption).

SuctionGraph is excluded on purpose: it already carries PrismSuctionClock feeding
SequentialFaceConverger. Do not splice PrismSuctionConverge there.

Coverage is the same census as the flight clock / jiggle / corridor: a live prism
can render with BlockGraph OR ExplodingBlockGraph. Wiring one leaves a hole.

What it adds to each graph:

  properties (HYBRID PER INSTANCE — hlslDeclarationOverride 3):
      _SuctionStartTime   float   clock value at stamp
      _SuctionDuration    float   seconds; 0 = unstamped = identity (LegacyState 0)
      _SuctionDirection   float   +1 implode 0→1 (cell-swap always +1)
      _SuctionGrowDelay   float   0 for cell-swap
      _Location           float3  WORLD-space convergence point

  nodes:
      Property x5                        -> the five above
      Property                           -> the existing unexposed _PrismClock global
      PrismSuctionClock (Custom Function)
      PrismSuctionConverge (Custom Function)

  edges (the splice sits LAST on VertexDescription.Position, after the flight Add,
  so grow / shield morph / jiggle / flight still apply and suction translates the
  result toward the cell centre):

      BEFORE:  <flight Add> ------------------------> VertexDescription.Position
      AFTER:   <flight Add> -> PrismSuctionConverge.Position
               PrismSuctionClock.State -> PrismSuctionConverge.State
               _Location               -> PrismSuctionConverge.WorldLocation
               PrismSuctionConverge.OutPosition -> VertexDescription.Position

  PrismSuctionClock.LegacyState is LEFT UNCONNECTED (slot default 0). Duration 0
  therefore returns State 0, so unstamped live prisms stay at rest. Do not wire a
  CPU-fed _State here — that is SuctionGraph's fallback, and putting it on live
  prisms would need PrismImplosionStateOverride on the Prism entity set.

Clock CF slot ids MUST match PrismSuctionClock_float parameter order.
Converge CF slot ids MUST match PrismSuctionConverge_float.

Out-of-editor ShaderGraph JSON synthesis per the /asset-surgery protocol: parse
the whole file, clone same-file donors so the schema is exact by construction,
rebuild in memory, assert every invariant, and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0.

Usage:  python3 Tools/Shaders/wire_prism_suction_clock.py [--check]
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

HLSL_GUID = "e3f9a1c27b8d4e05b6a4c9d1f0527a83"
CLOCK_FUNCTION = "PrismSuctionClock"
CONVERGE_FUNCTION = "PrismSuctionConverge"
GROW_FUNCTION = "PrismGrowScale"
CLOCK_PROP_NAME = "PrismClock"

# (display name, reference name, "Vector1"|"Vector3")
SUCTION_PROPS = [
    ("SuctionStartTime", "_SuctionStartTime", "Vector1"),
    ("SuctionDuration", "_SuctionDuration", "Vector1"),
    ("SuctionDirection", "_SuctionDirection", "Vector1"),
    ("SuctionGrowDelay", "_SuctionGrowDelay", "Vector1"),
    ("Location", "_Location", "Vector3"),
]

CLOCK_SLOTS = [
    (0, "Clock", "Vector1", False),
    (1, "StartTime", "Vector1", False),
    (2, "Duration", "Vector1", False),
    (3, "Direction", "Vector1", False),
    (4, "GrowDelay", "Vector1", False),
    (5, "LegacyState", "Vector1", False),  # UNCONNECTED on live graphs; default 0
    (6, "State", "Vector1", True),
]
CLOCK_UNCONNECTED = {5}

CONVERGE_SLOTS = [
    (0, "State", "Vector1", False),
    (1, "WorldLocation", "Vector3", False),
    (2, "Position", "Vector3", False),
    (3, "OutPosition", "Vector3", True),
]

VERTEX_POSITION_BLOCK = "VertexDescription.Position"


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
        if "ShaderProperty" not in d.get("m_Type", ""):
            continue
        if d.get("m_Name") == name or d.get("m_DefaultReferenceName") == name:
            return d
    return None


def resolve_property(docs, name, reference):
    return find_property(docs, name) or find_property(docs, reference)


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
    return {
        (e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]):
            (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])
        for e in graph["m_Edges"]
    }


# ---------------------------------------------------------------------------
# builders
# ---------------------------------------------------------------------------

def make_per_instance_property(donor, name, reference):
    p = json.loads(json.dumps(donor))
    p["m_ObjectId"] = new_oid()
    p["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
    p["m_Name"] = name
    p["m_RefNameGeneratedByDisplayName"] = name
    p["m_DefaultReferenceName"] = reference
    p["m_OverrideReferenceName"] = ""
    p["m_GeneratePropertyBlock"] = True
    p["overrideHLSLDeclaration"] = True
    p["hlslDeclarationOverride"] = 3
    p["m_Hidden"] = False
    if isinstance(p.get("m_Value"), dict):
        p["m_Value"] = {"x": 0.0, "y": 0.0, "z": 0.0}
    else:
        p["m_Value"] = 0.0
    if name == "SuctionDirection":
        p["m_Value"] = 1.0
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


def make_custom_function_node(donor_cf, donor_slot_v1, donor_slot_v3, function_name,
                              slots_spec, x, y, height):
    node = json.loads(json.dumps(donor_cf))
    node["m_ObjectId"] = new_oid()
    node["m_Name"] = f"{function_name} (Custom Function)"
    node["m_FunctionName"] = function_name
    node["m_FunctionSource"] = HLSL_GUID
    node["m_SourceType"] = 0
    node["m_FunctionBody"] = "Enter function body here..."
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 232.0, "height": height})
    slots = []
    for slot_id, name, kind, is_output in slots_spec:
        donor = donor_slot_v3 if kind == "Vector3" else donor_slot_v1
        slots.append(make_slot(donor, slot_id, name, is_output))
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

    clock = find_cf(docs, CLOCK_FUNCTION)
    converge = find_cf(docs, CONVERGE_FUNCTION)
    if (clock is None) != (converge is None):
        raise AssertionError(
            f"partial suction splice: {CLOCK_FUNCTION}={'present' if clock else 'missing'}, "
            f"{CONVERGE_FUNCTION}={'present' if converge else 'missing'} — restore the graph "
            "and re-run this wirer; do not hand-complete a half splice")

    if not expect_wired:
        return

    for name, reference, _kind in SUCTION_PROPS:
        p = resolve_property(docs, name, reference)
        assert p is not None, f"property {name} ({reference}) missing"
        assert p["m_DefaultReferenceName"] == reference, f"{name} reference name wrong"
        assert p["m_GeneratePropertyBlock"] is True, f"{name} must be EXPOSED"
        assert p["overrideHLSLDeclaration"] is True, f"{name} must override the HLSL declaration"
        assert p["hlslDeclarationOverride"] == 3, \
            f"{name} must be Hybrid Per Instance (3) or DOTS cannot write it per prism"
        assert any(r["m_Id"] == p["m_ObjectId"] for r in graph["m_Properties"]), \
            f"{name} not in m_Properties"

    assert clock is not None, f"{CLOCK_FUNCTION} custom function node missing"
    assert converge is not None, f"{CONVERGE_FUNCTION} custom function node missing"
    for cf, fn in ((clock, CLOCK_FUNCTION), (converge, CONVERGE_FUNCTION)):
        assert any(r["m_Id"] == cf["m_ObjectId"] for r in graph["m_Nodes"]), \
            f"{fn} not registered in m_Nodes"
        assert cf["m_FunctionSource"] == HLSL_GUID, f"{fn} points at the wrong HLSL asset"

    clock_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in clock["m_Slots"]}
    assert set(clock_slots) == {s[0] for s in CLOCK_SLOTS}, \
        f"{CLOCK_FUNCTION} slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CLOCK_SLOTS:
        assert clock_slots[slot_id]["m_DisplayName"] == name, f"clock slot {slot_id} name drifted"
        assert clock_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), \
            f"clock slot {slot_id} direction wrong"

    conv_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in converge["m_Slots"]}
    assert set(conv_slots) == {s[0] for s in CONVERGE_SLOTS}, \
        f"{CONVERGE_FUNCTION} slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CONVERGE_SLOTS:
        assert conv_slots[slot_id]["m_DisplayName"] == name, f"converge slot {slot_id} name drifted"
        assert conv_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), \
            f"converge slot {slot_id} direction wrong"

    sources = edge_sources(graph)
    fed = set(sources)
    for slot_id, name, _kind, is_output in CLOCK_SLOTS:
        if is_output or slot_id in CLOCK_UNCONNECTED:
            continue
        assert (clock["m_ObjectId"], slot_id) in fed, \
            f"{CLOCK_FUNCTION} input '{name}' is unconnected"
    assert (clock["m_ObjectId"], 5) not in fed, \
        f"{CLOCK_FUNCTION}.LegacyState is connected — live graphs must leave it at default 0"

    for slot_id, name, _kind, is_output in CONVERGE_SLOTS:
        if not is_output:
            assert (converge["m_ObjectId"], slot_id) in fed, \
                f"{CONVERGE_FUNCTION} input '{name}' is unconnected"

    assert sources.get((converge["m_ObjectId"], 0)) == (clock["m_ObjectId"], 6), \
        "PrismSuctionConverge.State is not fed by PrismSuctionClock.State"

    pos_block = find_block(docs, VERTEX_POSITION_BLOCK)
    assert pos_block, "VertexDescription.Position block missing"
    pos_src = sources.get((pos_block["m_ObjectId"], 0))
    assert pos_src == (converge["m_ObjectId"], 3), \
        "VertexDescription.Position is not fed by PrismSuctionConverge.OutPosition"
    pos_in = sources.get((converge["m_ObjectId"], 2))
    assert pos_in is not None, \
        "PrismSuctionConverge.Position is unconnected — the original vertex source was dropped"
    assert pos_in[0] != converge["m_ObjectId"], \
        "PrismSuctionConverge.Position is fed by the splice itself"


# ---------------------------------------------------------------------------
# the edit
# ---------------------------------------------------------------------------

def already_wired(docs):
    return find_cf(docs, CONVERGE_FUNCTION) is not None


def wire(path):
    docs = load_docs(os.path.join(REPO, path))
    validate(docs, expect_wired=False)

    if already_wired(docs):
        validate(docs, expect_wired=True)
        print(f"  {os.path.basename(path)}: already wired")
        return False

    graph = find_graph(docs)
    idx = index(docs)

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
    host = next(idx[c["m_Id"]] for c in graph["m_CategoryData"]
                if any(ch["m_Id"] == grow_start["m_ObjectId"]
                       for ch in idx[c["m_Id"]]["m_ChildObjectList"]))

    prop_oids = {}
    added_props = 0
    for name, reference, kind in SUCTION_PROPS:
        existing = resolve_property(docs, name, reference)
        if existing:
            prop_oids[name] = existing["m_ObjectId"]
            continue
        donor = grow_frac if kind == "Vector3" else grow_start
        p = make_per_instance_property(donor, name, reference)
        prop_oids[name] = p["m_ObjectId"]
        new_docs.append(p)
        graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
        host["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})
        added_props += 1

    base_x, base_y = -2600.0, 2700.0
    node_oids = {}
    for i, (name, _ref, kind) in enumerate(SUCTION_PROPS):
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

    clock_cf, clock_cf_slots = make_custom_function_node(
        donor_cf, donor_slot_v1, donor_slot_v3, CLOCK_FUNCTION, CLOCK_SLOTS,
        base_x + 320.0, base_y, 430.0)
    new_docs.append(clock_cf)
    new_docs.extend(clock_cf_slots)
    graph["m_Nodes"].append({"m_Id": clock_cf["m_ObjectId"]})

    converge_cf, converge_cf_slots = make_custom_function_node(
        donor_cf, donor_slot_v1, donor_slot_v3, CONVERGE_FUNCTION, CONVERGE_SLOTS,
        base_x + 640.0, base_y, 280.0)
    new_docs.append(converge_cf)
    new_docs.extend(converge_cf_slots)
    graph["m_Nodes"].append({"m_Id": converge_cf["m_ObjectId"]})

    pos_block = find_block(docs, VERTEX_POSITION_BLOCK)
    assert pos_block, "VertexDescription.Position block missing"
    retargeted = 0
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == pos_block["m_ObjectId"] and i["m_SlotId"] == 0:
            i["m_Node"]["m_Id"] = converge_cf["m_ObjectId"]
            i["m_SlotId"] = 2
            retargeted += 1
    assert retargeted == 1, \
        f"expected exactly 1 edge into VertexDescription.Position, retargeted {retargeted}"

    graph["m_Edges"].extend([
        edge(clock_node["m_ObjectId"], 0, clock_cf["m_ObjectId"], 0),
        edge(node_oids["SuctionStartTime"], 0, clock_cf["m_ObjectId"], 1),
        edge(node_oids["SuctionDuration"], 0, clock_cf["m_ObjectId"], 2),
        edge(node_oids["SuctionDirection"], 0, clock_cf["m_ObjectId"], 3),
        edge(node_oids["SuctionGrowDelay"], 0, clock_cf["m_ObjectId"], 4),
        edge(clock_cf["m_ObjectId"], 6, converge_cf["m_ObjectId"], 0),
        edge(node_oids["Location"], 0, converge_cf["m_ObjectId"], 1),
        edge(converge_cf["m_ObjectId"], 3, pos_block["m_ObjectId"], 0),
    ])

    docs.extend(new_docs)
    validate(docs, expect_wired=True)

    open(os.path.join(REPO, path), "w", encoding="utf-8").write(dump_docs(docs))
    print(f"  {os.path.basename(path)}: wired "
          f"(+{added_props} properties, +{len(new_docs)} objects)")
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
    print(f"{'Checking' if check_only else 'Wiring'} live-prism suction "
          f"({CLOCK_FUNCTION} + {CONVERGE_FUNCTION}) into {len(GRAPHS)} graphs:")
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
