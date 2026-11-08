#!/usr/bin/env python3
"""
diamond_nca.py -- A4 "Capacity search via the diamond optimizer" (NCA Ecology Lab, Flora prong).

The lab's jobs.py listed `diamond_nca` as not implemented yet (the runner parks it). This is that job: the diamond
optimizer (pareto_complexity.DiamondController, unchanged) searching the 3D NCA's capacity, where

    genome      = (channels, hidden)
    complexity  = the model's parameter count (Config "complexity params", the only one so far)
    score       = a short nca3d_trainer run's stability_iou (or best_iou: Config "score best_iou")

so it grows the network until the score stops improving, then compresses it back while the leaky floor holds the
score: the smallest NCA that still grows the target.

Built to be a Froglet Compute COMMAND job that runs for hours in pieces:

  * every evaluation is cached in evals.json, keyed by genome. On --resume the controller is re-run from the start
    with the same seed: the cached evaluations answer at once and the rng draws the same children, so it reaches
    exactly where it stopped and carries on. evals.json is a few KB, so it relays to another computer through the
    results branch; at worst the one evaluation that was interrupted is redone there.
  * an evaluation in progress is itself a nca3d_trainer run with its own checkpoint (evals/c<C>_h<H>/), so on the
    SAME computer an interrupted evaluation resumes mid-training.
  * status.json: {step: generations done, steps: generations, phase, regime, best_iou, loss}; SIGINT/SIGTERM/SIGBREAK
    stop after a checkpoint and exit 0 (an interrupted run is not a failed one).

    python3 diamond_nca.py --config "job diamond_nca | complexity params" --out runs/a4
    python3 diamond_nca.py --config "..." --out runs/a4 --resume
    python3 diamond_nca.py --stub --out /tmp/x --set generations=12     # no torch: a synthetic score, for tests
"""
from __future__ import annotations

import argparse
import json
import os
import random
import shutil
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import pareto_complexity as P  # noqa: E402

DEFAULTS = dict(target="sphere", size=24, eval_iters=600, generations=30, children=4, patience=4, seed=0,
                score="stability_iou", channels=8, hidden=32, max_channels=32, max_hidden=256, min_channels=5,
                min_hidden=16, retain_fraction=0.98, complexity_budget=float("inf"), stub_secs=0.0)


class Interrupted(Exception):
    pass


class _Stop:
    """SIGINT / SIGTERM / SIGBREAK: stop at the next evaluation boundary and exit 0 (the trainer has its own, for the
    evaluation in flight). Here so the stub needs no torch."""
    requested = False

    @classmethod
    def install(cls, also=None):
        """`also`: the trainer's own _Stop, so one signal stops both the evaluation in flight and the search."""
        import signal

        def handler(*_):
            cls.requested = True
            if also is not None:
                also.requested = True
        for name in ("SIGINT", "SIGTERM", "SIGBREAK"):
            sig = getattr(signal, name, None)
            if sig is not None:
                try:
                    signal.signal(sig, handler)
                except (ValueError, OSError):
                    pass


def nca_params(c: int, h: int) -> int:
    """NCA3D's parameter count: w1 is a 1x1x1 conv 5C -> H, w2 is H -> C (the perception kernels are a buffer)."""
    return (5 * c * h + h) + (h * c + c)


def _parse_config(s: str) -> dict:
    cfg: dict = {}
    for part in (s or "").split("|"):
        part = part.strip()
        if not part:
            continue
        key, _, val = part.partition(":") if (":" in part and " " not in part.split(":", 1)[0]) else part.partition(" ")
        key, val = key.strip(), val.strip()
        for cast in (int, float):
            try:
                cfg[key] = cast(val)
                break
            except ValueError:
                continue
        else:
            cfg[key] = val if val else True
    return cfg


