// Shiva's Cell - the model. Pure, seeded, no DOM: the page draws it, node scores it (check.js).
// A squad of three threads Shiva's ring circuit through three forms; each finished circuit cracks one shell of his
// danger-prism shield, he claps, the wave wipes the squad's prisms and rips the elements off anyone near, the cell
// collapses and regrows, and a new swarm comes out. The third shell broken is the win. A phase clock that runs out is
// the loss. Every constant names where it came from: a shipped asset/class field, or "lab" (a proposal, no game field).
(function (G) {
  'use strict';

  // ── hulls: Tools/Build/regatta_balance.read_constants() (prefab ts + ms = cruise, yaw deg/s, rts) and the top speeds
  // from .claude/skills/arenagame/SKILL.md §1's fleet table. Boost economies are modelled COARSELY (lab): `free` hulls
  // (the Manta's Soar, the Sparrow's indefinite boost, the Rhino's ramp, the Scarab's throttle) boost without a meter;
  // the rest drain one lab meter that skimming flora and picking crystals refill (the Squirrel's skim energy, the
  // Dolphin's charge, the Serpent's charges, the Urchin's slipstream, the Stoat's sling all stand in as this meter).
  const HULLS = {
    Squirrel: { cruise: 60, top: 300, turn: 120, rts: 0.0, accel: 240, free: false },
    Sparrow:  { cruise: 35, top: 135, turn: 80,  rts: 0.1, accel: 120, free: true },
    Rhino:    { cruise: 50, top: 1200, turn: 90, rts: 0.2, accel: 220, free: true, ramp: true },   // rhino_accel 220: the ramp holds only on a straight stick
    Manta:    { cruise: 180, top: 720, turn: 40, rts: 0.2, accel: 300, free: true, soarTurn: 0.5 },   // yaw 30 / pitch 50; Soar costs yaw
    Dolphin:  { cruise: 68, top: 347, turn: 110, rts: 0.0, accel: 240, free: false },
    Urchin:   { cruise: 65, top: 150, turn: 90,  rts: 0.0, accel: 160, free: false },   // 300 only on its own rail; no rails here (lab 150)
    Serpent:  { cruise: 60, top: 160, turn: 80,  rts: 0.4, accel: 160, free: false },
    Scarab:   { cruise: 216, top: 324, turn: 100, rts: 0.1, accel: 200, free: true },
    Stoat:    { cruise: 60, top: 360, turn: 48,  rts: 0.0, accel: 260, free: false },   // the sling is not modelled (lab 360)
  };
  const HULL_NAMES = Object.keys(HULLS);

  const SHIPPED = {
    // the cell
    cellRadius: 1600,        // lab: Shiva's cell (Tandava's old cell was 800 u; a humongous figure needs twice it)
    // the circuit (the Time games' switch rings, GrizzlyTimeCourse / HeadlongCircuit)
    mouth: 72,               // HeadlongCircuit mouth at intensity 2 (96 / 72 / 58 / 46)
    ringsP1: 10, ringsP2: 12, ringsP3: 12,   // lab
    ringOuter: 1350,         // lab: the first ring's radius from Shiva
    ringStepDeg: 42,         // lab: azimuth between rings (the spiral's pitch)
    ringRise: 240,           // lab: how far the spiral climbs and falls
    danceSpeed: 0.02,        // lab: Nataraja - how fast the whole circuit wheels round him (rad/s; 0.02 = 27 u/s at 1350 u)
    danceBob: 110,           // lab: Nataraja - how far each ring rises and falls (u)
    danceSway: 90,           // lab: Nataraja - how far each ring swings in and out (u)
    // the clock (the cosmic drum): one per phase
    clockP1: 110, clockP2: 180, clockP3: 170,   // lab (s): near the edge for a sloppy slow squad (check.js)
    // the shield: three danger-prism shells, outer to inner, one cracked per finished circuit
    shell1: 620, shell2: 520, shell3: 430,       // lab (u)
    // the clap
    waveSpeed: 1100,         // lab: u/s
    clapReach: 950,          // lab: inside this radius the wave rips every element off a vessel
    regrowSeconds: 3,        // lab: the new cell grows in this long after the wave
    // the third form's mouth: BlackHoleConfig.asset
    bhStrength: 30,          // lab: horizon = strength x horizonPerStrength (2) = 60 u
    bhPull: 1.0,             // BlackHoleConfig.vesselPullScale
    bhMaxPull: 90,           // BlackHoleConfig.maxVesselPullSpeed (u/s)
    // the swarms (counts per phase; lab)
    serpentsP1: 2, serpentsP2: 3, serpentsP3: 2,
    sharksP1: 3, sharksP2: 2, sharksP3: 3,
    piranhaP2: 16, piranhaP3: 12,
    clawfishP2: 3, clawfishP3: 4,
    serpentCruise: 60,       // TandavaDirectorSettings cruise (u/s)
    serpentLunge: 144,       // TandavaDirectorSettings: Lunging 2.4 x cruise
    lungeRange: 380,         // TandavaDirectorSettings: a pilot within 380 u
    lungeCooldown: 6,        // TandavaDirectorSettings: 6 s since the last
    sharkSpeed: 160,         // lab (MassSharkFauna's chase speed is not a named field)
    piranhaMin: 45, piranhaMax: 70,   // Astro League Piranha Fauna Config: MinSpeed / MaxSpeed
    clawfishSpeed: 55,       // lab
    // what the swarm does to a pilot (vessels have no health: Docs/Voyages/VOYAGES.md §2)
    stingSlow: 0.5, stingSeconds: 1.5,   // lab: a danger plate or the shield
    ramSlow: 0.6, ramSeconds: 1.0,       // lab: a shark
    nipSlow: 0.15, nipSeconds: 0.6,      // lab: each piranha
    bumpSlow: 0.3,                       // lab: ramming a serpent's shielded body (a shield's ram slow)
    latchSlow: 0.4,                      // lab: a clawfish holds on until you boost 1 s
    // the cell's contents
    crystals: 60,            // lab: elemental crystals afloat in the cell
    crystalRespawn: 8,       // lab (s)
    startLevel: 2,           // lab: every element starts at this level (0-10)
    trailInterval: 0.12,     // lab: a vessel lays a prism this often
    trailLife: 18,           // lab (s)
    aiNoise: 0.15,           // lab: 0 = a perfect AI, 1 = a sloppy one
  };

  const SPEC = [
    ['Circuit', 'mouth', 30, 120, 1, 'Ring mouth (u)', 'Radius a pilot must pass inside to thread a ring. Headlong: 96 / 72 / 58 / 46 by intensity.'],
    ['Circuit', 'ringsP1', 4, 20, 1, 'Rings, phase 1', 'How many rings the still Shiva\'s circuit has.'],
    ['Circuit', 'ringsP2', 4, 20, 1, 'Rings, phase 2', 'How many rings Nataraja\'s wheeling circuit has.'],
    ['Circuit', 'ringsP3', 4, 20, 1, 'Rings, phase 3', 'How many rings the Devouring Mouth\'s circuit has.'],
    ['Circuit', 'ringOuter', 900, 1500, 10, 'First ring radius (u)', 'Where each circuit starts; it spirals in to the shell.'],
    ['Circuit', 'ringStepDeg', 15, 90, 1, 'Ring spacing (deg)', 'How far round Shiva the next ring is. Wider = longer legs.'],
    ['Circuit', 'ringRise', 0, 500, 10, 'Spiral rise (u)', 'How far the circuit climbs and dives.'],
    ['Circuit', 'danceSpeed', 0, 0.2, 0.005, 'Nataraja wheel (rad/s)', 'How fast the whole circuit wheels round him in the second form.'],
    ['Circuit', 'danceBob', 0, 300, 5, 'Nataraja bob (u)', 'How far each ring rises and falls in the second form.'],
    ['Circuit', 'danceSway', 0, 300, 5, 'Nataraja sway (u)', 'How far each ring swings in and out in the second form.'],
    ['Clock', 'clockP1', 40, 400, 5, 'Drum, phase 1 (s)', 'The cosmic clock for the first form. Run out and the dance ends the cell.'],
    ['Clock', 'clockP2', 40, 400, 5, 'Drum, phase 2 (s)', 'The clock for Nataraja.'],
    ['Clock', 'clockP3', 40, 400, 5, 'Drum, phase 3 (s)', 'The clock for the Devouring Mouth.'],
    ['Shiva', 'shell1', 450, 900, 10, 'Outer shell (u)', 'The first danger-prism shell, cracked by the first circuit.'],
    ['Shiva', 'shell2', 440, 800, 10, 'Middle shell (u)', 'Cracked by the second circuit.'],
    ['Shiva', 'shell3', 420, 700, 10, 'Inner shell (u)', 'Cracked by the third: the win.'],
    ['Shiva', 'waveSpeed', 300, 3000, 50, 'Clap wave speed (u/s)', 'How fast the clap\'s wave sweeps the cell.'],
    ['Shiva', 'clapReach', 0, 1600, 10, 'Clap rips within (u)', 'A vessel the wave passes inside this radius loses every element. 0 = the clap never rips.'],
    ['Shiva', 'regrowSeconds', 0.5, 10, 0.5, 'Cell regrows in (s)', 'How long the new cell takes to grow after the wave.'],
    ['Shiva', 'bhStrength', 0, 80, 1, 'Mouth: black hole strength', 'Third form. Horizon = 2 u per strength (BlackHoleConfig.horizonPerStrength). 0 = no black hole.'],
    ['Shiva', 'bhPull', 0, 4, 0.05, 'Mouth: pull scale', 'BlackHoleConfig.vesselPullScale.'],
    ['Shiva', 'bhMaxPull', 0, 300, 5, 'Mouth: max pull (u/s)', 'BlackHoleConfig.maxVesselPullSpeed: the most pull a vessel carries.'],
    ['Swarm', 'serpentsP1', 0, 6, 1, 'Serpents, phase 1', 'Five-Headed Serpents released with the first form.'],
    ['Swarm', 'serpentsP2', 0, 6, 1, 'Serpents, phase 2', 'Seven-Headed Serpents with Nataraja.'],
    ['Swarm', 'serpentsP3', 0, 6, 1, 'Serpents, phase 3', 'Ten-Headed Serpents with the Mouth.'],
    ['Swarm', 'sharksP1', 0, 10, 1, 'Sharks, phase 1', 'Sharks ram a pilot: a hard slow.'],
    ['Swarm', 'sharksP2', 0, 10, 1, 'Sharks, phase 2', ''],
    ['Swarm', 'sharksP3', 0, 10, 1, 'Sharks, phase 3', ''],
    ['Swarm', 'piranhaP2', 0, 40, 1, 'Piranha, phase 2', 'A school that nips: each fish on you is a small slow, and they stack.'],
    ['Swarm', 'piranhaP3', 0, 40, 1, 'Piranha, phase 3', ''],
    ['Swarm', 'clawfishP2', 0, 10, 1, 'Clawfish, phase 2', 'A clawfish latches on and drags until you boost for a second.'],
    ['Swarm', 'clawfishP3', 0, 10, 1, 'Clawfish, phase 3', ''],
    ['Swarm', 'serpentLunge', 60, 300, 2, 'Serpent lunge (u/s)', 'TandavaDirectorSettings Lunging speed.'],
    ['Swarm', 'lungeRange', 100, 800, 10, 'Lunge range (u)', 'A pilot inside this draws a lunge.'],
    ['Swarm', 'sharkSpeed', 30, 250, 5, 'Shark speed (u/s)', ''],
    ['Hits', 'stingSlow', 0, 0.9, 0.05, 'Sting slow', 'A danger plate or the shield: the share of speed taken.'],
    ['Hits', 'stingSeconds', 0, 5, 0.1, 'Sting lasts (s)', ''],
    ['Hits', 'ramSlow', 0, 0.9, 0.05, 'Shark ram slow', ''],
    ['Hits', 'nipSlow', 0, 0.5, 0.01, 'Piranha nip slow (each)', ''],
    ['Hits', 'bumpSlow', 0, 0.9, 0.05, 'Serpent body bump slow', 'Flying through a serpent\'s shielded body cuts it but slows you.'],
    ['Hits', 'latchSlow', 0, 0.9, 0.05, 'Clawfish drag', ''],
    ['Cell', 'crystals', 0, 200, 5, 'Elemental crystals', 'Afloat in the cell. +1 level of their element each.'],
    ['Cell', 'startLevel', 0, 10, 1, 'Starting level', 'Every element on every hull at the start.'],
    ['Cell', 'aiNoise', 0, 1, 0.05, 'AI sloppiness', '0 = a perfect autopilot. Score with both a sharp and a sloppy squad.'],
  ];

  const PHASES = [
    { name: 'The Still Shiva', short: 'Still', serpent: 'many_headed_5', ruleKey: 'lattice' },
    { name: 'Nataraja', short: 'Nataraja', serpent: 'many_headed_7', ruleKey: 'coral' },
    { name: 'The Devouring Mouth', short: 'Mouth', serpent: 'many_headed_10', ruleKey: 'ash' },
  ];
  const ELEMENTS = ['Charge', 'Mass', 'Space', 'Time'];   // the plans' elem index order (TandavaFormLab EC/ENAME)

  // 3D outer-totalistic rules (26-neighbour) for the regrown cell: one look per form. Lab stand-ins for the game's
  // cellular-automaton flora (the gyroid colony frontiers); the counts are what the scorecard prices.
  const RULES = {
    lattice: { label: 'Crystal lattice', S: [0, 1, 2, 3, 4, 5, 6], B: [1, 3], fill: 0.0, seedR: 0.6, gens: 6 },
    coral:   { label: 'Coral', S: [5, 6, 7, 8], B: [6, 7, 9, 12], fill: 0.35, seedR: 6, gens: 6 },
    ash:     { label: 'Ash blooms', S: [9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26], B: [5, 6, 7, 12, 13, 15], fill: 0.12, seedR: 7, gens: 5 },
  };

  const UNIT = 2;                     // plan voxels -> world u (TandavaFormLab sim.js UNIT)
  const DT = 1 / 30;
  const CENTER = [0, 0, 0];

  function mulberry32(a) { return () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
  const V = {
    add: (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]], sub: (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]],
    mul: (a, s) => [a[0] * s, a[1] * s, a[2] * s], dot: (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2],
    cross: (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]],
    len: (a) => Math.hypot(a[0], a[1], a[2]), norm: (a) => { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; },
    lerp: (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t],
    d2: (a, b) => { const x = a[0] - b[0], y = a[1] - b[1], z = a[2] - b[2]; return x * x + y * y + z * z; },
  };
  const clamp = (x, a, b) => x < a ? a : x > b ? b : x;
  // rotate v about unit axis k by angle a (Rodrigues)
  function rot(v, k, a) {
    const c = Math.cos(a), s = Math.sin(a), d = V.dot(k, v), x = V.cross(k, v);
    return [v[0] * c + x[0] * s + k[0] * d * (1 - c), v[1] * c + x[1] * s + k[1] * d * (1 - c), v[2] * c + x[2] * s + k[2] * d * (1 - c)];
  }
  function orthoUp(fwd, up) { const r = V.norm(V.cross(up, fwd)); return V.norm(V.cross(fwd, r)); }

  // ── the serpent plans (the shipped Tandava plans, cut down by build.py) ──
  function buildBank(data) {
    const bank = {};
    for (const key of Object.keys(data)) {
      const d = data[key], n = d.n, frames = [];
      let rad = 0;
      for (let f = 0; f < d.frames; f++) {
        const src = d.pos[f], a = new Float32Array(n * 3);
        for (let i = 0; i < n * 3; i++) a[i] = src[i] * UNIT;
        for (let i = 0; i < n; i++) rad = Math.max(rad, Math.hypot(a[3 * i], a[3 * i + 1], a[3 * i + 2]));
        frames.push(a);
      }
      bank[key] = { key, n, frames, frameSteps: d.fs, tier: Uint8Array.from(d.tier), elem: Uint8Array.from(d.elem), radius: rad, mouth: (d.mouth || [0, 0, 0]).map((x) => x * UNIT), clip: d.clip || 1 };
    }
    return bank;
  }

  // ── Shiva's figure: joints per form (the page builds the prism figure from them; the sim needs the hands and mouth) ──
  // y up; the shield's centre is the origin; he faces +z. "Till the body": his waist sinks into the nebula at y = -320.
  function figure(phase, clapK, danceT, mouthOpen, yaw) {
    const sway = phase === 1 ? Math.sin(danceT * 1.3) : 0, sway2 = phase === 1 ? Math.sin(danceT * 2.6 + 0.7) : 0;
    const j = {
      waist: [0, -320, 0], chest: [sway * 26, 40, 0], neck: [sway * 34, 168, 6], head: [sway * 40, 250, 10],
      bun: [sway * 44 + sway2 * 8, 362, -14], shoulderL: [-150 + sway * 22, 118, 0], shoulderR: [150 + sway * 22, 118, 0],
    };
    if (phase === 0) {          // the still Shiva: back arms raised and open, front arms spread wide (the reference)
      Object.assign(j, { elbowUL: [-270, 232, -50], handUL: [-305, 340, -60], elbowUR: [270, 232, -50], handUR: [305, 340, -60],
        elbowLL: [-280, 10, 30], handLL: [-385, -80, 55], elbowLR: [280, 10, 30], handLR: [385, -80, 55] });
    } else if (phase === 1) {   // Nataraja: drum hand up right, the crescent hand up left, the blessing hand forward, the
      const s = sway;          // pointing hand across the body (no legs: he is the body to the waist)
      Object.assign(j, { elbowUR: [262 + s * 20, 238, -10], handUR: [300 + s * 30, 352 + sway2 * 20, -10],
        elbowUL: [-262 + s * 20, 230, -10], handUL: [-305 + s * 30, 330 - sway2 * 20, -10],
        elbowLR: [235 + s * 20, 64, 90], handLR: [210 + s * 22, 92, 190], elbowLL: [-190 + s * 20, 46, 110], handLL: [-40 + s * 30, -78, 205] });
    } else {                    // the devouring mouth: every arm reaching
      const r = Math.sin(danceT * 0.9) * 18;
      Object.assign(j, { elbowUL: [-290, 220, 60], handUL: [-395, 300 + r, 150], elbowUR: [290, 220, 60], handUR: [395, 300 - r, 150],
        elbowLL: [-300, 20, 90], handLL: [-395, -30 - r, 190], elbowLR: [300, 20, 90], handLR: [395, -30 + r, 190] });
    }
    if (clapK > 0) {            // the clap: the front hands meet before his chest, then part
      const meet = [0, 70, 210], k = Math.sin(Math.PI * clamp(clapK, 0, 1));
      j.handLL = V.lerp(j.handLL, [-8, meet[1], meet[2]], k); j.handLR = V.lerp(j.handLR, [8, meet[1], meet[2]], k);
      j.elbowLL = V.lerp(j.elbowLL, [-200, 40, 140], k); j.elbowLR = V.lerp(j.elbowLR, [200, 40, 140], k);
    }
    j.mouth = V.add(j.head, [0, -34, 78]);
    j.thirdEye = V.add(j.head, [0, 38, 80]);
    if (yaw) { const c = Math.cos(yaw), s = Math.sin(yaw); for (const k of Object.keys(j)) { const v = j[k]; j[k] = [v[0] * c + v[2] * s, v[1], -v[0] * s + v[2] * c]; } }
    j.mouthOpen = mouthOpen; j.yaw = yaw || 0;
    return j;
  }

  // ── the circuit: rings spiralling in from ringOuter to the phase's shell ──
  function ringCount(P, p) { return Math.round([P.ringsP1, P.ringsP2, P.ringsP3][p]); }
  function shellR(P, p) { return [P.shell1, P.shell2, P.shell3][p]; }
  function layRings(P, p, theta0, rnd) {
    const n = ringCount(P, p), rings = [], inner = shellR(P, p) + 85;
    const phi = rnd() * Math.PI * 2;
    for (let i = 0; i < n; i++) {
      const t = n > 1 ? i / (n - 1) : 1, r = P.ringOuter + (inner - P.ringOuter) * Math.pow(t, 0.85);
      const th = theta0 + i * P.ringStepDeg * Math.PI / 180, y = P.ringRise * Math.sin(i * 0.9 + phi) * (r / P.ringOuter);
      rings.push({ base: { r, th, y }, c: [r * Math.cos(th), y, r * Math.sin(th)], n: [1, 0, 0], omega: (i % 2 ? 1 : -1) * (0.7 + 0.6 * rnd()), bobPh: rnd() * 6.28, done: false });
    }
    orientRings(rings);
    return rings;
  }
  function orientRings(rings) {
    const n = rings.length;
    for (let i = 0; i < n; i++) {
      const a = rings[Math.max(0, i - 1)].c, b = rings[Math.min(n - 1, i + 1)].c;
      rings[i].n = i === n - 1 ? V.norm(V.mul(rings[i].c, -1)) : V.norm(V.sub(b, a));   // the last ring faces Shiva
    }
  }

  // ── the regrown cell: a 3D CA per cluster; live surface cells are flora prisms ──
  function growCA(rule, N, rnd) {
    let g = new Uint8Array(N * N * N); const c = (N - 1) / 2, idx = (x, y, z) => x + N * (y + N * z);
    if (rule.fill <= 0) { for (let k = 0; k < 3; k++) g[idx(Math.round(c + (rnd() - 0.5) * 2), Math.round(c + (rnd() - 0.5) * 2), Math.round(c + (rnd() - 0.5) * 2))] = 1; }
    else for (let z = 0; z < N; z++) for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) if (Math.hypot(x - c, y - c, z - c) < rule.seedR && rnd() < rule.fill) g[idx(x, y, z)] = 1;
    const born = new Int8Array(N * N * N).fill(-1);
    for (let i = 0; i < g.length; i++) if (g[i]) born[i] = 0;
    for (let k = 0; k < rule.gens; k++) {
      const h = new Uint8Array(g.length);
      for (let z = 1; z < N - 1; z++) for (let y = 1; y < N - 1; y++) for (let x = 1; x < N - 1; x++) {
        let n = 0;
        for (let dz = -1; dz <= 1; dz++) for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) if (dx | dy | dz) n += g[idx(x + dx, y + dy, z + dz)];
        const i = idx(x, y, z); h[i] = g[i] ? (rule.S.includes(n) ? 1 : 0) : (rule.B.includes(n) ? 1 : 0);
        if (h[i] && born[i] < 0) born[i] = k + 1;
      }
      g = h;
    }
    const cells = [];
    for (let z = 1; z < N - 1; z++) for (let y = 1; y < N - 1; y++) for (let x = 1; x < N - 1; x++) {
      const i = idx(x, y, z); if (!g[i]) continue;
      if (g[i + 1] && g[i - 1] && g[i + N] && g[i - N] && g[i + N * N] && g[i - N * N]) continue;   // inside: no prism
      cells.push([x - c, y - c, z - c, Math.max(0, born[i])]);
    }
    return cells;
  }
  function layFlora(P, p, rnd, W) {
    const rule = RULES[PHASES[p].ruleKey], prisms = [], clusters = 14, cs = 11;
    for (let k = 0; k < clusters; k++) {
      const r = shellR(P, Math.min(2, p)) + 220 + rnd() * (P.cellRadius - 380 - shellR(P, Math.min(2, p))), th = rnd() * 6.283, y = (rnd() - 0.5) * 900;
      const at = [r * Math.cos(th), y, r * Math.sin(th)];
      if (W && W.rings.some((g) => V.d2(g.c, at) < 200 * 200)) { k--; if (rnd() < 0.02) k++; continue; }   // keep the mouths clear
      let cells = growCA(rule, 18, rnd);
      if (cells.length > 260) cells = cells.filter((_, i) => (i * 7919) % cells.length < 260);
      const elem = Math.floor(rnd() * 4);
      for (const q of cells) prisms.push({ p: [at[0] + q[0] * cs, at[1] + q[1] * cs, at[2] + q[2] * cs], gen: q[3], elem, alive: true, grow: 0 });
    }
    return prisms;
  }

  // ── the orrery: planets wheeling round him (SpawnableOrrery's seven planets, as a backdrop with weight) ──
  function layPlanets(rnd) {
    const out = [];
    for (let i = 0; i < 7; i++) {
      const r = 760 + i * 120, tilt = (rnd() - 0.5) * 0.9, node = rnd() * 6.283;
      out.push({ r, size: 26 + ((i * 37) % 5) * 12 + (i === 3 ? 20 : 0), tilt, node, ph: rnd() * 6.283, w: 0.6 / Math.sqrt(r / 700) * 0.12, ringed: i === 3, c: [0, 0, 0] });
    }
    return out;
  }
  function planetPos(pl, t, speedK) {
    const a = pl.ph + pl.w * speedK * t, x = pl.r * Math.cos(a), z = pl.r * Math.sin(a);
    const y = z * Math.sin(pl.tilt), zz = z * Math.cos(pl.tilt), cn = Math.cos(pl.node), sn = Math.sin(pl.node);
    return [x * cn - zz * sn, y, x * sn + zz * cn];
  }

  // ═════════════════════════════════════════════════════════════════════════════════════════════════════════════
  function World(bank, P, opts) {
    opts = opts || {};
    this.bank = bank; this.P = P; this.seed = (opts.seed >>> 0) || 1; this.rnd = mulberry32(this.seed);
    this.aiRnd = mulberry32(this.seed * 7 + 3);
    this.t = 0; this.phase = 0; this.phaseT = 0; this.clock = P.clockP1; this.yaw = 0;
    this.state = 'play';           // play | clap | wave | regrow | won | lost
    this.stateT = 0; this.clapK = 0; this.waveR = 0; this.mouthOpen = 0; this.danceT = 0;
    this.events = []; this.outcome = 'Running';
    this.hulls = (opts.hulls || ['Squirrel', 'Sparrow', 'Rhino']).slice(0, 3);
    this.human = opts.human == null ? -1 : opts.human;   // seat a person flies (-1 = all autopilot)
    this.input = [{ pitch: 0, yaw: 0, boost: false, throttle: 1 }, null, null];
    this.noSwarm = !!opts.noSwarm;
    this.shellUp = [true, true, true];
    this.planets = layPlanets(this.rnd);
    this.rings = layRings(P, 0, this.rnd() * 6.283, this.rnd); this.ringIx = 0;
    this.flora = layFlora(P, 0, this.rnd, this); this.floraOld = [];
    for (const f of this.flora) f.grow = 1;
    this.crystals = [];
    for (let i = 0; i < Math.round(P.crystals); i++) this.crystals.push(this.newCrystal());
    this.trails = []; this.flung = [];
    this.pilots = this.hulls.map((h, i) => this.newPilot(h, i));
    this.serpents = []; this.sharks = []; this.fish = []; this.claws = [];
    this.stats = { stings: 0, rams: 0, nips: 0, latches: 0, stripped: 0, cuts: 0, serpentsCut: 0, rings: 0, wiped: 0, pulled: 0,
      phaseTime: [0, 0, 0], clockLeft: [0, 0, 0], crystals: 0 };
    this.release(0);
    this.yaw = Math.atan2(this.pilots[0].pos[0], this.pilots[0].pos[2]);
    this.fig = figure(0, 0, 0, 0, this.yaw);
  }
  const W = World.prototype;
  W.ev = function (k, o) { this.events.push(Object.assign({ k, t: this.t }, o || {})); };
  W.newCrystal = function () {
    const r = this.rnd, rad = shellR(this.P, Math.min(2, this.phase)) + 120 + r() * (this.P.cellRadius - 260 - shellR(this.P, Math.min(2, this.phase)));
    const th = r() * 6.283, y = (r() - 0.5) * 1000;
    return { p: [rad * Math.cos(th), y, rad * Math.sin(th)], v: [0, 0, 0], elem: Math.floor(r() * 4), alive: true, respawn: 0, armed: 0 };
  };
  W.newPilot = function (hull, i) {
    // the squad comes in through the membrane behind the first ring, in a line abreast
    const g = this.rings[0], back = V.sub(g.c, V.mul(g.n, 420)), side = V.norm(V.cross(g.n, [0, 1, 0]));
    const pos = V.add(back, V.mul(side, (i - 1) * 60)), fwd = V.norm(V.sub(g.c, pos));
    return { i, hull, H: HULLS[hull], pos, prev: pos.slice(), fwd, up: orthoUp(fwd, [0, 1, 0]), speed: HULLS[hull].cruise, meter: 0.6,
      el: [0, 1, 2, 3].map(() => this.P.startLevel), slowUntil: 0, slowK: 0, nips: [], latched: 0, latchBoost: 0, pull: [0, 0, 0],
      lastSting: -9, lastTrail: 0, boosting: false, stingFlash: 0, stripFlash: 0 };
  };
  W.nextPhaseShell = function () { return shellR(this.P, Math.min(2, this.phase)); };

  // the phase's swarm comes out of his hands
  W.release = function (p) {
    if (this.noSwarm) return;
    const P = this.P, rnd = this.rnd, f = figure(p, 0, 0, 0, this.yaw || 0), hands = [f.handUL, f.handUR, f.handLL, f.handLR];
    const out = (h) => { const d = V.norm(h), at = V.mul(d, shellR(P, p) + 70); return at; };
    const nS = Math.round([P.serpentsP1, P.serpentsP2, P.serpentsP3][p]);
    for (let k = 0; k < nS; k++) {
      const plan = this.bank[PHASES[p].serpent]; if (!plan) break;
      const at = out(hands[k % 4]), fwd = V.norm(V.add(V.norm(at), [rnd() - 0.5, 0, rnd() - 0.5]));
      this.serpents.push({ plan, rear: this.bank[PHASES[p].serpent + '_rear'], strike: this.bank[PHASES[p].serpent + '_strike'],
        pos: at, fwd, up: orthoUp(fwd, [0, 1, 0]), speed: P.serpentCruise, state: 'hunt', st: 0, cool: 2 + rnd() * 3, target: -1,
        alive: new Uint8Array(plan.n).fill(1), aliveN: plan.n, pose: 0, poseFrom: 0, poseK: 1, frameT: rnd() * 10, guard: rnd() * 6.283,
        dead: false, dieT: 0, born: this.t, world: new Float32Array(plan.n * 3) });
    }
    const nK = Math.round([P.sharksP1, P.sharksP2, P.sharksP3][p]);
    for (let k = 0; k < nK; k++) { const at = out(hands[(k + 1) % 4]); this.sharks.push({ pos: V.add(at, [rnd() * 40, rnd() * 40, rnd() * 40]), vel: V.mul(V.norm(at), P.sharkSpeed), cool: 2, target: -1, born: this.t, dead: false }); }
    const nF = Math.round([0, P.piranhaP2, P.piranhaP3][p]);
    for (let k = 0; k < nF; k++) { const at = out(hands[k % 4]); this.fish.push({ pos: V.add(at, [(rnd() - 0.5) * 60, (rnd() - 0.5) * 60, (rnd() - 0.5) * 60]), vel: V.mul(V.norm(at), P.piranhaMin), cool: 0, born: this.t, dead: false }); }
    const nC = Math.round([0, P.clawfishP2, P.clawfishP3][p]);
    for (let k = 0; k < nC; k++) { const at = out(hands[(k + 2) % 4]); this.claws.push({ pos: at, vel: V.mul(V.norm(at), P.clawfishSpeed), on: -1, onT: 0, cool: 1, born: this.t, dead: false }); }
    this.ev('release', { phase: p });
  };

  // ── a hit on a pilot ──
  W.slow = function (pl, k, secs) {
    if (this.t > pl.slowUntil) pl.slowK = 0;
    pl.slowK = Math.max(pl.slowK, k); pl.slowUntil = Math.max(pl.slowUntil, this.t + secs);
  };
  W.sting = function (pl, why) {
    if (this.t - pl.lastSting < 0.9) return;
    pl.lastSting = this.t; pl.stingFlash = 1; this.stats.stings++;
    pl.fwd = V.norm(V.add(pl.fwd, [this.rnd() - 0.5, this.rnd() - 0.5, this.rnd() - 0.5]));   // a sting throws you off your line
    this.slow(pl, this.P.stingSlow, this.P.stingSeconds);
    // a sting knocks one level of the richest element off, left as a crystal
    let e = 0; for (let k = 1; k < 4; k++) if (pl.el[k] > pl.el[e]) e = k;
    if (pl.el[e] > 0) { pl.el[e]--; this.fling(pl.pos, e, 1, 60); }
    this.ev('sting', { pilot: pl.i, why });
  };
  W.fling = function (at, elem, n, speed) {
    for (let k = 0; k < n; k++) {
      const d = V.norm([this.rnd() - 0.5, this.rnd() - 0.5, this.rnd() - 0.5]);
      this.crystals.push({ p: at.slice(), v: V.mul(d, speed * (0.6 + 0.6 * this.rnd())), elem, alive: true, respawn: -1, armed: this.t + 1.2 });
    }
  };
  W.strip = function (pl) {
    let n = 0;
    for (let e = 0; e < 4; e++) { if (pl.el[e] > 0) { this.fling(pl.pos, e, pl.el[e], 180); n += pl.el[e]; pl.el[e] = 0; } }
    if (n) { pl.stripFlash = 1; this.stats.stripped += n; this.ev('strip', { pilot: pl.i, n }); }
  };

  // ═════════════════════════════════════════════════════════════════════════════════════════════════════════════
  W.step = function (dt) {
    dt = dt || DT;
    if (this.state === 'won' || this.state === 'lost') { this.t += dt; this.stateT += dt; return; }
    const P = this.P;
    this.t += dt; this.stateT += dt; this.danceT += dt;
    if (this.state === 'play') { this.phaseT += dt; this.clock -= dt; }
    // he turns, slowly, to face the squad (lab: 0.12 rad/s)
    let cx = 0, cz = 0; for (const pl of this.pilots) { cx += pl.pos[0]; cz += pl.pos[2]; }
    const want = Math.atan2(cx, cz); let dy = want - this.yaw; dy = Math.atan2(Math.sin(dy), Math.cos(dy)); this.yaw += clamp(dy, -0.12 * dt, 0.12 * dt);
    this.fig = figure(this.phase, this.clapK, this.danceT, this.mouthOpen, this.yaw);

    // the rings dance in the second form
    if (this.phase === 1 && this.state === 'play') {
      const wheel = P.danceSpeed * this.phaseT;
      for (const g of this.rings) {
        const prev = g.c, k = 0.3 * this.phaseT + g.bobPh;
        const r = g.base.r + P.danceSway * Math.sin(0.23 * this.phaseT + g.bobPh * 1.7) * (g.base.r / P.ringOuter), th = g.base.th + wheel;
        g.c = [r * Math.cos(th), g.base.y + P.danceBob * Math.sin(k), r * Math.sin(th)];
        g.vel = V.mul(V.sub(g.c, prev), 1 / dt);
      }
      orientRings(this.rings);
    }
    // the mouth opens in the third
    this.mouthOpen += ((this.phase === 2 && this.state !== 'won' ? 1 : 0) - this.mouthOpen) * Math.min(1, dt * 0.8);
    for (const pl of this.planets) pl.c = planetPos(pl, this.t, this.phase === 1 ? 1.8 : 1);

    // the pilots
    for (const pl of this.pilots) this.stepPilot(pl, dt, pl.i === this.human ? this.input[0] : this.ai(pl));
    // the swarm
    for (const s of this.serpents) this.stepSerpent(s, dt);
    for (const s of this.sharks) this.stepShark(s, dt);
    this.stepFish(dt);
    for (const c of this.claws) this.stepClaw(c, dt);
    this.serpents = this.serpents.filter((s) => !(s.dead && this.t - s.dieT > 1.5));
    this.sharks = this.sharks.filter((s) => !s.dead); this.fish = this.fish.filter((s) => !s.dead); this.claws = this.claws.filter((s) => !s.dead);
    // the cell's contents
    this.stepCrystals(dt);
    this.trails = this.trails.filter((q) => q.alive && this.t - q.t < P.trailLife);
    for (const f of this.flora) if (f.grow < 1) f.grow = Math.min(1, f.grow + dt / Math.max(0.2, P.regrowSeconds * 0.4));
    this.floraOld = this.floraOld.filter((f) => (f.fall += dt) < 1.6);

    if (this.state === 'play' && this.clock <= 0) { this.state = 'lost'; this.stateT = 0; this.outcome = 'Lost'; this.stats.clockLeft[this.phase] = 0; this.ev('lost', { phase: this.phase }); return; }
    if (this.state === 'clap') this.stepClap(dt);
    else if (this.state === 'wave') this.stepWave(dt);
    else if (this.state === 'regrow' && this.stateT >= P.regrowSeconds * 0.5) { this.state = 'play'; this.stateT = 0; this.lastThread = this.t; this.release(this.phase); this.ev('play', { phase: this.phase }); }
  };

  W.threaded = function (pl) {
    const g = this.rings[this.ringIx]; if (!g || this.state !== 'play') return;
    const a = V.dot(V.sub(pl.prev, g.c), g.n), b = V.dot(V.sub(pl.pos, g.c), g.n);
    if ((a < 0) === (b < 0)) return;
    const k = a / (a - b), hit = V.lerp(pl.prev, pl.pos, k), mouth = this.P.mouth * (1 + 0.03 * pl.el[2]);   // Space widens the mouth (lab)
    if (V.len(V.sub(hit, g.c)) > mouth) return;
    g.done = true; this.ringIx++; this.stats.rings++; this.lastThread = this.t;
    this.ev('thread', { pilot: pl.i, ring: this.ringIx - 1, of: this.rings.length });
    if (this.ringIx >= this.rings.length) this.completePhase();
  };
  W.completePhase = function () {
    const p = this.phase;
    this.shellUp[p] = false; this.stats.phaseTime[p] = this.phaseT; this.stats.clockLeft[p] = this.clock;
    this.ev('shatter', { phase: p });
    if (p === 2) { this.state = 'won'; this.stateT = 0; this.outcome = 'Won'; this.ev('won', {}); return; }
    this.state = 'clap'; this.stateT = 0; this.ev('clap', { phase: p });
  };
  W.stepClap = function (dt) {
    this.clapK = clamp(this.stateT / 1.2, 0, 1);
    if (this.stateT >= 0.6 && !this.clapped) { this.clapped = true; this.waveR = 160; this.ev('wave', {}); }
    if (this.clapped) this.waveR += this.P.waveSpeed * dt;
    if (this.stateT >= 1.2) { this.state = 'wave'; this.stateT = 0; this.clapK = 0; }
    this.sweep();
  };
  W.stepWave = function (dt) {
    this.waveR += this.P.waveSpeed * dt; this.sweep();
    if (this.waveR >= this.P.cellRadius + 40) {
      this.clapped = false; this.waveR = 0; this.phase++; this.phaseT = 0; this.clock = [this.P.clockP1, this.P.clockP2, this.P.clockP3][this.phase];
      this.rings = layRings(this.P, this.phase, this.rnd() * 6.283, this.rnd); this.ringIx = 0;
      this.flora = layFlora(this.P, this.phase, this.rnd, this);
      const crystals = Math.round(this.P.crystals); while (this.crystals.filter((c) => c.respawn >= 0).length < crystals) this.crystals.push(this.newCrystal());
      this.state = 'regrow'; this.stateT = 0; this.ev('regrow', { phase: this.phase });
    }
  };
  // the wave front: everything it passes
  W.sweep = function () {
    const R = this.waveR, R2 = R * R, P = this.P;
    for (const q of this.trails) if (q.alive && V.d2(q.p, CENTER) < R2) { q.alive = false; this.stats.wiped++; }
    for (const f of this.flora) if (f.alive && V.d2(f.p, CENTER) < R2) { f.alive = false; this.floraOld.push({ p: f.p, elem: f.elem, fall: 0 }); }
    for (const s of this.serpents) if (!s.dead && V.d2(s.pos, CENTER) < R2) { s.dead = true; s.dieT = this.t; }
    for (const s of this.sharks.concat(this.fish, this.claws)) if (V.d2(s.pos, CENTER) < R2) s.dead = true;
    for (const pl of this.pilots) {
      const d = V.len(pl.pos);
      if (!pl.waved && d < R) {
        pl.waved = true; pl.latched = 0;
        if (d < P.clapReach) this.strip(pl);
        pl.pull = V.add(pl.pull, V.mul(V.norm(pl.pos), 120));   // the wave shoves everyone out
      }
    }
    for (const c of this.crystals) if (c.alive && !c.waved && V.d2(c.p, CENTER) < R2) {
      c.waved = true; if (V.len(c.p) < P.clapReach) { c.v = V.add(c.v, V.mul(V.norm(c.p), 260)); c.armed = this.t + 1.0; }
    }
    if (R > P.cellRadius) { for (const pl of this.pilots) pl.waved = false; for (const c of this.crystals) c.waved = false; }
  };

  // ── a vessel ──
  W.stepPilot = function (pl, dt, inp) {
    const P = this.P, H = pl.H, t = this.t;
    pl.prev = pl.pos.slice();
    pl.stingFlash = Math.max(0, pl.stingFlash - dt * 2); pl.stripFlash = Math.max(0, pl.stripFlash - dt);
    const timeK = 0.85 + 0.03 * pl.el[3], massK = 1 - 0.05 * pl.el[1];   // Time: speed; Mass: shrugs off slows and pull (lab)
    // turn: the stick, at the hull's rate (+ rts x speed: VesselTransformer's rotation throttle scaler)
    const rate = (H.turn + H.rts * pl.speed) * Math.PI / 180 * (pl.boosting && H.soarTurn ? H.soarTurn : 1);
    const right = V.norm(V.cross(pl.up, pl.fwd));
    let fwd = rot(pl.fwd, pl.up, clamp(inp.yaw, -1, 1) * rate * dt);
    fwd = rot(fwd, right, -clamp(inp.pitch, -1, 1) * rate * dt);
    pl.fwd = V.norm(fwd); pl.up = orthoUp(pl.fwd, V.lerp(pl.up, [0, 1, 0], Math.min(1, dt * 0.6)));
    // speed: cruise x throttle, or the top while boosting; slows multiply
    pl.boosting = !!inp.boost && pl.meter > 0.02 && !(H.ramp && Math.max(Math.abs(inp.yaw), Math.abs(inp.pitch)) > 0.3);
    if (pl.boosting) { if (!H.free) pl.meter = Math.max(0, pl.meter - dt * 0.16); if (pl.latched) pl.latchBoost += dt; } else { pl.meter = Math.min(1, pl.meter + dt * 0.06); pl.latchBoost = 0; }
    if (t > pl.slowUntil) pl.slowK = 0;
    pl.nips = pl.nips.filter((x) => x > t);
    let slow = Math.max(pl.slowK, pl.nips.length * P.nipSlow, pl.latched ? P.latchSlow : 0) * massK;
    const target = (pl.boosting ? H.top : H.cruise * clamp(inp.throttle == null ? 1 : inp.throttle, 0.3, 1)) * timeK * (1 - clamp(slow, 0, 0.9));
    const acc = pl.speed < target ? H.accel : H.accel * 2;
    pl.speed += clamp(target - pl.speed, -acc * dt, acc * dt);
    // the black hole in his mouth (third form): an accumulated pull, capped (BlackHoleVesselPull)
    if (this.phase === 2 && P.bhStrength > 0 && this.mouthOpen > 0.3) {
      const to = V.sub(this.fig.mouth, pl.pos), d = Math.max(60, V.len(to)), a = P.bhPull * P.bhStrength * 2.6e5 / (d * d) * massK * this.mouthOpen;
      pl.pull = V.add(pl.pull, V.mul(V.norm(to), a * dt));
      const pv = V.len(pl.pull); if (pv > P.bhMaxPull) pl.pull = V.mul(pl.pull, P.bhMaxPull / pv);
      if (d < P.bhStrength * 2 + 10) { this.strip(pl); this.stats.pulled++; pl.pos = V.mul(V.norm(pl.pos), P.cellRadius - 150); pl.pull = [0, 0, 0]; this.ev('swallowed', { pilot: pl.i }); }
    }
    pl.pull = V.mul(pl.pull, Math.exp(-dt * 0.6));   // lab: a vessel's own thrust bleeds the pull (terminal pull = accel / 0.6)
    pl.pos = V.add(pl.pos, V.add(V.mul(pl.fwd, pl.speed * dt), V.mul(pl.pull, dt)));
    // the membrane
    const r = V.len(pl.pos); if (r > P.cellRadius - 10) { const n = V.norm(pl.pos); pl.pos = V.mul(n, P.cellRadius - 12); if (V.dot(pl.fwd, n) > 0) pl.fwd = V.norm(V.sub(pl.fwd, V.mul(n, 2 * V.dot(pl.fwd, n)))); }
    // the shield: the lowest standing shell stings and turns you away
    const sh = this.shellUp.findIndex((u) => u);
    if (sh >= 0 && this.state !== 'won') {
      const R = shellR(P, sh), d = V.len(pl.pos);
      if (d < R + 8) { const n = V.norm(pl.pos); pl.pos = V.mul(n, R + 10); if (V.dot(pl.fwd, n) < 0) pl.fwd = V.norm(V.sub(pl.fwd, V.mul(n, 2 * V.dot(pl.fwd, n)))); pl.pull = V.mul(n, 160); this.sting(pl, 'shield'); }
    }
    // planets
    for (const q of this.planets) { const d = V.len(V.sub(pl.pos, q.c)); if (d < q.size + 6) { const n = V.norm(V.sub(pl.pos, q.c)); pl.pos = V.add(q.c, V.mul(n, q.size + 8)); pl.fwd = V.norm(V.add(pl.fwd, V.mul(n, 1.2))); this.slow(pl, 0.4, 0.6); } }
    // flora: flying through a prism breaks it and costs a little; skimming past refills the boost (skim energy)
    let near = false;
    for (const f of this.flora) {
      if (!f.alive || f.grow < 0.5) continue;
      const d2 = V.d2(f.p, pl.pos); if (d2 > 900) continue;
      if (d2 < 100) { f.alive = false; this.slow(pl, 0.1, 0.3); } else near = true;
    }
    if (near) pl.meter = Math.min(1, pl.meter + dt * 0.45);
    // crystals
    for (const c of this.crystals) {
      if (!c.alive || c.armed > t) continue;
      if (V.d2(c.p, pl.pos) < 26 * 26) { c.alive = false; c.respawn = c.respawn >= 0 ? t + P.crystalRespawn : -2; if (pl.el[c.elem] < 10) pl.el[c.elem]++; pl.meter = Math.min(1, pl.meter + 0.1); this.stats.crystals++; this.ev('crystal', { pilot: pl.i, elem: c.elem }); }
    }
    // the trail it lays (the squad's prisms)
    if (t - pl.lastTrail > P.trailInterval) { pl.lastTrail = t; this.trails.push({ p: V.sub(pl.pos, V.mul(pl.fwd, 8)), t, alive: true, owner: pl.i, fwd: pl.fwd }); }
    this.threaded(pl);
  };

  // ── the autopilot: writes the same input a stick does (AIPilot's rule), never the vessel ──
  W.ai = function (pl) {
    const P = this.P, H = pl.H, inp = { pitch: 0, yaw: 0, boost: false, throttle: 1 };
    if (this.state === 'won' || this.state === 'lost') return inp;
    let ringIx = this.ringIx;
    // squadmates split: the one nearest the lit ring takes it, the others pre-position on the next one
    if (this.state === 'play' && this.rings[ringIx + 1] && this.t - (this.lastThread || 0) < 10) {
      const lit = this.rings[ringIx].c, mine = V.d2(pl.pos, lit);
      const lead = this.pilots.find((o) => o !== pl && V.d2(o.pos, lit) < Math.min(mine, 600 * 600) && o.speed > 50);
      if (lead && mine > 300 * 300) ringIx++;
    }
    let g = this.rings[ringIx];
    let aim;
    if (!g || this.state !== 'play') aim = V.mul(V.norm(pl.pos), P.ringOuter);
    else {
      const dist0 = V.len(V.sub(pl.pos, g.c)), eta = clamp(dist0 / Math.max(40, pl.speed), 0, 3);
      const gc = g.vel ? V.add(g.c, V.mul(g.vel, eta)) : g.c;
      g = { c: gc, n: g.n };
      // the approach: get onto the ring's axis first (a gate a turn radius or two out), then fly straight through
      const rel = V.sub(pl.pos, g.c), side = Math.sign(V.dot(rel, g.n)) || 1, dist = V.len(rel);
      const rateA = (H.turn + H.rts * pl.speed) * Math.PI / 180 * (H.soarTurn || 1), turnR = pl.speed / Math.max(0.1, rateA);
      const L = Math.max(90, 2.2 * turnR), onAxis = V.dot(V.norm(rel), V.mul(g.n, side));
      if (onAxis > 0.8 || dist < P.mouth) aim = V.sub(g.c, V.mul(g.n, side * L));        // lined up: through it
      else aim = V.add(g.c, V.mul(g.n, side * Math.min(L, dist)));                          // not yet: to the gate
      pl.aiTurnR = turnR; pl.aiLined = onAxis > 0.8;
    }
    // a crystal close and ahead is worth a short detour while the ring is still far (lab)
    if (g && this.state === 'play' && V.len(V.sub(aim, pl.pos)) > 320) {
      let bestC = null, bc = 150 * 150;
      for (const c of this.crystals) { if (!c.alive || c.armed > this.t) continue; const to = V.sub(c.p, pl.pos), d2 = V.dot(to, to); if (d2 < bc && V.dot(to, pl.fwd) > 0.6 * Math.sqrt(d2)) { bc = d2; bestC = c; } }
      if (bestC) aim = bestC.p;
    }
    // lean against the mouth's pull: aim where the pull will not carry me
    if (V.len(pl.pull) > 5) aim = V.sub(aim, V.mul(pl.pull, 0.9));
    let want = V.norm(V.sub(aim, pl.pos));
    // keep off the shield - only when on a collision course with it (the last rings sit just outside it)
    const sh = this.shellUp.findIndex((u) => u), d = V.len(pl.pos);
    if (sh >= 0) {
      const R = shellR(P, sh), gap = d - R, out = V.norm(pl.pos), closing = -V.dot(want, out), reach = 30 + pl.speed * 0.5;
      if (gap < reach && closing > 0) want = V.norm(V.add(want, V.mul(out, closing * 1.5 * clamp((reach - gap) / reach, 0, 1))));
    }
    if (this.phase === 2 && this.mouthOpen > 0.3) { const away = V.norm(V.sub(pl.pos, this.fig.mouth)), dm = V.len(V.sub(pl.pos, this.fig.mouth)); if (dm < 260) want = V.norm(V.add(want, V.mul(away, (260 - dm) / 260))); }
    for (const q of this.planets) { const to = V.sub(q.c, pl.pos), dd = V.len(to); if (dd < q.size + 120 && V.dot(to, pl.fwd) > 0) want = V.norm(V.add(want, V.mul(V.norm(V.sub(pl.pos, q.c)), 0.8))); }
    // dodge a serpent striking at me
    for (const s of this.serpents) if (!s.dead && s.state === 'strike' && s.target === pl.i) { const to = V.sub(s.pos, pl.pos); if (V.len(to) < 300) want = V.norm(V.add(want, V.mul(V.norm(V.cross(to, pl.up)), 0.7))); }
    if (d > P.cellRadius - 140) want = V.norm(V.add(want, V.mul(V.norm(pl.pos), -0.8)));
    // sloppiness: a slow wander on the aim
    const n = P.aiNoise; if (n > 0) { pl.wob = (pl.wob || [0, 0, 0]).map((x) => clamp(x + (this.aiRnd() - 0.5) * n * 0.25, -n * 0.5, n * 0.5)); want = V.norm(V.add(want, pl.wob)); }
    // to stick: the error in the vessel's own frame
    const right = V.norm(V.cross(pl.up, pl.fwd)), lx = V.dot(want, right), ly = V.dot(want, pl.up), lz = V.dot(want, pl.fwd);
    const yawErr = Math.atan2(lx, lz), pitchErr = Math.atan2(ly, Math.hypot(lx, lz));
    inp.yaw = clamp(yawErr * 2.2, -1, 1); inp.pitch = clamp(pitchErr * 2.2, -1, 1);
    const err = Math.acos(clamp(lz, -1, 1)), rate = (H.turn + H.rts * pl.speed) * Math.PI / 180 * (H.soarTurn || 1);
    const distAim = g ? V.len(V.sub(g.c, pl.pos)) : 999;
    const turnR = pl.speed / Math.max(0.1, rate);
    const overshoot = err > 0.5 && distAim < turnR * 2.2;
    inp.boost = (pl.latched && pl.meter > 0.05) || (!overshoot && (H.free ? (err < 0.6 || distAim > 600) : (pl.meter > 0.12 && err < 0.35 && distAim > Math.max(260, turnR * 1.6))));
    inp.throttle = (err > 0.9 && turnR > distAim * 0.35) || (!pl.aiLined && distAim < turnR * 2.5 && err > 0.4) ? 0.35 : 1;
    return inp;
  };

  // ── a serpent: guards the lit ring, lunges at a pilot that comes near ──
  W.stepSerpent = function (s, dt) {
    if (s.dead) return;
    const P = this.P, plan = s.plan;
    s.frameT += dt;
    s.st += dt;
    let goal, speed = P.serpentCruise, turn = 1.6;
    const lit = this.rings[Math.min(this.ringIx, this.rings.length - 1)];
    // the nearest pilot
    let best = -1, bd = 1e18;
    for (const pl of this.pilots) { const d = V.d2(pl.pos, s.pos); if (d < bd) { bd = d; best = pl.i; } }
    const bdist = Math.sqrt(bd);
    if (s.state === 'hunt') {
      s.cool -= dt;
      // it coils round a ring across its mouth: the even serpents the lit one, the odd ones the next (lab)
      // each serpent holds a ring further along the circuit; once the squad has passed it, it moves on two ahead
      const N = this.rings.length, alive = this.serpents.filter((o) => !o.dead), k = alive.indexOf(s);
      if (s.guardRing == null) s.guardRing = Math.min(N - 1, Math.round((k + 1) * N / (alive.length + 1)));
      if (s.guardRing < this.ringIx) s.guardRing = Math.min(N - 1, this.ringIx + 2 + (k % 2));
      const gr = this.rings[s.guardRing] || lit;
      s.guard += dt * 0.9;
      let guardAt = V.mul(V.norm(s.pos), 900);
      if (gr) { const e1 = V.norm(V.cross(gr.n, [0, 1, 0.01])), e2 = V.cross(gr.n, e1), rr = this.P.mouth * 1.4; guardAt = V.add(gr.c, V.add(V.mul(e1, Math.cos(s.guard) * rr), V.mul(e2, Math.sin(s.guard) * rr))); }
      goal = bdist < P.lungeRange * 1.6 ? this.pilots[best].pos : guardAt;
      if (best >= 0 && bdist < P.lungeRange && s.cool <= 0 && this.state === 'play') { s.state = 'rear'; s.st = 0; s.target = best; this.setPose(s, 1); this.ev('lunge', { serpent: this.serpents.indexOf(s), pilot: best }); }
    }
    if (s.state === 'rear') {
      const pl = this.pilots[s.target]; goal = V.add(pl.pos, V.mul(pl.fwd, pl.speed * 0.35)); speed = 30; turn = 2.2;
      if (s.st > 0.6) { s.state = 'strike'; s.st = 0; this.setPose(s, 2); }
    } else if (s.state === 'strike') {
      const pl = this.pilots[s.target]; goal = V.add(pl.pos, V.mul(pl.fwd, pl.speed * 0.35)); speed = P.serpentLunge; turn = 1.0;
      if (s.st > 2.5 || V.len(V.sub(pl.pos, s.pos)) < 40 || V.len(V.sub(pl.pos, s.pos)) > P.lungeRange * 1.8) { s.state = 'hunt'; s.st = 0; s.cool = P.lungeCooldown; this.setPose(s, 0); }
    }
    // keep off his shield and inside the cell
    let want = V.norm(V.sub(goal, s.pos));
    const sh = this.shellUp.findIndex((u) => u), d = V.len(s.pos);
    if (sh >= 0 && d < shellR(P, sh) + 140) want = V.norm(V.add(want, V.mul(V.norm(s.pos), 1.2)));
    if (d > P.cellRadius - 200) want = V.norm(V.add(want, V.mul(V.norm(s.pos), -1)));
    const ang = Math.acos(clamp(V.dot(s.fwd, want), -1, 1));
    if (ang > 1e-4) { const ax = V.norm(V.cross(s.fwd, want)); s.fwd = V.norm(rot(s.fwd, ax, Math.min(ang, turn * dt))); }
    s.up = orthoUp(s.fwd, V.lerp(s.up, [0, 1, 0], dt));
    s.speed += clamp(speed - s.speed, -200 * dt, 200 * dt);
    s.pos = V.add(s.pos, V.mul(s.fwd, s.speed * dt));
    s.poseK = Math.min(1, s.poseK + dt / 0.35);
    // the body in the world, and what touches it
    this.bodyWorld(s);
    const R = plan.radius + 40, danger = s.state !== 'hunt';
    for (const pl of this.pilots) {
      if (V.d2(pl.pos, s.pos) > R * R) continue;
      const cut = 8 + 0.8 * pl.el[0];   // Charge widens the cut (lab)
      for (let i = 0; i < plan.n; i++) {
        if (!s.alive[i]) continue;
        const x = s.world[3 * i] - pl.pos[0], y = s.world[3 * i + 1] - pl.pos[1], z = s.world[3 * i + 2] - pl.pos[2], d2 = x * x + y * y + z * z;
        const tier = this.poseTier(s, i);
        if (danger && tier === 1 && d2 < 12 * 12) { this.sting(pl, 'serpent'); continue; }
        if (d2 < cut * cut && tier !== 1) {
          if (tier === 2 && pl.el[0] < 5) {
            // its shielded body is solid (a shield's ram): bounce off it, slowed - Charge 5+ cuts it like the core (lab)
            if (this.t - (pl.lastBump || -9) > 0.35) {
              pl.lastBump = this.t; this.slow(pl, P.bumpSlow, 0.6); this.stats.bumps = (this.stats.bumps || 0) + 1;
              const nrm = V.norm([-x, -y, -z]); if (V.dot(pl.fwd, nrm) < 0) pl.fwd = V.norm(V.sub(pl.fwd, V.mul(nrm, 2 * V.dot(pl.fwd, nrm))));
            }
            continue;
          }
          s.alive[i] = 0; s.aliveN--; this.stats.cuts++;
        }
      }
    }
    // the squad's trail prisms cut it too
    for (const q of this.trails) {
      if (!q.alive || V.d2(q.p, s.pos) > R * R) continue;
      for (let i = 0; i < plan.n; i += 2) {
        if (!s.alive[i]) continue;
        const x = s.world[3 * i] - q.p[0], y = s.world[3 * i + 1] - q.p[1], z = s.world[3 * i + 2] - q.p[2];
        if (x * x + y * y + z * z < 64) { s.alive[i] = 0; s.aliveN--; q.alive = false; this.stats.cuts++; break; }
      }
    }
    if (s.aliveN < plan.n * 0.35) { s.dead = true; s.dieT = this.t; this.stats.serpentsCut++; this.ev('serpentDown', {}); }
  };
  W.setPose = function (s, k) { s.poseFrom = s.pose; s.pose = k; s.poseK = 0; };
  W.posePlan = function (s, k) { return k === 0 ? s.plan : k === 1 ? (s.rear || s.plan) : (s.strike || s.plan); };
  W.poseTier = function (s, i) { const pl = this.posePlan(s, s.pose); return pl.tier[i]; };
  W.bodyWorld = function (s) {
    const A = this.posePlan(s, s.poseFrom), B = this.posePlan(s, s.pose), k = s.poseK;
    const fa = Math.floor(s.frameT / (A.frameSteps * 0.1)) % A.frames.length, fb = Math.floor(s.frameT / (B.frameSteps * 0.1)) % B.frames.length;
    const a = A.frames[fa], b = B.frames[fb], f = s.fwd, u = s.up, r = V.cross(u, f), n = s.plan.n;
    for (let i = 0; i < n; i++) {
      const lx = a[3 * i] + (b[3 * i] - a[3 * i]) * k, ly = a[3 * i + 1] + (b[3 * i + 1] - a[3 * i + 1]) * k, lz = a[3 * i + 2] + (b[3 * i + 2] - a[3 * i + 2]) * k;
      // plan x = swim axis, y = up, z = right-handed side
      s.world[3 * i] = s.pos[0] + f[0] * lx + u[0] * ly - r[0] * lz;
      s.world[3 * i + 1] = s.pos[1] + f[1] * lx + u[1] * ly - r[1] * lz;
      s.world[3 * i + 2] = s.pos[2] + f[2] * lx + u[2] * ly - r[2] * lz;
    }
  };

  // ── sharks: ram the nearest pilot ──
  W.stepShark = function (s, dt) {
    const P = this.P; s.cool -= dt;
    let best = null, bd = 1e18; for (const pl of this.pilots) { const d = V.d2(pl.pos, s.pos); if (d < bd) { bd = d; best = pl; } }
    // a pilot near: chase it; none near: lie in wait at the lit ring, where the squad must come
    const lit = this.rings[Math.min(this.ringIx, this.rings.length - 1)];
    let goal = best && bd < 450 * 450 ? V.add(best.pos, V.mul(best.fwd, best.speed * 0.4)) : lit ? V.add(lit.c, V.mul(lit.n, 90 * Math.sin(this.t * 0.7 + this.sharks.indexOf(s)))) : V.mul(V.norm(s.pos), 1000);
    if (s.cool > 0) goal = V.add(s.pos, V.mul(V.norm(s.vel), 200));
    let want = V.norm(V.sub(goal, s.pos));
    const sh = this.shellUp.findIndex((u) => u), d = V.len(s.pos);
    if (sh >= 0 && d < shellR(P, sh) + 100) want = V.norm(V.add(want, V.mul(V.norm(s.pos), 1.2)));
    if (d > P.cellRadius - 100) want = V.norm(V.add(want, V.mul(V.norm(s.pos), -1)));
    const v = V.add(V.mul(V.norm(s.vel), 1 - dt * 1.8), V.mul(want, dt * 1.8));
    s.vel = V.mul(V.norm(v), P.sharkSpeed * (s.cool > 0 ? 0.7 : 1));
    s.pos = V.add(s.pos, V.mul(s.vel, dt));
    if (best && s.cool <= 0 && bd < 18 * 18) { this.slow(best, P.ramSlow, P.ramSeconds); this.stats.rams++; s.cool = 3; this.ev('ram', { pilot: best.i }); }
    for (const q of this.trails) if (q.alive && V.d2(q.p, s.pos) < 100) { q.alive = false; s.cool = Math.max(s.cool, 0.6); }   // a trail prism costs it its run
  };
  // ── piranha: a school that nips ──
  W.stepFish = function (dt) {
    const P = this.P, F = this.fish; if (!F.length) return;
    let cx = 0, cy = 0, cz = 0; for (const f of F) { cx += f.pos[0]; cy += f.pos[1]; cz += f.pos[2]; } const cen = [cx / F.length, cy / F.length, cz / F.length];
    for (const f of F) {
      f.cool -= dt;
      let best = null, bd = 1e18; for (const pl of this.pilots) { const d = V.d2(pl.pos, f.pos); if (d < bd) { bd = d; best = pl; } }
      let goal = best && bd < 700 * 700 ? best.pos : cen;
      let want = V.norm(V.sub(goal, f.pos));
      want = V.norm(V.add(want, V.mul(V.norm(V.sub(cen, f.pos)), V.len(V.sub(cen, f.pos)) > 35 ? 0.4 : -0.2)));
      const sh = this.shellUp.findIndex((u) => u), d = V.len(f.pos);
      if (sh >= 0 && d < shellR(P, sh) + 60) want = V.norm(V.add(want, V.mul(V.norm(f.pos), 1.2)));
      if (d > P.cellRadius - 60) want = V.norm(V.add(want, V.mul(V.norm(f.pos), -1)));
      const sp = best && bd < 300 * 300 ? P.piranhaMax : P.piranhaMin;
      f.vel = V.mul(V.norm(V.add(V.mul(V.norm(f.vel), 1 - dt * 3), V.mul(want, dt * 3))), sp);
      f.pos = V.add(f.pos, V.mul(f.vel, dt));
      if (best && bd < 10 * 10 && f.cool <= 0) {
        if (best.boosting) { f.dead = true; continue; }                       // a boosting hull runs it down
        best.nips.push(this.t + P.nipSeconds); f.cool = 1.5; this.stats.nips++;
      }
    }
  };
  // ── clawfish: latch on until you boost ──
  W.stepClaw = function (c, dt) {
    const P = this.P;
    if (c.on >= 0) {
      const pl = this.pilots[c.on]; c.pos = V.sub(pl.pos, V.mul(pl.fwd, 6)); c.onT += dt;
      if (pl.latchBoost > 1.0 || c.onT > 6) { pl.latched = Math.max(0, pl.latched - 1); c.on = -1; c.cool = 3; c.vel = V.mul(V.norm(V.sub(c.pos, [0, 0, 0])), P.clawfishSpeed); this.ev('shaken', { pilot: pl.i }); }
      return;
    }
    c.cool -= dt;
    let best = null, bd = 1e18; for (const pl of this.pilots) { const d = V.d2(pl.pos, c.pos); if (d < bd) { bd = d; best = pl; } }
    const goal = best ? best.pos : [0, 0, 0];
    let want = V.norm(V.sub(goal, c.pos));
    const sh = this.shellUp.findIndex((u) => u), d = V.len(c.pos);
    if (sh >= 0 && d < shellR(P, sh) + 60) want = V.norm(V.add(want, V.mul(V.norm(c.pos), 1.2)));
    c.vel = V.mul(V.norm(V.add(V.mul(V.norm(c.vel), 1 - dt * 2), V.mul(want, dt * 2))), P.clawfishSpeed);
    c.pos = V.add(c.pos, V.mul(c.vel, dt));
    if (best && c.cool <= 0 && bd < 12 * 12) { c.on = best.i; c.onT = 0; best.latched++; this.stats.latches++; this.ev('latch', { pilot: best.i }); }
  };

  W.stepCrystals = function (dt) {
    const P = this.P;
    for (const c of this.crystals) {
      if (!c.alive) { if (c.respawn > 0 && this.t > c.respawn) Object.assign(c, this.newCrystal()); continue; }
      if (c.v[0] || c.v[1] || c.v[2]) { c.p = V.add(c.p, V.mul(c.v, dt)); c.v = V.mul(c.v, Math.exp(-dt * 1.4)); if (V.len(c.v) < 2) c.v = [0, 0, 0]; }
      if (V.len(c.p) > P.cellRadius - 20) { c.p = V.mul(V.norm(c.p), P.cellRadius - 22); c.v = [0, 0, 0]; }
      if (this.phase === 2 && this.mouthOpen > 0.3 && P.bhStrength > 0) {   // the mouth drinks the crystals near it
        const to = V.sub(this.fig.mouth, c.p), d = V.len(to);
        if (d < 520) c.p = V.add(c.p, V.mul(V.norm(to), dt * 2.6e5 * P.bhStrength / Math.max(3600, d * d)));
        if (d < P.bhStrength * 2 + 6) { c.alive = false; c.respawn = c.respawn >= 0 ? this.t + P.crystalRespawn : -2; }
      }
    }
    if (this.crystals.length > 400) this.crystals = this.crystals.filter((c) => c.alive || c.respawn >= 0);
  };

  W.score = function () {
    const s = this.stats;
    return { outcome: this.outcome, t: +this.t.toFixed(1), phase: this.phase + 1, rings: s.rings,
      p1: +s.phaseTime[0].toFixed(1), p2: +s.phaseTime[1].toFixed(1), p3: +s.phaseTime[2].toFixed(1),
      left1: +s.clockLeft[0].toFixed(1), left2: +s.clockLeft[1].toFixed(1), left3: +s.clockLeft[2].toFixed(1),
      stings: s.stings, bumps: s.bumps || 0, rams: s.rams, nips: s.nips, latches: s.latches, stripped: s.stripped, swallowed: s.pulled, wiped: s.wiped, cuts: s.cuts, serpentsCut: s.serpentsCut, crystals: s.crystals };
  };

  // run a squad to the end, headless
  function run(bank, P, opts, maxSeconds) {
    const w = new World(bank, P, opts), steps = Math.round((maxSeconds || 900) / DT);
    for (let i = 0; i < steps && w.outcome === 'Running'; i++) { w.step(DT); if (w.events.length > 64) w.events.length = 0; }
    return w.score();
  }

  G.ShivaSim = { SHIPPED, SPEC, HULLS, HULL_NAMES, PHASES, ELEMENTS, RULES, UNIT, DT, V, World, buildBank, figure, run, mulberry32, shellR, ringCount, growCA, planetPos };
})(typeof window !== 'undefined' ? window : globalThis);
