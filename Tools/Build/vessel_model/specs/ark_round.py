"""
ark_round - the ROUND Ark, the Ark every voyage draws since 2026-11-25 (Shombith: "Lets build this ark model in the game
and replace it in the voyages as well. Trail 3 is the one I selected."). Concept D, round 13 of the round-Ark concept
artifact (https://claude.ai/artifact/DhVb8uuZekpv6yj1KRcuNM; Docs/VesselModels/ArkRound.md has the rounds and the picks).
It replaces ArkReverse (`ark_reverse.py`, Garrett's clam valve), which stays in the repo, unused by the voyages.

THE GEOMETRY IS THE ARTIFACT'S OWN. `ark_round_geometry.js` is the artifact's geometry code, extracted verbatim, and this
spec runs it through node and turns what it draws into the model: every surface polygon becomes the mesh (in the four
slots), every prism block becomes a SOCKET in the rig table (the game lays a live prism of that tier there), and every
part the artifact turns becomes a bone the game turns. Nothing is re-modelled by hand, so the game shows the shape the
team looked at. The one difference is PRISM_CELL: the game paves the hull with fewer, longer blocks (~580 sockets, not
the page's 1,985), because every hull prism is a live prism the Ark re-files every frame (ArkReverse carried 108).

THE SHIP (the artifact's frame: x the nose axis, y up; the round Ark is radially symmetric about x):
  the mouth (bow)   a solid back wall, a stack of four decks paved with prisms, a glass dome at the nose, two ring gates
                    of prism slabs that turn against each other (gate.0 at the lip, gate.1 behind), danger at the lip
  the gap           open water between the mouth's wall and the hull's head, crossed by three glass walkways; the omni
                    crystal floats in the middle in its collar (omni.ring / omni). The model carries no crystal: the game
                    puts the real omni crystal on the `omni` bone, in the colour set's neutral lime (never a team colour)
  the hull          the hull's head (the walkways land on it), five decks on the tube, each turning against the next
                    (deck.0..4, alternating), the tube showing between them; three long pods on cables orbiting outside
                    (pods); the stern cap, wider than the middle
  the tube          hollow from its lip in the gap to the junction hall at the stern; three straight exits run out of the
                    hall through the stern (hangar.exit.0..2), the squad's launch tubes (hangar.slot.0..2 inside them)
  the booster       a skirt, a big bell and seven round it (engine.0 the big one, engine.1..7), each vectoring a little
                    with the turn (the takes) and leaving its own stream in the domain's colour (Trail 3; the game draws
                    the streams from the rig table's `trails`)
Every prism is the game's block prism. Super-shielded lead (the front of every deck run, the pods' leading ends, the
gates), shielded form the gates' belts, normal prisms are the largest share, danger only at the mouth's lip and the
stern. Prisms never turn on their own: they ride the decks, the pods and the gates (rig table `spins`).

Contract (FLEET_CONVENTION sections 1-4 for a NEW body; the Ark's own contract is ArkModel / ArkRigTable):
  file      Assets/_Models/Vessel Models/ArkRound.fbx, preset "zup"
  frame     Blender: nose -Y, up +Z, pilot's left +X. Unity (x, y, z) = Blender (-x, z, -y): NOSE = Unity +Z.
            artifact (ax, ay, az) -> Blender (-az, -ax, ay) * S, S so that nose to jets is 196 (as ArkReverse).
  nodes     mesh `mesh` (identity, metres), armature `rig`; root bone `hull`
  slots     0 Domain (the trim, in the domain), 1 Body (the black hull), 2 Window (glass), 3 Engine (the bells' throats)
  takes     rest (first); pitchup, pitchdown, yawleft, yawright, rollleft, rollright: the eight bells vector
  table     ArkRound.rig.json: sockets, the bells' takes, spins, trails, the reactor's axis and the crystal's radius
"""

import json
import math
import os
import subprocess

NAME = "ArkRound"
OUTPUT = "Assets/_Models/Vessel Models/ArkRound.fbx"
ARMATURE = "rig"
MESH_NODE = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")

