#!/usr/bin/env python3
"""
Fly a pilot into the crystal wormhole and out of the other end, OFFLINE, and render what they see
(Docs/CRYSTAL_WORMHOLE.md). No Unity: the light is traced by the SHIPPED
Assets/_Graphics/Materials/Graphs/CrystalWormholeLens.hlsl, compiled with clang++ (HLSL -> C++
spelling only) and called per pixel, and every source the shader samples is reproduced the way the
shader samples it:

  * the SCENE COPY (BlackHoleLensPass's after-transparents copy): the unbent scene from the eye,
    sampled at the screen position of the bent direction, with its depth;
  * the two PANORAMAS (each mouth's six-face capture from its pole centre), sampled by parallax
    direction against a proxy sphere;
  * the depth rule (CrystalWormholeBendDistance): mass in front of the bend is drawn where it is, mass
    the bent ray finds must lie beyond it.

The world is a stand-in for a cell: a membrane sphere (a lat-long checker, so every distortion shows),
a ring of eight toys, and small marker balls near each pole (orange at the attractor, cyan at the
repulsor) so near-field parallax through the throat is visible.

The flight is the runtime's rules, mirrored: speed and camera distance scale with the warp field
(RadialWarp's soft law, poles composed as a product), the hull's heading follows the field's geodesics
(VesselTransformer's warp turn — the same bend the light takes, so what you see ahead is where you go),
the felt pull (BlackHoleVesselPull.FeltAcceleration, attractor in, repulsor out, capped), and the
transit: entering a throat sphere puts the hull at the partner's antipodal point, heading kept, and the
camera keeps its offset from the hull.

Outputs (default: a temp folder, or --out): a contact sheet per flight, the frames either side of each
transit and of the camera's own crossing, the pair's 60 s life watched from one spot (life.png), and a
supersampled approach to the attractor (approach.png). Prints a
continuity number for every transit: the mean colour change between the frames either side of it,
next to the same number for ordinary consecutive frames of the flight.

--check runs the gate instead (exit 1 on any failure; no images):
  1. NO FIELD, NO BEND: amplitudes 0 and throats closed, or a ray that never enters the lens sphere,
     comes back exactly as it went.
  2. THE FIELD IS ThroatWarp's: the shipped CrystalWormholeLnS against ThroatWarp.cs's law (mirrored here
     as radial_scale), and CrystalWormholeDLnS against the derivative of CrystalWormholeLnS.
  3. STRAIGHT THROUGH: the ray down the axis of the attractor goes through once, comes out of the repulsor,
     unbent; the pair is point-symmetric (a ray into the repulsor mirrors one into the attractor).
  4. NO SPECKLE: neighbouring rays through a throat leave along neighbouring directions (the bend is
     continuous where a ray is glued on). Negative control: rebuilt with the neck's ramp collapsed
     (-DCRYSTAL_WORMHOLE_NECK_RAMP=1e-6), this test FAILS.
  5. SEAMLESS: a pilot flown into the attractor - the frames either side of the ship's transit, and of the
     camera's own crossing, differ by no more than ordinary consecutive frames do.
  6. COMPILE: CrystalWormholeLens.shader (vertex + fragment) and Wormhole.shader with glslang against
     verify_black_hole_lens.py's per-file URP mock. Negative control: with its depth include removed the
     lens shader must fail.

Usage: python3 Tools/Shaders/simulate_crystal_wormhole.py [--out DIR] [--width 256] [--quick] [--check]
"""

import argparse
import ctypes
import math
import os
import re
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/CrystalWormholeLens.hlsl")

# ---- the shipped numbers (kept in step with the cell's assets by CrystalWormholeTests) -------------
THROAT = float(os.environ.get("CW_THROAT", 30.0))          # ThroatWarp.throatRadius = the glued spheres
THROAT_SCALE = float(os.environ.get("CW_SCALE", 0.2))      # ThroatWarp.throatScale: s at the neck
FELT_NECK = THROAT / THROAT_SCALE
LAMBDA = FELT_NECK - THROAT
TAPER_IN = THROAT + 3.0 * LAMBDA
TAPER_OUT = THROAT + 4.5 * LAMBDA
HALF_SEPARATION = float(os.environ.get("CW_HALF", 360.0))  # the poles start at y = -/+ this
STEPS = 96
PROXY = 1200.0           # the panoramas' proxy sphere (the membrane)
FAR_EYE_FOV = 85.0      # the far eye: the gameplay camera view plus a margin for bent rays
NEAR_FRACTION = 0.5      # the near field: within this fraction of the eye's distance to its nearest pole

FELT_K, FELT_CAP, FELT_REACH = 4.0, 1.3, 12.0
MEMBRANE = 1200.0

SHIM = r"""
#pragma once
#include <cmath>
#include <algorithm>
struct float3 {
    float x=0,y=0,z=0;
    float3(){}
    float3(float a):x(a),y(a),z(a){}
    float3(float a,float b,float c):x(a),y(b),z(c){}
};
static inline float3 operator+(float3 a,float3 b){return float3(a.x+b.x,a.y+b.y,a.z+b.z);}
static inline float3 operator-(float3 a,float3 b){return float3(a.x-b.x,a.y-b.y,a.z-b.z);}
static inline float3 operator*(float3 a,float b){return float3(a.x*b,a.y*b,a.z*b);}
static inline float3 operator*(float a,float3 b){return b*a;}
static inline float3 operator/(float3 a,float b){return float3(a.x/b,a.y/b,a.z/b);}
static inline float3 operator-(float3 a){return float3(-a.x,-a.y,-a.z);}
static inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static inline float length(float3 a){return std::sqrt(dot(a,a));}
static inline float3 normalize(float3 a){return a/length(a);}
static inline float rsqrt(float a){return 1.0f/std::sqrt(a);}
static inline float min(float a,float b){return a<b?a:b;}
static inline float max(float a,float b){return a>b?a:b;}
static inline float clamp(float v,float a,float b){return v<a?a:(v>b?b:v);}
using std::pow; using std::sqrt; using std::exp; using std::log;
"""

