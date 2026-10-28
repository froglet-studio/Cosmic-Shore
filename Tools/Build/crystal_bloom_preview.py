#!/usr/bin/env python3
"""crystal_bloom_preview - every free crystal as the game draws it, through the gameplay BLOOM, beside its 2D mote.

A READER: it renders and measures, and writes nothing in the repo.

Garrett, 2026-10-27: "consider the goal of these is to be distinguishable but all read within the not green and not
yellow, not white, part of the hues and lightness family that defines lime, but these will be subject to the post
processing bloom so you will need to analyze the distinction after all the post processing bloom has been applied."

WHAT IS SIMULATED, read from the shipped assets:
  geometry   each crystal's real mesh (Charge, Space and Time from their FBX, Mass from the shipped prism bake, the
             omni's body), faceted, opaque, z-buffered, Cull Off where the shader culls nothing (back faces show
             through the gaps between plates and take the bright end, as in the game)
  colour     each shader's own formula on the colours the game hands it, gamma -> linear as Unity does for a
             material or property-block colour in a Linear project:
               Fresnel family (omni body, Mass prisms, Space, Time): lerp(_BrightColor, _DarkColor, (1 + N.V) / 2)
               Charge (ChargeCrystal.hlsl): lerp(dull, bright, sat(f * rimStrength)) * facet + bright * emission * f,
                 f = (1 - N.V)^rimPower, facet = lerp(ambient, 1, |N.key|); colours = Crystal.TryResolveTintColors
                 (the CTA pair -> the element's hue -> ElementalCrystalDimming). Its discharge bolts are NOT drawn.
  bloom      URP 17 (com.unity.render-pipelines.universal, PostProcessPassRenderGraph + Bloom.shader), LQ path, with
             GamePlay PostProcessing Profile.asset: threshold GammaToLinear(0.2), hard-coded knee threshold / 2,
             clamp 0.5 PER CHANNEL before the threshold, Half downscale, maxIterations 4, scatter lerp(0.05, 0.95,
             0.7), intensity 2.5, white tint. Prefilter -> (2x2 downsample + 9-tap H, 9-tap V) per mip -> upsample
             lerp(high, low, scatter) -> color += bloom * intensity.
  output     no tonemapping (the profile has none), clipped to [0, 1], encoded sRGB.
  backdrop   EnvironmentColors.SkyColor.

WHAT IS NOT: the depth ratchet's column motion, the Charge discharge bolts and the omni's flux arcs (white-blue,
transient), fog, other scene light, MSAA. Each crystal is drawn at the same on-screen radius, at a NEAR and a FAR
distance, on a 1920 x 1080 frame's pixel scale.

THE ANALYSIS asks the user's three questions of the light each crystal ADDS over the sky (its body and its glow,
weighted by luminance; the composite would borrow the navy sky's blue and call every halo green): is it lime (sRGB HSV
hue 75-105 degrees, the green-yellow window kite_mote_colors.py draws lime in) or has it gone GREEN (above 105), YELLOW
(below 75) or WHITE (saturation under 0.35 at value above 0.85); and how far apart the five crystals' mean colours
are in OKLab (about 0.02 is one just-noticeable difference).

--propose searches the green-yellow sheet inside the Wrasse's lime boundary (kite_mote_colors.in_lime) for the four
shades whose crystals stay lime after bloom at both distances (at most 5% off-lime) and sit farthest apart from each
other and from the omni, keeping each element's character: Mass the darkest, Space the coolest, Time the warmest.
--charge-brightest and --wrasse-apart add the Wrasse's two other rules. --shades renders any set; --dimming tries
another ElementalCrystalDimming (it reaches only the Charge crystal).

Usage:
  python3 Tools/Build/crystal_bloom_preview.py --out DIR [--json]                  # the shipped set
  python3 Tools/Build/crystal_bloom_preview.py --out DIR --shades Charge=#..,Mass=#..,Space=#..,Time=#..
  python3 Tools/Build/crystal_bloom_preview.py --out DIR --propose                 # ~20 min cold, seconds cached
"""
import argparse
import json
import math
import os
import re
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import fbx_binary as F  # noqa: E402
import kite_mote_colors as KITE  # noqa: E402

ELEMENTS = ("Charge", "Mass", "Space", "Time")
MAT = "Assets/_Graphics/Materials/CrystalMaterials"
PROFILE = "Assets/_Graphics/GamePlay PostProcessing Profile.asset"
PALETTE = KITE.PALETTE
NUM = KITE.NUM

FRAME_W, FRAME_H = 1920, 1080   # the bloom's pixel scale
CROP = 512                      # each crystal is rendered in a CROP x CROP window of that frame
SS = 2                          # supersampling
SIZES = {"near": 120, "far": 26}   # on-screen crystal radius, pixels

GREEN_YELLOW = (75.0, 105.0)
CHARGE_BRIGHTEST = False
DIMMING = None   # --dimming: try another EnvironmentColors.ElementalCrystalDimming (the only crystal it reaches is Charge)
WRASSE_APART = False   # --wrasse-apart: also hold the four 2D shades apart in hue, saturation AND value (0.02 each)


