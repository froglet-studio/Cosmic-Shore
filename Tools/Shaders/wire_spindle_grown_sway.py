#!/usr/bin/env python3
"""
Wire the GROWN SWAY (a per-vertex wave) into SpindleGraph, after SpindleSway.

    python3 Tools/Shaders/wire_spindle_grown_sway.py            # splice it in (idempotent)
    python3 Tools/Shaders/wire_spindle_grown_sway.py --check    # exit 1 if it is not wired, or wired wrong
    python3 Tools/Shaders/wire_spindle_grown_sway.py --self-test

Why this exists
---------------
A grown creature (Tools/Build/fauna_kit: the Ray and its kin) is one body of many limbs that each wave their own
way. The kit knows every vertex's wave as two vectors (a sine part and a cosine part); the lab plays them. In the
game SpindleSway is one shear along z, so a grown body could only move where it is cut into jointed pieces. This
adds the per-vertex wave to the spindle shader itself, so a wing ripples, a sleeve drifts and a streamer whips with
no joints and no per-frame CPU.

What it adds to SpindleGraph
----------------------------
  properties (exposed, plain per-material constants, default 0):
      GrownSway        _GrownSway         0 = off (an exact no-op), 1 = the wave as the mesh authors it
      GrownFrequency   _GrownFrequency    radians per second

  nodes:
      UV x2            TEXCOORD1, TEXCOORD2 (the two vectors a grown mesh carries; unity_port.py writes them)
      Property x4      _PrismClock, _Phase, _GrownSway, _GrownFrequency (the vertex chain's own nodes)
      SpindleGrownSway Custom Function (SpindleGrownSway.hlsl)

  edges:
      SpindleSway.Out      -> SpindleGrownSway.PositionOS      (was: -> VertexDescription.Position)
      UV1, UV2             -> SpindleGrownSway.SwaySin, .SwayCos
      the four properties  -> Clock, Phase, Strength, Frequency
      SpindleGrownSway.Out -> VertexDescription.Position

IT GOES AFTER SpindleSway, NOT BEFORE. wire_spindle_lod.py builds the four LOD / depth graphs from SpindleGraph by
splicing its own vertex function into the edge that FEEDS SpindleSway.PositionOS; that edge is left exactly as it
was, so the generator needs no change and every derived graph inherits the grown sway by being regenerated. A
creature's body draws with SpindleDepthGraph (SpindleLodSystem.AddBody), so that inheritance is what makes the wave
reach the game at all: re-run `wire_spindle_lod.py` after this, and its `--check` fails until somebody does.

DEFAULT 0 IS THE BLAST-RADIUS CONTROL, as it was for SpindleSway. SpindleGraph is worn by every flora branch, every
shipped creature and two non-spindle materials. The whole offset is multiplied by _GrownSway, so a material that
does not author it draws exactly what it drew, whatever its mesh holds in TEXCOORD1 and TEXCOORD2 (the braid's LOD
meshes keep other data there; most meshes have neither). Only a material written by fauna_kit/unity_port.py sets it.
"""

import copy
import hashlib
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wire_prism_spindle_death_clock import (  # noqa: E402  (shared, already-proven helpers)
    load_docs, dump_docs, find_graph, index, find_property, find_cf, make_slot, edge,
)
from wire_spindle_sway import (  # noqa: E402
    make_exposed_float, make_slot_v3, find_node, property_node_for, validate as validate_graph,
)

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPH = "Assets/_Graphics/Materials/Graphs/SpindleGraph.shadergraph"
UV_DONOR = "Assets/_Graphics/Materials/Lifeform_World_Shader Graphs/CreatureTextureGraph.shadergraph"
HLSL = "Assets/_Graphics/Materials/Graphs/SpindleGrownSway.hlsl"
HLSL_GUID = hashlib.md5(b"CosmicShore/SpindleGrownSway/SpindleGrownSway.hlsl").hexdigest()
FUNCTION = "SpindleGrownSway"
SWAY = "SpindleSway"
SWAY_OUT = 5
VERTEX_BLOCK = "VertexDescription.Position"

