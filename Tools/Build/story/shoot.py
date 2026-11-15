#!/usr/bin/env python3
"""Turn the day's shoot list into a script for the Unity trailer director, so real play is re-filmed with the
trailer's look (post, fog, the dark sea, 1080p60) instead of the replay viewer's.

    python3 Tools/Build/story/shoot.py --day <root>/days/<date> --footage <root>/footage_index.json \
        --out <trailer dir>/inbox/story-<date>.txt [--recordings C:/path/to/Recordings] [--max 8]
    python3 Tools/Build/story/shoot.py --best --footage ... --out ...    # the best unfilmed moments, all four elements
    python3 Tools/Build/story/shoot.py --self-test

The director (`Assets/_Scripts/Editor/Trailer/TrailerShoot.cs`, FrogletTools > Build > Trailer Shoot: Start Director)
runs every `*.txt` dropped in `<trailer dir>/inbox/`. Frames land in `<trailer dir>/raw/<clip name>/`; encode them
to `<root>/clips/<clip name>.mp4` and the next footage index marks the item showable for that element.

The shot rules (Garrett, 2026-11-12, after the first real reel: "the camera was way too close up so all I could see
was a vessel. we need more slow motion, wide shots ... show pvp, maneuvering, destruction, and creation"):

1. Every video shows the four ELEMENTS: PvP, maneuvering, destruction and creation. Moments are tagged with them
   (`moments.py`) and picked round-robin, so a shoot list covers all four before it takes a second of any.
2. Each element has its own camera, read from `story_config.json` `camera`: wide three-quarters for PvP and
   destruction, a crane pulling up and back for creation (the trail being drawn), a loose chase for maneuvering.
3. Nothing is filmed tight: a chase sits at least `min_chase_back` behind, every other camera at least
   `min_distance` away (`check_camera` refuses a closer one).
4. Slow motion is real: the director steps less game time per frame (`slowmo N`), so a 2.5 s raid plays as 10 s with
   every frame drawn. The moment's peak sits in the middle of the window.

Only replays are filmed this way. A flight-record moment first needs `Tools/Build/highlights/flight_to_replay.py`.
The camera follows the moment's own pilot: `local` when it is the recording's first human (the seat the replay gives
the local player), `pilot:NAME` for any other recorded pilot.
"""
from __future__ import annotations

import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from moments import ELEMENTS  # noqa: E402

FPS = 60
PVP_MAX_DIST = 30.0   # a pass farther apart than this is two trails near each other on screen, not a duel
LEAD = 0.85   # game seconds the director lets play after the start point before recording (TrailerShoot.Replay)
CONFIG = os.path.join(HERE, "story_config.json")
DEFAULT_CAMERA = {
    "min_distance": 40, "min_chase_back": 28,
    "pvp": {"verbs": ["ots local {rival} 30 8 6 2"], "slowmo": 3, "game_seconds": 3.0},
    "maneuvering": {"verbs": ["tq local 40 90 8 0 3"], "slowmo": 2, "game_seconds": 3.5},
    "destruction": {"verbs": ["tq local 52 -40 18 6"], "slowmo": 4, "game_seconds": 2.5},
    "creation": {"verbs": ["crane local 0,18,-42 0,95,-180 {frames} 3"], "slowmo": 1, "game_seconds": 6.0},
}


def load_camera(path=CONFIG):
    cam = dict(DEFAULT_CAMERA)
    if path and os.path.exists(path):
        cam.update(json.load(open(path, encoding="utf-8")).get("camera") or {})
    check_camera(cam)
    return cam


def check_camera(cam):
    """Rule 3: refuse a camera closer than the minimums. A crane's farthest point counts."""
    for el in ELEMENTS:
        rule = cam.get(el)
        if not rule:
            raise SystemExit(f"story camera: no rule for {el}")
        for verb in rule["verbs"]:
            w = verb.split()
            if w[0] in ("chase", "ots") and float(w[2 if w[0] == "chase" else 3]) < cam["min_chase_back"]:
                raise SystemExit(f"story camera: {el} chases {w[2]} back, closer than {cam['min_chase_back']}")
            if w[0] in ("tq", "orbit") and float(w[2]) < cam["min_distance"]:
                raise SystemExit(f"story camera: {el} {w[0]} at {w[2]}, closer than {cam['min_distance']}")
            if w[0] == "crane":
                far = max(sum(float(c) ** 2 for c in w[k].split(",")) ** 0.5 for k in (2, 3))
                if far < cam["min_distance"]:
                    raise SystemExit(f"story camera: {el} crane never gets {cam['min_distance']} away")


def _hero(item):
    for p in item.get("pilots", []):
        if not p.get("ai"):
            return p.get("name")
    return None


