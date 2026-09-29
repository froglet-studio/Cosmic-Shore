#!/usr/bin/env python3
"""
Make the debris face rotation pivot about the MESH's own per-face centroid when the mesh
knows where that is.

THE DEFECT (Docs/PRISM_ANIMATION.md §4.8.2)
-------------------------------------------
`RotateFacesAlongAxis` spins each face of a dying prism about a pivot it DERIVES:

    Pn    = dot(P, N) * N          the foot of the perpendicular from the object origin
                                   onto the face's plane
    slide = 0.5 * (0.5 + S) * T    a fixed step along the face's TANGENT
    pivot = Pn + slide             (the rotation is rot(P - pivot) + pivot)

The `0.5` inside `slide` is not a general fact about faces — it is a hardcoded measurement
of the PRISM CUBE, whose mesh carries twice a normal cube's triangles precisely so each
face is four isoceles wedges fanned from a face-centre vertex. Each wedge's own centre sits
half a face-half-width along its tangent, so `Pn + 0.25*T` lands the pivot on the wedge and
the four wedges spin in place as they rotate away. Correct, and load-bearing for how a
prism comes apart.

It is meaningless for any other mesh, and since §4.8.1 the shield tiers shatter through
this exact pipeline on their OWN meshes:

  * the OCTAHEDRON's faces are single triangles whose plane-foot IS their centroid when the
    mesh is regular, so the slide pushes the pivot off centre for no reason;
  * the STELLA's 24 faces are the lateral faces of eight spike tetrahedra, and three of them
    share one tetrahedron-face PLANE — so `Pn` is that big triangle's centre, which lies in
    the hole between the three small ones. The pivot is OUTSIDE the face it is meant to spin,
    a full circumradius away, before the slide is even added.

Both meshes already bake the exact per-face CENTROID into TEXCOORD1 (the ENGAGE bloom's
pivot, `Octahedron/StellatedOctahedronMeshGenerator.FaceCentroidUVChannel`) and
ExplodingBlockGraph already reads that channel. So the fix is to ASK the mesh instead of
assuming the cube — §4.8.1's own rule (port the mesh into the pipeline, never the pipeline
into the mesh) applied one level up, to the pipeline's hardcoded idea of what a face is.

THE FIX
-------
`RotateFacesAlongAxis` gains two inputs and lerps its pivot:

    pivot' = lerp(Pn + slide, FaceCentroid, CentroidPivotWeight)

implemented as a delta subtracted before the rotation and added back after it, which IS a
pivot shift. The delta is in-plane by construction (both endpoints lie in the face plane),
so the subgraph's own `dot(P, N) * N` decomposition is untouched by it.

At weight 0 the delta is exactly `0 * finite == 0`, so the cube path is BIT-IDENTICAL — no
retune, no look change, nothing to re-approve. At weight 1 every face spins about its own
centroid, which is right for both shield meshes and for any future generated mesh that
bakes the channel.

The weight rides ONE new Hybrid-Per-Instance float (`_FacePivotFromCentroid`), because
shield shards and prism debris share ExplodingBlockMaterial by design (§4.8.1) — so it
cannot be a material constant, and a shader keyword or a duplicate graph would both split
that batch AND fork the pipeline. One float per debris entity and one `mad` in the vertex
stage is the cheapest correct place to put it.

Implemented as a lerp rather than as "switch the slide off" because switching it off is
only right for the octahedron; the stella needs the centroid.

WHAT THIS WRITES
----------------
RotateFacesAlongAxis.shadersubgraph
  properties:  FaceCentroid (Vector3), CentroidPivotWeight (Vector1)
  nodes:       2 Property, 1 Tangent Slider (the negated slide), Subtract/Add/Multiply
               (the delta), and a Subtract/Add pair splicing the delta around the rotation

ExplodingBlockGraph.shadergraph
  property:    FacePivotFromCentroid (Vector1, EXPOSED + Hybrid Per Instance)
  node:        1 Property
  slots:       two new inputs on the Rotate Faces Along Axis node. Their integer ids are
               `Guid.GetHashCode()` of the subgraph property guids — the XOR of the guid's
               four little-endian 32-bit words — which is why the guids above are FIXED and
               not minted per run. Verified against all seven (guid, id) pairs the shipped
               node already serializes in m_PropertyGuids / m_PropertyIds, including one
               stale entry for a subgraph property that no longer exists.
  edges:       the EXISTING UV1 node (already feeding PrismShieldMorph.FaceCentroid) also
               feeds the new FaceCentroid input, so the two consumers of that mesh channel
               cannot drift apart; the new property feeds the weight.

Out-of-editor ShaderGraph JSON synthesis per the /asset-surgery protocol: parse the whole
file, clone same-file donors so the schema is exact by construction, rebuild in memory,
assert every invariant (unique ids, resolvable registries, exactly one feeder per input
slot, ACYCLIC, property-node slot types consistent with their properties), and only then
write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0, which
also makes it the resolver for a ShaderGraph merge conflict on either file.

Usage:  python3 Tools/Shaders/wire_prism_face_pivot.py [--check]
        --check validates without writing (exit 1 if not wired).
"""

import json
import os
import struct
import sys
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

SUBGRAPH = "Assets/_Graphics/Materials/Graphs/PrismGraphs/Subgraphs/RotateFacesAlongAxis.shadersubgraph"
GRAPH = "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph"

