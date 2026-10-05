"""The cell's emotional range (Docs/SWARM_FAUNA.md §27): score the GAME's creatures with the research's frozen emotion
probe and compare each with the research's own read of the same species under the SAME encounter.

    python3 Tools/Build/emotion_range/emotion_range.py [--research /path/to/Tools/Ecology] [--only swarm,substrate,...]
            [--png out.png] [--json out.json] [--assert]

How (one protocol for both sides, so a difference is the creature, not the camera):
  * ENCOUNTER - the bestiary's staged encounter (research bestiary/emotion_check.py): the viewing pilot starts 350 u from
    the creature's centre, flying at it, aimed 400 u beyond it; then it wanders (research Arena pilot law: turn-rate
    limited, constant speed, a new goal 0.2-0.9 R away when within 60 u). Viewers: hover 25 u/s (turn 1.5) and cruise
    90 u/s (turn 1.8) - the research's VIEWERS. 60 s, seeds 7 / 23 / 41. The pilot never reacts to the creature, so its
    track is written once and REPLAYED into both the shipped C# core (a harness `emotion` export) and the research's
    Python model.
  * READ - emotion/timeline.py: the probe on 8 s windows every 2 s (an ambusher's snap is not diluted), plus the
    run read (all 60 s). Assembled swarm bodies are declared ONE body (agent_body_id), the research's rule (negative 9).
  * PHASE - each export frame carries the creature's own state (a swarm's strike plates, a pack's ring and strike,
    a locust cloud's phase, a lurker's lunge). A window's phase is its most urgent state present in >= 25% of its
    frames; the table reads the probe per phase.
The probe, the affect features and the research species are imported from the research tree, unchanged.
"""
from __future__ import annotations

import argparse
import importlib
import json
import math
import os
import subprocess
import sys
from collections import Counter, defaultdict

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
DT, WINDOW, STRIDE, SECONDS = 0.1, 8.0, 2.0, 60.0
SEEDS = (7, 23, 41)
VIEWERS = {"hover": (25.0, 1.5), "cruise": (90.0, 1.8)}
EMO = ("cute", "playful", "eerie", "majestic", "menacing", "terrifying", "neutral")
LETTER = dict(cute="c", playful="p", eerie="e", majestic="J", menacing="M", terrifying="T", neutral="n")
AFFECT_ORDER = ("neutral", "cute", "playful", "majestic", "eerie", "menacing", "terrifying")   # cute .. terrifying (threat)

R = None   # research modules, filled by load_research()


def load_research(path):
    global R
    sys.path.insert(0, path); sys.path.insert(0, os.path.join(path, "emotion"))
    from common.affect import AffectRecorder          # noqa
    from common.arena import Arena, Pilot              # noqa
    from probe import EmotionProbe                     # noqa
    R = dict(AffectRecorder=AffectRecorder, Arena=Arena, Pilot=Pilot, probe=EmotionProbe.load(), path=path)


# ─────────────────────────────────────────────────────────────────────────── the pilot
def unit(v):
    n = np.linalg.norm(v); return v / n if n > 1e-9 else v


def pilot_track(viewer, seed, seconds=SECONDS, arena_r=1200.0):
    """(T, 6): the pilot's offset from the creature's centre and its velocity, one row per 0.1 s."""
    speed, turn = VIEWERS[viewer]
    rng = np.random.default_rng(seed * 7919 + 13)
    d = unit(rng.normal(size=3))
    pos, vel, goal = d * 350.0, -d * speed, -d * 400.0
    out = []
    for _ in range(int(seconds / DT)):
        if np.linalg.norm(goal - pos) < 60.0:
            g = unit(rng.normal(size=3)); u = rng.random()
            lo, hi = 0.2 * arena_r, 0.9 * arena_r
            goal = g * np.cbrt(lo ** 3 + u * (hi ** 3 - lo ** 3))
        want = goal - pos; n = np.linalg.norm(want)
        if n > 1e-6:
            v = vel / max(np.linalg.norm(vel), 1e-6); w = want / n
            ang = math.acos(float(np.clip(v @ w, -1, 1)))
            k = min(1.0, turn * DT / max(ang, 1e-6))
            nd = v + (w - v) * k
            vel = nd / max(np.linalg.norm(nd), 1e-6) * speed
        pos = pos + vel * DT
        out.append(np.concatenate([pos, vel]))
    return np.array(out)


