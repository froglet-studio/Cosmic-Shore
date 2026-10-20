// ============================================================================================
// shepherd.js — rounds 2-3: the herding vessel (proposed name MARLIN AND JACKS: the marlin is the pilot, the
// two jacks are the sheepdogs, the flock is the swarm). One pilot, two dogs (one phone player, one thumb per
// dog) and a flock. Garrett's brief, 2026-10-20:
//   - dogs are repulsive and live on the pilot's plane; their minimum repulsion radius is larger than a big
//     thumb's occlusion, so the ring always shows round the thumb that holds the dog;
//   - the pilot is repulsive too, and can briefly CALL the flock toward itself on a long cooldown;
//   - the flock flies in 3D like a swarm but is strongly drawn to the pilot's plane;
//   - lifting a dog's thumb leaves a repulsive DECOY (one at a time; a new one replaces the old);
//   - the pilot teleports to the decoy with a trigger or by lifting its thumbs;
//   - Mass = breeding rate, Time = flock speed, Space = repulsion radius of pilot, dogs and decoy,
//     Charge = share of danger sheep (0% at -5, 100% at 15);
//   - L5: Mass poops a cluster of mass, Space eats shielded prisms, Charge attacks players;
//   - the pilot has the Rhino's full speed and its fly-straight ramp.
// Round 3 (Garrett, after playing round 2, 2026-10-20):
//   1. the camera is the Wrasse's top-down view (orthographic, straight down the board's up axis, the board's
//      forward up the screen), zoomed out further; no isometric tilt;
//   2. Time L5 is a PILOT ability on the dogs' decoy: the pilot drops the one decoy where it is;
//   3. the pilot is a two-thumb flyer like the Grouper, and its ROLL sets the plane: the plane is the hull's
//      own x-z plane, eased like the Wrasse's board (KiteBoardFrame.Eased), so dogs and flock live on it;
//   4. a danger sheep the pilot hits dies, and the pilot takes the existing own-domain danger hit: the full
//      stop slow and boost reset plus the 4 s decaying element debuff, never the permanent burn.
// Round 4 (Garrett, after playing round 3 on a phone and a gamepad, 2026-10-20):
//   1. on a gamepad the two thumbsticks steer the two jacks (left stick the left jack, right stick the right);
//      a bumper or trigger is the thumb's lift, so it leaves the decoy;
//   2. the marlin no longer swings the jacks' camera: the jacks' view (Steady) never turns with the marlin's
//      heading; it tilts only as far as the plane tilts (the smallest turn that keeps it looking straight down
//      the plane) and pans only when the flock leaves a slack box in the middle of the screen;
//   3. the herd is the cell's real FAUNA, not a swarm of vessel agents (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md,
//      locked): a creature eats by the game's mouthful (LightFaunaDataSO: face the meal, hold 2 s, eat it plus its
//      neighbours, at most 8), a feed fills its stomach and moves it toward a birth, an empty stomach withers it
//      (no imposed death, starvationSeconds 30), births bloom in and deaths wither out, and shielded mass is never
//      food: at Space L5 a bite sheds a shield (what Prism.Consume does) so the next mouthful can eat it.
//      In the code a creature is still `s` in `w.sheep`.
// Same contract as the rest of the lab: shepStep never draws and never reads a device; the AI pilot and
// the AI dogs write the SAME input object two thumbs, the keys or a gamepad do; every run is seeded.
// ============================================================================================

// ================= (1) the shipped numbers this mode reads, and its own knobs =================
const SHEP_SHIPPED = {
  rhinoCruise: 50,      // Rhino.prefab VesselTransformer.DefaultThrottleScaler (full throttle, no boost)
  rhinoMaxBoost: 16.8,  // RhinoRampBoostAction.asset maxBoostMultiplier (50 x 16.8 = 840 u/s flying straight)
  rhinoAccel: 220,      // RhinoRampBoostAction.asset accelerationPerSecond (Time 0 multiplier x1)
  rhinoBleed: 300,      // RhinoRampBoostAction.asset bleedPerSecond (steering inside the ramp)
  rhinoReturn: 120,     // RhinoRampBoostAction.asset returnPerSecond (the coast after the ramp lets go)
  rhinoPlateau: 0.3,    // StraightLineGesture.EngageThreshold (full boost below this stick deviation)
  rhinoGrace: 1,        // RhinoRampBoostAction.asset straightnessGraceBand (boost reaches 0 here)
  minSpeed: 10,         // Wrasse.prefab (the Grouper) VesselTransformer.MinimumSpeed
  pilotTurn: 100,       // Wrasse.prefab (the Grouper) PitchScaler / YawScaler (deg/s at full stick)
  pilotRoll: 120,       // Wrasse.prefab (the Grouper) RollScaler: a real roll axis on two sticks
  pilotRts: 0.1,        // Wrasse.prefab (the Grouper) RotationThrottleScaler (deg/s added per u/s)
  frame: 0,             // KiteNavigatorConfig.asset frame: 0 Eased (signed off), 1 Locked, 2 Level
  easeRate: 3,          // KiteNavigatorConfig.asset easeRate (the board eases to the hull's x-z plane)
  dangerDebuff: 2,      // lab reading: levels every element loses at the hit, decaying to 0 (VesselElementalDebuffByDangerPrismEffect)
  debuffTime: 4,        // VesselElementalDebuffByDangerPrismEffect.asset debuffDuration
  dangerSlow: 3,        // VesselChangeSpeedByPrism speedModifierDuration 1 x dangerSlowDurationMultiplier 3 (full stop at the hit)
  elTime: 2,            // lab: elements run -5..15 here, Garrett's Charge scale (0% at -5, 100% at 15)
  elSpace: 2,
  elMass: 2,
  elCharge: 2,
  thumbMm: 22,          // lab: a BIG thumb's occlusion, in mm (the disc of screen a thumb hides)
  thumbMargin: 1.25,    // lab: the smallest ring is this many thumb radii
  viewSpan: 1800,       // lab: world units across the screen's SHORT side (round 2: 1400; Garrett: "zoom out a bit more")
  camSlack: 0.45,       // lab: Steady view, share of the half-screen the flock may wander before the view pans (round 4)
  padDogSpeed: 1,       // lab: a full stick runs a jack at this share of dogSpeed (round 4)
  sheep: 60,            // lab: flock size at the start
  sheepCap: 180,        // lab: most sheep alive at once
  sheepSpeed: 70,       // lab: sheep cruise (u/s) at Time 0; panic adds up to +80%
  planePull: 1.6,       // lab: how hard sheep are drawn to the pilot's plane (1/s^2)
  dogSpeed: 520,        // lab: how fast a dog runs to its thumb (u/s)
  callTime: 2.5,        // lab: seconds the pilot's call lasts
  callCooldown: 20,     // lab: seconds before the call is ready again
  decoyCooldown: 4,     // lab: Time L5, seconds between the pilot's own decoys
  breedFood: 4,         // lab: feeds (mouthfuls) a creature needs for a birth at Mass -5 (fewer as Mass rises); the game's FeedsPerOffspring
  birthCooldown: 3,     // lab: seconds between one creature's births (the game's reproduction cooldown)
  consumeHold: 2,       // LightFaunaDataSO.consumeHoldSeconds: a mouthful is a 2 s hold facing the meal
  maxBites: 8,          // LightFaunaDataSO.maxClusterBites: prisms per mouthful at most
  biteRadius: 40,       // lab: LightFaunaDataSO.feedingClusterRadius (12) at the lab's scale, where a creature is ~3x the game's
  starvation: 30,       // Fauna.starvationSeconds: one full meal buys this many seconds; an empty stomach withers the creature
  stomachMeals: 2,      // lab: meals a creature can bank (the game's stomach Capacity is authored per species; 1 nominal meal = shipped)
  pooEvery: 6,          // lab: Mass L5, seconds between clusters
  attackRange: 200,     // lab: Charge L5, how close a rival must be for the flock to go for it
  intensity: 2,         // lab: I1..I4 for every game (rivals, shields, pen size)
  aiNoise: 0.15,        // lab: AI pilot and AI dogs sloppiness (0 expert, 1 rookie)
  round: 45,            // lab: seconds per round
};
const FRAME_NAMES = ['Eased (Wrasse)', 'Locked to the hull', 'Level (round 2)'];
const SHEP_SPEC = [
  ['Elements', 'elTime', -5, 15, 1, 'Time', 'Flock speed: +6% per level (floor 40%). L5: the pilot drops the decoy where it is (F, X button or the DECOY pad), then can teleport back to it.'],
  ['Elements', 'elSpace', -5, 15, 1, 'Space', 'Repulsion radius of pilot, both dogs and the decoy: the thumb floor at -5, +6% of it per level. L5: a bite sheds a shield, so the next mouthful eats the prism (shielded mass is never food).'],
  ['Elements', 'elMass', -5, 15, 1, 'Mass', 'Breeding: feeds per birth fall from 4 at -5 to about 1.3 at 15. L5: the herd poops a cluster of mass every few seconds.'],
  ['Elements', 'elCharge', -5, 15, 1, 'Charge', 'Share of danger creatures: 0% at -5, 35% at 2, 75% at 10, 100% at 15. L5: creatures near a rival go for it.'],
  ['Plane', 'frame', 0, 2, 1, 'Plane frame', 'How the plane follows the pilot. 0 Eased: the hull\'s x-z plane, eased (the Wrasse\'s signed-off board). 1 Locked: the hull\'s plane every frame. 2 Level: flat, turning with heading only (round 2).'],
  ['Plane', 'easeRate', 0.5, 10, 0.5, 'Ease rate', 'Shipped Wrasse board ease (1/s). Higher follows the roll sooner.'],
  ['Plane', 'planePull', 0.2, 4, 0.1, 'Pull to the plane', 'How strongly the creatures are drawn to the pilot\'s plane. Roll or climb and the herd follows.'],
  ['Thumb', 'thumbMm', 0, 30, 1, 'Big thumb (mm)', 'The disc of screen a big thumb hides. 0 turns the floor off (what a mouse would allow).'],
  ['Thumb', 'thumbMargin', 1, 2, 0.05, 'Ring / thumb', 'The smallest repulsion ring, in thumb radii. Above 1 the ring always peeks out round the thumb.'],
  ['Thumb', 'viewSpan', 800, 3000, 50, 'View span', 'World units across the screen\'s short side (the zoom). The ring floor is fixed on screen, so it grows in the world as you zoom out.'],
  ['Thumb', 'camSlack', 0, 0.9, 0.05, 'View slack', 'Steady view only: how far from the middle (share of the half-screen) the flock may drift before the view pans. 0 follows it every frame.'],
  ['Thumb', 'padDogSpeed', 0.2, 1.5, 0.05, 'Stick jack speed', 'Gamepad: a full stick runs its jack at this share of the dog speed.'],
  ['Flock', 'sheep', 10, 150, 1, 'Starting herd', 'Creatures at the start of a round.'],
  ['Flock', 'sheepCap', 40, 300, 5, 'Herd cap', 'Most creatures alive at once.'],
  ['Flock', 'sheepSpeed', 30, 160, 1, 'Creature speed', 'Cruise at Time 0. Fleeing adds up to +80%.'],
  ['Flock', 'breedFood', 1, 10, 0.5, 'Feeds per birth', 'Mouthfuls per birth at Mass -5 (the game\'s FeedsPerOffspring). Mass divides it by 1 + 0.1 per level above -5.'],
  ['Flock', 'birthCooldown', 0, 10, 0.5, 'Birth cooldown', 'Seconds between one creature\'s births.'],
  ['Flock', 'consumeHold', 0.2, 4, 0.1, 'Mouthful hold', 'Shipped 2 s: a creature brakes and holds this long on its meal. A herder that scares it breaks the hold.'],
  ['Flock', 'maxBites', 1, 16, 1, 'Bites per mouthful', 'Shipped 8: the meal plus its neighbours, at most this many prisms.'],
  ['Flock', 'starvation', 0, 120, 5, 'Starvation (s)', 'Shipped 30: seconds one meal lasts. Empty = the creature withers. 0 = never starves.'],
  ['Flock', 'stomachMeals', 1, 4, 0.5, 'Stomach (meals)', 'Meals a creature banks (the game\'s per-species stomach Capacity). 1 = the shipped nominal stomach: an unfed herd withers 30 s in. Creatures start half to fully fed.'],
  ['Crew', 'dogSpeed', 150, 1200, 10, 'Dog speed', 'How fast a dog runs to its thumb. Fast reads as "under my thumb".'],
  ['Crew', 'callTime', 0.5, 6, 0.1, 'Call length (s)', 'How long the pilot pulls the herd in.'],
  ['Crew', 'callCooldown', 3, 60, 1, 'Call cooldown (s)', 'The long cooldown on the call.'],
  ['Crew', 'decoyCooldown', 0, 20, 0.5, 'Pilot decoy cooldown (s)', 'Time L5: seconds between the pilot\'s own decoys.'],
  ['Crew', 'pooEvery', 2, 20, 0.5, 'Poop every (s)', 'Mass L5: seconds between clusters of mass.'],
  ['Crew', 'attackRange', 60, 500, 10, 'Attack range', 'Charge L5: how close a rival must be before the herd goes for it.'],
  ['Hull', 'rhinoCruise', 20, 120, 1, 'Cruise (full throttle)', 'Shipped Rhino throttle scaler. The two sticks\' spread is the throttle: neutral is half.'],
  ['Hull', 'rhinoMaxBoost', 1, 24, 0.1, 'Straight boost', 'Shipped Rhino ramp ceiling (x cruise). Straight means yaw, pitch and roll near zero; the spread does not count.'],
  ['Hull', 'rhinoAccel', 20, 600, 5, 'Ramp acceleration', 'Shipped ramp wind-up (u/s per s).'],
  ['Hull', 'pilotTurn', 30, 200, 5, 'Pitch / yaw', 'Shipped Grouper pitch and yaw scalers (deg/s).'],
  ['Hull', 'pilotRoll', 30, 240, 5, 'Roll', 'Shipped Grouper roll scaler (deg/s). Roll is what tilts the plane.'],
  ['Danger hit', 'dangerDebuff', 0, 5, 0.5, 'Element debuff', 'Levels every element loses when the pilot hits its own danger creatures, decaying over the debuff time. Own domain: never a permanent burn.'],
  ['Danger hit', 'debuffTime', 1, 10, 0.5, 'Debuff time (s)', 'Shipped debuffDuration.'],
  ['Danger hit', 'dangerSlow', 0, 6, 0.5, 'Slow (s)', 'Full stop at the hit, back to full speed over this long (shipped 1 s x 3). The ramp resets.'],
  ['Arena', 'intensity', 1, 4, 1, 'Intensity', 'I1-I4: Charge rivals 1-4, Space shields 25-55%, Time pens shrink, rivals faster.'],
  ['Arena', 'aiNoise', 0, 1, 0.05, 'AI noise', '0 = expert AI pilot and dogs, 1 = rookies. The scorecard runs both.'],
  ['Arena', 'round', 20, 120, 5, 'Round (s)', 'Seconds per round.'],
];
const SHEP_LS = 'lab:' + LAB.name + ':shepP3';
const SP = Object.assign({}, SHEP_SHIPPED, lsGet(SHEP_LS, {}));
for (const k of Object.keys(SP)) if (!(k in SHEP_SHIPPED)) delete SP[k];
const SHEP_NAME = 'Marlin and Jacks', PILOT_NAME = 'marlin', DOG_NAME = 'jacks';

