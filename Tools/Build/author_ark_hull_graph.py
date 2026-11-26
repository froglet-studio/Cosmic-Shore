#!/usr/bin/env python3
"""Write ArkHullGraph.shadergraph: the vessel graph the Ark wears, plus the prisms' see-through corridor.

WHY. Shombith's playtest (2026-11-26) of the round Ark: inside its tube the chase camera sits behind and above the
vessel, outside the tube's wall, so the hull hides the vessel. He picked option C on the concept page
(https://claude.ai/artifact/DhVb8uuZekpv6yj1KRcuNM, round 14): the hull dissolves between the camera and the vessel by
the same rule the prisms already follow (PrismOcclusionCorridor.cs / PrismOcclusionCorridor.hlsl, Docs/PRISM_ANIMATION.md
section 4.7).

WHAT. The output is VesselGraph.shadergraph, the graph every Ark material wears, with these changes and nothing else:
  - the corridor's two globals as graph properties (_PrismOcclusionTarget, _PrismOcclusionParams; no property block,
    exactly as BlockGraph declares them);
  - a PrismOcclusionFade Custom Function node (the BlockGraph's own call into PrismOcclusionCorridor.hlsl), fed the
    world position and those two globals, with BaseAlpha 1: the Ark is opaque, so only the corridor makes it see-through;
  - its Alpha and ClipThreshold wired to SurfaceDescription.Alpha and .AlphaClipThreshold, and alpha clip on;
  - its colour defaults are black (a default never reaches a pixel: ArkModel swaps the shader onto material copies that
    carry their colours, and the colour trace has no untraced default to list).
The vessels keep VesselGraph unchanged. ArkModel swaps its own material copies to this shader
(ArkModelDefinitionSO.SeeThroughShader).

Every id this adds is derived from a fixed name, so a rerun writes the same file.

    python3 Tools/Build/author_ark_hull_graph.py           # write the graph (and its .meta the first time)
    python3 Tools/Build/author_ark_hull_graph.py --check   # fail when the committed graph drifted from VesselGraph
"""

import hashlib
import json
import os
import re
import sys
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPHS = os.path.join(REPO, "Assets", "_Graphics", "Materials", "Graphs")
SOURCE = os.path.join(GRAPHS, "VesselGraph.shadergraph")
OUTPUT = os.path.join(GRAPHS, "ArkHullGraph.shadergraph")
CORRIDOR_HLSL_GUID = "bf8e2c1fa76142c89ba03b2e1ae46201"   # PrismOcclusionCorridor.hlsl
OUTPUT_GUID = "6a1d2c58e3f04b7c9b2e0d4a7f3c81e5"            # ArkHullGraph.shadergraph (fixed: the definition binds it)


def oid(name):
    return hashlib.md5(("ArkHullGraph." + name).encode()).hexdigest()


def load(path):
    text = open(path, encoding="utf-8").read()
    return [json.loads(part) for part in re.split(r"\n\n(?=\{)", text) if part.strip()]


def dump(objs):
    return "\n\n".join(json.dumps(o, indent=4) for o in objs) + "\n"


def slot(kind, name, slot_id, display, output, value):
    zero = {"x": 0.0, "y": 0.0, "z": 0.0} if kind == "Vector3" else 0.0
    return {
        "m_SGVersion": 0, "m_Type": f"UnityEditor.ShaderGraph.{kind}MaterialSlot", "m_ObjectId": oid(name),
        "m_Id": slot_id, "m_DisplayName": display, "m_SlotType": 1 if output else 0, "m_Hidden": False,
        "m_ShaderOutputName": display,
        "m_StageCapability": 3, "m_Value": value if value is not None else zero, "m_DefaultValue": zero, "m_Labels": [],
    }


def node_ref(i):
    return {"m_Id": i}


def draw(x, y, w=208.0, h=300.0):
    return {"m_Expanded": True, "m_Position": {"serializedVersion": "2", "x": x, "y": y, "width": w, "height": h}}