def write_track(path, T):
    with open(path, "w") as f:
        for r in T:
            f.write(" ".join(f"{x:.4f}" for x in r) + "\n")


# ─────────────────────────────────────────────────────────────────────────── frames
class Frame:
    __slots__ = ("t", "phase", "pp", "pv", "P", "V", "S", "A", "H", "B")


def read_frames(path, labels):
    """The harness's float32 frame stream (EmotionWriter): [t phase px py pz vx vy vz n] + n x 12."""
    X = np.fromfile(path, dtype="<f4"); frames = []; k = 0
    while k < len(X):
        h = X[k:k + 9]; n = int(h[8]); k += 9
        A = X[k:k + 12 * n].reshape(n, 12).astype(float); k += 12 * n
        fr = Frame(); fr.t = float(h[0]); fr.phase = labels[int(h[1])]
        fr.pp = h[2:5].astype(float); fr.pv = h[5:8].astype(float)
        fr.P, fr.V, fr.S, fr.A, fr.H, fr.B = A[:, 0:3], A[:, 3:6], A[:, 6], A[:, 7], A[:, 8:11], A[:, 11].astype(int)
        if n == 0 or not np.any(fr.H): fr.H = None      # heading not published: the probe holds the velocity direction
        frames.append(fr)
    return frames


def observe(rec, fr, bodies):
    rec.observe(fr.pp, fr.pv, fr.P, fr.V, fr.S, fr.A, fr.H, fr.B if bodies else None)


def score_frames(frames, bodies, priority):
    pr = R["probe"]
    rec = R["AffectRecorder"](DT, pilot_radius=6.0)
    for fr in frames: observe(rec, fr, bodies)
    run = pr.score(rec.features())
    n, w, s = len(frames), int(WINDOW / DT), int(STRIDE / DT)
    wins = []
    for a in range(0, max(1, n - w + 1), s):
        rec = R["AffectRecorder"](DT, pilot_radius=6.0)
        chunk = frames[a:a + w]
        for fr in chunk: observe(rec, fr, bodies)
        sc = pr.score(rec.features())
        cnt = Counter(fr.phase for fr in chunk)
        phase = next((ph for ph in priority if cnt.get(ph, 0) >= 0.25 * len(chunk)), cnt.most_common(1)[0][0])
        dmin = min(float(np.min(np.linalg.norm(fr.P - fr.pp, axis=1))) if len(fr.P) else 1e9 for fr in chunk)
        wins.append(dict(t=round(a * DT, 1), phase=phase, top=sc["top"], p=sc["p"], dmin=round(dmin, 1)))
    return dict(run=dict(top=run["top"], p=run["p"], agree=run["agreement"]), windows=wins)


