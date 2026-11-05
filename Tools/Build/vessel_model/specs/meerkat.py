"""
meerkat - a NEW hull on a BORROWED rig (PIPELINE "The borrowed-rig path", LESSONS 48): the Meerkat (VesselClassType
28, the wormhole vessel) flew in the Stoat's model since it was cloned (`Meerkat.prefab` nests `Stoat.fbx` under its
`StoatHull` and `StoatAnimation` lopes it). This file keeps EVERY name the prefab and the driver reach - the armature
`rig`, its fifteen bones at the Stoat's rest, the mesh node and geometry `mesh`, the slots, the shape names and the take
names - under its own guid, so the prefab binds it by a guid swap alone and `StoatAnimation` drives it unchanged.
Yash asked for it on 2026-11-03.

THE BRIEF (`Docs/Reports/MEERKAT.md` section 1, `R_VesselActions/MEERKAT.md` section 10): RT forms a wormhole (the
soliton) on its nose and it dives through; Y leaves SENTINELS, afterimages of this hull baked from its renderers and stood
up "like a real sentinel meerkat" (nose to the sky, back to the way in) at each wormhole's way in; its passive lays six
prisms on a hexagon round the way out. A spaceship inspired by a meerkat, never the animal (Garrett, 2026-10-18), and
NOT the Stoat recoloured: where the Stoat is a low prow over a shingled spine with dorsal fins, the Meerkat is an alert
head raised on a neck over a smooth body with a banded back, digging claws under the chest and a long tapering tail.

THE READ: the Meerkat's camera sits straight BEHIND it and level (MeerkatCameraSettingsSO followOffset (0, 0, -21),
68 degree FOV, look-ahead 40; the root is scaled 0.8), so the hull is about 30 x 40 px at 1080p: what reads is the raised
head with its ear sensors standing over the back, the two ember engine mouths under their domain haunch cowls, the
banded back as the lope pitches it, and the tail running away from the camera to its dark tip. The visor, the face and
the claws are for the hangar, the cards and the standing sentinels.

Clade -> ship (every part answers "what is this on the ship?"):
  alert raised head  -> the SENSOR MODULE (bone `nose`): a wedge, broad between the ears, tapering to a pointed snout,
                        raised on a NECK column with a domain collar and floating over it
  dark eye band      -> the VISOR: a band of sapphire glass across the nose bridge, slanting back and down both sides,
                        floating on a hairline seam with dark edges (Window)
  pale face, muzzle  -> a domain CREST plate on the forehead behind the visor and a domain MUZZLE plate ahead of it
  small dark ears    -> two SENSOR DISHES low on the back of the head, dark faces in domain rims (the lookout)
  dark nose          -> the SOLITON FORMER: a dark ring floating round the snout, its inner wall and the nose tip hot
                        (ember), where RT forms the wormhole (a pale ring there read as a pig's snout, round 2)
  slender body       -> a smooth, pear-shaped CORE fuselage (a narrow chest, a fuller belly), lowered 6 cm under the bones'
                        line so the head stands clear of the back; skinned along the five plate bones so the lope bends
                        it in one line
  banded back        -> a dark CARAPACE plate per plate bone floating over the core, its edges chevrons pointing forward,
                        each carrying a domain CHEVRON stripe across its rear third (thin stripes alone read as clips on a
                        tube, round 3; thick ones as a bee, round 1)
  digging fore-paws  -> two DIGGING ARMS (bones `leg.1.L` / `leg.1.R`): a shoulder ball, a forearm, an armoured paw
                        with a domain knuckle and three hooked claw blades with hot tips; the lope's landing swings
                        them forward, which is the dig stroke
  hind feet          -> two HAUNCH LEGS (bones `leg.2.L` / `leg.2.R`): a thigh and a flat foot pad with a domain toe;
                        with the tail they are the tripod a standing sentinel rests on
  haunches           -> twin engine NACELLES at the hips under domain haunch COWLS, the ember mouths where the
                        prefab's two VesselJets sit
  long thin tail     -> four telescoping dark SLEEVES with domain collars at their joints, rising a little toward the
                        tip, and the dark TIP (pale tops on the sleeves made the tail a dashed line, round 2)

A HOVERING ASSEMBLY (vessel-model section 0.5): the body (the core over plate.0 .. plate.4, with its carapace, stripes,
nacelles and cowls) plus ten parts on their own bones - the sensor module, the two digging arms, the two haunch legs and
the five tail pieces - each a set of closed shells floating clear; the gate is `assembly_report.py Meerkat.fbx --floating
<FLOATING>`. Within a part, plates sit on hairline seams.

BLENDED POSES: StoatAnimation plays the lope (arch .. land) and the turn bend at once, lifting the plates, head and tail
as it tilts them. Every combination of the four documentation takes clears 0.4 % of the diagonal when the takes'
rotations are composed AND their lifts summed (the Stoat's own hull touches in 35 such cases). That is why the first tail
sleeve starts behind the nacelle lips (the rump swings them 0.1 inward in a full turn), the neck rises from 4 cm ahead of
the chest, and the paws hang 1.2 cm further out than the shoulders. `pose_clearance.py --combos` composes rotations only
(it zeroes each bone's location), so it reports the landing's raised chest as a collision the game never plays.

Contract (the Stoat's, which Meerkat.prefab binds; FLEET_CONVENTION 1-4):
  file      Assets/_Models/Vessel Models/Meerkat.fbx (new guid; its .meta is Stoat.fbx.meta with that guid: all four
            slots remapped by name)
  frame     preset "zup": Blender nose -Y, up +Z, pilot's left +X.  Unity (x, y, z) = Blender (-x, z, -y).
  rig       the Stoat's armature, imported from Stoat.fbx at RIG_REF (pinned, so a later Stoat edit never moves this
            file): `rig` with hull (the core plate), plate.0 plate.1 plate.3 plate.4, nose, tail tail.2 tail.3 tail.4
            tail.5, leg.1.L/R leg.2.L/R, every bone at the Stoat's rest (rest_compare 0.0000). StoatAnimation resolves
            them by name and poses each from its rest, so the lope, the turn bend, the head nod, the tail swing and the
            leg reach move this hull as they moved the Stoat's.
  jets      the prefab's two VesselJets ride StoatHull at Unity (+-0.3, -0.05, -2.15) = Blender (-+0.3, 2.15, -0.05):
            the nacelle mouths are there
  size      the Stoat's drawn hull (x +-0.447, y -3.168 .. 4.820, z -0.665 .. 0.769) within a few percent: the hit boxes
            (1.2 x 1.4 x 7 and 1.6 x 1.8 x 7.6), the skimmer and the camera were tuned to it
  slots     0 Domain (stripes, crest, muzzle, cowls, collar, ear rims, knuckles, shoulder caps, thighs, toes, tail
            collars, the head's back), 1 Body,
            2 Window (the visor), 3 Engine (nacelle mouths, the former's tip, the claw tips, the foot vents)
  shapes    Charge, Mass, Space, Time (the Stoat's names and order):
            Charge (Sentinel Mode, the zap) = the claw blades lengthen into long diggers, the ear dishes rise and flare,
                                              the former's ring flares;
            Mass (Exit Portal, volume)      = the core and its stripes fatten about the spine and the belly drops, the
                                              haunches swell, everything that floats off the body rides out with it;
            Space (the Soliton, reach)      = the neck telescopes and the head reaches up and forward, the tail draws out;
            Time (Faster Ride, rate)        = the snout sharpens, the ears and the stripes rake back, the claws fold
                                              under the chest
  takes     the Stoat's documentation poses (arch, land, turnright, turnleft), retargeted from the Stoat bone for bone,
            behind a leading `rest` take (LESSONS 51); StoatAnimation drives the bones in code and plays none of them
"""