def global_property(name):
    return {
        "m_SGVersion": 1, "m_Type": "UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty", "m_ObjectId": oid("prop." + name),
        "m_Guid": {"m_GuidSerialized": str(uuid.UUID(oid("guid." + name)))},
        "m_Name": name, "m_DefaultRefNameVersion": 1, "m_RefNameGeneratedByDisplayName": name,
        "m_DefaultReferenceName": "_" + name, "m_OverrideReferenceName": "_" + name, "m_GeneratePropertyBlock": False,
        "m_UseCustomSlotLabel": False, "m_CustomSlotLabel": "", "m_DismissedVersion": 0, "m_Precision": 0,
        "overrideHLSLDeclaration": False, "hlslDeclarationOverride": 0, "m_Hidden": False,
        "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0},
    }


def property_node(name, x, y):
    out = slot("Vector3", "propnode.slot." + name, 0, name, True, None)
    out["m_ShaderOutputName"] = "Out"
    node = {
        "m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.PropertyNode", "m_ObjectId": oid("propnode." + name),
        "m_Group": {"m_Id": ""}, "m_Name": "Property", "m_DrawState": draw(x, y, 230.0, 34.0),
        "m_Slots": [node_ref(out["m_ObjectId"])], "synonyms": [], "m_Precision": 0, "m_PreviewExpanded": True,
        "m_DismissedVersion": 0, "m_PreviewMode": 0, "m_CustomColors": {"m_SerializableColors": []},
        "m_Property": node_ref(oid("prop." + name)),
    }
    return node, out


def edge(out_node, out_slot, in_node, in_slot):
    return {"m_OutputSlot": {"m_Node": node_ref(out_node), "m_SlotId": out_slot},
            "m_InputSlot": {"m_Node": node_ref(in_node), "m_SlotId": in_slot}}


