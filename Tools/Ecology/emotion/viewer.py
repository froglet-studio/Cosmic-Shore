"""The emotion viewer: archetypes, searched creatures and game species in one self-contained HTML file,
each with the probe's reading AND a human rating panel (the labels in this direction are ours; this is how
they get checked). Ratings live in the browser (localStorage) and export as JSON for
`ratings.py` to compare against the probe.

    python viewer.py            -> results/emotion_viewer.html
"""
from __future__ import annotations

import json
import os
import sys

import numpy as np

from archetypes import FAMILIES
from critter import factory
from probe import EmotionProbe, EMOTIONS
from sealed import SEALED
from sim import run, HERE
from species import GAME

sys.path.insert(0, os.path.join(HERE, ".."))
from common.viewer import TPL  # noqa: E402

PANEL = r"""<div id="emo" style="position:fixed;right:12px;top:56px;width:250px;background:#121726ee;border:1px solid #2a3150;
border-radius:8px;padding:10px;z-index:3;font-size:12px">
<div style="color:var(--mute);margin-bottom:4px">probe reading (blind to the label)</div><div id="bars"></div>
<div style="color:var(--mute);margin:10px 0 4px">how does it make YOU feel? (pick one)</div>
<div id="rate" style="display:flex;flex-wrap:wrap;gap:4px"></div>
<div id="rated" style="color:var(--mute);margin-top:6px"></div>
<label style="display:block;margin-top:6px;color:var(--mute)"><input type="checkbox" id="blind"> blind mode (hide reading until rated)</label>
<button id="exp" style="margin-top:8px;width:100%;background:#151a28;color:var(--fg);border:1px solid #2a3150;border-radius:6px;padding:4px">export my ratings (JSON)</button>
</div>
<div id="info"></div>
<script>
const EMO=__EMO__, KEY='cs-emotion-ratings-v1';
function ratings(){try{return JSON.parse(localStorage.getItem(KEY)||'{}')}catch(e){return {}}}
function save(r){try{localStorage.setItem(KEY,JSON.stringify(r))}catch(e){}}
function drawPanel(){
  const r=RUNS[+document.getElementById('run').value||0], m=r.meta, R=ratings(), mine=R[m.label];
  const blind=document.getElementById('blind').checked && !mine;
  document.getElementById('bars').innerHTML = blind ? '<i style="color:var(--mute)">hidden until you rate</i>' :
    EMO.map(e=>{const p=(m.probe&&m.probe[e])||0;return `<div style="display:flex;align-items:center;gap:6px"><span style="width:62px">${e}</span><span style="flex:1;background:#1c2236;height:8px;border-radius:4px"><span style="display:block;height:8px;border-radius:4px;width:${Math.round(p*100)}%;background:${e==m.truth?'#7fd1ff':'#8a90b0'}"></span></span><span style="width:30px;text-align:right">${p.toFixed(2)}</span></div>`}).join('')
    + (m.truth?`<div style="color:var(--mute);margin-top:4px">authored as: ${m.truth}</div>`:'');
  document.getElementById('rate').innerHTML = EMO.map(e=>`<button data-e="${e}" style="flex:1 0 30%;background:${mine==e?'#2b4b6b':'#151a28'};color:var(--fg);border:1px solid #2a3150;border-radius:6px;padding:3px">${e}</button>`).join('')
    + `<button data-e="none" style="flex:1 0 30%;background:${mine=='none'?'#2b4b6b':'#151a28'};color:var(--fg);border:1px solid #2a3150;border-radius:6px;padding:3px">none / neutral</button>`;
  document.querySelectorAll('#rate button').forEach(b=>b.onclick=()=>{const R=ratings();R[m.label]=b.dataset.e;save(R);drawPanel()});
  document.getElementById('rated').textContent = Object.keys(R).length+' of '+RUNS.length+' runs rated';
}
document.getElementById('blind').onchange=drawPanel;
document.getElementById('exp').onclick=()=>{const b=new Blob([JSON.stringify({ratings:ratings(),runs:RUNS.map(r=>({label:r.meta.label,truth:r.meta.truth||null,probe:r.meta.probe}))},null,1)],{type:'application/json'});
  const a=document.createElement('a');a.href=URL.createObjectURL(b);a.download='emotion_ratings.json';a.click()};
document.getElementById('run').addEventListener('change',drawPanel); setTimeout(drawPanel,50);
</script>"""


def runs():
    pr = EmotionProbe.load(); out = []

    def add(label, fac, truth, seed, viewer="hover", note=""):
        f, rec = run(fac, seed, viewer, seconds=24.0, record=True)
        rec.every = 3
        sc = pr.score(f)
        path = os.path.join(HERE, "results", "_tmp_run.json")
        rec.frames = rec.frames[::2]
        rec.save(path, dict(label=label, truth=truth, probe=sc["p"], note=f"{note}  probe: {sc['top']}"))
        out.append(json.load(open(path))); os.remove(path)
        print(label, sc["top"], flush=True)

    for emo, fams in FAMILIES.items():
        for F in fams:
            add(f"archetype {emo} / {F.__name__}", F, emo, 21, note=F.__doc__.split(".")[0])
    for emo, F in SEALED.items():
        add(f"sealed {emo} / {F.__name__}", F, emo, 22, note="(sealed test family) " + F.__doc__.split(".")[0])
    sp = os.path.join(HERE, "results", "search.json")
    if os.path.exists(sp):
        for emo, r in json.load(open(sp)).items():
            add(f"searched -> {emo} (critter)", factory(np.array(r["cma"]["theta"])), emo, 23,
                note=f"one parametric family, parameters found by CMA-ES for '{emo}'")
    for name, fac in GAME.items():
        for v in ("hover", "cruise"):
            add(f"game {name} vs {v} pilot", fac, None, 24, v, note=f"today's species, modelled from shipped numbers ({v} viewer)")
    return out


if __name__ == "__main__":
    R = runs()
    html = TPL.replace('<div id="info"></div>', PANEL.replace("__EMO__", json.dumps(list(EMOTIONS))))
    html = html.replace("__TITLE__", "Emotion probe").replace("__DATA__", json.dumps(R, separators=(",", ":")))
    out = os.path.join(HERE, "results", "emotion_viewer.html")
    open(out, "w").write(html)
    print(out, os.path.getsize(out) // 1024, "KB")
