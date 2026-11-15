#!/usr/bin/env python3
"""
Wire the HULL LIGHT into the vessel hull graph (Docs/VesselModels/COLOR.md section 2a).

VesselGraph is unlit: every hull slot is a two-tone ramp keyed on facing. The hull light lights it
with the world's light, read off the live skybox (the galactic core as the key, the galactic band
and the sky as reflections, the pilot's domain as the rim). The HLSL is VesselHullLighting.hlsl;
the per-frame globals come from VesselHullLighting.cs; the tuning is VesselHullLightingConfigSO.

The splice sits between the graph's final Multiply and the vessel vision band:

  BEFORE:  Multiply(Blend x ColorMultiplier) --------------------------> VISION.BaseColor
  AFTER:   Multiply(Blend x ColorMultiplier) --> HULL.BaseColor
           Position(World)  (the vision node's) -> HULL.PositionWS
           Normal(World)    (the vision node's) -> HULL.NormalWS
           Property(_VesselHullDomain) [new] ---> HULL.Domain
           Property(_HullGloss)        [new] ---> HULL.Gloss
           Property(_HullSelfLit)      [new] ---> HULL.SelfLit
           HULL.Color ---------------------------------------------------> VISION.BaseColor

After the multiply, so the engine's boost flare and the Echo Sight still drive their slot; before
the vision band, so at range the pilot-identification mark still wins.

_VesselHullDomain is EXPOSED for the same reason _VesselVisionTint is (a MaterialPropertyBlock
cannot reach an unexposed property), and its default alpha is 0: alpha is the "a domain was
stamped" marker. _HullGloss (default 1) and _HullSelfLit (default 0) are per-material floats.

Same /asset-surgery shape as wire_vessel_vision_shading.py, whose helpers this reuses: parse,
clone donors, rebuild in memory, validate, write, validate again from disk. Idempotent.

Usage:  python3 Tools/Shaders/wire_vessel_hull_lighting.py [--check]
"""

import json
import os
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wire_vessel_vision_shading as vision  # noqa: E402

REPO = vision.REPO
GRAPH = vision.GRAPH

HLSL_GUID = "5445b40f98c94cfa811e8f2e43e268a2"   # VesselHullLighting.hlsl.meta
FUNCTION_NAME = "VesselHullLight"

DOMAIN_PROPERTY = ("VesselHullDomain", "_VesselHullDomain")
GLOSS_PROPERTY = ("HullGloss", "_HullGloss", 1.0)
SELFLIT_PROPERTY = ("HullSelfLit", "_HullSelfLit", 0.0)

# (slot id, display name, kind, is output) - MUST match VesselHullLight_float's parameter order.
CF_SLOTS = [
    (0, "PositionWS", "Vector3", False),
    (1, "NormalWS", "Vector3", False),
    (2, "Domain", "Vector4", False),
    (3, "Gloss", "Vector1", False),
    (4, "SelfLit", "Vector1", False),
    (5, "BaseColor", "Vector3", False),
    (6, "Color", "Vector3", True),
]

VISION_BASECOLOR_SLOT = 3
VISION_POSITION_SLOT = 0
VISION_NORMAL_SLOT = 1


def sources_of(graph):
    out = {}
    for e in graph["m_Edges"]:
        out[(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])] = \
            (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])
    return out


def property_feeding(idx, sources, node_id, slot):
    src = sources.get((node_id, slot))
    assert src is not None, f"hull light slot {slot} unconnected"
    node = idx[src[0]]
    assert node.get("m_Type", "").endswith("PropertyNode"), f"hull light slot {slot} not fed by a Property node"
    return idx[node["m_Property"]["m_Id"]]


