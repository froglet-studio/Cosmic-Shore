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
    ("swim", "Swimming", "Trained on an 8-frame loop instead of a still. Each sample picks its own phase but must advance one frame every 8 steps, so the cells keep time in their hidden channels with no global clock. Cuts still heal."),
]
SERIES_VARS = {"growing": "--s1", "persistent": "--s2", "regenerating": "--s3", "swim": "--s4"}


def b64(path):
    with open(path, "rb") as f:
        return base64.b64encode(f.read()).decode()


def loss_chart(runs, floor=None):
    """Inline SVG: log10 loss vs training step, 50-step moving mean, one line per experiment."""
    W, H, L, R, T, B = 640, 260, 52, 16, 14, 34
    lo, hi = -5.0, -1.0
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
    if floor is not None:
        y = sy(floor)
        out.append(f'<line x1="{L}" x2="{W-R}" y1="{y:.1f}" y2="{y:.1f}" class="floor" style="stroke:var(--s4)"/>')
        out.append(f'<text x="{W-R}" y="{y-6:.1f}" class="tick" text-anchor="end">best any still image can do on the swim loop</text>')
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
        for fig in ("growth_strip", "regeneration", "rotation", "loop_strip", "damage"):
            p = os.path.join(d, "figures", f"{fig}.png")
            r[fig] = b64(p) if os.path.isfile(p) else None
        p = os.path.join(d, "figures", "loop.gif")
        r["loop_gif"] = b64(p) if os.path.isfile(p) else None
        p = os.path.join(d, "frames.npy")
        r["frames"] = np.load(p) if os.path.isfile(p) else None
        runs[key] = r
    if not runs:
        sys.exit("no trained runs under Tools/NCA/runs/")

    t = np.pad(load_emoji("lizard"), ((16, 16), (16, 16), (0, 0)))[None]
    enc = lambda a: base64.b64encode((np.clip(a, 0, 1) * 255).round().astype(np.uint8).tobytes()).decode()
    targets = {k: {"n": 1 if r["frames"] is None else len(r["frames"]),
                   "d": enc(t if r["frames"] is None else r["frames"])} for k, r in runs.items()}
    floor = None
    if "swim" in runs:
        f = runs["swim"]["frames"]
        floor = math.log10(float(((f - f.mean(0, keepdims=True)) ** 2).mean()))

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
        if k not in runs or "error_at" not in runs[k]["summary"]:
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
        if k not in runs or not runs[k]["growth_strip"]:
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

    anim = ""
    if "swim" in runs:
        r, sm = runs["swim"], runs["swim"]["summary"]
        lg = lambda v: f"{math.log10(max(v, 1e-9)):+.2f}"
        tempo = lambda v: f"{v:.2f} steps / frame" if v and math.isfinite(v) and v > 0 else "no steady tempo"
        spf = tempo(sm["measured_steps_per_frame"])
        dspf = tempo(sm["after_damage_steps_per_frame"])
        anim = f"""
    <div class="anim" id="swim">
      <h2>Adding time: a lizard that swims</h2>
      <p class="caption">The same 8,336-parameter cell, trained on an 8-frame loop (a travelling body wave generated from the emoji, head still, tail widest). The loss checks the rollout every 8 steps and asks for consecutive frames starting from whichever frame fits best, so each lizard chooses its phase and must then keep moving. Below, one lizard from a single seed, left, beside the target frame it currently matches, right.</p>
      <div class="animrow">
        <figure class="fig"><div class="strip gifbox"><img src="data:image/gif;base64,{r['loop_gif']}" alt="The trained automaton swimming beside the matching target frame"></div></figure>
        <div class="tablewrap"><table class="kv"><tbody>
          <tr><th scope=row>Target tempo</th><td>{sm['period_target_steps_per_frame']} steps / frame</td></tr>
          <tr><th scope=row>Measured tempo, steps 200–3000</th><td>{spf}</td></tr>
          <tr><th scope=row>Distinct frames visited</th><td>{sm['distinct_frames_visited_after_200']} of {sm['frames']}</td></tr>
          <tr><th scope=row>Error vs best-matching frame</th><td>{lg(sm['best_frame_error_mean_after_200'])}</td></tr>
          <tr><th scope=row>Error vs frame its own clock predicts</th><td>{lg(sm['clock_predicted_frame_error_mean'])}</td></tr>
          <tr><th scope=row>Best any still image can do</th><td>{lg(sm['static_best_image_error'])}</td></tr>
          <tr><th scope=row>Tail cut at step 400, error 600 steps later</th><td>{lg(sm['after_damage_error_at_600'])}</td></tr>
          <tr><th scope=row>Tempo after the cut</th><td>{dspf}</td></tr>
        </tbody></table></div>
      </div>
      <figure class="fig">
        <figcaption><span class="swatch" style="background:var(--s4)"></span>One loop from step 200, every 8 steps (top), over the target frame each one matches (bottom)</figcaption>
        <div class="strip"><img src="data:image/png;base64,{r['loop_strip']}" alt="Eight consecutive automaton states over their matching target frames"></div>
      </figure>
      <figure class="fig">
        <figcaption><span class="swatch" style="background:var(--s4)"></span>Tail quarter cut at step 400, then 20, 50, 100, 200, 400 and 600 steps later</figcaption>
        <div class="strip"><img src="data:image/png;base64,{r['damage']}" alt="The swimming lizard regrowing its cut tail"></div>
      </figure>
    </div>"""

    bench3d, script3d = build_3d(args.runs)
    benchp, scriptp = build_particles(args.runs)
    benchpr, scriptpr = build_prisms(args.runs)
    status = build_status(args.runs)

    page = TEMPLATE
    for k, v in {
        "{{EXP_BUTTONS}}": exp_buttons, "{{EXP_NOTES}}": exp_notes, "{{WEIGHTS}}": weights_js,
        "{{TARGETS}}": json.dumps(targets), "{{CORE}}": core, "{{CHART}}": loss_chart(runs, floor),
        "{{ANIM}}": anim, "{{BENCH3D}}": bench3d, "{{SCRIPT3D}}": script3d,
        "{{BENCHP}}": benchp, "{{SCRIPTP}}": scriptp, "{{STATUS}}": status,
        "{{BENCHPRISM}}": benchpr, "{{SCRIPTPRISM}}": scriptpr, "{{RELATED}}": RELATED,
        "{{ROWS}}": "".join(rows), "{{FIGS}}": "".join(figs) + regen_fig + rot_fig,
        "{{DEFAULT}}": "regenerating" if "regenerating" in runs else next(iter(runs)),
    }.items():
        page = page.replace(k, v)
    out = args.out
    with open(out, "w") as f:
        f.write(page)
    print(f"wrote {out} ({len(page)/1024:.0f} KB, runs: {', '.join(runs)})")


