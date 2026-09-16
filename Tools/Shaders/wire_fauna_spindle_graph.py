#!/usr/bin/env python3
"""
Author FaunaSpindleGraph — SpindleGraph, plus the three things the CLAWFISH's one-off
shader did that a plant's spindle has no reason to do, minus the two colours nobody
should be hand-authoring.

Why a second graph rather than an edit
--------------------------------------
SpindleGraph is worn by FLORA and by fauna alike (SpindleMaterial alone is on twelve
prefabs — every flora branch, the shark, the brittlestar, the worm segments). A
creature breathing, flowing and rim-lighting is a statement about CREATURES; a fern
doing it is not. So this forks the graph and only the fauna materials move to it.
Flora keeps SpindleGraph byte-for-byte.

What it takes from CreatureTextureGraph
---------------------------------------
That graph (Assets/_Graphics/Materials/Lifeform_World_Shader Graphs/) was worn by the
Clawfish and by nothing else. Traced, most of it is dead — an unconnected Color1/Color2
pair, an unconnected gradient, an unconnected Smooth Wave subgraph, an unconnected
_Transparency_Color. What it actually rendered was three things:

  * BaseColor = Fresnel(power 3) * texture * remap(SineTime, -1..1 -> 0.5..1.2)
  * the alpha texture's UV scrolled by Time * _Direction   (authored (0, -0.01))
  * everything transparent, so you see into the body

The first two are why it read as alive. Both are now HLSL (FaunaSkin.hlsl), on the
platform's own clock rather than Unity's `Sine Time`, and both default to a provable
no-op (Tools/Shaders/verify_fauna_skin.py T2/T3).

The colours
-----------
_BrightColor / _DullColor are RE-POINTED at two UNEXPOSED globals,
_FaunaNeutralBright / _FaunaNeutralDull, published once from the live SO_ColorSet by
FaunaNeutralPalette. The shipped spindle materials carried a hand-authored
(0.370, 0.397, 0.956) / (0, 0.028, 1) — an eyeballed approximation of the palette's
neutral blue that nothing could keep in step with it. The palette's own answer is the
BLUE domain's SHIELDED pair: rim (1.113, 1.127, 1.260) ~ white over base (0, 0, 0.549)
= blue. Blue is the platform's neutral sentinel, and the SHIELDED tier is the row every
flora and fauna HEALTH PRISM already wears (Docs/PALETTE.md §2) — so a creature's soft
tissue and its conserved mass come out of one row instead of two.

Unexposed is what makes them globals (the same declaration _PrismClock already uses in
this graph), which matters because Spindle.cs mints EIGHT phase-variant materials per
base material at runtime: a per-material colour would have to be painted before the
first spindle starts or the variants keep a stale copy. A global has no such ordering.

Idempotent. Usage: python3 Tools/Shaders/wire_fauna_spindle_graph.py [--check]
"""

import hashlib
import json
import os
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wire_prism_spindle_death_clock as DC  # noqa: E402
import wire_spindle_sway as SW  # noqa: E402
from wire_prism_spindle_death_clock import (  # noqa: E402
    load_docs, dump_docs, find_graph, index, find_property, find_cf,
    make_slot, edge,
)
from wire_spindle_sway import acyclic, make_property_node, make_slot_v3  # noqa: E402


# ── every id this script mints is DETERMINISTIC ────────────────────────────────
# A generator with a --check that re-mints uuid4 ids re-writes the whole file on
# every run, so --check can only ever report "drifted" and stops being a gate
# (/asset-surgery §3: mint deterministically when a script OWNS an asset). The
# creation order below is fixed, so a counter is enough; validate() asserts global
# uniqueness, which is what catches a collision with a copied SpindleGraph id.
_SEQ = [0]


def new_oid():
    _SEQ[0] += 1
    return hashlib.md5(b"CosmicShore/FaunaSpindleGraph/oid/%d" % _SEQ[0]).hexdigest()


def new_guid():
    _SEQ[0] += 1
    return str(uuid.UUID(hashlib.md5(b"CosmicShore/FaunaSpindleGraph/guid/%d" % _SEQ[0]).hexdigest()))


