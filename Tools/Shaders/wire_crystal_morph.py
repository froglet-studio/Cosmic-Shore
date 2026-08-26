#!/usr/bin/env python3
"""
Wire the CRYSTAL MORPH into ShepardGraph — the shader every omni-crystal shell renders with.

A vessel may retire an omni crystal with its own animation instead of the shared husk spray
(VesselOmniCrystalRetirementSO). The Squirrel's carries the crystal's body onto the eight
shielded prisms of the boost ring it lays, and "carries" has to mean it literally: the
morph object draws the crystal's OWN mesh with the crystal's OWN materials, so at t = 0 it
is pixel-identical to the crystal it replaced. That only works if the morph lives inside
the crystal's shader, which is what this script splices in.

What it adds to ShepardGraph:

  properties
      _PrismClock     float   UNEXPOSED — the global PrismClock's publisher writes it once
                              per frame, from the same value the stamp uses.
      _CrystalMorph   Vector3 EXPOSED  — (start time, duration, stagger), written per
                              renderer through a MaterialPropertyBlock. Duration 0 is
                              "unstamped", so every crystal in the game is unchanged.

  nodes
      Property x2, UV (channel UV2 — the per-vertex TARGET baked by
      CrystalMorphMeshBuilder), and the CrystalMorph custom function.

  the splice — at the very END of the vertex chain, on VertexDescription.Position:

      BEFORE:  <vertex chain>.Out ------------------------> VertexDescription.Position
      AFTER:   <vertex chain>.Out -> CrystalMorph.Position
               UV2 -> .Target, _PrismClock -> .Clock, _CrystalMorph -> .Morph
               CrystalMorph.Out ------------------------> VertexDescription.Position

  It MUST be last. ShepardGraph's shells displace along the normal, and the morph ends by
  lerping fully onto the target — so splicing after the displacement is what removes it at
  t = 1 and lands the shape exactly on the octahedra. Splicing before would leave a shell
  hovering off the geometry the mesh was fitted to.

Out-of-editor ShaderGraph JSON synthesis per the /asset-surgery protocol: parse the whole
file, clone donors (same-file where one exists, cross-file where it does not) so the schema
is exact by construction, rebuild in memory, assert every invariant — including that the
graph stays ACYCLIC and that no input slot gains a second feeder — and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0. That
also makes it the resolver for a ShepardGraph merge conflict (take one side whole, re-run).

Usage:  python3 Tools/Shaders/wire_crystal_morph.py [--check]
        --check validates without writing (exit 1 if not wired).
"""

import json
import os
import sys
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

GRAPHS = ["Assets/_Graphics/Materials/Graphs/ShepardGraph.shadergraph"]

# ShepardGraph carries no CustomFunctionNode and no UVNode to clone. Both donors are
# cross-file and are the same ShaderGraph serialization version, so the schema is exact by
# construction — the pattern wire_prism_shield_morph.py already uses for its UV node.
CF_DONOR = "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph"
UV_DONOR = "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph"

# GUID of Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl, pinned by its committed
# .meta so this reference can never drift.
HLSL_GUID = "5b3d90c14f7a4e6ea0d5c2183be97f41"
FUNCTION_NAME = "CrystalMorph"

POSITION_BLOCK = "VertexDescription.Position"

# ShaderGraph UVChannel enum: UV0 = 0 … UV3 = 3. Must match
# CrystalMorphMeshBuilder.TargetUVChannel.
UV_CHANNEL_UV2 = 2

CLOCK_PROP = ("PrismClock", "_PrismClock")
MORPH_PROP = ("CrystalMorph", "_CrystalMorph")

# (integer slot id, display name, kind, is_output) — ids MUST match the HLSL parameter
# order (every input first, then every output).
CF_SLOTS = [
    (0, "Position", "Vector3", False),
    (1, "Target", "Vector4", False),
    (2, "Clock", "Vector1", False),
    (3, "Morph", "Vector3", False),
    (4, "Out", "Vector3", True),
]
CF_OUTPUT_SLOT = 4


# --------------------------------------------------------------------------- parse
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


def find_node_by_type(docs, fragment):
    for d in docs:
        if fragment in d.get("m_Type", ""):
            return d
    return None


def find_cf(docs, fn):
    for d in docs:
        if d.get("m_FunctionName") == fn:
            return d
    return None


def find_block(docs, descriptor):
    for d in docs:
        if d.get("m_SerializedDescriptor") == descriptor:
            return d
    return None


