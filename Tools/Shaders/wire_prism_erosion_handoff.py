#!/usr/bin/env python3
"""Wire the EROSION -> CORRIDOR handoff so one field decides, never two.

WHAT THIS CHANGES AND WHY. Before this, ExplodingBlockGraph ran the erosion IN FRONT of
the corridor: the erosion resolved its own front into a 0/1 verdict and that verdict went
into `PrismOcclusionFade.BaseAlpha`. So the corridor was handed a constant 1 for every
surviving chunk, however far the fade had actually run — it then applied its own
screen-anchored dither to that lie while the erosion front carried on carving the same
surface in UV space. Two independent threshold fields on one surface is the layered beat
the corridor already attacks from the other side, and outside the corridor the constant 1
took the "solid mass" fast out, so debris was never dithered at all at any range.

After this the two run BESIDE each other and the corridor's single clip SELECTS:

    PrismExplosionClock.Opacity -> PrismOcclusionFade.BaseAlpha      (the TRUE fade)
    PrismExplosionClock.Opacity -> PrismErosionFade.BaseOpacity      (unchanged)
    PrismErosionFade.Threshold  -> PrismOcclusionFade.ErosionThreshold   (new)

THE HLSL SIGNATURE MUST DECLARE EVERY INPUT BEFORE EITHER `out` (2026-09-17).
A file-mode Custom Function node does not generate its function — it calls the one in the
HLSL — and it builds that call as ALL ITS INPUT SLOTS, THEN ALL ITS OUTPUT SLOTS. The
first cut of this wirer APPENDED the new input as slot 6 and declared it LAST in the HLSL,
after the two `out` parameters, on the reasoning that HLSL permits an in parameter
anywhere and that appending leaves every existing slot id untouched. The slot half of that
is true; the SIGNATURE half is not. The node emitted
`(PositionWS, Target, Params, BaseAlpha, ErosionThreshold, Alpha, ClipThreshold)` against
an HLSL signature whose fifth parameter is `out float Alpha` — so BOTH prism graphs failed
to compile and EVERY PRISM IN THE GAME rendered with no material. Nothing else can report
that: an out-of-editor verifier compiles the HLSL alone and never sees the call the node
writes. The gate that does is Tools/Build/check_shadergraph_custom_function_signatures.py,
which resolves each file-mode node's function and asserts the signature's out-pattern
against that node's own input/output counts.

THE SLOT IDS ARE A SEPARATE QUESTION AND THEY ARE FREE. It is tempting to read the fix as
"ids must not interleave" — every other Custom Function node in these two graphs numbers
its inputs below its outputs, which looks like corroboration — but TextMesh Pro ships
file-mode nodes that interleave and compile: `Composite_float(float4, float4, out float4)`
is wired in [0, 3] / out [2]. Ids do not decide the argument order; the slot TYPE does.
This wirer still renumbers the erosion input to slot 4 and the two outputs to 5 and 6,
because matching the other twelve nodes costs one edge rewrite and leaves the file
readable — a house convention in these graphs, not a requirement, and not the bug.
Renumbering is done BY DISPLAY NAME, so this migrates a graph in any of the three states
it can be in (never wired, wired the broken way, already correct) and is a no-op on the
third.

BlockGraph gets the same slot and wires nothing into it; Shader Graph then passes the
slot's default 0, which is the documented "no erosion here" value, so that graph is
unchanged sample for sample. Both graphs must get the slot even so: the generated call's
argument count comes from the node's own slot list, and a graph one argument short would
not compile.

Idempotent. `--check` reports without writing.
"""
import json
import sys
import uuid

GRAPHS = [
    "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
    "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
]
EXPLODING = GRAPHS[1]

CORRIDOR_FN = "PrismOcclusionFade"
EROSION_FN = "PrismErosionFade"
CLOCK_FN = "PrismExplosionClock"

# The corridor's slot layout, BY NAME, in the order the HLSL declares its parameters —
# every input first, then every output (see the module docstring for why that is load
# bearing rather than tidy). This table is the authority: the wirer renumbers the node's
# slots onto it and rewrites the node's edges to match, from whatever state it finds.
CORRIDOR_LAYOUT = [
    ("PositionWS", 0),
    ("Target", 1),
    ("Params", 2),
    ("BaseAlpha", 3),
    ("ErosionThreshold", 4),
    ("Alpha", 5),
    ("ClipThreshold", 6),
]
CORRIDOR_INPUT_COUNT = 5