import math
from _common import box_uv

NAME = "Meerkat"
OUTPUT = "Assets/_Models/Vessel Models/Meerkat.fbx"
RIG_FROM = "Assets/_Models/Vessel Models/Stoat.fbx"
RIG_REF = "e3653c8243cfc3d6646c7917d4f91e33b336cded"   # the commit Stoat.fbx last changed in (its rest take, 2026-10-21)

ARMATURE = "rig"
MESH_NODE = "mesh"
GEOMETRY = "mesh"
SLOTS = ("Domain", "Body", "Window", "Engine")
D, H, W, J = 0, 1, 2, 3
STOAT_TAKES = ("arch", "land", "turnleft", "turnright")

# the Stoat's drawn hull (Blender frame), which the hit boxes, the skimmer and the camera were tuned to
SIZE_REF = ((-0.4472, -3.168, -0.6648), (0.4472, 4.820, 0.7686))

# ---- the Stoat's bones (Blender frame), asserted against the imported rig ------------------------------------------
PLATE_BONE = ["plate.0", "plate.1", "hull", "plate.3", "plate.4"]
PLATE_Y = [-1.6, -0.8, 0.0, 0.8, 1.6]
TAIL_BONE = ["tail", "tail.2", "tail.3", "tail.4", "tail.5"]
TAIL_Y = [2.0, 2.525, 3.05, 3.575, 4.1]
TAIL_Z = 0.065
NECK_Y = -2.0
LEG_HIP = {"leg.1": (0.2214, -0.8, -0.2214), "leg.2": (0.1691, 0.8, -0.1691)}
JET_X, JET_Y, JET_Z = 0.30, 2.15, -0.05      # the prefab's VesselJets (on StoatHull, not on a bone)
TIP_Y = 4.82                                 # the Stoat's tail tip

# ---- the core fuselage: (y, half-width, top, bottom, centre lift); Catmull-Rom between rows ----------------------
CORE = [
    (-1.990, 0.012, 0.012, 0.012, 0.010),
    (-1.975, 0.088, 0.080, 0.082, 0.008),
    (-1.940, 0.156, 0.136, 0.146, 0.004),
    (-1.880, 0.204, 0.176, 0.194, -0.002),
    (-1.780, 0.236, 0.202, 0.224, -0.010),
    (-1.550, 0.246, 0.218, 0.240, -0.024),
    (-1.000, 0.262, 0.228, 0.258, -0.044),
    (-0.250, 0.288, 0.234, 0.280, -0.060),
    (0.500, 0.302, 0.232, 0.304, -0.072),
    (0.950, 0.274, 0.216, 0.286, -0.068),
    (1.250, 0.204, 0.180, 0.224, -0.052),
    (1.500, 0.152, 0.146, 0.170, -0.034),
    (1.720, 0.110, 0.108, 0.118, -0.012),
    (1.860, 0.070, 0.068, 0.072, 0.004),
    (1.930, 0.030, 0.029, 0.029, 0.012),
    (1.950, 0.006, 0.006, 0.006, 0.014),
]
CORE_NT, CORE_NB = 2.5, 2.2

# ---- the sensor module: a wedge (y, half-width, top, bottom, centre lift), snout tip first -------------------------
HEAD = [
    (-3.166, 0.008, 0.008, 0.008, 0.480),
    (-3.150, 0.040, 0.036, 0.032, 0.482),
    (-3.110, 0.066, 0.056, 0.050, 0.487),
    (-3.040, 0.098, 0.078, 0.068, 0.495),
    (-2.960, 0.135, 0.104, 0.086, 0.505),
    (-2.870, 0.176, 0.136, 0.104, 0.518),
    (-2.770, 0.214, 0.166, 0.120, 0.530),
    (-2.660, 0.242, 0.190, 0.132, 0.540),
    (-2.550, 0.256, 0.204, 0.140, 0.546),
    (-2.450, 0.254, 0.204, 0.140, 0.548),
    (-2.370, 0.228, 0.186, 0.130, 0.548),
    (-2.310, 0.176, 0.146, 0.106, 0.548),
    (-2.275, 0.100, 0.084, 0.064, 0.548),
    (-2.262, 0.016, 0.014, 0.012, 0.548),
]
HEAD_NT, HEAD_NB = 2.4, 2.2

# the neck column: base (ahead of the chest) to top (under the head), rising about 50 degrees
NECK_BASE = (0.0, -2.115, 0.085)
NECK_TOP = (0.0, -2.395, 0.405)
NECK_R = (0.130, 0.118)
COLLAR_T = 0.40

# the visor: a band of glass across the nose bridge, slanting back and down to the sides (the meerkat's eye band)
VISOR_TOP_Y, VISOR_SIDE_Y, VISOR_W = -2.935, -2.705, 0.115
VISOR_A0, VISOR_A1 = 2.0, 178.0
EAR_Y, EAR_A = -2.395, 22.0

# the banded back: (bone, centre y, length, angle from, angle to); the chevrons point forward
CARAPACE = [  # (bone, centre y, length, angle from, angle to)
    ("plate.0", PLATE_Y[0] + 0.03, 0.58, 26.0, 154.0), ("plate.1", PLATE_Y[1], 0.62, 24.0, 156.0),
    ("hull", PLATE_Y[2], 0.62, 24.0, 156.0), ("plate.3", PLATE_Y[3], 0.60, 26.0, 154.0),
    ("plate.4", PLATE_Y[4] - 0.02, 0.36, 36.0, 144.0)]