# GUID of RotateFacesAlongAxis.shadersubgraph, pinned by its committed .meta. The
# SubGraphNode is matched by this, never by display name, so a rename cannot make the
# wirer splice into some other subgraph node.
ROTATE_SUBGRAPH_GUID = "401299b632063834e9b5e6990a638021"
# GUID of TangentSlider.shadersubgraph — the node cloned to produce the negated slide.
TANGENT_SLIDER_GUID = "a9f4c4477592253459d83f208f3b2fcf"
# GUID of PrismClockAnimation.hlsl — the shield morph's source, used only to locate the
# UV1 node the morph already reads.
SHIELD_MORPH_FUNCTION = "PrismShieldMorph"

# New subgraph inputs. FIXED, never minted per run: they determine the integer slot ids on
# every SubGraphNode that consumes this subgraph.
FACE_CENTROID_GUID = "f1c3b7a2-4e6d-4a91-9c05-2b7e8d3f1a44"
PIVOT_WEIGHT_GUID = "c0d5e8b1-73a4-4f2e-8b16-9d4a5c2e7f30"

FACE_CENTROID_NAME = "FaceCentroid"
FACE_CENTROID_REF = "_FaceCentroid"
PIVOT_WEIGHT_NAME = "CentroidPivotWeight"
PIVOT_WEIGHT_REF = "_CentroidPivotWeight"

# The parent-graph property that drives the weight per debris entity.
PARENT_PROP_NAME = "FacePivotFromCentroid"
PARENT_PROP_REF = "_FacePivotFromCentroid"

# ShaderGraph UVChannel enum: UV1 = 1. Must match
# OctahedronMeshGenerator.FaceCentroidUVChannel.
UV_CHANNEL_UV1 = 1

# TangentSlider's own input slot ids (the Guid.GetHashCode()s of ITS two properties).
# Asserted against the donor rather than trusted.
TS_POSITION_SLOT = -184269224
TS_SPREAD_SLOT = 1077851116
TS_OUT_SLOT = 1

# Binary-node slot convention shared by Add / Subtract / Multiply.
A_IN, B_IN, OUT = 0, 1, 2


# ---------------------------------------------------------------------------
# parse / serialize
# ---------------------------------------------------------------------------

def load_docs(path):
    """.shadergraph / .shadersubgraph are CONCATENATED JSON documents, not one document."""
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


def index(docs):
    return {d["m_ObjectId"]: d for d in docs if "m_ObjectId" in d}


def find_graph(docs):
    return next(d for d in docs if "GraphData" in d.get("m_Type", ""))


def guid_slot_id(guid_string):
    """SubGraphNode input slot id == Guid.GetHashCode() == XOR of the guid's four
    little-endian 32-bit words."""
    words = struct.unpack("<4I", uuid.UUID(guid_string).bytes_le)
    h = words[0] ^ words[1] ^ words[2] ^ words[3]
    return h - (1 << 32) if h >= (1 << 31) else h


def nodes_of(docs, graph, type_fragment):
    idx = index(docs)
    return [idx[r["m_Id"]] for r in graph["m_Nodes"]
            if type_fragment in idx[r["m_Id"]].get("m_Type", "")]


def slot_docs(idx, node):
    return [idx[s["m_Id"]] for s in node.get("m_Slots", [])]


def slot_by_int(idx, node, int_id):
    for s in slot_docs(idx, node):
        if s["m_Id"] == int_id:
            return s
    return None


def find_property(docs, name):
    for d in docs:
        if d.get("m_Name") == name and "ShaderProperty" in d.get("m_Type", ""):
            return d
    return None


def source_map(graph):
    return {(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]):
            (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])
            for e in graph["m_Edges"]}


def edge(out_node, out_slot, in_node, in_slot):
    return {
        "m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
        "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot},
    }


def one(seq, what):
    seq = list(seq)
    assert len(seq) == 1, f"expected exactly one {what}, found {len(seq)}"
    return seq[0]


# ---------------------------------------------------------------------------
# generic validation — runs on BOTH files, before and after the edit
# ---------------------------------------------------------------------------

