"""Build the Amoebius Genome Lab page: one HTML file plus frame strips and clips, from the search runs.

  python3 -m agenome.report OUT_DIR \
      --island main=/home/user/work/runs/run2,/home/user/work/runs/run3,/home/user/work/runs/run4 \
      --island A=/mnt/project-files/amoebius-genome/runs/A --island B=/mnt/project-files/amoebius-genome/runs/B \
      --seedbank /mnt/project-files/amoebius-genome/runs/seedbank/pareto.json \
      --reels '/home/user/work/targets/reel0*/frames.npy' --holdout reel03,reel05 \
      --stats /mnt/project-files/amoebius-genome/fitness/target_stats_train_v2.npz --hold /mnt/project-files/amoebius-genome/fitness/target_stats_hold_v2.npz

An island is name=dir[,dir...]: the dirs are its runs in order (a restart continues the curve). Every genome shown is
rendered again here and re-scored with the fixed statistics, so the numbers on the page are comparable across
islands and restarts (the search's own fitness also has the critic term, which moves as the critic retrains).

Every genome is also judged (agenome.judge v2: InceptionV3 and MobileNetV2 KID against the training reels 1, 2, 4,
1.0 = the held-out Amoebius source, reels 3 and 5), and the page ranks by the judge since the statistics proved
gameable on 2026-11-10.

Writes OUT_DIR/index.html, OUT_DIR/strips/<id>.jpg (4 frames) and OUT_DIR/clips/<id>.mp4. Publish index.html with
strips/ and clips/ as its files; re-run and republish to the same URL to refresh. Videos are never committed.
"""
import argparse
import glob
import hashlib
import json
import os
import shutil
import subprocess
import time

import numpy as np

from .features import GROUPS, TargetStats, window_features, windows
from .genome import Genome
from .ops import OPS

PICKS = (6, 22, 38, 54)
HERE = os.path.dirname(os.path.abspath(__file__))


def _json(p, default=None):
    try:
        with open(p) as f:
            return json.load(f)
    except Exception:
        return default


def _log(p):
    out = []
    try:
        with open(p) as f:
            for line in f:
                line = line.strip()
                if line.startswith("{"):
                    try:
                        out.append(json.loads(line))
                    except Exception:
                        pass
    except FileNotFoundError:
        pass
    return out


def _gid(g):
    return hashlib.sha1(g.to_bytes()).hexdigest()[:10]


def _lineage(g):
    m = g.meta or {}
    if m.get("lineage"):
        return m["lineage"]
    src = m.get("src") or m.get("source") or ""
    parts = src.replace("\\", "/").split("/")
    if len(parts) >= 2 and parts[0] == "seeds":
        return parts[1]
    return "search"


def _ops(g):
    counts, order = {}, []
    for n in g.nodes:
        if n.op not in counts:
            counts[n.op] = 0
            order.append(n.op)
        counts[n.op] += 1
    tkey = {"T": 0, "S": 1, "A": 2, "C": 3, "P": 4, "H": 5, "O": 6}
    first = {o: i for i, o in enumerate(order)}
    order.sort(key=lambda o: (tkey.get(OPS[o].out, 9), first[o]))
    return [[o, counts[o]] for o in order]


def _mp4(frames, path, crf=25):
    if not shutil.which("ffmpeg"):
        return False
    p = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s",
                          f"{frames.shape[2]}x{frames.shape[1]}", "-r", "15", "-i", "-", "-vf",
                          "scale=512:288:flags=neighbor", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", str(crf),
                          "-movflags", "+faststart", path], stdin=subprocess.PIPE)
    p.communicate(np.ascontiguousarray(frames).tobytes())
    return p.returncode == 0


def _strip(frames, path, picks=PICKS):
    from PIL import Image
    row = np.concatenate([frames[min(i, len(frames) - 1)] for i in picks], 1)
    Image.fromarray(row).save(path, quality=86)


