#!/usr/bin/env python3
"""Shoot a music-video plan in Amoebius: one player process per shot, frames on disk.

    python3 Tools/Build/music_video/shoot.py PLAN.json [--out DIR] [--jobs 2] [--only NAME] [--player DLL]

Each shot in the plan boots the real game (scripted run: every frame is exactly 1/60 s of game
time), opens the mode's arcade card the way a player does, seats AI pilots, starts the match,
presses Ready, waits for the action to settle, then hands the camera to the engine-side
director (`mv` verb, Port/src/CosmicShore.Player/MusicVideoDirector.cs) and records the shot's
frames at the plan's fps. Frames are encoded as they land into OUT/<shot>/shot.mp4 (near-lossless
H.264) next to frames.csv (game time per frame) and shoot.log (every command and the engine's
answers), so assemble.py can lay them on the song at true game speed and a failed shot can be read
back. The PNGs themselves are deleted as they are encoded: a minute of 1080p frames is 20 GB.

Needs: the player built (`dotnet build Port/src/CosmicShore.Player`), xvfb-run, and a profile
that has answered the first-run prompts (the runner answers them itself on a fresh profile:
birth year, data collection DECLINED, pilot name "Director").
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time
import urllib.request
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
PLAYER_DLL = os.path.join(ROOT, "Port", "src", "CosmicShore.Player", "bin", "Debug", "net10.0", "CosmicShore.dll")
DOTNET = shutil.which("dotnet") or "/opt/dotnet/dotnet"
MENU = "Menu_Main"


class Player:
    """One Amoebius player under xvfb with a control port."""

    def __init__(self, port, profile, log_path, size="1600x900", dll=PLAYER_DLL, extra=()):
        # the virtual screen must be larger than the window, and the pointer (screen centre) off it
        self.port = port
        self.log = open(log_path, "w")
        env = dict(os.environ)
        env.update({
            "COSMIC_SHORE_NET": "off", "COSMIC_SHORE_AUDIO": "off", "COSMIC_SHORE_PROFILE": profile,
            "COSMIC_SHORE_PROJECT": ROOT, "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        })
        dotnet_root = os.path.dirname(DOTNET)
        env.setdefault("DOTNET_ROOT", dotnet_root)
        env["PATH"] = dotnet_root + os.pathsep + env.get("PATH", "")
        cmd = ["xvfb-run", "-a", "-s", "-screen 0 4200x2400x24", DOTNET, dll,
               "--control-port", str(port), "--size", size, "--frames", "4000000", "--render-from", "999999999", *extra]
        self.proc = subprocess.Popen(cmd, stdout=self.log, stderr=subprocess.STDOUT, cwd=ROOT, env=env)

    def cmd(self, c, arg=None, timeout=900):
        body = {"cmd": c}
        if arg is not None:
            body["arg"] = arg
        req = urllib.request.Request(f"http://127.0.0.1:{self.port}/", data=json.dumps(body).encode(), method="POST")
        r = json.loads(urllib.request.urlopen(req, timeout=timeout).read())
        return r

    def do(self, action):
        r = self.cmd("do", action)
        return r.get("output", "")

    def state(self):
        return self.cmd("state", timeout=60)

    def wait(self, frames):
        return self.cmd("wait", str(frames))

    def find(self, text):
        return self.cmd("find", text).get("output", "")

    def wait_up(self, seconds=240):
        t0 = time.time()
        while time.time() - t0 < seconds:
            try:
                return self.state()
            except Exception:
                if self.proc.poll() is not None:
                    raise RuntimeError(f"player on {self.port} exited {self.proc.returncode}")
                time.sleep(2)
        raise RuntimeError(f"player on {self.port} never answered")

    def wait_scene(self, name, seconds=600, invert=False):
        t0 = time.time()
        while time.time() - t0 < seconds:
            s = self.state()
            if (s.get("scene") == name) != invert:
                return s
            self.wait(15)
        raise RuntimeError(f"scene {'left' if invert else 'reached'} {name!r} did not happen in {seconds}s")

    def quit(self):
        try:
            self.cmd("quit", timeout=30)
        except Exception:
            pass
        try:
            self.proc.wait(timeout=60)
        except Exception:
            self.proc.kill()
        self.log.close()


FFMPEG = shutil.which("ffmpeg") or "ffmpeg"


class ShotEncoder:
    """Rolling encode: every finished PNG is pushed into one ffmpeg (near-lossless H.264, 4:4:4, crf 8)
    and deleted, so a shot holds at most two frames on disk. A 1080p frame of a populated cell is
    5-6 MB as a PNG; a 60 s cut is 3600 of them, and the first cut-6 shoot (2026-10-28) filled the
    machine's disk at 1300 frames and took all four players down with it. assemble.py and score.py
    read shot.mp4 (with frames.csv for the game time of every frame)."""

    def __init__(self, d, fps):
        self.d, self.next, self.fps = d, 0, fps
        self.out = os.path.join(d, "shot.mp4")
        self.err = open(os.path.join(d, "encode.log"), "w")
        self.proc = subprocess.Popen(
            [FFMPEG, "-y", "-loglevel", "error", "-f", "image2pipe", "-framerate", str(fps), "-i", "-",
             "-c:v", "libx264", "-preset", "medium", "-crf", "8", "-pix_fmt", "yuv444p", self.out],
            stdin=subprocess.PIPE, stdout=self.err, stderr=self.err)

    def pending(self):
        return len([f for f in os.listdir(self.d) if f.startswith("f") and f.endswith(".png")])

    def feed(self, final=False):
        """Push every frame that a later frame proves complete (the recorder writes them in order)."""
        while True:
            f = os.path.join(self.d, f"f{self.next:05d}.png")
            nxt = os.path.join(self.d, f"f{self.next + 1:05d}.png")
            if not os.path.exists(f) or (not final and not os.path.exists(nxt)):
                return self.next
            with open(f, "rb") as fh:
                self.proc.stdin.write(fh.read())
            os.remove(f)
            self.next += 1

    def finish(self):
        n = self.feed(final=True)
        self.proc.stdin.close()
        self.proc.wait()
        self.err.close()
        return n


def record_and_wait(p, d, frames, every, accum, log):
    """Start the recording, then wait in chunks (one control-port request must not outlive its socket
    timeout: a 1080p sub-frame is about a second in the software renderer), encoding as frames land."""
    log(p.do(f"mv record {d} {frames} {every} {accum}").strip())
    want = frames // every
    enc = ShotEncoder(d, STEP_HZ_OF[0] // every)
    chunk = max(every * 8, 32)
    done_frames = 0
    while done_frames < frames + 3:
        step_n = min(chunk, frames + 3 - done_frames)
        p.wait(step_n)
        done_frames += step_n
        n_now = enc.feed()
        if n_now % 30 == 0 or n_now >= want:
            log(f"  {n_now}/{want} frames")
    n = enc.finish()
    log(f"{n} frames recorded -> shot.mp4")
    return n, want


def write_prefs(profile, prefs, log):
    """Merge the plan's graphics prefs into the profile's prefs.json (what the game's Settings screen writes)."""
    if not prefs:
        return
    base = os.environ.get("XDG_DATA_HOME") or os.path.join(os.path.expanduser("~"), ".local", "share")
    d = os.path.join(base, f"CosmicShore-{profile}")
    os.makedirs(d, exist_ok=True)
    path = os.path.join(d, "prefs.json")
    try:
        cur = json.load(open(path)) if os.path.exists(path) else {}
    except Exception:
        cur = {}
    changed = {k: v for k, v in prefs.items() if cur.get(k) != v}
    cur.update(prefs)
    # Atomic, and only ever BEFORE the players start: a player that reads a half-written prefs.json
    # boots as a first-time user and then saves its empty answers over everyone's (cut 4's first shoot).
    tmp = path + ".tmp"
    with open(tmp, "w") as f:
        json.dump(cur, f)
    os.replace(tmp, path)
    log(f"prefs {path}: set {changed}" if changed else f"prefs {path}: already cinematic")


def answer_first_run(p, log):
    """A fresh profile boots through three prompts; answer them (consent DECLINED). The click points
    were measured at 1600x900 and scale with the window (the canvases scale with it)."""
    s = p.state()
    sx, sy = s.get("width", 1600) / 1600.0, s.get("height", 900) / 900.0

    def click(x, y):
        p.do(f"click {int(x * sx)},{int(y * sy)}")

    for _ in range(40):
        s = p.state()
        if s.get("scene") == MENU:
            return
        if "born" in p.find("What year were you born"):
            log("first run: birth year")
            click(700, 455); p.wait(5); p.do("type 1990"); p.wait(5); click(695, 526); p.wait(40)
        elif "improve" in p.find("Help us improve"):
            log("first run: declining data collection")
            click(730, 579); p.wait(40)
        elif "call you" in p.find("What should we call you"):
            log("first run: pilot name")
            click(800, 357); p.wait(5); p.do("type Director"); p.wait(5); click(652, 556); p.wait(60)
        else:
            p.wait(30)


def stage_menu(p, shot, d, frames, every, accum, k, log, name):
    """A shot staged in the menu's lava-lamp cell: the setup commands swap the cell and release the
    fleet and the life (the Toy Box's own pipelines), then the camera, the cues and the recording."""
    for c in shot.get("setup", []):
        log(f"{c} -> {p.do(c).strip()[:200]}")
        if c.startswith("mv cell"):
            for _ in range(60):                 # the swap tears the old environment down and lays the new one
                st = p.do("mv status")
                if "swapping False" in st:
                    break
                p.wait(int(30 * k))
            p.wait(int(120 * k))
    p.wait(int(shot.get("settle_frames", 240 * k)))
    for c in shot.get("camera", []):
        log(f"{c} -> {p.do(c).strip()}")
    p.wait(int(shot.get("lead_frames", 30 * k)))
    for a in shot.get("events", []):
        at = int(round((a["t"] - shot["t0"]) * STEP_HZ_OF[0]))
        if 0 <= at < frames:
            p.do(f"mv cue {at} {a['cmd']}")
    log(f"{len(shot.get('events', []))} events cued")
    n, want = record_and_wait(p, d, frames, every, accum, log)
    return {"name": name, "frames": n, "dir": d, "ok": n >= want - 2}


STEP_HZ_OF = [240]


def shoot_one(shot, plan, out_dir, port, profile, dll):
    name = shot["name"]
    d = os.path.join(out_dir, name)
    if os.path.isdir(d):
        shutil.rmtree(d)
    os.makedirs(d)
    logf = open(os.path.join(d, "shoot.log"), "w")

    def log(msg):
        line = f"[{time.strftime('%H:%M:%S')}] {msg}"
        print(f"{name}: {msg}", flush=True)
        logf.write(line + "\n"); logf.flush()

    render = plan.get("render", {})
    step_hz = int(render.get("step_hz", 60))
    STEP_HZ_OF[0] = step_hz
    every = int(shot.get("every", max(1, round(step_hz / plan.get("fps", 30)))))
    accum = int(shot.get("accum", 1))
    frames = int(shot.get("frames", round((shot["t1"] - shot["t0"]) * step_hz)))
    k = step_hz / 60.0                      # the menu / card / settle waits were measured at 60 Hz
    log(f"shot {name}: {shot['mode']} {shot['t0']:.2f}-{shot['t1']:.2f} s = {frames} sim frames at {step_hz} Hz, one output frame per {every} (accum {accum})")
    extra = ["--step-hz", str(step_hz)]
    if render.get("msaa") is not None: extra += ["--msaa", str(render["msaa"])]
    if render.get("render_scale") is not None: extra += ["--render-scale", str(render["render_scale"])]
    if render.get("aniso") is not None: extra += ["--aniso", str(render["aniso"])]
    p = Player(port, profile, os.path.join(d, "player.log"), size=render.get("size", "1600x900"), dll=dll, extra=extra)
    try:
        p.wait_up()
        answer_first_run(p, log)
        p.wait_scene(MENU)
        p.wait(int(90 * k))             # the menu finishes building
        p.do("key F7")                  # the game's diagnostics panel starts visible in the engine
        if shot.get("stage") == "menu":
            r = stage_menu(p, shot, d, frames, every, accum, k, log, name)
            logf.close()
            return r
        out = p.do(f"arcade {shot['mode']}")
        log(out.strip())
        if "no card" in out or "no mode" in out:
            raise RuntimeError(out.strip())
        p.wait(int(60 * k))
        if shot.get("players"):
            log(p.do(f"arcade players {shot['players']}").strip())
        if shot.get("intensity"):
            log(p.do(f"arcade intensity {shot['intensity']}").strip())
        p.wait(int(10 * k))
        log(p.do("arcade start").strip())
        s = p.wait_scene(MENU, invert=True)
        log(f"in {s.get('scene')} at frame {s.get('frame')}")
        p.wait(int(120 * k))
        log(p.do("arcade ready").strip())
        for pre in shot.get("pre", []):          # commands before the settle (e.g. autopilot early)
            log(f"{pre} -> {p.do(pre).strip()}")
        # the race must be running before the camera is taken: wait until the local pilot moves, then settle
        for _ in range(40):
            st = p.do("mv status")
            m = re.search(r"local speed ([0-9.]+)", st)
            if m and float(m.group(1)) > 5.0:
                break
            p.wait(int(30 * k))
        log(f"race running: {st.strip().splitlines()[-1][:120] if st.strip() else st}")
        p.wait(int(shot.get("settle_frames", 240 * k)))
        for c in shot.get("setup", ["mv hud off", "mv autopilot on"]):
            log(f"{c} -> {p.do(c).strip()}")
        for c in shot.get("camera", []):
            log(f"{c} -> {p.do(c).strip()}")
        p.wait(int(shot.get("lead_frames", 30 * k)))  # the camera settles on its subject
        for a in shot.get("events", []) + shot.get("accents", []):   # cues are relative to the recording's first frame
            at = int(round((a["t"] - shot["t0"]) * step_hz))
            if 0 <= at < frames:
                p.do(f"mv cue {at} {a['cmd']}")
        log(f"{len(shot.get('events', []))} events cued")
        n, want = record_and_wait(p, d, frames, every, accum, log)
        logf.close()
        return {"name": name, "frames": n, "dir": d, "ok": n >= want - 2}
    except Exception as e:
        log(f"FAILED: {e}")
        logf.close()
        return {"name": name, "frames": 0, "dir": d, "ok": False, "error": str(e)}
    finally:
        p.quit()


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("plan")
    ap.add_argument("--out", help="frames root (default: <plan dir>/frames)")
    ap.add_argument("--jobs", type=int, default=2, help="players at once (each is a whole game; 2 on a 4-core box)")
    ap.add_argument("--only", action="append", help="shoot only these shot names")
    ap.add_argument("--port", type=int, default=47820)
    ap.add_argument("--profile", default="mv")
    ap.add_argument("--player", default=PLAYER_DLL)
    a = ap.parse_args(argv)
    plan = json.load(open(a.plan))
    out = a.out or os.path.join(os.path.dirname(os.path.abspath(a.plan)), "frames")
    os.makedirs(out, exist_ok=True)
    shots = [s for s in plan["shots"] if not a.only or s["name"] in a.only]
    write_prefs(a.profile, plan.get("render", {}).get("prefs"), lambda m: print(m, flush=True))
    results = []
    with ThreadPoolExecutor(max_workers=a.jobs) as ex:
        futures = [ex.submit(shoot_one, s, plan, out, a.port + i, a.profile, a.player) for i, s in enumerate(shots)]
        for f in futures:
            results.append(f.result())
    json.dump(results, open(os.path.join(out, "shoot.json"), "w"), indent=1)
    bad = [r for r in results if not r["ok"]]
    for r in results:
        print(f"{'ok  ' if r['ok'] else 'FAIL'} {r['name']}: {r['frames']} frames {r.get('error', '')}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
