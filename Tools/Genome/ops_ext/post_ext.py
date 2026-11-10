"""Ops requested by the fitness lab (fitness_lab/notes/spectral.md, items 3, 6 and 9): the measured gaps were low phase
congruency (no crisp beams), too much fine isotropic grain / colour speckle, and no temporal burstiness.

tex.lines     (T, GLSL)  phase-coherent line field: parallel beams, a vanishing-point fan, or random dashed segments;
                         gauss / lorentz glow / hard core + halo profile; dashes that travel along the lines
post.bandgain (P, numpy) per-frame Laplacian band gains (4 bands), soft-threshold of the finest band (the denoise tail),
                         chroma-only low-pass. Sits OVER a post.grade (input p): the renderer grades with the input, then
                         this op reshapes the uint8 frames (after the feedback loop, so it never feeds back)
cam.kick      (C, modifier over a rig like cam.shake) impulse train with power-law (Pareto) inter-arrival times; each
                         kick is a damped oscillation of the eye in a random direction: bursty camera motion
"""
import math

import numpy as np

from agenome import glsl
from agenome.ops import Op, P

from .registry import register, hsvp, EXT

f = glsl.f
v3 = glsl.v3
TAU = 2 * math.pi


# ---------------------------------------------------------------------------------------------------------- tex.lines
def g_lines(i, d, ins, T):
    B = T(0, "vec3(0.0)")
    n = max(1.0, round(d["n"]))
    mode = int(d["mode"])
    prof = int(d["profile"])
    w = f(d["width"] / 256.0)
    sd = f(d["seed"] * 1.37 + 0.11)
    th = d["theta"]
    c, s = math.cos(th), math.sin(th)
    core, halo = v3(glsl.hsv(d, "a")), v3(glsl.hsv(d, "b"))
    if mode == 1:  # vanishing-point fan: n rays around (vpx, vpy), angular jitter by spread
        geo = (f"vec2 p = uv - vec2({f(d['vpx'])}, {f(d['vpy'])}); float r = length(p) + 1e-5;"
               f" float a = atan(p.y, p.x)/6.2831853*{f(n)}; float k = floor(a + 0.5);"
               f" float jit = (h11(k*7.3+{sd}) - 0.5)*{f(d['spread'])}; float da = a - k - jit;"
               f" float dist = abs(sin(da*6.2831853/{f(n)}))*r; float sl = r; float id = k;"
               f" float on = step(h11(k*3.1+{sd}+0.5), {f(d['fill'])});")
    else:  # parallel (mode 0) or random dashed segments (mode 2) along direction theta
        geo = (f"vec2 q = mat2({f(c)},{f(-s)},{f(s)},{f(c)})*(uv-0.5); float y = q.y*{f(n)}; float k = floor(y + 0.5);"
               f" float jit = (h11(k*7.3+{sd}) - 0.5)*{f(d['spread'])}; float dist = abs(y - k - jit)/{f(n)};"
               f" float sl = q.x + h11(k*1.9+{sd})*{f(2.0 if mode == 2 else 0.0)}; float id = k;"
               f" float on = step(h11(k*3.1+{sd}+0.5), {f(d['fill'])});")
    if prof == 0:
        pr = "float cv = exp(-dist*dist/(ww*ww)); float hv = exp(-dist*dist/(ww*ww*16.0));"
    elif prof == 1:
        pr = "float cv = 1.0/(1.0 + dist*dist/(ww*ww)); float hv = 1.0/(1.0 + dist/(ww*4.0));"
    else:
        pr = "float cv = 1.0 - smoothstep(ww*0.5, ww, dist); float hv = exp(-dist/(ww*3.0));"
    # dashes travelling along the line (duty 1 = solid), and a taper toward the fan centre / line ends
    dash = (f"float ph = fract(sl*{f(d['dashf'])} - uTime*{f(d['speed'] * 0.1)} + h11(id*5.7+{sd})); "
            f"float dm = {f(d['duty'])} >= 0.999 ? 1.0 : smoothstep(0.0, 0.04, ph)*(1.0 - smoothstep({f(d['duty'])}, {f(d['duty'])}+0.04, ph));"
            f" float tp = mix(1.0, smoothstep(0.0, {f(d['taper'])}+1e-3, sl), step(0.5, {f(1.0 if mode == 1 else 0.0)}));")
    return (f"{geo} float ww = {w}*(1.0 + {f(d['grow'])}*{'sl' if mode == 1 else '0.0'}); {pr} {dash}"
            f" float m = on*dm*tp; return {B('uv')} + ({core}*cv + {halo}*hv*{f(d['halo'])})*m*{f(d['gain'])};")


register(Op(0xB7, "tex.lines", "T", ("t",), (
    P("mode", "enum", 0, 3, 100), P("n", "exp", 1, 256, 120), P("vpx", "lin", 0, 1, 128), P("vpy", "lin", 0, 1, 128),
    P("theta", "lin", 0, math.pi, 0), P("spread", "lin", 0, 1, 200), P("width", "exp", 0.3, 8, 110),
    P("profile", "enum", 0, 3, 200), P("grow", "exp", 0.01, 8, 60), P("taper", "exp", 0.01, 1, 90),
    P("fill", "lin", 0, 1, 110), P("dashf", "exp", 0.5, 40, 90), P("duty", "lin", 0.05, 1, 150),
    P("speed", "scub", 0, 40, 170), P("halo", "exp", 0.05, 4, 128), P("gain", "exp", 0.05, 8, 128), P("seed", "int", 0, 255, 0))
    + hsvp("a", 140, 60, 230) + hsvp("b", 200, 200, 150),
    "phase-coherent line field over the input: parallel beams, vanishing-point fan or random dashed segments, "
    "glow profile, travelling dashes (beams and laser lines)", "fitness lab tex.lines"), family="post", glsl_t=g_lines)


