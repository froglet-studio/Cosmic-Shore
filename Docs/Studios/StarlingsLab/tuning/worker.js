// node worker.js jobs.json out.jsonl  — jobs: { tune, jobs: [{ seed, intensity, mode }] }
const { chromium } = require('playwright'); const fs = require('fs');
(async () => {
  const spec = JSON.parse(fs.readFileSync(process.argv[2], 'utf8')), out = process.argv[3];
  const b = await chromium.launch(); const pg = await b.newPage(); const errs = []; pg.on('pageerror', e => errs.push(String(e)));
  await pg.goto('file://' + require('path').resolve(__dirname, '../../StarlingsLab.html')); await pg.waitForTimeout(300);
  await pg.evaluate((t) => { Object.assign(window.__lab.TUNE, t || {}); }, spec.tune);
  for (const j of spec.jobs) {
    const r = await pg.evaluate((j) => { const L = window.__lab;
      if (j.mode === 'look') return L.lookAhead({ seed: j.seed, intensity: j.intensity });
      const o = L.raceOne({ seed: j.seed, intensity: j.intensity }); return { picks: o.hands.map(() => null), time: o.time, held: o.held, course: o.course, hz: o.hz }; }, j);
    fs.appendFileSync(out, JSON.stringify(Object.assign({}, j, r)) + '\n');
  }
  if (errs.length) fs.appendFileSync(out, JSON.stringify({ errors: errs }) + '\n');
  await b.close();
})();
