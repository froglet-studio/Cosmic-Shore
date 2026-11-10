"""Shape (SDF) operators: fractal folds, spectral (spherical-harmonic / Fourier) shapes, superformula, heightfields.

Every op compiles to single-line GLSL bodies for `float sd<i>(vec3 p)` / `Mat mt<i>(vec3 p, vec3 n)`. Whatever can be
decided per genome (random coefficients from a seed byte, normalisations, Lipschitz safety factors of the radial
shapes) is computed here in Python at compile time, so the shader only pays for the evaluation itself.

  0xC0 sdf.kifs          kaleidoscopic IFS (Knighty): fold / rotate / scale-about-offset, any input shape as the seed
  0xC1 sdf.mandelbox     Mandelbox DE (Tom Lowe 2010): box fold + sphere fold, bounded
  0xC2 sdf.harmonic      star-shaped blob r(dir) = R (1 + amp sum c_lm Y_lm), l <= 4, seed coefficients, wobble
  0xC3 sdf.loop          closed (prism) tube around y whose radius / height are short Fourier series; solid/hollow/tunnel
  0xC4 sdf.superformula  Gielis 3D superformula (spherical product), exact radial function, numeric Lipschitz factor
  0xC5 sdf.heightfield   y = h * luma(T(xz*scale)) terrain, or T displacing an input shape (spherical projection)
  0xC6 sdf.warp          domain warp of an input shape: sine / value-noise / bend, animated, Lipschitz-corrected
  0xC7 sdf.voronoi       3D Voronoi foam walls or cell pebbles inside a sphere, animated sites (cell tissue)
  0xC8 sdf.prism         triangular / hexagonal prism along an axis, rounded (the Amoebius prism-wall cross-section)
  0xC9 sdf.path          bend the input along a winding path c(z) (two sines, scrolling): serpentine tunnels
"""
import math

import numpy as np

from agenome.glsl import f as _f, v3


def f(x):
    """GLSL float literal; negatives parenthesised so `a-{f(x)}` never becomes `a--0.3`."""
    s = _f(x)
    return f"({s})" if s.startswith("-") else s
from agenome.ops import Op, P

from .registry import register

TAU = 2 * math.pi
SIZE = P("size", "exp", 0.1, 100, 140)  # world size of the unit fractal / blob (default ~4.4)


def _rot(a):
    c, s = math.cos(a), math.sin(a)
    return f"mat2({f(c)},{f(s)},{f(-s)},{f(c)})"


def _fib(n=1600):
    """Fibonacci sphere directions [n, 3] (y up)."""
    k = np.arange(n) + 0.5
    y = 1 - 2 * k / n
    r = np.sqrt(1 - y * y)
    a = k * math.pi * (3 - 5 ** 0.5)
    return np.stack([r * np.cos(a), y, r * np.sin(a)], 1)


def _tangent_grad(fn, dirs, h=1e-3):
    """|grad_S fn| on the unit sphere by central differences along two tangents (fn: [n,3] unit dirs -> [n])."""
    up = np.where(np.abs(dirs[:, 1:2]) < 0.9, np.array([[0, 1, 0]]), np.array([[1, 0, 0]]))
    e1 = np.cross(dirs, up)
    e1 /= np.linalg.norm(e1, axis=1, keepdims=True)
    e2 = np.cross(dirs, e1)
    g2 = 0.0
    for e in (e1, e2):
        a = dirs + h * e
        b = dirs - h * e
        a /= np.linalg.norm(a, axis=1, keepdims=True)
        b /= np.linalg.norm(b, axis=1, keepdims=True)
        g2 = g2 + ((fn(a) - fn(b)) / (2 * h)) ** 2
    return np.sqrt(g2)


# ====================================================================== 0xC0 sdf.kifs
FOLDS = ["abs", "tetra", "octa", "menger", "box"]


