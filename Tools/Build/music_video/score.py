#!/usr/bin/env python3
"""Score a cut against its song: the loss the mapping is improved against.

    python3 Tools/Build/music_video/score.py PLAN.json [--frames DIR] [--analysis A.json] [--out scorecard.json]

Measures, from the recorded frames and the song's curves, what a viewer would feel without
being told:

  motion_vs_energy   Pearson correlation between on-screen motion (mean |frame - previous|) and
                     the song's loudness (rms) over the whole segment. Pictures that move when
                     the music moves.
  motion_vs_onsets   The same against the onset envelope: accents landing on hits.
  cut_alignment      Every cut's distance to the nearest beat, in beats (0 = on the beat).
  luminance_vs_brightness  Rank agreement between each shot's mean luminance and its beat's
                     spectral brightness (bright bars read bright).
  dead_frames        Fraction of frames with nearly no detail (a camera inside a wall, or
                     nothing in shot) - a hard floor: a dead frame is wrong whatever the music.
  stuck_frames       Fraction of frames with nearly no motion in a beat whose energy is high.
  diversity          Distinct scenes / shots (the secondary goal: span the landscape).
  shot_seconds       Mean shot length: a shot needs room for a setup and a payoff (Garrett, cut 5).

Each criterion gets a leaky-ReLU shaped penalty: nothing to pay above its floor (just a small
slope that still rewards doing better), a steep slope below it. The scorecard keeps the raw
criteria AND the penalties, so two cuts are compared on the Pareto front of the criteria
first (a cut that is better on every count wins outright) and only then on the summed loss -
a single scalar hides which thing got worse, and the person's feedback usually names one.
The weights and floors are in mapping.json's "loss" block when present, else the defaults here.
"""
import argparse
import csv
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))

DEFAULT_LOSS = {
    "floors": {"motion_vs_energy": 0.3, "motion_vs_onsets": 0.15, "cut_alignment": 0.15, "luminance_vs_brightness": 0.0,
               "dead_frames": 0.02, "stuck_frames": 0.1, "diversity": 0.9, "shot_seconds": 8.0},
    "weights": {"motion_vs_energy": 1.0, "motion_vs_onsets": 0.6, "cut_alignment": 1.0, "luminance_vs_brightness": 0.4,
                "dead_frames": 2.0, "stuck_frames": 0.8, "diversity": 0.8, "shot_seconds": 0.1},
    "higher_is_better": {"motion_vs_energy": True, "motion_vs_onsets": True, "cut_alignment": False, "luminance_vs_brightness": True,
                         "dead_frames": False, "stuck_frames": False, "diversity": True, "shot_seconds": True},
    "leak": 0.1, "steep": 4.0,
}


def leaky(value, floor, higher_is_better, leak, steep):
    """0 at the floor; a gentle negative (reward) beyond it, a steep positive penalty short of it."""
    d = (value - floor) if higher_is_better else (floor - value)
    return -leak * d if d >= 0 else -steep * d


