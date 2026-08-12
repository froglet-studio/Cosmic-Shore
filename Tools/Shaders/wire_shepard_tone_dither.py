#!/usr/bin/env python3
"""
Splice the Shepard-tone screen door into ShepardGraph — the mass crystal's four nested
shells stop blending and start dropping coverage instead.

WHY (short form; the long form is the header of ShepardToneDither.hlsl and Docs/SHEPARD_TONE.md):
a Shepard tone only works if the eye can TRACK an individual partial, and alpha blending
composites the four shells into one soft ball with no per-shell edge, no depth ordering,
and a 34% silhouette sawtooth that exposes the loop. A screen door drops fragments instead
of intensity, so every shell keeps a hard edge and full fresnel down to a handful of
shards, opaque + ZWrite gives the nesting real parallax, and the outermost 5% shell finally
reads as present.

THE SPLICE — the alpha the graph already computes becomes the dither's coverage input, and
the threshold takes over the (previously constant 0.01) clip block:

  BEFORE:  Multiply(1.05 - travel, _Opacity) --------------> SurfaceDescription.Alpha
                                                             SurfaceDescription.AlphaClipThreshold = 0.01 (constant)
  AFTER:   Multiply -----------------> DITHER.BaseAlpha
           Position(Object) ---------> DITHER.PositionOS      (cloned, fragment stage)
           _Start -------------------> DITHER.Start           (cloned property node)
           _Stop --------------------> DITHER.Stop            (cloned property node)
           DITHER.Alpha -------------------------------------> SurfaceDescription.Alpha
           DITHER.ClipThreshold -----------------------------> SurfaceDescription.AlphaClipThreshold

_Start/_Stop are wired in so the kernel can derive a per-shell lattice seed from the one
thing that already differs between the four shells. Without it every shell would punch its
holes along the same rays and the crystal would look like it had windows drilled through it.

It also flips the UniversalTarget to OPAQUE (surface 0, alpha mode 0, ZWrite auto), because
a dither that renders in the transparent queue with ZWrite off buys none of the depth
parallax that is half the reason to do this. The MATERIALS carry the same flip — run
Tools/Shaders/enable_shepard_alpha_clip.py after this.

Out-of-editor ShaderGraph JSON synthesis per /asset-surgery: parse everything, clone
same-file donors, rebuild in memory, assert every invariant, only then write. Idempotent —
re-run after a graph revert or a merge to repair. Usage: [--check] validates without writing.
"""

import json
import os
import sys
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPHS = ["Assets/_Graphics/Materials/Graphs/ShepardGraph.shadergraph"]

# ShepardGraph ships no Custom Function node, so the donor is external — the same pattern
# the corridor wirer uses for its Position node. BlockGraph's PrismOcclusionFade carries
# both a Vector3 and a Vector1 slot, in and out, which is the whole slot vocabulary needed.
CF_DONOR = "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph"
CF_DONOR_FUNCTION = "PrismOcclusionFade"

HLSL_GUID = "1af4b28d920441fd9ae968eaffac68c4"
FUNCTION_NAME = "ShepardToneDither"

ALPHA_BLOCK = "SurfaceDescription.Alpha"
CLIP_BLOCK = "SurfaceDescription.AlphaClipThreshold"

# Integer slot ids MUST match the HLSL parameter order (see ShepardToneDither_float).
CF_SLOTS = [
    (0, "PositionOS", "Vector3", False),
    (1, "BaseAlpha", "Vector1", False),
    (2, "Start", "Vector1", False),
    (3, "Stop", "Vector1", False),
    (4, "Alpha", "Vector1", True),
    (5, "ClipThreshold", "Vector1", True),
]

COORDINATE_SPACE_OBJECT = 0
SEED_PROPERTIES = ("Start", "Stop")

# UniversalTarget: the opaque pattern. SurfaceType 0 = Opaque, AlphaMode 0 = Alpha,
# ZWriteControl 0 = Auto (which on an opaque surface means ON). AlphaClip stays true.
OPAQUE_TARGET = {"m_SurfaceType": 0, "m_AlphaMode": 0, "m_ZWriteControl": 0, "m_AlphaClip": True}


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
    for r in graph["m_Nodes"]:
        node = idx[r["m_Id"]]
        if node.get("m_FunctionName") == fn:
            return node
    return None


