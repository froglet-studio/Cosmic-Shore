#!/usr/bin/env python3
"""
Per-system timings for one arcade match, measured in Prisma with no Unity: boot the player
headless, drive Menu_Main to the mode the way a player would (the configure modal's own
handlers), press Ready, open a marker window, let the match run, and write the game's own
ProfilerMarker figures (time, calls and bytes allocated per marker) as JSON.

    python3 Port/tools/prisma_markers_run.py                                  # Skim Race, I2, 3 seats
    python3 Port/tools/prisma_markers_run.py --mode SkimRace --intensity 2 --players 3 --frames 1800 \\
        --out /tmp/skim.json --label baseline
    python3 Port/tools/prisma_markers_run.py --compare /tmp/a.json /tmp/b.json   # A/B two runs

What carries over to Unity: the marker NAMES (the same ones diag reports), allocations (the game's
own code allocates the same way) and the ranking / scaling between runs. What does not: absolute
milliseconds (.NET, not Mono/IL2CPP; no Burst; jobs run on the loop thread here). Compare Prisma
runs with Prisma runs, and confirm a win in a Unity Release diag.

Noise (Skim Race I2, 3 seats, 1800 frames, 2026-10-09): allocations repeat to ~1%; milliseconds to
5-10% on the top markers (with the default fully-optimised JIT). A timing change smaller than that
needs repeated runs; an allocation change does not.

A fresh profile boots through the age gate, the data-collection consent and a username. This
answers them for its own throwaway local profile (--profile, default "perf"): a fixed adult birth
year, consent DECLINED, a fixed name. Nothing leaves the machine (network and audio are off).
"""
import argparse, json, os, re, subprocess, sys, time, urllib.request

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PLAYER = os.path.join(REPO, "Port", "src", "CosmicShore.Player")


class Player:
    def __init__(self, port):
        self.url = f"http://127.0.0.1:{port}/"

    def cmd(self, cmd, arg="", timeout=600):
        body = json.dumps({"cmd": cmd, "arg": arg}).encode()
        req = urllib.request.Request(self.url, data=body, method="POST")
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())

    def do(self, action):
        return self.cmd("do", action).get("output", "")

    def wait(self, frames):
        self.cmd("wait", str(frames))

    def scene(self):
        try:
            return self.cmd("state", timeout=5).get("scene", "")
        except Exception:
            return ""

    def wait_scene(self, names, seconds):
        end = time.time() + seconds
        while time.time() < end:
            s = self.scene()
            if s in names:
                return s
            time.sleep(1)
        raise SystemExit(f"timed out waiting for {names} (last scene: {self.scene() or 'none'})")

    def center(self, name):
        """Screenshot-pixel centre of an active UI object, from dump_ui's screen rect (bottom-left origin)."""
        out = self.cmd("dump_ui", f"{name}:0").get("output", "")
        m = re.search(r"active=True/True rect=\((-?\d+),(-?\d+)\)-\((-?\d+),(-?\d+)\)", out)
        if not m:
            return None
        x0, y0, x1, y1 = map(int, m.groups())
        height = self.cmd("state").get("height", 900)
        return (x0 + x1) // 2, height - (y0 + y1) // 2

    def click(self, name):
        c = self.center(name)
        if c is None:
            return False
        self.do(f"click {c[0]},{c[1]}")
        self.wait(10)
        return True


def first_run_prompts(p):
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
            p.do("type perfpilot")
            p.wait(5)
            p.click("ConfirmUsernameButton")
        p.wait(120)


