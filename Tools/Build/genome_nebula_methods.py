#!/usr/bin/env python3
"""Bench: ways to PRODUCE the genome nebula sky, scored against one pole-free reference.

The nebula (Assets/_Graphics/Materials/Shaders/GenomeNebula/GenomeNebula.hlsl) is two fbm fields and a double domain
warp. This bench renders the same nebula by every production method worth trying on a phone, in real GLSL on a real
GL pipeline (moderngl; llvmpipe in a cloud container), with real texture filtering, cubemaps and 3D textures, and
scores each against the per-pixel procedural SPHERE version supersampled 2x2:

  procedural-equirect   the lab's own mapping, per pixel (pinches at the poles; a different pattern, so no error)
  procedural-sphere     3D noise over the view direction, per pixel (the reference, 1x)
  bake-equirect-N       the sphere sky baked once into an NxN/2 panorama (StaticSkyPanorama's layout)
  bake-cube-N           baked once into a cubemap of N^2 faces
  noise3d-N             the two fbm fields baked into one RG8 N^3 tileable 3D texture; the warp runs live per pixel
  dome-L                evaluated per VERTEX on an icosphere of level L, interpolated across triangles
  lowres-1/K            procedural sphere drawn at 1/K resolution, bilinear upscale

Usage:  DISPLAY=:99 python3 Tools/Build/genome_nebula_methods.py [--out DIR] [--json OUT.json]
Timing is llvmpipe (CPU) milliseconds for one 1280x720 pass: it ranks ALU against fetches, it is not a phone.
"""
import argparse
import json
import math
import os
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import genome_nebula_preview as gp  # noqa: E402  (material values)

W, H = 1280, 720
FOV = math.radians(87)
VIEWS = [("horizon", 30, 10), ("below", 120, -25), ("north pole", 200, 86), ("south pole", 300, -88)]
PERIOD0, PERIOD1 = 2.0, 4.0   # base-lattice cells per tile for field 0 (freq 3) and field 1 (freq 6): same tile length

COMMON = r"""
uniform vec4 ColA0, ColB0, Noise0, ColA1, ColB1, Noise1, Warp, FogColor, Grade, Drift;
uniform float Gamma0, Gamma1, SkyIntensity, FogMix, SphereScale, Per0, Per1, Time;
float h31(vec3 p3){ p3 = fract(p3*0.1031); p3 += dot(p3, p3.zyx+31.32); return fract((p3.x+p3.y)*p3.z); }
float vn3p(vec3 p, float per){ vec3 i = floor(p), f = p-i; vec3 u = f*f*(3.0-2.0*f);
  #define HP(o) h31(mod(i+o, per))
  return mix(mix(mix(HP(vec3(0,0,0)),HP(vec3(1,0,0)),u.x), mix(HP(vec3(0,1,0)),HP(vec3(1,1,0)),u.x), u.y),
             mix(mix(HP(vec3(0,0,1)),HP(vec3(1,0,1)),u.x), mix(HP(vec3(0,1,1)),HP(vec3(1,1,1)),u.x), u.y), u.z); }
// periodic fbm: tile length per/n.x in p, every octave
float fbm3(vec3 p, vec4 n, float per){ float s=0.0, a=1.0, w=0.0, fq=n.x, pk=per;
  for(int i=0;i<8;i++){ if(i>=int(n.y)) break; s += a*vn3p(p*fq + n.w*0.013 + float(i)*17.0, pk); w+=a; a*=n.z; fq*=2.0; pk*=2.0; }
  return s/w; }
vec3 ramp(float x, float g, vec4 a, vec4 b){ x = pow(clamp((x-0.5)*1.6+0.5, 0.0, 1.0), g); return mix(a.xyz, b.xyz, x); }
vec3 aces(vec3 c){ return c*(2.51*c+0.03)/(c*(2.43*c+0.59)+0.14); }
vec3 finish(vec3 sky){ vec3 c = mix(sky*SkyIntensity, FogColor.xyz, FogMix);
  c = aces(c*Grade.x); float l = dot(c, vec3(0.299,0.587,0.114)); c = mix(vec3(l), c, Grade.y); return clamp((c-0.5)*Grade.z+0.5, 0.0, 1.0); }
float F0(vec3 p);  float F1(vec3 p);   // the two fields: procedural or texture, defined per method
vec3 nebula(vec3 dir){ vec3 p = dir*SphereScale; vec3 sl = vec3(Drift.x, Drift.y, Drift.x)*Time;
  vec3 b = ramp(F1(p+sl), Gamma1, ColA1, ColB1); vec3 p3 = p + (b-0.5)*(2.0*Warp.y);
  vec3 b2 = ramp(F1(p3+sl), Gamma1, ColA1, ColB1); vec3 p2 = p3 + (b2-0.5)*(2.0*Warp.x);
  return finish(ramp(F0(p2 + vec3(Drift.z*Time,0,0)), Gamma0, ColA0, ColB0)); }
"""
PROC_FIELDS = "float F0(vec3 p){ return fbm3(p, Noise0, Per0); } float F1(vec3 p){ return fbm3(p, Noise1, Per1); }\n"
TEX_FIELDS = ("uniform sampler3D Noise3D; uniform float L;\n"
              "float F0(vec3 p){ return texture(Noise3D, p/L).r; } float F1(vec3 p){ return texture(Noise3D, p/L).g; }\n")
