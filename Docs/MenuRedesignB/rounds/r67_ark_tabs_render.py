"""The Ark screen inside the real Ark, animated, in every colour set: the Squads, Converter, Artifacts and Decor tabs as
places in ArkReverse's hangar, in the style and lighting of Rev 61 C (picked as the Ark's final design by Shombith,
2026-11-06). One script, one scene per run:

- squads: the squad's three vessels lined up side by side across the bay, facing you (the Sparrow, the Dolphin, and
  Mothwing's Serpent with its lights off: Mothwing is offline). Ripples run out of the back wall's tunnel; on the
  HyperSea map a vessel marker travels the route from the Ark at Tumult; the online vessels hover and their engines
  breathe.
- converter: the conversion happens in the back wall's tunnel. The player's element crystals (the game's own Space,
  Charge, Mass and Time crystal models, as many as the player holds) lie about the floor before it. The idle loop
  breathes; the conversion run lifts one of each, in the game's order, into the tunnel and forms the Omni crystal.
- artifacts: a cabinet built into the starboard wall, twelve lit cells, the owned artifacts (built from the game's
  prism shapes) turning in them, the selected one lit.
- decor: a sealed hatch in the port wall with its lock hologram; Decor is locked.

Everything moves on seamless loops: the prisms' rims pulse, the haze drifts, the camera breathes.

    <bpy python> r67_ark_tabs_render.py <out_dir> <assets_dir> <scene> [sets=Nocturne,Photic,DartFrog] [W H samples]

assets_dir: window.png, board_sector.png, board_galaxy.png, colour_sets.json (Nocturne, Photic and Dart Frog read from
Assets/_SO_Assets/Color Palettes/), map/map_NNNN.png (the HyperSea map with the moving marker, one per loop frame).
Writes <out_dir>/<scene>_<set>_<run>/f_NNNN.png, run = loop (and convert for the converter).
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

OUT, ASSETS, SCENE = sys.argv[1], sys.argv[2], sys.argv[3]
SETS = (sys.argv[4] if len(sys.argv) > 4 else "Nocturne,Photic,DartFrog").split(",")
W = int(sys.argv[5]) if len(sys.argv) > 5 else 960
H = int(sys.argv[6]) if len(sys.argv) > 6 else 540
SAMPLES = int(sys.argv[7]) if len(sys.argv) > 7 else 16
LOOP = int(os.environ.get("ARK_LOOP", "40"))
CONV = int(os.environ.get("ARK_CONV", "48"))
ONLY = os.environ.get("ARK_ONLY")            # "f0,f1,..." render only these frame numbers (tests)
RUNS = os.environ.get("ARK_RUNS")            # "loop" / "convert": limit the runs
DOM = os.environ.get("ARK_DOMAIN", "Jade")   # the squad's domain: the trim, the pads, the squad's prisms, the map's marker
os.makedirs(OUT, exist_ok=True)
REPO = os.environ.get("COSMIC_SHORE_REPO") or os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
MODELS = os.path.join(REPO, "Assets/_Models/Vessel Models")
CRYSTALS = os.path.join(REPO, "Assets/_Models")
COLOUR_SETS = json.load(open(os.path.join(ASSETS, "colour_sets.json")))

bpy.ops.wm.read_factory_settings(use_empty=True)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import r67_prismkit as K  # noqa: E402  (binds to the fresh scene)
from r67_prismkit import add, rand_rot, frame, PAL, SQUAD, DOMS3  # noqa: E402

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
if SCENE == "pier":
    # the pier view: the clam doors swung on past the model's open pose, the bottom leaf folded down to the pier's
    # level and the top one up, so the mouth is a clear frame onto the cell
    from mathutils import Quaternion  # noqa: E402
    arm.animation_data.action = None
    for _bn, _deg in (("hangar.door.T", float(os.environ.get("ARK_DOOR_T", "-150"))), ("hangar.door.B", float(os.environ.get("ARK_DOOR_B", "150")))):
        _b = arm.pose.bones[_bn]
        _b.rotation_mode = "QUATERNION"
        _b.rotation_quaternion = Quaternion((1, 0, 0), math.radians(_deg))
    bpy.context.view_layer.update()
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



# ------------------------------------------------------------------------------------------------ the scene's dressing
HW, Z0, Z1, BACK, FRONT = 14.0, -23.0, -12.0, -44.0, -88.0
PADS = [Vector((0.0, -52.0, Z0)), Vector((0.0, -64.0, Z0)), Vector((0.0, -76.0, Z0))]
TUNNEL = Vector((0.0, -44.5, -17.5))       # the back wall's tunnel mouth: the ripples' source, the converter's heart
CAM = {   # scene -> (location, aim, lens, focus, f-stop): Rev 61 C's camera and its siblings
    "squads": ((6.0, -85.0, -15.6), (6.0, -62.0, -20.6), 24, 23.0, 3.6),     # the line in the left two-thirds, the cards' side clear
    "converter": ((2.6, -70.0, -19.0), (0.0, -47.0, -19.0), 30, 17.0, 3.4),
    "artifacts": ((0.6, -74.0, -17.4), (12.5, -72.6, -18.0), 26, 11.6, 3.0),   # the cabinet right of the list
    "decor": ((4.0, -73.5, -17.6), (-13.0, -79.0, -17.8), 30, 17.0, 3.2),
    # the converter, open to the cell: from the hangar's lip, out of the open mouth (Shombith, 2026-11-12: "I don't want
    # a room ... an open part of the Ark, where this is happening outside in the cell"); Garrett's combine is laid over
    "deck": ((-26.0, -128.0, -8.0), (-13.0, -95.0, -19.5), 26, 40.0, 8.0),
    # the converter, second pass (Shombith, 2026-11-12: "the camera should be inside the Ark and the crystals outside on
    # an elongated platform ... we should be able to see the cell toybox, and the conversion happens in the middle"):
    # from the back of the hangar, out of the open mouth, down the pier that runs out into the cell
    "pier": ((0.0, -63.0, -16.4), (0.0, -128.0, -19.6), 24, 65.0, 16.0),
}.get(SCENE, ((0.0, -70.0, -17.0), (0.0, -50.0, -17.0), 24, 20.0, 4.0))     # the artifact rooms set their own
PIER_MID = Vector((0.0, -128.0, Z0))      # the pier's middle: where Garrett's combine is laid over
PIER_END = -170.0
NEAR = [Vector(CAM[0])]
PAD_LIGHTS = []
MOVERS = []          # (object, base matrix, phase): things that hover
OFFLINE = []
DUST = 320

def dark_vessel_mats():
    return {"domain": hull_mat("off_domain", (0.02, 0.02, 0.025), metal=0.5, panel=0.9, emit=((0, 0, 0), 0.0)),
            "engine": hull_mat("off_engine", (0.05, 0.03, 0.02), metal=0.3, panel=0.9, emit=((0, 0, 0), 0.0)),
            "window": glass_mat("off_window", (0.25, 0.45, 1.0), 0.02)}


if SCENE == "squads":
    # ARK_LINE (Rev 110, the vessel-swap check): the hulls on the three pads, left to right ("Dolphin,Sparrow,Serpent" is
    # the shipped line-up; "none" leaves the pads empty, the plate a live vessel layer is drawn over). The last one is the
    # offline mate's, powered down.
    HULLS = {"Sparrow": ("SparrowModel1.fbx", FLEET, -1), "Dolphin": ("dolphin_shapekey_with_animations.fbx", ARTIST, -1),
             "Serpent": ("SerpentExport.fbx", FLEET, 1), "Manta": ("Manta_shapekey_rigged.fbx", ARTIST, -1),
             "Rhino": ("rhino_shapekey_with_animations.fbx", ARTIST, -1)}
    names = os.environ.get("ARK_LINE", "Dolphin,Sparrow,Serpent")
    names = [] if names == "none" else names.split(",")
    meshes = {}
    for n in names:
        if n not in meshes:
            f, roles, ny = HULLS[n]
            meshes[n] = load_vessel(f, roles, nose_y=ny)
            zs = [v.co.z for v in meshes[n].vertices]; ys = [v.co.y for v in meshes[n].vertices]
            if max(zs) - min(zs) > 1.5 * (max(ys) - min(ys)):        # authored nose-up (z): lay it flat, nose to +y
                meshes[n].transform(Matrix.Rotation(-math.pi / 2, 4, "X"))
    if "Sparrow" not in meshes and names:            # every hull at the Sparrow's length, as the game's pads scale them
        meshes["_ref"] = load_vessel(*HULLS["Sparrow"][:2], nose_y=HULLS["Sparrow"][2])
    ref = meshes.get("Sparrow") or meshes.get("_ref")
    if ref is not None:
        span = lambda m: max(max(v.co[i] for v in m.vertices) - min(v.co[i] for v in m.vertices) for i in (0, 1, 2))
        for n in names:
            if n not in ("Sparrow", "Dolphin", "Serpent"):           # the shipped three keep the clip's sizes
                meshes[n].transform(Matrix.Scale(0.6 * span(ref) / span(meshes[n]), 4))
    off = dark_vessel_mats()
    swap = {DOMAIN_V.name: off["domain"], ENGINE.name: off["engine"], WINDOW.name: off["window"]}
    # the squad lined up across the bay, facing the door: Rogue One, the leader a step back in the middle, Rogue Two
    SPOTS = [Vector((-9.0, -60.5, Z0 + 2.3)), Vector((0.0, -66.0, Z0 + 2.8)), Vector((9.6, -60.5, Z0 + 2.5))]
    LINE = []
    for k, n in enumerate(names):
        me = meshes[n]
        if k == 2:                                   # the offline mate's hull: its own powered-down copy
            me = me.copy()
            for i, s_ in enumerate(me.materials):
                if s_ is not None and s_.name in swap:
                    me.materials[i] = swap[s_.name]
        LINE.append((me, SPOTS[k], k != 2))
    for k, (me, pos, online) in enumerate(LINE):
        o = place(me, "in", pos, (0, -1, 0))
        PAD_LIGHTS.append((Vector((pos.x, pos.y, Z0)), online))
        if online:
            MOVERS.append((o, o.matrix_world.copy(), k * 2.1))
        else:
            OFFLINE.append(o)
    if not names:                                    # empty pads all glow: any seat may hold a live hull (the game lights
        PAD_LIGHTS.extend([(Vector((p.x, p.y, Z0)), True) for p in SPOTS])          # an offline one from its own layer)
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



HEAPS = {
    "squads": [(-11.5, -48.0, 1.3, 2.2, 650, 5.0), (11.5, -47.0, 1.3, 2.2, 650, 5.5), (-12.0, -71.0, 0.9, 3.5, 380, 3.0),
               (12.0, -75.0, 0.9, 4.0, 420, 3.5)],
    "converter": [(-11.5, -48.0, 1.3, 2.2, 650, 5.0), (11.5, -47.0, 1.3, 2.2, 650, 5.5), (-12.0, -71.0, 0.9, 3.5, 380, 3.0),
                  (12.0, -75.0, 0.9, 4.0, 420, 3.5)],
    "artifacts": [(-11.5, -48.0, 1.3, 2.2, 650, 5.0), (11.5, -47.0, 1.3, 2.2, 650, 5.5), (-12.0, -71.0, 0.9, 3.5, 380, 3.0),
                  (12.2, -86.0, 0.8, 1.6, 260, 2.5)],
    "decor": [(-11.5, -48.0, 1.3, 2.2, 650, 5.0), (11.5, -47.0, 1.3, 2.2, 650, 5.5), (-12.2, -66.0, 0.8, 1.8, 260, 3.0),
              (12.0, -75.0, 0.9, 4.0, 420, 3.5)],
}.get(SCENE, [(-11.5, -48.0, 1.3, 2.2, 650, 5.0), (11.5, -47.0, 1.3, 2.2, 650, 5.5), (-12.0, -71.0, 0.9, 3.5, 380, 3.0),
              (12.0, -75.0, 0.9, 4.0, 420, 3.5)])
THICKETS = [(-11.5, -84.0, 9), (11.5, -84.0, 9), (10.0, -79.5, 5)] + ([] if SCENE == "decor" else [(-9.5, -80.5, 5)])
for hp in HEAPS:
    heap(*hp)
# danger thickets in the mouth's corners: the way out has teeth
for (x, y, n) in THICKETS:
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
for _ in range(DUST):
    p = (rng.uniform(-12.5, 12.5), rng.uniform(-86, -45), rng.uniform(Z0 + 1.0, Z1 - 0.6))
    if min((Vector(p) - n).length for n in NEAR) < 8.0 or (abs(p[0]) < 7.5 and -72 < p[1] < -44):
        continue
    if SCENE == "pier" and (abs(p[0]) < 8.0 or p[1] < -66):      # the pier view looks down the middle: kept clear
        continue
    pal = rng.choice(DOMS3 + ["Blue"] * 4)
    s = rng.uniform(0.03, 0.1)
    add("box", p, (s * 2.2, s, s), (pal, "plain", 0.15), rot=rand_rot())

# ------------------------------------------------------------------------------------------------ outside
win = K.image_emission("cell_view", os.path.join(ASSETS, "window.png"), 0.75 if SCENE == "pier" else 0.22)
BACKDROPS = []
for (y, rotz) in ((-420.0, 0.0), (420.0, math.pi)):          # seen through the mouth from inside; outside, the world is the cell
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, y, 0), rotation=(math.radians(90), 0, rotz))
    bp = bpy.context.active_object
    BACKDROPS.append(bp)
    bp.scale = (1500, 850, 1)
    bp.data.materials.append(win)


def seg_dist(q, a, b):
    ab = b - a
    u = max(0.0, min(1.0, (q - a).dot(ab) / ab.length_squared))
    return (q - (a + ab * u)).length


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
    rad_ = rng.uniform(1.6, 3.2)
    if SCENE == "deck" and min((Vector(q) - Vector(CAM[0])).length for q in pts) < 40.0:
        continue                              # a tube through the deck camera's lens: left out of this view
    if SCENE == "pier" and min(seg_dist(Vector(q), Vector(CAM[0]), Vector((0.0, PIER_END - 10.0, Z0 + 4.0))) for q in pts) < 15.0:
        continue                              # a tube across the pier or the sight line down it: left out of this view
    lattice_tube(pts, rad_, "Blue")


# ------------------------------------------------------------------------------------------------ the tabs' own things
ART_OBJS = []        # (object, base matrix, spin turns per loop, cell light)
CRY = {}             # element -> list of crystal objects
OMNI = None
LOCK = []
SCAN = None
SLIT = None
_dg0 = None


def wall_hit(origin, direction):
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ok, loc, nrm, _i, ob, _m = sc.ray_cast(dg, Vector(origin), Vector(direction).normalized(), distance=40.0)
    return (loc, nrm) if ok and ob == ark else (None, None)


def crystal_mat(name, glow):
    """The elemental crystals as the Codex draws them: lime plates, darker in the middle, lit at their seams."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    ln = nt.links.new
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    p = nt.nodes.new("ShaderNodeBsdfPrincipled")
    p.inputs["Base Color"].default_value = (0.16, 0.32, 0.0, 1)
    p.inputs["Metallic"].default_value = 0.2
    p.inputs["Roughness"].default_value = 0.28
    p.inputs["Coat Weight"].default_value = 0.6
    bv = nt.nodes.new("ShaderNodeBevel"); bv.samples = 6; bv.inputs["Radius"].default_value = 0.025
    g = nt.nodes.new("ShaderNodeNewGeometry")
    d = nt.nodes.new("ShaderNodeVectorMath"); d.operation = "DOT_PRODUCT"
    ln(bv.outputs[0], d.inputs[0]); ln(g.outputs["Normal"], d.inputs[1])
    e1 = nt.nodes.new("ShaderNodeMath"); e1.operation = "SUBTRACT"; e1.inputs[0].default_value = 1.0; ln(d.outputs["Value"], e1.inputs[1])
    e2 = nt.nodes.new("ShaderNodeMath"); e2.operation = "MULTIPLY"; e2.inputs[1].default_value = 16.0; e2.use_clamp = True
    ln(e1.outputs[0], e2.inputs[0])
    gg = nt.nodes.new("ShaderNodeMath"); gg.operation = "MULTIPLY_ADD"; gg.name = "glow"
    gg.inputs[1].default_value = 4.0 * glow; gg.inputs[2].default_value = 0.25 * glow
    ln(e2.outputs[0], gg.inputs[0])
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (0.6, 1.0, 0.0, 1)
    ln(gg.outputs[0], em.inputs[1])
    ad = nt.nodes.new("ShaderNodeAddShader"); ln(p.outputs[0], ad.inputs[0]); ln(em.outputs[0], ad.inputs[1])
    ln(ad.outputs[0], o.inputs[0])
    return m


