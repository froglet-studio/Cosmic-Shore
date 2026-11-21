// Dumps the Omni Combine Lab's own poses (window.__lab.pose / omniPose) at fixed times, seed 7, the
// designer's style, for Driver.cs to compare against OmniCombineCore. Usage:
//   node lab_poses.cjs <OmniCombineLab.html> <out.json>
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  const p = await b.newPage();
  await p.goto('file://' + require('path').resolve(process.argv[2]), { waitUntil: 'load' });
  await p.waitForTimeout(300);
  const out = await p.evaluate(() => {
    const L = window.__lab;
    L.manualClock = true;
    L.applyStyle(L.STYLES[0]);
    L.reset(7);
    const P = Object.assign({}, L.SHIPPED);
    const cam = [0.35, 0.42, 0.837];
    const n = Math.hypot(...cam), camDir = cam.map((x) => x / n);
    const times = [0, 1.3, 2.6, 3.9, 4.4, 5.2, 6.3, 7.1, 8.0, 8.8, 9.6, 10.2, 10.69, 10.7];
    const poses = times.map((t) => {
      const o = L.pose(t, P, camDir, {});
      const r = { t, crystals: {} };
      for (const k of ['Space', 'Charge', 'Mass', 'Time']) r.crystals[k] = Array.from(o[k].pos);
      return r;
    });
    const omni = [[10.8, 0.15], [11.3, 0.6], [12.4, 1], [13.9, 1], [15.2, 1]].map(([t, fx]) => ({ t, fx, pos: Array.from(L.omniPose(t, P, fx, camDir).pos) }));
    return { camDir, morphEnds: L.morphEnds(P), poses, omni, rigs: L.flipRigs() };
  });
  require('fs').writeFileSync(process.argv[3], JSON.stringify(out));
  await b.close();
})();