def glsl_kifs(i, d, ins, SA, MA):
    n = int(d["iters"])
    s = d["scale"]
    sz = d["size"]
    off = [d["ox"], d["oy"], d["oz"]]
    fold = FOLDS[int(d["fold"])]
    w = d["anim"]
    a1 = f"{f(d['ax'])}+{f(w)}*uTime" if abs(w) > 1e-5 else f(d["ax"])
    a2 = f"{f(d['ay'])}-{f(0.61 * w)}*uTime" if abs(w) > 1e-5 else f(d["ay"])
    fd = {"abs": "q = abs(q);",
          "tetra": "if(q.x+q.y<0.0) q.xy = -q.yx; if(q.x+q.z<0.0) q.xz = -q.zx; if(q.y+q.z<0.0) q.zy = -q.yz;",
          "octa": "q = abs(q); if(q.x<q.y) q.xy = q.yx; if(q.x<q.z) q.xz = q.zx; if(q.y<q.z) q.yz = q.zy;",
          "box": "q = clamp(q,-1.0,1.0)*2.0-q;"}  # Mandelbox box fold (no sphere fold): rotated, gives boxy machinery
    if fold == "menger":
        c = 0.5 * off[2] * (s - 1) / s
        fs = fd["octa"] + f" q.z = {f(c)} - abs(q.z - {f(c)});"
        offv = v3([off[0] * (s - 1), off[1] * (s - 1), 0.0])
    else:
        fs = fd[fold]
        offv = v3([o * (s - 1) for o in off])
    loop = (f"vec3 q = p*{f(1 / sz)}; mat2 ra = rot2({a1}), rb = rot2({a2});"
            f" for(int k=0;k<{n};k++){{ q.xy = ra*q.xy; q.yz = rb*q.yz; {fs} q = q*{f(s)} - {offv}; }}")
    k = sz * s ** (-n)
    if ins and ins[0] is not None:
        base = SA("q")
        mt = f"{loop} return {MA('q')};"
    else:  # no seed shape: unit box (classic Menger / Sierpinski-ish seed)
        base = "length(max(abs(q)-vec3(1.0),0.0))+min(max(abs(q).x,max(abs(q).y,abs(q).z))-1.0,0.0)"
        mt = None
    if fold == "box":  # box fold alone is not contractive: an infinite lattice, so bound it by a sphere
        return f"{loop} return max(({base})*{f(k)}, length(p)-{f(2.5 * sz)});", mt
    return f"{loop} return ({base})*{f(k)};", mt


register(Op(0xC0, "sdf.kifs", "S", ("s",), (
    P("iters", "int", 1, 10, 85), P("fold", "enum", 0, 5, 128), P("ax", "lin", -math.pi, math.pi, 160),
    P("ay", "lin", -math.pi, math.pi, 148), P("scale", "exp", 1.2, 3.5, 175),
    P("ox", "scub", 0, 3, 216), P("oy", "scub", 0, 3, 216), P("oz", "scub", 0, 3, 216),
    P("anim", "scub", 0, 1, 183), SIZE),
    "kaleidoscopic IFS: n x (2 rotations (animated), fold abs/tetra/octa/menger/box (box: Mandelbox fold, sphere-bounded lattice) (animated), scale about offset), then the input "
    "shape at the folded point; distance / scale^n", "Knighty KIFS (fractalforums 2010)"),
    family="sdf", glsl_s=glsl_kifs)


# ====================================================================== 0xC1 sdf.mandelbox
def mbox_scale(x):
    """byte lin[-1,1] -> sign(x) * (1.2 .. 3): never the degenerate |s|<1.2 band."""
    return (1.2 + 1.8 * abs(x)) * (1 if x >= 0 else -1)


def glsl_mandelbox(i, d, ins, SA, MA):
    s = mbox_scale(d["scale"])
    mr2, fr2 = d["minr"] ** 2, d["fixr"] ** 2
    mr2 = min(mr2, fr2 * 0.99)
    n = int(d["iters"])
    sz = d["size"]
    w = d["anim"]
    sx = f"({f(s)}+{f(w)}*sin(uTime*0.4))" if abs(w) > 1e-5 else f(s)
    loop = (f"vec3 c = p*{f(1 / sz)}; vec4 z = vec4(c,1.0); float sc = {sx}; float tr = 1e9;"
            f" for(int k=0;k<{n};k++){{ z.xyz = clamp(z.xyz,-1.0,1.0)*2.0-z.xyz; float r2 = dot(z.xyz,z.xyz);"
            f" tr = min(tr, r2); z *= {f(fr2)}/clamp(r2,{f(mr2)},{f(fr2)}); z = vec4(z.xyz*sc, z.w*abs(sc)) + vec4(c,1.0); }}")
    sd = f"{loop} return max(length(z.xyz)/abs(z.w), length(c)-{f(d['bound'])})*{f(sz)};"
    mt = (f"{loop} vec3 al = 0.5+0.5*cos(6.2831853*(sqrt(tr)*0.6+vec3(0.0,0.33,0.67)));"
          f" return Mat(al*0.6, al*0.15, 0.4);")
    return sd, mt


register(Op(0xC1, "sdf.mandelbox", "S", (), (
    P("scale", "lin", -1, 1, 220), P("minr", "lin", 0.05, 1, 121), P("fixr", "lin", 0.5, 2, 85),
    P("iters", "int", 4, 14, 128), P("bound", "exp", 0.5, 12, 144), P("anim", "scub", 0, 0.5, 187),
    P("size", "exp", 0.1, 100, 111)),
    "Mandelbox distance estimate: box fold, sphere fold (min / fixed radius), scale s (byte lin[-1,1] -> "
    "sign*(1.2..3)), cut by a bounding sphere; orbit-trap colour", "Tom Lowe Mandelbox (2010)"),
    family="sdf", glsl_s=glsl_mandelbox)


