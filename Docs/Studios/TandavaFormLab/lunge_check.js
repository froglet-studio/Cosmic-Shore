const FORM = +process.env.FORM || 3;
const fs = require('fs');
globalThis.PLAN_DATA = JSON.parse(fs.readFileSync(__dirname + '/plans.json', 'utf8'));
require('./sim.js');
const S = globalThis.TandavaSim;
const bank = S.buildBank(PLAN_DATA);
// an Antlion hunt: the forms jump to the last; a pilot loiters near it
for (const v of [0, 1, 2]) {
  const h = new S.Hunt(bank, { seed: 7 + v, ai: 0, picks: FORM === 1 ? [0, v, 0, 0] : [0, 0, 0, v] }); h.start();
  const d = h.director; d.formIx = FORM; h.swarm.seed(d.form.planIx, h.swarm.anchor.slice(), h.swarm.BX.slice());
  h.swarm.stomach[1] = 0.15 * S.STOMACH;
  let lunges = 0, snaps = 0, onGape = 0, onSnap = 0, ticks = 0; const gaps = [];
  for (let i = 0; i < 600 && d.outcome === 'Running'; i++) {
    if (i > 60 && i % 20 === 0) h.command({ kind: 'goto', at: S.vec.add(h.swarm.anchor, [0, 240, 0]) });
    h.tick();
    if (d.lunging && !d.snapping) { ticks++; if (h.swarm.planIx === d.form.lungePlanIx) onGape++; }
    if (d.snapping && h.swarm.planIx === d.form.snapPlanIx) onSnap++;
    for (const e of h.events) { if (e.kind === 'mood' && e.to === 'Lunging') lunges++; if (e.kind === 'snapped') { snaps++; gaps.push(e.gap.toFixed(0)); } }
    h.events.length = 0;
  }
  console.log(`form ${FORM} variant ${v + 1}: ${lunges} lunges, ${snaps} snaps (jaws ${gaps.join('/')} u off), wearing the gape ${onGape}/${ticks} lunge ticks, snapped ${onSnap} ticks; ${d.outcome}`);
}
