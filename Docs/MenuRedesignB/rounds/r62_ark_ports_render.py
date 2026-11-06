"""The Ark's lobby by the game's own light: the hangar of the REAL Ark (ArkReverse.fbx, Docs/VesselModels/ArkReverse.md),
for the Squads tab's backdrop, in the style of Rev 61 C and E (picked by Shombith, 2026-11-06) - and without the vessels.

"No lighting, just the natural game lighting": no light rig at all. The room is lit only by what the game has: the cell
outside (its picture as the sky) coming in through the open mouth and through PORTHOLES cut along the hangar walls,
and the game's own glowing materials - the prisms' rims, the hull's domain trim, the HyperSea board's screen. Each
porthole is placed on the model's real wall (a ray cast from the bay's axis), a heavy ring of the hull's metal with
bolts, and its pane is a portal: it shows the cell in the direction you look through it, and lets its light in.

The launch pads are bare: no vessel models and no seat markers over them (the cards on top say who sits where).
Everything else as r61: the lit hull panels,
the prisms (glossy black faces, the rim on their edges), the spent prisms heaped dead along the walls, haze, AgX.

    <bpy python> r62_ark_ports_render.py <out_dir> <assets_dir> [cams=pads,board,aft,ports,cross,porthole]
                                       [domains=Jade,Ruby,Gold] [W H samples]

assets_dir: window.png (the cell: an Amoebius screenshot of the menu's live cell, HUD hidden), board_sector.png and
board_galaxy.png (the Voyage Board's Sector and Arm charts). Writes <out_dir>/lobby_<cam>_<dom>.png.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

OUT, ASSETS = sys.argv[1], sys.argv[2]
CAMS = (sys.argv[3] if len(sys.argv) > 3 else "pads,board,aft,ports,cross,porthole").split(",")
DOMS = (sys.argv[4] if len(sys.argv) > 4 else "Jade,Ruby,Gold").split(",")
W = int(sys.argv[5]) if len(sys.argv) > 5 else 1920
H = int(sys.argv[6]) if len(sys.argv) > 6 else 1080
SAMPLES = int(sys.argv[7]) if len(sys.argv) > 7 else 64
os.makedirs(OUT, exist_ok=True)
REPO = os.environ.get("COSMIC_SHORE_REPO") or os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
MODELS = os.path.join(REPO, "Assets/_Models/Vessel Models")

bpy.ops.wm.read_factory_settings(use_empty=True)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r62_prismkit as K  # noqa: E402  (binds to the fresh scene)
from r62_prismkit import add, rand_rot, frame, PAL, SQUAD, DOMS3  # noqa: E402

sc, col, rng = K.sc, K.col, K.rng
L = None


# ------------------------------------------------------------------------------------------------ hull materials

def hull_mat(name, base, metal=0.85, panel=2.6, emit=None):
    """A ship surface, lit: rectangular hull panels (seams on a 3D grid, shown only across a face, never flooding it,
    so a wall gets vertical and horizontal seams and a deck gets both its own), each panel its own shade, a finer
    grid of sub-plates, grime, gloss that wanders, scratches. `panel` is the plate size in metres (Unity units).
    `emit` (colour, strength) adds running-light emission on top."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    ln = nt.links.new

    def node(kind, **kw):
        n = nt.nodes.new(kind)
        for k, v in kw.items():
            setattr(n, k, v)
        return n

    def math(op, a, b=None, clamp=False):
        n = node("ShaderNodeMath", operation=op, use_clamp=clamp)
        for i, x in enumerate((a, b)):
            if x is None:
                continue
            if isinstance(x, (int, float)):
                n.inputs[i].default_value = x
            else:
                ln(x, n.inputs[i])
        return n.outputs[0]

    o = node("ShaderNodeOutputMaterial")
    p = node("ShaderNodeBsdfPrincipled", name="pbr")
    p.inputs["Metallic"].default_value = metal
    tc = node("ShaderNodeTexCoord")
    pos = node("ShaderNodeSeparateXYZ"); ln(tc.outputs["Object"], pos.inputs[0])
    nrm = node("ShaderNodeSeparateXYZ"); ln(tc.outputs["Normal"], nrm.inputs[0])

    def grid(size, width, ratios=(1.0, 1.7, 0.75)):
        seams = None
        for i in range(3):
            sz = size * ratios[i]
            t = math("DIVIDE", pos.outputs[i], sz)
            f = math("FRACT", math("ADD", t, 0.5))
            d = math("MULTIPLY", math("ABSOLUTE", math("SUBTRACT", f, 0.5)), sz)     # metres to the nearest seam
            line = math("SUBTRACT", 1.0, math("DIVIDE", d, width), clamp=True)
            wgt = math("MULTIPLY", math("SUBTRACT", 1.0, math("ABSOLUTE", nrm.outputs[i])), 4.0, clamp=True)
            sm = math("MULTIPLY", line, wgt)
            seams = sm if seams is None else math("MAXIMUM", seams, sm)
        return seams

    big = grid(panel, 0.045 * max(1.0, panel / 2.6))
    small = grid(panel / 3.0, 0.02 * max(1.0, panel / 2.6), (1.0, 0.6, 1.3))
    seams = math("MAXIMUM", big, math("MULTIPLY", small, 0.45))                  # 1 in a seam
    plate = math("SUBTRACT", 1.0, seams)
    # each panel its own shade: white noise on the panel's cell index
    cell = node("ShaderNodeVectorMath", operation="DIVIDE")
    ln(tc.outputs["Object"], cell.inputs[0]); cell.inputs[1].default_value = (panel, panel * 1.7, panel * 0.75)
    fl = node("ShaderNodeVectorMath", operation="FLOOR"); ln(cell.outputs[0], fl.inputs[0])
    wn = node("ShaderNodeTexWhiteNoise", noise_dimensions="3D"); ln(fl.outputs[0], wn.inputs["Vector"])
    shade = node("ShaderNodeMapRange"); shade.inputs[3].default_value = 0.5; shade.inputs[4].default_value = 1.4
    ln(wn.outputs["Value"], shade.inputs[0])
    gr = node("ShaderNodeTexNoise"); gr.inputs["Scale"].default_value = 0.35; gr.inputs["Detail"].default_value = 9.0
    gr.inputs["Roughness"].default_value = 0.62
    ln(tc.outputs["Object"], gr.inputs["Vector"])
    grime = node("ShaderNodeMapRange"); grime.inputs[1].default_value = 0.38; grime.inputs[2].default_value = 0.68
    grime.inputs[3].default_value = 0.5; grime.inputs[4].default_value = 1.1
    ln(gr.outputs["Fac"], grime.inputs[0])
    val = math("MULTIPLY", math("MULTIPLY", shade.outputs[0], grime.outputs[0]),
               math("ADD", math("MULTIPLY", plate, 0.8), 0.2))
    bc = node("ShaderNodeMix", data_type="RGBA", blend_type="MULTIPLY", name="base")
    bc.inputs["Factor"].default_value = 1.0
    bc.inputs[6].default_value = (*base, 1)
    cc = node("ShaderNodeCombineColor")
    for i in range(3):
        ln(val, cc.inputs[i])
    ln(cc.outputs[0], bc.inputs[7])
    ln(bc.outputs[2], p.inputs["Base Color"])
    scr = node("ShaderNodeTexNoise"); scr.inputs["Scale"].default_value = 9.0; scr.inputs["Detail"].default_value = 14.0
    ln(tc.outputs["Object"], scr.inputs["Vector"])
    rg = node("ShaderNodeMapRange"); rg.inputs[3].default_value = 0.16; rg.inputs[4].default_value = 0.6
    ln(gr.outputs["Fac"], rg.inputs[0])
    rough = math("ADD", math("MULTIPLY", scr.outputs["Fac"], 0.12), rg.outputs[0])
    rough = math("MAXIMUM", rough, math("MULTIPLY", seams, 0.9))
    ln(rough, p.inputs["Roughness"])
    bump = node("ShaderNodeBump"); bump.inputs["Strength"].default_value = 0.7; bump.inputs["Distance"].default_value = 0.05
    ln(math("ADD", plate, math("MULTIPLY", scr.outputs["Fac"], 0.06)), bump.inputs["Height"])
    ln(bump.outputs[0], p.inputs["Normal"])
    if emit is not None:
        p.inputs["Emission Color"].default_value = (*emit[0], 1)
        p.inputs["Emission Strength"].default_value = emit[1]
    ln(p.outputs[0], o.inputs[0])
    return m