def run(a):
    exe = os.path.join(PLAYER, "bin", a.config, "net10.0", "CosmicShore")
    if not a.no_build or not os.path.exists(exe):
        subprocess.run(["dotnet", "build", PLAYER, "-c", a.config, "-v", "q", "-nologo"], check=True)
    env = dict(os.environ, COSMIC_SHORE_PROFILE=a.profile, COSMIC_SHORE_NET="off", COSMIC_SHORE_AUDIO="off")
    if not a.tiered:
        # Fully optimised JIT from the first call. With tiering on, a short headless window still
        # runs part of the hot code at tier 0: measured on Skim Race (2026-10-09) the pilot read ~3x
        # its steady cost and identical runs differed by up to 20%; with this off, by 5-10%.
        env["DOTNET_TieredCompilation"] = "0"
    log = open(a.out + ".log", "w")
    proc = subprocess.Popen([exe, "--headless", "--seed", str(a.seed), "--control-port", str(a.port)],
                            env=env, stdout=log, stderr=subprocess.STDOUT)
    p = Player(a.port)
    try:
        p.wait_scene({"Authentication", "Menu_Main"}, 180)
        first_run_prompts(p)
        p.wait_scene({"Menu_Main"}, 180)
        p.wait(60)
        print(p.do(f"arcade {a.mode}").strip())
        p.wait(30)
        p.do(f"arcade intensity {a.intensity}")
        p.do(f"arcade players {a.players}")
        p.wait(10)
        p.do("arcade start")
        p.wait_scene({s for s in [a.scene or f"Minigame{a.mode}"]}, 300)
        p.wait(120)
        p.do("arcade ready")
        p.wait(a.warmup)
        p.do("markers reset")
        p.wait(a.frames)
        p.do(f"markers json {a.out}")
        print(p.do(f"markers {a.top}"))
    finally:
        try:
            p.cmd("quit", timeout=10)
        except Exception:
            pass
        try:
            proc.wait(30)
        except subprocess.TimeoutExpired:
            proc.kill()
    with open(a.out) as f:
        doc = json.load(f)
    doc.update(label=a.label, mode=a.mode, intensity=a.intensity, players=a.players, seed=a.seed,
               config=a.config, warmupFrames=a.warmup, tieredJit=a.tiered)
    with open(a.out, "w") as f:
        json.dump(doc, f, indent=2)
    print(f"wrote {a.out} ({len(doc['markers'])} markers over {doc['frames']} frames)")


def compare(a_path, b_path, top):
    a, b = (json.load(open(p)) for p in (a_path, b_path))
    am = {m["name"]: m for m in a["markers"]}
    bm = {m["name"]: m for m in b["markers"]}
    names = sorted(set(am) | set(bm), key=lambda n: -max(am.get(n, {}).get("avgMsPerFrame", 0), bm.get(n, {}).get("avgMsPerFrame", 0)))
    print(f"A = {a.get('label') or a_path}   B = {b.get('label') or b_path}")
    print(f"{'marker':<46} {'A ms/f':>8} {'B ms/f':>8} {'delta':>7}   {'A KB/f':>8} {'B KB/f':>8}")
    for n in names[:top]:
        x, y = am.get(n, {}), bm.get(n, {})
        ta, tb = x.get("avgMsPerFrame", 0), y.get("avgMsPerFrame", 0)
        delta = f"{(tb - ta) / ta * 100:+6.0f}%" if ta > 0 else "   new"
        print(f"{n[:46]:<46} {ta:8.4f} {tb:8.4f} {delta:>7}   {x.get('kbPerFrame', 0):8.3f} {y.get('kbPerFrame', 0):8.3f}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--mode", default="SkimRace", help="GameModes member (default SkimRace)")
    ap.add_argument("--scene", default="", help="scene the mode loads, if not Minigame<Mode>")
    ap.add_argument("--intensity", type=int, default=2)
    ap.add_argument("--players", type=int, default=3, help="seats; AI fill the ones no human takes")
    ap.add_argument("--frames", type=int, default=1800, help="frames in the marker window (60 per second)")
    ap.add_argument("--warmup", type=int, default=240, help="frames after Ready before the window opens")
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--config", default="Release", choices=["Release", "Debug"])
    ap.add_argument("--profile", default="perf")
    ap.add_argument("--port", type=int, default=47811)
    ap.add_argument("--top", type=int, default=30)
    ap.add_argument("--label", default="")
    ap.add_argument("--out", default="/tmp/prisma_markers.json")
    ap.add_argument("--no-build", action="store_true")
    ap.add_argument("--tiered", action="store_true", help="keep .NET tiered JIT on (default off: steadier numbers)")
    ap.add_argument("--compare", nargs=2, metavar=("A", "B"))
    a = ap.parse_args()
    if a.compare:
        compare(*a.compare, a.top)
    else:
        run(a)


if __name__ == "__main__":
    main()
