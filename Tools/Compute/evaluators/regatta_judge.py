#!/usr/bin/env python3
"""The Regatta race JUDGE: score a course with the real game's code (Plan step: time-arena-course).

Every seat is flown by that hull's SHIPPED AI (Legendary) in Amoebius, headless and faster than real time, through the
same flow as the editor's RegattaHullBenchmark (Port/src/CosmicShore.Player/RaceJudge.cs, `--race-judge`). Nothing is
modelled: rings, rails, prisms, lifeforms and every pilot are the game's own C#.

    judge(genome, seats, seeds, limit_s=400, backend="engine", field=False) -> RESULT

    RESULT = {"races": [{"seed", "mode": "alone"|"field", "intensity", "outcome", "race_s", "gates_total", "laps_total",
                         "course_seed", "seats": [{"hull", "domain", "finish_s"|None, "gates", "laps", "lap_s": [],
                                                   "gate_s": [], "prism_hits", "dnf_reason"}]}],
              "wall_s", "engine_commit", "backend", "notes": []}

    field=False   every hull races ALONE (one process per hull, run in parallel): a clean per-hull finish time.
    field=True    all seats race together; the race ends when the first pilot finishes, the rest report their gates.
    backend="model"  prices the genome with today's Python model (W3's regatta_judge_model.py), engine_commit "model".

The genome is the shared contract's JSON (regatta-flywheel BRIEFS.md). Until the game has RegattaCourseGenome, the judge
reads its "intensity" (default 1), "seed" (the course seed; default the shipped one) and "features" (RegattaCourseFeatures;
absent = the arena's own, {} = none); anything else is listed in RESULT["notes"] as ignored.

Command line:
    python3 Tools/Compute/evaluators/regatta_judge.py --calibrate [--seeds 1,2,3]   every hull alone on the editor
                                                         benchmark's course, against its 2026-11-03 times
    python3 Tools/Compute/evaluators/regatta_judge.py --genome g.json --hulls Urchin,Manta [--field]
    python3 Tools/Compute/evaluators/regatta_judge.py --self-test

Setup (once per machine): .NET 10 (`apt-get install -y dotnet-sdk-10.0`). The player is built on first use into
REGATTA_JUDGE_BIN (default /tmp/regatta-judge-bin) from this checkout's Port/ and Assets/.
"""
import argparse
import concurrent.futures
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
BIN = os.environ.get("REGATTA_JUDGE_BIN", "/tmp/regatta-judge-bin")
DLL = os.path.join(BIN, "CosmicShore.dll")

# The editor's Regatta Hull Benchmark, 2026-11-03: each AI alone, Legendary, I1, three laps, the course that is
# today's pool entry 0 with no features (regatta_measured_roster.MEASURED_ON, seed 20268834).
EDITOR_I1 = {"Urchin": 51.0, "Manta": 67.0, "Stoat": 70.0, "Squirrel": 75.0, "Rhino": 95.0, "Dolphin": 97.5,
             "Serpent": 100.0, "Sparrow": 133.0}
CALIBRATION_GENOME = {"version": 1, "intensity": 1, "features": {}}
REGATTA_HULLS = ["Squirrel", "Manta", "Rhino", "Urchin", "Dolphin", "Stoat", "Scarab", "Serpent", "Sparrow"]


def _commit():
    try:
        return subprocess.run(["git", "-C", ROOT, "rev-parse", "--short", "HEAD"], capture_output=True, text=True,
                              timeout=10).stdout.strip()
    except Exception:
        return ""


def build(force=False):
    """Build the player once (about 50 s). Returns the dll path."""
    if os.path.exists(DLL) and not force:
        return DLL
    if not shutil.which("dotnet"):
        raise RuntimeError("dotnet not found: apt-get install -y dotnet-sdk-10.0")
    cmd = ["dotnet", "build", os.path.join(ROOT, "Port", "src", "CosmicShore.Player"), "-c", "Release", "-o", BIN]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0 or not os.path.exists(DLL):
        raise RuntimeError("player build failed:\n" + r.stdout[-4000:] + r.stderr[-2000:])
    return DLL