def build():
    objs = load(SOURCE)
    by = {o["m_ObjectId"]: o for o in objs}
    graph = objs[0]
    assert graph["m_Type"].endswith("GraphData"), "VesselGraph's first document is not its GraphData"

    def block(descriptor):
        for o in objs:
            if o["m_Type"].endswith("BlockNode") and o.get("m_SerializedDescriptor") == descriptor:
                return o
        raise SystemExit(f"VesselGraph has no {descriptor} block")

    alpha, clip = block("SurfaceDescription.Alpha"), block("SurfaceDescription.AlphaClipThreshold")
    fragment = {b["m_Id"] for b in graph["m_FragmentContext"]["m_Blocks"]}
    assert alpha["m_ObjectId"] in fragment and clip["m_ObjectId"] in fragment, "Alpha / AlphaClipThreshold not on the stack"

    # The new nodes, at the left of the fragment stack.
    fx, fy = graph["m_FragmentContext"]["m_Position"]["x"] - 420.0, graph["m_FragmentContext"]["m_Position"]["y"] + 260.0
    pos_slot = slot("Vector3", "position.slot", 0, "Out", True, None)
    position = {
        "m_SGVersion": 1, "m_Type": "UnityEditor.ShaderGraph.PositionNode", "m_ObjectId": oid("position"),
        "m_Group": {"m_Id": ""}, "m_Name": "Position", "m_DrawState": draw(fx - 520.0, fy, 206.0, 131.0),
        "m_Slots": [node_ref(pos_slot["m_ObjectId"])], "synonyms": ["location"], "m_Precision": 1,
        "m_PreviewExpanded": False, "m_PreviewMode": 2, "m_CustomColors": {"m_SerializableColors": []},
        "m_Space": 2, "m_PositionSource": 0,
    }
    target_node, target_slot = property_node("PrismOcclusionTarget", fx - 520.0, fy + 150.0)
    params_node, params_slot = property_node("PrismOcclusionParams", fx - 520.0, fy + 200.0)
    cf_slots = [
        slot("Vector3", "cf.PositionWS", 0, "PositionWS", False, None),
        slot("Vector3", "cf.Target", 1, "Target", False, None),
        slot("Vector3", "cf.Params", 2, "Params", False, None),
        slot("Vector1", "cf.BaseAlpha", 3, "BaseAlpha", False, 1.0),   # the Ark is opaque: only the corridor fades it
        slot("Vector1", "cf.Alpha", 4, "Alpha", True, None),
        slot("Vector1", "cf.ClipThreshold", 5, "ClipThreshold", True, None),
    ]
    fade = {
        "m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.CustomFunctionNode", "m_ObjectId": oid("fade"),
        "m_Group": {"m_Id": ""}, "m_Name": "PrismOcclusionFade (Custom Function)", "m_DrawState": draw(fx, fy, 232.0, 350.0),
        "m_Slots": [node_ref(s["m_ObjectId"]) for s in cf_slots], "synonyms": [], "m_Precision": 0,
        "m_PreviewExpanded": False, "m_DismissedVersion": 0, "m_PreviewMode": 0,
        "m_CustomColors": {"m_SerializableColors": []}, "m_SourceType": 0, "m_FunctionName": "PrismOcclusionFade",
        "m_FunctionSource": CORRIDOR_HLSL_GUID, "m_FunctionBody": "Enter function body here...",
    }
    props = [global_property("PrismOcclusionTarget"), global_property("PrismOcclusionParams")]

    # The graph: the properties, the nodes, the edges (the old alpha source is unhooked from the stack).
    graph["m_Properties"] += [node_ref(p["m_ObjectId"]) for p in props]
    category = by[graph["m_CategoryData"][0]["m_Id"]]
    category["m_ChildObjectList"] += [node_ref(p["m_ObjectId"]) for p in props]
    graph["m_Nodes"] += [node_ref(n["m_ObjectId"]) for n in (position, target_node, params_node, fade)]
    graph["m_Edges"] = [e for e in graph["m_Edges"]
                        if e["m_InputSlot"]["m_Node"]["m_Id"] not in (alpha["m_ObjectId"], clip["m_ObjectId"])]
    graph["m_Edges"] += [
        edge(position["m_ObjectId"], 0, fade["m_ObjectId"], 0),
        edge(target_node["m_ObjectId"], 0, fade["m_ObjectId"], 1),
        edge(params_node["m_ObjectId"], 0, fade["m_ObjectId"], 2),
        edge(fade["m_ObjectId"], 4, alpha["m_ObjectId"], 0),
        edge(fade["m_ObjectId"], 5, clip["m_ObjectId"], 0),
    ]
    for o in objs:
        if o["m_Type"].endswith("UniversalTarget"):
            o["m_AlphaClip"] = True
        # The colour defaults are black: the Ark swaps this shader onto material copies that carry their own colours, so
        # a default never reaches a pixel, and no untraced colour is authored here (colour_trace.py --check).
        if o["m_Type"].endswith("ColorShaderProperty"):
            o["m_Value"] = dict(o["m_Value"], r=0.0, g=0.0, b=0.0)   # the alpha is the graph's (the vision tint's 0 = off)
    return objs + props + [position, pos_slot, target_node, target_slot, params_node, params_slot, fade] + cf_slots


META = f"""fileFormatVersion: 2
guid: {OUTPUT_GUID}
ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 2
  userData:
  assetBundleName:
  assetBundleVariant:
  script: {{fileID: 11500000, guid: 625f186215c104763be7675aa2d941aa, type: 3}}
"""


def main():
    text = dump(build())
    if "--check" in sys.argv:
        if not os.path.exists(OUTPUT) or open(OUTPUT, encoding="utf-8").read() != text:
            print("author_ark_hull_graph: ArkHullGraph.shadergraph drifted from VesselGraph + the corridor; rerun "
                  "python3 Tools/Build/author_ark_hull_graph.py")
            return 1
        print("author_ark_hull_graph: ArkHullGraph.shadergraph is VesselGraph + the corridor")
        return 0
    open(OUTPUT, "w", encoding="utf-8").write(text)
    if not os.path.exists(OUTPUT + ".meta"):
        open(OUTPUT + ".meta", "w", encoding="utf-8").write(META)
    print("wrote", os.path.relpath(OUTPUT, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main())