def build_3d(runs_root):
    """The 3D swim: a live, orbitable automaton (nca3d_core.js, verified by verify_js3d.py),
    the target frame it currently matches, and the measured record. Empty if not trained."""
    d = os.path.join(runs_root, "lizard3d_swim")
    if not os.path.isfile(os.path.join(d, "weights.json")):
        return "", ""
    w = json.load(open(os.path.join(d, "weights.json")))
    fr = np.load(os.path.join(d, "frames.npy")).astype(np.float32)
    sm = json.load(open(os.path.join(d, "figures", "summary.json")))
    img = lambda f: b64(os.path.join(d, "figures", f))
    lg = lambda v: f"{math.log10(max(v, 1e-9)):+.2f}"
    tempo = lambda v: f"{v:.2f} steps / frame" if v and math.isfinite(v) and v > 0 else "no steady tempo"
    frames_b64 = base64.b64encode((np.clip(fr, 0, 1) * 255).round().astype(np.uint8).tobytes()).decode()
    D, H, W = w["D"], w["H"], w["W"]
    bench = f"""
  <section class="bench3d" aria-labelledby="h3d">
    <div class="h3dhead">
      <div class="eyebrow">Extension · one more spatial dimension</div>
      <h2 id="h3d">The lizard in three dimensions</h2>
      <p class="caption">The same cell with one more axis: it senses its 3×3×3 neighbourhood (identity plus x, y and z gradients, 10,384 parameters), fires at random, and keeps time in its hidden channels. The target is the emoji inflated into a body and swimming a <em>helical</em> wave: the tail sweeps sideways and up-and-down a quarter-cycle apart, so its tip traces a circle, a motion a flat lizard cannot make. {D}×{H}×{W} voxels, running live below.</p>
    </div>
    <div class="bench">
      <div class="dish">
        <div class="plate plate3d" id="plate3"><canvas id="cv3" width="560" height="560" aria-label="Live 3D automaton, drag to orbit, click to cut"></canvas><span class="hint">drag to orbit · click to cut</span></div>
      </div>
      <div class="panel">
        <dl class="readouts">
          <div><dt>Step</dt><dd id="r3-step">0</dd></div>
          <div><dt>Alive voxels</dt><dd id="r3-alive">0</dd></div>
          <div><dt>log₁₀ error</dt><dd id="r3-err">–</dd></div>
          <div><dt>Frame</dt><dd id="r3-frame">–</dd></div>
        </dl>
        <div class="controls">
          <label class="row" for="speed3">Steps / frame<input id="speed3" type="range" min="1" max="4" value="1"><output id="o-speed3">1</output></label>
          <label class="row" for="spin3">Auto-orbit<input id="spin3" type="checkbox" checked><output></output></label>
        </div>
        <div class="buttons">
          <button type="button" class="btn primary" id="b3-play">Pause</button>
          <button type="button" class="btn" id="b3-seed">Restart from seed</button>
          <button type="button" class="btn" id="b3-tail">Cut the tail</button>
        </div>
        <figure class="fig tgt3"><figcaption>Target frame it matches now</figcaption><canvas id="cv3t" width="220" height="220" aria-label="Matching target frame"></canvas></figure>
      </div>
    </div>
    <div class="animrow">
      <figure class="fig"><figcaption><span class="swatch" style="background:var(--s4)"></span>Turntable: one lizard (left) beside the target frame it matches (right)</figcaption><div class="strip gifbox"><img src="data:image/gif;base64,{img('turntable.gif')}" alt="3D automaton swimming while the camera orbits"></div></figure>
      <div class="tablewrap"><table class="kv"><tbody>
        <tr><th scope=row>Target tempo</th><td>{sm['period_target_steps_per_frame']} steps / frame</td></tr>
        <tr><th scope=row>Measured tempo, steps 200–2000</th><td>{tempo(sm['measured_steps_per_frame'])}</td></tr>
        <tr><th scope=row>Distinct frames visited</th><td>{sm['distinct_frames_visited_after_200']} of {sm['frames']}</td></tr>
        <tr><th scope=row>Error vs best-matching frame</th><td>{lg(sm['best_frame_error_mean_after_200'])}</td></tr>
        <tr><th scope=row>Error vs frame its own clock predicts</th><td>{lg(sm['clock_predicted_frame_error_mean'])}</td></tr>
        <tr><th scope=row>Best any still volume can do</th><td>{lg(sm['static_best_image_error'])}</td></tr>
        <tr><th scope=row>Best vs worst frame gap (log₁₀)</th><td>{sm['best_worst_frame_margin_log10']:.2f}</td></tr>
        <tr><th scope=row>Tail ball cut at step 400, error 600 steps later</th><td>{lg(sm['after_damage_error_at_600'])}</td></tr>
        <tr><th scope=row>Tempo after the cut</th><td>{tempo(sm['after_damage_steps_per_frame'])}</td></tr>
      </tbody></table></div>
    </div>
    <figure class="fig"><figcaption><span class="swatch" style="background:var(--s4)"></span>Growth from one voxel: steps 0, 16, 32, 48, 64, 96</figcaption><div class="strip"><img src="data:image/png;base64,{img('growth_strip.png')}" alt="3D lizard growing from a single voxel"></div></figure>
    <figure class="fig"><figcaption><span class="swatch" style="background:var(--s4)"></span>One loop from step 200, every 8 steps (top), over the target frame each one matches (bottom)</figcaption><div class="strip"><img src="data:image/png;base64,{img('loop_strip.png')}" alt="Eight consecutive 3D states over their matching target frames"></div></figure>
    <figure class="fig"><figcaption><span class="swatch" style="background:var(--s4)"></span>A ball cut through the tail half at step 400, then 25, 50, 100, 200 and 600 steps later</figcaption><div class="strip"><img src="data:image/png;base64,{img('damage.png')}" alt="3D lizard regrowing after a spherical cut"></div></figure>
  </section>"""
    core3d = open(os.path.join(HERE, "nca3d_core.js")).read()
    script = SCRIPT3D.replace("{{CORE3D}}", core3d).replace("{{W3D}}", json.dumps(w, separators=(",", ":"))) \
        .replace("{{F3D}}", frames_b64).replace("{{K3D}}", str(len(fr)))
    return bench, script


def build_status(root):
    """'Where things stand': one row per strand, headline read from that run's own summary.json,
    so the page can never claim a result the committed run does not carry."""
    def sm(name):
        p = os.path.join(root, name, "figures", "summary.json")
        return json.load(open(p)) if os.path.isfile(p) else None
    def st(name):
        p = os.path.join(root, name, "status.json")
        return json.load(open(p)) if os.path.isfile(p) else None
    lg = lambda v: f"10<sup>{math.log10(max(v, 1e-9)):.2f}</sup>"
    rows = []
    g = sm("lizard_regenerating")
    if g:
        rows.append(("done", "Grid lizard", "#grid", "Grows from one cell, holds its shape and regrows any cut",
                     f"holds at {lg(g['error_at']['4000'])}"))
    s = sm("lizard_swim")
    if s:
        rows.append(("done", "Grid swim", "#swim", "The same cell trained on an eight-frame body wave",
                     f"{s['measured_steps_per_frame']:.2f} steps / frame, beats a still image"))
    s3 = sm("lizard3d_swim")
    if s3:
        rows.append(("done", "3D swim", "h3d", "One more spatial dimension: a helical stroke in a 22×44×44 volume",
                     f"{s3['measured_steps_per_frame']:.2f} steps / frame, heals a tail cut"))
    p = sm("particle_regenerating")
    if p:
        sh = p.get("shift_corrected", {})
        held = f", shape {lg(sh['3000']['error'])} at step 3000" if "3000" in sh else ""
        rows.append(("done", "Collision lizard", "hp", "No grid: particles that only sense and push their neighbours",
                     f"grown by step 96{held}, heals a quarter cut"))
    ps, pst = sm("particle_swim2d"), st("particle_swim2d")
    if ps:
        state = "run" if (pst and pst.get("training")) else "done"
        where = f"training, showing step {pst['step']} of {pst['of']}" if state == "run" else "trained"
        if pst and pst.get("note"):
            where += f" ({pst['note']})"
        tempo = ps.get("measured_steps_per_frame")
        sh = ps.get("shift_corrected", {}).get("1000")
        extra = f", drifts {sh['drift_px']:.0f} px per 1000 steps" if sh else ""
        rows.append((state, "Collision swim", "hp", f"A bolder stroke (amplitude 16); {where}",
                     (f"{tempo:.1f} steps / frame" if tempo else "no steady tempo yet") + extra))
    pname = "prism_swim3d_gpu" if sm("prism_swim3d_gpu") else "prism_swim3d"
    pp, ppt = sm(pname), st(pname)
    if pp:
        state = "run" if (ppt and ppt.get("training")) else "done"
        where = f"training, showing step {ppt['step']} of {ppt['of']}" if state == "run" else "trained"
        if ppt and ppt.get("note"):
            where += f" ({ppt['note']})"
        tempo = pp.get("measured_steps_per_frame")
        rows.append((state, "Prism 3D swim", "hprism", f"3D particles that can only be Cosmic Shore prisms; {where}",
                     f"{tempo:.1f} steps / frame" if tempo else "no steady tempo yet"))
    else:
        live = os.path.join(os.path.dirname(os.path.abspath(root)), "runs", "prism_swim3d", "state.json")
        if os.path.isfile(live):
            n = json.load(open(live))["step"]
            rows.append(("run", "Prism 3D swim", "", "3D particles that can only be Cosmic Shore prisms; "
                         "training (a GPU run is also set up: gpu_run.py)", f"step {n}, no snapshot yet"))
        else:
            rows.append(("todo", "Prism 3D swim", "", "3D particles that can only be Cosmic Shore prisms", "not started"))
    rows.append(("done", "Is this new?", "hrelated", "What else has been built, and what this adds", "closest: Kim et al. 2026"))
    label = {"done": "Done", "run": "Training", "todo": "Next"}
    body = "".join(
        f'<li class="ledger-row"><span class="chip chip-{s_}">{label[s_]}</span>'
        f'<span class="ledger-name">{f"<a href={chr(34)}#{a.lstrip(chr(35))}{chr(34)}>{n}</a>" if a else n}</span>'
        f'<span class="ledger-what">{w}</span><span class="ledger-num">{v}</span></li>'
        for s_, n, a, w, v in rows)
    import datetime
    day = datetime.datetime.utcnow().strftime("%-d %B %Y")
    return f"""
  <section class="ledger" aria-labelledby="hstatus">
    <div class="ledger-head"><h2 id="hstatus">Where things stand</h2><span class="mono ledger-date">updated {day}</span></div>
    <ul class="ledger-list">{body}</ul>
  </section>"""


