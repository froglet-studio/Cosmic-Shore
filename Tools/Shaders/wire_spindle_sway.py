#!/usr/bin/env python3
"""
Wire GPU-clock SPINDLE SWAY into SpindleGraph.

Why this exists
---------------
Before this splice, NO spindle in Cosmic Shore deformed — including the one whose
name says it does. Measured on the merge base:

  * SpindleGraph            no edge into VertexDescription.Position at all.
  * AnimatedSpindleGraph    has an Add feeding VertexDescription.Position whose A
                            input is a hardcoded (0,0,0) constant, i.e. `Position + 0`.
                            Its "animation" is a Gradient Noise scroll on BaseColor.
                            It is also used by ZERO materials and exposes no _Phase.

So Spindle.cs's whole phase-variant machinery (8 shared materials per base material,
bucketed by world position, deliberately NOT a MaterialPropertyBlock so the renderers
stay SRP-batchable) was stamping _Phase into a property that only ever drove a Voronoi
shimmer. Creatures read as rigid because they ARE rigid: the shark and brittlestar get
their motion from an Animator + Animation Rigging armature, the boids from flocking,
and anything without a rig — the QuadFish, every flora branch, the worm segments —
from nothing whatsoever.

What it adds
------------
  properties:
      SwayAmplitude   _SwayAmplitude   exposed, plain (NOT per-instance), default 0
      SwayFrequency   _SwayFrequency   exposed, plain (NOT per-instance), default 0

  nodes:
      Position (object space)
      Property x4   -> _PrismClock, _Phase, _SwayAmplitude, _SwayFrequency
      SpindleSway   Custom Function (HLSL GUID efddcf7a43d650551a2ceb7b095df22c)

  edges:
      Position        -> SpindleSway.PositionOS
      _PrismClock     -> SpindleSway.Clock
      _Phase          -> SpindleSway.Phase
      _SwayAmplitude  -> SpindleSway.Amplitude
      _SwayFrequency  -> SpindleSway.Frequency
      SpindleSway.Out -> VertexDescription.Position      <-- previously UNFED

DEFAULT 0 IS THE BLAST-RADIUS CONTROL, and it is why this is safe to land on a
LOCKED-area graph. Both sine terms in the HLSL are multiplied by Amplitude, so a
material that does not author the property compiles to the identical vertex output it
had before. SpindleGraph is worn by five materials and only ONE of them is a spindle:

  SpindleMaterial            12 prefabs — every flora branch, shark, brittlestar,
                             worm segment and the QuadFish.  <-- opts in
  BranchingMembraneMaterial  BranchingMembrane.prefab         <-- default 0, unchanged
  FireProjectileMaterial     SparrowExhaustProjectile.prefab  <-- default 0, unchanged
  BlueProjectileMaterial     no users
  JadeSpindleMaterial        no users

A DEDICATED PropertyNode is created for _PrismClock and _Phase even though the graph
already has one of each. Those existing nodes feed the FRAGMENT chain; giving the
vertex chain its own is what a human dragging the property in again would produce, and
it keeps the two stages' subgraphs from sharing a node.

Clock CF slot ids MUST match SpindleSway_float's parameter order.

Idempotent: re-running after a successful pass prints "already wired" and exits 0.

Usage:  python3 Tools/Shaders/wire_spindle_sway.py [--check]
        --check validates without writing (exit 1 if not wired).
"""

import json
import os
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wire_prism_spindle_death_clock import (  # noqa: E402  (shared, already-proven helpers)
    load_docs, dump_docs, new_oid, find_graph, index, find_property, find_cf,
    make_slot, edge,
)

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPH = "Assets/_Graphics/Materials/Graphs/SpindleGraph.shadergraph"
DONOR_GRAPH = "Assets/_Graphics/Materials/Graphs/AnimatedSpindleGraph.shadergraph"
HLSL_GUID = "efddcf7a43d650551a2ceb7b095df22c"
FUNCTION = "SpindleSway"
VERTEX_BLOCK = "VertexDescription.Position"

# (slot id, display name, is_vector3, is_output) — order IS the HLSL signature.
SLOTS = [
    (0, "PositionOS", True,  False),
    (1, "Clock",      False, False),
    (2, "Phase",      False, False),
    (3, "Amplitude",  False, False),
    (4, "Frequency",  False, False),
    (5, "Out",        True,  True),
]

NEW_PROPS = [
    ("SwayAmplitude", "_SwayAmplitude", 0.0),
    ("SwayFrequency", "_SwayFrequency", 0.0),
]

# Existing properties the vertex chain consumes.
CLOCK_PROP = "PrismClock"
PHASE_PROP = "Phase"


