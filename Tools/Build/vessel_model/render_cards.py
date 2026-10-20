#!/usr/bin/env python3
"""
render_cards - render a vessel's CARD IMAGES from its FBX model in the fleet's colour scheme, and wire
them into its class asset. When a vessel's model or the scheme changes, its card changes with it.

    python3 Tools/Build/vessel_model/render_cards.py <vessel> [...]        render, write PNGs + metas, wire SO_Class_<V>
    python3 Tools/Build/vessel_model/render_cards.py <vessel> --check      re-render and compare with the committed PNGs
    python3 Tools/Build/vessel_model/render_cards.py --list                the vessels it knows
    python3 Tools/Build/vessel_model/render_cards.py <vessel> --sheet DIR  also write a review sheet (all five images)
    python3 Tools/Build/vessel_model/render_cards.py --all [--check]       every vessel it knows
    python3 Tools/Build/vessel_model/render_cards.py <v...> --compare OUT  the scheme comparison sheet (flight + card)

Why it exists: the fleet's card icons were hand-drawn for the artist hulls (Squirrel.png and its siblings)
and rendered by one-off per-hull rasterizers for the procedural hulls (render_stoat_card_icon.py and its
siblings), which read the C# hull form. When the procedural hulls were replaced by real FBX models
(the Butterfly, 2026-10-18, then the rest of the fleet), the art had to come from the MODEL, so the card
shows the ship the player flies. One renderer reads any vessel FBX.

THE LOOK is the game's own (2026-10-18, Docs/VesselModels/COLOR.md): every slot is drawn the way VesselGraph
draws it - unlit, rim colour at the silhouette blending to face colour where the surface faces the camera - with
the colours of `vessel_colors.py` (the three shared materials' scheme), so a card is the ship the player flies.
The domain slot wears the CLASS livery (`vessel_colors.CLASS_LIVERY`: light, low-saturation Blue plates): a card
is shared by every player, and Ruby, Gold or Jade on it would claim a team (the old hand-drawn cards were gold
on every hull). A thin navy outline, a transparent background, and ONE camera for every hull (`CARD_VIEW`,
`CARD_NOSE_ANGLE`): three-quarters from behind and above, the nose to the upper right. The hangar preview sets the model on the hangar's dark hex field with a cool silver glow.

WHAT IT WRITES (all sprites; each PNG is overwritten IN PLACE when it exists, so its guid and every reference
survive; a new PNG gets a deterministic guid and a .meta cloned from Squirrel.png's). A vessel whose class asset
already names other files (the artist hulls: Dolphin_Square.png, Manta_Square_silhouette.png, Hangar/Urchin.png)
lists them in its `files`, and those are rewritten in place at their own sizes; the default names are:
    Assets/_Graphics/CardImages/<V>.png              256  IconActive, CardSilohoutteActive
    Assets/_Graphics/CardImages/<V>_Inactive.png     256  IconInactive (faded and partly desaturated: the
                                                          formula the per-hull renderers used)
    Assets/_Graphics/CardImages/<V>_silhouette.png   256  CardSilohoutteInactive (flat 64 grey, hard alpha)
    Assets/_Graphics/CardImages/<V>_large.png        512  SquadImage
    Assets/_Graphics/Design Assests/Hangar/<V>_Hangar_Preview.png  960x540  PreviewImage (the model on the
                                                          hangar's dark hex field, with a soft silver glow)
and points the six fields of `Assets/_SO_Assets/Classes/SO_Class_<V>.asset` at them. Every playable hull is
listed in VESSELS: the hand-drawn cards (gold plates, lime engines) broke the colour language and were
replaced by renders of the models on 2026-10-18.

Rendering: Blender's Cycles on the CPU (EEVEE and Workbench need an OpenGL context a headless container
lacks), with EMISSION-only materials that reproduce VesselGraph's fresnel (|N.V| mixes rim to face), so lights
play no part and the colours are exact; the outline is an inverted-hull shell. The camera is
orthographic and fitted to the posed mesh. Seeded and deterministic: `--check` allows 2/255 mean
difference per image for float noise between machines.
"""
import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
CARDS = os.path.join(REPO, "Assets", "_Graphics", "CardImages")
HANGAR = os.path.join(REPO, "Assets", "_Graphics", "Design Assests", "Hangar")
CLASSES = os.path.join(REPO, "Assets", "_SO_Assets", "Classes")
DONOR_META = os.path.join(CARDS, "Squirrel.png.meta")
MODELS = "Assets/_Models/Vessel Models/"

