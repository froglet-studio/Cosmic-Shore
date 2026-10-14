#!/usr/bin/env node
// Parity: the JS Charge light (src/chargeArcs.js) against the SHIPPED C# (ChargeArcs.cs via parity_charge/, which writes
// ref.json + frames.bin). Runs the JS on the same inputs - the shipped OmniCombineData.json, data/charge_arcs.json's
// profile and plate thickness, the same seeds, eyes, frame times and strike prisms - and compares:
//   settings (every field), the input vertices, both crystals' sites (axes, neighbours, junctions, seams, faces, corners),
//   four sampled flux paths, the noise / hash / System.Random, and for every one of the 1800 frames of each effect the
//   writer's counts and bit hashes (positions, UV0 data, indices), light[] and both fluxStrikes outputs, plus every 30th
//   frame's writer arrays in full.
// Then two NEGATIVE CONTROLS that must FAIL: the seam walk run with seed 8, and a copy of the port with one constant
// perturbed (the plasma's SampleSpacing 0.0125 -> 0.0126).
// Usage: node parity_charge.cjs <refDir>   (run_parity_charge.sh builds the reference and runs this). Exit 0 = parity
// holds AND both controls failed.
'use strict';
const fs = require('fs');
const path = require('path');

const HERE = __dirname;
const ROOT = path.resolve(HERE, '..', '..', '..');
const refDir = process.argv[2] || path.join(HERE, 'parity_charge', 'out');
const F = Math.fround;

function loadPort(mutate) {
  const file = path.join(HERE, 'src', 'chargeArcs.js');
  if (!mutate) { require(file); return globalThis.CR.ChargeArcs; }
  let src = fs.readFileSync(file, 'utf8');
  const before = src;
  src = src.replace('SampleSpacing = F(0.0125)', 'SampleSpacing = F(0.0126)');
  if (src === before) throw new Error('negative control: the constant to perturb was not found');
  const g = {};
  new Function('globalThis', src)(g);
  return g.CR.ChargeArcs;
}

const ref = JSON.parse(fs.readFileSync(path.join(refDir, 'ref.json'), 'utf8'));
const bin = fs.readFileSync(path.join(refDir, 'frames.bin'));
const cfg = JSON.parse(fs.readFileSync(path.join(HERE, 'data', 'charge_arcs.json'), 'utf8'));
const combine = JSON.parse(fs.readFileSync(path.join(ROOT, 'Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json'), 'utf8'));

// the reference's full frames
const full = new Map();
for (let o = 0; o < bin.length;) {
  const effect = bin.readInt32LE(o), frame = bin.readInt32LE(o + 4), nv = bin.readInt32LE(o + 8), ni = bin.readInt32LE(o + 12);
  o += 16;
  const pos = new Float32Array(nv * 3), data = new Float32Array(nv * 4), idx = new Int32Array(ni);
  for (let i = 0; i < nv * 3; i++, o += 4) pos[i] = bin.readFloatLE(o);
  for (let i = 0; i < nv * 4; i++, o += 4) data[i] = bin.readFloatLE(o);
  for (let i = 0; i < ni; i++, o += 4) idx[i] = bin.readInt32LE(o);
  full.set(effect + ':' + frame, { pos, data, idx });
}

const f32 = new Float32Array(1), u32 = new Uint32Array(f32.buffer);
const bits = (v) => { if (v === 0) return 0; f32[0] = v; return u32[0]; };
const fnv = (h, w) => Math.imul((h ^ w) >>> 0, 16777619) >>> 0;
const hashFlat = (a) => { let h = 2166136261; for (let i = 0; i < a.length; i++) h = fnv(h, bits(a[i])); return h; };
const hashInts = (a) => { let h = 2166136261; for (let i = 0; i < a.length; i++) h = fnv(h, a[i] >>> 0); return h; };
const vecs = (flat) => { const v = []; for (let i = 0; i + 2 < flat.length; i += 3) v.push([flat[i], flat[i + 1], flat[i + 2]]); return v; };