# ------------------------------------------------------------------------ builders
def make_slot(donor, slot_id, display_name, is_output):
    s = json.loads(json.dumps(donor))
    s["m_ObjectId"] = new_oid()
    s["m_Id"] = slot_id
    s["m_DisplayName"] = display_name
    s["m_ShaderOutputName"] = display_name
    s["m_SlotType"] = 1 if is_output else 0
    s["m_StageCapability"] = 3
    if isinstance(s.get("m_Value"), dict):
        zero = {k: 0.0 for k in s["m_Value"]}
        s["m_Value"] = dict(zero)
        s["m_DefaultValue"] = dict(zero)
    else:
        s["m_Value"] = 0.0
        s["m_DefaultValue"] = 0.0
    return s


def make_property(donor, name, reference, exposed, value):
    p = json.loads(json.dumps(donor))
    p["m_ObjectId"] = new_oid()
    p["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
    p["m_Name"] = name
    p["m_RefNameGeneratedByDisplayName"] = name
    p["m_DefaultReferenceName"] = reference
    p["m_OverrideReferenceName"] = ""
    p["m_GeneratePropertyBlock"] = exposed
    p["overrideHLSLDeclaration"] = False
    p["hlslDeclarationOverride"] = 0
    p["m_Hidden"] = False
    p["m_Value"] = value
    return p


def make_property_node(donor_node, donor_slot, property_oid, label, x, y):
    node = json.loads(json.dumps(donor_node))
    node["m_ObjectId"] = new_oid()
    node["m_Group"] = {"m_Id": ""}
    node["m_Property"] = {"m_Id": property_oid}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 200.0, "height": 36.0})
    slot = make_slot(donor_slot, 0, label, True)
    slot["m_ShaderOutputName"] = "Out"
    node["m_Slots"] = [{"m_Id": slot["m_ObjectId"]}]
    return node, [slot]


def clone_simple_node(donor, donor_idx, x, y, **overrides):
    node = json.loads(json.dumps(donor))
    node["m_ObjectId"] = new_oid()
    node["m_Group"] = {"m_Id": ""}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y})
    node.update(overrides)
    slots = []
    for ref in donor["m_Slots"]:
        s = json.loads(json.dumps(donor_idx[ref["m_Id"]]))
        s["m_ObjectId"] = new_oid()
        slots.append(s)
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def make_custom_function_node(donor_cf, donors_by_kind, x, y):
    node = json.loads(json.dumps(donor_cf))
    node["m_ObjectId"] = new_oid()
    node["m_Group"] = {"m_Id": ""}
    node["m_Name"] = f"{FUNCTION_NAME} (Custom Function)"
    node["m_FunctionName"] = FUNCTION_NAME
    node["m_FunctionSource"] = HLSL_GUID
    node["m_SourceType"] = 0
    node["m_FunctionBody"] = "Enter function body here..."
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 232.0, "height": 302.0})
    slots = [make_slot(donors_by_kind[kind], sid, name, out) for sid, name, kind, out in CF_SLOTS]
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def edge(out_node, out_slot, in_node, in_slot):
    return {
        "m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
        "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot},
    }


