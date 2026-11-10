"""Render test for ops_ext.sdf_ext: one small genome per shape op -> contact sheets in renders/sdf/.

    python3 ops_ext/tests/test_sdf.py [op-substring ...]

Each genome: shape -> sdf.material (neon emissive) -> out, with cam.orbit and post.grade. 8 frames are rendered
1.5 s of game time apart (so the animation shows), checked for blank / degenerate frames, timed, and byte-roundtripped.
"""
import os
import sys
import time

os.environ.setdefault("LP_NUM_THREADS", "2")  # llvmpipe: share the box
import os as _os; _H = _os.path.dirname(_os.path.dirname(_os.path.dirname(_os.path.abspath(__file__)))); sys.path[:0] = [_H, _os.path.join(_H, "genome")]  # ops_ext's parent (and a genome/ beside it)

import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

import ops_ext  # noqa: E402

ops_ext.install()
from agenome.genome import Genome  # noqa: E402
from agenome.render import Renderer, W, H  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "renders", "sdf")
NF = 8
DT = 1.5


def scaffold(g, shape, hue, cam=(22, 6), steps=64, far=200, emit=0.2, glow=0.003, sky=None, fog=0.0002, texmix=0.6):
    names = dict(ah=hue, as_=0.55, av=0.45, eh=(hue + 0.08) % 1, es=0.9, ev=1.0, emit=emit, glow=glow,
                 falloff=1.5, spec=0.6, texmix=texmix)
    from agenome.handmade import _named
    m = g.add("sdf.material", shape, **_named("sdf.material", **names))
    if sky is None:  # a dark sky node, so the root's sky slot does not pick up the shape's texture
        sky = g.add("tex.flat", **_named("tex.flat", h=0.7, s=0.6, v=0.012))
    c = g.addv("cam.orbit", radius=cam[0], height=cam[1], speed=0.25, fov=60)
    pg = g.add("post.grade", **_named("post.grade", exposure=1.3, thresh=0.6, bloom=0.8, bloomrad=2.0, fh=0.7, fs=0.6,
                                      fv=0.02, fog=fog, vignette=0.5, sat=1.2, contrast=1.05))
    g.add("out", m, None, c, pg, sky, None, **_named("out", sky=0.6, steps=steps, far=far, ltheta=0.8, lphi=0.6,
                                                    ambient=0.25, lv=1.2, ls=0.2, lh=0.1, warmup=0))
    return g.repair()


def nebula(g):
    return g.addv("tex.perlin", freq=2, oct=3, persist=0.5, seed=5, gamma=1.2)


# ---------------------------------------------------------------------- one case per op (name, genome builder)
def c_kifs():
    g = Genome(seed=1)
    b = g.addv("sdf.box", sx=0.9, sy=0.9, sz=0.9, round=0.1)
    s = g.add("sdf.kifs", b)
    return scaffold(g, s, 0.55, cam=(13, 4))


def c_kifs_tetra():
    g = Genome(seed=1)
    b = g.addv("sdf.octa", r=1.2)
    s = g.addv("sdf.kifs", b, fold=1, iters=7, scale=2.0)
    return scaffold(g, s, 0.85, cam=(13, 4))


def c_kifs_menger():
    g = Genome(seed=1)
    s = g.addv("sdf.kifs", fold=3, iters=4, scale=3.0, ox=1.0, oy=1.0, oz=1.0, ax=0.0, ay=0.0, anim=0.08)
    return scaffold(g, s, 0.1, cam=(13, 5))


def c_mandelbox():
    g = Genome(seed=1)
    s = g.add("sdf.mandelbox")
    return scaffold(g, s, 0.48, cam=(12, 4))


def c_harmonic():
    g = Genome(seed=1)
    s = g.add("sdf.harmonic")
    return scaffold(g, s, 0.33, cam=(16, 4))


def c_loop():
    g = Genome(seed=1)
    s = g.add("sdf.loop")
    return scaffold(g, s, 0.6, cam=(18, 8))


def c_loop_tunnel():
    g = Genome(seed=2)
    s = g.addv("sdf.loop", mode=1, sides=4, K=3, r=1.6, ribs=36, twist=1)
    return scaffold(g, s, 0.95, cam=(18, 9))


def c_superformula():
    g = Genome(seed=1)
    s = g.add("sdf.superformula")
    return scaffold(g, s, 0.75, cam=(14, 4))


def c_heightfield():
    g = Genome(seed=1)
    t = g.add("tex.spectral") if "tex.spectral" in ops_ext.EXT else g.addv("tex.perlin", freq=3, oct=5)
    s = g.add("sdf.heightfield", None, t)
    return scaffold(g, s, 0.45, cam=(35, 14), far=400, fog=0.003, texmix=0.15)


def c_heightfield_sphere():
    g = Genome(seed=1)
    t = g.addv("tex.perlin", freq=4, oct=4, persist=0.55, seed=3)
    b = g.addv("sdf.sphere", r=4.5)
    s = g.addv("sdf.heightfield", b, t, mode=1, height=1.2, scale=0.05)
    return scaffold(g, s, 0.05, cam=(15, 4))


def c_warp():
    g = Genome(seed=1)
    b = g.addv("sdf.torus", R=4.0, r=0.3)
    s = g.add("sdf.warp", b)
    return scaffold(g, s, 0.15, cam=(15, 6))