# ───────────────────────────────────────────────────────────── colour
def srgb_to_linear(c):
    c = np.asarray(c, dtype=np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((np.maximum(c, 0) + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(np.asarray(c, dtype=np.float64), 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def oklab_linear(lin):
    lin = np.asarray(lin, dtype=np.float64)
    r, g, b = lin[..., 0], lin[..., 1], lin[..., 2]
    l = np.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b)
    m = np.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b)
    s = np.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b)
    return np.stack([0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                     1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                     0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s], axis=-1)


def hsv(srgb):
    srgb = np.asarray(srgb, dtype=np.float64)
    mx, mn = srgb.max(-1), srgb.min(-1)
    d = mx - mn
    r, g, b = srgb[..., 0], srgb[..., 1], srgb[..., 2]
    with np.errstate(divide="ignore", invalid="ignore"):
        h = np.where(d == 0, 0.0,
            np.where(mx == r, (60 * ((g - b) / d)) % 360,
            np.where(mx == g, 60 * ((b - r) / d) + 120, 60 * ((r - g) / d) + 240)))
        s = np.where(mx > 0, d / mx, 0.0)
    return h, s, mx


def hexof(srgb):
    return "#%02x%02x%02x" % tuple(int(round(max(0, min(1, v)) * 255)) for v in srgb)


# ───────────────────────────────────────────────────────────── shipped values
def read(rel):
    with open(os.path.join(REPO, rel), encoding="utf-8") as fh:
        return fh.read()


def mat_color(rel, prop):
    m = re.search(r"^    - %s: \{r: %s, g: %s, b: %s" % (re.escape(prop), NUM, NUM, NUM), read(rel), re.M)
    if not m:
        raise SystemExit("crystal_bloom_preview: %s has no %s" % (rel, prop))
    return tuple(float(x) for x in m.groups())


def mat_float(rel, prop):
    m = re.search(r"^    - %s: %s$" % (re.escape(prop), NUM), read(rel), re.M)
    return float(m.group(1))


def palette_color(key):
    return KITE.yaml_color(read(PALETTE), key)


def palette_float(key):
    return float(re.search(r"^\s*%s: %s" % (key, NUM), read(PALETTE), re.M).group(1))


def bloom_settings():
    t = read(PROFILE)
    doc = [d for d in t.split("\n--- ") if "m_Name: Bloom" in d][0]

    def val(name, default):
        m = re.search(r"\n  %s:\n    m_OverrideState: (\d)\n    m_Value: %s" % (name, NUM), doc)
        return float(m.group(2)) if m and m.group(1) == "1" else default
    if re.search(r"\n  active: 0", doc):
        raise SystemExit("crystal_bloom_preview: the gameplay Bloom is inactive")
    hq = re.search(r"\n  highQualityFiltering:\n    m_OverrideState: 1\n    m_Value: 1", doc)
    if hq:
        raise SystemExit("crystal_bloom_preview: the profile turns on HQ bloom filtering, which this does not model")
    return {"threshold": val("threshold", 0.9), "intensity": val("intensity", 0.0), "clamp": val("clamp", 65472.0),
            "scatter": val("scatter", 0.7), "maxIterations": int(val("maxIterations", 6))}


def with_hue_of(c, hue):
    peak = max(c)
    return tuple(h * peak for h in hue)


def crystal_looks(shades, from_materials=True):
    """Each crystal's shader and its sRGB colours as the game hands them (free state). With from_materials False,
    Mass, Space and Time take `shades` by author_elemental_crystal_colors.py's rule (the shade at the lime donor's
    brightest channel) instead of reading their shipped materials - how a proposed set would ship."""
    cta_b, cta_d = palette_color("BrightCTA"), palette_color("DarkCTA")
    dim = DIMMING if DIMMING is not None else palette_float("ElementalCrystalDimming")
    charge_mat = f"{MAT}/ChargeCrystalMaterial.mat"
    charge_b = tuple(v * dim for v in with_hue_of(cta_b, shades["Charge"]))
    charge_d = tuple(v * dim for v in with_hue_of(cta_d, shades["Charge"]))
    looks = {
        "Omni": ("fresnel", mat_color(f"{MAT}/OmniCrystalBody.mat", "_BrightColor"),
                 mat_color(f"{MAT}/OmniCrystalBody.mat", "_DarkColor")),
        "Charge": ("charge", charge_b, charge_d, {
            "rimPower": mat_float(charge_mat, "_RimPower"), "rimStrength": mat_float(charge_mat, "_RimStrength"),
            "emission": mat_float(charge_mat, "_EmissionStrength"), "ambient": mat_float(charge_mat, "_FacetAmbient"),
            "key": mat_color(charge_mat, "_KeyDir")}),
        "Mass": ("fresnel", mat_color(f"{MAT}/MassDepthPrisms.mat", "_BrightColor"),
                 mat_color(f"{MAT}/MassDepthPrisms.mat", "_DarkColor")),
        "Space": ("fresnel", mat_color(f"{MAT}/SpaceCrystalFresnelMaterial.mat", "_BrightColor"),
                  mat_color(f"{MAT}/SpaceCrystalFresnelMaterial.mat", "_DarkColor")),
        "Time": ("fresnel", mat_color(f"{MAT}/TimeCrystalFresnelMaterial.mat", "_BrightColor"),
                 mat_color(f"{MAT}/TimeCrystalFresnelMaterial.mat", "_DarkColor")),
    }
    if not from_materials:
        donor = mat_color(f"{MAT}/LimeCrystalFresnelMaterial.mat", "_BrightColor")
        for e in ("Mass", "Space", "Time"):
            looks[e] = ("fresnel", with_hue_of(donor, shades[e]), looks[e][2])
    return looks


def wrasse_shades():
    m = re.search(r"  elementColors:\n((?:  - \{r: [^\n]*\}\n){4})",
                  read("Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset"))
    cols = [tuple(float(x) for x in c) for c in re.findall(r"\{r: %s, g: %s, b: %s" % (NUM, NUM, NUM), m.group(1))]
    return dict(zip(ELEMENTS, cols))


# ───────────────────────────────────────────────────────────── meshes
def fbx_mesh(rel):
    nodes, _, _ = F.read(os.path.join(REPO, rel))
    objs = [n for n in nodes if n.name == "Objects"][0]
    geo = [c for c in objs.children if c.name == "Geometry" and c.first("PolygonVertexIndex")][0]
    flat = geo.first("Vertices").props[0][1]
    pvi = geo.first("PolygonVertexIndex").props[0][1]
    verts = np.array(flat, dtype=np.float64).reshape(-1, 3)
    ln = geo.first("LayerElementNormal")
    nflat = np.array(ln.first("Normals").props[0][1], dtype=np.float64).reshape(-1, 3)
    if ln.first("MappingInformationType").props[0][1] != b"ByPolygonVertex":
        raise SystemExit("crystal_bloom_preview: %s normals are not ByPolygonVertex" % rel)
    nidx = (np.array(ln.first("NormalsIndex").props[0][1]) if ln.first("NormalsIndex") else np.arange(len(pvi)))
    tris, ntris, cur = [], [], []
    for pv, idx in enumerate(pvi):
        last = idx < 0
        cur.append((-idx - 1 if last else idx, nidx[pv]))
        if last:
            for k in range(1, len(cur) - 1):
                tris.append((cur[0][0], cur[k][0], cur[k + 1][0]))
                ntris.append((cur[0][1], cur[k][1], cur[k + 1][1]))
            cur = []
    return verts[np.array(tris)], nflat[np.array(ntris)]


def mass_mesh():
    import author_omni_crystal_triangles as OMNI
    import author_mass_crystal_look as MASS
    verts, _, idx = MASS.mass_prism_mesh()
    verts = OMNI.thicken(verts, MASS.MASS_DEPTH_THICKNESS)
    pos = np.array([v[0] for v in verts], dtype=np.float64)
    nrm = np.array([v[1] for v in verts], dtype=np.float64)
    tri = np.array(idx).reshape(-1, 3)
    return pos[tri], nrm[tri]


def meshes():
    import author_omni_crystal_triangles as OMNI
    return {"Omni": fbx_mesh(OMNI.OMNI_FBX), "Charge": fbx_mesh("Assets/_Models/ChargeCrystalExport1_7-11-25.fbx"),
            "Mass": mass_mesh(), "Space": fbx_mesh("Assets/_Models/spacecrystalanim.fbx"),
            "Time": fbx_mesh("Assets/_Models/TimeCrystalExport.fbx")}


# ───────────────────────────────────────────────────────────── raster
def rotation(yaw, pitch, roll):
    cy, sy, cp, sp, cr, sr = (math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch), math.cos(roll),
                              math.sin(roll))
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    rx = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]])
    rz = np.array([[cr, -sr, 0], [sr, cr, 0], [0, 0, 1]])
    return rz @ rx @ ry