def make_exposed_float(donor, name, reference, default_value):
    """A plain exposed material constant — NOT Hybrid Per Instance.

    Per-instance would push these out of UnityPerMaterial and cost the SRP batching
    that Spindle.cs's phase-variant design exists to preserve. Sway is authored per
    MATERIAL on purpose: a fern and a fish do not sway alike, so they get different
    materials rather than different per-renderer values.
    """
    p = json.loads(json.dumps(donor))
    p["m_ObjectId"] = new_oid()
    p["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
    p["m_Name"] = name
    p["m_RefNameGeneratedByDisplayName"] = name
    p["m_DefaultReferenceName"] = reference
    p["m_OverrideReferenceName"] = ""
    p["m_GeneratePropertyBlock"] = True
    p["overrideHLSLDeclaration"] = False
    p["hlslDeclarationOverride"] = 0
    p["m_Hidden"] = False
    p["m_Value"] = default_value
    return p


def make_slot_v3(donor_v3, slot_id, display_name, is_output):
    s = json.loads(json.dumps(donor_v3))
    s["m_ObjectId"] = new_oid()
    s["m_Id"] = slot_id
    s["m_DisplayName"] = display_name
    s["m_ShaderOutputName"] = display_name
    s["m_SlotType"] = 1 if is_output else 0
    s["m_StageCapability"] = 3
    s["m_Value"] = {"x": 0.0, "y": 0.0, "z": 0.0}
    s["m_DefaultValue"] = {"x": 0.0, "y": 0.0, "z": 0.0}
    s.pop("m_Space", None)
    s["m_Type"] = "UnityEditor.ShaderGraph.Vector3MaterialSlot"
    return s


def find_node(docs, name, type_fragment):
    for d in docs:
        if d.get("m_Name") == name and type_fragment in d.get("m_Type", ""):
            return d
    return None


def property_node_for(docs, prop_oid):
    for d in docs:
        if "PropertyNode" in d.get("m_Type", "") and d.get("m_Property", {}).get("m_Id") == prop_oid:
            return d
    return None


def make_property_node(donor_node, donor_slot_v1, property_oid, label, x, y):
    node = json.loads(json.dumps(donor_node))
    node["m_ObjectId"] = new_oid()
    node["m_Group"] = {"m_Id": ""}
    node["m_Property"] = {"m_Id": property_oid}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 200.0, "height": 36.0})
    slot = make_slot(donor_slot_v1, 0, label, True)
    slot["m_ShaderOutputName"] = "Out"
    node["m_Slots"] = [{"m_Id": slot["m_ObjectId"]}]
    return node, [slot]


def make_position_node(donor_pos_node, donor_slot_v3, x, y):
    node = json.loads(json.dumps(donor_pos_node))
    node["m_ObjectId"] = new_oid()
    node["m_Group"] = {"m_Id": ""}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y})
    node["m_Space"] = 0          # OBJECT space — the HLSL is written in it
    node["m_PositionSource"] = 0
    slot = make_slot_v3(donor_slot_v3, 0, "Out", True)
    node["m_Slots"] = [{"m_Id": slot["m_ObjectId"]}]
    return node, [slot]