CORRIDOR_BASEALPHA = 3
CORRIDOR_EROSION_SLOT = 4
CORRIDOR_EROSION_NAME = "ErosionThreshold"
EROSION_OUT = 4
EROSION_OUT_NAME = "Threshold"
CLOCK_OPACITY = 8


def load_docs(path):
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


def find_graph(docs):
    return next(d for d in docs if "GraphData" in d.get("m_Type", ""))


def index(docs):
    return {d["m_ObjectId"]: d for d in docs if "m_ObjectId" in d}


def cf_by_function(idx, graph, fn):
    for ref in graph["m_Nodes"]:
        node = idx[ref["m_Id"]]
        if node.get("m_FunctionName") == fn:
            return node
    return None


def slots_of(idx, node):
    return {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in node.get("m_Slots", [])}


def clone_vector1_input(donor):
    """A new Vector1 INPUT slot, cloned off one this node already carries.

    Cloning rather than synthesising is what keeps every serialized key Shader Graph
    expects (precision, concrete-slot-value-type, hidden flags) exactly as this Unity
    version writes them — a hand-built slot is the shape that loads and then behaves
    subtly differently.
    """
    s = json.loads(json.dumps(donor))
    s["m_ObjectId"] = uuid.uuid4().hex
    s["m_Id"] = CORRIDOR_EROSION_SLOT
    s["m_DisplayName"] = CORRIDOR_EROSION_NAME
    s["m_ShaderOutputName"] = CORRIDOR_EROSION_NAME
    s["m_SlotType"] = 0
    s["m_Value"] = 0.0
    s["m_DefaultValue"] = 0.0
    return s


def edge(out_node, out_slot, in_node, in_slot):
    return {
        "m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
        "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot},
    }


def has_edge(graph, out_node, out_slot, in_node, in_slot):
    return any(e["m_OutputSlot"]["m_Node"]["m_Id"] == out_node
               and e["m_OutputSlot"]["m_SlotId"] == out_slot
               and e["m_InputSlot"]["m_Node"]["m_Id"] == in_node
               and e["m_InputSlot"]["m_SlotId"] == in_slot
               for e in graph["m_Edges"])


def renumber_corridor(graph, idx, corridor, path):
    """Put the corridor's slots on CORRIDOR_LAYOUT and carry its edges across.

    Keyed on DISPLAY NAME because the ids are exactly what is being changed, and because
    that makes this idempotent from every state the node can be in. Edges are rewritten
    from the SAME mapping, so an edge can never be left pointing at a slot id that has
    moved out from under it.
    """
    slots = slots_of(idx, corridor)
    by_name = {}
    for sid, sd in slots.items():
        name = sd.get("m_DisplayName")
        assert name not in by_name, f"{path}: corridor has two slots named {name}"
        by_name[name] = sd

    want = dict(CORRIDOR_LAYOUT)
    missing = [n for n in want if n not in by_name]
    assert not missing, f"{path}: corridor is missing slot(s) {missing}"
    extra = [n for n in by_name if n not in want]
    assert not extra, f"{path}: corridor carries unexpected slot(s) {extra} — layout drifted"

    ordered = sorted(corridor["m_Slots"], key=lambda ref: idx[ref["m_Id"]]["m_Id"])
    moved = [(n, by_name[n]["m_Id"], want[n]) for n in want if by_name[n]["m_Id"] != want[n]]
    if not moved:
        if corridor["m_Slots"] != ordered:
            corridor["m_Slots"] = ordered
            return [f"~ {CORRIDOR_FN} slot list put in id order"]
        return []

    remap = {by_name[n]["m_Id"]: want[n] for n in want}
    for name, new_id in want.items():
        by_name[name]["m_Id"] = new_id
    cid = corridor["m_ObjectId"]
    for e in graph["m_Edges"]:
        for side in ("m_OutputSlot", "m_InputSlot"):
            if e[side]["m_Node"]["m_Id"] == cid:
                e[side]["m_SlotId"] = remap[e[side]["m_SlotId"]]
    # And put the node's OWN slot list in id order. Ids alone already give one argument
    # list under either reading, but only because the appended input happens to be the
    # last entry; ordering the list removes the coincidence, and it is how every other
    # node in both graphs is written.
    corridor["m_Slots"].sort(key=lambda ref: idx[ref["m_Id"]]["m_Id"])

    detail = ", ".join(f"{n} {o}->{w}" for n, o, w in moved)
    return [f"~ {CORRIDOR_FN} slots renumbered ({detail})"]


