"""Search a hull's Mass-Space AI in its OWN Mass and Space games (never Brood Rush or another arena game).

Plan step: mass-arcade, space-arcade (Docs/AI_SYSTEM/ai_plan.json). Score: each seat's volume books (VC + VD, the
Brood Rush rule) per second of the match, hull against itself, as the ratio to the shipped AI on the same seeds, then
the geometric mean over the hull's games (and intensities). The judge is brood_judge.judge(mode=<game>).

Genome per hull (HULLS below):
  - one gene triple per ability button: on, Duration, Cooldown (the AIPilot ability cycler, AIPilot.abilities);
  - aggr: a multiplier on defaultAggressivenessHigh/Low (how hard it turns onto its target);
  - throttle: a multiplier on defaultThrottleHigh/Low (capped at 1);
  - the Urchin has its own rail autopilot (UrchinAutopilotConfigSO): its numbers are the genome instead.

usage:
  massspace_ai_search.py run --hull Scarab --out DIR [--gens 4 --pop 6 --seeds 1,2 --intensities 2 --workers 3]
  massspace_ai_search.py confirm --hull Scarab --out DIR --seeds 101,102 --intensities 1,2,3,4
Resumes from DIR/evals.jsonl. Nothing is written to the project: every candidate is an in-memory override.
"""
import argparse, concurrent.futures, json, math, os, random, statistics, sys, tempfile, threading, time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import brood_judge as BJ  # noqa: E402

VA = "Assets/_SO_Assets/VesselActions/"
P = "Assets/_Prefabs/Spacevessels/"
# hull: (prefab, AIPilot fileID, games, buttons {name: [action assets pressed together]}, shipped ability list)
HULLS = {
    "Meerkat": (P + "Meerkat.prefab", 3440187540988767646, ["Warren", "CaveIn"],
                {"form": [VA + "Meerkat/MeerkatSolitonFormAction.asset"],
                 "shorten": [VA + "Meerkat/MeerkatSolitonShortenAction.asset"],
                 "hold": [VA + "Stoat/StoatHoldAction.asset"],
                 "sentinel": [VA + "Meerkat/MeerkatSentinelModeAction.asset"]}, []),
    "Wrasse": (P + "Wrasse.prefab", 333211638083239348, ["Reefbuild", "Blastline"],
               {"crew": [VA + "Common Vessel Action/CrewTriggerRT.asset"]}, []),
    "Honeyguide": (P + "Honeyguide.prefab", 333211638083239348, ["Honeycomb", "Woodpecker"],
                   {"rt": [VA + "Honeyguide/HoneyguideRTAction.asset"],
                    "lt": [VA + "Honeyguide/HoneyguideLTAction.asset"]}, []),
    "Stoat": (P + "Stoat.prefab", 3440187540988767646, ["Fulmen"],
              {"slingL": [VA + "Stoat/StoatSlingLeftAction.asset"],
               "slingR": [VA + "Stoat/StoatSlingRightAction.asset"],
               "hold": [VA + "Stoat/StoatHoldAction.asset"]}, []),
    "Sparrow": (P + "Sparrow.prefab", 333211638083239348, ["Salvo", "WildlifeLiberation"],
                {"skyburst": [VA + "Sparrow/SkyBurstGunAction.asset"],
                 "fullauto": [VA + "Sparrow/FullAutoAction.asset"],
                 "boost": [VA + "Common Vessel Action/BoostAction.asset"],
                 "modefire": [VA + "Sparrow/ModeSwitchingFire.asset"],
                 "stationary": [VA + "Sparrow/ToggleStationaryModeAction.asset"]},
                {"skyburst": (2, 5), "fullauto": (3, 0.8), "boost": (2, 10), "modefire": (2, 10)}),
    "Scarab": (P + "Scarab.prefab", 333211638083239348, ["Tollway", "WreckingBall"],
               {"drift": [VA + "Scarab/ScarabDriftAction.asset", VA + "Dolphin/DriftTrailAction.asset"],
                "sharp": [VA + "Scarab/ScarabSharpDriftAction.asset"],
                "switch": [VA + "Scarab/PlaceSwitchAction.asset"]}, []),
    "Urchin": (P + "Urchin.prefab", 3440187540988767646, ["Hijack"], {}, []),
}
URCHIN_CFG = "Assets/Resources/UrchinAutopilotConfig.asset"
URCHIN_GENES = {  # name: (shipped, lo, hi, log-scale)
    "RailLeadDistance": (260, 80, 800, True), "ScanArc": (1600, 400, 4000, True),
    "CaptureFraction": (0.8, 0.4, 0.98, False), "ClosingFraction": (0.7, 0.3, 0.95, False),
    "MaxDryCrawlSeconds": (4, 0.5, 12, True), "ParkedSeconds": (5, 0.5, 15, True),
    "MinSlipIntervalSeconds": (1.5, 0.3, 8, True), "SpikeIntervalSeconds": (1.5, 0.2, 8, True),
    "SpikeMinAmmo": (0.2, 0.0, 0.9, False), "TrackAimDegrees": (8, 2, 30, True),
    "TrackMinRangeInLengths": (3, 0.5, 10, True), "TrackAxisAlignment": (0.8, 0.3, 0.98, False),
}
# AIPilot as shipped: (defaultThrottleHigh, defaultThrottleLow, defaultAggressivenessHigh, defaultAggressivenessLow).
# The skill (intensity x 0.25 on an arcade card) lerps Low -> High, so the genome SCALES both and keeps the ladder.
STEER = {"Urchin": (0.8, 0.8, 0.035, 0.035), "Meerkat": (0.9, 0.2, 0.09, 0.04), "Stoat": (0.9, 0.2, 0.09, 0.04)}
STEER_DEFAULT = (0.9, 0.2, 0.03, 0.04)


