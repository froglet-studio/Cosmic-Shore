#!/usr/bin/env python3
"""
fbx_probe.py - everything a vessel-model session needs to know about an FBX, measured.

Reads a binary FBX with the repo's own codec (Tools/Build/fbx_binary.py) and prints, with no
interpretation: the global settings (unit scale, axes), the model hierarchy with every node's
authored rest TRS, which mesh node each geometry hangs off, the armature's bones and how many
vertices each one skins, every blend shape with how far it REALLY moves the hull (as a fraction
of the mesh diagonal - the same quantity the editor's morph audit reads, see
Docs/VESSEL_CONSTRUCTION.md 4.5 for why the two differ by a constant), every material and how
many polygons wear it, and every animation take with the nodes it actually moves and by how much.

A "labelled" shape is not a shape and a "present" take is not an animation: both of the
Rhino/Urchin rigs carry four element shapes that move ONE vertex by ZERO, and plenty of exports
carry takes whose every curve is constant. This probe measures, so neither trap survives it.

    python3 Tools/Build/vessel_model/fbx_probe.py "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx"
    python3 Tools/Build/vessel_model/fbx_probe.py <file.fbx> --json      # machine-readable
    python3 Tools/Build/vessel_model/fbx_probe.py <file.fbx> --takes     # per-take curve detail

Reader only: writes nothing. Used by the /vessel-model skill's dossiers and by
verify_vessel_model.py, whose checks are built on this probe's numbers.
"""

import argparse
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))  # Tools/Build
import fbx_binary as F  # noqa: E402

KTIME_PER_SECOND = 46186158000.0


# ----------------------------------------------------------------- node helpers

def _s(raw):
    if isinstance(raw, bytes):
        raw = raw.decode("utf-8", "replace")
    return raw


def name_of(node):
    """FBX object names are 'Name\\x00\\x01Class'. Return the Name half."""
    if len(node.props) < 2:
        return ""
    return _s(node.props[1][1]).split("\x00")[0]


def subclass_of(node):
    if len(node.props) < 3:
        return ""
    return _s(node.props[2][1])


def obj_id(node):
    return node.props[0][1] if node.props else None


def arr(node, child_name):
    c = node.first(child_name)
    if c is None or not c.props:
        return []
    v = c.props[0][1]
    return v if isinstance(v, list) else []


def props70(node):
    """{property name: value tuple} out of a Properties70 block."""
    out = {}
    p70 = node.first("Properties70")
    if p70 is None:
        return out
    for p in p70.children:
        if p.name != "P" or not p.props:
            continue
        key = _s(p.props[0][1])
        vals = [v for (_t, v) in p.props[4:]]
        out[key] = vals[0] if len(vals) == 1 else tuple(vals)
    return out


def bounds(verts):
    if not verts:
        return None
    xs, ys, zs = verts[0::3], verts[1::3], verts[2::3]
    lo = (min(xs), min(ys), min(zs))
    hi = (max(xs), max(ys), max(zs))
    return lo, hi


def diagonal(b):
    if not b:
        return 0.0
    lo, hi = b
    return math.sqrt(sum((hi[i] - lo[i]) ** 2 for i in range(3)))


# ------------------------------------------------------------------- the probe

