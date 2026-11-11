"""The Manta's Redline AI with its NOSE RATE in the genome: one Soar drive (MantaSoarAITuningSO's two stick bands) plus
one AI nose follow rate per intensity (VesselTransformer.AINoseFollowPerSecond, the knob Switchback sets per intensity
and difficulty), scored on all four Redline intensities at once (Docs/AI_SYSTEM/AI_PLAN.md step time-arcade,
decision 7).

Why: in Redline's model the fleet's AI nose lag (1.5/s) makes the AI Manta 1.7x (I1) to 5x (I4) slower than one with
none, more than any Soar band buys (REDLINE.md section 5.1); `redline-manta-time` (redline_fit) searches the bands
alone at the fleet lag. This search lets the rate move too, so its answer says whether the nose rate is a GENOME
property (an interior best rate per intensity, below which the AI is slow and above which it overshoots the mouths)
or a pure handicap (the search walks to the top of the range at every intensity: faster is always better, so the rate
is a difficulty setting, Switchback's shape, and Legendary should fly the top rate). Either way the answer is a
PROPOSAL for Yash (the AI owner); nothing here writes an asset.

The race and the score are redline_fit's (manta_redline_sim.race, the same courses, the same per-race / per-intensity
/ composite loss), with the intensity's nose rate passed to the race. On top of it:

    loss  = redline_fit's composite
          + rate_cost x mean over intensities of (rate - 1.5) / (hi - 1.5)    the smallest rate that buys the pace wins a tie
          + stay_near_shipped x the bands' pull toward the nearest genome      (redline_fit's)

The space: BoostStickBand, YawsteryStickBand ([0, 1], the SO's fields by name) and NoseFollowI1..I4 (per second, from the
fleet lag 1.5 up to `rate_hi`, 9 by default: the human library's NoseFollowPerSecond; 0 = "no lag" is not in the range
because 9/s already races within about 10% of it in the model and misses fewer mouths; manta_redline_nose_sweep.py,
Docs/Reports/REDLINE_MANTA_NOSE.md).

args: redline_fit's (intensities, seeds, seedbase, limit, genomes, stay_near_shipped) plus
    rate_cost          weight of the pull toward the fleet lag (default 0.02; 0 = pure pace)
    rate_hi            the top of every nose-rate range (only read by space_for; the job's space is authoritative)
    nose_lead          the AI nose lead (AIPilot.NoseLeadGain, Tools/Build/ai_nose_lead.py) on the genome's nose rate:
                       a gain stated at the fleet lag, or {"gain", "max_extra_deg"}; absent or 0 = OFF, today's steering
"""
import os

import redline_fit as base

VERSION = "1"
FLEET = 1.5                    # VesselTransformer.LERP_AMOUNT: the AI nose's follow rate when no mode sets one
RATE_HI = 9.0
RATES = {f"NoseFollowI{i}": i for i in (1, 2, 3, 4)}
PATHS = list(base.PATHS) + ["Tools/Compute/evaluators/redline_fit.py"]
REQUEST_ARGS = dict(base.REQUEST_ARGS)
_CTX = {}


def inputs(args):
    return base.inputs(args) + ["Tools/Compute/evaluators/redline_fit.py", "Tools/Build/ai_nose_lead.py"]


def space_for(root, rate_hi=RATE_HI):
    return base.space_for(root) + [{"name": k, "lo": FLEET, "hi": rate_hi, "init": FLEET} for k in RATES]


def seeds(args, root):
    """Every genome's bands at the fleet lag (today's AI), and the same bands at the top rate (the no-lag end)."""
    hi = float(args.get("rate_hi", RATE_HI))
    out = []
    for g in base.seeds(args, root):
        out.append(dict(g, **{k: FLEET for k in RATES}))
        out.append(dict(g, **{k: hi for k in RATES}))
    return out


def baseline(args):
    return None   # the space's init: the shipped bands at the fleet lag (today's AI Manta)