const SHEP_GAMES = [
  { id: 'time', name: 'Time: pen race', el: 'time', how: 'Five pens in a loop, some above and below the start. A pen counts when 60% of the herd is inside it at once, so the pilot rolls the plane through each pen. Score: pens x 10, plus seconds to spare.' },
  { id: 'space', name: 'Space: graze the fort', el: 'space', how: 'A ruby fort, part shielded, floating above the start. The creatures graze the plain prisms; at Space L5 a bite sheds a shield, then the next mouthful eats it. Score: enemy prisms eaten.' },
  { id: 'mass', name: 'Mass: grow the herd', el: 'mass', how: 'Pasture in patches at many heights. Every mouthful brings a birth closer, and a creature that goes 30 s without one withers; Mass L5 poops clusters of your own mass. Score: births plus your mass standing.' },
  { id: 'charge', name: 'Charge: wolves', el: 'charge', how: 'Rival ships hunt the pilot and eat plain creatures. A danger creature they touch costs them a petal and slows them. Score: petals taken - lost + 3 per knockout.' },
];
const SHEP_ROLES = [
  { id: 'dogs', name: 'You: the dogs', how: 'One thumb per dog (or drag with the mouse). Lift a thumb to leave a decoy. Gamepad: left stick the left jack, right stick the right; a bumper or trigger on that side leaves the decoy. The AI flies the pilot and rolls the plane.' },
  { id: 'pilot', name: 'You: the pilot', how: 'Two thumbs like the Grouper: left and right halves of the screen, or a gamepad\'s two sticks. Sum = yaw and pitch, difference = roll (the plane), spread = throttle. Keys: A/D yaw, W/S pitch, Q/E roll, Shift throttle. Call: C, A button or CALL. Teleport: Space, right trigger, or lift both thumbs. Time L5 decoy: F, X button or DECOY. The AI runs the dogs.' },
  { id: 'both', name: 'You: both (hot seat)', how: 'Pilot on keyboard or gamepad, dogs on touch or mouse.' },
  { id: 'ai', name: 'Watch the AI', how: 'Both crews are AI. Use it to see the scheme work before you try it.' },
];

// ================= (2) vectors, element rules =================
const v3 = (x, y, z) => ({ x, y, z });
const vadd = (a, b) => v3(a.x + b.x, a.y + b.y, a.z + b.z);
const vsub = (a, b) => v3(a.x - b.x, a.y - b.y, a.z - b.z);
const vmul = (a, k) => v3(a.x * k, a.y * k, a.z * k);
const vdot = (a, b) => a.x * b.x + a.y * b.y + a.z * b.z;
const vcross = (a, b) => v3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
const vlen = (a) => Math.hypot(a.x, a.y, a.z);
const vnorm = (a) => { const L = vlen(a) || 1; return v3(a.x / L, a.y / L, a.z / L); };
const vrot = (v, k, a) => { const c = Math.cos(a), s = Math.sin(a), kv = vcross(k, v), kd = vdot(k, v) * (1 - c); return v3(v.x * c + kv.x * s + k.x * kd, v.y * c + kv.y * s + k.y * kd, v.z * c + kv.z * s + k.z * kd); };
const UPW = v3(0, 1, 0);
// orthonormal frame from a forward and an up hint: r = u x f (screen right in the top-down view), u = f x r
function frameFrom(f, u) { f = vnorm(f); let r = vcross(u, f); if (vlen(r) < 1e-4) r = vcross(v3(0, 0, 1), f); r = vnorm(r); return { f, r, u: vcross(f, r) }; }

const L5k = (k) => elv(k) >= 5;
// the element level the vessel has NOW: authored level plus the decaying danger debuff (one vessel, one set of elements)
function elv(k) { return SP[k] + (SW ? SW.deb : 0); }
const SEL = {
  speed: () => Math.max(0.4, 1 + 0.06 * elv('elTime')),
  floorU: () => (SP.thumbMm / 2) * 6.3 * SP.thumbMargin * SP.viewSpan / 390,   // 6.3 CSS px per mm on a phone; 390 px = landscape phone height
  ring: () => Math.max(40, SEL.floorU() * (1 + 0.06 * Math.max(0, elv('elSpace') + 5))),   // never below the floor; floor turned off -> 40 u stand-in
  meals: () => SP.breedFood / (1 + 0.1 * Math.max(0, elv('elMass') + 5)),
  danger: () => clamp((elv('elCharge') + 5) / 20, 0, 1),
};
const SH_R = 3200;            // the cell: a sphere; the Rhino needs room to run its 840 u/s line
const SH_RIVAL_PETALS = 10;

// ================= (3) the world =================
let SW = null;
function shepWorld(seed, game, opt) {
  const fr = frameFrom(v3(1, 0, 0), UPW);
  const w = {
    seed, rnd: mulberry32(seed * 7919 + 13), t: 0, game, opt: opt || {},
    pilot: { x: -500, y: 0, z: 0, f: fr.f, u: fr.u, r: fr.r, v: SP.rhinoCruise * 0.5, petals: 10, hitCd: 0, selfCd: 0, slowT: 0, callT: 0, callCd: 0, teleCd: 0, decoyCd: 0, off: !!(opt && opt.noPilot) },
    board: { f: fr.f, u: fr.u, r: fr.r },
    dogs: [{ x: -300, y: 0, z: 260, down: false, id: 0, off: !!(opt && opt.noDogs) }, { x: -300, y: 0, z: -260, down: false, id: 1, off: !!(opt && opt.noDogs) }],
    decoy: null, sheep: [], prisms: [], rivals: [], pens: [], penNext: 0, fx: [], prev: { tele: false, call: false, decoy: false, d0: false, d1: false },
    stats: { eaten: 0, enemyEaten: 0, feeds: 0, shed: 0, starved: 0, born: 0, lost: 0, pooped: 0, petalsTaken: 0, petalsLost: 0, ko: 0, teleports: 0, calls: 0, decoys: 0, pilotDecoys: 0, dangerHits: 0, selfHits: 0 },
    hits: [], deb: 0, pooT: SP.pooEvery, regrowT: 0, finishT: null, done: false, phash: null, phashDirty: true,
    ai: { noiseT: 0, nx: 0, nz: 0, liftT: 4, lift: 0, pnx: 0, pnz: 0, decoyT: 3 },
  };
  SW = w;
  const I = SP.intensity;
  for (let i = 0; i < SP.sheep; i++) { const a = w.rnd() * TAU, r = Math.sqrt(w.rnd()) * 260; addSheep(Math.cos(a) * r, (w.rnd() - 0.5) * 60, Math.sin(a) * r); }
  w.stats.born = 0;
  // pasture: patches of neutral prisms at many heights, so the plane decides which ones the flock can reach
  const patches = game === 'mass' ? 10 : 6;
  for (let p = 0; p < patches; p++) {
    const a = w.rnd() * TAU, r = (game === 'mass' ? 300 : 500) + w.rnd() * (game === 'mass' ? 900 : 1500), cx = Math.cos(a) * r, cz = Math.sin(a) * r;
    const cy = p < 2 ? (w.rnd() - 0.5) * 80 : (w.rnd() - 0.5) * 1200;
    for (let i = 0; i < (game === 'mass' ? 34 : 18); i++) { const b = w.rnd() * TAU, rr = Math.sqrt(w.rnd()) * 150; addShPrism(cx + Math.cos(b) * rr, cy + (w.rnd() - 0.5) * 70, cz + Math.sin(b) * rr, 'pasture'); }
  }
  if (game === 'space') {
    // the fort: three shells of ruby prisms round a core, part shielded, floating above the start
    w.fort = { x: 1300, y: 380, z: 300 };
    const shield = 0.15 + 0.1 * I;
    for (let s = 0; s < 3; s++) {
      const rad = 90 + s * 70, n = Math.floor(TAU * rad / 34);
      for (let i = 0; i < n; i++) for (const yy of [-30, 0, 30]) { const a = i / n * TAU + s * 0.3; addShPrism(w.fort.x + Math.cos(a) * rad, w.fort.y + yy, w.fort.z + Math.sin(a) * rad, 'enemy', w.rnd() < shield + (2 - s) * 0.12); }
    }
    for (let i = 0; i < 50; i++) { const a = w.rnd() * TAU, r = Math.sqrt(w.rnd()) * 80; addShPrism(w.fort.x + Math.cos(a) * r, w.fort.y + (w.rnd() - 0.5) * 60, w.fort.z + Math.sin(a) * r, 'enemy', w.rnd() < shield + 0.25); }
  }
  if (game === 'time') {
    // pens are spheres: the first at the start's height, the rest above and below it
    const pr = 360 - 25 * I, ys = [0, 420, -360, 520, -480];
    for (let i = 0; i < 5; i++) { const a = -Math.PI / 2 + i / 5 * TAU * 0.9 + 0.5; w.pens.push({ x: 900 + Math.cos(a) * 900, y: ys[i], z: Math.sin(a) * 900, r: pr, fill: 0 }); }
  }
  if (game === 'charge') {
    const n = (opt && opt.rivals != null) ? opt.rivals : I;
    for (let i = 0; i < n; i++) { const a = 0.6 + i / Math.max(1, n) * TAU; w.rivals.push({ x: Math.cos(a) * 1500, y: (w.rnd() - 0.5) * 400, z: Math.sin(a) * 1500, vx: 0, vy: 0, vz: 0, petals: SH_RIVAL_PETALS, debt: 0, slow: 0, hitCd: 0, out: 0, tx: 0, ty: 0, tz: 0, re: 0 }); }
  }
  return w;
}
function addSheep(x, y, z) {
  if (SW.sheep.length >= SP.sheepCap) return null;
  const s = { x, y, z, vx: 0, vy: 0, vz: 0, fed: 0, eatCd: SW.rnd(), roll: SW.rnd(), ph: SW.rnd() * TAU, tgt: null, panic: 0, h: 0, hold: 0, stomach: SP.starvation * SP.stomachMeals * (0.5 + 0.5 * SW.rnd()), birthCd: SP.birthCooldown, age: 0 };
  SW.sheep.push(s); SW.stats.born++;
  return s;
}
function addShPrism(x, y, z, kind, shielded) {
  if (Math.hypot(x, y, z) > SH_R - 40) return null;
  const p = { x, y, z, kind, sh: !!shielded, alive: true, claim: 0 };
  SW.prisms.push(p); SW.phashDirty = true; return p;
}
const isDanger = (s) => s.roll < SEL.danger();
// what a creature may steer to: never shielded mass (locked), except that at Space L5 a bite sheds the shield
const edibleFor = (p) => p.alive && p.kind !== 'jade' && (!p.sh || L5k('elSpace'));
const foodFor = (p) => p.alive && p.kind !== 'jade' && !p.sh;   // what a mouthful actually eats: never shielded

// the plane: through the pilot, normal = the board's up
const height = (o) => (o.x - SW.pilot.x) * SW.board.u.x + (o.y - SW.pilot.y) * SW.board.u.y + (o.z - SW.pilot.z) * SW.board.u.z;
function onPlane(o) { const h = height(o), u = SW.board.u; return v3(o.x - u.x * h, o.y - u.y * h, o.z - u.z * h); }
function snapToPlane(o) { const q = onPlane(o); o.x = q.x; o.y = q.y; o.z = q.z; }
// board coordinates (a along the board's right, b along its forward) and back
function toB(o) { const p = SW.pilot, b = SW.board, d = v3(o.x - p.x, o.y - p.y, o.z - p.z); return { x: vdot(d, b.r), z: vdot(d, b.f) }; }
function fromB(a, c) { const p = SW.pilot, b = SW.board; return v3(p.x + b.r.x * a + b.f.x * c, p.y + b.r.y * a + b.f.y * c, p.z + b.r.z * a + b.f.z * c); }

// xz grid hash (y ignored in the key; distance checks are 3D, so a tilted plane only costs a few more checks)
const SH_CELL = 90;
function shKey(x, z) { return ((Math.floor(x / SH_CELL) + 2048) << 12) | (Math.floor(z / SH_CELL) + 2048); }
function shHash(list) { const m = new Map(); for (const o of list) { if (o.alive === false) continue; const k = shKey(o.x, o.z); let b = m.get(k); if (!b) m.set(k, b = []); b.push(o); } return m; }
function shQuery(m, x, z, r, fn) {
  const c0 = Math.floor((x - r) / SH_CELL), c1 = Math.floor((x + r) / SH_CELL), d0 = Math.floor((z - r) / SH_CELL), d1 = Math.floor((z + r) / SH_CELL);
  for (let i = c0; i <= c1; i++) for (let j = d0; j <= d1; j++) { const b = m.get(((i + 2048) << 12) | (j + 2048)); if (b) for (const o of b) fn(o); }
}
// the flock in 3D (centroid) and in board coordinates (what the dogs and the view use)
function flockStats() {
  const S = SW.sheep; let cx = 0, cy = 0, cz = 0;
  for (const s of S) { cx += s.x; cy += s.y; cz += s.z; }
  const n = Math.max(1, S.length); cx /= n; cy /= n; cz /= n;
  const c = toB(v3(cx, cy, cz));
  let r2 = 0, far = null, fd = -1;
  for (const s of S) { const q = toB(s), d = (q.x - c.x) ** 2 + (q.z - c.z) ** 2; r2 += d; if (d > fd) { fd = d; far = q; } }
  return { x: c.x, z: c.z, w: v3(cx, cy, cz), r: Math.sqrt(r2 / n), far, n: S.length };
}

