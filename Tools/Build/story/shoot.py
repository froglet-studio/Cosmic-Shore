#!/usr/bin/env python3
"""Turn the day's shoot list into a script for the Unity trailer director, so real play is re-filmed with the
trailer's look (post, fog, the dark sea, 1080p60) instead of the replay viewer's.

    python3 Tools/Build/story/shoot.py --day <root>/days/<date> --footage <root>/footage_index.json \
        --out <trailer dir>/inbox/story-<date>.txt [--recordings C:/path/to/Recordings] [--max 6]
    python3 Tools/Build/story/shoot.py --self-test

The director (`Assets/_Scripts/Editor/Trailer/TrailerShoot.cs`, FrogletTools > Build > Trailer Shoot: Start Director)
runs every `*.txt` dropped in `<trailer dir>/inbox/`. Per moment this writes: `replay FILE START` (the match in its
real scene, fast-forwarded), `hud off`, a chase on the recording's own pilot, `record <clip name> FRAMES`, then
`menu`. Frames land in `<trailer dir>/raw/<clip name>/`; encode them to `<root>/clips/<clip name>.mp4` and the next
footage index marks the item showable (a clip named after the item's id is that item's clip).

Only replays are filmed this way. A flight-record moment first needs `Tools/Build/highlights/flight_to_replay.py`.
The chase follows `local`, the seat the replay gives the recording's first human, so moments of that pilot come first.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

PRE, POST = 1.5, 1.0          # seconds of lead-in and tail around a moment's window
FPS = 60
CHASE = "chase local 14 3 0"   # back, up, side: the trailer's hero chase


def first_human(item):
    for p in item.get("pilots", []):
        if not p.get("ai"):
            return p.get("name")
    return None


def plan(shoot, index, recordings=None, max_clips=6):
    by_id = {it["id"]: it for it in index.get("items", [])}
    clips = []
    for entry in shoot:
        it = by_id.get(entry.get("item"))
        if not it or it.get("kind") != "replay" or it.get("showable"):
            continue
        hero = first_human(it)
        moments = sorted(it.get("moments", []),
                         key=lambda m: (m.get("pilot") != hero and hero not in (m.get("also") or []), -m.get("score", 0)))
        if not moments:
            continue
        m = moments[0]
        start = max(0.0, m["start"] - PRE)
        frames = int(round((m["end"] + POST - start) * FPS))
        name = os.path.basename(it["path"].replace("\\", "/"))
        clips.append({"clip": it["id"], "file": os.path.join(recordings, name) if recordings else name,
                      "start": round(start, 2), "frames": frames, "mode": it.get("mode"), "pilot": hero,
                      "why": entry.get("why"), "events": [e["type"] for e in m.get("events", [])]})
        if len(clips) >= max_clips:
            break
    return clips


def script(clips):
    lines = ["# story flywheel shoot list: one recorded match per block (Tools/Build/story/shoot.py)"]
    for c in clips:
        lines += [f"# {c['clip']}: {c['mode']}, {c['pilot']}, {' + '.join(c['events'])}",
                  f"replay {c['file'].replace(chr(92), '/')} {c['start']}",
                  "hud off", CHASE, f"record {c['clip']} {c['frames']}", "menu"]
    return "\n".join(lines) + "\n"


def self_test() -> int:
    index = {"items": [
        {"id": "replay-a", "kind": "replay", "path": "/x/Recordings/Kelpwood_1.cstheater", "mode": "Kelpwood",
         "pilots": [{"name": "Kyoki", "ai": False}, {"name": "ys1", "ai": False}], "showable": False,
         "moments": [{"pilot": "ys1", "start": 10, "end": 15, "score": 3, "events": [{"type": "stunt"}]},
                     {"pilot": "Kyoki", "start": 25, "end": 30, "score": 1, "events": [{"type": "near_pass"}]}]},
        {"id": "replay-b", "kind": "replay", "path": "/x/Recordings/Done.cstheater", "showable": True, "moments": []},
        {"id": "flight-c", "kind": "flight", "path": "f.json"}]}
    shoot = [{"item": "replay-a"}, {"item": "replay-b"}, {"item": "flight-c"}, {"activity": "Joust"}]
    clips = plan(shoot, index)
    assert [c["clip"] for c in clips] == ["replay-a"], clips          # filmed and flight items are skipped
    assert clips[0]["start"] == 23.5 and clips[0]["frames"] == int((31 - 23.5) * 60), clips   # the hero's moment
    text = script(plan(shoot, index, recordings="C:\\R"))
    assert "replay C:/R/Kelpwood_1.cstheater 23.5" in text and "record replay-a 450" in text, text
    print("shoot: self-test OK")
    return 0


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--day")
    ap.add_argument("--footage")
    ap.add_argument("--out")
    ap.add_argument("--recordings", help="the folder FILE is resolved in; default: the Unity project's Recordings")
    ap.add_argument("--max", type=int, default=6)
    ap.add_argument("--best", action="store_true",
                    help="no day folder: film the best unfilmed replays by moment score (builds the library early)")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        return self_test()
    if not (a.footage and a.out and (a.day or a.best)):
        ap.error("--footage, --out and --day (or --best)")
    index = json.load(open(a.footage, encoding="utf-8"))
    if a.best:
        replays = sorted((it for it in index["items"] if it["kind"] == "replay" and not it.get("showable")),
                         key=lambda it: -(it.get("best_score") or 0))
        shoot = [{"item": it["id"], "why": "best real moments"} for it in replays]
    else:
        shoot = json.load(open(os.path.join(a.day, "shoot.json"), encoding="utf-8"))
    clips = plan(shoot, index, a.recordings, a.max)
    open(a.out, "w", encoding="utf-8").write(script(clips))
    plan_path = os.path.join(a.day, "shoot_plan.json") if a.day else os.path.splitext(a.out)[0] + ".plan.json"
    json.dump(clips, open(plan_path, "w", encoding="utf-8"), indent=1)
    print(f"{len(clips)} clips -> {a.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
