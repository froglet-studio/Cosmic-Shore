// Holds src/combineCore.js to the SHIPPED converter core: run parity_core/run.sh (it dumps the C# poses first).
// --negative plants a defect (the socket spacing off by 1%) that must FAIL the gate.
const fs = require('fs'), path = require('path');
require(path.join(__dirname, 'src/combineCore.js'));
const { Core, fitMotion, Cadence } = globalThis.CR.Combine;
const ref = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const negative = process.argv.includes('--negative');
const data = JSON.parse(fs.readFileSync(path.join(__dirname, '../../../Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json'), 'utf8'));
const core = new Core(data);
const p = {
  Seed: 1234567, Lead: 2.7, Layout: 2, Spread: 3.05, LayoutYaw: 1.1, Flight: 4.9, Stagger: 4.9 * 1.01, Ease: 2, Arc: 0.41, Swirl: 0.07, Tumble: 0.27,
  MorphStart: 0.1, MorphLength: 0.8, SiteSpread: 0.52, Suck: 0.36, EffectFade: 0.7, EffectPhase: 41.3,
  SocketSpacing: negative ? 0.16 * 1.01 : 0.16, SocketSeconds: 0.9, MassRaise: 0.6, ApproachAboveCamera: 0,
  ShaderDepth: true, IdleTumble: false, GameMotion: true, ApproachTurn: null, ApproachElevation: null, ApproachCameraAzimuth: 0, CameraAzimuth: 0, CameraElevation: 0,
};
const az = 2.3, rate = 5 * Math.PI / 180;
core.faceCamera(p, az, rate, 0);
let worst = 0, worstAt = '';
const note = (e, at) => { if (e > worst) { worst = e; worstAt = at; } };
ref.approachTurn.forEach((v, k) => note(Math.abs(v - p.ApproachTurn[k]), 'approachTurn'));
for (const f of ref.frames) {
  p.CameraAzimuth = f.camAz; p.CameraElevation = f.camEl;
  const cam = [Math.sin(f.camAz) * Math.cos(f.camEl), Math.sin(f.camEl), Math.cos(f.camAz) * Math.cos(f.camEl)];
  for (let k = 0; k < 4; k++) {
    const pos = new Float32Array(core.vertexCount(k) * 3);
    const r = core.poseCrystal(k, f.t, p, cam, pos, null);
    for (let i = 0; i < pos.length; i++) note(Math.abs(pos[i] - f.pos[k][i]), 't=' + f.t + ' crystal ' + k);
    note(Math.abs(r.effect - f.effect[k]), 'effect');
  }
  note(Math.abs(Core.massColumnPlace(f.t, p) - f.massPlace), 'massPlace');
}
const motion = fitMotion(data, core.omniRadius, 1.1);
if (motion.problem) throw new Error(motion.problem);
const cad = new Cadence(1.733, 1.8, 2.4, 0.15);
const rest = new Float32Array(data.omni.verts.length), op = new Float32Array(data.omni.verts.length);
for (const o of ref.omni) {
  motion.swell(data.omni.verts, o.fx, rest);
  let turn = 0, progress = 0, waveSeconds = -1, wave = -1;
  if (o.beats >= 0) { const s = cad.squaresAt(o.beats); turn = s.turn; progress = s.progress; const w = cad.waveAt(o.beats); if (w) { wave = w.index; waveSeconds = data.flip.first + w.secondsIn; } }
  core.omniPose(30 + o.beats, p, o.fx, [0, 0.3, 0.95], op, rest, waveSeconds, wave);
  if (turn) motion.turnSquares(rest, op, turn === 1, Cadence.smoother(progress), 0.14 * core.omniRadius, 1.5, 1.5);
  for (let i = 0; i < op.length; i++) note(Math.abs(op[i] - o.pos[i]), 'omni beats=' + o.beats);
}
const R = core.omniRadius, tol = 1e-4 * R;
console.log(`worst ${worst.toExponential(3)} (${(worst / R).toExponential(2)} R) at ${worstAt}; tolerance ${tol.toExponential(2)}`);
const pass = worst <= tol;
console.log(negative ? (pass ? 'FAIL  negative control was NOT caught' : 'PASS  negative control caught') : (pass ? 'PASS  combineCore.js matches the shipped core' : 'FAIL  combineCore.js drifts from the shipped core'));
process.exit(negative ? (pass ? 1 : 0) : (pass ? 0 : 1));