def evaluate(params, ctx):
    a = ctx["args"]
    if base._CTX.get("key") != base._key(ctx):
        base.setup(ctx)
    sim = base._CTX["sim"]
    p = base.clamp({k: float(params.get(k, v)) for k, v in base._CTX["shipped"].items()})
    bands = (p["BoostStickBand"], p["YawsteryStickBand"])
    hi = float(a.get("rate_hi", RATE_HI))
    rates = {i: max(FLEET, min(hi, float(params.get(k, FLEET)))) for k, i in RATES.items()}
    limit = float(a.get("limit", 900))
    med = lambda xs: round(sorted(xs)[len(xs) // 2], 1) if xs else None
    scores, metrics, every_finishes = {}, {}, True
    for i, races in base._CTX["courses"].items():
        rows = []
        for s, c, flat in races:
            r = sim.race(c, i, bands=bands, limit=limit, nose_rate=rates.get(i, FLEET), nose_lead=a.get("nose_lead"))
            r["flat"] = flat
            rows.append(r)
        fin = [r["time"] for r in rows if r["time"] is not None]
        every_finishes &= len(fin) == len(rows)
        scores[i] = base.score(rows, limit)
        metrics[f"I{i}"] = {"score": round(scores[i], 4), "finished": f"{len(fin)}/{len(rows)}", "median_s": med(fin),
                            "nose": round(rates.get(i, FLEET), 3), "misses": sum(r["misses"] for r in rows),
                            "soar": round(sum(r["soar"] for r in rows) / len(rows), 3)}
    s = list(scores.values())
    combined = sum(s) / len(s) + (base.COMBINE_WORST * max(s) if len(s) > 1 else 0.0)
    loss = combined
    rc = float(a.get("rate_cost", 0.02))
    if rc and hi > FLEET:
        loss += rc * sum((rates[i] - FLEET) / (hi - FLEET) for i in scores) / len(scores)
    w = float(a.get("stay_near_shipped", 0.01))
    if w:
        span = lambda k: base.FIELDS[k][1] - base.FIELDS[k][0]
        loss += w * min(sum(((p[k] - g[k]) / span(k)) ** 2 for k in base.FIELDS if k in g) / len(base.FIELDS)
                        for _, g in base._CTX["genomes"] if g)
    metrics.update(score=round(combined, 4), worst=f"I{max(scores, key=scores.get)}", limits=sim.LIMITS)
    return {"loss": loss, "valid": every_finishes, "metrics": metrics}


def self_test():
    """At the fleet lag it is redline_fit's race (same composite); a faster nose scores better at I1 (the lever this
    job exists for); the rate cost pulls toward the fleet lag (the negative control: rate_cost 0 makes the fast nose
    win by more); the seeds carry both ends of the rate range; the space is the bands plus four rates."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(os.path.dirname(os.path.dirname(here)))
    ctx = {"root": root, "args": {"intensities": [1], "seeds": 2, "stay_near_shipped": 0.0, "rate_cost": 0.0}}
    base._CTX.clear()
    bad = []
    try:
        theirs = base.evaluate({}, {"root": root, "args": dict(ctx["args"])})
        fleet = evaluate({}, ctx)
        if abs(fleet["metrics"]["score"] - theirs["metrics"]["score"]) > 1e-9:
            bad.append(f"redline_nose_fit: at the fleet lag it is not redline_fit's race ({fleet['metrics']['score']} vs "
                       f"{theirs['metrics']['score']})")
        fast = evaluate({"NoseFollowI1": 6.0}, ctx)
        if not fast["loss"] < fleet["loss"]:
            bad.append(f"redline_nose_fit: a 6/s nose did not beat the fleet lag at I1 ({fast['loss']:.3f} vs {fleet['loss']:.3f})")
        if fast["metrics"]["I1"]["nose"] != 6.0:
            bad.append("redline_nose_fit: the rate did not reach the race's metrics")
        costly = evaluate({"NoseFollowI1": 6.0}, dict(ctx, args=dict(ctx["args"], rate_cost=1.0)))
        if not costly["loss"] > fast["loss"]:
            bad.append("redline_nose_fit: the rate cost does not pull toward the fleet lag")
        sd = seeds({}, root)
        if not any(s["NoseFollowI4"] == FLEET for s in sd) or not any(s["NoseFollowI4"] == RATE_HI for s in sd):
            bad.append("redline_nose_fit: the seeds do not carry both ends of the rate range")
        if [d["name"] for d in space_for(root)] != ["BoostStickBand", "YawsteryStickBand"] + list(RATES):
            bad.append("redline_nose_fit: the space is not the bands and four rates")
    except Exception as e:
        bad.append(f"redline_nose_fit: {type(e).__name__}: {e}")
    base._CTX.clear()
    return bad