def _duel(m):
    """PvP that reads on screen: two pilots within PVP_MAX_DIST, a close finish, or a raid on a rival's wall."""
    for e in m.get("events", []):
        if e["type"] == "near_pass" and (e.get("dist") is None or e["dist"] <= PVP_MAX_DIST):
            return True
        if e["type"] in ("close_finish", "raid"):
            return True
    return False


def candidates(items, prefer=()):
    """(score, item, moment, element) for every replay moment and every element it shows, not yet filmed.
    Items in `prefer` (the day's shoot list) count double."""
    out = []
    for it in items:
        if it.get("kind") != "replay":
            continue
        filmed = {c.get("element") for c in it.get("clips", [])}
        hero = _hero(it)
        for m in it.get("moments", []):
            mine = m.get("pilot") == hero or hero in (m.get("also") or [])
            for rank, el in enumerate(m.get("elements", [])):
                if el in filmed:
                    continue
                if el == "pvp" and not _duel(m):
                    continue
                # the element a moment is filmed as first counts fully; the hero pilot's moments come first
                weight = (1.0 if rank == 0 else 0.6) * (1.0 if mine else 0.8) * (2.0 if it["id"] in prefer else 1.0)
                out.append((m.get("score", 0) * weight, it, m, el))
    return sorted(out, key=lambda c: -c[0])


def plan(items, cam, recordings=None, max_clips=8, why=None):
    """Round-robin over the elements so the list covers all four before it repeats one (rule 1)."""
    pool = candidates(items, prefer=set(why or ()))
    clips, used_moments, used_pairs = [], set(), set()
    while len(clips) < max_clips:
        took = False
        for el in ELEMENTS:
            if len(clips) >= max_clips:
                break
            pick = next((c for c in pool if c[3] == el and (c[1]["id"], c[2]["t"]) not in used_moments
                         and (c[1]["id"], el) not in used_pairs), None)
            if not pick:
                continue
            _, it, m, _ = pick
            used_moments.add((it["id"], m["t"]))
            used_pairs.add((it["id"], el))
            rule = cam[el]
            g = float(rule["game_seconds"])
            start = round(max(0.0, m["t"] - g / 2 - LEAD), 2)
            frames = int(round(g * FPS * float(rule["slowmo"])))
            name = os.path.basename(it["path"].replace("\\", "/"))
            # the camera follows the moment's own pilot: local flies the first human, every other seat is named
            pilot = m.get("pilot")
            subject = "local" if not pilot or pilot == _hero(it) or " " in pilot else f"pilot:{pilot}"
            # a duel is filmed over the moment's pilot's shoulder at the one it passed (the local seat flies the first human)
            rival = next((e.get("with") for e in m.get("events", []) if e.get("with") and " " not in e["with"]), None)
            pair = f"pair:{subject},pilot:{rival}" if rival else subject
            other = ("local" if rival == _hero(it) else f"pilot:{rival}") if rival else "nearest"
            clips.append({"clip": f"{it['id']}-{el}", "item": it["id"], "element": el,
                          "file": os.path.join(recordings, name) if recordings else name,
                          "start": start, "frames": frames, "slowmo": rule["slowmo"],
                          "verbs": [v.replace("{frames}", str(frames)).replace("{pair}", pair).replace("{rival}", other)
                                    .replace(" local ", f" {subject} ") for v in rule["verbs"]],
                          "mode": it.get("mode"), "pilot": m.get("pilot"), "why": (why or {}).get(it["id"]),
                          "events": [e["type"] for e in m.get("events", [])]})
            took = True
        if not took:
            break
    missing = [el for el in ELEMENTS if not any(c["element"] == el for c in clips)]
    return clips, missing


def script(clips):
    lines = ["# story flywheel shoot list: one moment per block, each element with its own camera (Tools/Build/story/shoot.py)"]
    for c in clips:
        lines += [f"# {c['clip']}: {c['element']}, {c['mode']}, {c['pilot']}, {' + '.join(c['events'])}",
                  f"replay {c['file'].replace(chr(92), '/')} {c['start']}", "hud off", f"slowmo {c['slowmo']}",
                  *c["verbs"], f"record {c['clip']} {c['frames']}", "slowmo 1", "menu"]
    return "\n".join(lines) + "\n"