# ------------------------------------------------------------------------------------------------------ post.bandgain
register(Op(0xE8, "post.bandgain", "P", ("p",), (
    P("g1", "exp", 0.25, 4, 128), P("g2", "exp", 0.25, 4, 128), P("g3", "exp", 0.25, 4, 128), P("g4", "exp", 0.25, 4, 128),
    P("tailcut", "lin", 0, 1, 0), P("chroma", "exp", 0.25, 12, 0)),
    "Laplacian band gains (finest..coarse, 1.0 = unchanged at byte 128), soft-threshold of the finest band, chroma-only "
    "low-pass (sigma px). Applied to the frames after a post.grade input", "fitness lab post.bandgain"), family="post")


def bandgain(frames, d):
    from scipy.ndimage import gaussian_filter
    x = frames.astype(np.float32) / 255.0
    gs = [d["g1"], d["g2"], d["g3"], d["g4"]]
    tc = float(d["tailcut"])
    cs = float(d["chroma"])
    if all(abs(g - 1) < 0.03 for g in gs) and tc < 0.01 and cs < 0.3:
        return frames
    out = np.empty_like(x)
    sig = (1.0, 2.0, 4.0, 8.0)
    for k in range(len(x)):
        im = x[k]
        if cs >= 0.3:  # chroma low-pass in an opponent space (luma kept sharp)
            y = im @ np.array([0.299, 0.587, 0.114], np.float32)
            ch = im - y[..., None]
            ch = gaussian_filter(ch, (cs, cs, 0))
            im = ch + y[..., None]
        prev = im
        acc = 0.0
        for j, sg in enumerate(sig):
            gb = gaussian_filter(im, (sg, sg, 0))
            band = prev - gb
            if j == 0 and tc > 0.0:  # soft threshold: kills isotropic grain, keeps strong edges (raises band kurtosis)
                t = tc * 0.08
                band = np.sign(band) * np.maximum(np.abs(band) - t, 0.0)
            acc = acc + band * gs[j]
            prev = gb
        out[k] = acc + prev
    return (np.clip(out, 0, 1) * 255.0 + 0.5).astype(np.uint8)


# ----------------------------------------------------------------------------------------------------------- cam.kick
register(Op(0xE2, "cam.kick", "C", ("C",), (
    P("rate", "exp", 0.05, 4, 128), P("alpha", "lin", 1.05, 3, 80), P("amp", "exp", 0.002, 1, 100),
    P("decay", "exp", 0.5, 20, 128), P("freq", "exp", 0.3, 20, 110), P("tilt", "lin", 0, 1, 60), P("seed", "int", 0, 255, 0)),
    "impulse train with power-law (Pareto alpha) inter-arrival times; each kick a damped oscillation of the eye "
    "(bursty camera motion)", "fitness lab cam.kick"), family="post")


def kick_events(d, horizon=600.0):
    rng = np.random.default_rng(int(d["seed"]) * 7919 + 17)
    a = max(1.05, float(d["alpha"]))
    xm = (a - 1.0) / (a * max(1e-3, float(d["rate"])))  # mean inter-arrival = 1 / rate
    t, ev = 0.0, []
    while t < horizon and len(ev) < 4000:
        t += xm * (1.0 - rng.random()) ** (-1.0 / a)
        dv = rng.normal(size=3)
        dv[1] *= float(d["tilt"]) * 2 + 0.2
        dv /= np.linalg.norm(dv) + 1e-9
        ev.append((t, float(rng.exponential()) * float(d["amp"]), dv.astype(np.float32), float(rng.random()) * TAU))
    return ev


def kick_offset(ev, d, t):
    k, fq = float(d["decay"]), float(d["freq"]) * TAU
    win = 6.0 / k
    off = np.zeros(3, np.float32)
    for (ti, A, dv, ph) in ev:
        if ti > t:
            break
        u = t - ti
        if u < win:
            off += dv * (A * math.exp(-k * u) * math.sin(fq * u + ph) * min(1.0, u * 40.0))
    return off


def install_hooks(sim, render):
    if "cam.kick" not in sim.MODIFIERS:
        sim.MODIFIERS = tuple(sim.MODIFIERS) + ("cam.kick",)  # Camera.__init__ reads the module global
    C = sim.Camera
    orig_update = C.update

    def update(self, t, dt):
        eye, tgt = orig_update(self, t, dt)
        for op, d in self.chain[1:]:
            if op == "cam.kick":
                if not hasattr(self, "_kick_ev"):
                    self._kick_ev = {}
                key = id(d)
                if key not in self._kick_ev:
                    self._kick_ev[key] = kick_events(d)
                off = kick_offset(self._kick_ev[key], d, t) * (float(np.linalg.norm(tgt - eye)) + 1.0)
                eye = eye + off
                tgt = tgt + off * 0.3
        return eye, tgt
    C.update = update

    # post.bandgain runs in the one post-chain executor (agenome/postchain.py), which cuts every family's numpy ops
    # out of the chain in one walk and applies them in chain order. Until 2026-11-10 this module wrapped
    # Renderer.render itself and cut only bandgain, so a chain mixed with hier's post.hx_* ops ran in the order the
    # hooks were installed (bandgain first under agenome.search, last under ops_ext.run_search; code review finding)
    from agenome import postchain

    def _batch(frames, nodes):
        for n in nodes:  # application order (innermost first)
            frames = bandgain(frames, dict(_named(n)))
        return frames
    postchain.register("bandgain", ("post.bandgain",), _batch)
    postchain.install(render)


def _named(n):
    from agenome.ops import OPS
    o = OPS[n.op]
    return {p.name: p.decode(b) for p, b in zip(o.params, n.params)}
