"""Headless screenshots of swarm_demo.html -> creatures/shots/swarm_*.png (three r128 routed to a local copy).

    python Tools/Ecology/flight/creatures/swarm_shots.py [--three /path/to/three.min.js]
"""
import argparse
import os

from playwright.sync_api import sync_playwright

HERE = os.path.dirname(os.path.abspath(__file__))
CHROME = "/opt/pw-browsers/chromium-1194/chrome-linux/chrome"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--three", default="/home/claude/scratch/three.min.js")
    a = ap.parse_args()
    three = open(a.three, "rb").read()
    out = os.path.join(HERE, "shots")
    os.makedirs(out, exist_ok=True)
    with sync_playwright() as p:
        kw = {"executable_path": CHROME} if os.path.exists(CHROME) else {}
        b = p.chromium.launch(args=["--use-gl=swiftshader", "--enable-webgl", "--ignore-gpu-blocklist"], **kw)
        pg = b.new_page(viewport={"width": 1100, "height": 720})
        errs = []
        pg.on("pageerror", lambda e: errs.append(str(e)))
        pg.route("https://cdn.jsdelivr.net/**", lambda r: r.fulfill(status=200, body=three, content_type="application/javascript"))
        pg.goto("file://" + os.path.join(HERE, "swarm_demo.html"))
        pg.wait_for_function("window.__demo !== undefined", timeout=20000)
        pg.evaluate("__demo.cam.dist = 42")
        shots = []

        def shot(name):
            path = os.path.join(out, "swarm_" + name + ".png")
            pg.screenshot(path=path)
            shots.append(path)

        for e in ["mass", "space", "charge", "time"]:
            pg.evaluate(f"__demo.setEl('{e}'); __demo.advance(3.0); __demo.measure(); __demo.advance(0.02)")
            shot(e)
        pg.evaluate("__demo.setEl('mass'); __demo.advance(3.0); __demo.setEl('space'); __demo.advance(0.45)")
        shot("morph_whale_to_jelly")
        pg.evaluate("__demo.setEl('mass'); __demo.advance(3.0); __demo.strikeCentre(); __demo.advance(0.3)")
        shot("hit_whale")
        pg.evaluate("__demo.advance(2.5); __demo.measure(); __demo.advance(0.02)")
        shot("hit_whale_recovered")
        pg.evaluate("__demo.creature.setComposition([0.2, 0.25, 0.4, 0.15]); __demo.advance(3.0)")
        shot("mixed_composition")
        b.close()
    print("errors:", errs[:3])
    for s in shots:
        print(s, os.path.getsize(s))


if __name__ == "__main__":
    main()