def probe(path, want_takes=False):
    nodes, version, _footer = F.read(path)
    top = {n.name: n for n in nodes}
    objects = top.get("Objects")
    connections = top.get("Connections")
    if objects is None:
        raise SystemExit("%s: no Objects block" % path)

    # --- global settings
    gs = props70(top["GlobalSettings"]) if "GlobalSettings" in top else {}
    settings = {
        "version": version,
        "UnitScaleFactor": gs.get("UnitScaleFactor"),
        "OriginalUnitScaleFactor": gs.get("OriginalUnitScaleFactor"),
        "UpAxis": gs.get("UpAxis"), "UpAxisSign": gs.get("UpAxisSign"),
        "FrontAxis": gs.get("FrontAxis"), "FrontAxisSign": gs.get("FrontAxisSign"),
        "CoordAxis": gs.get("CoordAxis"), "CoordAxisSign": gs.get("CoordAxisSign"),
        "TimeMode": gs.get("TimeMode"), "CustomFrameRate": gs.get("CustomFrameRate"),
    }

    # --- index objects by id
    by_id = {}
    for o in objects.children:
        oid = obj_id(o)
        if oid is not None:
            by_id[oid] = o

    # --- connections: child -> parents (OO) and property connections (OP)
    oo_parents = {}      # child id -> [parent ids]
    oo_children = {}     # parent id -> [child ids]
    op_links = []        # (src id, dst id, property)
    if connections is not None:
        for c in connections.children:
            if c.name != "C" or len(c.props) < 3:
                continue
            kind = _s(c.props[0][1])
            src, dst = c.props[1][1], c.props[2][1]
            if kind == "OO":
                oo_parents.setdefault(src, []).append(dst)
                oo_children.setdefault(dst, []).append(src)
            elif kind == "OP":
                op_links.append((src, dst, _s(c.props[3][1]) if len(c.props) > 3 else ""))

    def kids(oid, node_name=None, sub=None):
        out = []
        for cid in oo_children.get(oid, []):
            n = by_id.get(cid)
            if n is None:
                continue
            if node_name and n.name != node_name:
                continue
            if sub is not None and subclass_of(n) != sub:
                continue
            out.append(n)
        return out

    def parent_model(oid):
        for pid in oo_parents.get(oid, []):
            n = by_id.get(pid)
            if n is not None and n.name == "Model":
                return n
        return None

    # --- models (the scene hierarchy)
    models = {}
    for o in objects.children:
        if o.name != "Model":
            continue
        p = props70(o)
        parent = parent_model(obj_id(o))
        models[obj_id(o)] = {
            "id": obj_id(o),
            "name": name_of(o),
            "type": subclass_of(o),
            "parent": name_of(parent) if parent is not None else None,
            "translation": p.get("Lcl Translation", (0.0, 0.0, 0.0)),
            "rotation": p.get("Lcl Rotation", (0.0, 0.0, 0.0)),
            "scaling": p.get("Lcl Scaling", (1.0, 1.0, 1.0)),
            "pre_rotation": p.get("PreRotation"),
            "geometry": None,
            "materials": [],
        }

    # --- geometry, shapes, deformers
    meshes = []
    shapes_by_geom = {}
    for o in objects.children:
        if o.name == "Geometry" and subclass_of(o) == "Mesh":
            verts = arr(o, "Vertices")
            pvi = arr(o, "PolygonVertexIndex")
            npoly = sum(1 for i in pvi if i < 0)
            b = bounds(verts)
            # materials per polygon
            mat_counts = {}
            lem = o.first("LayerElementMaterial")
            mapping = None
            if lem is not None:
                mapping = _s(lem.first("MappingInformationType").props[0][1]) if lem.first("MappingInformationType") else None
                mats = arr(lem, "Materials")
                if mapping == "ByPolygon":
                    for m in mats:
                        mat_counts[m] = mat_counts.get(m, 0) + 1
                elif mapping == "AllSame":
                    mat_counts[mats[0] if mats else 0] = npoly
            owner = parent_model(obj_id(o))
            uv = o.first("LayerElementUV") is not None
            nrm = o.first("LayerElementNormal") is not None
            has_edges = o.first("Edges") is not None
            meshes.append({
                "id": obj_id(o), "name": name_of(o),
                "model": name_of(owner) if owner is not None else None,
                "verts": len(verts) // 3, "polys": npoly,
                "tris_if_fanned": max(0, len(pvi) - 2 * npoly),
                "bounds": b, "diagonal": diagonal(b),
                "uv": uv, "normals": nrm, "edges": has_edges,
                "material_mapping": mapping, "material_poly_counts": mat_counts,
                "shapes": [], "skin": None,
            })
            if owner is not None and obj_id(owner) in models:
                models[obj_id(owner)]["geometry"] = name_of(o)

    # map materials to models (Material --OO--> Model)
    materials = []
    for o in objects.children:
        if o.name != "Material":
            continue
        p = props70(o)
        worn_by = [name_of(by_id[pid]) for pid in oo_parents.get(obj_id(o), []) if pid in by_id and by_id[pid].name == "Model"]
        materials.append({
            "id": obj_id(o), "name": name_of(o),
            "shading": _s(o.first("ShadingModel").props[0][1]) if o.first("ShadingModel") else None,
            "diffuse": p.get("DiffuseColor"), "emissive": p.get("EmissiveColor"),
            "worn_by": worn_by,
        })
        for pid in oo_parents.get(obj_id(o), []):
            if pid in models:
                models[pid]["materials"].append(name_of(o))

    # shapes: Geometry(Shape) --OO--> Deformer(BlendShapeChannel) --OO--> Deformer(BlendShape) --OO--> Geometry(Mesh)
    for o in objects.children:
        if o.name == "Geometry" and subclass_of(o) == "Shape":
            idx = arr(o, "Indexes")
            dv = arr(o, "Vertices")
            dn = arr(o, "Normals")
            mags = [math.sqrt(dv[i] ** 2 + dv[i + 1] ** 2 + dv[i + 2] ** 2) for i in range(0, len(dv), 3)]
            # walk up to the mesh
            mesh_name = None
            full_weight = None
            for ch_id in oo_parents.get(obj_id(o), []):
                ch = by_id.get(ch_id)
                if ch is None or ch.name != "Deformer":
                    continue
                fw = arr(ch, "FullWeights")
                full_weight = fw[-1] if fw else None
                for bs_id in oo_parents.get(ch_id, []):
                    for g_id in oo_parents.get(bs_id, []):
                        g = by_id.get(g_id)
                        if g is not None and g.name == "Geometry":
                            mesh_name = name_of(g)
            shapes_by_geom.setdefault(mesh_name, []).append({
                "name": name_of(o), "indexed": len(idx),
                "moved": sum(1 for m in mags if m > 1e-6),
                "max_delta": max(mags) if mags else 0.0,
                "sum_delta": sum(mags), "has_normals": len(dn) > 0,
                "full_weight": full_weight,
            })
    for m in meshes:
        m["shapes"] = shapes_by_geom.get(m["name"], [])
        for s in m["shapes"]:
            s["travel_fraction"] = (s["max_delta"] / m["diagonal"]) if m["diagonal"] else 0.0

    # skin: Deformer(Cluster) --OO--> Deformer(Skin) --OO--> Geometry; Model(LimbNode) --OO--> Cluster
    bones = {}
    for o in objects.children:
        if o.name == "Deformer" and subclass_of(o) == "Cluster":
            idx = arr(o, "Indexes")
            w = arr(o, "Weights")
            bone = None
            for cid in oo_children.get(obj_id(o), []):
                n = by_id.get(cid)
                if n is not None and n.name == "Model":
                    bone = name_of(n)
            mesh_name = None
            for skin_id in oo_parents.get(obj_id(o), []):
                for g_id in oo_parents.get(skin_id, []):
                    g = by_id.get(g_id)
                    if g is not None and g.name == "Geometry":
                        mesh_name = name_of(g)
            bones[bone] = {"skinned_verts": len(idx), "max_weight": max(w) if w else 0.0, "mesh": mesh_name}
            for m in meshes:
                if m["name"] == mesh_name:
                    m["skin"] = m["skin"] or {"clusters": 0, "bones": []}
                    m["skin"]["clusters"] += 1
                    m["skin"]["bones"].append(bone)

    limb_nodes = [mm for mm in models.values() if mm["type"] == "LimbNode"]
    for b in limb_nodes:
        b["skinned_verts"] = bones.get(b["name"], {}).get("skinned_verts", 0)

    # --- takes
    takes = []
    for o in objects.children:
        if o.name != "AnimationStack":
            continue
        p = props70(o)
        local_stop = p.get("LocalStop")
        local_start = p.get("LocalStart")
        length_s = ((local_stop or 0) - (local_start or 0)) / KTIME_PER_SECOND
        moved = {}
        shape_curves = 0
        for layer in kids(obj_id(o), "AnimationLayer"):
            for cn in kids(obj_id(layer), "AnimationCurveNode"):
                # the curve node connects (OP) to a Model property or a BlendShapeChannel
                targets = [(dst, prop) for (src, dst, prop) in op_links if src == obj_id(cn)]
                # Curves hang off their node by an OP link ("d|X"/"d|Y"/"d|Z"), not OO.
                curves = [by_id[src] for (src, dst, _p) in op_links
                          if dst == obj_id(cn) and src in by_id and by_id[src].name == "AnimationCurve"]
                # A take is either a MOTION (keys that differ along the take) or a POSE (one key
                # per channel that differs from the node's REST). The Squirrel's nine takes are
                # poses: the animator's blend tree mixes them by stick position, so a per-take
                # "range" of zero would read as "nothing animates" when every limb is deflected.
                # Report both: 'range' = max-min over the keys, 'offset' = farthest key from rest.
                nkeys = 0
                rng = 0.0
                keyvals = []  # (channel index 0..2, values)
                chan_of = {src: _p for (src, dst, _p) in op_links if dst == obj_id(cn)}
                for cv in curves:
                    vals = arr(cv, "KeyValueFloat")
                    if vals:
                        nkeys = max(nkeys, len(vals))
                        rng = max(rng, max(vals) - min(vals))
                        keyvals.append(({"d|X": 0, "d|Y": 1, "d|Z": 2}.get(chan_of.get(obj_id(cv), ""), -1), vals))
                for dst, prop in targets:
                    t = by_id.get(dst)
                    if t is None:
                        continue
                    if t.name == "Deformer":
                        shape_curves += 1
                        continue
                    if t.name == "Model":
                        key = name_of(t)
                        rest = models.get(dst, {}).get({"Lcl Translation": "translation", "Lcl Rotation": "rotation", "Lcl Scaling": "scaling"}.get(prop, ""), None)
                        # the curve node's own d|X d|Y d|Z defaults are the per-channel values
                        cnp = props70(cn)
                        offset = 0.0
                        if rest is not None and isinstance(rest, tuple):
                            for axis_i, axis in enumerate(("d|X", "d|Y", "d|Z")):
                                v = cnp.get(axis)
                                if isinstance(v, (int, float)):
                                    offset = max(offset, abs(v - rest[axis_i]))
                        for axis_i, vals in keyvals:
                            if rest is not None and isinstance(rest, tuple) and 0 <= axis_i < 3:
                                offset = max(offset, max(abs(v - rest[axis_i]) for v in vals))
                        d = moved.setdefault(key, {})
                        d[prop] = {"range": max(d.get(prop, {}).get("range", 0.0), rng),
                                   "offset": max(d.get(prop, {}).get("offset", 0.0), offset),
                                   "keys": max(d.get(prop, {}).get("keys", 0), nkeys)}
        moving = {k: {pp: {kk: (round(vv, 4) if isinstance(vv, float) else vv) for kk, vv in v.items()}
                      for pp, v in d.items() if v["range"] > 1e-4 or v["offset"] > 1e-4} for k, d in moved.items()}
        moving = {k: v for k, v in moving.items() if v}
        takes.append({
            "name": name_of(o), "seconds": round(length_s, 4),
            "nodes_with_curves": len(moved), "nodes_that_move": len(moving),
            "moving": moving if want_takes else {k: list(v.keys()) for k, v in moving.items()},
            "moving_detail": moving,
            "blend_shape_curves": shape_curves,
        })

    return {
        "path": path, "size": os.path.getsize(path), "settings": settings,
        "models": list(models.values()), "meshes": meshes, "materials": materials,
        "bones": limb_nodes, "takes": takes,
    }


