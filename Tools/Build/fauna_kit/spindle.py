#!/usr/bin/env python3
"""SPINDLES from scratch: what the game's spindles are made of, a library of new designs, and the mesh each makes.

    python3 Tools/Build/fauna_kit/spindle.py --shipped            # the shipped spindle meshes, taken apart
    python3 Tools/Build/fauna_kit/spindle.py --designs            # every design: what it is and what a 20 u limb costs
    python3 Tools/Build/fauna_kit/spindle.py --budget             # every grown creature's mesh, limb by limb, against the bar
    python3 Tools/Build/fauna_kit/spindle.py --obj <species> <dir>  # write a creature's limbs as .obj meshes Unity imports
    python3 Tools/Build/fauna_kit/spindle.py --self-test

Garrett, 2026-11-16: "create a whole new skill that can make the spindles from scratch. deconstruct the current spindles
of the fauna bodies and learn how to make new ones that are beautiful, organic, and performant. then remake these
bodies with bespoke spindles as another hierarchy of the design."

THE HIERARCHY: a creature (specs/<species>.py) is limbs; a limb names a spindle DESIGN (below); a design is how the
wire of that limb is made (how many strands, how they wind, what leaves them, how they are lit). The /spindle skill
holds the method; this file is the part that measures, builds and says no.

WHAT THE SHIPPED SPINDLES ARE (--shipped measures it from the model files): thin tubes with a THREE-sided section
braided in threes (the braid every plant and the tadpole wear: 3 strands x 25 rings x 3 vertices = 225), and flat
RIBBONS two vertices wide woven into loops and nets (the Brittlestar's arms, the Clawfish's flukes, the Shark's vanes).
Nothing has a round section and nothing is solid: that is why a whole Shark is under 3,000 vertices. A design here is
built from the same two parts, a three-sided tube or a ribbon, so it costs what the shipped ones cost.
"""
import math
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import celestial as C  # noqa: E402
import fbx_model as fm  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
# The bar: the heaviest single shipped body is the Brittlestar's (5,072 vertices); a Worm Colony is 7,242 in eight
# bodies. A grown creature's whole mesh stays under the Brittlestar's, and a limb under the heaviest shipped limb mesh.
CREATURE_VERTS = 5072
SHIPPED = {"bonita.fbx": ["BezierCurve.001"], "wormbody.fbx": None, "wormhead.fbx": None, "ClawfishTest.fbx": None,
           "Shark_model.fbx": ["Shark_creature_mesh", "Shark_creature_mesh.001"],
           "Brittlestar_Export.fbx": ["brittlestarmesh_base", "brittlestarmesh_armC1", "brittlestarmesh_armC2"]}

