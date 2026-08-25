#!/usr/bin/env python3
"""Render the SHIPPED ProjectileChargeField.hlsl to a PNG — a volley's two rounds, side by side,
at the size and distance the player actually judges them at.

Written because two rounds of measurement (planarity, lit-set overlap, per-round brightness) all
said the pair were decorrelated while the playtest said they still read as identical. When a
metric and an eye disagree, the metric is answering a different question — and this project's own
rule is to *judge a candidate at the size it will be judged* (`Docs/PALETTE.md` §4.3, and
SPARROW_SPRAY_ACCURACY.md's "render one large panel" note). A round is 15-77 px at combat range,
so this rasterizes the real shader through a real perspective camera instead of counting samples.

Usage:  python3 Tools/Shaders/render_projectile_charge_field.py [out.png] [--baseline]
Needs clang++ and git; nothing else, and no Unity.
"""

import math
import os
import struct
import subprocess
import sys
import shutil
import tempfile
import re
import zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import verify_projectile_charge_field as V

RENDER = r"""
// Rasterizer: a perspective camera, ray-sphere against each round's shell, the SHIPPED fragment
// function evaluated at the FRONT hit only (`Cull Back`), accumulated additively over black
// (`Blend One One`) exactly as the pass is configured, then a filmic curve so what is written out
// is what a player would see rather than raw HDR.
int main()
{
    int W, H, N; float fovDeg;
    if (std::scanf("%d %d %f %d", &W, &H, &fovDeg, &N) != 4) return 1;
    std::vector<float> cx(N), cy(N), cz(N), rad(N), ph(N), ch(N);
    std::vector<float3> RX(N), RY(N), RZ(N);
    for (int i = 0; i < N; i++) {
        float ox,oy,oz,r, rx,ry,rz, ux,uy,uz, fx,fy,fz, t, sd;
        std::scanf("%f %f %f %f %f %f %f %f %f %f %f %f %f %f %f",
                   &ox,&oy,&oz,&r,&rx,&ry,&rz,&ux,&uy,&uz,&fx,&fy,&fz,&t,&sd);
        cx[i]=ox; cy[i]=oy; cz[i]=oz; rad[i]=r;
        RX[i]=float3(rx,ry,rz); RY[i]=float3(ux,uy,uz); RZ[i]=float3(fx,fy,fz);
        float dia = 2.0f*r;
        PCF_Phase(RX[i]*dia, RY[i]*dia, float3(ox,oy,oz), t, sd, ph[i], ch[i]);
        g_seed[i] = sd;
    }

    float tanHalf = std::tan(fovDeg * 0.5f * 3.14159265f / 180.0f);
    std::vector<unsigned char> img(W*H*3, 0);
    for (int py = 0; py < H; py++) {
        for (int px = 0; px < W; px++) {
            float sx = (2.0f*((px+0.5f)/W) - 1.0f) * tanHalf * ((float)W/(float)H);
            float sy = (1.0f - 2.0f*((py+0.5f)/H)) * tanHalf;
            float3 dir = normalize(float3(sx, sy, 1.0f));
            float3 acc = float3(0.0f,0.0f,0.0f);
            for (int i = 0; i < N; i++) {
                float3 oc = float3(-cx[i], -cy[i], -cz[i]);      // camera at origin
                float b = dot(oc, dir), c = dot(oc,oc) - rad[i]*rad[i];
                float disc = b*b - c;
                if (disc <= 0.0f) continue;
                float tHit = -b - std::sqrt(disc);               // FRONT face only (Cull Back)
                if (tHit <= 0.0f) continue;
                float3 hit = dir * tHit;
                float3 w = hit - float3(cx[i], cy[i], cz[i]);
                // into the shell's object space: its axes are the round's own frame, mesh r=0.5
                float3 posOS = float3(dot(w,RX[i]), dot(w,RY[i]), dot(w,RZ[i])) * (0.5f/rad[i]);
                float3 nrmOS = normalize(posOS);
                float3 toCam = float3(-hit.x, -hit.y, -hit.z);
                float3 viewOS = float3(dot(toCam,RX[i]), dot(toCam,RY[i]), dot(toCam,RZ[i]));
                float3 em; float a;
                // per-OBJECT view axis: camera(origin) -> this round's centre, in object space
                float3 toCentre = float3(-cx[i], -cy[i], -cz[i]);
                float3 vaOS = float3(dot(toCentre,RX[i]), dot(toCentre,RY[i]), dot(toCentre,RZ[i]));
                PCF_Sample(posOS, nrmOS, viewOS, ph[i], ch[i], g_seed[i], vaOS, em, a);
                acc += em;
            }
            g_light += (double)(acc.x + acc.y + acc.z);
            for (int k = 0; k < 3; k++) {
                float v = (k==0?acc.x:(k==1?acc.y:acc.z));
                v = v / (1.0f + v);                              // filmic-ish shoulder
                v = std::pow(saturate(v), 1.0f/2.2f);            // to sRGB
                img[(py*W+px)*3+k] = (unsigned char)(saturate(v)*255.0f + 0.5f);
            }
        }
    }
    std::fwrite(img.data(), 1, img.size(), stdout);
    std::fprintf(stderr, "LIGHT %.6f PIXELS %d\n", g_light, W*H);
    return 0;
}
"""


