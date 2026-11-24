#!/usr/bin/env python3
"""The fauna DOSSIER: every possessable creature's body and numbers, measured from the shipped assets.

    python3 Tools/Build/fauna_kit/dossier.py                    # the table of every creature
    python3 Tools/Build/fauna_kit/dossier.py shark --sheet <png># one creature, drawn from three sides
    python3 Tools/Build/fauna_kit/dossier.py --self-test        # the readers' own controls

A READER. It is what "learn the fauna" means in numbers: for each creature a rider can take (the Jewel Wasp's hosts,
Fauna.CanBePossessed), the body in the creature's own space at root scale 1 (wires = the meshes its spindles draw,
prisms with pose, size and kind, the heart's seat, the bone chains its attack whips), and the numbers its class reads
(speeds, mouth reach, attack range, per-element body scale). Everything is read from the prefab, the model files and
the config assets (prefab_read.py, fbx_model.py), never from a C# field initializer and never from memory.

The kit builds creatures from specs; recreate.py holds a spec's body against this dossier, which is how the kit
proves it can make the creatures the game already has (Garrett, 2026-11-15: "recreate the existing fauna like you
did when you learned to create better vessels").
"""
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fbx_model as fm  # noqa: E402
import prefab_read as pr  # noqa: E402

REPO = pr.REPO
LIFEFORMS = "Assets/_SO_Assets/Lifeforms"
ELEMENTS = ("Charge", "Mass", "Space", "Time")

# The possessable roster: every FaunaPrefab a config ships whose class does not say CanBePossessed => false
# (builder colonies, substrates, NCA creatures and the managers do). The swarm is a population of drawn members, not
# a prefab body: the lab models it as a murmuration of tadpole members (SwarmFauna.PossessionHost).
ROSTER = {
    "tadpole":     {"name": "Tadpole", "prefab": "Assets/_Prefabs/FloraAndFauna/TadPoleFauna.prefab", "klass": "Boid", "configs": "Tadpole Fauna"},
    "quadfish":    {"name": "QuadFish", "prefab": "Assets/_Prefabs/FloraAndFauna/QuadFish.prefab", "klass": "LightFauna", "configs": "QuadFish Fauna"},
    "clawfish":    {"name": "Clawfish", "prefab": "Assets/_Prefabs/FloraAndFauna/Clawfish.prefab", "klass": "LightFauna", "configs": "Clawfish Fauna"},
    "shark":       {"name": "Shark", "prefab": "Assets/_Models/Fauna/MassSharkFauna.prefab", "klass": "LightFauna", "configs": "Shark Fauna"},
    "brittlestar": {"name": "Brittlestar", "prefab": "Assets/_Models/Fauna/MassBrittlestarFauna.prefab", "klass": "LightFauna", "configs": "Brittlestar Fauna"},
    "bee":         {"name": "Bee", "prefab": "Assets/_Prefabs/FloraAndFauna/Bees/BeeFauna.prefab", "klass": "BeeFauna", "configs": "Bee Fauna"},
    "worm":        {"name": "Worm Colony", "prefab": "Assets/_Prefabs/FloraAndFauna/WormColony.prefab", "klass": "WormFauna", "configs": "Worm Colony"},
}
DIETS = {0: "herbivore", 1: "predator", 2: "pollinator"}


# ------------------------------------------------------------------ helpers

def _field(body, key, default=None):
    m = re.search(r"^\s*%s: (.*)$" % re.escape(key), body, re.M)
    if not m:
        return default
    v = m.group(1).strip()
    try:
        return float(v)
    except ValueError:
        return v


def _asset(path):
    return open(os.path.join(REPO, path), errors="replace").read()