// ================= (4) one step =================
// input: { pilot: { yaw, pitch, roll, thr, call, tele, decoy }, dogs: [{ down, x, y, z }, { down, x, y, z }] }
// (held states; edges are found here; a dog's target is a world point, snapped to the plane)
function shepStep(dt, input) {
  const w = SW; if (w.done) return;
  w.t += dt;
  const inp = input || {};
  const pi = inp.pilot || {}, di = inp.dogs || [];
  // the danger debuff decays linearly to 0 over debuffTime (VesselElementalDebuffByDangerPrismEffect)
  w.hits = w.hits.filter((h) => w.t - h < SP.debuffTime);
  w.deb = 0; for (const h of w.hits) w.deb -= SP.dangerDebuff * (1 - (w.t - h) / SP.debuffTime);
  stepPilot(dt, pi);
  stepBoard(dt);
  // ---- dogs: run to the thumb along the plane; a lift leaves a decoy where the dog stands ----
  for (let k = 0; k < 2; k++) {
    const d = w.dogs[k], t = di[k] || {};
    snapToPlane(d);
    if (d.off) continue;
    const was = w.prev['d' + k];
    if (t.down && t.x != null) {
      const tq = onPlane(v3(t.x, t.y != null ? t.y : w.pilot.y, t.z));
      const dv = vsub(tq, d), L = vlen(dv), step = SP.dogSpeed * dt;
      if (L > step) { d.x += dv.x / L * step; d.y += dv.y / L * step; d.z += dv.z / L * step; } else { d.x = tq.x; d.y = tq.y; d.z = tq.z; }
    } else if (was) { dropDecoy(d.x, d.y, d.z, 'dog'); }
    d.down = !!t.down; w.prev['d' + k] = d.down;
  }
  if (w.decoy) snapToPlane(w.decoy);
  stepSheep(dt);
  stepRivalsSh(dt);
  stepSelfHits();
  // ---- Mass L5: the flock poops a cluster of its own mass ----
  if (L5k('elMass')) {
    w.pooT -= dt;
    if (w.pooT <= 0 && w.sheep.length) {
      w.pooT = SP.pooEvery;
      const s = w.sheep[Math.floor(w.rnd() * w.sheep.length)];
      for (let i = 0; i < 7; i++) { const a = w.rnd() * TAU, r = w.rnd() * 22; if (addShPrism(s.x + Math.cos(a) * r, s.y + (w.rnd() - 0.5) * 16, s.z + Math.sin(a) * r, 'jade')) w.stats.pooped++; }
      w.fx.push({ k: 'poo', x: s.x, y: s.y, z: s.z, t: 0 });
    }
  }
  // ---- pasture regrows slowly in the Mass game ----
  if (w.game === 'mass') { w.regrowT -= dt; if (w.regrowT <= 0) { w.regrowT = 0.4; const a = w.rnd() * TAU, r = 400 + w.rnd() * 1700; addShPrism(Math.cos(a) * r, (w.rnd() - 0.5) * 1000, Math.sin(a) * r, 'pasture'); } }
  // ---- pens (spheres) ----
  if (w.game === 'time' && w.penNext < w.pens.length) {
    const p = w.pens[w.penNext]; let inside = 0;
    for (const s of w.sheep) if ((s.x - p.x) ** 2 + (s.y - p.y) ** 2 + (s.z - p.z) ** 2 < p.r * p.r) inside++;
    p.fill = w.sheep.length ? inside / w.sheep.length : 0;
    if (w.sheep.length && p.fill >= 0.6) { w.penNext++; w.fx.push({ k: 'pen', x: p.x, y: p.y, z: p.z, t: 0, r: p.r }); if (w.penNext >= w.pens.length) w.finishT = w.t; }
  }
  for (const f of w.fx) f.t += dt;
  w.fx = w.fx.filter((f) => f.t < 1.2);
  if (w.t >= SP.round || (w.game === 'time' && w.finishT != null)) w.done = true;
}

function dropDecoy(x, y, z, by) {
  SW.decoy = { x, y, z, by, t: SW.t };
  SW.stats.decoys++; if (by === 'pilot') SW.stats.pilotDecoys++;
  SW.fx.push({ k: 'decoy', x, y, z, t: 0 });
}

// The Grouper's two sticks (VesselTransformer): yaw = XSum, pitch = YSum, roll = YDiff, throttle = the spread.
// On top, the Rhino's straight-line ramp: a slope, not a switch (RampBoostActionSO).
function stepPilot(dt, pi) {
  const w = SW, p = w.pilot;
  if (p.off) return;
  const yaw = clamp(pi.yaw || 0, -1, 1), pitch = clamp(pi.pitch || 0, -1, 1), roll = clamp(pi.roll || 0, -1, 1), thr = clamp(pi.thr == null ? 0.5 : pi.thr, 0, 1);
  const dev = Math.max(Math.abs(yaw), Math.abs(pitch), Math.abs(roll));
  const straight = 1 - clamp((dev - SP.rhinoPlateau) / Math.max(1e-4, SP.rhinoGrace - SP.rhinoPlateau), 0, 1);
  const cruise = Math.max(SP.minSpeed, SP.rhinoCruise * thr);
  p.slowT = Math.max(0, p.slowT - dt);
  const slow = SP.dangerSlow > 0 ? 1 - p.slowT / SP.dangerSlow : 1;     // full stop at the hit, back over dangerSlow
  const target = Math.max(SP.minSpeed * slow, cruise * (1 + (SP.rhinoMaxBoost - 1) * straight) * slow);
  if (p.v < target) p.v = Math.min(target, p.v + SP.rhinoAccel * dt);
  else p.v = Math.max(target, p.v - (straight > 0 ? SP.rhinoBleed : SP.rhinoReturn) * dt);
  const rate = (p.v * SP.pilotRts + SP.pilotTurn) * Math.PI / 180, rrate = (p.v * SP.pilotRts + SP.pilotRoll) * Math.PI / 180;
  // yaw turns the nose toward the right wing; pitch lifts it toward up; roll drops the right wing (up leans right)
  let f = p.f, u = p.u, r = p.r;
  const a = yaw * rate * dt, b = pitch * rate * dt, c = roll * rrate * dt;
  f = vadd(vmul(f, Math.cos(a)), vmul(r, Math.sin(a)));
  const f2 = vadd(vmul(f, Math.cos(b)), vmul(u, Math.sin(b))); u = vsub(vmul(u, Math.cos(b)), vmul(f, Math.sin(b))); f = f2;
  r = vcross(u, f); u = vadd(vmul(u, Math.cos(c)), vmul(r, Math.sin(c)));
  let fr = frameFrom(f, u);
  // the cell wall turns the hull back in (the model's, not an input)
  const pos = v3(p.x, p.y, p.z), dist = vlen(pos);
  if (dist > SH_R) {
    const inward = vmul(pos, -1 / dist), ax = vcross(fr.f, inward), s = vlen(ax);
    if (s > 1e-4) { const k = vmul(ax, 1 / s), ang = Math.min(Math.acos(clamp(vdot(fr.f, inward), -1, 1)), rate * 1.5 * dt); fr = frameFrom(vrot(fr.f, k, ang), vrot(fr.u, k, ang)); }
  }
  p.f = fr.f; p.u = fr.u; p.r = fr.r;
  p.x += p.f.x * p.v * dt; p.y += p.f.y * p.v * dt; p.z += p.f.z * p.v * dt;
  // call (edge, long cooldown)
  p.callT = Math.max(0, p.callT - dt); p.callCd = Math.max(0, p.callCd - dt); p.teleCd = Math.max(0, p.teleCd - dt); p.hitCd = Math.max(0, p.hitCd - dt); p.selfCd = Math.max(0, p.selfCd - dt); p.decoyCd = Math.max(0, p.decoyCd - dt);
  if (pi.call && !w.prev.call && p.callCd <= 0) { p.callT = SP.callTime; p.callCd = SP.callCooldown; w.stats.calls++; w.fx.push({ k: 'call', x: p.x, y: p.y, z: p.z, t: 0 }); }
  w.prev.call = !!pi.call;
  // Time L5: the pilot drops THE decoy (the dogs' one decoy; a new one replaces the old) where it is
  if (pi.decoy && !w.prev.decoy && L5k('elTime') && p.decoyCd <= 0) { dropDecoy(p.x, p.y, p.z, 'pilot'); p.decoyCd = SP.decoyCooldown; }
  w.prev.decoy = !!pi.decoy;
  // teleport to the decoy (edge); the decoy is spent
  if (pi.tele && !w.prev.tele && w.decoy && p.teleCd <= 0) {
    const d = w.decoy;
    w.fx.push({ k: 'tele', x: p.x, y: p.y, z: p.z, t: 0, x2: d.x, y2: d.y, z2: d.z });
    p.x = d.x; p.y = d.y; p.z = d.z; p.teleCd = 0.6; w.stats.teleports++;
    w.decoy = null;
  }
  w.prev.tele = !!pi.tele;
}

// The board follows the hull (KiteNavigator.UpdateBoard): Eased toward the hull's x-z plane, Locked to it, or Level.
function stepBoard(dt) {
  const w = SW, p = w.pilot, b = w.board;
  if (p.off) { w.board = frameFrom(b.f, UPW); return; }
  const mode = Math.round(SP.frame);
  if (mode === 1) { w.board = { f: p.f, u: p.u, r: p.r }; return; }
  if (mode === 0) {
    const a = 1 - Math.exp(-SP.easeRate * dt);
    const f = vnorm(vadd(b.f, vmul(vsub(p.f, b.f), a))), u = vadd(b.u, vmul(vsub(p.u, b.u), a));
    w.board = frameFrom(f, u); return;
  }
  const fh = v3(p.f.x, 0, p.f.z);
  w.board = frameFrom(vlen(fh) > 0.15 ? fh : b.f, UPW);
}

function repellers() {
  const w = SW, R = SEL.ring(), out = [];
  if (!w.pilot.off && w.pilot.callT <= 0) out.push({ x: w.pilot.x, y: w.pilot.y, z: w.pilot.z, r: R });
  for (const d of w.dogs) if (!d.off) out.push({ x: d.x, y: d.y, z: d.z, r: R });
  if (w.decoy) out.push({ x: w.decoy.x, y: w.decoy.y, z: w.decoy.z, r: R });
  return out;
}

function stepSheep(dt) {
  const w = SW, S = w.sheep, U = w.board.u;
  if (w.phashDirty) { w.phash = shHash(w.prisms); w.phashDirty = false; }
  const hash = shHash(S), reps = repellers(), vmax0 = SP.sheepSpeed * SEL.speed(), p = w.pilot;
  const calling = !p.off && p.callT > 0;
  const atk = L5k('elCharge') && w.rivals.length;
  const born = [];
  for (const s of S) {
    let sx = 0, sy = 0, sz = 0, ax = 0, ay = 0, az = 0, cx = 0, cy = 0, cz = 0, n = 0;
    shQuery(hash, s.x, s.z, 160, (o) => {
      if (o === s) return;
      const dx = s.x - o.x, dy = s.y - o.y, dz = s.z - o.z, d2 = dx * dx + dy * dy + dz * dz;
      if (d2 > 160 * 160) return;
      const d = Math.sqrt(d2) || 1;
      if (d < 58) { const k = (58 - d) / 58 / d; sx += dx * k; sy += dy * k; sz += dz * k; }
      ax += o.vx; ay += o.vy; az += o.vz; cx += o.x; cy += o.y; cz += o.z; n++;
    });
    let g = v3(sx * 1.6, sy * 1.6, sz * 1.6);
    if (n) g = vadd(g, vadd(vmul(v3(ax / n, ay / n, az / n), 0.5 / vmax0), vmul(v3(cx / n - s.x, cy / n - s.y, cz / n - s.z), 0.6 / 160)));
    // the herders: a cylinder of repulsion round each, along the plane's normal (in-plane distance)
    let panic = 0;
    for (const r of reps) {
      let dv = v3(s.x - r.x, s.y - r.y, s.z - r.z); dv = vsub(dv, vmul(U, vdot(dv, U)));
      const d = vlen(dv);
      if (d < r.r) { const k = 1 - d / r.r; g = vadd(g, vmul(dv, 3.2 * k / (d || 1))); panic = Math.max(panic, k); }
    }
    if (calling) { let dv = v3(p.x - s.x, p.y - s.y, p.z - s.z); dv = vsub(dv, vmul(U, vdot(dv, U))); const d = vlen(dv) || 1; if (d > 30) g = vadd(g, vmul(dv, 2.6 / d)); panic = Math.max(panic, 0.5); }
    // Charge L5: a rival in range draws the flock in
    if (atk) { let best = null, bd = SP.attackRange; for (const r of w.rivals) if (r.out <= 0) { const d = Math.hypot(r.x - s.x, r.y - s.y, r.z - s.z); if (d < bd) { bd = d; best = r; } } if (best) { g = vadd(g, vmul(v3(best.x - s.x, best.y - s.y, best.z - s.z), 2.2 / bd)); panic = Math.max(panic, 0.6); } }
    // graze: an unafraid sheep wanders to the nearest edible prism it can sense (in 3D)
    s.eatCd -= dt;
    let gz = 0;
    if (s.hold > 0 && panic >= 0.5) s.hold = 0;   // a herder scared it off its meal
    if (panic < 0.5 && s.hold <= 0) {
      if (!s.tgt || !s.tgt.alive || !edibleFor(s.tgt)) {
        s.tgt = null; let bd = 130 * 130;
        if (s.eatCd <= 0) shQuery(w.phash, s.x, s.z, 130, (q) => { if (!edibleFor(q)) return; const d = (q.x - s.x) ** 2 + (q.y - s.y) ** 2 + (q.z - s.z) ** 2; if (d < bd) { bd = d; s.tgt = q; } });
      }
      if (s.tgt) { const dv = v3(s.tgt.x - s.x, s.tgt.y - s.y, s.tgt.z - s.z), d = vlen(dv) || 1; g = vadd(g, vmul(dv, 0.9 / d)); gz = vdot(dv, U) / d; }
    } else if (s.hold <= 0) s.tgt = null;
    // wander (seeded per sheep, smooth), along the board
    s.ph += dt * (0.6 + s.roll * 0.8);
    g = vadd(g, vadd(vmul(w.board.r, Math.cos(s.ph * 1.7 + s.roll * 9) * 0.25), vmul(w.board.f, Math.sin(s.ph * 1.3 + s.roll * 5) * 0.25)));
    // the wall
    const rr = Math.hypot(s.x, s.y, s.z); if (rr > SH_R - 200) g = vadd(g, v3(-s.x / rr * 2, -s.y / rr * 2, -s.z / rr * 2));
    // steer along the plane; across it a spring pulls the sheep home to the plane
    const gn = vdot(g, U), gp = vsub(g, vmul(U, gn));
    const gm = vlen(gp), vmax = vmax0 * (1 + 0.8 * panic);
    const want = gm > 0.02 && s.hold <= 0 ? vmul(gp, vmax * Math.min(1, gm) / gm) : v3(0, 0, 0);
    const vel = v3(s.vx, s.vy, s.vz);
    let vn = vdot(vel, U); const vp = vsub(vel, vmul(U, vn));
    const ag = Math.min(1, (s.hold > 0 ? 4 : 3) * dt);   // feedingBrakeSharpness 4: it brakes to a hover on its meal
    const vp2 = vadd(vp, vmul(vsub(want, vp), ag));
    const h = height(s);
    vn += (-h * SP.planePull - vn * 1.8 + (gn - gz * 0.9) * 40 + gz * 60 + Math.sin(s.ph * 2.1 + s.roll * 3) * 18) * dt;
    s.vx = vp2.x + U.x * vn; s.vy = vp2.y + U.y * vn; s.vz = vp2.z + U.z * vn;
    s.x += s.vx * dt; s.y += s.vy * dt; s.z += s.vz * dt;
    s.panic = panic; s.h = h;
    // the mouthful (LightFauna): reach the meal, hold consumeHold facing it, then eat it and its neighbours
    s.age += dt; s.birthCd -= dt;
    if (s.tgt && s.hold <= 0 && s.eatCd <= 0 && (s.tgt.x - s.x) ** 2 + (s.tgt.y - s.y) ** 2 + (s.tgt.z - s.z) ** 2 < 22 * 22) s.hold = SP.consumeHold;
    if (s.hold > 0) {
      s.hold -= dt;
      const q = s.tgt;
      if (!q || !q.alive || !edibleFor(q)) { s.hold = 0; s.tgt = null; }
      else if (s.hold <= 0) {
        s.tgt = null; s.eatCd = 0.4;
        if (q.sh) { q.sh = false; w.stats.shed++; w.fx.push({ k: 'shed', x: q.x, y: q.y, z: q.z, t: 0 }); }   // Prism.Consume on shielded mass: the shield goes, nothing is eaten
        else {
          let bites = 0;
          const eat = (o) => { o.alive = false; bites++; w.stats.eaten++; if (o.kind === 'enemy') w.stats.enemyEaten++; };
          eat(q);
          shQuery(w.phash, q.x, q.z, SP.biteRadius, (o) => { if (bites >= SP.maxBites || o === q || !foodFor(o)) return; if ((o.x - q.x) ** 2 + (o.y - q.y) ** 2 + (o.z - q.z) ** 2 < SP.biteRadius * SP.biteRadius) eat(o); });
          w.phashDirty = true; s.stomach = Math.min(SP.starvation * SP.stomachMeals, s.stomach + SP.starvation * Math.min(1, bites / 2)); s.fed++; w.stats.feeds++;
          if (s.fed >= SEL.meals() && s.birthCd <= 0) { s.fed -= SEL.meals(); s.birthCd = SP.birthCooldown; born.push(s); }
        }
      }
    }
    // starvation: no imposed clock, only an empty stomach (it withers out, never pops)
    if (SP.starvation > 0) { s.stomach -= dt; if (s.stomach <= 0) s.starved = true; }
  }
  for (let i = S.length - 1; i >= 0; i--) if (S[i].starved) { const s = S[i]; S.splice(i, 1); w.stats.lost++; w.stats.starved++; w.fx.push({ k: 'wither', x: s.x, y: s.y, z: s.z, t: 0, danger: isDanger(s) }); }
  for (const s of born) { const l = addSheep(s.x + (w.rnd() - 0.5) * 20, s.y, s.z + (w.rnd() - 0.5) * 20); if (l) w.fx.push({ k: 'birth', x: l.x, y: l.y, z: l.z, t: 0 }); }
  if (w.phashDirty) w.prisms = w.prisms.filter((q) => q.alive);
}