function run(CA, opts) {
  const o = Object.assign({ seamSeed: 7, fluxSeed: 7 ^ 0x5bd1e995 }, opts);
  const s = CA.settings(cfg.profile);
  const chargeData = combine.crystals.find((c) => c.element === 'Charge');
  const chargeVerts = vecs(chargeData.basis.map(F)), omniVerts = vecs(combine.omni.verts.map(F));
  const cb = CA.ChargeSites.tryBuild(chargeVerts, chargeData.tris, 1);
  const ob = CA.ChargeSites.tryBuild(omniVerts, combine.omni.tris, cfg.plateThickness);
  if (!cb.ok || !ob.ok) throw new Error(`sites: ${cb.problem || ob.problem}`);
  const out = { settings: s, span: CA.span(s), inputHash: { charge: hashFlat(chargeVerts.flat()), omni: hashFlat(omniVerts.flat()) }, sites: { charge: cb.sites, omni: ob.sites } };

  out.paths = ref.paths.map((p) => {
    const path = ob.sites.pathOf(p.source, p.sink);
    const strands = [];
    for (let q = 0; q < 5; q++) for (let i = 0; i <= 24; i++) strands.push(path.strand(q, F(i / 24), s.FluxFork, F(s.FluxBundle * ob.sites.Radius), s.FluxTurns));
    const centre = [];
    for (let i = 0; i <= 8; i++) centre.push(path.centre(F(i / 8)));
    return { centre, strands, sourceOffsets: path.SourceOffsets, sinkOffsets: path.SinkOffsets };
  });
  const rnd = new CA.NetRandom(7 ^ 0x5bd1e995);
  out.noise = { fbm3: ref.noise.x.map((x) => CA.fbm(F(x), 3)), hash: Array.from({ length: 100 }, (_, i) => CA.hash((i - 50) * 7919, 977)),
    random: Array.from({ length: 50 }, () => rnd.next(1000)) };

  const frames = ref.seam.frames.length;
  out.seam = [];
  {
    const walker = new CA.ChargeSeamWalker(cb.sites, o.seamSeed, 0);
    const writer = new CA.ChargeArcMeshWriter();
    const light = new Float32Array(cb.sites.Faces.length);
    for (let i = 0; i < frames; i++) {
      const t = F(i / 60);
      walker.advance(t, s);
      const points = CA.seamWalk(cb.sites, walker, t, s, writer, ref.seam.eye, light);
      CA.writeFaces(cb.sites, writer, light, true);
      out.seam.push({ t, points, legs: walker.Legs.length, nv: writer.vertexCount, ni: writer.indices.length,
        hp: hashFlat(writer.positions), hd: hashFlat(writer.data), hi: hashInts(writer.indices), light: Array.from(light),
        full: i % 30 === 0 ? { pos: writer.positions.slice(), data: writer.data.slice(), idx: writer.indices.slice() } : null });
      writer.clear();
    }
  }
  out.flux = [];
  {
    const chain = new CA.ChargeFluxChain(ob.sites, o.fluxSeed);
    const writer = new CA.ChargeArcMeshWriter();
    const light = new Float32Array(ob.sites.Faces.length);
    const si = ref.flux.strikeInputs, n = si.radii.length;
    const strikeA = new Float32Array(n), strikeB = new Float32Array(n);
    out.gates = Array.from({ length: 21 }, (_, i) => CA.strikeGate(F(i / 20), s));
    for (let i = 0; i < frames; i++) {
      const t = F(i / 60);
      const drawn = CA.flux(ob.sites, chain, t, s, writer, ref.flux.eye, light);
      CA.writeFaces(ob.sites, writer, light, false);
      CA.fluxStrikes(ob.sites, chain, t, s, si.centres, si.radii, n, strikeA);
      CA.fluxStrikes(ob.sites, chain, t, s, si.centres, si.radii, si.places, si.seats, si.seatRadii, n, strikeB);
      out.flux.push({ t, drawn, nv: writer.vertexCount, ni: writer.indices.length,
        hp: hashFlat(writer.positions), hd: hashFlat(writer.data), hi: hashInts(writer.indices), light: Array.from(light),
        strike: Array.from(strikeA), strikeGated: Array.from(strikeB),
        full: i % 30 === 0 ? { pos: writer.positions.slice(), data: writer.data.slice(), idx: writer.indices.slice() } : null });
      writer.clear();
    }
  }
  return out;
}

