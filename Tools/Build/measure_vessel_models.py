#!/usr/bin/env python3
"""
Per-model survey of every vessel FBX: meshes, blend shapes AND THEIR MAGNITUDE,
bones, animation takes, and who references the file.

This is the Phase 0 gate of `Docs/VESSEL_CONSTRUCTION_FOLLOWUP.md`. It exists
because the finding that reshaped that plan is invisible to every label-based
check:

    A LABELLED SHAPE IS NOT A SHAPE.

`VesselAnimation` discovers element blend shapes BY NAME, and the in-editor morph
audit reports what it discovers - so a rig carrying four shapes named
charge/mass/space/time that move zero vertices reports a green audit while the
hull morphs by exactly nothing. Two of the three unreferenced rigs are in that
state. Every number below is measured out of the FBX; nothing is inferred from a
name.

Reference resolution follows the guid-ownership rule (`VESSEL_CONSTRUCTION.md` §2):
exactly one `.meta` OWNS a guid - the file whose own top-level `guid:` line carries
it - and every other hit is something REFERENCING it. `grep -rl ... | head -1`
picks by filename order and put two passes of Rhino jets on a placeholder hull.

Usage:
    python3 Tools/Build/measure_vessel_models.py            # full survey
    python3 Tools/Build/measure_vessel_models.py --json     # machine-readable
    python3 Tools/Build/measure_vessel_models.py --check    # diff vs the doc's table
"""

import argparse
import json
import math
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary as F

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VESSEL_MODELS = os.path.join(REPO, "Assets", "_Models", "Vessel Models")

ELEMENT_NAMES = ("charge", "mass", "space", "time")


# ------------------------------------------------------------------ fbx reading

def _name_of(node):
    """FBX object names are 'Name\\x00\\x01Class'. Return the Name half."""
    if len(node.props) < 2:
        return ""
    raw = node.props[1][1]
    if isinstance(raw, bytes):
        raw = raw.decode("utf-8", "replace")
    return raw.split("\x00")[0]


def _subclass_of(node):
    if len(node.props) < 3:
        return ""
    raw = node.props[2][1]
    if isinstance(raw, bytes):
        raw = raw.decode("utf-8", "replace")
    return raw


def _arr(node, child_name):
    c = node.first(child_name)
    if c is None or not c.props:
        return []
    v = c.props[0][1]
    return v if isinstance(v, list) else []


def _bounds(verts):
    """verts is a flat [x,y,z,...] list. Returns (min, max, extent) triples."""
    if not verts:
        return None
    xs = verts[0::3]
    ys = verts[1::3]
    zs = verts[2::3]
    lo = (min(xs), min(ys), min(zs))
    hi = (max(xs), max(ys), max(zs))
    return lo, hi, tuple(hi[i] - lo[i] for i in range(3))


def survey_fbx(path):
    """Everything measurable out of one FBX, with no interpretation."""
    nodes, version, _footer = F.read(path)
    objects = None
    for n in nodes:
        if n.name == "Objects":
            objects = n
            break
    if objects is None:
        return None

    meshes = []
    shapes = []
    bones = []
    takes = []
    skin_clusters = 0
    models = []

    for obj in objects.children:
        if obj.name == "Geometry":
            sub = _subclass_of(obj)
            if sub == "Mesh":
                verts = _arr(obj, "Vertices")
                b = _bounds(verts)
                meshes.append({
                    "name": _name_of(obj),
                    "verts": len(verts) // 3,
                    "bounds_min": b[0] if b else None,
                    "bounds_max": b[1] if b else None,
                    "extent": b[2] if b else None,
                })
            elif sub == "Shape":
                # A blend shape stores ONLY the vertices it moves: Indexes picks
                # them out of the base mesh, Vertices holds the deltas. So a shape
                # that moves nothing is not an empty node - it is a node with one
                # index and a zero delta, which is exactly what the two empty rigs
                # carry, and exactly what a name-based check cannot see.
                idx = _arr(obj, "Indexes")
                dv = _arr(obj, "Vertices")
                mags = [
                    math.sqrt(dv[i] ** 2 + dv[i + 1] ** 2 + dv[i + 2] ** 2)
                    for i in range(0, len(dv), 3)
                ]
                moved = sum(1 for m in mags if m > 1e-6)
                shapes.append({
                    "name": _name_of(obj),
                    "indexed_verts": len(idx),
                    "moved_verts": moved,
                    "max_delta": max(mags) if mags else 0.0,
                    "sum_delta": sum(mags),
                })
        elif obj.name == "NodeAttribute":
            if _subclass_of(obj) == "LimbNode":
                bones.append(_name_of(obj))
        elif obj.name == "AnimationStack":
            takes.append(_name_of(obj))
        elif obj.name == "Deformer":
            if _subclass_of(obj) == "Cluster":
                skin_clusters += 1
        elif obj.name == "Model":
            models.append((_name_of(obj), _subclass_of(obj)))

    return {
        "path": os.path.relpath(path, REPO),
        "version": version,
        "size": os.path.getsize(path),
        "meshes": meshes,
        "total_verts": sum(m["verts"] for m in meshes),
        "shapes": shapes,
        "bones": bones,
        "bone_count": len(bones),
        "skin_clusters": skin_clusters,
        "takes": takes,
        "take_count": len(takes),
        "models": models,
    }


