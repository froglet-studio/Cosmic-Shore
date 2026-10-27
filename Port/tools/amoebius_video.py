#!/usr/bin/env python3
"""
A short clip of an all-AI arcade match, made in Amoebius with no Unity, plus the match's replay
(Docs/claude/VIDEOS_AND_PREVIEWS.md section 3 is the recipe this runs).

    python3 Port/tools/amoebius_video.py --mode SkimRace --intensity 2 --players 4 --seconds 60 --out /tmp/v

writes <out>/<Mode>_I<n>.mp4 (1280x720, H.264, 60/--every fps so it plays at game speed) and copies the
match's replay pair (.cstheater + .replay.json) next to it.

How: the Debug player runs windowed under xvfb with a control port, scripted (--frames), so every frame
is a fixed 1/60 s of game time, and with --render-from far ahead, so only the frames it screenshots are
drawn. It drives Menu_Main to the mode as amoebius_markers_run.py does, waits for the HUD's Ready button
(the connecting screen comes first), turns the diagnostics overlay (F7) off, presses Ready, then hands the
local seat to the AI (`arcade autopilot`: the Third Eye's You / AI toggle, a development-build tool, hence
the Debug player). It screenshots every --every frames until the results screen: the race is dark and
the results bright, so the cut is the first frame whose mean brightness jumps, plus --tail seconds. A
match that outlasts --seconds is cut there and left to finish undrawn, so its replay still has results.

A fresh profile boots through the age gate, the data-collection consent and a username. This answers
them for its own throwaway local profile (--profile, default "video"): a fixed adult birth year, consent
DECLINED, a fixed name. Nothing leaves the machine (network and audio are off).

Measured 2026-10-27 (cloud, 4 cores, software rendering): build ~77 s, boot to race 35-60 s, 0.24 s a
drawn frame; a full Skim Race I2 clip and its replay with results in about 5 minutes.
"""
import argparse, glob, os, shutil, statistics, subprocess, sys, time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from amoebius_markers_run import PLAYER, Player  # noqa: E402

try:
    from PIL import Image, ImageStat
except ImportError:  # ffmpeg's signalstats reads it instead (slower)
    Image = None


def first_run_prompts(p, name):
    """Age gate, consent (declined) and username, for the throwaway profile only."""
    for _ in range(3):
        if p.scene() != "Authentication":
            return
        if p.click("BirthYear"):
            p.do("type 1990")
            p.wait(5)
            p.click("Button_Continue")
        p.click("Button_No thanks")
        if p.click("UsernameInputField"):
            p.do(f"type {name}")
            p.wait(5)
            p.click("ConfirmUsernameButton")
        p.wait(120)


def brightness(path):
    """Mean luma of a frame, 0-255."""
    if Image is not None:
        with Image.open(path) as im:
            return ImageStat.Stat(im.convert("L").reduce(8)).mean[0]
    out = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-vf",
                          "signalstats,metadata=print:key=lavfi.signalstats.YAVG", "-f", "null", "-"],
                         capture_output=True, text=True).stderr
    for line in out.splitlines():
        if "YAVG=" in line:
            return float(line.split("YAVG=")[1])
    return 0.0


def recordings(profile):
    root = os.path.join(os.environ.get("XDG_DATA_HOME") or os.path.expanduser("~/.local/share"),
                        f"CosmicShore-{profile}", "Recordings")
    return root, set(glob.glob(os.path.join(root, "*.replay.json")))


def wait_ready_button(p, seconds):
    end = time.time() + seconds
    while time.time() < end:
        if p.center("ReadyButton") is not None:
            return
        p.wait(30)
    raise SystemExit(f"timed out waiting for the Ready button (scene: {p.scene() or 'none'})")


