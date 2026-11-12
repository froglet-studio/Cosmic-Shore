#!/usr/bin/env python3
"""Decide today's story from today's measurement, pick its footage, and write the cut list.

The second stage of the daily story (Docs/StoryFlywheel/README.md). Inputs: `measure.json` (measure.py), the footage
index (footage.py), `story_config.json` (the editable rules: the song's beat slots, the end card, which activity
belongs to which part of the game) and yesterday's `story.json` if there is one. Outputs, in --out:

- `story.json`  the decision: headline, beats, every claim with the numbers behind it, the footage per beat
- `edl.json`    a cut list in Tools/Build/trailer/cut.py's format (song time, beat-aligned slots)
- `shoot.json`  what to film next in Unity (TrailerShoot on Gbox): real moments that have no clip yet, beats with
                no footage at all
- `brief.md`    the one page a person reads: what we measured, the story it supports, what changed since yesterday,
                where the footage is staged rather than real, and what data is missing

    python3 Tools/Build/story/story.py --measure measure.json --footage index.json --out DIR [--yesterday DIR]
    python3 Tools/Build/story/story.py --self-test

Honesty rules, enforced here rather than left to taste:
1. Every on-screen claim is a sentence filled from raw counts in measure.json (`came back`, `players`, `rematch`),
   never from the ranking score, and is listed in story.json with the numbers it used.
2. A beat needs at least MIN_PLAYERS players behind it. One fan is not a story.
3. Footage is labelled real or staged. A claim about what players do prefers real footage; staged footage may stand
   in, and the brief says how much of the cut is staged. No footage, no beat (it goes on the shoot list).
4. The story is re-decided every day. Keeping yesterday's headline is allowed only when today's numbers still put
   it first; the brief says which happened.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
CONFIG = os.path.join(HERE, "story_config.json")
MIN_PLAYERS = 3
MAX_BEATS = 3


def _norm(s):
    return re.sub(r"[^a-z0-9]", "", (s or "").lower())


def load_config(path=CONFIG):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def pillar_of(cfg, activity):
    if activity["kind"].startswith("toybox"):
        return "toybox"
    return cfg.get("pillars", {}).get(activity["name"], cfg.get("default_pillar", "arcade"))


def claim(cfg, a):
    """The on-screen line for a beat and the numbers it used. Only raw counts."""
    label = cfg.get("labels", {}).get(a["name"], _spaced(a["name"]))
    n, back = a.get("players", 0), a.get("came_back", 0)
    if a["kind"] == "toybox_vessel":
        head = f"THEY KEEP FLYING THE {label.upper()}"
    elif a["kind"] == "toybox":
        head = "THEY COME BACK JUST TO PLAY"
    elif a["kind"] == "vessel":
        head = f"THE {label.upper()} KEEPS THEM COMING BACK"
    else:
        head = f"THEY COME BACK FOR {label.upper()}"
    sub = f"{back} OF {n} PLAYTESTERS RETURNED ON ANOTHER DAY"
    used = {"players": n, "came_back": back}
    if a.get("rematch_rate") is not None and a.get("finished", 0) >= 10 and a["rematch_rate"] >= 0.3:
        sub = f"{round(100 * a['rematch_rate'])}% PLAYED AGAIN RIGHT AWAY"
        used = {"finished": a["finished"], "rematched": a["rematched"]}
    return {"lines": [head, sub], "numbers": used}


def _spaced(name):
    return re.sub(r"(?<=[a-z])(?=[A-Z])", " ", name)


def footage_for(a, items, used):
    """Real clips first, then staged shots, for the activity's mode or vessel. Skips footage already in the cut."""
    key = _norm(a["name"])
    def matches(it):
        if a["kind"] in ("mode",):
            return _norm(it.get("mode")) == key or key in _norm(it.get("mode"))
        if a["kind"] in ("vessel", "toybox_vessel"):
            vessels = [it.get("vessel")] + [p.get("vessel") for p in it.get("pilots", []) if not p.get("ai")]
            return any(_norm(v) == key for v in vessels if v)
        return "toybox" in _norm(it.get("mode")) or "freestyle" in _norm(it.get("mode")) or "orrery" in _norm(it.get("mode"))
    pool = [it for it in items if matches(it) and it["id"] not in used]
    showable = [it for it in pool if it.get("showable")]
    showable.sort(key=lambda it: (it["origin"] != "real", -(it.get("rating") or 0),
                                  "strong" not in (it.get("quality_note") or ""), -(it.get("best_score") or it.get("score") or 0)))
    refilm = sorted((it for it in pool if not it.get("showable") and it["origin"] == "real"),
                    key=lambda it: -(it.get("best_score") or it.get("score") or 0))
    return showable, refilm