def glass_mat(name, tint, glow):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (0.01, 0.012, 0.02, 1)
    p.inputs["Roughness"].default_value = 0.05
    p.inputs["Coat Weight"].default_value = 1.0
    p.inputs["Emission Color"].default_value = (*tint, 1)
    p.inputs["Emission Strength"].default_value = glow
    return m


BODY = hull_mat("hull_body", (0.16, 0.17, 0.19), metal=0.7, panel=3.2)
BODY_V = hull_mat("vessel_body", (0.2, 0.21, 0.24), metal=0.7, panel=0.9)        # the vessels: smaller plates
DOMAIN_ARK = hull_mat("hull_domain", (0.02, 0.02, 0.025), metal=0.5, panel=3.2, emit=((0, 1, 1), 0.35))
DOMAIN_V = hull_mat("vessel_domain", (0.02, 0.02, 0.025), metal=0.5, panel=0.9, emit=((0, 1, 1), 1.2))
ENGINE = hull_mat("engine", (0.05, 0.02, 0.01), metal=0.3, panel=0.9, emit=((1.0, 0.32, 0.08), 6.0))
ENGINE_ARK = hull_mat("engine_ark", (0.11, 0.1, 0.1), metal=0.75, panel=1.4, emit=((1.0, 0.45, 0.25), 0.008))   # the pads and jets at rest
WINDOW = glass_mat("window", (0.25, 0.45, 1.0), 1.2)
# the squad's domain (Nocturne ShipColor2: the domain slot's face colour), as light
SHIP_DOM = {"Jade": (0.079, 0.749, 0.737), "Ruby": (0.707, 0.102, 0.749), "Gold": (1.22, 0.688, 0.089)}


