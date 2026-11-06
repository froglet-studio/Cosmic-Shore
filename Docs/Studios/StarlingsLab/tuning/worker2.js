// node worker2.js jobs.json out.jsonl — jobs: { tune, jobs: [{ id, look?: bool, opts }] }
const { chromium } = require('playwright'); const fs = require('fs');
(async () => {
  const spec = JSON.parse(fs.readFileSync(process.argv[2], 'utf8')), out = process.argv[3];
  const b = await chromium.launch(); const pg = await b.newPage(); const errs = []; pg.on('pageerror', e => errs.push(String(e)));
  await pg.goto('file://' + require('path').resolve(__dirname, '../../StarlingsLab.html')); await pg.waitForTimeout(300);
  await pg.evaluate((t) => { Object.assign(window.__lab.TUNE, t || {}); }, spec.tune);
  for (const j of spec.jobs) {
    const r = await pg.evaluate((j) => { const L = window.__lab; if (j.look) return L.lookAhead(j.opts); const o = L.raceOne(j.opts); return { time: o.time, lost: o.lost, held: o.held, course: o.course, hz: o.hz, hands: o.hands }; }, j);
    fs.appendFileSync(out, JSON.stringify(Object.assign({ id: j.id, opts: j.opts, look: !!j.look }, r)) + '\n');
  }
  if (errs.length) fs.appendFileSync(out, JSON.stringify({ errors: errs }) + '\n');
  await b.close();
})();