def find_block(docs, descriptor):
    for d in docs:
        if d.get("m_SerializedDescriptor") == descriptor:
            return d
    return None


def find_target(docs):
    return next(d for d in docs if d.get("m_Type", "").endswith("UniversalTarget"))


def property_node(idx, graph, name):
    """The PropertyNode already on the graph for the named property (a donor to clone)."""
    for r in graph["m_Nodes"]:
        node = idx[r["m_Id"]]
        if not node.get("m_Type", "").endswith("PropertyNode"):
            continue
        prop = idx.get(node["m_Property"]["m_Id"])
        if prop is not None and prop.get("m_Name") == name:
            return node
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


def clone_node(idx, donor, x, y):
    """Deep-clone a node and its slots with fresh object ids. Returns (node, [slots])."""
    node = json.loads(json.dumps(donor))
    node["m_ObjectId"] = uuid.uuid4().hex
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y})
    slots = []
    for ref in donor["m_Slots"]:
        s = json.loads(json.dumps(idx[ref["m_Id"]]))
        s["m_ObjectId"] = uuid.uuid4().hex
        slots.append(s)
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def edge(out_node, out_slot, in_node, in_slot):
    return {
        "m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
        "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot},
    }


def validate(docs, expect_wired):
    """Every structural invariant, asserted against the in-memory model before any write."""
    idx = index(docs)
    graph = find_graph(docs)

    ids = [d["m_ObjectId"] for d in docs if "m_ObjectId" in d]
    assert len(ids) == len(set(ids)), "duplicate m_ObjectId"

    slot_ids = {}
    for ref in graph["m_Nodes"]:
        assert ref["m_Id"] in idx, f"m_Nodes references missing {ref['m_Id']}"
        node = idx[ref["m_Id"]]
        ints = set()
        for s in node.get("m_Slots", []):
            assert s["m_Id"] in idx, f"node {ref['m_Id']} slot {s['m_Id']} missing"
            sd = idx[s["m_Id"]]
            assert sd["m_Id"] not in ints, f"duplicate integer slot id on node {ref['m_Id']}"
            ints.add(sd["m_Id"])
        slot_ids[ref["m_Id"]] = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in node.get("m_Slots", [])}

    for ref in graph["m_Properties"]:
        assert ref["m_Id"] in idx, f"m_Properties references missing {ref['m_Id']}"

    feeders = {}
    for e in graph["m_Edges"]:
        o, i = e["m_OutputSlot"], e["m_InputSlot"]
        for end, label in ((o, "output"), (i, "input")):
            nid = end["m_Node"]["m_Id"]
            assert nid in slot_ids, f"edge {label} node {nid} not registered"
            assert end["m_SlotId"] in slot_ids[nid], f"edge {label} slot {end['m_SlotId']} missing on {nid}"
        assert slot_ids[o["m_Node"]["m_Id"]][o["m_SlotId"]]["m_SlotType"] == 1, "edge output not an output slot"
        assert slot_ids[i["m_Node"]["m_Id"]][i["m_SlotId"]]["m_SlotType"] == 0, "edge input not an input slot"
        key = (i["m_Node"]["m_Id"], i["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
    for key, count in feeders.items():
        assert count == 1, f"input slot {key} has {count} feeders (must be exactly 1)"

    if not expect_wired:
        return

    node = cf_by_function(idx, graph, FUNCTION_NAME)
    assert node is not None, f"{FUNCTION_NAME} custom function node missing"
    assert node["m_FunctionSource"] == HLSL_GUID, "dither node points at the wrong HLSL asset"
    assert node["m_SourceType"] == 0, "dither node is not in File mode"
    nslots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in node["m_Slots"]}
    assert set(nslots) == {s[0] for s in CF_SLOTS}, "dither slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CF_SLOTS:
        assert nslots[slot_id]["m_DisplayName"] == name, f"dither slot {slot_id} name drifted"
        assert nslots[slot_id]["m_SlotType"] == (1 if is_output else 0), f"dither slot {slot_id} direction wrong"

    sources = {}
    for e in graph["m_Edges"]:
        sources[(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])] = \
            (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])

    pos_src = sources.get((node["m_ObjectId"], 0))
    assert pos_src is not None, "dither PositionOS unconnected"
    assert idx[pos_src[0]].get("m_Type", "").endswith("PositionNode"), "PositionOS not fed by a Position node"
    assert idx[pos_src[0]].get("m_Space") == COORDINATE_SPACE_OBJECT, \
        "PositionOS node is not OBJECT space — the anchor must be scale-invariant and glued to the mesh"

    base_src = sources.get((node["m_ObjectId"], 1))
    assert base_src is not None, "dither BaseAlpha unconnected"
    assert not idx[base_src[0]].get("m_SerializedDescriptor"), "BaseAlpha fed by a block, not the alpha chain"

    for slot_id, prop_name in ((2, "Start"), (3, "Stop")):
        src = sources.get((node["m_ObjectId"], slot_id))
        assert src is not None, f"dither {prop_name} unconnected"
        feeder = idx[src[0]]
        assert feeder.get("m_Type", "").endswith("PropertyNode"), f"{prop_name} not fed by a property node"
        assert idx[feeder["m_Property"]["m_Id"]]["m_Name"] == prop_name, \
            f"dither {prop_name} is fed by the wrong property (the per-shell seed would collide)"

    alpha_block = find_block(docs, ALPHA_BLOCK)
    assert alpha_block is not None, f"{ALPHA_BLOCK} block missing"
    assert sources.get((alpha_block["m_ObjectId"], 0)) == (node["m_ObjectId"], 4), \
        f"{ALPHA_BLOCK} is not fed by the dither node's Alpha"

    clip_block = find_block(docs, CLIP_BLOCK)
    assert clip_block is not None, f"{CLIP_BLOCK} block missing"
    assert sources.get((clip_block["m_ObjectId"], 0)) == (node["m_ObjectId"], 5), \
        f"{CLIP_BLOCK} is not fed by the dither node's ClipThreshold"

    target = find_target(docs)
    for key, want in OPAQUE_TARGET.items():
        assert target.get(key) == want, \
            f"UniversalTarget {key} is {target.get(key)!r}, expected {want!r} (the dither needs the opaque queue)"


