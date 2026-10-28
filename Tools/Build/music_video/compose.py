#!/usr/bin/env python3
"""Compose a shot plan from a song's analysis and the mapping.

    python3 Tools/Build/music_video/compose.py ANALYSIS.json --start 30 --end 90 [--beats auto|N] [--out plan.json]
                                               [--mapping mapping.json] [--force INDEX=ARCHETYPE]

Reads the measured curves (analyze_song.py) and the music -> gameplay mapping (mapping.json) and
writes plan.json for shoot.py, assemble.py and score.py.

Cuts. `--beats auto` (the default since v3) follows the song's SECTIONS: one shot per section,
a section shorter than the mapping's min_seconds merged into its shorter neighbour, one longer
than max_seconds split on downbeats, and a shot that ends where a drop begins runs
payoff_tail_bars into the drop so its payoff lands ON the drop. `--beats N` is the older equal
split (snapped to a section boundary, else a downbeat, else a beat).

Phrases. Every shot is a setup and a payoff: the payoff is the section boundary inside the shot,
else the downbeat nearest payoff_position through it. The archetype's event templates fire on
the setup bars (the kit's abilities 1, 2, 3; a loop being laid; a fleet converging; a shoal
gathering), on the payoff (ability 4 with a boost and a burst; the loop closing; the fleet
boosting in at once; the shoal scattering) and on the release bars after it.

Choice. Each shot is measured against the SONG's own range and gets the archetype with the
closest feature target, a bonus when the archetype names the shot's section, and penalties for
an archetype or a cell already used in the cut (so a cut spans the landscape). Intensity, field
of view and the render block follow. The features and the reason for every choice are written
next to each shot so a person (or a cheaper model) can read why.
"""
import argparse
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))


def load_mapping(path):
    return json.load(open(path))


def norm(values, lo, hi):
    return float(np.clip((values - lo) / max(hi - lo, 1e-9), 0, 1))


def beat_features(an, t0, t1):
    c = an["curves"]
    hz = c["hz"]
    t = np.arange(len(c["rms"])) / hz
    m = (t >= t0) & (t < t1)
    rms = np.array(c["rms"]); cen = np.array(c["centroid"]); fl = np.array(c["flatness"]); bass = np.array(c["bass"])
    onsets = np.array(an["onsets"])
    song_on_density = len(onsets) / an["duration"]
    f = {
        "energy": norm(rms[m].mean(), np.percentile(rms, 5), np.percentile(rms, 97)),
        "peak": norm(rms[m].max(), np.percentile(rms, 5), np.percentile(rms, 99)),
        "brightness": norm(cen[m].mean(), np.percentile(cen, 10), np.percentile(cen, 90)),
        "flatness": norm(fl[m].mean(), np.percentile(fl, 10), np.percentile(fl, 90)),
        "bass": norm(bass[m].mean(), np.percentile(bass, 10), np.percentile(bass, 90)),
        "onset_density": norm(((onsets >= t0) & (onsets < t1)).sum() / max(t1 - t0, 1e-3), 0.0, song_on_density * 1.8),
    }
    seg = rms[m]
    if len(seg) > 4:
        x = np.linspace(-0.5, 0.5, len(seg))
        f["trend"] = float(np.clip(np.polyfit(x, seg, 1)[0] / (np.percentile(rms, 97) - np.percentile(rms, 5) + 1e-9), -1, 1))
    else:
        f["trend"] = 0.0
    secs = [s for s in an["sections"] if s["start"] < t1 and s["end"] > t0]
    f["section"] = max(secs, key=lambda s: min(s["end"], t1) - max(s["start"], t0))["label"] if secs else "?"
    f["position"] = round((t0 + t1) / 2 / an["duration"], 3)
    return {k: (round(v, 3) if isinstance(v, float) else v) for k, v in f.items()}


def bar_length(an):
    d = np.diff(an["downbeats"]) if len(an["downbeats"]) > 2 else []
    return float(np.median(d)) if len(d) else 4 * 60.0 / an["tempo"]


def section_at(an, t):
    for s in an["sections"]:
        if s["start"] <= t < s["end"]:
            return s["label"]
    return "?"


def nearest(cands, target):
    return min(cands, key=lambda b: abs(b - target)) if cands else None