NOSE_TO_JETS = 196.0      # as ArkReverse: half a standard nucleus radius (the voyages' framing was tuned to it)
PRISM_CELL = 2.2          # the game's paving: 1 reproduces the artifact's 1,985 blocks exactly; 2.2 lays ~580
PITCH, YAW = 6.0, 6.0     # how far a bell vectors in a full take, degrees (6 keeps every bell clear of its neighbours)

# The artifact's surfaces -> the model's four slots. The page draws hull, panel, metal and dark as four shades of black
# and the trim in the domain's colour; the game has one Body material and the domain's ship material.
SURFACE_SLOT = {"trim": 0, "glow": 0, "hull": 1, "panel": 1, "truss": 1, "metal": 1, "shell": 1, "dark": 1, "gold": 1,
                "glass": 2, "engine": 3}
TIER = {"prism": ("prism", "Plain"), "shield": ("shielded", "Shielded"), "super": ("supershielded", "SuperShielded"),
        "danger": ("danger", "Danger")}
OCTA_BLOCK = 0.7          # a stern danger spike (an octahedron on the page) is laid as the block 0.7 of its extents
SLOT_SIDE = ("Port", "Starboard", "Top")   # hangar.slot / hangar.exit index = the formation slot (ArkFormation.Slot)


def geometry(pc=PRISM_CELL):
    """Run the artifact's geometry through node: the polygons, prisms, turning parts and constants (the artifact frame)."""
    js = os.path.join(os.path.dirname(os.path.abspath(__file__)), "ark_round_geometry.js")
    out = subprocess.run(["node", js, repr(pc)], capture_output=True, check=True, text=True).stdout
    return json.loads(out)