def validate(docs):
    vision.validate(docs, expect_wired=True)
    idx = vision.index(docs)
    graph = vision.find_graph(docs)
    sources = sources_of(graph)

    hull = vision.cf_by_function(idx, graph, FUNCTION_NAME)
    assert hull is not None, "VesselHullLight custom function node missing"
    assert hull["m_FunctionSource"] == HLSL_GUID, "hull light node points at the wrong HLSL asset"
    assert hull["m_SourceType"] == 0, "hull light node is not in File source mode"

    slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in hull["m_Slots"]}
    assert set(slots) == {s[0] for s in CF_SLOTS}, "hull light slot ids do not match the HLSL signature"
    for slot_id, name, kind, is_output in CF_SLOTS:
        assert slots[slot_id]["m_DisplayName"] == name, f"hull light slot {slot_id} name drifted"
        assert slots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"hull light slot {slot_id} direction wrong"
        assert kind in slots[slot_id]["m_Type"], f"hull light slot {slot_id} is not a {kind} slot"

    hid = hull["m_ObjectId"]
    for slot, typ in ((0, "PositionNode"), (1, "NormalVectorNode")):
        src = sources.get((hid, slot))
        assert src is not None, f"hull light slot {slot} unconnected"
        node = idx[src[0]]
        assert node.get("m_Type", "").endswith(typ), f"hull light slot {slot} not fed by a {typ}"
        assert node.get("m_Space") == vision.COORDINATE_SPACE_WORLD, f"hull light slot {slot} is not WORLD space"

    dom = property_feeding(idx, sources, hid, 2)
    assert dom["m_DefaultReferenceName"] == DOMAIN_PROPERTY[1], "Domain is not fed by _VesselHullDomain"
    assert dom["m_GeneratePropertyBlock"] is True, "_VesselHullDomain must be EXPOSED (a property block cannot reach it otherwise)"
    assert dom["m_Value"]["a"] == 0.0, "_VesselHullDomain default alpha must be 0 (alpha is the stamped marker)"

    for slot, (_, ref, default) in ((3, GLOSS_PROPERTY), (4, SELFLIT_PROPERTY)):
        p = property_feeding(idx, sources, hid, slot)
        assert p["m_DefaultReferenceName"] == ref, f"hull light slot {slot} is not fed by {ref}"
        assert p["m_GeneratePropertyBlock"] is True, f"{ref} must be exposed (it is set per material)"
        assert p["m_Value"] == default, f"{ref} default drifted ({p['m_Value']} != {default})"

    base = sources.get((hid, 5))
    assert base is not None and idx[base[0]].get("m_Type", "").endswith("MultiplyNode"), \
        "hull light BaseColor is not fed by the graph's final Multiply"

    vis = vision.cf_by_function(idx, graph, vision.FUNCTION_NAME)
    assert sources.get((vis["m_ObjectId"], VISION_BASECOLOR_SLOT)) == (hid, 6), \
        "the vision band's BaseColor is not fed by the hull light (it must sit between the Multiply and the band)"


def make_float_slot(donor, slot_id, name):
    s = vision.clone(donor)
    s["m_Id"] = slot_id
    s["m_DisplayName"] = name
    s["m_ShaderOutputName"] = name
    s["m_SlotType"] = 0
    s["m_StageCapability"] = 3
    s["m_Hidden"] = False
    s["m_Value"] = 0.0
    s["m_DefaultValue"] = 0.0
    s["m_Labels"] = []
    return s


