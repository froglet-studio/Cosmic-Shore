#!/usr/bin/env python3
"""
Wire the WAKE deformation into every graph a live prism can render with.

Docs/PRISM_ANIMATION.md §4.7.3. A vessel travelling fast enough drags a travelling ripple
through the mass around its recent path: mass near the path swells away from it and shrinks
back toward it in a wave that streams backward, so the crests hold still in the world and the
ship flies out from under them. It is one smooth cylindrical strain field with an ANALYTIC
normal, evaluated entirely on the GPU from a bank of per-frame GLOBALS (each fast hull's
centre, radius, axis, phase and three derived scalars). The prism graphs need ONE new node and
no new properties: PrismWake.hlsl declares its uniforms at file scope, exactly as
PrismCradle.hlsl and PrismDestructionSight.hlsl do, so Shader.SetGlobalVectorArray reaches them
with no property surgery.

It is a §4.7 GLOBAL rather than a §1 STAMP because the value depends on where the hull is and
which way it is pointing THIS frame; contrast PrismJiggleClock / PrismShieldMorph on these same
graphs, which are pure functions of the clock and a one-shot stamp.

Coverage is the same census as the cradle, the flight clock, the jiggle and the corridor, for
the same reason: a live prism can render with BlockGraph OR ExplodingBlockGraph (transparent
live prisms rest on the latter), and a ship can fly fast past either. SuctionGraph is excluded —
it renders mass being CONSUMED, which is not mass a passing ship disturbs.

WHERE IT SPLICES, AND WHY IT IS NOT LAST. The wake goes immediately UPSTREAM of
PrismCradleDeform, so the chain tail reads

    <position chain tail> -> PrismWakeDeform -> PrismCradleDeform -> VertexDescription.Position
    <normal chain tail>   -> PrismWakeDeform -> PrismCradleDeform -> VertexDescription.Normal

The cradle is deliberately the LAST word on mass touching a hull. Both effects are live on a
riding Urchin at grind speed, and their supports overlap heavily (the drape reaches ~R+6 units
from the ship's centre; the wake's train envelope is still at ~70% strength there). Wake-last
would therefore push vertices the drape had just closed onto the hull back off it, which is the
one promise the cradle makes. Cradle-last leaves the drape intact and lets the wake own
everything further back, which is what each effect is for.

Composition across the two nodes is legal and is not free, and both halves of that are
discharged rather than assumed (the /prism-morph skill §5):
  * NORMALS compose by the chain rule — each node applies its own inverse-transpose to the
    normal it received — so the composite normal is the composite map's derivative.
  * NO-FOLD does not compose in general, but here it does and provably: the determinant of a
    product is the product of the determinants, det(J_wake) = 1·b·c with b, c > 0 held by
    PrismWakeConfigSO's amplitude clamp (verify_prism_wake.py test 4), and det(J_cradle) =
    a·b² with a, b > 0 held by its own floor (verify_prism_cradle.py test 4). Both positive, so
    the composite's is too.

Out-of-editor ShaderGraph JSON synthesis per the /asset-surgery protocol: parse the whole file,
clone same-file donors so the schema is exact by construction, rebuild in memory, assert every
invariant, and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0. A graph
carrying a wake node with ANY OTHER slot set is MIGRATED — the old node is unspliced (its
feeders handed back to the cradle's inputs), removed with its slots and edges, any feeder node
it ORPHANED is removed too, and the current node spliced fresh. The migration is written
against the slot DIRECTIONS rather than a table of known past signatures, so it runs in both
directions and a future signature change needs no new case. That also makes this the resolver
for a .shadergraph merge conflict: take one side whole, re-run every wirer, confirm each
reports "already wired".

Ordering: run wire_prism_cradle.py FIRST. This script splices into the cradle's inputs and
refuses to run without it, by design — the refusal is what keeps the two in the order above
rather than in whichever order somebody happened to run them.

Usage:  python3 Tools/Shaders/wire_prism_wake.py [--check]
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

# GUID of Assets/_Graphics/Materials/Graphs/PrismWake.hlsl, pinned by its committed .meta so
# this reference can never drift.
HLSL_GUID = "4e61030d8d1148b6a5751d98f71dfcab"
FUNCTION_NAME = "PrismWakeDeform"

# The node this one splices in FRONT of. The cradle is the last word on mass touching a hull
# (see the header), so the wake hands it a rippled vertex and the drape has the final say.
DOWNSTREAM_FUNCTION = "PrismCradleDeform"

# Donor Custom Function for the node shell + a Vector3 slot. Any CF on the graph would do; this
# one is on both live graphs and carries the Vector3 slots we need to clone. Deliberately NOT
# the cradle, even though it is the exact shape we want: cloning the node we are about to
# splice in front of makes the donor and the target the same object, and a future change to
# either would then silently move both.
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

    down = find_cf(docs, DOWNSTREAM_FUNCTION)
    assert down is not None, \
        f"{DOWNSTREAM_FUNCTION} is missing — run wire_prism_cradle.py first (see the header: the " \
        "wake splices in FRONT of the cradle so the drape keeps the final say on mass touching a hull)"
    down_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in down["m_Slots"]}
    down_in = sorted(sid for sid, sd in down_slots.items() if sd["m_SlotType"] == 0)
    down_out = sorted(sid for sid, sd in down_slots.items() if sd["m_SlotType"] == 1)
    assert len(down_in) >= 2 and len(down_out) >= 2, \
        f"{DOWNSTREAM_FUNCTION} has {len(down_in)} inputs / {len(down_out)} outputs; cannot splice in front of it"

    # --- the CRADLE is still last: it feeds both vertex blocks directly ---
    assert sources.get((pos_block["m_ObjectId"], pos_in)) == (down["m_ObjectId"], down_out[0]), \
        f"VertexDescription.Position is not fed by {DOWNSTREAM_FUNCTION} — the chain order is wrong"
    assert sources.get((nrm_block["m_ObjectId"], nrm_in)) == (down["m_ObjectId"], down_out[1]), \
        f"VertexDescription.Normal is not fed by {DOWNSTREAM_FUNCTION} — the chain order is wrong"

    # --- and the wake sits immediately in front of it ---
    assert sources.get((down["m_ObjectId"], down_in[0])) == (cf["m_ObjectId"], SLOT_OUT_POSITION), \
        f"{DOWNSTREAM_FUNCTION}.Position is not fed by {FUNCTION_NAME}.OutPosition"
    assert sources.get((down["m_ObjectId"], down_in[1])) == (cf["m_ObjectId"], SLOT_OUT_NORMAL), \
        f"{DOWNSTREAM_FUNCTION}.Normal is not fed by {FUNCTION_NAME}.OutNormal"

    # --- and the chains that used to feed the cradle were RETARGETED into the wake, not dropped ---
    for slot_id, label in ((SLOT_POSITION, "Position"), (SLOT_NORMAL, "Normal")):
        src = sources.get((cf["m_ObjectId"], slot_id))
        assert src is not None, f"{FUNCTION_NAME}.{label} is unconnected — the original chain was dropped"
        assert src[0] != cf["m_ObjectId"], f"{FUNCTION_NAME}.{label} is fed by the splice itself"
        assert src[0] != down["m_ObjectId"], \
            f"{FUNCTION_NAME}.{label} is fed by {DOWNSTREAM_FUNCTION} — the two are wired in a cycle"
        feeder = idx[src[0]]
        assert "BlockNode" not in feeder.get("m_Type", ""), \
            f"{FUNCTION_NAME}.{label} is fed by a block node — the chain is inverted"
    # Whatever feeds the Normal input must be OBJECT space if it is a NormalVector node: the
    # block's slot is object space, and a world-space normal here rotates the wrong basis.
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
    return cf is not None and cf_slot_ids(docs, cf) == {s[0] for s in CF_SLOTS}


def carries_foreign_node(docs):
    """A cradle node whose slot set is not the current signature — an earlier (or later) cut."""
    cf = find_cf(docs, FUNCTION_NAME)
    return cf is not None and cf_slot_ids(docs, cf) != {s[0] for s in CF_SLOTS}


def downstream_inputs(docs):
    """(node, [positionSlotId, normalSlotId]) for the node the wake splices in front of.

    Read by slot DIRECTION rather than by a pinned integer, for the same reason the migration
    below is: a Custom Function's slot ids follow its HLSL parameter order, inputs first, so
    ascending input ids are (Position, Normal) in any signature it could have."""
    idx = index(docs)
    down = find_cf(docs, DOWNSTREAM_FUNCTION)
    assert down is not None, \
        f"{DOWNSTREAM_FUNCTION} is missing — run wire_prism_cradle.py first"
    slot_docs = [idx[s["m_Id"]] for s in down["m_Slots"]]
    ins = sorted(sd["m_Id"] for sd in slot_docs if sd["m_SlotType"] == 0)
    assert len(ins) >= 2, f"{DOWNSTREAM_FUNCTION} has fewer than two input slots"
    return down, ins[:2]


def unsplice_foreign(docs):
    """Hand a foreign wake node's position/normal feeders back to the cradle's inputs, drop the
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
    down, (pos_in, nrm_in) = downstream_inputs(docs)
    pos_block = down          # the wake's downstream is the cradle node, not a vertex block
    nrm_block = down

    slot_docs = [idx[s["m_Id"]] for s in cf["m_Slots"]]
    inputs = sorted((sd["m_Id"] for sd in slot_docs if sd["m_SlotType"] == 0))
    outputs = sorted((sd["m_Id"] for sd in slot_docs if sd["m_SlotType"] == 1))
    assert len(inputs) >= 2 and len(outputs) >= 2, \
        f"wake node has {len(inputs)} inputs / {len(outputs)} outputs; cannot migrate"
    old_pos_in, old_nrm_in = inputs[0], inputs[1]
    old_pos_out, old_nrm_out = outputs[0], outputs[1]

    pos_feed = sources.get((cf["m_ObjectId"], old_pos_in))
    nrm_feed = sources.get((cf["m_ObjectId"], old_nrm_in))
    assert pos_feed and nrm_feed, "foreign wake node has an unconnected input; cannot migrate"
    assert sources.get((pos_block["m_ObjectId"], pos_in)) == (cf["m_ObjectId"], old_pos_out), \
        f"foreign wake node does not feed {DOWNSTREAM_FUNCTION}.Position; cannot migrate"
    assert sources.get((nrm_block["m_ObjectId"], nrm_in)) == (cf["m_ObjectId"], old_nrm_out), \
        f"foreign wake node does not feed {DOWNSTREAM_FUNCTION}.Normal; cannot migrate"

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

    # Orphan sweep: a node that ONLY ever fed the removed wake node now has no edges at all.
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

    down, (pos_in, nrm_in) = downstream_inputs(docs)
    pos_block = down          # the wake's downstream is the cradle node, not a vertex block
    nrm_block = down

    # Place the node just up-chain of the cradle it feeds.
    x = down["m_DrawState"]["m_Position"]["x"] - 300.0
    y = down["m_DrawState"]["m_Position"]["y"] + 300.0
    cf, cf_slots = make_custom_function_node(donor_cf, donor_slot_v3, x, y)
    new_docs = [cf] + cf_slots
    graph["m_Nodes"].append({"m_Id": cf["m_ObjectId"]})

    # ---- the splice: retarget both of the cradle's feeders into the wake ----
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
        f"expected exactly 1 feeder on {DOWNSTREAM_FUNCTION}.Position, found {retargeted[SLOT_POSITION]}"
    assert retargeted[SLOT_NORMAL] == 1, \
        f"expected exactly 1 feeder on {DOWNSTREAM_FUNCTION}.Normal, found {retargeted[SLOT_NORMAL]}"

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
              + (" (carries a wake node of another signature — run without --check to migrate)"
                 if carries_foreign_node(docs) else ""))
        return False
    validate(docs, expect_wired=True)
    print(f"  {os.path.basename(path)}: wired ✅")
    return True


def main():
    check_only = "--check" in sys.argv
    print(f"{'Checking' if check_only else 'Wiring'} the WAKE "
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
