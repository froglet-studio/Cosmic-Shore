#!/usr/bin/env python3
"""
Wire GPU-clock spindle evaporate/condense into SpindleGraph and AnimatedSpindleGraph.

Docs/PRISM_ANIMATION.md §5 C11. A withering creature leaves its body prisms standing
as a skeleton (LightFauna.LeaveSkeleton before the wither), so the spindles ARE the
wither visual. The CPU path was Spindle.SetFadeValue / EvaporateCoroutine /
CondenseCoroutine stepping _DeathAnimation every frame — and that MPB excluded the
renderer from the SRP Batcher for the fade's ~1s.

This splice inserts PrismDeathClock between the _DeathAnimation property and every
consumer of that property. State is computed off _PrismClock + per-spindle stamps
(_DeathStartTime / _DeathDuration / _DeathDirection). Ordering (starvation
extremity-first, joust heart-outward) is a StartTime offset stamped ONCE at death,
never a per-frame cascade.

Prompt 13 named ONLY these two graphs. Do not splice WorldSpaceDesign*,
CreatureTextureGraph, DynamicFresnelGraph, InverseDynamicFresnelGraph, or
TimeCrystalGraph even though they also declare _DeathAnimation.

What it adds to each graph:

  properties:
      PrismClock          unexposed global (m_GeneratePropertyBlock False)
      _DeathStartTime     Hybrid Per Instance
      _DeathDuration      Hybrid Per Instance; 0 = unstamped = LegacyState 0 (visible)
      _DeathDirection     Hybrid Per Instance; +1 evaporate 0→1, −1 condense 1→0

  nodes:
      Property x4         -> PrismClock + the three Death* stamps
      PrismDeathClock     Custom Function (HLSL GUID e3f9a1c27b8d4e05b6a4c9d1f0527a83)

  edges:
      BEFORE: Property(_DeathAnimation) -> <consumers>
      AFTER:  PrismDeathClock.State     -> <same consumers>
              _PrismClock               -> PrismDeathClock.Clock
              _DeathStartTime           -> PrismDeathClock.StartTime
              _DeathDuration            -> PrismDeathClock.Duration
              _DeathDirection           -> PrismDeathClock.Direction
              LegacyState LEFT UNCONNECTED (slot default 0)

Clock CF slot ids MUST match PrismDeathClock_float parameter order.

The CF donor is BlockGraph's PrismSuctionClock — these spindle graphs have no
Custom Function nodes of their own. Cross-file clone is safe: m_Group is empty.

Idempotent: re-running after a successful pass prints "already wired" and exits 0.

Usage:  python3 Tools/Shaders/wire_prism_spindle_death_clock.py [--check]
        --check validates without writing (exit 1 if not wired).
"""

import json
import os
import shutil
import subprocess
import sys
import tempfile
import uuid

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPHS = [
    "Assets/_Graphics/Materials/Graphs/SpindleGraph.shadergraph",
    "Assets/_Graphics/Materials/Graphs/AnimatedSpindleGraph.shadergraph",
]
BLOCKGRAPH = "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph"
HLSL_PATH = "Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl"
HLSL_GUID = "e3f9a1c27b8d4e05b6a4c9d1f0527a83"
CLOCK_FUNCTION = "PrismDeathClock"
DONOR_CF = "PrismSuctionClock"
CLOCK_PROP_NAME = "PrismClock"
DEATH_PROP_NAME = "DeathAnimation"
DEATH_PROP_REF = "_DeathAnimation"

# basename -> expected retarget count of DeathAnimation PropertyNode outgoing edges
EXPECTED_REDIRECTS = {
    "SpindleGraph.shadergraph": 1,
    "AnimatedSpindleGraph.shadergraph": 4,
}

DEATH_PROPS = [
    ("DeathStartTime", "_DeathStartTime"),
    ("DeathDuration", "_DeathDuration"),
    ("DeathDirection", "_DeathDirection"),
]