def load_crystal(fname, diameter):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(CRYSTALS, fname), use_anim=False)
    new = [o for o in bpy.data.objects if o not in before]
    for a in new:
        if a.type == "ARMATURE":
            a.data.pose_position = "REST"
    bpy.context.view_layer.update()
    src_ = [o for o in new if o.type == "MESH"][0]
    d = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(src_.evaluated_get(d), depsgraph=d)
    me.transform(src_.matrix_world)
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    vs = [v.co for v in me.vertices]
    c = Vector([(min(v[i] for v in vs) + max(v[i] for v in vs)) / 2 for i in range(3)])
    s = diameter / max(max(v[i] for v in vs) - min(v[i] for v in vs) for i in range(3))
    me.transform(Matrix.Diagonal((s, s, s, 1)) @ Matrix.Translation(-c))
    me.materials.clear()
    return me


# ------------------------------------------------------------------------------------------------ the tabs' own rooms
# Every tab is its own room in the Ark (Shombith, 2026-11-06: "all screens have different rooms"): the hangar for the
# Squads, and three rooms built here, each far from the hangar so nothing of one shows in another, in the hangar's own
# materials, prisms and light.
RINGS = []           # (object, axis, turns per loop, base rotation): the converter's gimbal rings
ROOM_BOX = None      # (centre, half size): the room's haze
CORE = None


def block(cx, cy, cz, sx, sy, sz, matl, bevel=0.04, name="slab"):
    """A box whose mesh is built at its true size, so the hull's panel grid reads at metre scale on it."""
    me = bpy.data.meshes.new(name)
    vs = [(x * sx / 2, y * sy / 2, z * sz / 2) for (x, y, z) in K.CUBE_V]
    me.from_pydata(vs, [], K.CUBE_F)
    me.update()
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    o.location = (cx, cy, cz)
    o.data.materials.append(matl)
    if bevel:
        bvm = o.modifiers.new("b", "BEVEL"); bvm.width = bevel; bvm.segments = 2
    return o