# (slot id, display name, kind, is_output) - order IS the HLSL signature.
SLOTS = [
    (0, "PositionOS", "v3", False),
    (1, "SwaySin",    "v3", False),
    (2, "SwayCos",    "v3", False),
    (3, "Clock",      "v1", False),
    (4, "Phase",      "v1", False),
    (5, "Strength",   "v1", False),
    (6, "Frequency",  "v1", False),
    (7, "BendX",      "v1", False),
    (8, "BendY",      "v1", False),
    (9, "BendZ",      "v1", False),
    (10, "StrokeX",   "v1", False),
    (11, "StrokeY",   "v1", False),
    (12, "Twist",     "v1", False),
    (13, "Out",       "v3", True),
]
OUT = 13
# The turn inputs (2026-11-18) are plain floats like the rest: GrownLimbDriver sets them per creature with a
# MaterialPropertyBlock, and a renderer nobody drives keeps the material's 0, which is the wave alone.
TURN_PROPS = ["GrownBendX", "GrownBendY", "GrownBendZ", "GrownStrokeX", "GrownStrokeY", "GrownTwist"]
NEW_PROPS = [("GrownSway", "_GrownSway", 0.0), ("GrownFrequency", "_GrownFrequency", 0.0)] + [(n, "_" + n, 0.0) for n in TURN_PROPS]
PROP_SLOTS = [(3, "PrismClock"), (4, "Phase"), (5, "GrownSway"), (6, "GrownFrequency")] + [(7 + i, n) for i, n in enumerate(TURN_PROPS)]


def _oid(seed):
    """Deterministic ids: a re-run on the same SpindleGraph reproduces the file byte for byte."""
    return hashlib.md5(f"CosmicShore/SpindleGrownSway/oid/{seed}".encode()).hexdigest()


def _guid(seed):
    h = hashlib.md5(f"CosmicShore/SpindleGrownSway/guid/{seed}".encode()).hexdigest()
    return f"{h[:8]}-{h[8:12]}-{h[12:16]}-{h[16:20]}-{h[20:]}"


def _block_feeders(docs):
    graph, idx = find_graph(docs), index(docs)
    block = find_node(docs, VERTEX_BLOCK, "BlockNode")
    slot = idx[block["m_Slots"][0]["m_Id"]]["m_Id"]
    return block, slot, [e for e in graph["m_Edges"] if e["m_InputSlot"]["m_Node"]["m_Id"] == block["m_ObjectId"]
                         and e["m_InputSlot"]["m_SlotId"] == slot]