# ONE camera for every card (Garrett 2026-10-18: every hull "flying up and to the right", consistently): the
# camera direction in the model's (right, nose, up) frame - behind, above and to the right - and the nose's
# direction on the card, degrees from +x. A vessel never overrides it; fix its `nose`/`up` instead if it faces
# another way.
CARD_VIEW = (0.85, -0.35, 0.6)
CARD_NOSE_ANGLE = 30.0
# vessel -> how to read its model. `roles` maps slot NAME (or its name before the first '.') -> role, as the
# vessel's .meta remaps it to the shared materials; `nose`/`up` are the model's axes in Blender's frame after
# import (default nose -Y, up +Z); `files` names the PNGs its class asset already uses (repo-relative under
# Assets/_Graphics/; a None skips that image) and `hangar_size` the preview's size.
FLEET_SLOTS = {"Domain": "domain", "Body": "body", "Window": "window", "Engine": "engine"}
# the artist files' names, as their .meta remap them (accent = GreenAccent = domain, BASE = BlueBase = body)
ARTIST_SLOTS = {"accent": "domain", "BASE": "body", "windsheild": "window", "N": "engine"}
CI, HG = "CardImages/", "Design Assests/Hangar/"


def _files(icon, inactive, silhouette, large, hangar):
    return {"icon": CI + icon, "inactive": CI + inactive, "silhouette": CI + silhouette,
            "large": CI + large if large else None, "hangar": HG + hangar}


VESSELS = {
    "Butterfly": {"fbx": MODELS + "Butterfly.fbx", "roles": FLEET_SLOTS},
    "Angler": {"fbx": MODELS + "Angler.fbx", "roles": FLEET_SLOTS, "class_owner": "Tools/Build/author_angler_assets.py"},
    "Krill": {"fbx": MODELS + "Krill.fbx", "roles": FLEET_SLOTS, "class_owner": "Tools/Build/author_krill_assets.py"},
    "BrittleStar": {"fbx": MODELS + "BrittleStar.fbx", "roles": FLEET_SLOTS, "class_owner": "Tools/Build/author_brittlestar_assets.py"},
    "Scarab": {"fbx": MODELS + "Scarab.fbx", "roles": FLEET_SLOTS},
    "Shrike": {"fbx": MODELS + "Shrike.fbx", "roles": FLEET_SLOTS, "class_owner": "Tools/Build/author_shrike_assets.py"},
    "Stoat": {"fbx": MODELS + "Stoat.fbx", "roles": FLEET_SLOTS, "class_owner": "Tools/Build/author_stoat_assets.py"},
    "Thresher": {"fbx": MODELS + "Thresher.fbx", "roles": FLEET_SLOTS},
    # the artist hulls: rendered in place over the files their class assets (and other UI) already reference
    "Squirrel": {"fbx": MODELS + "SquirrelVessel_CosmicShoresTest1.fbx", "roles": FLEET_SLOTS},
    "Dolphin": {"fbx": MODELS + "dolphin_shapekey_with_animations.fbx", "roles": ARTIST_SLOTS,
                "files": _files("Dolphin_Square.png", "Dolphin_Inactive.png", "Dolphin_Square_silhouette.png",
                                "Dolphin_large.png", "Dolphin_Hangar_Preview.png")},
    "Sparrow": {"fbx": MODELS + "SparrowModel1.fbx", "roles": FLEET_SLOTS},
    "Serpent": {"fbx": MODELS + "SerpentExport.fbx", "roles": FLEET_SLOTS, "nose": (0, 1, 0)},
    "Manta": {"fbx": MODELS + "Manta_shapekey_rigged.fbx", "roles": ARTIST_SLOTS,
              "files": _files("Manta.png", "Manta_Inactive.png", "Manta_Square_silhouette.png",
                              "Manta_large.png", "Manta_Hangar_Preview.png")},
    "Rhino": {"fbx": MODELS + "rhino_shapekey_with_animations.fbx", "roles": ARTIST_SLOTS,
              "files": _files("Rhino.png", "Rhino_Inactive.png", "Rhino_Square_silhouette.png",
                              "Rhino_large.png", "Rhino_Hangar_Preview.png")},
    "Urchin": {"fbx": MODELS + "urchan_shapekey_with_animations.fbx", "roles": ARTIST_SLOTS, "hangar_size": (1620, 920),
               "files": _files("Urchin_Square.png", "Urchin_Inactive.png", "Urchin_Square_silhouette.png",
                               None, "Urchin.png")},
}

