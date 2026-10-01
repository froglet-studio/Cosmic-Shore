"""The swarm co-evolution on your GPU, publishing back to the branch while it trains.

    git pull
    python Tools/NCA/gpu_run.py swarm               # check -> train (resumable) -> publish every 1000 steps
    python Tools/NCA/gpu_run.py swarm-check         # only: does this GPU agree with the CPU on one step?
    python Tools/NCA/gpu_run.py swarm --steps 12000 --no-push

What it trains: ONE tadpole rule, four seedings (Mass, Space, Charge and Time majorities). Every
swarm is scored against ONE plan only - the plan of its CURRENT majority element (Mass -> whale,
Space -> jellyfish, Charge -> pufferfish, Time -> dragonfly) - and the total is the sum. Headcount is
not a goal. Some pool swarms lose enough of their majority element (eaten) that another element takes
over; from then on they are scored against the new majority's plan, which is how a rule can learn to
switch. Warm-started from the cloud session's best
rule (results/swarm_coevo/warm_start.pt). The GPU buys what the 4-core cloud run had to cut:
batch 16 (4 samples per body plan, not 2), rollouts of 64-128 steps (not 48-96), 48 backpropagated
steps (not 28).

Every 1000 steps it grows each plan from a fresh seed for 240 steps, scores it against every target,
then removes enough of its majority element to hand the majority to another element and runs 240 more
steps (whale->jellyfish, jellyfish->pufferfish, pufferfish->dragonfly, dragonfly->whale). Eight tests:
each seeding closest to its own plan, each switched swarm closest to its new plan. If that is the best so far writes results/swarm_coevo_gpu/ (rollout, cross-score
summary, rule, training log) and commits + pushes ONLY that folder. It never touches viewer.html, so
it cannot conflict with the cloud session, which rebuilds the viewer from whatever lands. A failed
pull or push is reported and training carries on. Re-running resumes from runs/swarm_gpu.
"""
import json
import os
import shutil
import subprocess
import sys
import time

import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402

RUN = os.path.join(HERE, "runs", "swarm_gpu2")          # v2: scored against the current majority plan
RESULT = os.path.join(HERE, "results", "swarm_coevo_gpu")
WARM = os.path.join(HERE, "results", "swarm_coevo", "warm_start.pt")


def banner(msg):
    print(f"\n=== {msg} ===", flush=True)


OVERRIDES = {}                          # --set key=value (TrainCfg fields), e.g. from a parallel experiment


def config(steps, anim=False):
    cfg = sn.TrainCfg(run=RUN, init=WARM, steps=steps, per_kind=4, pool=32, seed_every=4,
                      roll_min=64, roll_max=128, bptt=48, snap_every=100, log_every=10, anim=int(anim))
    for k, v in OVERRIDES.items():
        t = type(getattr(cfg, k))
        setattr(cfg, k, str(v).lower() in ("1", "true", "yes") if t is bool else t(float(v)) if t is int else t(v))
    return cfg


def configure(tag=None, overrides=None):
    """A parallel experiment: its own run and result folders (results/swarm_coevo_<tag>)."""
    global RUN, RESULT
    if tag:
        RUN = os.path.join(HERE, "runs", f"swarm_{tag}")
        RESULT = os.path.join(HERE, "results", f"swarm_coevo_{tag}")
    for kv in overrides or []:
        k, v = kv.split("=", 1)
        assert hasattr(sn.TrainCfg, k) or k in sn.TrainCfg.__dataclass_fields__, f"unknown TrainCfg field {k}"
        OVERRIDES[k] = v


def check(device):
    """One step + the four-plan loss + its gradient, on the CPU and on the device, from the same state
    and the same firing mask. A device that disagrees is not worth training on."""
    banner(f"swarm check: {device} vs cpu")
    torch.backends.cuda.matmul.allow_tf32 = False
    torch.backends.cudnn.allow_tf32 = False
    sn.set_device("cpu")
    world = sn.World()
    rule = sn.SwarmRule(world)
    sd = torch.load(WARM, map_location="cpu", weights_only=False)["rule"]
    for k, v in rule.state_dict().items():
        if sd[k].shape != v.shape:
            pad = torch.zeros_like(v); pad[tuple(slice(0, d) for d in sd[k].shape)] = sd[k]; sd[k] = pad
    rule.load_state_dict(sd)
    targets = sn.load_targets()
    g = sn.make_gen(0)
    sw = sn.seed_swarm([targets[k] for k in sn.KINDS], world, g)
    with torch.no_grad():
        for _ in range(40):
            sw = rule(sw, g)
    fire = torch.rand(sw.pos.shape[:2], generator=g) < 0.5

    def run(dev):
        sn.set_device(dev)
        m = sn.SwarmRule(world).to(dev)
        m.load_state_dict({k: v.to(dev) for k, v in rule.state_dict().items()})
        T = sn.load_targets()
        st = sn.Swarm(*[getattr(sw, a).to(dev) for a in sn.Swarm.FIELDS])
        y = m(st, bud=False, fire=fire.to(dev))
        L = sn.LossCfg()
        loss = sum(sn.swarm_loss(sn.decode(y, b), T[k], L)[0] for b, k in enumerate(sn.KINDS))
        loss.backward()
        return y.pos.detach().cpu(), y.s.detach().cpu(), y.active.cpu(), float(loss.detach()), m.w1.grad.cpu()

    t0 = time.time(); a = run("cpu"); tc = time.time() - t0
    t0 = time.time(); b = run(device); b = run(device); td = (time.time() - t0) / 2
    sn.set_device("cpu")
    same = bool(torch.equal(a[2], b[2]))
    dpos, ds = float((a[0] - b[0]).abs().max()), float((a[1] - b[1]).abs().max())
    dl = abs(a[3] - b[3]) / max(abs(a[3]), 1e-12)
    dg = float((a[4] - b[4]).norm() / a[4].norm().clamp(min=1e-12))
    print(f"alive identical: {same}  max |pos| diff {dpos:.2e}  max |state| diff {ds:.2e}")
    print(f"loss rel diff {dl:.2e}  w1-gradient rel diff {dg:.2e}  one step+loss+grad: cpu {tc:.2f}s, {device} {td:.2f}s")
    ok = same and dpos < 1e-3 and ds < 1e-3 and dl < 1e-3 and dg < 2e-2
    print("CHECK OK" if ok else "CHECK FAILED - the device disagrees with the CPU; not training on it")
    return ok