# ====================================================================== 0xC2 sdf.harmonic
# real spherical harmonics l=1..4 as polynomials of the unit direction (x,y,z) (normalised numerically)
SH = [
    (1, "x", lambda x, y, z: x), (1, "y", lambda x, y, z: y), (1, "z", lambda x, y, z: z),
    (2, "x*y", lambda x, y, z: x * y), (2, "y*z", lambda x, y, z: y * z), (2, "x*z", lambda x, y, z: x * z),
    (2, "(x2-y2)", lambda x, y, z: x * x - y * y), (2, "(3.0*z2-1.0)", lambda x, y, z: 3 * z * z - 1),
    (3, "y*(3.0*x2-y2)", lambda x, y, z: y * (3 * x * x - y * y)), (3, "x*y*z", lambda x, y, z: x * y * z),
    (3, "y*(5.0*z2-1.0)", lambda x, y, z: y * (5 * z * z - 1)), (3, "z*(5.0*z2-3.0)", lambda x, y, z: z * (5 * z * z - 3)),
    (3, "x*(5.0*z2-1.0)", lambda x, y, z: x * (5 * z * z - 1)), (3, "z*(x2-y2)", lambda x, y, z: z * (x * x - y * y)),
    (3, "x*(x2-3.0*y2)", lambda x, y, z: x * (x * x - 3 * y * y)),
    (4, "x*y*(x2-y2)", lambda x, y, z: x * y * (x * x - y * y)), (4, "y*z*(3.0*x2-y2)", lambda x, y, z: y * z * (3 * x * x - y * y)),
    (4, "x*y*(7.0*z2-1.0)", lambda x, y, z: x * y * (7 * z * z - 1)), (4, "y*z*(7.0*z2-3.0)", lambda x, y, z: y * z * (7 * z * z - 3)),
    (4, "(35.0*z2*z2-30.0*z2+3.0)", lambda x, y, z: 35 * z ** 4 - 30 * z * z + 3),
    (4, "x*z*(7.0*z2-3.0)", lambda x, y, z: x * z * (7 * z * z - 3)), (4, "(x2-y2)*(7.0*z2-1.0)", lambda x, y, z: (x * x - y * y) * (7 * z * z - 1)),
    (4, "x*z*(x2-3.0*y2)", lambda x, y, z: x * z * (x * x - 3 * y * y)),
    (4, "(x2*(x2-3.0*y2)-y2*(3.0*x2-y2))", lambda x, y, z: x * x * (x * x - 3 * y * y) - y * y * (3 * x * x - y * y)),
]


def harmonic_coeffs(seed, lmax, decay, amp, anim):
    """-> (A, B, ls, k): per-basis cos / sin coefficients (normalised so max |sum| = 1), and the Lipschitz factor."""
    rng = np.random.default_rng(1000 + int(seed))
    D = _fib(1200)
    basis = np.stack([fn(D[:, 0], D[:, 1], D[:, 2]) for _, _, fn in SH], 1)
    rms = np.sqrt((basis ** 2).mean(0))
    ls = np.array([l for l, _, _ in SH])
    wl = (ls ** (-decay)) * (ls <= lmax) * (ls >= 2) / rms  # l=1 would only shift the blob
    A = rng.normal(size=len(SH)) * wl
    B = rng.normal(size=len(SH)) * wl if anim else np.zeros(len(SH))
    # worst case over random phase combinations per degree (the anim makes every degree run at its own rate)
    phases = [np.zeros(5)] + ([rng.uniform(0, TAU, 5) for _ in range(23)] if anim else [])

    def S_of(dirs, ph):
        cs, sn = np.cos(ph)[ls], np.sin(ph)[ls]
        coef = A * cs + B * sn
        return np.stack([fn(dirs[:, 0], dirs[:, 1], dirs[:, 2]) for _, _, fn in SH], 1) @ coef
    M = max(np.abs(S_of(D, ph)).max() for ph in phases) + 1e-9
    A, B = A / M, B / M
    kk = 1.0
    for ph in phases[:8]:
        Sv = S_of(D, ph)
        G = _tangent_grad(lambda dd: S_of(dd, ph), D)
        g = (amp * G / np.maximum(1 + amp * Sv, 0.05)).max()
        kk = min(kk, 1 / math.sqrt(1 + g * g))
    return A, B, ls, max(0.05, kk * 0.95)


