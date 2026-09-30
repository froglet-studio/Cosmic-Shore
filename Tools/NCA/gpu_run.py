"""One command: train the prism swim on your GPU and hand the result back to this branch.

    git pull
    python Tools/NCA/gpu_run.py                 # check -> train -> pick -> figures -> verify -> viewer -> commit + push
    python Tools/NCA/gpu_run.py check           # only: does this GPU agree with the CPU on one step?
    python Tools/NCA/gpu_run.py --no-push       # everything except git
    python Tools/NCA/gpu_run.py --steps 6000    # a longer run
    python Tools/NCA/gpu_run.py --device mps    # Apple silicon

Re-running resumes: the run lives in Tools/NCA/runs/prism_swim3d_gpu (gitignored) and training picks
up from its last checkpoint. Needs torch with CUDA (or MPS), numpy, pillow; node for the JS
verifier (skipped with a warning if missing).

What the GPU buys over the CPU run: the settings the 4-core cloud session had to cut - full
backprop through the rollout (not the last 48 steps), batch 8 (not 4), more particles - and it
ranks every 250-step snapshot as a swimmer, which was being done by hand.
"""
import argparse
import json
import math
import os
import shutil
import subprocess
import sys
import time

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import particle_nca as pn  # noqa: E402

RUN = os.path.join(HERE, "runs", "prism_swim3d_gpu")
RESULT = os.path.join(HERE, "results", "prism_swim3d")
INIT = os.path.join(HERE, "results", "particle_swim2d", "model.pt")


def config(steps):
    return pn.PConfig(experiment="prism3d", amp=5.0, init=INIT, clock_steps=1500, steps=steps,
                      lr_drop_step=int(steps * 2 / 3), batch_size=8, pool_size=1024, pool_seeds=2,
                      damage_n=2, bptt=0, world={"capacity": 360})


def banner(msg):
    print(f"\n=== {msg} ===", flush=True)


# ------------------------------------------------------------------ check ---

def check(device):
    """One step and one loss gradient, on the CPU and on the device, from the same state: every
    position, channel and gradient must agree. A GPU that disagrees is not worth training on."""
    banner(f"check: {device} vs cpu")
    torch.backends.cuda.matmul.allow_tf32 = False
    torch.backends.cudnn.allow_tf32 = False
    pn.set_device("cpu")
    cfg = config(10)
    world = pn.make_world(cfg)
    sd = pn.widen_channels(pn.lift_2d_to_3d(torch.load(INIT, map_location="cpu"), 16, 128), 16, 32, 3)
    g = torch.Generator().manual_seed(0)
    sd["w2"][16:32] = torch.randn(16, 128, generator=g) * 0.02      # make the prism channels move
    ca = pn.ParticleNCA(world, 32)
    ca.load_state_dict(sd)
    torch.manual_seed(0)
    frames = torch.from_numpy(pn.build_targets(cfg).astype(np.float32))
    grid = tuple(frames.shape[1:-1])
    x = pn.seed_state(2, world, [22.0, 22.0, 11.0], 32)
    with torch.no_grad():
        for _ in range(40):
            x = ca(x)
    fire = torch.rand(x.pos.shape[:2], generator=g) < 0.5

    def run(dev):
        pn.set_device(dev)
        m = pn.ParticleNCA(world, 32).to(dev)
        m.load_state_dict({k: v.to(dev) for k, v in sd.items()})
        st = pn.State(x.pos.to(dev), x.s.to(dev), x.active.to(dev))
        y = m(st, fire=fire.to(dev), bud=False)
        loss = pn.frame_mse(pn.make_render(cfg, world, grid)(y), frames.to(dev)).mean()
        loss.backward()
        return y.pos.detach().cpu(), y.s.detach().cpu(), y.active.cpu(), float(loss.detach()), m.w1.grad.cpu()

    t0 = time.time(); a = run("cpu"); tc = time.time() - t0
    t0 = time.time(); b = run(device); b = run(device); td = (time.time() - t0) / 2
    pn.set_device("cpu")
    same_active = bool(torch.equal(a[2], b[2]))
    dpos, ds = float((a[0] - b[0]).abs().max()), float((a[1] - b[1]).abs().max())
    dl = abs(a[3] - b[3]) / max(abs(a[3]), 1e-12)
    dg = float((a[4] - b[4]).norm() / a[4].norm().clamp(min=1e-12))
    print(f"particles alive identical: {same_active}  max |pos| diff {dpos:.2e}  max |state| diff {ds:.2e}")
    print(f"loss rel diff {dl:.2e}  w1-gradient rel diff {dg:.2e}  one step+loss+grad: cpu {tc:.2f}s, {device} {td:.2f}s")
    ok = same_active and dpos < 1e-3 and ds < 1e-3 and dl < 1e-3 and dg < 1e-2
    print("CHECK OK" if ok else "CHECK FAILED - the device disagrees with the CPU; not training on it")
    return ok