def build(ctx):
    import bpy
    from mathutils import Vector, Matrix
    from _common import box_uv
    f, rig, takes = ctx.forms, ctx.rig, ctx.takes

    g = geometry()
    K = g["consts"]
    XB, CX, STERN = K["XB"], K["CX"], K["STERN"]
    BELL_BIG, BELL_RING, BELL_R, BELL_L, BIG_L = 6.6, 13.2, 4.0, 9.0, 12.0   # booster(): bell(c, rr, L, n)
    flat, mats = g["polys"], g["mats"]
    nose_x, i = -1e9, 0                              # the front gate's slabs; the jets end at the big bell's mouth
    while i < len(flat):
        n = int(flat[i + 2])
        nose_x = max(nose_x, max(flat[i + 3 + 3 * k] for k in range(n)))
        i += 3 + 3 * n
    S = NOSE_TO_JETS / (nose_x - (XB - BIG_L))
    XC = 0.5 * (nose_x + XB - BIG_L)                 # the model's origin: halfway from the nose to the jets (as ArkReverse)

    def P(a):
        """artifact point -> Blender (nose -Y, up +Z, pilot's left +X), scaled, centred nose to jets."""
        return Vector((-a[2] * S, -(a[0] - XC) * S, a[1] * S))

    def D(a):
        return Vector((-a[2], -a[0], a[1]))

    # ---------------------------------------------------------------- which part (bone) each polygon belongs to
    motions = g["motions"]
    polys = []                                       # (motion, mat name, [artifact points])
    i = 0
    while i < len(flat):
        m, mt, n = int(flat[i]), mats[int(flat[i + 1])], int(flat[i + 2])
        pts = [flat[i + 3 + 3 * k: i + 6 + 3 * k] for k in range(n)]
        polys.append((m, mt, pts))
        i += 3 + 3 * n
    # a turning part's name, from what it carries: the gates (+-0.14) by where they sit, the decks (+-0.1) bow to stern,
    # the pods (-0.12)
    sums = {}
    for m, mt, pts in polys:
        if m >= 0:
            s = sums.setdefault(m, [0.0, 0])
            s[0] += sum(p[0] for p in pts) / len(pts)
            s[1] += 1
    mean_x = {m: s[0] / s[1] for m, s in sums.items()}
    for p in g["prisms"]:
        if p["motion"] >= 0 and p["motion"] not in mean_x:
            mean_x[p["motion"]] = p["c"][0]
    gates = sorted([m for m in mean_x if abs(abs(motions[m]["speed"]) - 0.14) < 1e-6], key=lambda m: -mean_x[m])
    decks = sorted([m for m in mean_x if abs(abs(motions[m]["speed"]) - 0.10) < 1e-6], key=lambda m: -mean_x[m])
    pods = [m for m in mean_x if abs(motions[m]["speed"] + 0.12) < 1e-6]
    assert len(gates) == 2 and len(decks) == 5 and len(pods) == 1, (gates, decks, pods, motions)
    bone_of_motion = {gates[0]: "gate.0", gates[1]: "gate.1", pods[0]: "pods"}
    for k, m in enumerate(decks):
        bone_of_motion[m] = "deck.%d" % k

    bells = [("engine.0", [XB, 0.0, 0.0], BELL_BIG, BIG_L)]
    for k in range(7):
        t = (k + 0.5) * 2 * math.pi / 7
        bells.append(("engine.%d" % (k + 1), [XB, BELL_RING * math.cos(t), BELL_RING * math.sin(t)], BELL_R, BELL_L))

    def static_part(mt, pts):
        cx = sum(p[0] for p in pts) / len(pts)
        cy = sum(p[1] for p in pts) / len(pts)
        cz = sum(p[2] for p in pts) / len(pts)
        if cx < XB - 0.01:                            # aft of the booster's face: a bell
            for nm, c, rr, ln in bells:
                if math.hypot(cy - c[1], cz - c[2]) < rr + 0.8:
                    return nm
        if mt == "panel" and abs(cx - CX) < 0.9 and 7.4 < math.hypot(cy, cz) < 9.4:
            return "omni.ring"                        # the crystal's collar (gap(): torusX(CX, 8.4, 0.7))
        return "hull"

    parts = {}                                        # bone -> (verts, faces, slots)
    skipped = {}
    for m, mt, pts in polys:
        if mt not in SURFACE_SLOT:
            skipped[mt] = skipped.get(mt, 0) + 1
            continue
        bone = bone_of_motion[m] if m >= 0 else static_part(mt, pts)
        vs, fs, ss = parts.setdefault(bone, ([], [], []))
        base = len(vs)
        vs.extend(tuple(P(p)) for p in pts)
        fs.append(tuple(range(base, base + len(pts))))
        ss.append(SURFACE_SLOT[mt])
    assert not skipped, "surfaces with no slot: %s" % skipped

    objs = []
    for bone, (vs, fs, ss) in sorted(parts.items()):
        me = bpy.data.meshes.new(bone)
        me.from_pydata(vs, [], fs)
        me.update()
        ob = bpy.data.objects.new(bone, me)
        bpy.context.collection.objects.link(ob)
        f.ensure_slots(ob, SLOTS)
        me.polygons.foreach_set("material_index", ss)
        # the page's polygons each carry their own corners: weld them, keep every face flat (the page's faceted look)
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4 * S)
        bm.to_mesh(me)
        bm.free()
        f.set_smooth(ob, False)
        f.tag_group(ob, bone)
        objs.append(ob)
    mesh = f.join(objs, MESH_NODE, SLOTS)
    mesh.data.name = MESH_NODE
    f.set_smooth(mesh, False)
    box_uv(mesh)
    ctx.log("ark round: %d surface polygons, %d verts, %d tris, parts %s" % (
        len(polys), len(mesh.data.vertices), f.tri_count(mesh), sorted(parts)))

    # ---------------------------------------------------------------- the rig
    B = rig.BoneSpec
    ax = D([1, 0, 0])                                 # the nose axis in Blender (-Y)
    bones = [B("hull", (0, 0, 0), (0, -20.0, 0))]
    for m, nm in sorted(bone_of_motion.items(), key=lambda kv: kv[1]):
        h = P([mean_x[m], 0, 0])
        bones.append(B(nm, tuple(h), tuple(h + ax * 8.0), parent="hull"))
    for nm, c, rr, ln in bells:
        h = P(c)
        bones.append(B(nm, tuple(h), tuple(P([c[0] - ln, c[1], c[2]])), parent="hull"))
    om = g["omni"][0]
    oc = P(om["c"])
    bones.append(B("omni.ring", tuple(oc), tuple(oc + ax * 8.0), parent="hull"))
    bones.append(B("omni", tuple(oc), tuple(oc + Vector((0, 0, 10.0))), parent="omni.ring"))
    # the launch tubes: the stern's three exits, one per formation slot (port and starboard low, top up)
    exits = {}
    for b in K["BRANCH"]:
        e = P(b["E"])
        side = 2 if e.z > 0 and abs(e.x) < 1e-3 * S else (1 if e.x < 0 else 0)   # Blender -X is the pilot's right
        exits[side] = b
    assert sorted(exits) == [0, 1, 2], exits
    JC = K["JC"]
    pads = {}
    for s, b in sorted(exits.items()):
        d = D(b["D"])
        pad = P([JC[k] + b["D"][k] * b["L"] * 0.55 for k in range(3)])
        e = P(b["E"])
        pads[s] = (pad, e, d)
        bones.append(B("hangar.slot.%d" % s, tuple(pad), tuple(pad + d * 6.0), parent="hull"))
        bones.append(B("hangar.exit.%d" % s, tuple(e), tuple(e + d * 6.0), parent="hull"))
    arm = rig.build_armature(ARMATURE, bones)
    rig.skin_rigid(mesh, arm, default_bone="hull")
    bone_names = [b.name for b in arm.data.bones]

    # ---------------------------------------------------------------- takes: the bells vector with the turn
    # world degrees in the Blender frame (ark_reverse's convention): +X turns a point aft of the pivot UP (a nozzle tilts
    # up); +Z swings it toward -X, the pilot's right. Thrust pushes the stern opposite the exhaust, so the nose turns
    # toward where the nozzles point. For a roll the bells on the right tilt up and those on the left down.
    jets = [b[0] for b in bells]
    lat = {nm: P(c).x / (BELL_RING * S) for nm, c, rr, ln in bells}   # +1 far left .. -1 far right, 0 the big one
    poses = {
        "pitchup": {e: (PITCH, 0, 0) for e in jets},
        "pitchdown": {e: (-PITCH, 0, 0) for e in jets},
        "yawright": {e: (0, 0, YAW) for e in jets},
        "yawleft": {e: (0, 0, -YAW) for e in jets},
        "rollright": {e: (-PITCH * lat[e], 0, 0) for e in jets if abs(lat[e]) > 0.2},
        "rollleft": {e: (PITCH * lat[e], 0, 0) for e in jets if abs(lat[e]) > 0.2},
    }
    names = ("pitchup", "pitchdown", "yawleft", "yawright", "rollleft", "rollright")
    for p in names:
        takes.pose_take(arm, p, poses[p])
    takes.clear_pose(arm)

    # ---------------------------------------------------------------- the rig table (the game reads it: ArkModel)
    # Unity model space: (x, y, z) = Blender (-x, z, -y), a reflection, so a rotation R becomes M R M^T.
    Mb = Matrix(((-1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, -1.0, 0.0)))

    def U(v):
        return [round(-v[0], 4), round(v[2], 4), round(-v[1], 4)]

    def Uq_mat(R):
        q = (Mb @ R @ Mb.transposed()).to_quaternion()
        return [round(q.x, 6), round(q.y, 6), round(q.z, 6), round(q.w, 6)]

    from mathutils import Euler

    def Uq(rot_deg):
        return Uq_mat(Euler([math.radians(a) for a in rot_deg], "XYZ").to_matrix())

    table = {"model": NAME, "frame": "Unity model space: x right (pilot's right), y up, z nose; units = Blender metres",
             "noseToJets": NOSE_TO_JETS, "sockets": [], "bones": [], "spins": [], "trails": []}
    counts = {}
    for p in g["prisms"]:
        tier, kind = TIER[p["tier"]]
        a, b = D(p["a"]).normalized(), D(p["b"])
        b = (b - a * b.dot(a)).normalized()
        d = a.cross(b).normalized()
        ha, hb, hd = (v * S * (OCTA_BLOCK if p.get("octa") else 1.0) for v in p["h"])
        k = counts.get(tier, 0)
        counts[tier] = k + 1
        table["sockets"].append({
            "name": "socket.%s.%03d" % (tier, k), "tier": tier, "kind": kind,
            "position": U(P(p["c"])), "forward": U(a), "up": U(b),
            "size": [round(abs(2 * hd), 4), round(abs(2 * hb), 4), round(abs(2 * ha), 4)],   # a box may be built off a reversed axis
            "bone": bone_of_motion[p["motion"]] if p["motion"] >= 0 else "hull"})
    # the turning parts: a rotation of `speed` radians a second about the artifact's +x through the origin, carried to
    # Unity (the reflection turns the sense), as an axis and degrees a second in model space
    for m, nm in sorted(bone_of_motion.items(), key=lambda kv: kv[1]):
        R = Matrix.Rotation(motions[m]["speed"], 3, D([1, 0, 0]))
        q = (Mb @ R @ Mb.transposed()).to_quaternion()
        axis, ang = q.axis.normalized(), q.angle
        table["spins"].append({"bone": nm, "axis": [round(v, 6) for v in axis], "degreesPerSecond": round(math.degrees(ang), 4)})
    INPUTS = (("pitchUp", "pitchup"), ("pitchDown", "pitchdown"), ("yawRight", "yawright"), ("yawLeft", "yawleft"),
              ("rollRight", "rollright"), ("rollLeft", "rollleft"))
    moved = {}
    for field, take in INPUTS:
        for b_, rot in poses[take].items():
            moved.setdefault(b_, {})[field] = Uq(rot)
    for b_ in sorted(moved):
        entry = {"bone": b_, "head": U(arm.data.bones[b_].head_local)}
        entry.update(moved[b_])
        table["bones"].append(entry)
    for nm, c, rr, ln in bells:                       # Trail 3: every bell leaves its own stream, from its mouth
        table["trails"].append({"bone": nm, "position": U(P([c[0] - ln, c[1], c[2]])), "width": round(2 * rr * S * 0.9, 3)})
    table["reactorAxis"] = U(ax)
    table["artifactScale"] = round(S, 6)             # artifact units -> model units, and where the artifact's x = 0 sits
    table["artifactOrigin"] = U(P([0, 0, 0]))
    table["reactorRadius"] = round(om["radius"] * S, 4)
    rig_path = os.path.join(ctx.out_dir, NAME + ".rig.json")
    with open(rig_path, "w") as fh:
        json.dump(table, fh, indent=1, sort_keys=True)
        fh.write("\n")
    ctx.log("rig table: %d sockets %s, %d spins, %d puppeted bells, %d trails -> %s" % (
        len(table["sockets"]), counts, len(table["spins"]), len(table["bones"]), len(table["trails"]), os.path.basename(rig_path)))

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s; nose to jets %.1f (want %.1f); S %.6f, centre x %.3f (ArkRoundSolid holds both)" % (
        tuple(round(v, 1) for v in lo), tuple(round(v, 1) for v in hi), hi[1] - lo[1], NOSE_TO_JETS, S, XC))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in names],
            "pose_takes": [ARMATURE + "|" + p for p in names],
            "sheet_poses": ("pitchup", "yawright", "rollright"),
            "pose_expect": {
                "pitchup": [("engine.0", "z", +1, 0.3)],
                "pitchdown": [("engine.0", "z", -1, 0.3)],
                "yawright": [("engine.0", "x", -1, 0.3)],
                "yawleft": [("engine.0", "x", +1, 0.3)],
            },
            "shapes": [],
            "slots": list(SLOTS),
            "symmetric": False,      # radially symmetric about the nose axis in threes, not mirror symmetric
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
        },
    }
