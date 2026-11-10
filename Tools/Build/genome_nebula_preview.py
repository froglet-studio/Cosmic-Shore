#!/usr/bin/env python3
"""Preview, check and price the genome nebula skybox WITHOUT Unity.

The sky is Assets/_Graphics/Materials/Shaders/GenomeNebula/GenomeNebula.hlsl, a line-for-line port of the sky the
Amoebius genome search evolved (seed amoebius_like/minimal__tuned). This tool compiles that include as C++ (the same
trick Tools/Build/bake_static_skybox.py plays on HyperSeaSkybox.shader) with the material's values and:

  --pano OUT.png [--size 2048] [--mode equirect|sphere]   the equirect panorama (StaticSkyPanorama layout)
  --view OUT.png [--mode ...] [--yaw 0 --pitch 10 --fov 87]  a 1280x720 camera view
  --hypersea-view OUT.png [...]                          the same camera on the shipped HyperSea sky
  --bench                                                CPU ns/pixel: genome equirect, genome sphere, HyperSea
  --seam                                                 max colour step across u=0/1 and near the poles
  --parity LAB_AGENOME_DIR                               compare with the lab's own GLSL (needs moderngl + Xvfb)

The CPU timing is a proxy for GPU ALU cost, not a frame time: run the shaders on a device to know.
"""
import argparse
import os
import re
import subprocess
import sys
import tempfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
INC = ROOT / "Assets/_Graphics/Materials/Shaders/GenomeNebula/GenomeNebula.hlsl"
SHADER = ROOT / "Assets/_Graphics/Materials/Shaders/GenomeNebula/GenomeNebulaSkybox.shader"
MAT = ROOT / "Assets/_Graphics/Skyboxes/GenomeNebulaSkybox.mat"
SHIM = ROOT / "Tools/Build/genome_nebula_harness/shim.h"

INTRINSICS = ("floor", "frac", "saturate", "sin", "cos", "dot", "lerp", "pow", "atan2", "asin", "clamp", "normalize")


def transpile_include() -> str:
    s = INC.read_text()
    s = s.replace("[unroll]", "")
    s = re.sub(r"\.xyz\b", ".xyz_()", s)
    s = re.sub(r"\b(" + "|".join(INTRINSICS) + r")\s*\(", r"h_\1(", s)
    return s


def material_values() -> dict:
    """Vector/Float values: the .mat when it has them, else the shader's Properties defaults."""
    vals = {}
    for name, kind, default in re.findall(
            r"^\s*(?:\[[^\]]*\]\s*)*(_\w+)\s*\(\s*\"[^\"]*\"\s*,\s*(Vector|Float|Range\([^)]*\))\s*\)\s*=\s*([^\n]+)",
            SHADER.read_text(), re.M):
        nums = [float(x) for x in re.findall(r"-?[\d.]+(?:[eE][-+]?\d+)?", default)]
        vals[name] = (nums + [0, 0, 0, 0])[:4] if kind == "Vector" else nums[:1]
    if MAT.exists():
        t = MAT.read_text()
        for n, v in re.findall(r"^\s*- (_\w+): (-?[\d.eE+-]+)\s*$", t, re.M):
            if n in vals:
                vals[n] = [float(v)]
        for n, *v in re.findall(r"^\s*- (_\w+): \{r: (\S+), g: (\S+), b: (\S+), a: (\S+)\}", t, re.M):
            if n in vals:
                vals[n] = [float(x) for x in v]
    return vals


def inits(vals: dict) -> str:
    declared = INC.read_text()
    out = []
    for n, v in vals.items():
        if not re.search(r"\b" + n + r"\b", declared):   # pass-only properties (_Slice, ...) live in the .shader
            continue
        out.append(f"  {n} = float4({v[0]}f,{v[1]}f,{v[2]}f,{v[3]}f);" if len(v) == 4 else f"  {n} = {v[0]}f;")
    return "\n".join(out)