class NcaCapacity:
    """The Problem protocol of pareto_complexity over NCA capacity, with the evaluation cache that makes it resumable."""

    def __init__(self, cfg: dict, out_dir: str, stub: bool = False, device: str = "auto", threads: int = 0):
        self.cfg, self.out, self.stub, self.device, self.threads = cfg, out_dir, stub, device, threads
        self.cache_path = os.path.join(out_dir, "evals.json")
        try:
            with open(self.cache_path) as f:
                self.cache = json.load(f)
        except (OSError, ValueError):
            self.cache = {}
        self.fresh = 0          # evaluations actually run this session (the rest came from the cache)

    def seed(self, rng):
        return (int(self.cfg["channels"]), int(self.cfg["hidden"]))

    def grow(self, g, rng):
        c, h = g
        if rng.random() < 0.5 and c < self.cfg["max_channels"]:
            return (min(self.cfg["max_channels"], c + rng.choice((2, 4))), h)
        return (c, min(self.cfg["max_hidden"], h + rng.choice((16, 32, 64))))

    def shrink(self, g, rng):
        c, h = g
        if rng.random() < 0.5 and c > self.cfg["min_channels"]:
            return (max(self.cfg["min_channels"], c - rng.choice((1, 2))), h)
        return (c, max(self.cfg["min_hidden"], h - rng.choice((8, 16, 32))))

    def evaluate(self, g):
        key = f"{g[0]},{g[1]}"
        if key in self.cache:
            r = self.cache[key]
            return r["score"], r["complexity"]
        if _Stop.requested:
            raise Interrupted(f"c{g[0]}_h{g[1]}")
        if self.stub:
            time.sleep(float(self.cfg.get("stub_secs") or 0))     # a stand-in for the training's wall time (tests)
            if _Stop.requested:
                raise Interrupted(f"c{g[0]}_h{g[1]}")
            r = self._stub(g)
        else:
            r = self._train(g)
        r["complexity"] = float(nca_params(*g))
        self.cache[key] = r
        tmp = self.cache_path + ".tmp"
        with open(tmp, "w") as f:
            json.dump(self.cache, f, indent=1)
        os.replace(tmp, self.cache_path)
        self.fresh += 1
        return r["score"], r["complexity"]

    @staticmethod
    def _stub(g):
        """A synthetic capacity curve (saturates near 10 channels x 96 hidden) - only for tests without torch."""
        c, h = g
        s = min(1.0, (min(c, 10) / 10.0) * 0.6 + (min(h, 96) / 96.0) * 0.4)
        return {"score": round(s, 4), "best_iou": s, "stability_iou": s, "wall_secs": 0.0}

    def _train(self, g):
        import nca3d_trainer as T
        c, h = g
        run_dir = os.path.join(self.out, "evals", f"c{c}_h{h}")
        params = {"target": self.cfg["target"], "size": int(self.cfg["size"]), "channels": c, "hidden": h,
                  "iters": int(self.cfg["eval_iters"]), "seed": int(self.cfg["seed"]), "out_dir": run_dir}
        res = T.run(params, resume=os.path.isfile(os.path.join(run_dir, "ckpt.pt")), device_pref=self.device,
                    threads=self.threads, install_signals=False)
        if res.get("interrupted"):
            raise Interrupted(f"c{c}_h{h}")
        m, gif = res["metrics"], res["artifacts"].get("gif")
        return {"score": float(m[self.cfg["score"]]), "best_iou": m["best_iou"], "stability_iou": m["stability_iou"],
                "wall_secs": m["wall_secs"], "gif": os.path.relpath(gif, self.out) if gif else None}


class Controller(P.DiamondController):
    """DiamondController, unchanged, plus a status.json / archive.json after every generation."""

    def __init__(self, problem, cfg, out_dir, generations):
        super().__init__(problem, cfg)
        self.out, self.generations = out_dir, generations

    def _log(self, gen, cur):
        super()._log(gen, cur)
        bs = self.archive.best_score()
        front = sorted(({"channels": m.genome[0], "hidden": m.genome[1], "score": m.score, "params": m.complexity}
                        for m in self.archive.members), key=lambda r: r["params"])
        _write(os.path.join(self.out, "archive.json"), {"front": front, "history": self.history})
        _write(os.path.join(self.out, "status.json"), {
            "step": gen + 1, "steps": self.generations, "phase": "searching", "regime": self.regime,
            "best_iou": round(bs.score, 4), "loss": round(1.0 - bs.score, 4), "params_of_best": bs.complexity,
            "evals": len(self.p.cache), "updated": int(time.time())})


def _write(path, data):
    tmp = path + ".tmp"
    with open(tmp, "w") as f:
        json.dump(data, f, indent=1)
    os.replace(tmp, path)


