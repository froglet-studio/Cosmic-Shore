"""The Ark's lobby, filmed: the REAL Ark (ArkReverse.fbx, Garrett's Ark, Docs/VesselModels/ArkReverse.md) with the
squad's REAL vessels (SparrowModel1.fbx, dolphin_shapekey_with_animations.fbx), for the Squads tab's backdrop.

Rev 59 read as a cartoon (Shombith, 2026-11-06): flat self-lit surfaces, a strong blue fill light everywhere, the
Standard view transform clipping every glow to a flat colour, candy-coloured debris. This render keeps the game's
pieces and its palette and films them physically:
- AgX tone mapping (highlights roll off instead of clipping), a near-black world, every light motivated: the cell's
  cold light through the open mouth, the back wall's ember, the pad lights, strip lights in the arches;
- the hull lit, not self-lit: dark gunmetal plates with seams, wear and varying gloss (procedural, no textures),
  the domain slot as the hull's running lights in the squad's domain (Nocturne ShipColor2), engines as embers,
  glass as glass;
- the prisms as the game draws them, but lit: near-black glossy faces that reflect the room, their bevelled edges
  glowing in the rim colour (Nocturne's block colours), the fresnel kept faint;
- haze in the hangar (a volume), depth of field, and in the grade bloom, chromatic fringing and film grain.

The squad's vessels: the leader's Sparrow and Rogue One's Dolphin, at their real sizes (12.5 u and 5.3 u). Inside they
stand on their launch pads (`hangar.slot.0..1`, head on the pad, tail toward the door); the third pad is KEPT for a
friend, empty but for a dark prism outline. Outside they fly beside the Ark in the squad's domain, each laying the
game's block trail.

    <bpy python> r61_ark_real_render.py <out_dir> <assets_dir> [cams=beside,flank,pads,cross,board] [domains=Jade,Ruby,Gold]
                                   [W H samples]

assets_dir: window.png (the cell: an Amoebius screenshot of the menu's live cell, HUD hidden), board_sector.png and
board_galaxy.png (the Voyage Board's Sector and Arm charts). Writes <out_dir>/lobby_<cam>_<dom>.png.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

OUT, ASSETS = sys.argv[1], sys.argv[2]
CAMS = (sys.argv[3] if len(sys.argv) > 3 else "beside,flank,pads,cross,board").split(",")
DOMS = (sys.argv[4] if len(sys.argv) > 4 else "Jade,Ruby,Gold").split(",")
W = int(sys.argv[5]) if len(sys.argv) > 5 else 1920
H = int(sys.argv[6]) if len(sys.argv) > 6 else 1080
SAMPLES = int(sys.argv[7]) if len(sys.argv) > 7 else 64
os.makedirs(OUT, exist_ok=True)
REPO = os.environ.get("COSMIC_SHORE_REPO") or os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
MODELS = os.path.join(REPO, "Assets/_Models/Vessel Models")

bpy.ops.wm.read_factory_settings(use_empty=True)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r61_prismkit as K  # noqa: E402  (binds to the fresh scene)
from r61_prismkit import add, rand_rot, frame, PAL, SQUAD, DOMS3  # noqa: E402

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
ENGINE_ARK = hull_mat("engine_ark", (0.11, 0.1, 0.1), metal=0.75, panel=1.4, emit=((1.0, 0.36, 0.12), 0.05))   # the pads and jets at rest
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

# ------------------------------------------------------------------------------------------------ the vessels
dg = bpy.context.evaluated_depsgraph_get()


def load_vessel(fname, roles, nose_y=1):
    """Import a vessel FBX at rest, freeze its skinned mesh (no armature), paint it; return the frozen mesh data."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, fname), use_custom_normals=True, ignore_leaf_bones=False,
                             automatic_bone_orientation=False, use_anim=False)
    new = [o for o in bpy.data.objects if o not in before]
    for a in new:
        if a.type == "ARMATURE":
            a.data.pose_position = "REST"
    for o in new:
        if o.type == "MESH" and o.data.shape_keys:
            for kb in o.data.shape_keys.key_blocks:
                kb.value = 0.0
    bpy.context.view_layer.update()
    src = [o for o in new if o.type == "MESH"][0]
    d = bpy.context.evaluated_depsgraph_get()
    ev = src.evaluated_get(d)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=d)
    me.transform(src.matrix_world)                      # world-at-import space: nose and up as the file has them
    if nose_y < 0:                                      # turn it so the nose is at +y for place()
        me.transform(Matrix.Rotation(math.pi, 4, "Z"))
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    holder = bpy.data.objects.new("tmp", me)
    paint(holder, roles, BODY_V, DOMAIN_V)
    bpy.data.objects.remove(holder, do_unlink=True)
    return me