def validate(docs, expect_wired):
    """Every structural rule wire_spindle_sway.validate holds for the whole graph, then this splice's own."""
    validate_graph(docs, expect_wired=False)
    errs = []
    graph, idx = find_graph(docs), index(docs)
    cf = find_cf(docs, FUNCTION)
    if not expect_wired:
        return errs
    if cf is None:
        return [f"{FUNCTION} custom function node missing"]
    if cf.get("m_FunctionSource") != HLSL_GUID:
        errs.append(f"{FUNCTION} points at the wrong HLSL asset")
    slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
    if set(slots) != {s[0] for s in SLOTS}:
        errs.append(f"{FUNCTION} slot ids do not match the HLSL signature")
    else:
        for slot_id, name, kind, is_output in SLOTS:
            s = slots[slot_id]
            want = "Vector3MaterialSlot" if kind == "v3" else "Vector1MaterialSlot"
            if s["m_DisplayName"] != name or s["m_SlotType"] != (1 if is_output else 0) or want not in s["m_Type"]:
                errs.append(f"{FUNCTION} slot {slot_id} ({name}) drifted from the HLSL signature")
    feeder = {}
    for e in graph["m_Edges"]:
        if e["m_InputSlot"]["m_Node"]["m_Id"] == cf["m_ObjectId"]:
            feeder[e["m_InputSlot"]["m_SlotId"]] = e["m_OutputSlot"]
    for slot_id, name, _kind, is_output in SLOTS:
        if not is_output and slot_id not in feeder:
            errs.append(f"{FUNCTION} input '{name}' is unconnected")
    sway = find_cf(docs, SWAY)
    if sway is None or feeder.get(0, {}).get("m_Node", {}).get("m_Id") != sway["m_ObjectId"] or feeder[0]["m_SlotId"] != SWAY_OUT:
        errs.append(f"{FUNCTION}.PositionOS is not fed by {SWAY}.Out")
    for slot_id, channel in ((1, 1), (2, 2)):
        src = idx.get(feeder.get(slot_id, {}).get("m_Node", {}).get("m_Id"))
        if not src or "UVNode" not in src.get("m_Type", "") or src.get("m_OutputChannel") != channel:
            errs.append(f"{FUNCTION} slot {slot_id} must read TEXCOORD{channel}")
    for slot_id, prop in PROP_SLOTS:
        src = idx.get(feeder.get(slot_id, {}).get("m_Node", {}).get("m_Id"))
        p = idx.get((src or {}).get("m_Property", {}).get("m_Id")) if src else None
        if not p or p.get("m_Name") != prop:
            errs.append(f"{FUNCTION} slot {slot_id} must read the {prop} property")
    for name, reference, _default in NEW_PROPS:
        p = find_property(docs, name)
        if p is None:
            errs.append(f"property {name} missing")
            continue
        if p["m_DefaultReferenceName"] != reference or p["m_GeneratePropertyBlock"] is not True or p["overrideHLSLDeclaration"] is not False:
            errs.append(f"{name} must be an exposed, plain per-material constant named {reference}")
        if p["m_Value"] != 0.0:
            errs.append(f"{name} default must be 0: that is what keeps every other material unchanged")
        if not any(r["m_Id"] == p["m_ObjectId"] for r in graph["m_Properties"]):
            errs.append(f"{name} is not in m_Properties")
    block, _slot, into = _block_feeders(docs)
    if len(into) != 1 or into[0]["m_OutputSlot"]["m_Node"]["m_Id"] != cf["m_ObjectId"] or into[0]["m_OutputSlot"]["m_SlotId"] != OUT:
        errs.append(f"{VERTEX_BLOCK} is not fed by {FUNCTION}.Out alone")
    return errs