# the same fetch with the hardware's linear weights bent into smoothstep (C1 between texels, a few ALU, one fetch)
TEX_FIELDS_SMOOTH = ("uniform sampler3D Noise3D; uniform float L; uniform float N3;\n"
              "vec3 sm(vec3 q){ q = q*N3 + 0.5; vec3 i = floor(q), f = q - i; f = f*f*(3.0-2.0*f); return (i + f - 0.5)/N3; }\n"
              "float F0(vec3 p){ return texture(Noise3D, sm(p/L)).r; } float F1(vec3 p){ return texture(Noise3D, sm(p/L)).g; }\n")

CAM = r"""
uniform vec2 Res; uniform mat3 Cam; uniform float TanHalf; uniform int SS;
vec3 rayAt(vec2 frag){ vec2 uv = frag/Res*2.0-1.0; uv.x *= Res.x/Res.y; return normalize(Cam*vec3(uv*TanHalf, 1.0)); }
"""
VS_FULL = "#version 330\nin vec2 p; void main(){ gl_Position = vec4(p,0,1); }"


def fs_view(body_fields, pixel):
    return "#version 330\n" + COMMON + body_fields + CAM + "out vec4 o;\n" + pixel


PIX_SPHERE = r"""void main(){ vec3 c = vec3(0.0); for(int y=0;y<SS;y++) for(int x=0;x<SS;x++){
  c += nebula(rayAt(gl_FragCoord.xy - 0.5 + (vec2(x,y)+0.5)/float(SS))); } o = vec4(c/float(SS*SS),1.0); }"""

# the lab's equirect mapping on 2D periodic value noise (genome-exact; GenomeNebula.hlsl gn_sky_uv)
LAB_EQ = r"""
float h21(vec2 p){ vec3 p3 = fract(vec3(p.xyx)*0.1031); p3 += dot(p3, p3.yzx+33.33); return fract((p3.x+p3.y)*p3.z); }
float vn2p(vec2 p, float per, float sd){ vec2 i = floor(p), fr = fract(p); vec2 u = fr*fr*(3.0-2.0*fr);
  float a = h21(mod(i, per)+sd), b = h21(mod(i+vec2(1,0), per)+sd), c = h21(mod(i+vec2(0,1), per)+sd), d = h21(mod(i+vec2(1,1), per)+sd);
  return mix(mix(a,b,u.x), mix(c,d,u.x), u.y); }
float fbm2(vec2 uv, vec4 n){ float s=0.0,a=1.0,w=0.0,fq=n.x; for(int i=0;i<8;i++){ if(i>=int(n.y)) break; s+=a*vn2p(uv*fq,fq,n.w+float(i)*17.0); w+=a; a*=n.z; fq*=2.0; } return s/w; }
vec3 t0(vec2 uv){ return ramp(fbm2(fract(uv), Noise0), Gamma0, ColA0, ColB0); }
vec3 t1(vec2 uv){ return ramp(fbm2(fract(uv), Noise1), Gamma1, ColA1, ColB1); }
void main(){ vec3 rd = rayAt(gl_FragCoord.xy); vec2 uv = vec2(atan(rd.z, rd.x)/6.2831853+0.5, asin(clamp(rd.y,-1.0,1.0))/3.14159265+0.5);
  vec3 b = t1(uv); vec2 q = uv + (b.rg-0.5)*2.0*Warp.y; vec3 b2 = t1(q); q += (b2.rg-0.5)*2.0*Warp.x; o = vec4(finish(t0(q)),1.0); }"""