def build(tmp, tag, src, mat):
    cpp = os.path.join(tmp, tag + ".cpp")
    exe = os.path.join(tmp, tag)
    body = V.translate(src, mat)
    body = body.replace("static long g_fbmCalls = 0;",
                        "static float g_seed[512];\nstatic double g_light = 0.0;\n"
                        "static long g_fbmCalls = 0;")
    with open(cpp, "w", encoding="utf-8") as f:
        f.write(body)
        f.write(RENDER)
    r = subprocess.run(["clang++", "-O2", "-std=c++17", "-o", exe, cpp],
                       capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr[-4000:]); raise SystemExit("build failed")
    return exe


def render(exe, W, H, fov, rounds):
    lines = ["%d %d %f %d" % (W, H, fov, len(rounds))]
    for (o, r, R, U, F, t, sd) in rounds:
        lines.append(" ".join("%.9f" % x for x in
                              (o[0], o[1], o[2], r, R[0], R[1], R[2],
                               U[0], U[1], U[2], F[0], F[1], F[2], t, sd)))
    p = subprocess.run([exe], input=("\n".join(lines) + "\n").encode(), capture_output=True)
    if p.returncode != 0 or len(p.stdout) != W * H * 3:
        raise SystemExit("render failed: %s" % p.stderr[-2000:].decode(errors="replace"))
    m = re.search(rb"LIGHT ([\d.eE+-]+) PIXELS (\d+)", p.stderr)
    render.last_light = float(m.group(1)) if m else 0.0
    return bytearray(p.stdout)


def screen_stats(buf):
    """What the frame actually costs the eye: how much of the screen it lights, and how hard."""
    n = len(buf) // 3
    lit05 = lit20 = lit50 = 0
    for i in range(0, len(buf), 3):
        v = max(buf[i], buf[i + 1], buf[i + 2])
        if v >= 13: lit05 += 1
        if v >= 51: lit20 += 1
        if v >= 128: lit50 += 1
    return (lit05 / n, lit20 / n, lit50 / n)


def png(path, W, H, rgb):
    raw = b"".join(b"\x00" + bytes(rgb[y*W*3:(y+1)*W*3]) for y in range(H))
    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff))
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 2, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def volley(distance, t, roll, growth=V.GROWTH_AT_REST, progress=0.6):
    """Both rounds of one volley, placed in front of a camera at the origin looking down +z."""
    F = (0.0, 0.0, 1.0)
    R = (1.0, 0.0, 0.0)
    U = (0.0, 1.0, 0.0)
    radius = V.LAUNCH_HIT_RADIUS * (1.0 + (growth - 1.0) * progress)
    out = []
    for si, side in enumerate((-1.0, +1.0)):
        ship_right = (math.cos(roll), math.sin(roll), 0.0)
        o = (ship_right[0] * V.MUZZLE_HALF_SPACING * side,
             ship_right[1] * V.MUZZLE_HALF_SPACING * side,
             distance)
        out.append((o, radius, R, U, F, t, _seed()))
    return out