def validate_structure(docs, label):
    idx = index(docs)
    graph = find_graph(docs)

    ids = [d["m_ObjectId"] for d in docs if "m_ObjectId" in d]
    assert len(ids) == len(set(ids)), f"{label}: duplicate m_ObjectId"

    for ref in graph["m_Nodes"]:
        assert ref["m_Id"] in idx, f"{label}: m_Nodes references missing {ref['m_Id']}"
    for ref in graph["m_Properties"]:
        assert ref["m_Id"] in idx, f"{label}: m_Properties references missing {ref['m_Id']}"
    for cat in graph["m_CategoryData"]:
        for child in idx[cat["m_Id"]]["m_ChildObjectList"]:
            assert child["m_Id"] in idx, f"{label}: category child missing"

    slots = {}
    for ref in graph["m_Nodes"]:
        node = idx[ref["m_Id"]]
        ints = set()
        for s in node.get("m_Slots", []):
            assert s["m_Id"] in idx, f"{label}: node {ref['m_Id']} slot {s['m_Id']} missing"
            sd = idx[s["m_Id"]]
            assert sd["m_Id"] not in ints, f"{label}: duplicate integer slot id on node {ref['m_Id']}"
            ints.add(sd["m_Id"])
        slots[ref["m_Id"]] = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in node.get("m_Slots", [])}

    feeders, adjacency = {}, {}
    for e in graph["m_Edges"]:
        o, i = e["m_OutputSlot"], e["m_InputSlot"]
        for end, side in ((o, "output"), (i, "input")):
            nid = end["m_Node"]["m_Id"]
            assert nid in slots, f"{label}: edge {side} node {nid} not registered in m_Nodes"
            assert end["m_SlotId"] in slots[nid], \
                f"{label}: edge {side} slot {end['m_SlotId']} missing on {nid}"
        assert slots[o["m_Node"]["m_Id"]][o["m_SlotId"]]["m_SlotType"] == 1, \
            f"{label}: edge output is not an output slot"
        assert slots[i["m_Node"]["m_Id"]][i["m_SlotId"]]["m_SlotType"] == 0, \
            f"{label}: edge input is not an input slot"
        key = (i["m_Node"]["m_Id"], i["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
        adjacency.setdefault(o["m_Node"]["m_Id"], set()).add(i["m_Node"]["m_Id"])
    for key, count in feeders.items():
        assert count == 1, f"{label}: input slot {key} has {count} feeders (must be exactly 1)"

    # A cycle makes the WHOLE graph magenta on import — including effects this edit never
    # touched — so it is asserted over the whole edge list, not just the new nodes.
    WHITE, GREY, BLACK = 0, 1, 2
    colour = {}

    def visit(root):
        colour[root] = GREY
        stack = [(root, iter(sorted(adjacency.get(root, ()))))]
        while stack:
            node, it = stack[-1]
            nxt = next(it, None)
            if nxt is None:
                colour[node] = BLACK
                stack.pop()
                continue
            assert colour.get(nxt, WHITE) != GREY, f"{label}: edge cycle through {nxt}"
            if colour.get(nxt, WHITE) == WHITE:
                colour[nxt] = GREY
                stack.append((nxt, iter(sorted(adjacency.get(nxt, ())))))

    for ref in graph["m_Nodes"]:
        if colour.get(ref["m_Id"], WHITE) == WHITE:
            visit(ref["m_Id"])

    # A property node cloned from a donor of the wrong KIND wires "successfully" and
    # delivers a silent zero, with no import error and no magenta.
    want_slot = {"Vector1ShaderProperty": "Vector1MaterialSlot",
                 "Vector2ShaderProperty": "Vector2MaterialSlot",
                 "Vector3ShaderProperty": "Vector3MaterialSlot",
                 "Vector4ShaderProperty": "Vector4MaterialSlot",
                 "ColorShaderProperty": "Vector4MaterialSlot"}
    for node in nodes_of(docs, graph, "PropertyNode"):
        prop = idx[node["m_Property"]["m_Id"]]
        want = want_slot.get(prop["m_Type"].split(".")[-1])
        if want is None:
            continue
        got = slot_docs(idx, node)[0]["m_Type"].split(".")[-1]
        assert got == want, \
            f"{label}: property node for {prop['m_Name']} carries a {got} (expected {want})"


# ---------------------------------------------------------------------------
# builders — every one clones a same-file donor of the same type
# ---------------------------------------------------------------------------

def clone(doc, **overrides):
    c = json.loads(json.dumps(doc))
    c["m_ObjectId"] = new_oid()
    c.update(overrides)
    return c


def zero_slot(slot):
    """Force a slot's unconnected default to zero, whatever width it serializes."""
    for key in ("m_Value", "m_DefaultValue"):
        v = slot.get(key)
        if isinstance(v, dict):
            slot[key] = {k: 0.0 for k in v}
        elif isinstance(v, (int, float)):
            slot[key] = 0.0
    return slot


def clone_node(idx, donor, x, y, zero_defaults=True):
    """Clone a node together with fresh copies of every one of its slot docs."""
    node = clone(donor)
    node["m_Group"] = {"m_Id": ""}
    node["m_DrawState"] = json.loads(json.dumps(donor["m_DrawState"]))
    node["m_DrawState"]["m_Position"]["x"] = float(x)
    node["m_DrawState"]["m_Position"]["y"] = float(y)
    slots = [clone(idx[s["m_Id"]]) for s in donor.get("m_Slots", [])]
    if zero_defaults:
        for s in slots:
            zero_slot(s)
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def make_property(donor, guid, name, reference, value, per_instance):
    """per_instance=True adds the Hybrid-Per-Instance HLSL declaration (a per-entity stamp);
    False leaves the donor's plain exposure, which is what every SUBGRAPH input carries —
    there the property IS the node's input port and the flag means nothing."""
    p = clone(donor)
    p["m_Guid"] = {"m_GuidSerialized": guid}
    p["m_Name"] = name
    p["m_RefNameGeneratedByDisplayName"] = name
    p["m_DefaultReferenceName"] = reference
    p["m_OverrideReferenceName"] = ""
    p["m_GeneratePropertyBlock"] = True
    p["overrideHLSLDeclaration"] = bool(per_instance)
    p["hlslDeclarationOverride"] = 3 if per_instance else 0
    p["m_Hidden"] = False
    p["m_Value"] = value
    return p


def make_property_node(idx, donor_node, prop_oid, label, x, y):
    node, slots = clone_node(idx, donor_node, x, y)
    node["m_Property"] = {"m_Id": prop_oid}
    slots[0]["m_Id"] = 0
    slots[0]["m_DisplayName"] = label
    slots[0]["m_ShaderOutputName"] = "Out"
    slots[0]["m_SlotType"] = 1
    return node, slots


# ---------------------------------------------------------------------------
# PART A — RotateFacesAlongAxis.shadersubgraph
# ---------------------------------------------------------------------------

def subgraph_anchors(docs):
    """Locate the nodes the splice hangs off, asserting each is unambiguous."""
    idx = index(docs)
    graph = find_graph(docs)
    sources = source_map(graph)

    # The centering constant: the Subtract whose A input is the unconnected (-0.5)^3 that
    # this whole change exists to stop applying to non-cube meshes.
    def is_half_const(node):
        if "SubtractNode" not in node.get("m_Type", ""):
            return False
        if (node["m_ObjectId"], A_IN) in sources:
            return False
        a = slot_by_int(idx, node, A_IN)
        v = a.get("m_Value") if a else None
        return isinstance(v, dict) and all(abs(v.get(k, 0.0) + 0.5) < 1e-6 for k in "xyz")

    slide_const = one((n for n in nodes_of(docs, graph, "SubtractNode") if is_half_const(n)),
                      "Subtract carrying the -0.5 face-centering constant")

    # Pn = dot(P, N) * N — the Multiply whose A input comes from the Dot Product.
    def fed_by_dot(node):
        src = sources.get((node["m_ObjectId"], A_IN))
        return src is not None and "DotProductNode" in idx[src[0]].get("m_Type", "")

    pn = one((n for n in nodes_of(docs, graph, "MultiplyNode") if fed_by_dot(n)),
             "Multiply computing dot(Position, Normal) * Normal")

    sliders = [n for n in nodes_of(docs, graph, "SubGraphNode")
               if TANGENT_SLIDER_GUID in str(n.get("m_SerializedSubGraph", ""))]
    for s in sliders:
        for sid in (TS_POSITION_SLOT, TS_SPREAD_SLOT, TS_OUT_SLOT):
            assert slot_by_int(idx, s, sid) is not None, \
                f"Tangent Slider is missing slot {sid} — its property guids changed"

    # The two sliders the ROTATION owns both slide a real position; the third one this
    # wirer adds slides ZERO (its Position input is deliberately unconnected), so the
    # anchors stay unambiguous once the graph is wired and the wirer is re-runnable.
    positioned = [n for n in sliders if (n["m_ObjectId"], TS_POSITION_SLOT) in sources]
    assert len(positioned) == 2, \
        f"expected 2 position-fed Tangent Slider nodes, found {len(positioned)}"

    slide_out = one((n for n in positioned
                     if sources.get((n["m_ObjectId"], TS_SPREAD_SLOT), (None,))[0]
                     == slide_const["m_ObjectId"]),
                    "Tangent Slider fed by the centering constant (the slide OUT)")
    slide_back = one((n for n in positioned if n["m_ObjectId"] != slide_out["m_ObjectId"]),
                     "the other Tangent Slider (the slide BACK)")

    out_node = one(nodes_of(docs, graph, "SubGraphOutputNode"), "SubGraphOutputNode")

    slide_out_edge = one((e for e in graph["m_Edges"]
                          if e["m_OutputSlot"]["m_Node"]["m_Id"] == slide_out["m_ObjectId"]
                          and e["m_OutputSlot"]["m_SlotId"] == TS_OUT_SLOT),
                         "consumer of the slide-out Tangent Slider")
    slide_back_edge = one((e for e in graph["m_Edges"]
                           if e["m_OutputSlot"]["m_Node"]["m_Id"] == slide_back["m_ObjectId"]
                           and e["m_OutputSlot"]["m_SlotId"] == TS_OUT_SLOT),
                          "consumer of the slide-back Tangent Slider")

    return dict(slide_const=slide_const, pn=pn, slide_out=slide_out, slide_back=slide_back,
                slide_out_edge=slide_out_edge, slide_back_edge=slide_back_edge, out_node=out_node)


def subgraph_is_wired(docs):
    return find_property(docs, FACE_CENTROID_NAME) is not None


def validate_subgraph(docs):
    """Assert the pivot lerp is present and shaped exactly as designed."""
    idx = index(docs)
    graph = find_graph(docs)
    sources = source_map(graph)

    for name, ref, guid, kind in (
            (FACE_CENTROID_NAME, FACE_CENTROID_REF, FACE_CENTROID_GUID, "Vector3ShaderProperty"),
            (PIVOT_WEIGHT_NAME, PIVOT_WEIGHT_REF, PIVOT_WEIGHT_GUID, "Vector1ShaderProperty")):
        p = find_property(docs, name)
        assert p is not None, f"subgraph property {name} missing"
        assert p["m_Type"].endswith(kind), f"{name} is a {p['m_Type']} (expected {kind})"
        assert p["m_DefaultReferenceName"] == ref, f"{name} reference name drifted"
        assert p["m_Guid"]["m_GuidSerialized"] == guid, \
            f"{name}'s guid drifted — every consumer's slot id is derived from it"
        assert p["m_GeneratePropertyBlock"] is True and p["hlslDeclarationOverride"] == 0, \
            f"{name}'s exposure flags do not match its sibling subgraph inputs"
        assert any(r["m_Id"] == p["m_ObjectId"] for r in graph["m_Properties"]), \
            f"{name} not registered in m_Properties"
        assert any(any(c["m_Id"] == p["m_ObjectId"] for c in idx[cat["m_Id"]]["m_ChildObjectList"])
                   for cat in graph["m_CategoryData"]), f"{name} not in any blackboard category"

    a = subgraph_anchors(docs)

    def prop_node(name):
        prop = find_property(docs, name)
        return one((n for n in nodes_of(docs, graph, "PropertyNode")
                    if n["m_Property"]["m_Id"] == prop["m_ObjectId"]), f"{name} property node")

    centroid_node = prop_node(FACE_CENTROID_NAME)
    weight_node = prop_node(PIVOT_WEIGHT_NAME)

    delta = one((idx[e["m_InputSlot"]["m_Node"]["m_Id"]] for e in graph["m_Edges"]
                 if e["m_OutputSlot"]["m_Node"]["m_Id"] == weight_node["m_ObjectId"]),
                "consumer of CentroidPivotWeight")
    assert "MultiplyNode" in delta["m_Type"], "the weight does not feed a Multiply"
    assert sources[(delta["m_ObjectId"], B_IN)][0] == weight_node["m_ObjectId"], \
        "the weight is not the Multiply's B input"

    delta_sum = idx[sources[(delta["m_ObjectId"], A_IN)][0]]
    assert "AddNode" in delta_sum["m_Type"], "the delta's A input is not an Add"

    c_minus_pn = idx[sources[(delta_sum["m_ObjectId"], A_IN)][0]]
    assert "SubtractNode" in c_minus_pn["m_Type"], "C - Pn is not a Subtract"
    assert sources[(c_minus_pn["m_ObjectId"], A_IN)][0] == centroid_node["m_ObjectId"], \
        "the pivot delta does not start from FaceCentroid"
    assert sources[(c_minus_pn["m_ObjectId"], B_IN)][0] == a["pn"]["m_ObjectId"], \
        "the pivot delta does not subtract dot(Position, Normal) * Normal"

    neg_slide = idx[sources[(delta_sum["m_ObjectId"], B_IN)][0]]
    assert TANGENT_SLIDER_GUID in str(neg_slide.get("m_SerializedSubGraph", "")), \
        "the negated slide is not a Tangent Slider node"
    assert (neg_slide["m_ObjectId"], TS_POSITION_SLOT) not in sources, \
        "the negated slide's Position input must stay unconnected at zero"
    pos_slot = slot_by_int(idx, neg_slide, TS_POSITION_SLOT)
    assert all(abs(v) < 1e-9 for v in pos_slot["m_Value"].values()), \
        "the negated slide's Position default is not zero, so it adds a constant offset"
    assert sources[(neg_slide["m_ObjectId"], TS_SPREAD_SLOT)][0] == a["slide_const"]["m_ObjectId"], \
        "the negated slide is not driven by the same (-0.5 - SpreadValue) the rotation uses"

    # The splice: delta out before the rotation, delta back in after it.
    pre = idx[a["slide_out_edge"]["m_InputSlot"]["m_Node"]["m_Id"]]
    assert "SubtractNode" in pre["m_Type"], \
        "the slide-out slider no longer feeds the pre-rotation Subtract"
    assert a["slide_out_edge"]["m_InputSlot"]["m_SlotId"] == A_IN, \
        "the slide-out slider must feed the pre-splice Subtract's A input"
    assert sources[(pre["m_ObjectId"], B_IN)][0] == delta["m_ObjectId"], \
        "the pre-rotation Subtract does not subtract the pivot delta"

    post = idx[a["slide_back_edge"]["m_InputSlot"]["m_Node"]["m_Id"]]
    assert "AddNode" in post["m_Type"], \
        "the slide-back slider no longer feeds the post-rotation Add"
    assert a["slide_back_edge"]["m_InputSlot"]["m_SlotId"] == A_IN, \
        "the slide-back slider must feed the post-splice Add's A input"
    assert sources[(post["m_ObjectId"], B_IN)][0] == delta["m_ObjectId"], \
        "the post-rotation Add does not add the pivot delta back"
    assert sources[(a["out_node"]["m_ObjectId"], 1)][0] == post["m_ObjectId"], \
        "the subgraph Position output is not the post-rotation Add"


def wire_subgraph(path):
    docs = load_docs(path)
    validate_structure(docs, "RotateFacesAlongAxis (before)")

    if subgraph_is_wired(docs):
        validate_subgraph(docs)
        print("  RotateFacesAlongAxis.shadersubgraph: already wired")
        return docs, False

    idx = index(docs)
    graph = find_graph(docs)
    a = subgraph_anchors(docs)

    # Donors — same file, same type, so the serializer version is right by construction.
    donor_v3_prop = find_property(docs, "Position")
    donor_v1_prop = find_property(docs, "ExplosionAmount")
    assert donor_v3_prop and donor_v1_prop, "the Vector3/Vector1 property donors are gone"
    donor_v3_node = one((n for n in nodes_of(docs, graph, "PropertyNode")
                         if n["m_Property"]["m_Id"] == donor_v3_prop["m_ObjectId"]),
                        "Position property node (Vector3 donor)")
    donor_v1_node = one((n for n in nodes_of(docs, graph, "PropertyNode")
                         if n["m_Property"]["m_Id"] == donor_v1_prop["m_ObjectId"]),
                        "ExplosionAmount property node (Vector1 donor)")
    donor_sub = a["slide_const"]
    donor_add = nodes_of(docs, graph, "AddNode")[0]
    donor_mul = a["pn"]

    new_docs, new_nodes = [], []

    def add(pair):
        node, slots = pair
        new_docs.append(node)
        new_docs.extend(slots)
        new_nodes.append(node["m_ObjectId"])
        return node

    centroid_prop = make_property(donor_v3_prop, FACE_CENTROID_GUID, FACE_CENTROID_NAME,
                                  FACE_CENTROID_REF,
                                  {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}, per_instance=False)
    weight_prop = make_property(donor_v1_prop, PIVOT_WEIGHT_GUID, PIVOT_WEIGHT_NAME,
                                PIVOT_WEIGHT_REF, 0.0, per_instance=False)
    new_docs += [centroid_prop, weight_prop]

    centroid_node = add(make_property_node(idx, donor_v3_node, centroid_prop["m_ObjectId"],
                                           FACE_CENTROID_NAME, -1085.0, 1150.0))
    weight_node = add(make_property_node(idx, donor_v1_node, weight_prop["m_ObjectId"],
                                         PIVOT_WEIGHT_NAME, -1085.0, 1260.0))

    # negSlide = TangentSlider(0, -0.5 - SpreadValue) = -0.5 * (0.5 + S) * T
    neg_slide = add(clone_node(idx, a["slide_out"], 300.0, 1150.0))
    c_minus_pn = add(clone_node(idx, donor_sub, -520.0, 1150.0))
    delta_sum = add(clone_node(idx, donor_add, 600.0, 1260.0))
    delta = add(clone_node(idx, donor_mul, 880.0, 1260.0))
    pre = add(clone_node(idx, donor_sub, 330.0, 150.0))
    post = add(clone_node(idx, donor_add, 1150.0, 60.0))

    # Where the two sliders used to feed. Captured BEFORE the retarget: the splice nodes
    # take over those inputs, and the originals now feed the splice nodes' A slots.
    pre_target = dict(a["slide_out_edge"]["m_InputSlot"])
    post_target = dict(a["slide_back_edge"]["m_InputSlot"])
    a["slide_out_edge"]["m_InputSlot"] = {"m_Node": {"m_Id": pre["m_ObjectId"]}, "m_SlotId": A_IN}
    a["slide_back_edge"]["m_InputSlot"] = {"m_Node": {"m_Id": post["m_ObjectId"]}, "m_SlotId": A_IN}

    graph["m_Edges"] += [
        edge(centroid_node["m_ObjectId"], 0, c_minus_pn["m_ObjectId"], A_IN),
        edge(a["pn"]["m_ObjectId"], OUT, c_minus_pn["m_ObjectId"], B_IN),
        edge(a["slide_const"]["m_ObjectId"], OUT, neg_slide["m_ObjectId"], TS_SPREAD_SLOT),
        edge(c_minus_pn["m_ObjectId"], OUT, delta_sum["m_ObjectId"], A_IN),
        edge(neg_slide["m_ObjectId"], TS_OUT_SLOT, delta_sum["m_ObjectId"], B_IN),
        edge(delta_sum["m_ObjectId"], OUT, delta["m_ObjectId"], A_IN),
        edge(weight_node["m_ObjectId"], 0, delta["m_ObjectId"], B_IN),
        edge(delta["m_ObjectId"], OUT, pre["m_ObjectId"], B_IN),
        edge(delta["m_ObjectId"], OUT, post["m_ObjectId"], B_IN),
        edge(pre["m_ObjectId"], OUT, pre_target["m_Node"]["m_Id"], pre_target["m_SlotId"]),
        edge(post["m_ObjectId"], OUT, post_target["m_Node"]["m_Id"], post_target["m_SlotId"]),
    ]

    graph["m_Properties"] += [{"m_Id": centroid_prop["m_ObjectId"]},
                              {"m_Id": weight_prop["m_ObjectId"]}]
    idx[graph["m_CategoryData"][0]["m_Id"]]["m_ChildObjectList"] += [
        {"m_Id": centroid_prop["m_ObjectId"]}, {"m_Id": weight_prop["m_ObjectId"]}]
    graph["m_Nodes"] += [{"m_Id": n} for n in new_nodes]
    docs += new_docs

    validate_structure(docs, "RotateFacesAlongAxis (after)")
    validate_subgraph(docs)
    print("  RotateFacesAlongAxis.shadersubgraph: wired "
          f"(+2 properties, +{len(new_nodes)} nodes, +11 edges)")
    return docs, True


# ---------------------------------------------------------------------------
# PART B — ExplodingBlockGraph.shadergraph
# ---------------------------------------------------------------------------

def find_rotate_node(docs, graph):
    return one((n for n in nodes_of(docs, graph, "SubGraphNode")
                if ROTATE_SUBGRAPH_GUID in str(n.get("m_SerializedSubGraph", ""))),
               "Rotate Faces Along Axis SubGraphNode")


def find_centroid_uv_node(docs, graph):
    """The UV1 node PrismShieldMorph already reads its FaceCentroid from — reused so the
    two consumers of that mesh channel cannot drift onto different channels."""
    idx = index(docs)
    uvs = [n for n in nodes_of(docs, graph, "UVNode") if n.get("m_OutputChannel") == UV_CHANNEL_UV1]
    node = one(uvs, f"UV node reading channel UV{UV_CHANNEL_UV1}")
    morph = [n for n in nodes_of(docs, graph, "CustomFunctionNode")
             if n.get("m_FunctionName") == SHIELD_MORPH_FUNCTION]
    if morph:
        consumers = {e["m_OutputSlot"]["m_Node"]["m_Id"] for e in graph["m_Edges"]
                     if e["m_InputSlot"]["m_Node"]["m_Id"] == morph[0]["m_ObjectId"]}
        assert node["m_ObjectId"] in consumers, \
            "the UV1 node does not feed PrismShieldMorph — the centroid channel moved"
    return node


def graph_is_wired(docs):
    return find_property(docs, PARENT_PROP_NAME) is not None


def validate_graph(docs):
    idx = index(docs)
    graph = find_graph(docs)
    sources = source_map(graph)

    p = find_property(docs, PARENT_PROP_NAME)
    assert p is not None, f"{PARENT_PROP_NAME} property missing"
    assert p["m_DefaultReferenceName"] == PARENT_PROP_REF, "reference name drifted"
    assert p["m_GeneratePropertyBlock"] is True, f"{PARENT_PROP_NAME} must be EXPOSED"
    assert p["overrideHLSLDeclaration"] is True and p["hlslDeclarationOverride"] == 3, \
        (f"{PARENT_PROP_NAME} must be Hybrid Per Instance (3) — shield shards and prism "
         "debris share one material, so a per-material value cannot separate them")
    assert p["m_Value"] == 0.0, \
        "the default must be 0 (the legacy derived pivot) so unstamped debris is unchanged"
    assert any(r["m_Id"] == p["m_ObjectId"] for r in graph["m_Properties"]), \
        f"{PARENT_PROP_NAME} not registered in m_Properties"
    assert any(any(c["m_Id"] == p["m_ObjectId"] for c in idx[cat["m_Id"]]["m_ChildObjectList"])
               for cat in graph["m_CategoryData"]), f"{PARENT_PROP_NAME} not in any category"

    rotate = find_rotate_node(docs, graph)
    centroid_slot_id = guid_slot_id(FACE_CENTROID_GUID)
    weight_slot_id = guid_slot_id(PIVOT_WEIGHT_GUID)

    cs = slot_by_int(idx, rotate, centroid_slot_id)
    ws = slot_by_int(idx, rotate, weight_slot_id)
    assert cs is not None, "the FaceCentroid input slot is missing from the rotate node"
    assert ws is not None, "the CentroidPivotWeight input slot is missing from the rotate node"
    assert cs["m_Type"].endswith("Vector3MaterialSlot"), "FaceCentroid slot is not a Vector3"
    assert ws["m_Type"].endswith("Vector1MaterialSlot"), "CentroidPivotWeight slot is not a Vector1"
    assert cs["m_ShaderOutputName"] == FACE_CENTROID_REF and cs["m_SlotType"] == 0
    assert ws["m_ShaderOutputName"] == PIVOT_WEIGHT_REF and ws["m_SlotType"] == 0

    guids, ids = rotate["m_PropertyGuids"], rotate["m_PropertyIds"]
    assert len(guids) == len(ids), "m_PropertyGuids / m_PropertyIds fell out of alignment"
    for guid, sid in ((FACE_CENTROID_GUID, centroid_slot_id), (PIVOT_WEIGHT_GUID, weight_slot_id)):
        assert guid in guids, f"{guid} missing from the rotate node's m_PropertyGuids"
        assert ids[guids.index(guid)] == sid, f"{guid} maps to the wrong slot id"

    uv_node = find_centroid_uv_node(docs, graph)
    assert sources[(rotate["m_ObjectId"], centroid_slot_id)][0] == uv_node["m_ObjectId"], \
        "FaceCentroid is not fed by the UV1 node the shield morph reads"
    weight_src = idx[sources[(rotate["m_ObjectId"], weight_slot_id)][0]]
    assert "PropertyNode" in weight_src["m_Type"] and \
        weight_src["m_Property"]["m_Id"] == p["m_ObjectId"], \
        f"CentroidPivotWeight is not fed by {PARENT_PROP_NAME}"


def wire_graph(path):
    docs = load_docs(path)
    validate_structure(docs, "ExplodingBlockGraph (before)")

    if graph_is_wired(docs):
        validate_graph(docs)
        print("  ExplodingBlockGraph.shadergraph: already wired")
        return docs, False

    idx = index(docs)
    graph = find_graph(docs)
    rotate = find_rotate_node(docs, graph)
    uv_node = find_centroid_uv_node(docs, graph)

    # Donor property: an existing Hybrid-Per-Instance Vector1 stamp, so the exposure flags
    # are right by construction (and asserted anyway).
    donor_prop = find_property(docs, "ShieldMorphDuration")
    assert donor_prop is not None and donor_prop.get("hlslDeclarationOverride") == 3, \
        "the Hybrid-Per-Instance Vector1 property donor is gone"
    donor_prop_node = one((n for n in nodes_of(docs, graph, "PropertyNode")
                           if n["m_Property"]["m_Id"] == donor_prop["m_ObjectId"]),
                          "ShieldMorphDuration property node (donor)")

    # Donor slots: the rotate node's own inputs, so the SubGraphNode slot schema is exact.
    existing_ints = {s["m_Id"] for s in slot_docs(idx, rotate)}
    donor_v3_slot = one((s for s in slot_docs(idx, rotate)
                         if s["m_Type"].endswith("Vector3MaterialSlot") and s["m_SlotType"] == 0
                         and s["m_ShaderOutputName"] == "_Position"),
                        "Vector3 input slot donor on the rotate node")
    donor_v1_slot = one((s for s in slot_docs(idx, rotate)
                         if s["m_Type"].endswith("Vector1MaterialSlot") and s["m_SlotType"] == 0
                         and s["m_ShaderOutputName"] == "_ExplosionAmount"),
                        "Vector1 input slot donor on the rotate node")

    centroid_slot_id = guid_slot_id(FACE_CENTROID_GUID)
    weight_slot_id = guid_slot_id(PIVOT_WEIGHT_GUID)
    assert centroid_slot_id != weight_slot_id, "the two new subgraph guids collide"
    for sid, name in ((centroid_slot_id, FACE_CENTROID_NAME), (weight_slot_id, PIVOT_WEIGHT_NAME)):
        assert sid not in existing_ints, \
            f"{name}'s derived slot id {sid} collides with an existing slot on the rotate node"

    prop = make_property(donor_prop, str(uuid.uuid4()), PARENT_PROP_NAME, PARENT_PROP_REF,
                         0.0, per_instance=True)
    prop_node, prop_slots = make_property_node(idx, donor_prop_node, prop["m_ObjectId"],
                                               PARENT_PROP_NAME, -3200.0, 2900.0)

    centroid_slot = zero_slot(clone(donor_v3_slot))
    centroid_slot["m_Id"] = centroid_slot_id
    centroid_slot["m_DisplayName"] = FACE_CENTROID_NAME
    centroid_slot["m_ShaderOutputName"] = FACE_CENTROID_REF

    weight_slot = zero_slot(clone(donor_v1_slot))
    weight_slot["m_Id"] = weight_slot_id
    weight_slot["m_DisplayName"] = PIVOT_WEIGHT_NAME
    weight_slot["m_ShaderOutputName"] = PIVOT_WEIGHT_REF

    rotate["m_Slots"] += [{"m_Id": centroid_slot["m_ObjectId"]}, {"m_Id": weight_slot["m_ObjectId"]}]
    rotate["m_PropertyGuids"] += [FACE_CENTROID_GUID, PIVOT_WEIGHT_GUID]
    rotate["m_PropertyIds"] += [centroid_slot_id, weight_slot_id]

    graph["m_Edges"] += [
        edge(uv_node["m_ObjectId"], 0, rotate["m_ObjectId"], centroid_slot_id),
        edge(prop_node["m_ObjectId"], 0, rotate["m_ObjectId"], weight_slot_id),
    ]
    graph["m_Properties"].append({"m_Id": prop["m_ObjectId"]})
    idx[graph["m_CategoryData"][0]["m_Id"]]["m_ChildObjectList"].append({"m_Id": prop["m_ObjectId"]})
    graph["m_Nodes"].append({"m_Id": prop_node["m_ObjectId"]})
    docs += [prop, prop_node, centroid_slot, weight_slot] + prop_slots

    validate_structure(docs, "ExplodingBlockGraph (after)")
    validate_graph(docs)
    print(f"  ExplodingBlockGraph.shadergraph: wired (+1 property, +1 node, +2 slots, +2 edges; "
          f"slot ids {centroid_slot_id} / {weight_slot_id})")
    return docs, True


# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------

def main():
    check_only = "--check" in sys.argv
    sub_path = os.path.join(REPO, SUBGRAPH)
    graph_path = os.path.join(REPO, GRAPH)

    if check_only:
        ok = True
        for path, is_wired, validator, label in (
                (sub_path, subgraph_is_wired, validate_subgraph, "RotateFacesAlongAxis.shadersubgraph"),
                (graph_path, graph_is_wired, validate_graph, "ExplodingBlockGraph.shadergraph")):
            docs = load_docs(path)
            validate_structure(docs, label)
            if not is_wired(docs):
                print(f"  {label}: NOT wired")
                ok = False
                continue
            validator(docs)
            print(f"  {label}: wired and valid")
        return 0 if ok else 1

    print("Wiring the mesh-centroid face pivot (Docs/PRISM_ANIMATION.md §4.8.2):")
    sub_docs, sub_changed = wire_subgraph(sub_path)
    graph_docs, graph_changed = wire_graph(graph_path)

    if sub_changed:
        open(sub_path, "w", encoding="utf-8", newline="\n").write(dump_docs(sub_docs))
    if graph_changed:
        open(graph_path, "w", encoding="utf-8", newline="\n").write(dump_docs(graph_docs))

    # Re-read from disk and re-assert: the write is the step neither the in-memory model
    # nor code review can see.
    for path, validator, label in (
            (sub_path, validate_subgraph, "RotateFacesAlongAxis.shadersubgraph"),
            (graph_path, validate_graph, "ExplodingBlockGraph.shadergraph")):
        docs = load_docs(path)
        validate_structure(docs, f"{label} (re-read)")
        validator(docs)
    print("Re-read from disk: both files parse, resolve and validate.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