# ------------------------------------------------------- guid ownership + refs

def guid_of(asset_path):
    """The guid this asset's OWN .meta declares. None if it has no meta."""
    meta = asset_path + ".meta"
    if not os.path.exists(meta):
        return None
    with open(meta, "r", encoding="utf-8", errors="replace") as fh:
        for line in fh:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    return None


def owner_of_guid(guid, search_root=None):
    """
    Which file OWNS this guid - the one whose own top-level `guid:` line carries it.

    `grep -rl "guid: $g" --include=*.meta | head -1` returns the first hit in
    FILENAME order, and a .meta legitimately contains OTHER files' guids (an FBX's
    externalObjects material remap points into another FBX). That false positive is
    what put two passes of Rhino jets on a placeholder hull a fifth the ship's
    height. `^guid:` anchors to the declaration; everything else is a reference.
    """
    search_root = search_root or os.path.join(REPO, "Assets")
    out = subprocess.run(
        ["grep", "-rl", "--include=*.meta", "-e", "^guid: " + guid, search_root],
        capture_output=True, text=True,
    ).stdout.strip()
    hits = [h for h in out.split("\n") if h]
    owners = [h[:-5] for h in hits]          # strip ".meta"
    return owners


def referrers_of(asset_path, search_root=None):
    """
    Every file that REFERENCES this asset's guid, excluding the asset's own .meta.
    Classified, because 'referenced by nothing' is the claim being tested and the
    KIND of referrer is what decides whether a model is live.
    """
    guid = guid_of(asset_path)
    if not guid:
        return {"guid": None, "refs": []}
    search_root = search_root or os.path.join(REPO, "Assets")
    own_meta = os.path.abspath(asset_path + ".meta")
    out = subprocess.run(
        ["grep", "-rl", guid, search_root], capture_output=True, text=True,
    ).stdout.strip()
    refs = []
    for hit in out.split("\n"):
        if not hit or os.path.abspath(hit) == own_meta:
            continue
        refs.append(os.path.relpath(hit, REPO))
    return {"guid": guid, "refs": sorted(refs)}


def classify_refs(refs):
    kinds = {"prefab": [], "scene": [], "controller": [], "meta": [], "asset": [], "other": []}
    for r in refs:
        low = r.lower()
        if low.endswith(".prefab"):
            kinds["prefab"].append(r)
        elif low.endswith(".unity"):
            kinds["scene"].append(r)
        elif low.endswith(".controller") or low.endswith(".overridecontroller"):
            kinds["controller"].append(r)
        elif low.endswith(".meta"):
            kinds["meta"].append(r)
        elif low.endswith(".asset"):
            kinds["asset"].append(r)
        else:
            kinds["other"].append(r)
    return kinds


# ------------------------------------------------------------------- reporting

