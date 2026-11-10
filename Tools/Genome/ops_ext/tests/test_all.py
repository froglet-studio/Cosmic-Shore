"""Smoke test: every ext op with default bytes compiles, renders and byte-roundtrips. python3 ops_ext/tests/test_all.py"""
import sys, time
sys.path[:0] = ["/home/user/work/genome", "/home/user/work"]
import numpy as np
import ops_ext
ops_ext.install()
from agenome.genome import Genome
from agenome.render import Renderer
from ops_ext.registry import EXT

R = Renderer()
bad = 0
from agenome.ops import OPS as _OPS
for name, e in sorted([kv for kv in EXT.items() if kv[0] in _OPS], key=lambda kv: kv[1].op.code):
    o = e.op
    g = Genome(seed=5)
    t = g.add("tex.perlin")
    s = g.add("sdf.sphere")
    a = g.add("ag.spawn", n=60)
    ins = {"T": t, "S": s, "A": a, "P": g.add("post.grade") if o.out == "P" else None}
    me = g.add(name, *[ins.get(x.upper()) for x in o.inputs])
    if o.out == "T":
        root = g.add("out", None, a, None, None, me)
    elif o.out == "S":
        m = g.add("sdf.material", me, emit=100, ev=200)
        root = g.add("out", m, a, None, None, t)
    elif o.out == "A":
        tr = g.add("ag.trail", me)
        root = g.add("out", None, tr, None, None, t)
    elif o.out == "P":
        root = g.add("out", None, a, None, me, t)
    else:
        root = g.add("out", None, a, me, None, t)
    try:
        t0 = time.time()
        fr = R.render(g, 6)
        g2 = Genome.from_bytes(g.to_bytes())
        ok = [n.op for n in g2.nodes] == [n.op for n in g.nodes] and np.isfinite(fr).all()
        print(f"0x{o.code:02X} {name:18s} {'ok ' if ok else 'BAD'} {(time.time()-t0)/6*1000:6.0f} ms/frame  mean {fr.mean():5.1f}")
        bad += not ok
    except Exception as ex:
        bad += 1
        print(f"0x{o.code:02X} {name:18s} FAIL {type(ex).__name__}: {str(ex)[:200]}")
print("ops", sum(1 for k in EXT if k in _OPS), "failures", bad)
sys.exit(1 if bad else 0)