# ------------------------------------------------------------------ train ---

def train(device, steps):
    banner(f"train on {device}: {steps} steps, full backprop, batch 8 -> {RUN}")
    pn.set_device(device)
    resume = os.path.isfile(os.path.join(RUN, "state.json"))
    if resume:
        done = json.load(open(os.path.join(RUN, "state.json")))["step"]
        if done >= steps:
            print(f"already trained to step {done}")
            return
        print(f"resuming from step {done}")
    pn.train(config(steps), RUN, resume=resume)


# ------------------------------------------------------------------- pick ---

def pick(device, steps):
    """Score every pool-stage snapshot as a swimmer (4 seeds, 1000 steps) and keep the best:
    no extinctions, every seed within 20% of the target tempo, then lowest in-place error."""
    banner("pick the best snapshot")
    pn.set_device(device)
    cfg = pn.PConfig(**json.load(open(os.path.join(RUN, "config.json"))))
    names = sorted(f for f in os.listdir(RUN) if f.startswith("model_") and int(f[6:11]) >= cfg.clock_steps)
    if not names:
        sys.exit(f"no pool-stage snapshots in {RUN} yet (snapshots every {cfg.snapshot_every} steps after "
                 f"step {cfg.clock_steps}) - train longer first")
    scores = []
    for f in names:
        s = pn.score_snapshot(RUN, f, seeds=4, horizon=1000)
        s["step"] = int(f[6:11])
        scores.append(s)
        tempo = ", ".join(f"{v:.1f}" for v in s["tempo"])
        print(f"step {s['step']:5d}: tempo [{tempo}]  error {s['log10_error']:+.2f}  frame gap {s['frame_gap']:.2f}  "
              f"extinct {s['extinct']}  particles {s['particles']}", flush=True)
    json.dump(scores, open(os.path.join(RUN, "snapshot_scores.json"), "w"), indent=2)
    tgt = cfg.period
    good = [s for s in scores if s["extinct"] == 0 and s["tempo"] and all(abs(v / tgt - 1) < 0.2 for v in s["tempo"])]
    if good:
        best = min(good, key=lambda s: s["log10_error"])
        why = f"best in-place error of {len(good)} on-tempo snapshots"
    else:
        best = min(scores, key=lambda s: (s["extinct"], np.mean([abs(v / tgt - 1) for v in s["tempo"]]) if s["tempo"] else 9))
        why = "no snapshot held tempo on every seed; closest to tempo"
    print(f"picked step {best['step']} ({why})")
    return best, why, len(scores)


# ---------------------------------------------------------------- promote ---

def promote(device, best, why, n, steps):
    banner(f"promote step {best['step']} -> {RESULT}")
    os.makedirs(RESULT, exist_ok=True)
    for f in ("config.json", "frames.npy", "loss.npy", "snapshot_scores.json"):
        shutil.copy(os.path.join(RUN, f), os.path.join(RESULT, f))
    shutil.copy(os.path.join(RUN, f"model_{best['step']:05d}.pt"), os.path.join(RESULT, "model.pt"))
    pn.set_device("cpu")
    cfg, ca, _ = pn.load_run(RESULT)
    pn.export(ca, cfg, os.path.join(RESULT, "weights.json"))
    name = torch.cuda.get_device_name() if device.startswith("cuda") else device
    json.dump({"training": False, "step": best["step"], "of": steps, "device": name,
               "note": f"picked by gpu_run.py from {n} snapshots: {why}"},
              open(os.path.join(RESULT, "status.json"), "w"))
    banner("figures")
    pn.set_device(device)
    pn.figures(RESULT, horizon=1500)
    pn.set_device("cpu")