def self_test() -> int:
    cam = dict(DEFAULT_CAMERA)
    check_camera(cam)
    def mom(pilot, t, score, els, ev):
        return {"pilot": pilot, "t": t, "start": t - 3, "end": t + 2, "score": score, "elements": els,
                "events": [{"type": ev}]}
    index = {"items": [
        {"id": "replay-a", "kind": "replay", "path": "/x/Recordings/Kelpwood_1.cstheater", "mode": "Kelpwood",
         "pilots": [{"name": "Kyoki", "ai": False}, {"name": "ys1", "ai": False}],
         "moments": [mom("Kyoki", 20, 5, ["pvp"], "near_pass"), mom("Kyoki", 30, 4, ["pvp"], "near_pass"),
                     mom("Kyoki", 40, 3, ["destruction", "pvp"], "raid"), mom("Kyoki", 50, 2, ["maneuvering"], "stunt")]},
        {"id": "replay-b", "kind": "replay", "path": "/x/Recordings/Jack.cstheater", "mode": "Jackstraw",
         "pilots": [{"name": "Kyoki", "ai": False}], "clips": [{"path": "c.mp4", "element": "maneuvering"}],
         "moments": [mom("Kyoki", 10, 9, ["maneuvering"], "stunt"), mom("Kyoki", 15, 1, ["creation"], "create")]},
        {"id": "flight-c", "kind": "flight", "path": "f.json"}]}
    clips, missing = plan(index["items"], cam, max_clips=4)
    assert [c["element"] for c in clips] == list(ELEMENTS), [c["element"] for c in clips]   # all four first
    assert not missing
    assert all(c["item"] != "replay-b" or c["element"] != "maneuvering" for c in clips), "filmed already"
    d = next(c for c in clips if c["element"] == "destruction")
    assert d["start"] == round(40 - 1.25 - LEAD, 2) and d["frames"] == int(2.5 * 60 * 4), d   # peak centred, 4x slower
    text = script(plan(index["items"], cam, recordings="C:\\R", max_clips=4)[0])
    assert f"replay C:/R/Kelpwood_1.cstheater {round(18.5 - LEAD, 2)}" in text and "slowmo 3" in text, text
    assert "record replay-a-pvp 540" in text and "crane local 0,18,-42 0,95,-180 360 3" in text, text   # {frames} filled
    assert "ots local nearest 30" in text, text                     # a pass with nobody named frames the pilot alone
    # rule 3: a tight chase is refused (negative control)
    tight = dict(cam, maneuvering={"verbs": ["chase local 14 3 0"], "slowmo": 1, "game_seconds": 3})
    try:
        check_camera(tight)
        raise AssertionError("a 14-unit chase passed")
    except SystemExit:
        pass
    # an element with no moment is reported missing, not faked
    clips, missing = plan([index["items"][0]], cam, max_clips=8)
    assert missing == ["creation"], missing
    # a moment of the second human follows that pilot by name, not the local seat
    index["items"][0]["moments"].append(mom("ys1", 60, 8, ["creation"], "create"))
    text = script(plan(index["items"], cam, max_clips=4)[0])
    assert "crane pilot:ys1 0,18,-42" in text, text
    # a duel frames both pilots
    index["items"][0]["moments"][0]["events"] = [{"type": "near_pass", "with": "ys1"}]
    text = script(plan(index["items"], cam, max_clips=4)[0])
    assert "ots local pilot:ys1 30" in text, text
    # a pass 50 units apart is not filmed as a duel (negative control)
    index["items"][0]["moments"][0]["events"] = [{"type": "near_pass", "with": "ys1", "dist": 50.0}]
    assert not any(c["element"] == "pvp" and c["item"] == "replay-a" and "pilot:ys1" in " ".join(c["verbs"])
                   for c in plan(index["items"], cam, max_clips=4)[0])
    print("shoot: self-test OK")
    return 0


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--day")
    ap.add_argument("--footage")
    ap.add_argument("--out")
    ap.add_argument("--config", default=CONFIG)
    ap.add_argument("--recordings", help="the folder FILE is resolved in; default: the Unity project's Recordings")
    ap.add_argument("--max", type=int, default=8)
    ap.add_argument("--best", action="store_true",
                    help="no day folder: film the best unfilmed moments of every replay, all four elements")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        return self_test()
    if not (a.footage and a.out and (a.day or a.best)):
        ap.error("--footage, --out and --day (or --best)")
    index = json.load(open(a.footage, encoding="utf-8"))
    cam = load_camera(a.config)
    items, why = index["items"], {}
    if not a.best:
        shoot = json.load(open(os.path.join(a.day, "shoot.json"), encoding="utf-8"))
        wanted = {e["item"]: e.get("why") for e in shoot if e.get("item")}
        why = wanted   # the day's list counts double; the library tops up an element it cannot show
    clips, missing = plan(items, cam, a.recordings, a.max, why)
    open(a.out, "w", encoding="utf-8").write(script(clips))
    plan_path = os.path.join(a.day, "shoot_plan.json") if a.day else os.path.splitext(a.out)[0] + ".plan.json"
    json.dump({"clips": clips, "missing_elements": missing}, open(plan_path, "w", encoding="utf-8"), indent=1)
    print(f"{len(clips)} clips -> {a.out}" + (f"; no moment shows {', '.join(missing)}" if missing else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