OUT_SIZE = 256
RENDER = 1024
# the card's domain livery and its outline (linear): see Docs/VesselModels/COLOR.md
CARD_DOMAIN = "Class"
HANGAR_GLOW = (150, 175, 230)         # the preview's glow: cool silver, the neutral family (was gold)
OUTLINE_LINEAR = (0.0012, 0.0014, 0.0035)
SKY_SRGB = (0.0, 0.0, 0.102)          # OriginalColorSetSO.EnvironmentColors.SkyColor: what a ship is seen against


_BPY = r'''
import bpy, sys, json, math
from mathutils import Vector, Quaternion, Matrix
args = json.loads(sys.argv[-1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=args["fbx"], use_custom_normals=True, ignore_leaf_bones=False,
                         automatic_bone_orientation=False, use_anim=False)
for a in [o for o in bpy.data.objects if o.type == "ARMATURE"]:
    a.data.pose_position = "REST"
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
for m in meshes:
    if m.data.shape_keys:
        for k in m.data.shape_keys.key_blocks:
            k.value = 0.0
bpy.context.view_layer.update()

def vessel_material(name, rim, face):
    """VesselGraph, emulated: unlit, colour = lerp(rim, face, facing), facing = |N . V| (1 facing the camera, 0 at
    the silhouette). Colours arrive LINEAR with the multiplier applied (vessel_colors.linear_role)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dot = nt.nodes.new("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"
    nt.links.new(geo.outputs["Normal"], dot.inputs[0]); nt.links.new(geo.outputs["Incoming"], dot.inputs[1])
    ab = nt.nodes.new("ShaderNodeMath"); ab.operation = "ABSOLUTE"
    nt.links.new(dot.outputs["Value"], ab.inputs[0])
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"
    nt.links.new(ab.outputs[0], mix.inputs["Factor"])
    mix.inputs[6].default_value = tuple(rim) + (1,)
    mix.inputs[7].default_value = tuple(face) + (1,)
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(mix.outputs[2], em.inputs[0])
    nt.links.new(em.outputs[0], out.inputs[0])
    return mat

def outline_material(color):
    mat = bpy.data.materials.new("card_outline")
    mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = tuple(color) + (1,)
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mx = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(geo.outputs["Backfacing"], mx.inputs[0])
    nt.links.new(em.outputs[0], mx.inputs[1]); nt.links.new(tr.outputs[0], mx.inputs[2])
    nt.links.new(mx.outputs[0], out.inputs[0])
    return mat

cols = args["colors"]; roles = args["roles"]
mats = {r: vessel_material("card_" + r, cols[r]["rim"], cols[r]["face"]) for r in ("domain", "body", "window", "engine")}
mats["outline"] = outline_material(args["outline_color"])
pts = []
dg = bpy.context.evaluated_depsgraph_get()
for m in meshes:
    names = [s.name if s else "" for s in m.data.materials]
    for i, n in enumerate(names):
        base = n.split(".")[0]
        m.data.materials[i] = mats[roles.get(n, roles.get(base, "body"))]
    ev = m.evaluated_get(dg)
    me = ev.to_mesh()
    pts += [ev.matrix_world @ v.co for v in me.vertices]
    ev.to_mesh_clear()
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
diag = (hi - lo).length
for m in (meshes if args["outline"] > 0 else []):
    m.data.materials.append(mats["outline"])
    sol = m.modifiers.new("card_outline", "SOLIDIFY")
    sol.thickness = diag * args["outline"]
    sol.offset = 1.0
    sol.use_flip_normals = True
    sol.use_rim = False
    sol.material_offset = len(m.data.materials) - 1
    sol.use_even_offset = False

F = Vector(args["nose"]).normalized(); U = Vector(args["up"]).normalized(); R = F.cross(U)
vx, vy, vz = args["view"]
d = (R * vx + F * vy + U * vz).normalized()
center = (lo + hi) / 2
sc = bpy.context.scene
cd = bpy.data.cameras.new("CardCam"); cd.type = "ORTHO"
cam = bpy.data.objects.new("CardCam", cd); sc.collection.objects.link(cam); sc.camera = cam
q = (-d).to_track_quat("-Z", "Y")
cx = q @ Vector((1, 0, 0)); cy = q @ Vector((0, 1, 0))
theta = math.degrees(math.atan2(F.dot(cy), F.dot(cx)))
q = q @ Quaternion((0, 0, 1), math.radians(theta - args["nose_angle"]))
cx = q @ Vector((1, 0, 0)); cy = q @ Vector((0, 1, 0))
xs = [(p - center).dot(cx) for p in pts]; ys = [(p - center).dot(cy) for p in pts]
mid = center + cx * (min(xs) + max(xs)) / 2 + cy * (min(ys) + max(ys)) / 2
w, h = max(xs) - min(xs), max(ys) - min(ys)
cam.rotation_euler = q.to_euler()
cam.location = mid + d * diag * 3
cd.clip_start = diag * 0.1; cd.clip_end = diag * 10
sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = args["samples"]
sc.cycles.seed = 7; sc.cycles.use_denoising = False
sc.cycles.transparent_max_bounces = 16; sc.cycles.max_bounces = 0
sc.render.film_transparent = args.get("background") is None
sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
sc.render.resolution_percentage = 100
world = bpy.data.worlds.new("Card"); sc.world = world; world.use_nodes = True
bgn = world.node_tree.nodes["Background"]
if args.get("background") is None:
    bgn.inputs[1].default_value = 0.0
else:
    bgn.inputs[0].default_value = tuple(args["background"]) + (1,); bgn.inputs[1].default_value = 1.0
for tag, (rx, ry, fill) in args["shots"].items():
    sc.render.resolution_x, sc.render.resolution_y = rx, ry
    aspect = rx / ry
    cd.ortho_scale = max(w, h * aspect) / fill
    sc.render.filepath = args["out"] + "/" + tag + ".png"
    bpy.ops.render.render(write_still=True)
print("CARD_OK " + json.dumps({"w": w, "h": h, "diag": diag}))
'''