SPARROW = load_vessel("SparrowModel1.fbx", FLEET)
DOLPHIN = load_vessel("dolphin_shapekey_with_animations.fbx", ARTIST, nose_y=-1)   # its nose is at -y (Dolphin.md: aft -2.47, nose +2.83)
for me in (SPARROW, DOLPHIN):                            # which way is the nose? the longer half of y
    ys = [v.co.y for v in me.vertices]
    print("NOSE", me.name, round(min(ys), 2), round(max(ys), 2))

VESSEL_OBJS = {"in": [], "out": []}


def place(me, where, pos, nose, up=(0, 0, 1), bank=0.0):
    o = bpy.data.objects.new(f"{me.name}_{where}", me)
    col.objects.link(o)
    f = Vector(nose).normalized()
    u = Vector(up)
    x = f.cross(u).normalized()
    z = x.cross(f).normalized()
    Rm = Matrix((x, f, z)).transposed()                 # model x -> right, y (nose) -> f, z -> up
    Rm = Rm @ Matrix.Rotation(bank, 3, "Y")
    o.matrix_world = Matrix.Translation(pos) @ Rm.to_4x4()
    VESSEL_OBJS[where].append(o)
    return o


# ------------------------------------------------------------------------------------------------ the hangar, dressed
HW, Z0, Z1, BACK, FRONT = 14.0, -23.0, -12.0, -44.0, -88.0
PADS = [Vector((0.0, -52.0, Z0)), Vector((0.0, -64.0, Z0)), Vector((0.0, -76.0, Z0))]
# head on the pad, +3 up, the tail toward the door (ArkReverse.md): the noses point at the back wall
place(SPARROW, "in", PADS[0] + Vector((0, 0, 3.0)), (0, 1, 0))
place(DOLPHIN, "in", PADS[1] + Vector((0, 0, 2.2)), (0, 1, 0))
# the kept seat: an outline of dark blocks over the empty pad, the shape a prism would take
c = PADS[2] + Vector((0, 0, 2.6))
Rr = rand_rot()
verts = [Rr @ (Vector(v) * 0.8) for v in K.STELLA_V]
for (ia, ib, ic) in K.STELLA_F:
    for (u, w) in ((ia, ib), (ib, ic), (ic, ia)):
        pa, pb = verts[u], verts[w]
        for q in range(6):
            cc = pa.lerp(pb, (q + 0.5) / 6)
            add("box", c + cc, (0.06, 0.06, (pb - pa).length / 6 * 0.42), ("Blue", "plain", 0.3), fwd=pb - pa)

# the HyperSea board between the arches on the left wall; the galaxy on the right wall
BX = -HW + 0.9
board = K.image_emission("board_sector", os.path.join(ASSETS, "board_sector.png"), 0.9)
bpy.ops.mesh.primitive_plane_add(size=1, location=(BX, -63.0, -17.4), rotation=(math.radians(90), 0, math.radians(-90)))
bs = bpy.context.active_object
bs.scale = (13.0, 8.5, 1)
bs.data.materials.append(board)
gal = K.image_emission("board_galaxy", os.path.join(ASSETS, "board_galaxy.png"), 1.3)
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
NEAR = [Vector((7.0, -82.0, -17.0)), Vector((11.0, -59.5, -17.0)), Vector((11.5, -73.0, -13.6))]   # the cameras inside
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

