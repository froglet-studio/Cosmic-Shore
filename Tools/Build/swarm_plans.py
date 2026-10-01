"""Bake the four swarm body plans for the game from the research targets.

The research targets (Tools/NCA/results/swarm_targets/{mass,space,charge,time}.json) live on the
research branch `cece/gifted-curie-x2cpd0`. This module reads them (from that git ref by default,
or a directory passed as --src) and emits the compact plan JSON the game's SwarmPlanLibrary loads
through JsonUtility: per slot, eight frames of position + facing, plus the slot's element, prism
half-extents and tier. Everything stays in RESEARCH units (voxels): the game's sim core runs in
them unchanged, and SwarmFaunaConfigSO.UnitScale converts to world units at the boundary.

It reproduces field_swarm.Plan exactly where it matters:
  * positions are centred on frame 0's centroid (one common centring for all frames);
  * the frame order is a loop when the last frame returns to the first (wrap < 1.5 x the mean
    step), else a ping-pong 0..F-1..1.
"""
import json
import math
import os
import subprocess

RESEARCH_REF = "origin/cece/gifted-curie-x2cpd0"
RESEARCH_DIR = "Tools/NCA/results/swarm_targets"
KINDS = ("mass", "space", "charge", "time")
# research element index (0 Charge, 1 Mass, 2 Space, 3 Time) of each plan's majority
MAJOR = {"mass": 1, "space": 2, "charge": 0, "time": 3}
FRAME_STEPS = 6


def _load(kind, src, repo):
    if src:
        with open(os.path.join(src, f"{kind}.json"), encoding="utf-8") as fh:
            return json.load(fh)
    text = subprocess.run(["git", "-C", repo, "show", f"{RESEARCH_REF}:{RESEARCH_DIR}/{kind}.json"],
                          check=True, capture_output=True, text=True).stdout
    return json.loads(text)


def _r(x):
    return round(float(x), 3)


def bake(kind, src=None, repo="."):
    d = _load(kind, src, repo)
    frames = d["frames"]
    F, N = len(frames), len(frames[0]["units"])
    P = [[u["p"] for u in fr["units"]] for fr in frames]
    c = [sum(P[0][k][a] for k in range(N)) / N for a in range(3)]
    P = [[[p[a] - c[a] for a in range(3)] for p in fr] for fr in P]

    def dist(a, b):
        return math.sqrt(sum((a[i] - b[i]) ** 2 for i in range(3)))

    steps = sum(dist(P[f + 1][k], P[f][k]) for f in range(F - 1) for k in range(N)) / ((F - 1) * N)
    wrap = sum(dist(P[0][k], P[F - 1][k]) for k in range(N)) / N
    order = list(range(F)) if wrap < 1.5 * steps else list(range(F)) + list(range(F - 2, 0, -1))

    u0 = frames[0]["units"]
    elem = [int(u["elem"]) for u in u0]
    tier = [int(u["prism"].get("tier", 0)) for u in u0]
    half = [_r(v) for u in u0 for v in u["prism"]["h"]]
    pos = [_r(v) for fr in P for p in fr for v in p]
    face = []
    for fr in frames:
        for u in fr["units"]:
            f = u["f"]; n = math.sqrt(sum(x * x for x in f)) or 1.0
            face.extend(_r(x / n) for x in f)
    swim = [0, 1, 0] if kind == "space" else [1, 0, 0]
    up = [1, 0, 0] if kind == "space" else [0, 1, 0]
    return {
        "kind": kind, "name": d.get("name", kind), "major": MAJOR[kind], "n": N, "frames": F,
        "frameSteps": FRAME_STEPS, "order": order, "elem": elem, "tier": tier, "half": half,
        "pos": pos, "face": face, "swimAxis": swim, "upAxis": up,
    }


def bake_all(src=None, repo="."):
    return {k: bake(k, src, repo) for k in KINDS}


def dumps(plan):
    return json.dumps(plan, separators=(",", ":"))
