#!/usr/bin/env python3
"""
Wire the Dolphin's Echo Sight highlight into every shader graph a LIVE prism can render with.

The sight lights up every prism standing inside the volume the next crystal blast would sweep
(Docs/PRISM_ANIMATION.md §4.7 — the global-uniform shape for a view-dependent prism visual;
Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ECHO_SIGHT.md for the mechanic).

Coverage matters for the same reason it does for the occlusion corridor: a prism material that
cannot light up is a hole in the targeting aid, and a targeting aid with holes is worse than none —
the pilot reads "nothing there" and aims somewhere else. The census matches
wire_prism_occlusion_corridor.py exactly:

  BlockGraph           PrismMaterial, Shielded, SuperShielded, Danger + their cloak-state variants
                       + the cloak material  -> every trail and environment prism
  ExplodingBlockGraph  TransparentPrismMaterial, MazeDangerBlockMateral and the explosion debris
                       material

SuctionGraph is deliberately excluded, as there: it renders a prism DURING consumption, never
standing mass a blast could claim. The legacy SpreadFresnel/TriangleFresnel family is decor/tool-
scene only and PRISM_ANIMATION.md 3.7 says do not extend it.

Out-of-editor ShaderGraph JSON synthesis, following the /asset-surgery protocol: parse the whole
file, clone same-file donors for schema exactness, rebuild in memory, assert every invariant, and
only then write.

Idempotent: re-running after a successful pass prints "already wired" and exits 0. Re-run it after
a graph revert to repair the wiring.

What it adds to each graph:

  properties (all UNEXPOSED -> declared as globals, driven by Shader.SetGlobalVector from
  PrismDestructionSight.cs; same shape as the existing _PrismClock / _PrismOcclusion* globals):
      _PrismSightApex     float3  blast apex, world space
      _PrismSightAxis     float3  sweep axis (unit)
      _PrismSightGape     float3  gape axis (unit, perpendicular to the sweep axis)
      _PrismSightParams   float3  (height, coreRadiusPerUnitDepth, halfLengthPerUnitDepth)
      _PrismSightStrength float   highlight fade, 0-1

  nodes:
      Position (World)                          -> fragment world position
      Property x5                               -> the five globals
      PrismDestructionSight (Custom Function)    -> PrismDestructionSight.hlsl

  edges (the splice — whatever fed SurfaceDescription.BaseColor is RETARGETED into the custom
  function's BaseColor input, so the graph's own colour still applies and the sight only ADDS to
  it; the prism graphs are Unlit and carry no Emission block, so additive-into-BaseColor is how
  emission is expressed there):
      BEFORE:  <colour source> -----------------------> SurfaceDescription.BaseColor
      AFTER:   <colour source> -> CF.BaseColor
               Position(World) -> CF.PositionWS
               _PrismSightApex/Axis/Gape/Params/Strength -> CF.{Apex,Axis,Gape,Params,Strength}
               CF.Color -----------------------------> SurfaceDescription.BaseColor

Usage:  python3 Tools/Shaders/wire_prism_destruction_sight.py [--check]
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
# Neither prism graph contains a Position node to clone; CageGraph does, at the same ShaderGraph
# version, so the schema is exact by construction. Same donor the corridor tool uses.
POSITION_DONOR = "Assets/_Graphics/Materials/Graphs/CageGraph.shadergraph"

# GUID of Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl, pinned by its committed
# .meta so this reference can never drift.
HLSL_GUID = "c7d41a9e5b8f4e3ab216d0f97c4e8a52"
FUNCTION_NAME = "PrismDestructionSight"

# (display name, shader reference). All Vector3 except Strength — see CF_SLOTS.
VEC3_PROPS = [
    ("PrismSightApex", "_PrismSightApex"),
    ("PrismSightAxis", "_PrismSightAxis"),
    ("PrismSightGape", "_PrismSightGape"),
    ("PrismSightParams", "_PrismSightParams"),
]
STRENGTH_PROP = ("PrismSightStrength", "_PrismSightStrength")

# (integer slot id, display name, "Vector1"|"Vector3", is_output) — the integer ids MUST match the
# HLSL parameter order (inputs first, then outputs).
CF_SLOTS = [
    (0, "PositionWS", "Vector3", False),
    (1, "Apex", "Vector3", False),
    (2, "Axis", "Vector3", False),
    (3, "Gape", "Vector3", False),
    (4, "Params", "Vector3", False),
    (5, "Strength", "Vector1", False),
    (6, "BaseColor", "Vector3", False),
    (7, "Color", "Vector3", True),
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

def make_global_property(donor, donor_unexposed, name, reference, is_vector3):
    """Shape from a same-type donor; exposure flags from the unexposed _PrismClock donor."""
    p = json.loads(json.dumps(donor))
    p["m_ObjectId"] = new_oid()
    p["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
    p["m_Name"] = name
    p["m_RefNameGeneratedByDisplayName"] = name
    p["m_DefaultReferenceName"] = reference
    p["m_OverrideReferenceName"] = reference
    # Unexposed => declared OUTSIDE UnityPerMaterial => a true global uniform, exactly like
    # _PrismClock. Never Hybrid Per Instance: this is one value for the whole frame.
    p["m_GeneratePropertyBlock"] = donor_unexposed["m_GeneratePropertyBlock"]
    p["overrideHLSLDeclaration"] = donor_unexposed["overrideHLSLDeclaration"]
    p["hlslDeclarationOverride"] = donor_unexposed["hlslDeclarationOverride"]
    p["m_Value"] = {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0} if is_vector3 else 0.0
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


def make_property_node(donor_property_node, donor_slot, property_oid, label, x, y):
    node = json.loads(json.dumps(donor_property_node))
    node["m_ObjectId"] = new_oid()
    node["m_Property"] = {"m_Id": property_oid}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 200.0, "height": 36.0})
    slot = make_slot(donor_slot, 0, label, True)
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
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 232.0, "height": 400.0})
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
    assert len(ids) == len(set(ids)), "duplicate m_ObjectId in graph"

    # every referenced object exists
    for ref in graph["m_Nodes"] + graph["m_Properties"]:
        assert ref["m_Id"] in idx, f"dangling reference {ref['m_Id']}"
    for e in graph["m_Edges"]:
        assert e["m_OutputSlot"]["m_Node"]["m_Id"] in idx, "edge from missing node"
        assert e["m_InputSlot"]["m_Node"]["m_Id"] in idx, "edge into missing node"

    base_block = find_block(docs, "SurfaceDescription.BaseColor")
    assert base_block is not None, "no SurfaceDescription.BaseColor block"

    feeders = [e for e in graph["m_Edges"]
               if e["m_InputSlot"]["m_Node"]["m_Id"] == base_block["m_ObjectId"]]
    assert len(feeders) == 1, f"expected exactly one feeder into BaseColor, found {len(feeders)}"

    cf_nodes = [idx[r["m_Id"]] for r in graph["m_Nodes"]
                if idx[r["m_Id"]].get("m_FunctionName") == FUNCTION_NAME]

    if not expect_wired:
        assert not cf_nodes, "graph already carries the sight custom function"
        return

    assert len(cf_nodes) == 1, f"expected exactly one {FUNCTION_NAME} node, found {len(cf_nodes)}"
    cf = cf_nodes[0]
    assert cf["m_FunctionSource"] == HLSL_GUID, "sight node points at the wrong HLSL guid"

    # the CF must own exactly the declared slots, with the declared ids
    cf_slot_ids = sorted(idx[s["m_Id"]]["m_Id"] for s in cf["m_Slots"])
    assert cf_slot_ids == sorted(s[0] for s in CF_SLOTS), \
        f"sight node slot ids {cf_slot_ids} do not match the HLSL signature"

    # the one BaseColor feeder must be the CF's Color output
    fed = feeders[0]["m_OutputSlot"]
    assert fed["m_Node"]["m_Id"] == cf["m_ObjectId"] and fed["m_SlotId"] == 7, \
        "SurfaceDescription.BaseColor is not fed by the sight node's Color output"

    # every CF input must be driven — an unwired input silently reads 0 and the sight
    # would highlight either nothing or everything
    driven = {e["m_InputSlot"]["m_SlotId"] for e in graph["m_Edges"]
              if e["m_InputSlot"]["m_Node"]["m_Id"] == cf["m_ObjectId"]}
    expected = {s[0] for s in CF_SLOTS if not s[3]}
    assert driven == expected, f"sight node inputs {sorted(driven)} != {sorted(expected)}"

    # all five globals present and unexposed
    for name, reference in VEC3_PROPS + [STRENGTH_PROP]:
        prop = find_property(docs, name)
        assert prop is not None, f"missing property {name}"
        assert prop["m_OverrideReferenceName"] == reference, f"{name} has the wrong reference"
        assert prop["m_GeneratePropertyBlock"] == 0, \
            f"{name} is exposed — it must be a global, not a per-material property"


# ---------------------------------------------------------------------------
# wiring
# ---------------------------------------------------------------------------

def wire_graph(rel_path, check_only):
    """Returns (changed, message). Raises on any invariant failure (before writing)."""
    path = os.path.join(REPO, rel_path)
    docs = load_docs(path)
    graph = find_graph(docs)

    if find_property(docs, VEC3_PROPS[0][0]) is not None:
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
    assert donor_unexposed is not None, \
        f"{rel_path}: no unexposed _PrismClock property to clone exposure flags from"

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
    props = []
    for name, reference in VEC3_PROPS:
        props.append(make_global_property(donor_v3_prop, donor_unexposed, name, reference, True))
    props.append(make_global_property(donor_unexposed, donor_unexposed,
                                      STRENGTH_PROP[0], STRENGTH_PROP[1], False))
    new_docs += props
    graph["m_Properties"] += [{"m_Id": p["m_ObjectId"]} for p in props]

    category = max((idx[c["m_Id"]] for c in graph["m_CategoryData"]),
                   key=lambda c: len(c["m_ChildObjectList"]))
    category["m_ChildObjectList"] += [{"m_Id": p["m_ObjectId"]} for p in props]

    # ---- nodes ----
    made = []
    y = 2400.0
    # props is built VEC3_PROPS-then-Strength, so the last one is the only scalar. Keyed off the
    # count rather than sniffing m_Value, which differs by type and reads as a riddle.
    for i, prop in enumerate(props):
        donor_slot = donor_slot_v3 if i < len(VEC3_PROPS) else donor_slot_v1
        made.append(make_property_node(donor_prop_node, donor_slot,
                                       prop["m_ObjectId"], prop["m_Name"], -1500.0, y))
        y += 90.0

    position_node, pslots = make_position_node(donor_pos, donor_pos_slot, -1500.0, 2320.0)
    cf_node, cslots = make_custom_function_node(donor_cf, donor_slot_v1, donor_slot_v3,
                                                -1180.0, 2380.0)
    made.append((position_node, pslots))
    made.append((cf_node, cslots))

    for node, slots in made:
        new_docs.append(node)
        new_docs += slots
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    # ---- edges ----
    base_block = find_block(docs, "SurfaceDescription.BaseColor")
    assert base_block is not None, f"{rel_path}: no SurfaceDescription.BaseColor block"

    # RETARGET the graph's ONE existing colour feeder into BaseColor (never add a second feeder
    # into the same input). Whatever it is, the sight ADDS to it rather than replacing it.
    retargeted = 0
    for e in graph["m_Edges"]:
        if e["m_InputSlot"]["m_Node"]["m_Id"] == base_block["m_ObjectId"]:
            e["m_InputSlot"] = {"m_Node": {"m_Id": cf_node["m_ObjectId"]}, "m_SlotId": 6}
            retargeted += 1
    assert retargeted == 1, (
        f"{rel_path}: expected exactly one feeder into SurfaceDescription.BaseColor, "
        f"found {retargeted}")

    graph["m_Edges"].append(edge(position_node["m_ObjectId"], 0, cf_node["m_ObjectId"], 0))
    for slot_id, (node, _slots) in enumerate(made[:5], start=1):
        graph["m_Edges"].append(edge(node["m_ObjectId"], 0, cf_node["m_ObjectId"], slot_id))
    graph["m_Edges"].append(edge(cf_node["m_ObjectId"], 7, base_block["m_ObjectId"], 0))

    docs += new_docs
    validate(docs, expect_wired=True)  # nothing has been written yet

    open(path, "w", encoding="utf-8").write(dump_docs(docs))
    validate(load_docs(path), expect_wired=True)  # re-read from disk and re-check
    return True, (f"{os.path.basename(rel_path)}: wired and validated "
                  f"(+5 globals, +7 nodes, +{len(new_docs) - 12} slots, 7 new edges, 1 retargeted).")


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