def score(summary):
    """Lower is better. First: how many of the eight tests pass - each seeding grows closest to its OWN
    plan (4), and after losing its majority each swarm ends closest to the NEW majority's plan (4).
    Then: how close, summed over the same eight."""
    cross, sw = summary["cross"], summary.get("switch", {})
    correct = sum(min(cross[k], key=cross[k].get) == k for k in sn.KINDS)
    close = sum(cross[k][k] for k in sn.KINDS)
    for k, v in sw.items():
        correct += min(v["cross"], key=v["cross"].get) == v["to"]
        close += v["cross"][v["to"]]
    return (8 - correct) * 1000 + close, correct


def git(args, root):
    r = subprocess.run(["git"] + args, cwd=root, capture_output=True, text=True)
    if r.returncode:
        print(f"git {' '.join(args)} failed: {r.stderr.strip()[:300]}")
    return r.returncode == 0


def make_publisher(device, push):
    state_path = os.path.join(RUN, "published.json")
    best = json.load(open(state_path)) if os.path.isfile(state_path) else {"score": 1e18}

    def publish(step, rule):
        if step % 1000:
            return
        banner(f"step {step}: grow every plan from a fresh seed and score it")
        rule.eval()
        L = sn.LossCfg(scale_inv=int(float(OVERRIDES.get("scale_inv", 0))))   # score with the loss it trains on
        data, summary = sn.rollout(rule, 240, L=L)
        summary["scale_inv"] = L.scale_inv
        rule.train()
        sn.print_cross(summary); sn.print_geo(summary); sn.print_switch(summary)
        sc, correct = score(summary)
        print(f"score {sc:.1f} ({correct}/8 tests pass: own plan x4, switch x4); best so far {best['score']:.1f}")
        if sc >= best["score"]:
            return
        best.update(score=sc, step=step, correct=correct)
        json.dump(best, open(state_path, "w"))
        os.makedirs(RESULT, exist_ok=True)
        json.dump(sn.pack(data, 240), open(os.path.join(RESULT, "rollout.json"), "w"))
        name = torch.cuda.get_device_name() if device.startswith("cuda") else device
        summary["meta"] = {"device": name, "step": step, "tag": os.path.basename(RESULT), "overrides": OVERRIDES,
                           "note": f"Trained on {name} ({os.path.basename(RESULT)}), snapshot {step}."}
        summary["rule"] = f"rule_{step:05d}.pt"
        json.dump(summary, open(os.path.join(RESULT, "summary.json"), "w"), indent=1)
        shutil.copy(os.path.join(RUN, "log.jsonl"), os.path.join(RESULT, "log.jsonl"))
        torch.save(dict(rule={k: v.cpu() for k, v in rule.state_dict().items()}, world=sn.asdict(rule.world),
                        hidden=rule.hidden, step=step), os.path.join(RESULT, "rule.pt"))
        if not push:
            return
        root = subprocess.check_output(["git", "rev-parse", "--show-toplevel"], cwd=HERE, text=True).strip()
        branch = subprocess.check_output(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=root, text=True).strip()
        rel = os.path.relpath(RESULT, root)
        git(["pull", "--ff-only", "origin", branch], root)
        if git(["add", rel], root) and git(["commit", "-m", f"feat(nca): swarm co-evolution {os.path.basename(RESULT)} on {name} - step {step}, "
                                                          f"{correct}/8 plan + switch tests pass"], root):
            git(["push", "origin", branch], root)

    return publish


def run(device, steps, push=True, anim=False):
    if not check(device):
        sys.exit(1)
    banner(f"train on {device}: {steps} steps -> {RUN}")
    sn.set_device(device)
    cfg = config(steps, anim)
    sn.train(cfg, sn.World(), sn.LossCfg(), resume=True, on_snapshot=make_publisher(device, push))
    sn.set_device("cpu")
