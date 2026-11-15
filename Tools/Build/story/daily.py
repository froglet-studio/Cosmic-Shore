#!/usr/bin/env python3
"""Run one day of the story flywheel: index the footage, measure, decide the story, cut it.

    python Tools/Build/story/daily.py --root C:\\Claude\\story \\
        --replays <Recordings folder> [--replays ...] --shots C:\\Claude\\trailer\\shots [--shots ...] \\
        [--clips C:\\Claude\\story\\clips] [--flight-moments moments.json] \\
        (--db aggregates.db | --api https://analytics.<vm>  with FROGLET_ANALYTICS_KEY set | --measure measure.json) \\
        [--cut --cut-shots C:\\Claude\\trailer\\shots]
    python3 Tools/Build/story/daily.py --self-test

Writes <root>/days/<YYYY-MM-DD>/ (measure.json, story.json, edl.json, shoot.json, brief.md, run.json and, with
--cut, story-<day>.mp4 and its contact sheet) and keeps the footage index at <root>/footage_index.json, so ratings
and notes on footage carry from day to day. Yesterday is the newest earlier day folder: its story is what today's
is compared with. Each stage is its own script and can be run alone (README).
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import footage  # noqa: E402
import measure as MEAS  # noqa: E402
import story as STORY  # noqa: E402

CUT = os.path.join(os.path.dirname(HERE), "trailer", "cut.py")


def previous_day(days_dir, today):
    if not os.path.isdir(days_dir):
        return None
    earlier = sorted(d for d in os.listdir(days_dir) if d < today and os.path.exists(os.path.join(days_dir, d, "story.json")))
    return os.path.join(days_dir, earlier[-1]) if earlier else None


def run(a, today=None):
    today = today or dt.date.today().isoformat()
    day = os.path.join(a.root, "days", today)
    os.makedirs(day, exist_ok=True)
    log = {"day": today, "started": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"), "stages": []}

    idx_path = os.path.join(a.root, "footage_index.json")
    idx = footage.build(idx_path, a.replays, a.shots, a.flight_moments, a.clips)
    log["stages"].append({"stage": "footage", "counts": idx["counts"], "errors": len(idx["errors"])})

    if a.measure:
        m = json.load(open(a.measure, encoding="utf-8"))
    else:
        m = MEAS.measure(MEAS.Source(a.db, a.api, os.environ.get("FROGLET_ANALYTICS_KEY")), a.days, a.build)
    json.dump(m, open(os.path.join(day, "measure.json"), "w", encoding="utf-8"), indent=1)
    log["stages"].append({"stage": "measure", "source": m.get("source"), "activities": len(m["activities"])})

    cfg = STORY.load_config(a.config)
    prev = previous_day(os.path.join(a.root, "days"), today)
    y = json.load(open(os.path.join(prev, "story.json"), encoding="utf-8")) if prev else None
    s = STORY.decide(m, idx, cfg, y)
    s["day"] = today
    json.dump(s, open(os.path.join(day, "story.json"), "w", encoding="utf-8"), indent=1)
    e = STORY.edl(s, cfg)
    json.dump(e, open(os.path.join(day, "edl.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    json.dump(s["shoot"], open(os.path.join(day, "shoot.json"), "w", encoding="utf-8"), indent=1)
    open(os.path.join(day, "brief.md"), "w", encoding="utf-8").write(STORY.brief(s))
    log["stages"].append({"stage": "story", "headline": s["headline"], "held": s["held_from_yesterday"],
                          "beats": len(s["beats"]), "footage": s["footage"]})

    if a.cut and s["beats"]:
        out = os.path.join(day, f"story-{today}.mp4")
        cmd = [sys.executable, CUT, "--edl", os.path.join(day, "edl.json"), "--shots", a.cut_shots, "--out", out,
               "--sheet", os.path.join(day, f"sheet-{today}.png")]
        r = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8",
                           errors="replace")
        log["stages"].append({"stage": "cut", "ok": r.returncode == 0, "out": out if r.returncode == 0 else None,
                              "tail": r.stdout[-1500:]})
    log["finished"] = dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")
    json.dump(log, open(os.path.join(day, "run.json"), "w", encoding="utf-8"), indent=1)
    return day, log


def self_test() -> int:
    import theater
    tmp = tempfile.mkdtemp(prefix="story-daily-")
    rdir, sdir = os.path.join(tmp, "Recordings"), os.path.join(tmp, "shots")
    os.makedirs(rdir), os.makedirs(sdir)
    open(os.path.join(rdir, "m.cstheater"), "wb").write(theater.write(theater.synthetic()))
    json.dump({"MatchId": "m-1", "GameModeName": "SkimRace"}, open(os.path.join(rdir, "m.replay.json"), "w"))
    for f in ("S01_a.mp4", "S16b_b.mp4", "S09_skim.mp4"):
        open(os.path.join(sdir, f), "wb").write(b"")
    json.dump([{"file": "S09_skim.mp4", "shot": "S09", "vessel_mode": "Squirrel / SkimRace"}],
              open(os.path.join(sdir, "shots.json"), "w"))
    db = os.path.join(tmp, "aggregates.db")
    MEAS._fixture(db)
    args = argparse.Namespace(root=os.path.join(tmp, "story"), replays=[rdir], shots=[sdir], clips=[],
                              flight_moments=None, measure=None, db=db, api=None, days=28, build="*",
                              config=STORY.CONFIG, cut=False, cut_shots=sdir)
    day1, log1 = run(args, "2026-11-12")
    s1 = json.load(open(os.path.join(day1, "story.json")))
    assert s1["headline"]["name"] == "SkimRace", s1["headline"]           # the Toybox has no footage in the fixture
    assert any(x.get("item") for x in s1["shoot"]), "the unfilmed SkimRace replay is on the shoot list"
    for f in ("measure.json", "edl.json", "brief.md", "run.json"):
        assert os.path.exists(os.path.join(day1, f)), f
    day2, log2 = run(args, "2026-11-13")
    assert log2["stages"][2]["held"], "same numbers, same headline, said so"
    shutil.rmtree(tmp)
    print("daily: self-test OK")
    return 0


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root")
    ap.add_argument("--replays", action="append", default=[])
    ap.add_argument("--shots", action="append", default=[])
    ap.add_argument("--clips", action="append", default=[])
    ap.add_argument("--flight-moments")
    ap.add_argument("--measure", help="use this measure.json instead of measuring")
    ap.add_argument("--db")
    ap.add_argument("--api")
    ap.add_argument("--days", type=int, default=28)
    ap.add_argument("--build", default="*")
    ap.add_argument("--config", default=STORY.CONFIG)
    ap.add_argument("--cut", action="store_true")
    ap.add_argument("--cut-shots", help="the folder cut.py resolves shot names in (the opener and closer)")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        return self_test()
    if not a.root or not (a.measure or a.db or a.api):
        ap.error("--root and one of --measure, --db, --api")
    if a.cut and not a.cut_shots:
        a.cut_shots = a.shots[0] if a.shots else None
    day, log = run(a)
    print(open(os.path.join(day, "brief.md"), encoding="utf-8").read())
    print(json.dumps(log["stages"], indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
