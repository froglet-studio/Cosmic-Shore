#!/usr/bin/env python3
"""A spec's BODY, in the same shape the dossier measures a shipped creature in.

    from body import from_spec           # body = {"wires", "prisms", "heart", "chains", "root_scale"}

One function serves both kinds of spec:
- a CELESTIAL spec (specs/<species>.py, the kit's generative language: braid spindles from -> to, prisms on them):
  every spindle wears the shipped braid mesh (bonita.fbx BezierCurve.001) posed exactly as build.py poses it in a
  prefab (root at `from`, local +z from tip to root, stretched along its length only, `girth` across), and every
  prism takes celestial.oriented's radial frame. So what the lab flies is what build.py would build.
- a SHIPPED spec (specs/shipped/<key>.py, a transcription of a creature the game already has): its wires are the
  shipped meshes by reference with the placement the spec states, its prisms and heart the poses the spec states.

recreate.py holds this function's output against the dossier (the shipped prefab measured again), which is the
kit's proof that it loses nothing between a spec and a body.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build as B  # noqa: E402
import celestial as C  # noqa: E402
import fbx_model as fm  # noqa: E402
import prefab_read as pr  # noqa: E402

BRAID_SOURCE = "Assets/_Models/Fauna/bonita.fbx#BezierCurve.001"
_MESHES, _POLYS = {}, {}


def mesh(source):
    """(verts, tris) of 'path.fbx#MeshName', in the mesh's own space, Unity units."""
    if source not in _MESHES:
        path, name = source.split("#")
        model = fm.load(os.path.join(pr.REPO, path))
        me = next(m for m in model["meshes"] if m["name"] == name)
        _MESHES[source] = (me["verts"], me["tris"])
        _POLYS[source] = me["polys"]
    return _MESHES[source]


def polys(source):
    """The mesh's polygons as the artist modelled them (quads, mostly): what a wireframe view draws, no diagonals."""
    mesh(source)
    return _POLYS[source]


def _quat_m(q):
    return fm.quat_to_m(q)


def spindle_chains(spec):
    """FaunaAttackDriver.Scan on the transform tree build.py writes for a spec: every spindle is a GameObject under
    its parent spindle, with a Braid child (a renderer, so never a chain) and its prisms (renderers too). The hub is
    the spindle with the most child spindles that themselves have a child; three such chains or the body lunges.
    A chain runs down each such child by its FIRST child, which build.py writes as the Braid: so a limb is
    [the spindle, its Braid] unless the spindle's first child is another spindle (`first_child_chain`)."""
    kids = {}
    for s in spec["spindles"]:
        kids.setdefault(s.get("parent"), []).append(s)
    best, hub = 0, None
    for s in spec["spindles"]:
        k = len(kids.get(s["name"], []))      # every child spindle has a child (its Braid): each is a chain
        if k > best:
            best, hub = k, s
    if hub is None or best < 3:
        return []
    out = []
    for c in kids[hub["name"]]:
        joints, at, cur = [], [], c
        while cur is not None and len(joints) < 12:
            joints.append(cur["name"])
            at.append([round(v, 5) for v in cur["from"]])
            nxt = kids.get(cur["name"], [])
            # the last joint is that spindle's Braid child, which build.py seats where the braid mesh's origin
            # lands: BRAID_FRONT x the stretch back from the root, along the limb
            if not nxt and cur.get("mesh") == "grown":      # a grown limb ends at its own tip
                joints.append(cur["name"] + "/tip")
                at.append([round(v, 5) for v in cur["to"]])
                cur = None
            elif not nxt:
                _across, along = B.braid_scale(cur)
                d = C.unit(C.sub(cur["to"], cur["from"]))
                joints.append(cur["name"] + "/Braid")
                at.append([round(cur["from"][i] + d[i] * B.BRAID_FRONT * along, 5) for i in range(3)])
                cur = None
            else:
                cur = nxt[0]
        out.append({"joints": joints, "at": at})
    return out


def _beat(s):
    """A spindle's beat with its axis resolved in the body's space: 'travel' is the travel axis, 'tangent' the
    direction round it at the limb (square to the travel axis and to the limb's radial), or a vector as given.
    A beat is a ROTATION about the limb's root (a CPU driver writing localRotation in the game, /fauna section 3)."""
    b = s.get("beat")
    if not b:
        return None
    ax = b["axis"]
    if ax == "travel":
        axis = [0.0, 0.0, 1.0]
    elif ax == "tangent":
        ref = s["from"] if (s["from"][0] ** 2 + s["from"][1] ** 2) > 1e-6 else s["to"]
        axis = C.unit(C.cross([0.0, 0.0, 1.0], [ref[0], ref[1], 0.0]))
    else:
        axis = C.unit([float(v) for v in ax])
    return {"axis": [round(v, 6) for v in axis], "deg": float(b["deg"]), "hz": float(b["hz"]), "phase": float(b.get("phase", 0.0))}