// The pilot flies into its own danger sheep: the existing collision mechanics. The sheep dies; the pilot takes the
// own-domain danger hit (full stop slow, ramp reset, every element debuffed for debuffTime, cooldown 1 s), and no
// permanent burn because the sheep is its own domain (Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md, danger prisms).
function stepSelfHits() {
  const w = SW, p = w.pilot; if (p.off) return;
  for (let i = w.sheep.length - 1; i >= 0; i--) {
    const s = w.sheep[i];
    if (Math.abs(s.x - p.x) > 26 || Math.abs(s.z - p.z) > 26 || Math.abs(s.y - p.y) > 26) continue;
    if (!isDanger(s) || (s.x - p.x) ** 2 + (s.y - p.y) ** 2 + (s.z - p.z) ** 2 > 26 * 26) continue;
    w.sheep.splice(i, 1); w.stats.lost++; w.fx.push({ k: 'sting', x: s.x, y: s.y, z: s.z, t: 0 });
    if (p.selfCd <= 0) { p.selfCd = 1; w.stats.selfHits++; w.hits.push(w.t); p.slowT = SP.dangerSlow; p.v = Math.min(p.v, SP.minSpeed); w.fx.push({ k: 'hit', x: p.x, y: p.y, z: p.z, t: 0 }); }
  }
}

function stepRivalsSh(dt) {
  const w = SW; if (!w.rivals.length) return;
  const p = w.pilot, spd0 = 110 + 25 * SP.intensity;
  for (const r of w.rivals) {
    if (r.out > 0) { r.out -= dt; if (r.out <= 0) { r.petals = SH_RIVAL_PETALS; const a = w.rnd() * TAU; r.x = Math.cos(a) * 1800; r.y = (w.rnd() - 0.5) * 400; r.z = Math.sin(a) * 1800; } continue; }
    r.slow = Math.max(0, r.slow - dt); r.hitCd = Math.max(0, r.hitCd - dt); r.eatCd = Math.max(0, (r.eatCd || 0) - dt); r.re -= dt;
    // hunt: the pilot when it is near, else the nearest plain sheep (wolves eat the flock)
    if (r.re <= 0) {
      r.re = 0.5;
      const dp = p.off ? 1e9 : Math.hypot(p.x - r.x, p.y - r.y, p.z - r.z);
      let best = null, bd = 1e9; for (const s of w.sheep) { const d = Math.hypot(s.x - r.x, s.y - r.y, s.z - r.z) + (isDanger(s) ? 400 : 0); if (d < bd) { bd = d; best = s; } }
      if (dp < 700 || !best) { r.tx = p.x; r.ty = p.y; r.tz = p.z; } else { r.tx = best.x; r.ty = best.y; r.tz = best.z; }
    }
    const spd = spd0 * (r.slow > 0 ? 0.35 : 1), dx = r.tx - r.x, dy = r.ty - r.y, dz = r.tz - r.z, d = Math.hypot(dx, dy, dz) || 1, k = Math.min(1, 2 * dt);
    r.vx += (dx / d * spd - r.vx) * k; r.vy += (dy / d * spd - r.vy) * k; r.vz += (dz / d * spd - r.vz) * k;
    r.x += r.vx * dt; r.y += r.vy * dt; r.z += r.vz * dt;
    // ram the pilot
    if (!p.off && p.hitCd <= 0 && Math.hypot(p.x - r.x, p.y - r.y, p.z - r.z) < 30) { p.hitCd = 1.5; if (p.petals > 0) { p.petals--; r.petals++; w.stats.petalsLost++; } w.fx.push({ k: 'hit', x: p.x, y: p.y, z: p.z, t: 0 }); }
    // touch sheep: danger sheep sting (a petal and a slow) and are spent; plain sheep get eaten (Charge L5: they nip first)
    for (let i = w.sheep.length - 1; i >= 0 && r.eatCd <= 0; i--) {
      const s = w.sheep[i];
      if (Math.abs(s.x - r.x) > 26 || Math.abs(s.z - r.z) > 26 || Math.abs(s.y - r.y) > 26) continue;
      if (isDanger(s)) { r.debt += 1; r.slow = 1.5; w.stats.dangerHits++; }
      else if (L5k('elCharge')) r.debt += 0.34;
      r.eatCd = 1.2; w.sheep.splice(i, 1); w.stats.lost++; w.fx.push({ k: isDanger(s) ? 'sting' : 'eaten', x: s.x, y: s.y, z: s.z, t: 0 });
    }
    if (r.debt >= 1) { const n = Math.floor(r.debt); r.debt -= n; const take = Math.min(n, r.petals); r.petals -= take; p.petals += take; w.stats.petalsTaken += take; if (r.petals <= 0) { r.out = 5; w.stats.ko++; w.fx.push({ k: 'ko', x: r.x, y: r.y, z: r.z, t: 0 }); } }
  }
}

function shepScore() {
  const w = SW, s = w.stats;
  if (w.game === 'time') return w.penNext * 10 + (w.finishT != null ? Math.max(0, Math.round(SP.round - w.finishT)) : 0);
  if (w.game === 'space') return s.enemyEaten;
  if (w.game === 'mass') return s.born + w.prisms.filter((q) => q.kind === 'jade').length;
  return s.petalsTaken - s.petalsLost + 3 * s.ko;
}

// ================= (5) the AI crews: inputs only =================
// Where the flock should go, in 3D (a pen, the fort, a rival, the nearest pasture).
// Fauna starve (round 4), so a hungry herd goes to pasture first: the crew's job includes letting it graze.
function herdHunger() { const S = SW.sheep; if (!S.length || SP.starvation <= 0) return 0; let h = 0; for (const s of S) h += 1 - s.stomach / (SP.starvation * SP.stomachMeals); return h / S.length; }
function shTarget() {
  const w = SW, F = flockStats().w;
  if (w.game !== 'time' && herdHunger() > 0.6 && !(w.game === 'space' && w.fort && Math.hypot(w.fort.x - F.x, w.fort.y - F.y, w.fort.z - F.z) < 500)) {
    let best = null, bd = 1e9;
    for (const q of w.prisms) { if (!foodFor(q)) continue; const d = Math.hypot(q.x - F.x, q.y - F.y, q.z - F.z); if (d < bd) { bd = d; best = q; } }
    if (best) return v3(best.x, best.y, best.z);
  }
  if (w.game === 'time') { const p = w.pens[Math.min(w.penNext, w.pens.length - 1)]; return v3(p.x, p.y, p.z); }
  if (w.game === 'charge') { let best = null, bd = 1e9; for (const r of w.rivals) if (r.out <= 0) { const d = Math.hypot(r.x - F.x, r.y - F.y, r.z - F.z); if (d < bd) { bd = d; best = r; } } if (best) return v3(best.x, best.y, best.z); }
  if (w.game === 'space' && w.fort) {
    let any = false; for (const q of w.prisms) if (q.kind === 'enemy' && edibleFor(q)) { any = true; break; }
    if (any) return v3(w.fort.x, w.fort.y, w.fort.z);
  }
  let best = null, bd = 1e9;
  for (const q of w.prisms) { if (!edibleFor(q)) continue; const d = Math.hypot(q.x - F.x, q.y - F.y, q.z - F.z); if (d > 150 && d < bd) { bd = d; best = q; } }
  return best ? v3(best.x, best.y, best.z) : F;
}
// The point behind the flock (seen from the target) — Strombom's driving position, in board coordinates.
function drivePoint(F, T, back) { let ux = T.x - F.x, uz = T.z - F.z; const L = Math.hypot(ux, uz) || 1; ux /= L; uz /= L; return { x: F.x - ux * back, z: F.z - uz * back, ux, uz }; }

function shAI(role) {
  const w = SW, F = flockStats(), T3 = shTarget(), T = toB(T3), R = SEL.ring(), nz = SP.aiNoise;
  const out = { pilot: { yaw: 0, pitch: 0, roll: 0, thr: 0.5, call: false, tele: false, decoy: false }, dogs: [{ down: true, x: w.dogs[0].x, y: w.dogs[0].y, z: w.dogs[0].z }, { down: true, x: w.dogs[1].x, y: w.dogs[1].y, z: w.dogs[1].z }] };
  const adt = Math.max(0, w.t - (w.ai.lastT || 0)); w.ai.lastT = w.t;
  w.ai.noiseT -= adt;
  if (w.ai.noiseT <= 0) { w.ai.noiseT = 0.5; w.ai.nx = (w.rnd() - 0.5) * 2 * nz * 260; w.ai.nz = (w.rnd() - 0.5) * 2 * nz * 260; w.ai.pnx = (w.rnd() - 0.5) * nz; w.ai.pnz = (w.rnd() - 0.5) * nz; }
  if (role === 'dogs' || role === 'all') {
    // in board coordinates: dog 0 collects the straggler; dog 1 drives from behind, on a flank
    // a hungry herd at its pasture is left to graze: the drive stands off so the rings stop breaking its mouthfuls
    const graze = w.game !== 'time' && herdHunger() > 0.6 && Math.hypot(T.x - F.x, T.z - F.z) < F.r + 200;
    const drive = drivePoint(F, T, F.r + R * (graze ? 1.6 : 0.55));
    const far = F.far, spread = F.r;
    let a;
    if (far && spread > 150) { const ox = far.x - F.x, oz = far.z - F.z, L = Math.hypot(ox, oz) || 1; a = { x: far.x + ox / L * R * 0.55, z: far.z + oz / L * R * 0.55 }; }
    else a = { x: drive.x - drive.uz * R * 0.7, z: drive.z + drive.ux * R * 0.7 };
    const b = { x: drive.x + drive.uz * R * 0.45, z: drive.z - drive.ux * R * 0.45 };
    const route = (d, pt) => {   // go round the flock, not through it
      const vx = pt.x - d.x, vz = pt.z - d.z, L = Math.hypot(vx, vz) || 1, tx = F.x - d.x, tz = F.z - d.z, along = clamp((tx * vx + tz * vz) / (L * L), 0, 1);
      const qx = d.x + vx * along - F.x, qz = d.z + vz * along - F.z, q = Math.hypot(qx, qz);
      if (along > 0 && along < 1 && q < F.r + R * 0.6) { const s = q > 1 ? 1 / q : 1; return { x: F.x + qx * s * (F.r + R * 0.9), z: F.z + qz * s * (F.r + R * 0.9) }; }
      return pt;
    };
    const pa = route(toB(w.dogs[0]), a), pb = route(toB(w.dogs[1]), b);
    const wa = fromB(pa.x + w.ai.nx, pa.z + w.ai.nz), wb = fromB(pb.x - w.ai.nz, pb.z + w.ai.nx);
    out.dogs[0] = { down: true, x: wa.x, y: wa.y, z: wa.z };
    out.dogs[1] = { down: true, x: wb.x, y: wb.y, z: wb.z };
    // every few seconds the driving dog lifts for a moment: a decoy behind the flock holds that side
    w.ai.liftT -= adt;
    if (w.ai.liftT <= 0) { w.ai.lift = 0.15; w.ai.liftT = 7 + nz * 4; }
    if (w.ai.lift > 0) { w.ai.lift -= adt; out.dogs[1].down = false; }
  }
  if (role === 'pilot' || role === 'all') {
    const p = w.pilot, P = v3(p.x, p.y, p.z);
    // the plane the pilot wants: through itself and the flock's target, as level as that allows (the flock follows the
    // plane, so a plane through the pilot and the target carries the flock to the target's height)
    const tp = vsub(T3, P), td = vlen(tp) > 1 ? vmul(tp, 1 / vlen(tp)) : p.f;
    let n = vsub(UPW, vmul(td, td.y));
    n = vlen(n) > 0.05 ? vnorm(n) : p.u;
    // its station: the flank opposite the driving dog, put on that plane
    let dir = vsub(T3, F.w); dir = vsub(dir, vmul(n, vdot(dir, n))); dir = vlen(dir) > 1 ? vnorm(dir) : p.f;
    const side = vcross(n, dir), back = F.r + R * 0.35;
    let goal = vadd(F.w, vadd(vmul(dir, -back), vmul(side, -(F.r + R * 0.6))));
    goal = vsub(goal, vmul(n, vdot(vsub(goal, P), n)));
    // Charge: keep away from the wolves first
    for (const r of w.rivals) if (r.out <= 0 && Math.hypot(r.x - p.x, r.y - p.y, r.z - p.z) < 260) goal = vadd(P, vmul(v3(p.x - r.x, p.y - r.y, p.z - r.z), 3));
    // and away from its own danger sheep just ahead (the hit costs speed and elements)
    for (const s of w.sheep) { if (!isDanger(s)) continue; const dv = v3(s.x - p.x, s.y - p.y, s.z - p.z), ahead = vdot(dv, p.f); if (ahead > 0 && ahead < 120 && vlen(vsub(dv, vmul(p.f, ahead))) < 30) { out.pilot.pitch = vdot(dv, p.u) > 0 ? -1 : 1; break; } }
    const gv = vsub(goal, P), dist = vlen(gv), g = vmul(gv, 1 / (dist || 1));
    if (dist < 160) { out.pilot.yaw = 0.8; }                                           // there: circle on station
    else { out.pilot.yaw = clamp(Math.atan2(vdot(g, p.r), vdot(g, p.f)) * 1.6 + w.ai.pnx, -1, 1); if (!out.pilot.pitch) out.pilot.pitch = clamp(Math.asin(clamp(vdot(g, p.u), -1, 1)) * 1.6 + w.ai.pnz, -1, 1); }   // elevation only: a goal behind is a turn, not a loop
    // roll the hull's up toward the wanted normal: that is what tilts the plane
    const np = vsub(n, vmul(p.f, vdot(n, p.f)));
    out.pilot.roll = clamp(Math.atan2(vdot(np, p.r), vdot(np, p.u)) * 2, -1, 1);
    out.pilot.thr = dist > 600 ? 1 : 0.5;
    // call when the flock has scattered; teleport when the decoy sits much closer to where the pilot wants to be
    out.pilot.call = F.r > 200 && p.callCd <= 0 && Math.hypot(F.w.x - p.x, F.w.y - p.y, F.w.z - p.z) < 900;
    // Time L5: on station with no decoy down, leave one to come back to
    w.ai.decoyT -= adt;
    if (L5k('elTime') && !w.decoy && dist < 260 && w.ai.decoyT <= 0) { out.pilot.decoy = !w.prev.decoy; w.ai.decoyT = 6; }
    if (w.decoy && vlen(vsub(v3(w.decoy.x, w.decoy.y, w.decoy.z), goal)) < dist - 500) out.pilot.tele = !w.prev.tele;
  }
  return out;
}

