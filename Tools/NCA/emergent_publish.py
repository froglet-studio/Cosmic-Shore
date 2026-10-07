"""Publish a species-3 checkpoint to results/emergent/: rule.pt, eval.json (the emergent_eval yardstick),
probe.json, feel.json, summary.json + rollout.json (swarm_nca.rollout + swarm_nca.pack, exactly as
swarm_gpu.py's publisher writes them, so the existing viewer can play it; the rule is grown from each of
the four plans' seeds), and swim.gif (the swarm next to the target frame at the fitted phase).

    python Tools/NCA/emergent_publish.py --rule Tools/NCA/runs/em_b/rule_00300.pt [--skip-eval]
"""
from __future__ import annotations

import argparse
import json
import math
import os
import shutil

import numpy as np
import torch
from PIL import Image, ImageDraw

import emergent_eval as ee
import emergent_model as em
import swarm_feel as sf
import swarm_nca as sn

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "results", "emergent")
EL_UI = [(232, 169, 58), (142, 107, 216), (58, 123, 220), (47, 179, 154)]
DOM_UI = [(255, 255, 255), (30, 30, 30), (220, 60, 60)]


def _rot(p, yaw=0.55, pitch=0.35):
    cy, sy, cp, spi = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    x1, z1 = cy * x + sy * z, -sy * x + cy * z
    y2, z2 = cp * y - spi * z1, spi * y + cp * z1
    return np.stack([x1, y2, z2], 1)


def draw_panel(img, p, elem, dom, ox, size, scale, title):
    d = ImageDraw.Draw(img)
    if len(p):
        q = _rot(p - p.mean(0))
        order = np.argsort(q[:, 2])
        for i in order:
            x, y = ox + size / 2 + q[i, 0] * scale, size / 2 - q[i, 1] * scale + 14
            r = 2.2 + 0.9 * (q[i, 2] - q[:, 2].min()) / (np.ptp(q[:, 2]) + 1e-6) * 2
            d.ellipse([x - r, y - r, x + r, y + r], fill=EL_UI[int(elem[i])], outline=DOM_UI[int(dom[i]) % 3])
    d.text((ox + 6, 2), title, fill=(230, 230, 230))


@torch.no_grad()
def gif(rule, path, seed=7, grow=600, steps=128, every=2):
    T = sn.load_targets()["space"]
    L = sn.LossCfg()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T], rule.world, gen)
    snaps = []
    for t in range(grow + steps):
        sw = rule(sw, gen)
        if t == 40 or t == 120 or t == 200:
            snaps.append(("grow", t, sw.clone()))
        if t >= grow and (t - grow) % every == 0:
            snaps.append(("swim", t, sw.clone()))
    # phase fit over the swim snaps, to show the matching target frame
    D = np.stack([ee.frame_table(sn.decode(s, 0), T, L) for kind, _, s in snaps if kind == "swim"])
    _, tpf, k0 = ee.fit_phase(D, every, steps)
    size = 300
    frames = []
    j = 0
    for kind, t, s in snaps:
        m = (s.active[0] & s.hatched[0]).numpy()
        p, e, dmn = s.pos[0].numpy()[m], s.elem[0].numpy()[m], s.dom[0].numpy()[m]
        img = Image.new("RGB", (2 * size, size + 14), (16, 18, 24))
        if kind == "swim":
            k = int(math.floor(k0 + j * every / tpf + 0.5)) % 8
            tf = T.frames[k]
            draw_panel(img, tf["p"].numpy(), tf["elem"].numpy(), tf["slot"].numpy(), size, size, 4.0, f"target frame {k}")
            lab = f"swarm step {t}  n={len(p)}  loss {D[j, k]:.1f}"
            j += 1
        else:
            lab = f"growing from 16: step {t}  n={len(p)}"
        draw_panel(img, p, e, dmn, 0, size, 4.0, lab)
        reps = 6 if kind == "grow" else 1
        frames += [img] * reps
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=80, loop=0)
    return dict(tpf=tpf, k0=k0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rule", required=True)
    ap.add_argument("--skip-eval", action="store_true")
    ap.add_argument("--seeds", default="7,23,41,1000")
    ap.add_argument("--note", default="")
    a = ap.parse_args()
    torch.set_num_threads(1 if not a.skip_eval else 4)
    os.makedirs(OUT, exist_ok=True)
    rule = em.load_rule(a.rule)
    rule.eval()
    meta = torch.load(a.rule, weights_only=False)
    shutil.copy(a.rule, os.path.join(OUT, "rule.pt"))
    if not a.skip_eval:
        ev = ee.evaluate(rule, [int(s) for s in a.seeds.split(",")], locality="local")
        ev["rule"] = os.path.relpath(a.rule, HERE); ev["step"] = meta.get("step"); ev["stage"] = meta.get("stage")
        json.dump(ev, open(os.path.join(OUT, "eval.json"), "w"), indent=1)
        print(ev["headline"])
        probe = {"space": {k: v for k, v in ev["per_seed"]["7"]["regrowth"].items()},
                 "per_seed": {s: {k: v for k, v in r["regrowth"].items() if k != "traj"} for s, r in ev["per_seed"].items()},
                 "note": "swarm_probe.strike (sphere of one RMS radius, ~a third of the body) on the grown jellyfish, then "
                         "160 steps of regrowth; scored as swarm_probe scores (best-frame divergence). Only the jellyfish: "
                         "this rule has one target."}
        json.dump(probe, open(os.path.join(OUT, "probe.json"), "w"), indent=1)
        cal = json.load(open(os.path.join(HERE, "results", "hgrid2", "feel_calibration.json")))
        p2 = json.load(open(os.path.join(HERE, "results", "posinfo2", "feel.json")))
        feel = dict(space=ev["per_seed"]["7"]["feel"], mean_over_seeds=ev["emergent"]["mean"],
                    organic=dict(ok=ev["emergent"]["in_band"], checks=ev["emergent"]["checks"],
                                 planar_excess=ev["emergent"]["planar_excess"]),
                    locality="local", band=sf.BAND,
                    compare=dict(posinfo2_space=p2.get("space"), posinfo2_mean=p2.get("mean"),
                                 calibration_space={k: v.get("space") for k, v in cal.items() if isinstance(v, dict) and "space" in v}))
        json.dump(feel, open(os.path.join(OUT, "feel.json"), "w"), indent=1)
    # viewer rollout: the rule grown from each plan's seed for 480 steps (no switch: one target)
    data, summary = sn.rollout(rule, 480, every=5, seed=7, switch_steps=0)
    sn.print_cross(summary)
    summary["meta"] = dict(label="Pure emergent swimmer (species 3)", step=meta.get("step"), tag="emergent",
                           note=("ONE learned local rule, one animated target (the pulsing jellyfish). It perceives only neighbours "
                                 "within R and chemicals the tadpoles secrete; no census, frame, clock or designed composition. "
                                 "Rows other than the jellyfish show the same rule grown from another plan's seed mix. " + a.note))
    summary["rule"] = "rule.pt"
    json.dump(summary, open(os.path.join(OUT, "summary.json"), "w"), indent=1)
    json.dump(sn.pack(data, 480), open(os.path.join(OUT, "rollout.json"), "w"))
    print("gif", gif(rule, os.path.join(OUT, "swim.gif")))


if __name__ == "__main__":
    main()