MAIN = r"""
#include "shim.h"
#include "shipped.h"
extern "C" void cw_trace(int n, const float* eye, const float* dir, const float* fp, int steps, float nearest,
                         float* out)
{
    CrystalWormholeField f;
    f.poleA = float3(fp[0], fp[1], fp[2]);
    f.poleB = float3(fp[3], fp[4], fp[5]);
    f.ampA = fp[6]; f.ampB = fp[7]; f.throat = fp[8];
    f.neck = fp[9]; f.feltNeck = fp[10]; f.taperIn = fp[11]; f.taperOut = fp[12];
    f.lensCentre = float3(fp[13], fp[14], fp[15]); f.lensRadius = fp[16];
    for (int i = 0; i < n; i++)
    {
        float3 e(eye[3*i], eye[3*i+1], eye[3*i+2]);
        float3 d(dir[3*i], dir[3*i+1], dir[3*i+2]);
        float3 o, od, xp, xd; float cr, le, fc, ex;
        CrystalWormholeTrace(e, d, f, steps, o, od, cr, le, xp, xd, fc, ex);
        float tb = CrystalWormholeBendDistance(e, d, o, od, fc, nearest);
        float tf = cr > 0.5f ? CrystalWormholeBendDistance(xp, xd, o, od, -1.0f, 0.0f) : 0.0f;
        float* r = out + 18*i;
        r[0]=o.x; r[1]=o.y; r[2]=o.z; r[3]=od.x; r[4]=od.y; r[5]=od.z; r[6]=cr; r[7]=le; r[8]=ex; r[9]=tb;
        r[10]=xp.x; r[11]=xp.y; r[12]=xp.z; r[13]=xd.x; r[14]=xd.y; r[15]=xd.z; r[16]=tf; r[17]=fc;
    }
}
extern "C" void cw_lns(int n, const float* r, const float* fp, float* lns, float* dlns)
{
    CrystalWormholeField f;
    f.throat = fp[8]; f.neck = fp[9]; f.feltNeck = fp[10]; f.taperIn = fp[11]; f.taperOut = fp[12];
    for (int i = 0; i < n; i++) { lns[i] = CrystalWormholeLnS(r[i], f); dlns[i] = CrystalWormholeDLnS(r[i], f); }
}
extern "C" void cw_bend(int n, const float* p, const float* fp, float* out)
{
    CrystalWormholeField f;
    f.poleA = float3(fp[0], fp[1], fp[2]);
    f.poleB = float3(fp[3], fp[4], fp[5]);
    f.ampA = fp[6]; f.ampB = fp[7]; f.throat = fp[8];
    f.neck = fp[9]; f.feltNeck = fp[10]; f.taperIn = fp[11]; f.taperOut = fp[12];
    f.lensCentre = float3(fp[13], fp[14], fp[15]); f.lensRadius = fp[16];
    for (int i = 0; i < n; i++)
    {
        float3 g = CrystalWormholeBend(float3(p[3*i], p[3*i+1], p[3*i+2]), f);
        out[3*i]=g.x; out[3*i+1]=g.y; out[3*i+2]=g.z;
    }
}
"""


def hlsl_to_cpp(text):
    """HLSL -> C++ spelling only: `out T name` parameters become references."""
    return re.sub(r"\b(?:out|inout)\s+(float3|float|bool|int)\s+", r"\1& ", text)


def build(tmp, defines=(), name="libcw.so"):
    with open(HLSL) as fh:
        src = hlsl_to_cpp(fh.read())
    for file_name, body in (("shim.h", SHIM), ("shipped.h", '#include "shim.h"\n' + src), ("main.cpp", MAIN)):
        with open(os.path.join(tmp, file_name), "w") as fh:
            fh.write(body)
    lib = os.path.join(tmp, name)
    cmd = ["clang++", "-std=c++17", "-O2", "-shared", "-fPIC", "-o", lib, os.path.join(tmp, "main.cpp")]
    cmd += ["-D%s" % d for d in defines]
    subprocess.run(cmd, check=True)
    so = ctypes.CDLL(lib)
    so.cw_trace.argtypes = [ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int,
                            ctypes.c_float, ctypes.c_void_p]
    so.cw_bend.argtypes = [ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p]
    so.cw_lns.argtypes = [ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p]
    return so


# ---- the field (RadialWarp, mirrored) ---------------------------------------------------------------

class Field:
    def __init__(self, pole_a, pole_b, amp_a=1.0, amp_b=1.0, throat=THROAT):
        self.a = np.asarray(pole_a, np.float64)
        self.b = np.asarray(pole_b, np.float64)
        self.amp_a, self.amp_b, self.throat = amp_a, amp_b, throat

    @property
    def centre(self):
        return (self.a + self.b) * 0.5

    @property
    def lens_radius(self):
        return 0.5 * np.linalg.norm(self.a - self.b) + TAPER_OUT

    def params(self):
        c = self.centre
        return np.array([*self.a, *self.b, self.amp_a, self.amp_b, self.throat, THROAT, FELT_NECK, TAPER_IN,
                         TAPER_OUT, *c, self.lens_radius], np.float32)


def radial_scale(r):
    """ThroatWarp.ScaleAt, for a distance (the same lines as CrystalWormholeLnS)."""
    r = max(float(r), 1e-3)
    felt = FELT_NECK
    if r <= THROAT:
        r = THROAT
    else:
        u = (r - THROAT) / LAMBDA
        felt = r + LAMBDA * math.exp(-u - 0.5 * u * u)
    t = min(1.0, max(0.0, (r - TAPER_IN) / (TAPER_OUT - TAPER_IN)))
    w = 1.0 - t * t * t * (t * (t * 6 - 15) + 10)
    return math.exp(w * (math.log(r) - math.log(felt)))