def glsl_harmonic(i, d, ins, SA, MA):
    lmax, amp, w = int(d["lmax"]), d["amp"], d["anim"]
    anim = abs(w) > 1e-4
    A, B, ls, k = harmonic_coeffs(d["seed"], lmax, d["decay"], amp, anim)
    R = d["R"]
    terms = []
    for j, (l, ex, _) in enumerate(SH):
        if l > lmax or l < 2:
            continue
        a = A[j] * amp
        if anim:
            b = B[j] * amp
            terms.append(f"({f(a)}*c{l}+{f(b)}*s{l})*{ex}")
        else:
            terms.append(f"{f(a)}*{ex}")
    pre = ""
    if anim:
        for l in range(2, lmax + 1):
            wl = w * (0.6 + 0.45 * l) * (1 if l % 2 else -1)
            pre += f" float c{l} = cos({f(wl)}*uTime), s{l} = sin({f(wl)}*uTime);"
    sd = (f"float L = length(p); vec3 e = p/max(L,1e-6); float x = e.x, y = e.y, z = e.z;"
          f" float x2 = x*x, y2 = y*y, z2 = z*z;{pre} float S = {'+'.join(terms) or '0.0'};"
          f" return (L-{f(R)}*(1.0+S))*{f(k)};")
    mt = (f"float L = length(p); vec3 e = p/max(L,1e-6); float x = e.x, y = e.y, z = e.z;"
          f" float x2 = x*x, y2 = y*y, z2 = z*z;{pre} float S = {'+'.join(terms) or '0.0'};"
          f" vec3 al = 0.5+0.5*cos(6.2831853*(S*0.8+vec3(0.0,0.33,0.67))); return Mat(al*0.6, al*0.2, 0.5);")
    return sd, mt


register(Op(0xC2, "sdf.harmonic", "S", (), (
    P("R", "exp", 0.2, 100, 140), P("amp", "lin", 0, 0.9, 210), P("lmax", "int", 2, 4, 255),
    P("decay", "lin", 0, 3, 30), P("seed", "int", 0, 255, 0), P("anim", "scub", 0, 3, 190)),
    "star-shaped blob r(dir) = R(1 + amp sum_lm c_lm Y_lm(dir)), real spherical harmonics l=2..lmax, coefficients "
    "from the seed with l^-decay power decay; anim runs every degree at its own rate (wobbling amoeba); exact "
    "Lipschitz factor computed at compile time", "spherical harmonics (spectral shape synthesis)"),
    family="sdf", glsl_s=glsl_harmonic)


# ====================================================================== 0xC3 sdf.loop
def loop_coeffs(seed, K, ramp, yamp):
    rng = np.random.default_rng(2000 + int(seed))
    kr = np.arange(2, K + 2)
    ky = np.arange(1, K + 1)
    a = rng.uniform(0.4, 1.0, K) * rng.choice([-1, 1], K) / kr ** 0.6
    b = rng.uniform(0.4, 1.0, K) * rng.choice([-1, 1], K) / ky ** 0.6
    a *= ramp / (np.abs(a).sum() + 1e-9)
    b *= yamp / (np.abs(b).sum() + 1e-9)
    pa, pb = rng.uniform(0, TAU, K), rng.uniform(0, TAU, K)
    wa = rng.uniform(0.5, 1.5, K) * rng.choice([-1, 1], K)
    wb = rng.uniform(0.5, 1.5, K) * rng.choice([-1, 1], K)
    return kr, a, pa, wa, ky, b, pb, wb