CLOCK_SLOTS = [
    (0, "Clock", "Vector1", False),
    (1, "StartTime", "Vector1", False),
    (2, "Duration", "Vector1", False),
    (3, "Direction", "Vector1", False),
    (4, "LegacyState", "Vector1", False),  # UNCONNECTED; default 0 = visible
    (5, "State", "Vector1", True),
]
CLOCK_UNCONNECTED = {4}


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
        if "ShaderProperty" not in d.get("m_Type", ""):
            continue
        if d.get("m_Name") == name or d.get("m_DefaultReferenceName") == name:
            return d
    return None


def resolve_property(docs, name, reference):
    return find_property(docs, name) or find_property(docs, reference)


def find_cf(docs, fn):
    for d in docs:
        if d.get("m_FunctionName") == fn:
            return d
    return None


def find_property_node_for(docs, prop_oid):
    for d in docs:
        if "PropertyNode" not in d.get("m_Type", ""):
            continue
        if d.get("m_Property", {}).get("m_Id") == prop_oid:
            return d
    return None


def edge_sources(graph):
    return {
        (e["m_InputSlot"]["m_Node"]["m_Id"], e["m_InputSlot"]["m_SlotId"]):
            (e["m_OutputSlot"]["m_Node"]["m_Id"], e["m_OutputSlot"]["m_SlotId"])
        for e in graph["m_Edges"]
    }


