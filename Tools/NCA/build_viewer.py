"""Bake the trained runs into ONE self-contained HTML page: a live, in-browser copy of
each automaton (nca_core.js, verified against the PyTorch model by verify_js.py), the
training curves, and the figures `growing_nca.py figures` rendered.

    python3 Tools/NCA/build_viewer.py            # -> Tools/NCA/viewer.html
"""
import base64
import html
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from growing_nca import load_emoji  # noqa: E402

EXPERIMENTS = [
    ("growing", "Growing", "Trained from the seed every time. Learns to grow the lizard by step 64–96, but nothing asks it to stop, so past the training window it is free to overgrow or fade."),
    ("persistent", "Persistent", "Trained from a pool of its own earlier results. The lizard becomes an attractor: it grows, then holds its shape for thousands of steps."),
    ("regenerating", "Regenerating", "The pool plus damage: before each training step three samples lose a random disc. The lizard learns to repair itself. Drag across it to test that."),
]
SERIES_VARS = {"growing": "--s1", "persistent": "--s2", "regenerating": "--s3"}


def b64(path):
    with open(path, "rb") as f:
        return base64.b64encode(f.read()).decode()


def loss_chart(runs):
    """Inline SVG: log10 loss vs training step, 50-step moving mean, one line per experiment."""
    W, H, L, R, T, B = 640, 260, 52, 16, 14, 34
    lo, hi = -4.0, -1.0
    steps = max((len(r["loss"]) for r in runs.values()), default=1) - 1
    xmax = max(8000, steps)
    sx = lambda s: L + (W - L - R) * s / xmax
    sy = lambda v: T + (H - T - B) * (hi - v) / (hi - lo)
    out = [f'<svg viewBox="0 0 {W} {H}" role="img" aria-label="Training loss (log10) for the three experiments">']
    for v in np.arange(lo, hi + 0.01, 0.5):
        y = sy(v)
        out.append(f'<line x1="{L}" x2="{W-R}" y1="{y:.1f}" y2="{y:.1f}" class="grid"/>')
        if abs(v - round(v)) < 1e-6:
            out.append(f'<text x="{L-8}" y="{y+4:.1f}" class="tick" text-anchor="end">{v:.0f}</text>')
    for s in range(0, xmax + 1, 2000):
        x = sx(s)
        out.append(f'<text x="{x:.1f}" y="{H-12}" class="tick" text-anchor="middle">{s}</text>')
    out.append(f'<line x1="{sx(2000):.1f}" x2="{sx(2000):.1f}" y1="{T}" y2="{H-B}" class="drop"/>')
    out.append(f'<text x="{sx(2000)+6:.1f}" y="{T+12}" class="tick">lr 2e-3 → 2e-4</text>')
    for key, r in runs.items():
        lg = np.log10(np.maximum(r["loss"], 1e-8))
        k = 50
        sm = np.convolve(lg, np.ones(k) / k, mode="valid") if len(lg) > k else lg
        idx = np.linspace(0, len(sm) - 1, min(400, len(sm))).astype(int)
        pts = " ".join(f"{sx(i + (k-1 if len(lg) > k else 0)):.1f},{sy(max(lo, min(hi, sm[i]))):.1f}" for i in idx)
        out.append(f'<polyline points="{pts}" class="series" style="stroke:var({SERIES_VARS[key]})"/>')
        e = sm[-1]
        out.append(f'<circle cx="{sx(len(lg)-1):.1f}" cy="{sy(max(lo, min(hi, e))):.1f}" r="3.5" style="fill:var({SERIES_VARS[key]})"/>')
    out.append(f'<text x="{W-R}" y="{H-12}" class="tick" text-anchor="end" dx="0" dy="0"></text>')
    out.append("</svg>")
    return "".join(out)