def glsl_loop(i, d, ins, SA, MA):
    K = int(d["K"])
    R0, r = d["R"], d["r"]
    kr, a, pa, wa, ky, b, pb, wb = loop_coeffs(d["seed"], K, d["ramp"], d["yamp"])
    w = d["anim"]

    def arg(k, ph, wk):
        t = f"+{f(wk * w)}*uTime" if abs(w) > 1e-5 else ""
        return f"{k}.0*th+{f(ph)}{t}"

    def curve(deriv=True):
        Rx = f"{f(R0)}" + "".join(f"+{f(R0 * a[j])}*cos({arg(kr[j], pa[j], wa[j])})" for j in range(K))
        Yx = "0.0" + "".join(f"+{f(R0 * b[j])}*cos({arg(ky[j], pb[j], wb[j])})" for j in range(K))
        s = f"float R = {Rx}; float Y = {Yx}; float ct = cos(th), st = sin(th); vec3 c = vec3(R*ct, Y, R*st);"
        if deriv:
            dR = "0.0" + "".join(f"-{f(R0 * a[j] * kr[j])}*sin({arg(kr[j], pa[j], wa[j])})" for j in range(K))
            dY = "0.0" + "".join(f"-{f(R0 * b[j] * ky[j])}*sin({arg(ky[j], pb[j], wb[j])})" for j in range(K))
            s += f" float dR = {dR}; float dY = {dY}; vec3 dc = vec3(dR*ct-R*st, dY, dR*st+R*ct);"
        return s

    sides = int(d["sides"])
    poly = sides >= 3
    # one Newton step from the polar angle of p: theta that makes (p - c) orthogonal to the tangent
    body = (f"float th = atan(p.z, p.x); {curve(True)} th += clamp(dot(p-c, dc)/max(dot(dc,dc),1e-6),-0.6,0.6);"
            f" {{ {curve(poly)} vec3 w = p-c;")
    if poly:
        tw = int(d["twist"]) / sides
        spin = d["spin"]
        rexp = f"{f(tw)}*th" + (f"+{f(spin)}*uTime" if abs(spin) > 1e-5 else "")
        an = math.pi / sides
        body += (f" vec3 T = normalize(dc); vec3 er = vec3(ct,0.0,st); vec3 N = normalize(er-dot(er,T)*T);"
                 f" vec2 uv = rot2({rexp})*vec2(dot(w,N), dot(w,cross(T,N))); float a = atan(uv.y,uv.x);"
                 f" a = mod(a+{f(an)},{f(2 * an)})-{f(an)};"
                 f" float dd = max(length(uv)*cos(a)-{f(r)}, length(w)-{f(r / math.cos(an))});")
    else:
        body += f" float dd = length(w)-{f(r)};"
    mode = int(d["mode"])
    if mode == 1:
        body += f" dd = abs(dd)-{f(r * 0.12)};"
    ribs = int(d["ribs"])
    if ribs > 0 and mode != 2:  # cut the tube into prism rings (60 % solid), arc-length distance to the cut planes
        body += f" dd = max(dd, (abs(fract(th*{f(ribs / TAU)})-0.5)-0.3)*{f(R0 * TAU / ribs)});"
    if mode == 2:
        body += " dd = -dd;"
    sd = body + " return dd*0.8; }"
    mt = (f"float th = atan(p.z, p.x); vec3 al = 0.5+0.5*cos(th*{K + 1}.0+vec3(0.0,2.1,4.2));"
          f" return Mat(al*0.5, al*0.25, 0.5);")
    return sd, mt


register(Op(0xC3, "sdf.loop", "S", (), (
    P("R", "exp", 0.2, 200, 125), P("r", "exp", 0.01, 40, 142), P("K", "int", 1, 4, 128),
    P("ramp", "lin", 0, 0.6, 90), P("yamp", "lin", 0, 0.6, 110), P("seed", "int", 0, 255, 0),
    P("sides", "int", 2, 8, 64), P("twist", "int", -8, 8, 160), P("spin", "scub", 0, 4, 128),
    P("anim", "scub", 0, 2, 175), P("mode", "enum", 0, 3, 0), P("ribs", "int", 0, 64, 0)),
    "closed tube around the y axis: radius R(th) and height Y(th) are K-term Fourier series (seed coefficients, "
    "animated phases); round or n-gon (prism) cross-section twisting along the loop; solid / hollow shell / "
    "inverted tunnel; ribs cut it into prism rings. Nearest point by one Newton step from atan(z,x)", "Fourier curves; Amoebius prism tunnels"),
    family="sdf", glsl_s=glsl_loop)


# ====================================================================== 0xC4 sdf.superformula
def _sf(ang, m, n1, n2, n3):
    t = m * ang * 0.25
    return np.minimum((np.abs(np.cos(t)) ** n2 + np.abs(np.sin(t)) ** n3) ** (-1.0 / n1), 8.0)


def _sf_radius(D, d, mf=1.0):
    x, y, z = D[:, 0], D[:, 1], D[:, 2]
    th = np.arctan2(z, x)
    r1 = _sf(th, d["m1"], d["n11"] * mf, d["n12"], d["n13"])
    ph = np.arctan2(r1 * y, np.hypot(x, z))
    r2 = _sf(ph, d["m2"], d["n21"] * mf, d["n22"], d["n23"])
    return r2 * np.sqrt(r1 * r1 * np.cos(ph) ** 2 + np.sin(ph) ** 2)


def superformula_consts(d):
    D = _fib(4000)
    mo = d["morph"]
    mfs = [1.0] if mo < 1e-4 else [math.exp(mo * s) for s in (-1, -0.5, 0, 0.5, 1)]
    mx, kk = 1e-9, 1.0
    for mf in mfs:
        Rv = _sf_radius(D, d, mf)
        mx = max(mx, Rv.max())
        G = _tangent_grad(lambda dd: _sf_radius(dd, d, mf), D, 1e-3)
        g = np.percentile(G / np.maximum(Rv, 1e-6), 99.9)  # the very tips of cusps may be slightly over
        kk = min(kk, 1 / math.sqrt(1 + g * g))
    return 1.0 / mx, min(1.0, max(0.12, kk * 1.4))  # 1.4: grazing ridges converge, no visible holes


