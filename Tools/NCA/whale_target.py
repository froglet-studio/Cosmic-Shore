"""Turn a whale-designer parameter set into the training target, with the SAME generator the page runs.

    python whale_target.py [params.json] [--out results/whale_target]

`params.json` is what the designer's "Send to Claude" saves (the `targets/whale` document: a
`params` object, optionally `note`/`summary`) or a bare params object; omitted, the humpback
defaults. Needs node (whale_model.js is run as-is). Writes, per swim frame, the prism list centred
in the training grid (voxel units, +x head, +y up), plus the report the designer shows:

    <out>/params.json   the merged parameters
    <out>/prisms.json   {grid, frames: [[{p, h, R, dom, tier, part}, ...], ...], report}

Committing <out> means training never needs node.
"""
import argparse
import json
import os
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))

JS = r"""
const W = require(process.argv[1]);
const P = JSON.parse(require('fs').readFileSync(0, 'utf8'));
const r = W.report(P), off = r.offset, g = r.grid;
const round = v => Math.round(v * 1e4) / 1e4;
const frames = r.frames.map(f => f.map(q => ({
  p: q.p.map((v, i) => round(v + off[i] + g[i] / 2)), h: q.h.map(round), R: q.R.map(round), dom: q.dom, tier: q.tier, part: q.part })));
process.stdout.write(JSON.stringify({ params: r.params, grid: g, frames,
  report: { count: r.count, overlapsPerFrame: r.overlapsPerFrame, worstDepth: r.worstDepth, fits: r.fits,
            size: r.size, hmin: r.hmin, hmax: r.hmax, meanAspect: r.meanAspect, census: r.census } }));
"""


def load_params(path):
    if not path:
        return {}
    d = json.load(open(path))
    return d.get("params", d) if isinstance(d, dict) else {}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("params", nargs="?")
    ap.add_argument("--out", default=os.path.join(HERE, "results", "whale_target"))
    a = ap.parse_args()
    res = subprocess.run(["node", "-e", JS, os.path.join(HERE, "whale_model.js")],
                         input=json.dumps(load_params(a.params)), capture_output=True, text=True, check=True)
    d = json.loads(res.stdout)
    os.makedirs(a.out, exist_ok=True)
    json.dump(d["params"], open(os.path.join(a.out, "params.json"), "w"), indent=1)
    json.dump({"grid": d["grid"], "frames": d["frames"], "report": d["report"]}, open(os.path.join(a.out, "prisms.json"), "w"))
    r = d["report"]
    print(f"{r['count']} prisms x {len(d['frames'])} frames, grid {d['grid']}, fits {r['fits']}, "
          f"overlaps/frame {r['overlapsPerFrame']}, census {r['census']}")


if __name__ == "__main__":
    main()