# ─────────────────────────────────────────────────────────────────────────── research side
def research_frames(make, track, seed, label_fn=lambda sp, pp: "-", warm=2.0, bodies=None, centre=None, arena=None):
    """Replay `track` into a research species: make(arena, pilot) -> species. The pilot is placed by the track each
    step (it never reacts), exactly as the C# export does."""
    Arena, Pilot = R["Arena"], R["Pilot"]
    ar = arena(seed) if arena else Arena(seed=seed)
    p = ar.add_pilot(Pilot("wander", speed=25.0, turn=1.5, name="replay"))
    far = np.array([5000.0, 0, 0]); p.pos = far.copy(); p.vel = np.zeros(3); p.prev = far.copy()
    sp = make(ar, p)
    for _ in range(int(warm / DT)):
        ar.threats = list(np.asarray(sp.agent_pos)); ar.targets = ar.threats
        p.pos = far.copy(); p.vel = np.zeros(3); p.prev = far.copy()
        sp.step(ar, DT); ar.step(DT)
    c = centre(sp) if centre else np.asarray(sp.agent_pos, float).reshape(-1, 3).mean(0)
    frames = []
    for row in track:
        p.prev = p.pos.copy()                     # flora read the pilot's swept segment (prev -> pos)
        p.pos = c + row[:3]; p.vel = row[3:].copy()
        ar.threats = list(np.asarray(sp.agent_pos)); ar.targets = ar.threats
        sp.step(ar, DT)
        pp, pv = p.pos.copy(), p.vel.copy()
        ar.step(DT)
        fr = Frame(); fr.t = 0.0; fr.phase = label_fn(sp, pp); fr.pp = pp; fr.pv = pv
        P = np.asarray(sp.agent_pos, float).reshape(-1, 3); n = len(P)
        fr.P = P; fr.V = np.asarray(sp.agent_vel, float).reshape(-1, 3)
        sz = getattr(sp, "agent_size", None); fr.S = np.full(n, 3.0) if sz is None else np.broadcast_to(np.asarray(sz, float), (n,)).copy()
        asp = getattr(sp, "agent_aspect", None); fr.A = np.full(n, 1.5) if asp is None else np.broadcast_to(np.asarray(asp, float), (n,)).copy()
        H = getattr(sp, "agent_heading", None); fr.H = None if H is None or len(np.asarray(H)) != n else np.asarray(H, float).reshape(-1, 3)
        b = getattr(sp, "agent_body_id", None) if bodies is None else bodies
        fr.B = None if b is None else np.asarray(b)
        frames.append(fr)
    return frames


def observe_research(rec, fr, bodies):
    rec.observe(fr.pp, fr.pv, fr.P, fr.V, fr.S, fr.A, fr.H, fr.B if bodies else None)


# ─────────────────────────────────────────────────────────────────────────── the game side (C# harness exports)
def bash(cmd, env=None):
    e = dict(os.environ); e.update(env or {})
    r = subprocess.run(["bash", "-c", cmd], cwd=ROOT, env=e, capture_output=True, text=True)
    if r.returncode != 0:
        sys.stderr.write(r.stdout[-4000:] + r.stderr[-4000:]); raise SystemExit(f"export failed: {cmd}")
    return r.stdout


def tmpdir():
    d = os.path.join(os.environ.get("TMPDIR", "/tmp"), "emotion_range"); os.makedirs(d, exist_ok=True); return d


def tracks():
    d = tmpdir(); out = {}
    for v in VIEWERS:
        for s in SEEDS:
            path = os.path.join(d, f"pilot_{v}_{s}.txt"); write_track(path, pilot_track(v, s)); out[(v, s)] = path
    return out


# Each creature: name, group, how the game side exports, how the research side runs, phase priority, bodies,
# the research's published read (DISCOVERIES.md) and the design intent.
SWARM_PLANS = ("charge", "mass", "space", "time")
SWARM_ROLE = dict(charge="pufferfish", mass="lurker (whale)", space="locust (jellyfish)", time="pack hunter (dragonfly)")


class Harness:
    """Compile a harness once (its run.sh with an empty job list), then run its exe per job: each export is scored
    and deleted before the next is written (a dense swarm encounter is ~25 MB)."""
    def __init__(self, name, runsh, exe, pre_args, env=None):
        self.name, self.env = name, dict(env or {})
        self.exe, self.pre = exe, pre_args
        empty = os.path.join(tmpdir(), f"{name}_empty.txt"); open(empty, "w").close()
        bash(f"bash {runsh} {pre_args} emotion '{empty}'", self.env)

    def run(self, job):
        jf = os.path.join(tmpdir(), f"{self.name}_job.txt"); open(jf, "w").write(job + "\n")
        dotnet = os.path.join(os.environ.get("DOTNET_ROOT", os.path.expanduser("~/.dotnet")), "dotnet")
        tmp = os.environ.get("TMPDIR", "/tmp")
        return bash(f"'{dotnet}' '{os.path.join(tmp, self.exe)}' {self.pre} emotion '{jf}'", self.env)


