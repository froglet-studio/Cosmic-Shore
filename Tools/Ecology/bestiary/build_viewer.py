"""Build the bestiary viewer: one self-contained HTML with a showcase run of every species.

    python Tools/Ecology/bestiary/build_viewer.py            -> bestiary/bestiary.html (target < 4 MB)

Each species is recorded against the pilot that shows it best (SHOWCASE) for 45 s, seed 7, every 2nd step.
The format is compact binary (base64) rather than the shared Recorder's JSON, because eight species at the
shared format were ~25 MB: agents are int16 positions + uint8 rgb + uint8 size per frame, mass is sent once
with a death frame per prism, and trail prisms carry a birth and death frame. The page interpolates between
recorded frames when the agent count is unchanged, so playback is smooth at any speed.

The panel shows each species' scorecard from scorecards.json and the outside emotion read from emotion.json.
"""
from __future__ import annotations

import base64
import importlib
import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE); sys.path.insert(0, os.path.join(HERE, ".."))
from run import make_arena, POLICIES, DT, SPECIES  # noqa: E402

SHOWCASE = {"pack": "wander", "leech": "skimmer", "locust": "skimmer", "stampede": "hunter", "lurker": "skimmer",
            "leviathan": "skimmer", "thief": "wander", "mobber": "skimmer"}
SECONDS, EVERY, SEED = 45.0, 2, 7
WARM = {"locust": 30.0}            # start the locust recording as the cloud is tipping


def b64(a):
    return base64.b64encode(np.ascontiguousarray(a).tobytes()).decode()


def record(key):
    pol = SHOWCASE[key]
    ar = make_arena(SEED)
    sp = importlib.import_module(f"species.{key}").make(ar)
    p = ar.add_pilot(POLICIES[pol]())
    for _ in range(int(WARM.get(key, 0.0) / DT)):
        sp.step(ar, DT); ar.step(DT)
    log0 = len(ar.log)
    t0 = ar.t
    nm0 = len(ar.mass_vol)
    mass_pos0 = ar.mass_pos[:nm0].copy(); elem0 = ar.mass_elem[:nm0].copy(); own0 = ar.mass_owner[:nm0].copy()
    alive0 = ar.mass_alive[:nm0].copy()
    frames, counts, pil = [], [], []
    death = {}                                  # prism -> frame it died / changed hands
    born = {}
    nsteps = int(SECONDS / DT)
    prev_alive = ar.mass_alive.copy(); prev_own = ar.mass_owner.copy()
    for s in range(nsteps):
        sp.step(ar, DT); ar.step(DT)
        f = s // EVERY
        n = len(ar.mass_vol)
        if n > len(prev_alive):
            for j in range(len(prev_alive), n):
                born[j] = f
            prev_alive = np.concatenate([prev_alive, ar.mass_alive[len(prev_alive):]])
            prev_own = np.concatenate([prev_own, ar.mass_owner[len(prev_own):]])
        gone = np.flatnonzero((prev_alive & ~ar.mass_alive[:n]) | ((prev_own >= 0) & (ar.mass_owner[:n] == -2)))
        for j in gone:
            death.setdefault(int(j), f)
        prev_alive = ar.mass_alive[:n].copy(); prev_own = ar.mass_owner[:n].copy()
        if s % EVERY:
            continue
        out = {}
        sp.render(out)
        if hasattr(sp, "render_extra"):
            sp.render_extra(out, ar)
        P = np.concatenate([np.asarray(b["pos"], float).reshape(-1, 3) for b in out.values()]) if out else np.zeros((0, 3))
        C = np.concatenate([np.asarray(b["col"], float).reshape(-1, 3) for b in out.values()]) if out else np.zeros((0, 3))
        Z = np.concatenate([np.asarray(b["size"], float).reshape(-1) for b in out.values()]) if out else np.zeros(0)
        frames.append((np.clip(np.round(P), -32000, 32000).astype(np.int16),
                       (np.clip(C, 0, 1) * 255).astype(np.uint8),
                       np.clip(np.round(Z * 4), 0, 255).astype(np.uint8)))
        counts.append(len(P))
        pil.append(np.round(p.pos).astype(np.int16))
    nf = len(frames)
    # environment mass alive at the start (skip the long-dead) + its death frame
    keep = np.flatnonzero(alive0 & (own0 < 0))
    mdeath = np.array([death.get(int(j), 65535) for j in keep], np.uint16)
    # trail prisms (laid by the pilot during the recording, or alive trail at its start)
    trail = sorted(set(j for j in born) | set(np.flatnonzero(alive0 & (own0 >= 0)).tolist()))
    tpos = np.round(ar.mass_pos[trail]).astype(np.int16) if trail else np.zeros((0, 3), np.int16)
    # a stolen prism's position at the moment it changed hands is its last trail position; use the
    # position recorded at birth for trail drawing (thieves render what they carry)
    tb = np.array([born.get(j, 0) for j in trail], np.uint16)
    td = np.array([death.get(j, 65535) for j in trail], np.uint16)
    hits = [(int((t - t0) / (DT * EVERY)), kind)
            for (t, _n, kind, _a) in ar.log[log0:]]
    return dict(key=key, pilot=pol, nf=nf, dt=DT * EVERY,
                counts=b64(np.array(counts, np.uint16)),
                pos=b64(np.concatenate([f[0] for f in frames]) if nf else np.zeros(0, np.int16)),
                col=b64(np.concatenate([f[1] for f in frames])),
                size=b64(np.concatenate([f[2] for f in frames])),
                pilots=b64(np.array(pil, np.int16)),
                mpos=b64(np.round(mass_pos0[keep]).astype(np.int16)), melem=b64(elem0[keep].astype(np.uint8)),
                mdeath=b64(mdeath), tpos=b64(tpos), tborn=b64(tb), tdeath=b64(td), hits=hits)


