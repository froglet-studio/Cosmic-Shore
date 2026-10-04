#!/usr/bin/env python3
"""
Wire the Urchin's CRADLE deformation into every graph a live prism can render with.

Docs/PRISM_ANIMATION.md §4.7.2. While an Urchin rides a prismscape, the mass around it DRAPES
over the hull: every vertex within the drape reach of the hull's SURFACE slides along its own
radius toward that surface, closing over the parts of the ship it swallows and rising to meet
the parts it has not reached — one smooth radial field with an ANALYTIC normal, evaluated
entirely on the GPU from a bank of per-frame GLOBALS (the hull's centre + radius + an eased
strength, one slot per riding Urchin). The prism graphs need ONE new node and no new
properties: PrismCradle.hlsl declares its uniforms at file scope, exactly as
PrismDestructionSight.hlsl's peer bank does, so Shader.SetGlobalVectorArray reaches them with
no property surgery.

It is a §4.7 GLOBAL rather than a §1 STAMP because the value depends on where the hull is THIS
frame; contrast PrismJiggleClock / PrismShieldMorph on these same graphs, which are pure
functions of the clock and a one-shot stamp.

Coverage is the same census as the flight clock, the jiggle and the corridor, for the same
reason: a live prism can render with BlockGraph OR ExplodingBlockGraph (transparent live
prisms rest on the latter), and the Urchin can ride either. SuctionGraph is excluded — it
renders mass being CONSUMED, which is not mass the hull is resting on.

What it adds to each graph:

  nodes:
      PrismCradleDeform (Custom Function) -> PrismCradle.hlsl

  edges (the splice — the cradle goes LAST on the vertex chain, so it operates on the position
  every earlier stage has already produced):

      BEFORE:  <position chain tail> ----------> VertexDescription.Position
               <normal chain tail> ------------> VertexDescription.Normal
      AFTER:   <position chain tail> -> PrismCradleDeform.Position
               <normal chain tail> ---> PrismCradleDeform.Normal
               PrismCradleDeform.OutPosition --> VertexDescription.Position
               PrismCradleDeform.OutNormal ----> VertexDescription.Normal

The two vertex BLOCKS are the anchors, which is what makes one splice rule cover both graphs
whatever feeds them today (PrismSuctionConverge.OutPosition and PrismJiggleClock.OutNormal
on both, as it happens — asserted, not assumed).

Out-of-editor ShaderGraph JSON synthesis per the /asset-surgery protocol: parse the whole
file, clone same-file donors so the schema is exact by construction, rebuild in memory,
assert every invariant, and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0. A graph
carrying a cradle node with ANY OTHER slot set — the first cut's per-face node, or the second
cut's per-wedge node with its extra object-space Tangent Vector feeder — is MIGRATED: the old
node is unspliced (its feeders handed back to the blocks), removed with its slots and edges,
any feeder node it ORPHANED is removed too, and the current node spliced fresh. The migration
is deliberately written against the slot DIRECTIONS rather than against a table of known past
signatures, so it runs in both directions and a future signature change needs no new case.
That also makes this the resolver for a .shadergraph merge conflict — take one side whole,
re-run every wirer, confirm each reports "already wired".

Usage:  python3 Tools/Shaders/wire_prism_cradle.py [--check]
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

# GUID of Assets/_Graphics/Materials/Graphs/PrismCradle.hlsl, pinned by its committed .meta so
# this reference can never drift.
HLSL_GUID = "02815910e1a7418bb18c430341747719"
FUNCTION_NAME = "PrismCradleDeform"

# Donor Custom Function for the node shell + a Vector3 slot. Any CF on the graph would do; this
# one is on both live graphs and carries the Vector3 slots we need to clone.
DONOR_FUNCTION = "PrismSuctionConverge"

# (integer slot id, display name, "Vector3", is_output) — the integer ids MUST match the HLSL
# parameter order (every input first, then every output).
CF_SLOTS = [
    (0, "Position", "Vector3", False),
    (1, "Normal", "Vector3", False),
    (2, "OutPosition", "Vector3", True),
    (3, "OutNormal", "Vector3", True),
]
SLOT_POSITION, SLOT_NORMAL, SLOT_OUT_POSITION, SLOT_OUT_NORMAL = 0, 1, 2, 3

OBJECT_SPACE = 0  # UnityEditor.ShaderGraph.CoordinateSpace.Object

VERTEX_POSITION_BLOCK = "VertexDescription.Position"
VERTEX_NORMAL_BLOCK = "VertexDescription.Normal"


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


def find_block(docs, descriptor):
    for d in docs:
        if d.get("m_SerializedDescriptor") == descriptor:
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


def block_input_slot(idx, block):
    ins = [idx[s["m_Id"]]["m_Id"] for s in block["m_Slots"] if idx[s["m_Id"]]["m_SlotType"] == 0]
    assert len(ins) == 1, f"{block.get('m_SerializedDescriptor')} does not have exactly one input slot"
    return ins[0]


# ---------------------------------------------------------------------------
# builders (every one clones a donor of the same type)
# ---------------------------------------------------------------------------

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


def make_custom_function_node(donor_cf, donor_slot_v3, x, y):
    node = json.loads(json.dumps(donor_cf))
    node["m_ObjectId"] = new_oid()
    node["m_Name"] = f"{FUNCTION_NAME} (Custom Function)"
    node["m_FunctionName"] = FUNCTION_NAME
    node["m_FunctionSource"] = HLSL_GUID
    node["m_SourceType"] = 0
    node["m_FunctionBody"] = "Enter function body here..."
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 232.0, "height": 260.0})
    slots = []
    for slot_id, name, _kind, is_output in CF_SLOTS:
        slots.append(make_slot(donor_slot_v3, slot_id, name, is_output))
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
        o_sd = slot_ids[o["m_Node"]["m_Id"]][o["m_SlotId"]]
        i_sd = slot_ids[i["m_Node"]["m_Id"]][i["m_SlotId"]]
        assert o_sd["m_SlotType"] == 1, "edge output is not an output slot"
        assert i_sd["m_SlotType"] == 0, "edge input is not an input slot"
        key = (i["m_Node"]["m_Id"], i["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
    for key, count in feeders.items():
        assert count == 1, f"input slot {key} has {count} feeders (must be exactly 1)"

    if not expect_wired:
        return

    cf = find_cf(docs, FUNCTION_NAME)
    assert cf is not None, f"{FUNCTION_NAME} custom function node missing"
    assert any(r["m_Id"] == cf["m_ObjectId"] for r in graph["m_Nodes"]), \
        f"{FUNCTION_NAME} not registered in m_Nodes"
    assert cf["m_FunctionSource"] == HLSL_GUID, "custom function points at the wrong HLSL asset"
    assert cf["m_SourceType"] == 0, "custom function must source a FILE, not a body"
    cf_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
    assert set(cf_slots) == {s[0] for s in CF_SLOTS}, "custom function slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CF_SLOTS:
        assert cf_slots[slot_id]["m_DisplayName"] == name, f"slot {slot_id} name drifted"
        assert cf_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"slot {slot_id} direction wrong"
        assert "Vector3MaterialSlot" in cf_slots[slot_id]["m_Type"], \
            f"slot {slot_id} must be a Vector3 slot (the HLSL takes float3) — a mismatched slot type ships silently"
        # Vertex-stage work: every slot must be usable in the vertex stage.
        assert cf_slots[slot_id].get("m_StageCapability", 3) in (1, 3), f"slot {slot_id} is fragment-only"

    sources = edge_sources(graph)

    pos_block = find_block(docs, VERTEX_POSITION_BLOCK)
    nrm_block = find_block(docs, VERTEX_NORMAL_BLOCK)
    assert pos_block and nrm_block, "vertex Position/Normal block missing"
    pos_in = block_input_slot(idx, pos_block)
    nrm_in = block_input_slot(idx, nrm_block)

    # --- the cradle is LAST: it feeds both vertex blocks directly ---
    assert sources.get((pos_block["m_ObjectId"], pos_in)) == (cf["m_ObjectId"], SLOT_OUT_POSITION), \
        "VertexDescription.Position is not fed by PrismCradleDeform.OutPosition"
    assert sources.get((nrm_block["m_ObjectId"], nrm_in)) == (cf["m_ObjectId"], SLOT_OUT_NORMAL), \
        "VertexDescription.Normal is not fed by PrismCradleDeform.OutNormal"

    # --- and the chains that used to feed the blocks were RETARGETED into it, not dropped ---
    for slot_id, label in ((SLOT_POSITION, "Position"), (SLOT_NORMAL, "Normal")):
        src = sources.get((cf["m_ObjectId"], slot_id))
        assert src is not None, f"PrismCradleDeform.{label} is unconnected — the original chain was dropped"
        assert src[0] != cf["m_ObjectId"], f"PrismCradleDeform.{label} is fed by the splice itself"
        feeder = idx[src[0]]
        assert "BlockNode" not in feeder.get("m_Type", ""), \
            f"PrismCradleDeform.{label} is fed by a block node — the chain is inverted"
    # Whatever feeds the Normal input must be OBJECT space if it is a NormalVector node: the
    # block's slot is object space, and a world-space normal here rotates the wrong basis.
    nrm_feeder = idx[sources[(cf["m_ObjectId"], SLOT_NORMAL)][0]]
    if "NormalVectorNode" in nrm_feeder.get("m_Type", ""):
        assert nrm_feeder.get("m_Space") == 0, "PrismCradleDeform.Normal is fed by a NON-object-space NormalVector node"


# ---------------------------------------------------------------------------
# the edit
# ---------------------------------------------------------------------------

def cf_slot_ids(docs, cf):
    idx = index(docs)
    return {idx[s["m_Id"]]["m_Id"] for s in cf["m_Slots"]}


def already_wired(docs):
    cf = find_cf(docs, FUNCTION_NAME)
    return cf is not None and cf_slot_ids(docs, cf) == {s[0] for s in CF_SLOTS}


def carries_foreign_node(docs):
    """A cradle node whose slot set is not the current signature — an earlier (or later) cut."""
    cf = find_cf(docs, FUNCTION_NAME)
    return cf is not None and cf_slot_ids(docs, cf) != {s[0] for s in CF_SLOTS}


def unsplice_foreign(docs):
    """Hand a foreign cradle node's position/normal feeders back to the vertex blocks, drop the
    node with its slots and every edge that touched it, and drop any node it ORPHANED.

    Written against slot DIRECTIONS rather than a table of known past signatures: inputs in
    ascending id order are (Position, Normal) and outputs in ascending id order are
    (OutPosition, OutNormal) in every cut this function has to undo, and in any future one, for
    the same reason the current table is ordered that way — a Custom Function's slot ids must
    follow its HLSL parameter order, inputs first. So the migration runs in BOTH directions and
    a signature change needs no new case here.

    The orphan sweep is what makes that true of ADDED feeders too: the per-wedge cut also added
    an object-space Tangent Vector node feeding a slot that no longer exists, and a node left
    registered with no edges is not harmless — it compiles into the graph and reads as somebody's
    live input."""
    graph = find_graph(docs)
    idx = index(docs)
    cf = find_cf(docs, FUNCTION_NAME)
    sources = edge_sources(graph)
    pos_block = find_block(docs, VERTEX_POSITION_BLOCK)
    nrm_block = find_block(docs, VERTEX_NORMAL_BLOCK)
    pos_in = block_input_slot(idx, pos_block)
    nrm_in = block_input_slot(idx, nrm_block)

    slot_docs = [idx[s["m_Id"]] for s in cf["m_Slots"]]
    inputs = sorted((sd["m_Id"] for sd in slot_docs if sd["m_SlotType"] == 0))
    outputs = sorted((sd["m_Id"] for sd in slot_docs if sd["m_SlotType"] == 1))
    assert len(inputs) >= 2 and len(outputs) >= 2, \
        f"cradle node has {len(inputs)} inputs / {len(outputs)} outputs; cannot migrate"
    old_pos_in, old_nrm_in = inputs[0], inputs[1]
    old_pos_out, old_nrm_out = outputs[0], outputs[1]

    pos_feed = sources.get((cf["m_ObjectId"], old_pos_in))
    nrm_feed = sources.get((cf["m_ObjectId"], old_nrm_in))
    assert pos_feed and nrm_feed, "foreign cradle node has an unconnected input; cannot migrate"
    assert sources.get((pos_block["m_ObjectId"], pos_in)) == (cf["m_ObjectId"], old_pos_out), \
        "foreign cradle node does not feed VertexDescription.Position; cannot migrate"
    assert sources.get((nrm_block["m_ObjectId"], nrm_in)) == (cf["m_ObjectId"], old_nrm_out), \
        "foreign cradle node does not feed VertexDescription.Normal; cannot migrate"

    # Every node that fed a slot of the old signature, so the sweep below can tell an orphan
    # from a node that was already there.
    fed_by = {src[0] for (n, _sid), src in sources.items() if n == cf["m_ObjectId"]}

    graph["m_Edges"] = [e for e in graph["m_Edges"]
                        if e["m_InputSlot"]["m_Node"]["m_Id"] != cf["m_ObjectId"]
                        and e["m_OutputSlot"]["m_Node"]["m_Id"] != cf["m_ObjectId"]]
    graph["m_Edges"].extend([
        edge(pos_feed[0], pos_feed[1], pos_block["m_ObjectId"], pos_in),
        edge(nrm_feed[0], nrm_feed[1], nrm_block["m_ObjectId"], nrm_in),
    ])
    graph["m_Nodes"] = [r for r in graph["m_Nodes"] if r["m_Id"] != cf["m_ObjectId"]]
    dead = {cf["m_ObjectId"]} | {s["m_Id"] for s in cf["m_Slots"]}

    # Orphan sweep: a node that ONLY ever fed the removed cradle node now has no edges at all.
    touched = set()
    for e in graph["m_Edges"]:
        touched.add(e["m_InputSlot"]["m_Node"]["m_Id"])
        touched.add(e["m_OutputSlot"]["m_Node"]["m_Id"])
    orphans = [nid for nid in fed_by
               if nid not in touched and nid != cf["m_ObjectId"]
               and "BlockNode" not in idx[nid].get("m_Type", "")]
    for nid in orphans:
        graph["m_Nodes"] = [r for r in graph["m_Nodes"] if r["m_Id"] != nid]
        dead.add(nid)
        dead.update(s["m_Id"] for s in idx[nid].get("m_Slots", []))

    docs[:] = [d for d in docs if d.get("m_ObjectId") not in dead]
    validate(docs, expect_wired=False)
    assert find_cf(docs, FUNCTION_NAME) is None
    return len(orphans)


def wire(path):
    docs = load_docs(os.path.join(REPO, path))
    validate(docs, expect_wired=False)

    if already_wired(docs):
        validate(docs, expect_wired=True)
        print(f"  {os.path.basename(path)}: already wired")
        return False

    migrated = False
    if carries_foreign_node(docs):
        unsplice_foreign(docs)
        migrated = True
    assert find_cf(docs, FUNCTION_NAME) is None, \
        f"{FUNCTION_NAME} still present after the migration pass"

    graph = find_graph(docs)
    idx = index(docs)

    # ---- donors, all schema-exact by construction -------------------------
    donor_cf = find_cf(docs, DONOR_FUNCTION)
    assert donor_cf, f"no {DONOR_FUNCTION} CustomFunctionNode donor — wire the suction clock first"
    cf_slot_docs = [idx[s["m_Id"]] for s in donor_cf["m_Slots"]]
    donor_slot_v3 = next(s for s in cf_slot_docs if "Vector3MaterialSlot" in s["m_Type"])

    pos_block = find_block(docs, VERTEX_POSITION_BLOCK)
    nrm_block = find_block(docs, VERTEX_NORMAL_BLOCK)
    assert pos_block and nrm_block, "vertex Position/Normal block missing"
    pos_in = block_input_slot(idx, pos_block)
    nrm_in = block_input_slot(idx, nrm_block)

    # Place the node just left of the block stack, below the suction converge node.
    x = donor_cf["m_DrawState"]["m_Position"]["x"] + 320.0
    y = donor_cf["m_DrawState"]["m_Position"]["y"] + 320.0
    cf, cf_slots = make_custom_function_node(donor_cf, donor_slot_v3, x, y)
    new_docs = [cf] + cf_slots
    graph["m_Nodes"].append({"m_Id": cf["m_ObjectId"]})

    # ---- the splice: retarget both block feeders into the cradle -----------
    retargeted = {SLOT_POSITION: 0, SLOT_NORMAL: 0}
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == pos_block["m_ObjectId"] and i["m_SlotId"] == pos_in:
            i["m_Node"]["m_Id"] = cf["m_ObjectId"]
            i["m_SlotId"] = SLOT_POSITION
            retargeted[SLOT_POSITION] += 1
        elif i["m_Node"]["m_Id"] == nrm_block["m_ObjectId"] and i["m_SlotId"] == nrm_in:
            i["m_Node"]["m_Id"] = cf["m_ObjectId"]
            i["m_SlotId"] = SLOT_NORMAL
            retargeted[SLOT_NORMAL] += 1
    assert retargeted[SLOT_POSITION] == 1, \
        f"expected exactly 1 feeder on VertexDescription.Position, found {retargeted[SLOT_POSITION]}"
    assert retargeted[SLOT_NORMAL] == 1, \
        f"expected exactly 1 feeder on VertexDescription.Normal, found {retargeted[SLOT_NORMAL]} " \
        "(the jiggle wirer feeds it on both live graphs — run wire_prism_jiggle_clock.py first)"

    graph["m_Edges"].extend([
        edge(cf["m_ObjectId"], SLOT_OUT_POSITION, pos_block["m_ObjectId"], pos_in),
        edge(cf["m_ObjectId"], SLOT_OUT_NORMAL, nrm_block["m_ObjectId"], nrm_in),
    ])

    docs.extend(new_docs)
    validate(docs, expect_wired=True)   # nothing written yet

    open(os.path.join(REPO, path), "w", encoding="utf-8").write(dump_docs(docs))
    validate(load_docs(os.path.join(REPO, path)), expect_wired=True)
    print(f"  {os.path.basename(path)}: {'migrated from an earlier signature and ' if migrated else ''}"
          f"wired (+1 node, +{len(cf_slots)} slots, 2 new edges, 2 retargeted)")
    return True


def check(path):
    docs = load_docs(os.path.join(REPO, path))
    validate(docs, expect_wired=False)
    if not already_wired(docs):
        print(f"  {os.path.basename(path)}: NOT wired"
              + (" (carries a cradle node of another signature — run without --check to migrate)"
                 if carries_foreign_node(docs) else ""))
        return False
    validate(docs, expect_wired=True)
    print(f"  {os.path.basename(path)}: wired ✅")
    return True


def main():
    check_only = "--check" in sys.argv
    print(f"{'Checking' if check_only else 'Wiring'} the Urchin cradle "
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