# ------------------------------------------------------------------ the designs
# strands: wires wound round the limb's curve; twist: turns from root to tip; section: 3 = a three-sided tube, 2 = a
# ribbon; step: world units between rings (the mesh's cost); ties: a cross-tie every n rings; wire: a strand's own
# half-width at root and tip; barbs: side filaments (ribbons); glow: brightness root to tip, and pulses along it.
DESIGNS = {
    "core":    {"about": "a body's axis: five strands, slow wind, the brightest wire", "strands": 5, "twist": 1.2, "section": 3, "step": 0.9, "ties": 2, "glow": {"bright": [1.0, 0.8], "waves": 0.6}},
    "spar":    {"about": "a leading edge or an arm: the braid's three strands, tighter", "strands": 3, "twist": 1.6, "section": 3, "step": 0.9},
    "vein":    {"about": "a hind edge, a leaf's rib: two fine ribbons", "strands": 2, "twist": 2.0, "section": 2, "step": 1.1, "glow": {"bright": [0.8, 0.55]}},
    "whip":    {"about": "a tail: two ribbons wound tight, light running to the tip", "strands": 2, "twist": 3.0, "section": 2, "step": 1.3, "glow": {"bright": [0.9, 0.6], "waves": 2.5}},
    "tendril": {"about": "a stinging line: two ribbons and short side lines, pulses falling down it", "strands": 2, "twist": 3.0, "section": 2, "step": 1.4,
                "barbs": {"count": 18, "length": [1.6, 0.5], "angle": 95.0, "sides": 1, "flutter": 0.5}, "glow": {"bright": [0.9, 0.5], "waves": 3.5}},
    "frill":   {"about": "a ruffled arm: five ribbons wound four times, so it reads as lace", "strands": 5, "twist": 4.0, "section": 2, "step": 0.8, "ties": 2},
    "rib":     {"about": "a bell's rib, a cage's bar: two strands, hardly wound", "strands": 2, "twist": 0.6, "section": 3, "step": 0.9},
    "rachis":  {"about": "a feather: a three-strand shaft and barbs on both sides, long at the root", "strands": 3, "twist": 1.0, "section": 3, "step": 1.0,
                "barbs": {"count": 30, "length": [5.0, 1.0], "angle": 58.0, "sides": 2, "dir": "tangent", "flutter": 0.35}, "glow": {"bright": [1.0, 0.6], "waves": 1.5}},
    "comb":    {"about": "a row of beating plates: a fine shaft and short cilia both ways, light running down it", "strands": 2, "twist": 0.4, "section": 2, "step": 1.0,
                "barbs": {"count": 36, "length": [1.5, 1.2], "angle": 88.0, "sides": 2, "dir": "tangent", "flutter": 0.6}, "glow": {"bright": [1.0, 0.9], "waves": 4.0}},
    "shell":   {"about": "a coiled chamber: nine straight ribs hooped every other ring, widening as it grows", "strands": 9, "twist": 0.0, "section": 2, "step": 1.0, "ties": 2,
                "glow": {"bright": [1.0, 0.55], "waves": 5.0}},
    "frond":   {"about": "a leaf's edge: one ribbon with fine teeth on its outer side", "strands": 2, "twist": 1.0, "section": 2, "step": 1.0,
                "barbs": {"count": 10, "length": [1.2, 0.4], "angle": 70.0, "sides": 1, "flutter": 0.4}},
}


def resolve(limb):
    """A limb with its design's parts filled in under its own (the limb's own keys win)."""
    d = DESIGNS.get(limb.get("design") or "")
    if not d:
        return limb
    out = {k: v for k, v in d.items() if k != "about"}
    out.update(limb)
    return out


# ------------------------------------------------------------------ the mesh a limb makes

LACE_SPAN = 12.0      # u of a strand's length that one lap of the lace texture covers (SpindleGraph reads UV0)


