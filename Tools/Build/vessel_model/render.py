"""
render.py - look at the model: Cycles (CPU) contact sheets in a studio clay look.

Why a renderer is part of a MODELLING pipeline: a vessel is judged by eye, at the chase
camera's distance, and a session can read a PNG. Every build renders the same sheet so two
iterations can be compared frame for frame:

    row 1  the four views: chase (where the pilot sees it from - behind, above, slightly
           right), three-quarter front, side, top
    row 2  the four element extremes from the chase view (charge, mass, space, time at 100%)
    row 3  pose takes from the chase view (pitchup, yawright, rollright, speedup) when there are any

The look is deliberate: a dark neutral backdrop, a three-point rig, and the four slots in
flat studio colours (Domain peach, Body near-black, Window cyan, Engine lime) so what is being
judged is FORM and the SLOT LAYOUT - not materials the game will replace anyway.

Renders from the EXPORTED file re-imported, never from the live scene, so what is looked at
is what Unity will get (the re-import is a different reader from the exporter).

GPU-free: Cycles on CPU at a low sample count; ~10 s per 900x700 frame on this box. EEVEE and
Workbench need an OpenGL context that a headless container lacks, so they are not used.
"""

import math
import os
import re
import sys

import bpy
from mathutils import Vector, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import materials as M  # noqa: E402

# Directions from the hull to the camera, in the BLENDER frame the re-import lands in (the one
# the model was built in): nose on -Y, up +Z, right +X. The chase view is the one that matters
# most: it is where the pilot sees the hull from - behind, above, a little to the right.
VIEWS = {
    "chase": (0.30, 1.0, 0.42),       # behind-above-right (the in-game chase camera)
    "front34": (0.8, -1.0, 0.40),     # from ahead, three-quarter
    "side": (1.0, 0.0, 0.10),
    "top": (0.0, -0.001, 1.0),        # straight down, rolled so the nose points up the frame
    "rear": (0.0, 1.0, 0.12),
    "front": (0.0, -1.0, 0.12),
    "under": (0.3, 0.9, -0.55),
}


def _setup_scene(res=(900, 700), samples=24):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = False
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.get("Studio") or bpy.data.worlds.new("Studio")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.11, 0.12, 0.15, 1.0)
        bg.inputs[1].default_value = 0.6
    return sc


def _lights(center, size):
    sc = bpy.context.scene
    for name, dir_, energy, col in (
        ("Key", (0.6, -0.5, 0.9), 4.0, (1.0, 0.97, 0.92)),
        ("Fill", (-0.9, -0.3, 0.3), 1.5, (0.85, 0.9, 1.0)),
        ("Rim", (-0.2, 0.9, 0.5), 3.0, (1.0, 1.0, 1.0)),
    ):
        l = bpy.data.lights.new(name, "SUN")
        l.energy = energy
        l.color = col
        l.angle = math.radians(8)
        lo = bpy.data.objects.new(name, l)
        sc.collection.objects.link(lo)
        d = Vector(dir_).normalized()
        lo.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
        lo.location = Vector(center) + d * size * 3


def _camera(center, size, view, fov_deg=32.0, dist_factor=1.0):
    sc = bpy.context.scene
    cam = bpy.data.objects.get("SheetCam")
    if cam is None:
        cd = bpy.data.cameras.new("SheetCam")
        cam = bpy.data.objects.new("SheetCam", cd)
        sc.collection.objects.link(cam)
    cam.data.lens_unit = "FOV"
    cam.data.angle = math.radians(fov_deg)
    cam.data.clip_start = size * 0.01
    cam.data.clip_end = size * 100
    d = Vector(VIEWS[view]).normalized()
    dist = (size * 0.5) / math.tan(math.radians(fov_deg) / 2) * dist_factor
    cam.location = Vector(center) + d * dist
    q = (Vector(center) - cam.location).to_track_quat("-Z", "Y")
    if view == "top":
        q = q @ Quaternion((0, 0, 1), math.pi)  # roll so the nose (-Y) points up the frame
    cam.rotation_euler = q.to_euler()
    sc.camera = cam
    return cam


def scene_bounds(meshes):
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def import_for_render(fbx_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path, use_custom_normals=True, ignore_leaf_bones=False,
                             automatic_bone_orientation=False, use_anim=True)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    for m in meshes:
        names = [mat.name if mat else "Body" for mat in m.data.materials]
        for i, n in enumerate(names):
            m.data.materials[i] = M.studio_material(n)
    return meshes, arms


def render_frame(out_png, view, meshes, fov=32.0, res=(900, 700), samples=24, dist_factor=1.0):
    sc = _setup_scene(res, samples)
    lo, hi = scene_bounds(meshes)
    center = (lo + hi) / 2
    size = max((hi - lo).length, 1e-3)
    if not bpy.data.objects.get("Key"):
        _lights(center, size)
    _camera(center, size, view, fov, dist_factor)
    sc.render.filepath = out_png
    bpy.ops.render.render(write_still=True)
    return out_png