# The cloned helpers mint through THEIR module's new_oid, so both have to be pointed
# at the deterministic one or half the file re-mints per run.
DC.new_oid = new_oid
SW.new_oid = new_oid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SOURCE = "Assets/_Graphics/Materials/Graphs/SpindleGraph.shadergraph"
TARGET = "Assets/_Graphics/Materials/Graphs/FaunaSpindleGraph.shadergraph"
DONOR = "Assets/_Graphics/Materials/Lifeform_World_Shader Graphs/CreatureTextureGraph.shadergraph"
HLSL_GUID = "0644441c7a1f4638ae79d91cf81ce39f"          # FaunaSkin.hlsl
GRAPH_GUID = "3f7c1a9e5b2d4c08a6e1d7b4c9f30215"          # pinned: re-minting orphans every .mat
SHADERGRAPH_IMPORTER = "625f186215c104763be7675aa2d941aa"

SHADE = "FaunaSkinShade"
FLOW = "FaunaSkinFlow"

# (slot id, display name, kind, is_output) — order IS the HLSL signature.
SHADE_SLOTS = [
    (0, "Base",        "v3", False),
    (1, "Rim",         "v3", False),
    (2, "Fresnel",     "v1", False),
    (3, "Clock",       "v1", False),
    (4, "RimStrength", "v1", False),
    (5, "Pulse",       "v2", False),
    (6, "Out",         "v3", True),
]
FLOW_SLOTS = [
    (0, "BaseOffset", "v2", False),
    (1, "Clock",      "v1", False),
    (2, "FlowSpeed",  "v2", False),
    (3, "Out",        "v2", True),
]

# name -> (reference, kind, default)
NEW_PROPS = {
    "RimStrength": ("_RimStrength", "v1", 0.0),
    "Pulse":       ("_Pulse",       "v2", {"x": 1.0, "y": 1.0, "z": 0.0, "w": 0.0}),
    "FlowSpeed":   ("_FlowSpeed",   "v2", {"x": 0.0, "y": 0.0, "z": 0.0, "w": 0.0}),
}

RENAMES = {                      # old property name -> (new name, new reference)
    "BrightColor": ("FaunaNeutralBright", "_FaunaNeutralBright"),
    "DullColor":   ("FaunaNeutralDull",   "_FaunaNeutralDull"),
}


# ── locating the two splice points STRUCTURALLY, never by hardcoded id ──────────
def find_node(docs, name, type_fragment):
    for d in docs:
        if d.get("m_Name") == name and type_fragment in d.get("m_Type", ""):
            return d
    return None


def sole_feeder_of(graph, node_oid, slot_id):
    hits = [e for e in graph["m_Edges"]
            if e["m_InputSlot"]["m_Node"]["m_Id"] == node_oid
            and e["m_InputSlot"]["m_SlotId"] == slot_id]
    assert len(hits) == 1, "expected exactly one feeder, found %d" % len(hits)
    return hits[0]


def locate_basecolor(docs, graph, idx):
    block = find_node(docs, "SurfaceDescription.BaseColor", "BlockNode")
    assert block is not None, "no SurfaceDescription.BaseColor block"
    slot = idx[block["m_Slots"][0]["m_Id"]]["m_Id"]
    return block, slot, sole_feeder_of(graph, block["m_ObjectId"], slot)


def locate_voronoi_uv(docs, graph, idx):
    """The TilingAndOffset whose Out feeds the Voronoi's UV — found by walking the
    edge, never by name, because two TilingAndOffsets would be indistinguishable."""
    vor = [d for d in docs if "VoronoiNode" in d.get("m_Type", "")]
    assert len(vor) == 1, "expected exactly one Voronoi node, found %d" % len(vor)
    uv = next(idx[s["m_Id"]] for s in vor[0]["m_Slots"]
              if idx[s["m_Id"]]["m_DisplayName"] == "UV")
    fed = sole_feeder_of(graph, vor[0]["m_ObjectId"], uv["m_Id"])
    tao = idx[fed["m_OutputSlot"]["m_Node"]["m_Id"]]
    assert "TilingAndOffsetNode" in tao["m_Type"], \
        "the Voronoi's UV is fed by a %s, not a TilingAndOffset" % tao["m_Type"]
    offset = next(idx[s["m_Id"]] for s in tao["m_Slots"]
                  if idx[s["m_Id"]]["m_DisplayName"] == "Offset")
    already = [e for e in graph["m_Edges"]
               if e["m_InputSlot"]["m_Node"]["m_Id"] == tao["m_ObjectId"]
               and e["m_InputSlot"]["m_SlotId"] == offset["m_Id"]]
    assert not already, "the Voronoi UV's Offset already has a feeder — re-read the graph"
    return tao, offset