def from_spec(spec):
    if spec.get("shipped"):
        return _from_shipped(spec)
    wires, prisms = [], []
    bverts, btris = mesh(BRAID_SOURCE)
    by_name = {s["name"]: s for s in spec["spindles"]}
    inherit = {}      # what each grown limb's ROOT is already doing: its parent's whole sway where they join

    def base_of(s):
        if s["name"] not in inherit:
            par = by_name.get(s.get("parent"))
            inherit[s["name"]] = organic.sway_at(par, s["from"], base_of(par)) if par and par.get("mesh") == "grown" else None
        return inherit[s["name"]]
    import organic
    import spindle as SP
    spec = dict(spec, spindles=[SP.resolve(s) for s in spec["spindles"]])      # a limb names a spindle DESIGN; its own keys win
    by_name = {s["name"]: s for s in spec["spindles"]}
    for s in spec["spindles"]:
        if s.get("mesh") == "grown":
            end = by_name.get(s.get("to_limb"))      # a limb that ends ON another limb rides both (a rib between two edges)
            verts, edges, sway, glow, lines = organic.tube(s, base_of(s), organic.sway_at(end, s["to"], base_of(end)) if end else None)
            wires.append({"name": s["name"], "source": "grown", "limb": s["name"], "verts": [[round(c, 4) for c in v] for v in verts],
                          "edges": edges, "sway": sway, "glow": glow, "tris": [], "parent": s.get("parent"), "pivot": list(s["from"]), "beat": None,
                          "lines": lines, "design": s.get("design"), "section": s.get("section", 3), "wire": s.get("wire", [0.09, 0.04])})
            continue
        pos, rot = B.spindle_world(s)
        across, along = B.braid_scale(s)
        m = _quat_m(rot)
        off = [0.0, 0.0, -B.BRAID_FRONT * along]
        verts = []
        for v in bverts:
            loc = [v[0] * across + off[0], v[1] * across + off[1], v[2] * along + off[2]]
            w = fm.mdir(m, loc)
            verts.append([round(w[i] + pos[i], 5) for i in range(3)])
        wires.append({"name": s["name"], "source": BRAID_SOURCE, "limb": s["name"], "verts": verts, "tris": btris,
                      "parent": s.get("parent"), "pivot": [float(v) for v in s["from"]], "beat": _beat(s)})
    for m in spec.get("membranes", []):
        la, lb = by_name[m["a"]], by_name[m["b"]]
        verts, edges, sway, glow, tris = organic.sheet(la, lb, m.get("across", 9), base_of(la), base_of(lb))
        wires.append({"name": m["name"], "source": "grown", "limb": m["a"], "verts": [[round(c, 4) for c in v] for v in verts],
                      "edges": edges, "sway": sway, "glow": glow, "tris": tris, "parent": None, "pivot": [0, 0, 0], "beat": None, "membrane": True,
                      "across": m.get("across", 9)})
    for p in spec["prisms"]:
        at, rot = B.prism_world(p)
        prisms.append({"name": p["name"], "kind": p["kind"], "shielded": bool(p.get("shielded")),
                       "at": [round(v, 5) for v in at], "q": [round(v, 6) for v in rot],
                       "size": [float(v) for v in p["size"]], "limb": p["spindle"], "role": p.get("role"),
                       "sway": ([round(v, 4) for v in organic.sway_at(by_name[p["spindle"]], at, base_of(by_name[p["spindle"]]))]
                                if by_name.get(p["spindle"], {}).get("mesh") == "grown" else None)})
    return {"root_scale": 1.0, "wires": wires, "prisms": prisms,
            "heart": {"at": [0.0, 0.0, 0.0], "scale": C.HEART_WORLD}, "chains": spindle_chains(spec),
            "spindles": [s["name"] for s in spec["spindles"]]}


def _from_shipped(spec):
    def one(part):
        wires = []
        for sk in part["skin"]:
            verts, tris = mesh(sk["mesh"])
            m = [list(r) for r in sk["local"]] + [[0.0, 0.0, 0.0, 1.0]]
            wires.append({"name": sk["name"], "source": sk["mesh"], "limb": sk.get("limb"),
                          "verts": [[round(c, 5) for c in fm.mpoint(m, v)] for v in verts], "tris": tris})
        return {"root_scale": part.get("root_scale", 1.0), "wires": wires, "prisms": [dict(p) for p in part["prisms"]],
                "heart": dict(part["heart"]) if part.get("heart") else None, "chains": [dict(c) for c in part.get("limbs", [])],
                "spindles": list(part.get("spindles", []))}
    if "segments" in spec:
        return {"segments": {role: one(part) for role, part in spec["segments"].items()}}
    return one(spec)
