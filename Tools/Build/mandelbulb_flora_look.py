#!/usr/bin/env python3
"""Render ONE candidate curve family and report the gate metrics beside it.

The look-search tool: a growth rule is judged by RENDERING it (an offline model can report a
perfect size distribution for a form that reads as gravel), so this takes a species, an element
and any number of rule overrides, grows the plant the game would lay (walk -> claim -> budget),
renders the four-view judging sheet with the heart drawn, and prints the numbers the measure
gates will ask about. It writes nothing into the repo.

    mandelbulb_flora_look.py --species CoralBloom --element Mass --out sheet.png
    mandelbulb_flora_look.py --species CoralBloom --element Mass \\
        --set momentum=0.9 --set lanes=20 --cross 0.03,0.014 --out sheet.png
    mandelbulb_flora_look.py --species X --element E --json candidate.json --out sheet.png

`--set key=value` keys are mandelbulb_flora_model.Rules.FIELDS names; `--json` is a dict of the
same. Every field the C# GrowthRules gains is reachable here the day it lands in Rules.FIELDS.
"""
import argparse
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mandelbulb_flora_model as M                                    # noqa: E402
import mandelbulb_flora_render as R                                   # noqa: E402
import measure_mandelbulb_flora as X                                  # noqa: E402


def grow_candidate(species, element, overrides=None, cross=None, seed=12345,
                   budget=None, weights=(0.0, 0.0, 0.0)):
    """The plant the game lays for this rule: (surface, rules, prisms, curves, boxes)."""
    budget = budget or M.budget_for(species)
    degree, tables = M.load_tables()
    surface = M.surface_for(element, *weights, tables=tables, degree=degree, width=M.FIELD_WIDTH)
    rules = M.rules_for(element, species)
    for k, v in (overrides or {}).items():
        if k not in M.Rules.FIELDS:
            raise SystemExit(f"unknown rule field {k!r}; fields are {M.Rules.FIELDS}")
        setattr(rules, k, type(getattr(rules, k))(v))
    cross = cross or M.cross_section_for(element, species)
    raw, _, _ = M.grow(surface, rules, seed, budget * X.CANDIDATE_FACTOR)
    centres = [M._mul(M.pose(surface, p)[0], M.SHELL_RADIUS) for p in raw]
    kept = M.claim_filter(raw, centres)[:budget]
    curves = len({p.curve for p in kept})
    boxes = [X.obb(surface, p, M.SHELL_RADIUS, cross) for p in kept]
    return surface, rules, kept, curves, boxes


def metrics(prisms, boxes, heart):
    vols = [8 * b[2][0] * b[2][1] * b[2][2] for b in boxes]
    dist = [math.sqrt(sum(c * c for c in b[0])) for b in boxes]
    bins = [0] * 8
    for p in prisms:
        bins[min(7, int((1 - math.cos(p.theta)) / 2 * 8))] += 1
    n = max(1, len(prisms))
    filled = sum(1 for b in bins if b > 0.02 * n)
    # interleave, the two bounds measure --check gates
    reach = 2 * max(math.sqrt(sum(h * h for h in b[2])) for b in boxes) if boxes else 1.0
    pairs = deep = inter = 0
    worst = 1e9
    for i, j in X.near_pairs(boxes, reach):
        if prisms[i].curve == prisms[j].curve and abs(i - j) == 1:
            continue
        pairs += 1
        s = X.touching_scale(boxes[i], boxes[j])
        if s < 1.0:
            inter += 1
        if s < 0.5:
            deep += 1
        worst = min(worst, s)
    return {
        "prisms": len(prisms),
        "curves": len({p.curve for p in prisms}),
        "volume": round(sum(vols)),
        "size_span": round(max(vols) / max(min(vols), 1e-9), 1) if vols else 0,
        "bands": bins,
        "bands_filled": filled,
        "radius": round(max(dist), 1) if dist else 0,
        "nearest_to_heart": round(min(dist), 1) if dist else 0,
        "nearest_gap": round(min(dist) - heart, 1) if dist else 0,
        "within_5u_of_heart": sum(1 for d in dist if d <= heart + 5.0),
        "pairs": pairs,
        "interpenetrating_fraction": round(inter / max(1, pairs), 3),
        "deep_fraction": round(deep / max(1, pairs), 3),
        "worst_scale": round(worst, 3) if pairs else None,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--species", required=True)
    ap.add_argument("--element", required=True)
    ap.add_argument("--set", action="append", default=[], metavar="FIELD=VALUE")
    ap.add_argument("--json", help="JSON dict of rule overrides")
    ap.add_argument("--cross", help="cross-section x,y in surface units")
    ap.add_argument("--weights", default="0,0,0", help="the three family weights")
    ap.add_argument("--seed", type=int, default=12345)
    ap.add_argument("--budget", type=int, default=None)
    ap.add_argument("--heart", type=float, default=1.5, help="heart crystal world radius to draw")
    ap.add_argument("--out", required=True, help="PNG sheet path")
    ap.add_argument("--single", action="store_true", help="one 900px view instead of the sheet")
    args = ap.parse_args()

    overrides = {}
    if args.json:
        overrides.update(json.load(open(args.json)))
    for s in args.set:
        k, v = s.split("=", 1)
        overrides[k] = float(v)
    cross = tuple(float(v) for v in args.cross.split(",")) if args.cross else None
    weights = tuple(float(v) for v in args.weights.split(","))

    surface, rules, prisms, curves, boxes = grow_candidate(
        args.species, args.element, overrides, cross, args.seed, args.budget, weights)
    m = metrics(prisms, boxes, args.heart)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)) or ".", exist_ok=True)
    if args.single:
        R.render(boxes, args.out)
    else:
        R.render_sheet(boxes, args.out, heart=args.heart)
    m["rules"] = dict(zip(M.Rules.FIELDS, rules.as_list()))
    m["cross"] = list(cross or M.cross_section_for(args.element, args.species))
    m["sheet"] = args.out
    print(json.dumps(m, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