def set_domain(dom):
    c = SHIP_DOM[dom]
    peak = max(c)
    for m in (DOMAIN_ARK, DOMAIN_V):
        m.node_tree.nodes["pbr"].inputs["Emission Color"].default_value = (*[v / peak for v in c], 1)
        m.node_tree.nodes["base"].inputs[6].default_value = (*[0.02 + 0.22 * v / peak for v in c], 1)   # painted in the domain


def paint(mesh, roles, body, domain, engine=None):
    for i, s in enumerate(mesh.data.materials):
        r = roles.get(s.name.split(".")[0], "body")
        mesh.data.materials[i] = {"body": body, "domain": domain, "engine": engine or ENGINE, "window": WINDOW}[r]


FLEET = {"Domain": "domain", "Body": "body", "Window": "window", "Engine": "engine"}
ARTIST = {"accent": "domain", "BASE": "body", "windsheild": "window", "N": "engine"}

# ------------------------------------------------------------------------------------------------ the Ark itself
bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, "ArkReverse.fbx"), use_custom_normals=True, ignore_leaf_bones=False,
                         automatic_bone_orientation=False, use_anim=True)
arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
ark = [o for o in bpy.data.objects if o.type == "MESH"][0]
arm.data.pose_position = "POSE"
act = bpy.data.actions.get("rig|rig|hangaropen")
arm.animation_data_create()
arm.animation_data.action = act
sc.frame_set(int(act.frame_range[0]))
paint(ark, FLEET, BODY, DOMAIN_ARK, ENGINE_ARK)

# the hull's prisms in the model's own sockets, each socket's kind at its real size, in the Ark's (the squad's) domain
SLAB = (1.8, 0.8, 4.5)
OCTA_H, STELLA_H = 2.4, 3.3
for b in arm.pose.bones:
    if not b.name.startswith("socket."):
        continue
    M = arm.matrix_world @ b.matrix
    pos, R, kind = M.to_translation(), M.to_3x3().normalized(), b.name.split(".")[1]
    if kind == "prism":
        add("box", pos, (SLAB[0], SLAB[2], SLAB[1]), (SQUAD, "plain", 1.0), rot=R)
    elif kind == "danger":
        add("blade", pos, (3.2, 3.2, 5.5), ("Red", "danger", 1.0), rot=R)
    elif kind == "shielded":
        add("octa", pos, (OCTA_H, OCTA_H, OCTA_H * 1.2), (SQUAD, "shielded", 1.0), rot=R)
    elif kind == "supershielded":
        add("stella", pos, (STELLA_H,) * 3, (SQUAD, "super", 1.0), rot=R)