// ================= (6) the headless scorecard =================
const SHEP_ROWS = [
  { id: 'pilotOnly', name: 'Pilot only (no dogs)', opt: { noDogs: true } },
  { id: 'dogsOnly', name: 'Dogs only (no pilot)', opt: { noPilot: true } },
  { id: 'crew', name: 'Pilot + 2 dogs (yours)', opt: {} },
  { id: 'level', name: 'Level plane (round 2 frame)', set: { frame: 2 } },
  { id: 'smallthumb', name: 'Small thumb (14 mm floor)', set: { thumbMm: 14 } },
  { id: 'rookie', name: 'Rookie crew (noise 0.6)', set: { aiNoise: 0.6 } },
  { id: 'time5', name: 'Time 5 (pilot decoy)', set: { elTime: 5 } },
  { id: 'space5', name: 'Space 5 (eat shields)', set: { elSpace: 5 } },
  { id: 'mass5', name: 'Mass 5 (poop mass)', set: { elMass: 5 } },
  { id: 'charge5', name: 'Charge 5 (attack)', set: { elCharge: 5 } },
  { id: 'charge15', name: 'Charge 15 (all danger)', set: { elCharge: 15 } },
];
function shepRun(game, seed, opt, secs) {
  const keep = SW, round = SP.round; SP.round = secs || 30;
  shepWorld(seed, game, opt);
  const dt = 1 / 20;
  while (!SW.done) shepStep(dt, shAI('all'));
  const sc = shepScore(), st = Object.assign({ sheep: SW.sheep.length }, SW.stats);
  SP.round = round; SW = keep;
  return { score: sc, stats: st };
}
function shepBatch(opts) {
  const o = opts || {}, seeds = o.seeds || [1], secs = o.secs || 30, rows = o.rows || SHEP_ROWS, res = [];
  for (const row of rows) {
    const saved = {}; for (const k in (row.set || {})) { saved[k] = SP[k]; SP[k] = row.set[k]; }
    const r = { id: row.id, name: row.name };
    for (const g of SHEP_GAMES) { let s = 0; for (const sd of seeds) s += shepRun(g.id, sd, row.opt, secs).score; r[g.id] = Math.round(s / seeds.length * 10) / 10; }
    for (const k in saved) SP[k] = saved[k];
    res.push(r);
  }
  return res;
}

// ================= (7) the page: overlay, camera, drawing, touch =================
let shepOn = false;
const shepState = { game: lsGet('lab:' + LAB.name + ':shepGame', 'space'), role: lsGet('lab:' + LAB.name + ':shepRole', PLATFORM.device === 'phone' ? 'dogs' : 'ai'), seed: 1, paused: false, manual: false, sheet: false, showThumb: true, batch: null, view: lsGet('lab:' + LAB.name + ':shepView4', 'auto') };
const shepRoot = el('div'); shepRoot.id = 'shep'; shepRoot.hidden = true;
shepRoot.innerHTML =
  '<canvas id="shepCanvas" aria-label="' + SHEP_NAME + ': the herd, the pilot and two dogs on the pilot\'s plane, seen from above"></canvas>' +
  '<div id="shepTop">' +
  '<button class="sbtn" id="shepBack" title="Back to the lab">&#8592; Lab</button>' +
  '<b class="stitle">' + SHEP_NAME + '</b>' +
  '<select id="shepGame" aria-label="Game"></select>' +
  '<select id="shepRole" aria-label="Who you play"></select>' +
  '<button class="sbtn" id="shepRestart">Restart</button>' +
  '<button class="sbtn" id="shepPause">Pause</button>' +
  '<button class="sbtn" id="shepMore" aria-expanded="false">Settings</button>' +
  '</div>' +
  '<div id="shepHud"></div>' +
  '<div id="shepToast"></div>' +
  '<div id="shepPads"><button class="spad" id="shepCall">CALL</button><button class="spad" id="shepDecoy" hidden>DECOY</button></div>' +
  '<div id="shepEnd" hidden></div>' +
  '<aside id="shepSheet" hidden></aside>';
document.body.appendChild(shepRoot);
const shepCss = document.createElement('style');
shepCss.textContent = `
  #shep { position: fixed; inset: 0; z-index: 50; background: var(--stage); color: #e6efec; touch-action: none; user-select: none; -webkit-user-select: none; overflow: hidden; }
  #shepCanvas { position: absolute; inset: 0; width: 100%; height: 100%; display: block; }
  #shepTop { position: absolute; left: 0; right: 0; top: 0; display: flex; gap: 6px; align-items: center; flex-wrap: wrap; pointer-events: none;
             padding: calc(6px + env(safe-area-inset-top, 0px)) max(12px, env(safe-area-inset-right, 0px)) 6px max(12px, env(safe-area-inset-left, 0px)); }
  #shepTop > * { pointer-events: auto; }
  #shepTop .stitle { font: 700 15px var(--f-display); letter-spacing: .03em; margin: 0 4px; }
  #shepTop select { width: auto; max-width: 46vw; background: rgba(10,20,22,.85); color: #e6efec; border: 1px solid #34514d; border-radius: 6px; padding: 4px 6px; font: 12px var(--f-ui); }
  .sbtn { background: rgba(10,20,22,.85); color: #e6efec; border: 1px solid #34514d; border-radius: 6px; padding: 4px 10px; cursor: pointer; font: 600 12px var(--f-display); min-height: 30px; }
  .sbtn:hover, .sbtn:focus-visible { border-color: #f0a043; }
  #shepHud { position: absolute; left: max(12px, env(safe-area-inset-left, 0px)); top: calc(46px + env(safe-area-inset-top, 0px)); font: 12px/1.45 var(--f-mono); color: #d6e4e0; pointer-events: none; white-space: pre; text-shadow: 0 1px 2px #000; }
  #shepHud .big { font: 700 18px var(--f-display); color: #fff; }
  #shepToast { position: absolute; left: 50%; bottom: calc(118px + env(safe-area-inset-bottom, 0px)); transform: translateX(-50%); background: rgba(8,16,18,.88); color: #ffe2b8; border: 1px solid #f0a043; border-radius: 6px; padding: 5px 12px; font: 12px var(--f-mono); pointer-events: none; opacity: 0; transition: opacity .25s; max-width: calc(100% - 32px); text-align: center; }
  #shepToast.show { opacity: 1; }
  #shepPads { position: absolute; left: 50%; transform: translateX(-50%); bottom: calc(22px + env(safe-area-inset-bottom, 0px)); pointer-events: none; display: flex; gap: 12px; }
  .spad[hidden] { display: none; }
  .spad { pointer-events: auto; width: 84px; height: 84px; border-radius: 50%; border: 2px solid #f0a043; background: rgba(240,160,67,.16); color: #ffd9a6; font: 700 14px var(--f-display); letter-spacing: .06em; cursor: pointer; }
  .spad[disabled] { opacity: .45; }
  #shepEnd { position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%); background: rgba(10,18,20,.95); border: 1px solid #f0a043; border-radius: 10px; padding: 14px 16px; width: min(420px, calc(100% - 32px)); font: 13px/1.5 var(--f-ui); }
  #shepEnd h3 { font: 700 16px var(--f-display); margin: 0 0 6px; }
  #shepEnd .row2 { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 10px; }
  #shepSheet { position: absolute; right: 0; top: 0; bottom: 0; width: min(380px, 100%); background: var(--panel); color: var(--ink); overflow: auto; border-left: 1px solid var(--line);
               padding: calc(10px + env(safe-area-inset-top, 0px)) max(16px, env(safe-area-inset-right, 0px)) calc(16px + env(safe-area-inset-bottom, 0px)) 16px; user-select: text; -webkit-user-select: text; touch-action: pan-y; }
  #shepSheet .head { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
  #shepSheet h2 { font: 700 15px var(--f-display); margin: 0; }
  #shepSheet .el5 { font: 10px var(--f-mono); padding: 1px 6px; border-radius: 99px; background: color-mix(in srgb, var(--accent) 22%, transparent); margin-left: 6px; }
  #shepSheet textarea { min-height: 54px; }
  #shepSheet td.best { color: var(--ok); font-weight: 700; }
  @media (prefers-reduced-motion: reduce) { #shepToast { transition: none; } }
`;
document.head.appendChild(shepCss);
const sc = $('shepCanvas'), sg = sc.getContext('2d');
for (const g of SHEP_GAMES) { const o = el('option', '', g.name); o.value = g.id; $('shepGame').appendChild(o); }
for (const r of SHEP_ROLES) { const o = el('option', '', r.name); o.value = r.id; $('shepRole').appendChild(o); }
if (!SHEP_GAMES.some((g) => g.id === shepState.game)) shepState.game = 'space';
if (!SHEP_ROLES.some((r) => r.id === shepState.role)) shepState.role = 'ai';
$('shepGame').value = shepState.game; $('shepRole').value = shepState.role;

// ---- camera: the Wrasse's kite view (KiteNavigatorView): orthographic, straight down the board's up axis ----
// Heading up (the Wrasse): the board's forward is up the screen. North up: world +z projected onto the plane.
// Steady (round 4, the jacks' default): the view never turns with the marlin's heading. Each frame the last
// forward is laid onto the new plane (the smallest turn that keeps looking straight down it), and the view
// pans only when the flock leaves a slack box in the middle of the screen.
const cam = { c: v3(0, 0, 0), sf: v3(0, 0, 1), sr: v3(1, 0, 0), k: 1, kpx: 1, cx: 0, cy: 0, short: 390 };
const VIEW_NAMES = { auto: 'Auto (Steady for the jacks, Heading up for the marlin)', steady: 'Steady (no turning with the marlin)', heading: 'Heading up (the Wrasse\'s kite view)', north: 'North up' };
function camView() { const v = shepState.view; return v !== 'auto' ? v : shepState.role === 'pilot' ? 'heading' : 'steady'; }
function shepResize() { const r = sc.getBoundingClientRect(), dpr = Math.min(2, devicePixelRatio || 1); const w = Math.max(1, Math.round(r.width * dpr)), h = Math.max(1, Math.round(r.height * dpr)); if (sc.width !== w || sc.height !== h) { sc.width = w; sc.height = h; } cam.kpx = sc.width / Math.max(1, r.width); }
function camUpdate(dt) {
  const F = flockStats(), p = SW.pilot, U = SW.board.u;
  const wgt = p.off ? 0 : shepState.role === 'pilot' ? 0.45 : 0.25;
  const goal = onPlane(F.n ? vadd(vmul(F.w, 1 - wgt), vmul(v3(p.x, p.y, p.z), wgt)) : v3(p.x, p.y, p.z));
  const a = dt > 0 ? Math.min(1, dt * 1.6) : 1, view = camView();
  cam.cx = sc.width / 2; cam.cy = sc.height * 0.54; cam.short = Math.min(sc.width, sc.height);
  cam.k = cam.short / SP.viewSpan;
  let pan = vsub(goal, cam.c);
  if (view === 'steady' && dt > 0) {
    // only the part of the offset outside the slack box moves the view
    const ga = vdot(pan, cam.sr), gb = vdot(pan, cam.sf), hw = cam.cx / cam.k * SP.camSlack, hh = Math.min(cam.cy, sc.height - cam.cy) / cam.k * SP.camSlack;
    pan = vadd(vmul(cam.sr, ga - clamp(ga, -hw, hw)), vmul(cam.sf, gb - clamp(gb, -hh, hh)));
  }
  cam.c = onPlane(vadd(cam.c, vmul(pan, a)));
  if (view === 'steady') {
    let nf = vsub(cam.sf, vmul(U, vdot(cam.sf, U))); if (vlen(nf) < 0.2) nf = SW.board.f;   // past 78 deg in one frame: take the heading
    const fr = frameFrom(nf, U); cam.sf = fr.f; cam.sr = fr.r;
  } else if (view === 'north') {
    let nf = vsub(v3(0, 0, 1), vmul(U, U.z)); if (vlen(nf) < 0.2) nf = vsub(v3(1, 0, 0), vmul(U, U.x));
    const fr = frameFrom(nf, U); cam.sf = fr.f; cam.sr = fr.r;
  } else { cam.sf = SW.board.f; cam.sr = SW.board.r; }
}
// world -> screen (device px); h is the height above the plane (the Wrasse's depth from the plane)
function proj(x, y, z) {
  const d = v3(x - cam.c.x, y - cam.c.y, z - cam.c.z);
  return { x: cam.cx + vdot(d, cam.sr) * cam.k, y: cam.cy - vdot(d, cam.sf) * cam.k, s: cam.k, h: height({ x, y, z }) };
}
// screen point -> the plane (where the dogs live)
function toPlane(clientX, clientY) {
  const r = sc.getBoundingClientRect(), px = (clientX - r.left) * cam.kpx, py = (clientY - r.top) * cam.kpx;
  const a = (px - cam.cx) / cam.k, b = -(py - cam.cy) / cam.k;
  return vadd(cam.c, vadd(vmul(cam.sr, a), vmul(cam.sf, b)));
}

