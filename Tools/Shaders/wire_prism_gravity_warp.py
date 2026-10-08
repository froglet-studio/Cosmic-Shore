#!/usr/bin/env python3
"""
Wire the black hole's GRAVITY WARP deformation into every graph a live prism can render with.

Docs/BLACK_HOLE.md §5, Docs/PRISM_ANIMATION.md §4.7.4. Mass near a black hole's event horizon is
drawn tidally stretched toward the singularity: every vertex within the warp's reach slides along
its own radius toward the hole by a strain that falls off from the horizon — one smooth radial
field with an ANALYTIC normal, evaluated entirely on the GPU from a bank of per-frame GLOBALS (the
hole's centre + horizon, its strain + reach, one slot per live hole). The prism graphs need ONE new
node and no new properties: PrismGravityWarp.hlsl declares its uniforms at file scope, exactly as
PrismCradle.hlsl's bank does, so Shader.SetGlobalVectorArray reaches them with no property surgery.

It is a §4.7 GLOBAL rather than a §1 STAMP because the value depends on where the hole is THIS
frame (and the hole moves); contrast PrismJiggleClock / PrismShieldMorph on these same graphs,
which are pure functions of the clock and a one-shot stamp.

Coverage is the same census as the cradle, the flight clock, the jiggle and the corridor: a live
prism can render with BlockGraph OR ExplodingBlockGraph (transparent live prisms rest on the
latter). SuctionGraph is excluded — it renders mass being CONSUMED, and a prism a hole has
captured is drawn by the implosion carrier converging on the hole, not by this warp.

What it adds to each graph:

  nodes:
      PrismGravityWarpDeform (Custom Function) -> PrismGravityWarp.hlsl

  edges (the splice — the warp goes IMMEDIATELY BEFORE the cradle, which must stay LAST on the
  vertex chain; PrismCradle.hlsl's header says why):

      BEFORE:  <position chain tail> -> PrismCradleDeform.Position
               <normal chain tail> ---> PrismCradleDeform.Normal
      AFTER:   <position chain tail> -> PrismGravityWarpDeform.Position
               <normal chain tail> ---> PrismGravityWarpDeform.Normal
               PrismGravityWarpDeform.OutPosition --> PrismCradleDeform.Position
               PrismGravityWarpDeform.OutNormal ----> PrismCradleDeform.Normal

  On a graph with no cradle node (the cradle not yet wired) the warp splices onto the vertex
  BLOCKS instead, and wire_prism_cradle.py then splices the cradle after it — the two orders
  commute because each wirer retargets whatever currently feeds its anchor.

The anchor is found STRUCTURALLY: the cradle is "the morph that feeds the vertex blocks", with a
morph identified by its slot shape (Tools/Shaders/prism_vertex_chain.py), never by name — so this
wirer also needs no edit if the cradle is one day renamed, and the three sibling wirers that walk
past morphs need no edit for this one.

Out-of-editor ShaderGraph JSON synthesis per the /asset-surgery protocol: parse the whole file,
clone same-file donors so the schema is exact by construction, rebuild in memory, assert every
invariant, and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0. A graph
carrying a warp node with ANY OTHER slot set is MIGRATED: the old node is unspliced (each of its
consumers handed the source of the matching input), removed with its slots and edges, and the
current node spliced fresh. Written against slot DIRECTIONS rather than a table of past
signatures, so it runs in both directions and a future signature needs no new case.

Usage:  python3 Tools/Shaders/wire_prism_gravity_warp.py [--check]
        --check validates without writing (exit 1 if not wired).
"""

import json
import os
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prism_vertex_chain import morph_slots  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPHS = [
    "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
    "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
]

# GUID of Assets/_Graphics/Materials/Graphs/PrismGravityWarp.hlsl, pinned by its committed .meta
# so this reference can never drift. PrismClockWiringValidator.GravityWarpHlslGuid is the same
# string and BlackHoleTests asserts the three agree.
HLSL_GUID = "81b7289b3d0a4dfcb88f06e135bf22be"
FUNCTION_NAME = "PrismGravityWarpDeform"

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
# the anchor: whatever morph currently feeds the vertex blocks (the cradle), else the blocks
# ---------------------------------------------------------------------------