def main():
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", default=os.path.join(HERE, "results"))
    ap.add_argument("--out", default=os.path.join(HERE, "viewer.html"))
    args = ap.parse_args()
    runs = {}
    for key, _, _ in EXPERIMENTS:
        d = os.path.join(args.runs, f"lizard_{key}")
        if not os.path.isfile(os.path.join(d, "weights.json")):
            continue
        r = {"weights": json.load(open(os.path.join(d, "weights.json"))),
             "loss": np.load(os.path.join(d, "loss.npy")),
             "summary": json.load(open(os.path.join(d, "figures", "summary.json"))),
             "cfg": json.load(open(os.path.join(d, "config.json")))}
        for fig in ("growth_strip", "regeneration", "rotation"):
            p = os.path.join(d, "figures", f"{fig}.png")
            r[fig] = b64(p) if os.path.isfile(p) else None
        runs[key] = r
    if not runs:
        sys.exit("no trained runs under Tools/NCA/runs/")

    t = load_emoji("lizard")
    t = np.pad(t, ((16, 16), (16, 16), (0, 0)))
    target_b64 = base64.b64encode((np.clip(t, 0, 1) * 255).round().astype(np.uint8).tobytes()).decode()

    weights_js = json.dumps({k: {"w": r["weights"], "steps": len(r["loss"]) - 1} for k, r in runs.items()},
                            separators=(",", ":"))
    core = open(os.path.join(HERE, "nca_core.js")).read()

    exp_buttons = "".join(
        f'<button type="button" role="tab" id="exp-{k}" data-exp="{k}" aria-selected="false" {"" if k in runs else "disabled"}>'
        f'<span class="swatch" style="background:var({SERIES_VARS[k]})"></span>{label}</button>'
        for k, label, _ in EXPERIMENTS)
    exp_notes = json.dumps({k: note for k, _, note in EXPERIMENTS})

    # Results table: the paper's claims, measured.
    rows = []
    for k, label, _ in EXPERIMENTS:
        if k not in runs:
            continue
        s = runs[k]["summary"]
        ea = s["error_at"]
        cell = lambda v: f"{math.log10(max(v, 1e-9)):+.2f}" if math.isfinite(v) and v < 1e3 else "diverged"
        final_loss = float(np.mean(runs[k]["loss"][-100:]))
        regen = s["regeneration_error_after_300"]
        worst_regen = max(regen.values())
        rows.append(f"<tr><th scope=row><span class=swatch style='background:var({SERIES_VARS[k]})'></span>{label}</th>"
                    f"<td>{len(runs[k]['loss'])-1}</td><td>{cell(final_loss)}</td>"
                    f"<td>{cell(ea['96'])}</td><td>{cell(ea['1000'])}</td><td>{cell(ea['4000'])}</td>"
                    f"<td>{cell(worst_regen)}</td></tr>")

    figs = []
    for k, label, _ in EXPERIMENTS:
        if k not in runs:
            continue
        r = runs[k]
        figs.append(f"""
      <figure class="fig">
        <figcaption><span class="swatch" style="background:var({SERIES_VARS[k]})"></span>{label}: growth from one cell</figcaption>
        <div class="strip"><img src="data:image/png;base64,{r['growth_strip']}" alt="{label} automaton at steps 0 to 4000"></div>
        <div class="ticks mono">0 · 10 · 20 · 30 · 40 · 50 · 60 · 72 · 96 · 200 · 500 · 1000 · 2000 · 4000 steps</div>
      </figure>""")
    regen_fig = ""
    if "regenerating" in runs and runs["regenerating"]["regeneration"]:
        regen_fig = f"""
      <figure class="fig">
        <figcaption><span class="swatch" style="background:var(--s3)"></span>Regenerating: four cuts at step 200, then 10, 25, 50, 100, 200 and 300 steps of repair</figcaption>
        <div class="strip"><img src="data:image/png;base64,{runs['regenerating']['regeneration']}" alt="Regeneration after left, top, corner and disc cuts"></div>
      </figure>"""
    rot_fig = ""
    if "regenerating" in runs and runs["regenerating"]["rotation"]:
        rot_fig = f"""
      <figure class="fig">
        <figcaption><span class="swatch" style="background:var(--s3)"></span>Rotating only the Sobel kernels (0°, 45°, 90°, 135°) rotates the grown lizard, with no retraining</figcaption>
        <div class="strip narrow"><img src="data:image/png;base64,{runs['regenerating']['rotation']}" alt="Lizard grown with perception rotated by 0, 45, 90 and 135 degrees"></div>
      </figure>"""

    page = TEMPLATE
    for k, v in {
        "{{EXP_BUTTONS}}": exp_buttons, "{{EXP_NOTES}}": exp_notes, "{{WEIGHTS}}": weights_js,
        "{{TARGET}}": target_b64, "{{CORE}}": core, "{{CHART}}": loss_chart(runs),
        "{{ROWS}}": "".join(rows), "{{FIGS}}": "".join(figs) + regen_fig + rot_fig,
        "{{DEFAULT}}": "regenerating" if "regenerating" in runs else next(iter(runs)),
    }.items():
        page = page.replace(k, v)
    out = args.out
    with open(out, "w") as f:
        f.write(page)
    print(f"wrote {out} ({len(page)/1024:.0f} KB, runs: {', '.join(runs)})")