def _guid(name):
    return hashlib.md5(("cosmic-shore-card:" + name).encode()).hexdigest()


def card_guid(rel_path):
    """The guid a NEW card PNG gets (`rel_path` from the repo root). A generator that writes a class asset
    computes the same value, so the two agree without either reading the other's output."""
    return _guid(rel_path)


def _meta_for(png_path, guid):
    donor = open(DONOR_META, encoding="utf-8").read()
    out = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + guid, donor, count=1, flags=re.M)
    assert out.count(guid) == 1
    return out


def colors_for(scheme, domain, boost=False):
    """The linear rim/face colours the renderer paints each role with: the scheme's body, window and engine (the
    engine boosted when asked) and the named domain's livery from the palette."""
    sys.path.insert(0, HERE)
    import vessel_colors as VC
    out = {}
    for role in ("body", "window", "engine"):
        rim, face = VC.linear_role(scheme[role], boost=boost and role == "engine")
        out[role] = {"rim": rim, "face": face}
    rim, face = VC.linear_role(scheme["domains"][domain])
    out["domain"] = {"rim": rim, "face": face}
    return out


def render_view(cfg, colors, out_dir, shots, background=None, outline=0.006, view=None, nose_angle=None, samples=16):
    """Render the vessel's model with VesselGraph's look: `shots` = {tag: (w, h, fill)}; returns {tag: png path}."""
    args = {
        "fbx": os.path.join(REPO, cfg["fbx"]), "roles": cfg["roles"], "colors": colors,
        "nose": cfg.get("nose", (0, -1, 0)), "up": cfg.get("up", (0, 0, 1)),
        "view": view or CARD_VIEW, "nose_angle": CARD_NOSE_ANGLE if nose_angle is None else nose_angle,
        "outline": outline, "outline_color": OUTLINE_LINEAR, "background": background,
        "samples": samples, "out": out_dir, "shots": shots,
    }
    p = subprocess.run([sys.executable, "-c", _BPY, json.dumps(args)], capture_output=True, text=True, timeout=1800)
    if "CARD_OK" not in p.stdout:
        raise RuntimeError("render failed for %s:\n%s" % (cfg["fbx"], (p.stdout + p.stderr)[-2500:]))
    return {tag: os.path.join(out_dir, tag + ".png") for tag in shots}