PLATE_T = 0.034            # the carapace plate's thickness (a ridge adds to it along the spine)
CHEVRON, BAND_GAP = 0.060, 0.016

# the digging paw (y, half-width, top, bottom, centre z), claws first
PAW = [(-1.112, 0.034, 0.022, 0.020, -0.575), (-1.096, 0.060, 0.038, 0.032, -0.573), (-1.050, 0.070, 0.047, 0.038, -0.568),
       (-0.990, 0.067, 0.047, 0.038, -0.562), (-0.958, 0.048, 0.036, 0.030, -0.558), (-0.948, 0.012, 0.010, 0.010, -0.557)]
CLAW_LEN, CLAW_DROP = 0.36, 0.082
# the haunch leg's foot pad
FOOT = [(0.700, 0.016, 0.012, 0.010, -0.606), (0.712, 0.046, 0.027, 0.020, -0.608), (0.760, 0.056, 0.033, 0.024, -0.608),
        (0.900, 0.054, 0.035, 0.024, -0.604), (0.975, 0.046, 0.031, 0.022, -0.600), (0.990, 0.012, 0.010, 0.010, -0.599)]

# the haunch nacelles: (radius, distance from the front); the lip's back is at JET_Y - 0.02
NAC_PROF = [(0.012, 0.0), (0.050, 0.035), (0.080, 0.10), (0.100, 0.20), (0.110, 0.34), (0.114, 0.50),
            (0.114, 0.66), (0.104, 0.70), (0.104, 0.74), (0.118, 0.78), (0.120, 0.86), (0.117, 0.895),
            (0.092, 0.905), (0.078, 0.875), (0.040, 0.870), (0.0, 0.868)]

# the tail: radius at y, root to tip
TAIL_R0, TAIL_R1 = 0.092, 0.048
TAIL_GAP = 0.050


def catmull(tab, y, k):
    """Catmull-Rom through column k of a (y, ...) table at y (clamped)."""
    ys = [r[0] for r in tab]
    if y <= ys[0]:
        return tab[0][k]
    if y >= ys[-1]:
        return tab[-1][k]
    i = max(j for j in range(len(ys) - 1) if ys[j] <= y)
    p1, p2 = tab[i][k], tab[i + 1][k]
    p0 = tab[i - 1][k] if i > 0 else 2 * p1 - p2
    p3 = tab[i + 2][k] if i + 2 < len(tab) else 2 * p2 - p1
    t = (y - ys[i]) / (ys[i + 1] - ys[i])
    t2, t3 = t * t, t * t * t
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)


def outline(tab, y):
    return tuple(max(0.003, catmull(tab, y, k)) if k < 4 else catmull(tab, y, k) for k in (1, 2, 3, 4))


def se_pt(w, ht, hb, a, nt, nb):
    """The superellipse point at angle a (radians, from +X counter-clockwise in x-z)."""
    c, s = math.cos(a), math.sin(a)
    hh, nn = (ht, nt) if s >= 0 else (hb, nb)
    return (w * math.copysign(abs(c) ** (2.0 / nn), c), hh * math.copysign(abs(s) ** (2.0 / nn), s))


def body_pt(tab, y, a, nt, nb):
    w, ht, hb, dz = outline(tab, y)
    x, z = se_pt(w, ht, hb, a, nt, nb)
    return x, z + dz


def body_normal(tab, y, a, nt, nb):
    e = 1e-3
    x0, z0 = body_pt(tab, y, a - e, nt, nb)
    x1, z1 = body_pt(tab, y, a + e, nt, nb)
    tx, tz = x1 - x0, z1 - z0
    ln = math.hypot(tx, tz) or 1.0
    return tz / ln, -tx / ln


def tail_r(y):
    u = max(0.0, min(1.0, (y - TAIL_Y[0]) / (TAIL_Y[-1] - TAIL_Y[0])))
    return TAIL_R0 + (TAIL_R1 - TAIL_R0) * u