def game_scores(h, job_fmt, labels, bodies, priority):
    """job_fmt(track_path, out_path, seed) -> job line; returns {viewer: [score per seed]}."""
    out = defaultdict(list)
    for v in VIEWERS:
        for s in SEEDS:
            tp = os.path.join(tmpdir(), f"pilot_{v}_{s}.txt")
            o = os.path.join(tmpdir(), f"{h.name}_out.f32")
            log = h.run(job_fmt(tp, o, s))
            out[v].append(score_frames(read_frames(o, labels), bodies, priority))
            out[v][-1]["log"] = log.strip().splitlines()[-1] if log.strip() else ""
            os.remove(o)
    return out


def research_swarm(plan, track, seed):
    """The research's own model of the game's swarm bodies (emotion/species.py SwarmBody, as ONE body)."""
    spm = importlib.import_module("species")
    kind = "dragonfly" if plan == "time" else "whale"
    return research_frames(lambda ar, p: spm.SwarmBody(np.random.default_rng(seed * 7919 + 13), p, kind), track, seed)


def summarise(per_seed_scores, viewer):
    """Merge seeds: run read = mean probability over seeds; windows pooled by phase."""
    P = defaultdict(list); phases = defaultdict(list); letters = []
    for sc in per_seed_scores:
        for e in EMO: P[e].append(sc["run"]["p"].get(e, 0.0))
        for w in sc["windows"]: phases[w["phase"]].append(w)
        letters.append("".join(LETTER[w["top"]] for w in sc["windows"]))
    run_p = {e: round(float(np.mean(P[e])), 3) for e in EMO}
    run_top = max(run_p, key=run_p.get)
    by_phase = {}
    for ph, ws in phases.items():
        pm = {e: round(float(np.mean([w["p"].get(e, 0.0) for w in ws])), 3) for e in EMO}
        tops = Counter(w["top"] for w in ws)
        by_phase[ph] = dict(n=len(ws), top=max(pm, key=pm.get), p=pm, votes=dict(tops),
                            threat=round(pm["menacing"] + pm["terrifying"], 3))
    pk = [w["p"]["menacing"] + w["p"]["terrifying"] for sc in per_seed_scores for w in sc["windows"]]
    return dict(viewer=viewer, run=dict(top=run_top, p=run_p), phases=by_phase, timelines=letters,
                peak_threat=round(float(max(pk)) if pk else 0.0, 3), mean_threat=round(float(np.mean(pk)) if pk else 0.0, 3))


def run_swarm(trk, research=True):
    h = Harness("swarm", "Tools/Build/swarm_core_harness/run.sh", "swarm_core_harness/swarmcore.exe",
                '"Assets/_SO_Assets/Swarm Fauna/Plans"', {"SWARM_DENSITY": "5"})
    res = {}
    for e, plan in enumerate(SWARM_PLANS):
        entry = {}
        g = game_scores(h, lambda tp, o, s: f"{tp} {o} {e} {s}", ("calm", "startled", "plates", "strike"), True, ("strike", "plates", "startled", "calm"))
        for v in VIEWERS:
            entry[f"game/{v}"] = summarise(g[v], v)
            entry[f"game/{v}"]["log"] = [x["log"] for x in g[v]]
            if research and plan in ("mass", "time"):
                r = [score_frames(research_swarm(plan, pilot_track(v, s), s), True, ("calm",)) for s in SEEDS]
                entry[f"research/{v}"] = summarise(r, v)
        res[f"swarm {plan} - {SWARM_ROLE[plan]}"] = entry
        print(f"swarm {plan}: " + " | ".join(f"{k}: {x['run']['top']} {max(x['run']['p'].values()):.2f} {dict((p, q['top']) for p, q in x['phases'].items())}" for k, x in entry.items()), flush=True)
    return res


def bestiary(key):
    """A bestiary species module, loaded by path (its package name `species` collides with emotion/species.py)."""
    import importlib.util
    b = os.path.join(R["path"], "bestiary")
    if b not in sys.path: sys.path.insert(0, b)
    spec = importlib.util.spec_from_file_location(f"bestiary_{key}", os.path.join(b, "species", f"{key}.py"))
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m); return m


