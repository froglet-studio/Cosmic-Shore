# Single-plan specialists (round 1b)

The user's idea: train each body plan SEPARATELY first, to confirm what one rule can do for one
plan, then use HYBRIDS of the separately trained genomes to seed the gene pool for the search with
the combined four-plan loss.

Every specialist starts from the SAME rule (G2: `Tools/NCA/results/swarm_coevo_g2/rule.pt`, 5/8,
full-size swarms) and the same world flags, and trains on ONE plan only (`TrainCfg.only`). Starting
from one point is what makes the hybrids meaningful: each specialist's genome is G2 plus a "task
vector" tau_k = theta_k - theta_G2, and networks fine-tuned from a shared start stay in one basin,
so they can be added (task arithmetic / model soups): hybrid = theta_G2 + sum_k a_k tau_k, plus
crossovers (per-layer picks). Hybrids with different coefficients seed the population for the
combined-loss search (gradient or evolution).

## Brief for one specialist session (PLAN is mass, space, charge or time)

You are babysitting ONE training run in the Cosmic Shore NCA swarm research (Tools/NCA). Do not ask
questions; work autonomously; do not edit code. Context: Tools/NCA/README.md (swarm sections) and
this file.

1. Create and check out branch `cece/swarm-solo-PLAN` from cece/gifted-curie-x2cpd0 (commit a3af90c1
   or later, which adds `TrainCfg.only`). Push only there; never open a PR. Commit messages end with
   the two attribution lines from your system prompt.
2. Torch CPU: `pip install torch --index-url https://download.pytorch.org/whl/cpu` if missing (and numpy).
3. Launch with the Bash tool, run_in_background=true, timeout 7200000:
   cd <repo root> && NCA_THREADS=4 python3 -u -W ignore Tools/NCA/gpu_run.py swarm --device cpu --tag solo_PLAN --steps 3000 --set only=PLAN --set p_switch=0 --set per_kind=4 --set pool=24 --set seed_every=4 --set roll_min=48 --set roll_max=96 --set bptt=28 --set sticky_plan=1 --set w_over=1 --set learned_lay=1 --set min_body=76 --set w_body=20 --set init=Tools/NCA/results/swarm_coevo_g2/rule.pt >> Tools/NCA/runs/solo_PLAN.out 2>&1
   It resumes from Tools/NCA/runs/swarm_solo_PLAN on relaunch (snapshots every 100 steps). Its log
   lines show only this plan (`pl>pl sink n.. d..`; `s<N>` is the largest |state|, which must stay
   small). Every 1000 steps it scores all four seedings (only its own plan is expected to pass) and
   may publish to Tools/NCA/results/swarm_coevo_solo_PLAN.
4. At EVERY check-in, also copy the newest Tools/NCA/runs/swarm_solo_PLAN/rule_*.pt to
   Tools/NCA/results/solo_PLAN/rule_latest.pt, write Tools/NCA/results/solo_PLAN/STATUS.md (step,
   last 5 log lines), commit and push. This rule is the genome the hybrid stage needs.
5. When training finishes, run `python3 Tools/NCA/swarm_probe.py --rule Tools/NCA/results/solo_PLAN/rule_latest.pt > Tools/NCA/results/solo_PLAN/probe.json`
   and commit + push.
6. If the background task exits before 3000 steps, relaunch the same command (it resumes). Fix
   only environment problems. Schedule your own check-ins with send_later (~45 min); stop after the
   run finishes or 23:00 UTC; make sure everything is pushed in the final turn. Never kill a
   process by name (use its PID or TaskStop). Keep the working tree clean.
