"""Close-range portraits of the NCA lizard in the flyable cell (index.html), for judging its look and motion.

    python Tools/Ecology/flight/nca_quality_shots.py --three PATH --tag before|after [--mode creatures]

Takes over the camera through window.__camHook (orbit portraits around the lizard, a low side view, a chase view from
a pilot parked behind it), cuts it once (debris + wound glow), and records a strip of 6 consecutive close frames
~50 ms apart (popping shows as voxels blinking between the strip's panels). Also measures the lizard's path (speed,
turn, how far it wanders) and, after a ram, whether it turned away. Writes shots/nca_quality_<tag>_*.png and
results/nca_quality_<tag>.json.
"""
import argparse, json, os, sys
from playwright.sync_api import sync_playwright
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
CHROME = "/opt/pw-browsers/chromium-1194/chrome-linux/chrome"
ap = argparse.ArgumentParser(); ap.add_argument("--three", required=True); ap.add_argument("--tag", required=True)
ap.add_argument("--mode", default="creatures"); ap.add_argument("--query", default=""); ap.add_argument("--which", type=int, default=0); ap.add_argument("--shots", default=os.path.join(HERE, "shots"))
a = ap.parse_args(); os.makedirs(a.shots, exist_ok=True)
three = open(a.three, "rb").read(); url = "file://" + os.path.join(HERE, "index.html")
shot = lambda name: os.path.join(a.shots, f"nca_quality_{a.tag}_{name}.png")