def build_prisms(runs_root):
    """The prism swim: 3D particles whose visible state is a Cosmic Shore prism, running live in
    three.js (particle_core.js + prism_core.js, verified by verify_particle_js.py --prism)."""
    import prism_render as pr
    d = os.path.join(runs_root, "prism_swim3d_gpu")          # a GPU result (gpu_run.py) wins over the CPU run
    if not os.path.isfile(os.path.join(d, "figures", "summary.json")):
        d = os.path.join(runs_root, "prism_swim3d")
    if not (os.path.isfile(os.path.join(d, "weights.json")) and os.path.isfile(os.path.join(d, "figures", "summary.json"))):
        return "", ""
    w = json.load(open(os.path.join(d, "weights.json")))
    sm = json.load(open(os.path.join(d, "figures", "summary.json")))
    stp = os.path.join(d, "status.json")
    status = json.load(open(stp)) if os.path.isfile(stp) else None
    img = lambda f: b64(os.path.join(d, "figures", f))
    has = lambda f: os.path.isfile(os.path.join(d, "figures", f))
    lg = lambda v: f"{math.log10(max(v, 1e-9)):+.2f}"
    tempo = lambda v: f"{v:.2f} steps / frame" if v and math.isfinite(v) and v > 0 else "no steady tempo"
    census = sm.get("prism_census_t200", {})
    cen = " · ".join(f"{k.replace('-', ' ')} {v}" for k, v in sorted(census.items(), key=lambda kv: -kv[1]))
    ext = sm.get("prism_half_extent_t200", {})
    pa = sm["particles_at"]; late = max(pa, key=int)
    rows = [("Target tempo", f"{sm['period_target_steps_per_frame']} steps / frame"),
            (f"Measured tempo, steps 200–{late}", tempo(sm.get("measured_steps_per_frame"))),
            (f"Prisms, step 96 / {late}", f"{pa.get('96', '–')} / {pa[late]}"),
            ("Error vs best-matching frame, after step 200", lg(sm["best_frame_error_mean_after_200"])),
            ("Best any still volume can do", lg(sm["static_best_image_error"])),
            (f"Quarter cut at 400 ({sm['damage_removed_particles']} prisms): error before", lg(sm["error_before_damage"])),
            ("… 20 / 100 / 200 steps later", " / ".join(lg(sm["after_damage_error_at"][s]) for s in ("20", "100", "200"))),
            ("What the prisms chose to be, step 200", cen or "–"),
            ("Half-extent, mean (min–max) · mean aspect", f"{ext['mean']:.2f} ({ext['min']:.2f}–{ext['max']:.2f}) · {ext['mean_aspect']:.2f}" if ext else "–")]
    table = "".join(f"<tr><th scope=row>{a}</th><td>{b}</td></tr>" for a, b in rows)
    note = ""
    if status and status.get("training"):
        note = f'<p class="caption"><strong>Still training</strong> — this is step {status["step"]} of {status["of"]}{": " + status["note"] if status.get("note") else ""}. The page updates as it improves.</p>'
    figs = []
    if has("growth_strip.png"):
        figs.append(("From one prism: steps 0, 16, 32, 48, 64, 96, 200", "growth_strip.png", "Prism lizard growing from one particle"))
    if has("loop_strip.png"):
        figs.append((f"One stroke from step 200, every {w['period']} steps (top), over the target frame each one matches (bottom, the palette-quantised voxel target)", "loop_strip.png", "Consecutive prism-swim states over their matching target frames"))
    if has("damage.png"):
        figs.append(("Every prism in one quarter removed at step 400, then 20, 50, 100, 200, 400 and 600 steps later", "damage.png", "Prism lizard regrowing its cut quarter"))
    figh = "".join(f'<figure class="fig"><figcaption><span class="swatch" style="background:var(--s4)"></span>{c}</figcaption>'
                   f'<div class="strip"><img src="data:image/png;base64,{img(f)}" alt="{a}"></div></figure>' for c, f, a in figs)
    gif = (f'<figure class="fig"><figcaption><span class="swatch" style="background:var(--s4)"></span>Growing and swimming, rendered as exact prisms with each tier\'s base face and fresnel rim</figcaption>'
           f'<div class="strip gifbox"><img src="data:image/gif;base64,{img("loop.gif")}" alt="Prism lizard growing and swimming"></div></figure>') if has("loop.gif") else ""
    swatches = "".join(
        f'<li><span class="pswatch" style="background:rgb({",".join(str(int(v * 255)) for v in pr.palette_colours()[a, b])})"></span>'
        f'{pr.DOMAINS[a]} {pr.TIERS[b] if pr.TIERS[b] != "super" else "super-shield"}</li>'
        for a in range(3) for b in range(4))
    bench = f"""
  <section class="benchp" aria-labelledby="hprism">
    <div class="h3dhead">
      <div class="eyebrow">Extension · collision, in three dimensions, in the game's vocabulary</div>
      <h2 id="hprism">The lizard made of prisms</h2>
      <p class="caption">The collision automaton in a volume, with one constraint: every particle is a Cosmic Shore prism, and all it can show is what a prism can show. It picks a <strong>domain</strong> (Jade, Ruby, Gold), a <strong>tier</strong> (a plain box, a danger box, a shield octahedron or a super-shield stella octangula — the game's own shapes, at the game's 3× circumscribing scale, so a shield really does cost 4.5× the volume), an <strong>orientation</strong> and <strong>three half-extents</strong>. Colour and tier are discrete: training renders the argmax and sends its gradient through the softmax, so the rule can never show a prism the game could not draw. The target is the helical swim with its colours quantised to the twelve appearances. Running live below; drag to orbit.</p>
      {note}
    </div>
    <div class="bench">
      <div class="dish">
        <div class="plate plate3d" id="platepr"><canvas id="cvpr" width="560" height="560" aria-label="Live prism automaton, drag to orbit"></canvas><span class="hint">drag to orbit</span></div>
        <ul class="palette" aria-label="The twelve prism appearances">{swatches}</ul>
      </div>
      <div class="panel">
        <dl class="readouts">
          <div><dt>Step</dt><dd id="rpr-step">0</dd></div>
          <div><dt>Prisms</dt><dd id="rpr-n">0</dd></div>
          <div><dt>Buds</dt><dd id="rpr-bud">0</dd></div>
          <div><dt>Shields</dt><dd id="rpr-sh">0</dd></div>
        </dl>
        <div class="controls">
          <label class="row" for="speedpr">Steps / frame<input id="speedpr" type="range" min="1" max="4" value="1"><output id="o-speedpr">1</output></label>
          <label class="row" for="spinpr">Auto-orbit<input id="spinpr" type="checkbox" checked><output></output></label>
        </div>
        <div class="buttons">
          <button type="button" class="btn primary" id="bpr-play">Pause</button>
          <button type="button" class="btn" id="bpr-seed">Restart from seed</button>
          <button type="button" class="btn" id="bpr-cut">Cut a quarter</button>
          <button type="button" class="btn" id="bpr-tail">Cut the tail</button>
        </div>
        <p class="census mono" id="rpr-census" aria-live="off"></p>
        <div class="tablewrap"><table class="kv"><tbody>{table}</tbody></table></div>
      </div>
    </div>
    {gif}
    {figh}
  </section>"""
    grid = list(np.load(os.path.join(d, "frames.npy"), mmap_mode="r").shape[1:4])      # (D, H, W)
    pdata = {"w": w, "colours": pr.palette_colours().round(4).tolist(), "grid": grid}
    script = SCRIPTPRISM.replace("{{COREP}}", open(os.path.join(HERE, "particle_core.js")).read()) \
        .replace("{{PRISMCORE}}", open(os.path.join(HERE, "prism_core.js")).read()) \
        .replace("{{PRDATA}}", json.dumps(pdata, separators=(",", ":")))
    return bench, script


RELATED = """
  <section class="related" aria-labelledby="hrelated">
    <h2 id="hrelated">Is this new?</h2>
    <p class="caption">A search of the literature, closest first. The particle automaton itself is not new: Kim et al. built essentially the same model independently and published it this year. What this page adds on top of it appears to be new: growth by budding, a learned periodic stroke on free particles, and a primitive vocabulary taken from a game.</p>
    <ol class="rel">
      <li><strong><a href="https://arxiv.org/abs/2601.16096">Neural Particle Automata</a></strong> — Kim, Pajouheshgar, Süsstrunk, Jakob, Park, 2026 (SIGGRAPH). NCA on free particles with corrected SPH perception, a learned position update, a Gaussian-splat loss and regeneration; in 3D each particle decodes a rotated, anisotropic Gaussian. <em>Not there:</em> a fixed particle count (no budding), no designed collision, no animated target, free Gaussians rather than a discrete vocabulary.</li>
      <li><strong><a href="https://arxiv.org/abs/2301.10497">E(n)-equivariant Graph NCA</a></strong> — Gala, Grattarola, Quaeghebeur, TMLR 2024. Isotropic graph NCA with coordinate updates; pattern formation, not a rendered body.</li>
      <li><strong><a href="https://arxiv.org/abs/2110.14237">Learning Graph Cellular Automata</a></strong> — Grattarola, Livi, Alippi, NeurIPS 2021. Learned rules on arbitrary graphs, including a boids imitation task.</li>
      <li><strong><a href="https://pmc.ncbi.nlm.nih.gov/articles/PMC12577699/">Sensorimotor Lenia</a></strong> — Hamon et al., Science Advances 2025. Gradient-searched Lenia creatures that move and reconstitute after damage: the nearest thing to self-maintaining locomotion, on a grid and without a target animation.</li>
      <li><strong><a href="https://arxiv.org/abs/2211.11417">DyNCA</a></strong> (CVPR 2023) and <strong><a href="https://meshnca.github.io/">Mesh NCA</a></strong> (SIGGRAPH 2024) — Pajouheshgar et al. NCA trained on motion, as dynamic textures on grids and meshes rather than a body grown from a seed.</li>
      <li><strong><a href="https://github.com/Something94807/anim_nca">AnimNCA</a></strong> — an unreviewed hobby repository: a grid NCA that loops a walk cycle on fixed frame slots. Close to the grid swim here, without the phase-free loss or learned tempo.</li>
      <li><strong><a href="https://arxiv.org/abs/2103.08737">Growing 3D Artefacts and Functional Machines</a></strong> — Sudhakaran et al., ALIFE 2021. 3D grid NCA over ~50 discrete Minecraft block types: the precedent for a constrained vocabulary, static and on a grid.</li>
      <li><strong><a href="https://google-research.github.io/self-organising-systems/particle-lenia/">Particle Lenia</a></strong> (Mordvintsev, Niklasson, Randazzo 2022) and Particle Life — particle life from hand-designed energies, not trained toward a target.</li>
      <li><strong>Engineering morphogenesis of cell clusters with differentiable programming</strong> — Deshpande et al., 2024. Differentiable cells that divide, optimised toward growth goals: the precedent for division, without an image loss.</li>
    </ol>
  </section>"""