def scale_at(field, p):
    """WarpFieldRuntime.ScaleAt with two poles: the product, each raised to its amplitude."""
    s = 1.0
    for c, a in ((field.a, field.amp_a), (field.b, field.amp_b)):
        if a > 0:
            s *= radial_scale(np.linalg.norm(p - c)) ** a
    return max(1e-4, s)


# ---- the stand-in world -------------------------------------------------------------------------------

def make_world():
    spheres = []
    ring = [(1.0, .25, .25), (1.0, .7, .2), (.95, .95, .3), (.3, 1.0, .3), (.2, .9, .9), (.3, .4, 1.0),
            (.75, .3, 1.0), (1.0, .4, .8)]
    for i, col in enumerate(ring):
        ang = 2 * math.pi * i / 8
        spheres.append(((984 * math.cos(ang), 0.0, 984 * math.sin(ang)), 45.0, col))
    for pole, col in (((0, -HALF_SEPARATION, 0), (1.0, .55, .1)), ((0, HALF_SEPARATION, 0), (.1, .85, 1.0))):
        for off in ((0, 110, 110), (0, -110, 110), (0, 110, -110), (0, -110, -110), (110, 0, 140), (-110, 0, -140)):
            spheres.append((tuple(np.add(pole, off)), 10.0, col))
    centres = np.array([s[0] for s in spheres], np.float64)
    radii = np.array([s[1] for s in spheres], np.float64)
    colours = np.array([s[2] for s in spheres], np.float64)
    return centres, radii, colours


WORLD = make_world()


def membrane_colour(n):
    lat = np.arcsin(np.clip(n[:, 1], -1, 1))
    lon = np.arctan2(n[:, 2], n[:, 0])
    checker = ((np.floor(lat / (math.pi / 12)) + np.floor(lon / (math.pi / 12))) % 2)
    base = 0.25 + 0.5 * (0.5 + 0.5 * n)
    base = base * (0.55 + 0.45 * checker[:, None])
    # the equator and the poles' meridian marked white, so orientation reads at a glance
    line = (np.abs(lat) < 0.02) | (np.abs(np.sin(lon)) < 0.015)
    base[line] = 0.95
    return base


def cast(origins, dirs, t_min=None, extra=()):
    """The unbent world: colour and distance of the first thing along each ray (beyond t_min)."""
    centres, radii, colours = WORLD
    n = len(dirs)
    lo = np.zeros(n) if t_min is None else t_min
    best_t = np.full(n, np.inf)
    best_c = np.zeros((n, 3))
    spheres = list(zip(centres, radii, colours)) + list(extra)
    for c, r, col in spheres:
        c = np.asarray(c, np.float64)
        oc = origins - c
        b = np.einsum("ij,ij->i", oc, dirs)
        cc = np.einsum("ij,ij->i", oc, oc) - r * r
        disc = b * b - cc
        hit = disc > 0
        t = -b - np.sqrt(np.where(hit, disc, 0))
        hit &= (t > np.maximum(lo, 1e-3)) & (t < best_t)
        shade = 0.6 + 0.4 * np.clip(-np.einsum("ij,ij->i", (origins + dirs * t[:, None] - c) / r, dirs), 0, 1)
        best_t = np.where(hit, t, best_t)
        best_c = np.where(hit[:, None], np.asarray(col)[None, :] * shade[:, None], best_c)
    # the membrane, from inside
    b = np.einsum("ij,ij->i", origins, dirs)
    cc = np.einsum("ij,ij->i", origins, origins) - MEMBRANE ** 2
    t = -b + np.sqrt(np.maximum(b * b - cc, 0))
    miss = ~np.isfinite(best_t)
    p = origins + dirs * t[:, None]
    mcol = membrane_colour(p / np.linalg.norm(p, axis=1, keepdims=True))
    best_c = np.where(miss[:, None], mcol, best_c)
    best_t = np.where(miss, t, best_t)
    return best_c, best_t


def turn_through(v, n):
    """CrystalWormholeTurnThrough: 180 degrees about the throat normal."""
    return n * (2.0 * (v @ n)) - v


def far_eye_pose(field, eye, f, u):
    """CrystalWormholeView: where the far side is seen FROM - the eye taken through the pair at the near
    throat's point facing it: the antipode of that point, turned 180 degrees about its normal. When the
    camera itself reaches the throat this IS the pose it crosses to (CrystalWormhole.CameraThrough)."""
    ra, rb = np.linalg.norm(eye - field.a), np.linalg.norm(eye - field.b)
    near_c = field.a if ra <= rb else field.b
    n = eye - near_c
    n = n / max(np.linalg.norm(n), 1e-6)
    x = near_c + n * field.throat
    x2 = field.a + field.b - x
    return x2 + turn_through(eye - x, n), turn_through(f, n), turn_through(u, n)


def camera_through(field, pos, f, u):
    """CrystalWormhole.CameraThrough: a camera that has reached a throat crosses at its OWN point."""
    ra, rb = np.linalg.norm(pos - field.a), np.linalg.norm(pos - field.b)
    near_c = field.a if ra <= rb else field.b
    n = (pos - near_c) / max(np.linalg.norm(pos - near_c), 1e-6)
    return field.a + field.b - pos, turn_through(f, n), turn_through(u, n)


# ---- the camera ---------------------------------------------------------------------------------------