def search(cfg: dict, out_dir: str, stub=False, device="auto", threads=0) -> dict:
    os.makedirs(out_dir, exist_ok=True)
    if stub:
        _Stop.install()
    else:
        import nca3d_trainer as T
        _Stop.install(also=T._Stop)
    problem = NcaCapacity(cfg, out_dir, stub=stub, device=device, threads=threads)
    if problem.cache:
        print(json.dumps({"resumed_with_cached_evals": len(problem.cache)}), flush=True)
    dc = P.DiamondConfig(generations=int(cfg["generations"]), children=int(cfg["children"]),
                         patience=int(cfg["patience"]), seed=int(cfg["seed"]),
                         retain_fraction=float(cfg["retain_fraction"]),
                         complexity_budget=float(cfg["complexity_budget"]))
    ctrl = Controller(problem, dc, out_dir, dc.generations)
    try:
        ctrl.run()
    except Interrupted as e:
        st = json.load(open(os.path.join(out_dir, "status.json"))) if os.path.isfile(os.path.join(out_dir, "status.json")) else \
            {"step": 0, "steps": dc.generations}
        _write(os.path.join(out_dir, "status.json"), dict(st, phase="interrupted", interrupted_in=str(e), updated=int(time.time())))
        print(json.dumps({"interrupted_in": str(e), "evals_cached": len(problem.cache)}), flush=True)
        return {"interrupted": True}
    chosen = ctrl.archive.simplest_at_or_above(dc.retain_fraction * ctrl.peak_score) or ctrl.archive.best_score()
    hist = ctrl.history
    peak_c = max(r["cur_complexity"] for r in hist) if hist else chosen.complexity
    ev = problem.cache.get(f"{chosen.genome[0]},{chosen.genome[1]}", {})
    gif = None
    if ev.get("gif") and os.path.isfile(os.path.join(out_dir, ev["gif"])):
        gif = os.path.join(out_dir, "growth.gif")
        shutil.copyfile(os.path.join(out_dir, ev["gif"]), gif)
    metrics = {"best_iou": ev.get("best_iou"), "stability_iou": ev.get("stability_iou"),
               "params": int(chosen.complexity), "channels": chosen.genome[0], "hidden": chosen.genome[1],
               "peak_params": int(peak_c), "evals": len(problem.cache),
               "diamond": bool(peak_c > chosen.complexity and chosen.score >= dc.retain_fraction * ctrl.peak_score),
               "wall_secs": round(sum(float(v.get("wall_secs") or 0) for v in problem.cache.values()), 1)}
    result = {"metrics": metrics, "artifacts": {"gif": gif}, "config": {k: v for k, v in cfg.items() if v != float("inf")}}
    _write(os.path.join(out_dir, "result.json"), result)
    _write(os.path.join(out_dir, "status.json"), {"step": dc.generations, "steps": dc.generations, "phase": "done",
                                                  "best_iou": round(chosen.score, 4), "loss": round(1.0 - chosen.score, 4),
                                                  "params_of_best": chosen.complexity, "evals": len(problem.cache),
                                                  "updated": int(time.time())})
    print("RESULT", json.dumps(metrics), flush=True)
    return result


def main(argv=None):
    ap = argparse.ArgumentParser(description="A4: NCA capacity search with the diamond optimizer")
    ap.add_argument("--config", default="job diamond_nca | complexity params", help="a Notion row's Config, verbatim")
    ap.add_argument("--out", required=True)
    ap.add_argument("--resume", action="store_true", help="accepted for the Compute contract; evals.json is always reused")
    ap.add_argument("--device", default="auto", choices=("auto", "cuda", "mps", "cpu"))
    ap.add_argument("--require-gpu", action="store_true")
    ap.add_argument("--threads", type=int, default=0)
    ap.add_argument("--stub", action="store_true", help="synthetic score (no torch): tests only")
    ap.add_argument("--set", action="append", default=[], metavar="KEY=VALUE")
    a = ap.parse_args(argv)
    os.makedirs(a.out, exist_ok=True)
    given = _parse_config(a.config)
    for kv in a.set:
        k, _, v = kv.partition("=")
        given.update(_parse_config(f"{k} {v}"))
    if given.get("job", "diamond_nca") != "diamond_nca":
        raise SystemExit(f"this runs job diamond_nca, not {given['job']!r}")
    if given.get("complexity", "params") != "params":
        raise SystemExit(f"complexity {given['complexity']!r}: only 'params' is implemented")
    unknown = [k for k in given if k not in DEFAULTS and k not in ("job", "complexity")]
    if unknown:
        print(json.dumps({"dropped_config_keys": unknown}), flush=True)
    cfg = dict(DEFAULTS, **{k: v for k, v in given.items() if k in DEFAULTS})
    if a.require_gpu and not a.stub:
        import nca3d_trainer as T
        if T.pick_device(a.device) == "cpu":
            _write(os.path.join(a.out, "result.json"), {"reason": "no GPU (cuda or mps) on this computer"})
            return 3
    search(cfg, a.out, stub=a.stub, device=a.device, threads=a.threads)
    return 0


if __name__ == "__main__":
    sys.exit(main())