def make_cf_node(donor_cf, donor_v1, donor_v3, x, y):
    node = json.loads(json.dumps(donor_cf))
    node["m_ObjectId"] = new_oid()
    node["m_Name"] = f"{FUNCTION} (Custom Function)"
    node["m_FunctionName"] = FUNCTION
    node["m_FunctionSource"] = HLSL_GUID
    node["m_SourceType"] = 0
    node["m_FunctionBody"] = "Enter function body here..."
    node["m_Group"] = {"m_Id": ""}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 232.0, "height": 360.0})
    slots = []
    for slot_id, name, is_v3, is_output in SLOTS:
        slots.append(make_slot_v3(donor_v3, slot_id, name, is_output) if is_v3
                     else make_slot(donor_v1, slot_id, name, is_output))
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def acyclic(graph):
    """A cycle imports as MAGENTA on every material the graph carries."""
    adj = {}
    for e in graph["m_Edges"]:
        adj.setdefault(e["m_OutputSlot"]["m_Node"]["m_Id"], set()).add(
            e["m_InputSlot"]["m_Node"]["m_Id"])
    WHITE, GREY, BLACK = 0, 1, 2
    colour = {}

    def visit(n):
        colour[n] = GREY
        for m in adj.get(n, ()):
            if colour.get(m, WHITE) == GREY:
                return False
            if colour.get(m, WHITE) == WHITE and not visit(m):
                return False
        colour[n] = BLACK
        return True

    for n in list(adj):
        if colour.get(n, WHITE) == WHITE and not visit(n):
            return False
    return True


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

    refnames = [d.get("m_DefaultReferenceName") for d in docs if "ShaderProperty" in d.get("m_Type", "")]
    assert len(refnames) == len(set(refnames)), f"duplicate property reference name: {refnames}"

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

    assert acyclic(graph), "edge CYCLE — the graph would import magenta"

    # A property node's slot type must match its property's kind, or the value is
    # silently zero at runtime with no import error (asset-surgery §2, cycles/slot-type).
    for d in docs:
        if "PropertyNode" not in d.get("m_Type", ""):
            continue
        prop = idx.get(d.get("m_Property", {}).get("m_Id"))
        if prop is None or not d.get("m_Slots"):
            continue
        slot = idx[d["m_Slots"][0]["m_Id"]]
        if "Vector1ShaderProperty" in prop.get("m_Type", ""):
            assert "Vector1MaterialSlot" in slot["m_Type"], \
                f"PropertyNode for {prop.get('m_Name')} has a {slot['m_Type']} slot"

    block = find_node(docs, VERTEX_BLOCK, "BlockNode")
    assert block is not None, f"{VERTEX_BLOCK} block missing — this is not a vertex-capable graph"

    if not expect_wired:
        return

    cf = find_cf(docs, FUNCTION)
    assert cf is not None, f"{FUNCTION} custom function node missing"
    assert any(r["m_Id"] == cf["m_ObjectId"] for r in graph["m_Nodes"]), f"{FUNCTION} not in m_Nodes"
    assert cf["m_FunctionSource"] == HLSL_GUID, f"{FUNCTION} points at the wrong HLSL asset"

    cf_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
    assert set(cf_slots) == {s[0] for s in SLOTS}, f"{FUNCTION} slot ids do not match the HLSL signature"
    for slot_id, name, is_v3, is_output in SLOTS:
        assert cf_slots[slot_id]["m_DisplayName"] == name, f"{FUNCTION} slot {slot_id} name drifted"
        assert cf_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), \
            f"{FUNCTION} slot {slot_id} direction wrong"
        want = "Vector3MaterialSlot" if is_v3 else "Vector1MaterialSlot"
        assert want in cf_slots[slot_id]["m_Type"], \
            f"{FUNCTION} slot {slot_id} ({name}) must be {want}, is {cf_slots[slot_id]['m_Type']}"

    for name, reference, _default in NEW_PROPS:
        p = find_property(docs, name) or find_property(docs, reference)
        assert p is not None, f"property {name} ({reference}) missing"
        assert p["m_DefaultReferenceName"] == reference, f"{name} reference name wrong"
        assert p["m_GeneratePropertyBlock"] is True, f"{name} must be EXPOSED so a .mat can author it"
        assert p["overrideHLSLDeclaration"] is False, \
            f"{name} must stay a plain UnityPerMaterial constant (SRP batching)"
        assert p["m_Value"] == 0.0, \
            f"{name} default must be 0 — that is what keeps non-spindle materials unchanged"
        assert any(r["m_Id"] == p["m_ObjectId"] for r in graph["m_Properties"]), f"{name} not in m_Properties"

    # every CF input is fed, and the output reaches the vertex block
    fed = {(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]) for e in graph["m_Edges"]}
    for slot_id, name, _v3, is_output in SLOTS:
        if is_output:
            continue
        assert (cf["m_ObjectId"], slot_id) in fed, f"{FUNCTION} input '{name}' is unconnected"

    block_slot = idx[block["m_Slots"][0]["m_Id"]]["m_Id"]
    into_block = [e for e in graph["m_Edges"]
                  if e["m_InputSlot"]["m_Node"]["m_Id"] == block["m_ObjectId"]
                  and e["m_InputSlot"]["m_SlotId"] == block_slot]
    assert len(into_block) == 1, f"{VERTEX_BLOCK} has {len(into_block)} feeders, expected 1"
    assert into_block[0]["m_OutputSlot"]["m_Node"]["m_Id"] == cf["m_ObjectId"], \
        f"{VERTEX_BLOCK} is not fed by {FUNCTION}"
    assert into_block[0]["m_OutputSlot"]["m_SlotId"] == 5, f"{VERTEX_BLOCK} fed by the wrong slot"


