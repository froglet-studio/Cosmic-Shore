#!/usr/bin/env python3
"""Measure a song the way the music-video director reads it.

    python3 Tools/Build/music_video/analyze_song.py SONG [--out analysis.json] [--window 15]

Writes one JSON with everything the composer maps from:
  tempo, beats (s), downbeats (s), onsets (s), sections (start, end, label, energy, brightness,
  onset density, bass, position), a per-frame curve table (43 Hz: rms, centroid, flatness, bass,
  highs, onset strength), and the top candidate WINDOW-second segments ranked by how much
  changes inside them (a build into a drop, a drop into a breakdown) - the segments worth a
  milestone. Every number is in song seconds. Reads anything ffmpeg decodes.

The curves are the raw material; the composer (compose.py) does the mapping. Keeping them
apart means a new mapping never needs the audio re-measured, and a cheaper model can inspect
the JSON instead of the waveform.
"""
import argparse
import json
import os
import sys

import numpy as np

SR = 22050
HOP = 512


def load(path):
    import librosa
    y, sr = librosa.load(path, sr=SR, mono=True)
    return y, sr


def curves(y, sr):
    import librosa
    S = np.abs(librosa.stft(y, n_fft=2048, hop_length=HOP))
    freqs = librosa.fft_frequencies(sr=sr, n_fft=2048)
    power = S ** 2
    total = power.sum(axis=0) + 1e-9
    rms = librosa.feature.rms(S=S, hop_length=HOP)[0]
    centroid = librosa.feature.spectral_centroid(S=S, sr=sr)[0]
    flatness = librosa.feature.spectral_flatness(S=S)[0]
    bass = power[(freqs >= 20) & (freqs < 200)].sum(axis=0) / total
    highs = power[freqs >= 4000].sum(axis=0) / total
    onset = librosa.onset.onset_strength(S=librosa.power_to_db(power), sr=sr, hop_length=HOP)
    n = min(len(rms), len(centroid), len(flatness), len(bass), len(highs), len(onset))
    t = librosa.frames_to_time(np.arange(n), sr=sr, hop_length=HOP)
    return {
        "t": t, "rms": rms[:n], "centroid": centroid[:n], "flatness": flatness[:n],
        "bass": bass[:n], "highs": highs[:n], "onset": onset[:n],
    }


def rhythm(y, sr, onset):
    import librosa
    tempo, beat_frames = librosa.beat.beat_track(onset_envelope=onset, sr=sr, hop_length=HOP, trim=False)
    tempo = float(np.atleast_1d(tempo)[0])
    beats = librosa.frames_to_time(beat_frames, sr=sr, hop_length=HOP)
    # Downbeats: the beat phase (of 4) whose beats carry the most onset strength, on average.
    strength = onset[np.clip(beat_frames, 0, len(onset) - 1)]
    best, best_v = 0, -1.0
    for phase in range(4):
        v = strength[phase::4].mean() if len(strength[phase::4]) else 0.0
        if v > best_v:
            best, best_v = phase, v
    downbeats = beats[best::4]
    onset_frames = librosa.onset.onset_detect(onset_envelope=onset, sr=sr, hop_length=HOP, backtrack=False)
    onsets = librosa.frames_to_time(onset_frames, sr=sr, hop_length=HOP)
    beat_strength = strength / (strength.max() + 1e-9)
    return tempo, beats, downbeats, onsets, beat_strength


def sections(y, sr, c, beats, k=None):
    """Section boundaries from a beat-synchronous self-similarity, agglomerative (librosa)."""
    import librosa
    import scipy
    dur = float(c["t"][-1])
    if k is None:
        k = int(max(4, min(24, round(dur / 16.0))))
    mfcc = librosa.feature.mfcc(y=y, sr=sr, hop_length=HOP, n_mfcc=13)
    chroma = librosa.feature.chroma_cqt(y=y, sr=sr, hop_length=HOP)
    n = min(mfcc.shape[1], chroma.shape[1], len(c["rms"]))
    feat = np.vstack([mfcc[:, :n], chroma[:, :n] * 4, c["rms"][None, :n] / (c["rms"].max() + 1e-9) * 8])
    beat_frames = librosa.time_to_frames(beats, sr=sr, hop_length=HOP)
    beat_frames = beat_frames[(beat_frames > 0) & (beat_frames < n)]
    sync = librosa.util.sync(feat, beat_frames, aggregate=np.mean)
    sync = scipy.stats.zscore(sync, axis=1, nan_policy="omit")
    sync = np.nan_to_num(sync)
    k = min(k, sync.shape[1] - 1)
    bounds = librosa.segment.agglomerative(sync, k)
    bound_frames = beat_frames[np.clip(bounds, 0, len(beat_frames) - 1)]
    times = librosa.frames_to_time(bound_frames, sr=sr, hop_length=HOP)
    times = np.unique(np.concatenate([[0.0], times, [dur]]))
    return times


def summarise(c, start, end, onsets):
    m = (c["t"] >= start) & (c["t"] < end)
    if not m.any():
        return None
    dens = float(((onsets >= start) & (onsets < end)).sum() / max(end - start, 1e-3))
    return {
        "energy": float(c["rms"][m].mean()),
        "peak": float(c["rms"][m].max()),
        "brightness": float(c["centroid"][m].mean()),
        "flatness": float(c["flatness"][m].mean()),
        "bass": float(c["bass"][m].mean()),
        "highs": float(c["highs"][m].mean()),
        "onset_density": dens,
    }


