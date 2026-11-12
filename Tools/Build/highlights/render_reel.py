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

--exclude REEL.json ... never reuses a flight an earlier reel used. --explore 0.25 fills that share of the slots
from the middle of the ranking (seeded), so the rewatch scores also see moments the hand weights rate lower.
Each moment is filmed on the pilot's chase camera, or (for a moment with a rival in it, by a seeded draw of
--rival-shots) on the next pilot's chase camera, and the cut list records which, so shots get scored too.
The game's screen-space canvases are switched off once the replay plays (HUD, toasts, minimap), and each pilot
gets a profile of their own, named after them, so the race's toasts name the real pilot.

--game films with a player built in another checkout (when this one's Amoebius does not build): its player,
its assets (COSMIC_SHORE_PROJECT) and its enums; moments in modes or hulls that checkout lacks are skipped.

--cut REEL.json re-films an earlier reel's cut list: the same moments, in its order, each on the shot it was
filmed on (to compare two players on one cut); --moments, --count, --explore and --rival-shots are then unused.
"""
from __future__ import annotations

import argparse
import glob
import json
import os
import random
import re
import shutil
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(REPO, "Port", "tools"))
sys.path.insert(0, HERE)
from amoebius_markers_run import Player  # noqa: E402
import flight_to_replay  # noqa: E402

PLAYER_PROJECT = os.path.join(REPO, "Port", "src", "CosmicShore.Player")
DIRECTOR = "[ReplayMatch]"
# the editor's flight records are filed by OS user name; the reels show the person (Docs/TEAM.json)
PILOT_NAMES = {"gradi": "Garrett", "yskha": "Yash", "belac": "Caleb", "shombith": "Shombith"}
RIVAL_EVENTS = {"near_pass", "overtake", "crystal_snatch", "close_finish"}


def pick(moments, count, playable=None, exclude=(), explore=0.0, seed=1):
    """The reel's moments: best first by score, one per flight, no two of the same lead type in a row, the best last.
    A share `explore` of the slots is drawn (seeded) from the middle of the ranking instead of its top."""
    pool = [m for m in moments if m["flight"] not in exclude and (not playable or playable(m))]
    n_explore = int(round(count * explore))
    rng = random.Random(seed)
    chosen, flights = [], set()

    def take(m, how):
        if m["flight"] in flights:
            return False
        lead = m["events"][0]["type"]
        if chosen and chosen[-1]["events"][0]["type"] == lead and len(pool) > count * 3:
            return False
        chosen.append(dict(m, picked=how))
        flights.add(m["flight"])
        return True

    for m in pool:
        if len(chosen) >= count - n_explore:
            break
        take(m, "top")
    middle = pool[len(pool) // 5: len(pool) * 3 // 5]
    rng.shuffle(middle)
    for m in middle:
        if len(chosen) >= count:
            break
        take(m, "explore")
    best = max(chosen, key=lambda m: m["score"]) if chosen else None
    rest = [m for m in chosen if m is not best]
    rng.shuffle(rest)
    return rest + ([best] if best else [])  # the best moment last


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


def hide_hud(p):
    """Switch off every Canvas in the scene (the HUD, toasts, minimap, the ships' ability panels) and the silence
    screen effect: they are the pilot's instruments, not the footage. Canvases are found by component, not by name
    (the ships' HUD canvas is 'ShipHUDContainer'), from a hierarchy dump; inactive ones too, as they may wake later."""
    out = p.cmd("hierarchy", ":40").get("output", "")
    stack, paths = [], set()
    for line in out.splitlines():
        m = re.match(r"( *)(\S.*?)  \[(.*)\]", line)
        if not m:
            continue
        level = len(m.group(1)) // 2
        name = re.sub(r" \(inactive\)$", "", m.group(2))
        del stack[level:]
        stack.append(name)
        if re.search(r"(^|, )Canvas(, |$)", m.group(3)):
            paths.add("/".join(stack))
    for path in sorted(paths):
        p.cmd("set", f'"{path}" Canvas enabled False')
    p.cmd("set", '"[Silence Screen Feedback]" SilenceScreenFeedback enabled False')
    return len(paths)


def follow_next_pilot(p):
    """The replay's own Tab: from the recorded pilot's chase camera to the next pilot's (skipping the free camera,
    which Tab passes through after the last pilot). True when the camera now follows another pilot."""
    def follow():
        m = re.search(r"_follow = (-?\d+)", p.cmd("get", f"{DIRECTOR} ReplayMatchDirector _follow").get("output", ""))
        return int(m.group(1)) if m else None
    start = follow()
    for _ in range(2):
        p.do("key Tab")
        p.wait(3)
        now = follow()
        if now is not None and now >= 0 and now != start:
            return True
    return False


def first_run(p, name):
    """Age gate, consent (declined) and the pilot's name, for this pilot's throwaway profile only. A name the game
    refuses (its reserved names include 'owner', an OS user name some flights are filed under) becomes 'Pilot'."""
    for attempt in range(3):
        if p.scene() != "Authentication":
            return
        if p.click("BirthYear"):
            p.do("type 1990")
            p.wait(5)
            p.click("Button_Continue")
        p.click("Button_No thanks")
        if p.click("UsernameInputField"):
            for _ in range(24):  # the field keeps a refused name: clear it
                p.do("key Backspace")
            p.do(f"type {name if attempt == 0 else 'Pilot'}")
            p.wait(5)
            p.click("ConfirmUsernameButton")
        p.wait(120)


def profile_recordings(recordings, pilot):
    """The Recordings folder of profile reels-<pilot>, beside the 'reels' profile's one."""
    return recordings.replace("CosmicShore-reels", f"CosmicShore-reels-{pilot}") if pilot else recordings


def film_moment(exe, m, flights_root, work, recordings, port, size, every, game=None, slow=0.4, shot="pilot"):
    flight_path = m["path"] if os.path.exists(m.get("path", "")) else next(iter(glob.glob(os.path.join(flights_root, "**", m["flight"]), recursive=True)), None)
    if not flight_path:
        raise SystemExit(f"flight {m['flight']} not found under {flights_root}")
    pilot = PILOT_NAMES.get(m.get("pilot", ""), m.get("pilot", "") or "Pilot")
    recordings = profile_recordings(recordings, re.sub(r"[^A-Za-z0-9]", "", pilot))
    shutil.rmtree(recordings, ignore_errors=True)
    os.makedirs(recordings, exist_ok=True)
    flight_to_replay.convert_file(flight_path, recordings, game)

    shots = os.path.join(work, "frames")
    shutil.rmtree(shots, ignore_errors=True)
    os.makedirs(shots)
    env = dict(os.environ, COSMIC_SHORE_PROFILE="reels-" + re.sub(r"[^A-Za-z0-9]", "", pilot), COSMIC_SHORE_NET="off", COSMIC_SHORE_AUDIO="off")
    if game:
        env["COSMIC_SHORE_PROJECT"] = game
    log = open(os.path.join(work, "player.log"), "w")
    proc = subprocess.Popen([exe, "--frames", "100000000", "--render-from", "2000000000", "--size", size,
                             "--control-port", str(port)], env=env, stdout=log, stderr=subprocess.STDOUT)
    p = Player(port)
    rows = []
    try:
        p.wait_scene({"Authentication", "Menu_Main"}, 300)
        first_run(p, pilot)
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
        print("   canvases off:", hide_hud(p))
        if shot == "rival" and not follow_next_pilot(p):
            print("   no other pilot to follow: filmed on the pilot")
            shot = "pilot"
        target = max(0.0, m["start"])
        if clock < target - 2.5:
            p.do("timescale 8")
            while (clock or 0) < target - 2.0:  # 10 frames at 8x is 1.3 s of race: stop early, never past the start
                p.wait(10)
                clock = replay_clock(p) or clock
        # each picture costs ~5 game frames in control round-trips, so film in slow motion; the replay clock beside
        # every frame puts it back to true speed (0.4 gives about 30 pictures a game second instead of 12)
        p.do(f"timescale {slow}")
        hide_hud(p)  # again: a ship's HUD or a toast canvas built since the replay began
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
    return clip, rows, rows[-1][1] - rows[0][1] + (rows[-1][1] - rows[-2][1]), shot


def _commit(repo):
    r = subprocess.run(["git", "-C", repo, "rev-parse", "--short=10", "HEAD"], capture_output=True, text=True)
    dirty = subprocess.run(["git", "-C", repo, "status", "--porcelain", "--untracked-files=no"], capture_output=True, text=True)
    return (r.stdout.strip() + ("-dirty" if dirty.stdout.strip() else "")) or None  # a local patch shows


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # the console's replies carry symbols a cp1252 console cannot print
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--moments")
    ap.add_argument("--cut", help="an earlier reel's cut list (REEL.json) to film again: its moments and shots")
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
    ap.add_argument("--exclude", nargs="*", default=[], help="earlier reels' cut lists: their flights are not reused")
    ap.add_argument("--explore", type=float, default=0.0, help="share of the slots drawn from the middle of the ranking")
    ap.add_argument("--rival-shots", type=float, default=0.3, help="chance a moment with a rival is filmed from the rival")
    ap.add_argument("--seed", type=int, default=1)
    a = ap.parse_args()
    game = os.path.abspath(a.game) if a.game else None

    if a.cut:
        doc = json.load(open(a.cut))
        chosen = sorted(doc["moments"], key=lambda c: c["index"])
    elif a.moments:
        doc = json.load(open(a.moments))
        modes, hulls, _ = flight_to_replay.enums(game)
        used = set()
        for e in a.exclude:
            used |= {c["flight"] for c in json.load(open(e)).get("moments", [])}
        chosen = pick(doc.get("all") or doc["top"], a.count,
                      lambda m: m["mode"] in modes and m["hull"] in hulls, used, a.explore, a.seed)
    else:
        sys.exit("--moments or --cut is required")
    rng = random.Random(a.seed + 1)
    exe = player_exe(a.player, os.path.join(game, "Port", "src", "CosmicShore.Player") if game else PLAYER_PROJECT)
    os.makedirs(a.out, exist_ok=True)
    clips, cuts, durs = [], [], []
    for i, m in enumerate(chosen):
        work = os.path.join(a.out, f"{a.name}_m{i:02d}")
        os.makedirs(work, exist_ok=True)
        print(f"[{i + 1}/{len(chosen)}] {m['mode']} {m['hull']} {m['pilot']} {m['start']}-{m['end']} s: "
              + ", ".join(e["type"] for e in m["events"]))
        with_rival = any(e["type"] in RIVAL_EVENTS for e in m["events"])
        shot = "rival" if with_rival and rng.random() < a.rival_shots else "pilot"
        if a.cut:
            shot = "rival" if m.get("shot", "").endswith("next pilot") else "pilot"
        clip, rows, dur, shot = film_moment(exe, m, a.flights, work, a.recordings, a.port, a.size, a.every, game, a.slow, shot)
        clips.append(clip)
        durs.append(dur)
        cuts.append({"index": i, "flight": m["flight"], "pilot": m["pilot"], "mode": m["mode"], "hull": m["hull"],
                     "start": m["start"], "peak": m["peak"], "end": m["end"], "shot": "chase on the next pilot" if shot == "rival" else "chase on the pilot",
                     "picked": m.get("picked", "top"),
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
               "weights": doc.get("weights"), "cut_from": os.path.basename(a.cut) if a.cut else None,
               "player_commit": _commit(game or REPO), "moments": cuts}, open(os.path.join(a.out, f"{a.name}.json"), "w"), indent=1)
    print(f"wrote {reel}")


if __name__ == "__main__":
    main()
