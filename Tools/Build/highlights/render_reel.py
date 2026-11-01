#!/usr/bin/env python3
"""Film a highlight reel from real players' flights in Amoebius, at true game speed.

Highlight reels, stage 2 (Docs/HIGHLIGHT_REELS.md). For each picked moment (from mine_moments.py) it
converts the flight to a match replay (flight_to_replay.py), drops it alone into a throwaway profile's
Recordings folder, boots the Amoebius Debug player, runs the game's own `replay latest` console command
(ReplayMatchDirector plays the race in its real scene, behind the recorded pilot), fast-forwards to the
moment, and screenshots every second frame while it plays, reading the replay clock beside each frame.
The frames are joined at true game speed, then the moments into one reel with a cut list beside it, so
any reel can be re-rendered (in Unity too) or re-cut.

Runs where Amoebius builds (Gbox; a cloud session needs the .NET 10 SDK hosts allowed).

    python Tools/Build/highlights/render_reel.py --moments moments.json --flights FLIGHTS --out OUT \
        [--count 6] [--player PATH_TO_CosmicShore(.exe)] [--size 1280x720] [--game OTHER_CHECKOUT]

--game films with a player built in another checkout (when this one's Amoebius does not build): its player,
its assets (COSMIC_SHORE_PROJECT) and its enums; moments in modes or hulls that checkout lacks are skipped.
"""
from __future__ import annotations

import argparse
import glob
import json
import os
import re
import shutil
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(REPO, "Port", "tools"))
sys.path.insert(0, HERE)
from amoebius_markers_run import Player, first_run_prompts  # noqa: E402
import flight_to_replay  # noqa: E402

PLAYER_PROJECT = os.path.join(REPO, "Port", "src", "CosmicShore.Player")
DIRECTOR = "[ReplayMatch]"


def pick(moments, count, playable=None):
    """The reel's moments: best first by score, one per flight, no two of the same lead type in a row, the best last."""
    chosen, flights = [], set()
    for m in moments:
        if m["flight"] in flights or (playable and not playable(m)):
            continue
        lead = m["events"][0]["type"]
        if chosen and chosen[-1]["events"][0]["type"] == lead and len(moments) > count * 3:
            continue
        chosen.append(m)
        flights.add(m["flight"])
        if len(chosen) == count:
            break
    return chosen[1:][::-1] + chosen[:1] if len(chosen) > 1 else chosen  # weakest first, the best moment last


def player_exe(arg, project=PLAYER_PROJECT, built=False):
    if arg:
        return arg
    for name in ("CosmicShore.exe", "CosmicShore"):
        p = os.path.join(project, "bin", "Debug", "net10.0", name)
        if os.path.exists(p):
            return p
    if built:
        sys.exit("no player build")
    subprocess.run(["dotnet", "build", project, "-c", "Debug", "-v", "q", "-nologo"], check=True)
    return player_exe(None, project, True)


def replay_clock(p):
    out = p.cmd("get", f"{DIRECTOR} ReplayMatchDirector Position").get("output", "")
    m = re.search(r"Position = (-?[\d.]+)", out)
    return float(m.group(1)) if m else None


def film_moment(exe, m, flights_root, work, recordings, port, size, every, game=None, slow=0.4):
    flight_path = m["path"] if os.path.exists(m["path"]) else next(iter(glob.glob(os.path.join(flights_root, "**", m["flight"]), recursive=True)), None)
    if not flight_path:
        raise SystemExit(f"flight {m['flight']} not found under {flights_root}")
    shutil.rmtree(recordings, ignore_errors=True)
    os.makedirs(recordings, exist_ok=True)
    flight_to_replay.convert_file(flight_path, recordings, game)

    shots = os.path.join(work, "frames")
    shutil.rmtree(shots, ignore_errors=True)
    os.makedirs(shots)
    env = dict(os.environ, COSMIC_SHORE_PROFILE="reels", COSMIC_SHORE_NET="off", COSMIC_SHORE_AUDIO="off")
    if game:
        env["COSMIC_SHORE_PROJECT"] = game
    log = open(os.path.join(work, "player.log"), "w")
    proc = subprocess.Popen([exe, "--frames", "100000000", "--render-from", "2000000000", "--size", size,
                             "--control-port", str(port)], env=env, stdout=log, stderr=subprocess.STDOUT)
    p = Player(port)
    rows = []
    try:
        p.wait_scene({"Authentication", "Menu_Main"}, 300)
        first_run_prompts(p)
        p.wait_scene({"Menu_Main"}, 300)
        p.wait(60)
        print("  ", p.do("console replay latest").strip()[:200])
        # wait for the director, then fast-forward to the moment
        end = time.time() + 600
        clock = None
        while time.time() < end:
            p.wait(30)
            clock = replay_clock(p)
            if clock is not None and clock > 0:
                break
        if clock is None:
            raise SystemExit("the replay never started (no ReplayMatchDirector): see player.log")
        p.do("key F7")  # the development build's diagnostics overlay off
        target = max(0.0, m["start"])
        if clock < target - 2.5:
            p.do("timescale 8")
            while (clock or 0) < target - 2.0:  # 10 frames at 8x is 1.3 s of race: stop early, never past the start
                p.wait(10)
                clock = replay_clock(p) or clock
        # each picture costs ~5 game frames in control round-trips, so film in slow motion; the replay clock beside
        # every frame puts it back to true speed (0.4 gives about 30 pictures a game second instead of 12)
        p.do(f"timescale {slow}")
        while (clock or 0) < target:
            p.wait(1)
            clock = replay_clock(p) or clock
        n = 0
        while True:
            p.wait(every)
            clock = replay_clock(p)
            if clock is None or clock >= m["end"]:
                break
            name = f"f{n:05d}.png"
            p.cmd("screenshot", os.path.join(shots, name))
            rows.append((name, clock))
            n += 1
    finally:
        try:
            p.cmd("quit", timeout=10)
        except Exception:
            pass
        try:
            proc.wait(30)
        except subprocess.TimeoutExpired:
            proc.kill()
    if len(rows) < 10:
        raise SystemExit(f"only {len(rows)} frames for {m['flight']}: see {work}/player.log")
    # hold each frame for the game time to the next one (true speed, like Port/tools/assemble_recording.py)
    concat = ["ffconcat version 1.0"]
    for (name, t), nxt in zip(rows, rows[1:] + [(None, rows[-1][1] + (rows[-1][1] - rows[-2][1]))]):
        concat += [f"file '{os.path.join(shots, name).replace(os.sep, '/')}'", f"duration {max(0.001, nxt[1] - t):.4f}"]
    lst = os.path.join(work, "frames.ffconcat")
    open(lst, "w").write("\n".join(concat) + "\n")
    clip = os.path.join(work, "clip.mp4")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", lst, "-r", "30",
                    "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p", clip], check=True)
    return clip, rows, rows[-1][1] - rows[0][1] + (rows[-1][1] - rows[-2][1])