PARTICLE_RUNS = [("regenerating", "Lizard", "particle_regenerating"), ("swim2d", "Swimming", "particle_swim2d")]


def build_particles(runs_root):
    """The collision automaton: a live 2D particle lizard (particle_core.js, verified by
    verify_particle_js.py) you can cut, its render as the loss sees it, and the record."""
    runs = {}
    for key, label, name in PARTICLE_RUNS:
        d = os.path.join(runs_root, name)
        if not (os.path.isfile(os.path.join(d, "weights.json")) and os.path.isfile(os.path.join(d, "figures", "summary.json"))):
            continue
        fr = np.load(os.path.join(d, "frames.npy")).astype(np.float32)
        stp = os.path.join(d, "status.json")
        status = json.load(open(stp)) if os.path.isfile(stp) else None
        if status and status.get("training"):
            label = f"{label} · training, step {status['step']}"
        runs[key] = {"label": label, "status": status, "dir": d, "w": json.load(open(os.path.join(d, "weights.json"))),
                     "sm": json.load(open(os.path.join(d, "figures", "summary.json"))),
                     "K": len(fr), "f": base64.b64encode((np.clip(fr, 0, 1) * 255).round().astype(np.uint8).tobytes()).decode()}
    if not runs:
        return "", ""
    lg = lambda v: f"{math.log10(max(v, 1e-9)):+.2f}"
    img = lambda r, f: b64(os.path.join(r["dir"], "figures", f))
    tabs = "".join(f'<button type="button" role="tab" data-prun="{k}" aria-selected="false">{r["label"]}</button>'
                   for k, r in runs.items())
    figs, tables = [], []

    def loopfig(r):
        if r["K"] == 1 or not os.path.isfile(os.path.join(r["dir"], "figures", "loop_strip.png")):
            return ""
        return (f'<figure class="fig"><figcaption><span class="swatch" style="background:var(--s2)"></span>'
                f'One stroke from step 200, every {r["w"]["period"]} steps (top), over the target frame each one matches (bottom)</figcaption>'
                f'<div class="strip"><img src="data:image/png;base64,{img(r, "loop_strip.png")}" alt="Consecutive particle-swim states over their matching target frames"></div></figure>')
    for k, r in runs.items():
        sm = r["sm"]
        pa = sm["particles_at"]; late = max(pa, key=int)
        rows = [(f"Particles, step 96 / {late}", f"{pa.get('96', '–')} / {pa[late]}"),
                ("Error at step 96 (grown)", lg(sm['best_frame_error_at']['96']))]
        sh = sm.get("shift_corrected", {})
        if sh:
            ks = [k_ for k_ in ("1000", "3000") if k_ in sh]
            rows += [(f"Shape held at step {' / '.join(ks)}, drift removed", " / ".join(lg(sh[k_]['error']) for k_ in ks)),
                     (f"Body drift by step {' / '.join(ks)}", " / ".join(f"{sh[k_]['drift_px']:.1f}" for k_ in ks) + " px")]
        rows += [(f"Error in place, steps 200–{late}, mean", lg(sm['best_frame_error_mean_after_200'])),
                (f"Quarter cut at 400 ({sm['damage_removed_particles']} particles): error before", lg(sm['error_before_damage'])),
                ("… 20 / 100 / 200 steps later", " / ".join(lg(sm['after_damage_error_at'][s]) for s in ('20', '100', '200')))]
        if r["K"] > 1:
            rows += [("Target tempo", f"{sm['period_target_steps_per_frame']} steps / frame"),
                     ("Measured tempo", f"{sm['measured_steps_per_frame']:.2f} steps / frame" if sm.get('measured_steps_per_frame') else "no steady tempo"),
                     ("Best any still image can do", lg(sm['static_best_image_error']))]
        body = "".join(f"<tr><th scope=row>{a}</th><td>{b}</td></tr>" for a, b in rows)
        tables.append(f'<div class="tablewrap" data-pshow="{k}"><table class="kv"><tbody>{body}</tbody></table></div>')
        figs.append(f"""<div class="pfigs" data-pshow="{k}">
      <figure class="fig"><figcaption><span class="swatch" style="background:var(--s2)"></span>From one particle: steps 0, 16, 32, 48, 64, 96, 200 — the loss's view (top) and the particles themselves (bottom; grey = dormant buds)</figcaption><div class="strip"><img src="data:image/png;base64,{img(r, 'growth_strip.png')}" alt="Particle lizard growing from one particle"></div></figure>
      <figure class="fig"><figcaption><span class="swatch" style="background:var(--s2)"></span>Every particle in the lower-right quarter removed at step 400, then 20, 50, 100, 200, 400 and 600 steps later</figcaption><div class="strip"><img src="data:image/png;base64,{img(r, 'damage.png')}" alt="Particle lizard regrowing its cut quarter"></div></figure>
      {loopfig(r)}
    </div>""")
    bench = f"""
  <section class="benchp" aria-labelledby="hp">
    <div class="h3dhead">
      <div class="eyebrow">Extension · from cellular to collision</div>
      <h2 id="hp">The lizard as a collision automaton</h2>
      <p class="caption">No grid. Each particle has a position and the same sixteen channels; each step it senses only the particles within three units, and the learned rule returns a state change and a velocity. Designed physics does the rest: close pairs push apart, a particle with no living neighbour dies, and a visible particle short of neighbours buds a dormant child. The loss reads the particles through a soft splat onto the same 72×72 target. Running live below — drag across it to cut particles out.</p>
    </div>
    <div class="bench">
      <div class="dish">
        <div class="plate" id="platep"><canvas id="cvp" width="576" height="576" aria-label="Live particle automaton, drag to cut"></canvas><span class="hint">drag to cut · double-click to seed</span></div>
      </div>
      <div class="panel">
        {'<div class="tabs ptabs" role="tablist" aria-label="Particle experiment">' + tabs + '</div>' if len(runs) > 1 else ''}
        <dl class="readouts">
          <div><dt>Step</dt><dd id="rp-step">0</dd></div>
          <div><dt>Particles</dt><dd id="rp-n">0</dd></div>
          <div><dt id="rp-vis-l">Visible</dt><dd id="rp-vis">0</dd></div>
          <div><dt>log₁₀ error</dt><dd id="rp-err">–</dd></div>
        </dl>
        <div class="controls">
          <label class="row" for="speedp">Steps / frame<input id="speedp" type="range" min="1" max="6" value="2"><output id="o-speedp">2</output></label>
          <label class="row" for="brushp">Cut radius<input id="brushp" type="range" min="2" max="16" value="6"><output id="o-brushp">6</output></label>
          <label class="row" for="viewp">View<select id="viewp"><option value="dots">Particles</option><option value="splat">What the loss sees</option></select><output></output></label>
        </div>
        <div class="buttons">
          <button type="button" class="btn primary" id="bp-play">Pause</button>
          <button type="button" class="btn" id="bp-seed">Restart from seed</button>
          <button type="button" class="btn" id="bp-cut">Cut a quarter</button>
        </div>
        {''.join(tables)}
      </div>
    </div>
    {''.join(figs)}
    <div class="h3dhead">
      <h2>Why the first particle lizard was a blob</h2>
      <p class="caption">The first runs trained stably and grew a pale diagonal ellipse: the right colour and heading, no legs. Each run below removes one suspect (log₁₀ training loss at the step shown). The culprit was the gradient each particle senses: summed over whatever neighbours happen to be nearby, it is wrong by up to 91% even for a straight ramp, so on free particles it is noise and the answer to noise is an average. The least-squares gradient from particle fluid simulation is exact on any arrangement.</p>
    </div>
    <div class="diag"><table><thead><tr><th scope="col">Run</th><th scope="col">Step 250</th><th scope="col">500</th><th scope="col">750</th><th scope="col">What it shows</th></tr></thead><tbody>
      <tr><th scope="row">Free particles, speed cap 0.6</th><td>−1.84</td><td>−1.91</td><td>−1.91</td><td>the blob</td></tr>
      <tr><th scope="row">Free particles, speed cap 0.15 / 0</th><td>−1.81 / −1.83</td><td>−1.87 / −1.90</td><td>–</td><td>not motion</td></tr>
      <tr><th scope="row">Fit the render straight to the lizard</th><td colspan="3">−3.5 on a lattice, −4.6 free</td><td>not the renderer</td></tr>
      <tr><th scope="row">Fixed jittered lattice, grid rules</th><td>−1.79</td><td>−1.93</td><td>−2.27</td><td>grows legs: the sensing can learn it</td></tr>
      <tr><th scope="row">Same lattice, re-jittered every time</th><td>−1.83</td><td>−1.99</td><td>−2.04</td><td>a random arrangement slows it</td></tr>
      <tr class="win"><th scope="row">Free particles, corrected gradient</th><td>−1.96</td><td>−2.13</td><td>−2.21</td><td>the blob is gone</td></tr>
    </tbody></table></div>
  </section>"""
    core = open(os.path.join(HERE, "particle_core.js")).read()
    pdata = {k: {"w": r["w"], "K": r["K"], "f": r["f"]} for k, r in runs.items()}
    script = SCRIPTP.replace("{{COREP}}", core).replace("{{PDATA}}", json.dumps(pdata, separators=(",", ":"))) \
        .replace("{{PDEFAULT}}", next(iter(runs)))
    return bench, script


