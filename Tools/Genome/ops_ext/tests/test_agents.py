"""Render-test the agents_ext ops: each op vs the same genome without it; contact sheets in renders/agents/.

    python ops_ext/tests/test_agents.py [op ...]
"""
import os
import sys
import time

os.environ.setdefault("OMP_NUM_THREADS", "2")
os.environ.setdefault("OPENBLAS_NUM_THREADS", "2")
os.environ.setdefault("LP_NUM_THREADS", "2")  # llvmpipe
import os as _os; _H = _os.path.dirname(_os.path.dirname(_os.path.dirname(_os.path.abspath(__file__)))); sys.path[:0] = [_H, _os.path.join(_H, "genome")]  # ops_ext's parent (and a genome/ beside it)

import numpy as np  # noqa: E402

import ops_ext  # noqa: E402

ops_ext.install()
from agenome.genome import Genome  # noqa: E402
from agenome.handmade import _named  # noqa: E402
from agenome import render  # noqa: E402

OUT = _os.path.join(_os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))), "renders", "agents")
NF = 30


def base(g, n=90, speed=18, shape=0, radius=30):
    return g.add("ag.spawn", **_named("ag.spawn", n=n, radius=radius, shape=shape, speed=speed, seed=4, teams=3,
                                      hue0=0.55, huestep=0.2, sat=0.85, val=1.4))


def finish(g, a, cam="orbit", trail=True, cam_r=90, cam_h=35, body=True, look="trail"):
    if look == "trail":
        a = g.add("ag.trail", a, **_named("ag.trail", interval=0.08, sx=0.9, sy=0.25, sz=1.4, life=8, emit=0.6,
                                           colormode=0, edge=1.5, h=0.55, s=0.8, v=1.0))
    elif look == "ribbon":
        a = g.add("ag.ribbon", a, **_named("ag.ribbon", width=0.25, height=1.4, interval=0.1, life=8, emit=0.6,
                                            edge=1.5, colormode=0, h=0.55, s=0.8, v=1.0))
    if body:
        a = g.add("ag.body", a, **_named("ag.body", shape=2, sx=0.8, sy=0.3, sz=1.4, emit=1.0, colormode=1,
                                          h=0, s=0, v=1.6))
    if cam == "orbit":
        c = g.add("cam.orbit", **_named("cam.orbit", radius=cam_r, height=cam_h, speed=0.15, fov=60))
    elif cam == "track":
        c = g.add("cam.track", a)
    elif cam == "fourier":
        c = g.add("cam.fourier", **_named("cam.fourier", radius=cam_r))
    elif cam == "fourier_shake":
        c0 = g.add("cam.fourier", **_named("cam.fourier", radius=cam_r))
        c = g.add("cam.shake", c0, **_named("cam.shake", amp=0.01, freq=3))
    else:
        c = cam(g, a)
    p = g.add("post.grade", **_named("post.grade", exposure=1.1, thresh=0.5, bloom=0.9, bloomrad=2.5, fog=0.002,
                                     fh=0.66, fs=0.6, fv=0.02, vignette=0.5))
    t = g.add("tex.perlin", **_named("tex.perlin", freq=3, oct=4, seed=7, ah=0.7, as_=0.8, av=0.01, bh=0.62,
                                     bs=0.7, bv=0.05))
    g.add("out", None, a, c, p, t, None, **_named("out", sky=0.5, steps=32, far=800, warmup=4))
    return g.repair()


def g_curl(on):
    g = Genome(seed=3)
    a = base(g)
    if on:
        a = g.add("ag.curl", a)
    a = g.addv("ag.bound", a, radius=45, stiff=0.5)
    return finish(g, a)


def g_fourier(on):
    g = Genome(seed=3)
    a = base(g, n=60)
    if on:
        a = g.add("ag.fourier", a)
    a = g.addv("ag.bound", a, radius=45, stiff=0.5)
    return finish(g, a, cam_r=65, cam_h=25)


def g_modes(on):
    g = Genome(seed=3)
    a = base(g, n=150, speed=6, shape=1, radius=25)
    if on:
        a = g.add("ag.modes", a)
    a = g.addv("ag.bound", a, radius=45, stiff=0.5)
    return finish(g, a, cam_r=80)


def g_modes_static(on):
    g = Genome(seed=3)
    a = g.add("ag.scatter", **_named("ag.scatter", n=400, radius=25, shape=1, mesh=3, size=1.6, sizevar=0.3,
                                     spin=0.3, emit=1.2, seed=2, huevar=0.2, edge=0.5, h=0.45, s=0.8, v=1.3))
    if on:
        a = g.add("ag.modes", a, **_named("ag.modes", amp=0.5, freq=0.5, dir=3))
    return finish(g, a, cam_r=70, cam_h=20, body=False, look=None)


def g_texflow(on):
    g = Genome(seed=3)
    tx = g.add("tex.cell", **_named("tex.cell", count=6, seed=5, mode=0))
    a = base(g, n=120, speed=16, shape=2, radius=60)
    if on:
        a = g.add("ag.texflow", a, tx)
    a = g.addv("ag.bound", a, radius=80, stiff=0.5)
    return finish(g, a, cam_r=120, cam_h=70)


