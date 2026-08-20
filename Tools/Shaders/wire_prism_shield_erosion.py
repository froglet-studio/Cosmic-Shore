#!/usr/bin/env python3
"""
Wire the EROSION fade into BlockGraph, so a shattering shield is wiped away the way an
exploding prism is instead of being scaled down to nothing.

Docs/PRISM_ANIMATION.md §4.8.1. The shatter used to remove its faces by contracting each
one to a point, which reads as the shield DEFLATING. Debris does not shrink — the
exploding prism keeps its faces at full size and PrismErosionFade sweeps one hard, jagged
front across each of them (PrismOcclusionCorridor.hlsl). This gives the shield the same
removal, on the graph a shielded prism's own material actually lives on.

WHY A SECOND TOOL, next to wire_prism_explosion_erosion.py: that one owns the EXPLODING
prism's fade on ExplodingBlockGraph, where the erosion is driven by the explosion clock.
This one owns the SHIELD SHATTER's fade on BlockGraph, driven by PrismShieldMorph's
Opacity output. Same HLSL function, two graphs, two drivers, disjoint asset sets — so
neither tool can regress the other, and each --check inspects only its own graph.

The splice — note the MULTIPLY, which is what makes every non-shattering prism exact:

  BEFORE:  Property[Alpha] --------------------------------> PrismOcclusionFade.BaseAlpha
  AFTER:   PrismShieldMorph.Opacity -> EROSION.BaseOpacity
           UV (channel 0) ----------> EROSION.UV          (the face-local wipe frame)
           UV (channel 1) ----------> EROSION.Velocity    (per-FACE wipe identity, see below)
           Property[Alpha] ---------> Multiply.A
           EROSION.Survival --------> Multiply.B
           Multiply ----------------------------------> PrismOcclusionFade.BaseAlpha

  A prism that is not shattering has Opacity 1, PrismErosionFade returns Survival 1
  outright at BaseOpacity >= 1, and Alpha * 1 is Alpha — bit-for-bit the old chain,
  including the cloak family's authored near-zero alpha. Multiplying rather than
  REPLACING is the whole reason that holds: feeding the erosion the material's alpha
  directly would put a wipe pattern on every cloaked prism.

  EROSION.Velocity is a HASH SEED, not a velocity — the function uses it only for the
  wipe's direction and jag. The exploding prism seeds it from its stamped flight vector
  so no two chunks peel alike; here it is seeded from the per-FACE centroid (UV1), which
  is mesh data that always arrives and differs per face, so the eight faces of one shield
  peel independently. It deliberately does NOT use _ShieldMorphVelocity: a whole shield
  shares one impulse, so that would make all its faces peel identically.

Requires the shield meshes to carry UV0 (Octahedron/StellatedOctahedronMeshGenerator
.ErosionUVChannel). BlockGraph reads UV0 nowhere else — asserted below — so authoring it
changed no existing shading.

Out-of-editor ShaderGraph JSON synthesis per /asset-surgery: parse everything, clone
schema-exact donors (the erosion node and the UV0 node clone cross-file from
ExplodingBlockGraph, which ships both), rebuild in memory, assert every invariant —
including ACYCLICITY and property-node slot types, the two checks whose absence shipped a
magenta graph earlier on this branch — and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0.

Usage:  python3 Tools/Shaders/wire_prism_shield_erosion.py [--check]
"""

import json
import os
import sys
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPH = "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph"
DONOR = "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph"

HLSL_GUID = "bf8e2c1fa76142c89ba03b2e1ae46201"      # PrismOcclusionCorridor.hlsl
FUNCTION_NAME = "PrismErosionFade"
MORPH_FUNCTION = "PrismShieldMorph"
MORPH_OPACITY_SLOT = 10
CORRIDOR_FUNCTION = "PrismOcclusionFade"
CORRIDOR_BASEALPHA_SLOT = 3
ALPHA_PROPERTY = "Alpha"

EROSION_UV_CHANNEL = 0        # must match OctahedronMeshGenerator.ErosionUVChannel
CENTROID_UV_CHANNEL = 1       # the per-face hash seed