class Camera:
    def __init__(self, pos, forward, up, width, height, fov=65.0):
        self.pos = np.asarray(pos, np.float64)
        f = np.asarray(forward, np.float64)
        self.f = f / np.linalg.norm(f)
        r = np.cross(up, self.f)
        self.r = r / np.linalg.norm(r)
        self.u = np.cross(self.f, self.r)
        self.w, self.h = width, height
        self.tan = math.tan(math.radians(fov) * 0.5)
        self.aspect = width / height

    def rays(self):
        ys, xs = np.mgrid[0:self.h, 0:self.w]
        px = ((xs + 0.5) / self.w * 2 - 1) * self.tan * self.aspect
        py = (1 - (ys + 0.5) / self.h * 2) * self.tan
        d = self.f[None, None, :] + px[..., None] * self.r + py[..., None] * self.u
        d /= np.linalg.norm(d, axis=-1, keepdims=True)
        return d.reshape(-1, 3)

    def project(self, dirs):
        """Screen uv of world directions (points at infinity) and an on-screen fade (the shader's 4%)."""
        z = dirs @ self.f
        x = dirs @ self.r
        y = dirs @ self.u
        ok = z > 1e-5
        zs = np.where(ok, z, 1.0)
        u = (x / zs / (self.tan * self.aspect)) * 0.5 + 0.5
        v = (y / zs / self.tan) * 0.5 + 0.5
        edge = np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v))
        on = np.where(ok, np.clip(edge / 0.04, 0, 1), 0.0)
        return u, v, on


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / np.maximum(e1 - e0, 1e-9), 0, 1)
    return t * t * (3 - 2 * t)


def parallax(rel, d, proxy):
    b = np.einsum("ij,ij->i", rel, d)
    c = np.einsum("ij,ij->i", rel, rel) - proxy * proxy
    t = -b + np.sqrt(np.maximum(b * b - c, 0))
    p = rel + d * np.maximum(t, 0)[:, None]
    return p / np.linalg.norm(p, axis=1, keepdims=True)


def render(so, cam, field, ship=None):
    """One frame, the way CrystalWormholeLens.shader draws it. ship: (position, radius), a white ball."""
    extra = [] if ship is None else [(ship[0], ship[1], (1.0, 1.0, 1.0))]
    dirs = cam.rays()
    n = len(dirs)
    eye = np.repeat(cam.pos[None, :], n, axis=0)
    base, dist = cast(eye, dirs, extra=extra)

    ra = np.linalg.norm(cam.pos - field.a)
    rb = np.linalg.norm(cam.pos - field.b)
    nearest = NEAR_FRACTION * max(min(ra, rb), field.throat)

    out = np.zeros((n, 18), np.float32)
    e32 = np.ascontiguousarray(eye, np.float32)
    d32 = np.ascontiguousarray(dirs, np.float32)
    fp = field.params()
    so.cw_trace(n, e32.ctypes.data, d32.ctypes.data, fp.ctypes.data, STEPS, ctypes.c_float(nearest), out.ctypes.data)
    o = out[:, 0:3].astype(np.float64)
    od = out[:, 3:6].astype(np.float64)
    od /= np.linalg.norm(od, axis=1, keepdims=True)
    crossings, last_exit, tb = out[:, 6], out[:, 7], out[:, 9].astype(np.float64)

    # Mass in front of the bend stands where it is.
    keep = 1.0 - smoothstep(0.7 * tb, tb, dist)

    # The far side: the FAR EYE (the camera's pose taken through the pair, its near plane on the far
    # throat) for every ray that came out of that throat and lands in its frame; else the panorama of
    # the mouth the ray came out of. Far mass in front of the far side's own bend is seen along the
    # direction the ray LEFT the throat (exitDir) - the near field through a throat, e.g. your own
    # ship while the camera follows it through - and everything beyond it along the final one.
    near_is_a = ra <= rb
    far_c = field.b if near_is_a else field.a
    far_id = 2.0 if near_is_a else 1.0
    fe, ff, fu_ = far_eye_pose(field, cam.pos, cam.f, cam.u)
    far_cam = Camera(fe, ff, fu_, cam.w, cam.h, fov=FAR_EYE_FOV)
    _, _, fon = far_cam.project(od)
    use_far = (np.abs(last_exit - far_id) < 0.5) * fon * (np.mod(crossings, 2) > 0.5)
    to_c = far_c - fe
    dc = np.linalg.norm(to_c)

    def far_clip(dirs_):
        if dc <= field.throat:
            return np.zeros(n)
        nrm = to_c / dc
        denom = dirs_ @ nrm
        return np.where(denom > 1e-4, (dc - field.throat) / np.maximum(denom, 1e-4), 0.0)

    fe_n = np.repeat(fe[None, :], n, axis=0)
    far, _ = cast(fe_n, od, t_min=far_clip(od), extra=extra)
    xd = out[:, 13:16].astype(np.float64)
    xd /= np.maximum(np.linalg.norm(xd, axis=1, keepdims=True), 1e-9)
    xp = out[:, 10:13].astype(np.float64)
    near_far, near_dist = cast(fe_n, xd, t_min=far_clip(xd), extra=extra)
    bend_far = np.linalg.norm(xp + xd * out[:, 16:17].astype(np.float64) - fe[None, :], axis=1)
    bend_far = np.maximum(bend_far, nearest)
    keep_far = (1.0 - smoothstep(0.7 * bend_far, bend_far, near_dist))[:, None]
    far = near_far * keep_far + far * (1 - keep_far)

    pole_of = np.where(last_exit > 1.5, 1, np.where(last_exit > 0.5, 0, 0 if ra <= rb else 1))
    capture = np.where(pole_of[:, None] == 0, field.a[None, :], field.b[None, :])
    pano_dir = parallax(o - capture, od, PROXY)
    pano, _ = cast(capture, pano_dir, extra=extra)
    pano = pano * (1 - use_far[:, None]) + far * use_far[:, None]

    # This side: the scene copy at the bent direction, if on screen and beyond the bend.
    u, v, on = cam.project(od)
    xi = np.clip((u * cam.w).astype(int), 0, cam.w - 1)
    yi = np.clip(((1 - v) * cam.h).astype(int), 0, cam.h - 1)
    idx = yi * cam.w + xi
    sample, sample_dist = base[idx], dist[idx]
    accept = on * smoothstep(0.7 * tb, tb, sample_dist) * (np.mod(crossings, 2) < 0.5)
    lensed = pano * (1 - accept[:, None]) + sample * accept[:, None]

    colour = lensed * (1 - keep[:, None]) + base * keep[:, None]
    img = np.clip(colour.reshape(cam.h, cam.w, 3), 0, 1)
    return (img * 255).astype(np.uint8), out