MAIN = r"""
#include <cstdio>
#include <cstdlib>
#include <cstdint>
#include <vector>
#include <thread>
#include <chrono>
static float lin2srgb(float c){ c = c<0?0:(c>1?1:c); return c<=0.0031308f? c*12.92f : 1.055f*std::pow(c,1/2.4f)-0.055f; }
static float3 sky(float3 d, float t, int mode){ return mode==1 ? gn_finish(gn_sky_sphere(d,t)) : gn_finish(gn_sky_uv(gn_equirect(d),t)); }
int main(int argc, char** argv){
  init_material();
  int W=atoi(argv[1]), H=atoi(argv[2]), kind=atoi(argv[3]), mode=atoi(argv[4]);
  float yaw=atof(argv[5]), pitch=atof(argv[6]), fov=atof(argv[7]), t=atof(argv[8]); int threads=atoi(argv[9]);
  const float PI=3.14159265358979f;
  std::vector<uint8_t> px((size_t)W*H*3);
  float cy=std::cos(yaw), sy=std::sin(yaw), cp=std::cos(pitch), sp=std::sin(pitch), th=std::tan(fov*0.5f);
  auto dirAt=[&](int i,int r)->float3{
    if(kind==0){ float u=(i+0.5f)/W, v=1.0f-(r+0.5f)/H; float lon=(0.5f-u)*2*PI, lat=(1-v)*PI;
      return float3(std::sin(lat)*std::cos(lon), std::cos(lat), std::sin(lat)*std::sin(lon)); }
    float x=((i+0.5f)/W*2-1)*th*W/H, y=(1-(r+0.5f)/H*2)*th;
    float3 f(cp*sy, sp, cp*cy), rt(cy,0,-sy), up(-sp*sy, cp, -sp*cy);
    return h_normalize(f + rt*x + up*y); };
  auto work=[&](int y0,int st){ for(int r=y0;r<H;r+=st) for(int i=0;i<W;i++){ float3 c=sky(dirAt(i,r),t,mode);
    size_t o=((size_t)r*W+i)*3; px[o]=(uint8_t)(lin2srgb(c.x)*255+.5f); px[o+1]=(uint8_t)(lin2srgb(c.y)*255+.5f); px[o+2]=(uint8_t)(lin2srgb(c.z)*255+.5f);} };
  auto t0=std::chrono::steady_clock::now();
  std::vector<std::thread> pool; for(int k=0;k<threads;k++) pool.emplace_back(work,k,threads); for(auto&p:pool) p.join();
  double ns=std::chrono::duration<double,std::nano>(std::chrono::steady_clock::now()-t0).count();
  fprintf(stderr,"NS_PER_PIXEL %.1f\n", ns*threads/(double)(W*H));
  fwrite(px.data(),1,px.size(),stdout); return 0; }
"""


def build_genome(tmp: Path) -> Path:
    cpp = f'#include "{SHIM}"\nfloat4 _Time;\n{transpile_include()}\nvoid init_material(){{\n{inits(material_values())}\n}}\n{MAIN}'
    src, exe = tmp / "gn.cpp", tmp / "gn"
    src.write_text(cpp)
    r = subprocess.run([os.environ.get("CXX", "clang++"), "-std=c++17", "-O2", "-pthread", "-w", str(src), "-o", str(exe)],
                       capture_output=True, text=True)
    if r.returncode:
        sys.exit("GenomeNebula.hlsl did not compile as C++:\n" + r.stderr[-3000:])
    return exe


HS_MAIN = r"""
#include <cstdio>
#include <cstdlib>
#include <cstdint>
#include <vector>
#include <thread>
#include <chrono>
static float lin2srgb(float c){ c = c<0?0:(c>1?1:c); return c<=0.0031308f? c*12.92f : 1.055f*std::pow(c,1/2.4f)-0.055f; }
int main(int argc, char** argv){
  init_material(); _Time = float4(0,0,0,0);
  int W=atoi(argv[1]), H=atoi(argv[2]); float yaw=atof(argv[5]), pitch=atof(argv[6]), fov=atof(argv[7]); int threads=atoi(argv[9]);
  std::vector<uint8_t> px((size_t)W*H*3);
  float cy=std::cos(yaw), sy=std::sin(yaw), cp=std::cos(pitch), sp=std::sin(pitch), th=std::tan(fov*0.5f);
  auto work=[&](int y0,int st){ for(int r=y0;r<H;r+=st) for(int i=0;i<W;i++){
    float x=((i+0.5f)/W*2-1)*th*W/H, y=(1-(r+0.5f)/H*2)*th;
    float3 f(cp*sy, sp, cp*cy), rt(cy,0,-sy), up(-sp*sy, cp, -sp*cy);
    float3 d = h_normalize(f + rt*x + up*y); float4 c = hyperSeaFrag(d);
    size_t o=((size_t)r*W+i)*3; px[o]=(uint8_t)(lin2srgb(c.x)*255+.5f); px[o+1]=(uint8_t)(lin2srgb(c.y)*255+.5f); px[o+2]=(uint8_t)(lin2srgb(c.z)*255+.5f);} };
  auto t0=std::chrono::steady_clock::now();
  std::vector<std::thread> pool; for(int k=0;k<threads;k++) pool.emplace_back(work,k,threads); for(auto&p:pool) p.join();
  double ns=std::chrono::duration<double,std::nano>(std::chrono::steady_clock::now()-t0).count();
  fprintf(stderr,"NS_PER_PIXEL %.1f\n", ns*threads/(double)(W*H));
  fwrite(px.data(),1,px.size(),stdout); return 0; }
"""


