#!/usr/bin/env python3
"""Player behaviour creates new course genes: the Regatta PLAYER PRIOR (Plan step: time-arena-course; Regatta flywheel
W2; design: Docs/AI_SYSTEM/REGATTA_COURSE_GENOME.md "Genes from play").

Every Regatta race a person flies in the editor is recorded (HumanFlightRecorder, cosmic-shore.flight/3) and sent to a
bugledger/<user> branch on its own. This reads those flights and measures, per intensity and leg (ring to ring):

  ratio   the human's leg time over the median AI rival's leg time in the same race (< 1: people outfly the AIs here)
  line    where the human flew through the leg, in a placement's own coordinates about the spine (radius, bearing)
  hits    rail prisms destroyed near the human on that leg, per pass

and turns that into two things the course search uses:

  seed_genomes(prior, rng, intensity, n)   courses made from play: structure beside the line people chose on the legs
                                            where they beat the AIs, alternate rings on the lines they found, and a
                                            wider tube with nothing placed on the legs where they lose or crash
  regatta_genome.mutate(g, rng, prior=P)    half of the new or moved placements land on the legs and lines people flew

The prior proposes; the race judge still scores. No player's name is stored: only hulls, counts and file stamps.

    python3 Tools/Compute/evaluators/regatta_player_prior.py --build [--out Tools/Compute/regatta/player_prior.json]
                                                             (reads every origin/bugledger/* flight_Regatta_*.json)
    python3 Tools/Compute/evaluators/regatta_player_prior.py --seeds N --intensity I --out DIR
    python3 Tools/Compute/evaluators/regatta_player_prior.py --self-test
"""
import argparse
import json
import math
import os
import random
import statistics
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, HERE)
import course_port as cp  # noqa: E402
import regatta_genome as rg  # noqa: E402

PRIOR = os.path.join(REPO, "Tools", "Compute", "regatta", "player_prior.json")
RINGS = rg.RINGS
SUBS = 4                 # line samples per leg (quarters)
MIN_PASSES = 3           # a leg needs this many human passes before its weight moves off 1
SKILL_RATIO = 0.9        # people beat the AIs here
PAIN_RATIO = 1.3         # people lose here
HIT_RADIUS = 30.0        # a rail prism destroyed this close to the human counts as the human's hit
SPINE_STEPS = 48         # spine samples per leg for the projection


# ── Geometry: a point in a placement's coordinates (leg, radius, bearing) ────────────────────────────────────────────

def _gates_from_rings(ring_list):
    return [{"p": cp.v3(r[1], r[2], r[3]), "a": cp.normalized(cp.v3(r[4], r[5], r[6])), "r": float(r[7])}
            for r in sorted(ring_list, key=lambda r: r[0])]


class Spine:
    """The course's spine sampled densely, to project a pilot's position into (leg, radius, bearing)."""

    def __init__(self, gates):
        self.gates = gates
        self.n = len(gates)
        self.samples = []
        for i in range(self.n):
            for s in range(SPINE_STEPS):
                leg = i + s / SPINE_STEPS
                point, tangent, outward = rg.spine_frame(gates, leg)
                self.samples.append((leg, point, tangent, outward))

    def project(self, pos, near_leg=None):
        best, bd = None, float("inf")
        for smp in self.samples:
            if near_leg is not None:
                d_leg = (smp[0] - near_leg) % self.n
                if min(d_leg, self.n - d_leg) > 1.5:
                    continue
            d = (pos[0] - smp[1][0]) ** 2 + (pos[1] - smp[1][1]) ** 2 + (pos[2] - smp[1][2]) ** 2
            if d < bd:
                best, bd = smp, d
        leg, point, tangent, outward = best
        off = [pos[k] - point[k] for k in range(3)]
        along = sum(off[k] * tangent[k] for k in range(3))
        side = cp.cross(tangent, outward)
        x = sum(off[k] * outward[k] for k in range(3))
        y = sum(off[k] * side[k] for k in range(3))
        return leg, math.hypot(x, y), math.degrees(math.atan2(y, x)) % 360.0, along


# ── One flight ────────────────────────────────────────────────────────────────────────────────────────────────────

def _thread_times(times, rings):
    """[(ring count reached, time)]: where the counter starts, then each time it steps up."""
    if not rings:
        return []
    out, last = [(rings[0], times[0])], rings[0]
    for t, r in zip(times, rings):
        while r > last:
            last += 1
            out.append((last, t))
    return out


