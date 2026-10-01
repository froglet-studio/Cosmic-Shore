"""Turn the elemental-swarm designer's four body plans into training targets, with the SAME generator the page runs.

    python swarm_target.py [targets.json] [--out results/swarm_targets]

`targets.json` is what the designer's "Send all four to Claude" saves (the `targets/swarm`
document: `mass`, `space`, `charge` and `time` parameter objects, optionally `slotMap`/`note`/
`summary`); omitted, the defaults. Needs node (swarm_model.js runs as-is). Per body plan and
animation frame it writes every tadpole realised into its three parts, centred in that plan's
grid (voxels):

    <out>/<kind>.json   {grid, elem, frames: [{units, crystals, prisms, spindles}, ...], report}
    <out>/params.json   the merged parameters of all four plans

A unit is {p, f, n, elem, role, spindle: {len, bend, roll, thick}, prism: {h, tier, slot}} — the
state a learned rule must reach. Units keep their index across frames (unit i in frame k is the
same tadpole in frame k+1), which is what makes a Time runner's lap a real displacement.

`slot` (A/B/C = 0/1/2) is a DOMAIN REGION, not a domain: the loss is meant to take the minimum
over slot->domain assignments, so a plan asks for "two (or three) differently-coloured regions
shaped like this", never "the back must be Jade". `dom` on a rendered prism is only the designer's
preview mapping.

The co-evolution protocol scores ONE rule kit on four seedings, one per plan, each seeded with
that plan's element mix (`report.elements`), and reports the SUM of the four losses.
"""
import argparse
import json
import os
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
KINDS = ("mass", "space", "charge", "time")

JS = r"""
const S = require(process.argv[1]);
const D = JSON.parse(require('fs').readFileSync(0, 'utf8'));
const round = v => Math.round(v * 1e4) / 1e4, r3 = a => a.map(round);
const out = {};
for (const kind of ['mass', 'space', 'charge', 'time']) {
  const P = Object.assign({}, D[kind] || {}); if (D.slotMap) P.slotMap = D.slotMap;
  const r = S.report(kind, P), off = r.offset.map((v, i) => v + r.grid[i] / 2), sh = p => r3(p.map((v, i) => v + off[i]));
  out[kind] = { params: r.params, grid: r.grid, elem: r.elem, name: r.name,
    report: { units: r.units, elements: r.elements, share: r.share.map(round), majorityOk: r.majorityOk,
      speed: r.speed.map(round), states: r.states, slots: r.slots, domainsUsed: r.domainsUsed, roles: r.roles,
      overlapsPerFrame: r.overlapsPerFrame, worstDepth: round(r.worstDepth || 0), fits: r.fits, size: r3(r.size) },
    frames: r.frames.map(g => ({
      units: g.units.map(u => ({ p: sh(u.p), f: r3(u.f), n: r3(u.n), elem: u.elem, role: u.role,
        spindle: { len: round(u.spindle.len), bend: round(u.spindle.bend), roll: round(u.spindle.roll), thick: round(u.spindle.thick) },
        prism: { h: r3(u.prism.h), tier: u.prism.tier || 0, slot: u.prism.slot || 0 } })),
      crystals: g.crystals.map(c => ({ p: sh(c.p), r: round(c.r), elem: c.elem, unit: c.unit })),
      prisms: g.prisms.map(q => ({ p: sh(q.p), h: r3(q.h), R: r3(q.R), slot: q.slot, dom: q.dom, tier: q.tier, unit: q.unit })),
      spindles: g.spindles.map(s => ({ pts: s.pts.map(sh), r: round(s.r) })) })) };
}
process.stdout.write(JSON.stringify(out));
"""

ELEM = ["Charge", "Mass", "Space", "Time"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("targets", nargs="?")
    ap.add_argument("--out", default=os.path.join(HERE, "results", "swarm_targets"))
    a = ap.parse_args()
    d = json.load(open(a.targets)) if a.targets else {}
    payload = {k: d.get(k, {}) for k in KINDS}
    if d.get("slotMap"):
        payload["slotMap"] = d["slotMap"]
    res = subprocess.run(["node", "-e", JS, os.path.join(HERE, "swarm_model.js")],
                         input=json.dumps(payload), capture_output=True, text=True, check=True)
    out = json.loads(res.stdout)
    os.makedirs(a.out, exist_ok=True)
    json.dump({k: v["params"] for k, v in out.items()}, open(os.path.join(a.out, "params.json"), "w"), indent=1)
    for kind, v in out.items():
        json.dump({"name": v["name"], "elem": v["elem"], "grid": v["grid"], "frames": v["frames"], "report": v["report"]},
                  open(os.path.join(a.out, kind + ".json"), "w"))
        r = v["report"]
        mix = " ".join(f"{e} {n}" for e, n in zip(ELEM, r["elements"]) if n)
        fastest = ELEM[max(range(4), key=lambda i: r["speed"][i])]
        print(f"{kind:6s} {v['name']:11s} {r['units']:3d} units [{mix}] majority {ELEM[v['elem']]} "
              f"{round(100 * r['share'][v['elem']])}% ok={r['majorityOk']}, regions {r['domainsUsed']}, "
              f"tiers {r['states']}, fastest {fastest}, grid {v['grid']} fits={r['fits']}, "
              f"overlaps/frame {r['overlapsPerFrame']}")


if __name__ == "__main__":
    main()