// ---- input: thumbs, mouse, keys, gamepad -> ONE input object ----
const shIn = { pilot: { yaw: 0, pitch: 0, roll: 0, thr: 0.5, call: false, tele: false, decoy: false }, dogs: [{ down: false, x: 0, y: 0, z: 0 }, { down: false, x: 0, y: 0, z: 0 }] };
const touches = new Map();      // pointerId -> { kind: 'dog'|'stick', dog, side, x0, y0, x, y }
const keys = new Set();
function humanDogs() { return shepState.role === 'dogs' || shepState.role === 'both'; }
function humanPilot() { return shepState.role === 'pilot' || shepState.role === 'both'; }
const sticksDown = () => [...touches.values()].filter((t) => t.kind === 'stick').length;
sc.addEventListener('pointerdown', (ev) => {
  if (!SW) return;
  if (PLATFORM.device === 'phone' && document.fullscreenElement == null && shepRoot.requestFullscreen) shepRoot.requestFullscreen().catch(() => {});
  try { sc.setPointerCapture(ev.pointerId); } catch (e) { /* older engines */ }
  const r = sc.getBoundingClientRect();
  // the pilot's two thumbs: the left half is the left stick, the right half the right stick
  if (shepState.role === 'pilot' && ev.pointerType !== 'mouse') {
    const side = (ev.clientX - r.left) < r.width * 0.5 ? 'L' : 'R';
    if ([...touches.values()].some((t) => t.kind === 'stick' && t.side === side)) return;
    touches.set(ev.pointerId, { kind: 'stick', side, x0: ev.clientX, y0: ev.clientY, x: ev.clientX, y: ev.clientY });
    return;
  }
  if (!humanDogs()) return;
  // claim the nearest dog that no thumb holds (by screen distance)
  let best = -1, bd = 1e9;
  for (let k = 0; k < 2; k++) {
    if ([...touches.values()].some((t) => t.kind === 'dog' && t.dog === k)) continue;
    const d = SW.dogs[k], q = proj(d.x, d.y, d.z);
    const dd = Math.hypot(q.x / cam.kpx + r.left - ev.clientX, q.y / cam.kpx + r.top - ev.clientY); if (dd < bd) { bd = dd; best = k; }
  }
  if (best < 0) return;
  touches.set(ev.pointerId, { kind: 'dog', dog: best, x: ev.clientX, y: ev.clientY });
  aimDog(best, ev.clientX, ev.clientY);
});
sc.addEventListener('pointermove', (ev) => {
  const t = touches.get(ev.pointerId); if (!t) return;
  t.x = ev.clientX; t.y = ev.clientY;
  if (t.kind === 'dog') aimDog(t.dog, ev.clientX, ev.clientY);
});
// a held thumb stays on its screen point while the view turns, so the dog's world target is re-read every frame
function aimDog(k, x, y) { const p = toPlane(x, y); shIn.dogs[k].down = true; shIn.dogs[k].x = p.x; shIn.dogs[k].y = p.y; shIn.dogs[k].z = p.z; }
function reaimDogs() { for (const t of touches.values()) if (t.kind === 'dog') aimDog(t.dog, t.x, t.y); }
let hadBoth = false;
function endTouch(ev) {
  const t = touches.get(ev.pointerId); if (!t) return;
  touches.delete(ev.pointerId);
  if (t.kind === 'dog') shIn.dogs[t.dog].down = false;          // the lift: the step drops the decoy
  if (t.kind === 'stick' && sticksDown() === 0 && hadBoth) { teleLatch = 2; hadBoth = false; }   // both thumbs lifted: teleport (the Grouper's trigger)
}
let teleLatch = 0, callLatch = 0, decoyLatch = 0;
sc.addEventListener('pointerup', endTouch); sc.addEventListener('pointercancel', endTouch);
$('shepCall').addEventListener('pointerdown', (ev) => { ev.preventDefault(); callLatch = 3; });
$('shepDecoy').addEventListener('pointerdown', (ev) => { ev.preventDefault(); decoyLatch = 3; });
addEventListener('keydown', (ev) => {
  if (!shepOn) return;
  if (ev.target && /INPUT|TEXTAREA|SELECT/.test(ev.target.tagName)) return;
  const k = ev.key.toLowerCase(); keys.add(k);
  if (k === ' ' || k.startsWith('arrow')) ev.preventDefault();
  if (k === 'escape') { if (shepState.sheet) toggleSheet(false); else shepClose(); }
  if (k === 'p') shepPause();
  if (k === 'r') shepReset();
});
addEventListener('keyup', (ev) => keys.delete(ev.key.toLowerCase()));
addEventListener('blur', () => keys.clear());
function readPad() {
  try { const pads = navigator.getGamepads ? navigator.getGamepads() : []; for (const g of pads) if (g && g.connected) return g; } catch (e) { /* the viewer may block the Gamepad API */ }
  return null;
}
// two sticks -> the Grouper's four axes (DualStickMix: XSum yaw, YSum pitch, YDiff roll, XDiff throttle; spread = full)
function mixSticks(lx, ly, rx, ry) { return { yaw: clamp(lx + rx, -1, 1), pitch: clamp(ly + ry, -1, 1), roll: clamp(ry - ly, -1, 1), thr: clamp((rx - lx + 2) / 4, 0, 1) }; }
function gatherHuman() {
  // keys: the summed axes directly (stick down = nose up, so S lifts the nose)
  let m = { yaw: 0, pitch: 0, roll: 0, thr: keys.has('shift') ? 1 : 0.5 }, call = false, tele = false, decoy = false;
  if (keys.has('a') || keys.has('arrowleft')) m.yaw -= 1; if (keys.has('d') || keys.has('arrowright')) m.yaw += 1;
  if (keys.has('s') || keys.has('arrowdown')) m.pitch += 1; if (keys.has('w') || keys.has('arrowup')) m.pitch -= 1;
  if (keys.has('q')) m.roll -= 1; if (keys.has('e')) m.roll += 1;
  if (keys.has('c')) call = true; if (keys.has(' ')) tele = true; if (keys.has('f')) decoy = true;
  const g = readPad();
  if (g && shepState.role === 'dogs') gatherPadDogs(g);
  else if (g) {
    const dz = (v) => Math.abs(v || 0) > 0.12 ? v : 0, lx = dz(g.axes[0]), ly = dz(g.axes[1]), rx = dz(g.axes[2]), ry = dz(g.axes[3]);
    if (lx || ly || rx || ry) m = mixSticks(lx, ly, rx, ry);
    if (g.buttons[0] && g.buttons[0].pressed) call = true;
    if (g.buttons[2] && g.buttons[2].pressed) decoy = true;
    if (g.buttons[7] && g.buttons[7].value > 0.6) tele = true;
  }
  const st = [...touches.values()].filter((t) => t.kind === 'stick');
  if (st.length) {
    const L = st.find((t) => t.side === 'L'), R = st.find((t) => t.side === 'R');
    const ax = (t, k) => t ? clamp((t[k] - t[k + '0']) / 60, -1, 1) : 0;
    m = mixSticks(ax(L, 'x'), ax(L, 'y'), ax(R, 'x'), ax(R, 'y'));
    if (st.length === 2) hadBoth = true;
  }
  if (callLatch > 0) { call = true; callLatch--; }
  if (teleLatch > 0) { tele = true; teleLatch--; }
  if (decoyLatch > 0) { decoy = true; decoyLatch--; }
  Object.assign(shIn.pilot, m, { call, tele, decoy });
}
// ---- gamepad jacks (round 4): left stick the left jack, right stick the right; a bumper or trigger is the lift ----
// A stick runs its jack across the screen (stick up = screen up) at padDogSpeed x dogSpeed; at rest the jack holds.
// When both sticks rest, the left stick takes whichever jack is further left on screen. The jack stays on screen.
const padDog = { map: [0, 1], was: [false, false] };
const touchHolds = (k) => [...touches.values()].some((t) => t.kind === 'dog' && t.dog === k);
function gatherPadDogs(g) {
  const dz = (v) => { const a = Math.abs(v || 0); return a < 0.15 ? 0 : Math.sign(v) * (a - 0.15) / 0.85; };
  const st = [[dz(g.axes[0]), dz(g.axes[1])], [dz(g.axes[2]), dz(g.axes[3])]];
  if (!st[0][0] && !st[0][1] && !st[1][0] && !st[1][1]) {
    const qa = proj(SW.dogs[0].x, SW.dogs[0].y, SW.dogs[0].z), qb = proj(SW.dogs[1].x, SW.dogs[1].y, SW.dogs[1].z);
    padDog.map = qa.x <= qb.x ? [0, 1] : [1, 0];
  }
  for (let s = 0; s < 2; s++) {
    const k = padDog.map[s], d = SW.dogs[k], T = shIn.dogs[k];
    if (touchHolds(k) || d.off) continue;
    const bump = g.buttons[4 + s], trig = g.buttons[6 + s];
    const lift = !!((bump && bump.pressed) || (trig && trig.value > 0.5)), edge = lift && !padDog.was[s]; padDog.was[s] = lift;
    if (edge) { T.down = false; continue; }     // one step up: the step drops the decoy where the jack stands
    const [x, y] = st[s], m = Math.min(1, Math.hypot(x, y));
    let a = vdot(vsub(d, cam.c), cam.sr), b = vdot(vsub(d, cam.c), cam.sf);
    if (m > 0) {
      const step = SP.dogSpeed * SP.padDogSpeed * Math.pow(m, 1.4) / 60 / Math.hypot(x, y), mx = 24 * cam.kpx / cam.k;
      a = clamp(a + x * step, -(cam.cx / cam.k - mx), cam.cx / cam.k - mx);
      b = clamp(b - y * step, -((sc.height - cam.cy) / cam.k - mx), cam.cy / cam.k - mx);
    }
    const p = vadd(cam.c, vadd(vmul(cam.sr, a), vmul(cam.sf, b)));
    T.down = true; T.x = p.x; T.y = p.y; T.z = p.z;
  }
}
let shTestPilot = null;   // the test surface's pilot input (window.__lab.shep.input), standing in for the devices
function liveInputFor() {
  const role = shepState.role, ai = shAI(role === 'ai' ? 'all' : role === 'dogs' ? 'pilot' : role === 'pilot' ? 'dogs' : 'none');
  gatherHuman();
  return { pilot: humanPilot() ? (shTestPilot || shIn.pilot) : ai.pilot, dogs: humanDogs() ? shIn.dogs : ai.dogs };
}