# ------------------------------------------------------------------------------------------------ portholes
HW, Z0, Z1, BACK, FRONT = 14.0, -23.0, -12.0, -44.0, -88.0
bpy.context.view_layer.update()
_dg = bpy.context.evaluated_depsgraph_get()


def wall_hit(origin, direction):
    ok, loc, nrm, _i, ob, _m = sc.ray_cast(_dg, Vector(origin), Vector(direction).normalized(), distance=40.0)
    return (loc, nrm) if ok and ob == ark else (None, None)


def portal_mat():
    """The pane: the cell seen in the direction of the look (the world's own picture), so it has true parallax, plus a
    faint glass reflection. Its emission is the outside's light coming in."""
    m = bpy.data.materials.new("porthole_pane")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    ln = nt.links.new
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    g = nt.nodes.new("ShaderNodeNewGeometry")
    neg = nt.nodes.new("ShaderNodeVectorMath"); neg.operation = "SCALE"; neg.inputs["Scale"].default_value = -1.0
    ln(g.outputs["Incoming"], neg.inputs[0])
    tx = nt.nodes.new("ShaderNodeTexEnvironment"); tx.image = bpy.data.images.load(os.path.join(ASSETS, "window.png"))
    ln(neg.outputs[0], tx.inputs["Vector"])
    em = nt.nodes.new("ShaderNodeEmission"); em.name = "sky"; em.inputs[1].default_value = PANE_GLOW
    ln(tx.outputs[0], em.inputs[0])
    gl = nt.nodes.new("ShaderNodeBsdfGlossy"); gl.inputs["Roughness"].default_value = 0.04
    gl.inputs["Color"].default_value = (0.08, 0.09, 0.11, 1)
    ad = nt.nodes.new("ShaderNodeAddShader"); ln(em.outputs[0], ad.inputs[0]); ln(gl.outputs[0], ad.inputs[1])
    ln(ad.outputs[0], o.inputs[0])
    return m


PANE_GLOW = 0.75
PANE = portal_mat()
PORTS = []


def porthole(loc, nrm, r=1.05):
    """A porthole on the wall at loc, facing nrm (into the room): a pane, a heavy ring, eight bolts."""
    z = Vector(nrm).normalized()
    R = K.frame(z)
    M = Matrix.Translation(loc + z * 0.06) @ R.to_4x4()
    bpy.ops.mesh.primitive_circle_add(vertices=48, radius=r, fill_type="NGON")
    p = bpy.context.active_object
    p.matrix_world = M
    p.data.materials.append(PANE)
    bpy.ops.mesh.primitive_torus_add(major_radius=r + 0.16, minor_radius=0.2, major_segments=48, minor_segments=12)
    t = bpy.context.active_object
    t.matrix_world = Matrix.Translation(loc + z * 0.12) @ R.to_4x4() @ Matrix.Diagonal((1, 1, 0.7, 1))
    t.data.materials.append(BODY)
    for k in range(8):
        a = k * math.tau / 8 + math.pi / 8
        add("box", loc + z * 0.3 + R @ Vector((math.cos(a) * (r + 0.42), math.sin(a) * (r + 0.42), 0)), (0.07, 0.07, 0.06),
            ("Blue", "plain", 0.05), rot=R)
    PORTS.append(p)


# along both side walls, eye height over the heaps; none behind the HyperSea board (port) or the galaxy (starboard)
for side in (-1, 1):
    for y in (-47.5, -52.0, -56.5, -61.0, -65.5, -70.0, -74.5, -79.0):
        if side < 0 and -71.5 < y < -54.5:
            continue
        if side > 0 and -61.5 < y < -52.5:
            continue
        loc, nrm = wall_hit((0, y, -15.4), (side, 0, 0))
        if loc is None or abs(loc.x) < 11.0 or abs(nrm.x) < 0.6:
            continue
        porthole(loc, nrm if nrm.x * side < 0 else -nrm)