CF_SLOTS = [
    (0, "UV", "Vector3", False),
    (1, "Velocity", "Vector3", False),
    (2, "BaseOpacity", "Vector1", False),
    (3, "Survival", "Vector1", True),
]
CF_SURVIVAL_SLOT = 3


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


def find_cf(docs, fn):
    return next((d for d in docs if d.get("m_FunctionName") == fn), None)


def find_node_by_type(docs, fragment):
    return next((d for d in docs if fragment in d.get("m_Type", "")), None)


def find_property_node(docs, idx, name):
    for d in docs:
        if d.get("m_Type", "").endswith("PropertyNode"):
            prop = idx.get((d.get("m_Property") or {}).get("m_Id"))
            if prop is not None and prop.get("m_Name") == name:
                return d
    return None


def make_slot(donor, slot_id, display_name, is_output):
    s = json.loads(json.dumps(donor))
    s["m_ObjectId"] = uuid.uuid4().hex
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


def clone_node(donor, donor_idx, x, y, **overrides):
    node = json.loads(json.dumps(donor))
    node["m_ObjectId"] = uuid.uuid4().hex
    node["m_Group"] = {"m_Id": ""}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y})
    node.update(overrides)
    slots = []
    for ref in donor["m_Slots"]:
        s = json.loads(json.dumps(donor_idx[ref["m_Id"]]))
        s["m_ObjectId"] = uuid.uuid4().hex
        slots.append(s)
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def edge(out_node, out_slot, in_node, in_slot):
    return {
        "m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
        "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot},
    }