def mesh_data(wire):
    """(vertices, triangles, source, uv) of one grown limb as the game draws it: every strand a three-sided tube or a
    ribbon (its design's `section`), every barb a ribbon. `source[i]` is the wire vertex mesh vertex i was swept
    round (its sway and glow are that vertex's), and `uv[i]` is where SpindleGraph's lace lands on it: u runs along
    the strand in LACE_SPANs, v round its faces, as on the shipped braid. `wire` is a body.from_spec wire."""
    V, T, S, U = [], [], [], []
    src = wire["verts"]
    if wire.get("membrane"):      # a membrane is its own mesh: its lattice points and its fill
        across = wire.get("across") or 1
        rows = max(1, len(src) // across)
        run = sum(C.norm(C.sub(src[(i + 1) * across], src[i * across])) for i in range(rows - 1)) if across and rows > 1 else 0.0
        for i, v in enumerate(src):
            U.append([(i // across) / max(1, rows - 1) * run / LACE_SPAN, (i % across) / max(1, across - 1)])
        return [list(v) for v in src], [tuple(t) for t in wire.get("tris", [])], list(range(len(src))), U
    w0, w1 = wire.get("wire", [0.09, 0.04])
    for idx, kind in wire.get("lines", []):
        k = 2 if kind == "barb" else wire.get("section", 3)
        n = len(idx)
        first = len(V)
        run = 0.0
        for j, vi in enumerate(idx):
            p = src[vi]
            a, b = src[idx[max(j - 1, 0)]], src[idx[min(j + 1, n - 1)]]
            if j > 0:
                run += C.norm(C.sub(p, src[idx[j - 1]]))
            t = C.unit(C.sub(b, a))
            ref = [0.0, 1.0, 0.0] if abs(t[1]) < 0.9 else [1.0, 0.0, 0.0]
            n1 = C.unit(C.cross(ref, t))
            n2 = C.cross(t, n1)
            r = (w0 + (w1 - w0) * j / max(1, n - 1)) * (0.6 if kind == "barb" else 1.0)
            for c in range(k):
                ang = 2 * math.pi * c / k
                V.append([p[i] + r * (math.cos(ang) * n1[i] + math.sin(ang) * n2[i]) for i in range(3)])
                S.append(vi)
                U.append([run / LACE_SPAN, c / k])
            if j > 0:
                for c in range(k if k > 2 else 1):
                    a0, a1 = first + (j - 1) * k + c, first + (j - 1) * k + (c + 1) % k
                    b0, b1 = first + j * k + c, first + j * k + (c + 1) % k
                    T += [(a0, a1, b1), (a0, b1, b0)]
    return V, T, S, U


def mesh(wire):
    """(vertices, triangles) of one grown limb as the game would draw it (mesh_data without what rides each vertex)."""
    V, T, _S, _U = mesh_data(wire)
    return V, T


def creature_mesh(key):
    import body as BODY
    made = BODY.from_spec(C.load(key))
    rows = []
    for w in made["wires"]:
        if w.get("source") != "grown":
            continue
        V, T = mesh(w)
        rows.append((w["name"], w.get("design") or ("membrane" if w.get("membrane") else "(own)"), len(V), len(T), V, T))
    return rows


def grown_species():
    out = []
    for f in sorted(os.listdir(os.path.join(HERE, "specs"))):
        if f.endswith(".py") and not f.startswith("_"):
            spec = C.load(f[:-3])
            if any(s.get("mesh") == "grown" for s in spec["spindles"]):
                out.append(f[:-3])
    return out


# ------------------------------------------------------------------ the shipped spindles, taken apart

def shipped_table():
    """[(mesh, vertices, quads, parts, [(welded vertices, quads, ring sides, rings)])] for the shipped wire meshes.
    An open tube of k-sided rings has k more vertices than quads, so k = V - Q: 3 is the braid's strand, 2 a ribbon,
    0 a closed loop, and a negative number a net that branches and rejoins."""
    rows = []
    for f, names in SHIPPED.items():
        m = fm.load(os.path.join(REPO, "Assets", "_Models", "Fauna", f))
        for me in m["meshes"]:
            if names and me["name"] not in names:
                continue
            parts = []
            for comp in sorted(fm.components(me["verts"], me["polys"]), key=len, reverse=True)[:3]:
                polys = [me["polys"][i] for i in comp]
                welded = {(round(me["verts"][i][0], 5), round(me["verts"][i][1], 5), round(me["verts"][i][2], 5)) for p in polys for i in p}
                k = len(welded) - len(polys)
                parts.append((len(welded), len(polys), k, len(polys) // k if k > 0 else 0))
            rows.append((f[:-4] + "#" + me["name"], len(me["verts"]), len(me["polys"]), len(fm.components(me["verts"], me["polys"])), parts))
    return rows


def self_test():
    fails = []
    braid = next(r for r in shipped_table() if r[0].startswith("bonita"))
    if [p[2] for p in braid[4]] != [3, 3, 3] or braid[1] != 225:
        fails.append(f"the shipped braid no longer reads as three 3-sided strands of 225 vertices: {braid}")
    # a design's mesh: a three-strand, three-sided limb of n rings is exactly 9n vertices and 18(n-1) triangles
    import organic
    limb = resolve({"name": "t", "mesh": "grown", "design": "spar", "path": [[0, 0, 0], [0, 0, -9]], "girth": [0.5, 0.2], "step": 1.0})
    verts, edges, _s, _g, lines = organic.tube(limb)
    V, T = mesh({"verts": verts, "lines": lines, "section": 3})
    n = len(verts) // 3
    if (len(V), len(T)) != (9 * n, 18 * (n - 1)):
        fails.append(f"a spar of {n} rings made {len(V)} vertices and {len(T)} triangles, not {9 * n} and {18 * (n - 1)}")
    Vr, Tr = mesh({"verts": verts, "lines": lines, "section": 2})
    if not len(Vr) < len(V):
        fails.append("a ribbon section is not cheaper than a tube")
    if any(len({a, b, c}) < 3 for a, b, c in T):
        fails.append("a degenerate triangle")
    return fails


def main(argv):
    if "--self-test" in argv:
        fails = self_test()
        for f in fails:
            print("FAIL:", f)
        print("spindle self-test:", "ok" if not fails else f"{len(fails)} problem(s)")
        return 1 if fails else 0
    if "--shipped" in argv:
        print(f"{'shipped mesh':<42}{'verts':>6}{'quads':>6}{'parts':>6}  largest parts: welded verts / quads / ring sides (V - Q) / rings")
        for name, v, q, parts, rows in shipped_table():
            print(f"{name:<42}{v:>6}{q:>6}{parts:>6}  " + "   ".join(f"{a}/{b}/k{c}/{d}" for a, b, c, d in rows))
        return 0
    if "--designs" in argv:
        import organic
        print(f"{'design':<9}{'verts':>6}{'tris':>6}  a 20 u limb; the shipped braid is 225 verts, 432 tris at any length")
        for name, d in DESIGNS.items():
            limb = resolve({"name": name, "mesh": "grown", "design": name, "path": [[0, 0, 0], [3, 0, -7], [2, 0, -14], [4, 0, -20]], "girth": [0.6, 0.2]})
            verts, _e, _s, _g, lines = organic.tube(limb)
            V, T = mesh({"verts": verts, "lines": lines, "section": limb.get("section", 3)})
            print(f"{name:<9}{len(V):>6}{len(T):>6}  {d['about']}")
        return 0
    if "--obj" in argv:
        key, out = argv[argv.index("--obj") + 1], argv[argv.index("--obj") + 2]
        os.makedirs(out, exist_ok=True)
        for name, _design, _nv, _nt, V, T in creature_mesh(key):
            with open(os.path.join(out, f"{key}_{name}.obj"), "w") as f:
                f.write(f"# {key} {name}: grown by Tools/Build/fauna_kit/spindle.py (Unity units, +z forward; x negated for Unity's importer)\n")
                f.writelines(f"v {-v[0]:.4f} {v[1]:.4f} {v[2]:.4f}\n" for v in V)
                f.writelines(f"f {a + 1} {c + 1} {b + 1}\n" for a, b, c in T)
        print(f"wrote {len(creature_mesh(key))} meshes to {out}")
        return 0
    bad = 0
    print(f"{'creature':<10}{'limbs':>6}{'verts':>7}{'tris':>7}  heaviest limb (bar: {CREATURE_VERTS} vertices a creature, the Brittlestar's)")
    for key in grown_species():
        rows = creature_mesh(key)
        tv, tt = sum(r[2] for r in rows), sum(r[3] for r in rows)
        top = max(rows, key=lambda r: r[2])
        flag = "" if tv <= CREATURE_VERTS else "  OVER"
        bad += tv > CREATURE_VERTS
        print(f"{key:<10}{len(rows):>6}{tv:>7}{tt:>7}  {top[0]} ({top[1]}) {top[2]}{flag}")
    return 1 if ("--check" in argv and bad) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