// ---- drawing ----
const SHC = { wool: '#e9efe8', woolDim: '#9fb0ab', danger: '#ff5a3c', dog: '#f2c84b', pilot: '#1fb8b0', decoy: '#f0a043', ruby: '#c2368e', pasture: '#7fa86a', jade: '#2fd6c8', grid: 'rgba(120,170,160,.10)', ring: 'rgba(242,200,75,.55)' };
// a circle ON the plane
function planeRing(c, r, col, lw, dash) {
  sg.beginPath();
  for (let i = 0; i <= 48; i++) { const a = i / 48 * TAU, q = proj(c.x + (cam.sr.x * Math.cos(a) + cam.sf.x * Math.sin(a)) * r, c.y + (cam.sr.y * Math.cos(a) + cam.sf.y * Math.sin(a)) * r, c.z + (cam.sr.z * Math.cos(a) + cam.sf.z * Math.sin(a)) * r); if (i) sg.lineTo(q.x, q.y); else sg.moveTo(q.x, q.y); }
  sg.strokeStyle = col; sg.lineWidth = lw * cam.kpx; sg.setLineDash(dash ? dash.map((v) => v * cam.kpx) : []); sg.stroke(); sg.setLineDash([]);
}
// how a thing off the plane reads: the Wrasse's depth from the plane, as fading (never hidden)
const depthAlpha = (h) => 1 - Math.min(0.72, Math.abs(h) / 500);
function shepRender() {
  const w = SW; if (!w) return;
  shepResize();
  sg.fillStyle = '#061014'; sg.fillRect(0, 0, sc.width, sc.height);
  const U = w.board.u, kp = cam.kpx;
  // the plane: a grid fixed to the WORLD (so turning reads), through the world origin's foot on the plane
  {
    let gx = vsub(v3(1, 0, 0), vmul(U, U.x)); if (vlen(gx) < 0.2) gx = vsub(v3(0, 0, 1), vmul(U, U.z));
    const gf = frameFrom(gx, U), o = onPlane(v3(0, 0, 0)), span = SP.viewSpan * 1.6, step = 250;
    sg.lineWidth = 1 * kp; sg.strokeStyle = SHC.grid; sg.beginPath();
    // offsets of grid lines near the camera, along each axis
    const ca = vdot(vsub(cam.c, o), gf.f), cb = vdot(vsub(cam.c, o), gf.r);
    for (let i = Math.floor((ca - span) / step); i <= Math.ceil((ca + span) / step); i++) { const base = vadd(o, vmul(gf.f, i * step)), p0 = vadd(base, vmul(gf.r, cb - span)), p1 = vadd(base, vmul(gf.r, cb + span)), q0 = proj(p0.x, p0.y, p0.z), q1 = proj(p1.x, p1.y, p1.z); sg.moveTo(q0.x, q0.y); sg.lineTo(q1.x, q1.y); }
    for (let i = Math.floor((cb - span) / step); i <= Math.ceil((cb + span) / step); i++) { const base = vadd(o, vmul(gf.r, i * step)), p0 = vadd(base, vmul(gf.f, ca - span)), p1 = vadd(base, vmul(gf.f, ca + span)), q0 = proj(p0.x, p0.y, p0.z), q1 = proj(p1.x, p1.y, p1.z); sg.moveTo(q0.x, q0.y); sg.lineTo(q1.x, q1.y); }
    sg.stroke();
    // the cell wall where the plane cuts the sphere
    const d = height(v3(0, 0, 0)); if (Math.abs(d) < SH_R) planeRing(o, Math.sqrt(SH_R * SH_R - d * d), 'rgba(194,54,142,.35)', 2, [10, 8]);
  }
  // pens: spheres. Faint dashes = the sphere's outline; solid = where the plane cuts it (the flock can stand there)
  w.pens.forEach((p, i) => {
    const cur = i === w.penNext, done = i < w.penNext, h = height(p), c = onPlane(p);
    const col = done ? 'rgba(47,214,200,.35)' : cur ? 'rgba(159,247,198,.95)' : 'rgba(159,247,198,.3)';
    planeRing(c, p.r, done ? col : 'rgba(159,247,198,.22)', 1, [3, 7]);
    if (Math.abs(h) < p.r) planeRing(c, Math.sqrt(p.r * p.r - h * h), col, cur ? 3 : 1.5, done ? null : [6, 6]);
    const q = proj(c.x, c.y, c.z); sg.fillStyle = cur ? '#9ff7c6' : 'rgba(159,247,198,.5)'; sg.font = '600 ' + 13 * kp + 'px "Chakra Petch", system-ui'; sg.textAlign = 'center';
    sg.fillText(done ? '✓' : (i + 1) + (cur ? (Math.abs(h) > 40 ? (h > 0 ? ' ▲' : ' ▼') + Math.round(Math.abs(h)) : '') + (p.fill ? ' · ' + Math.round(p.fill * 100) + '%' : '') : ''), q.x, q.y + 4 * kp);
  });
  // everything with depth: low to high along the plane's normal
  const items = [];
  for (const q of w.prisms) items.push({ o: q, t: 'p' });
  for (const s of w.sheep) items.push({ o: s, t: 's' });
  for (const r of w.rivals) if (r.out <= 0) items.push({ o: r, t: 'r' });
  for (const it of items) it.q = proj(it.o.x, it.o.y, it.o.z);
  items.sort((a, b) => a.q.h - b.q.h);
  const R = SEL.ring();
  for (const it of items) {
    const q = it.q;
    if (q.x < -50 || q.y < -50 || q.x > sc.width + 50 || q.y > sc.height + 50) continue;
    const o = it.o; sg.globalAlpha = depthAlpha(q.h);
    if (it.t === 'p') {
      const s = Math.max(1.5 * kp, 9 * q.s * (1 + clamp(q.h / 1500, -0.4, 0.4)));
      sg.fillStyle = o.kind === 'pasture' ? SHC.pasture : o.kind === 'jade' ? SHC.jade : SHC.ruby;
      sg.fillRect(q.x - s / 2, q.y - s / 2, s, s);
      if (o.sh) { sg.strokeStyle = '#f4f1ff'; sg.lineWidth = 1.2 * kp; sg.strokeRect(q.x - s / 2 - 1.5 * kp, q.y - s / 2 - 1.5 * kp, s + 3 * kp, s + 3 * kp); }
    } else if (it.t === 's') {
      const r = Math.max(2.5 * kp, 13 * q.s * (1 + clamp(q.h / 1500, -0.4, 0.4))) * Math.min(1, 0.15 + o.age / 0.6);   // a birth blooms in
      sg.fillStyle = o.panic > 0.3 ? SHC.woolDim : SHC.wool; sg.beginPath(); sg.arc(q.x, q.y, r, 0, TAU); sg.fill();
      if (isDanger(o)) { sg.strokeStyle = SHC.danger; sg.lineWidth = Math.max(1.2 * kp, r * 0.38); sg.stroke(); }
      if (o.hold > 0) { sg.strokeStyle = SHC.pasture; sg.lineWidth = 1.5 * kp; sg.beginPath(); sg.arc(q.x, q.y, r + 3 * kp, -Math.PI / 2, -Math.PI / 2 + TAU * (1 - o.hold / SP.consumeHold)); sg.stroke(); }
      const hd = Math.hypot(o.vx, o.vy, o.vz) || 1, hq = proj(o.x + o.vx / hd * 10, o.y + o.vy / hd * 10, o.z + o.vz / hd * 10);
      sg.fillStyle = '#2b3433'; sg.beginPath(); sg.arc(q.x + (hq.x - q.x) * 0.9, q.y + (hq.y - q.y) * 0.9, r * 0.45, 0, TAU); sg.fill();
    } else {
      const r = Math.max(5 * kp, 20 * q.s), vm = Math.hypot(o.vx, o.vy, o.vz) || 1, hq = proj(o.x + o.vx / vm * 40, o.y + o.vy / vm * 40, o.z + o.vz / vm * 40), a = Math.atan2(hq.y - q.y, hq.x - q.x);
      sg.save(); sg.translate(q.x, q.y); sg.rotate(a); sg.fillStyle = o.slow > 0 ? '#7a2a58' : SHC.ruby; sg.beginPath(); sg.moveTo(r, 0); sg.lineTo(-r * 0.7, r * 0.6); sg.lineTo(-r * 0.4, 0); sg.lineTo(-r * 0.7, -r * 0.6); sg.closePath(); sg.fill(); sg.restore();
      sg.fillStyle = '#ffd0ec'; sg.font = 11 * kp + 'px "IBM Plex Mono", monospace'; sg.textAlign = 'center'; sg.fillText(o.petals + '✿', q.x, q.y - r - 4 * kp);
    }
  }
  sg.globalAlpha = 1;
  // the decoy (a pilot's decoy is drawn in the pilot's colour)
  if (w.decoy) { const d = w.decoy, q = proj(d.x, d.y, d.z), col = d.by === 'pilot' ? SHC.pilot : SHC.decoy; planeRing(d, R, d.by === 'pilot' ? 'rgba(31,184,176,.55)' : 'rgba(240,160,67,.55)', 1.5, [4, 6]); const s = 11 * kp; sg.strokeStyle = col; sg.lineWidth = 2.5 * kp; sg.beginPath(); sg.moveTo(q.x, q.y - s); sg.lineTo(q.x + s, q.y); sg.lineTo(q.x, q.y + s); sg.lineTo(q.x - s, q.y); sg.closePath(); sg.stroke(); }
  // dogs: the ring is the floor-respecting repulsion radius; the thumb disc is what a big thumb hides
  for (const d of w.dogs) {
    if (d.off) continue;
    const held = touchHolds(d.id);
    planeRing(d, R, d.down ? SHC.ring : 'rgba(242,200,75,.3)', d.down ? 2.5 : 1.5, d.down ? null : [3, 5]);
    const q = proj(d.x, d.y, d.z);
    if (shepState.showThumb && humanDogs() && held) { const tr = (SP.thumbMm / 2) * 6.3 * kp; sg.fillStyle = 'rgba(255,255,255,.07)'; sg.beginPath(); sg.arc(q.x, q.y, tr, 0, TAU); sg.fill(); }
    sg.fillStyle = SHC.dog; sg.beginPath(); sg.arc(q.x, q.y, 9 * kp, 0, TAU); sg.fill();
    sg.fillStyle = '#1b1206'; sg.font = '700 ' + 11 * kp + 'px "Chakra Petch", system-ui'; sg.textAlign = 'center'; sg.fillText(d.id ? 'B' : 'A', q.x, q.y + 4 * kp);
    const t = humanDogs() ? shIn.dogs[d.id] : null;
    if (t && t.down) { const tq = proj(t.x, t.y, t.z); sg.strokeStyle = 'rgba(242,200,75,.6)'; sg.lineWidth = 1 * kp; sg.beginPath(); sg.moveTo(tq.x - 6 * kp, tq.y); sg.lineTo(tq.x + 6 * kp, tq.y); sg.moveTo(tq.x, tq.y - 6 * kp); sg.lineTo(tq.x, tq.y + 6 * kp); sg.stroke(); }
  }
  // the pilot, its ring, its call
  const p = w.pilot;
  if (!p.off) {
    if (p.callT > 0) planeRing(p, R * (0.6 + 0.4 * ((w.t * 2) % 1)), 'rgba(159,247,198,.9)', 3, null);
    else planeRing(p, R, 'rgba(47,214,200,.5)', 2, [8, 6]);
    const q = proj(p.x, p.y, p.z), hq = proj(p.x + p.f.x * 40, p.y + p.f.y * 40, p.z + p.f.z * 40);
    const a = Math.atan2(hq.y - q.y, hq.x - q.x), r = 15 * kp;
    // the wings show the bank: the hull's right wing against the board's right
    const bank = clamp(vdot(p.u, w.board.r), -1, 1);
    sg.save(); sg.translate(q.x, q.y); sg.rotate(a); sg.fillStyle = p.slowT > 0 ? '#7fa8a6' : SHC.pilot; sg.beginPath(); sg.moveTo(r * 1.3, 0); sg.lineTo(-r, r * 0.75 * (1 - 0.6 * Math.max(0, -bank))); sg.lineTo(-r * 0.5, 0); sg.lineTo(-r, -r * 0.75 * (1 - 0.6 * Math.max(0, bank))); sg.closePath(); sg.fill();
    if (p.v > SP.rhinoCruise * 2) { sg.strokeStyle = 'rgba(47,214,200,.5)'; sg.lineWidth = 2 * kp; sg.beginPath(); sg.moveTo(-r, 0); sg.lineTo(-r - Math.min(80, p.v / 10) * kp, 0); sg.stroke(); }
    sg.restore();
    if (q.x < 0 || q.y < 0 || q.x > sc.width || q.y > sc.height) edgeArrow(q.x, q.y, SHC.pilot);
  }
  // effects
  for (const f of w.fx) {
    const q = proj(f.x, f.y, f.z), k = f.t / 1.2;
    sg.globalAlpha = 1 - k;
    if (f.k === 'wither') { const r = Math.max(2.5 * kp, 13 * cam.k) * (1 - k); sg.fillStyle = SHC.woolDim; sg.beginPath(); sg.arc(q.x, q.y, r, 0, TAU); sg.fill(); if (f.danger) { sg.strokeStyle = SHC.danger; sg.lineWidth = Math.max(1, r * 0.38); sg.stroke(); } }   // starved: withers out
    else if (f.k === 'tele' && f.x2 != null) { const q2 = proj(f.x2, f.y2, f.z2); sg.strokeStyle = SHC.pilot; sg.lineWidth = 2 * kp; sg.setLineDash([3 * kp, 5 * kp]); sg.beginPath(); sg.moveTo(q.x, q.y); sg.lineTo(q2.x, q2.y); sg.stroke(); sg.setLineDash([]); }
    else { sg.strokeStyle = { sting: SHC.danger, eaten: '#ff9aa6', birth: '#ffffff', shed: '#f4f1ff', poo: SHC.jade, decoy: SHC.decoy, call: '#9ff7c6', pen: '#9ff7c6', hit: '#ff5a3c', ko: '#ffd0ec' }[f.k] || '#fff'; sg.lineWidth = 2 * kp; sg.beginPath(); sg.arc(q.x, q.y, (6 + 40 * k) * kp * (f.k === 'pen' || f.k === 'call' ? 3 : 1), 0, TAU); sg.stroke(); }
    sg.globalAlpha = 1;
  }
  // the touch sticks
  for (const t of touches.values()) if (t.kind === 'stick') { const r = sc.getBoundingClientRect(), x0 = (t.x0 - r.left) * kp, y0 = (t.y0 - r.top) * kp; sg.strokeStyle = 'rgba(47,214,200,.6)'; sg.lineWidth = 2 * kp; sg.beginPath(); sg.arc(x0, y0, 60 * kp, 0, TAU); sg.stroke(); sg.fillStyle = 'rgba(47,214,200,.5)'; sg.beginPath(); sg.arc((t.x - r.left) * kp, (t.y - r.top) * kp, 22 * kp, 0, TAU); sg.fill(); }
  drawAttitude();
}
function toBcam(o) { return { a: vdot(vsub(o, cam.c), cam.sr), b: vdot(vsub(o, cam.c), cam.sf) }; }
// the plane's attitude, lower right: the world's up seen from above the plane (a dot that slides as the pilot rolls)
function drawAttitude() {
  const kp = cam.kpx, R0 = 28 * kp, cx = sc.width - R0 - 18 * kp, cy = sc.height - R0 - 120 * kp;
  const U = SW.board.u, ux = vdot(UPW, cam.sr), uy = vdot(UPW, cam.sf), tilt = Math.acos(clamp(vdot(U, UPW), -1, 1)) * 180 / Math.PI;
  sg.strokeStyle = 'rgba(214,228,224,.35)'; sg.lineWidth = 1.5 * kp; sg.beginPath(); sg.arc(cx, cy, R0, 0, TAU); sg.stroke();
  sg.fillStyle = vdot(U, UPW) >= 0 ? '#9ff7c6' : '#ff9aa6'; sg.beginPath(); sg.arc(cx + ux * R0, cy - uy * R0, 4 * kp, 0, TAU); sg.fill();
  sg.fillStyle = '#d6e4e0'; sg.font = 11 * kp + 'px "IBM Plex Mono", monospace'; sg.textAlign = 'center'; sg.fillText('plane ' + Math.round(tilt) + '°', cx, cy + R0 + 14 * kp);
}
function edgeArrow(x, y, col) {
  const cx = sc.width / 2, cy = sc.height / 2, a = Math.atan2(y - cy, x - cx), m = 30 * cam.kpx;
  const ex = clamp(cx + Math.cos(a) * 1e4, m, sc.width - m), ey = clamp(cy + Math.sin(a) * 1e4, m + 40 * cam.kpx, sc.height - m);
  sg.save(); sg.translate(ex, ey); sg.rotate(a); sg.fillStyle = col; sg.beginPath(); sg.moveTo(12 * cam.kpx, 0); sg.lineTo(-8 * cam.kpx, 8 * cam.kpx); sg.lineTo(-8 * cam.kpx, -8 * cam.kpx); sg.closePath(); sg.fill(); sg.restore();
}
let shHudT = 0;
function shepHud() {
  const w = SW, p = w.pilot, s = w.stats, g = SHEP_GAMES.find((x) => x.id === w.game);
  const badges = [['Time', 'elTime'], ['Space', 'elSpace'], ['Mass', 'elMass'], ['Charge', 'elCharge']].filter((e) => L5k(e[1])).map((e) => e[0] + ' L5').join(' · ');
  const extra = w.game === 'time' ? 'pens ' + w.penNext + '/5' : w.game === 'space' ? 'enemy eaten ' + s.enemyEaten : w.game === 'mass' ? 'births ' + s.born + ' · your mass ' + w.prisms.filter((q) => q.kind === 'jade').length : 'petals +' + s.petalsTaken + ' -' + s.petalsLost + ' · KO ' + s.ko;
  $('shepHud').innerHTML = '<span class="big">' + shepScore() + '</span>  ' + g.name + '  ·  ' + Math.max(0, Math.ceil(SP.round - w.t)) + ' s\n' +
    extra + '\nherd ' + w.sheep.length + (s.starved ? ' (starved ' + s.starved + ')' : '') + ' · danger ' + Math.round(SEL.danger() * 100) + '% · ring ' + Math.round(SEL.ring()) + ' u\n' +
    PILOT_NAME + ' ' + Math.round(p.v) + ' u/s · ' + p.petals + '✿ · call ' + (p.callT > 0 ? 'ON' : p.callCd > 0 ? Math.ceil(p.callCd) + ' s' : 'ready') + ' · decoy ' + (w.decoy ? (w.decoy.by === 'pilot' ? 'pilot\'s' : 'set') : 'none') +
    (w.deb < -0.05 ? '\ndanger hit: elements ' + w.deb.toFixed(1) + (p.slowT > 0 ? ' · slowed' : '') : '') + (badges ? '\n' + badges : '');
  const callBtn = $('shepCall'); callBtn.disabled = p.callCd > 0; callBtn.textContent = p.callCd > 0 ? Math.ceil(p.callCd) + ' s' : 'CALL';
  const dBtn = $('shepDecoy'); dBtn.hidden = !L5k('elTime'); dBtn.disabled = p.decoyCd > 0; dBtn.textContent = p.decoyCd > 0 ? Math.ceil(p.decoyCd) + ' s' : 'DECOY';
  $('shepPads').hidden = !(shepState.role === 'pilot' || (shepState.role === 'both' && PLATFORM.device === 'phone'));
}

let shToastT = 0;
function shToast(msg) { const t = $('shepToast'); t.textContent = msg; t.classList.add('show'); clearTimeout(shToastT); shToastT = setTimeout(() => t.classList.remove('show'), 3800); }

