"""Headless browser check of the flyable ecology (index.html).

    python Tools/Ecology/flight/browser_test.py [--three PATH] [--shots DIR]

Loads the page in headless Chromium (the copy Playwright ships at /opt/pw-browsers) with three.js served from a local
copy (--three, default: download once from jsDelivr into the scratch dir is NOT attempted - pass a local file), then:
  1. no page errors / console errors on load, in every mode;
  2. the vessel MOVES under scripted keys (W throttle, A/D yaw, arrow pitch) - position and heading change;
  3. species REACT to the player: a thief tails it (lifts its fresh wake - steal hits), a pack rings it
     (the pack's own closure quorum reaches the 0.55 strike threshold);
  4. frame time in whole-cell mode (sim + render CPU per frame, and wall frame time) over a fixed window;
  5. screenshots at desktop (1440x900) and phone (390x844) width into --shots;
  6. the GAME flight model (default; ?flight=arcade is the old one), stepped deterministically on a paused page:
     drift decouples heading from velocity (and releases), Soar multiplies the throttle target by 4 x the Time
     ElementalFloat, Mass grows the wake's prism volume, the Yastri trigger turn and the touch thumb-lift Yastri
     turn at the authored rates, and the arcade model has none of it.
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

        # ---- 6: the game flight model, stepped deterministically (paused page: only these steps move the world) --
        FM_JS = r"""() => {
          const E = window.__eco, f = E.flight, W = E.W, K = E.input.keys, T = 1 / 30, D = 180 / Math.PI;
          const fwd = (q) => new THREE.Vector3(0, 0, 1).applyQuaternion(q || f.q);
          const vel = () => new THREE.Vector3(...W.player.vel).normalize();
          const ang = (a, b) => Math.acos(Math.max(-1, Math.min(1, a.dot(b)))) * D;
          const run = (secs, keys) => { K.clear(); for (const k of keys) K.add(k); const n = Math.round(secs / T); for (let i = 0; i < n; i++) { E.flyStep(T); W.step(T); } K.clear(); };
          const o = { game: E.FM.game };
          run(2.5, ['KeyW']); o.cruise = f.speed;
          run(1.2, ['KeyA']); o.slip_turn = ang(vel(), fwd());
          run(1.0, []);
          run(0.2, ['KeyA', 'Space']); const v0 = vel(), h0 = fwd();
          run(1.0, ['KeyA', 'Space']); o.drifting = f.drifting;
          o.drift_heading_deg = ang(h0, fwd()); o.drift_velocity_deg = ang(v0, vel()); o.slip_drift = ang(vel(), fwd());
          run(0.4, []); o.slip_after = ang(vel(), fwd()); o.drifting_after = f.drifting;
          // Yastri, trigger path (X = right trigger): the commanded rotation turns at 60 deg/s, the same way as D
          run(1.0, []); let a0 = fwd(f.qa); run(1.0, ['KeyX']); const a1 = fwd(f.qa);
          o.yastri_trigger_deg = ang(a0, a1); o.yastri_trigger_right = new THREE.Vector3().crossVectors(a0, a1).dot(new THREE.Vector3(0, 1, 0).applyQuaternion(f.q)) < 0;
          // Yastri, touch path: two thumbs down, lift the right one -> YawsteryAction-Left, ramped in over 0.35 s
          run(1.0, []); E.input.touchL[2] = 1; E.input.touchR[2] = 1; run(0.1, []); E.input.touchR[2] = 0;
          run(0.4, []); o.yastri_touch_intensity = f.ya.i; a0 = fwd(f.qa); run(0.5, []); const a2 = fwd(f.qa);
          o.yastri_touch_deg = ang(a0, a2); o.yastri_touch_left = new THREE.Vector3().crossVectors(a0, a2).dot(new THREE.Vector3(0, 1, 0).applyQuaternion(f.q)) > 0;
          o.yastri_touch_expect = 80 * Math.pow(1 + W.player.speed * 0.01, 0.25) * 0.5;
          E.input.touchL[2] = 0; run(0.6, []); o.yastri_touch_after = f.ya.i;
          // Soar and the element scaling (petals are levels: 10 petals = level 10)
          const st = window.__stakes().stakes; st.base.time = 5; st.base.mass = 0;
          const s0 = f.speed; run(2.5, ['ShiftLeft']); o.soar_from = s0; o.soar_to = f.speed; o.soar_amt = f.boostAmt; o.wake_vol_mass0 = W.arena.trail_vol;
          st.base.time = 10; st.base.mass = 10; run(0.1, ['ShiftLeft']); o.soar_amt_time10 = f.boostAmt; o.wake_vol_mass10 = W.arena.trail_vol;
          st.base.time = 0; run(0.1, ['ShiftLeft']); o.soar_amt_time0 = f.boostAmt;
          const a = W.audit(); o.audit_residual = a.residual; o.audit_start = a.start;
          return o;
        }"""
        fm = {}
        for q in ("?mode=thief&seed=7", "?mode=thief&seed=7&flight=arcade"):
            ctx, pg, errs = page(1024, 700, q)
            r = pg.evaluate(FM_JS); r["errors"] = errs
            fm["arcade" if "arcade" in q else "game"] = r
            if errs: fails.append(f"flight model {q}: page errors {errs[:3]}")
            ctx.close()
        g, ar = fm["game"], fm["arcade"]
        if not g["game"] or ar["game"]: fails.append("flight model: ?flight= did not pick the model")
        if g["slip_turn"] > 1: fails.append(f"game: plain turn slipped {g['slip_turn']:.1f} deg (course must follow the nose)")
        if not g["drifting"] or g["slip_drift"] < 20: fails.append(f"game: drift did not decouple (slip {g['slip_drift']:.1f} deg)")
        if g["drift_heading_deg"] < 30 or g["drift_velocity_deg"] > 1: fails.append(f"game: drift heading moved {g['drift_heading_deg']:.1f} deg, velocity {g['drift_velocity_deg']:.2f} deg (Dolphin grip 0 freezes the course)")
        if g["slip_after"] > 1 or g["drifting_after"]: fails.append(f"game: drift did not release (slip {g['slip_after']:.1f} deg)")
        if not (55 <= g["yastri_trigger_deg"] <= 65) or not g["yastri_trigger_right"]: fails.append(f"game: X trigger turn {g['yastri_trigger_deg']:.1f} deg/s, right={g['yastri_trigger_right']} (want 60, right)")
        if g["yastri_touch_intensity"] < 0.999 or not g["yastri_touch_left"] or abs(g["yastri_touch_deg"] - g["yastri_touch_expect"]) > 0.1 * g["yastri_touch_expect"] or g["yastri_touch_after"] > 0:
            fails.append(f"game: touch Yastri {g['yastri_touch_deg']:.1f} deg vs {g['yastri_touch_expect']:.1f}, intensity {g['yastri_touch_intensity']:.2f}, left={g['yastri_touch_left']}, after {g['yastri_touch_after']}")
        if abs(g["soar_amt"] - 4.6) > 1e-6 or abs(g["soar_amt_time10"] - 5.2) > 1e-6 or abs(g["soar_amt_time0"] - 4.0) > 1e-6 or g["soar_to"] < 3 * g["soar_from"]:
            fails.append(f"game: Soar x{g['soar_amt']} / x{g['soar_amt_time10']} / x{g['soar_amt_time0']} (want 4.6 / 5.2 / 4), {g['soar_from']:.0f} -> {g['soar_to']:.0f} u/s")
        if abs(g["wake_vol_mass0"] - 10) > 1e-9 or abs(g["wake_vol_mass10"] - 25) > 1e-9: fails.append(f"game: wake volume {g['wake_vol_mass0']} / {g['wake_vol_mass10']} (want 10 / 25)")
        if abs(g["audit_residual"]) > 1e-6 * max(1, g["audit_start"]): fails.append(f"game: mass audit residual {g['audit_residual']} with a scaled wake")
        if ar["slip_drift"] > 1 or ar["yastri_trigger_deg"] > 1 or ar["soar_amt"] != 1: fails.append("arcade: game-model abilities leaked into ?flight=arcade")
        out["flight_model"] = fm
        # a drift and a Soar for the eye: stepped as above, then the paused page renders it from the chase camera
        ctx, pg, errs = page(1440, 900, "?mode=cell&seed=7")
        pg.wait_for_function("window.__perf.frame.length > 6", timeout=60000)
        SHOT_JS = r"""(keys) => { const E = window.__eco, W = E.W, K = E.input.keys, T = 1 / 30; document.getElementById('start').style.display = 'none';
          for (const [secs, ks] of keys) { K.clear(); for (const k of ks) K.add(k); for (let i = 0; i < Math.round(secs / T); i++) { E.flyStep(T); W.step(T); E.snapshot(); } }
          return { slip: E.flight.slip }; }"""
        r = pg.evaluate(SHOT_JS, [[2.5, ["KeyW"]], [1.3, ["KeyW", "KeyA", "Space"]]])
        pg.wait_for_timeout(1500)
        out["flight_model"]["shot_drift_slip"] = r["slip"]
        out["flight_model"]["live_drift_hud"] = pg.evaluate("document.getElementById('fm').textContent")
        out["flight_model"]["fpm_shown"] = pg.evaluate("getComputedStyle(document.getElementById('fpm')).display !== 'none'")
        pg.screenshot(path=os.path.join(a.shots, "desktop_drift.png"))
        pg.evaluate(SHOT_JS, [[1.0, []], [2.0, ["KeyW", "ShiftLeft"]]])
        pg.wait_for_timeout(1500)
        out["flight_model"]["live_soar_hud"] = pg.evaluate("document.getElementById('fm').textContent")
        pg.screenshot(path=os.path.join(a.shots, "desktop_soar.png"))
        pg.evaluate("window.__eco.input.keys.clear()")
        if "DRIFT" not in out["flight_model"]["live_drift_hud"] or "SOAR" not in out["flight_model"]["live_soar_hud"]: fails.append(f"HUD did not show the drift / Soar: {out['flight_model']['live_drift_hud']!r} / {out['flight_model']['live_soar_hud']!r}")
        if not out["flight_model"]["fpm_shown"]: fails.append("the flight-path marker was not shown during a drift")
        if errs: fails.append(f"drift shots: page errors {errs[:3]}")
        ctx.close()

        # ---- every mode loads clean ------------------------------------------------------------------------------
        for mode in ("locust", "lurker", "stampede", "leviathan", "mobber", "grazer", "fortress", "snaptrap", "siege"):
            ctx, pg, errs = page(1024, 700, f"?mode={mode}&go=1&auto=1&seed=11")
            pg.wait_for_timeout(2500)
            n = pg.evaluate("window.__eco.W.species[0].n")
            out[mode] = dict(errors=errs, agents=n)
            if errs: fails.append(f"{mode}: page errors {errs[:3]}")
            if mode in ("lurker", "snaptrap", "fortress", "leviathan", "siege"):
                pg.screenshot(path=os.path.join(a.shots, f"desktop_{mode}.png"))
            ctx.close()

        # ---- scored populations: ?pop=scored builds the cell at the scored defaults ------------------------------
        ctx, pg, errs = page(1024, 700, "?mode=cell&pop=scored&go=1&auto=1&seed=11")
        pg.wait_for_timeout(1500)
        sc = pg.evaluate("""(() => { const g = window.__eco.W.species.find(s => s.key === 'grazer');
          let n = 0; for (let i = 0; i < g.alive.length; i++) n += g.alive[i] ? 1 : 0;
          return { cap: g.alive.length, alive: n, picker: document.getElementById('pop').value }; })()""")
        out["pop_scored"] = dict(errors=errs, **sc)
        if errs: fails.append(f"pop=scored: page errors {errs[:3]}")
        if sc["cap"] != 240 or sc["picker"] != "scored": fails.append(f"pop=scored: grazer cap {sc['cap']}, picker {sc['picker']} (want 240, scored)")
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