PIX_EQ_TEX = r"""uniform sampler2D Pano; void main(){ vec3 d = rayAt(gl_FragCoord.xy);
  vec2 uv = vec2(0.5 - atan(d.z, d.x)*(0.5/3.14159265), 1.0 - acos(clamp(d.y,-1.0,1.0))/3.14159265);
  o = vec4(textureLod(Pano, uv, 0.0).rgb, 1.0); }"""
PIX_CUBE = "uniform samplerCube Cube; void main(){ o = vec4(texture(Cube, rayAt(gl_FragCoord.xy)).rgb, 1.0); }"
PIX_UPSCALE = "uniform sampler2D Low; void main(){ o = vec4(texture(Low, gl_FragCoord.xy/Res).rgb, 1.0); }"

# bake passes: equirect panorama, one cube face, one stack of 3D-texture slices
BAKE_EQ = r"""uniform vec2 BakeRes; void main(){ vec2 uv = gl_FragCoord.xy/BakeRes; float lon=(0.5-uv.x)*6.2831853, lat=(1.0-uv.y)*3.14159265;
  o = vec4(nebula(vec3(sin(lat)*cos(lon), cos(lat), sin(lat)*sin(lon))), 1.0); }"""
BAKE_FACE = r"""uniform vec2 BakeRes; uniform vec3 Fx, Fy, Fz; void main(){ vec2 uv = gl_FragCoord.xy/BakeRes*2.0-1.0;
  o = vec4(nebula(normalize(Fz + Fx*uv.x + Fy*uv.y)), 1.0); }"""
BAKE_3D = r"""uniform float N; void main(){ float x = floor(gl_FragCoord.x), yz = floor(gl_FragCoord.y);
  float y = mod(yz, N), z = floor(yz/N); vec3 q = (vec3(x,y,z)+0.5)/N;   // texel centres, as GL samples them
  o = vec4(fbm3(q*(Per0/Noise0.x), Noise0, Per0), fbm3(q*(Per1/Noise1.x), Noise1, Per1), 0.0, 1.0); }"""

DOME_VS = "#version 330\n" + COMMON + PROC_FIELDS + r"""
uniform mat4 VP; in vec3 pos; out vec3 col; void main(){ col = nebula(normalize(pos)); gl_Position = VP*vec4(pos, 1.0); }"""
DOME_FS = "#version 330\nin vec3 col; out vec4 o; void main(){ o = vec4(col, 1.0); }"


def cam_basis(yaw, pitch):
    y, p = math.radians(yaw), math.radians(pitch)
    f = np.array([math.cos(p) * math.sin(y), math.sin(p), math.cos(p) * math.cos(y)])
    r = np.cross([0, 1, 0], f); r /= np.linalg.norm(r)
    u = np.cross(f, r)
    return r, u, f


def icosphere(level):
    t = (1 + 5 ** 0.5) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    v = [np.array(x, float) / np.linalg.norm(x) for x in v]
    f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
         (8, 6, 7), (9, 8, 1)]
    for _ in range(level):
        cache, nf = {}, []

        def mid(a, b):
            k = (min(a, b), max(a, b))
            if k not in cache:
                m = v[a] + v[b]
                v.append(m / np.linalg.norm(m))
                cache[k] = len(v) - 1
            return cache[k]
        for a, b, c in f:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            nf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        f = nf
    return np.array(v, "f4"), np.array(f, "i4")