def shipped_genome(hull):
    if hull == "Urchin":
        return {k: v[0] for k, v in URCHIN_GENES.items()} | {"aggr": 1.0, "throttle": 1.0}
    _, _, _, buttons, shipped = HULLS[hull]
    shipped = shipped or {}
    g = {"aggr": 1.0, "throttle": 1.0}
    for b in buttons:
        d, c = shipped.get(b, (2.0, 5.0))
        g[b] = {"on": b in shipped, "dur": d, "cd": c}
    return g


def overrides(hull, g):
    prefab, comp, _, buttons, _ = HULLS[hull]
    tH, tL, aH, aL = STEER.get(hull, STEER_DEFAULT)
    fields = {"defaultAggressivenessHigh": round(aH * g["aggr"], 5), "defaultAggressivenessLow": round(aL * g["aggr"], 5),
              "defaultThrottleHigh": round(min(1.0, tH * g["throttle"]), 4), "defaultThrottleLow": round(min(1.0, tL * g["throttle"]), 4)}
    ov = []
    if hull == "Urchin":
        ov.append({"asset": URCHIN_CFG, "fields": {k: float(g[k]) for k in URCHIN_GENES}})
    else:
        abil = []
        for b, assets in buttons.items():
            if g[b]["on"]:
                for path in assets:
                    abil.append({"Ability": {"asset": path}, "Duration": round(g[b]["dur"], 3), "Cooldown": round(g[b]["cd"], 3)})
        fields["abilities"] = abil
    ov.append({"asset": prefab, "component": comp, "fields": fields})
    return ov


def _lu(x, lo, hi, rng, sigma, log):
    if log:
        v = math.exp(math.log(x) + rng.gauss(0, sigma * math.log(hi / lo) / 2))
    else:
        v = x + rng.gauss(0, sigma * (hi - lo) / 2)
    return min(hi, max(lo, v))


def mutate(hull, g, rng, sigma=0.35):
    g = json.loads(json.dumps(g))
    g["aggr"] = _lu(g["aggr"], 0.4, 4.0, rng, sigma, True)
    g["throttle"] = _lu(g["throttle"], 0.6, 1.25, rng, sigma, False)
    if hull == "Urchin":
        for k, (_, lo, hi, log) in URCHIN_GENES.items():
            if rng.random() < 0.6:
                g[k] = _lu(g[k], lo, hi, rng, sigma, log)
        return g
    for b in HULLS[hull][3]:
        if rng.random() < 0.25:
            g[b]["on"] = not g[b]["on"]
        g[b]["dur"] = _lu(g[b]["dur"], 0.15, 6.0, rng, sigma, True)
        g[b]["cd"] = _lu(g[b]["cd"], 0.2, 20.0, rng, sigma, True)
    return g


def random_genome(hull, rng):
    g = shipped_genome(hull)
    g = mutate(hull, g, rng, sigma=1.2)
    if hull != "Urchin":
        for b in HULLS[hull][3]:
            g[b]["on"] = rng.random() < 0.7
    return g


_lock = threading.Lock()


def evaluate(hull, g, seeds, intensities, workdir, limit_s=200, workers=1):
    """{"game@intensity": {"rates": per-seat (VC+VD)/s, "presses": per-seat press counts}} for genome g."""
    ov = overrides(hull, g)

    def one(game, inten):
        res = BJ.judge([[[hull], [hull]]], seeds_per_roster=len(seeds), seed0=seeds[0], limit_s=limit_s, mode=game,
                       intensity=inten, workers=1, overrides=ov, workdir=tempfile.mkdtemp(dir=workdir))
        rates, presses = [], []
        for m in res["matches"]:
            if not BJ.good(m):
                continue
            t = max(10.0, m.get("race_s") or limit_s)
            for s in m["seats"]:
                rates.append(BJ.score(s) / t)
                presses.append(sum((s.get("presses") or {}).values()))
        return f"{game}@{inten}", {"rates": [round(r, 2) for r in rates], "presses": presses}

    jobs = [(game, inten) for game in HULLS[hull][2] for inten in intensities]
    with concurrent.futures.ThreadPoolExecutor(max(1, workers)) as ex:
        return dict(ex.map(lambda j: one(*j), jobs))