def splice(docs):
    """SpindleGraph's docs with the grown sway wired in (the docs are changed in place and returned)."""
    graph, idx = find_graph(docs), index(docs)
    sway = find_cf(docs, SWAY)
    assert sway is not None, "SpindleGraph has no SpindleSway: run wire_spindle_sway.py first"
    phase, clock = find_property(docs, "Phase"), find_property(docs, "PrismClock")
    assert phase is not None and clock is not None, "SpindleGraph lost Phase / PrismClock"
    phase_pn = property_node_for(docs, phase["m_ObjectId"])
    donor_v1 = idx[phase_pn["m_Slots"][0]["m_Id"]]
    assert "Vector1MaterialSlot" in donor_v1["m_Type"]
    donor_v3 = next(idx[s["m_Id"]] for s in sway["m_Slots"] if idx[s["m_Id"]]["m_Id"] == SWAY_OUT)
    assert "Vector3MaterialSlot" in donor_v3["m_Type"]
    block, block_slot, into = _block_feeders(docs)
    assert len(into) == 1 and into[0]["m_OutputSlot"]["m_Node"]["m_Id"] == sway["m_ObjectId"] and into[0]["m_OutputSlot"]["m_SlotId"] == SWAY_OUT, \
        f"{VERTEX_BLOCK} is not fed by {SWAY}.Out: this splice does not know the graph it was given"
    host = next(idx[c["m_Id"]] for c in graph["m_CategoryData"]
                if any(ch["m_Id"] == phase["m_ObjectId"] for ch in idx[c["m_Id"]]["m_ChildObjectList"]))

    new_docs, prop_oid = [], {}
    for name, reference, default in NEW_PROPS:
        p = make_exposed_float(phase, name, reference, default)
        p["m_ObjectId"] = _oid(f"prop/{name}")
        p["m_Guid"] = {"m_GuidSerialized": _guid(name)}
        prop_oid[name] = p["m_ObjectId"]
        new_docs.append(p)
        graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
        host["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})

    def add(node, slots):
        new_docs.append(node)
        new_docs.extend(slots)
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})
        return node

    bx, by = -1900.0, 1900.0      # right of the SpindleSway chain, clear of the fragment graph
    uv_docs = load_docs(os.path.join(REPO, UV_DONOR))
    uv_idx = index(uv_docs)
    uv_donor = next(d for d in uv_docs if d.get("m_Type") == "UnityEditor.ShaderGraph.UVNode")

    def uv_node(channel, y):
        node = copy.deepcopy(uv_donor)
        node["m_ObjectId"] = _oid(f"uv{channel}")
        node["m_Group"] = {"m_Id": ""}
        node["m_OutputChannel"] = channel
        node["m_DrawState"]["m_Position"].update({"x": bx, "y": y, "width": 208.0, "height": 120.0})
        slot = copy.deepcopy(uv_idx[uv_donor["m_Slots"][0]["m_Id"]])
        slot["m_ObjectId"] = _oid(f"uv{channel}/slot")
        node["m_Slots"] = [{"m_Id": slot["m_ObjectId"]}]
        return add(node, [slot]), slot["m_Id"]

    def prop_node(oid, label, seed, y):
        node = copy.deepcopy(phase_pn)
        node["m_ObjectId"] = _oid(seed)
        node["m_Group"] = {"m_Id": ""}
        node["m_Property"] = {"m_Id": oid}
        node["m_DrawState"]["m_Position"].update({"x": bx, "y": y, "width": 200.0, "height": 36.0})
        slot = make_slot(donor_v1, 0, label, True)
        slot["m_ObjectId"] = _oid(seed + "/slot")
        slot["m_ShaderOutputName"] = "Out"
        node["m_Slots"] = [{"m_Id": slot["m_ObjectId"]}]
        return add(node, [slot])

    uv1, uv1_out = uv_node(1, by)
    uv2, uv2_out = uv_node(2, by + 140.0)
    feeds = [(uv1, uv1_out, 1), (uv2, uv2_out, 2)]
    own = {"PrismClock": clock["m_ObjectId"], "Phase": phase["m_ObjectId"], **prop_oid}
    for i, (slot_id, label) in enumerate(PROP_SLOTS):
        feeds.append((prop_node(own[label], label, f"v/{label}", by + 300.0 + i * 80.0), 0, slot_id))

    cf = copy.deepcopy(sway)
    cf["m_ObjectId"] = _oid("cf")
    cf["m_Name"] = f"{FUNCTION} (Custom Function)"
    cf["m_FunctionName"] = FUNCTION
    cf["m_FunctionSource"] = HLSL_GUID
    cf["m_SourceType"] = 0
    cf["m_FunctionBody"] = "Enter function body here..."
    cf["m_Group"] = {"m_Id": ""}
    cf["m_DrawState"]["m_Position"].update({"x": bx + 420.0, "y": by + 60.0, "width": 260.0, "height": 60.0 + 40.0 * len(SLOTS)})
    slots = []
    for slot_id, name, kind, is_output in SLOTS:
        s = make_slot_v3(donor_v3, slot_id, name, is_output) if kind == "v3" else make_slot(donor_v1, slot_id, name, is_output)
        s["m_ObjectId"] = _oid(f"cf/slot{slot_id}")
        slots.append(s)
    cf["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    add(cf, slots)

    # SpindleSway.Out -> SpindleGrownSway.PositionOS (the old edge into the block is RETARGETED, not duplicated)
    into[0]["m_InputSlot"] = {"m_Node": {"m_Id": cf["m_ObjectId"]}, "m_SlotId": 0}
    for node, out_slot, slot_id in feeds:
        graph["m_Edges"].append(edge(node["m_ObjectId"], out_slot, cf["m_ObjectId"], slot_id))
    graph["m_Edges"].append(edge(cf["m_ObjectId"], OUT, block["m_ObjectId"], block_slot))
    docs.extend(new_docs)
    return docs


def unsplice(docs):
    """SpindleGraph's docs with this splice taken OUT again, whatever version of it they carry: the function node, the
    nodes that feed only it (its UV and property nodes), the properties it added, and SpindleSway.Out back on the vertex
    block. What an upgrade of the function's signature runs before splicing the new one."""
    graph, idx = find_graph(docs), index(docs)
    cf = find_cf(docs, FUNCTION)
    if cf is None:
        return docs
    sway = find_cf(docs, SWAY)
    block, block_slot, _into = _block_feeders(docs)
    gone = {cf["m_ObjectId"]} | {s["m_Id"] for s in cf["m_Slots"]}
    for e in graph["m_Edges"]:
        src = e["m_OutputSlot"]["m_Node"]["m_Id"]
        if e["m_InputSlot"]["m_Node"]["m_Id"] == cf["m_ObjectId"] and src != sway["m_ObjectId"]:
            feeds_others = any(o["m_OutputSlot"]["m_Node"]["m_Id"] == src and o["m_InputSlot"]["m_Node"]["m_Id"] != cf["m_ObjectId"]
                               for o in graph["m_Edges"])
            assert not feeds_others, "a node feeding SpindleGrownSway also feeds something else: this splice did not make it"
            gone |= {src} | {s["m_Id"] for s in idx[src]["m_Slots"]}
    props = {d["m_ObjectId"] for d in docs if "ShaderProperty" in d.get("m_Type", "") and d.get("m_Name", "").startswith("Grown")}
    gone |= props
    graph["m_Edges"] = [e for e in graph["m_Edges"] if e["m_OutputSlot"]["m_Node"]["m_Id"] not in gone and e["m_InputSlot"]["m_Node"]["m_Id"] not in gone]
    graph["m_Edges"].append(edge(sway["m_ObjectId"], SWAY_OUT, block["m_ObjectId"], block_slot))
    graph["m_Nodes"] = [r for r in graph["m_Nodes"] if r["m_Id"] not in gone]
    graph["m_Properties"] = [r for r in graph["m_Properties"] if r["m_Id"] not in gone]
    for c in graph["m_CategoryData"]:
        idx[c["m_Id"]]["m_ChildObjectList"] = [ch for ch in idx[c["m_Id"]]["m_ChildObjectList"] if ch["m_Id"] not in gone]
    docs[:] = [d for d in docs if d.get("m_ObjectId") not in gone]
    return docs


def self_test():
    """The splice on a copy of the graph as it stands (or un-spliced), and four planted defects the validator must name."""
    fails = []
    docs = load_docs(os.path.join(REPO, GRAPH))
    if find_cf(docs, FUNCTION) is None:
        docs = splice(docs)
    # taking the splice out and putting it back gives the same graph, byte for byte (what an upgrade relies on)
    again = splice(unsplice(copy.deepcopy(docs)))
    if dump_docs(sorted(again, key=lambda d: d.get("m_ObjectId", ""))) != dump_docs(sorted(docs, key=lambda d: d.get("m_ObjectId", ""))):
        fails.append("unsplice then splice did not reproduce the graph")
    bare = unsplice(copy.deepcopy(docs))
    if find_cf(bare, FUNCTION) is not None or any(d.get("m_Name", "").startswith("Grown") for d in bare if "ShaderProperty" in d.get("m_Type", "")):
        fails.append("unsplice left part of the splice behind")
    try:
        validate_graph(bare, expect_wired=True)      # wire_spindle_sway's own contract: SpindleSway feeds the block again
    except AssertionError as e:
        fails.append(f"the un-spliced graph is not the SpindleSway graph: {e}")
    if validate(docs, True):
        fails.append("the shipped construction does not validate: " + validate(docs, True)[0])
    graph, idx = find_graph(docs), index(docs)
    cf = find_cf(docs, FUNCTION)

    def planted(label, mutate, needle):
        d = copy.deepcopy(docs)
        mutate(d, find_graph(d), index(d), find_cf(d, FUNCTION))
        try:
            got = validate(d, True)
        except AssertionError as e:      # the whole-graph rules (a doubled feeder, a dangling edge) assert
            got = [str(e)]
        if not any(needle in g for g in got):
            fails.append(f"planted '{label}' was not caught (got {got[:2]})")

    planted("the wave read from TEXCOORD0", lambda d, g, i, c: [n.update(m_OutputChannel=0) for n in d if "UVNode" in n.get("m_Type", "") and n.get("m_OutputChannel") == 1],
            "TEXCOORD1")
    planted("a non-zero default", lambda d, g, i, c: find_property(d, "GrownSway").update(m_Value=1.0), "default must be 0")
    planted("the block still fed by SpindleSway", lambda d, g, i, c: g["m_Edges"].append(edge(find_cf(d, SWAY)["m_ObjectId"], SWAY_OUT, _block_feeders(d)[0]["m_ObjectId"], _block_feeders(d)[1])),
            "feeders")
    planted("a slot dropped", lambda d, g, i, c: c["m_Slots"].pop(2), "slot")
    planted("the strength unfed", lambda d, g, i, c: g["m_Edges"].remove(next(e for e in g["m_Edges"] if e["m_InputSlot"]["m_Node"]["m_Id"] == c["m_ObjectId"] and e["m_InputSlot"]["m_SlotId"] == 5)),
            "unconnected")
    src = open(os.path.join(REPO, HLSL)).read()
    if "void SpindleGrownSway_float(" not in src:
        fails.append("SpindleGrownSway.hlsl lost its entry point: the node would not bind")
    meta = open(os.path.join(REPO, HLSL + ".meta")).read()
    if f"guid: {HLSL_GUID}" not in meta:
        fails.append("SpindleGrownSway.hlsl.meta does not carry the guid the node names")
    return fails


def main(argv):
    if "--self-test" in argv:
        fails = self_test()
        for f in fails:
            print("FAIL:", f)
        print("wire_spindle_grown_sway self-test:", "ok" if not fails else f"{len(fails)} problem(s)")
        return 1 if fails else 0
    full = os.path.join(REPO, GRAPH)
    docs = load_docs(full)
    if find_cf(docs, FUNCTION) is not None:
        errs = validate(docs, True)
        if not errs or "--check" in argv:
            for e in errs:
                print("FAIL:", e)
            print(f"  {os.path.basename(GRAPH)}: " + ("already wired" if not errs else "wired WRONG, or an older version: run without --check to re-wire"))
            return 1 if errs else 0
        print(f"  {os.path.basename(GRAPH)}: an older {FUNCTION} is wired; taking it out and wiring this one")
        docs = unsplice(docs)
    if "--check" in argv:
        print(f"  {os.path.basename(GRAPH)}: NOT wired", file=sys.stderr)
        return 1
    docs = splice(docs)
    errs = validate(docs, True)
    if errs:      # nothing was written
        for e in errs:
            print("FAIL:", e)
        return 1
    with open(full, "w", encoding="utf-8", newline="\n") as f:
        f.write(dump_docs(docs))
    again = load_docs(full)
    assert not validate(again, True), "the file as written does not validate"
    print(f"  {os.path.basename(GRAPH)}: wired {FUNCTION} after {SWAY}; now run wire_spindle_lod.py")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
