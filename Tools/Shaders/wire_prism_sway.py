#!/usr/bin/env python3
"""
Wire PRISM SWAY into every shader graph a live prism can render with.

Docs/ECOSYSTEM.md §47. §44 gave every spindle a GPU bend and left the conserved mass
bolted to it rigid, so a Clawfish's fin bent away from the four ribs lying on it and a
plant's leaves stood still while its branches moved. This is the other half: a LIVING
health prism reads the SAME shear field as the limb it is part of, evaluated at its own
vertices, so the two move together exactly.

Coverage is the census the shield morph and the flight clock already use, for the same
reason: a live prism can render with BlockGraph OR ExplodingBlockGraph
(TransparentPrismMaterial and MazeDangerBlockMateral rest on the latter), so wiring one
graph leaves a hole. SuctionGraph is excluded — it renders mass being consumed, and a
prism being eaten has stopped being part of anything.

What it adds to each graph:

  properties (all four Vector3, HYBRID PER INSTANCE — per-prism initial conditions,
  baked once when the prism is bound to its limb):
      _SwaySpanX   limb +x in THIS prism's object space, x the limb's sway Amplitude
      _SwaySpanY   limb +y in THIS prism's object space, x the limb's sway Amplitude
      _SwayAxis    limb +z as a linear functional on this prism's object space
      _SwayTiming  (Frequency rad/s, Phase rad, Z0 = the prism ORIGIN's limb height)

  Three scalars share one Vector3 because the prism graphs carry Vector1 and Vector3
  property donors and no Vector4 one — the same ruling `_JiggleParams` records.

  nodes:
      Property x4 + a PrismClock property node
      PrismSway (Custom Function) -> PrismSway.hlsl

  edges (the splice — PrismShieldMorph's MorphedPosition, i.e. the object-space vertex
  position AFTER any shield morph, is retargeted through the sway so the two compose and
  everything downstream — grow scale, explosion offset, ballistic flight — applies to the
  swayed shape):
      BEFORE:  PrismShieldMorph.MorphedPosition ------------> <next vertex consumer>
      AFTER:   PrismShieldMorph.MorphedPosition -> PrismSway.PositionOS
               clock + 4 props -> the rest
               PrismSway.Out -----------------------------> <next vertex consumer>

  SpanX = SpanY = (0,0,0) is an exact no-op and is the DEFAULT, so a vessel's trail, a
  cell's authored environment and a dead lifeform's skeleton are bit-identical to before.
  That is the feature, not just a safe default: living mass is the mass that moves.

Out-of-editor ShaderGraph JSON synthesis per the /asset-surgery protocol: parse the whole
file, clone same-file donors so the schema is exact by construction, rebuild in memory,
assert every invariant, and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0.

Usage:  python3 Tools/Shaders/wire_prism_sway.py [--check]
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

# GUID of Assets/_Graphics/Materials/Graphs/PrismSway.hlsl, pinned by its committed
# .meta so this reference can never drift. NOT PrismClockAnimation.hlsl: this function
# `#include`s SpindleSway.hlsl to share the two wave constants with the limb, and a
# constant copied instead of included would drift over exactly the timescale the ratio
# was chosen to make non-repeating — i.e. invisibly, for the first few seconds.
HLSL_GUID = "8d51a0c3f2e74b1e9a6c07d4b3f81520"
FUNCTION_NAME = "PrismSway"

CLOCK_PROP_NAME = "PrismClock"

# The splice anchor. Matched by FUNCTION NAME rather than by object id, because the
# shield morph is itself synthesized and its ids are minted per run.
ANCHOR_FUNCTION = "PrismShieldMorph"
ANCHOR_OUTPUT_SLOT = 8  # MorphedPosition

# (display name, reference name) — all Vector3.
SWAY_PROPS = [
    ("SwaySpanX", "_SwaySpanX"),
    ("SwaySpanY", "_SwaySpanY"),
    ("SwayAxis", "_SwayAxis"),
    ("SwayTiming", "_SwayTiming"),
]

# (integer slot id, display name, "Vector1"|"Vector3", is_output) — the integer ids MUST
# match the HLSL parameter order (every input first, then every output).
CF_SLOTS = [
    (0, "PositionOS", "Vector3", False),
    (1, "Clock", "Vector1", False),
    (2, "SpanX", "Vector3", False),
    (3, "SpanY", "Vector3", False),
    (4, "Axis", "Vector3", False),
    (5, "Timing", "Vector3", False),
    (6, "Out", "Vector3", True),
]

CF_OUTPUT_SLOT = 6
CF_POSITION_SLOT = 0


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


# ---------------------------------------------------------------------------
# builders (every one clones a donor of the same type)
# ---------------------------------------------------------------------------

def make_per_instance_property(donor, name, reference):
    """Clone a donor Vector3 ShaderProperty and re-key it as a fresh Hybrid-Per-Instance
    property. The donor is an existing per-instance Vector3 clock property, so the
    exposure flags are already correct — and they are asserted afterwards anyway."""
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
    # Settled default: a zero span is an exact no-op in PrismSway_float, so an untouched
    # material renders the mesh exactly as authored. Timing defaults to zero too, which
    # is inert precisely because the span it would be multiplied by is zero.
    p["m_Value"] = {"x": 0.0, "y": 0.0, "z": 0.0}
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

    for name, reference in SWAY_PROPS:
        p = find_property(docs, name)
        assert p is not None, f"property {name} missing"
        assert p["m_DefaultReferenceName"] == reference, f"{name} reference name wrong"
        assert "Vector3ShaderProperty" in p["m_Type"], f"{name} must be a Vector3"
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
    for slot_id, name, kind, is_output in CF_SLOTS:
        assert cf_slots[slot_id]["m_DisplayName"] == name, f"slot {slot_id} name drifted"
        assert cf_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"slot {slot_id} direction wrong"
        want = "Vector3MaterialSlot" if kind == "Vector3" else "Vector1MaterialSlot"
        assert want in cf_slots[slot_id]["m_Type"], f"slot {slot_id} is not a {kind}"

    sources = {}
    for e in graph["m_Edges"]:
        sources[(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])] = (
            e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])

    for slot_id, name, _kind, is_output in CF_SLOTS:
        if not is_output:
            assert (cf["m_ObjectId"], slot_id) in sources, f"custom function input '{name}' is unconnected"

    # The sway sits immediately AFTER the shield morph: PositionOS IS MorphedPosition,
    # and whatever consumed MorphedPosition now reads the sway. Composing the two in this
    # order is what lets a shielded prism on a living limb do both.
    anchor = find_cf(docs, ANCHOR_FUNCTION)
    assert anchor is not None, \
        f"{ANCHOR_FUNCTION} not found — wire_prism_shield_morph.py has to run first"
    assert sources[(cf["m_ObjectId"], CF_POSITION_SLOT)] == (anchor["m_ObjectId"], ANCHOR_OUTPUT_SLOT), \
        f"{FUNCTION_NAME}.PositionOS is not fed by {ANCHOR_FUNCTION}.MorphedPosition"

    consumers = [e for e in graph["m_Edges"]
                 if e["m_OutputSlot"]["m_Node"]["m_Id"] == cf["m_ObjectId"]
                 and e["m_OutputSlot"]["m_SlotId"] == CF_OUTPUT_SLOT]
    assert len(consumers) == 1, \
        f"{FUNCTION_NAME}.Out feeds {len(consumers)} inputs (expected exactly 1 — the retargeted vertex chain)"
    assert consumers[0]["m_InputSlot"]["m_Node"]["m_Id"] != cf["m_ObjectId"], "the sway feeds itself"

    # MorphedPosition must ONLY feed the sway now — a surviving direct edge would mean
    # the un-swayed position is still driving part of the vertex chain.
    direct = [e for e in graph["m_Edges"]
              if e["m_OutputSlot"]["m_Node"]["m_Id"] == anchor["m_ObjectId"]
              and e["m_OutputSlot"]["m_SlotId"] == ANCHOR_OUTPUT_SLOT]
    assert len(direct) == 1 and direct[0]["m_InputSlot"]["m_Node"]["m_Id"] == cf["m_ObjectId"], \
        f"{ANCHOR_FUNCTION}.MorphedPosition still feeds something other than the sway"

    # The clock feed must be the SHARED unexposed global, never a second one: two prism
    # clocks would let the limb and the mass bolted to it run on different time.
    clock_node = idx[sources[(cf["m_ObjectId"], 1)][0]]
    assert "PropertyNode" in clock_node.get("m_Type", ""), "Clock is not fed by a property node"
    clock_prop = idx[clock_node["m_Property"]["m_Id"]]
    assert clock_prop["m_Name"] == CLOCK_PROP_NAME, \
        f"Clock is fed by {clock_prop['m_Name']}, not the shared {CLOCK_PROP_NAME} global"
    assert clock_prop["m_GeneratePropertyBlock"] is False, \
        f"{CLOCK_PROP_NAME} must stay an unexposed global"


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
    v3_donor = find_property(docs, "JiggleParams")
    assert v3_donor and "Vector3ShaderProperty" in v3_donor["m_Type"], \
        "JiggleParams (the Vector3 per-instance donor) not found — wire the jiggle clock first"

    donor_property_node = find_node_by_type(docs, "ShaderGraph.PropertyNode")
    assert donor_property_node, "no PropertyNode donor"
    donor_cf = find_cf(docs, "PrismGrowScale")
    assert donor_cf, "no PrismGrowScale CustomFunctionNode donor"

    cf_slot_docs = [idx[s["m_Id"]] for s in donor_cf["m_Slots"]]
    donor_slot_v1 = next(s for s in cf_slot_docs if "Vector1MaterialSlot" in s["m_Type"])
    donor_slot_v3 = next(s for s in cf_slot_docs if "Vector3MaterialSlot" in s["m_Type"])

    anchor = find_cf(docs, ANCHOR_FUNCTION)
    assert anchor, f"{ANCHOR_FUNCTION} node not found — nothing to splice after"
    anchor_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in anchor["m_Slots"]}
    assert ANCHOR_OUTPUT_SLOT in anchor_slots, f"{ANCHOR_FUNCTION} has no slot {ANCHOR_OUTPUT_SLOT}"
    assert anchor_slots[ANCHOR_OUTPUT_SLOT]["m_DisplayName"] == "MorphedPosition", \
        f"{ANCHOR_FUNCTION} slot {ANCHOR_OUTPUT_SLOT} is no longer MorphedPosition — re-derive the anchor"

    new_docs = []

    # ---- properties -------------------------------------------------------
    prop_oids = {}
    host = next(idx[c["m_Id"]] for c in graph["m_CategoryData"]
                if any(ch["m_Id"] == v3_donor["m_ObjectId"]
                       for ch in idx[c["m_Id"]]["m_ChildObjectList"]))
    for name, reference in SWAY_PROPS:
        prop = make_per_instance_property(v3_donor, name, reference)
        prop_oids[name] = prop["m_ObjectId"]
        new_docs.append(prop)
        graph["m_Properties"].append({"m_Id": prop["m_ObjectId"]})
        host["m_ChildObjectList"].append({"m_Id": prop["m_ObjectId"]})

    # ---- property nodes ---------------------------------------------------
    base_x, base_y = -3200.0, 3400.0
    node_oids = {}
    for i, (name, _ref) in enumerate(SWAY_PROPS):
        node, slots = make_property_node(donor_property_node, donor_slot_v3, prop_oids[name],
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
                                             base_x + 340.0, base_y)
    new_docs.append(cf)
    new_docs.extend(cf_slots)
    graph["m_Nodes"].append({"m_Id": cf["m_ObjectId"]})

    # ---- the splice -------------------------------------------------------
    # Retarget every consumer of MorphedPosition onto the sway's output, then feed the
    # sway from MorphedPosition. Exactly one consumer exists (asserted), so this is a
    # single rewrite — never a drop, never a duplicate.
    retargeted = 0
    for e in graph["m_Edges"]:
        o = e["m_OutputSlot"]
        if o["m_Node"]["m_Id"] == anchor["m_ObjectId"] and o["m_SlotId"] == ANCHOR_OUTPUT_SLOT:
            o["m_Node"]["m_Id"] = cf["m_ObjectId"]
            o["m_SlotId"] = CF_OUTPUT_SLOT
            retargeted += 1
    assert retargeted == 1, \
        f"expected exactly 1 consumer of {ANCHOR_FUNCTION}.MorphedPosition, retargeted {retargeted}"

    graph["m_Edges"].extend([
        edge(anchor["m_ObjectId"], ANCHOR_OUTPUT_SLOT, cf["m_ObjectId"], CF_POSITION_SLOT),
        edge(clock_node["m_ObjectId"], 0, cf["m_ObjectId"], 1),
        edge(node_oids["SwaySpanX"], 0, cf["m_ObjectId"], 2),
        edge(node_oids["SwaySpanY"], 0, cf["m_ObjectId"], 3),
        edge(node_oids["SwayAxis"], 0, cf["m_ObjectId"], 4),
        edge(node_oids["SwayTiming"], 0, cf["m_ObjectId"], 5),
    ])

    docs.extend(new_docs)
    validate(docs, expect_wired=True)

    open(os.path.join(REPO, path), "w", encoding="utf-8").write(dump_docs(docs))
    print(f"  {os.path.basename(path)}: wired "
          f"(+{len(SWAY_PROPS)} properties, +{len(new_docs)} objects)")
    return True


def main():
    check_only = "--check" in sys.argv

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
        changed |= wire(path)
    print("done" if changed else "nothing to do")
    return 0


if __name__ == "__main__":
    sys.exit(main())