def glsl_superformula(i, d, ins, SA, MA):
    norm, k = superformula_consts(d)
    sz = d["size"]
    mo = d["morph"]
    mf = f"exp({f(mo)}*sin(uTime*0.6))" if mo > 1e-4 else "1.0"

    def sf(a, m, n1, n2, n3, mfx):
        t = f"{f(m * 0.25)}*{a}"
        return (f"min(pow(pow(abs(cos({t})),{f(n2)})+pow(abs(sin({t})),{f(n3)}), -1.0/({f(n1)}*{mfx})), 8.0)")
    body = (f"vec3 q = p*{f(1 / sz)}; float mf = {mf}; float th = atan(q.z, q.x);"
            f" float r1 = {sf('th', d['m1'], d['n11'], d['n12'], d['n13'], 'mf')};"
            f" float ph = atan(r1*q.y, length(q.xz));"
            f" float r2 = {sf('ph', d['m2'], d['n21'], d['n22'], d['n23'], 'mf')};"
            f" float cp = cos(ph), sp = sin(ph); float R = r2*sqrt(r1*r1*cp*cp+sp*sp)*{f(norm)};")
    sd = f"{body} return (length(q)-R)*{f(k * sz)};"
    mt = (f"{body} vec3 al = 0.5+0.5*cos(6.2831853*(R*0.9+vec3(0.0,0.33,0.67))); return Mat(al*0.6, al*0.2, 0.5);")
    return sd, mt


register(Op(0xC4, "sdf.superformula", "S", (), (
    P("m1", "int", 0, 16, 48), P("n11", "exp", 0.1, 40, 59), P("n12", "exp", 0.7, 40, 23), P("n13", "exp", 0.7, 40, 23),
    P("m2", "int", 0, 16, 64), P("n21", "exp", 0.1, 40, 68), P("n22", "exp", 0.7, 40, 23), P("n23", "exp", 0.7, 40, 23),
    P("morph", "lin", 0, 1.5, 51), SIZE),
    "Gielis superformula in 3D (spherical product of two 2D superformulas, a=b=1), exact radial function of the "
    "direction, normalised to unit max radius; Lipschitz factor estimated on the sphere at compile time; morph "
    "breathes n1", "Gielis 2003 superformula"),
    family="sdf", glsl_s=glsl_superformula)


# ====================================================================== 0xC5 sdf.heightfield
def glsl_heightfield(i, d, ins, SA, MA):
    j = ins[1] if len(ins) > 1 else None
    T = (lambda uv: f"t{j}({uv})") if j is not None else (lambda uv: f"vec3(fbm2({uv}, 4.0, 5, 0.5, 3.0))")
    H, sc, g, lip = d["height"], d["scale"], d["gamma"], d["lip"]
    mode = int(d["mode"])
    has_s = bool(ins) and ins[0] is not None
    hexp = f"{f(H)}*pow(clamp(luma({T('uv')})*{f(d['gain'])},0.0,1.0),{f(g)})"
    if mode == 0 or (mode == 2 and not has_s):
        uvs = f"vec2 uv = p.xz*{f(sc)};"
        sd = f"{uvs} return (p.y-{f(d['y0'])}-{hexp})*{f(lip)};"
        mt = f"{uvs} vec3 tc = max({T('uv')},vec3(0.0)); return Mat(tc, tc*0.15, 0.3);"
        return sd, mt
    if mode == 2:  # displace the input along a planar xz projection
        uvs = f"vec2 uv = p.xz*{f(sc)};"
    else:  # spherical projection with whole tile counts (FFT textures tile: no seam)
        nt = max(1, round(sc * 64))
        uvs = (f"vec3 e = normalize(p); vec2 uv = vec2(atan(e.z,e.x)*{f(nt / TAU)}, "
               f"acos(clamp(e.y,-1.0,1.0))*{f(max(1, nt // 2) / math.pi)});")
    base = SA("p") if has_s else f"(length(p)-{f(d['size'])})"
    sd = f"{uvs} return ({base}-{hexp})*{f(lip)};"
    mt = (f"{uvs} vec3 tc = max({T('uv')},vec3(0.0)); return Mat(tc, tc*0.15, 0.3);")
    return sd, mt