# camera rig: 'orbit' circles the lizard (az, el, dist); 'chase' parks the pilot behind it and lets the page's chase
# cam follow; the pilot is otherwise kept well away so it never cuts the lizard by accident.
RIG = """() => {
  const B = window.__bigObj, E = window.__eco, T = window.THREE;
  window.__rig = { which: WHICH, mode: 'orbit', az: 0.6, el: 0.45, dist: 210, spin: 0, head: [1, 0, 0], prev: null, path: [] };
  window.__camHook = (cam, dt) => {
    const r = window.__rig, L = (B.lizards && B.lizards[r.which]) || B.lizard; if (!L) return;
    const P = L.position, pl = E.W.player;
    if (r.prev && dt > 0) { const v = [P[0] - r.prev[0], P[1] - r.prev[1], P[2] - r.prev[2]], n = Math.hypot(...v);
      if (n > 1e-6) { const k = 1 - Math.exp(-dt * 3); for (let i = 0; i < 3; i++) r.head[i] += (v[i] / n - r.head[i]) * k; } }
    r.prev = P.slice(); r.tt = (r.tt || 0) + dt; r.path.push([r.tt, P[0], P[1], P[2]]); if (r.path.length > 4000) r.path.shift();
    const h = r.head, hn = Math.hypot(h[0], h[2]) || 1, hx = h[0] / hn, hz = h[2] / hn;
    E.flight.speed = 0; E.flight.throttle = 0; E.flight.auto = false;
    if (r.mode === 'chase') {
      pl.pos[0] = P[0] - hx * 150; pl.pos[1] = P[1] + 30; pl.pos[2] = P[2] - hz * 150;
      E.flight.q.setFromUnitVectors(new T.Vector3(0, 0, 1), new T.Vector3(hx, -0.15, hz).normalize());
      return;
    }
    if (r.mode === 'free' || r.mode === 'fixed') return;
    pl.pos[0] = P[0]; pl.pos[1] = P[1] + 700; pl.pos[2] = P[2];        // out of the way
    r.az += r.spin * dt;
    const az = r.az + Math.atan2(hz, hx);                                  // az relative to the lizard's heading
    cam.position.set(P[0] + r.dist * Math.cos(r.el) * Math.cos(az), P[1] + r.dist * Math.sin(r.el), P[2] + r.dist * Math.cos(r.el) * Math.sin(az));
    cam.up.set(0, 1, 0); cam.lookAt(P[0], P[1], P[2]); cam.fov = 50; cam.updateProjectionMatrix();
  };
  window.__view.ship.visible = false;
}"""
out = {"tag": a.tag}
with sync_playwright() as pw:
    br = pw.chromium.launch(executable_path=CHROME, args=["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"])
    ctx = br.new_context(viewport=dict(width=1280, height=800), device_scale_factor=1)
    ctx.route("**/cdn.jsdelivr.net/**", lambda r: r.fulfill(status=200, body=three, content_type="application/javascript"))
    pg = ctx.new_page(); errs = []
    pg.on("pageerror", lambda e: errs.append("pageerror: " + str(e)))
    pg.on("console", lambda m: errs.append("console: " + m.text) if m.type == "error" else None)
    pg.goto(url + f"?mode={a.mode}&go=1&seed=7" + a.query); pg.wait_for_function("window.__eco && window.__eco.W && window.__perf.frame.length > 12", timeout=90000)
    pg.evaluate(RIG.replace('WHICH', str(a.which)))
    rig = lambda **kw: pg.evaluate("(kw) => Object.assign(window.__rig, kw)", kw)
    pg.wait_for_timeout(3000)
    rig(mode="orbit", az=1.2, el=0.55, dist=200); pg.wait_for_timeout(700); pg.screenshot(path=shot("orbit_top"))
    rig(az=2.0, el=0.12, dist=170); pg.wait_for_timeout(700); pg.screenshot(path=shot("orbit_side"))
    rig(az=-0.4, el=0.3, dist=150); pg.wait_for_timeout(700); pg.screenshot(path=shot("orbit_front"))
    # strip: 6 frames ~50 ms apart, centre crop
    rig(az=1.6, el=0.5, dist=150); pg.wait_for_timeout(600)
    panels = []
    for k in range(6):
        p = os.path.join(a.shots, f"_strip{k}.png"); pg.screenshot(path=p, clip=dict(x=340, y=150, width=600, height=500)); panels.append(p); pg.wait_for_timeout(50)
    ims = [Image.open(p) for p in panels]; W = Image.new("RGB", (3 * 600, 2 * 500))
    for k, im in enumerate(ims): W.paste(im, ((k % 3) * 600, (k // 3) * 500))
    W.save(shot("strip")); [os.remove(p) for p in panels]
    rig(mode="chase"); pg.wait_for_timeout(2500); pg.screenshot(path=shot("chase"))
    # path over 12 s of free swimming (pilot far away)
    rig(mode="orbit", az=0.6, el=0.5, dist=220, path=[]); pg.wait_for_timeout(12000)
    path = pg.evaluate("window.__rig.path")
    import math
    sp, turn = [], []
    for i in range(2, len(path)):
        t0, t1, t2 = path[i - 2][0], path[i - 1][0], path[i][0]
        if t2 - t1 <= 0 or t1 - t0 <= 0: continue
        v1 = [(path[i - 1][j] - path[i - 2][j]) / (t1 - t0) for j in (1, 2, 3)]; v2 = [(path[i][j] - path[i - 1][j]) / (t2 - t1) for j in (1, 2, 3)]
        sp.append(math.hypot(*v2))
        a1, a2 = math.atan2(v1[2], v1[0]), math.atan2(v2[2], v2[0]); d = (a2 - a1 + math.pi) % (2 * math.pi) - math.pi; turn.append(d / (t2 - t1))
    xs = [p[1] for p in path]; zs = [p[3] for p in path]; ys = [p[2] for p in path]
    sp.sort(); out["path"] = {"samples": len(sp), "speed_med": round(sp[len(sp) // 2], 1) if sp else None, "speed_p10": round(sp[len(sp) // 10], 1) if sp else None,
        "speed_p90": round(sp[9 * len(sp) // 10], 1) if sp else None, "turn_rate_absmean": round(sum(abs(x) for x in turn) / max(1, len(turn)), 3),
        "turn_rate_sign_changes": sum(1 for i in range(1, len(turn)) if turn[i] * turn[i - 1] < 0), "extent_xyz": [round(max(xs) - min(xs)), round(max(ys) - min(ys)), round(max(zs) - min(zs))]}
    # ram: put the pilot into the lizard's flank, moving through it; then watch what it does
    n0 = pg.evaluate("window.__bigObj.lizard.count()")
    rig(mode="free")
    pg.evaluate("""() => { const L = window.__bigObj.lizard, v = L.voxels(), pl = window.__eco.W.player, j = 3 * (v.n >> 1), r = window.__rig;
      pl.pos[0] = v.pos[j]; pl.pos[1] = v.pos[j + 1]; pl.pos[2] = v.pos[j + 2];
      r.ramFrom = [pl.pos[0], pl.pos[1], pl.pos[2]]; r.ramHead = r.head.slice(); r.ramPos = L.position.slice();
      const cam = window.__view.cam; r.mode = 'fixed';
      const P = L.position, h = r.head; r.fixed = [P[0] - 120 * h[2] + 40 * h[0], P[1] + 90, P[2] + 120 * h[0] + 40 * h[2]]; }""")
    pg.evaluate("""() => { const r = window.__rig, prev = window.__camHook; window.__camHook = (cam, dt) => { prev(cam, dt);
      if (r.mode === 'fixed') { const P = window.__bigObj.lizard.position; cam.position.set(...r.fixed); cam.up.set(0, 1, 0); cam.lookAt(P[0], P[1], P[2]); cam.fov = 50; cam.updateProjectionMatrix(); } }; }""")
    pg.wait_for_timeout(450); out["cut"] = pg.evaluate("window.__big()"); pg.screenshot(path=shot("cut"))
    pg.evaluate("() => { window.__eco.W.player.pos[1] += 800; }")
    pg.wait_for_timeout(900); pg.screenshot(path=shot("cut_1s"))
    pg.wait_for_timeout(1500)
    rr = pg.evaluate("""() => { const r = window.__rig, L = window.__bigObj.lizard, P = L.position, d0 = Math.hypot(r.ramFrom[0] - r.ramPos[0], r.ramFrom[2] - r.ramPos[2]),
        d1 = Math.hypot(r.ramFrom[0] - P[0], r.ramFrom[2] - P[2]), h0 = r.ramHead, h1 = r.head;
      return { dist_from_ram_point_before: d0, after_2p4s: d1, heading_change_deg: Math.acos(Math.max(-1, Math.min(1, h0[0] * h1[0] + h0[2] * h1[2]))) * 180 / Math.PI }; }""")
    out["ram"] = {k: round(v, 1) for k, v in rr.items()}
    pg.screenshot(path=shot("cut_2s"))
    pg.wait_for_timeout(6000); out["regrow"] = pg.evaluate("window.__big()"); out["lizard_before_cut"] = n0
    rig(mode="orbit", az=1.2, el=0.55, dist=200); pg.wait_for_timeout(800); pg.screenshot(path=shot("regrown"))
    # whole-cell context
    pg.evaluate("() => { window.__camHook = null; window.__view.ship.visible = true; }")
    out["perf"] = pg.evaluate("(() => { const p = window.__perf, m = a => a.slice().sort((x, y) => x - y)[a.length >> 1]; return { frame_ms: m(p.frame), cpu_ms: m(p.cpu), sim_ms: m(p.sim), render_ms: m(p.render) }; })()")
    out["lizards"] = pg.evaluate("(() => { const B = window.__bigObj; return (B.lizards || [B.lizard]).map(L => ({ element: L.element, backend: L.backend, count: L.count(), steps: L.steps, rate: L.rate || L.stepsPerSecond })); })()")
    out["errors"] = errs[:6]; br.close()
json.dump(out, open(os.path.join(HERE, "results", f"nca_quality_{a.tag}.json"), "w"), indent=1)
print(json.dumps({k: out[k] for k in ("path", "ram", "perf", "lizards", "errors")})[:1500])
print("cut", out["cut"]["lizCut"], "regrow", out["regrow"]["lizard"], "of", out["lizard_before_cut"])