SCRIPTP = r"""<script>
{{COREP}}
(function () {
  const PD = {{PDATA}};
  const $ = id => document.getElementById(id);
  const cv = $('cvp'), g = cv.getContext('2d'), G = 72, S = cv.width / G;
  const off = document.createElement('canvas'); off.width = G; off.height = G;
  const og = off.getContext('2d'), oimg = og.createImageData(G, G);
  let key, ca, W, frames, K, step = 0, running = true, errTxt = '–';
  const img = new Float32Array(G * G * 4);
  const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
  function css(n) { return getComputedStyle(document.documentElement).getPropertyValue(n).trim(); }
  function rgb(c) { const e = document.createElement('div'); e.style.color = c; document.body.appendChild(e);
    const m = getComputedStyle(e).color.match(/\d+(\.\d+)?/g).map(Number); e.remove(); return m.slice(0, 3); }
  let BG = [255, 255, 255], MUTED = [150, 150, 150];
  function theme() { BG = rgb(css('--dish') || '#fff'); MUTED = rgb(css('--muted') || '#888'); }
  function load(k) {
    key = k; const d = PD[k]; W = d.w.world; K = d.K;
    const raw = Uint8Array.from(atob(d.f), c => c.charCodeAt(0));
    frames = new Float32Array(raw.length); for (let i = 0; i < raw.length; i++) frames[i] = raw[i] / 255;
    ca = makeParticleNCA(d.w); ca.seed([G / 2, G / 2]); step = 0;
    document.querySelectorAll('.ptabs [data-prun]').forEach(b => b.setAttribute('aria-selected', b.dataset.prun === k));
    document.querySelectorAll('[data-pshow]').forEach(e => e.hidden = e.dataset.pshow !== k);
  }
  // the same Gaussian splat as particle_nca.splat (premultiplied RGBA, cell centre k + 0.5)
  function splat() {
    img.fill(0);
    const sg = W.sigma, rad = Math.ceil(2.2 * sg), inv = 1 / (2 * sg * sg), C = ca.C;
    for (let i = 0; i < ca.cap; i++) {
      if (!ca.act[i]) continue;
      const px = ca.pos[i * 2], py = ca.pos[i * 2 + 1], bx = Math.floor(px), by = Math.floor(py);
      for (let oy = -rad; oy <= rad; oy++) for (let ox = -rad; ox <= rad; ox++) {
        const cx = bx + ox, cy = by + oy; if (cx < 0 || cy < 0 || cx >= G || cy >= G) continue;
        const dx = cx + 0.5 - px, dy = cy + 0.5 - py, w = Math.exp(-(dx * dx + dy * dy) * inv), o = (cy * G + cx) * 4;
        for (let c = 0; c < 4; c++) img[o + c] += w * ca.s[i * C + c];
      }
    }
  }
  let bestK = -1;
  function error() {
    let best = Infinity; const n = G * G * 4;
    for (let k = 0; k < K; k++) { let e = 0; const o = k * n; for (let i = 0; i < n; i++) { const d = img[i] - frames[o + i]; e += d * d; }
      if (e / n < best) { best = e / n; bestK = k; } }
    return best;
  }
  function draw() {
    const view = $('viewp').value;
    if (view === 'splat') {
      for (let i = 0; i < G * G; i++) { const a = Math.min(1, Math.max(0, img[i * 4 + 3]));
        for (let c = 0; c < 3; c++) oimg.data[i * 4 + c] = Math.min(255, BG[c] * (1 - a) + 255 * Math.min(1, Math.max(0, img[i * 4 + c])));
        oimg.data[i * 4 + 3] = 255; }
      og.putImageData(oimg, 0, 0); g.imageSmoothingEnabled = false; g.drawImage(off, 0, 0, cv.width, cv.height); return;
    }
    g.fillStyle = `rgb(${BG})`; g.fillRect(0, 0, cv.width, cv.height);
    const C = ca.C;
    for (let i = 0; i < ca.cap; i++) {
      if (!ca.act[i]) continue;
      const a = Math.min(1, Math.max(0, ca.s[i * C + 3])), x = ca.pos[i * 2] * S, y = ca.pos[i * 2 + 1] * S;
      let col, r;
      if (a <= 0.1) { col = `rgba(${MUTED},0.45)`; r = 0.32 * S; }
      else { const c = [0, 1, 2].map(k => Math.min(255, 255 * Math.max(0, ca.s[i * C + k]) / a) | 0);   // the particle's own colour (un-premultiplied)
             col = `rgb(${c})`; r = 0.6 * S; }
      g.fillStyle = col; g.beginPath(); g.arc(x, y, r, 0, 6.2832); g.fill();
    }
  }
  function readouts() {
    let vis = 0; for (let i = 0; i < ca.cap; i++) if (ca.act[i] && ca.s[i * ca.C + 3] > 0.1) vis++;
    $('rp-step').textContent = step; $('rp-n').textContent = ca.count(); $('rp-err').textContent = errTxt;
    $('rp-vis-l').textContent = K > 1 ? 'Frame' : 'Visible'; $('rp-vis').textContent = K > 1 ? (bestK < 0 ? '–' : bestK + 1) : vis;
  }
  function frame() {
    if (running) for (let i = 0; i < +$('speedp').value; i++) { ca.step(); step++; }
    splat();
    if (step % 4 === 0 || !running) { const e = error(); errTxt = e > 0 ? Math.log10(e).toFixed(2) : '–'; }
    draw(); readouts();
    if (visibleOnPage) requestAnimationFrame(frame); else pending = false;
  }
  let visibleOnPage = true, pending = true;
  new IntersectionObserver(es => { visibleOnPage = es[0].isIntersecting;
    if (visibleOnPage && !pending) { pending = true; requestAnimationFrame(frame); } }).observe(cv);
  function toWorld(ev) { const r = cv.getBoundingClientRect(); return [(ev.clientX - r.left) / r.width * G, (ev.clientY - r.top) / r.height * G]; }
  let cutting = false;
  cv.addEventListener('pointerdown', ev => { cutting = true; cv.setPointerCapture(ev.pointerId); ca.eraseBall(toWorld(ev), +$('brushp').value); });
  cv.addEventListener('pointermove', ev => { if (cutting) ca.eraseBall(toWorld(ev), +$('brushp').value); });
  cv.addEventListener('pointerup', () => cutting = false);
  cv.addEventListener('dblclick', ev => { ca.seed(toWorld(ev)); step = 0; });
  $('bp-play').onclick = () => { running = !running; $('bp-play').textContent = running ? 'Pause' : 'Play'; };
  $('bp-seed').onclick = () => { ca.seed([G / 2, G / 2]); step = 0; };
  $('bp-cut').onclick = () => { for (let i = 0; i < ca.cap; i++) if (ca.act[i] && ca.pos[i * 2] > G / 2 && ca.pos[i * 2 + 1] > G / 2) {
      ca.act[i] = 0; ca.s.fill(0, i * ca.C, (i + 1) * ca.C); } };
  for (const id of ['speedp', 'brushp']) { const o = $('o-' + id); const f = () => o.textContent = $(id).value; $(id).oninput = f; f(); }
  document.querySelectorAll('.ptabs [data-prun]').forEach(b => b.onclick = () => load(b.dataset.prun));
  matchMedia('(prefers-color-scheme: dark)').addEventListener('change', theme);
  new MutationObserver(theme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  theme(); load('{{PDEFAULT}}');
  if (reduce) { running = false; $('bp-play').textContent = 'Play'; for (let i = 0; i < 200; i++) { ca.step(); step++; } }
  requestAnimationFrame(frame);
})();
</script>"""


