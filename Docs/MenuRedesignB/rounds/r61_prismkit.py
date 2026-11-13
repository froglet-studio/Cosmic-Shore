"""The prism kit shared by the Ark lobby renders: the game's prism read (a dark face plus a fresnel^2.2 rim, Renderer.cs)
in Nocturne's block colours, and batched geometry (box, octahedron, stellated octahedron, danger blade) so thousands of
prisms build as one mesh per look. Import AFTER the scene is reset: it binds to bpy.context.scene."""
import math
import random

import bpy
from mathutils import Matrix, Vector

rng = random.Random(48)
SQUAD = "squad"
sc = bpy.context.scene
col = sc.collection

# NocturneColorSetSO block colours (the live palette since 2026-11-03; Docs/UI_STYLE_GUIDE.md section 1):
# (Outside = the dark face, Inside = the rim), linear HDR, read from Assets/_SO_Assets/Color Palettes/NocturneColorSetSO.asset
PAL = {
    "Jade": {"plain": ((0.064, 0.0, 0.036), (0.12, 1.135, 1.117)), "shielded": ((0.091, 0.017, 0.059), (0.495, 1.217, 1.204)),
             "super": ((0.069, 0.007, 0.042), (1.064, 1.498, 1.49))},
    "Ruby": {"plain": ((0.0, 0.04, 0.076), (1.414, 0.205, 1.498)), "shielded": ((0.039, 0.08, 0.118), (1.154, 0.592, 1.193)),
             "super": ((0.027, 0.104, 0.176), (1.455, 0.835, 1.498))},
    "Gold": {"plain": ((0.049, 0.015, 0.114), (0.996, 0.559, 0.067)), "shielded": ((0.087, 0.053, 0.15), (0.844, 0.638, 0.405)),
             "super": ((0.123, 0.038, 0.285), (1.498, 1.066, 0.58))},
    "Blue": {"plain": ((0.001, 0.044, 0.011), (0.215, 0.408, 1.217)), "shielded": ((0.001, 0.069, 0.017), (1.064, 1.102, 1.26)),
             "super": ((0.001, 0.069, 0.017), (1.265, 1.31, 1.498))},
}
DANGER = (1.498, 0.219, 0.143)
DOMS3 = ["Jade", "Ruby", "Gold"]
SQUAD = "squad"

sc = bpy.context.scene
col = sc.collection


# ------------------------------------------------------------------------------------------------ materials