class Bench:
    def __init__(self):
        import moderngl
        self.mgl = moderngl
        self.ctx = moderngl.create_standalone_context()
        self.ctx.enable_direct(0x884F)  # GL_TEXTURE_CUBE_MAP_SEAMLESS (always on in GLES3/Vulkan/Metal)
        self.vbo = self.ctx.buffer(np.array([-1, -1, 3, -1, -1, 3], "f4").tobytes())
        self.vals = gp.material_values()
        self.fbo = self.ctx.framebuffer(color_attachments=[self.ctx.texture((W, H), 4, dtype="f4")])

    def prog(self, fs, vs=VS_FULL):
        return self.ctx.program(vertex_shader=vs, fragment_shader=fs)

    def set_common(self, p, t=0.0):
        v = self.vals
        for n in ("ColA0", "ColB0", "Noise0", "ColA1", "ColB1", "Noise1", "Warp", "FogColor", "Grade", "Drift"):
            if n in p:
                p[n].value = tuple(v["_" + n])
        for n in ("Gamma0", "Gamma1", "SkyIntensity", "FogMix", "SphereScale"):
            if n in p:
                p[n].value = v["_" + n][0]
        for n, val in (("Per0", PERIOD0), ("Per1", PERIOD1), ("Time", t)):
            if n in p:
                p[n].value = val

    def set_cam(self, p, yaw, pitch, res=(W, H), ss=1):
        r, u, f = cam_basis(yaw, pitch)
        for n, val in (("Res", res), ("Cam", tuple(np.stack([r, u, f], 1).T.flatten())), ("TanHalf", math.tan(FOV / 2)),
                       ("SS", ss)):
            if n in p:
                p[n].value = val

    def full(self, p, fbo):
        fbo.use()
        self.ctx.viewport = (0, 0) + fbo.size   # set on the BOUND target only (moderngl keeps one per framebuffer)
        fbo.clear()
        self.ctx.simple_vertex_array(p, self.vbo, "p").render()

    def read(self, fbo, size=(W, H)):
        a = np.frombuffer(fbo.read(components=3, dtype="f4"), np.float32).reshape(size[1], size[0], 3)[::-1]
        return a

    def timed(self, fn, reps=3):
        fn(); self.ctx.finish()
        t0 = time.perf_counter()
        for _ in range(reps):
            fn()
        self.ctx.finish()
        return (time.perf_counter() - t0) / reps * 1000

    # ---- methods: each returns (render(yaw,pitch)->image, ms per view, extra info) ----
    def m_sphere(self, ss=1):
        p = self.prog(fs_view(PROC_FIELDS, PIX_SPHERE)); self.set_common(p)

        def render(yaw, pitch):
            self.set_cam(p, yaw, pitch, ss=ss); self.full(p, self.fbo); return self.read(self.fbo)
        return render, lambda: self.full(p, self.fbo), {}

    def m_lab_equirect(self):
        p = self.prog(fs_view("float F0(vec3 p){return 0.0;} float F1(vec3 p){return 0.0;}\n", LAB_EQ)); self.set_common(p)

        def render(yaw, pitch):
            self.set_cam(p, yaw, pitch); self.full(p, self.fbo); return self.read(self.fbo)
        return render, lambda: self.full(p, self.fbo), {}

    def bake_tex2d(self, fs, size, dtype="f1", **uni):
        tex = self.ctx.texture(size, 4, dtype=dtype)
        fbo = self.ctx.framebuffer(color_attachments=[tex])
        p = self.prog("#version 330\n" + COMMON + PROC_FIELDS + "out vec4 o;\n" + fs); self.set_common(p)
        for k, val in uni.items():
            p[k].value = val
        t0 = time.perf_counter(); self.full(p, fbo); self.ctx.finish(); ms = (time.perf_counter() - t0) * 1000
        return tex, fbo, ms

    def m_bake_equirect(self, n):
        tex, fbo, bake_ms = self.bake_tex2d(BAKE_EQ, (n, n // 2), BakeRes=(n, n // 2))
        tex.repeat_x, tex.repeat_y = True, False
        p = self.prog(fs_view(PROC_FIELDS, PIX_EQ_TEX)); self.set_common(p)

        def render(yaw, pitch):
            self.set_cam(p, yaw, pitch); tex.use(0); p["Pano"].value = 0; self.full(p, self.fbo); return self.read(self.fbo)
        return render, lambda: (tex.use(0), self.full(p, self.fbo)), {"bake_ms": bake_ms, "bytes": n * n // 2 * 4}

    def m_bake_cube(self, n):
        # GL cube face order +X -X +Y -Y +Z -Z, and the face (s,t) axes from the GL spec table
        faces = [((0, 0, -1), (0, -1, 0), (1, 0, 0)), ((0, 0, 1), (0, -1, 0), (-1, 0, 0)),
                 ((1, 0, 0), (0, 0, 1), (0, 1, 0)), ((1, 0, 0), (0, 0, -1), (0, -1, 0)),
                 ((1, 0, 0), (0, -1, 0), (0, 0, 1)), ((-1, 0, 0), (0, -1, 0), (0, 0, -1))]
        data, bake_ms = [], 0.0
        for fx, fy, fz in faces:
            tex, fbo, ms = self.bake_tex2d(BAKE_FACE, (n, n), BakeRes=(n, n), Fx=fx, Fy=fy, Fz=fz)
            bake_ms += ms
            data.append(fbo.read(components=4, dtype="f1"))
            fbo.release(); tex.release()
        cube = self.ctx.texture_cube((n, n), 4, b"".join(data), dtype="f1")
        p = self.prog(fs_view(PROC_FIELDS, PIX_CUBE)); self.set_common(p)

        def render(yaw, pitch):
            self.set_cam(p, yaw, pitch); cube.use(0); p["Cube"].value = 0; self.full(p, self.fbo); return self.read(self.fbo)
        return render, lambda: (cube.use(0), self.full(p, self.fbo)), {"bake_ms": bake_ms, "bytes": n * n * 6 * 4}

    def m_noise3d(self, n, smooth=False, half=False):
        tex, fbo, bake_ms = self.bake_tex2d(BAKE_3D, (n, n * n), N=float(n), dtype="f2" if half else "f1")
        if half:
            raw = np.frombuffer(fbo.read(components=4, dtype="f2"), np.float16).reshape(n, n, n, 4)[..., :2]
        else:
            raw = np.frombuffer(fbo.read(components=4, dtype="f1"), np.uint8).reshape(n, n, n, 4)[..., :2]  # z, y, x
        t3 = self.ctx.texture3d((n, n, n), 2, np.ascontiguousarray(raw).tobytes(), dtype="f2" if half else "f1")
        t3.repeat_x = t3.repeat_y = t3.repeat_z = True
        p = self.prog(fs_view(TEX_FIELDS_SMOOTH if smooth else TEX_FIELDS, PIX_SPHERE)); self.set_common(p)
        p["L"].value = PERIOD0 / self.vals["_Noise0"][0]
        if smooth:
            p["N3"].value = float(n)

        def render(yaw, pitch):
            self.set_cam(p, yaw, pitch); t3.use(0); p["Noise3D"].value = 0; self.full(p, self.fbo); return self.read(self.fbo)
        return render, lambda: (t3.use(0), self.full(p, self.fbo)), {"bake_ms": bake_ms, "bytes": n ** 3 * (4 if half else 2)}

    def m_dome(self, level):
        v, f = icosphere(level)
        p = self.ctx.program(vertex_shader=DOME_VS, fragment_shader=DOME_FS); self.set_common(p)
        vao = self.ctx.vertex_array(p, [(self.ctx.buffer((v * 10).astype("f4").tobytes()), "3f", "pos")],
                                    self.ctx.buffer(f.tobytes()))

        def draw(yaw, pitch):
            r, u, fw = cam_basis(yaw, pitch)
            view = np.eye(4); view[:3, :3] = np.stack([r, u, -fw])     # camera looks down -z in GL
            t = math.tan(FOV / 2); a = W / H; near, far = 0.1, 100.0
            proj = np.array([[1 / (t * a), 0, 0, 0], [0, 1 / t, 0, 0],
                             [0, 0, -(far + near) / (far - near), -2 * far * near / (far - near)], [0, 0, -1, 0]])
            p["VP"].value = tuple((proj @ view).T.flatten())
            self.fbo.use(); self.ctx.viewport = (0, 0, W, H); self.fbo.clear(); vao.render()

        def render(yaw, pitch):
            draw(yaw, pitch); return self.read(self.fbo)
        return render, lambda: draw(30, 10), {"vertices": len(v)}

    def m_lowres(self, k):
        lw, lh = W // k, H // k
        low = self.ctx.texture((lw, lh), 4, dtype="f1"); low.filter = (self.mgl.LINEAR, self.mgl.LINEAR)
        lfbo = self.ctx.framebuffer(color_attachments=[low])
        p = self.prog(fs_view(PROC_FIELDS, PIX_SPHERE)); self.set_common(p)
        up = self.prog(fs_view(PROC_FIELDS, PIX_UPSCALE)); self.set_common(up)

        def draw(yaw, pitch):
            self.set_cam(p, yaw, pitch, res=(lw, lh)); self.full(p, lfbo)
            self.set_cam(up, yaw, pitch); low.use(0); up["Low"].value = 0
            self.full(up, self.fbo)

        def render(yaw, pitch):
            draw(yaw, pitch); return self.read(self.fbo)
        return render, lambda: draw(30, 10), {"bytes": lw * lh * 4}


def to8(a):
    s = np.where(a <= 0.0031308, a * 12.92, 1.055 * np.power(np.clip(a, 0, 1), 1 / 2.4) - 0.055)
    return np.clip(s * 255 + 0.5, 0, 255).astype(np.uint8)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default=None, help="write per-method view sheets here")
    ap.add_argument("--json", default=None)
    a = ap.parse_args()
    B = Bench()
    ref_render, _, _ = B.m_sphere(ss=2)
    refs = {name: to8(ref_render(y, p)) for name, y, p in VIEWS}

    methods = [("procedural-equirect (lab)", B.m_lab_equirect, ()), ("procedural-sphere", B.m_sphere, ()),
               ("bake-equirect-2048", B.m_bake_equirect, (2048,)), ("bake-equirect-4096", B.m_bake_equirect, (4096,)),
               ("bake-cube-256", B.m_bake_cube, (256,)), ("bake-cube-512", B.m_bake_cube, (512,)),
               ("bake-cube-1024", B.m_bake_cube, (1024,)),
               ("noise3d-32", B.m_noise3d, (32,)), ("noise3d-64", B.m_noise3d, (64,)), ("noise3d-128", B.m_noise3d, (128,)),
               ("noise3d-64-smooth", B.m_noise3d, (64, True)),
               ("noise3d-32-half", B.m_noise3d, (32, False, True)), ("noise3d-64-half", B.m_noise3d, (64, False, True)),
               ("noise3d-96-half", B.m_noise3d, (96, False, True)), ("noise3d-128-half", B.m_noise3d, (128, False, True)),
               ("dome-5", B.m_dome, (5,)), ("dome-6", B.m_dome, (6,)), ("dome-7", B.m_dome, (7,)),
               ("lowres-1/2", B.m_lowres, (2,)), ("lowres-1/4", B.m_lowres, (4,))]
    rows = []
    sheets = {}
    for name, fn, args in methods:
        render, draw, info = fn(*args)
        render(*VIEWS[0][1:])   # sets the camera first: timing a pass with unset uniforms measures a degenerate ray
        ms = B.timed(draw, reps=6)
        errs = {}
        imgs = []
        for vname, y, p in VIEWS:
            img = to8(render(y, p)); imgs.append(img)
            d = np.abs(img.astype(int) - refs[vname].astype(int)).max(axis=2)
            errs[vname] = (float(d.mean()), float(np.percentile(d, 99)))
        lab = name.startswith("procedural-equirect")
        row = {"method": name, "ms_1280x720_llvmpipe": round(ms, 2), **{k: v for k, v in info.items()},
               "mean_err": None if lab else round(np.mean([e[0] for e in errs.values()]), 2),
               "p99_err": None if lab else round(np.mean([e[1] for e in errs.values()]), 1),
               "pole_mean_err": None if lab else round(np.mean([errs["north pole"][0], errs["south pole"][0]]), 2)}
        rows.append(row)
        sheets[name] = imgs
        print(json.dumps(row), flush=True)
    if a.json:
        Path(a.json).write_text(json.dumps(rows, indent=1))
    if a.out:
        from PIL import Image, ImageDraw
        os.makedirs(a.out, exist_ok=True)
        tw, th = 320, 180
        allrows = [("reference (sphere, 2x2 SS)", [refs[v[0]] for v in VIEWS])] + list(sheets.items())
        sheet = Image.new("RGB", (tw * len(VIEWS), th * len(allrows)))
        for r, (name, imgs) in enumerate(allrows):
            for c, img in enumerate(imgs):
                im = Image.fromarray(img).resize((tw, th), Image.BILINEAR)
                d = ImageDraw.Draw(im)
                label = f"{name} | {VIEWS[c][0]}"
                d.rectangle((0, 0, len(label) * 6 + 6, 13), fill=(0, 0, 0)); d.text((3, 1), label, fill=(255, 255, 0))
                sheet.paste(im, (c * tw, r * th))
        sheet.save(os.path.join(a.out, "methods_sheet.png"))
        # 1:1 crops at the north pole for the methods that differ most there
        crops = [("reference", refs["north pole"])] + [(n, sheets[n][2]) for n in
                 ("procedural-equirect (lab)", "bake-equirect-2048", "bake-cube-512", "noise3d-64", "noise3d-96-half", "dome-6", "lowres-1/4")]
        cs = Image.new("RGB", (400 * 4, 300 * 2))
        for k, (n, img) in enumerate(crops):
            im = Image.fromarray(img[H // 2 - 150:H // 2 + 150, W // 2 - 200:W // 2 + 200]); d = ImageDraw.Draw(im)
            d.rectangle((0, 0, len(n) * 6 + 6, 13), fill=(0, 0, 0)); d.text((3, 1), n + " (north pole, 1:1)", fill=(255, 255, 0))
            cs.paste(im, ((k % 4) * 400, (k // 4) * 300))
        cs.save(os.path.join(a.out, "pole_crops.png"))
        print(os.path.join(a.out, "methods_sheet.png"))


if __name__ == "__main__":
    main()
