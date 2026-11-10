"""Search-side helpers for ext ops (opt in from run_search with --prior-init P).

prior_init: core mutation fills a NEW node's params with uniform random bytes. For ops with 10-16 params whose good
region is a small part of byte space (flames, attractors, reaction-diffusion), that wastes most insertions. With
probability P, a node that a mutation inserted (or swapped in) gets its op's DEFAULT bytes (tuned by eye per op)
plus gaussian noise (sigma 18 bytes) instead. Measured by ops_ext.tools.landscape --init default, which sets the bytes directly and does not go through this
wrapper (2026-11-10): it HURTS. The share of random draws under judge 3.0
fell from 20.7% to 6.0% (ext) and from 8.0% to 0.5% (core). Leave --prior-init off: eye-tuned defaults are no judge prior.
"""
import random

from agenome.ops import OPS


def prior_params(op, rng, sigma=18.0):
    return [int(max(0, min(255, round(p.default + rng.gauss(0, sigma))))) for p in OPS[op].params]


def install_prior_init(prob=0.7, sigma=18.0, ext_only=False):
    from agenome import genome
    from .registry import EXT
    base_mutate = genome.mutate

    def mutate(g, rng, *a, **kw):
        child = base_mutate(g, rng, *a, **kw)
        if len(child.nodes) == len(g.nodes) and all(x.op == y.op for x, y in zip(child.nodes, g.nodes)):
            return child  # parameter-only or rewire step
        # which child nodes are new: align (op, params) sequences, so an op that already existed earlier in the
        # genome is not mistaken for the inserted one (code review 2026-11-10: counting occurrences per op reset
        # the OLDER node when a mutation inserted or duplicated an op type the genome already had)
        import difflib
        sig = lambda n: (n.op, tuple(n.params))
        sm = difflib.SequenceMatcher(None, [sig(n) for n in g.nodes], [sig(n) for n in child.nodes], autojunk=False)
        matched = set()
        for blk in sm.get_matching_blocks():
            matched.update(range(blk.b, blk.b + blk.size))
        for j, n in enumerate(child.nodes):
            if j not in matched and (not ext_only or n.op in EXT) and rng.random() < prob:
                n.params = prior_params(n.op, rng, sigma)
        return child
    genome.mutate = mutate
    try:
        from agenome import search
        search.mutate = mutate  # search did `from .genome import mutate`
    except Exception:
        pass
    return mutate