# ---------------------------------------------------------------------- validation
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

    refnames = [idx[r["m_Id"]].get("m_DefaultReferenceName") for r in graph["m_Properties"]]
    assert len(refnames) == len(set(refnames)), f"duplicate property reference names: {refnames}"

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

    feeders, adjacency = {}, {}
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
        adjacency.setdefault(o["m_Node"]["m_Id"], set()).add(i["m_Node"]["m_Id"])
    for key, count in feeders.items():
        assert count == 1, f"input slot {key} has {count} feeders (must be exactly 1)"

    # A CYCLE makes the WHOLE graph magenta at import, including effects this edit never
    # touched — and nothing else in this validator would notice one.
    colour = {}

    def visit(n):
        state = colour.get(n, 0)
        if state == 1:
            raise AssertionError(f"edge cycle through node {n}")
        if state == 2:
            return
        colour[n] = 1
        for m in adjacency.get(n, ()):
            visit(m)
        colour[n] = 2

    sys.setrecursionlimit(10000)
    for ref in graph["m_Nodes"]:
        visit(ref["m_Id"])

    if not expect_wired:
        return

    for name, reference, exposed, kind in ((CLOCK_PROP[0], CLOCK_PROP[1], False, "Vector1ShaderProperty"),
                                           (MORPH_PROP[0], MORPH_PROP[1], True, "Vector3ShaderProperty")):
        p = find_property(docs, name)
        assert p is not None, f"property {name} missing"
        assert p["m_DefaultReferenceName"] == reference, f"{name} reference name wrong"
        assert kind in p["m_Type"], f"{name} is a {p['m_Type']}, expected {kind}"
        want = ("EXPOSED (a MaterialPropertyBlock cannot reach an unexposed property)"
                if exposed else "UNEXPOSED (it is a global uniform)")
        assert p["m_GeneratePropertyBlock"] is exposed, f"{name} must be {want}"
        assert any(r["m_Id"] == p["m_ObjectId"] for r in graph["m_Properties"]), f"{name} not in m_Properties"
        assert any(any(c["m_Id"] == p["m_ObjectId"] for c in idx[cat["m_Id"]]["m_ChildObjectList"])
                   for cat in graph["m_CategoryData"]), f"{name} not on the blackboard"

    cf = find_cf(docs, FUNCTION_NAME)
    assert cf is not None, f"{FUNCTION_NAME} custom function node missing"
    assert cf["m_FunctionSource"] == HLSL_GUID, f"{FUNCTION_NAME} points at the wrong HLSL asset"
    cslots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
    assert set(cslots) == {s[0] for s in CF_SLOTS}, "slot ids do not match the HLSL signature"
    for slot_id, name, kind, is_output in CF_SLOTS:
        assert cslots[slot_id]["m_DisplayName"] == name, f"slot {slot_id} name drifted"
        assert cslots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"slot {slot_id} direction wrong"
        # A property/attribute node cloned from the wrong-width donor wires "successfully"
        # and delivers a silently truncated value.
        assert kind + "MaterialSlot" in cslots[slot_id]["m_Type"], \
            f"slot {slot_id} ({name}) is {cslots[slot_id]['m_Type']}, expected {kind}"

    sources = {(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]):
               (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])
               for e in graph["m_Edges"]}

    uv_src = sources.get((cf["m_ObjectId"], 1))
    assert uv_src is not None, "Target unconnected"
    assert "UVNode" in idx[uv_src[0]].get("m_Type", ""), "Target is not fed by a UV node"
    assert idx[uv_src[0]].get("m_OutputChannel") == UV_CHANNEL_UV2, \
        "Target UV node is not on UV2 — it must match CrystalMorphMeshBuilder.TargetUVChannel"

    for slot, prop_name in ((2, CLOCK_PROP[0]), (3, MORPH_PROP[0])):
        src = sources.get((cf["m_ObjectId"], slot))
        assert src is not None, f"{prop_name} unconnected"
        node = idx[src[0]]
        assert "PropertyNode" in node.get("m_Type", ""), f"slot {slot} is not fed by a property node"
        assert idx[node["m_Property"]["m_Id"]]["m_Name"] == prop_name, \
            f"slot {slot} is fed by {idx[node['m_Property']['m_Id']]['m_Name']}, expected {prop_name}"

    block = find_block(docs, POSITION_BLOCK)
    assert block is not None, f"{POSITION_BLOCK} block missing"
    assert sources.get((block["m_ObjectId"], 0)) == (cf["m_ObjectId"], CF_OUTPUT_SLOT), \
        f"{POSITION_BLOCK} is not fed by {FUNCTION_NAME}.Out — the morph must be LAST in the vertex chain"
    assert sources.get((cf["m_ObjectId"], 0)) is not None, "Position unconnected"


