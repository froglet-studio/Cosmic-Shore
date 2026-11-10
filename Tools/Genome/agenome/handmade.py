"""Hand-built seed genomes (the 'designed starting points'), written with the public builder API."""
from .genome import Genome


def amoebius_like(seed=3):
    """A first guess at Cosmic Shore as seen in Amoebius: domain-warped nebula, a cell membrane, gyroid flora,
    vessels laying neon prism walls, green crystals, chase camera, bloom, a HUD."""
    g = Genome(seed=seed, meta={"lineage": "handmade", "name": "amoebius_like"})
    # sky: fbm warped by fbm (domain warp), purple-blue
    t_warp = g.addv("tex.perlin", freq=2, oct=4, persist=0.5, seed=3, gamma=1.0)
    t_base = g.add("tex.perlin", **_named("tex.perlin", freq=2.5, oct=5, persist=0.55, seed=11, gamma=1.4,
                                          ah=0.74, as_=0.85, av=0.015, bh=0.63, bs=0.8, bv=0.45))
    t_sky = g.addv("tex.distort", t_base, t_warp, amount=0.18)
    # scene: membrane + flora
    s_cell = g.addv("sdf.sphere", r=170)
    s_mem = g.addv("sdf.shell", s_cell, thick=0.6)
    m_mem = g.add("sdf.material", s_mem, **_named("sdf.material", ah=0.6, as_=0.5, av=0.2, eh=0.58, es=0.8, ev=0.6,
                                                     emit=0.2, glow=0.004, falloff=0.08, spec=0.3))
    s_gy = g.addv("sdf.gyroid", scale=0.35, thick=0.25, bound=28)
    s_gy2 = g.addv("sdf.translate", s_gy, x=40, y=-10, z=30)
    m_gy = g.add("sdf.material", s_gy2, **_named("sdf.material", ah=0.4, as_=0.7, av=0.25, eh=0.38, es=0.9, ev=0.8,
                                                    emit=0.6, glow=0.01, falloff=0.6, spec=0.5))
    s_scene = g.addv("sdf.union", m_mem, m_gy, k=0.001)
    # agents: vessels laying prism walls
    a0 = g.add("ag.spawn", **_named("ag.spawn", n=6, radius=60, shape=0, speed=28, seed=5, teams=4, hue0=0.58,
                                     huestep=0.27, sat=0.85, val=1.6))
    a1 = g.addv("ag.wander", a0, freq=0.18, amp=1.5)
    a2 = g.addv("ag.bound", a1, radius=120, stiff=0.6)
    a3 = g.add("ag.trail", a2, **_named("ag.trail", interval=0.06, sx=2.2, sy=0.25, sz=1.6, life=25, emit=0.9,
                                         colormode=0, edge=2.5, h=0.6, s=0.8, v=1.0))
    a4 = g.add("ag.body", a3, **_named("ag.body", shape=2, sx=1.6, sy=0.6, sz=2.4, emit=0.6, colormode=1, edge=0.5,
                                        h=0.0, s=0.0, v=1.6))
    a5 = g.add("ag.scatter", **_named("ag.scatter", n=60, radius=110, shape=0, mesh=3, size=1.6, sizevar=0.4, spin=0.6,
                                      emit=1.2, seed=9, huevar=0.05, edge=0.3, h=0.36, s=0.85, v=1.3))
    a6 = g.addv("ag.merge", a4, a5)
    cam = g.add("cam.chase", a6, **_named("cam.chase", agent=0, dist=9, height=2.5, lag=0.85, fov=70, look=1.2))
    post = g.add("post.grade", **_named("post.grade", exposure=1.2, thresh=0.55, bloom=0.9, bloomrad=2.5,
                                        fh=0.68, fs=0.7, fv=0.02, fog=0.004, vignette=0.6, sat=1.15, contrast=1.05,
                                        feedback=0.0, grain=0.02, aberr=0.002))
    h0 = g.add("hud.bar", **_named("hud.bar", anchor=7, x=0.0, y=-0.06, w=0.3, hh=0.012, segs=10, fill=0.7,
                                   h=0.35, s=0.8, v=1.0, alpha=0.85))
    h1 = g.add("hud.ring", h0, **_named("hud.ring", anchor=8, x=-0.09, y=-0.12, r=0.06, thick=0.012, arc=0.75,
                                         h=0.55, s=0.6, v=1.0, alpha=0.8))
    h2 = g.add("hud.text", h1, **_named("hud.text", anchor=0, x=0.03, y=0.05, size=0.022, chars=14, rows=2, seed=4,
                                         h=0.0, s=0.0, v=1.0, alpha=0.8))
    h3 = g.add("hud.rect", h2, **_named("hud.rect", anchor=2, x=-0.1, y=0.06, w=0.14, hh=0.05, h=0.6, s=0.5, v=0.3,
                                         alpha=0.5, outline=0.003))
    g.add("out", s_scene, a6, cam, post, t_sky, h3, **_named("out", sky=1.0, steps=72, far=600, ltheta=0.9, lphi=0.7,
                                                            ambient=0.25, lh=0.6, ls=0.15, lv=1.2, warmup=6))
    g.nodes = [n for n in g.nodes]  # keep order
    return g.prune().repair()


def _named(op, **vals):
    """Decoded values -> bytes by param name; a trailing underscore escapes python keywords (as_ -> as)."""
    from .ops import OPS
    o = OPS[op]
    byname = {p.name: p for p in o.params}
    out = {}
    for k, v in vals.items():
        k2 = k[:-1] if k.endswith("_") else k
        out[k2] = byname[k2].encode(v)
    return out


def _bytes(op, **vals):
    from .ops import OPS
    o = OPS[op]
    nb = _named(op, **vals)
    return [nb.get(p.name, p.default) for p in o.params]