def g_texflow_sphere(on):
    g = Genome(seed=3)
    tx = g.add("tex.spectral", **_named("tex.spectral", fc=6, mode=4))
    a = base(g, n=120, speed=16, shape=1, radius=40)
    if on:
        a = g.add("ag.texflow", a, tx, **_named("ag.texflow", map=1, radius=40, height=8))
    a = g.addv("ag.bound", a, radius=60, stiff=0.5)
    return finish(g, a, cam_r=110, cam_h=40)


def g_vortex(on):
    g = Genome(seed=3)
    a = base(g, n=120)
    if on:
        a = g.add("ag.vortex", a)
    a = g.addv("ag.bound", a, radius=50, stiff=0.5)
    return finish(g, a)


def g_ribbon(on):
    g = Genome(seed=3)
    a = base(g, n=24, speed=18)
    a = g.addv("ag.wander", a, freq=0.3, amp=2.0)
    a = g.addv("ag.bound", a, radius=40, stiff=0.5)
    return finish(g, a, look="ribbon" if on else "trail", cam_r=80)


def g_ribbon_twist(on):
    g = Genome(seed=3)
    a = base(g, n=12, speed=18)
    a = g.add("ag.fourier", a, **_named("ag.fourier", spread=1.0, tube=4))
    if on:
        a = g.add("ag.ribbon", a, **_named("ag.ribbon", width=0.3, height=2.5, twist=1.5, facets=1, life=10,
                                            emit=1.2, edge=2.0, h=0.85, s=0.8, v=1.0))
    else:
        a = g.add("ag.trail", a, **_named("ag.trail", interval=0.08, sx=2.5, sy=0.3, sz=1.4, life=10, emit=1.2,
                                           edge=2.0))
    return finish(g, a, look=None, cam=lambda g, a: g.add("cam.chase", a, **_named(
        "cam.chase", agent=1, dist=14, height=5, lag=0.85, fov=70, look=1.0)))


def g_cam_fourier(on):
    g = Genome(seed=3)
    a = base(g, n=60)
    a = g.add("ag.fourier", a, **_named("ag.fourier", seed=9, radius=40))
    return finish(g, a, cam="fourier_shake" if on else "orbit", cam_r=90)


def g_cam_track(on):
    g = Genome(seed=3)
    a = base(g, n=80)
    a = g.addv("ag.attract", a, cx=60, cy=0, cz=0, strength=0.05, orbit=0.5, radius=25)
    return finish(g, a, cam="track" if on else "orbit")


CASES = {"ag.curl": g_curl, "ag.fourier": g_fourier, "ag.modes": g_modes, "ag.modes.static": g_modes_static,
         "ag.texflow": g_texflow, "ag.texflow.sphere": g_texflow_sphere, "ag.vortex": g_vortex,
         "ag.ribbon": g_ribbon, "ag.ribbon.twist": g_ribbon_twist,
         "cam.fourier": g_cam_fourier, "cam.track": g_cam_track}


def sheet(rows, path, labels=None):
    from PIL import Image, ImageDraw
    h, w = rows[0].shape[1:3]
    picks = np.linspace(0, len(rows[0]) - 1, 4).astype(int)
    im = Image.new("RGB", (4 * w, len(rows) * h))
    dr = ImageDraw.Draw(im)
    for r, fr in enumerate(rows):
        for k, i in enumerate(picks):
            im.paste(Image.fromarray(fr[i]), (k * w, r * h))
        if labels:
            dr.text((3, r * h + 2), labels[r], fill=(255, 255, 0))
    im.save(path)


def main(names):
    R = render.Renderer()
    allrows, alllab, report = [], [], []
    for name in names:
        rows = []
        for on in (False, True):
            g = CASES[name](on)
            g2 = Genome.from_bytes(g.to_bytes())
            assert g2.to_bytes() == g.to_bytes(), name
            assert [n.op for n in g2.nodes] == [n.op for n in g.nodes], name
            t0 = time.time()
            fr = R.render(g, NF)
            dt = time.time() - t0
            rows.append(fr)
            mean = float(fr.mean())
            assert mean > 2.0, f"{name} blank ({mean:.2f})"
            report.append((name, on, dt, mean, len(g.to_bytes())))
            print(f"{name:20s} {'on ' if on else 'off'} {dt:5.2f}s/{NF}f  mean {mean:5.1f}  bytes {len(g.to_bytes())}",
                  flush=True)
        diff = float(np.abs(rows[0].astype(int) - rows[1].astype(int)).mean())
        assert diff > 1.0, f"{name}: op has no visible effect ({diff:.2f})"
        sheet(rows, f"{OUT}/{name}.png", [f"{name} off", f"{name} on"])
        allrows.append(rows[1])
        alllab.append(name)
    if len(names) > 1:
        sheet(allrows, f"{OUT}/all.png", alllab)
    return report


if __name__ == "__main__":
    main(sys.argv[1:] or list(CASES))