def build_hypersea(tmp: Path) -> Path:
    sys.path.insert(0, str(ROOT / "Tools/Build"))
    import bake_static_skybox as b
    st, mt = b.read(b.SHADER), b.read(b.MATERIAL)
    cpp = (f'#include "{b.SHIM}"\nfloat4 _Time;\n' + b.transpile(st) + "\nvoid init_material()\n{\n"
           + b.material_inits(st, mt) + "\n}\n" + HS_MAIN)
    src, exe = tmp / "hs.cpp", tmp / "hs"
    src.write_text(cpp)
    r = subprocess.run([os.environ.get("CXX", "clang++"), "-std=c++17", "-O2", "-pthread", "-w", str(src), "-o", str(exe)],
                       capture_output=True, text=True)
    if r.returncode:
        sys.exit("HyperSeaSkybox did not compile as C++:\n" + r.stderr[-3000:])
    return exe


def run(exe, W, H, kind, mode, yaw=0.0, pitch=0.0, fov=87.0, t=0.0, threads=None):
    import math
    threads = threads or os.cpu_count() or 4
    r = subprocess.run([str(exe), str(W), str(H), str(kind), str(mode), str(math.radians(yaw)), str(math.radians(pitch)),
                        str(math.radians(fov)), str(t), str(threads)], capture_output=True)
    ns = float(re.search(rb"NS_PER_PIXEL ([\d.]+)", r.stderr).group(1))
    import numpy as np
    return np.frombuffer(r.stdout, np.uint8).reshape(H, W, 3), ns