def _commit(repo):
    r = subprocess.run(["git", "-C", repo, "rev-parse", "--short=10", "HEAD"], capture_output=True, text=True)
    return r.stdout.strip() or None


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # the console's replies carry symbols a cp1252 console cannot print
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--moments", required=True)
    ap.add_argument("--flights", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--count", type=int, default=6)
    ap.add_argument("--player")
    ap.add_argument("--recordings", required=True,
                    help="the 'reels' profile's Recordings folder (where the player's ReplayStore looks; see player.log)")
    ap.add_argument("--size", default="1280x720")
    ap.add_argument("--every", type=int, default=1, help="game frames to wait between pictures (on top of the control round-trips)")
    ap.add_argument("--port", type=int, default=47831)
    ap.add_argument("--name", default="reel")
    ap.add_argument("--slow", type=float, default=0.4, help="the timescale while filming (true speed is restored from the clock)")
    ap.add_argument("--game", help="the checkout the player was built from, when not this one")
    a = ap.parse_args()
    game = os.path.abspath(a.game) if a.game else None

    doc = json.load(open(a.moments))
    modes, hulls, _ = flight_to_replay.enums(game)
    chosen = pick(doc.get("all") or doc["top"], a.count,
                  lambda m: m["mode"] in modes and m["hull"] in hulls)
    exe = player_exe(a.player, os.path.join(game, "Port", "src", "CosmicShore.Player") if game else PLAYER_PROJECT)
    os.makedirs(a.out, exist_ok=True)
    clips, cuts, durs = [], [], []
    for i, m in enumerate(chosen):
        work = os.path.join(a.out, f"{a.name}_m{i:02d}")
        os.makedirs(work, exist_ok=True)
        print(f"[{i + 1}/{len(chosen)}] {m['mode']} {m['hull']} {m['pilot']} {m['start']}-{m['end']} s: "
              + ", ".join(e["type"] for e in m["events"]))
        clip, rows, dur = film_moment(exe, m, a.flights, work, a.recordings, a.port, a.size, a.every, game, a.slow)
        clips.append(clip)
        durs.append(dur)
        cuts.append({"index": i, "flight": m["flight"], "pilot": m["pilot"], "mode": m["mode"], "hull": m["hull"],
                     "start": m["start"], "peak": m["peak"], "end": m["end"], "shot": "chase (replay default)",
                     "events": m["events"], "features": m["features"], "score": m["score"],
                     "clip": os.path.relpath(clip, a.out), "frames": len(rows)})
    # join the clips with a short dissolve
    reel = os.path.join(a.out, f"{a.name}.mp4")
    if len(clips) == 1:
        shutil.copy(clips[0], reel)
    else:
        fade, inputs, chain, last, off = 0.4, [], [], "[0:v]", 0.0
        for c in clips:
            inputs += ["-i", c]
        for i in range(1, len(clips)):
            off += durs[i - 1] - fade
            out = f"[v{i}]"
            chain.append(f"{last}[{i}:v]xfade=transition=fade:duration={fade}:offset={off:.3f}{out}")
            last = out
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *inputs, "-filter_complex", ";".join(chain), "-map", last,
                        "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p", reel], check=True)
        t = 0.0
        for i, c in enumerate(cuts):
            c["reel_start"] = round(t, 2)
            t += durs[i] - (fade if i < len(cuts) - 1 else 0)
            c["reel_end"] = round(t, 2)
    json.dump({"schema": "cosmic-shore.reel/1", "reel": os.path.basename(reel), "renderer": "amoebius",
               "weights": doc.get("weights"),
               "player_commit": _commit(game or REPO), "moments": cuts}, open(os.path.join(a.out, f"{a.name}.json"), "w"), indent=1)
    print(f"wrote {reel}")


if __name__ == "__main__":
    main()