def wire(rel_path, check_only):
    path = os.path.join(REPO, rel_path)
    docs = load_docs(path)
    graph = find_graph(docs)
    idx = index(docs)

    if cf_by_function(idx, graph, FUNCTION_NAME) is not None:
        validate(docs, expect_wired=True)
        return False, f"{os.path.basename(rel_path)}: already wired (validated)."
    if check_only:
        return False, None

    validate(docs, expect_wired=False)

    # ---- donors -------------------------------------------------------------
    donor_docs = load_docs(os.path.join(REPO, CF_DONOR))
    donor_idx = index(donor_docs)
    donor_cf = cf_by_function(donor_idx, find_graph(donor_docs), CF_DONOR_FUNCTION)
    assert donor_cf is not None, f"no {CF_DONOR_FUNCTION} node in {CF_DONOR} to clone"
    donor_slot_v3 = next(donor_idx[s["m_Id"]] for s in donor_cf["m_Slots"]
                         if "Vector3MaterialSlot" in donor_idx[s["m_Id"]]["m_Type"])
    donor_slot_v1 = next(donor_idx[s["m_Id"]] for s in donor_cf["m_Slots"]
                         if "Vector1MaterialSlot" in donor_idx[s["m_Id"]]["m_Type"])

    donor_pos = next((idx[r["m_Id"]] for r in graph["m_Nodes"]
                      if idx[r["m_Id"]].get("m_Type", "").endswith("PositionNode")
                      and idx[r["m_Id"]].get("m_Space") == COORDINATE_SPACE_OBJECT), None)
    assert donor_pos is not None, f"{rel_path}: no object-space Position node to clone"

    new_docs = []

    # A FRESH object-space Position node for the fragment stage. The graph's existing ones
    # live in the vertex chain; cloning rather than fanning one out keeps the two stages'
    # wiring independent and unambiguous to read in the editor.
    position_node, position_slots = clone_node(idx, donor_pos, -1200.0, 900.0)
    new_docs += [position_node] + position_slots

    # Fresh property nodes for the seed inputs, for the same reason.
    seed_nodes = {}
    for i, name in enumerate(SEED_PROPERTIES):
        donor = property_node(idx, graph, name)
        assert donor is not None, f"{rel_path}: no '{name}' property node to clone"
        n, s = clone_node(idx, donor, -1200.0, 1080.0 + i * 90.0)
        seed_nodes[name] = (n, s)
        new_docs += [n] + s

    dither_node = json.loads(json.dumps(donor_cf))
    dither_node["m_ObjectId"] = uuid.uuid4().hex
    dither_node["m_Name"] = f"{FUNCTION_NAME} (Custom Function)"
    dither_node["m_FunctionName"] = FUNCTION_NAME
    dither_node["m_FunctionSource"] = HLSL_GUID
    dither_node["m_SourceType"] = 0
    dither_node["m_DrawState"]["m_Position"].update(
        {"x": -820.0, "y": 900.0, "width": 240.0, "height": 300.0})
    dslots = [make_slot(donor_slot_v3 if kind == "Vector3" else donor_slot_v1, sid, name, out)
              for sid, name, kind, out in CF_SLOTS]
    dither_node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in dslots]
    new_docs += [dither_node] + dslots

    for node in [position_node, dither_node] + [n for n, _ in seed_nodes.values()]:
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    # ---- edges --------------------------------------------------------------
    alpha_block = find_block(docs, ALPHA_BLOCK)
    clip_block = find_block(docs, CLIP_BLOCK)
    assert alpha_block is not None, f"{rel_path}: no {ALPHA_BLOCK} block"
    assert clip_block is not None, f"{rel_path}: no {CLIP_BLOCK} block"

    # The clip block must still be an authored constant — a feeder here means someone else
    # already owns the threshold and this splice would be the second one.
    for e in graph["m_Edges"]:
        assert e["m_InputSlot"]["m_Node"]["m_Id"] != clip_block["m_ObjectId"], \
            f"{rel_path}: {CLIP_BLOCK} already has a feeder — graph shape drifted, refusing"

    # Retarget the existing alpha feeder into the dither's BaseAlpha (never add a second
    # feeder to an input slot).
    retargeted = 0
    for e in graph["m_Edges"]:
        if e["m_InputSlot"]["m_Node"]["m_Id"] == alpha_block["m_ObjectId"]:
            e["m_InputSlot"] = {"m_Node": {"m_Id": dither_node["m_ObjectId"]}, "m_SlotId": 1}
            retargeted += 1
    assert retargeted == 1, f"{rel_path}: expected one Alpha feeder, retargeted {retargeted}"

    graph["m_Edges"] += [
        edge(position_node["m_ObjectId"], position_slots[0]["m_Id"], dither_node["m_ObjectId"], 0),
        edge(seed_nodes["Start"][0]["m_ObjectId"], seed_nodes["Start"][1][0]["m_Id"], dither_node["m_ObjectId"], 2),
        edge(seed_nodes["Stop"][0]["m_ObjectId"], seed_nodes["Stop"][1][0]["m_Id"], dither_node["m_ObjectId"], 3),
        edge(dither_node["m_ObjectId"], 4, alpha_block["m_ObjectId"], 0),
        edge(dither_node["m_ObjectId"], 5, clip_block["m_ObjectId"], 0),
    ]

    # ---- target: transparent -> opaque --------------------------------------
    target = find_target(docs)
    flips = [f"{k} {target.get(k)!r}->{v!r}" for k, v in OPAQUE_TARGET.items() if target.get(k) != v]
    target.update(OPAQUE_TARGET)

    docs += new_docs
    validate(docs, expect_wired=True)   # nothing written yet

    open(path, "w", encoding="utf-8").write(dump_docs(docs))
    validate(load_docs(path), expect_wired=True)
    nodes = 2 + len(SEED_PROPERTIES)
    return True, (f"{os.path.basename(rel_path)}: wired and validated "
                  f"(+{nodes} nodes, +{len(new_docs) - nodes} slots, 5 new edges, 1 retargeted; "
                  f"target {', '.join(flips) if flips else 'already opaque'}).")


def main():
    check_only = "--check" in sys.argv
    unwired = []
    for rel in GRAPHS:
        _changed, message = wire(rel, check_only)
        if message is None:
            unwired.append(rel)
            print(f"  {os.path.basename(rel)}: NOT wired.", file=sys.stderr)
        else:
            print("  " + message)
    return 1 if unwired else 0


if __name__ == "__main__":
    sys.exit(main())