def verify():
    banner("verify the browser runner against torch")
    if not shutil.which("node"):
        print("node not found - skipping the JS verifier (the viewer still builds)")
        return True
    r = subprocess.run([sys.executable, os.path.join(HERE, "verify_particle_js.py"), "--run", RESULT])
    return r.returncode == 0


def viewer():
    banner("rebuild viewer.html")
    subprocess.run([sys.executable, os.path.join(HERE, "build_viewer.py")], check=True)


def push(best):
    banner("commit and push")
    root = subprocess.check_output(["git", "rev-parse", "--show-toplevel"], cwd=HERE, text=True).strip()
    branch = subprocess.check_output(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=root, text=True).strip()
    rel = lambda p: os.path.relpath(p, root)
    subprocess.run(["git", "add", rel(RESULT), rel(os.path.join(HERE, "viewer.html"))], cwd=root, check=True)
    msg = (f"feat(tools): prism swim trained on GPU - snapshot {best['step']}\n\n"
           f"Tempo {', '.join(f'{v:.2f}' for v in best['tempo'])} steps/frame (target {best['target_tempo']}), "
           f"in-place error 10^{best['log10_error']:.2f}, frame gap {best['frame_gap']:.2f}, "
           f"{best['particles']} prisms. Produced by Tools/NCA/gpu_run.py.")
    subprocess.run(["git", "commit", "-m", msg], cwd=root, check=True)
    subprocess.run(["git", "push", "-u", "origin", branch], cwd=root, check=True)
    print(f"pushed to {branch}")


def main():
    global RUN
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("stage", nargs="?", default="all", choices=["all", "check", "train", "pick", "promote"])
    ap.add_argument("--device", default="cuda")
    ap.add_argument("--steps", type=int, default=4500)
    ap.add_argument("--no-push", action="store_true")
    ap.add_argument("--run", default=RUN, help="training directory (default runs/prism_swim3d_gpu)")
    a = ap.parse_args()
    RUN = a.run
    if a.device.startswith("cuda") and not torch.cuda.is_available():
        sys.exit(f"torch {torch.__version__} cannot see a CUDA GPU.\n"
                 + ("  This is the CPU-only build ('+cpu'). Replace it with a CUDA build, e.g.:\n"
                    "    pip uninstall -y torch\n"
                    "    pip install torch --index-url https://download.pytorch.org/whl/cu128\n"
                    "  (pick the CUDA version for your driver at https://pytorch.org/get-started/locally/ ;\n"
                    "   `nvidia-smi` shows the highest CUDA version your driver supports)\n"
                    if "+cpu" in torch.__version__ else
                    "  Check the NVIDIA driver (`nvidia-smi`) and that this torch's CUDA version is supported by it.\n")
                 + '  Then: python -c "import torch; print(torch.cuda.is_available(), torch.cuda.get_device_name())"')
    print(f"torch {torch.__version__}, device {a.device}" +
          (f" ({torch.cuda.get_device_name()})" if a.device.startswith("cuda") and torch.cuda.is_available() else ""))
    if a.stage in ("all", "check"):
        if not check(a.device):
            sys.exit(1)
        if a.stage == "check":
            return
    if a.stage in ("all", "train"):
        train(a.device, a.steps)
    if a.stage in ("all", "pick", "promote"):
        best, why, n = pick(a.device, a.steps)
        promote(a.device, best, why, n, a.steps)
        ok = verify()
        viewer()
        if not ok:
            sys.exit("JS verifier failed - result promoted locally but not pushed")
        if not a.no_push:
            push(best)


if __name__ == "__main__":
    main()