POSE = rotation(0.55, 0.42, 0.18)


def render_crystal(mesh, look, radius_px, background_lin):
    """Linear HDR CROP x CROP image (after SS downsampling) and the crystal's coverage mask."""
    tris, corner_n = mesh
    n = CROP * SS
    centre = tris.reshape(-1, 3).mean(0)
    t = (tris - centre) @ POSE.T
    cn = corner_n @ POSE.T
    rmax = np.linalg.norm(t.reshape(-1, 3), axis=1).max()
    t = t / rmax                                  # unit radius
    fov = math.radians(60.0)
    focal = (FRAME_H * SS / 2) / math.tan(fov / 2)
    dist = focal / (radius_px * SS)               # distance at which a unit sphere spans radius_px
    cam = np.array([0.0, 0.0, -dist])             # camera on -z looking +z (world = crystal frame after the pose)
    p = t - cam
    sx = p[..., 0] / p[..., 2] * focal + n / 2
    sy = -p[..., 1] / p[..., 2] * focal + n / 2
    z = p[..., 2]

    nl = np.linalg.norm(np.cross(t[:, 1] - t[:, 0], t[:, 2] - t[:, 0]), axis=1)
    keep = nl > 1e-12

    depth = np.full((n, n), np.inf)
    color = np.zeros((n, n, 3))
    color[:] = background_lin
    cover = np.zeros((n, n))
    kind = look[0]
    bright = srgb_to_linear(look[1])
    dark = srgb_to_linear(look[2])
    if kind == "charge":
        prm = look[3]
        key = np.array(prm["key"], dtype=np.float64)
        key = key / np.linalg.norm(key)
        # the key is a WORLD direction; this world is the camera's (looking along +z), the crystal posed in it

    for i in np.nonzero(keep)[0]:
        x0, y0, x1, y1, x2, y2 = sx[i, 0], sy[i, 0], sx[i, 1], sy[i, 1], sx[i, 2], sy[i, 2]
        minx, maxx = int(max(0, math.floor(min(x0, x1, x2)))), int(min(n - 1, math.ceil(max(x0, x1, x2))))
        miny, maxy = int(max(0, math.floor(min(y0, y1, y2)))), int(min(n - 1, math.ceil(max(y0, y1, y2))))
        if minx > maxx or miny > maxy:
            continue
        area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if abs(area) < 1e-9:
            continue
        xs, ys = np.meshgrid(np.arange(minx, maxx + 1) + 0.5, np.arange(miny, maxy + 1) + 0.5)
        w0 = ((x1 - xs) * (y2 - ys) - (x2 - xs) * (y1 - ys)) / area
        w1 = ((x2 - xs) * (y0 - ys) - (x0 - xs) * (y2 - ys)) / area
        w2 = 1 - w0 - w1
        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not inside.any():
            continue
        # perspective-correct depth
        iz = w0 / z[i, 0] + w1 / z[i, 1] + w2 / z[i, 2]
        zz = 1 / iz
        sub = depth[miny:maxy + 1, minx:maxx + 1]
        win = inside & (zz < sub)
        if not win.any():
            continue
        pos = (w0[..., None] * t[i, 0] / z[i, 0] + w1[..., None] * t[i, 1] / z[i, 1]
               + w2[..., None] * t[i, 2] / z[i, 2]) * zz[..., None]
        view = cam - pos
        view /= np.linalg.norm(view, axis=-1, keepdims=True)
        # the authored normals, interpolated (perspective-correct) and renormalised, as the vertex stage hands them
        nn = (w0[..., None] * cn[i, 0] / z[i, 0] + w1[..., None] * cn[i, 1] / z[i, 1]
              + w2[..., None] * cn[i, 2] / z[i, 2]) * zz[..., None]
        nn /= np.maximum(np.linalg.norm(nn, axis=-1, keepdims=True), 1e-9)
        ndv = (view * nn).sum(-1)
        if kind == "fresnel":
            f = (1.0 + ndv) / 2.0
            col = bright + (dark - bright) * f[..., None]
        else:
            fr = np.clip(1.0 - np.clip(ndv, 0, 1), 0, 1) ** prm["rimPower"]
            facet = prm["ambient"] + (1 - prm["ambient"]) * np.abs(nn @ key)[..., None]
            body = (dark + (bright - dark) * np.clip(fr * prm["rimStrength"], 0, 1)[..., None]) * facet
            col = body + bright * (prm["emission"] * fr)[..., None]
            tri_n = np.cross(t[i, 1] - t[i, 0], t[i, 2] - t[i, 0])
            if float(tri_n @ (cam - t[i].mean(0))) * float(tri_n @ cn[i].mean(0)) < 0:
                continue   # Cull Back: the face turns away from the camera
        sub[win] = zz[win]
        csub = color[miny:maxy + 1, minx:maxx + 1]
        csub[win] = col[win]
        cover[miny:maxy + 1, minx:maxx + 1][win] = 1.0

    img = color.reshape(CROP, SS, CROP, SS, 3).mean((1, 3))
    cov = cover.reshape(CROP, SS, CROP, SS).mean((1, 3))
    return img, cov