def _run_one(spec, timeout_s, workdir, tag):
    spec_path = os.path.join(workdir, f"{tag}.spec.json")
    out_path = os.path.join(workdir, f"{tag}.result.json")
    with open(spec_path, "w") as f:
        json.dump(spec, f)
    env = dict(os.environ, COSMIC_SHORE_PROJECT=ROOT, COSMIC_SHORE_NET="off", COSMIC_SHORE_AUDIO="off",
               COSMIC_SHORE_COMMIT=_commit(),
               # Each process its own profile, so parallel runs never share a save slot.
               COSMIC_SHORE_PROFILE=f"judge-{os.getpid()}-{tag}")
    cmd = ["dotnet", DLL, "--headless", "--quiet", "--race-judge", spec_path, "--race-out", out_path]
    with open(os.path.join(workdir, f"{tag}.log"), "w") as log:
        try:
            r = subprocess.run(cmd, stdout=log, stderr=subprocess.STDOUT, timeout=timeout_s, env=env)
            code = r.returncode
        except subprocess.TimeoutExpired:
            code = "timeout"
    if os.path.exists(out_path):
        with open(out_path) as f:
            res = json.load(f)
    else:
        res = {"races": [], "notes": []}
    if code != 0:
        res.setdefault("notes", []).append(f"{tag}: player exited {code} (log {workdir}/{tag}.log)")
    return res


def judge(genome, seats, seeds, limit_s=400, backend="engine", field=False, workers=None, workdir=None):
    """Score one course. seats: [{"hull": "Urchin", "ai": "shipped"}] or ["Urchin", ...]."""
    seats = [s if isinstance(s, dict) else {"hull": s, "ai": "shipped"} for s in seats]
    if backend == "model":
        import regatta_judge_model  # W3's module: the Python model priced in the same RESULT shape
        return regatta_judge_model.judge(genome, seats, seeds, limit_s=limit_s)
    if backend != "engine":
        raise ValueError(f"unknown backend {backend!r}")
    build()
    t0 = time.time()
    workdir = workdir or tempfile.mkdtemp(prefix="regatta-judge-")
    base = {"genome": genome, "seeds": list(seeds), "limit_s": limit_s}
    # Each process boots once (~20 s) and races its seeds through Play Again (~5-20 s a race).
    jobs = [("field", dict(base, seats=seats, field=True))] if field else \
        [(f"{i}_{s['hull']}", dict(base, seats=[s], field=False)) for i, s in enumerate(seats)]
    timeout_s = 120 + len(seeds) * (limit_s / 4 + 60)
    workers = workers or max(1, min(len(jobs), os.cpu_count() or 1))
    with concurrent.futures.ThreadPoolExecutor(workers) as ex:
        results = list(ex.map(lambda j: _run_one(j[1], timeout_s, workdir, j[0]), jobs))
    races, notes = [], []
    for res in results:
        races.extend(res.get("races", []))
        notes.extend(res.get("notes", []))
    races.sort(key=lambda r: (r.get("seed", 0), r["seats"][0]["hull"] if r.get("seats") else ""))
    return {"races": races, "wall_s": round(time.time() - t0, 2), "engine_commit": _commit(), "backend": "amoebius",
            "notes": sorted(set(notes)), "workdir": workdir}


def mean_finish(result):
    """{hull: (mean finish_s over finished alone races, finished count, races)}."""
    acc = {}
    for race in result["races"]:
        for s in race.get("seats", []):
            a = acc.setdefault(s["hull"], [[], 0])
            a[1] += 1
            if s.get("finish_s") is not None:
                a[0].append(s["finish_s"])
    return {h: (sum(v[0]) / len(v[0]) if v[0] else None, len(v[0]), v[1]) for h, v in acc.items()}