# two in the back wall, either side of the tunnel
for x in (-8.5, 8.5):
    loc, nrm = wall_hit((x, -60.0, -15.0), (0, 1, 0))
    if loc is not None and abs(nrm.y) > 0.6:
        porthole(loc, nrm if nrm.y < 0 else -nrm, r=0.9)
print("PORTHOLES", len(PORTS), flush=True)

# ------------------------------------------------------------------------------------------------ the hangar, dressed
CAM_LOCS = [
    # C, kept: from the door, down the pads to the back wall's tunnel
    ((7.0, -82.0, -17.0), (-0.5, -57.0, -20.6), 32, 21.0, 3.2, "pads"),
    # E, kept: the HyperSea board between the arches
    ((11.0, -59.5, -17.0), (-HW, -63.5, -17.4), 26, 25.0, 4.0, "board"),
    # from the back wall, high: down the bay to the open mouth, the portholes on both walls
    ((0.0, -46.0, -14.6), (0.0, -84.0, -19.5), 22, 22.0, 3.5, "aft"),
    # along the starboard wall: its row of portholes receding, the pads beside them
    ((-8.5, -46.2, -15.0), (12.5, -73.0, -16.4), 24, 16.0, 3.2, "ports"),
    # across the bay from the starboard corner: the pads before the board
    ((11.5, -73.0, -13.6), (-6.0, -59.0, -21.0), 20, 13.0, 3.5, "cross"),
    # at a porthole: the cell through the glass, the room behind
    ((9.8, -62.0, -15.7), (13.6, -66.4, -15.4), 34, 5.6, 2.2, "porthole"),
]
PADS = [Vector((0.0, -52.0, Z0)), Vector((0.0, -64.0, Z0)), Vector((0.0, -76.0, Z0))]
# the pads are left bare: no seat markers over them (Shombith, 2026-11-06: "remove these")

# the HyperSea board between the arches on the left wall; the galaxy on the right wall
BX = -HW + 0.9
board = K.image_emission("board_sector", os.path.join(ASSETS, "board_sector.png"), 0.45)
bpy.ops.mesh.primitive_plane_add(size=1, location=(BX, -63.0, -17.4), rotation=(math.radians(90), 0, math.radians(-90)))
bs = bpy.context.active_object
bs.scale = (13.0, 8.5, 1)
bs.data.materials.append(board)
gal = K.image_emission("board_galaxy", os.path.join(ASSETS, "board_galaxy.png"), 0.35)
bpy.ops.mesh.primitive_plane_add(size=1, location=(-BX, -57.0, -16.6), rotation=(math.radians(90), 0, math.radians(90)))
bg2 = bpy.context.active_object
bg2.scale = (6.4, 4.7, 1)
bg2.data.materials.append(gal)
# each board in a heavy frame of the hull's own metal, a glass sheet over it
for (x, cy, cz, w, h) in ((BX, -63.0, -17.4, 13.0, 8.5), (-BX, -57.0, -16.6, 6.4, 4.7)):
    sx = 0.2 if x < 0 else -0.2
    for (yy, zz, sy, sz) in ((cy, cz + h / 2 + 0.3, w / 2 + 0.6, 0.3), (cy, cz - h / 2 - 0.3, w / 2 + 0.6, 0.3),
                             (cy - w / 2 - 0.3, cz, 0.3, h / 2), (cy + w / 2 + 0.3, cz, 0.3, h / 2)):
        bpy.ops.mesh.primitive_cube_add(size=2, location=(x + sx * 0.5, yy, zz))
        fr = bpy.context.active_object
        fr.scale = (0.35, sy, sz)
        fr.data.materials.append(BODY)
        bvm = fr.modifiers.new("b", "BEVEL"); bvm.width = 0.06; bvm.segments = 2
    bpy.ops.mesh.primitive_plane_add(size=1, location=(x + sx * 0.4, cy, cz), rotation=(math.radians(90), 0, math.radians(-90 if x < 0 else 90)))
    gl = bpy.context.active_object
    gl.scale = (w, h, 1)
    gm = bpy.data.materials.new("board_glass")
    gm.use_nodes = True
    gp = gm.node_tree.nodes["Principled BSDF"]
    gp.inputs["Base Color"].default_value = (0, 0, 0, 1)
    gp.inputs["Roughness"].default_value = 0.08
    gp.inputs["Alpha"].default_value = 0.12
    gl.data.materials.append(gm)


