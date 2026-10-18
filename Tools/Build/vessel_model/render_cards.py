#!/usr/bin/env python3
"""
render_cards - render a vessel's CARD IMAGES from its FBX model in the house card look, and wire
them into its class asset. When a vessel's model changes, its card changes with it.

    python3 Tools/Build/vessel_model/render_cards.py <vessel> [...]        render, write PNGs + metas, wire SO_Class_<V>
    python3 Tools/Build/vessel_model/render_cards.py <vessel> --check      re-render and compare with the committed PNGs
    python3 Tools/Build/vessel_model/render_cards.py --list                the vessels it knows
    python3 Tools/Build/vessel_model/render_cards.py <vessel> --sheet DIR  also write a review sheet (all five images)

Why it exists: the fleet's card icons were hand-drawn for the artist hulls (Squirrel.png and its siblings)
and rendered by one-off per-hull rasterizers for the procedural hulls (render_stoat_card_icon.py and its
siblings), which read the C# hull form. When the procedural hulls were replaced by real FBX models
(the Butterfly, 2026-10-18, then the rest of the fleet), the art had to come from the MODEL, so the card
shows the ship the player flies. One renderer reads any vessel FBX.

THE HOUSE LOOK, measured from the hand-drawn cards (`Assets/_Graphics/CardImages/Squirrel.png`, `Rhino.png`,
`Manta.png`; see `PALETTE`): flat cel shading in three bands, the domain plates in gold (255,175,20) with an
orange shade, the body near-black navy (0,0,12), the glass indigo (33,33,139), the engines green (0,139,4) to
lime, a thin navy outline, a transparent background, three-quarters from above with the nose to the upper
right. The domain colour is gold on every card whatever the pilot's domain, because the card is the class,
not the player.

WHAT IT WRITES (all sprites; each PNG is overwritten IN PLACE when it exists, so its guid and every reference
survive; a new PNG gets a deterministic guid and a .meta cloned from Squirrel.png's):
    Assets/_Graphics/CardImages/<V>.png              256  IconActive, CardSilohoutteActive
    Assets/_Graphics/CardImages/<V>_Inactive.png     256  IconInactive (faded and partly desaturated: the
                                                          formula the per-hull renderers used)
    Assets/_Graphics/CardImages/<V>_silhouette.png   256  CardSilohoutteInactive (flat 64 grey, hard alpha)
    Assets/_Graphics/CardImages/<V>_large.png        512  SquadImage
    Assets/_Graphics/Design Assests/Hangar/<V>_Hangar_Preview.png  960x540  PreviewImage (the model on the
                                                          hangar's dark hex field, with a soft gold glow)
and points the six fields of `Assets/_SO_Assets/Classes/SO_Class_<V>.asset` at them. An artist-drawn card is
never overwritten: a vessel is listed in VESSELS only when its card should follow its model.

Rendering: Blender's Cycles on the CPU (EEVEE and Workbench need an OpenGL context a headless container
lacks), with EMISSION-only toon materials, so lights play no part and the colours are exact. Bands come
from N.L against a fixed key direction; the outline is an inverted-hull shell. The camera is
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

# sRGB colours measured from the hand-drawn cards: (shade, lit, highlight) per role
PALETTE = {
    "domain": ((214, 112, 16), (255, 175, 20), (255, 214, 60)),
    "body": ((0, 0, 12), (14, 16, 38), (44, 48, 92)),
    "window": ((12, 12, 88), (33, 33, 139), (104, 110, 206)),
    "engine": ((0, 120, 4), (16, 190, 20), (120, 255, 90)),
    "outline": ((0, 0, 12),) * 3,
}

# vessel -> how to read its model. `roles` maps slot NAME -> role; `view` is the camera direction in the
# model's (right, nose, up) frame; `nose_angle` is where the nose points on the card, degrees from +x.
FLEET_SLOTS = {"Domain": "domain", "Body": "body", "Window": "window", "Engine": "engine"}
VESSELS = {
    "Butterfly": {"fbx": MODELS + "Butterfly.fbx", "roles": FLEET_SLOTS,
                  "view": (0.55, -0.35, 0.95), "nose_angle": 62.0},
    "Thresher": {"fbx": MODELS + "Thresher.fbx", "roles": FLEET_SLOTS,
                 "view": (0.85, -0.25, 0.55), "nose_angle": 28.0},
}
# placeholders' renderers that wrote the same PNGs before the models existed (their card now follows the model)
SUPERSEDES = {}

OUT_SIZE = 256
RENDER = 1024


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

def lin(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

key = Vector(args["key"]).normalized()
def toon(name, bands):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    if name == "card_outline":
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = tuple(lin(c) for c in bands[0]) + (1,)
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        mix = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(geo.outputs["Backfacing"], mix.inputs[0])
        nt.links.new(em.outputs[0], mix.inputs[1]); nt.links.new(tr.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs[0])
        return mat
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dot = nt.nodes.new("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"
    dot.inputs[1].default_value = tuple(key)
    nt.links.new(geo.outputs["Normal"], dot.inputs[0])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    els = ramp.color_ramp.elements
    els[0].position = 0.0; els[0].color = tuple(lin(c) for c in bands[0]) + (1,)
    els[1].position = 0.52; els[1].color = tuple(lin(c) for c in bands[1]) + (1,)
    e3 = els.new(0.90); e3.color = tuple(lin(c) for c in bands[2]) + (1,)
    remap = nt.nodes.new("ShaderNodeMapRange")
    remap.inputs[1].default_value = -1.0; remap.inputs[2].default_value = 1.0
    nt.links.new(dot.outputs["Value"], remap.inputs[0])
    nt.links.new(remap.outputs[0], ramp.inputs[0])
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(ramp.outputs[0], em.inputs[0])
    nt.links.new(em.outputs[0], out.inputs[0])
    return mat

pal = args["palette"]; roles = args["roles"]
mats = {r: toon("card_" + r, pal[r]) for r in ("domain", "body", "window", "engine", "outline")}
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
for m in meshes:
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
sc.render.film_transparent = True
sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
sc.render.resolution_percentage = 100
world = bpy.data.worlds.new("Card"); sc.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[1].default_value = 0.0
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


def _meta_for(png_path, guid):
    donor = open(DONOR_META, encoding="utf-8").read()
    out = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + guid, donor, count=1, flags=re.M)
    assert out.count(guid) == 1
    return out


def render_raw(name, cfg, out_dir, samples=16):
    import numpy as np  # noqa: F401  (fail early if numpy is missing)
    args = {
        "fbx": os.path.join(REPO, cfg["fbx"]), "roles": cfg["roles"], "palette": PALETTE,
        "nose": cfg.get("nose", (0, -1, 0)), "up": cfg.get("up", (0, 0, 1)),
        "view": cfg["view"], "nose_angle": cfg.get("nose_angle", 35.0),
        "key": cfg.get("key", (0.35, -0.45, 0.82)), "outline": cfg.get("outline", 0.006),
        "samples": samples, "out": out_dir,
        "shots": {"card": (RENDER, RENDER, 0.92), "hangar": (1920, 1080, 0.62)},
    }
    p = subprocess.run([sys.executable, "-c", _BPY, json.dumps(args)], capture_output=True, text=True, timeout=1800)
    if "CARD_OK" not in p.stdout:
        raise RuntimeError("card render failed for %s:\n%s" % (name, (p.stdout + p.stderr)[-2500:]))
    return os.path.join(out_dir, "card.png"), os.path.join(out_dir, "hangar.png")


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


def compose(name, card_png, hangar_png):
    """The five images, as float RGBA arrays (0..1) keyed by output path."""
    import numpy as np
    from PIL import Image, ImageFilter
    card = Image.open(card_png).convert("RGBA")
    big = np.asarray(card.resize((512, 512), Image.LANCZOS)).astype(float) / 255
    small = np.asarray(card.resize((OUT_SIZE, OUT_SIZE), Image.LANCZOS)).astype(float) / 255
    hang = Image.open(hangar_png).convert("RGBA").resize((960, 540), Image.LANCZOS)
    bg = _hangar_background(960, 540, int(_guid(name)[:6], 16))
    alpha = hang.split()[3]
    glow = Image.new("RGBA", (960, 540), (255, 175, 20, 0))
    glow.putalpha(alpha.filter(ImageFilter.GaussianBlur(18)).point(lambda v: int(v * 0.55)))
    bg.alpha_composite(glow)
    bg.alpha_composite(hang)
    prev = np.asarray(bg).astype(float) / 255
    return {
        os.path.join(CARDS, name + ".png"): small,
        os.path.join(CARDS, name + "_Inactive.png"): _inactive(small),
        os.path.join(CARDS, name + "_silhouette.png"): _silhouette(small),
        os.path.join(CARDS, name + "_large.png"): big,
        os.path.join(HANGAR, name + "_Hangar_Preview.png"): prev,
    }


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


FIELDS = (("IconActive", ""), ("IconInactive", "_Inactive"), ("CardSilohoutteActive", ""),
          ("CardSilohoutteInactive", "_silhouette"), ("SquadImage", "_large"), ("PreviewImage", "@hangar"))


def wire_class(name):
    path = os.path.join(CLASSES, "SO_Class_%s.asset" % name)
    s = open(path, encoding="utf-8").read()
    for field, suffix in FIELDS:
        png = os.path.join(HANGAR, name + "_Hangar_Preview.png") if suffix == "@hangar" else os.path.join(CARDS, name + suffix + ".png")
        g = _guid_of(png)
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
        for t in tiles[:4]:
            t = t.resize((256, 256))
            out.alpha_composite(t, (x, 22)); x += 256
        out.alpha_composite(tiles[4].resize((480, 270)), (x, 15))
        out.convert("RGB").save(os.path.join(sheet, "%s_cards.jpg" % name.lower()), quality=90)
    if not check:
        owner = cfg.get("class_owner")
        if owner:
            # a generator writes this class asset: it must carry the guids, or its --check reverts the wiring
            print("class asset owned by %s; put these in it:" % owner)
            for field, suffix in FIELDS:
                png = os.path.join(HANGAR, name + "_Hangar_Preview.png") if suffix == "@hangar" else os.path.join(CARDS, name + suffix + ".png")
                print("  %-24s %s" % (field, _guid_of(png)))
        else:
            print("wired %s" % os.path.relpath(wire_class(name), REPO))
    return fails


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("vessels", nargs="*")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--sheet")
    ap.add_argument("--samples", type=int, default=16)
    a = ap.parse_args()
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