def _rot_of(world):
    """The rotation part of a world matrix as a quaternion (scale divided out)."""
    cols = [[world[i][j] for i in range(3)] for j in range(3)]
    for c in cols:
        n = math.sqrt(sum(v * v for v in c)) or 1.0
        c[:] = [v / n for v in c]
    m = [[cols[j][i] for j in range(3)] for i in range(3)]
    tr = m[0][0] + m[1][1] + m[2][2]
    if tr > 0:
        s = math.sqrt(tr + 1.0) * 2
        q = [(m[2][1] - m[1][2]) / s, (m[0][2] - m[2][0]) / s, (m[1][0] - m[0][1]) / s, 0.25 * s]
    elif m[0][0] > m[1][1] and m[0][0] > m[2][2]:
        s = math.sqrt(1.0 + m[0][0] - m[1][1] - m[2][2]) * 2
        q = [0.25 * s, (m[0][1] + m[1][0]) / s, (m[0][2] + m[2][0]) / s, (m[2][1] - m[1][2]) / s]
    elif m[1][1] > m[2][2]:
        s = math.sqrt(1.0 + m[1][1] - m[0][0] - m[2][2]) * 2
        q = [(m[0][1] + m[1][0]) / s, 0.25 * s, (m[1][2] + m[2][1]) / s, (m[0][2] - m[2][0]) / s]
    else:
        s = math.sqrt(1.0 + m[2][2] - m[0][0] - m[1][1]) * 2
        q = [(m[0][2] + m[2][0]) / s, (m[1][2] + m[2][1]) / s, 0.25 * s, (m[1][0] - m[0][1]) / s]
    if q[3] < 0:
        q = [-v for v in q]
    n = math.sqrt(sum(v * v for v in q)) or 1.0   # a sheared parent (the QuadFish's stretched spindle) leaves it non-unit
    return [round(v / n, 6) for v in q]


def _under(n, script_names):
    p = n.parent
    while p is not None:
        if any(s in p.scripts for s in script_names):
            return True
        p = p.parent
    return False


def _limb(n):
    """The nearest ancestor that is a limb: a Spindle or a model bone."""
    p = n.parent
    while p is not None:
        if "Spindle" in p.scripts or p.bone:
            return p
        p = p.parent
    return None


_FBX_CACHE = {}


def _fbx(path):
    if path not in _FBX_CACHE:
        _FBX_CACHE[path] = fm.load(os.path.join(REPO, path))
    return _FBX_CACHE[path]