from common.viewer import TPL as VTPL  # noqa: E402

PANEL = open(os.path.join(HERE, "viewer.html.tpl")).read()
OUT = os.path.join(HERE, "bestiary.html")


def render(runs):
    """Shared 3D viewer (common/viewer.py) + the bestiary ledger; runs stay in the compact binary format."""
    for r in runs:
        r["fmt"] = "bestiary"
        r["label"] = f"{r['key']} vs {r['pilot']}"
    return (VTPL.replace('<div id="info"></div>', PANEL, 1).replace("__TITLE__", "Hypersea Bestiary")
            .replace("__DATA__", json.dumps(runs, separators=(",", ":"))))


def extract(path=OUT):
    import re
    s = open(path).read()
    m = re.search(r"const (?:RUNS|D) = (\[.*?\]);\n", s, re.S)
    return json.loads(m.group(1))


if __name__ == "__main__":
    if "--retemplate" in sys.argv:          # regenerate the page from the runs already recorded in bestiary.html
        runs = extract()
        open(OUT, "w").write(render(runs))
        print(OUT, len(runs), "runs", round(os.path.getsize(OUT) / 1e6, 2), "MB")
        sys.exit(0)
    cards = json.load(open(os.path.join(HERE, "scorecards.json")))
    emo = json.load(open(os.path.join(HERE, "emotion.json"))) if os.path.exists(os.path.join(HERE, "emotion.json")) else {}
    runs = []
    for key in (sys.argv[1:] or SPECIES):
        r = record(key)
        mod = importlib.import_module(f"species.{key}")
        r["doc"] = (mod.__doc__ or "").strip()
        r["emotion"] = mod.EMOTION; r["counter"] = mod.COUNTER
        r["card"] = cards["species"].get(key, {})
        r["probe"] = emo.get(key, {})
        runs.append(r)
        print(key, r["nf"], "frames")
    open(OUT, "w").write(render(runs))
    print(OUT, round(os.path.getsize(OUT) / 1e6, 2), "MB")