# ---- flight ---------------------------------------------------------------------------------------------

def rotate(v, axis, angle):
    axis = axis / np.linalg.norm(axis)
    return v * math.cos(angle) + np.cross(axis, v) * math.sin(angle) + axis * np.dot(axis, v) * (1 - math.cos(angle))


def segment_enters(a, b, c, r):
    o = a - c
    cc = o @ o - r * r
    if cc <= 0:
        return None
    s = b - a
    aa = max(s @ s, 1e-12)
    bb = o @ s
    disc = bb * bb - aa * cc
    if disc < 0 or bb >= 0:
        return None
    t = (-bb - math.sqrt(disc)) / aa
    return t if t <= 1 else None


def felt_accel(p, centre, sign, cruise, s):
    r = p - centre
    rt = THROAT
    q = r @ r + rt * rt
    return r * (-sign * FELT_K * cruise * cruise * rt * s / (q * math.sqrt(q)))


def fly(so, field, start, heading, seconds, cruise=60.0, boost=1.0, aim=None, aim_rate=1.2, dt=1 / 60,
        geodesic=True):
    """The runtime's rules, mirrored. aim: a world point the pilot steers at (rad/s turn limit)."""
    pos = np.asarray(start, np.float64)
    f = np.asarray(heading, np.float64)
    f /= np.linalg.norm(f)
    up = np.array([0.0, 0.0, 1.0]) if abs(f[1]) > 0.9 else np.array([0.0, 1.0, 0.0])
    up = up - f * (up @ f)
    up /= np.linalg.norm(up)
    pull = np.zeros(3)
    fp = field.params()
    log = []
    t = 0.0
    while t < seconds:
        s = scale_at(field, pos)
        speed = cruise * boost
        world_step = speed * s * dt
        if geodesic:
            g = np.zeros(3, np.float32)
            p32 = np.ascontiguousarray(pos, np.float32)
            so.cw_bend(1, p32.ctypes.data, fp.ctypes.data, g.ctypes.data)
            g = g.astype(np.float64)
            gp = g - f * (g @ f)
            m = np.linalg.norm(gp)
            if m > 1e-9:
                axis = np.cross(f, gp)
                ang = m * world_step
                f = rotate(f, axis, ang)
                up = rotate(up, axis, ang)
        if aim is not None:
            want = np.asarray(aim) - pos
            want /= np.linalg.norm(want)
            c = np.clip(f @ want, -1, 1)
            ang = math.acos(c)
            if ang > 1e-5:
                axis = np.cross(f, want)
                step = min(ang, aim_rate * dt)
                f = rotate(f, axis, step)
                up = rotate(up, axis, step)
        f /= np.linalg.norm(f)
        up -= f * (up @ f)
        up /= np.linalg.norm(up)

        a = np.zeros(3)
        cap = 100.0
        for c, sign in ((field.a, 1.0), (field.b, -1.0)):
            if np.linalg.norm(pos - c) <= FELT_REACH * THROAT:
                a += felt_accel(pos, c, sign, cruise, s)
                cap = max(cap, FELT_CAP * cruise)
        pull = pull + a * dt
        nrm = np.linalg.norm(pull)
        if nrm > cap:
            pull *= cap / nrm

        new = pos + (f * speed + pull) * s * dt
        transit = None
        carry = None
        for c in (field.a, field.b):
            hit_t = segment_enters(pos, new, c, field.throat)
            if hit_t is not None:
                hit = pos + (new - pos) * hit_t
                nrm = (hit - c) / np.linalg.norm(hit - c)
                hit = c + nrm * field.throat
                new = field.a + field.b - hit
                f, up, pull = turn_through(f, nrm), turn_through(up, nrm), turn_through(pull, nrm)
                transit = "attractor" if c is field.a else "repulsor"
                carry = (hit, new.copy(), nrm)
                break
        pos = new
        t += dt
        log.append(dict(t=t, pos=pos.copy(), f=f.copy(), up=up.copy(), s=s, transit=transit, carry=carry,
                        ra=np.linalg.norm(pos - field.a), rb=np.linalg.norm(pos - field.b),
                        felt=np.linalg.norm(f * speed + pull)))
    return log


CAMERA_OFFSET = np.array([0.0, 4.0, -24.0])   # felt units: up, back (a mid-fleet chase camera)


def chase_pose(entry):
    s = entry["s"]
    f, up = entry["f"], entry["up"]
    return entry["pos"] + (up * CAMERA_OFFSET[1] + f * CAMERA_OFFSET[2]) * s, f, up


CAMERA_POS_TAU = 0.06     # CustomCameraController: position SmoothDamp-ish lag, seconds
CAMERA_ROT_RATE = 12.0    # rotation slerp rate, 1/s


def camera_track(log, field, dt=1 / 60):
    """CustomCameraController's follow and carry, mirrored. After the ship goes through, the camera
    keeps framing it as if the two throats were one (the ship's pose taken BACK through the pair, the
    rigid map at its crossing point) until the camera itself reaches the throat; then it crosses at
    its OWN point (camera_through). Position and rotation lag the framed pose as the controller's do.
    Per frame: (camera pos, f, up, carrying)."""
    track = []
    carry = None
    cam_pos = cam_f = cam_u = None
    for e in log:
        if e["carry"] is not None:
            carry = e["carry"]
        follow = e
        if carry is not None:
            x, x2, n = carry
            follow = dict(e, pos=x + turn_through(e["pos"] - x2, n), f=turn_through(e["f"], n),
                          up=turn_through(e["up"], n))
        want, _, _ = chase_pose(follow)
        if cam_pos is None:
            cam_pos = want.copy()
            cam_f, cam_u = follow["f"].copy(), follow["up"].copy()
        a = 1.0 - math.exp(-dt / CAMERA_POS_TAU)
        cam_pos = cam_pos + (want - cam_pos) * a
        look = follow["pos"] - cam_pos
        look /= np.linalg.norm(look)
        b = 1.0 - math.exp(-CAMERA_ROT_RATE * dt)
        cam_f = cam_f + (look - cam_f) * b
        cam_f /= np.linalg.norm(cam_f)
        up_want = follow["up"] - cam_f * (follow["up"] @ cam_f)
        cam_u = cam_u + (up_want / np.linalg.norm(up_want) - cam_u) * b
        cam_u -= cam_f * (cam_u @ cam_f)
        cam_u /= np.linalg.norm(cam_u)
        if carry is not None:
            near_c = field.a if np.linalg.norm(carry[0] - field.a) < np.linalg.norm(carry[0] - field.b) else field.b
            if np.linalg.norm(cam_pos - near_c) <= field.throat:
                cam_pos, cam_f, cam_u = camera_through(field, cam_pos, cam_f, cam_u)
                carry = None
        track.append((cam_pos.copy(), cam_f.copy(), cam_u.copy(), carry))
    return track


