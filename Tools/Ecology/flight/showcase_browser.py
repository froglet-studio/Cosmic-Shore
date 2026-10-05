"""Headless check of the showcase cell (index.html, mode=showcase): every species + siege + big creatures + stakes.

    python Tools/Ecology/flight/showcase_browser.py --three PATH [--shots DIR] [--secs 60]

The autopilot flies for --secs; checks no page errors, every species present, the big creatures running, the stakes
ticking; reports frame/sim/render ms; takes screenshots, the last one parked near the siege. Writes
results/showcase_browser.json.
"""
import argparse, json, os, sys
from playwright.sync_api import sync_playwright
HERE = os.path.dirname(os.path.abspath(__file__))
CHROME = "/opt/pw-browsers/chromium-1194/chrome-linux/chrome"
ap = argparse.ArgumentParser(); ap.add_argument("--three", required=True); ap.add_argument("--shots", default=os.path.join(HERE, "shots")); ap.add_argument("--secs", type=float, default=60)
a = ap.parse_args(); os.makedirs(a.shots, exist_ok=True)
three = open(a.three, "rb").read(); url = "file://" + os.path.join(HERE, "index.html")
STATE = """() => { const W = window.__eco.W, k = window.__stakes().stakes, b = window.__big(), sg = W.species.find(s => s.key === 'siege');
  return { t: W.arena.t, species: W.species.map(s => s.key), agents: W.species.reduce((n, s) => n + (s.n || 0), 0), petals: k.total(), burned: k.burned,
    gained: k.gained, by: k.bySpecies, crystals: window.__crystals.length, swarm: b.plan, swarmState: b.state, morphs: b.morphs, lizard: b.lizard,
    siege: sg ? sg.phase : null, siegeEvents: sg ? sg.events.length : 0 }; }"""
out, fails = {}, []
with sync_playwright() as pw:
    br = pw.chromium.launch(executable_path=CHROME, args=["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"])
    ctx = br.new_context(viewport=dict(width=1440, height=900), device_scale_factor=1)
    ctx.route("**/cdn.jsdelivr.net/**", lambda r: r.fulfill(status=200, body=three, content_type="application/javascript"))
    pg = ctx.new_page(); errs = []
    pg.on("pageerror", lambda e: errs.append("pageerror: " + str(e)))
    pg.on("console", lambda m: errs.append("console: " + m.text) if m.type == "error" else None)
    pg.goto(url + "?go=1&seed=11"); pg.wait_for_function("window.__eco && window.__eco.W && window.__perf.frame.length > 12", timeout=90000)
    out["mode"] = pg.evaluate("window.__eco.W.mode")
    pg.evaluate("window.__eco.setAuto(true)")
    trace = []
    for k in range(int(a.secs // 10)):
        pg.wait_for_timeout(10000); trace.append(pg.evaluate(STATE))
        if k == 1: pg.screenshot(path=os.path.join(a.shots, "showcase_flying.png"))
    out["trace"] = trace
    # park near the siege's cloud to provoke it, and photograph the closure
    pg.evaluate("""() => { const W = window.__eco.W, sg = W.species.find(s => s.key === 'siege'); if (!sg) return; let c = [0,0,0], m = 0;
      for (let i = 0; i < sg.n; i++) if (sg.alive[i]) { c[0] += sg.pos[3*i]; c[1] += sg.pos[3*i+1]; c[2] += sg.pos[3*i+2]; m++; }
      const pl = W.player; for (let a = 0; a < 3; a++) pl.pos[a] = c[a] / m; pl.pos[2] -= 380; }""")
    pg.wait_for_timeout(5000); out["siege_near"] = pg.evaluate(STATE); pg.screenshot(path=os.path.join(a.shots, "showcase_siege.png"))
    out["perf"] = pg.evaluate("(() => { const p = window.__perf, m = a => a.slice().sort((x, y) => x - y)[a.length >> 1]; return { frame_ms: m(p.frame), cpu_ms: m(p.cpu), sim_ms: m(p.sim), render_ms: m(p.render) }; })()")
    out["errors"] = errs[:6]; br.close()
last = out["trace"][-1]
if errs: fails.append("page errors")
if out["mode"] != "showcase": fails.append("default mode is not showcase")
for k in ("grazer", "pack", "thief", "locust", "lurker", "stampede", "leviathan", "mobber", "fortress", "snaptrap", "siege"):
    if k not in last["species"]: fails.append("missing " + k)
if not last["lizard"]: fails.append("no lizard")
if last["swarm"] is None: fails.append("no swarm")
out["fails"] = fails
json.dump(out, open(os.path.join(HERE, "results", "showcase_browser.json"), "w"), indent=1)
for r in out["trace"] + [out["siege_near"]]: print({k: r[k] for k in ("t", "agents", "petals", "burned", "gained", "crystals", "swarm", "swarmState", "lizard", "siege", "siegeEvents")})
print("by", last["by"], "perf", out["perf"], "errors", errs[:3]); print("fails:", fails); sys.exit(1 if fails else 0)