def make_per_instance_property(donor, name, reference, default_value):
    p = json.loads(json.dumps(donor))
    p["m_ObjectId"] = new_oid()
    p["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
    p["m_Name"] = name
    p["m_RefNameGeneratedByDisplayName"] = name
    p["m_DefaultReferenceName"] = reference
    p["m_OverrideReferenceName"] = ""
    p["m_GeneratePropertyBlock"] = True
    p["overrideHLSLDeclaration"] = True
    p["hlslDeclarationOverride"] = 3
    p["m_Hidden"] = False
    p["m_Value"] = default_value
    return p


def make_unexposed_clock(donor):
    p = json.loads(json.dumps(donor))
    p["m_ObjectId"] = new_oid()
    p["m_Guid"] = {"m_GuidSerialized": str(uuid.uuid4())}
    p["m_Name"] = CLOCK_PROP_NAME
    p["m_RefNameGeneratedByDisplayName"] = CLOCK_PROP_NAME
    p["m_DefaultReferenceName"] = "_PrismClock"
    p["m_OverrideReferenceName"] = ""
    p["m_GeneratePropertyBlock"] = False
    p["overrideHLSLDeclaration"] = False
    p["hlslDeclarationOverride"] = 0
    p["m_Hidden"] = False
    p["m_Value"] = 0.0
    return p


def make_slot(donor, slot_id, display_name, is_output):
    s = json.loads(json.dumps(donor))
    s["m_ObjectId"] = new_oid()
    s["m_Id"] = slot_id
    s["m_DisplayName"] = display_name
    s["m_ShaderOutputName"] = display_name
    s["m_SlotType"] = 1 if is_output else 0
    s["m_StageCapability"] = 3
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


def make_custom_function_node(donor_cf, donor_slot_v1, function_name, slots_spec, x, y, height):
    node = json.loads(json.dumps(donor_cf))
    node["m_ObjectId"] = new_oid()
    node["m_Name"] = f"{function_name} (Custom Function)"
    node["m_FunctionName"] = function_name
    node["m_FunctionSource"] = HLSL_GUID
    node["m_SourceType"] = 0
    node["m_FunctionBody"] = "Enter function body here..."
    node["m_Group"] = {"m_Id": ""}
    node["m_DrawState"]["m_Position"].update({"x": x, "y": y, "width": 232.0, "height": height})
    slots = []
    for slot_id, name, _kind, is_output in slots_spec:
        slots.append(make_slot(donor_slot_v1, slot_id, name, is_output))
    node["m_Slots"] = [{"m_Id": s["m_ObjectId"]} for s in slots]
    return node, slots


def edge(out_node, out_slot, in_node, in_slot):
    return {
        "m_OutputSlot": {"m_Node": {"m_Id": out_node}, "m_SlotId": out_slot},
        "m_InputSlot": {"m_Node": {"m_Id": in_node}, "m_SlotId": in_slot},
    }


def validate(docs, expect_wired, basename):
    idx = index(docs)
    graph = find_graph(docs)

    ids = [d["m_ObjectId"] for d in docs if "m_ObjectId" in d]
    assert len(ids) == len(set(ids)), "duplicate m_ObjectId"

    for ref in graph["m_Nodes"]:
        assert ref["m_Id"] in idx, f"m_Nodes references missing {ref['m_Id']}"
    for ref in graph["m_Properties"]:
        assert ref["m_Id"] in idx, f"m_Properties references missing {ref['m_Id']}"
    for cat in graph["m_CategoryData"]:
        for child in idx[cat["m_Id"]]["m_ChildObjectList"]:
            assert child["m_Id"] in idx, "category child missing"

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
        for end, lbl in ((o, "output"), (i, "input")):
            nid = end["m_Node"]["m_Id"]
            assert nid in slot_ids, f"edge {lbl} node {nid} not registered in m_Nodes"
            assert end["m_SlotId"] in slot_ids[nid], f"edge {lbl} slot {end['m_SlotId']} missing on {nid}"
        assert slot_ids[o["m_Node"]["m_Id"]][o["m_SlotId"]]["m_SlotType"] == 1, "edge output is not an output slot"
        assert slot_ids[i["m_Node"]["m_Id"]][i["m_SlotId"]]["m_SlotType"] == 0, "edge input is not an input slot"
        key = (i["m_Node"]["m_Id"], i["m_SlotId"])
        feeders[key] = feeders.get(key, 0) + 1
    for key, count in feeders.items():
        assert count == 1, f"input slot {key} has {count} feeders (must be exactly 1)"

    clock = find_cf(docs, CLOCK_FUNCTION)
    death = resolve_property(docs, DEATH_PROP_NAME, DEATH_PROP_REF)
    assert death is not None, f"{DEATH_PROP_NAME} property missing — this is not a spindle graph"

    if not expect_wired:
        return

    clock_prop = find_property(docs, CLOCK_PROP_NAME)
    assert clock_prop is not None, f"{CLOCK_PROP_NAME} property missing"
    assert clock_prop["m_DefaultReferenceName"] == "_PrismClock"
    assert clock_prop["m_GeneratePropertyBlock"] is False, \
        "PrismClock must be an UNEXPOSED global (m_GeneratePropertyBlock False)"
    assert clock_prop["overrideHLSLDeclaration"] is False
    assert clock_prop["hlslDeclarationOverride"] == 0
    assert any(r["m_Id"] == clock_prop["m_ObjectId"] for r in graph["m_Properties"]), \
        "PrismClock not in m_Properties"

    for name, reference in DEATH_PROPS:
        p = resolve_property(docs, name, reference)
        assert p is not None, f"property {name} ({reference}) missing"
        assert p["m_DefaultReferenceName"] == reference, f"{name} reference name wrong"
        assert p["m_GeneratePropertyBlock"] is True, f"{name} must be EXPOSED"
        assert p["overrideHLSLDeclaration"] is True, f"{name} must override the HLSL declaration"
        assert p["hlslDeclarationOverride"] == 3, \
            f"{name} must be Hybrid Per Instance (3)"
        assert any(r["m_Id"] == p["m_ObjectId"] for r in graph["m_Properties"]), \
            f"{name} not in m_Properties"

    duration = resolve_property(docs, "DeathDuration", "_DeathDuration")
    assert duration["m_Value"] == 0.0, \
        "_DeathDuration default must be 0 so unstamped spindles stay at LegacyState"

    assert clock is not None, f"{CLOCK_FUNCTION} custom function node missing"
    assert any(r["m_Id"] == clock["m_ObjectId"] for r in graph["m_Nodes"]), \
        f"{CLOCK_FUNCTION} not registered in m_Nodes"
    assert clock["m_FunctionSource"] == HLSL_GUID, f"{CLOCK_FUNCTION} points at the wrong HLSL asset"

    clock_slots = {idx[s["m_Id"]]["m_Id"]: idx[s["m_Id"]] for s in clock["m_Slots"]}
    assert set(clock_slots) == {s[0] for s in CLOCK_SLOTS}, \
        f"{CLOCK_FUNCTION} slot ids do not match the HLSL signature"
    for slot_id, name, _kind, is_output in CLOCK_SLOTS:
        assert clock_slots[slot_id]["m_DisplayName"] == name, f"clock slot {slot_id} name drifted"
        assert clock_slots[slot_id]["m_SlotType"] == (1 if is_output else 0), \
            f"clock slot {slot_id} direction wrong"

    sources = edge_sources(graph)
    fed = set(sources)
    for slot_id, name, _kind, is_output in CLOCK_SLOTS:
        if is_output or slot_id in CLOCK_UNCONNECTED:
            continue
        assert (clock["m_ObjectId"], slot_id) in fed, \
            f"{CLOCK_FUNCTION} input '{name}' is unconnected"
    assert (clock["m_ObjectId"], 4) not in fed, \
        f"{CLOCK_FUNCTION}.LegacyState is connected — leave it at default 0"

    expected = EXPECTED_REDIRECTS[basename]
    from_state = sum(
        1 for e in graph["m_Edges"]
        if e["m_OutputSlot"]["m_Node"]["m_Id"] == clock["m_ObjectId"]
        and e["m_OutputSlot"]["m_SlotId"] == 5
    )
    assert from_state == expected, \
        f"{basename}: expected {expected} consumers of {CLOCK_FUNCTION}.State, found {from_state}"

    death_pn = find_property_node_for(docs, death["m_ObjectId"])
    if death_pn is not None:
        leftover = sum(
            1 for e in graph["m_Edges"]
            if e["m_OutputSlot"]["m_Node"]["m_Id"] == death_pn["m_ObjectId"]
        )
        assert leftover == 0, \
            f"{DEATH_PROP_NAME} PropertyNode still feeds {leftover} consumer(s) — CPU writing it would bypass the clock"


def already_wired(docs):
    return find_cf(docs, CLOCK_FUNCTION) is not None


def donor_cf_from_blockgraph():
    bg = load_docs(os.path.join(REPO, BLOCKGRAPH))
    cf = find_cf(bg, DONOR_CF)
    assert cf is not None, f"BlockGraph is missing {DONOR_CF} — cannot clone a Custom Function node"
    return cf


def wire(path):
    full = os.path.join(REPO, path)
    basename = os.path.basename(path)
    docs = load_docs(full)
    validate(docs, expect_wired=False, basename=basename)

    if already_wired(docs):
        validate(docs, expect_wired=True, basename=basename)
        print(f"  {basename}: already wired")
        return False

    graph = find_graph(docs)
    idx = index(docs)

    death = resolve_property(docs, DEATH_PROP_NAME, DEATH_PROP_REF)
    assert death is not None, f"{basename} has no {DEATH_PROP_NAME} property"
    death_pn = find_property_node_for(docs, death["m_ObjectId"])
    assert death_pn is not None, f"{basename} has no {DEATH_PROP_NAME} PropertyNode"
    death_slot = idx[death_pn["m_Slots"][0]["m_Id"]]
    assert "Vector1MaterialSlot" in death_slot["m_Type"]

    donor_cf = donor_cf_from_blockgraph()

    host = next(idx[c["m_Id"]] for c in graph["m_CategoryData"]
                if any(ch["m_Id"] == death["m_ObjectId"]
                       for ch in idx[c["m_Id"]]["m_ChildObjectList"]))

    new_docs = []
    added_props = 0

    clock_prop = find_property(docs, CLOCK_PROP_NAME)
    if clock_prop is None:
        clock_prop = make_unexposed_clock(death)
        new_docs.append(clock_prop)
        graph["m_Properties"].append({"m_Id": clock_prop["m_ObjectId"]})
        host["m_ChildObjectList"].append({"m_Id": clock_prop["m_ObjectId"]})
        added_props += 1

    defaults = {"DeathStartTime": 0.0, "DeathDuration": 0.0, "DeathDirection": 1.0}
    prop_oids = {}
    for name, reference in DEATH_PROPS:
        existing = resolve_property(docs, name, reference)
        if existing:
            prop_oids[name] = existing["m_ObjectId"]
            continue
        p = make_per_instance_property(death, name, reference, defaults[name])
        prop_oids[name] = p["m_ObjectId"]
        new_docs.append(p)
        graph["m_Properties"].append({"m_Id": p["m_ObjectId"]})
        host["m_ChildObjectList"].append({"m_Id": p["m_ObjectId"]})
        added_props += 1

    death_pos = death_pn["m_DrawState"]["m_Position"]
    base_x = death_pos["x"]
    base_y = death_pos["y"]

    node_oids = {}
    for i, (name, _ref) in enumerate(DEATH_PROPS):
        node, slots = make_property_node(death_pn, death_slot, prop_oids[name],
                                         name, base_x, base_y + (i + 1) * 80.0)
        node_oids[name] = node["m_ObjectId"]
        new_docs.append(node)
        new_docs.extend(slots)
        graph["m_Nodes"].append({"m_Id": node["m_ObjectId"]})

    clock_node, clock_slots = make_property_node(
        death_pn, death_slot, clock_prop["m_ObjectId"], CLOCK_PROP_NAME,
        base_x, base_y - 80.0)
    new_docs.append(clock_node)
    new_docs.extend(clock_slots)
    graph["m_Nodes"].append({"m_Id": clock_node["m_ObjectId"]})

    clock_cf, clock_cf_slots = make_custom_function_node(
        donor_cf, death_slot, CLOCK_FUNCTION, CLOCK_SLOTS,
        base_x + 350.0, base_y, 360.0)
    new_docs.append(clock_cf)
    new_docs.extend(clock_cf_slots)
    graph["m_Nodes"].append({"m_Id": clock_cf["m_ObjectId"]})

    expected = EXPECTED_REDIRECTS[basename]
    retargeted = 0
    for e in graph["m_Edges"]:
        o = e["m_OutputSlot"]
        if o["m_Node"]["m_Id"] == death_pn["m_ObjectId"]:
            o["m_Node"]["m_Id"] = clock_cf["m_ObjectId"]
            o["m_SlotId"] = 5
            retargeted += 1
    assert retargeted == expected, \
        f"{basename}: expected {expected} DeathAnimation outgoing edges, retargeted {retargeted}"

    graph["m_Edges"].extend([
        edge(clock_node["m_ObjectId"], 0, clock_cf["m_ObjectId"], 0),
        edge(node_oids["DeathStartTime"], 0, clock_cf["m_ObjectId"], 1),
        edge(node_oids["DeathDuration"], 0, clock_cf["m_ObjectId"], 2),
        edge(node_oids["DeathDirection"], 0, clock_cf["m_ObjectId"], 3),
    ])

    docs.extend(new_docs)
    validate(docs, expect_wired=True, basename=basename)

    open(full, "w", encoding="utf-8").write(dump_docs(docs))
    print(f"  {basename}: wired "
          f"(+{added_props} properties, +{len(new_docs)} objects, {retargeted} edges retargeted)")
    return True


def check(path):
    basename = os.path.basename(path)
    docs = load_docs(os.path.join(REPO, path))
    validate(docs, expect_wired=False, basename=basename)
    if not already_wired(docs):
        print(f"  {basename}: NOT wired")
        return False
    validate(docs, expect_wired=True, basename=basename)
    print(f"  {basename}: wired ✅")
    return True


def check_hlsl():
    text = open(os.path.join(REPO, HLSL_PATH), encoding="utf-8").read()
    assert "void PrismDeathClock_float(" in text, \
        "PrismDeathClock_float missing from PrismClockAnimation.hlsl"
    assert "if (Duration <= 0.0)" in text
    assert "State = Direction < 0.0 ? 1.0 - p : p;" in text
    print("  PrismClockAnimation.hlsl: PrismDeathClock_float present ✅")
    return True


# C++ transcription of PrismDeathClock_float for clang --check (asset-surgery 4.5c).
# If clang++ is missing, skip compile — graph validation still gates the splice.
DEATH_CLOCK_CPP = r'''
#include <algorithm>
#include <cmath>
#include <cstdio>
static float saturate(float x) { return std::max(0.f, std::min(1.f, x)); }
static void PrismDeathClock_float(float Clock, float StartTime, float Duration, float Direction,
    float LegacyState, float* State)
{
    if (Duration <= 0.0f) { *State = LegacyState; return; }
    float t = std::max(Clock - StartTime, 0.0f);
    float p = saturate(t / Duration);
    *State = Direction < 0.0f ? 1.0f - p : p;
}
int main() {
    float s;
    PrismDeathClock_float(10.f, 0.f, 0.f, 1.f, 0.f, &s);
    if (s != 0.f) { std::fprintf(stderr, "Duration 0 did not return LegacyState 0\n"); return 1; }
    PrismDeathClock_float(10.f, 0.f, 0.f, 1.f, 0.42f, &s);
    if (std::fabs(s - 0.42f) > 1e-6f) { std::fprintf(stderr, "Duration 0 identity failed\n"); return 1; }
    PrismDeathClock_float(0.5f, 0.f, 1.f, 1.f, 0.f, &s);
    if (std::fabs(s - 0.5f) > 1e-6f) { std::fprintf(stderr, "evaporate mid failed %f\n", s); return 1; }
    PrismDeathClock_float(1.f, 0.f, 1.f, 1.f, 0.f, &s);
    if (std::fabs(s - 1.f) > 1e-6f) { std::fprintf(stderr, "evaporate end failed\n"); return 1; }
    PrismDeathClock_float(0.25f, 0.f, 1.f, -1.f, 0.f, &s);
    if (std::fabs(s - 0.75f) > 1e-6f) { std::fprintf(stderr, "condense mid failed %f\n", s); return 1; }
    PrismDeathClock_float(0.f, 5.f, 1.f, 1.f, 0.f, &s);
    if (s != 0.f) { std::fprintf(stderr, "future StartTime evaporate not 0\n"); return 1; }
    PrismDeathClock_float(0.f, 5.f, 1.f, -1.f, 0.f, &s);
    if (s != 1.f) { std::fprintf(stderr, "future StartTime condense not 1\n"); return 1; }
    return 0;
}
'''


def clang_prove():
    clang = shutil.which("clang++")
    if not clang:
        print("  clang++ not found — skipping PrismDeathClock numeric prove (graph check still required)")
        return True
    with tempfile.TemporaryDirectory() as td:
        src = os.path.join(td, "death_clock.cpp")
        bin_path = os.path.join(td, "death_clock")
        open(src, "w", encoding="utf-8").write(DEATH_CLOCK_CPP)
        build = subprocess.run(
            [clang, "-std=c++17", "-O2", "-Wall", "-Werror", src, "-o", bin_path],
            capture_output=True, text=True)
        if build.returncode != 0:
            print(build.stderr, file=sys.stderr)
            print("  clang++ compile of PrismDeathClock_float transcription FAILED")
            return False
        run = subprocess.run([bin_path], capture_output=True, text=True)
        if run.returncode != 0:
            print(run.stderr, file=sys.stderr)
            print("  PrismDeathClock_float numeric prove FAILED")
            return False
    print("  PrismDeathClock_float numeric prove (clang++) ✅")
    return True


def main():
    check_only = "--check" in sys.argv
    print(f"{'Checking' if check_only else 'Wiring'} spindle death clock "
          f"({CLOCK_FUNCTION}) into {len(GRAPHS)} graphs:")
    ok = True
    if check_only:
        ok &= check_hlsl()
        ok &= clang_prove()
    for path in GRAPHS:
        if check_only:
            ok &= check(path)
        else:
            wire(path)
    if check_only and not ok:
        print("NOT fully wired — run without --check.")
        return 1
    print("done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