def bestiary_arena(seed):
    ar = R["Arena"](seed=seed); ar.scatter_mass(3000); ar.enable_trails(spacing=15.0, vol=10.0); return ar


def research_bestiary(key, track, seed):
    m = bestiary(key)
    pick = np.random.default_rng(seed)
    def centre(sp):
        live = np.flatnonzero(sp.alive); return sp.pos[pick.choice(live)]   # the bestiary's encounter: one individual
    return research_frames(lambda ar, p: m.make(ar), track, seed, centre=centre, arena=bestiary_arena)


def substrate_label(cond):
    def f(sp, pp):
        a = sp._live; ph = sp.phase[a]
        if cond == "pack":
            if (ph > 0.5).any(): return "strike"
            near = np.linalg.norm(sp.pos[a] - pp, axis=1) < 350.0
            return "ring" if len(a) and near.sum() >= min(4, len(a)) else "stalk"
        if cond.startswith("locust"): return "storm" if (ph > 0.5).sum() >= 0.2 * max(1, len(a)) else "solitary"
        return "snap" if (ph > 0.5).any() else "gape" if (ph > 0.05).any() else "still"
    return f


SUB_LABELS = dict(pack=("stalk", "ring", "strike", "winded"), locust_sparse=("solitary", "storm"),
                  locust_dense=("solitary", "storm"), lurker=("still", "creep", "gape", "snap", "spent"))
SUB_PRIORITY = dict(pack=("strike", "winded", "ring", "stalk"), locust_sparse=("storm", "solitary"),
                    locust_dense=("storm", "solitary"), lurker=("snap", "spent", "gape", "creep", "still"))
SUB_BESTIARY = dict(pack="pack", locust_sparse="locust", locust_dense=None, lurker="lurker")


def research_substrate(cond, track, seed):
    importlib.import_module("substrate")
    from substrate.core import Substrate
    from substrate import species as S
    home = np.array([0.0, 0.0, 300.0])
    def make(ar, p):
        if cond == "pack":
            ar.scatter_mass(2500); return Substrate(ar, S.pack(seed=seed), center=home, spread=50.0)
        if cond == "locust_sparse":
            ar.scatter_mass(900); return Substrate(ar, S.locust(n0=40, seed=seed), center=home, spread=300.0, init_hunger=0.1)
        if cond == "locust_dense":
            return Substrate(ar, S.locust(n0=300, seed=seed), center=home, spread=40.0, init_hunger=0.9)
        ar.scatter_mass(1500); return Substrate(ar, S.lurker(seed=seed), center=home, spread=120.0)
    return research_frames(make, track, seed, label_fn=substrate_label(cond))


def run_substrate(trk, research=True):
    h = Harness("substrate", "Tools/Build/substrate_harness/run.sh", "substrate_harness/substrate.exe",
                "Tools/Build/substrate_harness/research_params.json")
    res = {}
    for cond in ("pack", "locust_sparse", "locust_dense", "lurker"):
        entry = {}
        g = game_scores(h, lambda tp, o, s: f"{tp} {o} {cond} {s}", SUB_LABELS[cond], False, SUB_PRIORITY[cond])
        for v in VIEWERS:
            entry[f"game/{v}"] = summarise(g[v], v)
            entry[f"game/{v}"]["log"] = [x["log"] for x in g[v]]
            if research:
                r = [score_frames(research_substrate(cond, pilot_track(v, s), s), False, SUB_PRIORITY[cond]) for s in SEEDS]
                entry[f"research/{v}"] = summarise(r, v)
                if SUB_BESTIARY[cond]:
                    b = [score_frames(research_bestiary(SUB_BESTIARY[cond], pilot_track(v, s), s), False, ("-",)) for s in SEEDS]
                    entry[f"bestiary/{v}"] = summarise(b, v)
        res[f"substrate {cond}"] = entry
        print(f"substrate {cond}: " + " | ".join(f"{k}: {x['run']['top']} {max(x['run']['p'].values()):.2f} {dict((p, q['top']) for p, q in x['phases'].items())}" for k, x in entry.items()), flush=True)
    return res


