"""Smoothing the intent instead of (or on top of) coasting: idir <- unit(b*old + (1-b)*new) per re-steer.
Attention LOD on (engaged agents re-steer every step), k = 4 for the calm rest. Does a low-pass buy the
smoothness the fractional update bought, WITHOUT the pursuit loss?

    python -m substrate.blend_study   -> results/blend.json
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from dataclasses import replace
import substrate.frac_study as fs
from substrate import species as S


def run(cond, b, seed):
    orig = S.locust if cond != "grazer" else S.grazer
    name = "locust" if cond != "grazer" else "grazer"
    def patched(*a, **kw):
        return replace(orig(*a, **kw), intent_blend=b)
    setattr(fs.S, name, patched)
    try:
        return fs.run(cond, 4, seed, attn=True)
    finally:
        setattr(fs.S, name, orig)


if __name__ == "__main__":
    rows = []
    for cond in ("grazer", "locust_frenzy"):
        for b in (0.0, 0.3, 0.5, 0.7, 0.85):
            rs = [run(cond, b, s) for s in (7, 23)]
            agg = dict(cond=cond, blend=b, hits_per_min=float(np.mean([r["hits_per_min"] for r in rs])),
                       jerk_rel=float(np.mean([r["feel"]["jerk_rel"] for r in rs])),
                       coherence=float(np.mean([r["feel"]["coherence"] for r in rs])),
                       heading_to_pilot=float(np.mean([r["enc"]["heading_to_pilot"] or 0 for r in rs])),
                       near_frac=float(np.mean([r["enc"]["near_frac"] for r in rs])),
                       turn_deg_s=float(np.mean([r["turn_deg_s"] for r in rs])))
            agg = {k: (round(v, 3) if isinstance(v, float) else v) for k, v in agg.items()}
            rows.append(agg); print(json.dumps(agg))
    json.dump(rows, open(os.path.join(os.path.dirname(__file__), "results", "blend.json"), "w"), indent=1)