TEMPLATE = r"""<title>One-Cell Lizard</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,500;12..96,700&family=Atkinson+Hyperlegible:wght@400;700&family=JetBrains+Mono:wght@400;600&display=swap">
<style>
/* Layout: a lab bench. The dish (live automaton) is the hero; the instrument panel sits
   beside it; the measured record (curves, table, figures) reads below. */
:root {
  --ground: #edf0ea; --surface: #f8faf5; --ink: #1b221d; --muted: #5a645c; --rule: #d2d8cf;
  --accent: #b8501d; --dish: #ffffff;
  --s1: #7a6fb0; --s2: #2f7f86; --s3: #b8501d;
  --display: "Bricolage Grotesque", "Helvetica Neue", Arial, sans-serif;
  --body: "Atkinson Hyperlegible", "Segoe UI", system-ui, sans-serif;
  --mono: "JetBrains Mono", ui-monospace, "SFMono-Regular", Menlo, monospace;
}
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {
  --ground: #111713; --surface: #18201b; --ink: #e2e9e1; --muted: #92a095; --rule: #2b362e;
  --accent: #ef8a4f; --dish: #0b0f0c; --s1: #a79be0; --s2: #5fb8bf; --s3: #ef8a4f; color-scheme: dark } }
:root[data-theme="dark"] {
  --ground: #111713; --surface: #18201b; --ink: #e2e9e1; --muted: #92a095; --rule: #2b362e;
  --accent: #ef8a4f; --dish: #0b0f0c; --s1: #a79be0; --s2: #5fb8bf; --s3: #ef8a4f; color-scheme: dark }
* { box-sizing: border-box }
body { background: var(--ground); color: var(--ink); font: 16px/1.55 var(--body); padding-inline: 16px; padding-block: 28px 64px }
.wrap { max-width: 1080px; margin: 0 auto; display: grid; grid-template-columns: minmax(0, 1fr); gap: 40px }
.wrap > *, .record > *, .panel > *, .dish > * { min-width: 0 }
.mono { font-family: var(--mono) }
header { display: grid; gap: 10px }
.eyebrow { font: 600 12px/1 var(--mono); letter-spacing: .12em; text-transform: uppercase; color: var(--muted) }
h1 { font: 700 clamp(34px, 6vw, 58px)/1.02 var(--display); letter-spacing: -.02em; margin: 0; text-wrap: balance }
h1 em { font-style: normal; color: var(--accent) }
.lede { max-width: 64ch; margin: 0; color: var(--muted) }
.lede a { color: var(--ink) }
h2 { font: 700 22px/1.2 var(--display); margin: 0 0 12px; text-wrap: balance }

.bench { display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(0, 1fr); gap: 28px; align-items: start }
@media (max-width: 820px) { .bench { grid-template-columns: minmax(0, 1fr) } }
.dish { background: var(--surface); border: 1px solid var(--rule); border-radius: 14px; padding: 16px; display: grid; gap: 10px }
.plate { position: relative; aspect-ratio: 1; max-width: 100%; border-radius: 8px; overflow: hidden; background: var(--dish); cursor: crosshair; touch-action: none }
.plate canvas { width: 100%; height: 100%; display: block; image-rendering: pixelated }
.plate .hint { position: absolute; left: 10px; bottom: 8px; font: 12px var(--mono); color: var(--muted); pointer-events: none }
.axis { display: flex; justify-content: space-between; font: 11px var(--mono); color: var(--muted) }
.panel { display: grid; gap: 20px }
.tabs { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); border: 1px solid var(--rule); border-radius: 10px; overflow: hidden }
.tabs button { font: 600 14px var(--body); background: var(--surface); color: var(--ink); border: 0; padding: 10px 6px; cursor: pointer; display: flex; gap: 7px; align-items: center; justify-content: center; border-right: 1px solid var(--rule) }
.tabs button:last-child { border-right: 0 }
.tabs button[aria-selected="true"] { background: var(--ink); color: var(--ground) }
.tabs button:disabled { opacity: .4; cursor: not-allowed }
.tabs button:focus-visible, .btn:focus-visible, input:focus-visible, select:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px }
.swatch { width: 10px; height: 10px; border-radius: 2px; display: inline-block; flex: none; margin-right: 6px; vertical-align: 0 }
.tabs .swatch { margin-right: 0 }
.note { margin: 0; color: var(--muted); font-size: 15px; min-height: 4.6em }
.readouts { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1px; background: var(--rule); border: 1px solid var(--rule); border-radius: 10px; overflow: hidden }
.readouts div { background: var(--surface); padding: 10px 12px; display: grid; gap: 2px }
.readouts dt { font: 600 11px var(--mono); letter-spacing: .08em; text-transform: uppercase; color: var(--muted) }
.readouts dd { margin: 0; font: 600 20px var(--mono); font-variant-numeric: tabular-nums }
.controls { display: grid; gap: 14px }
.row { display: grid; grid-template-columns: 7.5em minmax(0, 1fr) 3.4em; gap: 12px; align-items: center; font-size: 14px }
.row output { font: 13px var(--mono); text-align: right; font-variant-numeric: tabular-nums; color: var(--muted) }
.row select { font: 14px var(--body); padding: 6px; background: var(--surface); color: var(--ink); border: 1px solid var(--rule); border-radius: 6px; grid-column: 2 / 4 }
input[type=range] { width: 100%; accent-color: var(--accent) }
.buttons { display: flex; flex-wrap: wrap; gap: 8px }
.btn { font: 600 14px var(--body); padding: 9px 14px; border-radius: 8px; border: 1px solid var(--rule); background: var(--surface); color: var(--ink); cursor: pointer }
.btn.primary { background: var(--accent); border-color: var(--accent); color: #fff }

.record { display: grid; grid-template-columns: minmax(0, 1fr); gap: 28px }
.chart { background: var(--surface); border: 1px solid var(--rule); border-radius: 14px; padding: 16px; overflow-x: auto }
.chart svg { width: 100%; min-width: 480px; height: auto; display: block }
.chart .grid { stroke: var(--rule); stroke-width: 1 }
.chart .drop { stroke: var(--muted); stroke-dasharray: 3 4; stroke-width: 1 }
.chart .tick { fill: var(--muted); font: 11px var(--mono) }
.chart .series { fill: none; stroke-width: 2; stroke-linejoin: round }
.legend { display: flex; flex-wrap: wrap; gap: 16px; font-size: 13px; color: var(--muted); margin-top: 8px }
.tablewrap { overflow-x: auto; border: 1px solid var(--rule); border-radius: 14px; background: var(--surface) }
table { border-collapse: collapse; width: 100%; font-size: 14px }
th, td { padding: 10px 14px; text-align: right; border-bottom: 1px solid var(--rule); white-space: nowrap }
thead th { font: 600 11px var(--mono); letter-spacing: .06em; text-transform: uppercase; color: var(--muted); vertical-align: bottom }
tbody th { text-align: left; font-weight: 700 }
tbody td { font: 14px var(--mono); font-variant-numeric: tabular-nums }
tbody tr:last-child > * { border-bottom: 0 }
.caption { font-size: 14px; color: var(--muted); max-width: 72ch; margin: 8px 0 0 }
.fig { margin: 0; display: grid; gap: 8px }
.fig figcaption { font-size: 14px; font-weight: 700 }
.strip { overflow-x: auto; background: #fff; border-radius: 8px; border: 1px solid var(--rule) }
.strip img { display: block; width: 100%; min-width: 640px; max-width: none; height: auto; image-rendering: pixelated }
.strip.narrow img { width: 60%; min-width: 420px }
.ticks { font-size: 11px; color: var(--muted) }
@media (prefers-reduced-motion: reduce) { * { scroll-behavior: auto } }
</style>

<div class="wrap">
  <header>
    <div class="eyebrow">Growing Neural Cellular Automata · reproduction</div>
    <h1>A lizard grown from <em>one cell</em></h1>
    <p class="lede">Every pixel below runs the same 8,336-parameter rule, sees only its 3×3 neighbours, and fires at random half the time. Starting from a single live cell, the rule grows the Noto lizard emoji. This is a CPU PyTorch reproduction of <a href="https://distill.pub/2020/growing-ca/">Mordvintsev et al., Distill 2020</a>, running live in your browser.</p>
  </header>

  <section class="bench" aria-label="Live automaton">
    <div class="dish">
      <div class="plate" id="plate"><canvas id="cv" width="72" height="72" aria-label="72 by 72 cell grid"></canvas><span class="hint">drag to cut · double-click to seed</span></div>
      <div class="axis"><span>0</span><span>36</span><span>72 cells</span></div>
    </div>
    <div class="panel">
      <div class="tabs" role="tablist" aria-label="Experiment">{{EXP_BUTTONS}}</div>
      <p class="note" id="note"></p>
      <dl class="readouts">
        <div><dt>Step</dt><dd id="r-step">0</dd></div>
        <div><dt>Alive cells</dt><dd id="r-alive">0</dd></div>
        <div><dt>log₁₀ error</dt><dd id="r-err">–</dd></div>
      </dl>
      <div class="controls">
        <label class="row" for="speed">Steps / frame<input id="speed" type="range" min="1" max="8" value="2"><output id="o-speed">2</output></label>
        <label class="row" for="brush">Cut radius<input id="brush" type="range" min="2" max="20" value="8"><output id="o-brush">8</output></label>
        <label class="row" for="angle">Rotate sensing<input id="angle" type="range" min="0" max="359" value="0"><output id="o-angle">0°</output></label>
        <label class="row" for="view">View<select id="view">
          <option value="rgb">Colour (RGBA channels)</option>
          <option value="alpha">Alive / alpha</option>
        </select></label>
      </div>
      <div class="buttons">
        <button type="button" class="btn primary" id="b-play">Pause</button>
        <button type="button" class="btn" id="b-seed">Restart from seed</button>
        <button type="button" class="btn" id="b-cut">Cut in half</button>
      </div>
    </div>
  </section>

  <section class="record" aria-label="Training record">
    <div>
      <h2>Training</h2>
      <div class="chart">{{CHART}}
        <div class="legend"><span><span class="swatch" style="background:var(--s1)"></span>Growing</span><span><span class="swatch" style="background:var(--s2)"></span>Persistent</span><span><span class="swatch" style="background:var(--s3)"></span>Regenerating</span></div>
      </div>
      <p class="caption">log₁₀ of the pixel MSE against the premultiplied RGBA target after 64–96 steps, 50-step moving mean. Batch 8, Adam, per-variable gradient normalisation, as in the paper.</p>
    </div>
    <div>
      <h2>What each rule does past its training window</h2>
      <div class="tablewrap"><table>
        <thead><tr><th scope=col>Experiment</th><th scope=col>Train steps</th><th scope=col>Final train loss</th><th scope=col>Error @ 96</th><th scope=col>@ 1000</th><th scope=col>@ 4000</th><th scope=col>Worst cut, 300 steps later</th></tr></thead>
        <tbody>{{ROWS}}</tbody>
      </table></div>
      <p class="caption">All values log₁₀ MSE against the target from one stochastic rollout. Training only ever sees steps 64–96. The paper's claim is visible in the last three columns: only the pool experiments hold the lizard at step 4000, and only the damaged-pool experiment recovers from a cut.</p>
    </div>
    {{FIGS}}
  </section>
</div>

<script>
{{CORE}}
const RUNS = {{WEIGHTS}};
const NOTES = {{EXP_NOTES}};
const G = 72;
const TARGET = Uint8Array.from(atob("{{TARGET}}"), c => c.charCodeAt(0));
const cv = document.getElementById('cv'), ctx = cv.getContext('2d'), img = ctx.createImageData(G, G);
const $ = id => document.getElementById(id);
let nca = null, exp = null, stepN = 0, running = true, lastAlive = 0;

const view = $('view');
for (let c = 4; c < 16; c++) { const o = document.createElement('option'); o.value = 'h' + c; o.textContent = 'Hidden channel ' + c; view.appendChild(o); }

function load(key) {
  exp = key;
  nca = makeNCA(RUNS[key].w, G, G);
  nca.seed(); stepN = 0;
  document.querySelectorAll('.tabs button').forEach(b => b.setAttribute('aria-selected', b.dataset.exp === key));
  $('note').textContent = NOTES[key];
  try { localStorage.setItem('nca-exp', key) } catch (e) {}
}
document.querySelectorAll('.tabs button').forEach(b => b.addEventListener('click', () => !b.disabled && load(b.dataset.exp)));

function bgRGB() {
  const c = getComputedStyle(document.documentElement).getPropertyValue('--dish').trim();
  const m = c.match(/^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})/i);
  return m ? [1, 2, 3].map(i => parseInt(m[i], 16) / 255) : [1, 1, 1];
}
let BG = bgRGB();
matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => BG = bgRGB());
new MutationObserver(() => BG = bgRGB()).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });

function error() {
  const s = nca.state; let e = 0;
  for (let i = 0; i < G * G; i++) for (let c = 0; c < 4; c++) { const d = s[i * 16 + c] - TARGET[i * 4 + c] / 255; e += d * d; }
  return e / (G * G * 4);
}
function draw() {
  const s = nca.state, d = img.data, v = view.value, hc = v[0] === 'h' ? +v.slice(1) : -1;
  for (let i = 0; i < G * G; i++) {
    let r, g, b;
    if (v === 'rgb') {
      const a = Math.min(1, Math.max(0, s[i * 16 + 3]));
      r = Math.min(1, Math.max(0, s[i * 16])) + (1 - a) * BG[0];
      g = Math.min(1, Math.max(0, s[i * 16 + 1])) + (1 - a) * BG[1];
      b = Math.min(1, Math.max(0, s[i * 16 + 2])) + (1 - a) * BG[2];
    } else {
      const val = hc < 0 ? s[i * 16 + 3] : s[i * 16 + hc];
      const t = Math.max(-1, Math.min(1, val));
      // diverging: negative -> teal, positive -> accent orange, zero -> dish
      const pos = [0.93, 0.54, 0.31], neg = [0.37, 0.72, 0.75], k = Math.abs(t), c = t >= 0 ? pos : neg;
      r = BG[0] + (c[0] - BG[0]) * k; g = BG[1] + (c[1] - BG[1]) * k; b = BG[2] + (c[2] - BG[2]) * k;
    }
    d[i * 4] = Math.min(255, r * 255); d[i * 4 + 1] = Math.min(255, g * 255); d[i * 4 + 2] = Math.min(255, b * 255); d[i * 4 + 3] = 255;
  }
  ctx.putImageData(img, 0, 0);
  $('r-step').textContent = stepN.toLocaleString();
  $('r-alive').textContent = lastAlive.toLocaleString();
  const e = error(); $('r-err').textContent = e > 0 ? Math.log10(e).toFixed(2) : '–';
}
function frame() {
  if (running) {
    const n = +$('speed').value, ang = +$('angle').value * Math.PI / 180;
    for (let i = 0; i < n; i++) { lastAlive = nca.step({ angle: ang }); stepN++; }
  }
  draw();
  requestAnimationFrame(frame);
}

function cellAt(ev) { const r = cv.getBoundingClientRect(); return [(ev.clientY - r.top) / r.height * G, (ev.clientX - r.left) / r.width * G]; }
let dragging = false;
cv.addEventListener('pointerdown', ev => { dragging = true; cv.setPointerCapture(ev.pointerId); const [y, x] = cellAt(ev); nca.erase(y, x, +$('brush').value); });
cv.addEventListener('pointermove', ev => { if (!dragging) return; const [y, x] = cellAt(ev); nca.erase(y, x, +$('brush').value); });
cv.addEventListener('pointerup', () => dragging = false);
cv.addEventListener('dblclick', ev => { const [y, x] = cellAt(ev); nca.seed(Math.floor(y), Math.floor(x), false); });

for (const [id, out, fmt] of [['speed', 'o-speed', v => v], ['brush', 'o-brush', v => v], ['angle', 'o-angle', v => v + '°']])
  $(id).addEventListener('input', () => $(out).textContent = fmt($(id).value));
$('b-play').addEventListener('click', () => { running = !running; $('b-play').textContent = running ? 'Pause' : 'Run'; });
$('b-seed').addEventListener('click', () => { nca.seed(); stepN = 0; });
$('b-cut').addEventListener('click', () => { const s = nca.state; for (let y = 0; y < G; y++) for (let x = 0; x < G / 2; x++) s.fill(0, (y * G + x) * 16, (y * G + x + 1) * 16); });

let start = '{{DEFAULT}}';
try { const k = localStorage.getItem('nca-exp'); if (k && RUNS[k]) start = k; } catch (e) {}
load(start);
if (matchMedia('(prefers-reduced-motion: reduce)').matches) { $('speed').value = 1; $('o-speed').textContent = 1; }
requestAnimationFrame(frame);
</script>
"""

if __name__ == "__main__":
    main()
