"""The Ark's lobby, inside the REAL Ark: the hangar of ArkReverse.fbx (Garrett's Ark, Docs/VesselModels/ArkReverse.md),
for the Squads tab's backdrop.

The model's own hangar (the prow, between the jaws): a bay of arched frames, three launch pads for the three escorts
(`hangar.slot.0..2`), floor lights leading to them, a glowing back wall with a tunnel curving down through the keel, and
clam-jaw doors opened with the model's own `hangaropen` take. Painted with the game's ship materials: the body
gunmetal, glass, the engine ember (vessel_colors.py), the domain slot in the squad's domain (Nocturne's ShipColor1/2),
drawn with VesselGraph's read (rim to face by facing) under the room's light. The hull's 108 prisms sit in the model's
own sockets, of each socket's kind and at their real sizes (gills and danger blades 3.6 x 1.6 x 9, shielded octahedra
4.8, super-shielded stellations 6.6), in Nocturne's block colours.

Dressed with the game's own pieces, raw and noisy: the squad's three seat prisms hover over the three pads (the third
seat KEPT for a friend: an outline, dark, until they come), the HyperSea board mounted between the arches (the Voyage
Board's Sector chart with the Ark at Tumult; the galaxy on the far wall), heaps of prism debris along the walls, danger
thickets by the mouth, hanging block chains, junk in the haze. Out of the open mouth: the cell (the game's own picture
of it, Amoebius) with lattice tubes and a trail of blocks passing.

    <bpy python> r59_ark_hangar_render.py <out_dir> <assets_dir> [cams=hero,board,mouth] [domains=Jade,Ruby,Gold] [W H samples]

assets_dir: window.png (the cell: an Amoebius screenshot of the menu's live cell with the HUD hidden, `set UI_Refactored
Canvas enabled false`), board_sector.png and board_galaxy.png (hi_Sector.png and hi_Arm.png from
r59_voyage_board_shot.cjs). Writes <out_dir>/lobby_<cam>_<dom>.png; r59_ark_lobby_grade.py grades them for the boards.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

OUT, ASSETS = sys.argv[1], sys.argv[2]
CAMS = (sys.argv[3] if len(sys.argv) > 3 else "hero,board,mouth").split(",")
DOMS = (sys.argv[4] if len(sys.argv) > 4 else "Jade,Ruby,Gold").split(",")
W = int(sys.argv[5]) if len(sys.argv) > 5 else 1920
H = int(sys.argv[6]) if len(sys.argv) > 6 else 1080
SAMPLES = int(sys.argv[7]) if len(sys.argv) > 7 else 40
os.makedirs(OUT, exist_ok=True)
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
FBX = os.path.join(REPO, "Assets/_Models/Vessel Models/ArkReverse.fbx")

bpy.ops.wm.read_factory_settings(use_empty=True)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r59_prismkit as K  # noqa: E402  (binds to the fresh scene)
from r59_prismkit import add, rand_rot, yaw, frame, PAL, DANGER, SQUAD, DOMS3  # noqa: E402

sc, col, rng = K.sc, K.col, K.rng

# ------------------------------------------------------------------------------------------------ the Ark itself
bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, ignore_leaf_bones=False, automatic_bone_orientation=False, use_anim=True)
arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
mesh = [o for o in bpy.data.objects if o.type == "MESH"][0]
arm.data.pose_position = "POSE"
act = bpy.data.actions.get("rig|rig|hangaropen")
arm.animation_data_create()
arm.animation_data.action = act
sc.frame_set(int(act.frame_range[0]))

# the ship materials: vessel_colors SCHEME (body, glass, ember) and the domain from Nocturne's ShipColor1 (rim) / 2 (face)
SHIP = {"body": ((0.18, 0.21, 0.33), (0.028, 0.033, 0.065)), "window": ((0.7, 0.8, 1.0), (0.06, 0.16, 0.62)),
        "engine": ((0.42, 0.11, 0.04), (0.19, 0.04, 0.02))}
SHIP_DOM = {"Jade": ((0.018, 0.0, 0.01), (0.079, 0.749, 0.737)), "Ruby": ((0.0, 0.012, 0.023), (0.707, 0.102, 0.749)),
            "Gold": ((0.108, 0.047, 0.225), (1.22, 0.688, 0.089))}
GLOW = {"domain": 2.2, "engine": 1.1, "window": 1.0, "body": 0.25}
BASE = {"domain": 0.3, "engine": 0.12, "window": 1.0, "body": 1.6}


def ship_mat(role):
    m = bpy.data.materials.new("ship_" + role)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    g = nt.nodes.new("ShaderNodeNewGeometry")
    d = nt.nodes.new("ShaderNodeVectorMath"); d.operation = "DOT_PRODUCT"
    nt.links.new(g.outputs["Normal"], d.inputs[0]); nt.links.new(g.outputs["Incoming"], d.inputs[1])
    a = nt.nodes.new("ShaderNodeMath"); a.operation = "ABSOLUTE"; nt.links.new(d.outputs["Value"], a.inputs[0])
    mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = "RGBA"; mx.name = "fres"
    nt.links.new(a.outputs[0], mx.inputs["Factor"])
    e = nt.nodes.new("ShaderNodeEmission"); e.inputs[1].default_value = GLOW[role]
    nt.links.new(mx.outputs[2], e.inputs[0])
    b = nt.nodes.new("ShaderNodeBsdfPrincipled"); b.name = "pbr"
    b.inputs["Metallic"].default_value = 0.65 if role == "body" else 0.1
    b.inputs["Roughness"].default_value = 0.32 if role == "body" else 0.25
    ad = nt.nodes.new("ShaderNodeAddShader")
    nt.links.new(b.outputs[0], ad.inputs[0]); nt.links.new(e.outputs[0], ad.inputs[1])
    nt.links.new(ad.outputs[0], o.inputs[0])
    return m


def paint_ship(m, role, rim, face):
    m.node_tree.nodes["fres"].inputs[6].default_value = (*rim, 1)
    m.node_tree.nodes["fres"].inputs[7].default_value = (*face, 1)
    base = [min(1.0, (rim[i] * 0.6 + face[i] * 0.4) * BASE[role]) for i in range(3)]
    m.node_tree.nodes["pbr"].inputs["Base Color"].default_value = (*base, 1)


SHIP_MATS = {r: ship_mat(r) for r in ("domain", "body", "window", "engine")}
ROLE = {"Domain": "domain", "Body": "body", "Window": "window", "Engine": "engine"}
for i, s in enumerate(mesh.data.materials):
    mesh.data.materials[i] = SHIP_MATS[ROLE.get(s.name.split(".")[0], "body")]
for r in ("body", "window", "engine"):
    paint_ship(SHIP_MATS[r], r, *SHIP[r])

# the hull's prisms in the model's own sockets, each socket's kind at its real size, in the Ark's (the squad's) domain
SLAB = (1.8, 0.8, 4.5)           # half extents of SLAB (3.6, 1.6, 9.0)
OCTA_H, STELLA_H = 2.4, 3.3      # half sizes of OCTA_S 4.8, STELLA_S 6.6
for b in arm.pose.bones:
    if not b.name.startswith("socket."):
        continue
    M = arm.matrix_world @ b.matrix
    pos = M.to_translation()
    R = M.to_3x3().normalized()
    kind = b.name.split(".")[1]
    if kind == "prism":
        add("box", pos, (SLAB[0], SLAB[2], SLAB[1]), (SQUAD, "plain", 1.0), rot=R)
    elif kind == "danger":
        add("blade", pos, (3.2, 3.2, 5.5), ("Red", "danger", 1.2), rot=R)
    elif kind == "shielded":
        add("octa", pos, (OCTA_H, OCTA_H, OCTA_H * 1.2), (SQUAD, "shielded", 1.0), rot=R)
    elif kind == "supershielded":
        add("stella", pos, (STELLA_H,) * 3, (SQUAD, "super", 1.0), rot=R)

# ------------------------------------------------------------------------------------------------ the hangar, dressed
HW, Z0, Z1, BACK, FRONT = 14.0, -23.0, -12.0, -44.0, -88.0
PADS = [Vector((0.0, -52.0, Z0)), Vector((0.0, -64.0, Z0)), Vector((0.0, -76.0, Z0))]

# the squad's seat prisms over the pads; the third seat is KEPT: its prism an outline of small dark blocks
SEAT_Z = Z0 + 2.2
for i, p in enumerate(PADS):
    c = p + Vector((0, 0, SEAT_Z - Z0))
    if i < 2:
        add("stella", c, (0.75,) * 3, (SQUAD, "plain", 1.6), rot=rand_rot())
        add("octa", c, (0.34, 0.34, 0.44), (SQUAD, "plain", 4.0), rot=rand_rot())
        for k in range(8):
            a = k * math.tau / 8
            add("octa", c + Vector((math.cos(a) * 1.7, math.sin(a) * 1.7, 0.3 * math.sin(a * 2))), (0.11, 0.11, 0.18),
                (SQUAD, "plain", 2.2), rot=rand_rot())
    else:
        Rr = rand_rot()
        verts = [Rr @ (Vector(v) * 0.75) for v in K.STELLA_V]
        for (ia, ib, ic) in K.STELLA_F:
            for (u, w) in ((ia, ib), (ib, ic), (ic, ia)):
                pa, pb = verts[u], verts[w]
                for q in range(6):
                    cc = pa.lerp(pb, (q + 0.5) / 6)
                    add("box", c + cc, (0.07, 0.07, (pb - pa).length / 6 * 0.42), ("Blue", "plain", 0.9), fwd=pb - pa)

# the HyperSea board between the arches on the left wall; the galaxy on the right wall
BX = -HW + 0.9
board = K.image_emission("board_sector", os.path.join(ASSETS, "board_sector.png"), 2.4)
bpy.ops.mesh.primitive_plane_add(size=1, location=(BX, -63.0, -17.4), rotation=(math.radians(90), 0, math.radians(-90)))
bs = bpy.context.active_object
bs.scale = (13.0, 8.5, 1)
bs.data.materials.append(board)
gal = K.image_emission("board_galaxy", os.path.join(ASSETS, "board_galaxy.png"), 2.0)
bpy.ops.mesh.primitive_plane_add(size=1, location=(-BX, -57.0, -16.6), rotation=(math.radians(90), 0, math.radians(90)))
bg2 = bpy.context.active_object
bg2.scale = (6.4, 4.7, 1)
bg2.data.materials.append(gal)
for (x, cy, cz, w, h) in ((BX, -63.0, -17.4, 13.0, 8.5), (-BX, -57.0, -16.6, 6.4, 4.7)):
    sx = -0.15 if x < 0 else 0.15
    for s in range(int(w / 0.7) + 2):
        yy = cy - w / 2 - 0.35 + s * 0.7
        add("box", (x + sx, yy, cz + h / 2 + 0.35), (0.25, 0.33, 0.22), ("Blue", "super", 1.0))
        add("box", (x + sx, yy, cz - h / 2 - 0.35), (0.25, 0.33, 0.22), ("Blue", "super", 1.0))
    for s in range(int(h / 0.7) + 1):
        zz = cz - h / 2 + s * 0.7
        add("box", (x + sx, cy - w / 2 - 0.35, zz), (0.25, 0.22, 0.33), ("Blue", "super", 1.0))
        add("box", (x + sx, cy + w / 2 + 0.35, zz), (0.25, 0.22, 0.33), ("Blue", "super", 1.0))


def heap(cx, cy, rx, ry, count, hmax, z0=Z0):
    for _ in range(count):
        x, y = cx + rng.gauss(0, rx), cy + rng.gauss(0, ry)
        x = max(-HW + 0.8, min(HW - 0.8, x))
        y = max(FRONT + 3, min(BACK - 0.8, y))
        d = math.hypot((x - cx) / (rx * 2.2), (y - cy) / (ry * 2.2))
        h = max(0.0, hmax * (1 - d)) * rng.random() + rng.uniform(0, 0.4)
        pal = rng.choice(DOMS3 + ["Blue"] * 4)
        kind = rng.choices(["plain", "shielded", "super"], [0.84, 0.12, 0.04])[0]
        s = rng.uniform(0.2, 0.7)
        shape = K.kind_shape(kind)
        size = (s * rng.uniform(0.8, 2.8), s * 0.5, s * 0.5) if shape == "box" else (s * 0.8,) * 3
        add(shape, (x, y, z0 + h), size, (pal, kind, round(rng.uniform(0.6, 1.1), 1)), rot=rand_rot())


# debris heaped along both walls, deepest by the back wall's corners, kept off the pads' path
heap(-11.0, -48.0, 1.6, 2.4, 900, 6.0)
heap(11.0, -47.0, 1.6, 2.4, 900, 6.5)
heap(-11.5, -70.0, 1.2, 4.0, 650, 3.5)
heap(11.5, -74.0, 1.2, 4.5, 800, 4.5)
# danger thickets in the mouth's corners: the way out has teeth
for (x, y, n) in ((-11.5, -84.0, 9), (11.5, -84.0, 9), (-9.0, -80.0, 5), (9.5, -79.0, 5)):
    for _ in range(n):
        d = Vector((rng.uniform(-0.7, 0.7), rng.uniform(-0.7, 0.7), 1)).normalized()
        add("blade", (x, y, Z0 + rng.uniform(0, 1.0)), (1.2, 1.4, rng.uniform(1.0, 3.2)), ("Red", "danger", 1.0), fwd=d)
# block chains hanging from the ceiling between the arches
for c in range(16):
    x0, y0 = rng.uniform(-11, 11), rng.uniform(-82, -46)
    if abs(x0) < 4.5:
        continue
    L = rng.uniform(2.0, 6.5)
    pal = rng.choice(DOMS3 + ["Blue", "Blue"])
    for s in range(int(L / 0.5)):
        add("box", (x0, y0, Z1 - 0.3 - s * 0.5), (0.11, 0.11, 0.22), (pal, "plain", 1.0), fwd=(0, 0, 1), up=(0, 1, 0))
# junk in the haze
NEAR = [Vector((4.2, -45.2, -19.4)), Vector((10.5, -50.0, -19.0))]       # the hero and board cameras: no junk at the lens
for _ in range(560):
    p = (rng.uniform(-12.5, 12.5), rng.uniform(-86, -45), rng.uniform(Z0 + 1.0, Z1 - 0.6))
    if min((Vector(p) - n).length for n in NEAR) < 8.0:
        continue
    pal = rng.choice(DOMS3 + ["Blue", "Blue"])
    kind = rng.choices(["plain", "shielded", "super"], [0.86, 0.1, 0.04])[0]
    s = rng.uniform(0.04, 0.16)
    shape = K.kind_shape(kind)
    add(shape, p, (s * 2.2, s, s) if shape == "box" else (s,) * 3, (pal, kind, 1.0), rot=rand_rot())

# ------------------------------------------------------------------------------------------------ outside: the cell
win = K.image_emission("cell_view", os.path.join(ASSETS, "window.png"), 1.7)
for (y, rotz) in ((-420.0, 0.0), (420.0, math.pi)):          # ahead of the mouth, and behind the Ark for the mouth camera
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, y, 0), rotation=(math.radians(90), 0, rotz))
    bp = bpy.context.active_object
    bp.scale = (1500, 850, 1)
    bp.data.materials.append(win)


def lattice_tube(path, radius, pal, step=1.6):
    for s in range(len(path) - 1):
        a, b = Vector(path[s]), Vector(path[s + 1])
        seg = b - a
        nseg = max(1, int(seg.length / step))
        R = frame(seg)
        for q in range(nseg):
            cc = a + seg * (q + 0.5) / nseg
            for r in range(8):
                ang = 2 * math.pi * r / 8 + (q % 2) * math.pi / 8
                off = R @ Vector((math.cos(ang) * radius, math.sin(ang) * radius, 0))
                tang = R @ Vector((-math.sin(ang), math.cos(ang), 0))
                diag = (seg.normalized() * 0.7 + tang * (1 if (q + r) % 2 else -1)).normalized()
                add("box", cc + off, (0.14, 0.14, step * 0.55), (pal, "plain", 0.81), fwd=diag)   # its own look: hidden for the mouth camera


for t in range(7):
    pts = []
    x0, z0, ph = rng.uniform(-120, 120), rng.uniform(-60, 60), rng.uniform(0, 6)
    yb = rng.uniform(-260, -130)
    for s in range(30):
        u = s / 29
        pts.append((x0 + 260 * (u - 0.5) * (1 if t % 2 else -1), yb + 60 * math.sin(u * 4 + ph), z0 + 40 * math.sin(u * 3 + ph)))
    lattice_tube(pts, rng.uniform(1.6, 3.2), "Blue")
for s in range(110):                                          # a trail of blocks crossing in front of the mouth
    u = s / 110
    p = Vector((-140 + 280 * u, -250 + 25 * math.sin(u * 5), 20 + 50 * u))
    q = Vector((-140 + 280 * (u + 0.01), -250 + 25 * math.sin((u + 0.01) * 5), 20 + 50 * (u + 0.01)))
    w = 0.5 + 1.2 * u
    add("box", p, (w, w, 1.6), ("Jade", "plain", 1.31), fwd=q - p)   # its own look: hidden for the mouth camera

K.build(bevel_width=0.08)
SQUAD_MATS = {k: m for k, m in K.MATS.items() if k[0] == SQUAD}

# ------------------------------------------------------------------------------------------------ light and air
DOMAIN_LIGHTS = []


def light(kind, name, loc, energy, color, size=0.5, aim=None, angle=40):
    d = bpy.data.lights.new(name, kind)
    d.energy, d.color = energy, color
    if kind == "SPOT":
        d.spot_size, d.spot_blend = math.radians(angle), 0.7
    if kind in ("POINT", "SPOT"):
        d.shadow_soft_size = size
    if kind == "AREA":
        d.size = size
    o = bpy.data.objects.new(name, d)
    col.objects.link(o)
    o.location = loc
    if aim is not None:
        o.rotation_euler = (Vector(aim) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


# the cell's cold light pours in through the open mouth; the back wall's ember answers from behind
light("AREA", "mouth", (0, FRONT - 3, -17.5), 6500, (0.45, 0.62, 1.0), size=26, aim=(0, -50, -18))
light("AREA", "back_glow", (0, BACK - 1.2, -17.5), 1200, (1.0, 0.45, 0.2), size=20, aim=(0, -70, -18))
light("AREA", "board_glow", (BX + 2.0, -63.0, -17.4), 1600, (0.55, 0.5, 1.0), size=10, aim=(-HW, -63.0, -17.4))
for i, p in enumerate(PADS[:2]):
    DOMAIN_LIGHTS.append(light("POINT", f"seat_{i}", p + Vector((0, 0, SEAT_Z - Z0)), 900, (1, 1, 1), 0.6))
for (x, y) in ((-11.5, -84.0), (11.5, -84.0)):
    light("POINT", f"danger_{x}", (x, y, Z0 + 2.0), 700, (1.0, 0.12, 0.06), 0.8)
# the outside, for the mouth camera: a broad cold sky light on the prow
light("SUN", "cell_sun", (0, -200, 120), 3.0, (0.55, 0.68, 1.0), aim=(0, -60, -10))

world = bpy.data.worlds.new("W")
sc.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.1, 0.3, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -66, -17.5))
haze = bpy.context.active_object
haze.scale = (2 * HW - 0.6, 46, Z1 - Z0 - 0.4)
hm = bpy.data.materials.new("haze")
hm.use_nodes = True
hnt = hm.node_tree
hnt.nodes.clear()
ho = hnt.nodes.new("ShaderNodeOutputMaterial")
hv = hnt.nodes.new("ShaderNodeVolumePrincipled")
hv.inputs["Density"].default_value = 0.006
hv.inputs["Color"].default_value = (0.6, 0.68, 1.0, 1)
hnt.links.new(hv.outputs[0], ho.inputs["Volume"])
haze.data.materials.append(hm)

# ------------------------------------------------------------------------------------------------ cameras and render
CAMS_DEF = {
    # hero: low by the back wall's corner, down the line of the three pads to the open mouth and the cell
    "hero": ((4.2, -45.2, -19.4), (-1.0, -92.0, -18.6), 26, 13.0, 2.8),
    # the HyperSea board between the arches, from across the bay
    "board": ((10.5, -50.0, -19.0), (-HW, -64.0, -17.4), 26, 28.0, 4.0),
    # outside, before the prow: the real Ark, its jaws, the mouth open and lit, the squad's prisms inside
    "mouth": ((70.0, -205.0, 26.0), (0.0, -78.0, -12.0), 44, 150.0, 8.0),
}
camd = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", camd)
col.objects.link(cam)
sc.camera = cam
sc.render.engine = "CYCLES"
sc.cycles.device = "CPU"
sc.cycles.samples = SAMPLES
sc.cycles.use_denoising = True
sc.cycles.max_bounces = 4
sc.cycles.volume_bounces = 0
sc.cycles.volume_step_rate = 4.0
sc.cycles.seed = 48
sc.render.resolution_x, sc.render.resolution_y = W, H
sc.render.resolution_percentage = 100
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGB"
sc.view_settings.view_transform = "Standard"
sc.view_settings.look = "None"
sc.view_settings.exposure = -0.3

for cname in CAMS:
    loc, aim, lens, focus, fstop = CAMS_DEF[cname]
    cam.location = loc
    camd.lens = lens
    camd.clip_start = 0.1
    camd.clip_end = 3000
    camd.dof.use_dof = True
    camd.dof.focus_distance = focus
    camd.dof.aperture_fstop = fstop
    cam.rotation_euler = (Vector(aim) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    for o in col.objects:                     # the outside lattice would cross the prow from out there
        if o.name in (str(("Blue", "plain", 0.81)), str(("Jade", "plain", 1.31))):
            o.hide_render = cname == "mouth"
    for dom in DOMS:
        paint_ship(SHIP_MATS["domain"], "domain", *SHIP_DOM[dom])
        for (pal, kind, gain), m in SQUAD_MATS.items():
            b, r = PAL[dom][kind]
            K.recolour(m, b, r, K.LOOK[kind])
        sig = PAL[dom]["plain"][1]
        peak = max(sig)
        for L in DOMAIN_LIGHTS:
            L.data.color = tuple(c / peak for c in sig)
        sc.render.filepath = os.path.join(OUT, f"lobby_{cname}_{dom}.png")
        bpy.ops.render.render(write_still=True)
        print("RENDER", cname, dom, flush=True)
print("DONE")