def assert_acyclic(graph, idx):
    """A shader graph is a DAG; ShaderGraph fails the whole asset on a cycle and every
    material on it renders magenta. Splicing a node in FRONT of something already
    DOWNSTREAM of it is how that happens, and no per-node check can see it."""
    upstream = {}
    for e in graph["m_Edges"]:
        upstream.setdefault(e["m_InputSlot"]["m_Node"]["m_Id"], set()).add(
            e["m_OutputSlot"]["m_Node"]["m_Id"])

    def label(nid):
        n = idx.get(nid, {})
        return n.get("m_FunctionName") or n.get("m_Name") or n.get("m_Type", "?").split(".")[-1]

    colour = {}

    def walk(node, stack):
        colour[node] = 1
        stack.append(node)
        for parent in upstream.get(node, ()):
            state = colour.get(parent, 0)
            if state == 1:
                loop = stack[stack.index(parent):] + [parent]
                raise AssertionError("edge cycle: " + " -> ".join(label(n) for n in loop))
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

    slot_ids = {}
    for ref in graph["m_Nodes"]:
        node = idx[ref["m_Id"]]
        slot_ids[ref["m_Id"]] = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in node.get("m_Slots", [])}

    feeders = {}
    for e in graph["m_Edges"]:
        o, i = e["m_OutputSlot"], e["m_InputSlot"]
        for end, label in ((o, "output"), (i, "input")):
            nid = end["m_Node"]["m_Id"]
            assert nid in slot_ids, f"edge {label} node not in m_Nodes"
            assert end["m_SlotId"] in slot_ids[nid], f"edge {label} slot missing"
        assert slot_ids[o["m_Node"]["m_Id"]][o["m_SlotId"]]["m_SlotType"] == 1
        assert slot_ids[i["m_Node"]["m_Id"]][i["m_SlotId"]]["m_SlotType"] == 0
        key = (i["m_Node"]["m_Id"], i["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
    for key, count in feeders.items():
        assert count == 1, f"input slot {key} has {count} feeders (must be exactly 1)"

    # Property nodes must carry their property's own concrete type — a Vector1 slot on a
    # Vector3 property silently delivers no vector (shipped once on this branch).
    for ref in graph["m_Nodes"]:
        node = idx[ref["m_Id"]]
        if "PropertyNode" not in node.get("m_Type", "") or not node.get("m_Slots"):
            continue
        prop = idx.get((node.get("m_Property") or {}).get("m_Id"))
        if prop is None:
            continue
        kind = prop["m_Type"].split(".")[-1].replace("ShaderProperty", "")
        if kind not in ("Vector1", "Vector2", "Vector3", "Vector4"):
            continue
        assert f"{kind}MaterialSlot" in idx[node["m_Slots"][0]["m_Id"]]["m_Type"], \
            f"property node for {prop.get('m_Name')} ({kind}) has the wrong slot type"

    assert_acyclic(graph, idx)

    if not expect_wired:
        return

    cf = find_cf(docs, FUNCTION_NAME)
    assert cf is not None, f"{FUNCTION_NAME} node missing"
    assert cf["m_FunctionSource"] == HLSL_GUID, "erosion points at the wrong HLSL asset"
    cf_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
    assert set(cf_slots) == {s[0] for s in CF_SLOTS}, "erosion slot ids drifted from the HLSL"
    for slot_id, name, kind, is_output in CF_SLOTS:
        assert cf_slots[slot_id]["m_DisplayName"] == name
        assert cf_slots[slot_id]["m_SlotType"] == (1 if is_output else 0)
        assert f"{kind}MaterialSlot" in cf_slots[slot_id]["m_Type"], f"slot {name} wrong type"

    sources = {(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]):
               (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])
               for e in graph["m_Edges"]}

    for slot_id, name, _k, is_output in CF_SLOTS:
        if not is_output:
            assert (cf["m_ObjectId"], slot_id) in sources, f"erosion input '{name}' unconnected"

    # BaseOpacity is the shield morph's Opacity — that is what makes this the SHIELD's fade.
    morph = find_cf(docs, MORPH_FUNCTION)
    assert morph is not None, f"{MORPH_FUNCTION} not found — run its wirer first"
    assert sources[(cf["m_ObjectId"], 2)] == (morph["m_ObjectId"], MORPH_OPACITY_SLOT), \
        "erosion BaseOpacity is not fed by PrismShieldMorph.Opacity"

    uv_node = idx[sources[(cf["m_ObjectId"], 0)][0]]
    assert "UVNode" in uv_node.get("m_Type", ""), "erosion UV is not fed by a UV node"
    assert uv_node.get("m_OutputChannel") == EROSION_UV_CHANNEL, \
        f"erosion UV node is not reading UV{EROSION_UV_CHANNEL} (must match ErosionUVChannel)"
    seed_node = idx[sources[(cf["m_ObjectId"], 1)][0]]
    assert "UVNode" in seed_node.get("m_Type", ""), "erosion seed is not fed by a UV node"
    assert seed_node.get("m_OutputChannel") == CENTROID_UV_CHANNEL, \
        "the erosion's per-face seed must come from the face-centroid channel"

    # The multiply: Alpha * Survival -> BaseAlpha. Replacing rather than multiplying would
    # put a wipe on every cloaked prism.
    corridor = find_cf(docs, CORRIDOR_FUNCTION)
    assert corridor is not None, "PrismOcclusionFade not found"
    mul_id, _ = sources[(corridor["m_ObjectId"], CORRIDOR_BASEALPHA_SLOT)]
    mul = idx[mul_id]
    assert "MultiplyNode" in mul.get("m_Type", ""), \
        "PrismOcclusionFade.BaseAlpha is not fed by the Alpha x Survival multiply"
    a_src, b_src = sources[(mul_id, 0)], sources[(mul_id, 1)]
    alpha_node = find_property_node(docs, idx, ALPHA_PROPERTY)
    assert alpha_node is not None, "the Alpha property node is gone"
    assert a_src[0] == alpha_node["m_ObjectId"], "multiply A is not the Alpha property"
    assert b_src == (cf["m_ObjectId"], CF_SURVIVAL_SLOT), "multiply B is not erosion Survival"

    consumers = [e for e in graph["m_Edges"]
                 if e["m_OutputSlot"]["m_Node"]["m_Id"] == cf["m_ObjectId"]]
    assert len(consumers) == 1, f"Survival feeds {len(consumers)} inputs (expected 1)"


