"""Rev 47 renders: the Ark and the squad hulls in each domain's REAL ship colours (body, glass and engine from
vessel_colors.py, the domain slot from the palette's ShipColor1/2), lit for a showroom (key, fill and rim light, the
domain plates glowing), on a transparent ground; plus the Ark as a 36-frame orbit for the animated preview. The unlit
VesselGraph fresnel of render_cards.py is kept as each material's emission.

Run with a Python that has bpy (Docs/VesselModels and .claude/skills/vessel-model/references/PIPELINE.md: a
python3.11 venv with `pip install bpy numpy pillow`):

    <bpy python> r47_render.py <out_dir> hulls 0 512 [Sparrow,Dolphin,...]     <hull>_<Jade|Ruby|Gold|Class>.png
    <bpy python> r47_render.py <out_dir> ark 36 1280 <Jade|Ruby|Gold>          ark_<domain>_<frame>.png

The frames were packed into one animated WebP per domain (PIL, 167 ms a frame, cropped to the union of the frames).
"""
import json, math, os, sys
REPO = __import__("os").path.abspath(__import__("os").path.join(__import__("os").path.dirname(__file__), "..", "..", ".."))
sys.path.insert(0, REPO + "/Tools/Build/vessel_model")  # vessel_colors
import vessel_colors as VC
import bpy
from mathutils import Vector, Quaternion

OUT = sys.argv[1]; WHAT = sys.argv[2]
FRAMES = int(sys.argv[3]) if len(sys.argv) > 3 else 24
SIZE = int(sys.argv[4]) if len(sys.argv) > 4 else 900
os.makedirs(OUT, exist_ok=True)
M = REPO + "/Assets/_Models/Vessel Models/"
FLEET = {"Domain": "domain", "Body": "body", "Window": "window", "Engine": "engine"}
ARTIST = {"accent": "domain", "BASE": "body", "windsheild": "window", "N": "engine"}
HULLS = {
    "Sparrow": (M + "SparrowModel1.fbx", FLEET, (0, -1, 0)),
    "Dolphin": (M + "dolphin_shapekey_with_animations.fbx", ARTIST, (0, -1, 0)),
    "Squirrel": (M + "SquirrelVessel_CosmicShoresTest1.fbx", FLEET, (0, -1, 0)),
    "Rhino": (M + "rhino_shapekey_with_animations.fbx", ARTIST, (0, -1, 0)),
    "Manta": (M + "Manta_shapekey_rigged.fbx", ARTIST, (0, -1, 0)),
    "Serpent": (M + "SerpentExport.fbx", FLEET, (0, 1, 0)),
}
DOMAINS = ["Jade", "Ruby", "Gold", "Class"]
SCH = VC.scheme()


def cols(domain):
    out = {}
    for role in ("body", "window", "engine"):
        rim, face = VC.linear_role(SCH[role]); out[role] = (rim, face)
    out["domain"] = VC.linear_role(SCH["domains"][domain])
    return out


GLOW = {"domain": 1.9, "engine": 1.8, "window": 0.8, "body": 0.35}
BASE = {"domain": 0.25, "engine": 0.3, "window": 1.0, "body": 1.0}


def mat(name, rim, face):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial"); g = nt.nodes.new("ShaderNodeNewGeometry")
    d = nt.nodes.new("ShaderNodeVectorMath"); d.operation = "DOT_PRODUCT"
    nt.links.new(g.outputs["Normal"], d.inputs[0]); nt.links.new(g.outputs["Incoming"], d.inputs[1])
    a = nt.nodes.new("ShaderNodeMath"); a.operation = "ABSOLUTE"; nt.links.new(d.outputs["Value"], a.inputs[0])
    mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = "RGBA"; nt.links.new(a.outputs[0], mx.inputs["Factor"])
    mx.inputs[6].default_value = tuple(rim) + (1,); mx.inputs[7].default_value = tuple(face) + (1,)
    role = name[2:]
    e = nt.nodes.new("ShaderNodeEmission"); nt.links.new(mx.outputs[2], e.inputs[0]); e.inputs[1].default_value = GLOW.get(role, 1.0)
    b = nt.nodes.new("ShaderNodeBsdfPrincipled"); b.name = "pbr"
    b.inputs["Metallic"].default_value = 0.55 if role == "body" else 0.0
    b.inputs["Roughness"].default_value = 0.3 if role == "body" else 0.25
    ad = nt.nodes.new("ShaderNodeAddShader"); nt.links.new(b.outputs[0], ad.inputs[0]); nt.links.new(e.outputs[0], ad.inputs[1])
    nt.links.new(ad.outputs[0], o.inputs[0])
    return m