SCRIPT3D = r"""<script>
{{CORE3D}}
(function () {
  const W3 = {{W3D}}, K3 = {{K3D}};
  const F3 = Uint8Array.from(atob("{{F3D}}"), c => c.charCodeAt(0));
  const D = W3.D, H = W3.H, W = W3.W, N = D * H * W, C = W3.channel_n;
  const $ = id => document.getElementById(id);
  const cv = $('cv3'), ctx = cv.getContext('2d'), cvt = $('cv3t'), ctxt = cvt.getContext('2d');
  const nca = makeNCA3D(W3); nca.seed();
  let az = 0.9, tilt = 0.95, running = true, stepN = 0, alive = 0, spin = true, drawn = [];
  const A = new Float32Array(N);
  const L = (() => { const l = [-0.35, -0.55, 0.75], n = Math.hypot(...l); return l.map(v => v / n); })();
  const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (reduce) { spin = false; $('spin3').checked = false; }

  function cssColor(name) {
    const c = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return c || '#fff';
  }
  // Voxel splatting, back to front, orthographic, same camera as nca3d.render().
  function draw(g, size, get, keep) {
    const zoom = 0.40, R = zoom * Math.hypot(D, H, W), ppu = size / (2 * R);
    const st = Math.sin(tilt), ct = Math.cos(tilt), ca = Math.cos(az), sa = Math.sin(az);
    const fwd = [st * ca, st * sa, -ct];
    const hint = [-ca, -sa, 0];
    let right = [fwd[1] * hint[2] - fwd[2] * hint[1], fwd[2] * hint[0] - fwd[0] * hint[2], fwd[0] * hint[1] - fwd[1] * hint[0]];
    const rn = Math.hypot(...right); right = right.map(v => v / rn);
    const up = [right[1] * fwd[2] - right[2] * fwd[1], right[2] * fwd[0] - right[0] * fwd[2], right[0] * fwd[1] - right[1] * fwd[0]];
    for (let i = 0; i < N; i++) A[i] = Math.min(1, Math.max(0, get(i, 3)));
    const list = [];
    for (let z = 0; z < D; z++) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = (z * H + y) * W + x; if (A[i] < 0.04) continue;
      const px = x + 0.5 - W / 2, py = y + 0.5 - H / 2, pz = z + 0.5 - D / 2;
      list.push([px * fwd[0] + py * fwd[1] + pz * fwd[2], px * right[0] + py * right[1] + pz * right[2],
                 px * up[0] + py * up[1] + pz * up[2], i, x, y, z]);
    }
    list.sort((a, b) => b[0] - a[0]);
    g.fillStyle = cssColor('--dish'); g.fillRect(0, 0, size, size);
    const sq = ppu * 1.35;
    const at = (x, y, z) => (x < 0 || x >= W || y < 0 || y >= H || z < 0 || z >= D) ? 0 : A[(z * H + y) * W + x];
    if (keep) drawn = [];
    for (const [, u, v, i, x, y, z] of list) {
      const nx = -(at(x + 1, y, z) - at(x - 1, y, z)), ny = -(at(x, y + 1, z) - at(x, y - 1, z)), nz = -(at(x, y, z + 1) - at(x, y, z - 1));
      const nn = Math.hypot(nx, ny, nz);
      const lam = nn > 1e-3 ? Math.max(0, (nx * L[0] + ny * L[1] + nz * L[2]) / nn) : 0.6;
      const sh = 0.5 + 0.6 * lam, a = A[i];
      const r = Math.min(255, 255 * sh * Math.max(0, get(i, 0)) / a), gg = Math.min(255, 255 * sh * Math.max(0, get(i, 1)) / a),
            b = Math.min(255, 255 * sh * Math.max(0, get(i, 2)) / a);
      const sx = size / 2 + u * ppu, sy = size / 2 - v * ppu;
      g.fillStyle = `rgba(${r | 0},${gg | 0},${b | 0},${Math.min(1, a * 1.15).toFixed(3)})`;
      g.fillRect(sx - sq / 2, sy - sq / 2, sq, sq);
      if (keep) drawn.push([sx, sy, x, y, z]);
    }
  }
  function bestFrame() {
    const s = nca.state, M = N * 4; let best = Infinity, bk = 0;
    for (let k = 0; k < K3; k++) {
      let e = 0; const o = k * M;
      for (let i = 0; i < N; i++) for (let c = 0; c < 4; c++) { const d = s[i * C + c] - F3[o + i * 4 + c] / 255; e += d * d; }
      if (e < best) { best = e; bk = k; }
    }
    return [best / M, bk];
  }
  let lastErr = [NaN, 0], errTick = 0;
  function frame() {
    if (running) { const n = +$('speed3').value; for (let i = 0; i < n; i++) { alive = nca.step(); stepN++; } }
    if (spin && !dragging) az += 0.006;
    const s = nca.state;
    draw(ctx, cv.width, (i, c) => s[i * C + c], true);
    if ((errTick++ & 3) === 0) lastErr = bestFrame();
    const o = lastErr[1] * N * 4;
    draw(ctxt, cvt.width, (i, c) => F3[o + i * 4 + c] / 255, false);
    $('r3-step').textContent = stepN.toLocaleString();
    $('r3-alive').textContent = alive.toLocaleString();
    $('r3-err').textContent = lastErr[0] > 0 ? Math.log10(lastErr[0]).toFixed(2) : '–';
    $('r3-frame').textContent = (lastErr[1] + 1) + ' / ' + K3;
    requestAnimationFrame(frame);
  }
  // Drag orbits; a click (no drag) cuts a ball around the voxel under the pointer.
  let dragging = false, moved = 0, lx = 0, ly = 0;
  cv.addEventListener('pointerdown', e => { dragging = true; moved = 0; lx = e.clientX; ly = e.clientY; cv.setPointerCapture(e.pointerId); });
  cv.addEventListener('pointermove', e => {
    if (!dragging) return;
    const dx = e.clientX - lx, dy = e.clientY - ly; lx = e.clientX; ly = e.clientY; moved += Math.abs(dx) + Math.abs(dy);
    az -= dx * 0.01; tilt = Math.min(1.5, Math.max(0.05, tilt - dy * 0.01));
  });
  cv.addEventListener('pointerup', e => {
    dragging = false;
    if (moved > 5) return;
    const r = cv.getBoundingClientRect(), px = (e.clientX - r.left) / r.width * cv.width, py = (e.clientY - r.top) / r.height * cv.height;
    for (let j = drawn.length - 1; j >= 0; j--) {
      const [sx, sy, x, y, z] = drawn[j];
      if (Math.abs(sx - px) < 8 && Math.abs(sy - py) < 8) { nca.eraseBall(z, y, x, 6); break; }
    }
  });
  $('speed3').addEventListener('input', () => $('o-speed3').textContent = $('speed3').value);
  $('spin3').addEventListener('change', () => spin = $('spin3').checked);
  $('b3-play').addEventListener('click', () => { running = !running; $('b3-play').textContent = running ? 'Pause' : 'Run'; });
  $('b3-seed').addEventListener('click', () => { nca.seed(); stepN = 0; });
  $('b3-tail').addEventListener('click', () => nca.eraseBall(D / 2, H * 0.68, W * 0.68, 0.3 * W));
  requestAnimationFrame(frame);
})();
</script>"""


