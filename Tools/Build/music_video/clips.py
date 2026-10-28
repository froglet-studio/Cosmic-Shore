#!/usr/bin/env python3
"""Compose a plan of SHORT CLIPS from the clip library: one vessel doing its thing, framed so it reads.

    python3 Tools/Build/music_video/clips.py CLIP [CLIP ...] [--song "Assets/_Audio/Music/SKY FIRE.ogg" --start 41]
                                             [--seconds 5] [--out plan.json] [--list]

Garrett (cut 6, 2026-10-28): "where is the dolphin shooting things, the thresher slicing things, the
serpent building a wall, where are the gyroids?" A clip is a composed gameplay moment, not a scene the
camera happens to be in: the featured hull becomes the LOCAL vessel (`mv vessel CLASS`, the Vessel
Changer's own swap), its targets are staged on its path (`mv place`, fleets laying trails, flora),
it is aimed straight at them, and its ability fires at a known frame while a steady camera holds the
action in frame. The library (clips.json) is the memory: every clip carries the stills it was composed
from and what each attempt taught, so a cheaper model can shoot it again.

The plan is shoot.py's: the clips run back to back on the song from --start, each --seconds long (or
the clip's own `seconds`), so assemble.py lays them on the music and score.py reads them.
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("clips", nargs="*")
    ap.add_argument("--library", default=os.path.join(HERE, "clips.json"))
    ap.add_argument("--mapping", default=os.path.join(HERE, "mapping.json"))
    ap.add_argument("--song", default="Assets/_Audio/Music/SKY FIRE.ogg")
    ap.add_argument("--start", type=float, default=41.0)
    ap.add_argument("--seconds", type=float, default=5.0)
    ap.add_argument("--out")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--render", action="append", default=[], help="KEY=VALUE overrides of the render block (size=960x540 render_scale=1 msaa=4 accum=1 step_hz=60)")
    a = ap.parse_args(argv)
    lib = json.load(open(a.library))
    if a.list or not a.clips:
        for name, c in lib["clips"].items():
            print(f"{name:<22} {c['vessel']:<10} {c['what']}")
        return 0
    mapping = json.load(open(a.mapping))
    render = dict(mapping["render"])
    for kv in a.render:
        k, v = kv.split("=", 1)
        render[k] = v if k == "size" else float(v) if "." in v else int(v)
    step_hz, fps = int(render["step_hz"]), int(render["fps"])
    every = max(1, round(step_hz / fps)); accum = min(int(render["accum"]), every)
    shots, t = [], a.start
    for i, name in enumerate(a.clips):
        c = lib["clips"][name]
        seconds = float(c.get("seconds", a.seconds))
        frames = int(round(seconds * step_hz))
        # `{3s}` in a command = 3 seconds in sim frames at the plan's step rate (`mv in {3s} ...` in a stage list)
        import re
        frames_of = lambda txt: re.sub(r"\{([0-9.]+)s\}", lambda m: str(int(round(float(m.group(1)) * step_hz))), txt)
        events = []
        for e in c.get("events", []):
            # events are authored in seconds from the clip's start; `all` of a repeated command spreads at its interval
            events.append({"t": round(t + float(e["at"]), 3), "cmd": frames_of(e["cmd"]), "why": e.get("why", "clip")})
        shots.append({
            "name": f"{i + 1:02d}-{name}", "t0": round(t, 3), "t1": round(t + seconds, 3), "frames": frames, "every": every, "accum": accum,
            "mode": "Menu_Main", "players": 0, "intensity": 1, "stage": "menu", "cell": c.get("cell"),
            "settle_frames": int(float(c.get("settle_seconds", 6)) * step_hz),
            "setup": ["mv hud off", "mv autopilot on"] + [frames_of(x) for x in c["stage"]],
            "camera": ["mv camera main"] + c["camera"],
            "events": events, "features": {"energy": 0.5, "brightness": 0.5, "onset_density": 0.5},
            "archetype": name, "what": c["what"], "why": "clip library", "vessel": c["vessel"],
        })
        t += seconds
    plan = {"song": a.song, "song_name": os.path.basename(a.song), "tempo": 0, "segment": {"start": a.start, "end": t}, "fps": fps, "render": render,
            "mapping": os.path.relpath(a.mapping, os.getcwd()), "clips": a.clips, "shots": shots}
    out = a.out or os.path.join(HERE, "out", "clips-" + "-".join(a.clips)[:60], "plan.json")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    json.dump(plan, open(out, "w"), indent=1)
    for s in shots:
        print(f"{s['name']}: {s['vessel']} {s['t0']:.1f}-{s['t1']:.1f} s, {len(s['setup'])} stage commands, {len(s['events'])} events - {s['what']}")
    print(f"plan -> {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