def c_voronoi():
    g = Genome(seed=1)
    s = g.add("sdf.voronoi")
    return scaffold(g, s, 0.52, cam=(16, 5))


def c_kifs_box():
    g = Genome(seed=1)
    b = g.addv("sdf.box", sx=0.6, sy=0.6, sz=0.6)
    s = g.addv("sdf.kifs", b, fold=4, iters=3, scale=2.0, ax=0.4, ay=0.28, ox=0, oy=0, oz=0, size=2.0, anim=0.05)
    return scaffold(g, s, 0.3, cam=(14, 4))


def c_prism():
    g = Genome(seed=1)
    s = g.addv("sdf.prism", w=4.0, len=4.0, round=0.05)
    s = g.addv("sdf.spin", s, axis=1, speed=0.3)
    return scaffold(g, s, 0.58, cam=(14, 4))


def c_prism_tunnel():
    """prism -> translate -> polar ring around z -> repeat along z -> path: a serpentine prism tunnel."""
    g = Genome(seed=1)
    s = g.addv("sdf.prism", w=0.8, len=0.6, mode=0, round=0.05)
    s = g.addv("sdf.translate", s, x=4.0)
    s = g.addv("sdf.polar", s, count=9, axis=2)
    s = g.addv("sdf.repeat", s, axes=4, pz=2.5)
    s = g.add("sdf.path", s)
    return scaffold(g, s, 0.85, cam=(26, 10), far=150, fog=0.01)


def c_path():
    g = Genome(seed=1)
    b = g.addv("sdf.torus", R=2.5, r=0.12)
    b = g.addv("sdf.rotate", b, ax=1.5708)
    b = g.addv("sdf.repeat", b, axes=4, pz=1.5)
    s = g.add("sdf.path", b)
    return scaffold(g, s, 0.12, cam=(24, 9), far=150, fog=0.01)


CASES = [("sdf.kifs", c_kifs), ("sdf.kifs_tetra", c_kifs_tetra), ("sdf.kifs_menger", c_kifs_menger),
         ("sdf.mandelbox", c_mandelbox), ("sdf.harmonic", c_harmonic), ("sdf.loop", c_loop),
         ("sdf.loop_shell", c_loop_tunnel), ("sdf.superformula", c_superformula), ("sdf.heightfield", c_heightfield),
         ("sdf.heightfield_sphere", c_heightfield_sphere), ("sdf.warp", c_warp), ("sdf.voronoi", c_voronoi),
         ("sdf.kifs_box", c_kifs_box), ("sdf.prism", c_prism), ("sdf.prism_tunnel", c_prism_tunnel),
         ("sdf.path", c_path)]


def render_case(R, g):
    frames, times = [], []
    for k in range(NF):
        t0 = time.time()
        fr = R.render(g, 1, t0=0.5 + k * DT)
        times.append(time.time() - t0)
        frames.append(fr[0])
    return np.stack(frames), times


def main(filt):
    os.makedirs(OUT, exist_ok=True)
    R = Renderer(W, H)
    rows, report, fails = [], [], []
    for name, fn in CASES:
        if filt and not any(s in name for s in filt):
            continue
        g = fn()
        # byte roundtrip
        g2 = Genome.from_bytes(g.to_bytes())
        rt = [(n.op, n.params, n.inputs) for n in g.nodes] == [(n.op, n.params, n.inputs) for n in g2.nodes]
        try:
            R.render(g, 1, t0=0.0)  # compile (not timed)
            frames, times = render_case(R, g)
        except Exception as e:  # noqa: BLE001
            print(f"{name:26s} FAILED: {e}")
            fails.append(name)
            continue
        fl = frames.astype(np.float32)
        mean, std = fl.mean(), fl.std()
        lit = (fl.max(-1) > 40).mean()
        ok = mean > 4 and std > 4 and 0.02 < lit < 0.995 and rt
        if not ok:
            fails.append(name)
        t_med = float(np.median(times[1:])) if len(times) > 1 else times[0]
        report.append((name, t_med, mean, std, lit, rt, ok))
        print(f"{name:26s} {t_med * 1000:7.1f} ms/frame  mean {mean:5.1f} std {std:5.1f} lit {lit:.2f} "
              f"roundtrip {'ok' if rt else 'FAIL'}  bytes {g.raw_size()} cost {g.cost()[0]}  {'OK' if ok else 'CHECK'}")
        sheet = Image.new("RGB", (4 * W, 2 * H))
        for k in range(NF):
            sheet.paste(Image.fromarray(frames[k]), ((k % 4) * W, (k // 4) * H))
        sheet.save(os.path.join(OUT, f"{name}.png"))
        row = Image.new("RGB", (NF * W // 2, H // 2))
        for k in range(NF):
            row.paste(Image.fromarray(frames[k]).resize((W // 2, H // 2), Image.BILINEAR), (k * W // 2, 0))
        rows.append(row)
    if rows and not filt:
        allsheet = Image.new("RGB", (rows[0].width, sum(r.height for r in rows)))
        y = 0
        for r in rows:
            allsheet.paste(r, (0, y))
            y += r.height
        allsheet.save(os.path.join(OUT, "all.png"))
    if fails:
        print("CHECK:", fails)
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