def frame_series(frames_dir):
    """(song-relative game time, luminance mean, luminance std, motion) per recorded frame."""
    from PIL import Image
    p = os.path.join(frames_dir, "frames.csv")
    if not os.path.exists(p):
        return []
    rows = list(csv.DictReader(open(p)))
    out, prev = [], None
    video = os.path.join(frames_dir, "shot.mp4")
    if rows and not os.path.exists(os.path.join(frames_dir, rows[0]["file"])) and os.path.exists(video):
        # the PNGs were encoded and deleted as they landed (shoot.py): decode the shot small and grey
        import shutil, subprocess
        raw = subprocess.run([shutil.which("ffmpeg") or "ffmpeg", "-loglevel", "error", "-i", video, "-vf", "scale=160:90", "-f", "rawvideo", "-pix_fmt", "gray", "-"],
                             capture_output=True, check=True).stdout
        ims = np.frombuffer(raw, dtype=np.uint8).reshape(-1, 90, 160).astype(np.float32) / 255.0
        for r, im in zip(rows, ims):
            motion = float(np.abs(im - prev).mean()) if prev is not None else 0.0
            out.append((float(r["time"]), float(im.mean()), float(im.std()), motion))
            prev = im
        return out
    for r in rows:
        f = os.path.join(frames_dir, r["file"])
        if not os.path.exists(f):
            continue
        im = np.asarray(Image.open(f).convert("L").resize((160, 90)), dtype=np.float32) / 255.0
        motion = float(np.abs(im - prev).mean()) if prev is not None else 0.0
        out.append((float(r["time"]), float(im.mean()), float(im.std()), motion))
        prev = im
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("plan")
    ap.add_argument("--frames")
    ap.add_argument("--analysis")
    ap.add_argument("--out")
    a = ap.parse_args(argv)
    plan = json.load(open(a.plan))
    pdir = os.path.dirname(os.path.abspath(a.plan))
    root = a.frames or os.path.join(pdir, "frames")
    an_path = a.analysis or os.path.join(HERE, "out", os.path.splitext(plan["song_name"])[0] + ".analysis.json")
    an = json.load(open(an_path))
    mapping = json.load(open(os.path.join(HERE, "mapping.json")))
    loss_cfg = {**DEFAULT_LOSS, **mapping.get("loss", {})}
    c = an["curves"]; hz = c["hz"]
    rms = np.array(c["rms"]); onset = np.array(c["onset"]); rms_n = rms / (rms.max() + 1e-9); on_n = onset / (onset.max() + 1e-9)

    times, motions, lums, stds, shot_rows = [], [], [], [], []
    for shot in plan["shots"]:
        series = frame_series(os.path.join(root, shot["name"]))
        if not series:
            shot_rows.append({"name": shot["name"], "frames": 0})
            continue
        g0 = series[0][0]
        st = [shot["t0"] + (g - g0) for g, _, _, _ in series]
        m = [x[3] for x in series][1:]; st_m = st[1:]
        lum = float(np.mean([x[1] for x in series])); det = [x[2] for x in series]
        dead = float(np.mean([d < 0.02 for d in det]))
        e = shot["features"]["energy"]
        stuck = float(np.mean([mm < 0.004 for mm in m])) if e >= 0.5 and m else 0.0
        times += st_m; motions += m; lums.append(lum); stds.append(float(np.mean(det)))
        shot_rows.append({"name": shot["name"], "frames": len(series), "mean_luminance": round(lum, 4), "mean_detail": round(float(np.mean(det)), 4),
                          "mean_motion": round(float(np.mean(m)), 4) if m else 0.0, "dead_frames": round(dead, 3), "stuck_frames": round(stuck, 3),
                          "brightness": shot["features"]["brightness"], "energy": e})
    crit = {}
    if len(times) > 8:
        idx = np.clip((np.array(times) * hz).astype(int), 0, len(rms_n) - 1)
        mo = np.array(motions)
        # a cut is itself a huge "motion" sample; drop the first frame of each shot's pairs (already dropped) and clip outliers
        mo = np.minimum(mo, np.percentile(mo, 98))
        crit["motion_vs_energy"] = float(np.corrcoef(mo, rms_n[idx])[0, 1]) if mo.std() > 0 else 0.0
        # onsets: compare motion's own onset (positive derivative) with the onset envelope
        dmo = np.maximum(np.diff(mo, prepend=mo[0]), 0)
        crit["motion_vs_onsets"] = float(np.corrcoef(dmo, on_n[idx])[0, 1]) if dmo.std() > 0 else 0.0
    else:
        crit["motion_vs_energy"] = 0.0; crit["motion_vs_onsets"] = 0.0
    beats = np.array(an["beats"]); period = 60.0 / an["tempo"]
    cuts = [s["t0"] for s in plan["shots"][1:]]
    crit["cut_alignment"] = float(np.mean([np.abs(beats - ct).min() / period for ct in cuts])) if cuts else 0.0
    rows = [r for r in shot_rows if r.get("frames")]
    if len(rows) >= 2:
        from scipy.stats import spearmanr
        r, _ = spearmanr([x["mean_luminance"] for x in rows], [x["brightness"] for x in rows])
        crit["luminance_vs_brightness"] = float(0.0 if np.isnan(r) else r)
    else:
        crit["luminance_vs_brightness"] = 0.0
    crit["dead_frames"] = float(np.mean([x["dead_frames"] for x in rows])) if rows else 1.0
    crit["stuck_frames"] = float(np.mean([x["stuck_frames"] for x in rows])) if rows else 1.0
    crit["diversity"] = len({s.get("archetype", s["mode"]) for s in plan["shots"]}) / max(len(plan["shots"]), 1)
    crit["shot_seconds"] = float(np.mean([s["t1"] - s["t0"] for s in plan["shots"]])) if plan["shots"] else 0.0
    penalties = {k: round(leaky(v, loss_cfg["floors"][k], loss_cfg["higher_is_better"][k], loss_cfg["leak"], loss_cfg["steep"]) * loss_cfg["weights"][k], 4)
                 for k, v in crit.items()}
    total = round(sum(penalties.values()), 4)
    below = [k for k, v in penalties.items() if v > 0]
    card = {"plan": os.path.abspath(a.plan), "criteria": {k: round(v, 4) for k, v in crit.items()}, "penalties": penalties, "loss": total,
            "below_floor": below, "shots": shot_rows}
    out = a.out or os.path.join(pdir, "scorecard.json")
    json.dump(card, open(out, "w"), indent=1)
    for k, v in crit.items():
        print(f"{k:<26} {v:7.3f}   floor {loss_cfg['floors'][k]:<5} penalty {penalties[k]:+.3f}")
    print(f"loss {total:+.3f}   below floor: {', '.join(below) or 'nothing'}")
    for r in shot_rows:
        print(f"  {r['name']}: {r}")
    print(f"scorecard -> {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
