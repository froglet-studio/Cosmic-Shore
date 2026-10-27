#!/usr/bin/env python3
"""Compose a shot plan from a song's analysis and the mapping.

    python3 Tools/Build/music_video/compose.py ANALYSIS.json --start 41 --end 56 [--beats 3] [--out plan.json]
                                               [--mapping mapping.json] [--fps 30] [--force NAME=ARCHETYPE]

Reads the measured curves (analyze_song.py) and the music -> gameplay mapping (mapping.json),
cuts the segment into beats (equal parts snapped to a section boundary or a downbeat), measures
each beat against the SONG's own range, picks an archetype per beat (closest feature target,
with a penalty for repeating an archetype or a mode so the cut spans the gameplay landscape),
derives intensity, field of view and camera speed from the beat's features, and places accents
(FOV pulses on downbeats, shakes on hard onsets) at exact song times. Writes plan.json for
shoot.py and assemble.py, with the features and the reason behind every choice written next to
each shot so a person (or a cheaper model) can read why.
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
    rms = np.array(c["rms"]); cen = np.array(c["centroid"]); fl = np.array(c["flatness"]); bass = np.array(c["bass"]); on = np.array(c["onset"])
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
    # trend: does the beat rise or fall (slope of rms over the beat, in song energy units)
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


def cut_points(an, start, end, beats, min_len=3.0):
    """Equal parts, each cut snapped to a section boundary within 2 s, else the nearest downbeat within 1.2 s, else the nearest beat."""
    cuts = [start]
    bounds = [s["start"] for s in an["sections"]]
    for i in range(1, beats):
        target = start + (end - start) * i / beats
        cand = [b for b in bounds if abs(b - target) <= 2.0 and b - cuts[-1] >= min_len and end - b >= min_len]
        if cand:
            cut = min(cand, key=lambda b: abs(b - target)); how = "section boundary"
        else:
            cand = [b for b in an["downbeats"] if abs(b - target) <= 1.2 and b - cuts[-1] >= min_len and end - b >= min_len]
            if cand:
                cut = min(cand, key=lambda b: abs(b - target)); how = "downbeat"
            else:
                cand = [b for b in an["beats"] if b - cuts[-1] >= min_len and end - b >= min_len]
                cut = min(cand, key=lambda b: abs(b - target)) if cand else target; how = "beat"
        cuts.append(round(float(cut), 3))
        print(f"cut {i} at {cut:.2f} s ({how})")
    cuts.append(end)
    return cuts


def pick(archetypes, feats, weights, used_names, used_modes, mapping, force=None):
    if force:
        a = next(a for a in archetypes if a["name"] == force)
        return a, 0.0, "forced"
    best, best_d, why = None, 1e9, ""
    for a in archetypes:
        d = 0.0
        for k, w in weights.items():
            d += w * (feats.get(k, 0.5) - a["target"].get(k, 0.5)) ** 2
        d = math.sqrt(d)
        if a["name"] in used_names:
            d += mapping["diversity_penalty"]
        if a["mode"] in used_modes:
            d += mapping["same_mode_penalty"]
        if d < best_d:
            best, best_d = a, d
    parts = sorted(weights, key=lambda k: -weights[k] * abs(feats.get(k, 0.5) - best["target"].get(k, 0.5)))
    why = f"closest target (distance {best_d:.2f}); led by " + ", ".join(f"{k}={feats.get(k)}" for k in parts[:3])
    return best, best_d, why


def accents_for(an, t0, t1, feats, mapping):
    out = []
    acc = mapping["accents"]
    strength = dict(zip(an["beats"], an["beat_strength"]))
    downbeats = set(an["downbeats"])
    for b in an["beats"]:
        if not (t0 <= b < t1):
            continue
        s = strength.get(b, 0.5)
        if b in downbeats and feats["energy"] >= acc["downbeat_pulse"]["min_energy"]:
            out.append({"t": round(b, 3), "cmd": f"pulse {acc['downbeat_pulse']['amount_per_strength'] * s:.1f} {acc['downbeat_pulse']['frames']}", "why": "downbeat"})
        elif feats["energy"] >= acc["beat_pulse"]["min_energy"]:
            out.append({"t": round(b, 3), "cmd": f"pulse {acc['beat_pulse']['amount_per_strength'] * s:.1f} {acc['beat_pulse']['frames']}", "why": "beat"})
    if feats["energy"] >= acc["onset_shake"]["min_energy"]:
        c = an["curves"]; hz = c["hz"]; on = np.array(c["onset"]); peak = np.percentile(on, 99)
        for o in an["onsets"]:
            if t0 <= o < t1 and on[min(int(o * hz), len(on) - 1)] / peak >= acc["onset_shake"]["min_onset"]:
                out.append({"t": round(o, 3), "cmd": f"shake {acc['onset_shake']['amount']} {acc['onset_shake']['frames']}", "why": "hard onset"})
    return out


def compose(an, mapping, start, end, beats, fps, forced):
    cuts = cut_points(an, start, end, beats)
    shots, used_names, used_modes = [], [], []
    tempo = an["tempo"]
    for i, (t0, t1) in enumerate(zip(cuts, cuts[1:])):
        feats = beat_features(an, t0, t1)
        arch, dist, why = pick(mapping["archetypes"], feats, mapping["feature_weights"], used_names, used_modes, mapping, forced.get(str(i)))
        used_names.append(arch["name"]); used_modes.append(arch["mode"])
        e = feats["energy"]
        thresholds = mapping["intensity_by_energy"]
        intensity = arch.get("intensity", 1 + sum(1 for th in thresholds if e >= th))
        fov = mapping["fov"]["min"] + (mapping["fov"]["max"] - mapping["fov"]["min"]) * feats["brightness"]
        frames = int(round((t1 - t0) * 60))
        subs = {"shot_frames": frames, "slow_degps": f"{8 + 12 * e:.0f}", "fast_degps": f"{tempo / 120 * (30 + 50 * e):.0f}"}
        camera = [c.format(**subs) for c in arch["camera"]]
        camera.insert(1, f"mv fov {fov:.0f}")
        shot = {
            "name": f"{i + 1:02d}-{arch['name']}",
            "t0": round(t0, 3), "t1": round(t1, 3), "frames": frames,
            "mode": arch["mode"], "players": arch.get("players", 4), "intensity": int(intensity),
            "settle_frames": arch.get("settle_frames", 300),
            "setup": ["mv hud off", "mv autopilot on"],
            "camera": camera,
            "accents": accents_for(an, t0, t1, feats, mapping),
            "features": feats,
            "archetype": arch["name"], "what": arch["what"],
            "why": why,
        }
        shots.append(shot)
        print(f"beat {i + 1}: {t0:.2f}-{t1:.2f} s  {arch['name']}  I{intensity} fov {fov:.0f}  {feats}  <- {why}")
    return {
        "song": an["path"], "song_name": an["song"], "tempo": tempo,
        "segment": {"start": start, "end": end}, "fps": fps,
        "mapping": os.path.relpath(os.path.join(HERE, "mapping.json"), os.getcwd()),
        "shots": shots,
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("analysis")
    ap.add_argument("--start", type=float, required=True)
    ap.add_argument("--end", type=float, required=True)
    ap.add_argument("--beats", type=int, default=3)
    ap.add_argument("--fps", type=int, default=30)
    ap.add_argument("--mapping", default=os.path.join(HERE, "mapping.json"))
    ap.add_argument("--force", action="append", default=[], help="BEATINDEX=ARCHETYPE (0-based) to pin a beat")
    ap.add_argument("--out")
    a = ap.parse_args(argv)
    an = json.load(open(a.analysis))
    mapping = load_mapping(a.mapping)
    forced = dict(f.split("=", 1) for f in a.force)
    plan = compose(an, mapping, a.start, a.end, a.beats, a.fps, forced)
    stem = os.path.splitext(an['song'])[0].replace(" ", "-")
    out = a.out or os.path.join(HERE, "out", f"{stem}_{a.start:.0f}-{a.end:.0f}", "plan.json")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    json.dump(plan, open(out, "w"), indent=1)
    print(f"plan -> {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