def research_fortress(track, seed):
    from builders.fortress import Fortress
    def make(ar, p):
        ar.scatter_mass(1500); ar.struct_owner = {}
        return Fortress(ar, seed=seed)
    lab = lambda sp, pp: "strike" if (np.asarray(sp.intent) >= 1.0).any() else "build"
    return research_frames(make, track, seed, label_fn=lab, warm=120.0)


def run_builders(trk, research=True):
    h = Harness("builders", "Tools/Build/builders_harness/run.sh", "builders_harness/builders.exe", "")
    res = {}
    for kind, labels in (("fortress", ("build", "strike")), ("thief", ("forage", "laden"))):
        entry = {}
        g = game_scores(h, lambda tp, o, s: f"{tp} {o} {kind} {s}", labels, False, labels[::-1])
        for v in VIEWERS:
            entry[f"game/{v}"] = summarise(g[v], v)
            entry[f"game/{v}"]["log"] = [x["log"] for x in g[v]]
            if research:
                if kind == "fortress":
                    r = [score_frames(research_fortress(pilot_track(v, s), s), False, ("strike", "build")) for s in SEEDS]
                    entry[f"research/{v}"] = summarise(r, v)
                else:
                    b = [score_frames(research_bestiary("thief", pilot_track(v, s), s), False, ("-",)) for s in SEEDS]
                    entry[f"bestiary/{v}"] = summarise(b, v)
        res[f"builders {kind}"] = entry
        print(f"builders {kind}: " + " | ".join(f"{k}: {x['run']['top']} {max(x['run']['p'].values()):.2f} {dict((p, q['top']) for p, q in x['phases'].items())}" for k, x in entry.items()), flush=True)
    return res


def research_flora(kind, track, seed):
    fl = os.path.join(R["path"], "flora")
    if fl not in sys.path: sys.path.insert(0, fl)
    from harness import FloraArena, GROVE_C              # flora's own modules import `harness` top-level
    def arena(sd):
        ar = FloraArena(seed=sd); ar.grove_mass(1600, clumps=20); return ar
    def make(ar, p):
        if kind == "snaptrap":
            from snaptrap import SnapTrap; sp = SnapTrap(ar)
        else:
            from physarum import Physarum; sp = Physarum(ar)
        ar.species = sp; return sp
    lab = (lambda sp, pp: "snap" if (np.asarray(sp.intent) >= 0.5).any() else "rest") if kind == "snaptrap" else (lambda sp, pp: "-")
    return research_frames(make, track, seed, label_fn=lab, arena=arena, centre=lambda sp: np.asarray(GROVE_C, float))


def run_flora(trk, research=True):
    h = Harness("flora", "Tools/Build/threat_flora_harness/run.sh", "threat_flora_harness/threatflora.exe", "")
    res = {}
    for kind, labels in (("snaptrap", ("rest", "snap")), ("physarum", ("rest", "pulse"))):
        entry = {}
        g = game_scores(h, lambda tp, o, s: f"{tp} {o} {kind} {s}", labels, False, labels[::-1])
        for v in VIEWERS:
            entry[f"game/{v}"] = summarise(g[v], v)
            entry[f"game/{v}"]["log"] = [x["log"] for x in g[v]]
            if research:
                r = [score_frames(research_flora(kind, pilot_track(v, s), s), False, ("snap", "rest", "-")) for s in SEEDS]
                entry[f"research/{v}"] = summarise(r, v)
        res[f"flora {kind}"] = entry
        print(f"flora {kind}: " + " | ".join(f"{k}: {x['run']['top']} {max(x['run']['p'].values()):.2f} {dict((p, q['top']) for p, q in x['phases'].items())}" for k, x in entry.items()), flush=True)
    return res


GROUPS = {"swarm": run_swarm, "substrate": run_substrate, "builders": run_builders, "flora": run_flora}


