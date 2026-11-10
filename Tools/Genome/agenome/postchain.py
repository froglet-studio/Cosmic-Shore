"""One executor for the numpy post ops (P ops that reshape the rendered uint8 frames instead of running in GLSL).

Two families have them: the part-by-part thread's post.hx_fft / hx_pca / hx_dog / hx_histeq (agenome/hops.py) and
ops_ext's post.bandgain (ops_ext/post_ext.py). Until 2026-11-10 each family wrapped Renderer.render with its own hook
that cut only its own ops out of the root's p chain, so in a mixed chain the inner hook's family always ran first,
whatever the chain said, and which family was inner depended on which package was imported first: one genome rendered
differently under agenome.search and under ops_ext.run_search (found by the code review of 2026-11-10).

Now each family only registers its functions here, and one wrapper walks the chain once: every registered op is cut
out (the GLSL P ops between them stay linked, down to the post.grade), the frames are rendered, and the cut ops are
applied innermost first. Consecutive ops of one family run as one batch (hops keeps its float pipeline between hx ops,
so a chain of one family renders byte for byte as before)."""

FUNCS = {}  # op name -> family
FAMILIES = {}  # family -> batch(frames uint8 [T,H,W,3], nodes in application order) -> frames uint8
_INSTALLED = [False]


def register(family, ops, batch):
    FAMILIES[family] = batch
    for op in ops:
        FUNCS[op] = family


def split(g):
    """On a copy of g: cut every registered numpy op out of the root's p chain. Returns (g2, [nodes outermost first])."""
    from agenome.ops import OPS
    g2 = g.copy().ensure_root()
    r = g2.root()
    rn = g2.nodes[r]
    if len(rn.inputs) < 4 or not FUNCS:
        return g2, []
    seq, seen, p = [], set(), rn.inputs[3]
    while p is not None and p not in seen and g2.nodes[p].op != "post.grade" and \
            getattr(OPS.get(g2.nodes[p].op), "out", "P") == "P":
        seen.add(p)
        seq.append(p)
        ins = g2.nodes[p].inputs
        p = ins[0] if ins else None
    tail = p if (p is not None and g2.nodes[p].op == "post.grade") else None
    mine = [i for i in seq if g2.nodes[i].op in FUNCS]
    if not mine:
        return g2, []
    rest = [i for i in seq if g2.nodes[i].op not in FUNCS]
    link = rest + ([tail] if tail is not None else [])
    rn.inputs[3] = link[0] if link else None
    for k, i in enumerate(rest):
        nxt = link[k + 1] if k + 1 < len(link) else None
        if g2.nodes[i].inputs:
            g2.nodes[i].inputs[0] = nxt
        else:
            g2.nodes[i].inputs = [nxt]
    return g2, [g2.nodes[i] for i in mine]


def apply(frames, chain):
    """chain: nodes outermost first (as split returns them); applied innermost first, one batch per family run."""
    order = list(chain)[::-1]
    k = 0
    while k < len(order):
        fam = FUNCS[order[k].op]
        j = k
        while j < len(order) and FUNCS[order[j].op] == fam:
            j += 1
        frames = FAMILIES[fam](frames, order[k:j])
        k = j
    return frames


def install(render_module):
    """Wrap Renderer.render once (idempotent; every family calls it after registering)."""
    if _INSTALLED[0]:
        return
    R = render_module.Renderer
    orig_render = R.render

    def render(self, g, n_frames=60, *a, **kw):  # forwards every argument (t_shift for judged windows)
        g2, chain = split(g)
        if not chain:
            return orig_render(self, g, n_frames, *a, **kw)
        res = orig_render(self, g2, n_frames, *a, **kw)
        if isinstance(res, tuple):
            return (apply(res[0], chain),) + tuple(res[1:])
        return apply(res, chain)

    render._postchain_orig = orig_render
    R.render = render
    _INSTALLED[0] = True
