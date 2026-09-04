#!/usr/bin/env python3
"""
Re-prove the SHIPPED RotateFacesAlongAxis subgraph, from the file, by evaluating it.

`wire_prism_face_pivot.py` asserts the graph's SHAPE — that the delta chain exists and is
connected where it should be. That is not the same claim as "the vertex position it
computes is the pivot lerp we designed": a splice can be structurally perfect and still
have an A/B input swapped, a sign inverted, or a slot mis-slotted. The transcription from
a proven design to a serialized asset is the step neither the design nor code review can
see (asset-surgery §4.5), so this evaluates the actual JSON as a dataflow graph and checks
the OUTPUT.

It proves four things:

  1. With CentroidPivotWeight = 0, the shipped subgraph reproduces the PRE-EDIT subgraph
     (read straight out of git) to the last bit, over randomized inputs. That is the whole
     "the prism cube is unchanged" claim, measured rather than argued.
  2. With weight = 1 and no explosive spread, feeding the FACE CENTROID in returns it
     unchanged — the centroid is the map's FIXED POINT — and every other vertex keeps its
     distance from it (measured in the object-scale frame the rotation runs in). A fixed
     point plus preserved distances IS "the face spins rigidly about its own centre".
  3. With weight = 0 the centroid MOVES, by a distance worth reporting: that is the defect,
     measured on the shipped graph rather than argued from the source.
  4. The rotation is actually rotating (a generic vertex moves), so 1-3 are not vacuous.

Note for anyone extending this: `SpreadValue` is a Vector3 and the tangent slide multiplies
it COMPONENTWISE, so the legacy slide is not generally parallel to the tangent and the
legacy map is not a rigid rotation about any point at all. That is pre-existing behaviour
this change deliberately reproduces bit-for-bit at weight 0 (proof 1); do not "fix" it here.
At weight 1 the delta cancels the whole `Pn + slide` construction algebraically, so the map
collapses to an exact rotation about the centroid whatever SpreadValue is doing.

Then, independently of the shader, it re-derives the GEOMETRY that motivated the change
from the two mesh generators' own construction: where each shield face's plane-foot lands
relative to that face. This is the part that says the octahedron's pivot was merely off
centre while the stella's was outside the triangle altogether.

Usage:  python3 Tools/Shaders/verify_prism_face_pivot.py
"""

import json
import math
import os
import random
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SUBGRAPH = "Assets/_Graphics/Materials/Graphs/PrismGraphs/Subgraphs/RotateFacesAlongAxis.shadersubgraph"
SUBGRAPH_DIR = "Assets/_Graphics/Materials/Graphs/PrismGraphs/Subgraphs"


# ---------------------------------------------------------------------------
# tiny vector helpers (the shader works in float3; scalars broadcast)
# ---------------------------------------------------------------------------

def v3(x, y=None, z=None):
    if y is None:
        return (float(x), float(x), float(x))
    return (float(x), float(y), float(z))


def as3(a):
    return a if isinstance(a, tuple) else v3(a)


def binop(a, b, f):
    if isinstance(a, tuple) or isinstance(b, tuple):
        a, b = as3(a), as3(b)
        return tuple(f(a[i], b[i]) for i in range(3))
    return f(a, b)


def add(a, b):
    return binop(a, b, lambda x, y: x + y)


def sub(a, b):
    return binop(a, b, lambda x, y: x - y)


def mul(a, b):
    return binop(a, b, lambda x, y: x * y)


def div(a, b):
    return binop(a, b, lambda x, y: x / y)


def dot(a, b):
    a, b = as3(a), as3(b)
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    a, b = as3(a), as3(b)
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def norm(a):
    a = as3(a)
    m = math.sqrt(dot(a, a))
    return a if m == 0.0 else tuple(c / m for c in a)


def rotate_about_axis(vec, axis, angle):
    """Unity_RotateAboutAxis_Radians_float — Rodrigues on a normalized axis."""
    axis = norm(axis)
    s, c = math.sin(angle), math.cos(angle)
    k = 1.0 - c
    x, y, z = axis
    m = ((k * x * x + c, k * x * y - s * z, k * x * z + s * y),
         (k * x * y + s * z, k * y * y + c, k * y * z - s * x),
         (k * x * z - s * y, k * y * z + s * x, k * z * z + c))
    v = as3(vec)
    return tuple(sum(m[r][i] * v[i] for i in range(3)) for r in range(3))


# ---------------------------------------------------------------------------
# graph evaluation
# ---------------------------------------------------------------------------

def load_docs_text(text):
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


def load_docs(path):
    return load_docs_text(open(path, encoding="utf-8").read())