# ── donors ─────────────────────────────────────────────────────────────────────
def donor_slots(docs, idx):
    v1 = v2 = v3 = None
    for d in docs:
        t = d.get("m_Type", "")
        if v1 is None and "Vector1MaterialSlot" in t: v1 = d
        if v2 is None and "Vector2MaterialSlot" in t: v2 = d
        if v3 is None and "Vector3MaterialSlot" in t: v3 = d
    assert v1 and v2 and v3, "missing a slot donor (v1=%s v2=%s v3=%s)" % (bool(v1), bool(v2), bool(v3))
    return {"v1": v1, "v2": v2, "v3": v3}


def make_slot_v2(donor, slot_id, name, is_output):
    s = json.loads(json.dumps(donor))
    s["m_ObjectId"] = new_oid()
    s["m_Id"] = slot_id
    s["m_DisplayName"] = name
    s["m_ShaderOutputName"] = name
    s["m_SlotType"] = 1 if is_output else 0
    s["m_StageCapability"] = 3
    s["m_Value"] = {"x": 0.0, "y": 0.0}
    s["m_DefaultValue"] = {"x": 0.0, "y": 0.0}
    s["m_Labels"] = []
    s["m_Type"] = "UnityEditor.ShaderGraph.Vector2MaterialSlot"
    return s


def build_slot(donors, kind, slot_id, name, is_output):
    if kind == "v1": return make_slot(donors["v1"], slot_id, name, is_output)
    if kind == "v2": return make_slot_v2(donors["v2"], slot_id, name, is_output)
    return make_slot_v3(donors["v3"], slot_id, name, is_output)


def make_cf(donor_cf, donors, fn, slot_spec, x, y):
    node = json.loads(json.dumps(donor_cf))
    node["m_ObjectId"] = new_oid()
    node["m_Name"] = "%s (Custom Function)" % fn
    node["m_FunctionName"] = fn
    node["m_FunctionSource"] = HLSL_GUID
    node["m_SourceType"] = 0
    node["m_FunctionBody"] = "Enter function body here..."
    node["m_Group"] = {"m_Id": ""}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 240.0, "height": 400.0})
    slots = [build_slot(donors, kind, sid, nm, out) for sid, nm, kind, out in slot_spec]
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def make_property(donor, name, reference, kind, default, exposed=True):
    p = json.loads(json.dumps(donor))
    p["m_ObjectId"] = new_oid()
    p["m_Guid"] = {"m_GuidSerialized": new_guid()}
    p["m_Name"] = name
    p["m_RefNameGeneratedByDisplayName"] = name
    p["m_DefaultReferenceName"] = reference
    p["m_OverrideReferenceName"] = ""
    p["m_GeneratePropertyBlock"] = exposed
    p["overrideHLSLDeclaration"] = False
    p["hlslDeclarationOverride"] = 0
    p["m_Hidden"] = False
    p["m_Value"] = default
    return p


def clone_subtree(node, idx, donors=None):
    """Clone a node and every slot it owns, re-minting every object id."""
    new_node = json.loads(json.dumps(node))
    new_node["m_ObjectId"] = new_oid()
    new_node["m_Group"] = {"m_Id": ""}
    slots = []
    refs = []
    for s in node["m_Slots"]:
        c = json.loads(json.dumps(idx[s["m_Id"]]))
        c["m_ObjectId"] = new_oid()
        slots.append(c)
        refs.append({"m_Id": c["m_ObjectId"]})
    new_node["m_Slots"] = refs
    return new_node, slots