def process(path, check):
    docs = load_docs(path)
    graph = find_graph(docs)
    idx = index(docs)
    changes = []

    corridor = cf_by_function(idx, graph, CORRIDOR_FN)
    assert corridor, f"{path}: no {CORRIDOR_FN} node — run wire_prism_occlusion_corridor.py first"
    cslots = slots_of(idx, corridor)

    if not any(sd.get("m_DisplayName") == CORRIDOR_EROSION_NAME for sd in cslots.values()):
        donor = next(sd for sd in cslots.values() if sd.get("m_DisplayName") == "BaseAlpha")
        new_slot = clone_vector1_input(donor)
        docs.append(new_slot)
        corridor["m_Slots"].append({"m_Id": new_slot["m_ObjectId"]})
        changes.append(f"+ {CORRIDOR_FN}.{CORRIDOR_EROSION_NAME}")
        idx = index(docs)
        cslots = slots_of(idx, corridor)

    changes += renumber_corridor(graph, idx, corridor, path)
    cslots = slots_of(idx, corridor)

    if path == EXPLODING:
        erosion = cf_by_function(idx, graph, EROSION_FN)
        clock = cf_by_function(idx, graph, CLOCK_FN)
        assert erosion, f"{path}: no {EROSION_FN} node — run wire_prism_explosion_erosion.py first"
        assert clock, f"{path}: no {CLOCK_FN} node"
        eslots = slots_of(idx, erosion)
        out = eslots[EROSION_OUT]
        if out["m_DisplayName"] != EROSION_OUT_NAME:
            out["m_DisplayName"] = EROSION_OUT_NAME
            out["m_ShaderOutputName"] = EROSION_OUT_NAME
            changes.append(f"~ {EROSION_FN} output renamed -> {EROSION_OUT_NAME}")

        eid, cid, kid = erosion["m_ObjectId"], corridor["m_ObjectId"], clock["m_ObjectId"]

        stale = [e for e in graph["m_Edges"]
                 if e["m_OutputSlot"]["m_Node"]["m_Id"] == eid
                 and e["m_InputSlot"]["m_Node"]["m_Id"] == cid
                 and e["m_InputSlot"]["m_SlotId"] == CORRIDOR_BASEALPHA]
        for e in stale:
            graph["m_Edges"].remove(e)
            changes.append(f"- {EROSION_FN} -> {CORRIDOR_FN}.BaseAlpha (the verdict path)")

        if not has_edge(graph, kid, CLOCK_OPACITY, cid, CORRIDOR_BASEALPHA):
            graph["m_Edges"].append(edge(kid, CLOCK_OPACITY, cid, CORRIDOR_BASEALPHA))
            changes.append(f"+ {CLOCK_FN}.Opacity -> {CORRIDOR_FN}.BaseAlpha")
        if not has_edge(graph, eid, EROSION_OUT, cid, CORRIDOR_EROSION_SLOT):
            graph["m_Edges"].append(edge(eid, EROSION_OUT, cid, CORRIDOR_EROSION_SLOT))
            changes.append(f"+ {EROSION_FN}.{EROSION_OUT_NAME} -> {CORRIDOR_FN}.{CORRIDOR_EROSION_NAME}")

    validate(docs, path)
    if changes and not check:
        open(path, "w", encoding="utf-8").write(dump_docs(docs))
    return changes