def wire():
    path = os.path.join(REPO, GRAPH)
    docs = load_docs(path)
    validate(docs, expect_wired=False)

    if find_cf(docs, FUNCTION_NAME) is not None:
        validate(docs, expect_wired=True)
        print(f"  {os.path.basename(GRAPH)}: already wired")
        return False

    graph = find_graph(docs)
    idx = index(docs)

    donor_docs = load_docs(os.path.join(REPO, DONOR))
    donor_idx = index(donor_docs)

    morph = find_cf(docs, MORPH_FUNCTION)
    assert morph is not None, f"{MORPH_FUNCTION} not found — run wire_prism_shield_morph.py first"
    morph_slots = {idx[s["m_Id"]]["m_Id"] for s in morph["m_Slots"]}
    assert MORPH_OPACITY_SLOT in morph_slots, \
        "PrismShieldMorph has no Opacity output — re-run wire_prism_shield_morph.py"

    corridor = find_cf(docs, CORRIDOR_FUNCTION)
    assert corridor is not None, "PrismOcclusionFade not found — run the corridor wirer first"

    # UV0 must be unused today, or splicing the erosion onto it would change existing
    # shading rather than only adding the fade.
    for d in docs:
        if "UVNode" in d.get("m_Type", ""):
            assert d.get("m_OutputChannel") != EROSION_UV_CHANNEL, \
                "BlockGraph already reads UV0 — re-derive the erosion's anchor before wiring"

    donor_cf = next(d for d in donor_docs if d.get("m_FunctionName") == FUNCTION_NAME)
    donor_uv = next(d for d in donor_docs
                    if "UVNode" in d.get("m_Type", "") and d.get("m_OutputChannel") == EROSION_UV_CHANNEL)
    donor_mul = find_node_by_type(docs, "ShaderGraph.MultiplyNode")
    assert donor_mul is not None, "no MultiplyNode donor in BlockGraph"

    base_x, base_y = 1200.0, 1800.0
    new_docs = []

    cf, cf_slots = clone_node(donor_cf, donor_idx, base_x + 320.0, base_y)
    uv0, uv0_slots = clone_node(donor_uv, donor_idx, base_x, base_y,
                                m_OutputChannel=EROSION_UV_CHANNEL)
    seed, seed_slots = clone_node(donor_uv, donor_idx, base_x, base_y + 200.0,
                                  m_OutputChannel=CENTROID_UV_CHANNEL)
    mul, mul_slots = clone_node(donor_mul, index(docs), base_x + 640.0, base_y)
    # A cloned Multiply inherits the donor's dynamic-vector slots; both feeds here are
    # scalars, so its concrete type resolves to Vector1 on its own.
    for node, slots in ((cf, cf_slots), (uv0, uv0_slots), (seed, seed_slots), (mul, mul_slots)):
        new_docs.append(node)
        new_docs.extend(slots)
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    mul_slot_ids = sorted(s["m_Id"] for s in mul_slots)
    mul_a, mul_b, mul_out = mul_slot_ids[0], mul_slot_ids[1], mul_slot_ids[2]

    # Retarget the existing Alpha -> BaseAlpha edge through the multiply.
    retargeted = 0
    for e in graph["m_Edges"]:
        i_ = e["m_InputSlot"]
        if i_["m_Node"]["m_Id"] == corridor["m_ObjectId"] and i_["m_SlotId"] == CORRIDOR_BASEALPHA_SLOT:
            e["m_InputSlot"] = {"m_Node": {"m_Id": mul["m_ObjectId"]}, "m_SlotId": mul_a}
            retargeted += 1
    assert retargeted == 1, f"expected 1 feeder on BaseAlpha, retargeted {retargeted}"

    graph["m_Edges"].extend([
        edge(uv0["m_ObjectId"], 0, cf["m_ObjectId"], 0),
        edge(seed["m_ObjectId"], 0, cf["m_ObjectId"], 1),
        edge(morph["m_ObjectId"], MORPH_OPACITY_SLOT, cf["m_ObjectId"], 2),
        edge(cf["m_ObjectId"], CF_SURVIVAL_SLOT, mul["m_ObjectId"], mul_b),
        edge(mul["m_ObjectId"], mul_out, corridor["m_ObjectId"], CORRIDOR_BASEALPHA_SLOT),
    ])

    docs.extend(new_docs)
    validate(docs, expect_wired=True)
    open(path, "w", encoding="utf-8").write(dump_docs(docs))
    print(f"  {os.path.basename(GRAPH)}: wired (+{len(new_docs)} objects)")
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