def guid_to_path(guid):
    """Resolve a subgraph guid to its .shadersubgraph via the committed .meta."""
    out = subprocess.run(["grep", "-rl", f"guid: {guid}", os.path.join(REPO, SUBGRAPH_DIR)],
                         capture_output=True, text=True).stdout.split()
    metas = [p for p in out if p.endswith(".shadersubgraph.meta")]
    assert len(metas) == 1, f"guid {guid} resolves to {len(metas)} subgraphs"
    return metas[0][: -len(".meta")]


class Graph:
    """A parsed ShaderGraph document set, evaluated as a pure dataflow DAG."""

    def __init__(self, docs, vertex_inputs):
        self.idx = {d["m_ObjectId"]: d for d in docs if "m_ObjectId" in d}
        self.graph = next(d for d in docs if "GraphData" in d.get("m_Type", ""))
        self.vertex = vertex_inputs  # {"normal":..., "tangent":..., "objectScale":...}
        self.sources = {}
        for e in self.graph["m_Edges"]:
            i, o = e["m_InputSlot"], e["m_OutputSlot"]
            self.sources[(i["m_Node"]["m_Id"], i["m_SlotId"])] = (o["m_Node"]["m_Id"], o["m_SlotId"])
        self.props = {}
        self._cache = {}

    def slot(self, node, int_id):
        for ref in node.get("m_Slots", []):
            s = self.idx[ref["m_Id"]]
            if s["m_Id"] == int_id:
                return s
        raise KeyError(f"slot {int_id} on {node.get('m_Name')}")

    def default(self, node, int_id):
        v = self.slot(node, int_id)["m_Value"]
        if isinstance(v, dict):
            if "e00" in v:  # DynamicValue slots serialize as a 4x4 matrix
                return v3(v["e00"], v["e01"], v["e02"])
            return v3(v["x"], v["y"], v["z"])
        return float(v)

    def read(self, node, int_id):
        src = self.sources.get((node["m_ObjectId"], int_id))
        if src is None:
            return self.default(node, int_id)
        return self.eval(self.idx[src[0]], src[1])

    def eval(self, node, out_slot):
        key = (node["m_ObjectId"], out_slot)
        if key in self._cache:
            return self._cache[key]
        t = node["m_Type"].split(".")[-1]

        if t == "PropertyNode":
            name = self.idx[node["m_Property"]["m_Id"]]["m_Name"]
            assert name in self.props, f"no value supplied for property '{name}'"
            val = self.props[name]
        elif t == "NormalVectorNode":
            assert node["m_Space"] == 0, "only object-space normals are modelled"
            val = self.vertex["normal"]
        elif t == "TangentVectorNode":
            assert node["m_Space"] == 0, "only object-space tangents are modelled"
            val = self.vertex["tangent"]
        elif t == "ObjectNode":
            assert out_slot == 1, "only Object.Scale is modelled"
            val = self.vertex["objectScale"]
        elif t == "AddNode":
            val = add(self.read(node, 0), self.read(node, 1))
        elif t == "SubtractNode":
            val = sub(self.read(node, 0), self.read(node, 1))
        elif t == "MultiplyNode":
            val = mul(self.read(node, 0), self.read(node, 1))
        elif t == "DivideNode":
            val = div(self.read(node, 0), self.read(node, 1))
        elif t == "DotProductNode":
            val = dot(self.read(node, 0), self.read(node, 1))
        elif t == "CrossProductNode":
            val = cross(self.read(node, 0), self.read(node, 1))
        elif t == "RotateAboutAxisNode":
            val = rotate_about_axis(self.read(node, 0), self.read(node, 1), self.read(node, 2))
        elif t == "SubGraphNode":
            val = self.eval_subgraph(node, out_slot)
        else:
            raise NotImplementedError(f"node type {t} is not modelled")

        self._cache[key] = val
        return val

    def eval_subgraph(self, node, out_slot):
        meta = json.loads(node["m_SerializedSubGraph"])
        child_docs = load_docs(guid_to_path(meta["subGraph"]["guid"]))
        child = Graph(child_docs, self.vertex)
        # Map this node's input slots onto the child's properties by the serialized
        # guid -> slot-id table the SubGraphNode carries.
        guids, ids = node["m_PropertyGuids"], node["m_PropertyIds"]
        for cp in child_docs:
            if "ShaderProperty" not in cp.get("m_Type", ""):
                continue
            g = cp["m_Guid"]["m_GuidSerialized"]
            assert g in guids, f"subgraph property {cp['m_Name']} has no slot on the consumer"
            child.props[cp["m_Name"]] = self.read(node, ids[guids.index(g)])
        out_node = next(d for d in child_docs if "SubGraphOutputNode" in d.get("m_Type", ""))
        return child.read(out_node, out_slot)

    def output(self, out_slot, **props):
        self._cache.clear()
        self.props = props
        out_node = next(self.idx[r["m_Id"]] for r in self.graph["m_Nodes"]
                        if "SubGraphOutputNode" in self.idx[r["m_Id"]].get("m_Type", ""))
        return self.read(out_node, out_slot)


