#!/usr/bin/env python3
"""Bench: ways to PRODUCE the shipped HyperSea sky on a phone, scored against the per-pixel shader.

The HyperSea sky (HyperSeaSkybox.shader) runs here as GLSL transpiled from the shader itself (hypersea_glsl.py), so
the reference is the shipped look, stars, cell net, band and core included. Each method is scored against that
shader supersampled 2x2 at 1280x720 (87 degree FOV), in the same views as genome_nebula_methods.py:

  per-pixel            the shader every frame (desktop tier today)
  shipped-png-4096     StaticHyperSeaSky.png through StaticSkyPanorama (MobileLow today), as the PNG holds it
  bake-equirect-N      the shader baked at load into an N x N/2 sRGB8 panorama (the same layout, no file)
  bake-cube-N          baked at load into an sRGB8 cubemap
  hybrid-cube-N        everything but the stars baked into a cube (alpha = the dust that dims them); stars run live
  hybrid-cells-cube-N  only the soft layers baked; the cell net and the stars run live
  cells-live-cube-N    everything but the cell net baked (stars included); the cell net runs live

Bakes are rendered in float and quantised to sRGB8 before sampling, as an ARGB32 sRGB RenderTexture would hold them.
Usage:  DISPLAY=:99 python3 Tools/Build/hypersea_sky_methods.py [--out DIR] [--json OUT.json]
"""
import argparse
import json
import os
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import genome_nebula_methods as gm  # noqa: E402
import hypersea_glsl as hg  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
PNG = REPO / "Assets/_Graphics/Skyboxes/StaticHyperSeaSky.png"
W, H, VIEWS = gm.W, gm.H, gm.VIEWS

SKY = r"""
vec3 nebula(vec3 d){ return clamp(hyperSeaFrag(d).rgb, 0.0, 1.0); }
// hyperSeaFrag without the stars; alpha is the dust factor the stars are multiplied by (stars are added before dust)
vec4 hsBase(vec3 viewDir){ vec3 dir = normalize(viewDir); float time = _Time.y;
  vec3 c = computeAmbient(dir, time) + computeGalacticPlane(dir, time) + computeNebulae(dir, time);
  float dust = computeDust(dir, time);
  c *= dust; c += computeCore(dir) + computeAndromeda(dir) + computeCellOverlay(dir, time) + computeAtmosphereBridge(dir);
  return vec4(c, dust); }
vec3 hsStars(vec3 viewDir){ vec3 dir = normalize(viewDir); return computeStars(dir, _Time.y); }
// the same split with the cell net live too: the bake holds only the soft layers
vec4 hsSoft(vec3 viewDir){ vec4 b = hsBase(viewDir); return vec4(b.rgb - computeCellOverlay(normalize(viewDir), _Time.y), b.a); }
vec4 hsNoCells(vec3 viewDir){ vec4 b = hsSoft(viewDir); return vec4(b.rgb + hsStars(viewDir)*b.a, 1.0); }
vec3 hsCells(vec3 viewDir){ return computeCellOverlay(normalize(viewDir), _Time.y); }
"""


def src(pixel):
    return "#version 330\n" + hg.hypersea_source() + SKY + gm.CAM + "out vec4 o;\n" + pixel


PIX_HYBRID = r"""uniform samplerCube Cube; void main(){ vec3 d = rayAt(gl_FragCoord.xy); vec4 b = texture(Cube, d);
  o = vec4(clamp(b.rgb + hsStars(d)*b.a, 0.0, 1.0), 1.0); }"""
PIX_HYBRID_CELLS = r"""uniform samplerCube Cube; void main(){ vec3 d = rayAt(gl_FragCoord.xy); vec4 b = texture(Cube, d);
  o = vec4(clamp(b.rgb + hsCells(d) + hsStars(d)*b.a, 0.0, 1.0), 1.0); }"""
BAKE_FACE_SOFT = r"""uniform vec2 BakeRes; uniform vec3 Fx, Fy, Fz; void main(){ vec2 uv = gl_FragCoord.xy/BakeRes*2.0-1.0;
  vec4 b = hsSoft(normalize(Fz + Fx*uv.x + Fy*uv.y)); o = vec4(clamp(b.rgb, 0.0, 1.0), clamp(b.a, 0.0, 1.0)); }"""
PIX_CELLS_ONLY_LIVE = r"""uniform samplerCube Cube; void main(){ vec3 d = rayAt(gl_FragCoord.xy);
  o = vec4(clamp(texture(Cube, d).rgb + hsCells(d), 0.0, 1.0), 1.0); }"""
BAKE_FACE_NOCELLS = r"""uniform vec2 BakeRes; uniform vec3 Fx, Fy, Fz; void main(){ vec2 uv = gl_FragCoord.xy/BakeRes*2.0-1.0;
  o = vec4(clamp(hsNoCells(normalize(Fz + Fx*uv.x + Fy*uv.y)).rgb, 0.0, 1.0), 1.0); }"""