def read_flight(flight):
    """One flight's per-leg evidence: [{"leg", "ratio"|None, "line": [(u, radius, bearing)], "hits"}], or None when
    it is not a usable Regatta race."""
    # "mixed" is a race the AI held for part of (those legs are skipped below by the held column); records before
    # the pilot field have none and are a person's own races.
    if flight.get("mode") != "Regatta" or flight.get("pilot") not in ("human", "mixed", None):
        return None
    cols = flight.get("columns") or []
    samples = flight.get("samples") or []
    rings = flight.get("ringList") or []
    if len(rings) != RINGS or not samples or "rings" not in cols:
        return None
    ci = {c: i for i, c in enumerate(cols)}
    held_i = ci.get("held")
    t = [s[ci["t"]] for s in samples]
    human_rings = [int(s[ci["rings"]]) for s in samples]
    human = _thread_times(t, human_rings)
    if len(human) < 2:
        return None
    gates = _gates_from_rings(rings)
    spine = Spine(gates)

    # The rivals: four numbers each per sample (x, y, z, rings), sampled with the human.
    riv = flight.get("rivals") or []
    n_riv = len(flight.get("rivalHulls") or [])
    rival_threads = []
    for j in range(n_riv):
        rr = [int(row[4 * j + 3]) if len(row) > 4 * j + 3 else 0 for row in riv]
        rival_threads.append(dict(_thread_times(t[:len(rr)], rr)))

    # The rail prisms the race destroyed, by time, with their positions.
    rails = flight.get("rails") or []
    hits = []
    for ev in flight.get("railEvents") or []:
        tt, rail, idx, state = ev[0], int(ev[1]), int(ev[2]), ev[3]
        if state != 1 or rail >= len(rails) or idx >= len(rails[rail].get("prisms", [])):
            continue
        p = rails[rail]["prisms"][idx]
        hits.append((tt, (p[0], p[1], p[2])))

    legs = []
    for (k0, t0), (k1, t1) in zip(human, human[1:]):
        if k1 < 2:
            continue                                           # the run to the first ring is a start, not a leg
        # Threading ring count k1 means the k1-th ring of the race: ring (k1 - 1) mod 8 in course order, reached
        # from ring (k1 - 2) mod 8, along spine leg (k1 - 2) mod 8.
        leg = (k1 - 2) % RINGS
        dt = t1 - t0
        ai = [rt[k1] - rt[k0] for rt in rival_threads if k0 in rt and k1 in rt]
        ratio = dt / statistics.median(ai) if ai and statistics.median(ai) > 0 else None
        line = []
        held = False
        for s, ts in zip(samples, t):
            if ts < t0 or ts > t1:
                continue
            if held_i is not None and s[held_i]:
                held = True
                break
            lg, rad, brg, _ = spine.project((s[ci["x"]], s[ci["y"]], s[ci["z"]]), near_leg=leg + 0.5)
            u = (lg - leg) % RINGS
            if u <= 1.0:
                line.append((u, rad, brg))
        if held:
            continue                                           # the AI held the hull: not the human's leg
        n_hits = 0
        for tt, pos in hits:
            if t0 <= tt <= t1:
                near = min((sum((s[ci[c]] - pos[k]) ** 2 for k, c in enumerate("xyz")), ts)
                           for s, ts in zip(samples, t) if abs(ts - tt) < 0.3) if any(abs(ts - tt) < 0.3 for ts in t) else None
                if near and near[0] <= HIT_RADIUS ** 2:
                    n_hits += 1
        legs.append({"leg": leg, "ratio": ratio, "line": line, "hits": n_hits})
    return legs


# ── The prior ────────────────────────────────────────────────────────────────────────────────────────────────────

def _circ_mean(degs):
    if not degs:
        return 0.0
    x = sum(math.cos(math.radians(d)) for d in degs)
    y = sum(math.sin(math.radians(d)) for d in degs)
    return math.degrees(math.atan2(y, x)) % 360.0