def load_island(name, dirs):
    """Chain the runs of one island: curves continue across restarts; candidates come from every run's front and
    the last run's elites, population and best."""
    log, segs, e0, g0 = [], [], 0, 0
    cands = []
    last_mtime = 0
    for k, d in enumerate(dirs):
        L = _log(os.path.join(d, "log.jsonl"))
        if L:
            segs.append(e0)
            for r in L:
                log.append([r["evals"] + e0, r["best_dist"], r["best_stat"], 1 if r["regime"] == "COMPRESS" else 0,
                            r.get("median_zbytes", 0), r["gen"] + g0, r.get("best_zbytes", 0)])
            e0 += L[-1]["evals"]
            g0 += L[-1]["gen"] + 1
            last_mtime = max(last_mtime, os.path.getmtime(os.path.join(d, "log.jsonl")))
        last = k == len(dirs) - 1
        for fname, kind in (("pareto.json", "front"), ("elites.json", "elite"), ("population.json", "pop")):
            if fname != "pareto.json" and not last:
                continue
            data = _json(os.path.join(d, fname), []) or []
            if kind == "pop":
                data = sorted(data, key=lambda c: c.get("judge", c.get("stat", 99)))[:10]
            for c in data:
                cands.append((kind, c))
        if last:
            b = _json(os.path.join(d, "best.json")) or _json(os.path.join(d, "best", "best.json"))
            if b:
                cands.append(("best", {"genome": b}))
    return {"name": name, "log": log, "segments": segs, "gens": g0, "evals": e0,
            "running": (time.time() - last_mtime) < 900 if last_mtime else False,
            "updated": time.strftime("%H:%M UTC", time.gmtime(last_mtime)) if last_mtime else None}, cands