def find_anchor(docs):
    """Return (node id, position-input slot, normal-input slot, is_morph).

    The warp goes in front of the LAST morph on the chain — the one whose outputs feed both
    vertex blocks directly — identified structurally by its slot shape, never by name. If no
    morph feeds the blocks (the cradle is not wired), the blocks themselves are the anchor and
    the warp becomes the last node until the cradle wirer runs."""
    idx = index(docs)
    graph = find_graph(docs)
    sources = edge_sources(graph)
    pos_block = find_block(docs, VERTEX_POSITION_BLOCK)
    nrm_block = find_block(docs, VERTEX_NORMAL_BLOCK)
    assert pos_block and nrm_block, "vertex Position/Normal block missing"
    pos_in = block_input_slot(idx, pos_block)
    nrm_in = block_input_slot(idx, nrm_block)

    pos_src = sources.get((pos_block["m_ObjectId"], pos_in))
    nrm_src = sources.get((nrm_block["m_ObjectId"], nrm_in))
    assert pos_src and nrm_src, "a vertex block is unfed — the graph is broken upstream of this wirer"

    if pos_src[0] == nrm_src[0]:
        node = idx[pos_src[0]]
        slots = morph_slots(node, idx)
        if slots is not None and node.get("m_FunctionName") != FUNCTION_NAME \
                and slots["OutPosition"] == pos_src[1] and slots["OutNormal"] == nrm_src[1]:
            return node["m_ObjectId"], slots["Position"], slots["Normal"], True

    # No morph feeds the blocks: anchor on the blocks themselves (two different nodes, so the
    # caller retargets each block's feeder separately).
    return None, (pos_block["m_ObjectId"], pos_in), (nrm_block["m_ObjectId"], nrm_in), False


def anchor_inputs(docs):
    """The two (node, slot) pairs the warp's outputs must feed: the cradle's inputs, else the blocks'."""
    node, pos, nrm, is_morph = find_anchor(docs)
    if is_morph:
        return (node, pos), (node, nrm)
    return pos, nrm


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
        assert cf_slots[slot_id].get("m_StageCapability", 3) in (1, 3), f"slot {slot_id} is fragment-only"

    sources = edge_sources(graph)

    # --- the warp feeds the anchor (the cradle's inputs, else the vertex blocks) directly ---
    (pos_anchor, nrm_anchor) = anchor_inputs(docs)
    assert sources.get(pos_anchor) == (cf["m_ObjectId"], SLOT_OUT_POSITION), \
        f"{FUNCTION_NAME}.OutPosition does not feed the Position anchor — the warp must sit immediately before the cradle"
    assert sources.get(nrm_anchor) == (cf["m_ObjectId"], SLOT_OUT_NORMAL), \
        f"{FUNCTION_NAME}.OutNormal does not feed the Normal anchor — the warp must sit immediately before the cradle"

    # --- the cradle, when present, is still LAST: it feeds the blocks, not the warp ---
    cradle = find_cf(docs, "PrismCradleDeform")
    if cradle is not None:
        pos_block = find_block(docs, VERTEX_POSITION_BLOCK)
        nrm_block = find_block(docs, VERTEX_NORMAL_BLOCK)
        pos_in = block_input_slot(idx, pos_block)
        nrm_in = block_input_slot(idx, nrm_block)
        assert sources.get((pos_block["m_ObjectId"], pos_in))[0] == cradle["m_ObjectId"], \
            "VertexDescription.Position is no longer fed by the cradle — the warp must not displace it from LAST"
        assert sources.get((nrm_block["m_ObjectId"], nrm_in))[0] == cradle["m_ObjectId"], \
            "VertexDescription.Normal is no longer fed by the cradle — the warp must not displace it from LAST"

    # --- and the chains that used to feed the anchor were RETARGETED into the warp, not dropped ---
    for slot_id, label in ((SLOT_POSITION, "Position"), (SLOT_NORMAL, "Normal")):
        src = sources.get((cf["m_ObjectId"], slot_id))
        assert src is not None, f"{FUNCTION_NAME}.{label} is unconnected — the original chain was dropped"
        assert src[0] != cf["m_ObjectId"], f"{FUNCTION_NAME}.{label} is fed by the splice itself"
        feeder = idx[src[0]]
        assert "BlockNode" not in feeder.get("m_Type", ""), \
            f"{FUNCTION_NAME}.{label} is fed by a block node — the chain is inverted"
        assert feeder.get("m_FunctionName") != "PrismCradleDeform", \
            f"{FUNCTION_NAME}.{label} is fed by the cradle — the warp is spliced on the wrong side of it"
    nrm_feeder = idx[sources[(cf["m_ObjectId"], SLOT_NORMAL)][0]]
    if "NormalVectorNode" in nrm_feeder.get("m_Type", ""):
        assert nrm_feeder.get("m_Space") == 0, f"{FUNCTION_NAME}.Normal is fed by a NON-object-space NormalVector node"


# ---------------------------------------------------------------------------
# the edit
# ---------------------------------------------------------------------------

def cf_slot_ids(docs, cf):
    idx = index(docs)
    return {idx[s["m_Id"]]["m_Id"] for s in cf["m_Slots"]}


def already_wired(docs):
    cf = find_cf(docs, FUNCTION_NAME)
    if cf is None or cf_slot_ids(docs, cf) != {s[0] for s in CF_SLOTS}:
        return False
    try:
        validate(docs, expect_wired=True)
        return True
    except AssertionError:
        return False


def carries_foreign_node(docs):
    """A warp node whose slot set is not the current signature, or one spliced in the wrong place."""
    cf = find_cf(docs, FUNCTION_NAME)
    return cf is not None and not already_wired(docs)