def render_raw(name, cfg, out_dir, samples=16):
    import numpy as np  # noqa: F401  (fail early if numpy is missing)
    sys.path.insert(0, HERE)
    import vessel_colors as VC
    colors = colors_for(VC.scheme(), CARD_DOMAIN)
    hw, hh = cfg.get("hangar_size", (960, 540))
    files = render_view(cfg, colors, out_dir, {"card": (RENDER, RENDER, 0.92), "hangar": (hw * 2, hh * 2, 0.62)},
                        outline=cfg.get("outline", 0.006), samples=samples)
    return files["card"], files["hangar"]


def _inactive(a):
    import numpy as np
    out = a.copy()
    grey = out[..., :3] @ np.array([0.299, 0.587, 0.114])
    out[..., :3] = 0.5 * out[..., :3] + 0.5 * grey[..., None]
    out[..., :3] = out[..., :3] * 0.7 + 0.3
    out[..., 3] *= 0.85
    return out


def _silhouette(a):
    import numpy as np
    out = np.zeros_like(a)
    out[..., :3] = 64 / 255.0
    out[..., 3] = (a[..., 3] > 0.5).astype(float)
    return out


def _hangar_background(w, h, seed):
    """The hangar previews' field: deep navy with a faint hex lattice and a scatter of stars."""
    import numpy as np
    from PIL import Image, ImageDraw
    y = np.linspace(0, 1, h)[:, None]
    x = np.linspace(0, 1, w)[None, :]
    r = np.sqrt((x - 0.5) ** 2 * 1.4 + (y - 0.5) ** 2)
    base = np.stack([0.02 + 0.03 * (1 - r), 0.03 + 0.05 * (1 - r), 0.10 + 0.14 * (1 - r)], -1).clip(0, 1)
    img = Image.fromarray((base * 255).astype(np.uint8), "RGB").convert("RGBA")
    d = ImageDraw.Draw(img, "RGBA")
    s = 54.0
    import math
    for row in range(-1, int(h / (s * 1.5)) + 2):
        for col in range(-1, int(w / (s * math.sqrt(3))) + 2):
            cx = col * s * math.sqrt(3) + (row % 2) * s * math.sqrt(3) / 2
            cy = row * s * 1.5
            pts = [(cx + s * math.cos(math.radians(60 * k + 30)), cy + s * math.sin(math.radians(60 * k + 30))) for k in range(7)]
            d.line(pts, fill=(40, 70, 150, 70), width=2)
    rng = np.random.RandomState(seed)
    for _ in range(260):
        px, py = rng.randint(0, w), rng.randint(0, h)
        b = int(rng.uniform(90, 255))
        rad = 1 if rng.rand() < 0.85 else 2
        d.ellipse([px - rad, py - rad, px + rad, py + rad], fill=(b, b, min(255, b + 20), 255))
    return img


def outputs(name):
    """kind -> absolute output path for the vessel (None: not written); kinds icon, inactive, silhouette, large,
    hangar."""
    files = VESSELS[name].get("files")
    if files:
        return {k: os.path.join(REPO, "Assets", "_Graphics", v) if v else None for k, v in files.items()}
    return {"icon": os.path.join(CARDS, name + ".png"), "inactive": os.path.join(CARDS, name + "_Inactive.png"),
            "silhouette": os.path.join(CARDS, name + "_silhouette.png"),
            "large": os.path.join(CARDS, name + "_large.png"),
            "hangar": os.path.join(HANGAR, name + "_Hangar_Preview.png")}


def compose(name, card_png, hangar_png):
    """The five images, as float RGBA arrays (0..1) keyed by output path."""
    import numpy as np
    from PIL import Image, ImageFilter
    hw, hh = VESSELS[name].get("hangar_size", (960, 540))
    card = Image.open(card_png).convert("RGBA")
    big = np.asarray(card.resize((512, 512), Image.LANCZOS)).astype(float) / 255
    small = np.asarray(card.resize((OUT_SIZE, OUT_SIZE), Image.LANCZOS)).astype(float) / 255
    hang = Image.open(hangar_png).convert("RGBA").resize((hw, hh), Image.LANCZOS)
    bg = _hangar_background(hw, hh, int(_guid(name)[:6], 16))
    alpha = hang.split()[3]
    glow = Image.new("RGBA", (hw, hh), HANGAR_GLOW + (0,))
    glow.putalpha(alpha.filter(ImageFilter.GaussianBlur(18 * hw / 960)).point(lambda v: int(v * 0.45)))
    bg.alpha_composite(glow)
    bg.alpha_composite(hang)
    prev = np.asarray(bg).astype(float) / 255
    images = {"icon": small, "inactive": _inactive(small), "silhouette": _silhouette(small), "large": big,
              "hangar": prev}
    return {path: images[k] for k, path in outputs(name).items() if path}