def plight(kind, loc, energy, color, size=1.0, aim=None):
    d = bpy.data.lights.new("l", kind)
    d.energy, d.color = energy, color
    if kind == "AREA":
        d.size = size
    else:
        d.shadow_soft_size = size
    o = bpy.data.objects.new("l", d)
    col.objects.link(o)
    o.location = loc
    if aim is not None:
        o.rotation_euler = (Vector(aim) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


def room(O, hx, hy, h, opening=None, sockets=True):
    """A room of the Ark: floor, ceiling and four walls of the hull's plating, ribs and ceiling beams, the domain's
    trim along the floor and the ceiling, the game's prisms set in the walls like the hull's sockets, cold strip
    lights. `opening` = (half width, z0, z1) cut in the front wall (y = -hy) for a window."""
    t = 0.6
    block(O.x, O.y, O.z - t / 2, 2 * hx + 2 * t, 2 * hy + 2 * t, t, BODY, name="floor")
    block(O.x, O.y, O.z + h + t / 2, 2 * hx + 2 * t, 2 * hy + 2 * t, t, BODY, name="ceiling")
    for sx in (-1, 1):
        block(O.x + sx * (hx + t / 2), O.y, O.z + h / 2, t, 2 * hy, h, BODY, name="side")
    block(O.x, O.y + hy + t / 2, O.z + h / 2, 2 * hx, t, h, BODY, name="back")
    if opening is None:
        block(O.x, O.y - hy - t / 2, O.z + h / 2, 2 * hx, t, h, BODY, name="front")
    else:
        ow, z0, z1 = opening
        fy = O.y - hy - t / 2
        block(O.x, fy, O.z + z0 / 2, 2 * hx, t, z0, BODY, name="front")
        block(O.x, fy, O.z + (z1 + h) / 2, 2 * hx, t, h - z1, BODY, name="front")
        for sx in (-1, 1):
            block(O.x + sx * (ow + (hx - ow) / 2), fy, O.z + (z0 + z1) / 2, hx - ow, t, z1 - z0, BODY, name="front")
    # ribs up the side walls and over the ceiling, every three metres
    n = int(2 * hy / 3.0)
    for k in range(n + 1):
        y = O.y - hy + k * (2 * hy / n)
        for sx in (-1, 1):
            block(O.x + sx * (hx - 0.25), y, O.z + h / 2, 0.5, 0.45, h, BODY, name="rib")
        block(O.x, y, O.z + h - 0.3, 2 * hx, 0.45, 0.6, BODY, name="beam")
    # the domain's trim: floor and ceiling edges
    for sx in (-1, 1):
        block(O.x + sx * (hx - 0.55), O.y, O.z + 0.12, 0.06, 2 * hy - 0.6, 0.06, DOMAIN_ARK, bevel=0, name="trim")
        block(O.x + sx * (hx - 0.55), O.y, O.z + h - 0.75, 0.06, 2 * hy - 0.6, 0.06, DOMAIN_ARK, bevel=0, name="trim")
    block(O.x, O.y + hy - 0.55, O.z + 0.12, 2 * hx - 0.6, 0.06, 0.06, DOMAIN_ARK, bevel=0, name="trim")
    # the game's prisms set in the side walls high up, like the hull's sockets
    if sockets:
        for k in range(n):
            y = O.y - hy + (k + 0.5) * (2 * hy / n)
            for sx in (-1, 1):
                kind = "shielded" if k % 3 == 1 else "plain"
                if kind == "plain":
                    add("box", (O.x + sx * (hx - 0.4), y, O.z + h - 2.2), (0.3, 0.9, 0.3), (SQUAD, "plain", 1.0), fwd=(0, 1, 0))
                else:
                    add("octa", (O.x + sx * (hx - 0.6), y, O.z + h - 2.2), (0.55, 0.55, 0.65), (SQUAD, "shielded", 1.0))
    # cold strip lights along the ceiling, as the hangar's
    for k in range(3):
        y = O.y - hy * 0.6 + k * hy * 0.6
        plight("AREA", (O.x, y, O.z + h - 0.9), 260, (0.85, 0.9, 1.0), size=3.0, aim=(O.x, y, O.z))
    return O


if SCENE == "converter":
    # the reactor chamber, seen through a big observation window from the dark gallery outside it
    O = Vector((0.0, 420.0, -20.0))
    room(O, 13.0, 13.0, 12.5, opening=(9.0, 1.6, 9.2))
    ROOM_BOX = (O + Vector((0, 0, 6.2)), Vector((12.5, 12.5, 6.0)))
    # the gallery: a short dark room before the window, its frame and two mullions, the glass
    G = O + Vector((0, -18.6, 0))
    block(G.x, G.y, G.z - 0.3, 22.0, 11.0, 0.6, BODY, name="gallery_floor")
    block(G.x, G.y, G.z + 10.5, 22.0, 11.0, 0.6, BODY, name="gallery_ceiling")
    for sx in (-1, 1):
        block(G.x + sx * 10.8, G.y, G.z + 5.0, 0.6, 11.0, 10.0, BODY, name="gallery_side")
        block(O.x + sx * 3.0, O.y - 13.3, O.z + 5.4, 0.35, 0.5, 7.6, BODY, name="mullion")
    block(O.x, O.y - 13.4, O.z + 1.25, 18.6, 1.4, 0.7, BODY, name="sill")
    block(O.x, O.y - 14.05, O.z + 1.62, 18.0, 0.04, 0.05, DOMAIN_ARK, bevel=0, name="sill_trim")
    gm = bpy.data.materials.new("window_glass")
    gm.use_nodes = True
    gp = gm.node_tree.nodes["Principled BSDF"]
    gp.inputs["Base Color"].default_value = (0, 0, 0, 1); gp.inputs["Roughness"].default_value = 0.02
    gp.inputs["Alpha"].default_value = 0.07
    bpy.ops.mesh.primitive_plane_add(size=1, location=(O.x, O.y - 13.05, O.z + 5.4), rotation=(math.radians(90), 0, 0))
    gl = bpy.context.active_object
    gl.scale = (18.0, 7.6, 1)
    gl.data.materials.append(gm)
    plight("POINT", G + Vector((4.0, -2.0, 7.0)), 40, (0.6, 0.7, 1.0), 2.0)
    # the reactor: a well, a column, the core, three gimbal rings turning round it
    CORE = O + Vector((0.0, 1.5, 4.4))
    TUNNEL = CORE
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=3.0, depth=1.0, location=O + Vector((0, 1.5, 0.5)))
    bpy.context.active_object.data.materials.append(BODY)
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=3.15, depth=0.08, location=O + Vector((0, 1.5, 1.02)))
    bpy.context.active_object.data.materials.append(DOMAIN_ARK)
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.7, depth=2.4, location=O + Vector((0, 1.5, 2.2)))
    bpy.context.active_object.data.materials.append(ENGINE_ARK)
    for k, (rad, axis, turns, tilt) in enumerate(((2.3, "X", 1, 0.0), (3.0, "Y", -1, 0.6), (3.7, "Z", 1, 1.1))):
        bpy.ops.mesh.primitive_torus_add(major_radius=rad, minor_radius=0.17 + 0.02 * k, major_segments=64, minor_segments=12)
        ring = bpy.context.active_object
        ring.data.materials.append(BODY)
        bpy.ops.mesh.primitive_torus_add(major_radius=rad, minor_radius=0.05, major_segments=64, minor_segments=8)
        glow = bpy.context.active_object
        glow.data.materials.append(DOMAIN_ARK)
        glow.parent = ring
        glow.matrix_parent_inverse = Matrix.Identity(4)
        base = Matrix.Rotation(tilt, 4, "X") @ Matrix.Rotation(0.4 * k, 4, "Z")
        RINGS.append((ring, axis, turns, base))
        ring.matrix_world = Matrix.Translation(CORE) @ base
    CORE_LIGHT = plight("POINT", CORE, 500, (0.8, 1.0, 0.55), 0.8)
    # the crystals: the player's, the game's own models, floating in cradles round the core by element
    CMAT = crystal_mat("crystal", 0.45)
    OMAT = crystal_mat("omni", 0.9)
    ELEMENTS = [("Space", "SpaceCrystalExport1_7-17-25.fbx", 7, 158.0),
                ("Charge", "ChargeCrystalExport1_7-11-25.fbx", 4, 118.0),
                ("Mass", "MassCrystalExport1_10-16-25.fbx", 5, 66.0),
                ("Time", "TimeCrystalExport.fbx", 3, 24.0)]
    for name, fname, n, ang0 in ELEMENTS:
        me = load_crystal(fname, 1.05)
        me.materials.append(CMAT)
        CRY[name] = []
        for k in range(n):
            tier = k % 2
            a = math.radians(ang0 + (k // 2 - (n - 1) / 4) * 13.0 + tier * 6.5)
            r = 7.6 + tier * 1.4
            pos = Vector((CORE.x + math.cos(a) * r, CORE.y + math.sin(a) * r, O.z + 2.3 + tier * 1.3))
            bpy.ops.mesh.primitive_torus_add(major_radius=0.62, minor_radius=0.06, location=pos - Vector((0, 0, 0.78)))
            bpy.context.active_object.data.materials.append(BODY)
            block(pos.x, pos.y, (O.z + pos.z - 0.8) / 2, 0.12, 0.12, pos.z - 0.8 - O.z, BODY, bevel=0.01, name="stalk")
            o = bpy.data.objects.new(f"{name}_{k}", me)
            col.objects.link(o)
            o.matrix_world = Matrix.Translation(pos) @ rand_rot().to_4x4()
            CRY[name].append(o)
            MOVERS.append((o, o.matrix_world.copy(), rng.uniform(0, math.tau)))
    ome = load_crystal("OmniCrystalExport1_8-21-25.fbx", 1.7)
    ome.materials.append(OMAT)
    OMNI = bpy.data.objects.new("Omni", ome)
    col.objects.link(OMNI)
    OMNI.matrix_world = Matrix.Diagonal((0.0001, 0.0001, 0.0001, 1))
    CAM = ((-1.0, O.y - 19.5, O.z + 4.6), (-3.4, O.y + 1.0, O.z + 3.8), 28, 21.0, 4.0)   # the reactor right of the panel

if SCENE == "artifacts":
    # the vault: a long room whose back wall is the artifact cabinet
    O = Vector((0.0, 480.0, -20.0))
    room(O, 8.0, 7.0, 8.0)
    ROOM_BOX = (O + Vector((0, 0, 4.0)), Vector((7.6, 6.6, 3.9)))
    CW, CH, CD = 2.4, 2.1, 1.3
    cy_back = O.y + 7.0
    front = cy_back - CD
    x0 = O.x - 2 * CW
    zb = O.z + 1.2
    block(O.x, cy_back - 0.05, zb + 1.5 * CH, 4 * CW + 0.4, 0.1, 3 * CH + 0.4, BODY, name="cab_back")
    for r in range(4):
        block(O.x, front + CD / 2, zb + r * CH, 4 * CW + 0.4, CD, 0.12, BODY, bevel=0.02, name="shelf")
    for c in range(5):
        block(x0 + c * CW, front + CD / 2, zb + 1.5 * CH, 0.12, CD, 3 * CH + 0.12, BODY, bevel=0.02, name="upright")
    block(O.x, front + CD / 2, zb + 3 * CH + 0.35, 4 * CW + 0.8, CD + 0.3, 0.4, BODY, name="lintel")
    block(O.x, front - 0.16, zb + 3 * CH + 0.35, 4 * CW + 0.6, 0.04, 0.06, DOMAIN_ARK, bevel=0, name="lintel_trim")
    gm = bpy.data.materials.new("cabinet_glass")
    gm.use_nodes = True
    gp = gm.node_tree.nodes["Principled BSDF"]
    gp.inputs["Base Color"].default_value = (0, 0, 0, 1); gp.inputs["Roughness"].default_value = 0.03
    gp.inputs["Alpha"].default_value = 0.08
    bpy.ops.mesh.primitive_plane_add(size=1, location=(O.x, front - 0.02, zb + 1.5 * CH), rotation=(math.radians(90), 0, 0))
    gl = bpy.context.active_object
    gl.scale = (4 * CW, 3 * CH, 1)
    gl.data.materials.append(gm)
    # the four artifacts the game has (Assets/_SO_Assets/Pilots/Artifacts), each shaped from the game's prisms after
    # what it does; the other cells are empty, still to be found
    ring_ = lambda n, r, z, sz: [("octa", (r * math.cos(k * math.tau / n), r * math.sin(k * math.tau / n), z), sz) for k in range(n)]
    SHAPES = [
        [("blade", (0, 0, -0.5), (0.3, 0.3, 1.05))],                                                         # Javelin
        [("octa", (0, 0, 0), (0.17, 0.17, 0.24))] + ring_(5, 0.46, 0.08, (0.08, 0.08, 0.12)),                # Hydra
        [("octa", (0, 0, 0.2), (0.2, 0.2, 0.26)), ("octa", (0, 0, -0.22), (0.16, 0.16, 0.2))]
        + ring_(6, 0.34, -0.22, (0.05, 0.05, 0.07)),                                                         # Aftershock
        [("stella", (0, 0, 0), (0.34, 0.34, 0.34))],                                                         # Aegis
    ]
    SELECTED = 1                                          # Hydra
    for i in range(4):
        vs, fs = [], []
        for (shp, pos, size) in SHAPES[i]:
            sv, sf = K.SHAPES[shp]
            b0 = len(vs)
            for v in sv:
                vs.append(Vector((pos[0] + v[0] * size[0], pos[1] + v[1] * size[1], pos[2] + v[2] * size[2])) * 1.5)
            for f in sf:
                fs.append(tuple(k + b0 for k in f))
        me = bpy.data.meshes.new(f"artifact_{i}")
        me.from_pydata(vs, [], fs)
        me.update()
        o = bpy.data.objects.new(f"artifact_{i}", me)
        col.objects.link(o)
        o.data.materials.append(K.mat((SQUAD, "plain", 2.4 if i == SELECTED else 1.5)))
        bvm = o.modifiers.new("b", "BEVEL"); bvm.width = 0.015; bvm.segments = 1; bvm.limit_method = "ANGLE"
        cx_ = x0 + (i + 0.5) * CW
        o.matrix_world = Matrix.Translation((cx_, front + CD / 2, zb + 1.5 * CH))
        ART_OBJS.append([o, o.matrix_world.copy(), 1 if i % 2 == 0 else -1, None, i == SELECTED])
    for c in range(4):
        for r in range(3):
            sel = r == 1 and c == SELECTED
            plight("POINT", (x0 + (c + 0.5) * CW, front + 0.3, zb + (r + 0.93) * CH),
                   (160 if sel else 45) if r == 1 else 8, (0.8, 0.9, 1.0), 0.3)
    # two plinths flanking the cabinet, a prism on each
    for sx in (-1, 1):
        block(O.x + sx * 6.2, O.y + 4.6, O.z + 0.6, 1.2, 1.2, 1.2, BODY, name="plinth")
        add("stella", (O.x + sx * 6.2, O.y + 4.6, O.z + 1.95), (0.42,) * 3, (SQUAD, "super", 1.0), rot=rand_rot())
    CAM = ((-1.8, O.y - 6.2, O.z + 3.1), (-2.6, O.y + 7.0, O.z + 3.5), 24, 10.4, 3.2)

SPOTS = []
from mathutils import Euler  # noqa: E402
# ------------------------------------------------------------------------------------------------ Artifacts, three rooms
# (Shombith, 2026-11-12: "it can be inside a room, but a cooler room of the Ark. You can give me a few still mockups")
def emit_mat(name, color, strength, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    o_ = nt.nodes.new("ShaderNodeOutputMaterial")
    e_ = nt.nodes.new("ShaderNodeEmission"); e_.inputs[0].default_value = (*color, 1); e_.inputs[1].default_value = strength
    if alpha >= 1.0:
        nt.links.new(e_.outputs[0], o_.inputs[0])
    else:
        t_ = nt.nodes.new("ShaderNodeBsdfTransparent")
        mx = nt.nodes.new("ShaderNodeMixShader"); mx.inputs[0].default_value = alpha
        nt.links.new(t_.outputs[0], mx.inputs[1]); nt.links.new(e_.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], o_.inputs[0])
    return m


_ring = lambda n, r, z, sz: [("octa", (r * math.cos(k * math.tau / n), r * math.sin(k * math.tau / n), z), sz) for k in range(n)]
ART_SHAPES = [
    [("blade", (0, 0, -0.5), (0.3, 0.3, 1.05))],                                                         # Javelin
    [("octa", (0, 0, 0), (0.17, 0.17, 0.24))] + _ring(5, 0.46, 0.08, (0.08, 0.08, 0.12)),                # Hydra
    [("octa", (0, 0, 0.2), (0.2, 0.2, 0.26)), ("octa", (0, 0, -0.22), (0.16, 0.16, 0.2))]
    + _ring(6, 0.34, -0.22, (0.05, 0.05, 0.07)),                                                         # Aftershock
    [("stella", (0, 0, 0), (0.34, 0.34, 0.34))],                                                         # Aegis
]
ART_SEL = 1                                                                                              # Hydra


def art_obj(i, pos, scale=1.5, spin=0.0):
    vs, fs = [], []
    for (shp, p_, size) in ART_SHAPES[i]:
        sv, sf = K.SHAPES[shp]
        b0 = len(vs)
        for v in sv:
            vs.append(Vector((p_[0] + v[0] * size[0], p_[1] + v[1] * size[1], p_[2] + v[2] * size[2])) * scale)
        for f in sf:
            fs.append(tuple(k + b0 for k in f))
    me = bpy.data.meshes.new(f"artifact_{i}")
    me.from_pydata(vs, [], fs)
    me.update()
    o = bpy.data.objects.new(f"artifact_{i}", me)
    col.objects.link(o)
    o.data.materials.append(K.mat((SQUAD, "plain", 2.6 if i == ART_SEL else 1.5)))
    bvm = o.modifiers.new("b", "BEVEL"); bvm.width = 0.015; bvm.segments = 1; bvm.limit_method = "ANGLE"
    o.matrix_world = Matrix.Translation(pos) @ Matrix.Rotation(spin, 4, "Z")
    return o


def torus(loc, R, r, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=128, minor_segments=10, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.data.materials.append(mat)
    return o


def disc(loc, R, h, mat, verts=96):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=R, depth=h, location=loc)
    o = bpy.context.active_object
    o.data.materials.append(mat)
    return o


def ring_wall(O, R, h, n, mat):
    """A round wall: n panels and n ribs facing the centre."""
    for k in range(n):
        a = k * math.tau / n
        for (rr, w, d, nm) in ((R + 0.2, 2 * math.pi * R / n + 0.05, 0.35, "panel"), (R - 0.25, 0.5, 0.6, "rib")):
            if nm == "rib":
                a2 = a + math.pi / n
            else:
                a2 = a
            b_ = block(O.x + rr * math.sin(a2), O.y + rr * math.cos(a2), O.z + h / 2, w, d, h, mat, name=nm)
            b_.rotation_euler = (0, 0, -a2)


if SCENE == "art_a":
    # A - the Reliquary: a tall round chamber at the Ark's core; a column of cold light up its middle; the four
    # artifacts float over pedestals round it; eight dark sockets up the wall wait for the ones still to be found
    O = Vector((0.0, 600.0, -20.0)); R_, H_ = 9.0, 15.0
    disc(O + Vector((0, 0, -0.3)), R_ + 0.6, 0.6, BODY)
    disc(O + Vector((0, 0, H_ + 0.3)), R_ + 0.6, 0.6, BODY)
    ring_wall(O, R_, H_, 28, BODY)
    for z_ in (0.12, 5.2, H_ - 0.6):
        torus(O + Vector((0, 0, z_)), R_ - 0.55, 0.05, DOMAIN_ARK)
    for r_ in (2.2, 3.0):
        torus(O + Vector((0, 0, 0.05)), r_, 0.04, DOMAIN_ARK)
    disc(O + Vector((0, 0, H_ / 2)), 0.22, H_, emit_mat("beam", (0.55, 0.75, 1.0), 2.0, alpha=0.22), verts=48)
    disc(O + Vector((0, 0, H_ / 2)), 1.0, H_, emit_mat("beam_halo", (0.4, 0.6, 1.0), 0.25, alpha=0.06), verts=48)
    plight("POINT", O + Vector((0, 0, 3.0)), 900, (0.65, 0.8, 1.0), 0.6)
    plight("POINT", O + Vector((0, 0, 10.0)), 700, (0.65, 0.8, 1.0), 0.6)
    angs = [-75, -27, 27, 75]                 # on the far side of the column, facing the door you came in by
    for i, a_ in enumerate(angs):
        a = math.radians(a_)
        p = O + Vector((5.0 * math.sin(a), 5.0 * math.cos(a), 0))
        disc(p + Vector((0, 0, 0.55)), 0.75, 1.1, BODY, verts=48)
        torus(p + Vector((0, 0, 1.12)), 0.68, 0.035, DOMAIN_ARK)
        art_obj(i, p + Vector((0, 0, 2.75)), scale=2.9 if i == ART_SEL else 2.3, spin=0.4 * i)
        SPOTS.append(("SPOT", f"art_spot_{i}", p + Vector((0, 0, 8.5)), 1400 if i == ART_SEL else 420, (0.85, 0.92, 1.0), 0.15,
                      p + Vector((0, 0, 1.0)), 16))
        plight("POINT", p + Vector((0, 0, 2.6)), 140 if i == ART_SEL else 45, (0.8, 0.9, 1.0), 0.4)
    for k in range(8):                       # the empty sockets, up the wall behind
        a = math.radians(-140 + k * 40)
        p = O + Vector(((R_ - 0.4) * math.sin(a), (R_ - 0.4) * math.cos(a), 8.6 + 1.6 * (k % 2)))
        sk = block(p.x, p.y, p.z, 1.2, 0.5, 1.4, BODY, name="socket")
        sk.rotation_euler = (0, 0, -a)
        add("octa", p - Vector((math.sin(a), math.cos(a), 0)) * 0.4, (0.32, 0.32, 0.42), (SQUAD, "shielded", 0.08))
    ROOM_BOX = (O + Vector((0, 0, H_ / 2)), Vector((R_ - 0.6, R_ - 0.6, H_ / 2 - 0.2)))
    CAM = ((0.0, O.y - 8.3, O.z + 3.4), (0.0, O.y + 2.5, O.z + 3.6), 20, 11.0, 4.0)

if SCENE == "art_b":
    # B - the Gallery: a long hall of tall lit alcoves, the found artifacts in the nearest four, the rest dark, the
    # hall ending in a round porthole onto the cell
    O = Vector((0.0, 680.0, -20.0)); HX, HY, H_ = 5.5, 17.0, 9.0
    room(O, HX, HY, H_, sockets=False)
    port = K.image_emission("cell_port", os.path.join(ASSETS, "window.png"), 0.8)
    disc(Vector((O.x, O.y + HY - 0.05, O.z + 4.6)), 3.2, 0.1, port).rotation_euler = (math.radians(90), 0, 0)
    torus(Vector((O.x, O.y + HY - 0.2, O.z + 4.6)), 3.25, 0.12, DOMAIN_ARK, rot=(math.radians(90), 0, 0))
    torus(Vector((O.x, O.y + HY - 0.3, O.z + 4.6)), 3.6, 0.35, BODY, rot=(math.radians(90), 0, 0))
    for sx in (-1, 1):                         # runner trim converging down the floor
        block(O.x + sx * 1.6, O.y, O.z + 0.03, 0.06, 2 * HY - 1.0, 0.04, DOMAIN_ARK, bevel=0, name="trim")
    slot = 0
    for k in range(4):
        y = O.y - HY + 5.0 + k * 7.0
        for sx in (-1, 1):
            xw = O.x + sx * (HX - 0.3)
            for dy in (-1.6, 1.6):
                block(xw - sx * 0.35, y + dy, O.z + 3.4, 0.7, 0.5, 6.8, BODY, name="pilaster")
            block(xw - sx * 0.35, y, O.z + 6.95, 0.7, 3.7, 0.5, BODY, name="lintel")
            block(xw - sx * 0.1, y, O.z + 3.4, 0.2, 2.7, 6.4, emit_mat(f"niche_{k}_{sx}", (0.35, 0.5, 0.9), 0.35 if slot < 4 else 0.05), name="niche")
            block(xw - sx * 0.65, y, O.z + 0.6, 1.3, 1.6, 1.2, BODY, name="plinth")
            block(xw - sx * 1.31, y, O.z + 1.18, 0.05, 1.6, 0.05, DOMAIN_ARK, bevel=0, name="trim")
            if slot < 4:
                art_obj(slot, Vector((xw - sx * 0.7, y, O.z + 2.9)), scale=1.6, spin=0.5 * slot)
                SPOTS.append(("SPOT", f"niche_spot_{slot}", (xw - sx * 1.6, y, O.z + 6.4), 1100 if slot == ART_SEL else 380,
                              (0.85, 0.92, 1.0), 0.15, (xw - sx * 0.7, y, O.z + 2.6), 26))
            else:
                add("octa", (xw - sx * 0.7, y, O.z + 2.9), (0.3, 0.3, 0.4), (SQUAD, "shielded", 0.06))
            slot += 1
    ROOM_BOX = (O + Vector((0, 0, H_ / 2)), Vector((HX - 0.4, HY - 0.4, H_ / 2 - 0.2)))
    CAM = ((0.6, O.y - HY + 1.2, O.z + 2.4), (-0.3, O.y + HY, O.z + 3.6), 20, 9.0, 4.0)

if SCENE == "art_c":
    # C - the Orrery: a low chart room; over a round table the four artifacts ride gimbal rings round a small cold
    # core, the selected one (Hydra) drawn out toward you in a cone of light from the table
    O = Vector((0.0, 760.0, -20.0)); HX, HY, H_ = 10.0, 9.0, 7.0
    room(O, HX, HY, H_)
    T = O + Vector((0, 0.5, 0))
    disc(T + Vector((0, 0, 0.5)), 3.2, 1.0, BODY)
    torus(T + Vector((0, 0, 1.02)), 3.1, 0.05, DOMAIN_ARK)
    torus(T + Vector((0, 0, 1.02)), 1.6, 0.03, DOMAIN_ARK)
    C0 = T + Vector((0, 0, 3.6))
    add("stella", C0, (0.55, 0.55, 0.55), (SQUAD, "super", 2.2), rot=rand_rot())
    plight("POINT", C0, 600, (0.65, 0.8, 1.0), 0.4)
    rings = [(2.2, (math.radians(90), 0, 0)), (2.6, (math.radians(70), math.radians(35), 0)), (3.0, (math.radians(20), 0, 0)),
             (1.8, (math.radians(105), math.radians(-40), 0))]
    for i, (R2, rot) in enumerate(rings):
        torus(C0, R2, 0.035, DOMAIN_ARK, rot=rot)
        Rm = Euler(rot).to_matrix()
        a = [0.6, 2.4, 3.9, 5.2][i]
        p = C0 + Rm @ Vector((R2 * math.cos(a), R2 * math.sin(a), 0))
        if i == ART_SEL:
            p = C0 + Vector((0.9, -2.6, -0.9))
            torus(Vector((p.x, p.y, T.z + 1.03)), 0.7, 0.03, DOMAIN_ARK)
            SPOTS.append(("SPOT", "holo_spot", p + Vector((0, 0, 3.0)), 900, (0.85, 0.92, 1.0), 0.15, p, 22))
        art_obj(i, p, scale=3.0 if i == ART_SEL else 1.3, spin=0.7 * i)
        plight("POINT", p + Vector((0, -0.6, 0.4)), 120 if i == ART_SEL else 30, (0.8, 0.9, 1.0), 0.3)
    for sx in (-1, 1):                       # dim display shelves along the side walls, mostly empty
        for r in range(3):
            block(O.x + sx * (HX - 0.7), O.y, O.z + 1.4 + r * 1.6, 1.2, 10.0, 0.12, BODY, bevel=0.02, name="shelf")
    ROOM_BOX = (O + Vector((0, 0, H_ / 2)), Vector((HX - 0.4, HY - 0.4, H_ / 2 - 0.2)))
    CAM = ((-3.2, O.y - 8.6, O.z + 3.4), (0.3, O.y + 0.5, O.z + 3.0), 22, 6.5, 3.6)


if SCENE == "art_ab":
    # A + B (Shombith, 2026-11-12: "combine A and B, A for the platform and B for the background"): the Reliquary's
    # platform - pedestals round a column of light, domain rings - standing in the Gallery's hall; the hall's alcoves
    # now hold the eight dark slots for the artifacts still to be found, its porthole onto the cell at the far end
    O = Vector((0.0, 840.0, -20.0)); HX, HY, H_ = 8.0, 17.0, 10.0
    room(O, HX, HY, H_, sockets=False)
    port = K.image_emission("cell_port_ab", os.path.join(ASSETS, "window.png"), 0.8)
    disc(Vector((O.x, O.y + HY - 0.05, O.z + 5.0)), 3.4, 0.1, port).rotation_euler = (math.radians(90), 0, 0)
    torus(Vector((O.x, O.y + HY - 0.2, O.z + 5.0)), 3.45, 0.12, DOMAIN_ARK, rot=(math.radians(90), 0, 0))
    torus(Vector((O.x, O.y + HY - 0.3, O.z + 5.0)), 3.8, 0.35, BODY, rot=(math.radians(90), 0, 0))
    for sx in (-1, 1):
        block(O.x + sx * 1.6, O.y - HY / 2 - 2.5, O.z + 0.03, 0.06, HY - 5.0, 0.04, DOMAIN_ARK, bevel=0, name="trim")
        block(O.x + sx * 1.6, O.y + HY / 2 + 2.5, O.z + 0.03, 0.06, HY - 5.0, 0.04, DOMAIN_ARK, bevel=0, name="trim")
    for k in range(4):
        y = O.y - HY + 5.0 + k * 7.6
        for sx in (-1, 1):
            xw = O.x + sx * (HX - 0.3)
            for dy in (-1.6, 1.6):
                block(xw - sx * 0.35, y + dy, O.z + 3.6, 0.7, 0.5, 7.2, BODY, name="pilaster")
            block(xw - sx * 0.35, y, O.z + 7.35, 0.7, 3.7, 0.5, BODY, name="lintel")
            block(xw - sx * 0.1, y, O.z + 3.6, 0.2, 2.7, 6.8, emit_mat(f"niche_ab_{k}_{sx}", (0.35, 0.5, 0.9), 0.16), name="niche")
            block(xw - sx * 0.65, y, O.z + 0.6, 1.3, 1.6, 1.2, BODY, name="plinth")
            block(xw - sx * 1.31, y, O.z + 1.18, 0.05, 1.6, 0.05, DOMAIN_ARK, bevel=0, name="trim")
            add("octa", (xw - sx * 0.7, y, O.z + 2.9), (0.3, 0.3, 0.4), (SQUAD, "shielded", 0.06))
    # A's platform in the middle of the hall
    P = O + Vector((0.0, 2.0, 0.0)); H2 = H_
    disc(P + Vector((0, 0, 0.15)), 6.6, 0.3, BODY)
    for r_, th in ((6.4, 0.06), (2.2, 0.04), (3.0, 0.04)):
        torus(P + Vector((0, 0, 0.32)), r_, th, DOMAIN_ARK)
    disc(P + Vector((0, 0, H2 / 2)), 0.22, H2, emit_mat("beam_ab", (0.55, 0.75, 1.0), 2.0, alpha=0.22), verts=48)
    disc(P + Vector((0, 0, H2 / 2)), 1.0, H2, emit_mat("beam_halo_ab", (0.4, 0.6, 1.0), 0.25, alpha=0.06), verts=48)
    torus(P + Vector((0, 0, H2 - 0.2)), 1.2, 0.08, DOMAIN_ARK)
    plight("POINT", P + Vector((0, 0, 3.0)), 900, (0.65, 0.8, 1.0), 0.6)
    for i, a_ in enumerate([-75, -27, 27, 75]):
        a = math.radians(a_)
        p = P + Vector((4.4 * math.sin(a), 4.4 * math.cos(a), 0.3))
        disc(p + Vector((0, 0, 0.55)), 0.75, 1.1, BODY, verts=48)
        torus(p + Vector((0, 0, 1.12)), 0.68, 0.035, DOMAIN_ARK)
        art_obj(i, p + Vector((0, 0, 2.75)), scale=2.9 if i == ART_SEL else 2.3, spin=0.4 * i)
        SPOTS.append(("SPOT", f"ab_spot_{i}", p + Vector((0, 0, 8.5)), 1400 if i == ART_SEL else 420, (0.85, 0.92, 1.0), 0.15,
                      p + Vector((0, 0, 1.0)), 16))
        plight("POINT", p + Vector((0, 0, 2.6)), 140 if i == ART_SEL else 45, (0.8, 0.9, 1.0), 0.4)
    ROOM_BOX = (O + Vector((0, 0, H_ / 2)), Vector((HX - 0.4, HY - 0.4, H_ / 2 - 0.2)))
    CAM = ((3.6, O.y - 9.5, O.z + 3.6), (0.6, O.y + 6.0, O.z + 3.4), 20, 12.0, 4.5)


if SCENE == "decor":
    # the antechamber of the decor bay: the bay's sealed hatch is the room's back wall
    O = Vector((0.0, 540.0, -20.0))
    room(O, 6.5, 8.0, 7.6)
    ROOM_BOX = (O + Vector((0, 0, 3.8)), Vector((6.1, 7.6, 3.7)))
    hy, hz = O.y + 7.95, O.z + 3.6
    # the frame and its two heavy leaves, shut; the domain's trim round the frame
    block(O.x, hy - 0.35, hz + 3.3, 7.4, 0.7, 0.6, BODY, name="hatch"); block(O.x, hy - 0.35, hz - 3.3, 7.4, 0.7, 0.6, BODY, name="hatch")
    block(O.x - 3.45, hy - 0.35, hz, 0.5, 0.7, 7.2, BODY, name="hatch"); block(O.x + 3.45, hy - 0.35, hz, 0.5, 0.7, 7.2, BODY, name="hatch")
    block(O.x, hy - 0.72, hz + 3.0, 6.4, 0.08, 0.08, DOMAIN_ARK, bevel=0, name="hatch_trim")
    block(O.x, hy - 0.72, hz - 3.0, 6.4, 0.08, 0.08, DOMAIN_ARK, bevel=0, name="hatch_trim")
    block(O.x - 1.6, hy - 0.25, hz, 3.15, 0.4, 5.9, ENGINE_ARK, name="leaf"); block(O.x + 1.6, hy - 0.25, hz, 3.15, 0.4, 5.9, ENGINE_ARK, name="leaf")
    for k in range(6):
        block(O.x - 1.6, hy - 0.5, hz - 2.4 + k * 0.96, 2.9, 0.12, 0.16, BODY, name="leaf_rib")
        block(O.x + 1.6, hy - 0.5, hz - 2.4 + k * 0.96, 2.9, 0.12, 0.16, BODY, name="leaf_rib")
    # hazard teeth either side: danger blades set edge-out, as the hull sets them
    for sx in (-1, 1):
        for k in range(5):
            add("blade", (O.x + sx * 4.6, hy - 0.8, O.z + 0.6 + k * 1.4), (0.6, 0.6, 1.1), ("Red", "danger", 0.6), fwd=(sx, -0.4, 0.2))
    sm = bpy.data.materials.new("slit"); sm.use_nodes = True
    sp_ = sm.node_tree.nodes["Principled BSDF"]
    sp_.inputs["Base Color"].default_value = (0, 0, 0, 1)
    sp_.inputs["Emission Color"].default_value = (1.0, 0.62, 0.3, 1); sp_.inputs["Emission Strength"].default_value = 2.5
    SLIT = block(O.x, hy - 0.47, hz + 1.6, 5.6, 0.05, 0.22, sm, name="slit")
    lm = bpy.data.materials.new("lock"); lm.use_nodes = True
    lnt = lm.node_tree; lnt.nodes.clear()
    lo = lnt.nodes.new("ShaderNodeOutputMaterial"); le = lnt.nodes.new("ShaderNodeEmission"); le.name = "e"
    lt = lnt.nodes.new("ShaderNodeBsdfTransparent"); lmx = lnt.nodes.new("ShaderNodeMixShader"); lmx.inputs[0].default_value = 0.35
    le.inputs[0].default_value = (0.5, 0.56, 0.85, 1); le.inputs[1].default_value = 0.8
    lnt.links.new(lt.outputs[0], lmx.inputs[1]); lnt.links.new(le.outputs[0], lmx.inputs[2]); lnt.links.new(lmx.outputs[0], lo.inputs[0])
    LOCKMAT = lm
    ly = hy - 1.6
    bpy.ops.mesh.primitive_torus_add(major_radius=0.55, minor_radius=0.09, location=(O.x, ly, hz + 0.75), rotation=(math.radians(90), 0, 0))
    sh = bpy.context.active_object
    bm_ = sh.modifiers.new("cut", "BOOLEAN")
    cutter = block(O.x, ly, hz + 0.25, 1.4, 1.0, 1.0, BODY, bevel=0, name="cutter")
    cutter.hide_render = True; cutter.hide_viewport = True
    bm_.object = cutter; bm_.operation = "DIFFERENCE"
    sh.data.materials.append(lm)
    body_ = block(O.x, ly, hz - 0.15, 1.5, 0.12, 1.2, lm, bevel=0, name="lock_body")
    SCAN = block(O.x, ly - 0.02, hz, 1.9, 0.02, 0.03, lm, bevel=0, name="scan")
    lk = plight("POINT", (O.x, ly - 0.6, hz), 120, (0.75, 0.8, 1.0), 0.6)
    LOCK = [sh, body_, lk]
    CAM = ((2.4, O.y - 6.6, O.z + 3.2), (1.0, O.y + 8.0, O.z + 3.2), 26, 13.2, 3.2)

K.build(bevel_width=0.06)
PIER_LIGHT = None
if SCENE == "pier":
    # the pier: the hangar's floor carried on out of the open mouth into the cell, in the hull's plating, on girders,
    # the domain's trim down both edges, posts with the squad's prisms for lamps, and a round deck in the middle
    # (trimmed in the domain) where the four crystals turn and fuse
    L_ = FRONT - PIER_END
    yc = (FRONT + PIER_END) / 2
    block(0.0, yc, Z0 - 0.45, 20.0, L_, 0.9, BODY, name="pier")
    block(0.0, yc, Z0 - 2.4, 4.0, L_ - 4.0, 3.0, BODY, name="spine")
    for k in range(int(L_ / 8) + 1):
        y = FRONT - 2 - k * 8.0
        block(0.0, y, Z0 - 1.6, 19.0, 0.6, 1.4, BODY, name="girder")
    for sx in (-1, 1):
        block(sx * 9.55, yc, Z0 + 0.04, 0.12, L_ - 0.6, 0.08, DOMAIN_ARK, bevel=0, name="trim")
        block(sx * 10.02, yc, Z0 - 0.45, 0.06, L_, 0.06, DOMAIN_ARK, bevel=0, name="trim")
        for k in range(int(L_ / 10)):
            y = FRONT - 6 - k * 10.0
            if abs(y - PIER_MID.y) < 9:
                continue
            block(sx * 9.4, y, Z0 + 0.7, 0.3, 0.3, 1.4, BODY, name="post")
            add("box", (sx * 9.4, y, Z0 + 1.75), (0.22, 0.22, 0.6), (SQUAD, "plain", 1.0), fwd=(0, 0, 1), up=(0, 1, 0))
    block(0.0, PIER_END - 0.3, Z0 + 0.04, 19.0, 0.12, 0.08, DOMAIN_ARK, bevel=0, name="trim")
    bpy.ops.mesh.primitive_cylinder_add(vertices=96, radius=7.2, depth=0.3, location=(PIER_MID.x, PIER_MID.y, Z0 + 0.12))
    _dk = bpy.context.active_object; _dk.data.materials.append(BODY)
    for r_, th in ((7.0, 0.07), (5.2, 0.05)):
        bpy.ops.mesh.primitive_torus_add(major_radius=r_, minor_radius=th, major_segments=128, minor_segments=8,
                                         location=(PIER_MID.x, PIER_MID.y, Z0 + 0.3))
        bpy.context.active_object.data.materials.append(DOMAIN_ARK)
    # the crystals' own light on the deck under them (the combine is laid over this spot)
    PIER_LIGHT = plight("POINT", PIER_MID + Vector((0, 0, 4.0)), 2600, (0.6, 1.0, 0.25), size=3.0)
    # the cell's light falling on the pier from above
    plight("AREA", (0.0, yc, 18.0), 9000, (0.55, 0.68, 1.0), size=60.0, aim=(0.0, yc, Z0))
for _bp in BACKDROPS:                        # the cell's picture outside the hangar's mouth: only the hangar sees it, and
    _bp.hide_render = SCENE not in ("squads", "deck", "pier")      # it would cut through the rooms
for _o in col.objects:                       # the cell's lattice outside: no camera here looks out of the mouth, and one
    if _o.name == str(("Blue", "plain", 0.81)):      # of its curves dips through the hangar's mouth
        _o.hide_render = SCENE not in ("deck", "pier")

# ------ light and air

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
for (_k, _n, _l, _e, _c, _s, _aim, _ang) in SPOTS:
    light(_k, _n, _l, _e, _c, _s, aim=_aim, angle=_ang)
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
for i, (p, online) in enumerate(PAD_LIGHTS):
    if online:
        DOMAIN_LIGHTS.append(light("POINT", f"pad_{i}", p + Vector((0, 0, 0.6)), 380, (1, 1, 1), 2.5))
    else:                                       # offline: no domain light, only a dim work light so the hull still reads
        light("POINT", f"pad_off_{i}", p + Vector((-2.0, -4.0, 6.0)), 260, (0.75, 0.8, 0.9), 3.0)
light("AREA", "board_glow", (BX + 2.2, -63.0, -17.4), 450, (0.55, 0.55, 1.0), size=9, aim=(-HW, -63.0, -17.4))
for (x, y) in ((-11.5, -84.0), (11.5, -84.0)):
    light("POINT", f"danger_{x}", (x, y, Z0 + 2.0), 500, (1.0, 0.14, 0.07), 0.8)
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


# ------------------------------------------------------------------------------------------------ what moves
SQUAD_MATS = {k: m for k, m in K.MATS.items() if k[0] == SQUAD}
GAINS = []
for k_, m in K.MATS.items():
    g = m.node_tree.nodes.get("gain")
    if g is not None:
        GAINS.append((g, g.inputs[1].default_value, rng.uniform(0, math.tau)))
# ripples out of the tunnel: the domain trim's light carried outward in rings
dnt = DOMAIN_ARK.node_tree
dp = dnt.nodes["pbr"]
geo = dnt.nodes.new("ShaderNodeNewGeometry")
dsub = dnt.nodes.new("ShaderNodeVectorMath"); dsub.operation = "SUBTRACT"; dsub.inputs[1].default_value = TUNNEL
dnt.links.new(geo.outputs["Position"], dsub.inputs[0])
dlen = dnt.nodes.new("ShaderNodeVectorMath"); dlen.operation = "LENGTH"; dnt.links.new(dsub.outputs[0], dlen.inputs[0])


def dmath(op, *xs, clamp=False):
    n = dnt.nodes.new("ShaderNodeMath"); n.operation = op; n.use_clamp = clamp
    for i_, x in enumerate(xs):
        if x is None:
            continue
        if isinstance(x, (int, float)):
            n.inputs[i_].default_value = x
        else:
            dnt.links.new(x, n.inputs[i_])
    return n


RIPPLE_PHASE = dnt.nodes.new("ShaderNodeValue"); RIPPLE_PHASE.name = "phase"
RIPPLE_GAIN = dnt.nodes.new("ShaderNodeValue"); RIPPLE_GAIN.outputs[0].default_value = 1.0
kr = dmath("MULTIPLY", dlen.outputs[1], 1.25)
wave = dmath("SINE", dmath("SUBTRACT", kr.outputs[0], RIPPLE_PHASE.outputs[0]).outputs[0])
crest = dmath("POWER", dmath("MULTIPLY_ADD", wave.outputs[0], 0.5, 0.5).outputs[0], 4.0)
near_ = dmath("SUBTRACT", 1.0, dmath("DIVIDE", dlen.outputs[1], 24.0).outputs[0], clamp=True)
rip = dmath("MULTIPLY", dmath("MULTIPLY", crest.outputs[0], near_.outputs[0]).outputs[0], RIPPLE_GAIN.outputs[0])
strength = dmath("MULTIPLY", dmath("MULTIPLY_ADD", rip.outputs[0], 9.0, 1.0).outputs[0], dp.inputs["Emission Strength"].default_value)
dnt.links.new(strength.outputs[0], dp.inputs["Emission Strength"])
# haze: 4D noise moved round a circle, so the loop closes
hn = hm.node_tree
hz_tc = hn.nodes.new("ShaderNodeTexCoord")
hz_add = hn.nodes.new("ShaderNodeVectorMath"); hz_add.operation = "ADD"
hn.links.new(hz_tc.outputs["Object"], hz_add.inputs[0])
hz_n = hn.nodes.new("ShaderNodeTexNoise"); hz_n.noise_dimensions = "4D"
hz_n.inputs["Scale"].default_value = 2.2; hz_n.inputs["Detail"].default_value = 3.0
hn.links.new(hz_add.outputs[0], hz_n.inputs["Vector"])
hz_r = hn.nodes.new("ShaderNodeMapRange"); hz_r.inputs[1].default_value = 0.35; hz_r.inputs[2].default_value = 0.75
hz_r.inputs[3].default_value = 0.0; hz_r.inputs[4].default_value = 0.014 if SCENE == "squads" else 0.0035   # the rooms: thinner
hn.links.new(hz_n.outputs["Fac"], hz_r.inputs[0]); hn.links.new(hz_r.outputs[0], hv.inputs["Density"])
BOARD_TEX = [n for n in board.node_tree.nodes if n.bl_idname == "ShaderNodeTexImage"][0]
MAPS = [os.path.join(ASSETS, "map" if DOM == "Jade" else f"map_{DOM}", f"map_{i:04d}.png") for i in range(LOOP)]
ENGINE_E = ENGINE.node_tree.nodes["pbr"].inputs["Emission Strength"]
ENGINE_E0 = ENGINE_E.default_value


def apply_set(name):
    S = COLOUR_SETS[name]
    for dom in ("Jade", "Ruby", "Gold", "Blue"):
        for kind in ("plain", "shielded", "super"):
            K.PAL[dom][kind] = (tuple(S[dom][kind][0]), tuple(S[dom][kind][1]))
    for (pal, kind, gain), m in K.MATS.items():
        if kind == "danger":
            m.node_tree.nodes["rim"].inputs[0].default_value = (*S["env"]["danger"], 1)
        elif kind in ("plain", "shielded", "super"):
            b_, r_ = K.PAL[DOM if pal == SQUAD else pal][kind]
            K.recolour(m, b_, r_, K.LOOK[kind])
    c = S[DOM]["ship"][1]
    peak = max(c)
    for m in (DOMAIN_ARK, DOMAIN_V):
        m.node_tree.nodes["pbr"].inputs["Emission Color"].default_value = (*[v / peak for v in c], 1)
        m.node_tree.nodes["base"].inputs[6].default_value = (*[0.02 + 0.22 * v / peak for v in c], 1)
    rim = S[DOM]["plain"][1]
    pk = max(rim)
    for Lt in DOMAIN_LIGHTS:
        Lt.data.color = tuple(v / pk for v in rim)


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def pose(t, run):
    s1, c1 = math.sin(math.tau * t), math.cos(math.tau * t)
    for g, base, ph in GAINS:
        g.inputs[1].default_value = base * (1.0 + 0.4 * math.sin(math.tau * 2 * t + ph))
    hz_add.inputs[1].default_value = (0.9 * s1, 0.9 * c1, 0.0)
    hz_n.inputs["W"].default_value = 0.6 * s1
    RIPPLE_PHASE.outputs[0].default_value = math.tau * 3 * t
    RIPPLE_GAIN.outputs[0].default_value = 1.0
    BOARD_TEX.image = bpy.data.images.load(MAPS[int(round(t * LOOP)) % LOOP], check_existing=True)
    ENGINE_E.default_value = ENGINE_E0 * (1.0 + 0.18 * math.sin(math.tau * 4 * t))
    for o, M0, ph in MOVERS:
        bob = Vector((0, 0, 0.12 * math.sin(math.tau * t + ph)))
        roll = Matrix.Rotation(0.03 * math.sin(math.tau * t + ph * 1.7), 4, "Y")
        if SCENE == "converter":
            bob = Vector((0, 0, 0.09 * math.sin(math.tau * t + ph)))
            roll = Matrix.Rotation(math.tau * t * (1 if ph > math.pi else -1), 4, "Z")
        o.matrix_world = Matrix.Translation(bob) @ M0 @ roll
    for o, M0, turns, _l, sel in ART_OBJS:
        o.matrix_world = Matrix.Translation(Vector((0, 0, 0.05 * math.sin(math.tau * t)))) @ M0 @ Matrix.Rotation(math.tau * t * turns, 4, "Z")
    if SCENE == "decor":
        LOCKMAT.node_tree.nodes["e"].inputs[1].default_value = 0.6 + 0.5 * (0.5 + 0.5 * math.sin(math.tau * 2 * t))
        SCAN.location.z = LOCK[1].location.z + 0.95 * math.sin(math.tau * t)
        LOCK[2].data.energy = 90 + 50 * (0.5 + 0.5 * math.sin(math.tau * 2 * t))
        SLIT.data.materials[0].node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 2.2 + 0.5 * math.sin(math.tau * 3 * t + 1.0)
    for ring, axis, turns, base in RINGS:
        ring.matrix_world = Matrix.Translation(CORE) @ Matrix.Rotation(math.tau * t * turns * (2.0 if run == "convert" else 1.0), 4, axis) @ base
    if SCENE == "converter":
        CORE_LIGHT.data.energy = 500 * (1.0 + 0.25 * math.sin(math.tau * 2 * t))
    if PIER_LIGHT is not None:
        PIER_LIGHT.data.energy = 2600 * (1.0 + 0.2 * math.sin(math.tau * 2 * t))
    if SCENE in ("deck", "pier"):
        for o_, l0 in zip(LATTICE, LAT0):
            o_.location = l0 + Vector((9.0 * s1, 0, 3.5 * c1))
        WIN_MAP.inputs["Location"].default_value = (0.03 * s1, 0.012 * c1, 0)
    if SCENE == "converter" and run == "convert":
        convert(t)


ORDER = ["Space", "Charge", "Mass", "Time"]            # the game's fly-in order (ConverterRecipeRow)


def convert(t):
    """One of each element leaves its cradle, in order, and flies into the core; each arrival flashes the trim; the rings
    spin up; the Omni forms in the core and rises out of the rings, pulsing."""
    flash = 0.0
    for i, name in enumerate(ORDER):
        o = CRY[name][0]
        M0 = [m for (ob, m, ph) in MOVERS if ob is o][0]
        a, b = 0.05 + i * 0.13, 0.05 + i * 0.13 + 0.24
        u = smooth((t - a) / (b - a))
        p0 = M0.to_translation()
        p1 = CORE
        p = p0.lerp(p1, u) + Vector((0, 0, 3.2 * math.sin(math.pi * u)))
        sc_ = 1.0 - 0.85 * u
        o.matrix_world = Matrix.Translation(p) @ M0.to_quaternion().to_matrix().to_4x4() @ Matrix.Rotation(math.tau * 2 * u, 4, "Z") @ Matrix.Diagonal((sc_, sc_, sc_, 1))
        o.hide_render = u >= 0.995
        flash = max(flash, math.exp(-((t - b) / 0.035) ** 2))
    RIPPLE_GAIN.outputs[0].default_value = 1.0 + 3.5 * flash + 2.0 * smooth((t - 0.6) / 0.1) * (1 - smooth((t - 0.9) / 0.1))
    RIPPLE_PHASE.outputs[0].default_value = math.tau * 6 * t
    g = smooth((t - 0.62) / 0.16)
    out_ = smooth((t - 0.8) / 0.2)
    s_ = max(0.0001, g * (1.0 + 0.08 * math.sin(math.tau * 6 * t)))
    OMNI.matrix_world = (Matrix.Translation(CORE + Vector((0, 0, 2.6 * out_)))
                         @ Matrix.Rotation(math.tau * 1.5 * t, 4, "Z") @ Matrix.Rotation(0.4, 4, "X") @ Matrix.Diagonal((s_, s_, s_, 1)))
    OMNI.data.materials[0].node_tree.nodes["glow"].inputs[1].default_value = 4.0 * (1.8 + 1.5 * math.exp(-((t - 0.78) / 0.05) ** 2))
    FLASH.data.energy = 3500 * flash + 2500 * math.exp(-((t - 0.78) / 0.05) ** 2)


FLASH = None
if SCENE == "converter":
    bpy.ops.object.light_add(type="POINT", location=CORE + Vector((0, -1.0, 0)))
    FLASH = bpy.context.active_object
    FLASH.data.color = (0.7, 1.0, 0.35); FLASH.data.shadow_soft_size = 1.0; FLASH.data.energy = 0.0

if ROOM_BOX is not None:
    bpy.ops.mesh.primitive_cube_add(size=2, location=ROOM_BOX[0])
    rh = bpy.context.active_object
    rh.scale = ROOM_BOX[1]
    rh.data.materials.append(hm)
LATTICE = [o for o in col.objects if o.name == str(("Blue", "plain", 0.81))]
LAT0 = [o.location.copy() for o in LATTICE]
WIN_MAP = None
if SCENE in ("deck", "pier"):
    _wnt = win.node_tree
    _wtx = [n for n in _wnt.nodes if n.bl_idname == "ShaderNodeTexImage"][0]
    _tc = _wnt.nodes.new("ShaderNodeTexCoord")
    WIN_MAP = _wnt.nodes.new("ShaderNodeMapping")
    _wnt.links.new(_tc.outputs["UV"], WIN_MAP.inputs["Vector"]); _wnt.links.new(WIN_MAP.outputs[0], _wtx.inputs["Vector"])
    WORLD_BG.inputs[1].default_value = 0.28 if SCENE == "deck" else 0.5
if os.environ.get("ARK_CAM"):              # "x,y,z;ax,ay,az;lens": try a camera
    _c = [tuple(float(v) for v in part.split(",")) for part in os.environ["ARK_CAM"].split(";")]
    CAM = (_c[0], _c[1], _c[2][0], (Vector(_c[1]) - Vector(_c[0])).length, 8.0)
loc0, aim, lens, focus, fstop = CAM
if os.environ.get("ARK_PASS") == "vessels":     # the live layer: only the hulls, the room held out (it still occludes)
    sc.render.film_transparent = True
    sc.render.image_settings.color_mode = "RGBA"
    keep = set(VESSEL_OBJS["in"])
    for o_ in sc.objects:
        if o_.name.startswith("Cube") and o_.data.materials and o_.data.materials[0].name.startswith("haze"):
            o_.hide_render = True                    # the haze box encloses the bay: it would hold out everything
        elif o_.type == "MESH" and o_ not in keep:
            o_.is_holdout = True
if os.environ.get("ARK_BORDER"):              # "x0,x1,y0,y1" in pixels from the top left: render only that box (the frame
    _b = [float(v) for v in os.environ["ARK_BORDER"].split(",")]     # stays full size, black outside it; r110_squads_empty_pads.py re-renders the pads)
    sc.render.use_border, sc.render.use_crop_to_border = True, False
    sc.render.border_min_x, sc.render.border_max_x = _b[0] / W, _b[1] / W
    sc.render.border_min_y, sc.render.border_max_y = 1.0 - _b[3] / H, 1.0 - _b[2] / H
camd.lens, camd.clip_start, camd.clip_end = lens, 0.1, 3000
camd.dof.use_dof, camd.dof.focus_distance, camd.dof.aperture_fstop = True, focus, fstop
sc.view_settings.exposure = {"deck": -0.4, "pier": -0.7}.get(SCENE, -0.9)
if os.environ.get("ARK_PROBE"):              # print what a column of rays from the camera hits, then stop
    bpy.context.view_layer.update()
    _dg = bpy.context.evaluated_depsgraph_get()
    for zz in [Z0 + 0.5 * k for k in range(-2, 24)]:
        _d = Vector((0.0, -128.0, zz)) - Vector(loc0)
        ok, hit, _n, _i, ob, _m = sc.ray_cast(_dg, Vector(loc0), _d.normalized(), distance=500)
        print("PROBE", round(zz, 1), ob.name if ok else None, tuple(round(v, 1) for v in hit) if ok else None, flush=True)
    sys.exit(0)
only = {int(x) for x in ONLY.split(",")} if ONLY else None
runs = [("loop", LOOP)] + ([("convert", CONV)] if SCENE == "converter" else [])
if RUNS:
    runs = [r for r in runs if r[0] in RUNS.split(",")]
for set_name in SETS:
    apply_set(set_name)
    for run, n in runs:
        od = os.path.join(OUT, f"{SCENE}_{set_name}{'' if DOM == 'Jade' else '_' + DOM}_{run}")
        os.makedirs(od, exist_ok=True)
        for f in range(n):
            if only is not None and f not in only:
                continue
            if os.path.exists(os.path.join(od, f"f_{f:04d}.png")) and not only:   # resume after a restart
                continue
            t = f / n
            for objs in CRY.values():
                for o in objs:
                    o.hide_render = False
            if OMNI is not None:
                OMNI.matrix_world = Matrix.Diagonal((0.0001, 0.0001, 0.0001, 1))
                if FLASH:
                    FLASH.data.energy = 0.0
            pose(t if run == "loop" else (f / n), run)
            s1 = math.sin(math.tau * t)
            sway = 0.0 if SCENE == "pier" else 1.0      # the pier holds still: Garrett's combine is laid over it
            cam.location = Vector(loc0) + sway * Vector((0.18 * s1, 0.0, 0.08 * math.sin(math.tau * 2 * t)))
            cam.rotation_euler = (Vector(aim) + sway * Vector((0.3 * math.cos(math.tau * t), 0, 0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
            bpy.context.view_layer.update()
            sc.render.filepath = os.path.join(od, f"f_{f:04d}.png")
            bpy.ops.render.render(write_still=True)
            print("FRAME", SCENE, set_name, run, f, flush=True)
print("DONE")