def unsplice_foreign(docs):
    """Remove an existing warp node: every consumer of one of its outputs is handed the source of
    the matching input (outputs in ascending id order pair with inputs in ascending id order —
    a Custom Function's slot ids follow its HLSL parameter order, inputs first), then the node,
    its slots and every edge that touched it are dropped, along with any node it ORPHANED."""
    graph = find_graph(docs)
    idx = index(docs)
    cf = find_cf(docs, FUNCTION_NAME)
    sources = edge_sources(graph)

    slot_docs = [idx[s["m_Id"]] for s in cf["m_Slots"]]
    inputs = sorted((sd["m_Id"] for sd in slot_docs if sd["m_SlotType"] == 0))
    outputs = sorted((sd["m_Id"] for sd in slot_docs if sd["m_SlotType"] == 1))
    assert len(inputs) >= 2 and len(outputs) >= 2, \
        f"warp node has {len(inputs)} inputs / {len(outputs)} outputs; cannot migrate"
    pairing = {outputs[k]: inputs[k] for k in range(min(len(inputs), len(outputs)))}

    fed_by = {src[0] for (n, _sid), src in sources.items() if n == cf["m_ObjectId"]}

    new_edges = []
    for e in graph["m_Edges"]:
        o, i = e["m_OutputSlot"], e["m_InputSlot"]
        if i["m_Node"]["m_Id"] == cf["m_ObjectId"]:
            continue                                   # an edge INTO the warp: dropped
        if o["m_Node"]["m_Id"] == cf["m_ObjectId"]:
            # an edge OUT of the warp: the consumer takes the matching input's source
            src = sources.get((cf["m_ObjectId"], pairing.get(o["m_SlotId"])))
            assert src is not None, "foreign warp node has an unconnected input; cannot migrate"
            new_edges.append(edge(src[0], src[1], i["m_Node"]["m_Id"], i["m_SlotId"]))
            continue
        new_edges.append(e)
    graph["m_Edges"] = new_edges
    graph["m_Nodes"] = [r for r in graph["m_Nodes"] if r["m_Id"] != cf["m_ObjectId"]]
    dead = {cf["m_ObjectId"]} | {s["m_Id"] for s in cf["m_Slots"]}

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

    donor_cf = find_cf(docs, DONOR_FUNCTION)
    assert donor_cf, f"no {DONOR_FUNCTION} CustomFunctionNode donor — wire the suction clock first"
    cf_slot_docs = [idx[s["m_Id"]] for s in donor_cf["m_Slots"]]
    donor_slot_v3 = next(s for s in cf_slot_docs if "Vector3MaterialSlot" in s["m_Type"])

    (pos_anchor, nrm_anchor) = anchor_inputs(docs)

    # Place the node just left of the cradle (or of the block stack).
    anchor_node = idx[pos_anchor[0]]
    x = anchor_node["m_DrawState"]["m_Position"]["x"] - 300.0
    y = anchor_node["m_DrawState"]["m_Position"]["y"] + 40.0
    cf, cf_slots = make_custom_function_node(donor_cf, donor_slot_v3, x, y)
    new_docs = [cf] + cf_slots
    graph["m_Nodes"].append({"m_Id": cf["m_ObjectId"]})

    # ---- the splice: retarget both anchor feeders into the warp -----------
    retargeted = {SLOT_POSITION: 0, SLOT_NORMAL: 0}
    for e in graph["m_Edges"]:
        i = e["m_InputSlot"]
        if (i["m_Node"]["m_Id"], i["m_SlotId"]) == pos_anchor:
            i["m_Node"]["m_Id"] = cf["m_ObjectId"]
            i["m_SlotId"] = SLOT_POSITION
            retargeted[SLOT_POSITION] += 1
        elif (i["m_Node"]["m_Id"], i["m_SlotId"]) == nrm_anchor:
            i["m_Node"]["m_Id"] = cf["m_ObjectId"]
            i["m_SlotId"] = SLOT_NORMAL
            retargeted[SLOT_NORMAL] += 1
    assert retargeted[SLOT_POSITION] == 1, \
        f"expected exactly 1 feeder on the Position anchor, found {retargeted[SLOT_POSITION]}"
    assert retargeted[SLOT_NORMAL] == 1, \
        f"expected exactly 1 feeder on the Normal anchor, found {retargeted[SLOT_NORMAL]}"

    graph["m_Edges"].extend([
        edge(cf["m_ObjectId"], SLOT_OUT_POSITION, pos_anchor[0], pos_anchor[1]),
        edge(cf["m_ObjectId"], SLOT_OUT_NORMAL, nrm_anchor[0], nrm_anchor[1]),
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
              + (" (carries a warp node of another signature or position — run without --check to migrate)"
                 if carries_foreign_node(docs) else ""))
        return False
    validate(docs, expect_wired=True)
    print(f"  {os.path.basename(path)}: wired ✅")
    return True


def main():
    check_only = "--check" in sys.argv
    print(f"{'Checking' if check_only else 'Wiring'} the black hole gravity warp "
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
