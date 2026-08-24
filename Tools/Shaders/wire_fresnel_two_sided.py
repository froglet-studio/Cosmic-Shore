#!/usr/bin/env python3
"""
Make the prism fresnel TWO-SIDED: x = |N.V| in FresnelPower4.shadersubgraph.

Docs/PRISM_ANIMATION.md §4.8.1. The prism look is lerp(dark, bright, (1-x)^4) and
FresnelPower4 computed x with an authored branch:

    x = (N.V > 0) ? N.V : (N.V + 1) * 0.2

For a front-facing fragment (every visible surface of a CLOSED prism) the branch is the
identity. For a BACK-facing one it crushes x into [0, 0.2] - fresnel 0.41..1.0 - so any
back face renders 41-100% RIM, and the rim is the bright, bloomy half of the palette.
Closed boxes never show a back face, which is why this stayed invisible for the life of
the graph; the shield shatter's shards are OPEN faces that show their backs half of every
tumble, and they flashed bright each half-revolution - the reported grain.

(The first fix attempt baked mirrored back faces into the shield meshes instead. It was
REVERTED the same day: prism materials render Cull Off, so BOTH coincident copies
rasterize at every pixel, and two coplanar triangles with reversed winding carry
ULP-different depth planes - z-fighting between two differently-shaded surfaces, worse
than the grain. Under Cull Off, two-sidedness is a FRAGMENT problem, never a geometry
problem.)

The fix replaces the whole branch chain with the mirror:

    BEFORE:  Dot -> Comparison -> Branch(pred, Dot, (Dot+1)*0.2) -> OneMinus -> ...
    AFTER:   Dot -> Absolute -> OneMinus -> ...

abs() only clears the sign bit, so every front-facing fragment - every live prism, every
crystal shell, every explosion debris face - is BIT-IDENTICAL to before. Back-facing
fragments shade exactly like their mirror-image front view: a thin open face looks the
same from both sides, which is also the natural read for the two other consumers
(ShepardGraph's closed crystal shells: no-op in practice; the Fringe spindle planes:
symmetric instead of rim-crushed backs).

Structure-matched rather than id-matched, so it survives a regeneration of the subgraph.
Idempotent: a graph already carrying Dot -> Absolute -> OneMinus prints "already wired".

Usage:  python3 Tools/Shaders/wire_fresnel_two_sided.py [--check]
"""

import json
import os
import sys
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPH = "Assets/_Graphics/Materials/Graphs/FresnelPower4.shadersubgraph"


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


def node_of_type(docs, fragment):
    return [d for d in docs if d.get("m_Type", "").endswith(fragment)]


def assert_acyclic(graph, idx):
    upstream = {}
    for e in graph["m_Edges"]:
        upstream.setdefault(e["m_InputSlot"]["m_Node"]["m_Id"], set()).add(
            e["m_OutputSlot"]["m_Node"]["m_Id"])
    colour = {}

    def walk(node, stack):
        colour[node] = 1
        stack.append(node)
        for parent in upstream.get(node, ()):
            state = colour.get(parent, 0)
            assert state != 1, f"edge cycle through {parent}"
            if state == 0:
                walk(parent, stack)
        stack.pop()
        colour[node] = 2

    for node in list(upstream):
        if colour.get(node, 0) == 0:
            walk(node, [])


def validate(docs, expect_wired):
    idx = index(docs)
    graph = find_graph(docs)

    ids = [d["m_ObjectId"] for d in docs if "m_ObjectId" in d]
    assert len(ids) == len(set(ids)), "duplicate m_ObjectId"
    for ref in graph["m_Nodes"]:
        assert ref["m_Id"] in idx, "m_Nodes references a missing object"
        for s in idx[ref["m_Id"]].get("m_Slots", []):
            assert s["m_Id"] in idx, "node slot missing from file"

    registered = {r["m_Id"] for r in graph["m_Nodes"]}
    feeders = {}
    for e in graph["m_Edges"]:
        for end in (e["m_OutputSlot"], e["m_InputSlot"]):
            assert end["m_Node"]["m_Id"] in registered, "edge references unregistered node"
        key = (e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
    for key, count in feeders.items():
        assert count == 1, f"input slot {key} has {count} feeders"
    assert_acyclic(graph, idx)

    dots = [d for d in node_of_type(docs, "DotProductNode") if d["m_ObjectId"] in registered]
    ones = [d for d in node_of_type(docs, "OneMinusNode") if d["m_ObjectId"] in registered]
    assert len(dots) == 1 and len(ones) == 1, "FresnelPower4's Dot/OneMinus anchors changed"

    branches = [d for d in node_of_type(docs, "BranchNode") if d["m_ObjectId"] in registered]
    abses = [d for d in node_of_type(docs, "AbsoluteNode") if d["m_ObjectId"] in registered]

    if not expect_wired:
        return

    assert not branches, "the back-face branch chain is still present"
    assert len(abses) == 1, "expected exactly one Absolute node"

    sources = {(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]):
               (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])
               for e in graph["m_Edges"]}
    dot, one, ab = dots[0], ones[0], abses[0]
    idx2 = index(docs)
    ab_in = next(idx2[s["m_Id"]]["m_Id"] for s in ab["m_Slots"] if idx2[s["m_Id"]]["m_SlotType"] == 0)
    ab_out = next(idx2[s["m_Id"]]["m_Id"] for s in ab["m_Slots"] if idx2[s["m_Id"]]["m_SlotType"] == 1)
    one_in = next(idx2[s["m_Id"]]["m_Id"] for s in one["m_Slots"] if idx2[s["m_Id"]]["m_SlotType"] == 0)
    assert sources.get((ab["m_ObjectId"], ab_in), (None,))[0] == dot["m_ObjectId"], \
        "Absolute is not fed by the Dot"
    assert sources.get((one["m_ObjectId"], one_in)) == (ab["m_ObjectId"], ab_out), \
        "OneMinus is not fed by the Absolute"