# ───────────────────────────────────────────────────────────── URP bloom
def sample(img, u, v):
    """Bilinear, clamp-to-edge, texel centres at (i + 0.5) / size - the GPU's LinearClamp."""
    h, w = img.shape[:2]
    x = u * w - 0.5
    y = v * h - 0.5
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = (x - x0)[..., None]
    fy = (y - y0)[..., None]
    x0c, x1c = np.clip(x0, 0, w - 1), np.clip(x0 + 1, 0, w - 1)
    y0c, y1c = np.clip(y0, 0, h - 1), np.clip(y0 + 1, 0, h - 1)
    a = img[y0c, x0c] * (1 - fx) + img[y0c, x1c] * fx
    b = img[y1c, x0c] * (1 - fx) + img[y1c, x1c] * fx
    return a * (1 - fy) + b * fy


def uv_grid(w, h):
    u, v = np.meshgrid((np.arange(w) + 0.5) / w, (np.arange(h) + 0.5) / h)
    return u, v


G9 = (0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703, 0.19459459, 0.12162162, 0.05405405, 0.01621622)


def bloom(color, s):
    """URP 17 bloom, LQ: returns the image with bloom composited, still linear HDR."""
    h, w = color.shape[:2]
    # The pyramid's sizes come from the FRAME, so a crop blooms at the frame's pixel scale.
    fw, fh = FRAME_W >> 1, FRAME_H >> 1
    iterations = int(math.floor(math.log2(max(fw, fh)) - 1))
    mips = max(1, min(iterations, s["maxIterations"]))
    threshold = float(srgb_to_linear(s["threshold"])) if s["threshold"] <= 0.04045 else \
        ((s["threshold"] + 0.055) / 1.055) ** 2.4   # Mathf.GammaToLinearSpace
    knee = threshold * 0.5
    clamp = s["clamp"]
    scatter = 0.05 + (0.95 - 0.05) * s["scatter"]

    tw, th = max(1, w >> 1), max(1, h >> 1)
    u, v = uv_grid(tw, th)
    c = sample(color, u, v)
    c = np.minimum(clamp, c)
    br = c.max(-1)
    soft = np.clip(br - threshold + knee, 0.0, 2.0 * knee)
    soft = soft * soft / (4.0 * knee + 1e-4)
    mult = np.maximum(br - threshold, soft) / np.maximum(br, 1e-4)
    down = [np.maximum(c * mult[..., None], 0)]
    ups = [None]
    for i in range(1, mips):
        src = down[-1]
        sw, sh = src.shape[1], src.shape[0]
        dw, dh = max(1, tw >> i), max(1, th >> i)
        u, v = uv_grid(dw, dh)
        tx = 2.0 / sw
        hb = sum(G9[k] * sample(src, u + (k - 4) * tx, v) for k in range(9))
        ty = 1.0 / dh
        vb = (0.07027027 * sample(hb, u, v - 3.23076923 * ty) + 0.31621622 * sample(hb, u, v - 1.38461538 * ty)
              + 0.22702703 * sample(hb, u, v)
              + 0.31621622 * sample(hb, u, v + 1.38461538 * ty) + 0.07027027 * sample(hb, u, v + 3.23076923 * ty))
        down.append(vb)
        ups.append(None)
    up = [None] * mips
    for i in range(mips - 2, -1, -1):
        high = down[i]
        low = down[i + 1] if i == mips - 2 else up[i + 1]
        u, v = uv_grid(high.shape[1], high.shape[0])
        up[i] = high + (sample(low, u, v) - high) * scatter
    final = up[0] if mips > 1 else down[0]
    u, v = uv_grid(w, h)
    return color + sample(final, u, v) * s["intensity"]