BAKE_FACE_BASE = r"""uniform vec2 BakeRes; uniform vec3 Fx, Fy, Fz; void main(){ vec2 uv = gl_FragCoord.xy/BakeRes*2.0-1.0;
  vec4 b = hsBase(normalize(Fz + Fx*uv.x + Fy*uv.y)); o = vec4(clamp(b.rgb, 0.0, 1.0), clamp(b.a, 0.0, 1.0)); }"""
FACES = [((0, 0, -1), (0, -1, 0), (1, 0, 0)), ((0, 0, 1), (0, -1, 0), (-1, 0, 0)),
         ((1, 0, 0), (0, 0, 1), (0, 1, 0)), ((1, 0, 0), (0, 0, -1), (0, -1, 0)),
         ((1, 0, 0), (0, -1, 0), (0, 0, 1)), ((-1, 0, 0), (0, -1, 0), (0, 0, -1))]


def srgb8_linear(lin, alpha_linear=True):
    """Quantise linear float RGBA as an sRGB8 RenderTexture stores it, and decode back (what a sample returns)."""
    rgb = lin[..., :3]
    s = np.where(rgb <= 0.0031308, rgb * 12.92, 1.055 * np.power(np.clip(rgb, 0, 1), 1 / 2.4) - 0.055)
    q = np.round(np.clip(s, 0, 1) * 255) / 255
    out = np.empty_like(lin)
    out[..., :3] = np.where(q <= 0.04045, q / 12.92, np.power((q + 0.055) / 1.055, 2.4))
    out[..., 3] = np.round(np.clip(lin[..., 3], 0, 1) * 255) / 255   # alpha is linear in an sRGB RT
    return out.astype(np.float32)