def fmt_shape(s):
    verdict = "EMPTY" if s["moved_verts"] == 0 else "real"
    return "%-10s idx=%-6d moved=%-6d maxD=%.4f sumD=%.1f  %s" % (
        s["name"], s["indexed_verts"], s["moved_verts"],
        s["max_delta"], s["sum_delta"], verdict,
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", action="store_true", help="machine-readable dump")
    ap.add_argument("--only", help="substring filter on filename")
    ap.add_argument("--no-refs", action="store_true", help="skip the (slow) reference sweep")
    args = ap.parse_args()

    files = sorted(
        os.path.join(VESSEL_MODELS, f)
        for f in os.listdir(VESSEL_MODELS) if f.lower().endswith(".fbx")
    )
    # Placeholder/ subfolder
    ph = os.path.join(VESSEL_MODELS, "Placeholder")
    if os.path.isdir(ph):
        files += sorted(
            os.path.join(ph, f) for f in os.listdir(ph) if f.lower().endswith(".fbx")
        )
    if args.only:
        files = [f for f in files if args.only.lower() in os.path.basename(f).lower()]

    results = []
    for path in files:
        try:
            s = survey_fbx(path)
        except Exception as exc:                       # noqa: BLE001 - report, keep going
            results.append({"path": os.path.relpath(path, REPO), "error": str(exc)})
            continue
        if s is None:
            results.append({"path": os.path.relpath(path, REPO), "error": "no Objects node"})
            continue
        if not args.no_refs:
            rr = referrers_of(path)
            s["guid"] = rr["guid"]
            s["refs"] = rr["refs"]
            s["ref_kinds"] = classify_refs(rr["refs"])
            s["guid_owners"] = owner_of_guid(rr["guid"]) if rr["guid"] else []
        results.append(s)

    if args.json:
        print(json.dumps(results, indent=2))
        return 0

    for s in results:
        print("=" * 78)
        if "error" in s:
            print("%s  -- ERROR: %s" % (s["path"], s["error"]))
            continue
        print(s["path"])
        print("  fbx %d   %.1f MB" % (s["version"], s["size"] / 1e6))
        print("  meshes %d (%d verts total)  bones %d  skin-clusters %d  takes %d"
              % (len(s["meshes"]), s["total_verts"], s["bone_count"],
                 s["skin_clusters"], s["take_count"]))
        for m in s["meshes"][:20]:
            ext = m["extent"]
            print("    mesh %-28s %6d verts  extent (%.3f, %.3f, %.3f)"
                  % (m["name"][:28], m["verts"], ext[0], ext[1], ext[2]) if ext
                  else "    mesh %-28s %6d verts" % (m["name"][:28], m["verts"]))
        if len(s["meshes"]) > 20:
            print("    ... %d more meshes" % (len(s["meshes"]) - 20))
        if s["shapes"]:
            print("  blend shapes (%d):" % len(s["shapes"]))
            for sh in s["shapes"]:
                print("    " + fmt_shape(sh))
            elem = [sh for sh in s["shapes"] if sh["name"].lower() in ELEMENT_NAMES]
            live = [sh for sh in elem if sh["moved_verts"] > 0]
            print("    -> element shapes: %d labelled, %d that MOVE ANYTHING"
                  % (len(elem), len(live)))
        else:
            print("  blend shapes: none")
        if s["bones"]:
            print("  bones: " + ", ".join(s["bones"][:12])
                  + (" ... (+%d)" % (len(s["bones"]) - 12) if len(s["bones"]) > 12 else ""))
        if "refs" in s:
            k = s["ref_kinds"]
            print("  guid %s" % s.get("guid"))
            if len(s.get("guid_owners", [])) != 1:
                print("    !! guid ownership is ambiguous: %s" % s.get("guid_owners"))
            print("  referenced by: %d file(s)  [prefab %d, scene %d, controller %d, asset %d, meta %d, other %d]"
                  % (len(s["refs"]), len(k["prefab"]), len(k["scene"]), len(k["controller"]),
                     len(k["asset"]), len(k["meta"]), len(k["other"])))
            for kind in ("prefab", "scene", "controller", "asset", "meta", "other"):
                for r in k[kind][:8]:
                    print("      %-11s %s" % (kind, r))
                if len(k[kind]) > 8:
                    print("      %-11s ... +%d more" % (kind, len(k[kind]) - 8))
    return 0


if __name__ == "__main__":
    sys.exit(main())