def chase_camera(pose, width, height):
    pos, f, up, _ = pose
    return Camera(pos, f, up, width, height)


def ship_of(entry):
    return (entry["pos"], 2.5 * entry["s"])


def label(img, text):
    im = Image.fromarray(img)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, im.width, 12], fill=(0, 0, 0))
    d.text((3, 0), text, fill=(255, 255, 255))
    return np.asarray(im)


def sheet(frames, cols):
    h, w, _ = frames[0].shape
    rows = (len(frames) + cols - 1) // cols
    out = np.zeros((rows * (h + 2), cols * (w + 2), 3), np.uint8)
    for i, fr in enumerate(frames):
        r, c = divmod(i, cols)
        out[r * (h + 2):r * (h + 2) + h, c * (w + 2):c * (w + 2) + w] = fr
    return out


def frame_delta(a, b):
    return float(np.mean(np.abs(a.astype(np.float64) - b.astype(np.float64))))


def run_flight(so, name, field, log, width, height, outdir, picks=16):
    track = camera_track(log, field)

    def frame(i):
        return render(so, chase_camera(track[i], width, height), field, ship=ship_of(log[i]))[0]

    times = np.linspace(0, len(log) - 1, picks).astype(int).tolist()
    transits = [i for i, e in enumerate(log) if e["transit"]]
    crossings = [i for i in range(1, len(track)) if track[i - 1][3] is not None and track[i][3] is None]
    frames = []
    for i in sorted(set(times)):
        e = log[i]
        frames.append(label(frame(i), "t%.1fs s%.2f rA%.0f rB%.0f" % (e["t"], e["s"], e["ra"], e["rb"])))
    Image.fromarray(sheet(frames, 4)).save(os.path.join(outdir, name + "_flight.png"))

    # Continuity: the frames either side of each moment something goes through - the ship's transit
    # and the camera's own crossing - against the ordinary consecutive frames around it.
    report, ordinary = [], []
    for k, what in [(k, "ship") for k in transits] + [(k, "camera") for k in crossings]:
        for i in range(max(1, k - 30), min(len(log) - 1, k + 30), 6):
            if i + 1 == k or i == k:
                continue
            ordinary.append(frame_delta(frame(i), frame(i + 1)))
        before, after = frame(k - 1), frame(k)
        d = frame_delta(before, after)
        strip = sheet([label(frame(max(0, k - 4)), "t%.2f" % log[max(0, k - 4)]["t"]),
                       label(before, "before %s t%.2f" % (what, log[k - 1]["t"])),
                       label(after, "after t%.2f d%.1f" % (log[k]["t"], d)),
                       label(frame(min(len(log) - 1, k + 4)), "t%.2f" % log[min(len(log) - 1, k + 4)]["t"])], 4)
        Image.fromarray(strip).save(os.path.join(outdir, "%s_%s_%d.png" % (name, what, k)))
        report.append(("%s/%s" % (what, log[k]["transit"] or "-"), log[k]["t"], d))
    return report, ordinary


# ---- the pair's life (CrystalWormhole.Life, mirrored) -------------------------------------------------

LIFE_SECONDS = 60.0       # SpawnableCrystalWormhole.lifeSeconds: from opening to meeting
FORM_SECONDS = 4.0
TOUCH = 0.45              # separation (of the opening one) at which the annihilation begins
SPIRAL_TURNS = 2.0
BEAT_CYCLES = 7.0
BEAT_DEPTH = 0.6


def life(t, life_s=LIFE_SECONDS, form_s=FORM_SECONDS, touch=TOUCH, turns=SPIRAL_TURNS, beats=BEAT_CYCLES,
         depth=BEAT_DEPTH):
    """CrystalWormhole.Life: (separation 1..0, spiral angle, envelope, attractor amplitude, repulsor amplitude)."""
    p = min(1.0, max(0.0, t / life_s))
    q = 1.0 - p
    separation = math.sqrt(q)                                # slowly, then faster: they attract
    spiral = 2 * math.pi * turns * (1.0 - separation)        # turning quicker as they close
    form = min(1.0, max(0.0, t / max(form_s, 1e-6)))
    form = form * form * (3 - 2 * form)
    v = min(1.0, max(0.0, 1.0 - separation / touch))         # 0 until they touch, 1 when they meet
    envelope = form * (1.0 - v) ** 1.5
    beat = depth * v * math.sin(2 * math.pi * beats * v * v)
    return separation, spiral, envelope, envelope * (1 + beat), envelope * (1 - beat)


def field_at(t, half=HALF_SEPARATION):
    separation, spiral, envelope, amp_a, amp_r = life(t)
    axis = np.array([0.0, 0.0, 1.0])                         # across the pair
    half_vec = np.array([0.0, -half, 0.0]) * separation
    c, sn = math.cos(spiral), math.sin(spiral)
    rot = half_vec * c + np.cross(axis, half_vec) * sn
    throat = min(THROAT * envelope, 0.4 * 2 * half * separation)
    return Field(rot, -rot, amp_a, amp_r, throat)