# ---------------------------------------------------------------------------
# the proofs
# ---------------------------------------------------------------------------

POSITION_OUT = 1  # the SubGraphOutputNode's Position slot


def make_case(rng):
    """One randomized but geometrically consistent vertex: a face frame plus a vertex on
    it. Normal and tangent are orthonormal, as any real mesh's are."""
    n = norm((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1)))
    t = (rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))
    t = norm(sub(t, mul(n, dot(t, n))))
    b = cross(n, t)
    d = rng.uniform(-3, 3)                     # face plane offset along the normal
    u, w = rng.uniform(-2, 2), rng.uniform(-2, 2)
    p = add(mul(n, d), add(mul(t, u), mul(b, w)))
    c = add(mul(n, d), add(mul(t, rng.uniform(-2, 2)), mul(b, rng.uniform(-2, 2))))
    return dict(normal=n, tangent=t, objectScale=(rng.uniform(0.4, 3.0),
                                                  rng.uniform(0.4, 3.0),
                                                  rng.uniform(0.4, 3.0))), p, c


def base_props(rng):
    return dict(velocity=(rng.uniform(-40, 40), rng.uniform(-40, 40), rng.uniform(-40, 40)),
                SpreadValue=v3(rng.uniform(0.0, 0.4), rng.uniform(0.0, 0.4), rng.uniform(0.0, 0.4)),
                ExplosionAmount=rng.uniform(0.0, 21.0),
                ExplosiveRotation=0.0169,
                ExplosiveSpread=0.05)


def pre_edit_docs():
    """The last revision of the subgraph that does NOT carry the pivot inputs.

    Found by walking the file's own history rather than assuming HEAD is pre-change —
    an assumption that is true exactly once, on the day the change is written, and makes
    the script un-runnable forever after. Returns None when no such revision is reachable,
    which is the normal state of a shallow clone; proof 1 is then reported as SKIPPED
    rather than silently passing.
    """
    revs = subprocess.run(["git", "-C", REPO, "log", "--format=%H", "--", SUBGRAPH],
                          capture_output=True, text=True).stdout.split()
    for rev in revs:
        got = subprocess.run(["git", "-C", REPO, "show", f"{rev}:{SUBGRAPH}"],
                             capture_output=True, text=True)
        if got.returncode != 0:
            continue
        docs = load_docs_text(got.stdout)
        if not any(d.get("m_Name") == "FaceCentroid" for d in docs):
            return rev, docs
    return None, None


