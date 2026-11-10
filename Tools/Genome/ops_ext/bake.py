"""Baked textures: werkkzeug computed every Bitmap_* op on the CPU into a bitmap; ops_ext does the same for the ops
that need the whole image at once (FFT, SVD, PCA, eigen-solves, reaction-diffusion, IFS histograms).

A baked node i is sampled in GLSL as `texture(uB<i>, uv)` (REPEAT wrap: FFT-made textures tile seamlessly). Its T
inputs are first rendered to float arrays on the GPU (any core GLSL texture subgraph works as an input), baked in
topological order, cached by the hash of the node's whole input subgraph, and uploaded once per render.
"""
import hashlib
import math
import os
from collections import OrderedDict

import numpy as np

from agenome import glsl
from agenome.ops import OPS

from .registry import EXT

N = int(os.environ.get("OPS_EXT_BAKE_N", "256"))  # bake resolution (power of two: FFT friendly)
MAX_BAKES = 12  # texture units 3..14 (units 0..2 are the core post passes)
UNIT0 = 3
_CACHE = OrderedDict()
_CACHE_MAX = int(os.environ.get("OPS_EXT_BAKE_CACHE", "96"))
STATS = {"hits": 0, "miss": 0, "gpu_inputs": 0}


def is_baked(op_name):
    e = EXT.get(op_name)
    return e is not None and e.bake_t is not None


def _decode(n):
    o = OPS[n.op]
    return {p.name: p.decode(b) for p, b in zip(o.params, n.params)}


def subgraph_key(g, i, memo=None):
    """Hash of node i's op, params and (recursively) inputs: identical subgraphs share one bake."""
    if memo is None:
        memo = {}
    if i in memo:
        return memo[i]
    n = g.nodes[i]
    h = hashlib.blake2b(digest_size=12)
    h.update(n.op.encode())
    h.update(bytes(max(0, min(255, int(b))) for b in n.params))
    h.update(bytes([g.seed & 255]))
    for j in n.inputs:
        h.update(b"-" if j is None else subgraph_key(g, j, memo).encode())
    memo[i] = h.hexdigest()
    return memo[i]


def t_ancestors(g, j):
    """All T nodes feeding T node j (inclusive)."""
    seen, stack = set(), [j]
    while stack:
        k = stack.pop()
        if k is None or k in seen or OPS[g.nodes[k].op].out != "T":
            continue
        seen.add(k)
        stack.extend(g.nodes[k].inputs)
    return seen


class Baker:
    """Per-Renderer helper: owns the GPU input-evaluation target and the per-render uploaded textures."""

    def __init__(self, renderer):
        self.R = renderer
        ctx = renderer.ctx
        self.tex_in = ctx.texture((N, N), 4, dtype="f4")
        self.fbo_in = ctx.framebuffer([self.tex_in])
        self._progs = {}
        self.live_tex = {}  # node index -> (moderngl texture, unit)

    def release(self):
        for t, _ in self.live_tex.values():
            t.release()
        self.live_tex = {}

    def bind(self, prog):
        for i, (tex, unit) in self.live_tex.items():
            nm = f"uB{i}"
            if nm in prog:
                tex.use(unit)
                prog[nm].value = unit

    def eval_input(self, g, j):
        """Render T node j (any GLSL/baked texture subgraph) to a float32 [N, N, 3] array; row = v, col = u."""
        live = t_ancestors(g, j)
        src = glsl.PRELUDE + "\n" + glsl.compile_textures(g, live) + (
            f"\nvoid main(){{ vec2 uv = gl_FragCoord.xy/{float(N)}; oCol = vec4(t{j}(uv), 1.0); }}\n")
        key = hash(src)
        pv = self._progs.get(key)
        if pv is None:
            if len(self._progs) > 48:
                for k, (pp, vv) in list(self._progs.items())[:24]:
                    vv.release()
                    pp.release()
                    del self._progs[k]
            from agenome.render import VS_QUAD
            pp = self.R.ctx.program(vertex_shader=VS_QUAD, fragment_shader=src)
            vv = self.R.ctx.vertex_array(pp, [(self.R.quad, "2f", "p")])
            pv = self._progs[key] = (pp, vv)
        pp, vv = pv
        self.bind(pp)
        self.fbo_in.use()
        self.R.ctx.viewport = (0, 0, N, N)
        vv.render(self.R.mgl.TRIANGLE_STRIP)
        a = np.frombuffer(self.fbo_in.read(components=4, dtype="f4"), np.float32).reshape(N, N, 4)[..., :3]
        STATS["gpu_inputs"] += 1
        return np.nan_to_num(a.copy(), nan=0.0, posinf=8.0, neginf=-8.0)

    def bake_all(self, g):
        """Bake every live baked node of g (topological order) and upload it. Returns {node: (tex, unit)}."""
        self.release()
        live = g.live()
        order = [i for i in sorted(live) if is_baked(g.nodes[i].op)]
        if len(order) > MAX_BAKES:
            raise ValueError(f"too many baked textures: {len(order)} > {MAX_BAKES}")
        memo = {}
        for k, i in enumerate(order):
            n = g.nodes[i]
            key = subgraph_key(g, i, memo)
            arr = _CACHE.get(key)
            if arr is not None:
                _CACHE.move_to_end(key)
                STATS["hits"] += 1
            else:
                STATS["miss"] += 1
                ins = []
                for j in n.inputs:
                    ins.append(None if j is None else self.eval_input(g, j))
                d = _decode(n)
                arr = EXT[n.op].bake_t(d, ins, seed=g.seed)
                arr = np.nan_to_num(np.asarray(arr, np.float32), nan=0.0, posinf=8.0, neginf=-8.0)
                arr = np.clip(arr, -8.0, 8.0)
                if arr.ndim == 2:
                    arr = np.repeat(arr[..., None], 3, 2)
                _CACHE[key] = arr
                if len(_CACHE) > _CACHE_MAX:
                    _CACHE.popitem(last=False)
            tex = self.R.ctx.texture((arr.shape[1], arr.shape[0]), 3, np.ascontiguousarray(arr, np.float32).tobytes(),
                                     dtype="f4")
            tex.repeat_x = tex.repeat_y = True
            tex.filter = (self.R.mgl.LINEAR, self.R.mgl.LINEAR)
            self.live_tex[i] = (tex, UNIT0 + k)
        return self.live_tex


def baked_glsl(i, d):
    """GLSL for sampling baked node i, with the shared `drift` (slow scroll, like the reels' nebula)."""
    dr = d.get("drift", 0.0)
    if abs(dr) > 1e-6:
        ang = 0.7 + 2.3 * dr  # direction tied to the speed byte: one byte, two degrees of freedom
        off = f"uTime*vec2({glsl.f(dr * math.cos(ang) * 0.02)},{glsl.f(dr * math.sin(ang) * 0.02)})"
        return f"uniform sampler2D uB{i}; vec3 t{i}(vec2 uv){{ return texture(uB{i}, uv + {off}).rgb; }}"
    return f"uniform sampler2D uB{i}; vec3 t{i}(vec2 uv){{ return texture(uB{i}, uv).rgb; }}"