def calibrate(seeds, hulls=None):
    hulls = hulls or REGATTA_HULLS
    res = judge(CALIBRATION_GENOME, hulls, seeds)
    rows = []
    for h, (m, n, total) in sorted(mean_finish(res).items(), key=lambda kv: (kv[1][0] is None, kv[1][0] or 0)):
        times = [s["finish_s"] for r in res["races"] for s in r["seats"] if s["hull"] == h and s["finish_s"] is not None]
        sd = (sum((t - m) ** 2 for t in times) / (len(times) - 1)) ** 0.5 if len(times) > 1 else 0.0
        ed = EDITOR_I1.get(h)
        rows.append({"hull": h, "port_s": None if m is None else round(m, 1), "sd_s": round(sd, 2),
                     "finished": f"{n}/{total}", "editor_s": ed,
                     "ratio": None if (m is None or ed is None) else round(m / ed, 3)})
    return {"rows": rows, "wall_s": res["wall_s"], "races": sum(len(r["seats"]) for r in res["races"]),
            "cores": os.cpu_count(), "engine_commit": res["engine_commit"], "notes": res["notes"], "result": res}


def self_test():
    """The judge's contract, cheaply: one Urchin race on the calibration course finishes near the editor's 51 s, and a
    negative control (a 10 s limit) reports a DNF instead of a time."""
    res = judge(CALIBRATION_GENOME, ["Urchin"], [1], limit_s=400)
    t = mean_finish(res).get("Urchin", (None,))[0]
    assert t is not None, f"the Urchin did not finish: {res['notes']}"
    assert abs(t / EDITOR_I1["Urchin"] - 1) < 0.15, f"Urchin {t:.1f} s is not within 15% of the editor's 51 s"
    neg = judge(CALIBRATION_GENOME, ["Urchin"], [1], limit_s=10)
    seat = neg["races"][0]["seats"][0]
    assert seat["finish_s"] is None and seat["dnf_reason"] == "timeout", f"negative control finished: {seat}"
    print(f"self-test ok: Urchin {t:.1f} s (editor 51.0), a 10 s limit is a timeout DNF")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--calibrate", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--genome", help="genome JSON file (default: the calibration course)")
    ap.add_argument("--hulls", default=",".join(REGATTA_HULLS))
    ap.add_argument("--seeds", default="1,2,3")
    ap.add_argument("--limit", type=float, default=400)
    ap.add_argument("--field", action="store_true")
    ap.add_argument("--backend", default="engine")
    ap.add_argument("--out", help="write the RESULT JSON here")
    a = ap.parse_args()
    seeds = [int(s) for s in a.seeds.split(",") if s]
    hulls = [h.strip() for h in a.hulls.split(",") if h.strip()]
    if a.self_test:
        self_test()
        return
    if a.calibrate:
        cal = calibrate(seeds, hulls)
        print(f"{'hull':10} {'port s':>8} {'sd':>6} {'fin':>5} {'editor':>7} {'ratio':>6}")
        for r in cal["rows"]:
            print(f"{r['hull']:10} {r['port_s'] if r['port_s'] is not None else 'DNF':>8} {r['sd_s']:>6} "
                  f"{r['finished']:>5} {r['editor_s'] or '-':>7} {r['ratio'] or '-':>6}")
        print(f"{cal['races']} races in {cal['wall_s']} s wall on {cal['cores']} cores, engine {cal['engine_commit']}")
        out = cal
    else:
        genome = json.load(open(a.genome)) if a.genome else CALIBRATION_GENOME
        out = judge(genome, hulls, seeds, limit_s=a.limit, backend=a.backend, field=a.field)
        print(json.dumps({h: v for h, v in mean_finish(out).items()}, indent=1))
    if a.out:
        with open(a.out, "w") as f:
            json.dump(out, f, indent=1)


if __name__ == "__main__":
    main()