function compare(js) {
  const problems = [];
  let maxErr = 0, compared = 0;
  const num = (what, a, b) => {
    compared++;
    const r = F(b);
    if (typeof a !== 'number' || Number.isNaN(a)) { problems.push(`${what}: ${a} vs ${r}`); return; }
    const e = Math.abs(a - r);
    if (e > maxErr) maxErr = e;
    if (e !== 0 && problems.length < 4000) problems.push(`${what}: js ${a} vs C# ${r} (|d| ${e.toExponential(2)})`);
  };
  const int = (what, a, b) => { compared++; if (a !== b) problems.push(`${what}: js ${a} vs C# ${b}`); };
  const arr = (what, a, b) => {
    if (a.length !== b.length) { problems.push(`${what}: length js ${a.length} vs C# ${b.length}`); return; }
    for (let i = 0; i < a.length; i++) {
      if (Array.isArray(b[i])) arr(`${what}[${i}]`, a[i], b[i]); else num(`${what}[${i}]`, a[i], b[i]);
    }
  };
  const ints = (what, a, b) => {
    if (a.length !== b.length) { problems.push(`${what}: length js ${a.length} vs C# ${b.length}`); return; }
    for (let i = 0; i < a.length; i++) { if (Array.isArray(b[i])) ints(`${what}[${i}]`, Array.from(a[i]), b[i]); else int(`${what}[${i}]`, a[i], b[i]); }
  };

  for (const k of Object.keys(ref.settings)) {
    if (k === 'Span') num('settings.Span', js.span, ref.settings.Span);
    else if (k === 'FluxTurns') int('settings.FluxTurns', js.settings.FluxTurns, ref.settings.FluxTurns);
    else num(`settings.${k}`, js.settings[k], ref.settings[k]);
  }
  num('plateThickness', F(cfg.plateThickness), ref.plateThickness);
  int('inputHash.charge', js.inputHash.charge, ref.inputHash.charge);
  int('inputHash.omni', js.inputHash.omni, ref.inputHash.omni);
  for (const which of ['charge', 'omni']) {
    const a = js.sites[which], b = ref.sites[which], w = `sites.${which}`;
    int(`${w}.kind`, a.Kind, b.kind);
    num(`${w}.radius`, a.Radius, b.radius);
    arr(`${w}.axes`, a.Axes, b.axes);
    ints(`${w}.neighbours`, a.Neighbours, b.neighbours);
    arr(`${w}.junctions`, a.Junctions, b.junctions);
    ints(`${w}.junctionSites`, a.JunctionSites, b.junctionSites);
    ints(`${w}.seamsOfJunction`, a.SeamsOfJunction, b.seamsOfJunction);
    ints(`${w}.facesOfSite`, a.FacesOfSite, b.facesOfSite);
    arr(`${w}.corners`, a.Corners, b.corners);
    if (a.Seams.length !== b.seams.length) problems.push(`${w}.seams: ${a.Seams.length} vs ${b.seams.length}`);
    else a.Seams.forEach((e, i) => {
      const r = b.seams[i];
      ints(`${w}.seams[${i}].ends`, [e.A, e.B, e.SiteA, e.SiteB], [r.a, r.b, r.siteA, r.siteB]);
      num(`${w}.seams[${i}].length`, e.Length, r.length);
      arr(`${w}.seams[${i}].points`, e.Points, r.points);
    });
    if (a.Faces.length !== b.faces.length) problems.push(`${w}.faces: ${a.Faces.length} vs ${b.faces.length}`);
    else a.Faces.forEach((f, i) => {
      const r = b.faces[i], fw = `${w}.faces[${i}]`;
      int(`${fw}.site`, f.Site, r.site);
      arr(`${fw}.ring`, f.Ring, r.ring); arr(`${fw}.centre`, f.Centre, r.centre); arr(`${fw}.own`, f.OwnFalloff, r.own);
      arr(`${fw}.siteFalloff`, f.SiteFalloff, r.siteFalloff); num(`${fw}.atCentre`, f.SiteFalloffAtCentre, r.atCentre);
      arr(`${fw}.back`, f.Back, r.back); num(`${fw}.sigma`, f.Sigma, r.sigma);
    });
  }
  js.paths.forEach((p, i) => {
    const r = ref.paths[i];
    arr(`paths[${i}].centre`, p.centre, r.centre); arr(`paths[${i}].strands`, p.strands, r.strands);
    arr(`paths[${i}].sourceOffsets`, p.sourceOffsets, r.sourceOffsets); arr(`paths[${i}].sinkOffsets`, p.sinkOffsets, r.sinkOffsets);
  });
  arr('noise.fbm3', js.noise.fbm3, ref.noise.fbm3);
  arr('noise.hash', js.noise.hash, ref.noise.hash);
  ints('noise.random', js.noise.random, ref.noise.random);
  [287815223 ^ 0x5bd1e995, 899190242 ^ 0x5bd1e995, 2147483647, -2147483648, -12345, 1552075790].forEach((sd, k) => {
    const r = new CA.NetRandom(sd);
    ints('random seed ' + sd, Array.from({ length: 200 }, () => r.next(1000)), ref.noise.randomSeeds[k]);
  });
  arr('gates', js.gates, ref.flux.gates);

  const frameStats = {};
  for (const [name, jsFrames, refFrames, effect] of [['seam', js.seam, ref.seam.frames, 0], ['flux', js.flux, ref.flux.frames, 1]]) {
    let hashMismatch = 0, fullFrames = 0, verts = 0;
    jsFrames.forEach((a, i) => {
      const b = refFrames[i], w = `${name}[${i}]`;
      num(`${w}.t`, a.t, b.t);
      for (const k of ['points', 'legs', 'drawn', 'nv', 'ni']) if (k in b) int(`${w}.${k}`, a[k], b[k]);
      if (a.hp !== b.hp || a.hd !== b.hd || a.hi !== b.hi) { hashMismatch++; if (hashMismatch <= 5) problems.push(`${w}: writer hash differs`); }
      arr(`${w}.light`, a.light, b.light);
      if ('strike' in b) { arr(`${w}.strike`, a.strike, b.strike); arr(`${w}.strikeGated`, a.strikeGated, b.strikeGated); }
      if (a.full) {
        const r = full.get(effect + ':' + i);
        if (!r) { problems.push(`${w}: no reference full frame`); return; }
        fullFrames++;
        verts += r.pos.length / 3;
        arr(`${w}.positions`, a.full.pos, Array.from(r.pos));
        arr(`${w}.data`, a.full.data, Array.from(r.data));
        ints(`${w}.indices`, a.full.idx, Array.from(r.idx));
      }
    });
    frameStats[name] = { frames: jsFrames.length, hashMismatch, fullFrames, verts };
  }
  return { problems, maxErr, compared, frameStats };
}