def validate(docs, path):
    """Every slot resolves, every edge lands on a real (node, slot), ids stay unique."""
    graph = find_graph(docs)
    idx = index(docs)
    slot_ids = {}
    for ref in graph["m_Nodes"]:
        assert ref["m_Id"] in idx, f"{path}: m_Nodes references missing {ref['m_Id']}"
        node = idx[ref["m_Id"]]
        ints = set()
        for s in node.get("m_Slots", []):
            assert s["m_Id"] in idx, f"{path}: slot {s['m_Id']} missing"
            sd = idx[s["m_Id"]]
            assert sd["m_Id"] not in ints, f"{path}: duplicate slot id {sd['m_Id']}"
            ints.add(sd["m_Id"])
        slot_ids[ref["m_Id"]] = ints
    for e in graph["m_Edges"]:
        for side in ("m_OutputSlot", "m_InputSlot"):
            nid = e[side]["m_Node"]["m_Id"]
            assert nid in slot_ids, f"{path}: edge to unknown node {nid}"
            assert e[side]["m_SlotId"] in slot_ids[nid], \
                f"{path}: edge to unknown slot {e[side]['m_SlotId']} on {nid}"

    # exactly one feeder per input, and the handoff shape itself
    corridor = cf_by_function(idx, graph, CORRIDOR_FN)
    cslots = slots_of(idx, corridor)
    assert CORRIDOR_EROSION_SLOT in cslots, f"{path}: corridor is missing the erosion input"
    assert cslots[CORRIDOR_EROSION_SLOT]["m_SlotType"] == 0, f"{path}: erosion input is not an input"

    # THE INVARIANT THAT DECIDES WHETHER THE GRAPH COMPILES. A file-mode node calls the
    # HLSL with all its inputs and then all its outputs, so a node whose outputs are not
    # the last slots hands the function its arguments in an order the signature does not
    # describe — and the only report of that is every prism rendering with no material.
    # What the COMPILER requires is the HLSL signature (inputs before either `out`), and
    # that is gated by Tools/Build/check_shadergraph_custom_function_signatures.py across the
    # whole project. What is asserted HERE is the house convention these two graphs keep:
    # every input numbered below every output, so a reader can take the slot order for the
    # argument order. Ids themselves are free — TextMesh Pro interleaves and compiles — so
    # this is a readability check scoped to the prism graphs, not a compile gate.
    for ref in graph["m_Nodes"]:
        node = idx[ref["m_Id"]]
        fn = node.get("m_FunctionName")
        if not fn:
            continue
        ins = [idx[s["m_Id"]]["m_Id"] for s in node.get("m_Slots", [])
               if idx[s["m_Id"]]["m_SlotType"] == 0]
        outs = [idx[s["m_Id"]]["m_Id"] for s in node.get("m_Slots", [])
                if idx[s["m_Id"]]["m_SlotType"] == 1]
        if ins and outs:
            assert min(outs) > max(ins), \
                f"{path}: {fn} interleaves inputs {sorted(ins)} with outputs {sorted(outs)} — " \
                "these graphs keep every input numbered below every output"
    assert [sid for sid, sd in sorted(cslots.items()) if sd["m_SlotType"] == 0] == \
        list(range(CORRIDOR_INPUT_COUNT)), f"{path}: corridor inputs are not 0..{CORRIDOR_INPUT_COUNT - 1}"
    if path == EXPLODING:
        cid = corridor["m_ObjectId"]
        feeders = [e for e in graph["m_Edges"]
                   if e["m_InputSlot"]["m_Node"]["m_Id"] == cid
                   and e["m_InputSlot"]["m_SlotId"] == CORRIDOR_BASEALPHA]
        assert len(feeders) == 1, f"{path}: BaseAlpha has {len(feeders)} feeders"
        fed = idx[feeders[0]["m_OutputSlot"]["m_Node"]["m_Id"]]
        assert fed.get("m_FunctionName") == CLOCK_FN, \
            f"{path}: BaseAlpha is fed by {fed.get('m_FunctionName')}, not the clock"


def main():
    check = "--check" in sys.argv
    dirty = False
    for path in GRAPHS:
        changes = process(path, check)
        name = path.split("/")[-1]
        if changes:
            dirty = True
            print(f"{name}:")
            for c in changes:
                print(f"   {c}")
        else:
            print(f"{name}: already wired")
    if check and dirty:
        print("\ncheck FAILED: the erosion handoff is not wired into the shipped graphs")
        return 1
    print("\nerosion handoff: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