def tail_z(y):
    """The tail rises a little toward its tip, as a meerkat carries it."""
    u = max(0.0, min(1.0, (y - TAIL_Y[0]) / (TIP_Y - TAIL_Y[0])))
    return TAIL_Z + 0.10 * u ** 1.8


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def build(ctx):
    import bpy
    import bmesh
    from mathutils import Vector
    from mathutils.kdtree import KDTree
    import build as driver
    f, rig, morphs, takes = ctx.forms, ctx.rig, ctx.morphs, ctx.takes
    parts = []

    # ================================================================== THE STOAT'S RIG, as Meerkat.prefab and StoatAnimation bind it
    rig_from = driver.artist_path(RIG_FROM, RIG_REF)
    arm, donor = rig.import_shipped_model(rig_from)
    assert arm.name == ARMATURE, arm.name
    baked = rig.bake_parked_pose(donor, arm, deform_mesh=True)      # the Stoat's rest is its node chain: expect none
    donor_data = donor.data
    bpy.data.objects.remove(donor, do_unlink=True)                   # the Meerkat is new geometry
    bpy.data.meshes.remove(donor_data)                               # and its datablock held the name `mesh`
    bpy.context.view_layer.update()
    bone_names = [b.name for b in arm.data.bones]
    ctx.log("the Stoat's rig from %s: %d bones, parked pose baked on %d" % (RIG_REF[:10], len(bone_names), len(baked)))

    def head_of(b):
        return Vector(rig.bone_world_head(arm, b))
    for b, y in zip(PLATE_BONE, PLATE_Y):
        assert (head_of(b) - Vector((0, y, 0))).length < 1e-3, (b, tuple(head_of(b)))
    for b, y in zip(TAIL_BONE, TAIL_Y):
        assert (head_of(b) - Vector((0, y, TAIL_Z))).length < 1e-3, (b, tuple(head_of(b)))
    assert (head_of("nose") - Vector((0, NECK_Y, 0))).length < 1e-3
    for role, (hx, hy, hz) in LEG_HIP.items():
        for side, sx in (("L", 1), ("R", -1)):
            assert (head_of("%s.%s" % (role, side)) - Vector((sx * hx, hy, hz))).length < 1e-3, (role, side)

    # ================================================================== helpers
    def tag(obj, pid):
        me_ = obj.data
        at = me_.attributes.get("pid") or me_.attributes.new("pid", "FLOAT", "POINT")
        at.data.foreach_set("value", [float(pid)] * len(me_.vertices))
        return obj

    def piece(obj, slot, pid, bevel=0.0, angle=35):
        f.ensure_slots(obj, SLOTS)
        if slot is not None:
            f.paint(obj, slot)
        if bevel > 0:
            f.bevel(obj, width=bevel, segments=1, angle_deg=angle)
            f.apply_modifiers(obj)
        tag(obj, pid)
        return obj

    def bone_part(objs, name):
        o = objs[0] if len(objs) == 1 else f.join(objs, name, SLOTS)
        o.name = name
        for vg in list(o.vertex_groups):
            o.vertex_groups.remove(vg)
        f.tag_group(o, name)
        parts.append(o)
        ctx.log("part %-10s %6d tris" % (name, f.tri_count(o)))
        return o

    def table_loft(name, tab, nt, nb, segments=28, step=0.10, cx=0.0):
        """A fuselage along +Y through a (y, w, ht, hb, dz) table: the table's rows plus a station every `step`."""
        y0, y1 = tab[0][0], tab[-1][0]
        k_ = max(1, int((y1 - y0) / step))
        ys = sorted(set([round(r[0], 4) for r in tab] + [round(y0 + (y1 - y0) * k / k_, 4) for k in range(k_ + 1)]))
        sts = []
        for y in ys:
            w, ht, hb, dz = outline(tab, y)
            sts.append(f.Station((cx, y, dz), f.superellipse(w, ht, n=nt, segments=segments, h_bottom=hb, n_bottom=nb),
                                 right=(1, 0, 0), up=(0, 0, 1)))
        return f.loft(name, sts)

    def surf(tab, nt, nb, cx=0.0):
        """The body's surface: (y, angle in degrees from +X counter-clockwise in x-z, offset along the normal) -> point."""
        def fn(y, a_deg, off):
            a_ = math.radians(a_deg)
            px, pz = body_pt(tab, y, a_, nt, nb)
            nx, nz = body_normal(tab, y, a_, nt, nb)
            return Vector((px + nx * off + cx, y, pz + nz * off))
        return fn

    def slab(name, grid, nu, nv, slots=(D, H, D)):
        """A closed plate over a parametric patch: grid(u, v, outer) -> point; outer and inner skins plus four walls,
        `slots` = (outer face, inner face, walls). The conformal plate (LESSONS 53) with any outline: a plate whose y
        varies across it (a chevron, a slanted visor) still follows the body underneath at an even gap."""
        bm_ = bmesh.new()
        outer = [[bm_.verts.new(grid(i / nu, j / nv, True)) for j in range(nv + 1)] for i in range(nu + 1)]
        inner = [[bm_.verts.new(grid(i / nu, j / nv, False)) for j in range(nv + 1)] for i in range(nu + 1)]

        def quad(vs, slot):
            fc = bm_.faces.new(vs)
            fc.material_index = slot
        for i in range(nu):
            for j in range(nv):
                quad((outer[i][j], outer[i + 1][j], outer[i + 1][j + 1], outer[i][j + 1]), slots[0])
                quad((inner[i][j + 1], inner[i + 1][j + 1], inner[i + 1][j], inner[i][j]), slots[1])
        for i in range(nu):
            quad((outer[i][0], inner[i][0], inner[i + 1][0], outer[i + 1][0]), slots[2])
            quad((outer[i + 1][nv], inner[i + 1][nv], inner[i][nv], outer[i][nv]), slots[2])
        for j in range(nv):
            quad((outer[0][j + 1], inner[0][j + 1], inner[0][j], outer[0][j]), slots[2])
            quad((outer[nu][j], inner[nu][j], inner[nu][j + 1], outer[nu][j + 1]), slots[2])
        bmesh.ops.recalc_face_normals(bm_, faces=bm_.faces)
        obj = f.new_mesh_object(name, bm_, smooth=True)
        f.ensure_slots(obj, SLOTS)
        return obj

    def ring(name, profile, axis_, origin):
        """A closed ring: a lathe of an open profile whose two end loops are bridged into its inner wall."""
        obj = f.lathe(name, profile, segments=20, axis=axis_, origin=origin, cap=False)
        bm_ = bmesh.new()
        bm_.from_mesh(obj.data)
        bmesh.ops.bridge_loops(bm_, edges=[e for e in bm_.edges if e.is_boundary])
        bmesh.ops.recalc_face_normals(bm_, faces=bm_.faces)
        bm_.to_mesh(obj.data)
        bm_.free()
        obj.data.update()
        return obj

    def kd_of(objs):
        pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
        kd = KDTree(len(pts))
        for i, p_ in enumerate(pts):
            kd.insert(p_, i)
        kd.balance()
        return kd

    def clearance(a_objs, b_objs):
        kd = kd_of(b_objs)
        return min(kd.find(o.matrix_world @ v.co)[2] for o in a_objs for v in o.data.vertices)

    # ================================================================== THE BODY: the core and the banded back (plate.0 .. plate.4)
    core = table_loft("core", CORE, CORE_NT, CORE_NB, segments=24, step=0.16)
    f.ensure_slots(core, SLOTS)
    f.paint(core, H)
    # panel seams down each flank (four plates a side) and a keel seam under the belly
    for ya, yb in ((-1.62, -1.02), (-0.88, -0.18), (-0.04, 0.66), (0.80, 1.32)):
        f.inset_faces(core, lambda c, n, i, ya=ya, yb=yb: abs(n.x) > 0.80 and ya < c.y < yb and -0.165 < c.z < 0.065, 0.016, depth=0.012)
    f.inset_faces(core, lambda c, n, i: n.z < -0.93 and -1.45 < c.y < 1.05, 0.02, depth=0.012)
    core = piece(core, None, 1)
    core.name = "core"
    for vg in list(core.vertex_groups):
        core.vertex_groups.remove(vg)
    f.tag_group(core, "core")
    parts.append(core)
    ctx.log("part core       %6d tris" % f.tri_count(core))

    # the banded back: a dark carapace plate per plate bone on a hairline seam over the core, its edges chevrons pointing
    # forward, and on each a domain chevron stripe across its rear third (the meerkat's bands, as the ship's armour)
    core_s = surf(CORE, CORE_NT, CORE_NB)
    body_pieces = {b: [] for b in PLATE_BONE}
    for bone, yc, ln, a0, a1 in CARAPACE:
        def plate_t(v):
            return PLATE_T + 0.016 * max(0.0, 1.0 - abs(2 * v - 1) / 0.35)

        def plate_grid(u, v, outer, yc=yc, ln=ln, a0=a0, a1=a1):
            y = yc + (u - 0.5) * ln - CHEVRON * (1.0 - abs(2 * v - 1)) + CHEVRON * 0.5
            return core_s(y, a0 + (a1 - a0) * v, BAND_GAP + (plate_t(v) if outer else 0.0))
        body_pieces[bone].append(piece(slab("carapace", plate_grid, 4, 12, slots=(H, H, H)), None, 2, bevel=0.005, angle=40))
        sl = 0.16 if bone != "plate.4" else 0.11
        sa0, sa1 = a0 + 10.0, a1 - 10.0

        def stripe_grid(u, v, outer, yc=yc, ln=ln, sl=sl, sa0=sa0, sa1=sa1, a0=a0, a1=a1):
            vv = ((sa0 + (sa1 - sa0) * v) - a0) / (a1 - a0)        # the plate's own v under this point
            y = yc + ln * 0.5 - 0.05 - sl + u * sl - CHEVRON * (1.0 - abs(2 * vv - 1)) + CHEVRON * 0.5
            base = BAND_GAP + plate_t(vv) + 0.004
            return core_s(y, sa0 + (sa1 - sa0) * v, base + (0.014 if outer else 0.0))
        body_pieces[bone].append(piece(slab("stripe", stripe_grid, 2, 12), None, 5))

    # ---- the haunch nacelles (plate.4) under their domain cowls; the mouths are the prefab's VesselJets
    for side, sx in (("L", 1), ("R", -1)):
        cx, cz = sx * JET_X, JET_Z
        y0 = JET_Y - 0.02 - 0.905       # the lip's back at JET_Y - 0.02, just ahead of the jet
        nac = f.lathe("nacelle", NAC_PROF, segments=14, axis=(0, 1, 0), origin=(cx, y0, cz))
        f.ensure_slots(nac, SLOTS)
        f.paint(nac, H)
        f.paint(nac, J, lambda c, n, i, y0=y0, cx=cx, cz=cz: c.y > y0 + 0.85 and math.hypot(c.x - cx, c.z - cz) < 0.098)
        body_pieces["plate.4"].append(piece(nac, None, 3))
        # the cowl: a conformal domain plate over the nacelle's top and outboard side (the haunch)
        cs = surf([(y0 + d, r, r, r, cz) for (r, d) in NAC_PROF[2:12]], 2.0, 2.0, cx=cx)
        ca0, ca1 = (-25.0, 128.0) if sx > 0 else (52.0, 205.0)

        def cowl_grid(u, v, outer, cs=cs, y0=y0, ca0=ca0, ca1=ca1):
            t = 0.026 + 0.012 * math.sin(math.pi * u) ** 0.5
            return cs(y0 + 0.24 + 0.42 * u, ca0 + (ca1 - ca0) * v, 0.014 + (t if outer else 0.0))
        body_pieces["plate.4"].append(piece(slab("cowl", cowl_grid, 5, 10), None, 4, bevel=0.006, angle=40))
    for b in PLATE_BONE:
        bone_part(body_pieces[b], b)

    # ================================================================== THE SENSOR MODULE: neck, head, visor, crest, ears, former (nose)
    hp = []
    nb_, nt_ = Vector(NECK_BASE), Vector(NECK_TOP)
    axis = (nt_ - nb_).normalized()
    neck_len = (nt_ - nb_).length
    neck_pts, neck_prof = [], []
    for k in range(7):
        t = k / 6
        r = NECK_R[0] + (NECK_R[1] - NECK_R[0]) * t
        cap = 0.82 if k in (0, 6) else 1.0
        neck_pts.append(tuple(nb_ + (nt_ - nb_) * t))
        neck_prof.append(f.superellipse(r * cap, r * cap * 0.96, n=2.3, segments=16))
    neck = f.spine_loft("neck", neck_pts, neck_prof)
    hp.append(piece(neck, H, 10))
    col_at = nb_ + (nt_ - nb_) * COLLAR_T
    col_r = NECK_R[0] + (NECK_R[1] - NECK_R[0]) * COLLAR_T
    collar = ring("collar", [(col_r + 0.012, -0.030), (col_r + 0.034, -0.022), (col_r + 0.036, 0.022), (col_r + 0.012, 0.030)],
                  tuple(axis), tuple(col_at))
    hp.append(piece(collar, D, 11))

    # the head: a wedge, broad between the ears and tapering to a pointed snout
    head = table_loft("head", HEAD, HEAD_NT, HEAD_NB, segments=24, step=0.08)
    f.ensure_slots(head, SLOTS)
    f.paint(head, H)
    f.paint(head, D, lambda c, n, i: c.y > -2.300 and n.y > 0.55)       # the back of the module: the chase camera sees it
    f.paint(head, J, lambda c, n, i: c.y < -3.128)                        # the hot point of the soliton former
    hp.append(piece(head, None, 12))
    hs = surf(HEAD, HEAD_NT, HEAD_NB)

    def eye_y(a_deg):
        """The visor's centre line: across the nose bridge at the top, slanting back to the sides (the eye band)."""
        return VISOR_SIDE_Y + (VISOR_TOP_Y - VISOR_SIDE_Y) * math.sin(math.radians(a_deg)) ** 1.6

    def visor_grid(u, v, outer):
        a_ = VISOR_A0 + (VISOR_A1 - VISOR_A0) * u
        y = eye_y(a_) + (v - 0.5) * VISOR_W
        return hs(y, a_, 0.006 + (0.016 if outer else 0.0))
    visor = slab("visor", visor_grid, 22, 3, slots=(W, H, H))
    hp.append(piece(visor, None, 13))

    def crest_grid(u, v, outer):
        a_ = 58.0 + 64.0 * v
        yf = eye_y(a_) + VISOR_W * 0.5 + 0.024
        y = yf + (-2.470 - yf) * u
        t = 0.020 + 0.016 * math.sin(math.pi * v) ** 0.7 * math.sin(math.pi * min(1.0, u * 1.15)) ** 0.4
        return hs(y, a_, 0.010 + (t if outer else 0.0))
    crest = slab("crest", crest_grid, 8, 12)
    hp.append(piece(crest, None, 14, bevel=0.004, angle=40))

    def muzzle_grid(u, v, outer):
        a_ = 56.0 + 68.0 * v
        yb = eye_y(a_) - VISOR_W * 0.5 - 0.022
        y = -3.045 + (yb + 3.045) * u
        return hs(y, a_, 0.008 + (0.016 if outer else 0.0))
    muzzle = slab("muzzle", muzzle_grid, 5, 8)
    hp.append(piece(muzzle, None, 15))

    fy = -3.096
    fw, fht, fhb, fdz = outline(HEAD, fy)
    fr = max(fw, fht) + 0.020
    former = ring("former", [(fr, 0.0), (fr + 0.028, 0.006), (fr + 0.032, 0.028), (fr + 0.006, 0.034)], (0, 1, 0), (0, fy - 0.017, fdz))
    f.ensure_slots(former, SLOTS)
    f.paint(former, H)
    f.paint(former, J, lambda c, n, i: math.hypot(c.x, c.z - fdz) < fr + 0.0045)
    hp.append(piece(former, None, 16))

    # the ear sensors: small dishes low on the back of the head, dark faces in domain rims
    ears = {}
    head_kd = kd_of([head, crest, visor])
    for side, sx in (("L", 1), ("R", -1)):
        w_, ht_, hb_, dz_ = outline(HEAD, EAR_Y)
        a_ = math.radians(EAR_A)
        px, pz = se_pt(w_, ht_, hb_, a_, HEAD_NT, HEAD_NB)
        nx, nz = body_normal(HEAD, EAR_Y, a_, HEAD_NT, HEAD_NB)
        ax = Vector((sx * nx, 0.25, nz)).normalized()
        base = Vector((sx * px, EAR_Y, pz + dz_)) + ax * 0.016
        dish = f.lathe("ear", [(0.0, -0.016), (0.054, -0.016), (0.076, 0.0), (0.086, 0.020), (0.080, 0.038), (0.060, 0.043),
                               (0.032, 0.034), (0.0, 0.032)], segments=12, axis=tuple(ax), origin=tuple(base))
        f.ensure_slots(dish, SLOTS)
        f.paint(dish, H)
        f.paint(dish, D, lambda c, n, i, base=base, ax=ax: ((c - base) - ax * (c - base).dot(ax)).length > 0.069)
        step = 0
        while min(head_kd.find(v.co)[2] for v in dish.data.vertices) < 0.014 and step < 40:   # lift clear (LESSONS 53)
            for v in dish.data.vertices:
                v.co += ax * 0.004
            step += 1
        dish.data.update()
        ears[side] = (base + ax * 0.004 * step, ax)
        hp.append(piece(dish, None, 17 if sx > 0 else 18))
    ctx.log("head: neck %.3f, visor %.3f, crest %.3f, muzzle %.3f, former %.3f, ear %.3f clear" % (
        clearance([neck], [head]), clearance([visor], [head]), clearance([crest], [head, visor]), clearance([muzzle], [head, visor]),
        clearance([former], [head, muzzle]), clearance([hp[-1]], [head, crest, visor])))
    bone_part(hp, "nose")

    # ================================================================== THE DIGGING ARMS (leg.1.L / leg.1.R)
    claw_roots = {}
    for side, sx in (("L", 1), ("R", -1)):
        ap = []
        sh = Vector((sx * 0.340, -0.796, -0.326))
        ball = f.sphere("shoulder", 0.060, center=tuple(sh), segments=12, rings=7, scale=(1.0, 1.15, 0.95))
        f.ensure_slots(ball, SLOTS)
        f.paint(ball, H)
        f.paint(ball, D, lambda c, n, i, sh=sh: c.z > sh.z + 0.028)
        ap.append(piece(ball, None, 20))
        fa = f.tube_along("forearm", [(sx * 0.338, -0.806, -0.440), (sx * 0.338, -0.858, -0.492), (sx * 0.338, -0.906, -0.526)],
                          [0.042, 0.040, 0.036], segments=10)
        ap.append(piece(fa, H, 21))
        paw = table_loft("paw", PAW, 3.4, 3.0, segments=12, step=0.05, cx=sx * 0.338)
        ap.append(piece(paw, H, 22))
        ps = surf(PAW, 3.4, 3.0, cx=sx * 0.338)

        def kn_grid(u, v, outer, ps=ps):
            return ps(PAW[1][0] + 0.012 + (PAW[-3][0] - PAW[1][0] - 0.02) * u, 52.0 + 76.0 * v, 0.006 + (0.014 if outer else 0.0))
        ap.append(piece(slab("knuckle", kn_grid, 3, 6), None, 23))
        # three hooked claw blades raking forward and down from the paw's front; their tips are hot
        front = PAW[0][0]
        for j in (-1, 0, 1):
            root = Vector((sx * 0.338 + j * 0.040, front - 0.010, PAW[2][4] - 0.006))
            L = CLAW_LEN - 0.04 * abs(j)
            pts = [tuple(root + Vector((j * 0.014 * (s / 7), -L * (s / 7), -CLAW_DROP * (L / CLAW_LEN) * (s / 7) ** 2.1))) for s in range(8)]
            radii = [0.021 * (1.0 - 0.9 * (s / 7) ** 1.15) for s in range(8)]
            blade = f.tube_along("claw", pts, radii, segments=6)
            f.ensure_slots(blade, SLOTS)
            f.paint(blade, H)
            f.paint(blade, J, lambda c, n, i, root=root, L=L: root.y - c.y > 0.64 * L)
            ap.append(piece(blade, None, 24))
        claw_roots[side] = Vector((sx * 0.338, front - 0.010, PAW[2][4]))
        bone_part(ap, "leg.1." + side)

    # ================================================================== THE HAUNCH LEGS (leg.2.L / leg.2.R)
    for side, sx in (("L", 1), ("R", -1)):
        lp = []
        thigh = f.tube_along("thigh", [(sx * 0.316, 0.775, -0.340), (sx * 0.320, 0.840, -0.420), (sx * 0.322, 0.905, -0.488)],
                             [0.060, 0.056, 0.046], segments=12)
        f.ensure_slots(thigh, SLOTS)
        f.paint(thigh, H)
        f.paint(thigh, D, lambda c, n, i: c.z > -0.372 and (n.x * sx > 0.2 or n.z > 0.3))
        lp.append(piece(thigh, None, 30))
        foot = table_loft("foot", FOOT, 3.6, 3.0, segments=12, step=0.07, cx=sx * 0.322)
        f.ensure_slots(foot, SLOTS)
        f.paint(foot, H)
        f.paint(foot, D, lambda c, n, i: c.y < 0.745 and n.z > -0.3)                 # the domain toe
        f.paint(foot, J, lambda c, n, i: n.z < -0.85 and 0.80 < c.y < 0.93)          # a landing thruster vent
        lp.append(piece(foot, None, 31))
        bone_part(lp, "leg.2." + side)

    # ================================================================== THE TAIL (tail .. tail.5): telescoping sleeves, pale on top, a dark tip
    for j, bone in enumerate(TAIL_BONE[:4]):
        y0 = TAIL_Y[j] + (0.195 if j == 0 else TAIL_GAP / 2)
        y1 = TAIL_Y[j + 1] - TAIL_GAP / 2
        r0, r1 = tail_r(y0), tail_r(y1)
        z0, z1 = tail_z(y0), tail_z(y1)
        ttab = [(y0, r0 * 0.70, r0 * 0.66, r0 * 0.62, z0), (y0 + 0.010, r0 * 0.86, r0 * 0.82, r0 * 0.78, z0),
                (y0 + 0.050, r0 * 0.92, r0 * 0.88, r0 * 0.84, z0 + (z1 - z0) * 0.1),
                (y1 - 0.090, r1 * 1.00, r1 * 0.96, r1 * 0.92, z1 - (z1 - z0) * 0.2),
                (y1 - 0.050, r1 * 1.12, r1 * 1.08, r1 * 1.02, z1), (y1 - 0.012, r1 * 1.10, r1 * 1.06, r1 * 1.00, z1),
                (y1, r1 * 0.90, r1 * 0.86, r1 * 0.82, z1)]
        sleeve = table_loft("sleeve", ttab, 2.3, 2.1, segments=16, step=0.12)
        f.ensure_slots(sleeve, SLOTS)
        f.paint(sleeve, H)
        f.paint(sleeve, D, lambda c, n, i, y1=y1: y1 - 0.062 < c.y < y1 - 0.003)       # the collar at each joint
        bone_part([piece(sleeve, None, 40)], bone)
    # the dark tip (the meerkat's black tail tip)
    y0 = TAIL_Y[4] + TAIL_GAP / 2
    tip_tab = [(y0, 0.034, 0.033, 0.032, tail_z(y0)), (y0 + 0.010, 0.044, 0.043, 0.041, tail_z(y0)),
               (y0 + 0.050, 0.050, 0.049, 0.047, tail_z(y0 + 0.05)), (4.40, 0.042, 0.041, 0.039, tail_z(4.40)),
               (4.62, 0.026, 0.025, 0.024, tail_z(4.62)), (4.76, 0.011, 0.011, 0.011, tail_z(4.76)),
               (TIP_Y, 0.002, 0.002, 0.002, tail_z(TIP_Y))]
    tip = table_loft("tip", tip_tab, 2.2, 2.2, segments=12, step=0.12)
    f.ensure_slots(tip, SLOTS)
    f.paint(tip, H)
    bone_part([piece(tip, None, 42)], "tail.5")

    # ================================================================== JOIN, n-gons out
    for p in parts:
        f.set_smooth(p, True)
    mesh = f.join(parts, MESH_NODE, SLOTS)
    mesh.data.name = GEOMETRY
    assert mesh.data.name == GEOMETRY, mesh.data.name
    stray = mesh.vertex_groups.get(MESH_NODE)
    if stray:
        mesh.vertex_groups.remove(stray)
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    big = [fc for fc in bm.faces if len(fc.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(mesh.data)
    bm.free()
    f.set_smooth(mesh, True)
    f.sharpen_by_angle(mesh, 40)
    box_uv(mesh)
    ctx.log("meerkat joined: %d verts, %d tris (n-gons triangulated: %d)" % (len(mesh.data.vertices), f.tri_count(mesh), len(big)))

    # ================================================================== MASKS (before skinning folds the core into the plates)
    me = mesh.data
    pid = [0.0] * len(me.vertices)
    me.attributes["pid"].data.foreach_get("value", pid)

    def pmask(*ids):
        s_ = set(float(i) for i in ids)
        return [1.0 if p_ in s_ else 0.0 for p_ in pid]
    ms = morphs.MaskSet(mesh)
    g = {n_: ms.mask(n_) for n_ in ms.names}
    body_m = pmask(1, 2, 5)                                # the core, its carapace and the stripes
    bands = pmask(2, 5)
    nacelles = {s_: [w * (1.0 if v.co.x * sx > 0 else 0.0) for w, v in zip(pmask(3, 4), me.vertices)] for s_, sx in (("L", 1), ("R", -1))}
    headm = g["nose"]
    face = pmask(12, 13, 14, 15, 16)                       # the head and every plate on it
    ear_m = {"L": pmask(17), "R": pmask(18)}
    claws = {s_: [a_ * b_ for a_, b_ in zip(g["leg.1." + s_], pmask(24))] for s_ in ("L", "R")}
    arms = {s_: g["leg.1." + s_] for s_ in ("L", "R")}
    legs2 = {s_: g["leg.2." + s_] for s_ in ("L", "R")}
    tail_m = ms.mask(*TAIL_BONE)

    # ================================================================== SKIN: the core bends along the plate chain; every other part is rigid
    anchors = list(zip(PLATE_Y, PLATE_BONE))
    cg = mesh.vertex_groups.get("core")
    for v in me.vertices:
        if not any(gg.group == cg.index for gg in v.groups):
            continue
        y = (mesh.matrix_world @ v.co).y
        if y <= anchors[0][0]:
            pairs = [(anchors[0][1], 1.0)]
        elif y >= anchors[-1][0]:
            pairs = [(anchors[-1][1], 1.0)]
        else:
            k = max(i for i in range(len(anchors)) if anchors[i][0] <= y)
            (ya, ba), (yb, bb) = anchors[k], anchors[k + 1]
            t = smooth01((y - ya) / (yb - ya))
            pairs = [(ba, 1.0 - t), (bb, t)]
        for bname, wgt in pairs:
            if wgt <= 1e-4:
                continue
            gr = mesh.vertex_groups.get(bname) or mesh.vertex_groups.new(name=bname)
            gr.add([v.index], wgt, "REPLACE")
    mesh.vertex_groups.remove(cg)
    rig.skin_rigid(mesh, arm, default_bone="hull", bake_park=False)
    skinned = {mesh.vertex_groups[gg.group].name for v in me.vertices for gg in v.groups if gg.weight > 0}
    unskinned = [b for b in bone_names if b not in skinned]
    if unskinned:
        raise AssertionError("bones that skin nothing (Unity's optimizeBones would strip them): %s" % unskinned)

    # ================================================================== MORPHS (4-12 % of the ~8.2 diagonal)
    def where(mask, pred):
        return [w if pred(v.co) else 0.0 for w, v in zip(mask, me.vertices)]

    def field_move(delta, mask):
        d = Vector(delta)

        def fn(i, co, n):
            return d * mask[i] if mask[i] > 0 else Vector((0, 0, 0))
        return fn

    # Mass (Exit Portal, volume): the core and its stripes fatten about the spine and the belly drops; the haunches swell;
    # everything that floats off the body rides out with its surface
    MASS_X, MASS_TOP, MASS_BELLY = 1.62, 1.45, 1.85

    def spine_scale(mask):
        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            rz = co.z - outline(CORE, co.y)[3]
            return Vector((co.x * (MASS_X - 1), 0, rz * ((MASS_TOP if rz > 0 else MASS_BELLY) - 1))) * mask[i]
        return fn
    belly = 0.30 * (MASS_BELLY - 1)
    mass = [spine_scale(body_m)]
    for s_, sx in (("L", 1), ("R", -1)):
        mass.append(morphs.scale_about(1.38, (sx * JET_X, 0, JET_Z), nacelles[s_], axes=(1, 0, 1)))
        mass.append(field_move((sx * 0.30 * (MASS_X - 1), 0, -0.05 * (MASS_BELLY - 1)), nacelles[s_]))
        mass.append(field_move((sx * 0.25 * (MASS_X - 1), 0, -belly), arms[s_]))
        mass.append(field_move((sx * 0.22 * (MASS_X - 1), 0, -belly), legs2[s_]))
    mass.append(morphs.scale_about(1.16, (0, -2.66, 0.53), face, axes=(1, 0.5, 1)))
    mass.append(morphs.scale_about(1.30, (0, 0, TAIL_Z), where(tail_m, lambda co: co.y < 3.4), axes=(1, 0, 1)))

    # Charge (Sentinel Mode, the zap): the claw blades lengthen into long diggers; the ear dishes rise and flare
    charge = []
    for s_, sx in (("L", 1), ("R", -1)):
        charge.append(morphs.stretch_along((0, -1, -0.30), tuple(claw_roots[s_]), 1.95, claws[s_]))
        base, ax = ears[s_]
        charge.append(morphs.scale_about(1.50, tuple(base), ear_m[s_]))
        charge.append(field_move(tuple(ax * 0.08 + Vector((0, 0, 0.07))), ear_m[s_]))
    charge.append(morphs.scale_about(1.40, (0, -3.096, outline(HEAD, -3.096)[3]), pmask(16), axes=(1, 0.5, 1)))

    # Space (the Soliton, reach): the neck telescopes and the module reaches up and forward; the tail draws out
    REACH = 0.32
    reach = axis * REACH
    space = [field_move(tuple(reach), [a_ * (1.0 - b_) for a_, b_ in zip(headm, pmask(10, 11))]),
             morphs.stretch_along(tuple(axis), NECK_BASE, 1.0 + REACH / neck_len, pmask(10)),
             field_move(tuple(reach * COLLAR_T), pmask(11)),
             morphs.stretch_along((0, 1, 0), (0, TAIL_Y[0], TAIL_Z), 1.17, tail_m)]

    # Time (Faster Ride, rate): the snout sharpens, the ears and the stripes rake back, the claws fold under the chest
    time_ = [morphs.stretch_along((0, -1, 0), (0, -2.80, 0), 1.80, face, ramp=(0.0, 0.22))]

    def rake(mask, pivot_z, k):
        def fn(i, co, n):
            if mask[i] <= 0:
                return Vector((0, 0, 0))
            h = max(0.0, co.z - pivot_z)
            return Vector((0, k * h, -0.12 * k * h)) * mask[i]
        return fn
    time_.append(rake(bands, 0.10, 0.95))
    for s_, sx in (("L", 1), ("R", -1)):
        base, ax = ears[s_]
        time_.append(field_move((0, 0.10, -0.035), ear_m[s_]))
        time_.append(morphs.sweep((0, 0, 1), tuple(base), (0, 1.2, 0), ear_m[s_]))
        time_.append(field_move((0, 0.22, 0.10), claws[s_]))
    shapes = {"Charge": charge, "Mass": mass, "Space": space, "Time": time_}
    for name in ("Charge", "Mass", "Space", "Time"):
        kb, mx, frac, moved = morphs.add_shape(mesh, name, shapes[name])
        ctx.log("shape %s: travel %.3f (%.1f%% of diag), %d verts" % (name, mx, 100 * frac, moved))
    if "pid" in me.attributes:
        me.attributes.remove(me.attributes["pid"])

    # ================================================================== TAKES: the Stoat's four documentation poses, retargeted unchanged
    acts = takes.retarget_from(arm, rig_from, only={ARMATURE + "|" + t for t in STOAT_TAKES})
    for act in acts.values():
        act.use_fake_user = True     # kept like an authored take, so build.run leads the file with a rest take (LESSONS 51)
    takes.clear_pose(arm)
    ctx.log("takes: %d retargeted from the Stoat (%s)" % (len(acts), ", ".join(sorted(acts))))
    assert sorted(a.split("|")[-1] for a in acts) == sorted(STOAT_TAKES), sorted(acts)

    lo, hi = f.bounds(mesh)
    ctx.log("bounds (blender) %s .. %s" % (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi)))
    return {
        "armature": arm, "mesh": mesh,
        "spec": {
            "preset": "zup", "armature": ARMATURE, "mesh_node": MESH_NODE,
            "bones": bone_names,
            "takes": [ARMATURE + "|" + p for p in STOAT_TAKES],
            "pose_takes": [ARMATURE + "|" + p for p in STOAT_TAKES],
            "sheet_poses": ("arch", "land", "turnright", "turnleft"),
            "pose_expect": {
                "arch": [("plate.0", "z", -1, 0.08), ("plate.4", "z", -1, 0.08), ("nose", "z", -1, 0.10)],
                "land": [("tail.5", "z", +1, 0.3), ("leg.1.L", "y", -1, 0.05), ("leg.2.R", "y", +1, 0.05), ("plate.0", "z", +1, 0.08)],
                "turnright": [("nose", "x", -1, 0.05), ("tail.5", "x", -1, 0.10)],
                "turnleft": [("nose", "x", +1, 0.05), ("tail.5", "x", +1, 0.10)],
            },
            "shapes": ["Charge", "Mass", "Space", "Time"],
            "slots": list(SLOTS),
            "size_ref_blender": SIZE_REF, "size_tol": (0.05, 0.02, 0.05),
            "bounds_blender": (tuple(lo), tuple(hi)), "bounds_tol": 0.05,
            "min_travel": 0.03,
        },
    }


# the hovering assembly: the body (the core across the plate chain, with its stripes, nacelles and cowls) and ten floating parts
FLOATING = ("plate.0+plate.1+hull+plate.3+plate.4,nose,leg.1.L,leg.1.R,leg.2.L,leg.2.R,"
            "tail,tail.2,tail.3,tail.4,tail.5")