def label_sections(secs, song_energy_pct):
    """intro / build / drop / breakdown / outro from each section's energy against the song's."""
    n = len(secs)
    for i, s in enumerate(secs):
        e = s["energy"]
        pos = (s["start"] + s["end"]) / 2.0 / secs[-1]["end"]
        s["position"] = round(pos, 3)
        prev = secs[i - 1]["energy"] if i > 0 else e
        if i == 0 and e < song_energy_pct[50]:
            lab = "intro"
        elif i == n - 1 and e < song_energy_pct[50]:
            lab = "outro"
        elif e >= song_energy_pct[75]:
            lab = "drop"
        elif e >= song_energy_pct[40] and e > prev * 1.08:
            lab = "build"
        elif e < song_energy_pct[40]:
            lab = "breakdown"
        else:
            lab = "verse"
        s["label"] = lab
    return secs


def candidate_windows(c, secs, win, tempo):
    """Rank WINDOW-second segments by how much changes inside them (the segments worth a milestone)."""
    dur = float(c["t"][-1])
    out = []
    step = 60.0 / tempo if tempo > 0 else 0.5
    starts = np.arange(0.0, max(dur - win, 0.0), step)
    r = c["rms"] / (c["rms"].max() + 1e-9)
    for s in starts:
        e = s + win
        m = (c["t"] >= s) & (c["t"] < e)
        if m.sum() < 10:
            continue
        third = m.sum() // 3
        seg = r[m]
        a, b, d = seg[:third].mean(), seg[third:2 * third].mean(), seg[2 * third:].mean()
        contrast = abs(d - a) + abs(b - a) * 0.5 + abs(d - b) * 0.5
        boundaries = [x for x in [sec["start"] for sec in secs] if s + 2 < x < e - 2]
        score = contrast * 2 + 0.6 * min(len(boundaries), 2) + seg.max() * 0.5
        out.append({"start": round(float(s), 3), "end": round(float(e), 3), "score": round(float(score), 4),
                    "energy_thirds": [round(float(a), 3), round(float(b), 3), round(float(d), 3)],
                    "section_boundaries": [round(float(x), 3) for x in boundaries]})
    out.sort(key=lambda w: -w["score"])
    # keep the best of each neighbourhood
    kept = []
    for w in out:
        if all(abs(w["start"] - k["start"]) > win * 0.6 for k in kept):
            kept.append(w)
        if len(kept) >= 8:
            break
    return kept


def analyse(path, win=15.0):
    y, sr = load(path)
    c = curves(y, sr)
    tempo, beats, downbeats, onsets, beat_strength = rhythm(y, sr, c["onset"])
    times = sections(y, sr, c, beats)
    secs = []
    for a, b in zip(times, times[1:]):
        if b - a < 1.0:
            continue
        s = summarise(c, a, b, onsets)
        if s is None:
            continue
        s.update({"start": round(float(a), 3), "end": round(float(b), 3)})
        secs.append(s)
    pct = {p: float(np.percentile([s["energy"] for s in secs], p)) for p in (25, 40, 50, 75, 90)}
    secs = label_sections(secs, pct)
    curve_table = {
        "hz": round(sr / HOP, 4),
        "rms": [round(float(v), 5) for v in c["rms"]],
        "centroid": [round(float(v), 1) for v in c["centroid"]],
        "flatness": [round(float(v), 5) for v in c["flatness"]],
        "bass": [round(float(v), 5) for v in c["bass"]],
        "highs": [round(float(v), 5) for v in c["highs"]],
        "onset": [round(float(v), 4) for v in c["onset"]],
    }
    return {
        "song": os.path.basename(path),
        "path": os.path.abspath(path),
        "duration": round(float(len(y) / sr), 3),
        "tempo": round(tempo, 2),
        "beats": [round(float(b), 4) for b in beats],
        "beat_strength": [round(float(b), 3) for b in beat_strength],
        "downbeats": [round(float(b), 4) for b in downbeats],
        "onsets": [round(float(o), 4) for o in onsets],
        "song_energy_percentiles": pct,
        "sections": secs,
        "windows": candidate_windows(c, secs, win, tempo),
        "curves": curve_table,
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("song")
    ap.add_argument("--out", help="analysis JSON (default: <out dir>/<song>.analysis.json)")
    ap.add_argument("--window", type=float, default=15.0, help="candidate segment length in seconds")
    ap.add_argument("--brief", action="store_true", help="print the sections and candidate windows only")
    a = ap.parse_args(argv)
    r = analyse(a.song, a.window)
    out = a.out or os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", os.path.splitext(os.path.basename(a.song))[0] + ".analysis.json")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w") as f:
        json.dump(r, f)
    print(f"{r['song']}: {r['duration']:.1f} s, {r['tempo']:.1f} bpm, {len(r['beats'])} beats, {len(r['sections'])} sections -> {out}")
    for s in r["sections"]:
        print(f"  {s['start']:7.2f}-{s['end']:7.2f} {s['label']:<10} energy {s['energy']:.3f} bright {s['brightness']:6.0f} bass {s['bass']:.2f} onsets/s {s['onset_density']:.1f}")
    print("  candidate windows:")
    for w in r["windows"]:
        print(f"    {w['start']:7.2f}-{w['end']:7.2f} score {w['score']:.2f} thirds {w['energy_thirds']} bounds {w['section_boundaries']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
