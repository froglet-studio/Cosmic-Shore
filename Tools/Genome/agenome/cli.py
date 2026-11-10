"""Command line: render / score / sheet genomes.

  python3 -m agenome.cli render seed.json OUTDIR [--frames 60]      -> OUTDIR/frames.npy, clip.mp4, sheet.png
  python3 -m agenome.cli sheet  OUT.png seed1.json seed2.json ...     -> one row of 4 frames per genome
  python3 -m agenome.cli score  seed.json STATS.npz                    -> fitness distances vs a target set
Run from Tools/Genome (or put it on PYTHONPATH). Needs Xvfb (started automatically) and moderngl.
"""
import argparse
import json
import os
import sys

import numpy as np


def main(argv=None):
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd")
    r = sub.add_parser("render")
    r.add_argument("genome")
    r.add_argument("out")
    r.add_argument("--frames", type=int, default=60)
    s = sub.add_parser("sheet")
    s.add_argument("out")
    s.add_argument("genomes", nargs="+")
    s.add_argument("--frames", type=int, default=45)
    sc = sub.add_parser("score")
    sc.add_argument("genome")
    sc.add_argument("stats")
    a = ap.parse_args(argv)
    from .genome import Genome
    from .render import Renderer, save_clip, contact_sheet
    if a.cmd == "render":
        g = Genome.load(a.genome)
        fr = Renderer().render(g, a.frames)
        save_clip(fr, a.out, {"source": "genome", "genome": os.path.abspath(a.genome), "zbytes": g.size()})
        contact_sheet(fr, os.path.join(a.out, "sheet.png"))
        print(a.out, fr.shape, "zbytes", g.size())
    elif a.cmd == "sheet":
        from PIL import Image, ImageDraw
        R = Renderer()
        rows = []
        for p in a.genomes:
            try:
                g = Genome.load(p)
                fr = R.render(g, a.frames)
                picks = np.linspace(0, len(fr) - 1, 4).astype(int)
                row = np.concatenate([fr[i] for i in picks], 1)
            except Exception as e:  # a broken seed shows as a red row
                row = np.zeros((144, 256 * 4, 3), np.uint8)
                row[..., 0] = 120
                print("FAIL", p, e, file=sys.stderr)
            im = Image.fromarray(row)
            ImageDraw.Draw(im).text((4, 4), os.path.basename(p), fill=(255, 255, 0))
            rows.append(np.asarray(im))
        Image.fromarray(np.concatenate(rows, 0)).save(a.out)
        print(a.out)
    elif a.cmd == "score":
        from .features import TargetStats, score
        g = Genome.load(a.genome)
        fr = Renderer().render(g, 60)
        st = TargetStats.load(a.stats)
        print(json.dumps({k: round(v, 3) for k, v in score(fr, st).items()}))
    else:
        ap.print_help()


if __name__ == "__main__":
    main()