SCRIPTPRISM = r"""<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>
<script>
{{COREP}}
{{PRISMCORE}}
(function () {
  if (typeof THREE === 'undefined') return;
  const PD = {{PRDATA}};
  const $ = id => document.getElementById(id);
  const [GD, GH, GW] = PD.grid, CEN = [GW / 2, GH / 2, GD / 2];
  const cv = $('cvpr');
  const renderer = new THREE.WebGLRenderer({ canvas: cv, antialias: true });
  renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
  renderer.outputEncoding = THREE.sRGBEncoding;   // palette colours are converted to linear below
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(32, 1, 1, 400);
  camera.up.set(0, -1, 0);                       // image rows run down +y; the camera sits on -z
  scene.add(new THREE.HemisphereLight(0xffffff, 0x445066, 0.75));
  const sun = new THREE.DirectionalLight(0xffffff, 0.75); sun.position.set(-20, -30, -40); scene.add(sun);
  const S = 3.0;                                 // the game's circumscribing shield scale
  function stellaGeometry() {                    // union of two tetrahedra, vertices at S * (+-1, +-1, +-1)
    const A = [[1, 1, 1], [1, -1, -1], [-1, 1, -1], [-1, -1, 1]], pts = [];
    for (const sgn of [1, -1]) {
      const v = A.map(p => p.map(c => c * S * sgn));
      for (const [a, b, c] of [[0, 1, 2], [0, 3, 1], [0, 2, 3], [1, 3, 2]]) {
        const p = [v[a], v[b], v[c]];
        const n = new THREE.Vector3().subVectors(new THREE.Vector3(...p[1]), new THREE.Vector3(...p[0]))
          .cross(new THREE.Vector3().subVectors(new THREE.Vector3(...p[2]), new THREE.Vector3(...p[0])));
        const cen = new THREE.Vector3(...p[0]).add(new THREE.Vector3(...p[1])).add(new THREE.Vector3(...p[2]));
        if (n.dot(cen) < 0) p.reverse();         // outward winding
        p.forEach(q => pts.push(...q));
      }
    }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
    g.computeVertexNormals(); return g;
  }
  const cap = PD.w.world.capacity;
  const mat = new THREE.MeshStandardMaterial({ roughness: 0.42, metalness: 0.08, flatShading: true });
  const geos = [new THREE.BoxGeometry(2, 2, 2), new THREE.OctahedronGeometry(S), stellaGeometry(), new THREE.BoxGeometry(0.5, 0.5, 0.5)];
  const meshes = geos.map((g, i) => { const m = new THREE.InstancedMesh(g, i === 3 ? new THREE.MeshStandardMaterial({ color: 0x9aa39c, roughness: 0.8 }) : mat, cap);
    m.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    if (i < 3) m.setColorAt(0, new THREE.Color(1, 1, 1));   // sizes the colour buffer from count: before count = 0
    m.count = 0; scene.add(m); return m; });
  const shapeOf = [0, 0, 1, 2];                  // plain, danger -> box; shield -> octahedron; super -> stella
  const COL = PD.colours.map(row => row.map(c => new THREE.Color(c[0], c[1], c[2]).convertSRGBToLinear()));
  const M = new THREE.Matrix4(), dec = { h: [0, 0, 0], R: new Array(9) };
  let ca, step = 0, running = !matchMedia('(prefers-reduced-motion: reduce)').matches, az = -0.35, el = 0.55, dist = 95, visible = true;
  $('spinpr').checked = running;
  function seed() { ca = makeParticleNCA(PD.w); ca.seed(CEN); step = 0; }
  function css(n) { return getComputedStyle(document.documentElement).getPropertyValue(n).trim(); }
  function theme() { scene.background = new THREE.Color(css('--dish') || '#ffffff'); }
  function draw() {
    const n = [0, 0, 0, 0], C = ca.C, cen = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    let shields = 0;
    for (let i = 0; i < ca.cap; i++) {
      if (!ca.act[i]) continue;
      const px = ca.pos[i * 3], py = ca.pos[i * 3 + 1], pz = ca.pos[i * 3 + 2];
      decodePrism(ca.s, C, i, dec);
      if (dec.alpha <= 0.1) { M.makeTranslation(px, py, pz); meshes[3].setMatrixAt(n[3]++, M); continue; }
      const R = dec.R, h = dec.h, k = shapeOf[dec.tier], j = n[k]++;
      M.set(R[0] * h[0], R[1] * h[1], R[2] * h[2], px,
            R[3] * h[0], R[4] * h[1], R[5] * h[2], py,
            R[6] * h[0], R[7] * h[1], R[8] * h[2], pz, 0, 0, 0, 1);
      meshes[k].setMatrixAt(j, M); meshes[k].setColorAt(j, COL[dec.dom][dec.tier]);
      cen[dec.dom * 4 + dec.tier]++; if (dec.tier >= 2) shields++;
    }
    meshes.forEach((m, i) => { m.count = n[i]; m.instanceMatrix.needsUpdate = true; if (m.instanceColor) m.instanceColor.needsUpdate = true; });
    const eye = new THREE.Vector3(Math.sin(el) * Math.cos(az), Math.sin(el) * Math.sin(az), -Math.cos(el)).multiplyScalar(dist);
    camera.position.set(CEN[0] + eye.x, CEN[1] + eye.y, CEN[2] + eye.z); camera.lookAt(CEN[0], CEN[1], CEN[2]);
    renderer.render(scene, camera);
    $('rpr-step').textContent = step; $('rpr-n').textContent = n[0] + n[1] + n[2]; $('rpr-bud').textContent = n[3]; $('rpr-sh').textContent = shields;
    if (step % 10 === 0) {
      const parts = [];
      cen.forEach((v, q) => { if (v) parts.push([v, PRISM.DOMAINS[q >> 2] + ' ' + (PRISM.TIERS[q & 3] === 'super' ? 'super-shield' : PRISM.TIERS[q & 3])]); });
      $('rpr-census').textContent = parts.sort((a, b) => b[0] - a[0]).map(([v, s]) => s + ' ' + v).join(' · ');
    }
  }
  function resize() { const w = cv.clientWidth || 560; renderer.setSize(w, w, false); camera.aspect = 1; camera.updateProjectionMatrix(); }
  function frame() {
    if (visible) {
      if (running) for (let i = 0; i < +$('speedpr').value; i++) { ca.step(); step++; }
      if ($('spinpr').checked && !drag) az += 0.004;
      draw();
    }
    requestAnimationFrame(frame);
  }
  let drag = null;
  cv.addEventListener('pointerdown', e => { drag = [e.clientX, e.clientY, az, el]; cv.setPointerCapture(e.pointerId); });
  cv.addEventListener('pointermove', e => { if (!drag) return; az = drag[2] - (e.clientX - drag[0]) * 0.01; el = Math.min(1.25, Math.max(-1.25, drag[3] + (e.clientY - drag[1]) * 0.01)); });
  cv.addEventListener('pointerup', () => { drag = null; });
  $('speedpr').addEventListener('input', e => $('o-speedpr').textContent = e.target.value);
  $('bpr-play').onclick = () => { running = !running; $('bpr-play').textContent = running ? 'Pause' : 'Play'; };
  $('bpr-seed').onclick = seed;
  $('bpr-cut').onclick = () => {
    for (let i = 0; i < ca.cap; i++) if (ca.act[i] && ca.pos[i * 3] > CEN[0] && ca.pos[i * 3 + 1] > CEN[1]) { ca.act[i] = 0; ca.s.fill(0, i * ca.C, (i + 1) * ca.C); }
  };
  $('bpr-tail').onclick = () => {             // a ball around the particle farthest from the body's centroid
    let m = [0, 0, 0], c = 0, far = -1, fd = -1;
    for (let i = 0; i < ca.cap; i++) if (ca.act[i]) { for (let k = 0; k < 3; k++) m[k] += ca.pos[i * 3 + k]; c++; }
    if (!c) return; m = m.map(v => v / c);
    for (let i = 0; i < ca.cap; i++) if (ca.act[i]) { const q = (ca.pos[i * 3] - m[0]) ** 2 + (ca.pos[i * 3 + 1] - m[1]) ** 2 + (ca.pos[i * 3 + 2] - m[2]) ** 2; if (q > fd) { fd = q; far = i; } }
    ca.eraseBall([ca.pos[far * 3], ca.pos[far * 3 + 1], ca.pos[far * 3 + 2]], 7);
  };
  if ('IntersectionObserver' in window) new IntersectionObserver(es => { visible = es[0].isIntersecting; }).observe(cv);
  matchMedia('(prefers-color-scheme: dark)').addEventListener('change', theme);
  new MutationObserver(theme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  window.addEventListener('resize', resize);
  if (!running) $('bpr-play').textContent = 'Play';
  theme(); resize(); seed(); frame();
})();
</script>"""


