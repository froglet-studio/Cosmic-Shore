// Headless scorecard for Shiva's Cell: all-autopilot squads, seeded. Run: node Docs/Studios/ShivaLab/check.js [seeds]
// (build.py writes plans.json first.)
const fs = require('fs');
globalThis.PLAN_DATA = JSON.parse(fs.readFileSync(__dirname + '/plans.json', 'utf8'));
require('./sim.js');
const S = globalThis.ShivaSim, bank = S.buildBank(PLAN_DATA);
const seeds = (process.argv[2] || '1,2,3').split(',').map(Number);
const SQUADS = { Mixed: ['Squirrel', 'Sparrow', 'Rhino'], Fast: ['Manta', 'Dolphin', 'Squirrel'], Slow: ['Sparrow', 'Serpent', 'Urchin'] };
const VARIANTS = [['Shipped', {}, {}], ['No swarm', {}, { noSwarm: true }], ['Sloppy AI', { aiNoise: 0.6 }, {}]];
const cols = ['outcome', 'p1', 'p2', 'p3', 'left1', 'left2', 'left3', 'stings', 'bumps', 'rams', 'nips', 'latches', 'stripped', 'swallowed', 'cuts', 'serpentsCut'];
console.log(['squad', 'variant', 'seed'].concat(cols).join('\t'));
for (const [sq, hulls] of Object.entries(SQUADS)) for (const [vn, pv, ov] of VARIANTS) for (const seed of seeds) {
  const P = Object.assign({}, S.SHIPPED, pv), t0 = Date.now();
  const r = S.run(bank, P, Object.assign({ seed, hulls }, ov), 900);
  console.log([sq, vn, seed].concat(cols.map((c) => r[c])).join('\t') + '\t' + (Date.now() - t0) + 'ms');
}