# the escorts beside the Ark, flying with it (nose -y like the Ark), each laying the game's block trail behind it
HEADING = Vector((-0.75, -0.66, 0.0)).normalized()          # peeling across the prow, out toward the cell
ESCORTS = [(SPARROW, Vector((60.0, -187.0, 10.5)), -0.42), (DOLPHIN, Vector((67.2, -169.5, 16.3)), -0.3)]   # banked to show their backs
for me, pos, bank in ESCORTS:
    place(me, "out", pos, HEADING, bank=bank)
    span = 12.5 if me is SPARROW else 5.3
    back = -HEADING
    side = Vector((0, 0, 1)).cross(back).normalized()
    for s in range(60):                                 # the game's block trail, laid where it flew: a gentle curve
        u = s / 60
        q = pos + back * (span * 0.5 + s * 1.05) + side * (8 * u * u) + Vector((0, 0, -3 * u * u))
        nxt = pos + back * (span * 0.5 + (s + 1) * 1.05) + side * (8 * (u + 0.017) ** 2) + Vector((0, 0, -3 * (u + 0.017) ** 2))
        add("box", q, (0.32 + 0.1 * u, 0.14, 0.42), (SQUAD, "plain", 0.9), fwd=nxt - q, up=(0, 0, 1))

K.build(bevel_width=0.06)
SQUAD_MATS = {k: m for k, m in K.MATS.items() if k[0] == SQUAD}

# ------------------------------------------------------------------------------------------------ light and air


def light(kind, name, loc, energy, color, size=0.5, aim=None, angle=40, group=None):
    d = bpy.data.lights.new(name, kind)
    d.energy, d.color = energy, color
    if kind == "SPOT":
        d.spot_size, d.spot_blend = math.radians(angle), 0.6
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


DOMAIN_LIGHTS = []
# the cell's cold light through the open mouth: a broad soft fill and a hard shaft that the haze shows
light("AREA", "mouth", (0, FRONT - 3, -17.5), 1400, (0.5, 0.64, 1.0), size=24, aim=(0, -50, -18))
light("SPOT", "mouth_shaft", (6, FRONT - 30, -6), 90000, (0.55, 0.7, 1.0), size=2.0, aim=(-2, -60, -22), angle=26)
# the back wall's ember
light("AREA", "back_glow", (0, BACK - 1.2, -17.5), 160, (1.0, 0.5, 0.25), size=18, aim=(0, -70, -18))
light("AREA", "overhead", (0, -64.0, Z1 - 0.3), 900, (0.62, 0.72, 1.0), size=16, aim=(0, -64.0, Z0))
# strip lights along the arches, cold white, thin
for y in (-50.0, -58.0, -66.0, -74.0, -82.0):
    light("AREA", f"strip_{y}", (0, y, Z1 - 0.4), 120, (0.85, 0.9, 1.0), size=3.0, aim=(0, y, Z0))
# the pads, lit from below in the squad's domain
for i, p in enumerate(PADS):
    DOMAIN_LIGHTS.append(light("POINT", f"pad_{i}", p + Vector((0, 0, 0.6)), 380 if i < 2 else 120, (1, 1, 1), 2.5))
light("AREA", "board_glow", (BX + 2.2, -63.0, -17.4), 450, (0.55, 0.55, 1.0), size=9, aim=(-HW, -63.0, -17.4))
for (x, y) in ((-11.5, -84.0), (11.5, -84.0)):
    light("POINT", f"danger_{x}", (x, y, Z0 + 2.0), 500, (1.0, 0.14, 0.07), 0.8)