def wire(check_only):
    path = os.path.join(REPO, GRAPH)
    docs = vision.load_docs(path)
    graph = vision.find_graph(docs)
    idx = vision.index(docs)

    if vision.cf_by_function(idx, graph, FUNCTION_NAME) is not None:
        validate(docs)
        return f"{os.path.basename(GRAPH)}: hull light already wired (validated)."
    if check_only:
        return None

    vision.validate(docs, expect_wired=True)
    sources = sources_of(graph)
    vis = vision.cf_by_function(idx, graph, vision.FUNCTION_NAME)
    vid = vis["m_ObjectId"]

    # ---- donors (all in this graph) ------------------------------------------------------------
    vslots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in vis["m_Slots"]}
    donor_v3 = vslots[3]
    donor_v4 = vslots[2]
    color_prop_node = next(n for n in vision.nodes_of_type(idx, graph, "PropertyNode")
                           if "ColorShaderProperty" in idx[n["m_Property"]["m_Id"]]["m_Type"])
    color_prop = idx[color_prop_node["m_Property"]["m_Id"]]
    float_prop_node = next(n for n in vision.nodes_of_type(idx, graph, "PropertyNode")
                           if "Vector1ShaderProperty" in idx[n["m_Property"]["m_Id"]]["m_Type"])
    float_prop = idx[float_prop_node["m_Property"]["m_Id"]]
    float_out_slot = idx[float_prop_node["m_Slots"][0]["m_Id"]]
    color_out_slot = idx[color_prop_node["m_Slots"][0]["m_Id"]]

    new_docs = []
    category = idx[graph["m_CategoryData"][0]["m_Id"]]

    def add_property(prop_donor, node_donor, out_donor, name, ref, value, y):
        prop = vision.clone(prop_donor)
        prop["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
        prop["m_Name"] = name
        prop["m_RefNameGeneratedByDisplayName"] = name
        prop["m_DefaultReferenceName"] = ref
        prop["m_OverrideReferenceName"] = ""
        prop["m_GeneratePropertyBlock"] = True
        prop["m_Hidden"] = False
        prop["m_Value"] = value
        if "isMainColor" in prop:
            prop["isMainColor"] = False
            prop["m_ColorMode"] = vision.COLOR_MODE_HDR
        node = vision.clone(node_donor)
        node["m_Property"] = {"m_Id": prop["m_ObjectId"]}
        node["m_DrawState"]["m_Position"].update({"x": 60.0, "y": y, "width": 160.0, "height": 34.0})
        out = vision.clone(out_donor)
        out["m_Id"] = 0
        out["m_DisplayName"] = name
        out["m_ShaderOutputName"] = "Out"
        out["m_SlotType"] = 1
        node["m_Slots"] = [{"m_Id": out["m_ObjectId"]}]
        new_docs.extend([prop, node, out])
        graph["m_Properties"].append({"m_Id": prop["m_ObjectId"]})
        category["m_ChildObjectList"].append({"m_Id": prop["m_ObjectId"]})
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})
        return node, out

    dom_node, dom_out = add_property(color_prop, color_prop_node, color_out_slot,
                                     DOMAIN_PROPERTY[0], DOMAIN_PROPERTY[1],
                                     {"r": 0.0, "g": 0.0, "b": 0.0, "a": 0.0}, 1180.0)
    gloss_node, gloss_out = add_property(float_prop, float_prop_node, float_out_slot,
                                         GLOSS_PROPERTY[0], GLOSS_PROPERTY[1], GLOSS_PROPERTY[2], 1230.0)
    self_node, self_out = add_property(float_prop, float_prop_node, float_out_slot,
                                       SELFLIT_PROPERTY[0], SELFLIT_PROPERTY[1], SELFLIT_PROPERTY[2], 1280.0)

    # ---- the custom function node ----------------------------------------------------------------
    hull = vision.clone(vis)
    hull["m_Name"] = f"{FUNCTION_NAME} (Custom Function)"
    hull["m_FunctionName"] = FUNCTION_NAME
    hull["m_FunctionSource"] = HLSL_GUID
    hull["m_SourceType"] = 0
    hull["m_DrawState"]["m_Position"].update({"x": 380.0, "y": 1150.0, "width": 232.0, "height": 330.0})
    hslots = []
    for slot_id, name, kind, is_output in CF_SLOTS:
        if kind == "Vector1":
            hslots.append(make_float_slot(float_out_slot, slot_id, name))
        else:
            hslots.append(vision.make_slot(donor_v4 if kind == "Vector4" else donor_v3, slot_id, name, is_output))
    hull["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in hslots]
    new_docs += [hull] + hslots
    graph["m_Nodes"].append({"m_Id": hull["m_ObjectId"]})
    hid = hull["m_ObjectId"]

    # ---- retarget Multiply -> VISION.BaseColor into HULL.BaseColor ----------------------------------
    retargeted = 0
    for e in graph["m_Edges"]:
        if e["m_InputSlot"]["m_Node"]["m_Id"] == vid and e["m_InputSlot"]["m_SlotId"] == VISION_BASECOLOR_SLOT:
            assert idx[e["m_OutputSlot"]["m_Node"]["m_Id"]].get("m_Type", "").endswith("MultiplyNode"), \
                "vision BaseColor is not fed by the final Multiply - graph shape drifted, refusing to splice"
            e["m_InputSlot"] = {"m_Node": {"m_Id": hid}, "m_SlotId": 5}
            retargeted += 1
    assert retargeted == 1, f"expected one vision BaseColor feeder, retargeted {retargeted}"

    pos = sources[(vid, VISION_POSITION_SLOT)]
    nrm = sources[(vid, VISION_NORMAL_SLOT)]
    graph["m_Edges"] += [
        vision.edge(pos[0], pos[1], hid, 0),
        vision.edge(nrm[0], nrm[1], hid, 1),
        vision.edge(dom_node["m_ObjectId"], dom_out["m_Id"], hid, 2),
        vision.edge(gloss_node["m_ObjectId"], gloss_out["m_Id"], hid, 3),
        vision.edge(self_node["m_ObjectId"], self_out["m_Id"], hid, 4),
        vision.edge(hid, 6, vid, VISION_BASECOLOR_SLOT),
    ]

    docs += new_docs
    validate(docs)
    open(path, "w", encoding="utf-8").write(vision.dump_docs(docs))
    validate(vision.load_docs(path))
    return (f"{os.path.basename(GRAPH)}: hull light wired and validated "
            f"(+4 nodes, +3 exposed properties, 6 new edges, 1 retargeted).")


def main():
    message = wire("--check" in sys.argv)
    if message is None:
        print(f"  {os.path.basename(GRAPH)}: hull light NOT wired.", file=sys.stderr)
        return 1
    print("  " + message)
    return 0


if __name__ == "__main__":
    sys.exit(main())
