"""The fitness lab's element-aware scorer ("lab"), beside the judge: render genomes on N windows and score each.

    python3 -m agenome.labscore out.json --genome g.json ... [--front name=pareto.json] --windows 4 --hop 6

Lower is better. 1.0 = Amoebius footage from a held-out source (4 s windows of reels 3 and 5) against a reference
fitted on reels 1, 2 and 4, the judge's protocol; a typical phenotype reads about 9. Five axes (spec, crit, mech,
bove, dist), each calibrated to 0 = new-source real and 1 = a typical phenotype, combined by a p=2 power mean
(fitness_lab/scorer/MERGE.md). The judge keeps its meaning; this score is reported as `lab` and `lab_windows`, with
the per-axis `lab_parts` (mean over windows) so one axis riding near 0 while the others stay high shows up.

Below about 1.0 the total no longer shows whether the game's elements are there: real footage with its bright and
non-blue elements inpainted away (the blue prism walls stay) reads below 1.0 in 16 of 24 held-out windows, and a
genome with almost none of them reads 0.29 (CONTRACT.md (37)). So each row also splits every window's lab into its
element axes (`lab_elements`: bove and dist, the patch and spatial-token axes built to see prisms, beams and rings)
and the other three (`lab_other`: spec, crit, mech), each the lab computed with the other group at 0, so that
lab^2 = elements^2 + other^2 window by window (p = 2), then averaged over windows like `lab`. Reference
`lab_elements`, means over the 24 held-out windows (fitness/checks/labA_best_1111/strip_lab.json): real 0.09 (median
0.01, single windows up to 0.47), bright and non-blue elements inpainted away 0.17, the walls inpainted too 0.49.
It can show that a genome is short of real footage; it cannot prove the elements are there.

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


ELEMENTS = ("bove", "dist")  # the axes that see the game's elements; the rest see sky, sharpness and motion


def _score(frames_list, parts):
    """Each window's lab, and its per-axis parts when asked (held-out-real units: 1.0 = the held-out mean)."""
    S = scorer()
    ws = [S._win(f) for f in frames_list]
    per = [float(v) / S.level for v in np.asarray(S.m.score(ws), dtype=float).ravel()]
    return per, ([S.parts(w) for w in ws] if parts else None)


def split(parts):
    """One window's parts -> (elements, other): its lab with only the ELEMENTS axes, and with only the others, in the
    combiner's own units (the power mean of part x parts_level, weighted over all the axes, / level). With the
    scorer's power 2, lab^2 = elements^2 + other^2."""
    S = scorer()
    m = S.m
    W = sum(m.w[k] for k in parts)

    def lab_of(keys):
        return (sum(m.w[k] * (parts[k] * S.parts_level[k]) ** m.p for k in keys) / W) ** (1.0 / m.p) / S.level
    return lab_of([k for k in parts if k in ELEMENTS]), lab_of([k for k in parts if k not in ELEMENTS])


def windows(frames_list, parts=False):
    """Score each window on its own. Returns (mean, [per-window], {axis: mean over windows} or None)."""
    per, ps = _score(frames_list, parts)
    P = {k: float(np.mean([p[k] for p in ps])) for k in ps[0]} if ps else None
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
            per, ps = _score(frs, parts=not a.no_parts)
        except Exception as e:  # a genome that no longer renders is reported, not fatal
            print(f"{name}: {type(e).__name__}: {e}", file=sys.stderr, flush=True)
            continue
        m = float(np.mean(per))
        P = {k: float(np.mean([p[k] for p in ps])) for k in ps[0]} if ps else None
        meta = (r.get("genome", {}).get("meta", {}) or {})
        row = {"front": name, "zbytes": g.size(), "nodes": len(g.nodes), "src": meta.get("name") or r.get("src"),
               "dist": r.get("dist"), "judge": r.get("judge"), "lab": m, "lab_windows": [round(x, 3) for x in per],
               "lab_offsets": offs, "lab_parts": P}
        if ps:
            sp = [split(p) for p in ps]
            row.update({"lab_elements": float(np.mean([e for e, _ in sp])),
                        "lab_elements_windows": [round(e, 3) for e, _ in sp],
                        "lab_other": float(np.mean([o for _, o in sp])),
                        "lab_other_windows": [round(o, 3) for _, o in sp],
                        "lab_parts_windows": [{k: round(v, 4) for k, v in p.items()} for p in ps]})
        out.append(row)
        json.dump(out, open(a.out, "w"), indent=1)  # after every genome, so a long run can be read while it works
        print(f"{name:34s} z {row['zbytes']:5d} lab {m:6.2f}  windows {' '.join(f'{x:.2f}' for x in per)}"
              + (f"  elements {row['lab_elements']:.2f} other {row['lab_other']:.2f}"
                 f"  parts {' '.join(f'{k} {v:.2f}' for k, v in P.items())}" if P else "")
              + f"  {str(row['src'])[:36]}", flush=True)
    json.dump(out, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