_SEED = [12345]
def _seed():
    """A per-SHOT random number, exactly as Projectile.StampChargeFieldSeed writes one."""
    _SEED[0] = (_SEED[0] * 1103515245 + 12345) & 0x7fffffff
    return (_SEED[0] >> 8) / float(1 << 23)


def stream_rounds(t0, growth=V.GROWTH_AT_REST, volleys=V.ROUNDS_IN_FLIGHT, cam_back=26.0):
    """What the player actually sees: a live burst streaming away down the flight axis, both
    muzzles, at their true positions and sizes. Judging a VFX on isolated pairs is how three
    passes of measurement all missed that most rounds were showing no stroke at all."""
    F, R, U = (0.0, 0.0, 1.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0)
    out = []
    for k in range(volleys):
        p01 = (k + 0.5) / volleys
        radius = V.LAUNCH_HIT_RADIUS * (1.0 + (growth - 1.0) * p01)
        z = V.MUZZLE_SPEED * V.FLIGHT_SECONDS * p01 + cam_back
        # each volley left the muzzle earlier, so it is further along ITS OWN clock
        t = t0 - V.FLIGHT_SECONDS * p01
        for side in (-1.0, +1.0):
            out.append(((V.MUZZLE_HALF_SPACING * side, 0.0, z), radius, R, U, F, t, _seed()))
    return out


def main():
    out = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("-") else "chargefield.png"
    tmp = tempfile.mkdtemp(prefix="cfrender_")
    try:
        new_src = open(V.HLSL, encoding="utf-8").read()
        old = subprocess.run(["git", "-C", V.ROOT, "show", "c3f2f856~1:%s" % V.HLSL_REL],
                             capture_output=True, text=True)
        exes = [("shipped", build(tmp, "new", new_src, V.NEW_MAT))]
        if old.returncode == 0:
            exes.append(("before", build(tmp, "old", old.stdout, V.OLD_MAT)))

        if "--stream" in sys.argv:
            W, H = 1500, 520
            fov = 2 * math.degrees(math.atan(math.tan(math.radians(30.0)) * H / 1080.0))
            sheet = bytearray(W * H * len(exes) * 3)
            for ri, (tag, exe) in enumerate(exes):
                buf = render(exe, W, H, fov, stream_rounds(2.4))
                sheet[ri * W * H * 3:(ri + 1) * W * H * 3] = buf
            png(out, W, H * len(exes), sheet)
            print("wrote %s  (%dx%d)  rows: %s" % (out, W, H * len(exes),
                                                   ", ".join(t for t, _ in exes)))
            return
        PW, PH, FOV = 380, 380, 22.9658   # a true 1:1 crop of a 1080p 60-deg-vertical view
        dists = [30.0, 50.0, 85.0]
        rolls = [0.0, math.pi / 4.0]
        cols = len(dists) * len(rolls)
        rows = len(exes)
        sheet = bytearray(cols * PW * rows * PH * 3)
        SW = cols * PW
        for ri, (tag, exe) in enumerate(exes):
            ci = 0
            for roll in rolls:
                for d in dists:
                    buf = render(exe, PW, PH, FOV, volley(d, 1.7 + 0.37 * ci, roll))
                    for y in range(PH):
                        src_off = y * PW * 3
                        dst_off = ((ri * PH + y) * SW + ci * PW) * 3
                        sheet[dst_off:dst_off + PW * 3] = buf[src_off:src_off + PW * 3]
                    ci += 1
        png(out, SW, rows * PH, sheet)
        print("wrote %s  (%dx%d)" % (out, SW, rows * PH))
        print("rows: " + ", ".join(t for t, _ in exes))
        print("cols: roll 0 deg @ %s u, then roll 45 deg @ %s u"
              % (dists, dists))
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    main()