def heap(cx, cy, rx, ry, count, hmax, z0=Z0):
    for _ in range(count):
        x, y = cx + rng.gauss(0, rx), cy + rng.gauss(0, ry)
        x = max(-HW + 0.8, min(HW - 0.8, x))
        y = max(FRONT + 3, min(BACK - 0.8, y))
        d = math.hypot((x - cx) / (rx * 2.2), (y - cy) / (ry * 2.2))
        h = max(0.0, hmax * (1 - d)) * rng.random() + rng.uniform(0, 0.4)
        pal = rng.choice(DOMS3 + ["Blue"] * 7)
        kind = rng.choices(["plain", "shielded", "super"], [0.86, 0.11, 0.03])[0]
        s = rng.uniform(0.2, 0.7)
        shape = K.kind_shape(kind)
        size = (s * rng.uniform(0.8, 2.8), s * 0.5, s * 0.5) if shape == "box" else (s * 0.8,) * 3
        add(shape, (x, y, z0 + h), size, (pal, kind, rng.choice((0.08, 0.08, 0.08, 0.15, 0.15, 0.4))), rot=rand_rot())   # spent: most are dead


# spent prisms heaped along both walls, deepest by the back wall's corners, clear of the pads and the vessels
heap(-11.5, -48.0, 1.3, 2.2, 650, 5.0)
heap(11.5, -47.0, 1.3, 2.2, 650, 5.5)
heap(-12.0, -71.0, 0.9, 3.5, 380, 3.0)
heap(12.0, -75.0, 0.9, 4.0, 420, 3.5)
NEAR = [Vector(v[0]) for v in CAM_LOCS]   # the cameras: no junk at the lens
# danger thickets in the mouth's corners: the way out has teeth
for (x, y, n) in ((-11.5, -84.0, 9), (11.5, -84.0, 9), (-9.5, -80.5, 5), (10.0, -79.5, 5)):
    for _ in range(n):
        d = Vector((rng.uniform(-0.7, 0.7), rng.uniform(-0.7, 0.7), 1)).normalized()
        add("blade", (x, y, Z0 + rng.uniform(0, 1.0)), (1.2, 1.4, rng.uniform(1.0, 3.2)), ("Red", "danger", 0.6), fwd=d)
# block chains hanging from the arches, against the walls
for cch in range(5):
    x0, y0 = rng.choice((-1, 1)) * rng.uniform(8.5, 12.5), rng.uniform(-82, -46)
    if min((Vector((x0, y0, -16.0)) - n).length for n in NEAR) < 6.0:
        continue
    Lc = rng.uniform(2.0, 6.0)
    pal = rng.choice(DOMS3 + ["Blue"] * 3)
    for s in range(int(Lc / 0.5)):
        add("box", (x0, y0, Z1 - 0.3 - s * 0.5), (0.11, 0.11, 0.22), (pal, "plain", 0.15), fwd=(0, 0, 1), up=(0, 1, 0))
# dust: fine shards in the air, few and small
for _ in range(320):
    p = (rng.uniform(-12.5, 12.5), rng.uniform(-86, -45), rng.uniform(Z0 + 1.0, Z1 - 0.6))
    if min((Vector(p) - n).length for n in NEAR) < 8.0 or (abs(p[0]) < 7.5 and -72 < p[1] < -44):
        continue
    pal = rng.choice(DOMS3 + ["Blue"] * 4)
    s = rng.uniform(0.03, 0.1)
    add("box", p, (s * 2.2, s, s), (pal, "plain", 0.15), rot=rand_rot())

