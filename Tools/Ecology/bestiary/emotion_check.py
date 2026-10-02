"""Independent emotion read of every bestiary species with Direction C's FROZEN probe (cece/eco-emotion:
Tools/Ecology/emotion/results/probe.json + common/affect.py). The probe was trained on archetypes, not on
these species, so it is an outside judge of whether each species FEELS like the emotion it was built for.

    python Tools/Ecology/bestiary/emotion_check.py      -> emotion.json

The probe files are fetched from the sibling branch into .cache/ (gitignored) so this branch never forks
them. An ENCOUNTER is staged: the viewer pilot is placed 350 u from a random live individual, heading at it
(Direction C's three viewers: hover 25 u/s, cruise 90 u/s, evade 110 u/s), 3 seeds x 40 s.
"""
import importlib, json, os, subprocess, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(HERE, ".cache"); os.makedirs(CACHE, exist_ok=True)
BR = "origin/cece/eco-emotion"
for src, dst in (("Tools/Ecology/common/affect.py", "affect.py"), ("Tools/Ecology/emotion/probe.py", "probe.py"),
                 ("Tools/Ecology/emotion/results/probe.json", "probe.json")):
    p = os.path.join(CACHE, dst)
    if not os.path.exists(p):
        open(p, "w").write(subprocess.check_output(["git", "show", f"{BR}:{src}"], cwd=HERE, text=True))
sys.path.insert(0, CACHE); sys.path.insert(0, HERE); sys.path.insert(0, os.path.join(HERE, ".."))
from affect import AffectRecorder                                          # noqa: E402
import probe as probe_mod                                                  # noqa: E402
from common.arena import Pilot                                             # noqa: E402
from run import make_arena, SPECIES, DT                                    # noqa: E402

VIEWERS = {"hover": lambda: Pilot("wander", speed=25.0, turn=1.5, name="hover"),
           "cruise": lambda: Pilot("wander", speed=90.0, turn=1.8, name="cruise"),
           "evade": lambda: Pilot("evader", speed=110.0, turn=2.0, name="evade")}


def encounter(key, viewer, seed, seconds=40.0, warm=None):
    ar = make_arena(seed)
    sp = importlib.import_module(f"species.{key}").make(ar)
    p = ar.add_pilot(VIEWERS[viewer]())
    for _ in range(int((warm or 0) / DT)):          # let a species reach its own state first (locust breeding)
        sp.step(ar, DT); ar.step(DT)
    # focus on ONE individual's neighbourhood (a dispersed population's centroid is empty space)
    c = sp.pos[ar.rng.choice(np.flatnonzero(sp.alive))]
    d = ar.rng.normal(size=3); d /= np.linalg.norm(d)
    p.pos = c + d * 350.0; p.vel = -d * p.speed; p.goal = c - d * 400.0
    aff = AffectRecorder(DT, pilot_radius=p.radius)
    for _ in range(int(seconds / DT)):
        sp.step(ar, DT); ar.step(DT)
        aff.observe(p.pos, p.vel, sp.agent_pos, sp.agent_vel, sp.agent_size, sp.agent_aspect, sp.agent_heading)
    return aff.features()


if __name__ == "__main__":
    pr = probe_mod.EmotionProbe(json.load(open(os.path.join(CACHE, "probe.json"))))
    keys = sys.argv[1:] or SPECIES
    out = json.load(open(os.path.join(HERE, "emotion.json"))) if os.path.exists(os.path.join(HERE, "emotion.json")) else {}
    for key in keys:
        res = {}
        mod = importlib.import_module(f"species.{key}")
        for v in VIEWERS:
            P = []
            for s in (7, 23, 41):
                f = encounter(key, v, s, warm=getattr(mod, "EMOTION_WARMUP", 0.0))
                P.append([pr.score(f)["p"][e] for e in probe_mod.EMOTIONS])
            Pm = np.mean(P, 0)
            res[v] = dict(top=probe_mod.EMOTIONS[int(Pm.argmax())],
                          p={e: round(float(x), 3) for e, x in zip(probe_mod.EMOTIONS, Pm)},
                          affect=probe_mod.affect_of(Pm))
        res["target"] = mod.EMOTION
        out[key] = res
        print(f"{key:10s} target [{mod.EMOTION}]  " + " | ".join(f"{v}: {res[v]['top']} {max(res[v]['p'].values()):.2f}" for v in VIEWERS), flush=True)
    json.dump(out, open(os.path.join(HERE, "emotion.json"), "w"), indent=1)