register(Op(0xC5, "sdf.heightfield", "S", ("s", "T"), (
    P("height", "exp", 0.05, 100, 191), P("scale", "exp", 0.001, 2, 77), P("gamma", "exp", 0.25, 4, 150),
    P("lip", "lin", 0.1, 1, 140), P("y0", "scub", 0, 100, 60), P("mode", "enum", 0, 3, 0), SIZE,
    P("gain", "exp", 0.25, 16, 160)),
    "terrain from any texture: plane y = y0 + h*clamp(gain*luma(T(xz*scale)))^gamma (mode 0), or T displacing the input shape "
    "with a spherical (1) / planar (2) projection (no input: a sphere of radius size); albedo = the texture",
    "werkkzeug Mesh_Displace / heightmap terrain"),
    family="sdf", glsl_s=glsl_heightfield)


# ====================================================================== 0xC6 sdf.warp
def glsl_warp(i, d, ins, SA, MA):
    mode = int(d["mode"])
    fq, amp, sp = d["freq"], d["amp"], d["speed"]
    t = f"+{f(sp)}*uTime" if abs(sp) > 1e-5 else ""
    if mode == 0:  # sine warp: Lipschitz <= 1 + amp*freq*sqrt(3)
        q = f"vec3 q = p + {f(amp)}*sin(p.yzx*{f(fq)}+vec3(0.0,1.7,3.1){t});"
        k = 1 / (1 + amp * fq * 1.2)
    elif mode == 1:  # value-noise warp (3 lookups)
        q = (f"vec3 wp = p*{f(fq)}+vec3(0.0,0.0,1.0){t}; vec3 q = p + {f(2 * amp)}*(vec3(vn3(wp), vn3(wp+vec3(17.1,3.3,9.7)),"
             f" vn3(wp+vec3(5.9,31.7,1.3)))-0.5);")
        k = 1 / (1 + amp * fq * 2.6)
    else:  # bend about the y axis: x-y plane curls with x (iq opCheapBend), animated amount
        bend = f"({f(fq * 0.1)}{f'*(1.0+{f(amp)}*sin({f(sp)}*uTime))' if abs(sp) > 1e-5 else ''})"
        q = f"float ba = {bend}*p.x; vec3 q = vec3(rot2(ba)*p.xy, p.z);"
        k = 1 / (1 + 0.5 * fq * 0.1 * 30)
    k = max(0.15, min(1.0, k))
    return f"{q} return {SA('q')}*{f(k)};", f"{q} return {MA('q')};"


register(Op(0xC6, "sdf.warp", "S", ("S",), (
    P("mode", "enum", 0, 3, 0), P("freq", "exp", 0.01, 10, 151), P("amp", "exp", 0.01, 20, 155),
    P("speed", "scub", 0, 4, 170)),
    "domain warp of the input: sine (cheap, wavy), value noise (organic), bend; animated; distance scaled by the "
    "warp's Lipschitz bound", "Mesh_Bend / iq domain warping"),
    family="sdf", glsl_s=glsl_warp)


# ====================================================================== 0xC7 sdf.voronoi
def glsl_voronoi(i, d, ins, SA, MA):
    cs, th, bound, w = d["cell"], d["thick"], d["bound"], d["anim"]
    mode = int(d["mode"])
    jit = d["jitter"] * 0.5
    sd0 = d["seed"] * 7.13
    mov = f"+{f(jit * 0.35)}*sin({f(w)}*uTime+6.2831853*vec3(h31(g+{f(sd0 + 1.1)}),h31(g+{f(sd0 + 2.3)}),h31(g+{f(sd0 + 3.7)})))" \
        if abs(w) > 1e-5 else ""
    # 2x2x2 neighbourhood around the nearest lattice corner (cheap; sites jittered < 0.5 so artifacts stay small)
    loop = (f"vec3 q = p*{f(1 / cs)}; vec3 b = floor(q-0.5); vec3 a1 = vec3(9.0), a2 = vec3(9.0); float d1 = 9.0, d2 = 9.0; vec3 id1 = vec3(0.0);"
            f" for(int k=0;k<8;k++){{ vec3 g = b+vec3(float(k&1),float((k>>1)&1),float(k>>2));"
            f" vec3 o = g+0.5+{f(jit)}*(vec3(h31(g+{f(sd0)}),h31(g+{f(sd0 + 11.3)}),h31(g+{f(sd0 + 23.9)}))-0.5){mov};"
            f" float dd = dot(o-q,o-q); if(dd<d1){{ d2 = d1; a2 = a1; d1 = dd; a1 = o; id1 = g; }} else if(dd<d2){{ d2 = dd; a2 = o; }} }}"
            f" float wall = dot(0.5*(a1+a2)-q, normalize(a2-a1));")
    if mode == 0:
        dist = f"(wall-{f(th)})"
    else:
        dist = f"({f(th)}-wall)"
    sd = f"{loop} return max({dist}*{f(cs * 0.9)}, length(p)-{f(bound)});"
    mt = (f"{loop} vec3 al = 0.5+0.5*cos(6.2831853*(h31(id1+{f(sd0)})+vec3(0.0,0.33,0.67)));"
          f" return Mat(al*0.6, al*0.3, 0.5);")
    return sd, mt