def loss(ev, base):
    """Negative log of the geometric mean ratio to the shipped AI (lower is better). Missing rates cost a lot."""
    logs = []
    for k, b in base.items():
        bm = statistics.mean(b["rates"]) if b["rates"] else 1.0
        r = ev.get(k, {}).get("rates") or []
        cm = statistics.mean(r) if r else 0.0
        logs.append(math.log(max(cm, 1e-3) / max(bm, 1e-3)))
    return -statistics.mean(logs)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["run", "confirm", "show"])
    ap.add_argument("--hull", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--gens", type=int, default=4)
    ap.add_argument("--pop", type=int, default=6)
    ap.add_argument("--seeds", default="1,2")
    ap.add_argument("--intensities", default="2")
    ap.add_argument("--workers", type=int, default=3)
    ap.add_argument("--genome", help="confirm: a genome json file (default: best of the run)")
    ap.add_argument("--rng", type=int, default=7)
    a = ap.parse_args()
    seeds = [int(x) for x in a.seeds.split(",")]
    ints = [int(x) for x in a.intensities.split(",")]
    os.makedirs(a.out, exist_ok=True)
    work = os.path.join(a.out, "work")
    os.makedirs(work, exist_ok=True)
    log = os.path.join(a.out, "evals.jsonl" if a.cmd == "run" else "confirm.jsonl")
    done = [json.loads(l) for l in open(log)] if os.path.exists(log) else []

    def rec(tag, g, ev, base):
        r = {"tag": tag, "genome": g, "eval": ev, "loss": None if base is None else round(loss(ev, base), 4), "t": time.time()}
        with _lock, open(log, "a") as f:
            f.write(json.dumps(r) + "\n")
        done.append(r)
        print(tag, r["loss"], {k: round(statistics.mean(v["rates"]), 1) if v["rates"] else None for k, v in ev.items()},
              flush=True)
        return r

    BJ.rj.build()
    if a.cmd == "show":
        for r in sorted(done, key=lambda r: r["loss"] if r["loss"] is not None else 9)[:5]:
            print(r["tag"], r["loss"], json.dumps(r["genome"]))
        return
    if a.cmd == "confirm":
        g = json.load(open(a.genome)) if a.genome else min(
            (json.loads(l) for l in open(os.path.join(a.out, "evals.jsonl")) if json.loads(l)["loss"] is not None),
            key=lambda r: r["loss"])["genome"]
        base = evaluate(a.hull, shipped_genome(a.hull), seeds, ints, work, workers=a.workers)
        cand = evaluate(a.hull, g, seeds, ints, work, workers=a.workers)
        rec("confirm-shipped", shipped_genome(a.hull), base, base)
        rec("confirm-best", g, cand, base)
        return
    rng = random.Random(a.rng)
    base_r = next((r for r in done if r["tag"] == "shipped"), None)
    base = base_r["eval"] if base_r else rec("shipped", shipped_genome(a.hull),
                                             evaluate(a.hull, shipped_genome(a.hull), seeds, ints, work), None)["eval"]
    for r in done:
        if r["loss"] is None:
            r["loss"] = round(loss(r["eval"], base), 4)
    for gen in range(a.gens):
        tagp = f"g{gen}-"
        have = [r for r in done if r["tag"].startswith(tagp)]
        scored = sorted((r for r in done if r["loss"] is not None), key=lambda r: r["loss"])
        elite = [r["genome"] for r in scored[:3]] or [shipped_genome(a.hull)]
        cands = []
        for i in range(len(have), a.pop):
            if gen == 0:
                cands.append((f"{tagp}{i}", random_genome(a.hull, rng) if i % 2 == 0 else mutate(a.hull, elite[0], rng)))
            else:
                parent = elite[min(len(elite) - 1, int(rng.random() ** 2 * len(elite)))]
                cands.append((f"{tagp}{i}", mutate(a.hull, parent, rng, sigma=0.35 / (1 + 0.3 * gen))))
        with concurrent.futures.ThreadPoolExecutor(a.workers) as ex:
            futs = {ex.submit(evaluate, a.hull, g, seeds, ints, work): (t, g) for t, g in cands}
            for f in concurrent.futures.as_completed(futs):
                t, g = futs[f]
                try:
                    rec(t, g, f.result(), base)
                except Exception as e:  # noqa: BLE001 - a crashed judge is a bad candidate, not a dead search
                    print(t, "failed", e, flush=True)
    best = min((r for r in done if r["loss"] is not None), key=lambda r: r["loss"])
    json.dump(best["genome"], open(os.path.join(a.out, "best.json"), "w"), indent=1)
    print("best", best["tag"], best["loss"])


if __name__ == "__main__":
    main()