# ── validation ─────────────────────────────────────────────────────────────────
def validate(docs, expect_wired):
    idx = index(docs)
    graph = find_graph(docs)

    ids = [d["m_ObjectId"] for d in docs if "m_ObjectId" in d]
    assert len(ids) == len(set(ids)), "duplicate m_ObjectId"

    for ref in graph["m_Nodes"]:
        assert ref["m_Id"] in idx, "m_Nodes references missing %s" % ref["m_Id"]
    for ref in graph["m_Properties"]:
        assert ref["m_Id"] in idx, "m_Properties references missing %s" % ref["m_Id"]
    for cat in graph["m_CategoryData"]:
        for child in idx[cat["m_Id"]]["m_ChildObjectList"]:
            assert child["m_Id"] in idx, "category child missing"

    refnames = [d.get("m_DefaultReferenceName") for d in docs
                if "ShaderProperty" in d.get("m_Type", "")]
    assert len(refnames) == len(set(refnames)), "duplicate property reference name: %s" % refnames

    slot_ids = {}
    for ref in graph["m_Nodes"]:
        node = idx[ref["m_Id"]]
        ints = set()
        for s in node.get("m_Slots", []):
            assert s["m_Id"] in idx, "node %s slot %s missing" % (ref["m_Id"], s["m_Id"])
            sd = idx[s["m_Id"]]
            assert sd["m_Id"] not in ints, "duplicate integer slot id on node %s" % ref["m_Id"]
            ints.add(sd["m_Id"])
        slot_ids[ref["m_Id"]] = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in node.get("m_Slots", [])}

    feeders = {}
    for e in graph["m_Edges"]:
        o, i = e["m_OutputSlot"], e["m_InputSlot"]
        for end, lbl in ((o, "output"), (i, "input")):
            nid = end["m_Node"]["m_Id"]
            assert nid in slot_ids, "edge %s node %s not in m_Nodes" % (lbl, nid)
            assert end["m_SlotId"] in slot_ids[nid], \
                "edge %s slot %s missing on %s" % (lbl, end["m_SlotId"], nid)
        assert slot_ids[o["m_Node"]["m_Id"]][o["m_SlotId"]]["m_SlotType"] == 1, "edge output is not an output"
        assert slot_ids[i["m_Node"]["m_Id"]][i["m_SlotId"]]["m_SlotType"] == 0, "edge input is not an input"
        key = (i["m_Node"]["m_Id"], i["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
    for key, count in feeders.items():
        assert count == 1, "input slot %s has %d feeders" % (key, count)

    assert acyclic(graph), "edge CYCLE — the graph would import magenta"

    for d in docs:
        if "PropertyNode" not in d.get("m_Type", ""):
            continue
        prop = idx.get(d.get("m_Property", {}).get("m_Id"))
        if prop is None or not d.get("m_Slots"):
            continue
        slot = idx[d["m_Slots"][0]["m_Id"]]
        if "Vector1ShaderProperty" in prop.get("m_Type", ""):
            assert "Vector1MaterialSlot" in slot["m_Type"], \
                "PropertyNode for %s has a %s slot" % (prop.get("m_Name"), slot["m_Type"])
        if "Vector2ShaderProperty" in prop.get("m_Type", ""):
            assert "Vector2MaterialSlot" in slot["m_Type"], \
                "PropertyNode for %s has a %s slot" % (prop.get("m_Name"), slot["m_Type"])

    if not expect_wired:
        return

    for old, (name, reference) in RENAMES.items():
        assert find_property(docs, old) is None, "%s survived the rename" % old
        p = find_property(docs, name)
        assert p is not None, "%s missing" % name
        assert p["m_DefaultReferenceName"] == reference
        assert p["m_GeneratePropertyBlock"] is False, \
            "%s must be UNEXPOSED so Shader.SetGlobalColor reaches it" % name

    for name, (reference, kind, default) in NEW_PROPS.items():
        p = find_property(docs, name)
        assert p is not None, "property %s missing" % name
        assert p["m_DefaultReferenceName"] == reference
        assert p["m_GeneratePropertyBlock"] is True, "%s must be exposed for a .mat to author it" % name
        assert p["overrideHLSLDeclaration"] is False, "%s must stay UnityPerMaterial (SRP batching)" % name
        assert p["m_Value"] == default, "%s default drifted from the no-op value" % name
        assert any(r["m_Id"] == p["m_ObjectId"] for r in graph["m_Properties"]), "%s not in m_Properties" % name

    for fn, spec in ((SHADE, SHADE_SLOTS), (FLOW, FLOW_SLOTS)):
        cf = find_cf(docs, fn)
        assert cf is not None, "%s custom function missing" % fn
        assert cf["m_FunctionSource"] == HLSL_GUID, "%s points at the wrong HLSL asset" % fn
        assert any(r["m_Id"] == cf["m_ObjectId"] for r in graph["m_Nodes"]), "%s not in m_Nodes" % fn
        cfs = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in cf["m_Slots"]}
        assert set(cfs) == {s[0] for s in spec}, "%s slot ids do not match the HLSL signature" % fn
        for sid, nm, kind, out in spec:
            assert cfs[sid]["m_DisplayName"] == nm, "%s slot %d name drifted" % (fn, sid)
            assert cfs[sid]["m_SlotType"] == (1 if out else 0), "%s slot %d direction wrong" % (fn, sid)
            want = {"v1": "Vector1MaterialSlot", "v2": "Vector2MaterialSlot",
                    "v3": "Vector3MaterialSlot"}[kind]
            assert want in cfs[sid]["m_Type"], \
                "%s slot %d (%s) must be %s, is %s" % (fn, sid, nm, want, cfs[sid]["m_Type"])
        fed = {(e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"])
               for e in graph["m_Edges"]}
        for sid, nm, _k, out in spec:
            if out:
                continue
            if fn == FLOW and nm == "BaseOffset":
                assert cfs[sid]["m_Value"] == {"x": 0.0, "y": 0.5}, \
                    "%s.BaseOffset must carry the TilingAndOffset's own authored offset" % fn
                continue
            assert (cf["m_ObjectId"], sid) in fed, "%s input '%s' is unconnected" % (fn, nm)

    # the shade CF owns BaseColor, the flow CF owns the Voronoi's UV offset
    block, slot, feed = locate_basecolor(docs, graph, idx)
    shade = find_cf(docs, SHADE)
    assert feed["m_OutputSlot"]["m_Node"]["m_Id"] == shade["m_ObjectId"], \
        "SurfaceDescription.BaseColor is not fed by %s" % SHADE
    assert feed["m_OutputSlot"]["m_SlotId"] == 6, "BaseColor fed by the wrong %s slot" % SHADE

    vor = next(d for d in docs if "VoronoiNode" in d.get("m_Type", ""))
    uv = next(idx[s["m_Id"]] for s in vor["m_Slots"] if idx[s["m_Id"]]["m_DisplayName"] == "UV")
    tao = idx[sole_feeder_of(graph, vor["m_ObjectId"], uv["m_Id"])["m_OutputSlot"]["m_Node"]["m_Id"]]
    off = next(idx[s["m_Id"]] for s in tao["m_Slots"] if idx[s["m_Id"]]["m_DisplayName"] == "Offset")
    flow = find_cf(docs, FLOW)
    assert sole_feeder_of(graph, tao["m_ObjectId"], off["m_Id"])["m_OutputSlot"]["m_Node"]["m_Id"] \
        == flow["m_ObjectId"], "the Voronoi UV offset is not fed by %s" % FLOW

    # SpindleSway must have survived the fork
    assert find_cf(docs, "SpindleSway") is not None, "SpindleSway did not survive the fork"


def build():
    _SEQ[0] = 0
    docs = load_docs(os.path.join(REPO, SOURCE))
    validate(docs, expect_wired=False)
    graph = find_graph(docs)
    idx = index(docs)
    donors = donor_slots(docs, idx)

    donor_cf = find_cf(docs, "PrismDeathClock")
    assert donor_cf is not None, "no Custom Function node to clone"
    clock = find_property(docs, "PrismClock")
    assert clock is not None, "SpindleGraph has no PrismClock property"
    phase_pn = next(d for d in docs if "PropertyNode" in d.get("m_Type", "")
                    and d.get("m_Property", {}).get("m_Id")
                    == find_property(docs, "Phase")["m_ObjectId"])
    donor_v1_slot = idx[phase_pn["m_Slots"][0]["m_Id"]]

    donor_docs = load_docs(os.path.join(REPO, DONOR))
    donor_idx = index(donor_docs)
    fresnel_src = next(d for d in donor_docs if "FresnelNode" in d.get("m_Type", ""))
    vec2_prop_src = next(d for d in donor_docs if "Vector2ShaderProperty" in d.get("m_Type", ""))
    vec2_pn_src = next(d for d in donor_docs if "PropertyNode" in d.get("m_Type", "")
                       and d.get("m_Property", {}).get("m_Id") == vec2_prop_src["m_ObjectId"])
    float_prop_src = next(d for d in docs if "Vector1ShaderProperty" in d.get("m_Type", ""))
    colour_prop_src = find_property(docs, "BrightColor")
    colour_pn_src = next(d for d in docs if "PropertyNode" in d.get("m_Type", "")
                         and d.get("m_Property", {}).get("m_Id") == colour_prop_src["m_ObjectId"])
    donor_colour_slot = idx[colour_pn_src["m_Slots"][0]["m_Id"]]

    # ── 1. re-point the two colours at unexposed globals ─────────────────────
    for old, (name, reference) in RENAMES.items():
        p = find_property(docs, old)
        assert p is not None, "SpindleGraph has no %s property" % old
        p["m_Name"] = name
        p["m_RefNameGeneratedByDisplayName"] = name
        p["m_DefaultReferenceName"] = reference
        p["m_OverrideReferenceName"] = ""
        p["m_GeneratePropertyBlock"] = False
        # the PropertyNode's slot keeps the OLD display name otherwise, so a human
        # reading the graph sees "DullColor" on a property that no longer exists
        for d in docs:
            if "PropertyNode" not in d.get("m_Type", ""):
                continue
            if d.get("m_Property", {}).get("m_Id") != p["m_ObjectId"]:
                continue
            for s in d.get("m_Slots", []):
                idx[s["m_Id"]]["m_DisplayName"] = name

    # ── 2. the three new dials ───────────────────────────────────────────────
    host = next(idx[c["m_Id"]] for c in graph["m_CategoryData"]
                if idx[c["m_Id"]]["m_ChildObjectList"])
    new_docs = []
    prop_oid = {}
    for name, (reference, kind, default) in NEW_PROPS.items():
        src = float_prop_src if kind == "v1" else vec2_prop_src
        p = make_property(src, name, reference, kind, default)
        prop_oid[name] = p["m_ObjectId"]
        new_docs.append(p)
        graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
        host["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})

    bx, by = -2600.0, 3200.0

    def property_node(oid, label, kind, x, y):
        if kind == "v2":
            node, slots = clone_subtree(vec2_pn_src, donor_idx)
            node["m_Property"] = {"m_Id": oid}
            node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 200.0, "height": 36.0})
            slots[0]["m_ShaderOutputName"] = "Out"
            slots[0]["m_DisplayName"] = label
            return node, slots
        donor = donor_colour_slot if kind == "col" else donor_v1_slot
        node, slots = make_property_node(colour_pn_src if kind == "col" else phase_pn,
                                         donor, oid, label, x, y)
        return node, slots

    def add(node, slots):
        new_docs.append(node)
        new_docs.extend(slots)
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})
        return node["m_ObjectId"]

    # ── 3. the shade chain ───────────────────────────────────────────────────
    fres_node, fres_slots = clone_subtree(fresnel_src, donor_idx)
    fres_node["m_DrawState"]["m_Position"].update({"x": bx, "y": by})
    fres_out = next(s for s in fres_slots if s["m_SlotType"] == 1)["m_Id"]
    fres_oid = add(fres_node, fres_slots)

    shade, shade_slots = make_cf(donor_cf, donors, SHADE, SHADE_SLOTS, bx + 460.0, by + 40.0)
    shade_oid = add(shade, shade_slots)

    rim_oid = add(*property_node(find_property(docs, "FaunaNeutralBright")["m_ObjectId"],
                                 "FaunaNeutralBright", "col", bx, by + 400.0))
    clk_a = add(*property_node(clock["m_ObjectId"], "PrismClock", "v1", bx, by + 480.0))
    rs_oid = add(*property_node(prop_oid["RimStrength"], "RimStrength", "v1", bx, by + 560.0))
    pl_oid = add(*property_node(prop_oid["Pulse"], "Pulse", "v2", bx, by + 640.0))

    block, block_slot, base_feed = locate_basecolor(docs, graph, idx)
    base_src_node = base_feed["m_OutputSlot"]["m_Node"]["m_Id"]
    base_src_slot = base_feed["m_OutputSlot"]["m_SlotId"]
    # retarget the existing Add -> BaseColor edge so the shade CF sits between them
    base_feed["m_OutputSlot"] = {"m_Node": {"m_Id": shade_oid}, "m_SlotId": 6}
    graph["m_Edges"].append(edge(base_src_node, base_src_slot, shade_oid, 0))
    graph["m_Edges"].append(edge(rim_oid, 0, shade_oid, 1))
    graph["m_Edges"].append(edge(fres_oid, fres_out, shade_oid, 2))
    graph["m_Edges"].append(edge(clk_a, 0, shade_oid, 3))
    graph["m_Edges"].append(edge(rs_oid, 0, shade_oid, 4))
    graph["m_Edges"].append(edge(pl_oid, 0, shade_oid, 5))

    # ── 4. the flow chain ────────────────────────────────────────────────────
    tao, tao_off = locate_voronoi_uv(docs, graph, idx)
    authored = dict(tao_off["m_Value"])

    flow, flow_slots = make_cf(donor_cf, donors, FLOW, FLOW_SLOTS, bx + 460.0, by + 760.0)
    by_id = {s["m_Id"]: s for s in flow_slots}
    by_id[0]["m_Value"] = {"x": authored.get("x", 0.0), "y": authored.get("y", 0.0)}
    flow_oid = add(flow, flow_slots)

    clk_b = add(*property_node(clock["m_ObjectId"], "PrismClock", "v1", bx, by + 800.0))
    fs_oid = add(*property_node(prop_oid["FlowSpeed"], "FlowSpeed", "v2", bx, by + 880.0))
    graph["m_Edges"].append(edge(clk_b, 0, flow_oid, 1))
    graph["m_Edges"].append(edge(fs_oid, 0, flow_oid, 2))
    graph["m_Edges"].append(edge(flow_oid, 3, tao["m_ObjectId"], tao_off["m_Id"]))

    docs.extend(new_docs)
    validate(docs, expect_wired=True)
    return docs