def prism_mat(name, base, rim, dark=0.12, rim_gain=1.0, glow=0.0):
    """The prism, physically lit: a near-black glossy face (the game's dark Outside colour) that reflects the room, and
    its bevelled edges lit in the rim colour (the game's fresnel rim, read as light caught on the edges), plus a weak
    fresnel term. Edges come from a Bevel node: where the rounded normal leaves the true normal, the edge glows."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    L = nt.links.new
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    p = nt.nodes.new("ShaderNodeBsdfPrincipled")
    p.name = "face"
    p.inputs["Base Color"].default_value = (*[c * dark for c in base], 1)
    p.inputs["Metallic"].default_value = 0.0
    p.inputs["Specular IOR Level"].default_value = 0.4
    p.inputs["Coat Weight"].default_value = 0.0
    p.inputs["Coat Roughness"].default_value = 0.04
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 3.0; nz.inputs["Detail"].default_value = 6.0
    L(tc.outputs["Object"], nz.inputs["Vector"])
    rr = nt.nodes.new("ShaderNodeMapRange"); rr.inputs[3].default_value = 0.22; rr.inputs[4].default_value = 0.55
    L(nz.outputs["Fac"], rr.inputs[0]); L(rr.outputs[0], p.inputs["Roughness"])
    bv = nt.nodes.new("ShaderNodeBevel"); bv.samples = 6; bv.inputs["Radius"].default_value = 0.035
    g = nt.nodes.new("ShaderNodeNewGeometry")
    d = nt.nodes.new("ShaderNodeVectorMath"); d.operation = "DOT_PRODUCT"
    L(bv.outputs[0], d.inputs[0]); L(g.outputs["Normal"], d.inputs[1])
    e1 = nt.nodes.new("ShaderNodeMath"); e1.operation = "SUBTRACT"; e1.inputs[0].default_value = 1.0; L(d.outputs["Value"], e1.inputs[1])
    e2 = nt.nodes.new("ShaderNodeMath"); e2.operation = "MULTIPLY"; e2.inputs[1].default_value = 14.0; e2.use_clamp = True; L(e1.outputs[0], e2.inputs[0])
    # the weak fresnel term (the game's rim, kept faint)
    d2 = nt.nodes.new("ShaderNodeVectorMath"); d2.operation = "DOT_PRODUCT"
    L(g.outputs["Normal"], d2.inputs[0]); L(g.outputs["Incoming"], d2.inputs[1])
    a = nt.nodes.new("ShaderNodeMath"); a.operation = "ABSOLUTE"; L(d2.outputs["Value"], a.inputs[0])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0; L(a.outputs[0], inv.inputs[1])
    pw = nt.nodes.new("ShaderNodeMath"); pw.operation = "POWER"; pw.inputs[1].default_value = 3.0; L(inv.outputs[0], pw.inputs[0])
    mix = nt.nodes.new("ShaderNodeMath"); mix.operation = "MULTIPLY_ADD"; mix.inputs[1].default_value = 0.35
    L(pw.outputs[0], mix.inputs[0]); L(e2.outputs[0], mix.inputs[2])
    ma = nt.nodes.new("ShaderNodeMath"); ma.operation = "MULTIPLY_ADD"; ma.name = "gain"
    ma.inputs[1].default_value = 1.5 * rim_gain; ma.inputs[2].default_value = glow * 0.5
    L(mix.outputs[0], ma.inputs[0])
    e = nt.nodes.new("ShaderNodeEmission"); e.name = "rim"
    e.inputs[0].default_value = (*rim, 1)
    L(ma.outputs[0], e.inputs[1])
    ad = nt.nodes.new("ShaderNodeAddShader")
    L(p.outputs[0], ad.inputs[0]); L(e.outputs[0], ad.inputs[1])
    L(ad.outputs[0], o.inputs[0])
    return m


def recolour(m, base, rim, dark):
    m.node_tree.nodes["face"].inputs["Base Color"].default_value = (*[c * dark for c in base], 1)
    m.node_tree.nodes["rim"].inputs[0].default_value = (*rim, 1)


def image_emission(name, path, strength):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    e = nt.nodes.new("ShaderNodeEmission")
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(path)
    t.extension = "EXTEND"
    nt.links.new(t.outputs[0], e.inputs[0])
    e.inputs[1].default_value = strength
    nt.links.new(e.outputs[0], o.inputs[0])
    return m


MATS = {}
# (palette, kind, rim gain) -> material. Kinds: plain / shielded / super (blocks), danger, crystal, glass
LOOK = {"plain": 1.0, "shielded": 1.0, "super": 1.0}


def mat(key):
    if key not in MATS:
        pal, kind, gain = key
        if kind == "danger":
            MATS[key] = prism_mat(f"m_{pal}_{kind}_{gain}", (0.06, 0.0, 0.0), DANGER, 1.0, gain, 0.15)
        elif kind == "crystal":
            MATS[key] = prism_mat(f"m_{pal}_{kind}_{gain}", (0.2, 0.9, 1.0), (0.3, 1.0, 1.1), 0.5, gain, 0.6)
        elif kind == "glass":
            MATS[key] = prism_mat(f"m_{pal}_{kind}_{gain}", (0.0, 0.0, 0.02), (0.2, 0.3, 0.6), 0.1, gain, 0.0)
        else:
            b, r = PAL["Jade" if pal == SQUAD else pal][kind]
            MATS[key] = prism_mat(f"m_{pal}_{kind}_{gain}", b, r, LOOK[kind], gain)
    return MATS[key]


# ------------------------------------------------------------------------------------------------ batched geometry

BATCH = {}
CUBE_V = [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]
CUBE_F = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
OCTA_V = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
OCTA_F = [(0, 2, 4), (2, 1, 4), (1, 3, 4), (3, 0, 4), (2, 0, 5), (1, 2, 5), (3, 1, 5), (0, 3, 5)]
TET = [(1, 1, 1), (-1, -1, 1), (-1, 1, -1), (1, -1, -1)]
STELLA_V = TET + [(-x, -y, -z) for x, y, z in TET]
STELLA_F = [(0, 1, 2), (0, 3, 1), (0, 2, 3), (1, 3, 2), (4, 6, 5), (4, 5, 7), (4, 7, 6), (5, 6, 7)]
BLADE_V = [(0, 0, 1), (0.35, 0, 0), (0, 0.12, 0), (-0.35, 0, 0), (0, -0.12, 0), (0, 0, -0.15)]
BLADE_F = [(0, 1, 2), (0, 2, 3), (0, 3, 4), (0, 4, 1), (5, 2, 1), (5, 3, 2), (5, 4, 3), (5, 1, 4)]
SHAPES = {"box": (CUBE_V, CUBE_F), "octa": (OCTA_V, OCTA_F), "stella": (STELLA_V, STELLA_F), "blade": (BLADE_V, BLADE_F)}
BEVEL = {"box"}


def frame(fwd, up=(0, 0, 1)):
    z = Vector(fwd).normalized()
    u = Vector(up)
    if abs(z.dot(u.normalized())) > 0.98:
        u = Vector((1, 0, 0)) if abs(z.x) < 0.9 else Vector((0, 1, 0))
    x = u.cross(z).normalized()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed()


def add(shape, pos, size, key, rot=None, fwd=None, up=(0, 0, 1)):
    R = rot if rot is not None else (frame(fwd, up) if fwd is not None else Matrix.Identity(3))
    vs, fs = SHAPES[shape]
    bk = (key, shape in BEVEL)
    b = BATCH.setdefault(bk, ([], []))
    base = len(b[0])
    for v in vs:
        b[0].append(Vector(pos) + R @ Vector((v[0] * size[0], v[1] * size[1], v[2] * size[2])))
    for f in fs:
        b[1].append(tuple(i + base for i in f))


def rand_rot():
    return (Matrix.Rotation(rng.uniform(0, 6.283), 3, "Z") @ Matrix.Rotation(rng.uniform(0, 6.283), 3, "X")
            @ Matrix.Rotation(rng.uniform(0, 6.283), 3, "Y"))


def yaw(deg):
    return Matrix.Rotation(math.radians(deg), 3, "Z")


def build(bevel_width=0.03):
    for (key, bevel), (vs, fs) in BATCH.items():
        me = bpy.data.meshes.new(str(key))
        me.from_pydata(vs, [], fs)
        me.update()
        o = bpy.data.objects.new(str(key), me)
        col.objects.link(o)
        o.data.materials.append(mat(key))
        if bevel:
            bv = o.modifiers.new("bevel", "BEVEL")
            bv.width = bevel_width
            bv.segments = 1
            bv.limit_method = "ANGLE"


def kind_shape(kind):
    return {"plain": "box", "shielded": "octa", "super": "stella"}[kind]


