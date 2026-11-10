"""Seed genomes that put the extension ops into Amoebius-like compositions (writes ops_ext/seeds/*.json).

Each reuses the tuned agent / camera / post bytes of seeds/amoebius_like/minimal__tuned (the best-tuned small
scaffold) and swaps in one ext idea, so the search starts from working scenes instead of random ext nodes.
  python3 -m ops_ext.seeds.make_seeds
"""
import os

SHARE = "/mnt/project-files/amoebius-genome"
HERE = os.path.dirname(os.path.abspath(__file__))

AG = dict(n=85, radius=204, shape=27, speed=201, seed=4, teams=154, hue0=133, huestep=52, sat=228, val=203)
BOIDS = dict(coh=90, ali=86, sep=103, radius=109, turn=189)
TRAIL = dict(interval=60, sx=91, sy=100, sz=140, life=148, emit=165, colormode=62, edge=125, mesh=15, h=132, s=97, v=204)
CHASE = dict(agent=0, dist=166, height=150, lag=221, fov=170, look=53)
POST = dict(exposure=93, thresh=114, bloom=154, bloomrad=187, fh=163, fs=199, fv=84, fog=86, vignette=123, sat=130,
            contrast=145, feedback=89, grain=2, aberr=255)
OUT = dict(sky=180, steps=100, far=170, ltheta=59, lphi=156, ambient=61, warmup=22, lh=166, ls=208, lv=176)


def scaffold(g, sky, behaviours=(("ag.boids", BOIDS),), looks=(("ag.trail", TRAIL),), cam=("cam.chase", CHASE),
             scene=None):
    a = g.add("ag.spawn", **AG)
    for op, p in behaviours:
        a = g.add(op, a, **p)
    for op, p in looks:
        a = g.add(op, a, **p)
    c = g.add(cam[0], a, **cam[1]) if cam[0] in ("cam.chase", "cam.track") else g.add(cam[0], **cam[1])
    p = g.add("post.grade", **POST)
    g.add("out", scene, a, c, p, sky, None, **OUT)
    return g


def seeds():
    from agenome.genome import Genome
    out = {}

    g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "warpfbm_trails"})
    out["warpfbm_trails"] = scaffold(g, g.add("tex.warpfbm"))

    g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "spectral_palette_trails"})
    s = g.add("tex.spectral")
    out["spectral_palette_trails"] = scaffold(g, g.add("tex.palette", s))

    g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "kali_galaxy_trails"})
    k = g.add("tex.kali")
    out["kali_galaxy_trails"] = scaffold(g, g.add("tex.galaxy", k, mode=200))

    g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "flow_ribbons"})
    out["flow_ribbons"] = scaffold(g, g.add("tex.warpfbm"), behaviours=(("ag.flow", {}), ("ag.boids", BOIDS)),
                                   looks=(("ag.ribbon", {"facets": 128}),))

    g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "curl_ribbons_track"})
    out["curl_ribbons_track"] = scaffold(g, g.add("tex.spectral"), behaviours=(("ag.curl", {}),),
                                         looks=(("ag.ribbon", {"facets": 128}), ("ag.trail", TRAIL)),
                                         cam=("cam.track", {}))

    g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "fourier_tunnel"})
    out["fourier_tunnel"] = scaffold(g, g.add("tex.warpfbm"), behaviours=(("ag.fourier", {}),),
                                     looks=(("ag.ribbon", {"facets": 128}),), cam=("cam.fourier", {}))

    g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "flame_sky_trails"})
    w = g.add("tex.warpfbm")
    fl = g.add("tex.flame", bright=60)
    out["flame_sky_trails"] = scaffold(g, g.add("tex.blend", w, fl, mode=0, amount=120))

    g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "physarum_web_sky"})
    w = g.add("tex.warpfbm")
    ph = g.add("tex.physarum", av=0, bv=120)
    out["physarum_web_sky"] = scaffold(g, g.add("tex.blend", w, ph, mode=0, amount=90))

    try:  # shape seeds (sdf_ext)
        g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "kifs_crystals"})
        o = g.add("sdf.octa")
        kf = g.add("sdf.kifs", o)
        rp = g.add("sdf.repeat", kf, axes=7, px=200, py=200, pz=200)
        m = g.add("sdf.material", rp, None, ah=85, as_=0) if False else g.add("sdf.material", rp, eh=85, es=200, ev=200, emit=120)
        out["kifs_crystals"] = scaffold(g, g.add("tex.warpfbm"), scene=m)
        g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "prism_tunnel"})
        pr = g.add("sdf.prism")
        po = g.add("sdf.polar", pr, count=60, axis=200)
        rp = g.add("sdf.repeat", po, axes=4, pz=120)
        pa = g.add("sdf.path", rp)
        m = g.add("sdf.material", pa, eh=160, es=200, ev=200, emit=100)
        out["prism_tunnel"] = scaffold(g, g.add("tex.kali"), scene=m)
        g = Genome(seed=3, meta={"lineage": "ops_ext", "name": "loop_tunnel"})
        lp = g.add("sdf.loop")
        m = g.add("sdf.material", lp, eh=20, es=200, ev=200, emit=100)
        out["loop_tunnel"] = scaffold(g, g.add("tex.warpfbm"), scene=m)
    except KeyError as e:
        print("shape seeds skipped:", e)
    return out


def main():
    import ops_ext
    ops_ext.install()
    for name, g in seeds().items():
        g.repair()
        g.save(os.path.join(HERE, name + ".json"))
        print(name, len(g.nodes), "nodes", g.size(), "zbytes")


if __name__ == "__main__":
    main()
