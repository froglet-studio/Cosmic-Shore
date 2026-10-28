// The parity fixture for ShivaDirectorCore (Tools/Build/swarm_core_harness mode `shiva`): the lab's circuits, from the
// lab's own code, for a set of seeds - the same replay ShivaDirectorCore.RingsForForm does (per form: theta0, then the
// form's rings). Run: node Docs/Studios/ShivaLab/parity_fixture.js > fixture.json
require('./sim.js');
const S = globalThis.ShivaSim, P = Object.assign({}, S.SHIPPED), out = { P, cases: [] };
for (const seed of [1, 2, 3, 7, 42, 1234567]) {
  const rnd = S.mulberry32(seed);
  for (let form = 0; form < 3; form++) {
    const theta0 = rnd() * 6.283, rings = S.layRings(P, form, theta0, rnd);
    const dance = form === 1 ? [0, 10, 37.5].map((t) => rings.map((g) => S.danceAt(P, g, t))) : [];
    out.cases.push({ seed, form, centres: rings.map((g) => g.c), normals: rings.map((g) => g.n), dance });
  }
}
process.stdout.write(JSON.stringify(out));
