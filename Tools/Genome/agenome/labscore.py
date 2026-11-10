"""The fitness lab's element-aware scorer ("lab"), beside the judge: render genomes on N windows and score each.

    python3 -m agenome.labscore out.json --genome g.json ... [--front name=pareto.json] --windows 4 --hop 6

Lower is better. 1.0 = Amoebius footage from a held-out source (4 s windows of reels 3 and 5) against a reference
fitted on reels 1, 2 and 4, the judge's protocol; a typical phenotype reads about 9. Five axes (spec, crit, mech,
bove, dist), each calibrated to 0 = new-source real and 1 = a typical phenotype, combined by a p=2 power mean
(fitness_lab/scorer/MERGE.md). The judge keeps its meaning; this score is reported as `lab` and `lab_windows`, with
the per-axis `lab_parts` (mean over windows) so one axis riding near 0 while the others stay high shows up.

The scorer lives in the fitness lab's folder (AGENOME_LAB, default the shared scorer/); it needs the judge's Keras
stack plus cloudpickle, opencv-python-headless, scikit-image, PyWavelets and scikit-learn, and costs about 1.5 s
per window on a free core after a ~20 s first load. `windows(frames_list)` is the in-process entry point a search
term would call (one 60-frame uint8 [T,144,256,3] array per window)."""
import argparse
import json
import os
import sys

import numpy as np

LAB = os.environ.get("AGENOME_LAB", "/mnt/project-files/amoebius-genome/fitness_lab/scorer")
_S = [None]


def scorer():
    if _S[0] is None:
        os.environ.setdefault("KERAS_BACKEND", "jax")
        if LAB not in sys.path:
            sys.path.insert(0, LAB)
        from amoebius_scorer import Scorer
        _S[0] = Scorer()
    return _S[0]


def windows(frames_list, parts=False):
    """Score each window on its own. Returns (mean, [per-window], {axis: mean over windows} or None)."""
    S = scorer()
    ws = [S._win(f) for f in frames_list]
    per = [float(v) / S.level for v in np.asarray(S.m.score(ws), dtype=float).ravel()]
    P = None
    if parts:
        ps = [S.parts(w) for w in ws]
        P = {k: float(np.mean([p[k] for p in ps])) for k in ps[0]}
    return float(np.mean(per)), per, P


def _load_front(path):
    d = json.load(open(path))
    rows = d if isinstance(d, list) else d.get("members", d.get("pop", []))
    return [r for r in rows if r.get("genome")]


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out")
    ap.add_argument("--genome", nargs="*", default=[])
    ap.add_argument("--front", action="append", default=[], help="name=pareto.json; scores its --top rows by dist")
    ap.add_argument("--top", type=int, default=3)
    ap.add_argument("--windows", type=int, default=4)
    ap.add_argument("--hop", type=float, default=6.0, help="seconds between window starts (6 does not alias an 8 s "
                                                           "period the way 4 and 8 do)")
    ap.add_argument("--frames", type=int, default=60)
    ap.add_argument("--no-parts", action="store_true")
    a = ap.parse_args(argv)
    from .genome import Genome
    from .render import Renderer
    R = Renderer()
    items = []
    for spec in a.front:
        name, path = spec.split("=", 1)
        rows = sorted(_load_front(path), key=lambda r: r.get("dist", 1e9))[:a.top]
        items += [(f"{name}#{k}", r) for k, r in enumerate(rows)]
    items += [(os.path.basename(p), {"genome": json.load(open(p))}) for p in a.genome]
    out = []
    for name, r in items:
        try:
            g = Genome.from_json(r["genome"])
            offs = [k * a.hop for k in range(max(1, a.windows))]
            frs = [R.render(g, a.frames, t_shift=t) for t in offs]
            m, per, P = windows(frs, parts=not a.no_parts)
        except Exception as e:  # a genome that no longer renders is reported, not fatal
            print(f"{name}: {type(e).__name__}: {e}", file=sys.stderr, flush=True)
            continue
        meta = (r.get("genome", {}).get("meta", {}) or {})
        row = {"front": name, "zbytes": g.size(), "nodes": len(g.nodes), "src": meta.get("name") or r.get("src"),
               "dist": r.get("dist"), "judge": r.get("judge"), "lab": m, "lab_windows": [round(x, 3) for x in per],
               "lab_offsets": offs, "lab_parts": P}
        out.append(row)
        json.dump(out, open(a.out, "w"), indent=1)  # after every genome, so a long run can be read while it works
        print(f"{name:34s} z {row['zbytes']:5d} lab {m:6.2f}  windows {' '.join(f'{x:.2f}' for x in per)}"
              + (f"  parts {' '.join(f'{k} {v:.2f}' for k, v in P.items())}" if P else "")
              + f"  {str(row['src'])[:36]}", flush=True)
    json.dump(out, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
