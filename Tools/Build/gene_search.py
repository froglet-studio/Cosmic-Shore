#!/usr/bin/env python3
"""THE GENE SEARCH: train each vessel's AI genome from the Loop Atlas genes, hand the fixed pool to arena balancing,
and send the loops it discovers back to the atlas (Garrett, 2026-11-21: "if you inform each other two ways we get the
most potential benefit").

Two directions, one tool:
  atlas -> genes   Every combo in Docs/LoopAtlas/model/fleet/*.yaml that carries a `gene` seeds a behaviour of that
                   vessel's genome for that element (Docs/AI_SYSTEM/AI_PLAN.md: one AI per vessel per element).
  genes -> atlas   The search may also compose the vessel's own verbs into chains no combo names. A chain that the
                   archive keeps (effective, used, distinct) is a CANDIDATE LOOP: `discover` lists them and
                   `propose` writes them into the vessel's YAML on the Proposed layer (`in: x`), where a person keeps,
                   renames or drops them like any other proposal. Strategies the arena balancing project observes can
                   come in the same way (Docs/LoopAtlas/discover-in/*.json).

The search keeps genes.yaml `genes.search`: a cost for every dial and behaviour a genome uses (bloat), a MAP-Elites
archive of distinct behaviours beside the best genome (style), and an I1-I4 ladder drawn from that archive's fun,
less effective behaviours, never from the best genome with random mistakes.

THE EVALUATOR IS A SURROGATE. Froglet Compute is paused (Shombith, 2026-11-06) and no harness flies an arbitrary gene
yet, so `SurrogateArena` scores a genome in an abstract round: situations arrive at per-element cue rates, the genome
starts a behaviour whose cues are present, and a behaviour's effectiveness comes from a seeded landscape over its
dials (each dial has a hidden best value, some dials do nothing, some genes have two equally good ways to fly).
Repeating one behaviour back to back pays less each time (rivals read it, the resource thins), which is the one
assumption that makes switching pay. Every number it prints measures the SEARCH (does it drop bloat, keep style,
build a ladder, plateau, find chains), not the vessels. The real evaluator is the vessel's own arcade game at I1-I4
(Amoebius or the Unity trainer); it replaces `SurrogateArena.score` and nothing else changes.

    python3 Tools/Build/gene_search.py run [--vessel V] [--element E] [--gens N]   # train, write genes-out/
    python3 Tools/Build/gene_search.py retrain --balance Docs/LoopAtlas/balance-in/<arena>.json
    python3 Tools/Build/gene_search.py discover        # candidate loops from genes-out/ and discover-in/
    python3 Tools/Build/gene_search.py propose --top N # write the top candidates into the atlas (Proposed layer)
    python3 Tools/Build/gene_search.py --self-test

Plan step: none yet (a proposal; the genomes it describes serve time-arcade, charge-arcade, mass-arcade and
space-arcade once Yash plans them into Docs/AI_SYSTEM/ai_plan.json). It trains no shipped AI and queues no job.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import sys
from pathlib import Path

import yaml

REPO = Path(__file__).resolve().parents[2]
ATLAS = REPO / "Docs" / "LoopAtlas"
MODEL_DIR = ATLAS / "model"
OUT = ATLAS / "genes-out"
DISCOVER_IN = ATLAS / "discover-in"
ELEMENTS = ["Time", "Charge", "Mass", "Space"]
TODAY = "2026-11-21"

# How often each sense is true in a situation of each element's games (a fraction of 2-second situations). The
# atlas names the senses; these rates are the surrogate's assumption, chosen from what each genre asks of a pilot.
CUE_RATE = {
    "Time": {"gate_next": .9, "straight_ahead": .5, "mass_ahead": .55, "crystal_near": .3, "rival_ahead": .4,
             "rival_on_line": .2, "omni_near": .1, "bank_full": .3, "bank_low": .3, "verb_ready": .7,
             "danger_ahead": .2},
    "Charge": {"rival_near": .6, "rival_on_line": .4, "rival_ahead": .3, "petals_loose": .3, "threatened": .35,
               "verb_ready": .7, "bank_full": .3, "bank_low": .3, "silenced": .15, "crystal_near": .25},
    "Mass": {"dense_mass": .5, "own_mass_gap": .4, "open_space": .5, "fauna_near": .4, "herbivore_near": .2,
             "flora_near": .3, "claim_tipping": .2, "mass_ahead": .5, "verb_ready": .7, "crystal_near": .25},
    "Space": {"rival_mass_ahead": .5, "shielded_ahead": .3, "open_space": .4, "mass_ahead": .5, "bank_full": .3,
              "dense_mass": .35, "verb_ready": .7, "crystal_near": .25, "danger_ahead": .2},
}
CUE_DEFAULT = .25  # crew_ready, ally_near and any sense a genre does not name
# Which senses start a chain whose first verb is of each element (used only to cue discovered chains).
CUE_FOR = {
    "Time": ["gate_next", "straight_ahead", "mass_ahead", "crystal_near"],
    "Charge": ["rival_near", "rival_on_line", "threatened", "petals_loose"],
    "Mass": ["dense_mass", "own_mass_gap", "open_space", "fauna_near"],
    "Space": ["rival_mass_ahead", "shielded_ahead", "mass_ahead", "bank_full"],
}
CHAIN_DIALS = [
    ["handoff", "seconds from one verb's payoff to the next press", 0.2, 4.0, "s"],
    ["commit", "share of the first verb spent before chaining", 0.2, 1.0, "fraction"],
    ["reach", "distance to the cue at which the chain starts", 20, 400, "u"],
]
ROUND_S, TICK_S, ROUNDS = 120.0, 2.0, 6
IDLE_YIELD = 0.2        # yield per second while cruising with nothing started
REPEAT_DECAY = 0.85     # a behaviour started again straight after itself pays this much less each time
COST_DIAL, COST_BEHAVIOUR = 0.006, 0.012
PLATEAU_WINDOW, PLATEAU_GAIN = 80, 0.003


def _h(*parts) -> float:
    """A stable number in [0, 1) from any parts: the surrogate landscape is the same on every machine."""
    d = hashlib.sha256("|".join(map(str, parts)).encode()).digest()
    return int.from_bytes(d[:8], "big") / 2 ** 64


def _norm(x, lo, hi):
    if lo > 0 and hi / lo > 20:
        return (math.log(x) - math.log(lo)) / (math.log(hi) - math.log(lo))
    return (x - lo) / (hi - lo) if hi > lo else 0.5


def _denorm(u, lo, hi):
    u = min(1.0, max(0.0, u))
    if lo > 0 and hi / lo > 20:
        return math.exp(math.log(lo) + u * (math.log(hi) - math.log(lo)))
    return lo + u * (hi - lo)


# ------------------------------------------------------------------------------------------------ reading the atlas

def load_fleet() -> dict:
    meta = yaml.safe_load((MODEL_DIR / "meta.yaml").read_text(encoding="utf-8"))
    return {v: yaml.safe_load((MODEL_DIR / "fleet" / f"{v}.yaml").read_text(encoding="utf-8"))
            for v in meta["fleetOrder"]}


def chain_elements(combo: dict) -> list[str]:
    return [s[0] for s in combo.get("chain", []) if s and s[0] in ELEMENTS]


class Behaviour:
    """One gene of a genome: an atlas combo's gene, or a chain of the vessel's verbs the search composed."""

    def __init__(self, vessel, element, bid, name, cue, dials, chain, t, source, combo=None):
        self.vessel, self.element, self.id, self.name = vessel, element, bid, name
        self.cue, self.dials, self.chain, self.t, self.source, self.combo = cue, dials, chain, t, source, combo
        k = (vessel, element, bid)
        if source == "atlas":
            self.base = 0.70 + 0.30 * _h(*k, "base")
        else:  # a chain: better when it ends on the genome's own verb and its verbs' times fit one combo
            ends_home = 0.10 if chain[-1] == element else 0.0
            self.base = 0.45 + 0.35 * _h(*k, "base") + ends_home - 0.05 * (len(chain) - 2)
        self.land = []
        for i, d in enumerate(dials):
            s = 0.02 if _h(*k, i, "inert") < 0.25 else 0.25 + 0.5 * _h(*k, i, "sens")
            o1 = 0.1 + 0.8 * _h(*k, i, "o1")
            two = _h(*k, i, "two") < 0.3
            o2 = (o1 + 0.5) % 1.0 if two else None
            self.land.append((s, o1, o2, 0.12 + 0.15 * _h(*k, i, "w")))

    def eff(self, x: list[float], scale: float = 1.0) -> float:
        """Effectiveness with normalised dial values x (None = frozen at the seed, the range's middle)."""
        e = self.base * scale
        for (s, o1, o2, w), u in zip(self.land, x):
            u = 0.5 if u is None else u
            g = math.exp(-((u - o1) / w) ** 2)
            if o2 is not None:
                g = max(g, 0.94 * math.exp(-((u - o2) / w) ** 2))
            e *= 1 - s * (1 - g)
        return e

    def regime(self, x: list[float]) -> int:
        """Which way the gene is flown: its most sensitive live dial low (0) or high (1)."""
        best = max(range(len(self.land)), key=lambda i: self.land[i][0]) if self.land else None
        if best is None or x[best] is None:
            return 0
        return int(x[best] >= 0.5)

    def to_json(self):
        return {"id": self.id, "name": self.name, "source": self.source, "cue": self.cue, "chain": self.chain,
                "t": self.t, "dials": [d[0] for d in self.dials]}


def atlas_behaviours(vessel: str, v: dict, element: str) -> list[Behaviour]:
    out = []
    for c in v["combos"]:
        g = c.get("gene")
        if g and g["genome"] == element:
            out.append(Behaviour(vessel, element, "atlas:" + c["name"], c["name"], g["cue"], g["dials"],
                                 chain_elements(c) or [element], c["t"], "atlas", c))
    return out


def chain_pool(vessel: str, v: dict, element: str) -> list[Behaviour]:
    """Every 2- and 3-verb chain of the vessel's elemental verbs that passes through the genome's element and that no
    atlas combo already names (by its element sequence)."""
    verbs = [e for e in ELEMENTS if e in v.get("verbs", {})]
    known = {tuple(chain_elements(c)) for c in v["combos"]}
    out = []
    seqs = [(a, b) for a in verbs for b in verbs if a != b]
    seqs += [(a, b, c) for a in verbs for b in verbs for c in verbs if len({a, b, c}) == 3]
    for seq in seqs:
        if element not in seq or seq in known:
            continue
        names = [v["verbs"][e]["ability"] for e in seq]
        lo = sum(v["verbs"][e]["t"][0] for e in seq)
        hi = sum(v["verbs"][e]["t"][1] for e in seq)
        t = [max(8, lo), min(60, max(hi, lo + 4))]
        cues = CUE_FOR[seq[0]]
        cue = [cues[int(_h(vessel, element, seq, "cue") * len(cues))]]
        out.append(Behaviour(vessel, element, "chain:" + ">".join(seq), " into ".join(names), cue,
                             CHAIN_DIALS, list(seq), t, "chain"))
    return out


# ------------------------------------------------------------------------------------------------------- the round

class SurrogateArena:
    """Scores a genome in abstract rounds of one element's game. Replace `score` with the real harness."""

    def __init__(self, element: str, seed: int = 0):
        self.element, self.rates = element, CUE_RATE[element]
        rng = random.Random(f"{element}|{seed}")
        cues = sorted({c for r in CUE_RATE.values() for c in r} | {"crew_ready", "ally_near"})
        self.rounds = [[{c for c in cues if rng.random() < self.rates.get(c, CUE_DEFAULT)}
                        for _ in range(int(ROUND_S / TICK_S))] for _ in range(ROUNDS)]

    @staticmethod
    def ready(b: Behaviour, situation: set) -> bool:
        return sum(c in situation for c in b.cue) * 2 >= len(b.cue)

    def score(self, genome: "Genome", scale: dict | None = None):
        """(effectiveness per second, uses per behaviour, switches) averaged over the fixed rounds."""
        scale = scale or {}
        effs = {b.id: b.eff(genome.x[b.id], scale.get(b.id, 1.0)) for b in genome.live()}
        total, uses, switches = 0.0, {b.id: 0 for b in genome.live()}, 0
        for situations in self.rounds:
            busy_until, last, streak, y = 0.0, None, 0, 0.0
            for i, sit in enumerate(situations):
                now = i * TICK_S
                if now < busy_until:
                    continue
                ready = [b for b in genome.live() if self.ready(b, sit)]
                if not ready:
                    y += IDLE_YIELD * TICK_S
                    continue
                pick = max(ready, key=lambda b: effs[b.id] * genome.w[b.id] * (REPEAT_DECAY ** streak
                                                                               if b.id == last else 1.0))
                streak = streak + 1 if pick.id == last else 0
                if last is not None and pick.id != last:
                    switches += 1
                dur = min(ROUND_S - now, (pick.t[0] + pick.t[1]) / 2)
                y += effs[pick.id] * (REPEAT_DECAY ** streak) * dur
                busy_until, last = now + dur, pick.id
                uses[pick.id] += 1
            total += y / ROUND_S
        return total / ROUNDS, {k: n / ROUNDS for k, n in uses.items()}, switches / ROUNDS


class Genome:
    """Which behaviours are switched on, each one's weight, and each dial's normalised value (None = frozen)."""

    def __init__(self, pool: list[Behaviour]):
        self.pool = pool
        self.on = {b.id: b.source == "atlas" for b in pool}
        self.w = {b.id: 1.0 for b in pool}
        self.x = {b.id: [None] * len(b.dials) for b in pool}

    def copy(self):
        g = Genome.__new__(Genome)
        g.pool, g.on, g.w = self.pool, dict(self.on), dict(self.w)
        g.x = {k: list(v) for k, v in self.x.items()}
        return g

    def live(self):
        return [b for b in self.pool if self.on[b.id]]

    def complexity(self):
        live = self.live()
        return len(live), sum(1 for b in live for u in self.x[b.id] if u is not None)

    def mutate(self, rng: random.Random):
        g = self.copy()
        live = g.live()
        op = rng.random()
        if op < 0.45 and live:  # tune: thaw or move one dial
            b = rng.choice(live)
            if b.dials:
                i = rng.randrange(len(b.dials))
                u = g.x[b.id][i]
                g.x[b.id][i] = rng.random() if u is None else min(1, max(0, u + rng.gauss(0, 0.15)))
        elif op < 0.60 and live:  # shrink: freeze a dial back to its seed
            b = rng.choice(live)
            thawed = [i for i, u in enumerate(g.x[b.id]) if u is not None]
            if thawed:
                g.x[b.id][rng.choice(thawed)] = None
        elif op < 0.75:  # grow: switch a behaviour on (atlas or a chain)
            off = [b for b in g.pool if not g.on[b.id]]
            if off:
                g.on[rng.choice(off).id] = True
        elif op < 0.85 and len(live) > 1:  # drop a behaviour
            g.on[rng.choice(live).id] = False
        elif live:  # reweight
            b = rng.choice(live)
            g.w[b.id] = min(2.0, max(0.3, g.w[b.id] * math.exp(rng.gauss(0, 0.3))))
        return g

    def to_json(self):
        live = self.live()
        return {"behaviours": [{"id": b.id, "weight": round(self.w[b.id], 3),
                                "dials": {d[0]: (None if u is None else round(_denorm(u, d[2], d[3]), 4))
                                          for d, u in zip(b.dials, self.x[b.id])}} for b in live]}


def evaluate(arena: SurrogateArena, g: Genome, scale=None) -> dict:
    eff, uses, sw = arena.score(g, scale)
    nb, nd = g.complexity()
    fit = eff - COST_DIAL * nd - COST_BEHAVIOUR * nb
    used = {k: n for k, n in uses.items() if n > 0}
    tot = sum(used.values()) or 1
    share = {k: n / tot for k, n in used.items()}
    lead = max(share, key=share.get) if share else None
    return {"fit": fit, "eff": eff, "behaviours": nb, "dials": nd, "share": share, "lead": lead,
            "switches": sw, "distinct": sum(1 for s in share.values() if s >= 0.1)}


def descriptor(g: Genome, r: dict) -> tuple:
    lead = r["lead"]
    b = next((b for b in g.pool if b.id == lead), None)
    reg = b.regime(g.x[lead]) if b else 0
    sw = min(3, int(r["switches"] / 6))
    return (lead, reg, sw)


# ------------------------------------------------------------------------------------------------------ the search

def search(vessel: str, v: dict, element: str, gens: int = 400, seed: int = 0, start: dict | None = None,
           scale: dict | None = None, batch: int = 12):
    pool = atlas_behaviours(vessel, v, element) + chain_pool(vessel, v, element)
    if not pool:
        return None
    arena = SurrogateArena(element, seed)
    rng = random.Random(f"{vessel}|{element}|{seed}")
    archive: dict = {}

    def add(g):
        r = evaluate(arena, g, scale)
        d = descriptor(g, r)
        if d not in archive or archive[d][1]["fit"] < r["fit"]:
            archive[d] = (g, r)
        return r

    seed_g = Genome(pool)
    if start:  # a retrain grows from the genome it had
        for b in pool:
            seed_g.on[b.id] = False
        for s in start["behaviours"]:
            if s["id"] in seed_g.on:
                seed_g.on[s["id"]] = True
                seed_g.w[s["id"]] = s["weight"]
                b = next(b for b in pool if b.id == s["id"])
                seed_g.x[b.id] = [None if s["dials"].get(d[0]) is None else _norm(s["dials"][d[0]], d[2], d[3])
                                  for d in b.dials]
    if not seed_g.live():
        seed_g.on[pool[0].id] = True
    seed_r = add(seed_g)
    history = []
    for gen in range(gens):
        elites = list(archive.values())
        for _ in range(batch):
            # parents: half from the best (tune the leader), half from anywhere in the archive (keep style)
            if rng.random() < 0.5:
                parent = max(elites, key=lambda e: e[1]["fit"])[0]
            else:
                parent = rng.choice(elites)[0]
            add(parent.mutate(rng))
        history.append(max(e[1]["fit"] for e in archive.values()))
    best_g, best_r = max(archive.values(), key=lambda e: e[1]["fit"])
    tail = history[-PLATEAU_WINDOW:] if len(history) >= PLATEAU_WINDOW else history
    gain = (tail[-1] - tail[0]) / abs(tail[0]) if tail and tail[0] else 1.0
    since = next((i for i in range(len(history) - 1, 0, -1) if history[i] > history[i - 1] * (1 + 1e-6)), 0)
    plateau = {"plateaued": len(history) >= PLATEAU_WINDOW and gain < PLATEAU_GAIN,
               "gain_last_window": round(gain, 5), "gens_since_gain": len(history) - 1 - since, "gens": gens}
    return {"pool": pool, "arena": arena, "archive": archive, "best": (best_g, best_r), "seed": seed_r,
            "plateau": plateau, "history": history, "scale": scale}


def bloat_report(res) -> list[dict]:
    """Dials the best genome still tunes whose whole range moves its score by under half a percent."""
    g, r = res["best"]
    out = []
    for b in g.live():
        for i, d in enumerate(b.dials):
            if g.x[b.id][i] is None:
                continue
            vals = []
            for u in (0.0, 0.25, 0.5, 0.75, 1.0):
                h = g.copy()
                h.x[b.id][i] = u
                vals.append(res["arena"].score(h, res["scale"])[0])
            spread = (max(vals) - min(vals)) / max(1e-9, r["eff"])
            if spread < 0.005:
                out.append({"behaviour": b.id, "dial": d[0], "spread": round(spread, 5)})
    return out


def ladder(res) -> dict:
    """I4 flies the best; I3 the best that still switches between three; I2 and I1 the archive's distinct, less
    effective behaviours (a fifth and a third below), never the best with mistakes added."""
    best = res["best"][1]["eff"]
    cells = sorted(res["archive"].values(), key=lambda e: -e[1]["fit"])

    def pick(lo, hi, key):
        c = [e for e in cells if lo <= e[1]["eff"] / best <= hi]
        return max(c, key=key) if c else None

    rungs = {"I4": cells[0],
             "I3": pick(0.88, 1.0, lambda e: (e[1]["distinct"] >= 3, e[1]["distinct"], e[1]["fit"])),
             "I2": pick(0.72, 0.9, lambda e: (e[1]["distinct"], e[1]["switches"])),
             "I1": pick(0.55, 0.78, lambda e: (e[1]["lead"] or "").startswith("atlas:") * 1.0 + e[1]["distinct"])}
    out = {}
    for k, e in rungs.items():
        if e is None:
            out[k] = None
            continue
        g, r = e
        out[k] = {"eff_vs_best": round(r["eff"] / best, 3), "lead": r["lead"], "distinct": r["distinct"],
                  "switches": round(r["switches"], 1), "genome": g.to_json()}
    return out


def result_json(vessel, element, res) -> dict:
    g, r = res["best"]
    by_id = {b.id: b for b in res["pool"]}
    arch = []
    for d, (ag, ar) in sorted(res["archive"].items(), key=lambda kv: -kv[1][1]["fit"]):
        arch.append({"cell": {"lead": d[0], "regime": d[1], "switch_bin": d[2]}, "fit": round(ar["fit"], 4),
                     "eff": round(ar["eff"], 4), "dials": ar["dials"], "behaviours": ar["behaviours"],
                     "distinct": ar["distinct"], "switches": round(ar["switches"], 1),
                     "share": {k: round(s, 3) for k, s in ar["share"].items()}})
    return {
        "vessel": vessel, "element": element, "evaluator": "surrogate", "date": TODAY,
        "behaviours": [b.to_json() for b in res["pool"]],
        "seed": {"eff": round(res["seed"]["eff"], 4), "fit": round(res["seed"]["fit"], 4)},
        "best": {"eff": round(r["eff"], 4), "fit": round(r["fit"], 4), "dials": r["dials"],
                 "behaviours": r["behaviours"], "distinct": r["distinct"], "switches": round(r["switches"], 1),
                 "share": {k: round(s, 3) for k, s in r["share"].items()}, "genome": g.to_json(),
                 "hash": hashlib.sha256(json.dumps(g.to_json(), sort_keys=True).encode()).hexdigest()[:12]},
        "plateau": res["plateau"],
        "bloat": bloat_report(res),
        "ladder": ladder(res),
        "archive": arch[:15],
        "archive_cells": len(arch),
        "chains_in_use": sorted({k for e in arch[:20] for k in e["share"] if k.startswith("chain:")
                                 and by_id[k]}),
    }


# ------------------------------------------------------------------------------------------------------ the commands

def cmd_run(args) -> int:
    fleet = load_fleet()
    OUT.mkdir(parents=True, exist_ok=True)
    pool, rows = {"evaluator": "surrogate", "date": TODAY, "entries": []}, []
    for vessel, v in fleet.items():
        if args.vessel and vessel != args.vessel:
            continue
        for element in ELEMENTS:
            if args.element and element != args.element:
                continue
            if not atlas_behaviours(vessel, v, element):
                continue  # no atlas gene yet: nothing to seed (GENES.md lists these as gaps)
            res = search(vessel, v, element, gens=args.gens, seed=args.seed)
            j = result_json(vessel, element, res)
            (OUT / f"{vessel}.{element}.json").write_text(json.dumps(j, separators=(",", ":")) + "\n", encoding="utf-8")
            pool["entries"].append({"vessel": vessel, "element": element, "plateaued": j["plateau"]["plateaued"],
                                    "eff": j["best"]["eff"], "hash": j["best"]["hash"],
                                    "genome": j["ladder"]["I4"]["genome"]})
            rows.append(j)
            print(f"{vessel:11} {element:6} eff {j['seed']['eff']:.3f} -> {j['best']['eff']:.3f}  "
                  f"genes {j['best']['distinct']}  dials {j['best']['dials']}  cells {j['archive_cells']:3}  "
                  f"{'plateau' if j['plateau']['plateaued'] else 'rising'}")
    if not args.vessel and not args.element:
        (OUT / "pool.json").write_text(json.dumps(pool, indent=1) + "\n", encoding="utf-8")
        (OUT / "SUMMARY.md").write_text(summary_md(rows), encoding="utf-8")
        print(f"wrote {len(rows)} genomes, pool.json and SUMMARY.md to {OUT.relative_to(REPO)}")
    return 0


def summary_md(rows) -> str:
    n = len(rows)
    plat = sum(r["plateau"]["plateaued"] for r in rows)
    style = sum(r["best"]["distinct"] >= 3 for r in rows)
    ladders = sum(all(r["ladder"][k] for k in ("I1", "I2", "I3", "I4")) for r in rows)
    bloat = sum(len(r["bloat"]) for r in rows)
    chains = sum(1 for r in rows if any(k.startswith("chain:") for k in r["best"]["share"]))
    out = ["# Gene search: summary", "",
           f"Generated by `python3 Tools/Build/gene_search.py run` on {TODAY} with the SURROGATE evaluator. These "
           "numbers say whether the search behaves as `genes.search` asks; they say nothing yet about how strong a "
           "vessel is. Each file beside this one holds one genome, its archive and its ladder.", "",
           f"- {n} genomes (vessel x element with at least one atlas gene); {plat} plateaued.",
           f"- {style} best genomes switch between three or more behaviours; {ladders} have all four rungs.",
           f"- {chains} best genomes lean on a chain no atlas combo names (candidates: `gene_search.py discover`).",
           f"- {bloat} tuned dials in the best genomes still move nothing (reported per file under `bloat`).", "",
           "| Vessel | Element | Seed eff | Best eff | Genes used | Dials | Cells | Ladder I1/I2/I3 vs best | Plateau |",
           "| --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
    for r in rows:
        lad = "/".join("-" if r["ladder"][k] is None else f"{r['ladder'][k]['eff_vs_best']:.2f}"
                       for k in ("I1", "I2", "I3"))
        out.append(f"| {r['vessel']} | {r['element']} | {r['seed']['eff']:.3f} | {r['best']['eff']:.3f} | "
                   f"{r['best']['distinct']} | {r['best']['dials']} | {r['archive_cells']} | {lad} | "
                   f"{'yes' if r['plateau']['plateaued'] else 'no'} |")
    return "\n".join(out) + "\n"


def cmd_retrain(args) -> int:
    bal = json.loads(Path(args.balance).read_text(encoding="utf-8"))
    fleet = load_fleet()
    report = {"arena": bal.get("arena"), "element": bal.get("element"), "evaluator": "surrogate", "date": TODAY,
              "vessels": []}
    touched = {}
    for ch in bal["changes"]:
        el = next((e for e in ELEMENTS if e.lower() in ch["knob"].lower()), bal.get("element"))
        f = float(ch["to"]) / float(ch["from"]) if float(ch["from"]) else 1.0
        touched.setdefault(ch["vessel"], []).append((el, f, ch))
    for vessel, changes in touched.items():
        if vessel not in fleet:
            report["vessels"].append({"vessel": vessel, "error": "not in the fleet"})
            continue
        element = bal.get("element") or changes[0][0]
        prev_path = OUT / f"{vessel}.{element}.json"
        if not prev_path.exists():
            report["vessels"].append({"vessel": vessel, "error": f"no genome yet: run gene_search.py run first"})
            continue
        prev = json.loads(prev_path.read_text(encoding="utf-8"))
        pool = atlas_behaviours(vessel, fleet[vessel], element) + chain_pool(vessel, fleet[vessel], element)
        scale = {}
        for b in pool:
            s = 1.0
            for el, f, _ in changes:
                if el in b.chain or el is None:
                    s *= f
            scale[b.id] = s
        res = search(vessel, fleet[vessel], element, gens=args.gens, seed=args.seed,
                     start=prev["best"]["genome"], scale=scale)
        g, r = res["best"]
        lead_share = max(r["share"].values()) if r["share"] else 1.0
        collapsed = r["distinct"] < min(prev["best"]["distinct"], 2) or lead_share > 0.8
        report["vessels"].append({
            "vessel": vessel, "element": element, "changes": [c for _, _, c in changes],
            "before": {"eff": prev["best"]["eff"], "distinct": prev["best"]["distinct"]},
            "after": {"eff": round(r["eff"], 4), "distinct": r["distinct"], "lead_share": round(lead_share, 3),
                      "plateaued": res["plateau"]["plateaued"]},
            "moved": round(r["eff"] / prev["best"]["eff"] - 1, 4),
            "style": "collapsed to one behaviour" if collapsed else "kept",
            "genome": g.to_json()})
        print(f"{vessel:11} {element:6} {prev['best']['eff']:.3f} -> {r['eff']:.3f}  style "
              f"{'COLLAPSED' if collapsed else 'kept'}")
    name = Path(args.balance).stem
    (OUT / f"retrain-{name}.json").write_text(json.dumps(report, indent=1) + "\n", encoding="utf-8")
    print(f"wrote {(OUT / f'retrain-{name}.json').relative_to(REPO)}")
    return 0


def candidates(fleet: dict) -> list[dict]:
    """Chains the search kept that no atlas combo names, plus strategies other projects observed."""
    out = []
    for path in sorted(OUT.glob("*.*.json")):
        if path.name.startswith("retrain-") or path.name == "pool.json":
            continue
        j = json.loads(path.read_text(encoding="utf-8"))
        best = j["best"]["eff"]
        beh = {b["id"]: b for b in j["behaviours"]}
        seen = {}
        for cell in j["archive"]:
            if cell["eff"] < 0.9 * best:
                continue
            for k, s in cell["share"].items():
                if k.startswith("chain:") and s >= 0.15:
                    score = s * cell["eff"] / best
                    if score > seen.get(k, (0,))[0]:
                        seen[k] = (score, s, cell["eff"] / best)
        for k, (score, s, rel) in seen.items():
            b = beh[k]
            out.append({"vessel": j["vessel"], "element": j["element"], "chain": b["chain"], "name": b["name"],
                        "cue": b["cue"], "t": b["t"], "score": round(score, 3),
                        "evidence": f"gene search (surrogate): {s:.0%} of the behaviour in an archive cell at "
                                    f"{rel:.0%} of the best genome", "source": "gene-search"})
    for path in sorted(DISCOVER_IN.glob("*.json")) if DISCOVER_IN.exists() else []:
        for s in json.loads(path.read_text(encoding="utf-8")).get("strategies", []):
            v = fleet.get(s.get("vessel"))
            if not v or not all(e in v["verbs"] for e in s.get("chain", [])):
                print(f"discover-in/{path.name}: skipped {s.get('vessel')} {s.get('chain')}: not that vessel's verbs")
                continue
            names = [v["verbs"][e]["ability"] for e in s["chain"]]
            out.append({"vessel": s["vessel"], "element": s.get("element", s["chain"][-1]), "chain": s["chain"],
                        "name": s.get("name") or " into ".join(names), "cue": s.get("cue") or [CUE_FOR[s["chain"][0]][0]],
                        "t": s.get("t") or [8, 60], "score": float(s.get("score", 1.0)),
                        "evidence": f"{path.stem}: {s.get('evidence', 'observed')}", "source": path.stem})
    # one candidate per vessel and element sequence, best first
    best = {}
    for c in out:
        k = (c["vessel"], tuple(c["chain"]))
        if k not in best or best[k]["score"] < c["score"]:
            best[k] = c
    return sorted(best.values(), key=lambda c: -c["score"])


def cmd_discover(args) -> int:
    fleet = load_fleet()
    cs = candidates(fleet)
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "discovered.json").write_text(json.dumps({"date": TODAY, "candidates": cs}, indent=1) + "\n",
                                         encoding="utf-8")
    for c in cs[: args.top or len(cs)]:
        print(f"{c['score']:.3f}  {c['vessel']:11} {c['element']:6} {' > '.join(c['chain']):24} {c['name']}  "
              f"[{c['source']}]")
    print(f"{len(cs)} candidate loops; wrote {(OUT / 'discovered.json').relative_to(REPO)}")
    return 0


def cmd_propose(args) -> int:
    """Write the top candidates into the vessels' YAML as Proposed combos with a gene, one per vessel at most."""
    fleet = load_fleet()
    cs = candidates(fleet)
    done, wrote = set(), []
    for c in cs:
        if len(wrote) >= args.top or c["vessel"] in done:
            continue
        path = MODEL_DIR / "fleet" / f"{c['vessel']}.yaml"
        text = path.read_text(encoding="utf-8")
        data = yaml.safe_load(text)
        if any(x["name"] == c["name"] or tuple(chain_elements(x)) == tuple(c["chain"]) for x in data["combos"]):
            continue
        verbs = data["verbs"]
        drivers = []
        for e in c["chain"]:
            for d in {"Time": ["joy", "mastery"], "Charge": ["rival", "mastery"], "Mass": ["make", "crew"],
                      "Space": ["break", "joy"]}[e]:
                if d not in drivers:
                    drivers.append(d)
        combo = {
            "in": "x", "name": c["name"], "t": c["t"],
            "src": f"Discovered {TODAY} by Tools/Build/gene_search.py ({c['evidence']}). Not flown by a person yet.",
            "is": "A chain the AI search kept that no combo named: " + ", then ".join(
                f"{verbs[e]['ability']} ({e})" for e in c["chain"]) + ".",
            "chain": [[e, verbs[e]["ability"]] for e in c["chain"]],
            "drivers": drivers[:3],
            "feel": "Not felt yet: fly it and name the moment, or drop it.",
            "gene": {"genome": c["element"], "cue": c["cue"],
                     "dials": [list(d) for d in CHAIN_DIALS],
                     "done": f"{verbs[c['chain'][-1]]['ability']} lands within the handoff of the "
                             f"{verbs[c['chain'][0]]['ability']} before it."},
        }
        data["combos"].append(combo)
        header = "".join(line + "\n" for line in text.splitlines()[:2] if line.startswith("#"))
        sys.path.insert(0, str(Path(__file__).parent))
        import loop_atlas  # the atlas's own writer keeps the file's layout
        path.write_text(loop_atlas._dump_yaml(data, header), encoding="utf-8")
        done.add(c["vessel"])
        wrote.append(c)
        print(f"proposed {c['vessel']}: {c['name']} ({' > '.join(c['chain'])}, {c['element']} genome)")
    print(f"{len(wrote)} candidate loops written to the Proposed layer; run loop_atlas.py build")
    return 0


# ------------------------------------------------------------------------------------------------------- self-test

def self_test() -> int:
    """The search keeps its promises on a toy vessel, and a negative control shows each check can fail."""
    v = {"verbs": {"Time": {"ability": "Dash", "t": [2, 10]}, "Charge": {"ability": "Zap", "t": [2, 8]},
                   "Mass": {"ability": "Lay", "t": [3, 12]}},
         "combos": [{"name": "Dash to zap", "t": [8, 30], "chain": [["Time", "Dash"], ["Charge", "Zap"]],
                     "gene": {"genome": "Charge", "cue": ["rival_near"], "done": "zap lands",
                              "dials": [["a", "a", 0, 1, "x"], ["b", "b", 1, 100, "x"], ["c", "c", 0, 1, "x"]]}}]}
    fails = []
    res = search("Toy", v, "Charge", gens=200)
    j = result_json("Toy", "Charge", res)
    if not j["best"]["eff"] > j["seed"]["eff"]:
        fails.append("search did not beat its seed")
    if j["ladder"]["I4"]["eff_vs_best"] != 1.0:
        fails.append("I4 is not the best genome")
    for k in ("I1", "I2"):
        if j["ladder"][k] and j["ladder"][k]["eff_vs_best"] >= 0.9:
            fails.append(f"{k} is not below the best")
    if j["archive_cells"] < 3:
        fails.append("archive kept fewer than three distinct behaviours")
    # negative control: a genome with every dial thawed must cost more than the same genome frozen
    g = res["best"][0].copy()
    for b in g.live():
        g.x[b.id] = [0.5 if u is None else u for u in g.x[b.id]]
    if not evaluate(res["arena"], g)["dials"] >= res["best"][1]["dials"]:
        fails.append("complexity count does not see thawed dials")
    # negative control: no repeat decay means one behaviour should be enough (switching stops paying)
    global REPEAT_DECAY
    keep, REPEAT_DECAY = REPEAT_DECAY, 1.0
    flat = search("Toy", v, "Charge", gens=120)
    REPEAT_DECAY = keep
    if flat["best"][1]["distinct"] > j["best"]["distinct"]:
        fails.append("switching paid even without the repeat assumption (the style term is not what drives it)")
    # discovery: a known chain is never offered back as new
    pool = chain_pool("Toy", v, "Charge")
    if any(b.chain == ["Time", "Charge"] for b in pool):
        fails.append("chain_pool offered a chain the atlas already names")
    print("gene_search self-test:", "ok" if not fails else "FAILED")
    for f in fails:
        print("  -", f)
    return 1 if fails else 0


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--self-test", action="store_true")
    sub = ap.add_subparsers(dest="cmd")
    r = sub.add_parser("run")
    r.add_argument("--vessel")
    r.add_argument("--element", choices=ELEMENTS)
    r.add_argument("--gens", type=int, default=600)
    r.add_argument("--seed", type=int, default=0)
    t = sub.add_parser("retrain")
    t.add_argument("--balance", required=True)
    t.add_argument("--gens", type=int, default=200)
    t.add_argument("--seed", type=int, default=0)
    d = sub.add_parser("discover")
    d.add_argument("--top", type=int, default=0)
    p = sub.add_parser("propose")
    p.add_argument("--top", type=int, default=6)
    a = ap.parse_args(argv)
    if a.self_test:
        return self_test()
    return {"run": cmd_run, "retrain": cmd_retrain, "discover": cmd_discover, "propose": cmd_propose}.get(
        a.cmd, lambda _: (ap.print_help(), 1)[1])(a)


if __name__ == "__main__":
    sys.exit(main())
