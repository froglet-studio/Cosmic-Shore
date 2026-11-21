#!/usr/bin/env python3
"""The ARMS RACE fixture (Docs/SUBSTRATE_FAUNA.md §11): the lab's co-evolved schooling prey and packing predators, read
out of the research branch so the substrate harness (group arms) can hold the game's port to them.

    python3 Tools/Build/substrate_harness/arms_fixture.py              # write arms_fixture.json + SubstrateArmsPolicy.cs
    python3 Tools/Build/substrate_harness/arms_fixture.py --check      # FAIL if either differs from what this writes
    python3 Tools/Build/substrate_harness/arms_fixture.py --lab DIR    # read the lab from a checkout instead of git

Source: research branch cece/gifted-curie-x2cpd0, Tools/NCA/arms_sim.py (the world and the two MLPs) and
Tools/NCA/results/arms/snaps_a9_herd/g01500.npz (run a9, the selfish herd, generation 1500 - NOTE.md's "most FUN
pair"). g00000.npz of the same run (= a3 g1460: solo jukers, no school) is kept as the behaviour gate's negative
control. Needs numpy (the lab's only dependency) and, without --lab, a git fetch of the research branch.

What it writes:
  * Assets/.../Substrate/SubstrateArmsPolicy.cs - the two a9 g1500 MLPs as float32 literals (the game's weights);
  * arms_fixture.json -
      policy      the same weights plus g0's (sha256 of each), so the harness checks the C# literals bit for bit;
      parity      PARITY_CASES lab states mid-encounter (a vessel crossing the pond, seen by both species), each with
                  the lab's observations, MLP outputs and one physics step (catches drawn as misses, so the step is
                  deterministic) and the catch ATTEMPTS that step makes (predator, prey, crowd);
      behave      the lab's three behaviour seeds (arms_eval.behave: 11, 12, 13): each start state and food template,
                  and the lab's own metrics on them (60 s each), for the g1500 pair and the g0 negative control.
"""
import argparse
import hashlib
import io
import json
import os
import subprocess
import sys
import tempfile

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
REF = "cece/gifted-curie-x2cpd0"
SIM = "Tools/NCA/arms_sim.py"
METRICS = "Tools/NCA/arms_metrics.py"
SNAP = "Tools/NCA/results/arms/snaps_a9_herd/g{:05d}.npz"
GENS = {"g1500": 1500, "g0": 0}
OUT_JSON = os.path.join(HERE, "arms_fixture.json")
OUT_CS = os.path.join(REPO, "Assets", "_Scripts", "Controller", "Environment", "FloraAndFauna", "Substrate",
                      "SubstrateArmsPolicy.cs")
PARITY_SEED, PARITY_STEPS, PARITY_EVERY = 5, 360, 60     # 5 cases, t = 6, 12, ... 30 s
BEHAVE_SEEDS, BEHAVE_SECS = (11, 12, 13), 60.0
VESSEL_SPEED, VESSEL_RADIUS = 120.0, 6.0                  # a cruising vessel; the lab pads its hull by 4 u


def lab_files(lab_dir):
    """{relative path: bytes} for the lab files this fixture reads."""
    paths = [SIM, METRICS] + [SNAP.format(g) for g in GENS.values()]
    if lab_dir:
        return {p: open(os.path.join(lab_dir, p), "rb").read() for p in paths}
    subprocess.run(["git", "-C", REPO, "fetch", "--depth", "1", "--filter=blob:none", "origin", REF],
                   check=True, capture_output=True)
    return {p: subprocess.run(["git", "-C", REPO, "show", f"FETCH_HEAD:{p}"], check=True, capture_output=True).stdout
            for p in paths}


def f32(a):
    """float32 values as JSON numbers that round-trip exactly ('%.9g')."""
    return [float("%.9g" % x) for x in np.asarray(a, np.float32).ravel()]


class NoCatch:
    """A catch draw that always misses: the parity step is deterministic (attempts are still counted)."""
    def random(self, *a, **k):
        return 1.0

    def normal(self, *a, **k):
        return np.zeros(a[-1] if a else 1)


def vessel_at(t):
    """A vessel crossing the pond on a chord 60 u off its centre, back and forth (period 2 x 380 / 120 s)."""
    span = 380.0
    s = (t * VESSEL_SPEED) % (2 * span)
    x, vx = (s - span / 2, VESSEL_SPEED) if s < span else (span * 1.5 - s, -VESSEL_SPEED)
    return np.array([x, 60.0, -20.0], np.float32), np.array([vx, 0.0, 0.0], np.float32)