TEMPLATE = r"""<title>One-Cell Lizard</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,500;12..96,700&family=Atkinson+Hyperlegible:wght@400;700&family=JetBrains+Mono:wght@400;600&display=swap">
<style>
/* Layout: a lab bench. The dish (live automaton) is the hero; the instrument panel sits
   beside it; the measured record (curves, table, figures) reads below. */
:root {
  --ground: #edf0ea; --surface: #f8faf5; --ink: #1b221d; --muted: #5a645c; --rule: #d2d8cf;
  --accent: #b8501d; --dish: #ffffff;
  --s1: #7a6fb0; --s2: #2f7f86; --s3: #b8501d; --s4: #2f5fa8;
  --ok: #2c7a4b; --ok-bg: #dcefe1; --run: #8a5a00; --run-bg: #f6e7c4; --todo: #5a645c; --todo-bg: #e3e8e0;
  --display: "Bricolage Grotesque", "Helvetica Neue", Arial, sans-serif;
  --body: "Atkinson Hyperlegible", "Segoe UI", system-ui, sans-serif;
  --mono: "JetBrains Mono", ui-monospace, "SFMono-Regular", Menlo, monospace;
}
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {
  --ground: #111713; --surface: #18201b; --ink: #e2e9e1; --muted: #92a095; --rule: #2b362e;
  --accent: #ef8a4f; --dish: #0b0f0c; --s1: #a79be0; --s2: #5fb8bf; --s3: #ef8a4f; --s4: #82a9ea;
  --ok: #7fd49b; --ok-bg: #1d3325; --run: #f2c46b; --run-bg: #3a2f14; --todo: #92a095; --todo-bg: #232c26; color-scheme: dark } }
:root[data-theme="dark"] {
  --ground: #111713; --surface: #18201b; --ink: #e2e9e1; --muted: #92a095; --rule: #2b362e;
  --accent: #ef8a4f; --dish: #0b0f0c; --s1: #a79be0; --s2: #5fb8bf; --s3: #ef8a4f; --s4: #82a9ea;
  --ok: #7fd49b; --ok-bg: #1d3325; --run: #f2c46b; --run-bg: #3a2f14; --todo: #92a095; --todo-bg: #232c26; color-scheme: dark }
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
.tabs { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); border: 1px solid var(--rule); border-radius: 10px; overflow: hidden }
.tabs button { font: 600 14px var(--body); background: var(--surface); color: var(--ink); border: 0; padding: 10px 6px; cursor: pointer; display: flex; gap: 7px; align-items: center; justify-content: center; border-right: 1px solid var(--rule) }
.tabs button:nth-child(2n) { border-right: 0 }
.tabs button:nth-child(-n+2) { border-bottom: 1px solid var(--rule) }
.tabs button[aria-selected="true"] { background: var(--ink); color: var(--ground) }
.tabs button:disabled { opacity: .4; cursor: not-allowed }
.tabs button:focus-visible, .btn:focus-visible, input:focus-visible, select:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px }
.swatch { width: 10px; height: 10px; border-radius: 2px; display: inline-block; flex: none; margin-right: 6px; vertical-align: 0 }
.tabs .swatch { margin-right: 0 }
.note { margin: 0; color: var(--muted); font-size: 15px; min-height: 4.6em }
.readouts { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1px; background: var(--rule); border: 1px solid var(--rule); border-radius: 10px; overflow: hidden }
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

.palette { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(9.5em, 1fr)); gap: 4px 12px; font: 12px var(--mono); color: var(--muted) }
.palette li { display: flex; align-items: center; gap: 6px }
.pswatch { width: 12px; height: 12px; border-radius: 2px; flex: none; border: 1px solid var(--rule) }
.census { margin: 0; font-size: 12px; color: var(--muted); min-height: 3em }
.related .rel { margin: 0; padding-left: 1.3em; display: grid; gap: 10px; max-width: 78ch }
.related .rel a { color: var(--ink) }
.record { display: grid; grid-template-columns: minmax(0, 1fr); gap: 28px }
.chart { background: var(--surface); border: 1px solid var(--rule); border-radius: 14px; padding: 16px; overflow-x: auto }
.chart svg { width: 100%; min-width: 480px; height: auto; display: block }
.chart .grid { stroke: var(--rule); stroke-width: 1 }
.chart .drop { stroke: var(--muted); stroke-dasharray: 3 4; stroke-width: 1 }
.chart .tick { fill: var(--muted); font: 11px var(--mono) }
.chart .floor { stroke-dasharray: 6 5; stroke-width: 1.5 }
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
.anim { display: grid; gap: 20px }
.anim h2 { margin: 0 }
.anim > .caption { margin: -8px 0 0 }
.animrow { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 20px; align-items: start }
@media (max-width: 820px) { .animrow { grid-template-columns: minmax(0, 1fr) } }
.gifbox img { min-width: 0; width: 100% }
.bench3d, .benchp { display: grid; gap: 24px }
.ledger { background: var(--surface); border: 1px solid var(--rule); border-radius: 14px; padding: 16px 18px; display: grid; gap: 10px }
.ledger-head { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 8px }
.ledger-head h2 { margin: 0 }
.ledger-date { font-size: 12px; color: var(--muted) }
.ledger-list { list-style: none; margin: 0; padding: 0; display: grid }
.ledger-row { display: grid; grid-template-columns: 6.5em minmax(8em, 11em) minmax(0, 1fr) minmax(0, auto); gap: 6px 14px; align-items: baseline; padding: 9px 0; border-top: 1px solid var(--rule) }
.ledger-row:first-child { border-top: 0 }
.ledger-name { font-weight: 700 }
.ledger-name a { color: var(--ink); text-decoration-color: var(--rule); text-underline-offset: 3px }
.ledger-what { color: var(--muted); font-size: 15px; min-width: 0 }
.ledger-num { font: 13px var(--mono); font-variant-numeric: tabular-nums; text-align: right; min-width: 0 }
.chip { justify-self: start; font: 600 11px/1 var(--mono); letter-spacing: .08em; text-transform: uppercase; padding: 5px 8px; border-radius: 999px }
.chip-done { color: var(--ok); background: var(--ok-bg) }
.chip-run { color: var(--run); background: var(--run-bg) }
.chip-todo { color: var(--todo); background: var(--todo-bg) }
@media (max-width: 720px) { .ledger-row { grid-template-columns: 6.5em minmax(0, 1fr) } .ledger-what, .ledger-num { grid-column: 2 } .ledger-num { text-align: left } }
.diag { overflow-x: auto; border: 1px solid var(--rule); border-radius: 14px; background: var(--surface) }
.diag td:last-child, .diag th:last-child { text-align: left; white-space: normal; min-width: 16em; font-family: var(--body) }
.diag thead th:first-child { text-align: left }
.diag tr.win th, .diag tr.win td { color: var(--ok); font-weight: 700 }
[hidden] { display: none !important }
.pfigs { display: grid; gap: 18px }
.ptabs { grid-template-columns: repeat(2, minmax(0, 1fr)) }
.ptabs button { border-bottom: 0 !important }
.h3dhead { display: grid; gap: 8px }
.h3dhead h2 { margin: 0 }
.plate3d { cursor: grab }
.plate3d:active { cursor: grabbing }
.plate3d canvas { image-rendering: auto }
.tgt3 canvas { width: 160px; height: 160px; border-radius: 8px; border: 1px solid var(--rule); display: block }
.row input[type=checkbox] { justify-self: start; width: 18px; height: 18px; accent-color: var(--accent) }
table.kv th { text-align: left; font-weight: 400; color: var(--muted); white-space: normal }
table.kv td { font-weight: 600 }
@media (prefers-reduced-motion: reduce) { * { scroll-behavior: auto } }
</style>

<div class="wrap">
  <header>
    <div class="eyebrow">Growing Neural Cellular Automata · reproduction</div>
    <h1>A lizard grown from <em>one cell</em></h1>
    <p class="lede">Every pixel below runs the same 8,336-parameter rule, sees only its 3×3 neighbours, and fires at random half the time. Starting from a single live cell, the rule grows the Noto lizard emoji. This is a CPU PyTorch reproduction of <a href="https://distill.pub/2020/growing-ca/">Mordvintsev et al., Distill 2020</a>, running live in your browser, plus three extensions: the same cell trained on an animated loop, so the lizard swims; that swim with one more spatial dimension; and the cell taken off the grid entirely, as free particles that only sense and push their neighbours.</p>
  </header>
  {{STATUS}}

  <section class="bench" id="grid" aria-label="Live automaton">
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
        <div><dt>Frame</dt><dd id="r-frame">–</dd></div>
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
  {{BENCH3D}}
  {{BENCHP}}
  {{BENCHPRISM}}
  {{RELATED}}

  <section class="record" aria-label="Training record">
    <div>
      <h2>Training</h2>
      <div class="chart">{{CHART}}
        <div class="legend"><span><span class="swatch" style="background:var(--s1)"></span>Growing</span><span><span class="swatch" style="background:var(--s2)"></span>Persistent</span><span><span class="swatch" style="background:var(--s3)"></span>Regenerating</span><span><span class="swatch" style="background:var(--s4)"></span>Swimming (8-frame loop)</span></div>
      </div>
      <p class="caption">log₁₀ of the pixel MSE against the premultiplied RGBA target after 64–96 steps (for the swim, averaged over five checkpoints 8 steps apart against consecutive frames), 50-step moving mean. Batch 8, Adam, per-variable gradient normalisation, as in the paper.</p>
    </div>
    <div>
      <h2>What each rule does past its training window</h2>
      <div class="tablewrap"><table>
        <thead><tr><th scope=col>Experiment</th><th scope=col>Train steps</th><th scope=col>Final train loss</th><th scope=col>Error @ 96</th><th scope=col>@ 1000</th><th scope=col>@ 4000</th><th scope=col>Worst cut, 300 steps later</th></tr></thead>
        <tbody>{{ROWS}}</tbody>
      </table></div>
      <p class="caption">All values log₁₀ MSE against the target from one stochastic rollout. Training only ever sees steps 64–96. The paper's claim is visible in the last three columns: only the pool experiments hold the lizard at step 4000, and only the damaged-pool experiment reliably recovers from a cut. The persistent rule regrows three of the four cuts outright; after the corner cut it regrows a complete lizard two cells to the left of where it was, which pixel error scores as a failure (shifted back, its error is 10<sup>−4.96</sup>).</p>
    </div>
    {{FIGS}}
    {{ANIM}}
  </section>
</div>

<script>
{{CORE}}
const RUNS = {{WEIGHTS}};
const NOTES = {{EXP_NOTES}};
const G = 72;
const TARGETS = Object.fromEntries(Object.entries({{TARGETS}}).map(([k, t]) => [k, { n: t.n, d: Uint8Array.from(atob(t.d), c => c.charCodeAt(0)) }]));
const cv = document.getElementById('cv'), ctx = cv.getContext('2d'), img = ctx.createImageData(G, G);
const $ = id => document.getElementById(id);
let nca = null, exp = null, stepN = 0, running = true, lastAlive = 0;

const view = $('view');
for (let c = 4; c < 16; c++) { const o = document.createElement('option'); o.value = 'h' + c; o.textContent = 'Hidden channel ' + c; view.appendChild(o); }

function load(key) {
  exp = key;
  nca = makeNCA(RUNS[key].w, G, G);
  nca.seed(); stepN = 0;
  document.querySelectorAll('.tabs [data-exp]').forEach(b => b.setAttribute('aria-selected', b.dataset.exp === key));
  $('note').textContent = NOTES[key];
  try { localStorage.setItem('nca-exp', key) } catch (e) {}
}
document.querySelectorAll('.tabs [data-exp]').forEach(b => b.addEventListener('click', () => !b.disabled && load(b.dataset.exp)));

function bgRGB() {
  const c = getComputedStyle(document.documentElement).getPropertyValue('--dish').trim();
  const m = c.match(/^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})/i);
  return m ? [1, 2, 3].map(i => parseInt(m[i], 16) / 255) : [1, 1, 1];
}
let BG = bgRGB();
matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => BG = bgRGB());
new MutationObserver(() => BG = bgRGB()).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });

function error() {  // [best error, best frame] over the run's target frames
  const s = nca.state, T = TARGETS[exp], F = G * G * 4; let best = Infinity, bk = 0;
  for (let k = 0; k < T.n; k++) {
    let e = 0;
    for (let i = 0; i < G * G; i++) for (let c = 0; c < 4; c++) { const d = s[i * 16 + c] - T.d[k * F + i * 4 + c] / 255; e += d * d; }
    if (e < best) { best = e; bk = k; }
  }
  return [best / F, bk];
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
  const [e, k] = error(); $('r-err').textContent = e > 0 ? Math.log10(e).toFixed(2) : '–';
  $('r-frame').textContent = TARGETS[exp].n > 1 ? (k + 1) + ' / ' + TARGETS[exp].n : '–';
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
{{SCRIPT3D}}
{{SCRIPTP}}
{{SCRIPTPRISM}}
"""

if __name__ == "__main__":
    main()