def _to_png(a, path):
    import numpy as np
    from PIL import Image
    Image.fromarray(np.clip(np.round(a * 255), 0, 255).astype(np.uint8), "RGBA").save(path, optimize=False, compress_level=9)


def _guid_of(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        m = re.search(r"^guid: ([0-9a-f]{32})$", open(meta, encoding="utf-8").read(), re.M)
        if m:
            return m.group(1)
    return None


FIELDS = (("IconActive", "icon"), ("IconInactive", "inactive"), ("CardSilohoutteActive", "icon"),
          ("CardSilohoutteInactive", "silhouette"), ("SquadImage", "large"), ("PreviewImage", "hangar"))


def wire_class(name):
    path = os.path.join(CLASSES, "SO_Class_%s.asset" % name)
    s = open(path, encoding="utf-8").read()
    outs = outputs(name)
    for field, kind in FIELDS:
        if not outs[kind]:
            continue
        g = _guid_of(outs[kind])
        ref = "{fileID: 21300000, guid: %s, type: 3}" % g
        # `[^}]*` spans the line break Unity inserts in a long reference (`guid: ...,\n    type: 3}`)
        new, n = re.subn(r"^(  %s: )\{[^}]*\}" % field, lambda m: m.group(1) + ref, s, count=1, flags=re.M)
        assert n == 1, "%s: field %s not found" % (path, field)
        s = new
    open(path, "w", encoding="utf-8").write(s)
    return path


def run(name, check=False, sheet=None, samples=16):
    import numpy as np
    from PIL import Image
    cfg = VESSELS[name]
    with tempfile.TemporaryDirectory() as td:
        card_png, hangar_png = render_raw(name, cfg, td, samples)
        images = compose(name, card_png, hangar_png)
    fails = []
    for path, arr in images.items():
        rel = os.path.relpath(path, REPO)
        if check:
            if not os.path.exists(path):
                fails.append("%s: missing" % rel)
                continue
            have = np.asarray(Image.open(path).convert("RGBA")).astype(float) / 255
            if have.shape != arr.shape:
                fails.append("%s: size %s, renders %s" % (rel, have.shape, arr.shape))
                continue
            diff = float(np.abs(have - arr).mean() * 255)
            if diff > 2.0:
                fails.append("%s: mean difference %.2f/255 from a fresh render (re-run without --check)" % (rel, diff))
            continue
        _to_png(arr, path)
        if not os.path.exists(path + ".meta"):
            with open(path + ".meta", "w", encoding="utf-8") as f:
                f.write(_meta_for(path, _guid(rel)))
        print("wrote %s" % rel)
    if sheet:
        os.makedirs(sheet, exist_ok=True)
        tiles = [Image.open(p).convert("RGBA") if not check else Image.fromarray((a * 255).astype(np.uint8), "RGBA")
                 for p, a in images.items()]
        W = 256 * 4 + 480
        out = Image.new("RGBA", (W, 300), (70, 70, 80, 255))
        x = 0
        for t in tiles[:-1]:
            t = t.resize((256, 256))
            out.alpha_composite(t, (x, 22)); x += 256
        out.alpha_composite(tiles[-1].resize((480, 270)), (x, 15))
        out.convert("RGB").save(os.path.join(sheet, "%s_cards.jpg" % name.lower()), quality=90)
    if not check:
        print("wired %s" % os.path.relpath(wire_class(name), REPO))
        owner = cfg.get("class_owner")
        if owner:
            # a generator writes this class asset too: it must carry the same guids (card_guid below), or its
            # next write reverts the wiring
            print("class asset also written by %s: it must name these guids (see card_guid)" % owner)
            for field, kind in FIELDS:
                print("  %-24s %s" % (field, _guid_of(outputs(name)[kind])))
    return fails


def compare(vessels, out_png, domains=("Jade", "Ruby", "Gold"), samples=16, boost=False, candidates=None):
    """The comparison sheet the scheme is chosen on: for each vessel and domain, the CURRENT assets and the
    authored SCHEME, as the chase camera sees the ship against the arena sky and as a card."""
    from PIL import Image, ImageDraw
    sys.path.insert(0, HERE)
    import vessel_colors as VC
    sky = tuple(VC.srgb_to_linear(c) for c in SKY_SRGB)
    rows = [("now", VC.current())] if VC.check() else []   # the assets, when they differ from the scheme
    rows.append(("scheme", VC.scheme()))
    if candidates:   # {label: {role: {rim, face, mult}}}: alternatives to SCHEME, laid beside it
        with open(candidates) as f:
            for label, roles in json.load(f).items():
                sch = VC.scheme()
                sch.update(roles)
                rows.append((label, sch))
    T = 300
    tiles = []
    with tempfile.TemporaryDirectory() as td:
        for v in vessels:
            cfg = VESSELS[v]
            for d in domains:
                for label, sch in rows:
                    col = colors_for(sch, d, boost=boost)
                    sub = os.path.join(td, "%s_%s_%s" % (v, d, label)); os.makedirs(sub)
                    fl = render_view(cfg, col, sub, {"flight": (T * 2, T * 2, 0.80)}, background=sky, outline=0.0,
                                     view=(0.25, -1.0, 0.55), nose_angle=90.0, samples=samples)["flight"]
                    cd = render_view(cfg, col, sub, {"card": (T * 2, T * 2, 0.92)}, samples=samples)["card"]
                    tiles.append((v, d, label, fl, cd))
        cols = len(domains) * len(rows)
        W, H = cols * T, len(vessels) * (2 * T + 20)
        sheet = Image.new("RGB", (W, H), (60, 62, 72))
        dr = ImageDraw.Draw(sheet)
        for i, (v, d, label, fl, cd) in enumerate(tiles):
            r, c = divmod(i, cols)
            x, y = c * T, r * (2 * T + 20)
            sheet.paste(Image.open(fl).convert("RGB").resize((T, T), Image.LANCZOS), (x, y + 20))
            card = Image.open(cd).convert("RGBA").resize((T, T), Image.LANCZOS)
            bg = Image.new("RGBA", (T, T), (22, 26, 52, 255)); bg.alpha_composite(card)
            sheet.paste(bg.convert("RGB"), (x, y + 20 + T))
            dr.text((x + 6, y + 4), "%s  %s  %s" % (v, d, label), fill=(235, 235, 235))
        sheet.save(out_png, quality=90)
    return out_png


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("vessels", nargs="*")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--sheet")
    ap.add_argument("--samples", type=int, default=16)
    ap.add_argument("--compare", help="write the scheme comparison sheet (now vs scheme, flight and card) here")
    ap.add_argument("--boost", action="store_true", help="--compare with the engines at FlareEngine's boost")
    ap.add_argument("--candidates", help="--compare also these schemes (JSON {label: {role: {rim, face, mult}}})")
    ap.add_argument("--domains", default="Jade,Ruby,Gold")
    ap.add_argument("--all", action="store_true", help="every vessel in VESSELS")
    a = ap.parse_args()
    if a.all:
        a.vessels = list(VESSELS)
    if a.compare:
        print(compare(a.vessels or ["Thresher", "Butterfly"], a.compare, domains=tuple(a.domains.split(",")),
                      samples=a.samples, boost=a.boost, candidates=a.candidates))
        return 0
    if a.list or not a.vessels:
        for n, c in VESSELS.items():
            print("%-12s %s" % (n, c["fbx"]))
        return 0
    rc = 0
    for v in a.vessels:
        name = next((n for n in VESSELS if n.lower() == v.lower()), None)
        if not name:
            print("unknown vessel %s (known: %s)" % (v, ", ".join(VESSELS)))
            return 2
        fails = run(name, check=a.check, sheet=a.sheet, samples=a.samples)
        for f in fails:
            print("DRIFT: " + f)
        if a.check and not fails:
            print("OK: %s cards match a fresh render of its model" % name)
        rc = rc or (1 if fails else 0)
    return rc


if __name__ == "__main__":
    sys.exit(main())