def run(a):
    exe = os.path.join(PLAYER, "bin", a.config, "net10.0", "CosmicShore")
    if not a.no_build or not os.path.exists(exe):
        subprocess.run(["dotnet", "build", PLAYER, "-c", a.config, "-v", "q", "-nologo"], check=True)
    out = os.path.abspath(a.out)
    frames_dir = os.path.join(out, "frames")
    shutil.rmtree(frames_dir, ignore_errors=True)
    os.makedirs(frames_dir)
    rec_root, before = recordings(a.profile)

    env = dict(os.environ, COSMIC_SHORE_PROFILE=a.profile, COSMIC_SHORE_NET="off", COSMIC_SHORE_AUDIO="off")
    w, h = a.size.split("x")
    cmd = [exe, "--frames", "2000000", "--render-from", "2000000000", "--size", a.size,
           "--seed", str(a.seed), "--control-port", str(a.port)]
    if shutil.which("xvfb-run"):
        cmd = ["xvfb-run", "-a", "-s", f"-screen 0 {w}x{h}x24"] + cmd
    elif not os.environ.get("DISPLAY"):
        raise SystemExit("needs xvfb-run (or a DISPLAY) to draw frames")
    log_path = os.path.join(out, "player.log")
    log = open(log_path, "w")
    proc = subprocess.Popen(cmd, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    p = Player(a.port)
    t0 = time.time()
    shots, results_at = [], None
    try:
        p.wait_scene({"Authentication", "Menu_Main"}, 300)
        first_run_prompts(p, "videopilot")
        p.wait_scene({"Menu_Main"}, 180)
        p.wait(60)
        print(p.do(f"arcade {a.mode}").strip())
        p.wait(30)
        p.do(f"arcade intensity {a.intensity}")
        p.do(f"arcade players {a.players}")
        p.wait(10)
        p.do("arcade start")
        p.wait_scene({a.scene or f"Minigame{a.mode}"}, 300)
        wait_ready_button(p, 300)
        p.do("key F7")
        p.wait(5)
        print(p.do("arcade ready").strip())
        p.wait(10)
        print(p.do("arcade autopilot").strip())
        print(f"racing after {time.time() - t0:.0f} s; recording")

        per_second = 60 / a.every
        limit = int(a.seconds * per_second)
        tail = int(a.tail * per_second)
        lum = []
        start = p.cmd("state")["frame"]
        while len(shots) < limit:
            # A screenshot is of the NEXT presented frame, so pace on the player's frame counter: shot k is
            # game frame start + k * every whatever the wait and the capture each advance.
            ahead = start + (len(shots) + 1) * a.every - 1 - p.cmd("state")["frame"]
            if ahead > 0:
                p.wait(ahead)
            path = os.path.join(frames_dir, f"f{len(shots):05d}.png")
            p.cmd("screenshot", path)
            shots.append(path)
            b = brightness(path)
            lum.append(b)
            if results_at is None and len(lum) > 3:
                race = statistics.median(lum[:-1][-60:])
                if b > race + a.jump and b > 2 * race:
                    results_at = len(shots) - 1
                    limit = min(limit, results_at + 1 + tail)
                    print(f"results screen at {results_at / per_second:.1f} s (luma {race:.0f} -> {b:.0f})")
            if len(shots) % int(5 * per_second) == 0:
                print(f"  {len(shots) / per_second:.0f} s of game, {time.time() - t0:.0f} s wall")

        # The replay is written when the end screen shows; a match longer than the clip finishes undrawn.
        end = time.time() + a.finish_timeout
        new = []
        while time.time() < end:
            new = sorted(set(glob.glob(os.path.join(rec_root, "*.replay.json"))) - before)
            if new:
                break
            p.wait(600)
        if not new:
            print(f"no replay in {rec_root} within {a.finish_timeout} s (did the match end?)")
    finally:
        try:
            p.cmd("quit", timeout=10)
        except Exception:
            pass
        try:
            proc.wait(30)
        except subprocess.TimeoutExpired:
            os.killpg(proc.pid, 9)
        log.close()

    mp4 = os.path.join(out, f"{a.mode}_I{a.intensity}.mp4")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-framerate", f"{60 / a.every:g}",
                    "-i", os.path.join(frames_dir, "f%05d.png"), "-c:v", "libx264", "-crf", "28",
                    "-pix_fmt", "yuv420p", "-movflags", "+faststart", mp4], check=True)
    print(f"wrote {mp4} ({len(shots) / (60 / a.every):.1f} s, {os.path.getsize(mp4) / 1e6:.1f} MB"
          f"{', ends in the results screen' if results_at is not None else ', no results screen in the clip'})")
    for sidecar in new:
        theater = sidecar[: -len(".replay.json")] + ".cstheater"
        for f in (theater, sidecar):
            if os.path.exists(f):
                shutil.copy2(f, out)
        print(f"replay: {os.path.join(out, os.path.basename(theater))} (+ .replay.json)")
    if not a.keep_frames:
        shutil.rmtree(frames_dir, ignore_errors=True)
    print(f"done in {time.time() - t0:.0f} s; player log {log_path}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--mode", default="SkimRace", help="GameModes member (default SkimRace)")
    ap.add_argument("--scene", default="", help="scene the mode loads, if not Minigame<Mode>")
    ap.add_argument("--intensity", type=int, default=2)
    ap.add_argument("--players", type=int, default=4, help="seats; AI fill the ones no human takes")
    ap.add_argument("--seconds", type=float, default=60, help="longest clip, in seconds of game time")
    ap.add_argument("--every", type=int, default=4, help="draw one frame every N game frames (4 = 15 fps)")
    ap.add_argument("--tail", type=float, default=3, help="seconds kept after the results screen shows")
    ap.add_argument("--jump", type=float, default=60, help="luma rise over the race's median that marks the results")
    ap.add_argument("--finish-timeout", type=float, default=600, help="real seconds to wait for the replay after the clip")
    ap.add_argument("--size", default="1280x720")
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--config", default="Debug", choices=["Debug", "Release"],
                    help="Debug: autopilot is a development-build tool, Release leaves the seat idle")
    ap.add_argument("--profile", default="video")
    ap.add_argument("--port", type=int, default=47820)
    ap.add_argument("--out", default="/tmp/amoebius_video", help="folder for the mp4, the replay pair and the log")
    ap.add_argument("--keep-frames", action="store_true")
    ap.add_argument("--no-build", action="store_true")
    run(ap.parse_args())


if __name__ == "__main__":
    main()