# outside: a cold key from the cell's bright side, a rim from behind to cut the silhouette, and the escorts' engines
light("SUN", "cell_sun", (120, -220, 90), 3.5, (0.7, 0.78, 1.0), aim=(0, -80, -10))
light("SUN", "rim", (0, 300, 40), 2.4, (0.75, 0.82, 1.0), aim=(-20, -80, -10))
for me, pos, bank in ESCORTS:
    light("POINT", f"engine_{me.name}", pos + Vector((0, 6, 0)), 1800, (1.0, 0.45, 0.2), 1.5)
    light("AREA", f"key_{me.name}", pos + Vector((22, -18, 14)), 3500, (0.75, 0.82, 1.0), size=14, aim=pos)

world = bpy.data.worlds.new("W")
sc.world = world
world.use_nodes = True
wn = world.node_tree
env = wn.nodes.new("ShaderNodeTexEnvironment")
env.image = bpy.data.images.load(os.path.join(ASSETS, "window.png"))
wn.links.new(env.outputs[0], wn.nodes["Background"].inputs[0])
WORLD_BG = wn.nodes["Background"]
WORLD_BG.inputs[1].default_value = 0.25
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
OUTSIDE = {"beside", "flank"}
CAMS_DEF = {
    # outside before the prow (Rev 59 D, the one liked): the Ark, its mouth open on the lit hangar, the escorts beside
    "beside": ((78.0, -215.0, 22.0), (12.0, -92.0, -8.0), 42, 52.0, 8.0),
    # outside, low along the starboard flank: the Sparrow close, the Dolphin and the Ark's jaw behind it
    "flank": ((76.0, -203.0, 21.0), (46.0, -166.0, 0.0), 28, 21.0, 4.0),
    # inside, from the back wall's corner: the two vessels on their pads, nose on, the kept pad, the open mouth
    "pads": ((7.0, -82.0, -17.0), (-0.5, -57.0, -20.6), 32, 21.0, 3.2),
    # inside, across the bay from the starboard wall: the vessels in profile before the HyperSea board
    "cross": ((11.5, -73.0, -13.6), (-6.0, -59.0, -21.0), 20, 13.0, 3.5),
    # the HyperSea board between the arches (Rev 59 C's map)
    "board": ((11.0, -59.5, -17.0), (-HW, -63.5, -17.4), 26, 25.0, 4.0),
}
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
    out = cname in OUTSIDE
    if os.environ.get("ARK_PROBE"):    # print where the vessels fall in this camera and skip the render
        from bpy_extras.object_utils import world_to_camera_view
        bpy.context.view_layer.update()
        for o in VESSEL_OBJS["out" if out else "in"]:
            bb = [o.matrix_world @ Vector(c_) for c_ in o.bound_box]
            pr = [world_to_camera_view(sc, cam, v) for v in bb]
            print("PROBE", cname, o.name, tuple(round(v, 1) for v in o.matrix_world.translation),
                  "x", round(min(q.x for q in pr), 2), round(max(q.x for q in pr), 2), "y", round(min(q.y for q in pr), 2),
                  round(max(q.y for q in pr), 2), "z", round(min(q.z for q in pr), 1), flush=True)
        continue
    for o in col.objects:
        if o.name == str(("Blue", "plain", 0.81)):     # the outside lattice would cross the prow from out there
            o.hide_render = out
    for o in VESSEL_OBJS["in"]:
        o.hide_render = out
    for o in VESSEL_OBJS["out"]:
        o.hide_render = not out
    haze.hide_render = False
    WORLD_BG.inputs[1].default_value = 0.28 if out else 0.12
    for bp in BACKDROPS:
        bp.hide_render = out
    sc.view_settings.exposure = 0.0 if out else -0.9
    for dom in DOMS:
        set_domain(dom)
        for (pal, kind, gain), m in SQUAD_MATS.items():
            b, r = PAL[dom][kind]
            K.recolour(m, b, r, K.LOOK[kind])
        sig = PAL[dom]["plain"][1]
        peak = max(sig)
        for Lt in DOMAIN_LIGHTS:
            Lt.data.color = tuple(cc / peak for cc in sig)
        sc.render.filepath = os.path.join(OUT, f"lobby_{cname}_{dom}.png")
        bpy.ops.render.render(write_still=True)
        print("RENDER", cname, dom, flush=True)
print("DONE")