def parity_cases(A, thq, thp):
    cfg = A.Cfg()
    st = A.reset(cfg, 1, PARITY_SEED)
    rng = np.random.default_rng(PARITY_SEED + 11)
    th_q, th_p = thq[None].astype(np.float32), thp[None].astype(np.float32)
    cases = []
    for k in range(PARITY_STEPS):
        vp, vv = vessel_at(st.t)
        extra = dict(pos=vp[None], radius=VESSEL_RADIUS + 4.0, ghost_pred=(vp[None, None], vv[None, None]),
                     ghost_prey=(vp[None, None], vv[None, None]))
        if k > 0 and k % PARITY_EVERY == 0:
            c = dict(t=round(st.t, 3), vessel=dict(pos=f32(vp), vel=f32(vv), radius=VESSEL_RADIUS + 4.0))
            c["before"] = snapshot(st)
            c["food"] = f32(st.food[0])
            twin = copy_state(st)
            xq, xp, aux = A.observe(cfg, twin, extra["ghost_pred"], extra["ghost_prey"])
            oq = A.mlp(th_q, xq, A.PREY_IN, A.PREY_OUT)
            op = A.mlp(th_p, xp, A.PRED_IN, A.PRED_OUT)
            c["obs_prey"], c["obs_pred"] = f32(xq[0]), f32(xp[0])
            c["out_prey"], c["out_pred"] = f32(oq[0]), f32(op[0])
            twin.attempts_log = []
            A.apply(cfg, twin, oq, op, aux, extra=extra, rng=NoCatch())
            c["after"] = snapshot(twin)
            c["attempts"] = attempts(cfg, twin, st, aux)
            cases.append(c)
        A.step(cfg, st, th_q, th_p, extra=extra, rng=rng)
    return cases


def attempts(cfg, after, before, aux):
    """The catch attempts of the step before -> after, re-derived from the lab's swept-contact rule (the lab keeps only
    a count): per predator in order, the nearest touching living prey and its crowd."""
    P0q, P0p = before.pos[0][0], before.pos[1][0]
    Pq, Pp = after.pos[0][0], after.pos[1][0]
    can = before.alive[1][0] & (after.hand[0] <= 0)
    out = []
    for p in range(P0p.shape[0]):
        if not can[p]:
            continue
        rel0 = P0q - P0p[p]; rel1 = Pq - Pp[p]; dr = rel1 - rel0
        tt = np.clip(-(rel0 * dr).sum(-1) / np.maximum((dr * dr).sum(-1), 1e-9), 0, 1)
        dmin = np.sqrt(((rel0 + tt[:, None] * dr) ** 2).sum(-1))
        cand = np.nonzero((dmin < cfg.catch_r) & before.alive[0][0])[0]
        if len(cand) == 0:
            continue
        q = cand[np.argmin(dmin[cand])]
        crowd = int(((np.sqrt(((Pq - Pq[q]) ** 2).sum(-1)) < cfg.conf_r) & before.alive[0][0]).sum()) - 1
        out.append([int(p), int(q), crowd])
    n = int(after.attempts.sum() - before.attempts.sum())
    if n != len(out):
        raise SystemExit(f"attempt re-derivation disagrees with the lab: {len(out)} vs {n}")
    return out


def copy_state(st):
    import copy
    return copy.deepcopy(st)


def snapshot(st):
    d = {}
    for k, name in ((0, "prey"), (1, "pred")):
        d[name] = dict(pos=f32(st.pos[k][0]), vel=f32(st.vel[k][0]), fwd=f32(st.fwd[k][0]), up=f32(st.up[k][0]),
                       alive=[bool(x) for x in st.alive[k][0]], sig=f32(st.sig[k][0]))
    d["pred"].update(stam=f32(st.stam[0]), hand=f32(st.hand[0]), burst=[bool(x) for x in st.burst[0]])
    return d


def behave(A, M, thq, thp):
    """The lab's own behaviour numbers on its three seeds (arms_eval.behave's recording, without the vessel)."""
    cfg = A.Cfg()
    starts, per = [], []
    for s in BEHAVE_SEEDS:
        st = A.reset(cfg, 1, s)
        starts.append(dict(seed=s, food=f32(st.food[0]), state=snapshot(st)))
        rng = np.random.default_rng(s + 11)
        keys = dict(qp=[], qv=[], qa=[], pp=[], pv=[], pa=[], pb=[])
        th_q, th_p = thq[None].astype(np.float32), thp[None].astype(np.float32)
        for _ in range(int(BEHAVE_SECS / cfg.dt)):
            A.step(cfg, st, th_q, th_p, rng=rng, record_events=True)
            for k, v in (("qp", st.pos[0]), ("qv", st.vel[0]), ("qa", st.alive[0]), ("pp", st.pos[1]),
                         ("pv", st.vel[1]), ("pa", st.alive[1]), ("pb", st.burst)):
                keys[k].append(v[0].copy())
        rec = {k: np.array(v) for k, v in keys.items()}
        rec["dt"] = cfg.dt; rec["R_cell"] = cfg.R_cell
        rec["events"] = [(e[1], e[2], e[3], e[4], e[5], e[6]) for e in st.events]
        q = M.prey_metrics(rec)
        p = M.pred_metrics(rec, cfg)
        per.append(dict(catch_per_min=float(st.catches.sum()) / (BEHAVE_SECS / 60),
                        polarisation_local=q["polarisation_local"], polarisation_global=q["polarisation_global"],
                        social_share=q["social_share"], nnd=q["nnd"], pack_share=p["pack_share"],
                        pred_spacing=p["pred_spacing"]))
    mean = {k: round(float(np.mean([x[k] for x in per])), 3) for k in per[0]}
    return starts, dict(per_seed=per, mean=mean)