# ------------------------------------------------------------------- printing

def _fmt3(t):
    try:
        return "(%.4g, %.4g, %.4g)" % tuple(t)
    except Exception:
        return str(t)


def print_report(r, want_takes=False):
    s = r["settings"]
    print("=" * 78)
    print(r["path"], " %.1f MB  fbx %s" % (r["size"] / 1e6, s["version"]))
    print("  units: UnitScaleFactor=%s (original %s)  up=%s%s front=%s%s coord=%s%s  TimeMode=%s" % (
        s["UnitScaleFactor"], s["OriginalUnitScaleFactor"],
        "+" if (s["UpAxisSign"] or 1) > 0 else "-", s["UpAxis"],
        "+" if (s["FrontAxisSign"] or 1) > 0 else "-", s["FrontAxis"],
        "+" if (s["CoordAxisSign"] or 1) > 0 else "-", s["CoordAxis"], s["TimeMode"]))
    print("  hierarchy (%d models):" % len(r["models"]))
    by_parent = {}
    for m in r["models"]:
        by_parent.setdefault(m["parent"], []).append(m)

    def walk(parent, depth):
        for m in by_parent.get(parent, []):
            extra = ""
            if m["type"] == "LimbNode":
                extra = "  skins %d verts" % m.get("skinned_verts", 0)
            if m["geometry"]:
                extra += "  geometry=%s  materials=%s" % (m["geometry"], m["materials"])
            pre = ("  pre_rot=%s" % _fmt3(m["pre_rotation"])) if m["pre_rotation"] else ""
            print("    %s%s [%s] T=%s R=%s S=%s%s%s" % ("  " * depth, m["name"], m["type"],
                  _fmt3(m["translation"]), _fmt3(m["rotation"]), _fmt3(m["scaling"]), pre, extra))
            walk(m["name"], depth + 1)
    walk(None, 0)

    print("  meshes (%d):" % len(r["meshes"]))
    for m in r["meshes"]:
        lo, hi = m["bounds"] if m["bounds"] else ((0, 0, 0), (0, 0, 0))
        print("    %-28s on model %-24s verts %6d  polys %6d (%d tris)  bounds %s..%s diag %.3f  uv=%s normals=%s" % (
            m["name"], m["model"], m["verts"], m["polys"], m["tris_if_fanned"], _fmt3(lo), _fmt3(hi), m["diagonal"], m["uv"], m["normals"]))
        if m["material_poly_counts"]:
            print("       material slots (%s): %s" % (m["material_mapping"], dict(sorted(m["material_poly_counts"].items()))))
        if m["skin"]:
            print("       skin: %d clusters" % m["skin"]["clusters"])
        for sh in m["shapes"]:
            verdict = "REAL" if sh["travel_fraction"] >= 0.01 else ("weak" if sh["moved"] > 0 else "EMPTY")
            print("       shape %-16s indexed %6d moved %6d maxD %.4f (%.2f%% of diag) sumD %.1f fullW=%s normals=%s  %s" % (
                sh["name"], sh["indexed"], sh["moved"], sh["max_delta"], 100 * sh["travel_fraction"], sh["sum_delta"],
                sh["full_weight"], sh["has_normals"], verdict))
    print("  materials (%d):" % len(r["materials"]))
    for mt in r["materials"]:
        print("    %-20s shading=%s diffuse=%s emissive=%s worn by %s" % (
            mt["name"], mt["shading"], _fmt3(mt["diffuse"]) if mt["diffuse"] else None,
            _fmt3(mt["emissive"]) if mt["emissive"] else None, mt["worn_by"]))
    print("  bones (%d): %s" % (len(r["bones"]), ", ".join(b["name"] for b in r["bones"])))
    print("  takes (%d):" % len(r["takes"]))
    for t in r["takes"]:
        print("    %-48s %.3fs  curves on %d nodes, %d move; %d blend-shape curves" % (
            t["name"], t["seconds"], t["nodes_with_curves"], t["nodes_that_move"], t["blend_shape_curves"]))
        if want_takes:
            for node, props in t["moving"].items():
                print("        %-24s %s" % (node, {p.replace("Lcl ", ""): "range %.3g offset %.3g keys %d" % (c["range"], c["offset"], c["keys"]) for p, c in props.items()}))
        else:
            mv = list(t["moving"].items())
            if mv:
                print("        moves: %s" % ", ".join("%s(%s)" % (k, "/".join(p.replace("Lcl ", "") for p in v)) for k, v in mv[:12]) + (" ..." if len(mv) > 12 else ""))
            kinds = "pose" if all(all(c["keys"] <= 1 or c["range"] < 1e-4 for c in v.values()) for v in t["moving_detail"].values()) and t["moving_detail"] else "motion"
            print("        kind: %s" % (kinds if t["moving_detail"] else "static"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("fbx", nargs="+")
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--takes", action="store_true", help="per-take, per-node curve ranges")
    a = ap.parse_args()
    out = []
    for p in a.fbx:
        r = probe(p, want_takes=a.takes)
        out.append(r)
        if not a.json:
            print_report(r, want_takes=a.takes)
    if a.json:
        print(json.dumps(out, indent=1, default=str))


if __name__ == "__main__":
    main()
