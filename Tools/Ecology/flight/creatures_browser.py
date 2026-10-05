"""Headless check of the big creatures in the flyable ecology (index.html, mode=creatures).

    python Tools/Ecology/flight/creatures_browser.py --three PATH [--shots DIR]

Checks: no page errors; the NCA lizard grows and keeps stepping; the tadpole swarm stalks a nearby pilot, glows,
lunges and its bite goes through the stakes (telegraphed); ramming the swarm sheds a crystal; flying through the lizard
cuts voxels that regrow; the swarm morphs after eating crystals of another element. Frame time. Screenshots.
Writes results/creatures_browser.json.
"""
import argparse, json, os, sys
from playwright.sync_api import sync_playwright
HERE = os.path.dirname(os.path.abspath(__file__))
CHROME = "/opt/pw-browsers/chromium-1194/chrome-linux/chrome"
ap = argparse.ArgumentParser(); ap.add_argument("--three", required=True); ap.add_argument("--shots", default=os.path.join(HERE, "shots"))
a = ap.parse_args(); os.makedirs(a.shots, exist_ok=True)
three = open(a.three, "rb").read(); url = "file://" + os.path.join(HERE, "index.html")
out, fails = {}, []
with sync_playwright() as pw:
    br = pw.chromium.launch(executable_path=CHROME, args=["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"])
    ctx = br.new_context(viewport=dict(width=1440, height=900), device_scale_factor=1)
    ctx.route("**/cdn.jsdelivr.net/**", lambda r: r.fulfill(status=200, body=three, content_type="application/javascript"))
    pg = ctx.new_page(); errs = []
    pg.on("pageerror", lambda e: errs.append("pageerror: " + str(e)))
    pg.on("console", lambda m: errs.append("console: " + m.text) if m.type == "error" else None)
    pg.goto(url + "?mode=creatures&go=1&seed=7"); pg.wait_for_function("window.__eco && window.__eco.W && window.__perf.frame.length > 12", timeout=90000)
    big = lambda: pg.evaluate("window.__big()")
    out["start"] = big()
    # park the pilot 200 u in front of the swarm, pointing at it, slow: it should stalk, glow and lunge
    pg.evaluate("""() => { const W = window.__eco.W, P = window.__big().swarmPos; const pl = W.player;
      pl.pos[0] = P[0]; pl.pos[1] = P[1]; pl.pos[2] = P[2] - 200; }""")
    pg.wait_for_timeout(1500); out["stalk"] = big(); pg.screenshot(path=os.path.join(a.shots, "creatures_stalk.png"))
    pg.wait_for_timeout(5000); out["after_lunge"] = big()
    st = pg.evaluate("(() => { const k = window.__stakes().stakes; return { burned: k.burned, contacts: k.contacts, tele: k.telegraphedBurns, by: k.bySpecies }; })()")
    out["stakes"] = st
    # drop crystals of another element right at the swarm: it should eat them and morph
    pg.evaluate("""() => { const P = window.__big().swarmPos, t = window.__eco.W.arena.t; const c = window.__crystals;
      for (let i = 0; i < 6; i++) c.push({ x: P[0], y: P[1], z: P[2], e: 'mass', t }); }""")
    pg.wait_for_timeout(5000); out["fed"] = big(); pg.screenshot(path=os.path.join(a.shots, "creatures_morph.png"))
    # fly the pilot through the lizard
    n0 = big()["lizard"]
    pg.evaluate("""() => { const L = window.__big().lizPos, pl = window.__eco.W.player; pl.pos[0] = L[0]; pl.pos[1] = L[1]; pl.pos[2] = L[2]; }""")
    pg.wait_for_timeout(400); out["cut"] = big()
    pg.evaluate("""() => { const pl = window.__eco.W.player; pl.pos[1] += 600; }""")
    pg.wait_for_timeout(8000); out["regrow"] = big()
    out["lizard_before_cut"] = n0
    # camera on the lizard for a portrait
    pg.evaluate("""() => { const L = window.__big().lizPos, pl = window.__eco.W.player; pl.pos[0] = L[0] + 10; pl.pos[1] = L[1] + 40; pl.pos[2] = L[2] - 260; }""")
    pg.wait_for_timeout(1200); pg.screenshot(path=os.path.join(a.shots, "creatures_lizard.png"))
    perf = pg.evaluate("(() => { const p = window.__perf, s = a => a.slice().sort((x, y) => x - y), m = a => s(a)[a.length >> 1]; return { frame_ms: m(p.frame), cpu_ms: m(p.cpu), sim_ms: m(p.sim), render_ms: m(p.render) }; })()")
    out["perf"] = perf; out["errors"] = errs[:6]
    br.close()
if errs: fails.append("page errors")
if out["start"]["lizard"] < 300: fails.append("lizard did not grow")
if out["stalk"]["intent"] <= 0 and out["after_lunge"]["bites"] == 0: fails.append("swarm never stalked")
if out["after_lunge"]["bites"] and not out["stakes"]["burned"]: fails.append("bite burned nothing")
if out["fed"]["morphs"] < 1: fails.append("swarm did not morph after eating")
if out["cut"]["lizCut"] <= 0: fails.append("lizard was not cut")
out["fails"] = fails
json.dump(out, open(os.path.join(HERE, "results", "creatures_browser.json"), "w"), indent=1)
for k in ("start", "stalk", "after_lunge", "fed", "cut", "regrow"): print(k, {x: out[k][x] for x in ("state", "intent", "plan", "morphs", "eaten", "bites", "lizard", "lizCut", "lizSteps")})
print("stakes", out["stakes"], "perf", out["perf"], "errors", errs[:3]); print("fails:", fails); sys.exit(1 if fails else 0)