def cut_points_equal(an, start, end, beats, min_len=3.0):
    """Equal parts, each cut snapped to a section boundary within 2 s, else the nearest downbeat within 1.2 s, else the nearest beat."""
    cuts = [start]
    bounds = [s["start"] for s in an["sections"]]
    for i in range(1, beats):
        target = start + (end - start) * i / beats
        cand = [b for b in bounds if abs(b - target) <= 2.0 and b - cuts[-1] >= min_len and end - b >= min_len]
        how = "section boundary"
        if not cand:
            cand = [b for b in an["downbeats"] if abs(b - target) <= 1.2 and b - cuts[-1] >= min_len and end - b >= min_len]; how = "downbeat"
        if not cand:
            cand = [b for b in an["beats"] if b - cuts[-1] >= min_len and end - b >= min_len]; how = "beat"
        cut = nearest(cand, target) if cand else target
        cuts.append(round(float(cut), 3))
        print(f"cut {i} at {cut:.2f} s ({how})")
    cuts.append(end)
    return cuts


def cut_points_sections(an, start, end, cfg):
    """One shot per section, phrase length: short sections merged, long ones split on downbeats, a
    build that ends on a drop carried payoff_tail_bars into it."""
    min_s, max_s = float(cfg["min_seconds"]), float(cfg["max_seconds"])
    bar = bar_length(an)
    bounds = sorted({float(start), float(end)} | {float(s["start"]) for s in an["sections"] if start < s["start"] < end})
    pieces = [[a, b] for a, b in zip(bounds, bounds[1:])]

    def merge_short():
        while len(pieces) > 1:
            short = [i for i, p in enumerate(pieces) if p[1] - p[0] < min_s]
            if not short:
                return
            i = short[0]
            if i == 0: j = 1
            elif i == len(pieces) - 1: j = i - 1
            else: j = i - 1 if (pieces[i - 1][1] - pieces[i - 1][0]) <= (pieces[i + 1][1] - pieces[i + 1][0]) else i + 1
            lo, hi = min(i, j), max(i, j)
            print(f"section {pieces[i][0]:.2f}-{pieces[i][1]:.2f} s is shorter than {min_s:.0f} s: merged into {pieces[j][0]:.2f}-{pieces[j][1]:.2f}")
            pieces[lo] = [pieces[lo][0], pieces[hi][1]]; del pieces[hi]

    merge_short()
    # a shot that ends where a drop begins runs into the drop, so the payoff lands on it
    tail = float(cfg.get("payoff_tail_bars", 2)) * bar
    for i in range(len(pieces) - 1):
        b = pieces[i][1]
        if section_at(an, b) == "drop" and section_at(an, b - 0.01) != "drop":
            cand = [d for d in an["downbeats"] if b + tail * 0.6 <= d <= b + tail * 1.4 and pieces[i + 1][1] - d >= min_s]
            nb = nearest(cand, b + tail)
            if nb:
                print(f"the drop at {b:.2f} s: the shot before it runs {nb - b:.2f} s into it (payoff on the drop)")
                pieces[i][1] = nb; pieces[i + 1][0] = nb
    merge_short()
    out = []
    for a, b in pieces:
        n = max(1, math.ceil((b - a) / max_s))
        cuts = [a]
        for k in range(1, n):
            target = a + (b - a) * k / n
            cand = [d for d in an["downbeats"] if cuts[-1] + min_s <= d <= b - min_s * (n - k)]
            d = nearest(cand, target)
            cuts.append(float(d) if d is not None else target)
        cuts.append(b)
        if n > 1:
            print(f"section {a:.2f}-{b:.2f} s ({section_at(an, a)}) is longer than {max_s:.0f} s: {n} shots at " + ", ".join(f"{c:.2f}" for c in cuts[1:-1]))
        out += [round(x, 3) for x in cuts[:-1]]
    out.append(float(end))
    return out


def payoff_time(an, t0, t1, cfg):
    """The section boundary inside the shot, else the downbeat nearest payoff_position through it."""
    margin = float(cfg.get("payoff_margin_seconds", 1.5))
    bounds = [s["start"] for s in an["sections"] if t0 + margin < s["start"] < t1 - margin]
    if bounds:
        drops = [b for b in bounds if section_at(an, b) == "drop"]
        return float((drops or bounds)[0]), "section boundary" + (" (the drop)" if drops else "")
    target = t0 + (t1 - t0) * float(cfg.get("payoff_position", 0.7))
    cand = [d for d in an["downbeats"] if t0 + margin < d < t1 - margin]
    d = nearest(cand, target)
    if d is not None:
        return float(d), "downbeat"
    return float(target), "position"


