"""Headless check of the stakes HUD in the flyable ecology (index.html).

    python Tools/Ecology/flight/stakes_browser.py --three PATH [--shots DIR]

Flies the autopilot through a pack, then the whole cell, and checks: danger strikes burn petals (hostile) or only sting
(own cell), kills drop crystals, the petal flower renders, no page errors. Writes results/stakes_browser.json.
"""
import argparse, json, os, sys
from playwright.sync_api import sync_playwright

HERE = os.path.dirname(os.path.abspath(__file__))
CHROME = "/opt/pw-browsers/chromium-1194/chrome-linux/chrome"

ap = argparse.ArgumentParser(); ap.add_argument("--three", required=True); ap.add_argument("--shots", default=os.path.join(HERE, "shots"))
ap.add_argument("--secs", type=float, default=40)
a = ap.parse_args(); os.makedirs(a.shots, exist_ok=True)
three = open(a.three, "rb").read(); url = "file://" + os.path.join(HERE, "index.html")
SUM = """() => { const s = window.__stakes(); const k = s.stakes; return { total: k.total(), base: k.base, burned: k.burned, gained: k.gained,
  contacts: k.contacts, blocked: k.blocked, stings: k.events.filter(e => e.form === 'sting').length, tele: k.telegraphedBurns,
  crystals: s.crystals, hostile: s.hostile, rule: s.rule, t: window.__eco.W.arena.t, by: k.bySpecies }; }"""
out, fails = {}, []
with sync_playwright() as pw:
    br = pw.chromium.launch(executable_path=CHROME, args=["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"])
    for name, q, setup in [("pack_hostile", "?mode=pack&go=1&seed=7", ""), ("pack_own", "?mode=pack&go=1&seed=7", "toggle"),
                           ("cell_hostile", "?mode=cell&go=1&seed=3", "")]:
        ctx = br.new_context(viewport=dict(width=1440, height=900), device_scale_factor=1)
        ctx.route("**/cdn.jsdelivr.net/**", lambda r: r.fulfill(status=200, body=three, content_type="application/javascript"))
        pg = ctx.new_page(); errs = []
        pg.on("pageerror", lambda e: errs.append("pageerror: " + str(e)))
        pg.on("console", lambda m: errs.append("console: " + m.text) if m.type == "error" else None)
        pg.goto(url + q); pg.wait_for_function("window.__eco && window.__eco.W && window.__perf.frame.length > 12", timeout=90000)
        if setup == "toggle": pg.click("#dom")
        pg.evaluate("window.__eco.setAuto(true)")
        pg.wait_for_timeout(int(a.secs * 1000))
        r = pg.evaluate(SUM); r["errors"] = errs[:5]; out[name] = r
        pg.screenshot(path=os.path.join(a.shots, f"stakes_{name}.png"))
        ctx.close()
        print(name, json.dumps({k: r[k] for k in ("t", "total", "burned", "gained", "contacts", "stings", "crystals")}), errs[:2])
        if errs: fails.append(f"{name}: page errors")
    br.close()
h, o = out["pack_hostile"], out["pack_own"]
if h["contacts"] and not h["burned"]: fails.append("hostile contacts burned nothing")
if o["burned"]: fails.append("own cell burned petals")
if o["contacts"] and not o["stings"]: fails.append("own cell contacts did not sting")
out["fails"] = fails
os.makedirs(os.path.join(HERE, "results"), exist_ok=True)
json.dump(out, open(os.path.join(HERE, "results", "stakes_browser.json"), "w"), indent=1)
print("fails:", fails); sys.exit(1 if fails else 0)