# ------------------------------------------------------------------------------------------------ outside
win = K.image_emission("cell_view", os.path.join(ASSETS, "window.png"), 0.22)
BACKDROPS = []
for (y, rotz) in ((-420.0, 0.0), (420.0, math.pi)):          # seen through the mouth from inside; outside, the world is the cell
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, y, 0), rotation=(math.radians(90), 0, rotz))
    bp = bpy.context.active_object
    BACKDROPS.append(bp)
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
                add("box", cc + off, (0.14, 0.14, step * 0.55), (pal, "plain", 0.81), fwd=diag)   # hidden outside


for t in range(7):
    pts = []
    x0, z0, ph = rng.uniform(-120, 120), rng.uniform(-60, 60), rng.uniform(0, 6)
    yb = rng.uniform(-260, -130)
    for s in range(30):
        u = s / 29
        pts.append((x0 + 260 * (u - 0.5) * (1 if t % 2 else -1), yb + 60 * math.sin(u * 4 + ph), z0 + 40 * math.sin(u * 3 + ph)))
    lattice_tube(pts, rng.uniform(1.6, 3.2), "Blue")

K.build(bevel_width=0.06)
SQUAD_MATS = {k: m for k, m in K.MATS.items() if k[0] == SQUAD}

# ------------------------------------------------------------------------------------------------ air (no lights: the cell and the game's own glow)
world = bpy.data.worlds.new("W")
sc.world = world
world.use_nodes = True
wn = world.node_tree
env = wn.nodes.new("ShaderNodeTexEnvironment")
env.image = bpy.data.images.load(os.path.join(ASSETS, "window.png"))
wn.links.new(env.outputs[0], wn.nodes["Background"].inputs[0])
WORLD_BG = wn.nodes["Background"]
WORLD_BG.inputs[1].default_value = PANE_GLOW
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
hv.inputs["Color"].default_value = (0.7, 0.75, 0.9, 1)
hv.inputs["Anisotropy"].default_value = 0.35
hnt.links.new(hv.outputs[0], ho.inputs["Volume"])
haze.data.materials.append(hm)

# ------------------------------------------------------------------------------------------------ cameras and render
CAMS_DEF = {c[5]: c[:5] for c in CAM_LOCS}
camd = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", camd)
col.objects.link(cam)
sc.camera = cam
sc.render.engine = "CYCLES"
sc.cycles.device = "CPU"
sc.cycles.samples = SAMPLES
sc.cycles.use_denoising = True
sc.cycles.max_bounces = 6
sc.cycles.glossy_bounces = 3
sc.cycles.volume_bounces = 0
sc.cycles.volume_step_rate = 4.0
sc.cycles.blur_glossy = 1.0
sc.cycles.seed = 60
sc.render.resolution_x, sc.render.resolution_y = W, H
sc.render.resolution_percentage = 100
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGB"
sc.view_settings.view_transform = "AgX"
for look in ("AgX - Medium High Contrast", "AgX - Base Contrast", "Medium High Contrast"):
    try:
        sc.view_settings.look = look
        break
    except TypeError:
        continue
sc.view_settings.exposure = 0.0
EXPOSURE = float(os.environ.get("ARK_EXPOSURE", "1.4"))

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
    if os.environ.get("ARK_PROBE"):    # print each porthole's place in this camera and skip the render
        from bpy_extras.object_utils import world_to_camera_view
        bpy.context.view_layer.update()
        seen = [world_to_camera_view(sc, cam, p.matrix_world.translation) for p in PORTS]
        print("PROBE", cname, sum(1 for q in seen if 0 <= q.x <= 1 and 0 <= q.y <= 1 and q.z > 0), "portholes in view", flush=True)
        continue
    for o in col.objects:
        if o.name == str(("Blue", "plain", 0.81)):     # the outside lattice: only through the mouth
            o.hide_render = False
    sc.view_settings.exposure = EXPOSURE
    for dom in DOMS:
        set_domain(dom)
        for (pal, kind, gain), m in SQUAD_MATS.items():
            b, r = PAL[dom][kind]
            K.recolour(m, b, r, K.LOOK[kind])
        sc.render.filepath = os.path.join(OUT, f"lobby_{cname}_{dom}.png")
        bpy.ops.render.render(write_still=True)
        print("RENDER", cname, dom, flush=True)
print("DONE")