def pick(archetypes, feats, weights, used_names, used_cells, mapping, force=None):
    if force:
        a = next(a for a in archetypes if a["name"] == force)
        return a, 0.0, "forced"
    best, best_d = None, 1e9
    for a in archetypes:
        d = 0.0
        for k, w in weights.items():
            d += w * (feats.get(k, 0.5) - a["target"].get(k, 0.5)) ** 2
        d = math.sqrt(d)
        if feats.get("section") in a.get("sections", []):
            d -= mapping.get("section_bonus", 0.0)
        if a["name"] in used_names:
            d += mapping["diversity_penalty"]
        if a.get("cell") in used_cells:
            d += mapping.get("same_cell_penalty", 0.0)
        if d < best_d:
            best, best_d = a, d
    parts = sorted(weights, key=lambda k: -weights[k] * abs(feats.get(k, 0.5) - best["target"].get(k, 0.5)))
    why = f"closest target (distance {best_d:.2f}); led by " + ", ".join(f"{k}={feats.get(k)}" for k in parts[:3])
    if feats.get("section") in best.get("sections", []):
        why += f"; names the section ({feats['section']})"
    return best, best_d, why


def events_for(an, t0, t1, payoff, feats, arch, mapping):
    """Orchestration events at song times: the archetype's templates on the shot's own bars, beats
    and hits, split into the setup (before the payoff), the payoff and the release (after it)."""
    out = []
    strength = dict(zip(an["beats"], an["beat_strength"]))
    eps = 0.02   # a cut sits ON a downbeat; the shot's first bar counts (the kit combo's ability 1)
    downbeats = [max(b, t0) for b in an["downbeats"] if t0 - eps <= b < t1]
    beats = [max(b, t0) for b in an["beats"] if t0 - eps <= b < t1]
    med = float(np.median(list(strength.values()))) if strength else 0.5
    strong = [b for b in beats if strength.get(b, 0) >= med]
    c = an["curves"]; hz = c["hz"]; on = np.array(c["onset"]); peak = np.percentile(on, 99)
    onsets = [o for o in an["onsets"] if t0 <= o < t1 and on[min(int(o * hz), len(on) - 1)] / peak >= 0.75]
    before = lambda xs: [x for x in xs if x < payoff - 0.25]
    after = lambda xs: [x for x in xs if x > payoff + 0.25]
    kinds = {"start": [t0], "half": [(t0 + t1) / 2], "bar": downbeats, "beat": beats, "strong": strong, "onset": onsets,
             "setup_bar": before(downbeats), "setup_beat": before(beats), "setup_strong": before(strong),
             "payoff": [payoff],
             "release_bar": after(downbeats), "release_beat": after(beats), "release_strong": after(strong)}
    dirs = ["0,1,0", "1,0,0", "0,-1,0", "-1,0,0"]
    doms = ["Ruby", "Jade", "Gold", "Blue"]
    counters = {}
    loop_s = max(payoff - t0, 2.0)
    for tpl in arch.get("events", []):
        if feats["energy"] < tpl.get("min_energy", 0.0):
            continue
        on_kind = tpl["on"]
        if on_kind.startswith("payoff") and len(on_kind) > 6:
            when = [payoff + float(on_kind[6:])]
        else:
            when = kinds[on_kind]
        for t in when:
            if not (t0 <= t < t1):
                continue
            i = counters.get(tpl["cmd"], 0); counters[tpl["cmd"]] = i + 1
            cmd = (tpl["cmd"].replace("{i}", str(i % 4)).replace("{j}", str(i % 12)).replace("{b}", str(1 + i % 4)).replace("{b3}", str(1 + i % 3))
                   .replace("{dir}", dirs[i % 4]).replace("{dom}", doms[i % 4]).replace("{loop_s}", f"{loop_s:.1f}"))
            out.append({"t": round(float(t), 3), "cmd": cmd, "why": on_kind})
    out.sort(key=lambda e: (e["t"], e["why"] != "start"))
    cap = int(mapping.get("events", {}).get("defaults", {}).get("max_per_second", 8))
    kept, bucket = [], {}
    for e in out:
        k = int(e["t"]); bucket[k] = bucket.get(k, 0) + 1
        if bucket[k] <= cap:
            kept.append(e)
    return kept