def calibration(hold_frames, stats):
    """What the fixed statistics say about Amoebius itself and about broken versions of it."""
    from scipy.ndimage import uniform_filter
    wins = windows(np.asarray(hold_frames))  # the same windows as the held-out baseline
    rng = np.random.default_rng(0)

    def blur(w):
        return uniform_filter(w.astype(np.float32), size=(1, 9, 9, 1)).astype(np.uint8)

    cases = [
        ("Amoebius, held-out reel", lambda w: w),
        ("Mirrored left to right", lambda w: w[:, :, ::-1].copy()),
        ("Frames shuffled", lambda w: w[rng.permutation(len(w))]),
        ("Hue rotated a third", lambda w: w[..., [1, 2, 0]].copy()),
        ("Frozen on its first frame", lambda w: np.repeat(w[:1], len(w), 0)),
        ("Blurred 9 px", blur),
        ("Average colour only", lambda w: np.broadcast_to(w.reshape(-1, 3).mean(0).astype(np.uint8), w.shape).copy()),
        ("Uniform noise", lambda w: rng.integers(0, 256, w.shape, dtype=np.uint8)),
        ("Black", lambda w: np.zeros_like(w)),
    ]
    out = []
    for label, f in cases:
        ds = [stats.distance(window_features(f(w))) for w in wins]
        out.append({"label": label, "total": float(np.mean([d["total"] for d in ds])),
                    "groups": {g: float(np.mean([d[g] for d in ds])) for g in GROUPS}})
    return out


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--island", action="append", default=[], help="name=dir[,dir...]")
    ap.add_argument("--seedbank", default=None)
    ap.add_argument("--reels", default="/mnt/project-files/amoebius-genome/fitness/reels_256x144/reel0*/frames.npy")
    ap.add_argument("--holdout", default="reel03,reel05", help="held-out reels (one source: reel05 re-cuts reel03)")
    ap.add_argument("--stats", required=True)
    ap.add_argument("--hold", required=True)
    ap.add_argument("--max-genomes", type=int, default=100)
    ap.add_argument("--clips", type=int, default=14)
    ap.add_argument("--ss", type=int, default=1, help="supersampling factor, as the islands use (AGENOME_SS)")
    ap.add_argument("--windows", type=int, default=2,
                    help="4-second windows judged per genome, --hop seconds apart (2 = what the islands climb since "
                         "2026-11-10 19:15): each window scores the worse of the two nets, then the windows average")
    ap.add_argument("--hop", type=float, default=8.0)
    a = ap.parse_args(argv)
    os.environ["AGENOME_SS"] = str(a.ss)

    os.makedirs(os.path.join(a.out, "strips"), exist_ok=True)
    os.makedirs(os.path.join(a.out, "clips"), exist_ok=True)
    stats, hold = TargetStats.load(a.stats), TargetStats.load(a.hold)

    islands, pool = [], []
    for spec in a.island:
        name, dirs = spec.split("=", 1)
        dirs = [d for d in dirs.split(",") if os.path.isdir(d)]
        if not dirs:
            continue
        isl, cands = load_island(name, dirs)
        islands.append(isl)
        pool += [(name, k, c) for k, c in cands]
    if a.seedbank and os.path.exists(a.seedbank):
        for c in (_json(a.seedbank, []) or [])[:40]:
            pool.append(("seed bank", "seed", c))

    # dedupe by genome bytes; fronts and bests first so the cap keeps them
    prio = {"best": 0, "front": 1, "seed": 2, "elite": 3, "pop": 4}
    pool.sort(key=lambda t: (prio[t[1]], t[2].get("stat", t[2].get("dist", 99))))
    shown = {}
    for isl, kind, c in pool:
        try:
            g = Genome.from_json(c["genome"])
        except Exception:
            continue
        gid = _gid(g)
        if gid not in shown:
            if len(shown) >= a.max_genomes:
                continue
            shown[gid] = {"g": g, "islands": [], "kinds": [], "search": c, "cells": []}
        s = shown[gid]
        if isl not in s["islands"]:
            s["islands"].append(isl)
        if kind not in s["kinds"]:
            s["kinds"].append(kind)
        if kind == "elite" and "cell" in c:
            s["cells"].append(c["cell"])

    from .render import Renderer
    from .judge import Judge, sample, calibration as judge_calibration, SHARED
    R = Renderer()
    Js = [Judge(net, "native", 12) for net in ("inception", "mobilenet")]

    def judged(frs):
        """judge v3 on each window: the larger of the two nets' scores, then the mean over the windows (the number
        the search climbs; see search.JudgeState). Returns judge, judge_hold, per-net judge, per-window judge."""
        frs = [frs] if isinstance(frs, np.ndarray) else frs  # one clip (a reel window) or a genome's windows
        clips = [sample(fr, 12) for fr in frs]
        per = [J.score_many(clips) for J in Js]  # per[net][window]
        win = [max(p[k]["judge"] for p in per) for k in range(len(clips))]
        win_h = [max(p[k]["judge_hold"] for p in per) for k in range(len(clips))]
        nets = [float(np.mean([w["judge"] for w in p])) for p in per]
        return (round(float(np.mean(win)), 4), round(float(np.mean(win_h)), 4), [round(x, 3) for x in nets],
                [round(float(x), 3) for x in win])

    genomes, frames_of = [], {}
    t0 = time.time()
    for gid, s in shown.items():
        g = s["g"]
        try:
            fr = R.render(g, 60)
        except Exception as e:
            print("skip", gid, e, flush=True)
            continue
        try:
            later = [R.render(g, 60, t_shift=k * a.hop) for k in range(1, max(1, a.windows))]
        except Exception as e:
            print("skip", gid, e, flush=True)
            continue
        # the stat, held-out stat and groups are means over the same windows as the judge, the way the islands score
        # them since v1.4 (code review 2026-11-10: the page showed window 0 beside a two-window judge)
        dws = [stats.distance(window_features(x)) for x in [fr] + later]
        dhws = [hold.distance(window_features(x)) for x in [fr] + later]
        d = {k: float(np.mean([x[k] for x in dws])) for k in dws[0]}
        dh = {k: float(np.mean([x[k] for x in dhws])) for k in dhws[0]}
        jd, jdh, jdn, jdw = judged([fr] + later)
        _strip(fr, os.path.join(a.out, "strips", gid + ".jpg"))
        frames_of[gid] = fr
        c = s["search"]
        m = g.meta or {}
        genomes.append({
            "id": gid, "name": m.get("name") or "unnamed", "lineage": _lineage(g),
            "src": m.get("src") or "", "from": m.get("immigrant_from"),
            "islands": s["islands"], "kinds": s["kinds"],
            "d": jd, "dh": jdh, "dn": jdn, "dw": jdw,
            "stat": round(d["total"], 4), "hold": round(dh["total"], 4),
            "groups": {k: round(d[k], 3) for k in GROUPS},
            "fit": round(c["dist"], 4) if "dist" in c and "stat" in c and c.get("crit") is not None else None,
            "crit": round(c["crit"], 3) if c.get("crit") is not None else None,
            "zbytes": g.size(), "raw": len(g.to_bytes()), "nodes": len(g.nodes),
            "luma": round(float(fr.mean() / 255.0), 4), "ops": _ops(g), "hex": g.to_bytes().hex(),
            "strip": f"strips/{gid}.jpg", "clip": None, "cells": s["cells"]})
    print(f"rendered {len(genomes)} genomes in {time.time() - t0:.0f}s", flush=True)

    # clips for the closest genomes and for each island's closest
    genomes.sort(key=lambda r: r["d"])
    want = [r["id"] for r in genomes[:a.clips]]
    for isl in [i["name"] for i in islands] + ["seed bank"]:
        mine = [r for r in genomes if isl in r["islands"]]
        if mine and mine[0]["id"] not in want:
            want.append(mine[0]["id"])
    for r in genomes:
        if r["id"] in want and _mp4(frames_of[r["id"]], os.path.join(a.out, "clips", r["id"] + ".mp4")):
            r["clip"] = f"clips/{r['id']}.mp4"

    # the front over (bytes, judge distance) among the genomes shown
    front, best_so_far = [], 1e9
    for r in sorted(genomes, key=lambda r: (r["zbytes"], r["d"])):
        if r["d"] < best_so_far:
            front.append(r["id"])
            best_so_far = r["d"]

    # MAP-Elites cells (log2 bytes x mean luma, as the search bins them), closest genome per cell over all islands
    cells = {}
    for r in genomes:
        zb = min(9, max(0, int(np.log2(max(32, r["zbytes"])) - 5)))
        lu = min(7, max(0, int(r["luma"] * 16)))
        k = f"{zb},{lu}"
        if k not in cells or r["d"] < cells[k]["d"]:
            cells[k] = {"zb": zb, "lu": lu, "id": r["id"], "d": r["d"]}

    # Amoebius reels: a 4 s window from the middle of each
    reels = []
    hold_frames = None
    holdouts = set(a.holdout.split(","))
    for p in sorted(glob.glob(a.reels)):
        name = os.path.basename(os.path.dirname(p))
        fr = np.load(p, mmap_mode="r")
        s0 = max(0, (len(fr) - 60) // 2)
        w = np.asarray(fr[s0:s0 + 60])
        _strip(w, os.path.join(a.out, "strips", name + ".jpg"))
        clip = f"clips/{name}.mp4" if _mp4(w, os.path.join(a.out, "clips", name + ".mp4")) else None
        held = name in holdouts
        if held:
            hold_frames = np.asarray(fr) if hold_frames is None else np.concatenate([hold_frames, np.asarray(fr)])
        reels.append({"name": name, "held": held, "strip": f"strips/{name}.jpg", "clip": clip,
                      "window": [int(s0), int(s0 + 60)], "frames": int(len(fr)),
                      "stat": round(stats.distance(window_features(w))["total"], 3), "d": judged(w)[0]})

    calib = calibration(hold_frames, stats) if hold_frames is not None else []
    # the judge's calibration: cached per net beside the reference features, else computed (a few minutes each)
    per_net = []
    for J in Js:
        cp = os.path.join(SHARED, f"calibration_{J.net}_native_v{J.version}.json")
        cj = _json(cp)
        per_net.append(cj["cases"] if cj else judge_calibration(J))
    # v3 calibration: per window, the larger of the two nets (fitness/judge/calibration_max_native_v3.json); without
    # it, the larger of the nets' window means (a little lower than the mean of window maxima)
    c3 = _json(os.path.join(SHARED, "calibration_max_native_v3.json"))
    if c3:
        jcal = [{"label": r["label"], "judge": round(r["judge"], 3), "judge_hold": round(r["judge_hold"], 3),
                 "nets": [round(x, 3) for x in r["per_net"]]} for r in c3["cases"]]
    else:
        jcal = [{"label": rows[0]["label"], "judge": round(float(max(r["judge"] for r in rows)), 3),
                 "judge_hold": round(float(max(r["judge_hold"] for r in rows)), 3),
                 "nets": [round(r["judge"], 3) for r in rows]} for rows in zip(*per_net)]
    hold_all = [stats.distance(window_features(w)) for w in windows(hold_frames)] if hold_frames is not None else []
    hold_groups = {g: round(float(np.mean([d[g] for d in hold_all])), 3) for g in GROUPS} if hold_all else {}
    hold_total = round(float(np.mean([d["total"] for d in hold_all])), 3) if hold_all else None

    lineages = {}
    for r in genomes:
        L = lineages.setdefault(r["lineage"], {"name": r["lineage"], "n": 0, "best": 99, "best_id": None,
                                               "smallest": 1 << 30})
        L["n"] += 1
        if r["d"] < L["best"]:
            L["best"], L["best_id"] = r["d"], r["id"]
        L["smallest"] = min(L["smallest"], r["zbytes"])

    types = {}
    for o in OPS.values():
        types[o.out] = types.get(o.out, 0) + 1

    data = {
        "updated": time.strftime("%Y-%m-%d %H:%M UTC", time.gmtime()),
        "islands": islands, "genomes": genomes, "front": front, "best": genomes[0]["id"] if genomes else None,
        "cells": list(cells.values()), "reels": reels, "calibration": calib, "jcal": jcal, "level": 1.0,
        "hold_groups": hold_groups, "hold_total": hold_total, "groups": list(GROUPS),
        "lineages": sorted(lineages.values(), key=lambda L: L["best"]),
        "n_ops": len(OPS), "op_types": types,
    }
    with open(os.path.join(HERE, "report_page.html")) as f:
        page = f.read()
    blob = json.dumps(data, separators=(",", ":")).replace("</", "<\\/")
    with open(os.path.join(a.out, "index.html"), "w") as f:
        f.write(page.replace("__DATA__", blob))
    print(os.path.join(a.out, "index.html"), len(genomes), "genomes,", sum(1 for r in genomes if r["clip"]), "clips",
          flush=True)


if __name__ == "__main__":
    main()
