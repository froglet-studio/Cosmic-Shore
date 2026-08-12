#!/usr/bin/env python3
"""
Wire the camera->vessel occlusion corridor into EVERY shader graph a LIVE prism can
render with.

Coverage is the whole point (see Docs/PRISM_ANIMATION.md 4.6 "the corridor is a platform
law"): a prism material that cannot fade is an invisible hole in the corridor, and holes
are exactly what per-vessel / per-mode opt-in used to produce. The census:

  BlockGraph           PrismMaterial, Shielded, SuperShielded, Danger + their cloak-state
                       variants + the cloak material  -> every trail and environment prism
  ExplodingBlockGraph  TransparentPrismMaterial (cloak-invisible LIVE prisms), MazeDangerBlockMateral
                       (live maze/overheat prisms) and the explosion debris material

(Since 2026-08-10 every one of those materials is OPAQUE + alpha clip — the "transparent"
names survive as the cloak-state bind targets, but their transparency is dither coverage,
not blending; enable_prism_alpha_clip.py enforces that and converts strays.)

SuctionGraph is deliberately excluded: it renders a prism DURING consumption (a sub-second
implode of mass that is being removed), never standing mass the player can be occluded by.
The legacy SpreadFresnel/TriangleFresnel prism family is also excluded - it is decor/tool-
scene only (its two Prism-carrying prefabs are referenced by nothing but the Recording
Studio scenes) and PRISM_ANIMATION.md 3.7 I says do not extend it. Both are reported by
PrismOcclusionDiagnostics at runtime if they ever carry live mass.

Docs/PRISM_ANIMATION.md §5 C1. Out-of-editor ShaderGraph JSON synthesis, following
the /asset-surgery protocol: parse the whole file, clone same-file donors for schema
exactness, rebuild in memory, assert every invariant, and only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0.
Re-run it after a graph revert to repair the wiring.

What it adds to each graph:

  properties (both UNEXPOSED -> declared as globals, driven by Shader.SetGlobalVector
  from PrismOcclusionCorridor.cs; same shape as the existing _PrismClock global):
      _PrismOcclusionTarget  float3  vessel world position
      _PrismOcclusionParams  float3  (outerRadius, innerRadius, minAlpha)

  nodes:
      Position (World)                       -> fragment world position
      Property x2                            -> the two globals
      PrismOcclusionFade (Custom Function)    -> PrismOcclusionCorridor.hlsl

  edges (the splice - whatever fed SurfaceDescription.Alpha is RETARGETED into the
  custom function's BaseAlpha input, so the graph's own alpha still applies and the
  corridor only scales it; that is `_Alpha` on BlockGraph and PrismExplosionClock.Opacity
  on ExplodingBlockGraph):
      BEFORE:  <alpha source> ------------------------> SurfaceDescription.Alpha
      AFTER:   <alpha source> -> CF.BaseAlpha
               Position(World) -> CF.PositionWS
               _PrismOcclusionTarget -> CF.Target
               _PrismOcclusionParams -> CF.Params
               CF.Alpha ----------------------------> SurfaceDescription.Alpha
               CF.ClipThreshold --------------------> SurfaceDescription.AlphaClipThreshold

Usage:  python3 Tools/Shaders/wire_prism_occlusion_corridor.py [--check]
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
# BlockGraph has no Position node to clone; CageGraph does, and it is the same
# ShaderGraph version, so the schema is exact by construction.
POSITION_DONOR = "Assets/_Graphics/Materials/Graphs/CageGraph.shadergraph"

# GUID of Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl, pinned by its
# committed .meta so this reference can never drift.
HLSL_GUID = "bf8e2c1fa76142c89ba03b2e1ae46201"
FUNCTION_NAME = "PrismOcclusionFade"

TARGET_PROP = ("PrismOcclusionTarget", "_PrismOcclusionTarget")
PARAMS_PROP = ("PrismOcclusionParams", "_PrismOcclusionParams")

# (integer slot id, display name, "Vector1"|"Vector3", is_output) — the integer ids
# MUST match the HLSL parameter order (inputs first, then outputs).
CF_SLOTS = [
    (0, "PositionWS", "Vector3", False),
    (1, "Target", "Vector3", False),
    (2, "Params", "Vector3", False),
    (3, "BaseAlpha", "Vector1", False),
    (4, "Alpha", "Vector1", True),
    (5, "ClipThreshold", "Vector1", True),
]

COORDINATE_SPACE_WORLD = 2


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


def find_property(docs, name):
    for d in docs:
        if d.get("m_Name") == name and "ShaderProperty" in d.get("m_Type", ""):
            return d
    return None


def find_block(docs, descriptor):
    for d in docs:
        if d.get("m_SerializedDescriptor") == descriptor:
            return d
    return None


# ---------------------------------------------------------------------------
# builders (every one clones a donor of the same type)
# ---------------------------------------------------------------------------

def make_global_vector3_property(donor_vector3, donor_unexposed, name, reference):
    """Vector3 shape from the Vector3 donor; exposure flags from the unexposed donor."""
    p = json.loads(json.dumps(donor_vector3))
    p["m_ObjectId"] = new_oid()
    p["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
    p["m_Name"] = name
    p["m_RefNameGeneratedByDisplayName"] = name
    p["m_DefaultReferenceName"] = reference
    p["m_OverrideReferenceName"] = reference
    # Unexposed => declared OUTSIDE UnityPerMaterial => a true global uniform, exactly
    # like _PrismClock. Never Hybrid Per Instance: this is one value for the whole frame.
    p["m_GeneratePropertyBlock"] = donor_unexposed["m_GeneratePropertyBlock"]
    p["overrideHLSLDeclaration"] = donor_unexposed["overrideHLSLDeclaration"]
    p["hlslDeclarationOverride"] = donor_unexposed["hlslDeclarationOverride"]
    p["m_Value"] = {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}
    return p


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


def make_property_node(donor_property_node, donor_slot_v3, property_oid, label, x, y):
    node = json.loads(json.dumps(donor_property_node))
    node["m_ObjectId"] = new_oid()
    node["m_Property"] = {"m_Id": property_oid}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 200.0, "height": 36.0})
    slot = make_slot(donor_slot_v3, 0, label, True)
    slot["m_ShaderOutputName"] = "Out"
    node["m_Slots"] = [{"m_Id": slot["m_ObjectId"]}]
    return node, [slot]


def make_position_node(donor_position_node, donor_slot, x, y):
    node = json.loads(json.dumps(donor_position_node))
    node["m_ObjectId"] = new_oid()
    node["m_Space"] = COORDINATE_SPACE_WORLD
    node["m_PositionSource"] = 0
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 208.0, "height": 302.0})
    slot = json.loads(json.dumps(donor_slot))
    slot["m_ObjectId"] = new_oid()
    node["m_Slots"] = [{"m_Id": slot["m_ObjectId"]}]
    return node, [slot]


def make_custom_function_node(donor_cf, donor_slot_v1, donor_slot_v3, x, y):
    node = json.loads(json.dumps(donor_cf))
    node["m_ObjectId"] = new_oid()
    node["m_Name"] = f"{FUNCTION_NAME} (Custom Function)"
    node["m_FunctionName"] = FUNCTION_NAME
    node["m_FunctionSource"] = HLSL_GUID
    node["m_SourceType"] = 0
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 232.0, "height": 350.0})
    slots = []
    for slot_id, name, kind, is_output in CF_SLOTS:
        donor = donor_slot_v3 if kind == "Vector3" else donor_slot_v1
        slots.append(make_slot(donor, slot_id, name, is_output))
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
    for cat in graph["m_CategoryData"]:
        cd = idx[cat["m_Id"]]
        for child in cd["m_ChildObjectList"]:
            assert child["m_Id"] in idx, "category child missing"

    # every node's slots resolve, and every edge resolves to a real (node, slot) pair
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
        for end, label in ((o, "output"), (i, "input")):
            nid = end["m_Node"]["m_Id"]
            assert nid in slot_ids, f"edge {label} node {nid} not registered in m_Nodes"
            assert end["m_SlotId"] in slot_ids[nid], f"edge {label} slot {end['m_SlotId']} missing on {nid}"
        assert slot_ids[o["m_Node"]["m_Id"]][o["m_SlotId"]]["m_SlotType"] == 1, "edge output is not an output slot"
        assert slot_ids[i["m_Node"]["m_Id"]][i["m_SlotId"]]["m_SlotType"] == 0, "edge input is not an input slot"
        key = (i["m_Node"]["m_Id"], i["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
    for key, count in feeders.items():
        assert count == 1, f"input slot {key} has {count} feeders (must be exactly 1)"

    if not expect_wired:
        return

    # the edit itself landed
    for name, reference in (TARGET_PROP, PARAMS_PROP):
        p = find_property(docs, name)
        assert p is not None, f"property {name} missing"
        assert p["m_DefaultReferenceName"] == reference, f"{name} reference name wrong"
        assert p["m_OverrideReferenceName"] == reference, f"{name} override reference wrong"
        assert p["m_GeneratePropertyBlock"] is False, f"{name} must be UNEXPOSED (a global)"
        assert p["overrideHLSLDeclaration"] is False, f"{name} must not be Hybrid Per Instance"
        assert any(r["m_Id"] == p["m_ObjectId"] for r in graph["m_Properties"]), f"{name} not in m_Properties"
        assert any(
            any(c["m_Id"] == p["m_ObjectId"] for c in idx[cat["m_Id"]]["m_ChildObjectList"])
            for cat in graph["m_CategoryData"]
        ), f"{name} not in any blackboard category"

    cf = next((idx[r["m_Id"]] for r in graph["m_Nodes"]
               if idx[r["m_Id"]].get("m_FunctionName") == FUNCTION_NAME), None)
    assert cf is not None, "PrismOcclusionFade custom function node missing"
    assert cf["m_FunctionSource"] == HLSL_GUID, "custom function points at the wrong HLSL asset"
    cf_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
    assert set(cf_slots) == {s[0] for s in CF_SLOTS}, "custom function slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CF_SLOTS:
        assert cf_slots[slot_id]["m_DisplayName"] == name, f"slot {slot_id} name drifted"
        assert cf_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"slot {slot_id} direction wrong"

    # every custom-function input is fed, and the two outputs land on the two blocks
    fed = {(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]) for e in graph["m_Edges"]}
    for slot_id, name, _kind, is_output in CF_SLOTS:
        if not is_output:
            assert (cf["m_ObjectId"], slot_id) in fed, f"custom function input '{name}' is unconnected"

    alpha_block = find_block(docs, "SurfaceDescription.Alpha")
    clip_block = find_block(docs, "SurfaceDescription.AlphaClipThreshold")
    assert alpha_block and clip_block, "surface blocks missing"
    sources = {}
    for e in graph["m_Edges"]:
        sources[(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])] = (
            e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])
    # SurfaceDescription.Alpha must TRACE BACK to the corridor's Alpha — not necessarily
    # by a direct edge. Post-corridor stages may legitimately sit in between: the
    # back-face separation (wire_prism_backface_fade.py) sharpens the corridor's alpha on
    # far-facing surfaces and MUST be downstream of it, because in the gradient band the
    # graph's own alpha is 1 and only the corridor's fade is fractional. So walk the
    # chain rather than demanding adjacency; anything that consumes the corridor's alpha
    # and forwards one alpha onward is a valid link.
    chain, guard = sources.get((alpha_block["m_ObjectId"], 0)), 0
    while chain is not None and chain[0] != cf["m_ObjectId"] and guard < 8:
        node_id = chain[0]
        feeders_in = [src for (dst_node, _dst_slot), src in sources.items() if dst_node == node_id]
        # Follow the unique feeder that leads back toward the corridor.
        chain = next((s for s in feeders_in if s[0] == cf["m_ObjectId"]),
                     feeders_in[0] if len(feeders_in) == 1 else None)
        guard += 1
    assert chain == (cf["m_ObjectId"], 4), \
        "SurfaceDescription.Alpha does not trace back to PrismOcclusionFade.Alpha"
    assert sources.get((clip_block["m_ObjectId"], 0)) == (cf["m_ObjectId"], 5), \
        "SurfaceDescription.AlphaClipThreshold is not fed by PrismOcclusionFade.ClipThreshold"

    # The graph's ORIGINAL alpha source was RETARGETED into BaseAlpha, not dropped and
    # not duplicated — so the graph's own alpha still applies and the corridor only
    # scales it. (That source is the `_Alpha` property on BlockGraph and
    # PrismExplosionClock.Opacity on ExplodingBlockGraph — this asserts the shape, not
    # the identity, so it holds for any graph.)
    assert (cf["m_ObjectId"], 3) in sources, \
        "PrismOcclusionFade.BaseAlpha is unconnected — the graph's original alpha source was dropped"
    base_src = sources[(cf["m_ObjectId"], 3)]
    assert base_src[0] != cf["m_ObjectId"], "BaseAlpha is fed by the custom function itself"


# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------

def wire_graph(rel_path, check_only):
    """Returns (changed, message). Raises on any invariant failure (before writing)."""
    path = os.path.join(REPO, rel_path)
    docs = load_docs(path)
    graph = find_graph(docs)

    if find_property(docs, TARGET_PROP[0]) is not None:
        validate(docs, expect_wired=True)
        return False, f"{os.path.basename(rel_path)}: already wired (validated)."
    if check_only:
        return False, None  # signals NOT wired

    validate(docs, expect_wired=False)  # the file we are about to edit must be sane
    idx = index(docs)

    # ---- donors (all same-file except the Position node, which these graphs lack) ----
    donor_v3_prop = next((find_property(docs, n) for n in ("Spread", "StartSpread", "GrowStartFrac")
                          if find_property(docs, n) is not None), None)
    donor_unexposed = find_property(docs, "PrismClock")
    assert donor_v3_prop is not None, f"{rel_path}: no Vector3 property to clone"
    assert donor_unexposed is not None, f"{rel_path}: no unexposed _PrismClock property to clone exposure flags from"
    donor_prop_node = next(idx[r["m_Id"]] for r in graph["m_Nodes"]
                           if idx[r["m_Id"]].get("m_Type", "").endswith("PropertyNode"))
    donor_cf = next(idx[r["m_Id"]] for r in graph["m_Nodes"]
                    if "CustomFunctionNode" in idx[r["m_Id"]].get("m_Type", ""))
    donor_slot_v3 = next(idx[s["m_Id"]] for s in donor_cf["m_Slots"]
                         if "Vector3MaterialSlot" in idx[s["m_Id"]]["m_Type"])
    donor_slot_v1 = next(idx[s["m_Id"]] for s in donor_cf["m_Slots"]
                         if "Vector1MaterialSlot" in idx[s["m_Id"]]["m_Type"])

    pos_docs = load_docs(os.path.join(REPO, POSITION_DONOR))
    pos_idx = index(pos_docs)
    donor_pos = next(d for d in pos_docs if d.get("m_Type", "").endswith("PositionNode"))
    donor_pos_slot = pos_idx[donor_pos["m_Slots"][0]["m_Id"]]

    new_docs = []

    # ---- properties ----
    target_prop = make_global_vector3_property(donor_v3_prop, donor_unexposed, *TARGET_PROP)
    params_prop = make_global_vector3_property(donor_v3_prop, donor_unexposed, *PARAMS_PROP)
    new_docs += [target_prop, params_prop]
    graph["m_Properties"] += [{"m_Id": target_prop["m_ObjectId"]}, {"m_Id": params_prop["m_ObjectId"]}]

    category = max((idx[c["m_Id"]] for c in graph["m_CategoryData"]),
                   key=lambda c: len(c["m_ChildObjectList"]))
    category["m_ChildObjectList"] += [{"m_Id": target_prop["m_ObjectId"]},
                                      {"m_Id": params_prop["m_ObjectId"]}]

    # ---- nodes ----
    target_node, s1 = make_property_node(donor_prop_node, donor_slot_v3,
                                         target_prop["m_ObjectId"], TARGET_PROP[0], -1500.0, 1900.0)
    params_node, s2 = make_property_node(donor_prop_node, donor_slot_v3,
                                         params_prop["m_ObjectId"], PARAMS_PROP[0], -1500.0, 1990.0)
    position_node, s3 = make_position_node(donor_pos, donor_pos_slot, -1500.0, 1560.0)
    cf_node, s4 = make_custom_function_node(donor_cf, donor_slot_v1, donor_slot_v3, -1180.0, 1620.0)

    for node, slots in ((target_node, s1), (params_node, s2), (position_node, s3), (cf_node, s4)):
        new_docs.append(node)
        new_docs += slots
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    # ---- edges ----
    alpha_block = find_block(docs, "SurfaceDescription.Alpha")
    clip_block = find_block(docs, "SurfaceDescription.AlphaClipThreshold")
    assert alpha_block is not None, f"{rel_path}: no SurfaceDescription.Alpha block"
    assert clip_block is not None, (
        f"{rel_path}: no SurfaceDescription.AlphaClipThreshold block — the graph target must have "
        "Allow Material Override (or Alpha Clip) on, or an opaque material can never dissolve")

    # RETARGET the graph's ONE existing alpha feeder into BaseAlpha (never add a second
    # feeder into the same input). Whatever it is — a property node on BlockGraph, the
    # explosion clock's Opacity on ExplodingBlockGraph — the corridor multiplies it.
    retargeted = 0
    for e in graph["m_Edges"]:
        if e["m_InputSlot"]["m_Node"]["m_Id"] == alpha_block["m_ObjectId"]:
            e["m_InputSlot"] = {"m_Node": {"m_Id": cf_node["m_ObjectId"]}, "m_SlotId": 3}
            retargeted += 1
    assert retargeted == 1, (
        f"{rel_path}: expected exactly one feeder into SurfaceDescription.Alpha, found {retargeted}")

    graph["m_Edges"] += [
        edge(position_node["m_ObjectId"], 0, cf_node["m_ObjectId"], 0),
        edge(target_node["m_ObjectId"], 0, cf_node["m_ObjectId"], 1),
        edge(params_node["m_ObjectId"], 0, cf_node["m_ObjectId"], 2),
        edge(cf_node["m_ObjectId"], 4, alpha_block["m_ObjectId"], 0),
        edge(cf_node["m_ObjectId"], 5, clip_block["m_ObjectId"], 0),
    ]

    docs += new_docs
    validate(docs, expect_wired=True)  # nothing has been written yet

    open(path, "w", encoding="utf-8").write(dump_docs(docs))
    validate(load_docs(path), expect_wired=True)  # re-read from disk and re-check
    return True, (f"{os.path.basename(rel_path)}: wired and validated "
                  f"(+2 globals, +4 nodes, +{len(new_docs) - 6} slots, 5 new edges, 1 retargeted).")


def main():
    check_only = "--check" in sys.argv
    unwired = []
    for rel in GRAPHS:
        changed, message = wire_graph(rel, check_only)
        if message is None:
            unwired.append(rel)
            print(f"  {os.path.basename(rel)}: NOT wired.", file=sys.stderr)
        else:
            print("  " + message)
    if unwired:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