def build(flights):
    """flights: [(stamp, flight dict)]. Returns the prior."""
    acc = {}
    used, hulls = [], {}
    for stamp, fl in flights:
        legs = read_flight(fl)
        if not legs:
            continue
        inten = str(int(fl.get("courseIntensity") or fl.get("intensity") or 1))
        used.append({"flight": stamp, "intensity": int(inten), "hull": fl.get("hull"), "legs": len(legs)})
        hulls[fl.get("hull")] = hulls.get(fl.get("hull"), 0) + 1
        for lg in legs:
            a = acc.setdefault(inten, {}).setdefault(lg["leg"], {"ratios": [], "hits": 0, "passes": 0,
                                                                 "line": [[] for _ in range(SUBS)]})
            a["passes"] += 1
            a["hits"] += lg["hits"]
            if lg["ratio"] is not None:
                a["ratios"].append(lg["ratio"])
            for u, rad, brg in lg["line"]:
                a["line"][min(SUBS - 1, int(u * SUBS))].append((rad, brg))
    out = {"version": 1, "flights": used, "hulls": hulls, "by_intensity": {}}
    for inten, legs in acc.items():
        rows = []
        for leg in range(RINGS):
            a = legs.get(leg)
            if not a:
                rows.append({"leg": leg, "passes": 0, "ratio": None, "hits_per_pass": 0.0, "weight": 1.0,
                             "line": [], "off_line_share": 0.0, "role": "unknown"})
                continue
            ratio = statistics.median(a["ratios"]) if a["ratios"] else None
            line, off, tot = [], 0, 0
            for q, pts in enumerate(a["line"]):
                if not pts:
                    continue
                rads = [p[0] for p in pts]
                off += sum(1 for r in rads if r > rg.LINE_CLEARANCE[2])
                tot += len(rads)
                line.append({"u": round((q + 0.5) / SUBS, 3), "radius": round(statistics.median(rads), 2),
                             "bearing": round(_circ_mean([p[1] for p in pts if p[0] > 5.0]), 1),
                             "spread": round(statistics.pstdev(rads), 2) if len(rads) > 1 else 0.0})
            hpp = a["hits"] / a["passes"]
            enough = a["passes"] >= MIN_PASSES and ratio is not None
            role = "unknown"
            weight = 1.0
            if enough:
                weight = max(0.5, min(2.0, 1.0 / ratio)) ** 2
                role = "skill" if ratio < SKILL_RATIO else ("pain" if ratio > PAIN_RATIO or hpp > 3 else "even")
            rows.append({"leg": leg, "passes": a["passes"], "ratio": None if ratio is None else round(ratio, 4),
                         "hits_per_pass": round(hpp, 3), "weight": round(weight, 4), "line": line,
                         "off_line_share": round(off / tot, 4) if tot else 0.0, "role": role})
        out["by_intensity"][inten] = {"legs": rows}
    return out


def legs_for(prior, intensity):
    if not prior:
        return None
    block = prior.get("by_intensity", {}).get(str(int(intensity)))
    return block["legs"] if block else None


# ── What the search does with it ──────────────────────────────────────────────────────────────────────────────────

def draw_where(prior, intensity, rng):
    """(leg, [radius, bearing, along]) near a line people flew, the leg drawn by weight; None when the prior has
    nothing for this intensity. Used by regatta_genome.mutate(prior=)."""
    legs = legs_for(prior, intensity)
    if not legs:
        return None
    w = [max(0.0, r["weight"]) for r in legs]
    if sum(w) <= 0:
        return None
    leg = rng.choices(range(len(legs)), weights=w)[0]
    row = legs[leg]
    if not row["line"]:
        return leg + rng.random(), [rng.uniform(0.0, 160.0), rng.uniform(0.0, 360.0), rng.uniform(-60.0, 60.0)]
    pt = rng.choice(row["line"])
    # Beside the line, not on it: the line itself is the tube the carve keeps clear.
    radius = max(0.0, pt["radius"] + rng.choice((-1, 1)) * rng.uniform(40.0, 140.0))
    bearing = pt["bearing"] + rng.gauss(0.0, 35.0)
    return leg + min(0.999, max(0.0, pt["u"] + rng.gauss(0.0, 0.08))), [radius, bearing, rng.uniform(-30.0, 30.0)]


def seed_genome(prior, rng, intensity):
    """One course made from play on the shipped circuit of `intensity` (no circuit override, so the legs are the legs
    people flew). Returns the repaired genome; its "notes" say which evidence made each piece."""
    legs = legs_for(prior, intensity) or []
    g = rg.empty()
    g["intensity"] = int(intensity)
    made = []
    pain = [r for r in legs if r["role"] == "pain"]
    if pain:
        g["line_clearance"] = min(rg.LINE_CLEARANCE[1], rg.LINE_CLEARANCE[2] + 12.0 * len(pain))
        made.append(f"line tube {g['line_clearance']:.0f} u for pain legs {[r['leg'] for r in pain]}")
    for r in legs:
        if r["role"] == "skill":
            for pt in r["line"]:
                if rng.random() < 0.6:
                    continue
                p = rg.random_placement(rng)
                # Opposite the side people chose: their line stays open, the rest of the leg gets structure.
                p["leg"] = r["leg"] + pt["u"]
                p["offset"] = [max(60.0, pt["radius"] + rng.uniform(50.0, 160.0)),
                               (pt["bearing"] + 180.0 + rng.gauss(0.0, 30.0)) % 360.0, rng.uniform(-20.0, 20.0)]
                p["scale"] = min(p["scale"], 120.0)
                g["placements"].append(p)
            made.append(f"structure beside the human line on skill leg {r['leg']} (ratio {r['ratio']})")
        if r["line"] and r["off_line_share"] > 0.5:
            end = r["line"][-1]
            if end["radius"] > 100.0:
                g["features"]["AlternateGates"].append({"Ring": (r["leg"] + 1) % RINGS,
                                                        "Offset": max(150.0, min(420.0, end["radius"])),
                                                        "Bearing": end["bearing"], "RadiusScale": 1.0})
                made.append(f"alternate ring {(r['leg'] + 1) % RINGS} on the line people chose")
    g, notes = rg.repair(g)
    g["origin"] = {"from": "player prior", "flights": len((prior or {}).get("flights", [])), "made": made}
    return g