def wire():
    path = os.path.join(REPO, GRAPH)
    docs = load_docs(path)
    validate(docs, expect_wired=False)

    if any(node_of_type(docs, "AbsoluteNode")):
        validate(docs, expect_wired=True)
        print(f"  {os.path.basename(GRAPH)}: already wired")
        return False

    graph = find_graph(docs)
    idx = index(docs)

    dot = node_of_type(docs, "DotProductNode")[0]
    one = node_of_type(docs, "OneMinusNode")[0]
    branch = node_of_type(docs, "BranchNode")[0]
    comparison = node_of_type(docs, "ComparisonNode")[0]

    dot_out = next(idx[s["m_Id"]]["m_Id"] for s in dot["m_Slots"] if idx[s["m_Id"]]["m_SlotType"] == 1)
    one_in = next(idx[s["m_Id"]]["m_Id"] for s in one["m_Slots"] if idx[s["m_Id"]]["m_SlotType"] == 0)

    # The false-branch arithmetic chain: every node from which the Branch's FALSE input
    # is reachable, excluding the Dot itself (shared with the true path).
    sources = {}
    for e in graph["m_Edges"]:
        sources.setdefault((e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]), []).append(
            e["m_OutputSlot"]["m_Node"]["m_Id"])
    doomed = {branch["m_ObjectId"], comparison["m_ObjectId"]}
    frontier = [n for key, ns in sources.items() if key[0] == branch["m_ObjectId"] for n in ns]
    while frontier:
        n = frontier.pop()
        if n in doomed or n == dot["m_ObjectId"]:
            continue
        doomed.add(n)
        frontier.extend(n2 for key, ns in sources.items() if key[0] == n for n2 in ns)
    doomed.discard(dot["m_ObjectId"])

    # Absolute node: clone the OneMinus (same single-in/single-out math-node schema, same
    # DynamicVector slots) and retype it.
    ab = json.loads(json.dumps(one))
    ab["m_ObjectId"] = uuid.uuid4().hex
    ab["m_Type"] = "UnityEditor.ShaderGraph.AbsoluteNode"
    ab["m_Name"] = "Absolute"
    ab["m_DrawState"]["m_Position"]["x"] -= 220.0
    ab_slots = []
    for ref in one["m_Slots"]:
        s = json.loads(json.dumps(idx[ref["m_Id"]]))
        s["m_ObjectId"] = uuid.uuid4().hex
        ab_slots.append(s)
    ab["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in ab_slots]
    ab_in = next(s["m_Id"] for s in ab_slots if s["m_SlotType"] == 0)
    ab_out = next(s["m_Id"] for s in ab_slots if s["m_SlotType"] == 1)

    # Drop every edge touching a doomed node, plus the Dot->(anything doomed) feeds.
    graph["m_Edges"] = [e for e in graph["m_Edges"]
                        if e["m_OutputSlot"]["m_Node"]["m_Id"] not in doomed
                        and e["m_InputSlot"]["m_Node"]["m_Id"] not in doomed]
    graph["m_Edges"].extend([
        {"m_OutputSlot": {"m_Node": {"m_Id": dot["m_ObjectId"]}, "m_SlotId": dot_out},
         "m_InputSlot": {"m_Node": {"m_Id": ab["m_ObjectId"]}, "m_SlotId": ab_in}},
        {"m_OutputSlot": {"m_Node": {"m_Id": ab["m_ObjectId"]}, "m_SlotId": ab_out},
         "m_InputSlot": {"m_Node": {"m_Id": one["m_ObjectId"]}, "m_SlotId": one_in}},
    ])

    # Retire the doomed nodes and their slots from the registry and the file.
    doomed_slotids = {s["m_Id"] for nid in doomed for s in idx[nid].get("m_Slots", [])}
    graph["m_Nodes"] = [r for r in graph["m_Nodes"] if r["m_Id"] not in doomed]
    docs[:] = [d for d in docs
               if d.get("m_ObjectId") not in doomed and d.get("m_ObjectId") not in doomed_slotids]
    docs.append(ab)
    docs.extend(ab_slots)
    graph["m_Nodes"].append({"m_Id": ab["m_ObjectId"]})

    validate(docs, expect_wired=True)
    open(path, "w", encoding="utf-8").write(dump_docs(docs))
    print(f"  {os.path.basename(GRAPH)}: wired — x = |N.V| "
          f"(-{len(doomed)} branch nodes, +1 Absolute)")
    return True


def main():
    if "--check" in sys.argv:
        try:
            validate(load_docs(os.path.join(REPO, GRAPH)), expect_wired=True)
            print(f"  {os.path.basename(GRAPH)}: OK")
            return 0
        except AssertionError as exc:
            print(f"  {os.path.basename(GRAPH)}: NOT WIRED — {exc}")
            return 1
    wire()
    return 0


if __name__ == "__main__":
    sys.exit(main())