def cs_array(name, a):
    vals = np.asarray(a, np.float32).ravel()
    lines, row = [], []
    for v in vals:
        row.append(("%.9g" % v) + "f")
        if len(row) == 10:
            lines.append("            " + ", ".join(row) + ","); row = []
    if row:
        lines.append("            " + ", ".join(row) + ",")
    return f"        public static readonly float[] {name} =\n        {{\n" + "\n".join(lines) + "\n        };\n"


def policy_cs(thq, thp, sha):
    return (
        "// GENERATED by Tools/Build/substrate_harness/arms_fixture.py - do not edit. The lab's arms-race policies\n"
        "// (Docs/SUBSTRATE_FAUNA.md §11): run a9, the selfish herd, generation 1500, from the research branch\n"
        f"// {REF}, {SNAP.format(1500)} (sha256 {sha[:16]}). Layout: arms_sim.mlp - W1 [in x 32] row-major, b1, W2 [32 x 32], b2,\n"
        "// W3 [32 x out], b3. The substrate harness (group arms) checks these literals bit for bit against arms_fixture.json.\n"
        "namespace CosmicShore.Gameplay\n{\n"
        "    public static class SubstrateArmsPolicy\n    {\n"
        "        /// <summary>The schooling prey (31 inputs, 4 outputs: acceleration in the body frame, signal).</summary>\n"
        + cs_array("Prey", thq) +
        "        /// <summary>The packing predators (36 inputs, 5 outputs: acceleration, signal, burst gate).</summary>\n"
        + cs_array("Predator", thp) +
        "    }\n}\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--lab", default=None, help="a checkout of the research branch (default: git fetch it)")
    a = ap.parse_args()
    files = lab_files(a.lab)
    tmp = tempfile.mkdtemp(prefix="arms_fixture_")
    for p in (SIM, METRICS):
        open(os.path.join(tmp, os.path.basename(p)), "wb").write(files[p])
    sys.path.insert(0, tmp)
    import arms_sim as A        # noqa: E402  (the lab's own world, run as-is)
    import arms_metrics as M    # noqa: E402
    pol, sha = {}, {}
    for key, g in GENS.items():
        raw = files[SNAP.format(g)]
        z = np.load(io.BytesIO(raw))
        pol[key] = dict(thq=z["thq"].astype(np.float32), thp=z["thp"].astype(np.float32))
        sha[key] = hashlib.sha256(raw).hexdigest()
    best = pol["g1500"]
    fx = dict(source=dict(ref=REF, sim=SIM, snap=SNAP.format(1500), sha256=sha),
              cfg=A.cfg_dict(A.Cfg()),
              policy={k: dict(thq=f32(v["thq"]), thp=f32(v["thp"])) for k, v in pol.items()},
              parity=parity_cases(A, best["thq"], best["thp"]))
    starts, lab_g1500 = behave(A, M, best["thq"], best["thp"])
    _, lab_g0 = behave(A, M, pol["g0"]["thq"], pol["g0"]["thp"])
    fx["behave"] = dict(seeds=list(BEHAVE_SEEDS), secs=BEHAVE_SECS, starts=starts, lab_g1500=lab_g1500, lab_g0=lab_g0)
    js = json.dumps(fx, separators=(",", ":")) + "\n"
    cs = policy_cs(best["thq"], best["thp"], sha["g1500"])
    print(f"arms fixture: {len(fx['parity'])} parity cases, behaviour g1500 {lab_g1500['mean']}")
    print(f"                                     g0    {lab_g0['mean']}")
    if a.check:
        bad = [p for p, t in ((OUT_JSON, js), (OUT_CS, cs)) if not os.path.exists(p) or open(p).read() != t]
        if bad:
            print("FAIL - differs from what this script writes: " + ", ".join(os.path.relpath(p, REPO) for p in bad))
            return 1
        print("OK")
        return 0
    open(OUT_JSON, "w").write(js)
    open(OUT_CS, "w").write(cs)
    print(f"wrote {os.path.relpath(OUT_JSON, REPO)} ({len(js) // 1024} KB) and {os.path.relpath(OUT_CS, REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
