"""Headless check of nca_gallery.html: every species loads, grows from one cell, regrows a bite out of its middle,
and swims. Writes shots/gallery_<species>_*.png and results/gallery_browser.json.

    python Tools/Ecology/flight/creatures/gallery_browser.py --three PATH
"""
import argparse, json, os
from playwright.sync_api import sync_playwright
HERE = os.path.dirname(os.path.abspath(__file__)); FL = os.path.dirname(HERE)
CHROME = "/opt/pw-browsers/chromium-1194/chrome-linux/chrome"
ap = argparse.ArgumentParser(); ap.add_argument("--three", required=True); ap.add_argument("--shots", default=os.path.join(FL, "shots")); a = ap.parse_args()
three = open(a.three, "rb").read(); os.makedirs(a.shots, exist_ok=True)
url = "file://" + os.path.join(HERE, "nca_gallery.html")
out, fails = {}, []
with sync_playwright() as p:
    b = p.chromium.launch(executable_path=CHROME, args=["--use-gl=swiftshader", "--enable-unsafe-swiftshader"])
    ctx = b.new_context(viewport={"width": 1100, "height": 720})
    ctx.route("**/cdn.jsdelivr.net/**", lambda r: r.fulfill(status=200, body=three, content_type="application/javascript"))
    pg = ctx.new_page(); errs = []; pg.on("pageerror", lambda e: errs.append(str(e)))
    pg.goto(url); pg.wait_for_timeout(1500)
    keys = pg.evaluate("[...document.querySelectorAll('#species button')].map(b => b.dataset.k)") or ["lizard"]
    for k in keys:
        if len(keys) > 1: pg.click(f"#species button[data-k={k}]")
        pg.wait_for_timeout(4000); g0 = pg.evaluate("__gallery()")
        pg.screenshot(path=os.path.join(a.shots, f"gallery_{k}_adult.png"))
        pg.click("#core"); pg.wait_for_timeout(300); g1 = pg.evaluate("__gallery()")
        pg.wait_for_timeout(6000); g2 = pg.evaluate("__gallery()")
        pg.click("#seed"); pg.wait_for_timeout(2000); g3 = pg.evaluate("__gallery()")
        pg.screenshot(path=os.path.join(a.shots, f"gallery_{k}_growing.png"))
        pg.wait_for_timeout(14000); g4 = pg.evaluate("__gallery()")
        pg.click("#swim"); pg.wait_for_timeout(5000); g5 = pg.evaluate("__gallery()")
        pg.screenshot(path=os.path.join(a.shots, f"gallery_{k}_swim.png")); pg.click("#swim")
        moved = sum(x * x for x in g5["pos"]) ** 0.5
        r = {"adult": g0["count"], "bitten": g1["count"], "after_6s": g2["count"], "seed_2s": g3["count"], "seed_16s": g4["count"],
             "grown_at_s": g4["grownAt"], "swim_moved": round(moved, 1), "backend": g0["backend"], "steps": g4["steps"]}
        out[k] = r; print(k, r)
        if r["bitten"] >= r["adult"]: fails.append(f"{k}: bite removed nothing")
        if r["after_6s"] < 0.85 * r["adult"]: fails.append(f"{k}: bite not regrown to 85% in 6 s")
        if r["seed_16s"] < 0.5 * r["adult"]: fails.append(f"{k}: not half grown 16 s after one cell")
        if moved < 5: fails.append(f"{k}: swim did not move")
    out["errors"] = errs; fails += [f"page error: {e}" for e in errs]
    b.close()
out["fails"] = fails
json.dump(out, open(os.path.join(FL, "results", "gallery_browser.json"), "w"), indent=1)
print("fails:", fails)
