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

THE NEW INPUT IS APPENDED, NOT INSERTED. It takes slot id 6 and is declared LAST in the
HLSL — after the two `out` parameters, which HLSL permits — so every existing slot id on
the Custom Function node keeps its meaning and no existing edge is touched. BlockGraph
gets the same slot and wires nothing into it; Shader Graph then passes the slot's default
0, which is the documented "no erosion here" value, so that graph is unchanged sample for
sample. Both graphs must get the slot even so: the generated call's argument count comes
from the node's own slot list, and a graph one argument short would not compile.

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

CORRIDOR_BASEALPHA = 3
CORRIDOR_EROSION_SLOT = 6
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


def process(path, check):
    docs = load_docs(path)
    graph = find_graph(docs)
    idx = index(docs)
    changes = []

    corridor = cf_by_function(idx, graph, CORRIDOR_FN)
    assert corridor, f"{path}: no {CORRIDOR_FN} node — run wire_prism_occlusion_corridor.py first"
    cslots = slots_of(idx, corridor)

    if CORRIDOR_EROSION_SLOT not in cslots:
        donor = cslots[CORRIDOR_BASEALPHA]
        new_slot = clone_vector1_input(donor)
        docs.append(new_slot)
        corridor["m_Slots"].append({"m_Id": new_slot["m_ObjectId"]})
        changes.append(f"+ {CORRIDOR_FN}.{CORRIDOR_EROSION_NAME} (slot {CORRIDOR_EROSION_SLOT})")

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