def _native_mesh(path):
    """(verts, tris, source) of a native Unity Mesh asset: positions are the first three floats of each vertex, the
    index buffer is 16-bit triangles. Unity's own space already (no handedness flip: it is not an imported model)."""
    import struct
    t = _asset(path)
    if "\nMesh:" not in t:
        return None
    nv = int(re.search(r"m_VertexCount: (\d+)", t).group(1))
    blob = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]*)", t).group(1))
    stride = len(blob) // max(nv, 1)
    verts = [list(struct.unpack_from("<3f", blob, i * stride)) for i in range(nv)]
    ib = bytes.fromhex(re.search(r"m_IndexBuffer: ([0-9a-f]*)", t).group(1))
    idx = struct.unpack("<%dH" % (len(ib) // 2), ib)
    return verts, [tuple(idx[i:i + 3]) for i in range(0, len(idx), 3)], path + "#mesh"


def _mesh_of(n):
    """The mesh a node draws, when it comes from a model file: (verts, tris, source) in the node's local space."""
    if n.mesh and str(n.mesh[0]).lower().endswith(".asset"):      # a native Mesh asset (unity_port.py writes these)
        return _native_mesh(n.mesh[0])
    if not n.mesh or not str(n.mesh[0]).lower().endswith(".fbx"):
        return None
    model = _fbx(n.mesh[0])
    ref = n.mesh[1]
    if isinstance(ref, str):
        me = next((m for m in model["meshes"] if m["name"] == ref), None)
    else:
        me = fm.mesh_by_file_id(model, ref)
    if me is None:
        return None
    return me["verts"], me["tris"], f"{n.mesh[0]}#{me['name']}"


def attack_limbs(root):
    """FaunaAttackDriver.Scan on the flattened tree: the HUB is the node with the most children that are chains (a
    child with a child and no renderer of its own), three at least; each such child, followed down its FIRST child,
    is a limb (12 joints at most). None = the body lunges. Returns [[node, ...], ...]."""
    def is_chain(c):
        return bool(c.children) and c.mesh is None
    hub, best = None, 0
    for n in root.walk():
        if n is root:
            continue
        k = sum(1 for c in n.children if is_chain(c))
        if k > best:
            hub, best = n, k
    if hub is None or best < 3:
        return []
    limbs = []
    for c in hub.children:
        if not is_chain(c):
            continue
        chain, j = [], c
        while j is not None and len(chain) < 12:
            chain.append(j)
            j = j.children[0] if j.children else None
        if len(chain) >= 2:
            limbs.append(chain)
    return limbs


def body_of(prefab, root_identity=True):
    """A prefab's body in its own space at root scale 1: wires, prisms, the heart, bone chains."""
    root = pr.pose(pr.resolve(prefab), include_root=not root_identity)
    wires, prisms, bones, spindles = [], [], [], []
    heart = None
    unresolved = []
    for n in root.walk():
        if n.unresolved_parent:
            unresolved.append(n.name)
        if "Crystal" in n.scripts and heart is None and not _under(n, ("HealthPrism",)):
            heart = {"name": n.name, "at": [round(v, 5) for v in pr.world_pos(n)],
                     "scale": round(max(pr.world_scale(n)), 5), "source": n.source}
            continue
        if _under(n, ("Crystal", "HealthPrism")) or not n.active:
            continue
        if "HealthPrism" in n.scripts:
            src = os.path.basename(n.source or "")
            body = n.fields.get("HealthPrism", "")
            danger = "Danger" in src or n.overrides.get("prismProperties.IsDangerous") == "1"
            shielded = "Shielded" in src
            if "prismProperties.IsShielded" in n.overrides:
                shielded = n.overrides["prismProperties.IsShielded"] == "1"
            limb = _limb(n)
            prisms.append({"name": n.name, "kind": "danger" if danger else "health", "shielded": shielded,
                           "at": [round(v, 5) for v in pr.world_pos(n)], "q": _rot_of(n.world),
                           "size": [round(v, 5) for v in pr.world_scale(n)],
                           "limb": limb.name if limb else None, "placed_by": n.inferred_parent or "document",
                           "block": os.path.splitext(src)[0], "_has_body": bool(body)})
            continue
        if "Spindle" in n.scripts:
            spindles.append(n.name)
        if n.bone:
            bones.append({"name": n.name, "parent": n.parent.name if n.parent and n.parent.bone else None,
                          "at": [round(v, 5) for v in pr.world_pos(n)]})
        got = _mesh_of(n)
        if got:
            verts, tris, source = got
            limb = n if "Spindle" in n.scripts else _limb(n)
            wires.append({"name": n.name, "source": source, "limb": limb.name if limb else None,
                          "verts": [[round(c, 5) for c in fm.mpoint(n.world, v)] for v in verts], "tris": tris,
                          "local": [[round(c, 6) for c in row] for row in n.world[:3]]})
    chains = [{"joints": [j.name for j in limb], "at": [[round(v, 5) for v in pr.world_pos(j)] for j in limb]}
              for limb in attack_limbs(root)]
    return {"root_scale": round(max(abs(v) for v in pr.resolve(prefab).s), 6), "wires": wires, "prisms": prisms,
            "heart": heart, "bones": bones, "chains": chains, "spindles": spindles, "unresolved": unresolved}


def extent(body):
    pts = [v for w in body["wires"] for v in w["verts"]]
    for p in body["prisms"]:
        h = max(p["size"]) / 2
        pts += [[p["at"][0] + h, p["at"][1] + h, p["at"][2] + h], [p["at"][0] - h, p["at"][1] - h, p["at"][2] - h]]
    if not pts:
        return [0, 0, 0], [0, 0, 0]
    return fm.bounds(pts)


# ------------------------------------------------------------------ the numbers each class reads

def _variants(configs):
    out = {}
    for e in ELEMENTS:
        path = f"{LIFEFORMS}/{configs} {e}.asset"
        if not os.path.exists(os.path.join(REPO, path)):
            continue
        t = _asset(path)
        out[e] = {"asset": path, "body_scale": _field(t, "BaseBodyScale", 0.0) or 0.0,
                  "min_speed": _field(t, "MinSpeed", -1.0), "max_speed": _field(t, "MaxSpeed", -1.0),
                  "erode_spindle": _field(t, "ErodeSpindle", 0.0) == 1.0,
                  "starvation_seconds": _field(t, "StarvationSeconds", -1.0)}
    return out


def measure(key):
    """One creature's dossier (a dict that json.dumps takes)."""
    row = ROSTER[key]
    guids = pr.guid_index()
    root = pr.resolve(row["prefab"])
    out = {"id": key, "name": row["name"], "prefab": row["prefab"], "klass": row["klass"], "variants": _variants(row["configs"])}
    if row["klass"] == "WormFauna":
        body = root.fields["WormFauna"]
        cfg_path = guids[re.search(r"config: \{fileID: \d+, guid: (\w+)", body).group(1)]
        cfg = _asset(cfg_path)
        f = lambda k: _field(cfg, k)
        out["numbers"] = {"asset": cfg_path, "cruise_speed": f("CruiseSpeed"), "pursuit_speed_multiplier": f("PursuitSpeedMultiplier"),
                          "turn_degrees_per_second": f("TurnDegreesPerSecond"), "pursuit_turn_multiplier": f("PursuitTurnMultiplier"),
                          "mouth_radius": f("MouthRadius"), "fauna_bite_range": f("FaunaBiteRange"), "lunge_speed": f("LungeSpeed"),
                          "lunge_max_seconds": f("LungeMaxSeconds"), "kaiju_scale": f("KaijuScale"), "segment_spacing": f("SegmentSpacing"),
                          "taper_per_segment": f("TaperPerSegment"), "head_gap_multiplier": f("HeadGapMultiplier"),
                          "tail_gap_multiplier": f("TailGapMultiplier"), "spawn_segment_count": f("SpawnSegmentCount"),
                          "follow_sharpness": f("FollowSharpness"), "undulation_yaw_degrees": f("UndulationYawDegrees"),
                          "undulation_frequency": f("UndulationFrequency"), "undulation_phase_step": f("UndulationPhaseStep")}
        out["diet"] = DIETS.get(int(_field(body, "diet", 1)), "?")
        out["segments"] = {}
        for role in ("head", "body", "tail"):
            seg = guids[re.search(r"%sPrefab: \{fileID: \d+, guid: (\w+)" % role, body).group(1)]
            out["segments"][role] = dict(body_of(seg), prefab=seg)
        n = out["numbers"]
        # PossessionTopSpeed, PossessionMouthReach (WormFauna.cs)
        out["possession"] = {"top_speed": n["cruise_speed"] * max(1.0, n["pursuit_speed_multiplier"]),
                             "mouth_reach": max(n["mouth_radius"], n["fauna_bite_range"], 6.0), "attack": "lunge (the worm's own)"}
        return out
    body = body_of(row["prefab"])
    out.update(body)
    comp = root.fields[row["klass"]]
    out["diet"] = DIETS.get(int(_field(comp, "diet", 0)), "?")
    if row["klass"] == "LightFauna":
        data_path = guids[re.search(r"data: \{fileID: \d+, guid: (\w+)", comp).group(1)]
        d = _asset(data_path)
        f = lambda k, dv: _field(d, k, dv)
        # defaults are LightFaunaDataSO's initialisers, used only where the asset omits the key
        out["numbers"] = {"asset": data_path, "min_speed": f("minSpeed", 3.0), "max_speed": f("maxSpeed", 6.0),
                          "consume_radius": f("consumeRadius", 40.0), "feeding_cluster_radius": f("feedingClusterRadius", 12.0),
                          "attack_range": f("attackRange", 15.0), "rotation_lerp_speed": f("rotationLerpSpeed", 5.0),
                          "pursuit_speed_multiplier": f("pursuitSpeedMultiplier", 1.5), "shield_bite": f("shieldBite", 0.0)}
        n = out["numbers"]
        # LightFauna.PossessionTopSpeed = MaxSpeed; PossessionMouthReach = max(ConsumeRadius, FeedingClusterRadius, AttackRange, 6)
        out["possession"] = {"top_speed": n["max_speed"],
                             "mouth_reach": max(n["consume_radius"], n["feeding_cluster_radius"], n["attack_range"], 6.0),
                             "attack": "whip (rig)" if body["chains"] else "lunge (FaunaAttackDriver)"}
    else:   # Boid and its subclasses read their numbers off the prefab's own component
        f = lambda k, dv: _field(comp, k, dv)
        out["numbers"] = {"asset": row["prefab"], "min_speed": f("minSpeed", 0.0), "max_speed": f("maxSpeed", 0.0),
                          "trail_block_interaction_radius": f("trailBlockInteractionRadius", 0.0),
                          "feeding_cluster_radius": f("feedingClusterRadius", 10.0), "shield_bite": f("shieldBite", 0.0)}
        n = out["numbers"]
        # Boid.PossessionTopSpeed = maxSpeed; PossessionMouthReach = max(base, FeedReach, feedingClusterRadius), where
        # FeedReach = trailBlockInteractionRadius x ElementReach (1 off-element) and base = max(body radius x 2, 6)
        out["possession"] = {"top_speed": n["max_speed"],
                             "mouth_reach": max(n["trail_block_interaction_radius"], n["feeding_cluster_radius"], 6.0),
                             "attack": "lunge (FaunaAttackDriver)"}
    return out


# ------------------------------------------------------------------ the sheet

def _edges(tris):
    seen = set()
    for a, b, c in tris:
        for e in ((a, b), (b, c), (c, a)):
            seen.add((min(e), max(e)))
    return seen


def sheet(bodies, out_path, title, size=560):
    """Three orthographic views of one or more bodies ((label, body) pairs): from the side, from above, from ahead."""
    from PIL import Image, ImageDraw
    views = [("side (z right, y up)", lambda p: (p[2], p[1])), ("above (z right, x up)", lambda p: (p[2], p[0])),
             ("ahead (x right, y up)", lambda p: (p[0], p[1]))]
    img = Image.new("RGB", (size * 3, (size + 26) * len(bodies) + 30), (14, 18, 30))
    d = ImageDraw.Draw(img)
    d.text((10, 8), title, fill=(210, 215, 225))
    for bi, (label, body) in enumerate(bodies):
        lo, hi = extent(body)
        span = max(hi[i] - lo[i] for i in range(3)) or 1.0
        mid = [(lo[i] + hi[i]) / 2 for i in range(3)]
        k = (size - 40) / span
        y0 = 30 + bi * (size + 26)
        for vi, (vname, proj) in enumerate(views):
            ox, oy = vi * size + size / 2, y0 + 20 + size / 2
            cx, cy = proj(mid)

            def P(p):
                x, y = proj(p)
                return (ox + (x - cx) * k, oy - (y - cy) * k)
            d.text((vi * size + 8, y0 + 2), f"{label}: {vname}", fill=(150, 160, 180))
            for w in body["wires"]:
                vs = w["verts"]
                for a, b in _edges(w["tris"]):
                    d.line([P(vs[a]), P(vs[b])], fill=(90, 120, 170), width=1)
            for p in body["prisms"]:
                m = fm.trs(p["at"], p["q"], [s / 2 for s in p["size"]])
                c = [fm.mpoint(m, [sx, sy, sz]) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
                col = (255, 80, 100) if p["kind"] == "danger" else ((120, 255, 190) if p.get("shielded") else (235, 240, 250))
                for i in range(8):
                    for j in range(i + 1, 8):
                        if bin(i ^ j).count("1") == 1:
                            d.line([P(c[i]), P(c[j])], fill=col, width=2)
            if body.get("heart"):
                h = body["heart"]
                x, y = P(h["at"])
                r = max(3.0, 0.5 * k * 3.55 / max(body.get("root_scale", 1.0), 1e-6))
                d.ellipse([x - r, y - r, x + r, y + r], outline=(90, 230, 255), width=2)
    img.save(out_path)
    return out_path


# ------------------------------------------------------------------ the self-test

def self_test():
    fails = []
    if fm.xxh64(b"") != 0xEF46DB3751D8E999:
        fails.append("xxh64 known answer")
    # 1 the mesh fileID rule reproduces two shipped ids, and a wrong name does not
    if fm.mesh_file_id("BezierCurve") != 150581138598605843:
        fails.append("mesh fileID: mediumfish BezierCurve")
    if fm.mesh_file_id("Shark_creature_mesh") != -5517701702760522128:
        fails.append("mesh fileID: Shark_creature_mesh")
    if fm.mesh_file_id("BezierCurve.001") == 150581138598605843:
        fails.append("mesh fileID: the rule cannot fail")
    # 2 the model reader's bone poses are the shark prefab's own (an unpacked copy of Shark_model.fbx)
    root = pr.resolve("Assets/_Models/Fauna/MassSharkFauna.prefab")
    model = _fbx("Assets/_Models/Fauna/Shark_model.fbx")
    checked = 0
    worst = 0.0
    for n in root.walk():
        f = fm.node_by_name(model, n.name)
        if not f or "HealthPrism" in n.scripts or n.name == "Shark_model":
            continue
        checked += 1
        dq = min(sum((a - b) ** 2 for a, b in zip(n.q, f["q"])), sum((a + b) ** 2 for a, b in zip(n.q, f["q"]))) ** 0.5
        dt = sum((a - b) ** 2 for a, b in zip(n.t, f["t"])) ** 0.5
        worst = max(worst, dq, dt)
    if checked < 15:
        fails.append(f"shark bone check examined {checked} nodes (17 expected)")
    if worst > 2e-4:
        fails.append(f"shark: the model reader is {worst:.5f} off the prefab's own bone poses")
    # negative control: without the axis rule the same check must fail
    plain = fm.euler_xyz_to_quat(0, 0, 180)
    arm = next(n for n in root.walk() if n.name == "Armature")
    if sum((a - b) ** 2 for a, b in zip(arm.q, (plain[0], -plain[1], -plain[2], plain[3]))) ** 0.5 < 0.1:
        fails.append("the axis rule's control: the Armature reads the same with and without it")
    # 3 every roster creature measures with a body, a heart and no unresolved parent
    for key in ROSTER:
        d = measure(key)
        bodies = list(d["segments"].values()) if "segments" in d else [d]
        for b in bodies:
            if not b["prisms"]:
                fails.append(f"{key}: no body prisms measured")
            if not b["wires"]:
                fails.append(f"{key}: no wires measured")
            if b["unresolved"]:
                fails.append(f"{key}: parts with a parent this reader could not place: {b['unresolved']}")
        if "segments" not in d and not d["heart"]:
            fails.append(f"{key}: no heart measured")
        if len(d["variants"]) != 4:
            fails.append(f"{key}: {len(d['variants'])} element configs found (4 expected)")
    return fails


def main(argv):
    if "--self-test" in argv:
        fails = self_test()
        for f in fails:
            print("FAIL:", f)
        print("dossier self-test:", "ok" if not fails else f"{len(fails)} problem(s)")
        return 1 if fails else 0
    names = [a for a in argv if not a.startswith("-") and a in ROSTER] or list(ROSTER)
    out_json = argv[argv.index("--json") + 1] if "--json" in argv else None
    sheet_path = argv[argv.index("--sheet") + 1] if "--sheet" in argv else None
    all_d = {}
    print(f"{'creature':<12} {'class':<10} {'diet':<10} {'root':>6} {'wires':>5} {'verts':>6} {'health':>6} {'danger':>6} "
          f"{'top u/s':>7} {'reach':>6}  attack")
    for key in names:
        d = measure(key)
        all_d[key] = d
        bodies = list(d["segments"].values()) if "segments" in d else [d]
        wires = sum(len(b["wires"]) for b in bodies)
        verts = sum(len(w["verts"]) for b in bodies for w in b["wires"])
        health = sum(1 for b in bodies for p in b["prisms"] if p["kind"] == "health")
        danger = sum(1 for b in bodies for p in b["prisms"] if p["kind"] == "danger")
        rs = d.get("root_scale", d.get("numbers", {}).get("kaiju_scale", 1.0))
        print(f"{d['name']:<12} {d['klass']:<10} {d['diet']:<10} {rs:>6.3f} {wires:>5} {verts:>6} {health:>6} {danger:>6} "
              f"{d['possession']['top_speed']:>7.1f} {d['possession']['mouth_reach']:>6.1f}  {d['possession']['attack']}")
    if sheet_path:
        pairs = []
        for key in names:
            d = all_d[key]
            if "segments" in d:
                pairs += [(f"{d['name']} {r}", b) for r, b in d["segments"].items()]
            else:
                pairs.append((d["name"], d))
        print("sheet:", sheet(pairs, sheet_path, "fauna dossier: measured from the shipped prefabs and models"))
    if out_json:
        json.dump(all_d, open(out_json, "w"))
        print("json:", out_json, os.path.getsize(out_json) // 1024, "KB")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