def save(img, path):
    from PIL import Image
    Image.fromarray(img).save(path)
    print(path)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--pano"); ap.add_argument("--view"); ap.add_argument("--hypersea-view")
    ap.add_argument("--size", type=int, default=2048)
    ap.add_argument("--mode", choices=["equirect", "sphere"], default="equirect")
    ap.add_argument("--yaw", type=float, default=0); ap.add_argument("--pitch", type=float, default=10)
    ap.add_argument("--fov", type=float, default=87); ap.add_argument("--time", type=float, default=0)
    ap.add_argument("--bench", action="store_true"); ap.add_argument("--seam", action="store_true")
    ap.add_argument("--parity")
    a = ap.parse_args()
    mode = 1 if a.mode == "sphere" else 0
    with tempfile.TemporaryDirectory() as td:
        tmp = Path(td)
        gexe = build_genome(tmp)
        if a.pano:
            save(run(gexe, a.size, a.size // 2, 0, mode, t=a.time)[0], a.pano)
        if a.view:
            save(run(gexe, 1280, 720, 1, mode, a.yaw, a.pitch, a.fov, a.time)[0], a.view)
        if a.hypersea_view:
            save(run(build_hypersea(tmp), 1280, 720, 1, 0, a.yaw, a.pitch, a.fov)[0], a.hypersea_view)
        if a.bench:
            hexe = build_hypersea(tmp)
            res = {}
            for name, exe, m in (("genome equirect", gexe, 0), ("genome sphere", gexe, 1), ("HyperSea procedural", hexe, 0)):
                best = min(run(exe, 640, 360, 1, m, 30, 10, threads=1)[1] for _ in range(3))
                res[name] = best
            base = res["HyperSea procedural"]
            for k, v in res.items():
                print(f"{k:22s} {v:8.1f} ns/pixel (1 CPU thread)  {v / base:5.2f}x HyperSea")
        if a.seam:
            import numpy as np
            for m, name in ((0, "equirect"), (1, "sphere")):
                img = run(gexe, 2048, 1024, 0, m)[0].astype(int)
                seam = np.abs(img[:, 0] - img[:, -1]).max(axis=1).mean()
                inner = np.abs(img[:, 1:] - img[:, :-1]).max(axis=2).mean()
                # near the pole every column of one row is the same direction: spread there is the pinch
                pole = img[2].std(axis=0).mean()
                print(f"{name:9s} mean step across the u=0/1 seam {seam:.2f} (any other column pair {inner:.2f}); "
                      f"pole row spread {pole:.1f} (0 = no pinch artefact)")
        if a.parity:
            parity(gexe, Path(a.parity))


def parity(gexe, agenome_dir: Path):
    """Draw the lab's own GLSL sky (agenome.glsl compile_textures) at an equirect panorama, and compare."""
    import json
    import numpy as np
    import moderngl
    sys.path.insert(0, str(agenome_dir.parent))
    from agenome.genome import Genome
    from agenome import glsl
    seed = Path(os.environ.get("GENOME_SEED", "/mnt/project-files/amoebius-genome/seeds/amoebius_like/minimal__tuned.json"))
    g = Genome.load(str(seed))
    live = set(range(len(g.nodes)))
    tex = glsl.compile_textures(g, live)
    vals = material_values()
    W, H = 1024, 512
    fs = glsl.PRELUDE + tex + f"""
uniform float uS, uFM; uniform vec3 uFC; uniform vec4 uG;
vec3 aces(vec3 c){{ return c*(2.51*c+0.03)/(c*(2.43*c+0.59)+0.14); }}
void main(){{ vec2 q = gl_FragCoord.xy/vec2({W}.0,{H}.0); float u=q.x, v=q.y;   // row 0 = bottom = v 0
  float lon=(0.5-u)*6.2831853, lat=(1.0-v)*3.14159265; vec3 rd=vec3(sin(lat)*cos(lon), cos(lat), sin(lat)*sin(lon));
  vec2 uv = vec2(atan(rd.z, rd.x)/6.2831853+0.5, asin(clamp(rd.y,-1.0,1.0))/3.14159265+0.5);
  vec3 c = mix(t3(uv)*uS, uFC, uFM); c = aces(c*uG.x); float l = dot(c, vec3(0.299,0.587,0.114)); c = mix(vec3(l), c, uG.y);
  c = clamp((c-0.5)*uG.z+0.5, 0.0, 1.0); oCol = vec4(c, 1.0); }}"""
    ctx = moderngl.create_standalone_context()
    prog = ctx.program(vertex_shader="#version 330\nin vec2 p; void main(){ gl_Position=vec4(p,0,1); }", fragment_shader=fs)
    for n, u in (("uS", "_SkyIntensity"), ("uFM", "_FogMix")):
        prog[n].value = vals[u][0]
    prog["uFC"].value = tuple(vals["_FogColor"][:3])
    prog["uG"].value = tuple(vals["_Grade"])
    vbo = ctx.buffer(np.array([-1, -1, 3, -1, -1, 3], "f4").tobytes())
    vao = ctx.simple_vertex_array(prog, vbo, "p")
    fbo = ctx.framebuffer(color_attachments=[ctx.texture((W, H), 4, dtype="f4")])
    fbo.use(); vao.render()
    lab = np.frombuffer(fbo.read(components=3, dtype="f4"), np.float32).reshape(H, W, 3)[::-1]  # top row first
    srgb = np.where(lab <= 0.0031308, lab * 12.92, 1.055 * np.power(np.clip(lab, 0, 1), 1 / 2.4) - 0.055)
    lab8 = np.clip(srgb * 255 + 0.5, 0, 255).astype(np.uint8)
    port = run(gexe, W, H, 0, 0)[0]
    d = np.abs(lab8.astype(int) - port.astype(int))
    rows = d.max(axis=2).max(axis=1)
    bad = np.where(d.max(axis=2) > 2)[0]
    if len(bad):
        print(f"rows with >2 steps: {len(set(bad.tolist()))} of {H}; worst rows {sorted(set(np.argsort(rows)[-5:].tolist()))}")
    print(json.dumps({"pixels": W * H, "max_abs_8bit": int(d.max()), "mean_abs_8bit": round(float(d.mean()), 4),
                      "pixels_off_by_more_than_2": int((d.max(axis=2) > 2).sum()),
                      "pixels_off_by_more_than_8": int((d.max(axis=2) > 8).sum())}))
    out = os.environ.get("PARITY_OUT")
    if out:
        save(np.concatenate([lab8, port, np.clip(d * 8, 0, 255).astype(np.uint8)], 0), out)


if __name__ == "__main__":
    main()