# ───────────────────────────────────────────────────────────── analysis
def classify(added_lin, weight_lin):
    """Light-weighted share of the light a crystal ADDS over the backdrop (its body and its glow) that reads lime /
    green / yellow / white, and that light's mean colour. The added light, not the composite: a dim glow over the
    navy sky borrows the sky's blue, which would call every crystal's halo green whatever the crystal's colour."""
    added_lin = np.maximum(added_lin, 0)
    peak = np.maximum(added_lin.max(-1, keepdims=True), 1e-9)
    # hue and saturation of the light's chromaticity, in sRGB (where the 75-105 degree window is defined)
    h, s, _ = hsv(linear_to_srgb(added_lin / peak))
    val = linear_to_srgb(np.minimum(added_lin, 1.0)).max(-1)
    w = weight_lin
    tot = w.sum()
    white = (s < 0.35) & (val > 0.85)
    green = ~white & (h > GREEN_YELLOW[1]) & (h < 200)
    yellow = ~white & ((h < GREEN_YELLOW[0]) | (h > 300))
    lime = ~white & ~green & ~yellow
    share = {k: float((w * m).sum() / tot) for k, m in (("lime", lime), ("green", green), ("yellow", yellow),
                                                        ("white", white))}
    mean_lin = (added_lin * w[..., None]).sum((0, 1)) / tot
    lab = oklab_linear(mean_lin)
    return share, mean_lin, lab


def lch(lab):
    return float(lab[0]), float(math.hypot(lab[1], lab[2])), float(math.degrees(math.atan2(lab[2], lab[1])) % 360)


def flower(size, color_srgb, bg_srgb):
    """The Wrasse's mote: a flat four-petal element flower, no post-processing."""
    im = Image.new("RGB", (size, size), tuple(int(round(c * 255)) for c in bg_srgb))
    d = ImageDraw.Draw(im)
    col = tuple(int(round(c * 255)) for c in color_srgb)
    cx = cy = size / 2
    r = size * 0.32
    for k in range(4):
        a = math.radians(45 + 90 * k)
        px, py = cx + math.cos(a) * r * 0.55, cy + math.sin(a) * r * 0.55
        pts = []
        for j in range(24):
            t = 2 * math.pi * j / 24
            ex, ey = math.cos(t) * r * 0.55, math.sin(t) * r * 0.28
            pts.append((px + ex * math.cos(a) - ey * math.sin(a), py + ex * math.sin(a) + ey * math.cos(a)))
        d.polygon(pts, fill=col)
    d.ellipse([cx - r * 0.18, cy - r * 0.18, cx + r * 0.18, cy + r * 0.18], fill=col)
    return im


def to_image(lin):
    return Image.fromarray((linear_to_srgb(lin) * 255 + 0.5).astype(np.uint8))


def run(out, shades_override=None):
    s = bloom_settings()
    shades = shades_override or dict(zip(ELEMENTS, KITE.palette_colors(read(PALETTE))))
    looks = crystal_looks(shades, from_materials=shades_override is None)
    mesh = meshes()
    sky = palette_color("SkyColor")
    bg_lin = srgb_to_linear(sky)
    results = {}
    tiles = {}
    for size_name, radius in SIZES.items():
        for name in ("Omni",) + ELEMENTS:
            raw, cov = render_crystal(mesh[name], looks[name], radius, bg_lin)
            post = bloom(raw, s)
            post_srgb = linear_to_srgb(post)
            raw_srgb = linear_to_srgb(raw)
            # weight: the light a pixel carries above the backdrop (crystal and glow), linear luminance
            lum = lambda a: 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]   # noqa: E731
            wpost = np.maximum(lum(np.minimum(post, 1.0)) - lum(bg_lin), 0)
            wraw = np.maximum(lum(raw) - lum(bg_lin), 0)
            share_post, mean_post, lab_post = classify(np.minimum(post, 1.0) - bg_lin, wpost)
            share_raw, mean_raw, lab_raw = classify(raw - bg_lin, wraw)
            clipped = float(((np.minimum(post, 10).max(-1) >= 1.0) * cov).sum() / max(cov.sum(), 1))
            results.setdefault(name, {})[size_name] = {
                "post": {"share": share_post, "mean_srgb": hexof(linear_to_srgb(mean_post)), "lch": lch(lab_post),
                         "lab": [float(x) for x in lab_post]},
                "raw": {"share": share_raw, "mean_srgb": hexof(linear_to_srgb(mean_raw)), "lch": lch(lab_raw),
                        "lab": [float(x) for x in lab_raw]},
                "clipped_crystal_px": clipped}
            tiles[(name, size_name, "raw")] = to_image(raw)
            tiles[(name, size_name, "post")] = to_image(post)
    return s, looks, shades, results, tiles, sky