def main():
    path = os.path.join(REPO, SUBGRAPH)
    new_docs = load_docs(path)
    old_rev, old_docs = pre_edit_docs()

    rng = random.Random(20260825)
    worst_identical = worst_fixed = worst_rigid = motion = 0.0
    least_legacy_drift = float("inf")

    for _ in range(400):
        vertex, p, c = make_case(rng)
        props = base_props(rng)
        new = Graph(new_docs, vertex)
        old = Graph(old_docs, vertex) if old_docs is not None else None

        # (1) weight 0 reproduces the pre-edit graph, bit for bit.
        a = new.output(POSITION_OUT, Position=p, FaceCentroid=c, CentroidPivotWeight=0.0, **props)
        if old is not None:
            b = old.output(POSITION_OUT, Position=p, **props)
            worst_identical = max(worst_identical, max(abs(a[i] - b[i]) for i in range(3)))

        # (4) and the rotation is not a no-op, so (1) is not vacuous.
        motion = max(motion, max(abs(a[i] - p[i]) for i in range(3)))

        # (2)/(3), with the explosive tangent spread switched off so the only remaining
        # term is the rotation itself.
        still = dict(props, ExplosiveSpread=0.0)
        sc = vertex["objectScale"]

        at_c = new.output(POSITION_OUT, Position=c, FaceCentroid=c,
                          CentroidPivotWeight=1.0, **still)
        worst_fixed = max(worst_fixed, max(abs(at_c[i] - c[i]) for i in range(3)))

        moved = new.output(POSITION_OUT, Position=p, FaceCentroid=c,
                           CentroidPivotWeight=1.0, **still)
        before = math.sqrt(dot(mul(sub(p, c), sc), mul(sub(p, c), sc)))
        after = math.sqrt(dot(mul(sub(moved, c), sc), mul(sub(moved, c), sc)))
        worst_rigid = max(worst_rigid, abs(after - before) / max(before, 1e-6))

        legacy_at_c = new.output(POSITION_OUT, Position=c, FaceCentroid=c,
                                 CentroidPivotWeight=0.0, **still)
        drift = math.sqrt(dot(sub(legacy_at_c, c), sub(legacy_at_c, c)))
        least_legacy_drift = min(least_legacy_drift, drift)

    print("SHIPPED SUBGRAPH — 400 randomized vertices, faces and explosion states")
    if old_docs is None:
        print("  1. weight 0 vs the pre-edit graph .......... SKIPPED — no pre-change revision "
              "of the subgraph is reachable (shallow clone). Deepen with "
              "`git fetch --deepen=50` to run it.")
    else:
        print(f"  1. weight 0 vs the pre-edit graph .......... max |delta|   = "
              f"{worst_identical:.3e}   (vs {old_rev[:8]})")
    print(f"  2a. weight 1: centroid is the fixed point .. max |delta|   = {worst_fixed:.3e}")
    print(f"  2b. weight 1: distances about it preserved . max rel error = {worst_rigid:.3e}")
    print(f"  3. weight 0: the centroid MOVES (the bug) .. min |drift|   = {least_legacy_drift:.3f}")
    print(f"  4. a generic vertex actually moves ......... max |delta|   = {motion:.3f}")
    if old_docs is not None:
        assert worst_identical == 0.0, "weight 0 is NOT bit-identical to the pre-edit graph"
    assert worst_fixed < 1e-12, "the centroid is not the fixed point at weight 1"
    assert worst_rigid < 1e-12, "the face is not rigid about the centroid at weight 1"
    assert least_legacy_drift > 1e-3, \
        "the legacy pivot already sat on the centroid, so there was nothing to fix"
    assert motion > 1e-3, "the rotation is a no-op, so the identity proves nothing"

    print()
    print("SHIELD FACE GEOMETRY — where the plane-foot lands, from the generators' own")
    print("construction (barycentric on the face; any coordinate < 0 is OUTSIDE it)")
    for label, verts in shield_faces():
        foot = plane_foot(verts)
        bary = barycentric(foot, verts)
        cent = tuple(sum(v[i] for v in verts) / 3.0 for i in range(3))
        inside = all(x > -1e-9 for x in bary)
        dist = math.sqrt(dot(sub(foot, cent), sub(foot, cent)))
        edge = max(math.sqrt(dot(sub(verts[i], verts[j]), sub(verts[i], verts[j])))
                   for i, j in ((0, 1), (1, 2), (2, 0)))
        print(f"  {label:34s} bary=({bary[0]:+.3f},{bary[1]:+.3f},{bary[2]:+.3f})  "
              f"{'INSIDE ' if inside else 'OUTSIDE'}  |foot-centroid| = {dist / edge:.3f} edges")
    print()
    print("All proofs passed.")
    return 0


def plane_foot(verts):
    n = norm(cross(sub(verts[1], verts[0]), sub(verts[2], verts[0])))
    return mul(n, dot(verts[0], n))


def barycentric(p, verts):
    """Solve p = a*v0 + b*v1 + c*v2 with a+b+c = 1 (p is assumed in the plane)."""
    e1, e2 = sub(verts[1], verts[0]), sub(verts[2], verts[0])
    r = sub(p, verts[0])
    d11, d12, d22 = dot(e1, e1), dot(e1, e2), dot(e2, e2)
    d1, d2 = dot(r, e1), dot(r, e2)
    det = d11 * d22 - d12 * d12
    b = (d22 * d1 - d12 * d2) / det
    c = (d11 * d2 - d12 * d1) / det
    return (1.0 - b - c, b, c)


def shield_faces():
    """One representative face of each shield mesh, built exactly as the C# generators do.
    Half-extents 0.5 (the fleet's authored prism collider) times CIRCUMSCRIBING_SCALE 3."""
    a = b = c = 0.5 * 3.0
    # OctahedronMeshGenerator.AddFace(pX, pY, pZ) — one octant's triangle.
    yield "octahedron face (regular prism)", [(a, 0, 0), (0, b, 0), (0, 0, c)]
    # The same face on an elongated prism (a trail slab), where the foot is no longer the
    # centroid but is still, provably, inside the triangle.
    yield "octahedron face (10:1 slab)", [(a, 0, 0), (0, b, 0), (0, 0, c * 10.0)]
    # StellatedOctahedronMeshGenerator.AddFace(T, Vx, Vy) — a spike's lateral face. Three
    # of these share one tetrahedron-face plane, so the foot lands in the hole between them.
    yield "stella spike face (regular prism)", [(a, b, c), (a, 0, 0), (0, b, 0)]
    yield "stella spike face (10:1 slab)", [(a, b, c * 10.0), (a, 0, 0), (0, b, 0)]


if __name__ == "__main__":
    sys.exit(main())
