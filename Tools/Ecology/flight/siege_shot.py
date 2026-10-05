"""Headless screenshots of the SIEGE mid-closure (flight/index.html?mode=siege).

    python3 Tools/Ecology/flight/siege_shot.py --three /path/to/three.min.js [--shots Tools/Ecology/flight/shots]

Flies the page's autopilot until the siege is in CLOSE (glow up, iris shrinking) and shoots the chase cam, then the
orbit cam during the next late CLOSE / HOLD; also asserts no page errors. Writes shots/siege_close.png, siege_hold_orbit.png, siege_dive.png.
"""
import argparse
import json
import os
import sys

from playwright.sync_api import sync_playwright

HERE = os.path.dirname(os.path.abspath(__file__))
CHROME = "/opt/pw-browsers/chromium-1194/chrome-linux/chrome"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--three", required=True)
    ap.add_argument("--shots", default=os.path.join(HERE, "shots"))
    ap.add_argument("--seed", type=int, default=7)
    a = ap.parse_args()
    os.makedirs(a.shots, exist_ok=True)
    three = open(a.three, "rb").read()
    with sync_playwright() as pw:
        br = pw.chromium.launch(executable_path=CHROME, args=["--use-gl=angle", "--use-angle=swiftshader",
                                                               "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"])
        ctx = br.new_context(viewport=dict(width=1440, height=900), device_scale_factor=1)
        ctx.route("**/cdn.jsdelivr.net/**", lambda r: r.fulfill(status=200, body=three, content_type="application/javascript"))
        pg = ctx.new_page()
        errs = []
        pg.on("pageerror", lambda e: errs.append("pageerror: " + str(e)))
        pg.on("console", lambda m: errs.append("console: " + m.text) if m.type == "error" else None)
        pg.goto("file://" + os.path.join(HERE, "index.html") + f"?mode=siege&go=1&auto=1&seed={a.seed}")
        pg.wait_for_function("window.__eco && window.__eco.W", timeout=60000)
        sg = "window.__eco.W.species.find(s => s.key === 'siege')"
        out = {}
        pg.wait_for_function(f"(() => {{ const s = {sg}; return (s.phase === 'close' && s.tp > 1.8) || s.phase === 'hold'; }})()", timeout=240000, polling=50)
        out["close"] = pg.evaluate(f"(() => {{ const s = {sg}; return Object.assign(s.report(), {{ t: window.__eco.W.arena.t, prog: s.prog, intent: s.phaseIntent() }}); }})()")
        pg.screenshot(path=os.path.join(a.shots, "siege_close.png"))
        # the next HOLD, from outside (orbit cam shows the shell, its iris and the player inside)
        enc0 = out["close"]["encounters"]
        pg.evaluate("window.__eco.setCam('orbit')")
        pg.wait_for_function(f"(() => {{ const s = {sg}; return s.encounters > {enc0} && (s.phase === 'hold' || (s.phase === 'close' && s.tp > 1.6)); }})()", timeout=300000, polling=50)
        out["hold"] = pg.evaluate(f"(() => {{ const s = {sg}; return Object.assign(s.report(), {{ t: window.__eco.W.arena.t, prog: s.prog }}); }})()")
        pg.screenshot(path=os.path.join(a.shots, "siege_hold_orbit.png"))
        # a dive, from the chase cam (every member converging at once)
        pg.evaluate("window.__eco.setCam('chase')")
        pg.wait_for_function(f"(() => {{ const s = {sg}; return s.phase === 'dive' && s.tp > 0.15; }})()", timeout=400000, polling=30)
        out["dive"] = pg.evaluate(f"(() => {{ const s = {sg}; return Object.assign(s.report(), {{ t: window.__eco.W.arena.t }}); }})()")
        pg.screenshot(path=os.path.join(a.shots, "siege_dive.png"))
        out["errors"] = errs
        br.close()
    print(json.dumps(out))
    sys.exit(1 if errs else 0)


if __name__ == "__main__":
    main()