def sheet(tiles, shades, sky, results, path, title):
    names = ("Omni",) + ELEMENTS
    tile = 300
    pad = 16
    head = 64
    rowlab = 150
    rows = [("2D mote (Wrasse, no bloom)", None), ("in game, NEAR, no bloom", ("near", "raw")),
            ("in game, NEAR, with bloom", ("near", "post")), ("in game, FAR, with bloom", ("far", "post"))]
    W = rowlab + len(names) * (tile + pad) + pad
    H = head + len(rows) * (tile + pad + 54) + pad
    im = Image.new("RGB", (W, H), (18, 18, 22))
    d = ImageDraw.Draw(im)
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 18)
        small = ImageFont.truetype("DejaVuSans.ttf", 14)
    except OSError:
        font = small = ImageFont.load_default()
    d.text((pad, 18), title, fill=(235, 235, 235), font=font)
    for c, name in enumerate(names):
        d.text((rowlab + c * (tile + pad) + pad, head - 26), name, fill=(235, 235, 235), font=font)
    for r, (label, key) in enumerate(rows):
        y = head + r * (tile + pad + 54)
        d.text((pad, y + tile / 2 - 20), "\n".join(label.split(", ")), fill=(200, 200, 200), font=small)
        for c, name in enumerate(names):
            x = rowlab + c * (tile + pad) + pad
            if key is None:
                if name == "Omni":
                    d.text((x + 10, y + tile / 2), "(no mote)", fill=(120, 120, 120), font=small)
                    continue
                t = flower(tile, shades[name], sky)
                cap = "%s" % hexof(shades[name])
            else:
                size_name, stage = key
                src = tiles[(name, size_name, stage)]
                crop = CROP // 2 if size_name == "near" else CROP // 4
                o = (CROP - crop * 2) // 2 if size_name == "near" else (CROP - crop * 2) // 2
                box = (CROP // 2 - crop, CROP // 2 - crop, CROP // 2 + crop, CROP // 2 + crop) if size_name == "near" \
                    else (CROP // 2 - 64, CROP // 2 - 64, CROP // 2 + 64, CROP // 2 + 64)
                t = src.crop(box).resize((tile, tile), Image.NEAREST if size_name == "far" else Image.LANCZOS)
                res = results[name][size_name]["raw" if stage == "raw" else "post"]
                sh = res["share"]
                cap = "%s  lime %d%%  grn %d%% yel %d%% wht %d%%" % (res["mean_srgb"], round(100 * sh["lime"]),
                                                                     round(100 * sh["green"]), round(100 * sh["yellow"]),
                                                                     round(100 * sh["white"]))
            im.paste(t, (x, y))
            d.text((x, y + tile + 4), cap, fill=(210, 210, 210), font=small)
    im.save(path)


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--shades", help="render a proposed set instead of the shipped one: Charge=#rrggbb,Mass=...,"
                                     "Space=...,Time=... (the 2D motes and the crystals both take it)")
    ap.add_argument("--dimming", type=float, help="render with this ElementalCrystalDimming instead of the palette's")
    ap.add_argument("--title", default="Free crystals through the gameplay bloom (URP 17, threshold 0.2, clamp 0.5, "
                                        "intensity 2.5)")
    ap.add_argument("--charge-brightest", action="store_true",
                    help="--propose keeps Charge's shade the brightest of the four, as kite_mote_colors assigns it")
    ap.add_argument("--wrasse-apart", action="store_true",
                    help="--propose also holds the 2D shades apart in hue, saturation and value (kite_mote_colors.apart)")
    ap.add_argument("--propose", action="store_true",
                    help="search the lime sheet for the four shades farthest apart after bloom, all reading lime")
    a = ap.parse_args(argv)
    os.makedirs(a.out, exist_ok=True)
    if a.propose:
        global CHARGE_BRIGHTEST
        CHARGE_BRIGHTEST = a.charge_brightest
        global WRASSE_APART
        WRASSE_APART = a.wrasse_apart
        propose(a.out)
        return 0
    global DIMMING
    DIMMING = a.dimming
    override = None
    if a.shades:
        override = {}
        for part in a.shades.split(","):
            k, v = part.split("=")
            v = v.strip().lstrip("#")
            override[k.strip()] = tuple(int(v[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
        if set(override) != set(ELEMENTS):
            raise SystemExit("crystal_bloom_preview: --shades needs all four of %s" % (ELEMENTS,))
    s, looks, shades, results, tiles, sky = run(a.out, override)
    for (name, size_name, stage), t in tiles.items():
        t.save(os.path.join(a.out, "%s_%s_%s.png" % (name.lower(), size_name, stage)))
    sheet(tiles, shades, sky, results, os.path.join(a.out, "crystal_bloom_sheet.png"),
          a.title)
    print("bloom:", s)
    for name in ("Omni",) + ELEMENTS:
        for size_name in SIZES:
            r = results[name][size_name]
            print("%-6s %-4s raw %s L%.2f C%.3f h%3.0f | bloom %s L%.2f C%.3f h%3.0f  lime %3d%% green %3d%% yellow %3d%% "
                  "white %3d%%  clipped %3d%%" % (
                      name, size_name, r["raw"]["mean_srgb"], *r["raw"]["lch"], r["post"]["mean_srgb"], *r["post"]["lch"],
                      *(round(100 * r["post"]["share"][k]) for k in ("lime", "green", "yellow", "white")),
                      round(100 * r["clipped_crystal_px"])))
    names = ("Omni",) + ELEMENTS
    for size_name in SIZES:
        print("pairwise OKLab distance after bloom (%s):" % size_name)
        for i in range(len(names)):
            row = []
            for j in range(len(names)):
                a_ = np.array(results[names[i]][size_name]["post"]["lab"])
                b_ = np.array(results[names[j]][size_name]["post"]["lab"])
                row.append("%.3f" % np.linalg.norm(a_ - b_))
            print("  %-6s %s" % (names[i], " ".join(row)))
    if a.json:
        with open(os.path.join(a.out, "analysis.json"), "w") as fh:
            json.dump({"bloom": s, "results": results}, fh, indent=1)
    return 0



# ───────────────────────────────────────────────────────────── proposal search
def _components(mesh, kind_look, radius, bg_lin):
    """A crystal's image is LINEAR in its bright and dark colours (the shading weights depend on geometry alone, and
    the supersample is a mean), so three renders give it for any pair: img = B + Ab * bright + Ad * dark."""
    kind = kind_look[0]
    extra = tuple(kind_look[3:])
    zero = (0.0, 0.0, 0.0)
    base, cov = render_crystal(mesh, (kind, zero, zero) + extra, radius, bg_lin)
    one = (1.0, 1.0, 1.0)
    # srgb_to_linear(1) = 1 and (0) = 0, so these unit renders are the linear weights
    with_b, _ = render_crystal(mesh, (kind, one, zero) + extra, radius, bg_lin)
    with_d, _ = render_crystal(mesh, (kind, zero, one) + extra, radius, bg_lin)
    return base, (with_b - base)[..., :1], (with_d - base)[..., :1], cov


def measure(img_lin, bg_lin, s):
    post = bloom(img_lin, s)
    lum = lambda a: 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]   # noqa: E731
    w = np.maximum(lum(np.minimum(post, 1.0)) - lum(bg_lin), 0)
    share, mean_lin, lab = classify(np.minimum(post, 1.0) - bg_lin, w)
    return share, lab, post


def candidates():
    """Inside the Wrasse's lime boundary (kite_mote_colors.in_lime: lime's cell against Jade, Ruby, Gold, Blue and
    Fire, with its margin, on the blue-0 green-yellow sheet), so a shade is lime in hue AND lightness before any
    rendering; the bloom test then asks whether it stays lime on screen."""
    fam, _ = KITE.families()
    out = []
    for h in np.arange(76.0, 104.01, 1.0):
        for g in np.arange(0.6, 1.001, 0.025):
            c = (round(float(g * (120.0 - h) / 60.0), 4), round(float(g), 4), 0.0)
            if KITE.in_lime(c, fam):
                out.append(c)
    return out


def propose(out):
    s = bloom_settings()
    shades = dict(zip(ELEMENTS, KITE.palette_colors(read(PALETTE))))
    looks = crystal_looks(shades)
    mesh = meshes()
    bg_lin = srgb_to_linear(palette_color("SkyColor"))
    cta_b, cta_d = palette_color("BrightCTA"), palette_color("DarkCTA")
    dim = palette_float("ElementalCrystalDimming")
    cands = candidates()
    cache_path = os.path.join(out, "proposal_cache.json")
    key = {"cands": cands, "bloom": s, "looks": {e: [list(x) if isinstance(x, tuple) else x for x in looks[e][:3]]
                                                 for e in looks}}
    cache = None
    if os.path.exists(cache_path):
        with open(cache_path) as fh:
            cache = json.load(fh)
        if cache.get("key") != json.loads(json.dumps(key)):
            cache = None
    table = {}   # element -> list of (cand, {size: (share, lab)})
    omni = {}
    for size, radius in SIZES.items():
        img, _ = render_crystal(mesh["Omni"], looks["Omni"], radius, bg_lin)
        share, lab, _ = measure(img, bg_lin, s)
        omni[size] = (share, lab)
    for e in ELEMENTS:
        if cache:
            table[e] = [(tuple(c), {sz: (r[sz][0], np.array(r[sz][1])) for sz in r}) for c, r in cache["table"][e]]
            continue
        comps = {size: _components(mesh[e], looks[e], radius, bg_lin) for size, radius in SIZES.items()}
        rows = []
        for c in cands:
            if e == "Charge":
                b = tuple(v * dim for v in with_hue_of(cta_b, c))
                d = tuple(v * dim for v in with_hue_of(cta_d, c))
            else:
                b, d = c, looks[e][2]
            res = {}
            for size, (base, ab, ad, _) in comps.items():
                img = base + ab * srgb_to_linear(b) + ad * srgb_to_linear(d)
                share, lab, _ = measure(img, bg_lin, s)
                res[size] = (share, lab)
            rows.append((c, res))
        table[e] = rows
        print("searched", e, len(rows), "candidates", flush=True)
    if not cache:
        with open(cache_path, "w") as fh:
            json.dump({"key": key, "table": {e: [(list(c), {sz: (r[sz][0], [float(x) for x in r[sz][1]]) for sz in r})
                                                 for c, r in table[e]] for e in ELEMENTS}}, fh)

    def ok(res):
        return all(r[0]["green"] + r[0]["yellow"] + r[0]["white"] <= 0.05 for r in res.values())

    pools = {e: [(c, r) for c, r in table[e] if ok(r)] for e in ELEMENTS}
    for e in ELEMENTS:
        print("%s: %d of %d candidates read lime after bloom at both sizes" % (e, len(pools[e]), len(table[e])))

    sizes = list(SIZES)
    labs = {e: np.array([[r[sz][1] for sz in sizes] for _, r in pools[e]]) for e in ELEMENTS}   # (n, sizes, 3)
    omni_lab = np.array([omni[sz][1] for sz in sizes])                                       # (sizes, 3)

    # Each shade keeps its element's character (kite_mote_colors.assign): Mass the lowest value (weight), Space cooler
    # than Time (toward Jade), and (with --charge-brightest) Charge the highest value (the spark); and the four stay
    # apart in hue, saturation AND value as the Wrasse's motes must (kite_mote_colors.apart at its 0.02 step). Charge's
    # value never reaches its crystal: the tint dims it by ElementalCrystalDimming whatever its shade.
    KITE.APART = KITE.APART_STEPS[-1]
    shade = {e: [KITE.oklab(c) for c, _ in pools[e]] for e in ELEMENTS}
    sL = {e: np.array([v[0] for v in shade[e]]) for e in ELEMENTS}
    sH = {e: np.array([KITE.lch(v)[2] for v in shade[e]]) for e in ELEMENTS}

    def valid(pick, e):
        """Over every candidate for e, with the others held at pick: does the set keep its characters?"""
        L = {k: (sL[k] if k == e else sL[k][pick[k]]) for k in ELEMENTS}
        H = {k: (sH[k] if k == e else sH[k][pick[k]]) for k in ELEMENTS}
        # Space the COOLEST of the four (toward Jade) and Time the WARMEST (toward Gold), not merely ordered
        ok = (L["Mass"] < L["Space"]) & (L["Mass"] < L["Time"]) & (H["Space"] > H["Time"]) & \
             (H["Space"] > H["Charge"]) & (H["Space"] > H["Mass"]) & (H["Time"] < H["Charge"]) & (H["Time"] < H["Mass"])
        if CHARGE_BRIGHTEST:
            ok = ok & (L["Charge"] > L["Space"]) & (L["Charge"] > L["Time"]) & (L["Charge"] > L["Mass"])
        ok = np.broadcast_to(ok, (len(pools[e]),)).copy()
        if WRASSE_APART:
            others = [shade[k][pick[k]] for k in ELEMENTS if k != e]
            for i in np.nonzero(ok)[0]:
                ok[i] = all(KITE.apart(shade[e][i], o) for o in others)
        return ok

    def score(pick, e=None):
        """Closest pair (over both sizes) for the pick; with e, a vector over every candidate for e (invalid -1)."""
        pts = {k: labs[k][pick[k]][None] for k in ELEMENTS}
        if e is not None:
            pts[e] = labs[e]
        allp = [omni_lab[None]] + [pts[k] for k in ELEMENTS]
        best = None
        for i in range(len(allp)):
            for j in range(i + 1, len(allp)):
                d = np.linalg.norm(allp[i] - allp[j], axis=-1).min(-1)
                best = d if best is None else np.minimum(best, d)
        mask = valid(pick, e if e is not None else "Charge")
        if e is None:
            return np.where(mask[pick["Charge"]], best, -1.0)
        return np.where(mask, best, -1.0)

    rng = np.random.default_rng(7)
    best_v, best = -1.0, None
    for _ in range(300):
        pick = {e: int(rng.integers(len(pools[e]))) for e in ELEMENTS}
        for _ in range(30):
            moved = False
            for e in ELEMENTS:
                cur = float(score(pick)[0])
                v = score(pick, e)
                k = int(np.argmax(v))
                if v[k] > cur + 1e-9:
                    pick[e], moved = k, True
            if not moved:
                break
        v = float(score(pick)[0])
        if v > best_v:
            best_v, best = v, {e: pools[e][pick[e]] for e in ELEMENTS}
    print("proposal: the closest pair is %.3f OKLab apart after bloom (current set: see the report)" % best_v)
    prop = {e: best[e][0] for e in ELEMENTS}
    for e in ELEMENTS:
        print("  %-6s %s  sRGB (%.4f, %.4f, 0)" % (e, hexof(prop[e]), prop[e][0], prop[e][1]))
    with open(os.path.join(out, "proposal.json"), "w") as fh:
        json.dump({"min_distance": best_v, "shades": prop}, fh, indent=1)
    return prop


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
