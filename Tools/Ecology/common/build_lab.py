"""Stage the Living Ecology Lab: copy each direction's self-contained viewer into one folder, give each a
doctype and a "<- Lab" link back to the index, so the published lab never strands a viewer.

    python Tools/Ecology/common/build_lab.py <out_dir>

The index page itself is hand-written in common/lab_index.html and copied to <out_dir>/index.html. A viewer is listed with the id of its top bar (a left-anchored flex row every viewer has), and the
back link is inserted as that bar's FIRST child, so it flows with the bar instead of overlapping it.
"""
from __future__ import annotations

import os
import re
import sys

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

VIEWERS = [  # (published name, source relative to Tools/Ecology, id of the viewer's top bar)
    ("substrate.html", "substrate/viewer_sample.html", "ui"),
    ("bestiary.html", "bestiary/bestiary.html", "ui"),
    ("emotion.html", "emotion/results/emotion_viewer.html", "ui"),
    ("builders.html", "builders/viewer.html", "ui"),
    ("hierarchy.html", "hierarchy/results/hierarchy_viewer.html", "ui"),
    ("flora.html", "flora/sandbox.html", "hud"),
    ("flight.html", "flight/index.html", "top"),
    ("living_cell.html", "living_cell/viewer.html", "ui"),
    ("nca.html", "flight/creatures/nca_gallery.html", "hud"),
    ("species.html", "../NCA/results/four_species.html", "top"),
    ("arms.html", "../NCA/results/arms/arms_viewer.html", "ui"),
    ("hybrid.html", "../NCA/hybrid_viewer/hybrid.html", "hud"),
]

PREFIX = '<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">'
CSS = ('<style>.lab-back{font:600 13px/1 system-ui,sans-serif;color:#0b0d14;background:#7fd1ff;text-decoration:none;'
       'padding:7px 11px;border-radius:6px;white-space:nowrap;flex:none;position:relative;z-index:10}'
       '.lab-back:hover{filter:brightness(1.12)}</style>')
LINK = '<a class="lab-back" href="index.html" title="Back to the Living Ecology Lab">&larr; Lab</a>'


def stage(out: str):
    os.makedirs(out, exist_ok=True)
    for name, src, bar in VIEWERS:
        s = open(os.path.join(HERE, src), encoding="utf-8").read()
        if not s[:40].lower().lstrip().startswith("<!doctype"):
            s = PREFIX + s
        tags = list(re.finditer(r'<div id="%s"[^>]*>' % bar, s))
        if len(tags) != 1:
            raise SystemExit(f"{src}: expected exactly one top bar #{bar}, found {len(tags)}")
        s = s[:tags[0].end()] + LINK + s[tags[0].end():]
        i = s.find("<style")
        s = s[:i] + CSS + s[i:]
        with open(os.path.join(out, name), "w", encoding="utf-8") as fh:
            fh.write(s)
        print(f"{name:16s} {len(s):>10,d} bytes")
    with open(os.path.join(HERE, "common", "lab_index.html"), encoding="utf-8") as fh:
        idx = fh.read()
    with open(os.path.join(out, "index.html"), "w", encoding="utf-8") as fh:
        fh.write(idx)


if __name__ == "__main__":
    stage(sys.argv[1])
