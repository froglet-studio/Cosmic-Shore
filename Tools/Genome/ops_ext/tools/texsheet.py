"""Flat texture sheet: render T ops (with param variants) straight to 2D tiles via the bake machinery.
  python3 -m ops_ext.tools.texsheet OUT.png  op[:k=v,k=v] ...   (each arg = one tile; prefix 'src=perlin' for filters)
"""
import sys, time
import numpy as np


def tile(spec, R, baker):
    from agenome.genome import Genome
    from agenome.ops import OPS
    parts = spec.split(":")
    op = parts[0]
    kv = {}
    if len(parts) > 1:
        for t in parts[1].split(","):
            k, v = t.split("=")
            kv[k] = v
    src = kv.pop("src", "perlin")
    src2 = kv.pop("src2", "cell")
    g = Genome(seed=int(kv.pop("gseed", 3)))
    ins = []
    for k in range(len(OPS[op].inputs)):
        s = [src, src2][min(k, 1)]
        if s == "none":
            ins.append(None)
        elif s == "perlin":
            ins.append(g.add("tex.perlin", freq=120, oct=200, bh=150, bs=200, bv=230, ah=200, as_=0) if False else g.add("tex.perlin", freq=120, oct=200, bh=150, bs=200, bv=230))
        elif s == "cell":
            ins.append(g.add("tex.cell", count=90, mode=100))
        elif s == "grad":
            ins.append(g.add("tex.gradient", mode=100))
        else:  # another ext op
            ins.append(g.add(s))
    t = g.add(op, *ins, **{k: int(v) for k, v in kv.items()})
    g.add("out", None, None, None, None, t)
    g.ensure_root()
    t0 = time.time()
    baker.bake_all(g)
    a = baker.eval_input(g, t)
    dt = time.time() - t0
    img = np.clip(a, 0, 1) ** (1 / 2.2)
    return (img[::-1] * 255).astype(np.uint8), dt


def main(argv):
    import os as _os; _H = _os.path.dirname(_os.path.dirname(_os.path.dirname(_os.path.abspath(__file__)))); sys.path[:0] = [_H, _os.path.join(_H, "genome")]  # ops_ext's parent (and a genome/ beside it)
    import ops_ext
    ops_ext.install()
    from agenome.render import Renderer
    from ops_ext.bake import Baker
    from PIL import Image, ImageDraw
    R = Renderer()
    baker = Baker(R)
    out = argv[0]
    tiles = []
    for spec in argv[1:]:
        try:
            im, dt = tile(spec, R, baker)
        except Exception as e:
            import traceback; traceback.print_exc()
            im, dt = np.full((256, 256, 3), 80, np.uint8), 0
            im[..., 1:] = 0
        pim = Image.fromarray(im)
        ImageDraw.Draw(pim).text((3, 3), f"{spec[:40]} {dt*1000:.0f}ms", fill=(255, 255, 0))
        tiles.append(np.asarray(pim))
        print(spec, f"{dt*1000:.0f} ms", float(im.mean()))
    cols = min(4, len(tiles))
    while len(tiles) % cols:
        tiles.append(np.zeros_like(tiles[0]))
    rows = [np.concatenate(tiles[i:i + cols], 1) for i in range(0, len(tiles), cols)]
    Image.fromarray(np.concatenate(rows, 0)).save(out)


if __name__ == "__main__":
    main(sys.argv[1:])