def outline_mat():
    m = bpy.data.materials.new("outline"); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial"); g = nt.nodes.new("ShaderNodeNewGeometry")
    e = nt.nodes.new("ShaderNodeEmission"); e.inputs[0].default_value = (0.0012, 0.0014, 0.0035, 1)
    t = nt.nodes.new("ShaderNodeBsdfTransparent"); s = nt.nodes.new("ShaderNodeMixShader")
    lp = nt.nodes.new("ShaderNodeLightPath"); inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
    nt.links.new(lp.outputs["Is Camera Ray"], inv.inputs[1])
    mxm = nt.nodes.new("ShaderNodeMath"); mxm.operation = "MAXIMUM"
    nt.links.new(g.outputs["Backfacing"], mxm.inputs[0]); nt.links.new(inv.outputs[0], mxm.inputs[1])
    nt.links.new(mxm.outputs[0], s.inputs[0]); nt.links.new(e.outputs[0], s.inputs[1]); nt.links.new(t.outputs[0], s.inputs[2])
    nt.links.new(s.outputs[0], o.inputs[0])
    return m


def load(fbx, roles):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx, use_custom_normals=True, ignore_leaf_bones=False,
                             automatic_bone_orientation=False, use_anim=False)
    for a in [o for o in bpy.data.objects if o.type == "ARMATURE"]:
        a.data.pose_position = "REST"
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    for m in meshes:
        if m.data.shape_keys:
            for k in m.data.shape_keys.key_blocks: k.value = 0.0
    bpy.context.view_layer.update()
    mats = {r: mat("m_" + r, (1, 1, 1), (1, 1, 1)) for r in ("domain", "body", "window", "engine")}
    ol = outline_mat()
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for m in meshes:
        for i, s in enumerate(m.data.materials):
            n = s.name if s else ""
            m.data.materials[i] = mats[roles.get(n, roles.get(n.split(".")[0], "body"))]
        ev = m.evaluated_get(dg); me = ev.to_mesh()
        pts += [ev.matrix_world @ v.co for v in me.vertices]; ev.to_mesh_clear()
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    diag = (hi - lo).length
    for m in meshes:
        m.data.materials.append(ol)
        so = m.modifiers.new("ol", "SOLIDIFY"); so.thickness = diag * 0.004; so.offset = 1.0
        so.use_flip_normals = True; so.use_rim = False; so.material_offset = len(m.data.materials) - 1
    return mats, (lo + hi) / 2, diag, pts


def paint(mats, domain):
    c = cols(domain)
    for r, m in mats.items():
        mx = [n for n in m.node_tree.nodes if n.bl_idname == "ShaderNodeMix"][0]
        mx.inputs[6].default_value = tuple(c[r][0]) + (1,); mx.inputs[7].default_value = tuple(c[r][1]) + (1,)
        rim, face = c[r]
        base = [min(1.0, (rim[i] * 0.6 + face[i] * 0.4) * (2.0 if r == "body" else 1.0) * BASE.get(r, 1.0)) for i in range(3)]
        m.node_tree.nodes["pbr"].inputs["Base Color"].default_value = tuple(base) + (1,)


