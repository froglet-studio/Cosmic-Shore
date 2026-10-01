"""evo16: a process pool whose workers are each PINNED to one core.

Measured on the 4-core research box: an unpinned torch process spreads over all 4 cores even with
torch.set_num_threads(1) / OMP_NUM_THREADS=1 (some pool ignores both), so 4 such workers oversubscribe
and every one runs ~18x slower than alone. Pinned (sched_setaffinity), 4 workers finish 4 evaluations
in the time one takes alone. Use this for every parallel evaluation here.
"""
import multiprocessing as mp
import os


def _init(q):
    core = q.get()
    os.sched_setaffinity(0, {core})
    import torch
    torch.set_num_threads(1)


def pinned_pool(n=4):
    ctx = mp.get_context("spawn")
    q = ctx.Queue()
    cores = sorted(os.sched_getaffinity(0))
    for i in range(n):
        q.put(cores[i % len(cores)])
    return ctx.Pool(n, initializer=_init, initargs=(q,))