def compose(an, mapping, start, end, beats, forced):
    cfg = mapping.get("shots", {"min_seconds": 6, "max_seconds": 16})
    cuts = cut_points_sections(an, start, end, cfg) if beats == "auto" else cut_points_equal(an, start, end, int(beats))
    shots, used_names, used_cells = [], [], []
    tempo = an["tempo"]
    render = dict(mapping.get("render", {}))
    render.setdefault("fps", 60); render.setdefault("step_hz", 240); render.setdefault("accum", 2)
    if end - start > float(render.get("long_seconds", 1e9)) and render.get("render_scale_long"):
        render["render_scale"] = render["render_scale_long"]
        print(f"a {end - start:.0f} s cut: render scale {render['render_scale']}")
    step_hz, out_fps = int(render["step_hz"]), int(render["fps"])
    every = max(1, round(step_hz / out_fps)); accum = min(int(render["accum"]), every)
    for i, (t0, t1) in enumerate(zip(cuts, cuts[1:])):
        feats = beat_features(an, t0, t1)
        arch, dist, why = pick(mapping["archetypes"], feats, mapping["feature_weights"], used_names, used_cells, mapping, forced.get(str(i)))
        used_names.append(arch["name"]); used_cells.append(arch.get("cell"))
        e = feats["energy"]
        thresholds = mapping["intensity_by_energy"]
        intensity = arch.get("intensity", 1 + sum(1 for th in thresholds if e >= th))
        fov = mapping["fov"]["min"] + (mapping["fov"]["max"] - mapping["fov"]["min"]) * feats["brightness"]
        frames = int(round((t1 - t0) * step_hz))
        payoff, payoff_how = payoff_time(an, t0, t1, cfg)
        subs = {"shot_frames": frames, "slow_degps": f"{3 + 2 * e:.0f}", "fast_degps": f"{4:.0f}"}
        camera = [c.format(**subs) for c in arch["camera"]]
        camera.insert(1, f"mv fov {fov:.0f}")
        shot = {
            "name": f"{i + 1:02d}-{arch['name']}",
            "t0": round(t0, 3), "t1": round(t1, 3), "frames": frames, "every": every, "accum": accum,
            "payoff": round(payoff, 3), "payoff_how": payoff_how,
            "mode": arch["mode"], "players": arch.get("players", 4), "intensity": int(intensity),
            "settle_frames": int(arch.get("settle_seconds", 5) * step_hz),
            "stage": arch.get("stage", "arcade"), "cell": arch.get("cell"),
            "setup": ["mv hud off", "mv autopilot on"] + [c.format(**subs) for c in arch.get("stage_commands", [])],
            "camera": camera,
            "events": events_for(an, t0, t1, payoff, feats, arch, mapping),
            "features": feats,
            "archetype": arch["name"], "what": arch["what"], "phrase": arch.get("phrase", {}),
            "why": why,
        }
        shots.append(shot)
        print(f"shot {i + 1}: {t0:.2f}-{t1:.2f} s ({t1 - t0:.1f} s, {feats['section']})  {arch['name']}  payoff {payoff:.2f} ({payoff_how})  "
              f"fov {fov:.0f}  {len(shot['events'])} events  <- {why}")
        ph = arch.get("phrase", {})
        if ph:
            print(f"        setup: {ph.get('setup')}  |  payoff: {ph.get('payoff')}")
    return {
        "song": an["path"], "song_name": an["song"], "tempo": tempo,
        "segment": {"start": start, "end": end}, "fps": out_fps, "render": render,
        "mapping": os.path.relpath(os.path.join(HERE, "mapping.json"), os.getcwd()),
        "shots": shots,
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("analysis")
    ap.add_argument("--start", type=float, required=True)
    ap.add_argument("--end", type=float, required=True)
    ap.add_argument("--beats", default="auto", help="auto (one phrase per section) or a number of equal beats")
    ap.add_argument("--mapping", default=os.path.join(HERE, "mapping.json"))
    ap.add_argument("--force", action="append", default=[], help="SHOTINDEX=ARCHETYPE (0-based) to pin a shot")
    ap.add_argument("--out")
    a = ap.parse_args(argv)
    an = json.load(open(a.analysis))
    mapping = load_mapping(a.mapping)
    forced = dict(f.split("=", 1) for f in a.force)
    plan = compose(an, mapping, a.start, a.end, a.beats, forced)
    stem = os.path.splitext(an['song'])[0].replace(" ", "-")
    out = a.out or os.path.join(HERE, "out", f"{stem}_{a.start:.0f}-{a.end:.0f}", "plan.json")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    json.dump(plan, open(out, "w"), indent=1)
    n = len(plan["shots"]); total = sum(s["t1"] - s["t0"] for s in plan["shots"])
    print(f"{n} shots, mean {total / max(n, 1):.1f} s, {len({s['cell'] for s in plan['shots']})} cells, {sum(len(s['events']) for s in plan['shots'])} events")
    print(f"plan -> {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