def decide(measure, index, cfg, yesterday=None):
    items = index.get("items", [])
    candidates = [a for a in measure["activities"] if a.get("players", 0) >= MIN_PLAYERS]
    used, beats, shoot, skipped = set(), [], [], []
    for a in candidates:
        if len(beats) >= MAX_BEATS:
            break
        if any(b["kind"] == a["kind"] == "toybox_vessel" for b in beats) and a["kind"] == "toybox_vessel" and len(beats) >= 2:
            continue
        showable, refilm = footage_for(a, items, used)
        for it in refilm[:2]:
            shoot.append({"why": f"real play of {a['name']} with no clip yet", "item": it["id"], "path": it.get("path"),
                          "mode": it.get("mode"), "moments": [(m["start"], m["end"]) for m in it.get("moments", [])[:3]]
                          if it["kind"] == "replay" else [(it.get("start"), it.get("end"))]})
        if not showable:
            skipped.append({"activity": a["name"], "kind": a["kind"], "why": "no footage yet"})
            shoot.append({"why": f"no footage of {a['name']} at all", "activity": a["name"], "kind": a["kind"]})
            continue
        picks = showable[:2]
        used.update(it["id"] for it in picks)
        beats.append({"kind": a["kind"], "name": a["name"], "pillar": pillar_of(cfg, a), "pull": a["pull"],
                      "claim": claim(cfg, a), "evidence": {k: a.get(k) for k in (
                          "players", "came_back", "came_back_3", "week_apart", "window_plays", "finished",
                          "rematched", "rematch_rate", "abandoned", "quit_rate", "hours", "trend")},
                      "footage": [{"id": it["id"], "origin": it["origin"], "path": it.get("clip") or it["path"],
                                   "src_in": it.get("best_in") or 0.0, "shows": it.get("shows", "")} for it in picks]})
    headline = beats[0] if beats else None
    held = bool(yesterday and headline and yesterday.get("headline") and
                (yesterday["headline"]["kind"], yesterday["headline"]["name"]) == (headline["kind"], headline["name"]))
    frames = [f for b in beats for f in b["footage"]]
    real = sum(1 for f in frames if f["origin"] == "real")
    return {
        "schema": "cosmic-shore.story/1", "day": dt.date.today().isoformat(),
        "measured": {"source": measure.get("source"), "window_days": measure.get("window_days"),
                     "since": measure.get("since"), "totals": measure.get("totals")},
        "headline": {"kind": headline["kind"], "name": headline["name"]} if headline else None,
        "held_from_yesterday": held,
        "yesterday_headline": (yesterday or {}).get("headline"),
        "beats": beats, "skipped": skipped, "shoot": shoot,
        "footage": {"clips": len(frames), "real": real, "staged": len(frames) - real},
        "gaps": gaps(measure, index),
    }


def gaps(measure, index):
    """What the story cannot see yet. Listed every day until it is fixed, so nobody mistakes silence for an answer."""
    out = []
    kinds = {a["kind"] for a in measure["activities"]}
    if "toybox" not in kinds:
        out.append("No Toybox return data (activity_returns has no toybox rows: is the server on the 2026-11-12 build?)")
    out.append("Voyages send no analytics events yet, so the story cannot measure them (plan phase 1).")
    if not any(it["origin"] == "real" and it.get("showable") for it in index.get("items", [])):
        out.append("No real-play clips yet: every frame in the cut is staged footage (plan phase 2).")
    if not any(it["kind"] == "replay" for it in index.get("items", [])):
        out.append("No match replays collected from playtesters' machines yet (plan phase 2).")
    return out