def approach_sheet(so, width, height, outdir, supersample=2):
    """Looking straight at the attractor from 900 u in to just outside its throat, supersampled (the
    shader's derivative-chosen mips stand in as a box filter)."""
    field = Field((0, -HALF_SEPARATION, 0), (0, HALF_SEPARATION, 0))
    frames = []
    for d in (900, 600, 400, 250, 150, 90, 55, 38):
        pos = np.array([-d, -HALF_SEPARATION + 0.15 * d, 0.1 * d])
        f = field.a - pos
        cam = Camera(pos, f / np.linalg.norm(f), np.array([0, 1.0, 0]), width * supersample, height * supersample)
        img, _ = render(so, cam, field)
        img = np.asarray(Image.fromarray(img).resize((width, height), Image.LANCZOS))
        frames.append(label(img, "%d u from the attractor (scale %.2f)" % (d, scale_at(field, pos))))
    Image.fromarray(sheet(frames, 2)).save(os.path.join(outdir, "approach.png"))


def watch_life(so, width, height, outdir):
    eye = np.array([-760.0, 120.0, 420.0])
    f = -eye / np.linalg.norm(eye)
    frames = []
    for t in (0.5, 2.0, 4.0, 15.0, 30.0, 40.0, 46.0, 49.0, 51.0, 53.0, 55.0, 56.5, 58.0, 59.0, 59.6, 60.0):
        field = field_at(t)
        cam = Camera(eye, f, np.array([0.0, 1.0, 0.0]), width, height)
        img, _ = render(so, cam, field)
        sep, _, env, aa, ar = life(t)
        frames.append(label(img, "t%.1f sep%.2f env%.2f A%.2f R%.2f" % (t, sep, env, aa, ar)))
    Image.fromarray(sheet(frames, 4)).save(os.path.join(outdir, "life.png"))


def trace(so, field, eyes, dirs, steps=STEPS, nearest=0.0):
    eyes = np.ascontiguousarray(np.atleast_2d(eyes), np.float32)
    dirs = np.ascontiguousarray(np.atleast_2d(dirs), np.float32)
    n = len(dirs)
    if len(eyes) == 1 and n > 1:
        eyes = np.ascontiguousarray(np.repeat(eyes, n, axis=0))
    out = np.zeros((n, 18), np.float32)
    so.cw_trace(n, eyes.ctypes.data, dirs.ctypes.data, field.params().ctypes.data, steps, ctypes.c_float(nearest),
                out.ctypes.data)
    return out


def scanline_jitter(so, field):
    """The worst second difference of the leaving direction along a smooth scanline of rays that all go
    through the attractor's throat from close by (where the speckle showed)."""
    eye = field.a + np.array([-34.9, 0.8, 0.0])
    ang = np.radians(np.linspace(-20, 20, 241))
    dirs = np.stack([np.cos(ang) * math.cos(0.3), np.full(len(ang), math.sin(0.3)), np.sin(ang) * math.cos(0.3)], 1)
    od = trace(so, field, eye, dirs)[:, 3:6].astype(np.float64)
    second = np.linalg.norm(od[2:] - 2 * od[1:-1] + od[:-2], axis=1)
    return float(second.max())


def glslang_checks(tmp):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import verify_black_hole_lens as V
    V.URP_MOCK[V.URP + "Core.hlsl"] += """
#define TEXTURE2D_FLOAT(t) Texture2D<float> t
#define SAMPLE_TEXTURE2D(t, s, c) t.Sample(s, c)
#define SAMPLE_TEXTURE2D_ARRAY(t, s, c, i) t.Sample(s, float3(c, i))
float4 _Time;
float4x4 UNITY_MATRIX_P;
float4 _ScreenParams;
"""
    V.write_mock_library(tmp)
    graphs = os.path.dirname(HLSL)
    for inc in ("BlackHoleLens.hlsl", "CrystalWormholeLens.hlsl", "PrismOcclusionCorridor.hlsl"):
        src = os.path.join(graphs, inc)
        if os.path.exists(src):
            with open(src) as fh, open(os.path.join(tmp, inc), "w") as out:
                out.write(fh.read())
    ok = True
    for path in (os.path.join(graphs, "CrystalWormholeLens.shader"), os.path.join(graphs, "Wormhole.shader")):
        text = open(path).read()
        for prog in re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", text, re.S):
            include = re.search(r"HLSLINCLUDE(.*?)ENDHLSL", text, re.S)
            body = (include.group(1) if include else "") + prog
            vert = re.search(r"#pragma vertex (\w+)", prog).group(1)
            frag = re.search(r"#pragma fragment (\w+)", prog).group(1)
            for stage, entry in (("vert", vert), ("frag", frag)):
                rc, out = V.glslang_compile(tmp, body, stage, entry)
                print("   %s %s [%s]: %s" % (os.path.basename(path), entry, stage, "compiled" if rc == 0 else "FAIL\n" + out))
                ok &= rc == 0
    lens = open(os.path.join(graphs, "CrystalWormholeLens.shader")).read()
    prog = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", lens, re.S)[0]
    assert V.DEPTH_INCLUDE in prog
    rc, out = V.glslang_compile(tmp, prog.replace(V.DEPTH_INCLUDE, ""), "frag", "CrystalWormholeFrag")
    fired = rc != 0 and "SampleSceneDepth" in out
    print("   negative control [depth include removed]: %s" % ("FIRED" if fired else "DID NOT FIRE"))
    return ok and fired