function report(label, res) {
  const ok = res.problems.length === 0;
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${label}: ${res.compared} values compared, max |error| ${res.maxErr.toExponential(3)}, ` +
    `${res.problems.length} differences; seam ${res.frameStats.seam.hashMismatch}/${res.frameStats.seam.frames} and flux ` +
    `${res.frameStats.flux.hashMismatch}/${res.frameStats.flux.frames} frames with a writer hash mismatch`);
  return ok;
}

const t0 = Date.now();
const CA = loadPort(false);
const main = compare(run(CA));
const ok = report('JS port vs shipped C#', main);
if (!ok) for (const p of main.problems.slice(0, 20)) console.log('      ' + p);
const s = main.frameStats;
const struck = ref.flux.frames.filter((f) => f.strike.some((v) => v > 0)).length;
const struckGated = ref.flux.frames.filter((f) => f.strikeGated.some((v) => v > 0)).length;
console.log(`      full frames compared: seam ${s.seam.fullFrames} (${s.seam.verts} vertices), flux ${s.flux.fullFrames} (${s.flux.verts} vertices); ` +
  `frames with a strike: ${struck} ungated, ${struckGated} gated; ${((Date.now() - t0) / 1000).toFixed(1)} s`);

const negSeed = compare(run(CA, { seamSeed: 8 }));
const negSeedFailed = !report('NEGATIVE CONTROL (seam walk seed 8) - must FAIL', negSeed);
const negConst = compare(run(loadPort(true)));
const negConstFailed = !report('NEGATIVE CONTROL (SampleSpacing 0.0125 -> 0.0126) - must FAIL', negConst);
console.log(negSeedFailed && negConstFailed ? 'PASS  both negative controls fail, so the comparison can see a difference'
  : 'FAIL  a negative control passed: the comparison is blind');
process.exit(ok && negSeedFailed && negConstFailed ? 0 : 1);