// ---- loop ----
let shLast = performance.now(), shAcc = 0;
function shepFrame(now) {
  if (!shepOn) return;
  const dt = Math.min(0.05, Math.max(0, (now - shLast) / 1000)); shLast = now;
  if (!shepState.manual) {
    shepResize();
    if (!shepState.paused && !SW.done) {
      shAcc += dt;
      while (shAcc >= 1 / 60) { shAcc -= 1 / 60; reaimDogs(); shepStep(1 / 60, liveInputFor()); }
      if (SW.done) shepEndCard();
    }
    camUpdate(dt); shepRender();
    shHudT -= dt; if (shHudT <= 0) { shHudT = 0.2; shepHud(); }
  }
  requestAnimationFrame(shepFrame);
}
function shepReset(seed) {
  if (seed != null) shepState.seed = seed;
  shepWorld(shepState.seed, shepState.game);
  for (const d of shIn.dogs) d.down = false; touches.clear(); hadBoth = false;
  cam.c = v3(SW.pilot.x, SW.pilot.y, SW.pilot.z); cam.sf = SW.board.f; cam.sr = SW.board.r;
  padDog.was = [false, false];
  $('shepEnd').hidden = true;
  camUpdate(0); shepHud();
}
function shepPause() { shepState.paused = !shepState.paused; $('shepPause').textContent = shepState.paused ? 'Resume' : 'Pause'; }
function shepOpen() {
  shepOn = true; shepRoot.hidden = false; shepResize(); shepReset();
  shToast(SHEP_ROLES.find((r) => r.id === shepState.role).how);
  shLast = performance.now(); requestAnimationFrame(shepFrame);
}
function shepClose() { shepOn = false; shepRoot.hidden = true; toggleSheet(false); if (document.fullscreenElement && document.exitFullscreen) document.exitFullscreen().catch(() => {}); }
function shepEndCard() {
  const box = $('shepEnd'), w = SW;
  box.innerHTML = '';
  box.appendChild(el('h3', '', SHEP_GAMES.find((g) => g.id === w.game).name + ': ' + shepScore()));
  box.appendChild(el('div', '', 'Herd ' + w.sheep.length + ' (born ' + w.stats.born + ', lost ' + w.stats.lost + ', starved ' + w.stats.starved + ') · decoys ' + w.stats.decoys + ' (pilot ' + w.stats.pilotDecoys + ') · teleports ' + w.stats.teleports + ' · calls ' + w.stats.calls + ' · danger hits on the pilot ' + w.stats.selfHits));
  const row = el('div', 'row2');
  const b1 = el('button', 'sbtn', 'Play again'); b1.onclick = () => shepReset(shepState.seed + 1);
  const b2 = el('button', 'sbtn', 'Keep this direction'); b2.onclick = async () => { const where = await shepRecord('keep', ''); shToast(where === 'shared' ? 'Recorded in the shared log.' : 'Recorded in this browser.'); };
  const b3 = el('button', 'sbtn', 'Settings + notes'); b3.onclick = () => toggleSheet(true);
  row.append(b1, b2, b3); box.appendChild(row); box.hidden = false;
}
function shepDiff() { const o = {}; for (const k of Object.keys(SHEP_SHIPPED)) if (Math.abs(SP[k] - SHEP_SHIPPED[k]) > 1e-9) o[k] = SP[k]; return o; }
async function shepRecord(choice, note) {
  const settings = { round: 4, game: SW ? SW.game : shepState.game, role: shepState.role, view: shepState.view, seed: shepState.seed, changed: shepDiff(), score: SW ? shepScore() : null };
  return record('shepherd:' + settings.game, choice, (note ? note + ' ' : '') + JSON.stringify(settings));
}

// ---- the settings sheet ----
function toggleSheet(open) {
  shepState.sheet = open == null ? !shepState.sheet : open;
  $('shepSheet').hidden = !shepState.sheet; $('shepMore').setAttribute('aria-expanded', String(shepState.sheet));
  if (shepState.sheet) buildSheet();
}
function buildSheet() {
  const box = $('shepSheet'); box.replaceChildren();
  const head = el('div', 'head'); head.appendChild(el('h2', '', SHEP_NAME + ' settings')); const x = el('button', 'btn', 'Close'); x.onclick = () => toggleSheet(false); head.appendChild(x); box.appendChild(head);
  const g = SHEP_GAMES.find((v) => v.id === shepState.game), r = SHEP_ROLES.find((v) => v.id === shepState.role);
  box.appendChild(el('p', 'hint', g.how)); box.appendChild(el('p', 'hint', r.how));
  const nd = Object.keys(shepDiff()).length; box.appendChild(el('p', nd ? 'status-warn' : 'hint', nd ? nd + ' setting(s) differ from shipped / round-3 defaults (underlined).' : 'All settings at their shipped / round-3 values.'));
  let grp = '';
  for (const [group, key, lo, hi, step, label, tip] of SHEP_SPEC) {
    if (group !== grp) { grp = group; box.appendChild(el('h2', 'sec', group)); }
    const row = el('div', 'row'), lab = el('label'), out = el('output'), inp = document.createElement('input');
    lab.appendChild(el('span', '', label)); if (group === 'Elements' && SP[key] >= 5) lab.appendChild(el('span', 'el5', 'L5'));
    inp.type = 'range'; inp.min = lo; inp.max = hi; inp.step = step; inp.value = SP[key]; inp.id = 'shep-' + key; inp.setAttribute('aria-label', label);
    const show = () => { out.textContent = key === 'frame' ? FRAME_NAMES[Math.round(SP[key])] : fmt(SP[key], step); out.classList.toggle('changed', Math.abs(SP[key] - SHEP_SHIPPED[key]) > 1e-9); out.title = 'shipped ' + SHEP_SHIPPED[key] + ' · yours ' + SP[key]; };
    inp.oninput = () => { SP[key] = +inp.value; lsSet(SHEP_LS, shepDiff()); show(); if (key === 'sheep' || key === 'intensity') shepReset(); if (group === 'Elements') { lab.querySelector('.el5') && lab.querySelector('.el5').remove(); if (SP[key] >= 5) lab.appendChild(el('span', 'el5', 'L5')); } };
    show(); row.append(lab, out, inp, el('div', 'tip', tip)); box.appendChild(row);
  }
  const tb = el('label', 'flex'); const cb = document.createElement('input'); cb.type = 'checkbox'; cb.id = 'shepThumb'; cb.checked = shepState.showThumb; cb.onchange = () => { shepState.showThumb = cb.checked; }; tb.append(cb, el('span', '', 'Show the big-thumb disc under a held dog'));
  box.appendChild(tb);
  const vb = el('label', 'flex'); const vc = document.createElement('select'); vc.id = 'shepView'; vc.setAttribute('aria-label', 'View');
  for (const [id, name] of Object.entries(VIEW_NAMES)) { const o = el('option', '', name); o.value = id; vc.appendChild(o); }
  vc.value = shepState.view; vc.onchange = () => { shepState.view = vc.value; lsSet('lab:' + LAB.name + ':shepView4', shepState.view); }; vb.append(el('span', '', 'View '), vc);
  box.appendChild(vb);
  const rs = el('button', 'btn', 'Back to shipped / round-3 values'); rs.onclick = () => { Object.assign(SP, SHEP_SHIPPED); lsSet(SHEP_LS, {}); buildSheet(); shepReset(); }; box.appendChild(el('p')).appendChild(rs);
  // scorecard
  box.appendChild(el('h2', 'sec', 'Scorecard (AI crews, 30 s, seeds 1-2)'));
  box.appendChild(el('p', 'hint', 'Each row plays all four games with the AI pilot and AI dogs. Pilot only and Dogs only are the baselines; Level plane is round 2\'s flat plane, so the gap to the crew row is what rolling the plane is worth. Your current settings are the starting point for every row.'));
  const run = el('button', 'btn primary', 'Run scorecard'); const tw = el('div', 'tblWrap');
  run.onclick = () => {
    run.disabled = true; shepState.batch = []; let i = 0;
    (function next() {   // one row per task, so the page stays alive on a phone
      if (i >= SHEP_ROWS.length) { run.disabled = false; run.textContent = 'Run again'; return; }
      run.textContent = 'Running ' + (i + 1) + '/' + SHEP_ROWS.length + '...';
      shepState.batch.push(shepBatch({ seeds: [1, 2], rows: [SHEP_ROWS[i++]] })[0]); drawBatch(tw); setTimeout(next, 0);
    })();
  };
  box.appendChild(run); box.appendChild(tw); if (shepState.batch) drawBatch(tw);
  // decisions
  box.appendChild(el('h2', 'sec', 'Your call'));
  const ta = document.createElement('textarea'); ta.id = 'shepNote'; ta.placeholder = 'What felt right or wrong? (saved with the game, role and settings)'; box.appendChild(ta);
  const row = el('div', 'flex');
  for (const [c, lbl] of [['keep', 'Keep'], ['change', 'Keep, with changes'], ['cut', 'Cut']]) { const b = el('button', 'btn', lbl); b.onclick = async () => { const where = await shepRecord(c, ta.value); ta.value = ''; shToast(where === 'shared' ? 'Recorded in the shared log.' : 'Recorded in this browser.'); }; row.appendChild(b); }
  box.appendChild(row);
  box.appendChild(el('p', 'hint', db ? 'Shared log: Claude reads it next round.' : 'Kept in this browser until the shared log connects.'));
  // not modelled
  box.appendChild(el('h2', 'sec', 'What this does NOT model'));
  const ul = el('ul');
  for (const t of [
    'One screen for both crews. In the game the pilot and the dog player have their own screens; here the dog view (the Wrasse\'s top-down kite view) is shown for both.',
    'The thumb floor uses a nominal phone (6.3 CSS px per mm, 390 px tall in landscape). Real phones differ by about 15%.',
    'The pilot flies the Grouper\'s two-stick axes and turn rates with the Rhino\'s speed and straight-line ramp, but not the Rhino\'s sword, shield swipe or trail. Time does not speed its ramp here (Time is the herd\'s).',
    'The danger hit\'s element debuff is modelled as levels off every element, decaying over 4 s (the lab\'s reading of the shipped -0.5 magnitude). It is real here: it lowers Charge, so it briefly thins the danger share too.',
    'Depth from the plane is drawn as fading and size, not the game\'s face spread and dither cone.',
    'Repulsion is a cylinder along the plane\'s normal round each herder, so it reads from above; creatures far off the plane still flee (in the game every vessel is already in a creature\'s separation, LightFauna\'s vessel scan).',
    'The herd is the cell\'s fauna (round 4), drawn as dots: no body prisms, no crystal heart dropped on death, no nucleus (so no spatial diet) and one colour, the player\'s, as if the player held the cell. A 180 cap of real LightFauna would cost body-prism colliders; the game\'s cells hold fewer.',
    'The herd does not eat your own mass (Mass L5\'s clusters); in the game a herbivore outside the nucleus grazes any domain\'s mass, so it would.',
    'Rivals are scripted wolves, not players. No networking, so no latency between the dog player and the pilot.',
    'The AI crew is a heuristic (Strombom-style drive and collect; the AI pilot rolls toward the plane through itself, the flock and the target). Treat a gap under ~10% as noise.',
  ]) ul.appendChild(el('li', 'hint', t));
  box.appendChild(ul);
}
function drawBatch(tw) {
  const t = el('table'), cols = SHEP_GAMES.map((g) => g.id), best = {};
  for (const c of cols) best[c] = Math.max(...shepState.batch.map((r) => r[c]));
  const hr = el('tr'); hr.appendChild(el('th', '', 'Build')); for (const g of SHEP_GAMES) hr.appendChild(el('th', '', g.el[0].toUpperCase() + g.el.slice(1))); t.appendChild(hr);
  for (const r of shepState.batch) { const tr = el('tr'); tr.appendChild(el('td', '', r.name)); for (const c of cols) { const td = el('td', r[c] === best[c] && r[c] > 0 ? 'best' : '', String(r[c])); tr.appendChild(td); } t.appendChild(tr); }
  tw.replaceChildren(t);
}

$('shepBack').onclick = shepClose;
$('shepRestart').onclick = () => shepReset();
$('shepPause').onclick = shepPause;
$('shepMore').onclick = () => toggleSheet();
$('shepGame').onchange = () => { shepState.game = $('shepGame').value; lsSet('lab:' + LAB.name + ':shepGame', shepState.game); shepReset(); if (shepState.sheet) buildSheet(); shToast(SHEP_GAMES.find((g) => g.id === shepState.game).how); };
$('shepRole').onchange = () => { shepState.role = $('shepRole').value; lsSet('lab:' + LAB.name + ':shepRole', shepState.role); for (const d of shIn.dogs) d.down = false; touches.clear(); if (shepState.sheet) buildSheet(); shToast(SHEP_ROLES.find((r) => r.id === shepState.role).how); };
addEventListener('resize', () => { if (shepOn) shepResize(); });

// the lab header gets the door in; #shepherd on the link opens straight into it
{
  const b = el('button', 'btn primary', SHEP_NAME); b.id = 'shepOpen'; b.title = 'Round 4: the marlin, two jacks and a herd of fauna (the Shepherd)';
  $('platform').before(b); b.onclick = shepOpen;
  if (location.hash === '#shepherd') setTimeout(shepOpen, 0);
}
shepWorld(1, shepState.game);

// the test surface
window.__lab.shep = {
  SHIPPED: SHEP_SHIPPED, SPEC: SHEP_SPEC, P: SP, GAMES: SHEP_GAMES, ROWS: SHEP_ROWS,
  open: shepOpen, close: shepClose,
  set manualClock(v) { shepState.manual = !!v; }, get manualClock() { return shepState.manual; },
  reset: (seed, game) => { if (game) { shepState.game = game; $('shepGame').value = game; } shepReset(seed); },
  tick(dt, n) { n = n || 1; for (let i = 0; i < n; i++) shepStep(dt || 1 / 60, liveInputFor()); camUpdate(0); shepRender(); },
  input: (o) => { if (o.pilot === null) shTestPilot = null; else if (o.pilot) shTestPilot = Object.assign(shTestPilot || { yaw: 0, pitch: 0, roll: 0, thr: 0.5 }, o.pilot); if (o.dogs) o.dogs.forEach((d, k) => Object.assign(shIn.dogs[k], d)); },
  toPlane, proj: (x, y, z) => proj(x, y, z), view: (v) => { if (v) shepState.view = v; return camView(); }, cam: () => ({ c: cam.c, sf: cam.sf, sr: cam.sr }), pad: (g) => { if (g) gatherPadDogs(g); return { map: padDog.map.slice(), dogs: shIn.dogs.map((d) => Object.assign({}, d)) }; },
  run: shepRun, runBatch: shepBatch, score: () => shepScore(),
  state: () => ({ t: SW.t, game: SW.game, sheep: SW.sheep.length, score: shepScore(), ring: SEL.ring(), floor: SEL.floorU(), danger: SEL.danger(), decoy: SW.decoy ? { x: SW.decoy.x, y: SW.decoy.y, z: SW.decoy.z, by: SW.decoy.by } : null, pilot: { x: SW.pilot.x, y: SW.pilot.y, z: SW.pilot.z, v: SW.pilot.v, callT: SW.pilot.callT, callCd: SW.pilot.callCd, decoyCd: SW.pilot.decoyCd, slowT: SW.pilot.slowT, up: SW.pilot.u }, board: { up: SW.board.u, tilt: Math.acos(clamp(SW.board.u.y, -1, 1)) * 180 / Math.PI }, deb: SW.deb, dogs: SW.dogs.map((d) => ({ x: d.x, y: d.y, z: d.z, down: d.down, h: height(d) })), stats: Object.assign({}, SW.stats), done: SW.done }),
};