def check():
    ok = True

    def verdict(name, passed, detail=""):
        nonlocal ok
        ok &= bool(passed)
        print("%s %s%s" % ("PASS" if passed else "FAIL", name, (" - " + detail) if detail else ""))

    with tempfile.TemporaryDirectory() as tmp:
        so = build(tmp)
        field = Field((0, -HALF_SEPARATION, 0), (0, HALF_SEPARATION, 0))
        rng = np.random.default_rng(7)

        # 1
        flat = Field(field.a, field.b, 0.0, 0.0, 0.0)
        dirs = rng.normal(size=(500, 3))
        dirs /= np.linalg.norm(dirs, axis=1, keepdims=True)
        eyes = rng.uniform(-800, 800, size=(500, 3))
        out = trace(so, flat, eyes, dirs)
        bent = np.max(np.abs(out[:, 3:6] - dirs.astype(np.float32)))
        verdict("1. no field, no bend", bent < 1e-6 and np.all(out[:, 6] == 0), "worst %.2e" % bent)
        far_eye = np.array([5000.0, 0, 0])
        out = trace(so, field, far_eye, np.array([[0.0, 1.0, 0.0]]))
        verdict("1. a ray that never enters the lens is untouched", np.allclose(out[0, 3:6], [0, 1, 0], atol=0) and
                np.allclose(out[0, 0:3], far_eye, atol=0))

        # 2
        rs = np.concatenate([np.linspace(1, THROAT, 40, endpoint=False), np.linspace(THROAT, TAPER_OUT + 100, 600)]).astype(np.float32)
        lns = np.zeros(len(rs), np.float32)
        dl = np.zeros(len(rs), np.float32)
        so.cw_lns(len(rs), rs.ctypes.data, field.params().ctypes.data, lns.ctypes.data, dl.ctypes.data)
        want = np.array([math.log(radial_scale(r)) for r in rs])
        verdict("2. the lens's field is ThroatWarp's", np.max(np.abs(lns - want)) < 2e-5,
                "worst %.2e" % np.max(np.abs(lns - want)))
        out_r = rs > THROAT + 1
        fd = np.gradient(want, rs.astype(np.float64))
        err = np.max(np.abs(dl[out_r][2:-2] - fd[out_r][2:-2]))
        verdict("2. its bend is the field's gradient", err < 2e-4, "worst %.2e" % err)

        # 3
        out = trace(so, field, field.a + np.array([-900.0, 0, 0]), np.array([[1.0, 0, 0]]))
        verdict("3. straight down the axis: through once, out of the repulsor, unbent",
                out[0, 6] == 1 and out[0, 7] == 2 and np.allclose(out[0, 3:6], [1, 0, 0], atol=1e-4))
        e1 = field.a + np.array([-700.0, 40.0, 25.0])
        d1 = np.array([[0.95, -0.05, -0.02]])
        d1 /= np.linalg.norm(d1)
        o1 = trace(so, field, e1, d1)[0]
        o2 = trace(so, field, -e1, -d1)[0]
        sym = np.max(np.abs(o1[3:6] + o2[3:6]))
        verdict("3. the pair is point-symmetric", sym < 1e-3 and o1[6] == o2[6], "worst %.2e" % sym)

        # 4
        jitter = scanline_jitter(so, field)
        verdict("4. no speckle through a throat", jitter < 2e-3, "worst second difference %.2e" % jitter)
        control = build(tmp, defines=("CRYSTAL_WORMHOLE_NECK_RAMP=1e-6",), name="libcw_control.so")
        cj = scanline_jitter(control, field)
        verdict("4. negative control [neck ramp collapsed] fires", cj > 2e-3, "worst second difference %.2e" % cj)

        # 5
        log = fly(so, field, (-700, -360, 0), (1, 0, 0), 12.0)
        report, ordinary = run_flight(so, "check", field, log, 160, 100, tmp, picks=1)
        typical = float(np.median(ordinary)) if ordinary else float("nan")
        kinds = {r[0].split("/")[0] for r in report}
        worst = max((r[2] for r in report), default=float("inf"))
        verdict("5. seamless transit (ship and camera)", kinds == {"ship", "camera"} and worst <= 1.5 * typical,
                "worst %.1f vs ordinary frame %.1f" % (worst, typical))

        # 6
        print("6. compile")
        verdict("6. shaders compile against the URP mock", glslang_checks(tmp))
    print("RESULT: %s" % ("OK" if ok else "FAILED"))
    return 0 if ok else 1


def main():
    if "--check" in sys.argv:
        return check()
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.environ.get("CW_SIM_OUT") or tempfile.mkdtemp(prefix="crystal_wormhole_sim_"))
    ap.add_argument("--width", type=int, default=256)
    ap.add_argument("--quick", action="store_true")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    width, height = args.width, int(args.width * 10 / 16)

    with tempfile.TemporaryDirectory() as tmp:
        so = build(tmp)
        field = Field((0, -HALF_SEPARATION, 0), (0, HALF_SEPARATION, 0))

        print("crystal wormhole simulation -> %s" % args.out)
        flights = {
            # Straight at the attractor from the side, then hands off: the field and the pull do the rest.
            "attractor_straight": fly(so, field, (-700, -360, 0), (1, 0, 0), 30.0),
            # Off-axis: aimed 160 u wide of the attractor. The geodesics carry it in anyway.
            "attractor_offaxis": fly(so, field, (-700, -360, 160), (1, 0, 0), 30.0),
            # At the repulsor at cruise (it should hold you off), then boosting (you get through).
            "repulsor_cruise": fly(so, field, (-700, 360, 0), (1, 0, 0), 30.0),
            "repulsor_boost": fly(so, field, (-700, 360, 0), (1, 0, 0), 30.0, boost=2.4),
        }
        watch_life(so, width, height, args.out)
        approach_sheet(so, 2 * width, 2 * height, args.out)
        if args.quick:
            flights = {k: v for k, v in flights.items() if k == "attractor_straight"}
        for name, log in flights.items():
            report, ordinary = run_flight(so, name, field, log, width, height, args.out)
            ord_mean = float(np.mean(ordinary)) if ordinary else 0.0
            through = [r for r in report]
            print("%-20s transits %s  ordinary frame delta near it %.1f" %
                  (name, ", ".join("%s@%.1fs delta %.1f" % r for r in through) or "none", ord_mean))
    return 0


if __name__ == "__main__":
    sys.exit(main())
