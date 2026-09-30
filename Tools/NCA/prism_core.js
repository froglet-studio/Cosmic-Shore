// Prism particles - decode a particle's state into the prism it shows (prism_render.decode /
// prism_table; verify_particle_js.py --prism holds the two equal).
//   channels 16-18 domain logits (Jade, Ruby, Gold)   19-22 tier logits (plain, danger, shield, super)
//   23-25 half-extents  H0 * exp(SPAN * tanh(c))       26-31 rotation, 6D (two columns, Gram-Schmidt)
//   channel 3 presence
const PRISM = { H0: 0.9, SPAN: 0.4, SHIELD: 3.0,
  DOMAINS: ["Jade", "Ruby", "Gold"], TIERS: ["plain", "danger", "shield", "super"] };

function decodePrism(s, C, i, out) {
  const o = i * C;
  let dom = 0; for (let k = 1; k < 3; k++) if (s[o + 16 + k] > s[o + 16 + dom]) dom = k;
  let tier = 0; for (let k = 1; k < 4; k++) if (s[o + 19 + k] > s[o + 19 + tier]) tier = k;
  for (let k = 0; k < 3; k++) out.h[k] = PRISM.H0 * Math.exp(PRISM.SPAN * Math.tanh(s[o + 23 + k]));
  const a = [s[o + 26] + 1, s[o + 27], s[o + 28]], b = [s[o + 29], s[o + 30] + 1, s[o + 31]];
  const an = Math.max(1e-6, Math.hypot(a[0], a[1], a[2]));
  const r1 = a.map(v => v / an);
  const dot = r1[0] * b[0] + r1[1] * b[1] + r1[2] * b[2];
  const bb = b.map((v, k) => v - dot * r1[k]);
  const bn = Math.max(1e-6, Math.hypot(bb[0], bb[1], bb[2]));
  const r2 = bb.map(v => v / bn);
  const r3 = [r1[1] * r2[2] - r1[2] * r2[1], r1[2] * r2[0] - r1[0] * r2[2], r1[0] * r2[1] - r1[1] * r2[0]];
  // R row-major with columns r1, r2, r3 (the prism's local axes in world)
  for (let r = 0; r < 3; r++) { out.R[r * 3] = r1[r]; out.R[r * 3 + 1] = r2[r]; out.R[r * 3 + 2] = r3[r]; }
  out.dom = dom; out.tier = tier; out.alpha = Math.min(1, Math.max(0, s[o + 3]));
  return out;
}
if (typeof module !== "undefined") module.exports = { PRISM, decodePrism };
