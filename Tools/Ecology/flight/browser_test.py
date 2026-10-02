"""Headless browser check of the flyable ecology (index.html).

    python Tools/Ecology/flight/browser_test.py [--three PATH] [--shots DIR]

Loads the page in headless Chromium (the copy Playwright ships at /opt/pw-browsers) with three.js served from a local
copy (--three, default: download once from jsDelivr into the scratch dir is NOT attempted - pass a local file), then:
  1. no page errors / console errors on load, in every mode;
  2. the vessel MOVES under scripted keys (W throttle, A/D yaw, arrow pitch) - position and heading change;
  3. species REACT to the player: a thief tails it (lifts its fresh wake - steal hits), a pack rings it
     (the pack's own closure quorum reaches the 0.55 strike threshold);
  4. frame time in whole-cell mode (sim + render CPU per frame, and wall frame time) over a fixed window;
  5. screenshots at desktop (1440x900) and phone (390x844) width into --shots.
Writes results/browser_test.json. Exit 1 on any failure.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys

from playwright.sync_api import sync_playwright

HERE = os.path.dirname(os.path.abspath(__file__))
CHROME = "/opt/pw-browsers/chromium-1194/chrome-linux/chrome"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--three", required=True, help="local three.min.js r128")
    ap.add_argument("--shots", default=os.path.join(HERE, "shots"))
    a = ap.parse_args()
    os.makedirs(a.shots, exist_ok=True)
    three = open(a.three, "rb").read()
    url = "file://" + os.path.join(HERE, "index.html")
    out, fails = {}, []

    with sync_playwright() as pw:
        br = pw.chromium.launch(executable_path=CHROME, args=["--use-gl=angle", "--use-angle=swiftshader",
                                                               "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"])

        def page(w, h, q):
            ctx = br.new_context(viewport=dict(width=w, height=h), device_scale_factor=1)
            ctx.route("**/cdn.jsdelivr.net/**", lambda r: r.fulfill(status=200, body=three, content_type="application/javascript"))
            pg = ctx.new_page()
            errs = []
            pg.on("pageerror", lambda e: errs.append("pageerror: " + str(e)))
            pg.on("console", lambda m: errs.append("console: " + m.text) if m.type == "error" else None)
            pg.goto(url + q)
            pg.wait_for_function("window.__eco && window.__eco.W", timeout=60000)
            return ctx, pg, errs

        # ---- 1+2+3: one species at a time, scripted keys --------------------------------------------------------
        for mode in ("thief", "pack"):
            ctx, pg, errs = page(1440, 900, f"?mode={mode}&go=1&seed=7")
            pg.wait_for_function("window.__perf.frame.length > 12", timeout=60000)   # shaders compiled, loop running
            s0 = pg.evaluate("({p: window.__eco.W.player.pos.slice(), q: window.__eco.flight.q.toArray()})")
            pg.keyboard.down("w"); pg.wait_for_timeout(1500); pg.keyboard.down("a"); pg.wait_for_timeout(900)
            pg.keyboard.up("a"); pg.keyboard.down("ArrowUp"); pg.wait_for_timeout(600); pg.keyboard.up("ArrowUp")
            pg.wait_for_timeout(600); pg.keyboard.up("w")
            s1 = pg.evaluate("({p: window.__eco.W.player.pos.slice(), q: window.__eco.flight.q.toArray()})")
            moved = math.dist(s0["p"], s1["p"])
            dq = 1 - abs(sum(x * y for x, y in zip(s0["q"], s1["q"])))
            # reaction: sample species-to-player geometry for a while with the autopilot flying (it wanders)
            pg.evaluate("window.__eco.setAuto(true)")
            geo = pg.evaluate("""async () => {
              const out = [];
              for (let k = 0; k < 24; k++) {
                await new Promise(r => setTimeout(r, 500));
                const W = window.__eco.W, sp = W.species[0], P = W.player.pos, d = [], b = [];
                for (let i = 0; i < sp.n; i++) if (sp.alive[i]) {
                  const dx = sp.pos[3*i]-P[0], dy = sp.pos[3*i+1]-P[1], dz = sp.pos[3*i+2]-P[2];
                  d.push(Math.hypot(dx, dy, dz)); b.push(Math.atan2(dx, dz));
                }
                d.sort((x, y) => x - y);
                const near = b.filter((_, j) => d[j] < 250);
                const R = near.length ? Math.hypot(near.reduce((s, x) => s + Math.cos(x), 0), near.reduce((s, x) => s + Math.sin(x), 0)) / near.length : null;
                const cmax = sp.closure ? Math.max(...sp.closure) : null;
                out.push({ cmax, t: W.arena.t, med: d.length ? d[d.length >> 1] : null, min: d[0], nearN: near.length, R, hits: W.arena.log.length });
              }
              return out;
            }""")
            pg.screenshot(path=os.path.join(a.shots, f"desktop_{mode}.png"))
            first, last = geo[0]["med"], min(g["med"] for g in geo[-8:])
            rec = dict(moved=moved, rot=dq, geo=geo, errors=errs)
            if moved < 50: fails.append(f"{mode}: vessel moved only {moved:.1f} u under W")
            if dq < 1e-3: fails.append(f"{mode}: vessel did not turn under A/ArrowUp")
            if errs: fails.append(f"{mode}: page errors {errs[:3]}")
            if mode == "thief":
                # a thief tails your WAKE, not your hull: it follows the fresh trail and lifts it (each lift is a
                # 'steal' hit on you). Tails = it stole from the player's wake within the window.
                rec["steals"] = geo[-1]["hits"]
                rec["tails"] = rec["steals"] > 0
                if not rec["tails"]: fails.append(f"thief: never stole from the player's wake (median dist {first:.0f} -> {last:.0f})")
            if mode == "pack":
                # the pack's own quorum readout: how evenly packmates' bearings cover the sphere round you (0..1);
                # past 0.55 the ring collapses and they strike. Rings = the closure crosses the strike quorum.
                rec["closure_max"] = max(g["cmax"] for g in geo)
                rec["rings"] = rec["closure_max"] >= 0.55 or geo[-1]["hits"] > 0
                if not rec["rings"]: fails.append(f"pack: closure never reached the strike quorum (max {rec['closure_max']:.2f})")
            out[mode] = rec
            ctx.close()

        # ---- every mode loads clean ------------------------------------------------------------------------------
        for mode in ("locust", "lurker", "stampede", "leviathan", "mobber", "grazer", "fortress", "snaptrap"):
            ctx, pg, errs = page(1024, 700, f"?mode={mode}&go=1&auto=1&seed=11")
            pg.wait_for_timeout(2500)
            n = pg.evaluate("window.__eco.W.species[0].n")
            out[mode] = dict(errors=errs, agents=n)
            if errs: fails.append(f"{mode}: page errors {errs[:3]}")
            if mode in ("lurker", "snaptrap", "fortress", "leviathan"):
                pg.screenshot(path=os.path.join(a.shots, f"desktop_{mode}.png"))
            ctx.close()

        # ---- 4+5: whole cell, frame time, screenshots ------------------------------------------------------------
        ctx, pg, errs = page(1440, 900, "?mode=cell&go=1&auto=1&seed=7&debug=1")
        pg.wait_for_timeout(3000)
        pg.evaluate("window.__perf.frame.length = 0; window.__perf.sim.length = 0; window.__perf.render.length = 0; window.__perf.cpu = []; window.__perf.steps = 0")
        pg.wait_for_timeout(8000)
        pf = pg.evaluate("""(() => { const p = window.__perf, m = a => a.reduce((s, x) => s + x, 0) / Math.max(1, a.length),
          q = (a, f) => { const b = a.slice().sort((x, y) => x - y); return b[Math.floor(f * (b.length - 1))] || 0; };
          const W = window.__eco.W; let ag = 0; for (const s of W.species) for (let i = 0; i < s.n; i++) ag += s.alive[i];
          let pr = 0; for (let i = 0; i < W.arena.n; i++) pr += W.arena.malive[i];
          return { frames: p.frame.length, frame_ms: m(p.frame), frame_p95: q(p.frame, 0.95), cpu_ms: m(p.cpu), cpu_p95: q(p.cpu, 0.95),
                   sim_ms: m(p.sim), render_ms: m(p.render), sim_ms_per_step: p.sim.reduce((s, x) => s + x, 0) / Math.max(1, p.steps), agents: ag, prisms: pr, audit: W.audit() }; })()""")
        out["cell_perf"] = pf
        pg.screenshot(path=os.path.join(a.shots, "desktop_cell_debug.png"))
        pg.evaluate("window.__eco.setCam('orbit')"); pg.wait_for_timeout(1200)
        pg.screenshot(path=os.path.join(a.shots, "desktop_cell_orbit.png"))
        if errs: fails.append(f"cell: page errors {errs[:3]}")
        if abs(pf["audit"]["residual"]) > 1e-6 * max(1, pf["audit"]["start"]): fails.append(f"cell: mass audit residual {pf['audit']['residual']}")
        ctx.close()
        ctx, pg, errs = page(1440, 900, "?mode=cell&go=1&auto=1&seed=7")
        pg.wait_for_timeout(5000)
        pg.screenshot(path=os.path.join(a.shots, "desktop_cell.png"))
        pg.evaluate("window.__eco.openRate()"); pg.wait_for_timeout(400)
        pg.screenshot(path=os.path.join(a.shots, "desktop_rating.png"))
        # the export must be readable by emotion/ratings.py: inject one rated encounter, export, run the reader on it
        exp = pg.evaluate("""(() => { window.__eco.enc.ratings.push({ id: 'thief@test', species: 'thief', mode: 'cell', seed: 7,
          t0: 0, t1: 5, hits: 2, kills: 0, emotion: 'playful', threat: 2, readability: 4, fun: 5, note: '', truth: 'playful' });
          return window.__eco.exportJSON(); })()""")
        ep = os.path.join(HERE, "results", "export_sample.json"); open(ep, "w").write(exp)
        import subprocess
        rr = subprocess.run([sys.executable, os.path.join(HERE, "..", "emotion", "ratings.py"), ep], capture_output=True, text=True)
        out["ratings_py"] = rr.stdout.strip()
        if rr.returncode != 0 or "1 rated" not in rr.stdout: fails.append(f"ratings.py could not read the export: {rr.stderr[-300:]}")
        ctx.close()
        ctx, pg, errs = page(390, 844, "?mode=cell&go=1&auto=1&seed=7")
        pg.wait_for_timeout(4000)
        pg.screenshot(path=os.path.join(a.shots, "phone_cell.png"))
        sw = pg.evaluate("document.documentElement.scrollWidth")
        out["phone"] = dict(errors=errs, scrollWidth=sw)
        if errs: fails.append(f"phone: page errors {errs[:3]}")
        if sw > 392: fails.append(f"phone: horizontal overflow ({sw}px)")
        ctx.close()
        ctx, pg, errs = page(390, 844, "?mode=thief")
        pg.wait_for_timeout(1000)
        pg.screenshot(path=os.path.join(a.shots, "phone_start.png"))
        ctx.close()
        br.close()

    out["fails"] = fails
    os.makedirs(os.path.join(HERE, "results"), exist_ok=True)
    json.dump(out, open(os.path.join(HERE, "results", "browser_test.json"), "w"), indent=1)
    print(json.dumps({k: (v if not isinstance(v, dict) or k in ("cell_perf", "phone") else {kk: vv for kk, vv in v.items() if kk != "geo"}) for k, v in out.items()}, indent=1))
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()
