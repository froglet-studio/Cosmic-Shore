#!/usr/bin/env python3
"""The footage library: one index of every piece of play the story pipeline can show, real or staged.

Three sources, each tagged with where it came from, because an honest story says which is which:

| kind     | origin | what it is                                                           | showable as is? |
|----------|--------|----------------------------------------------------------------------|-----------------|
| `replay` | real   | a match recording (.cstheater + .replay.json) from a playtest        | no: re-film it in Unity (TrailerShoot) |
| `flight` | real   | a moment mined from an editor flight record (mine_moments.py output) | no: re-film it (flight_to_replay.py)   |
| `shot`   | staged | a clip filmed in Unity for a trailer (shots.json beside it)          | yes |
| `clip`   | real   | a re-filmed replay or flight moment (`<id>.mp4` or `<id>-<element>.mp4`)| yes |

A replay's moments name their elements (PvP, maneuvering, destruction, creation: `moments.py`); a clip named
`<item id>-<element>.mp4` is that item filmed for that element (`shoot.py`), and the item lists it under `clips`.

    python3 Tools/Build/story/footage.py --index INDEX.json --replays DIR [--replays DIR] --shots DIR [--shots DIR]
                                         [--flight-moments moments.json] [--clips DIR]
    python3 Tools/Build/story/footage.py --index INDEX.json --summary
    python3 Tools/Build/story/footage.py --self-test

The index is rebuilt incrementally: an item whose file has not changed keeps its entry (and anything a person or a
later stage wrote on it: `rating`, `used_in`, `notes`). Paths are stored as given, so an index made on Gbox keeps
its Windows paths and is read there.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import theater  # noqa: E402
import moments as M  # noqa: E402

SCHEMA = "cosmic-shore.footage-index/1"
KEEP = ("rating", "used_in", "notes", "clip")   # written by people or later stages; survive a re-index
TOP_MOMENTS = 8
INDEXER = 5   # bump when a replay's entry would come out differently (moments, times): older entries are re-read


def _id(kind, key):
    return f"{kind}-{hashlib.sha1(key.encode('utf-8')).hexdigest()[:10]}"


def _ticks_to_iso(ticks):
    if not ticks:
        return ""
    try:
        return (dt.datetime(1, 1, 1) + dt.timedelta(microseconds=ticks / 10)).replace(microsecond=0).isoformat() + "Z"
    except OverflowError:
        return ""


def _mtime_iso(path):
    return dt.datetime.fromtimestamp(os.path.getmtime(path), dt.timezone.utc).replace(tzinfo=None).replace(microsecond=0).isoformat() + "Z"


def _probe_seconds(path):
    probe = shutil.which("ffprobe")
    if not probe:
        return None
    r = subprocess.run([probe, "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                       stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    try:
        return round(float(r.stdout.strip()), 2)
    except ValueError:
        return None


def index_replay(path):
    rec = theater.read(path)
    s = rec.summary
    ms = M.moments(rec)
    return {
        "kind": "replay", "origin": "real", "path": path,
        "mode": rec.mode_name, "scene": rec.scene, "intensity": s.get("Intensity"),
        "match_id": rec.match_id,
        "recorded_at": _ticks_to_iso(s.get("RecordedAtUtcTicks") or rec.utc_ticks) or _mtime_iso(path),
        "seconds": round(rec.duration, 1),
        "pilots": [{"name": t.name, "vessel": t.vessel_name, "domain": t.domain, "ai": t.is_ai} for t in rec.tracks],
        "humans": len(rec.humans),
        "ended_because": s.get("EndedBecause", ""),
        "winner": s.get("WinnerName", ""),
        "moments": ms[:TOP_MOMENTS],
        "best_score": ms[0]["score"] if ms else 0.0,
        "elements": sorted({el for m in ms for el in m.get("elements", [])}),
        "has_world": rec.has_world,
        "showable": False,
    }


def index_shots(folder):
    meta = {}
    sj = os.path.join(folder, "shots.json")
    if os.path.exists(sj):
        for s in json.load(open(sj, encoding="utf-8")):
            meta[s.get("file")] = s
    items = []
    for f in sorted(os.listdir(folder)):
        if not f.lower().endswith(".mp4"):
            continue
        path = os.path.join(folder, f)
        m = meta.get(f, {})
        vm = m.get("vessel_mode", "")
        vessel, _, mode = vm.partition(" / ")
        items.append({
            "kind": "shot", "origin": "staged", "path": path, "shot": m.get("shot") or os.path.splitext(f)[0],
            "shows": m.get("shows", ""), "vessel": vessel.strip(), "mode": mode.strip(),
            "best_in": m.get("best_in_s"), "best_out": m.get("best_out_s"), "quality_note": m.get("note", ""),
            "recorded_at": _mtime_iso(path), "seconds": _probe_seconds(path), "showable": True,
        })
    return items


def index_flight_moments(path, top=200):
    data = json.load(open(path, encoding="utf-8"))
    items = []
    for m in (data.get("top") or [])[:top]:
        items.append({
            "kind": "flight", "origin": "real", "path": m.get("path", ""), "flight": m.get("flight", ""),
            "pilot": m.get("pilot", ""), "mode": m.get("mode", ""), "vessel": m.get("hull", ""),
            "start": m.get("start"), "end": m.get("end"), "score": m.get("score"),
            "events": [e.get("type") for e in m.get("events", [])],
            "recorded_at": _flight_time(m.get("flight", "")), "showable": False,
        })
    return items


def _flight_time(name):
    # flight_<Mode>_<Hull>_YYYYMMDD_HHMMSS.json
    parts = os.path.splitext(name)[0].split("_")
    try:
        return dt.datetime.strptime(parts[-2] + parts[-1], "%Y%m%d%H%M%S").isoformat() + "Z"
    except (ValueError, IndexError):
        return ""


def build(index_path, replays=(), shots=(), flight_moments=None, clips=()):
    old = {}
    if index_path and os.path.exists(index_path):
        for it in json.load(open(index_path, encoding="utf-8")).get("items", []):
            old[it["id"]] = it
    items, errors = [], []
    for path in theater.find(replays):
        key = f"{os.path.basename(path)}|{os.path.getsize(path)}"
        iid = _id("replay", key)
        if iid in old and old[iid].get("path") == path and old[iid].get("indexer") == INDEXER:
            items.append(old[iid])
            continue
        try:
            it = index_replay(path)
        except (ValueError, EOFError, OSError) as e:
            errors.append({"path": path, "error": str(e)})
            continue
        it["id"], it["indexer"] = iid, INDEXER
        items.append(it)
    for folder in shots:
        for it in index_shots(folder):
            it["id"] = _id("shot", it["path"].replace("\\", "/").split("/shots")[-1] + "|" + it["shot"])
            items.append(it)
    if flight_moments:
        for it in index_flight_moments(flight_moments):
            it["id"] = _id("flight", f"{it['flight']}|{it['start']}")
            items.append(it)
    clip_files = {}
    for folder in clips:
        for f in sorted(os.listdir(folder)):
            if f.lower().endswith(".mp4"):
                stem = os.path.splitext(f)[0]
                # item ids are "<kind>-<hash>": a third part is the element the clip was filmed for
                iid, element = stem.rsplit("-", 1) if stem.count("-") > 1 else (stem, "")
                clip_files.setdefault(iid, []).append({"path": os.path.join(folder, f), "element": element or None})
    for it in items:
        prev = old.get(it["id"], {})
        for k in KEEP:
            if k in prev and k not in it:
                it[k] = prev[k]
        if it["id"] in clip_files:
            it["clips"] = clip_files[it["id"]]
            it["clip"] = it["clips"][0]["path"]
            it["showable"] = True
    index = {"schema": SCHEMA, "built_at": dt.datetime.now(dt.timezone.utc).replace(tzinfo=None).replace(microsecond=0).isoformat() + "Z",
             "counts": counts(items), "errors": errors, "items": items}
    if index_path:
        with open(index_path, "w", encoding="utf-8") as f:
            json.dump(index, f, indent=1)
    return index


def counts(items):
    c = {}
    for it in items:
        k = f"{it['origin']}:{it['kind']}"
        c[k] = c.get(k, 0) + 1
    c["showable"] = sum(1 for it in items if it.get("showable"))
    return c


def summary(index):
    lines = [f"built {index['built_at']}: " + ", ".join(f"{k} {v}" for k, v in sorted(index["counts"].items()))]
    by_mode = {}
    for it in index["items"]:
        by_mode.setdefault((it.get("mode") or "?", it["origin"]), 0)
        by_mode[(it.get("mode") or "?", it["origin"])] += 1
    for (mode, origin), n in sorted(by_mode.items(), key=lambda kv: -kv[1])[:20]:
        lines.append(f"  {mode:28s} {origin:6s} {n}")
    return "\n".join(lines)


def self_test() -> int:
    tmp = tempfile.mkdtemp(prefix="footage-test-")
    rdir, sdir = os.path.join(tmp, "Recordings"), os.path.join(tmp, "shots")
    os.makedirs(rdir), os.makedirs(sdir)
    rec = theater.synthetic()
    with open(os.path.join(rdir, "MinigameRegatta_20261112_120000.cstheater"), "wb") as f:
        f.write(theater.write(rec))
    json.dump({"MatchId": "m-1", "GameModeName": "Regatta", "RecordedAtUtcTicks": 639000000000000000,
               "Results": [{"Rank": 1, "Name": "Garrett", "Score": 10}, {"Rank": 2, "Name": "Ava", "Score": 9.9}]},
              open(os.path.join(rdir, "MinigameRegatta_20261112_120000.replay.json"), "w"))
    open(os.path.join(rdir, "broken.cstheater"), "wb").write(b"nope")
    open(os.path.join(sdir, "S01_a.mp4"), "wb").write(b"")
    json.dump([{"file": "S01_a.mp4", "shot": "S01", "shows": "wide", "vessel_mode": "Dolphin / Orrery"}],
              open(os.path.join(sdir, "shots.json"), "w"))
    idx_path = os.path.join(tmp, "index.json")
    idx = build(idx_path, replays=[rdir], shots=[sdir])
    kinds = sorted(it["kind"] for it in idx["items"])
    assert kinds == ["replay", "shot"], kinds
    assert len(idx["errors"]) == 1 and "broken" in idx["errors"][0]["path"]
    rep = next(it for it in idx["items"] if it["kind"] == "replay")
    assert rep["match_id"] == "m-1" and rep["mode"] == "Regatta" and rep["humans"] == 1 and rep["moments"]
    assert rep["recorded_at"].startswith("20"), rep["recorded_at"]
    # a person's rating survives a re-index; a clip named after the item makes it showable
    rep["rating"] = 5
    json.dump(idx, open(idx_path, "w"))
    cdir = os.path.join(tmp, "clips")
    os.makedirs(cdir)
    open(os.path.join(cdir, rep["id"] + ".mp4"), "wb").write(b"")
    idx2 = build(idx_path, replays=[rdir], shots=[sdir], clips=[cdir])
    rep2 = next(it for it in idx2["items"] if it["kind"] == "replay")
    assert rep2["rating"] == 5 and rep2["showable"] and rep2["clip"].endswith(".mp4")
    # a clip filmed for one element is listed with it
    open(os.path.join(cdir, rep["id"] + "-destruction.mp4"), "wb").write(b"")
    rep3 = next(it for it in build(idx_path, replays=[rdir], shots=[sdir], clips=[cdir])["items"] if it["kind"] == "replay")
    assert {c["element"] for c in rep3["clips"]} == {None, "destruction"}, rep3["clips"]
    assert "creation" in rep3["elements"] and rep3["has_world"], rep3["elements"]
    shutil.rmtree(tmp)
    print("footage: self-test OK")
    return 0


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--index")
    ap.add_argument("--replays", action="append", default=[])
    ap.add_argument("--shots", action="append", default=[])
    ap.add_argument("--clips", action="append", default=[])
    ap.add_argument("--flight-moments")
    ap.add_argument("--summary", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        return self_test()
    if not a.index:
        ap.error("--index is required")
    if a.replays or a.shots or a.flight_moments or a.clips:
        idx = build(a.index, a.replays, a.shots, a.flight_moments, a.clips)
    else:
        idx = json.load(open(a.index, encoding="utf-8"))
    print(summary(idx))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