class HBench(gm.Bench):
    def __init__(self):
        super().__init__()
        self.hv = hg.hypersea_uniforms()

    def prog(self, fs, vs=gm.VS_FULL):
        p = self.ctx.program(vertex_shader=vs, fragment_shader=fs)
        for k, v in self.hv.items():
            if k in p:
                p[k].value = v if isinstance(v, float) or p[k].dimension == 1 and not isinstance(v, tuple) else (
                    v[0] if p[k].dimension == 1 else tuple(v)[:p[k].dimension])
        if "_Time" in p:
            p["_Time"].value = (0.0, 0.0, 0.0, 0.0)
        return p

    def bake(self, fs, size, **uni):
        tex = self.ctx.texture(size, 4, dtype="f4"); fbo = self.ctx.framebuffer(color_attachments=[tex])
        p = self.prog(src(fs))
        for k, val in uni.items():
            p[k].value = val
        self.full(p, fbo)
        a = np.frombuffer(fbo.read(components=4, dtype="f4"), np.float32).reshape(size[1], size[0], 4)
        fbo.release(); tex.release()
        return a

    def view(self, pixel, bind=lambda p: None):
        p = self.prog(src(pixel))

        def render(yaw, pitch):
            self.set_cam(p, yaw, pitch); bind(p); self.full(p, self.fbo); return self.read(self.fbo)
        return render, lambda: (bind(p), self.full(p, self.fbo))

    def m_pixel(self, ss=1):
        p = self.prog(src(gm.PIX_SPHERE))

        def render(yaw, pitch):
            self.set_cam(p, yaw, pitch, ss=ss); self.full(p, self.fbo); return self.read(self.fbo)
        return render, lambda: self.full(p, self.fbo), {}

    def pano_tex(self, lin):   # lin: rows bottom-up (GL), RGBA float
        t = self.ctx.texture((lin.shape[1], lin.shape[0]), 4, np.ascontiguousarray(lin).tobytes(), dtype="f4")
        t.repeat_x, t.repeat_y = True, False
        return t

    def m_png(self):
        from PIL import Image
        s = np.asarray(Image.open(PNG).convert("RGB"), np.float32) / 255
        lin = np.where(s <= 0.04045, s / 12.92, np.power((s + 0.055) / 1.055, 2.4))
        lin = np.concatenate([lin, np.ones_like(lin[..., :1])], 2)[::-1]   # PNG top row = v 1
        t = self.pano_tex(lin)
        r, d = self.view(gm.PIX_EQ_TEX, lambda p: t.use(0))
        return r, d, {"bytes_rgba8": 4096 * 2048 * 4}

    def m_bake_equirect(self, n):
        lin = srgb8_linear(self.bake(gm.BAKE_EQ, (n, n // 2), BakeRes=(n, n // 2)))
        t = self.pano_tex(lin)
        r, d = self.view(gm.PIX_EQ_TEX, lambda p: t.use(0))
        return r, d, {"bytes_rgba8": n * n // 2 * 4}

    def cube(self, fs, n):
        faces = [srgb8_linear(self.bake(fs, (n, n), BakeRes=(n, n), Fx=fx, Fy=fy, Fz=fz)) for fx, fy, fz in FACES]
        return self.ctx.texture_cube((n, n), 4, b"".join(np.ascontiguousarray(f).tobytes() for f in faces), dtype="f4")

    def m_bake_cube(self, n):
        c = self.cube(gm.BAKE_FACE, n)
        r, d = self.view(gm.PIX_CUBE, lambda p: c.use(0))
        return r, d, {"bytes_rgba8": n * n * 6 * 4}

    def m_hybrid(self, n):
        c = self.cube(BAKE_FACE_BASE, n)
        r, d = self.view(PIX_HYBRID, lambda p: c.use(0))
        return r, d, {"bytes_rgba8": n * n * 6 * 4}

    def m_hybrid_cells(self, n):
        c = self.cube(BAKE_FACE_SOFT, n)
        r, d = self.view(PIX_HYBRID_CELLS, lambda p: c.use(0))
        return r, d, {"bytes_rgba8": n * n * 6 * 4}

    def m_cells_live(self, n):
        c = self.cube(BAKE_FACE_NOCELLS, n)
        r, d = self.view(PIX_CELLS_ONLY_LIVE, lambda p: c.use(0))
        return r, d, {"bytes_rgba8": n * n * 6 * 4}

    def m_cells_only_cost(self):
        r, d = self.view("void main(){ o = vec4(hsCells(rayAt(gl_FragCoord.xy)), 1.0); }")
        return r, d, {}

    def m_stars_only_cost(self):
        r, d = self.view("void main(){ o = vec4(hsStars(rayAt(gl_FragCoord.xy)), 1.0); }")
        return r, d, {}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default=None)
    ap.add_argument("--json", default=None)
    a = ap.parse_args()
    B = HBench()
    ref_render, _, _ = B.m_pixel(ss=2)
    refs = {name: gm.to8(ref_render(y, p)) for name, y, p in VIEWS}
    methods = [("per-pixel", B.m_pixel, ()), ("shipped-png-4096", B.m_png, ()),
               ("bake-equirect-2048", B.m_bake_equirect, (2048,)), ("bake-equirect-4096", B.m_bake_equirect, (4096,)),
               ("bake-cube-512", B.m_bake_cube, (512,)), ("bake-cube-1024", B.m_bake_cube, (1024,)),
               ("bake-cube-1536", B.m_bake_cube, (1536,)),
               ("hybrid-cube-256", B.m_hybrid, (256,)), ("hybrid-cube-512", B.m_hybrid, (512,)),
               ("hybrid-cells-cube-128", B.m_hybrid_cells, (128,)), ("hybrid-cells-cube-256", B.m_hybrid_cells, (256,)),
               ("cells-live-cube-256", B.m_cells_live, (256,)), ("cells-live-cube-512", B.m_cells_live, (512,)),
               ("cells-live-cube-1024", B.m_cells_live, (1024,)),
               ("stars only (cost)", B.m_stars_only_cost, ()), ("cell net only (cost)", B.m_cells_only_cost, ())]
    rows, sheets = [], {}
    for name, fn, args in methods:
        render, draw, info = fn(*args)
        render(*VIEWS[0][1:])   # sets the camera first: timing a pass with unset uniforms measures a degenerate ray
        ms = B.timed(draw, reps=6)
        imgs, errs = [], {}
        for vname, y, p in VIEWS:
            img = gm.to8(render(y, p)); imgs.append(img)
            dd = np.abs(img.astype(int) - refs[vname].astype(int)).max(axis=2)
            errs[vname] = (float(dd.mean()), float(np.percentile(dd, 99)), float(np.percentile(dd, 99.9)))
        row = {"method": name, "ms_1280x720_llvmpipe": round(ms, 2), **info,
               "mean_err": round(np.mean([e[0] for e in errs.values()]), 2),
               "p99_err": round(np.mean([e[1] for e in errs.values()]), 1),
               "p999_err": round(np.mean([e[2] for e in errs.values()]), 1),
               "pole_mean_err": round(np.mean([errs["north pole"][0], errs["south pole"][0]]), 2)}
        rows.append(row); sheets[name] = imgs
        print(json.dumps(row), flush=True)
    if a.json:
        Path(a.json).write_text(json.dumps(rows, indent=1))
    if a.out:
        from PIL import Image, ImageDraw
        os.makedirs(a.out, exist_ok=True)
        show = ["shipped-png-4096", "bake-cube-512", "cells-live-cube-256", "cells-live-cube-512"]
        cw, ch = 400, 260
        sheet = Image.new("RGB", (cw * len(VIEWS), ch * (len(show) + 1)))
        for r, (n, imgs) in enumerate([("reference: per-pixel 2x2 SS", [refs[v[0]] for v in VIEWS])] + [(s, sheets[s]) for s in show]):
            for c, img in enumerate(imgs):   # 1:1 crops: stars are only visible unscaled
                im = Image.fromarray(img[H // 2 - ch // 2:H // 2 + ch // 2, W // 2 - cw // 2:W // 2 + cw // 2]); d = ImageDraw.Draw(im)
                lab = f"{n} | {VIEWS[c][0]} (1:1)"
                d.rectangle((0, 0, len(lab) * 6 + 6, 13), fill=(0, 0, 0)); d.text((3, 1), lab, fill=(255, 255, 0))
                sheet.paste(im, (c * cw, r * ch))
        sheet.save(os.path.join(a.out, "hypersea_methods_sheet.png"))
        print(os.path.join(a.out, "hypersea_methods_sheet.png"))


if __name__ == "__main__":
    main()