def wire(check_only):
    full = os.path.join(REPO, GRAPH)
    docs = load_docs(full)
    validate(docs, expect_wired=False)

    if find_cf(docs, FUNCTION) is not None:
        validate(docs, expect_wired=True)
        print(f"  {os.path.basename(GRAPH)}: already wired")
        return False

    if check_only:
        print(f"  {os.path.basename(GRAPH)}: NOT wired", file=sys.stderr)
        sys.exit(1)

    graph = find_graph(docs)
    idx = index(docs)

    phase = find_property(docs, PHASE_PROP)
    clock = find_property(docs, CLOCK_PROP)
    assert phase is not None, "SpindleGraph has no Phase property"
    assert clock is not None, "SpindleGraph has no PrismClock property — run wire_prism_spindle_death_clock.py first"

    phase_pn = property_node_for(docs, phase["m_ObjectId"])
    assert phase_pn is not None, "no PropertyNode for Phase to clone"
    donor_v1 = idx[phase_pn["m_Slots"][0]["m_Id"]]
    assert "Vector1MaterialSlot" in donor_v1["m_Type"]

    donor_cf = find_cf(docs, "PrismDeathClock")
    assert donor_cf is not None, "no Custom Function node to clone"

    donor_docs = load_docs(os.path.join(REPO, DONOR_GRAPH))
    donor_idx = index(donor_docs)
    donor_pos = find_node(donor_docs, "Position", "PositionNode")
    assert donor_pos is not None, "AnimatedSpindleGraph has no Position node to clone"
    donor_v3 = donor_idx[donor_pos["m_Slots"][0]["m_Id"]]
    assert "Vector3MaterialSlot" in donor_v3["m_Type"]

    host = next(idx[c["m_Id"]] for c in graph["m_CategoryData"]
                if any(ch["m_Id"] == phase["m_ObjectId"]
                       for ch in idx[c["m_Id"]]["m_ChildObjectList"]))

    new_docs = []
    prop_oids = {}
    for name, reference, default in NEW_PROPS:
        p = make_exposed_float(phase, name, reference, default)
        prop_oids[name] = p["m_ObjectId"]
        new_docs.append(p)
        graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
        host["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})

    # Lay the vertex chain out well clear of the existing fragment graph.
    bx, by = -2600.0, 1900.0

    pos_node, pos_slots = make_position_node(donor_pos, donor_v3, bx, by)
    new_docs.append(pos_node); new_docs.extend(pos_slots)
    graph["m_Nodes"].append({"m_Id": pos_node["m_ObjectId"]})

    feeders = []   # (node_oid, cf_slot_id)
    feeders.append((pos_node["m_ObjectId"], 0))

    for i, (prop_oid, label, cf_slot) in enumerate([
        (clock["m_ObjectId"],           CLOCK_PROP,      1),
        (phase["m_ObjectId"],           PHASE_PROP,      2),
        (prop_oids["SwayAmplitude"],    "SwayAmplitude", 3),
        (prop_oids["SwayFrequency"],    "SwayFrequency", 4),
    ]):
        node, slots = make_property_node(phase_pn, donor_v1, prop_oid, label,
                                         bx, by + 360.0 + i * 80.0)
        new_docs.append(node); new_docs.extend(slots)
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})
        feeders.append((node["m_ObjectId"], cf_slot))

    cf, cf_slots = make_cf_node(donor_cf, donor_v1, donor_v3, bx + 400.0, by + 200.0)
    new_docs.append(cf); new_docs.extend(cf_slots)
    graph["m_Nodes"].append({"m_Id": cf["m_ObjectId"]})

    for node_oid, cf_slot in feeders:
        graph["m_Edges"].append(edge(node_oid, 0, cf["m_ObjectId"], cf_slot))

    block = find_node(docs, VERTEX_BLOCK, "BlockNode")
    block_slot = idx[block["m_Slots"][0]["m_Id"]]["m_Id"]
    existing = [e for e in graph["m_Edges"]
                if e["m_InputSlot"]["m_Node"]["m_Id"] == block["m_ObjectId"]
                and e["m_InputSlot"]["m_SlotId"] == block_slot]
    assert not existing, \
        f"{VERTEX_BLOCK} already has a feeder — SpindleGraph's vertex stage is no longer untouched, re-read it"
    graph["m_Edges"].append(edge(cf["m_ObjectId"], 5, block["m_ObjectId"], block_slot))

    docs.extend(new_docs)
    validate(docs, expect_wired=True)

    with open(full, "w", encoding="utf-8") as fh:
        fh.write(dump_docs(docs))
    print(f"  {os.path.basename(GRAPH)}: wired "
          f"({len(NEW_PROPS)} properties, 6 nodes, {len(feeders) + 1} edges)")
    return True


def main():
    check_only = "--check" in sys.argv
    print(f"{FUNCTION} -> {GRAPH}")
    wire(check_only)
    print("OK")


if __name__ == "__main__":
    main()