# ─────────────────────────────────────────────────────────────────────────── chart and asserted reads
ROWS = [   # (result key, phases shown in this order, short name) - game reads, hovering pilot
    ("substrate locust_sparse", ("solitary",), "locust, sparse + fed"),
    ("builders thief", ("forage", "laden"), "thief nest"),
    ("flora physarum", ("rest", "pulse"), "physarum grove"),
    ("flora snaptrap", ("rest", "snap"), "snap-trap grove"),
    ("swarm charge - pufferfish", ("startled", "plates", "strike"), "swarm pufferfish (Charge)"),
    ("swarm space - locust (jellyfish)", ("startled", "plates", "strike"), "swarm jellyfish (Space)"),
    ("swarm mass - lurker (whale)", ("startled", "plates", "strike"), "swarm whale (Mass)"),
    ("swarm time - pack hunter (dragonfly)", ("startled", "plates", "strike"), "swarm dragonfly (Time)"),
    ("builders fortress", ("build", "strike"), "fortress colony"),
    ("substrate lurker", ("creep", "gape", "snap", "spent"), "lurker"),
    ("substrate pack", ("stalk", "ring", "strike", "winded"), "pack hunters"),
    ("substrate locust_dense", ("solitary", "storm"), "locust, dense + hungry"),
]
COLS = ("neutral", "cute", "playful", "majestic", "eerie", "menacing", "terrifying")


def research_top(entry, viewer="hover"):
    for k in (f"research/{viewer}", f"bestiary/{viewer}"):
        if k in entry: return k.split("/")[0], entry[k]["run"]["top"]
    return None, None


def chart(res, path):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.colors import LinearSegmentedColormap
    ramp = ["#fcfcfb", "#cde2fb", "#9ec5f4", "#6da7ec", "#3987e5", "#256abf", "#184f95", "#0d366b"]
    cmap = LinearSegmentedColormap.from_list("blue", ramp)
    rows, labels, rtops = [], [], []
    for key, phases, name in ROWS:
        if key not in res: continue
        g = res[key]["game/hover"]; src, rt = research_top(res[key])
        rph = res[key].get(f"{src}/hover", {}).get("phases", {}) if src else {}
        first = True
        for ph in phases:
            if ph not in g["phases"] or g["phases"][ph]["n"] < 2: continue
            rows.append([g["phases"][ph]["p"].get(e, 0.0) for e in COLS]); labels.append(f"{name} - {ph}")
            # the research's read of the SAME phase when its model has one, else its whole-encounter read (first row)
            rtops.append(rph[ph]["top"] if ph in rph and rph[ph]["n"] >= 2 else (rt if first and not any(q in rph for q in phases) else None))
            first = False
    M = np.array(rows)
    fig, ax = plt.subplots(figsize=(8.6, 0.32 * len(rows) + 1.6), dpi=150)
    fig.patch.set_facecolor("#fcfcfb"); ax.set_facecolor("#fcfcfb")
    ax.imshow(M, cmap=cmap, vmin=0, vmax=0.9, aspect="auto")
    for i in range(len(rows)):
        j = int(np.argmax(M[i]))
        ax.text(j, i, f"{M[i, j]:.2f}", ha="center", va="center", fontsize=7, color="#ffffff" if M[i, j] > 0.45 else "#0b0b0b")
        if rtops[i] in COLS:
            ax.add_patch(plt.Rectangle((COLS.index(rtops[i]) - 0.46, i - 0.42), 0.92, 0.84, fill=False, ec="#c2410c", lw=1.4))
        if i and labels[i].split(" - ")[0] != labels[i - 1].split(" - ")[0]:
            ax.axhline(i - 0.5, color="#fcfcfb", lw=2.5)
    ax.set_xticks(range(len(COLS))); ax.set_xticklabels(COLS, fontsize=8, color="#52514e")
    ax.xaxis.tick_top()
    ax.set_yticks(range(len(rows))); ax.set_yticklabels(labels, fontsize=7.5, color="#0b0b0b")
    for s in ax.spines.values(): s.set_visible(False)
    ax.tick_params(length=0)
    fig.suptitle("The cell's emotional range - each creature's phases read by the research's emotion probe",
                 fontsize=9.5, color="#0b0b0b", x=0.01, ha="left")
    fig.text(0.01, 0.006, "Cell = mean P(emotion) over the 8 s windows in that phase (hovering pilot, 3 seeds x 60 s); the number is the read.\n"
             "Orange box = the research's own model of the same species in the same encounter (that phase's read where its model has the phase).",
             fontsize=6.5, color="#52514e", ha="left")
    fig.tight_layout(rect=(0, 0.035, 1, 0.97))
    fig.savefig(path, facecolor=fig.get_facecolor()); plt.close(fig)