def edl(story, cfg):
    """Fill the song's beat slots: an opener, two slots per beat, a closer. Text on each beat's first slot."""
    slots = cfg["song"]["slots"]
    frames, texts = [], []
    opener = cfg.get("opener")
    if opener:
        frames.append({"shot": opener["path"], "src_in": opener.get("src_in", 0.0), "fade_in": 1.6})
    beat_slots = {}
    for b in story["beats"]:
        beat_slots[b["name"]] = len(frames)
        for f in (b["footage"] * 2)[:2]:
            frames.append({"shot": f["path"], "src_in": f["src_in"]})
    closer = cfg.get("closer")
    if closer:
        frames.append({"shot": closer["path"], "src_in": closer.get("src_in", 0.0)})
    frames = frames[:len(slots)]
    first = len(slots) - len(frames)               # fewer frames than slots: start later in the song, end on time
    segs = [dict(f, t0=slots[first + i]) for i, f in enumerate(frames)]
    end = cfg["song"]["end"]
    for b in story["beats"]:
        i = beat_slots.get(b["name"])
        if i is None or i >= len(segs):
            continue
        t0 = segs[i]["t0"]
        t1 = segs[i + 2]["t0"] if i + 2 < len(segs) else end - cfg["endcard"]["dur"]
        texts.append({"t0": round(t0 + 0.3, 2), "t1": round(t1 - 0.2, 2), "lines": b["claim"]["lines"],
                      "sizes": [64, 34], "cy": 0.80})
    return {"music": cfg["song"]["music"], "audio_fade_in": 1.2, "audio_fade_out": 2.4,
            "loudness": {"I": -14, "TP": -1, "LRA": 11}, "end": end, "segments": segs, "texts": texts,
            "endcard": cfg["endcard"]}


def brief(story):
    m = story["measured"]
    t = m.get("totals") or {}
    L = [f"# Story for {story['day']}", ""]
    L.append(f"Measured {m.get('window_days')} days since {m.get('since')} from {m.get('source')}: "
             f"{t.get('players')} players, {t.get('match_plays')} matches, {t.get('toybox_entries')} Toybox entries.")
    L.append("")
    if not story["beats"]:
        L += ["**No story today.** Nothing had enough players and footage behind it. See the gaps and the shoot list.", ""]
    else:
        h = story["headline"]
        why = ("kept: today's numbers still put it first" if story["held_from_yesterday"] else
               f"changed from yesterday's {story['yesterday_headline']['name']}" if story.get("yesterday_headline")
               else "first story")
        L += [f"**Headline: {_spaced(h['name'])}** ({h['kind']}), {why}.", "", "## Beats", ""]
        for b in story["beats"]:
            e = b["evidence"]
            L.append(f"- **{' / '.join(b['claim']['lines'])}**  ")
            L.append(f"  {b['kind']} `{b['name']}` ({b['pillar']}): {e.get('came_back')} of {e.get('players')} came back "
                     f"on another day, {e.get('week_apart')} a week later; rematch {e.get('rematch_rate')}, "
                     f"quit {e.get('quit_rate')}, {e.get('hours')} h, {e.get('trend') or 'no trend yet'}.  ")
            L.append("  Footage: " + ", ".join(f"{os.path.basename(str(f['path']))} ({f['origin']})" for f in b["footage"]))
        f = story["footage"]
        L += ["", f"Footage in the cut: {f['real']} real, {f['staged']} staged."]
    if story["skipped"]:
        L += ["", "## Measured but not shown (no footage)", ""] + [f"- {s['activity']} ({s['kind']})" for s in story["skipped"]]
    if story["shoot"]:
        L += ["", "## Shoot next (Unity, TrailerShoot on Gbox)", ""] + [f"- {s['why']}" for s in story["shoot"][:10]]
    L += ["", "## What the story cannot see yet", ""] + [f"- {g}" for g in story["gaps"]]
    return "\n".join(L) + "\n"


