#!/usr/bin/env python3
"""Swap the live R67Ark board's converter stage (one set of stills on black) for the pier renders, per colour set:
idle loop, the combine (once), the Omni held. Keeps the board's converter UI as it is; the recipe's steps follow the
lab's morph (4 x 2675 ms = 10.7 s).   python3 patch_live_converter.py <live.html> <blobs.json> <out.html>"""
import json, sys
src, blobs, dst = sys.argv[1:4]
s = open(src).read()
B = json.load(open(blobs))
stage = {st: {"idle": "/_blob/" + B[f"converter_{st}_loop"], "mid": "/_blob/" + B[f"converter_{st}_convert"],
              "done": "/_blob/" + B[f"converter_{st}_done"]} for st in ("Nocturne", "Photic", "DartFrog")}
def rep(a, b):
    global s
    assert s.count(a) == 1, a
    s = s.replace(a, b)
i = s.index("const STAGE = "); j = s.index(";\n", i)
s = s[:i] + "const STAGE = " + json.dumps(stage) + s[j:]
rep("const STEP = 1300;", "const STEP = 2675;")
rep("""  if (st.tab === 0) layers.push({ src: st.running ? STAGE.mid : (st.done ? STAGE.done : STAGE.idle), op: 1, alt: 'The Ark open to the cell, the crystals flying into the reactor' });""",
    """  if (st.tab === 0) SETS.forEach((s, i) => { const G = STAGE[s[0]], on = i === st.set;
   layers.push({ src: G.idle, op: on && !st.running && !st.done ? 1 : 0, alt: on && !st.running && !st.done ? 'From inside the Ark, down the pier into the cell: the four crystals on its round deck, ' + s[1] : '' });
   layers.push({ src: on && st.running ? G.mid + '?run=' + st.run : G.mid, op: on && st.running ? 1 : 0, alt: on && st.running ? 'The omni combine on the pier deck' : '' });
   layers.push({ src: G.done, op: on && st.done && !st.running ? 1 : 0, alt: on && st.done && !st.running ? 'The Omni crystal made, over the pier deck' : '' }); });""")
rep("const secs = Math.round(st.step * 5.5)", "const secs = Math.round(st.step * STEP / 1000)")
rep("clock: '00:' + pad(secs) + ' / 00:22'", "clock: '00:' + pad(secs) + ' / 00:11'")
open(dst, "w").write(s)
print("patched", dst)