def asserts(res):
    """The reads this round stands on. Returns the failures."""
    bad = []
    def top(key, v="hover", side="game"): return res[key][f"{side}/{v}"]["run"]["top"]
    def ph(key, phase, v="hover"): return res[key][f"game/{v}"]["phases"].get(phase, {}).get("top")
    def check(ok, what):
        print(("  [ok]   " if ok else "  [FAIL] ") + what); (None if ok else bad.append(what))
    check(all(top("substrate locust_sparse", v) == "cute" for v in VIEWERS), "the sparse fed locust reads cute to both viewers (research: cute 0.86)")
    check(all(top("substrate locust_dense", v) == "terrifying" for v in VIEWERS) and ph("substrate locust_dense", "storm") == "terrifying",
          "the dense hungry locust's storm reads terrifying (research: terrifying 0.95)")
    check(top("substrate pack") in ("terrifying", "menacing") and ph("substrate pack", "strike") == "terrifying",
          "the pack reads as a threat to a hovering pilot and its strike reads terrifying (bestiary pack: terrifying 4/4)")
    check(ph("substrate lurker", "snap") == "terrifying", "the lurker's snap reads terrifying (bestiary timeline: terror at the snap)")
    check(top("builders thief") in ("playful", "cute"), "the thief nest reads playful/cute - mischief, not threat (bestiary thief: playful)")
    for k in [k for k in res if k.startswith("swarm ")]:
        check(all(top(k, v) in ("majestic", "menacing", "terrifying") for v in VIEWERS),
              f"{k}: a swarm body reads big (majestic..terrifying), never cute (research: swarm bodies majestic)")
    for k in ("swarm mass - lurker (whale)", "swarm time - pack hunter (dragonfly)"):
        if f"research/hover" in res.get(k, {}):
            g, r = res[k]["game/hover"]["run"]["p"], res[k]["research/hover"]["run"]["p"]
            dt = abs((g["menacing"] + g["terrifying"]) - (r["menacing"] + r["terrifying"]))
            check(dt < 0.25, f"{k}: game threat {g['menacing'] + g['terrifying']:.2f} within 0.25 of the research model's {r['menacing'] + r['terrifying']:.2f}")
    tops = {w for e in res.values() for k, x in e.items() if k.startswith("game/") for w in [x["run"]["top"]] + [q["top"] for q in x["phases"].values()]}
    check({"cute", "terrifying"} <= tops and len(tops & set(COLS)) >= 6,
          f"the cell spans cute to terrifying: game reads include {sorted(tops)}")
    return bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--research", default=os.environ.get("EMOTION_RESEARCH", "/home/claude/research/Tools/Ecology"))
    ap.add_argument("--only", default=",".join(GROUPS))
    ap.add_argument("--json", default=os.path.join(HERE, "emotion_range_results.json"))
    ap.add_argument("--png", default=None)
    ap.add_argument("--assert", dest="check", action="store_true", help="assert the reads (exit 1 on a failure)")
    ap.add_argument("--rescore", action="store_true", help="skip the runs: chart and assert the saved JSON")
    a = ap.parse_args()
    if a.rescore:
        allr = json.load(open(a.json))
        if a.png: chart(allr, a.png); print("wrote", a.png)
        return 1 if a.check and asserts(allr) else 0
    load_research(a.research)
    trk = tracks()
    allr = json.load(open(a.json)) if os.path.exists(a.json) else {}
    for g in a.only.split(","):
        allr.update(GROUPS[g](trk))
        json.dump(allr, open(a.json, "w"), indent=1)
    print("wrote", a.json)
    if a.png: chart(allr, a.png); print("wrote", a.png)
    if a.check and asserts(allr): return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
