"""A known answer, for smoke-testing the whole pipe (worker, pool, publish, board) on any machine in seconds.

The shifted Rastrigin-lite: minimum 0 at x_i = target_i. `compute.py self-test` runs a worker session on it and
requires the board to see the improvement.
"""
import math

VERSION = "1"
PATHS = ["Tools/Compute/"]


def evaluate(params, ctx=None):
    target = (ctx or {}).get("args", {}).get("target", 1.5)
    loss = 0.0
    for k, v in sorted(params.items()):
        d = v - target
        loss += d * d + 0.1 * (1 - math.cos(2 * math.pi * d))
    return {"loss": loss, "metrics": {"dims": len(params)}}


def baseline(args):
    return None


def self_test():
    bad = []
    if evaluate({"a": 1.5, "b": 1.5})["loss"] > 1e-12:
        bad.append("toy: the target does not score 0")
    return bad