def contact_sheet(fbx_path, out_dir, tag="sheet", views=("chase", "front34", "side", "top"),
                  morphs=("charge", "mass", "space", "time"), poses=("pitchup", "yawright", "rollright", "speedup"),
                  res=(640, 500), samples=20, nose=-1):
    """nose=-1: the hull's nose is on Blender -Y (the usual import frame); nose=+1 flips every
    view's Y so the chase camera still sits BEHIND a hull modelled nose-forward on +Y (Serpent)."""
    global VIEWS
    base_views = dict(VIEWS)
    if nose > 0:
        VIEWS = {k: (v[0], -v[1], v[2]) for k, v in base_views.items()}
    """
    Render the sheet frames as individual PNGs and compose them into one PNG with PIL if it is
    available (falls back to the individual frames). Returns the list of files written.
    """
    os.makedirs(out_dir, exist_ok=True)
    meshes, arms = import_for_render(fbx_path)
    files = []
    # row 1: views at rest
    for v in views:
        f = os.path.join(out_dir, "%s_%s.png" % (tag, v))
        render_frame(f, v, meshes, res=res, samples=samples)
        files.append(f)
    # row 2: morph extremes (chase view)
    for m in meshes:
        if m.data.shape_keys:
            for kb in m.data.shape_keys.key_blocks:
                kb.value = 0.0
    for el in morphs:
        hit = False
        for m in meshes:
            if m.data.shape_keys:
                for kb in m.data.shape_keys.key_blocks:
                    if kb.name.lower().split(".")[-1] == el:
                        kb.value = 1.0
                        hit = True
        if hit:
            f = os.path.join(out_dir, "%s_morph_%s.png" % (tag, el))
            render_frame(f, "chase", meshes, res=res, samples=samples)
            files.append(f)
        for m in meshes:
            if m.data.shape_keys:
                for kb in m.data.shape_keys.key_blocks:
                    kb.value = 0.0
    # row 3: poses (chase view)
    if arms:
        arm = arms[0]
        for p in poses:
            # exact take name first, then the normalised pose word ('pitchup' vs 'PitchUp_SparrowAnim' vs
            # 'squirrelanim_pitchup'), never a mere substring: 'pitchup' must not pick 'Speed+PitchUp'
            norm = lambda x: re.sub(r"[^a-z0-9]", "", x.lower())
            want = norm(p)
            def _rank(a):
                tail = a.name.split("|")[-1]
                t = norm(tail)
                if tail == p or t == want:
                    return 0
                # strip a vessel suffix/prefix: 'pitchupsparrowanim' -> drop 'sparrowanim'; 'squirrelanimpitchup' -> drop 'squirrelanim'
                core = re.sub(r"(sparrowanim|squirrelanim|anim|dolphin|serpent)", "", t)
                if core == want:
                    return 1
                return None
            ranked = [(r, a) for a in bpy.data.actions for r in [_rank(a)] if r is not None]
            if not ranked:
                print("  sheet: no take matches pose %r (have %s)" % (p, [a.name.split("|")[-1] for a in bpy.data.actions]))
                continue
            ranked.sort(key=lambda x: x[0])
            act = ranked[0][1]
            print("  sheet: pose tile %r renders take %r" % (p, act.name))
            if arm.animation_data is None:
                arm.animation_data_create()
            arm.animation_data.action = act
            if hasattr(act, "slots") and len(act.slots):
                try:
                    arm.animation_data.action_slot = act.slots[0]
                except Exception:
                    pass
            frame = int(act.frame_range[1]) if act.frame_range[1] > act.frame_range[0] else int(act.frame_range[0])
            bpy.context.scene.frame_set(max(frame, 1))
            f = os.path.join(out_dir, "%s_pose_%s.png" % (tag, p))
            render_frame(f, "chase", meshes, res=res, samples=samples)
            files.append(f)
        arm.animation_data.action = None
        bpy.context.scene.frame_set(1)
    sheet = compose(files, os.path.join(out_dir, "%s.png" % tag), cols=len(views))
    VIEWS = base_views
    return [sheet] if sheet else files


def compose(files, out_png, cols=4):
    """Tile PNGs into one sheet with PIL (if installed) and label each tile."""
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        return None
    ims = [(os.path.basename(f), Image.open(f)) for f in files if os.path.exists(f)]
    if not ims:
        return None
    w, h = ims[0][1].size
    rows = (len(ims) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * w, rows * h), (20, 22, 28))
    draw = ImageDraw.Draw(sheet)
    for i, (name, im) in enumerate(ims):
        x, y = (i % cols) * w, (i // cols) * h
        sheet.paste(im, (x, y))
        label = name.rsplit(".", 1)[0].split("_", 1)[-1]
        draw.rectangle((x, y, x + 8 + 7 * len(label), y + 16), fill=(0, 0, 0))
        draw.text((x + 4, y + 2), label, fill=(240, 240, 240))
    sheet.save(out_png)
    return out_png