def setup(w, h, samples):
    sc = bpy.context.scene
    cd = bpy.data.cameras.new("Cam"); cd.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = samples
    sc.cycles.seed = 7; sc.cycles.use_denoising = True; sc.cycles.max_bounces = 3; sc.cycles.transparent_max_bounces = 16
    for nm, rot, en, col in (("key", (55, 0, -35), 4.0, (1.0, 0.97, 0.92)), ("fill", (70, 0, 120), 1.4, (0.55, 0.65, 1.0)),
                             ("rim", (-60, 0, 180), 5.0, (0.75, 0.85, 1.0))):
        ld = bpy.data.lights.new(nm, "SUN"); ld.energy = en; ld.color = col; ld.angle = 0.2
        lo_ = bpy.data.objects.new(nm, ld); sc.collection.objects.link(lo_)
        lo_.rotation_euler = tuple(math.radians(a) for a in rot)
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
    sc.render.resolution_x, sc.render.resolution_y = w, h; sc.render.resolution_percentage = 100
    wo = bpy.data.worlds.new("W"); sc.world = wo; wo.use_nodes = True; bg = wo.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.03, 0.05, 0.16, 1); bg.inputs[1].default_value = 1.0
    return sc, cam, cd


def aim(cam, cd, center, diag, d, roll_to_nose=None, nose=None, scale=None, pts=None, fill=0.9, aspect=1.0):
    d = d.normalized()
    q = (-d).to_track_quat("-Z", "Y")
    if roll_to_nose is not None:
        cx = q @ Vector((1, 0, 0)); cy = q @ Vector((0, 1, 0))
        th = math.degrees(math.atan2(nose.dot(cy), nose.dot(cx)))
        q = q @ Quaternion((0, 0, 1), math.radians(th - roll_to_nose))
    cx = q @ Vector((1, 0, 0)); cy = q @ Vector((0, 1, 0))
    mid = center
    if pts is not None:
        xs = [(p - center).dot(cx) for p in pts[::7]]; ys = [(p - center).dot(cy) for p in pts[::7]]
        mid = center + cx * (min(xs) + max(xs)) / 2 + cy * (min(ys) + max(ys)) / 2
        scale = max(max(xs) - min(xs), (max(ys) - min(ys)) * aspect) / fill
    cam.rotation_euler = q.to_euler(); cam.location = mid + d * diag * 3
    cd.clip_start = diag * 0.1; cd.clip_end = diag * 10; cd.ortho_scale = scale


if WHAT == "ark":
    mats, center, diag, pts = load(M + "ArkReverse.fbx", FLEET)
    W, H = SIZE, int(SIZE * 0.62)
    sc, cam, cd = setup(W, H, 12)
    F, U = Vector((0, -1, 0)), Vector((0, 0, 1)); R = F.cross(U)
    scale = diag * 0.98
    doms = sys.argv[5].split(",") if len(sys.argv) > 5 else ["Jade", "Ruby", "Gold"]
    for dom in doms:
        paint(mats, dom)
        for f in range(FRAMES):
            # a slow orbit: the camera circles the Ark at 28 degrees elevation, starting three-quarter from the bow
            a = math.radians(-50 + 360.0 * f / FRAMES)
            d = (R * math.sin(a) + F * math.cos(a)) * math.cos(math.radians(28)) + U * math.sin(math.radians(28))
            aim(cam, cd, center, diag, d, scale=scale)
            sc.render.filepath = "%s/ark_%s_%02d.png" % (OUT, dom, f)
            bpy.ops.render.render(write_still=True)
            print("FRAME", dom, f, flush=True)
else:
    names = sys.argv[5].split(",") if len(sys.argv) > 5 else list(HULLS)
    for name in names:
        fbx, roles, nose = HULLS[name]
        mats, center, diag, pts = load(fbx, roles)
        sc, cam, cd = setup(SIZE, SIZE, 24)
        F, U = Vector(nose), Vector((0, 0, 1)); R = F.cross(U)
        d = R * 0.85 + F * -0.35 + U * 0.6
        aim(cam, cd, center, diag, d, roll_to_nose=30.0, nose=F, pts=pts, fill=0.92)
        for dom in DOMAINS:
            paint(mats, dom)
            sc.render.filepath = "%s/%s_%s.png" % (OUT, name, dom)
            bpy.ops.render.render(write_still=True)
            print("HULL", name, dom, flush=True)
print("DONE")