# ---------------------------------------------------------------------------- wire
def wire(rel_path, cf_donor_docs, uv_donor_docs):
    path = os.path.join(REPO, rel_path)
    docs = load_docs(path)

    if find_cf(docs, FUNCTION_NAME) is not None:
        validate(docs, expect_wired=True)
        return False, f"{os.path.basename(rel_path)}: already wired (validated)."

    validate(docs, expect_wired=False)

    graph = find_graph(docs)
    idx = index(docs)

    # ---- donors ---------------------------------------------------------------
    donor_v1_prop = find_node_by_type(docs, "Vector1ShaderProperty")
    donor_v3_prop = find_node_by_type(docs, "Vector3ShaderProperty")
    assert donor_v1_prop and donor_v3_prop, "no Vector1/Vector3 ShaderProperty donor in this graph"

    donor_property_node = find_node_by_type(docs, "ShaderGraph.PropertyNode")
    assert donor_property_node, "no PropertyNode donor"

    donors_by_kind = {}
    for kind in ("Vector1", "Vector3", "Vector4"):
        d = find_node_by_type(docs, f"ShaderGraph.{kind}MaterialSlot")
        assert d, f"no {kind}MaterialSlot donor in this graph"
        donors_by_kind[kind] = d

    donor_cf = find_cf(cf_donor_docs, "PrismGrowScale") or \
        find_node_by_type(cf_donor_docs, "ShaderGraph.CustomFunctionNode")
    assert donor_cf, "no CustomFunctionNode donor in the cross-file donor graph"

    donor_uv = find_node_by_type(docs, "ShaderGraph.UVNode")
    uv_idx = idx
    if donor_uv is None:
        donor_uv = find_node_by_type(uv_donor_docs, "ShaderGraph.UVNode")
        uv_idx = index(uv_donor_docs)
    assert donor_uv, "no UVNode donor in this graph or the cross-file donor"

    block = find_block(docs, POSITION_BLOCK)
    assert block, f"{rel_path}: no {POSITION_BLOCK} block — nothing to splice into"

    new_docs = []

    # ---- properties -----------------------------------------------------------
    clock = make_property(donor_v1_prop, CLOCK_PROP[0], CLOCK_PROP[1], exposed=False, value=0.0)
    morph = make_property(donor_v3_prop, MORPH_PROP[0], MORPH_PROP[1], exposed=True,
                          value={"x": 0.0, "y": 0.0, "z": 0.0})
    host = max((idx[c["m_Id"]] for c in graph["m_CategoryData"]),
               key=lambda c: len(c["m_ChildObjectList"]))
    for p in (clock, morph):
        new_docs.append(p)
        graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
        host["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})

    # ---- nodes ----------------------------------------------------------------
    base_x, base_y = -2400.0, -1400.0
    clock_node, clock_slots = make_property_node(donor_property_node, donors_by_kind["Vector1"],
                                                 clock["m_ObjectId"], CLOCK_PROP[0], base_x, base_y)
    morph_node, morph_slots = make_property_node(donor_property_node, donors_by_kind["Vector3"],
                                                 morph["m_ObjectId"], MORPH_PROP[0], base_x, base_y + 80.0)
    uv_node, uv_slots = clone_simple_node(donor_uv, uv_idx, base_x, base_y + 160.0,
                                          m_OutputChannel=UV_CHANNEL_UV2)
    cf, cf_slots = make_custom_function_node(donor_cf, donors_by_kind, base_x + 340.0, base_y)

    for node, slots in ((clock_node, clock_slots), (morph_node, morph_slots),
                        (uv_node, uv_slots), (cf, cf_slots)):
        new_docs.append(node)
        new_docs.extend(slots)
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    # ---- the splice -----------------------------------------------------------
    # Retarget whatever feeds VertexDescription.Position into the morph's Position input,
    # then feed the block from the morph. Exactly one feeder exists (asserted by the
    # generic validator above), so this is a single rewrite — never a drop, never a
    # duplicate feeder on one input.
    retargeted = 0
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == block["m_ObjectId"] and i["m_SlotId"] == 0:
            e["m_InputSlot"] = {"m_Node": {"m_Id": cf["m_ObjectId"]}, "m_SlotId": 0}
            retargeted += 1
    assert retargeted == 1, f"{rel_path}: expected one {POSITION_BLOCK} feeder, retargeted {retargeted}"

    uv_out = uv_idx[donor_uv["m_Slots"][0]["m_Id"]]["m_Id"]
    graph["m_Edges"].extend([
        edge(uv_node["m_ObjectId"], uv_out, cf["m_ObjectId"], 1),
        edge(clock_node["m_ObjectId"], 0, cf["m_ObjectId"], 2),
        edge(morph_node["m_ObjectId"], 0, cf["m_ObjectId"], 3),
        edge(cf["m_ObjectId"], CF_OUTPUT_SLOT, block["m_ObjectId"], 0),
    ])

    docs.extend(new_docs)
    validate(docs, expect_wired=True)          # nothing written yet

    open(path, "w", encoding="utf-8").write(dump_docs(docs))
    validate(load_docs(path), expect_wired=True)
    return True, (f"{os.path.basename(rel_path)}: wired and validated "
                  f"(+2 properties, +4 nodes, 4 new edges, 1 retargeted).")


def main():
    check_only = "--check" in sys.argv
    cf_donor_docs = load_docs(os.path.join(REPO, CF_DONOR))
    uv_donor_docs = load_docs(os.path.join(REPO, UV_DONOR))

    if check_only:
        ok = True
        for rel in GRAPHS:
            try:
                validate(load_docs(os.path.join(REPO, rel)), expect_wired=True)
                print(f"  {os.path.basename(rel)}: OK")
            except AssertionError as exc:
                print(f"  {os.path.basename(rel)}: NOT WIRED — {exc}")
                ok = False
        return 0 if ok else 1

    for rel in GRAPHS:
        _changed, message = wire(rel, cf_donor_docs, uv_donor_docs)
        print("  " + message)
    return 0


if __name__ == "__main__":
    sys.exit(main())