def self_test() -> int:
    cfg = load_config()
    measure = {"source": "test", "window_days": 28, "since": "2026-10-16",
               "totals": {"players": 40, "match_plays": 300, "toybox_entries": 90},
               "activities": [
                   {"kind": "toybox", "name": "Toybox", "players": 20, "came_back": 11, "week_apart": 3, "pull": 0.5},
                   {"kind": "mode", "name": "SkimRace", "players": 12, "came_back": 7, "finished": 40, "rematched": 20,
                    "rematch_rate": 0.5, "pull": 0.45},
                   {"kind": "mode", "name": "Joust", "players": 1, "came_back": 1, "pull": 0.9},
                   {"kind": "mode", "name": "Regatta", "players": 30, "came_back": 4, "pull": 0.2}]}
    index = {"items": [
        {"id": "shot-1", "kind": "shot", "origin": "staged", "path": "/s/S01.mp4", "mode": "Orrery", "showable": True,
         "best_in": 1.0, "quality_note": "strong"},
        {"id": "shot-2", "kind": "shot", "origin": "staged", "path": "/s/S09.mp4", "mode": "SkimRace", "showable": True},
        {"id": "clip-1", "kind": "replay", "origin": "real", "path": "/r/a.cstheater", "clip": "/c/clip-1.mp4",
         "mode": "Skim Race", "showable": True, "best_score": 3.0},
        {"id": "rep-2", "kind": "replay", "origin": "real", "path": "/r/b.cstheater", "mode": "Regatta",
         "showable": False, "best_score": 2.0, "moments": [{"start": 1, "end": 6}]}]}
    s = decide(measure, index, cfg)
    names = [b["name"] for b in s["beats"]]
    assert names == ["Toybox", "SkimRace"], names                    # Joust: one player; Regatta: no clip
    assert s["beats"][1]["footage"][0]["origin"] == "real", "real clips before staged shots"
    assert any(x.get("item") == "rep-2" for x in s["shoot"]), "an unfilmed real Regatta replay goes on the shoot list"
    assert any(x["activity"] == "Regatta" for x in s["skipped"])
    assert s["beats"][1]["claim"]["lines"][1] == "50% PLAYED AGAIN RIGHT AWAY"
    assert s["beats"][0]["claim"]["numbers"] == {"players": 20, "came_back": 11}
    e = edl(s, cfg)
    assert all(a["t0"] < b["t0"] for a, b in zip(e["segments"], e["segments"][1:]))
    assert e["segments"][-1]["t0"] < e["end"] and len(e["texts"]) == 2
    # yesterday's headline is kept only when today's numbers still put it first
    s2 = decide(measure, index, cfg, yesterday={"headline": {"kind": "toybox", "name": "Toybox"}})
    assert s2["held_from_yesterday"]
    s3 = decide(measure, index, cfg, yesterday={"headline": {"kind": "mode", "name": "Regatta"}})
    assert not s3["held_from_yesterday"] and "changed from" in brief(s3)
    print("story: self-test OK")
    return 0


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--measure")
    ap.add_argument("--footage")
    ap.add_argument("--config", default=CONFIG)
    ap.add_argument("--yesterday", help="yesterday's output folder (its story.json)")
    ap.add_argument("--out")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        return self_test()
    if not (a.measure and a.footage and a.out):
        ap.error("--measure, --footage and --out")
    cfg = load_config(a.config)
    measure = json.load(open(a.measure, encoding="utf-8"))
    index = json.load(open(a.footage, encoding="utf-8"))
    y = None
    if a.yesterday and os.path.exists(os.path.join(a.yesterday, "story.json")):
        y = json.load(open(os.path.join(a.yesterday, "story.json"), encoding="utf-8"))
    s = decide(measure, index, cfg, y)
    os.makedirs(a.out, exist_ok=True)
    json.dump(s, open(os.path.join(a.out, "story.json"), "w", encoding="utf-8"), indent=1)
    json.dump(edl(s, cfg), open(os.path.join(a.out, "edl.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    json.dump(s["shoot"], open(os.path.join(a.out, "shoot.json"), "w", encoding="utf-8"), indent=1)
    open(os.path.join(a.out, "brief.md"), "w", encoding="utf-8").write(brief(s))
    print(brief(s))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