def main():
    check_only = "--check" in sys.argv
    full = os.path.join(REPO, TARGET)
    fresh = build()

    if os.path.exists(full):
        on_disk = load_docs(full)
        validate(on_disk, expect_wired=True)
        if dump_docs(on_disk) == dump_docs(fresh):
            print("  %s: up to date" % os.path.basename(TARGET))
            print("OK")
            return
        if check_only:
            print("  %s: DRIFTED from the generator" % os.path.basename(TARGET), file=sys.stderr)
            sys.exit(1)
    elif check_only:
        print("  %s: MISSING" % os.path.basename(TARGET), file=sys.stderr)
        sys.exit(1)

    with open(full, "w", encoding="utf-8") as fh:
        fh.write(dump_docs(fresh))
    meta = full + ".meta"
    if not os.path.exists(meta):
        with open(meta, "w", encoding="utf-8") as fh:
            fh.write("fileFormatVersion: 2\nguid: %s\nScriptedImporter:\n"
                     "  internalIDToNameTable: []\n  externalObjects: {}\n"
                     "  serializedVersion: 2\n  userData: \n  assetBundleName: \n"
                     "  assetBundleVariant: \n"
                     "  script: {fileID: 11500000, guid: %s, type: 3}\n"
                     % (GRAPH_GUID, SHADERGRAPH_IMPORTER))
    print("  %s: written (%d docs)" % (os.path.basename(TARGET), len(fresh)))
    print("OK")


if __name__ == "__main__":
    main()