def seed_genomes(prior, rng, intensity, n):
    return [seed_genome(prior, rng, intensity) for _ in range(n)]


# ── Reading the flights ──────────────────────────────────────────────────────────────────────────────────────────

def git_flights(repo=REPO):
    """Every Regatta flight on the origin/bugledger/* branches (fetched first): [(stamp, flight)]. The stamp is the
    file name without its owner's folder, so no name goes into the prior."""
    subprocess.run(["git", "-C", repo, "fetch", "-q", "origin", "refs/heads/bugledger/*:refs/remotes/origin/bugledger/*"],
                   capture_output=True)
    refs = subprocess.run(["git", "-C", repo, "for-each-ref", "--format=%(refname)", "refs/remotes/origin/bugledger/"],
                          capture_output=True, text=True).stdout.split()
    out, seen = [], set()
    for ref in refs:
        names = subprocess.run(["git", "-C", repo, "ls-tree", "-r", "--name-only", ref], capture_output=True,
                               text=True).stdout.split("\n")
        for name in names:
            base = os.path.basename(name)
            if not base.startswith("flight_Regatta_") or not base.endswith(".json") or base in seen:
                continue
            seen.add(base)
            raw = subprocess.run(["git", "-C", repo, "show", f"{ref}:{name}"], capture_output=True, text=True).stdout
            try:
                out.append((base, json.loads(raw)))
            except ValueError:
                continue
    return out


# ── Self-test ────────────────────────────────────────────────────────────────────────────────────────────────────

def _synthetic_flight(rng, human_fast_leg=3, ratio=0.7, off_line=0.0, hold=False):
    """A flight on the shipped I1 course: the human and three AIs fly the spine, one lap, the AIs at 10 s a leg, the
    human at 10 s except `human_fast_leg` (10 x ratio), `off_line` u out from the spine at bearing 90."""
    gs = rg.gates({"intensity": 1})
    ring_list = [[i, *g["p"], *g["a"], 110] for i, g in enumerate(gs)]
    cols = ["t", "x", "y", "z", "rings", "held"]
    samples, rivals = [], []
    # Start just past ring 0's plane (rings counter 1 = ring 0 threaded), fly leg 0, 1, ... 7 to thread ring 0 again.
    t, h_ring, legs_done = 0.0, 1, 0
    a_time = 10.0
    h_times = [10.0 * (ratio if leg == human_fast_leg else 1.0) for leg in range(RINGS)]
    t_h = [0.0]
    for d in h_times:
        t_h.append(t_h[-1] + d)
    t_a = [10.0 * i for i in range(RINGS + 1)]
    end = max(t_h[-1], t_a[-1])
    t = 0.0
    while t <= end + 0.25:
        def where(times):
            k = max(i for i in range(len(times)) if times[i] <= t + 1e-9)
            k = min(k, RINGS - 1) if t < times[-1] else RINGS
            u = 0.0 if k >= RINGS else (t - times[k]) / (times[k + 1] - times[k])
            return k, u
        hk, hu = where(t_h)
        point, tangent, outward = rg.spine_frame(gs, min(RINGS - 1e-3, hk + hu))
        side = cp.cross(tangent, outward)
        pos = [point[i] + side[i] * off_line for i in range(3)]
        samples.append([round(t, 3), *pos, 1 + hk, 1 if hold else 0])
        row = []
        for j in range(3):
            ak, au = where(t_a)
            ap, _, _ = rg.spine_frame(gs, min(RINGS - 1e-3, ak + au))
            row += [ap[0], ap[1], ap[2], 1 + ak]
        rivals.append(row)
        t += 0.1
    return {"mode": "Regatta", "pilot": "human", "hull": "Urchin", "courseIntensity": 1, "intensity": 1,
            "columns": cols, "samples": samples, "ringList": ring_list, "rivals": rivals,
            "rivalHulls": ["a", "b", "c"], "rails": [], "railEvents": []}


