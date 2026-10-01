"""Turn the tadpole-swarm designer's parameters into training targets, with the SAME generator the page runs.

    python tadpole_target.py [targets.json] [--out results/tadpole_targets]

`targets.json` is what the designer's "Send both to Claude" saves (the `targets/tadpoles`
document: `whale` and `jelly` parameter objects, optionally `note`/`summary`); omitted, the
defaults. Needs node (tadpole_model.js runs as-is). Per target and swim frame it writes every
unit realised into its three parts, centred in that target's grid (voxels):

    <out>/<kind>.json   {grid, frames: [{units, crystals, prisms, spindles}, ...], report}
    <out>/params.json   the merged parameters of both targets

where a unit is {p, f, n, elem, spindle: {len, bend, roll, thick}, prism: {h, dom, tier, roll}, part}
(the state a learned rule must reach) and crystals/prisms/spindles are the rendered geometry.
Committing <out> means training never needs node.
"""
import argparse
import json
import os
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))

JS = r"""
const T = require(process.argv[1]);
const D = JSON.parse(require('fs').readFileSync(0, 'utf8'));
const round = v => Math.round(v * 1e4) / 1e4, r3 = a => a.map(round);
const out = {};
for (const kind of ['whale', 'jelly']) {
  const r = T.report(kind, D[kind] || {}), off = r.offset.map((v, i) => v + r.grid[i] / 2), sh = p => r3(p.map((v, i) => v + off[i]));
  out[kind] = { params: r.params, grid: r.grid, report: { units: r.units, elements: r.elements, parts: r.parts, census: r.census,
      overlapsPerFrame: r.overlapsPerFrame, worstDepth: r.worstDepth, fits: r.fits, size: r.size },
    frames: r.frames.map(g => ({
      units: g.units.map(u => ({ p: sh(u.p), f: r3(u.f), n: r3(u.n), elem: u.elem, part: u.part,
        spindle: { len: round(u.spindle.len), bend: round(u.spindle.bend), roll: round(u.spindle.roll), thick: round(u.spindle.thick) },
        prism: { h: r3(u.prism.h), dom: u.prism.dom, tier: u.prism.tier, roll: round(u.prism.roll || 0) } })),
      crystals: g.crystals.map(c => ({ p: sh(c.p), r: round(c.r), elem: c.elem })),
      prisms: g.prisms.map(q => ({ p: sh(q.p), h: r3(q.h), R: r3(q.R), dom: q.dom, tier: q.tier })),
      spindles: g.spindles.map(s => ({ pts: s.pts.map(sh), r: round(s.r) })) })) };
}
process.stdout.write(JSON.stringify(out));
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("targets", nargs="?")
    ap.add_argument("--out", default=os.path.join(HERE, "results", "tadpole_targets"))
    a = ap.parse_args()
    d = json.load(open(a.targets)) if a.targets else {}
    res = subprocess.run(["node", "-e", JS, os.path.join(HERE, "tadpole_model.js")],
                         input=json.dumps({k: d.get(k, {}) for k in ("whale", "jelly")}), capture_output=True, text=True, check=True)
    out = json.loads(res.stdout)
    os.makedirs(a.out, exist_ok=True)
    json.dump({k: v["params"] for k, v in out.items()}, open(os.path.join(a.out, "params.json"), "w"), indent=1)
    for kind, v in out.items():
        json.dump({"grid": v["grid"], "frames": v["frames"], "report": v["report"]}, open(os.path.join(a.out, kind + ".json"), "w"))
        r = v["report"]
        print(f"{kind}: {r['units']} units {dict(zip(['Charge', 'Mass', 'Space', 'Time'], r['elements']))} x {len(v['frames'])} frames, "
              f"grid {v['grid']}, fits {r['fits']}, overlaps/frame {r['overlapsPerFrame']}")
    print(f"jelly/whale units: {out['jelly']['report']['units']}/{out['whale']['report']['units']}")


if __name__ == "__main__":
    main()