register(Op(0xC7, "sdf.voronoi", "S", (), (
    P("cell", "exp", 0.1, 50, 120), P("thick", "lin", 0.0, 0.4, 40), P("jitter", "lin", 0, 1, 200),
    P("bound", "exp", 1, 400, 76), P("mode", "enum", 0, 2, 0), P("seed", "int", 0, 255, 0), P("anim", "scub", 0, 3, 170)),
    "3D Voronoi: foam walls (mode 0) or rounded cell pebbles (mode 1) inside a bounding sphere, exact bisector "
    "distance of the two nearest sites in a 2x2x2 neighbourhood, animated sites, per-cell colour",
    "Worley 1996 cellular; cell tissue / foam"),
    family="sdf", glsl_s=glsl_voronoi)


# ====================================================================== 0xC8 sdf.prism
def glsl_prism(i, d, ins, SA, MA):
    w, L, rr = d["w"], d["len"], d["round"] * d["w"]
    sw = ["p.zyx", "p.xzy", "p"][int(d["axis"])]  # prism length runs along the chosen axis
    if int(d["mode"]) == 0:  # equilateral triangle, apex +y (iq sdTriPrism bound)
        h = w - rr
        sd = (f"vec3 q = {sw}; vec3 a = abs(q); return max(a.z-{f(L - rr)}, max(a.x*0.866025+q.y*0.5, -q.y)-{f(h * 0.5)})"
              f"-{f(rr)};")
    else:  # hexagon (iq sdHexPrism, exact)
        h = w - rr
        sd = (f"vec3 q = abs({sw}); const vec3 k = vec3(-0.8660254, 0.5, 0.57735); q.xy -= 2.0*min(dot(k.xy, q.xy), 0.0)*k.xy;"
              f" vec2 e = vec2(length(q.xy-vec2(clamp(q.x,{f(-0.57735 * h)},{f(0.57735 * h)}),{f(h)}))*sign(q.y-{f(h)}),"
              f" q.z-{f(L - rr)}); return min(max(e.x,e.y),0.0)+length(max(e,0.0))-{f(rr)};")
    return sd, None


register(Op(0xC8, "sdf.prism", "S", (), (
    P("w", "exp", 0.02, 50, 157), P("len", "exp", 0.02, 200, 140), P("mode", "enum", 0, 2, 0),
    P("round", "lin", 0, 0.5, 20), P("axis", "enum", 0, 3, 255)),
    "prism: equilateral triangle (mode 0, apex +y) or hexagon (mode 1) cross-section of half-size w, half-length len "
    "along x/y/z, rounded; with polar / repeat / twist / path it builds prism walls and tunnels",
    "Amoebius prism walls; iq sdTriPrism / sdHexPrism"),
    family="sdf", glsl_s=glsl_prism)


# ====================================================================== 0xC9 sdf.path
def glsl_path(i, d, ins, SA, MA):
    a1, f1, a2, f2 = d["a1"], d["f1"], d["a2"], d["f2"]
    sc, ph = d["scroll"], d["phase"]
    t = f"+{f(sc)}*uTime" if abs(sc) > 1e-5 else ""
    q = f"vec3 q = p; q.xy -= vec2({f(a1)}*sin({f(f1)}*p.z{t}), {f(a2)}*sin({f(f2)}*p.z+{f(ph)}{t}));"
    # the shear (x,y,z) -> (x - c1(z), y - c2(z), z) has largest singular value (g + sqrt(g^2+4))/2, g = |c'|max
    g = math.hypot(a1 * f1, a2 * f2)
    k = 2.0 / (g + math.sqrt(g * g + 4.0))
    return f"{q} return {SA('q')}*{f(k)};", f"{q} return {MA('q')};"


register(Op(0xC9, "sdf.path", "S", ("S",), (
    P("a1", "exp", 0.01, 100, 158), P("f1", "exp", 0.001, 2, 168), P("a2", "exp", 0.01, 100, 147),
    P("f2", "exp", 0.001, 2, 182), P("scroll", "scub", 0, 4, 170), P("phase", "lin", 0, TAU, 64)),
    "bend the input along a winding path: xy offset by c(z) = (a1 sin(f1 z + scroll t), a2 sin(f2 z + phase + scroll t)); "
    "straight repeated structures become serpentine tunnels; distance scaled by the exact shear Lipschitz bound",
    "demoscene tunnel paths"),
    family="sdf", glsl_s=glsl_path)