def self_test():
    rng = random.Random(5)
    flights = [(f"syn_{i}", _synthetic_flight(rng, off_line=120.0)) for i in range(3)]
    prior = build(flights)
    legs = legs_for(prior, 1)
    assert legs, "no legs measured"
    fast = legs[3]
    assert fast["passes"] == 3, fast
    assert abs(fast["ratio"] - 0.7) < 0.03, f"leg 3 ratio {fast['ratio']} (want 0.7)"
    assert fast["role"] == "skill", fast
    for r in legs:
        if r["leg"] != 3 and r["passes"]:
            assert abs(r["ratio"] - 1.0) < 0.03 and r["role"] == "even", r
    rad = [pt["radius"] for pt in fast["line"]]
    assert all(abs(x - 120.0) < 12.0 for x in rad), f"line radius {rad} (want ~120)"
    assert fast["weight"] > 1.5 and legs[1]["weight"] == 1.0, (fast["weight"], legs[1]["weight"])
    # Draws land on the skill leg about twice as often as on an even leg.
    counts = [0] * RINGS
    for _ in range(4000):
        leg, off = draw_where(prior, 1, rng)
        counts[int(leg)] += 1
    assert counts[3] > 1.6 * (sum(counts) - counts[3]) / 7, counts
    # Seeds are legal (repair leaves them alone) and put structure on leg 3 only.
    for g in seed_genomes(prior, rng, 1, 10):
        again, notes = rg.repair(json.loads(json.dumps({k: v for k, v in g.items() if k != "origin"})))
        assert not notes, notes
        assert all(3.0 <= p["leg"] < 4.0 for p in g["placements"]), [p["leg"] for p in g["placements"]]
    # mutate with the prior stays legal and leans to leg 3.
    g, on3 = rg.empty(), 0
    g["intensity"] = 1
    for _ in range(60):
        g = rg.mutate(g, rng, 1.0, prior=prior)
        assert not rg.repair(json.loads(json.dumps(g)))[1]
    on3 = sum(1 for p in g["placements"] if 3.0 <= p["leg"] % RINGS < 4.0)
    # Negative controls: an AI-held race counts nothing; two passes do not move a weight.
    held = build([("held", _synthetic_flight(rng, hold=True))])
    assert not legs_for(held, 1), "an AI-held race was counted"
    thin = legs_for(build(flights[:2]), 1)
    assert thin[3]["weight"] == 1.0 and thin[3]["role"] == "unknown", thin[3]
    print(f"regatta_player_prior self-test OK: leg 3 ratio {fast['ratio']} (skill, weight {fast['weight']}), line "
          f"{rad[0]:.0f} u, draws {counts}, {on3}/{len(g['placements'])} mutated placements on leg 3, 2 negative controls.")
    return 0


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--build", action="store_true")
    ap.add_argument("--seeds", type=int, default=0)
    ap.add_argument("--intensity", type=int, default=1)
    ap.add_argument("--seed", type=int, default=20261116)
    ap.add_argument("--prior", default=PRIOR)
    ap.add_argument("--out")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        return self_test()
    if a.build:
        flights = git_flights()
        prior = build(flights)
        out = a.out or PRIOR
        os.makedirs(os.path.dirname(out), exist_ok=True)
        with open(out, "w") as f:
            json.dump(prior, f, indent=1, sort_keys=True)
            f.write("\n")
        print(f"read {len(flights)} Regatta flights, {len(prior['flights'])} usable; wrote {out}")
        for inten, block in sorted(prior["by_intensity"].items()):
            roles = " ".join(f"{r['leg']}:{r['role'][0]}{'' if r['ratio'] is None else format(r['ratio'], '.2f')}"
                             f"/{r['passes']}" for r in block["legs"])
            print(f"  I{inten}: {roles}")
        return 0
    if a.seeds:
        prior = json.load(open(a.prior))
        os.makedirs(a.out, exist_ok=True)
        rng = random.Random(a.seed)
        for i, g in enumerate(seed_genomes(prior, rng, a.intensity, a.seeds)):
            with open(os.path.join(a.out, f"seed_{i + 1:02d}.json"), "w") as f:
                json.dump(g, f, indent=1)
        print(f"wrote {a.seeds} seed genomes to {a.out}")
        return 0
    ap.print_help()
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
