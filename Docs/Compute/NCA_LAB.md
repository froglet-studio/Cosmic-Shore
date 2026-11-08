# The NCA Ecology Lab on Froglet Compute (proposal, 2026-11-08)

Garrett asked whether his NCA Ecology Lab should run on Froglet Compute, and for a prototype if so. Nothing here is
merged, queued or installed. **Froglet Compute is paused** (Shombith, 2026-11-06, `README.md` top), so both job files
are `proposed`, and everything in §8 waits on Shombith.

The lab today: a runner on Garrett's PC reads the Notion backlog "🧪 NCA Ecology Lab — Experiment Backlog"
(database `569aabc276f8404981d3d6813f64c5eb`) top-down by Priority then age, skips rows whose *Your call* is Kill,
trains on the local GPU and writes Result, a growth GIF and Status (Done / Parked) back to the row. Its code lived in
Garrett's Drive folder `nca_ecology_lab` (`runner.py`, `jobs.py`, `notion_client.py`, `nca3d_trainer.py`,
`pareto_complexity.py`). On 2026-11-08 the backlog had 15 rows Queued, 0 Running, 1 Done (OPT1), every row created
2026-10-16 and none written by the runner since: the runner has not run, it is not stuck on a row.

## 1. Can Compute take a new job type?

Yes, without touching Compute. There are two kinds, `search` and `command` (`Tools/Compute/compute_lib.py:75`). A
**command** job is any program: `command.argv` with `{python}`, `{out}`, `{threads}`, `{unit.<arg>}` filled in
(`worker.py:387-389`), one exclusive **unit** per run, each unit's args free-form. Validation asks only for `argv`, and
units once `ready` (`compute_lib.py:374-379`). What a command job must provide:

| need | field | the lab's answer |
|---|---|---|
| command | `command.argv` | `nca3d_trainer.py --config "{unit.config}" --out {out}`: the Notion row's Config verbatim |
| resume | `resume` + `resume_if` | `--resume` when `ckpt.pt` (A1) / `evals.json` (A4) is in the run folder (`worker.py:390-391`) |
| progress | `progress` {file, key, of} | `status.json` step / steps (`worker.py:336-354`) |
| score | `score` {file, key} | `status.json` `loss` = 1 - best IoU: the board ranks lower as better (`worker.py:440`) |
| what is kept | `keep`, `max_mb` | globs copied to the results branch, capped (`worker.py:443-464`) |
| code | `paths`, `ref` | `Tools/NCA/lab/` exported at bleeding-edge (`worker.py:51-63`) |
| who may run it | `requires` | `torch`, `cuda` (`compute_lib.py:593`) |
| a plan | `plan` | `bioforms`, the declared non-AI plan (`Docs/AI_SYSTEM/ai_plan.json`) |

The Compute self-test validates every job file and checks the plan (`selftest.py:721-748`): with the two lab jobs it
reads `jobs: 43 ok`. Its five other failures are on a clean checkout too (seed drift in `serpent_fit`,
`breakwater_fit`, `threshertime_fit`, and `unity-train`'s catalog), not from this.

## 2. GPUs

Compute detects CUDA and nothing else: `capabilities()` asks `torch.cuda.is_available()` (`compute_lib.py:569-578`).
There is no `mps`, no VRAM size and no GPU count. A job declares a GPU only as `requires: ["cuda"]`, which the ranking
turns into "this machine lacks cuda" (`worker.py:506`); there is no per-unit requirement and no capability probe a job
can run itself. So the lab jobs require `cuda`, which leaves Apple-silicon Macs out, though the trainer runs on `mps`.
The worker lowers the run's CPU priority (`os.nice(19)`, idle class on Windows, `worker.py:394-398`). That does not
slow a GPU kernel, so a person who comes back to a game shares the GPU until the stop in §4 lands, within about 15 s.

## 3. How results leave a computer

Only through git. A unit's kept files and `unit-<id>.json` go to that computer's `compute/<user>/<host>` branch every
10 minutes and at the end (`worker.py:419-422`, `433`). There is **no post-step and no hook** on finishing: the worker
writes the unit record and returns. A Notion writeback inside the job would need a Notion token on every
contributor's computer, which is a no. So the writeback runs on ONE computer (Garrett's PC, where the token already
is, or a scheduled session), reads the results branches the way the board does (`compute_lib.gather`,
`merged_job`), and writes the rows: `Tools/NCA/lab/notion_writeback.py push`.

## 4. The relay, and whether a 4-hour job survives the idle stop

A command unit runs until the person comes back, the session's budget runs out (`--session`, 60 minutes by default,
`compute.py:570`) or it finishes. Then the worker sends SIGINT (CTRL_BREAK on Windows), waits 60 s and kills it
(`worker.py:414-430`). The unit counts as done only if the process exits 0 at progress 1.0 (`worker.py:432`). Another
session, on this computer or another, takes the unit with the most progress first (`worker.py:375`). If the latest
progress is on another computer, it pulls that unit's kept files from that computer's results branch
(`worker.py:383-386`, `467-478`) and restarts with `--resume` (`worker.py:390`). While a unit runs, its heartbeat is a
lease (`lease_hours`) that keeps other computers off it (`worker.py:364-371`).

So a 4h+ job survives if, and only if, it checkpoints on SIGINT and exits 0. Garrett's Drive trainer did neither (no
checkpoint, no signal handling): every session would have restarted it from zero. What the prototype adds:

- **A1 (`nca3d_trainer.py`)**: `ckpt.pt` (model, optimiser, scheduler, both RNGs, history; under 1 MB) every 100
  iterations and on the signal, then exit 0. The training **pool** (256 × 16 × 24³ ≈ 110 MB in fp16) is over the
  relay cap, so it stays on the computer that trained it (`pool_local.pt`). A run relayed elsewhere reseeds its pool
  and says so in `status.json`. That costs little for A1. For A2 (regeneration) it is the open question in §8.
- **A4 (`diamond_nca.py`)**: every evaluation (a short NCA training per candidate size) is cached in `evals.json`, a
  few KB. Resuming re-runs the controller from the same seed: cached evaluations answer at once, the RNG draws the
  same children, and the search reaches where it stopped. The self-test proves a stopped-and-resumed search ends
  where an unbroken one does, re-running no cached evaluation. At most the one evaluation in flight is lost. On the
  same computer even that one resumes from its own checkpoint.

## 5. Kill and Prioritize

The lab's order is Priority, then age, with Kill skipped (`runner.py select_next`). Compute's is per job, never per
unit: `value × unblocks × asked × starvation / crowding` (`compute_lib.py:932-945`). Within a job, units run in file
order, except that a unit already in progress goes first (`worker.py:371-376`). The mapping:

- **Kill** drops the row's unit from the job's unit list. **Prioritize** moves it to the top, then P0 / P1 / P2,
  then age (`notion_writeback.py steer`, which reads the backlog and rewrites the unit lists with `--write`).
- **It is slow.** A command job runs only from a job file on bleeding-edge (`COMMAND_REFS`, `compute_lib.py:72`,
  `untrusted` at `458-470`; a job from `compute-queue` or a person's branch is refused). So a phone tap reaches the
  computers only when someone commits and merges the steer. The runner reacts at its next poll.
- **It costs relay.** A results epoch hashes the whole `command` block, units included (`compute_lib.py:517-524`,
  `worker.py:361`). Reordering or dropping one unit starts a new epoch, so every unit in flight loses its relay. That
  is Compute's rule ("results are only comparable within one epoch") applied to the unit list. The fix is §8 Q3.

## 6. Recommendation: both, split by what each is good at

**Keep the standalone runner** as the lab: it steers from the phone at once, runs every job kind (including the
not-yet-written ones it parks), and needs no PR. **Add to Compute only the long, settled, checkpointable trainings**:
A4 (the capacity search, hours), then A8 and the A3/A5 sweeps once written. Those gain from many idle GPUs and lose
little to a merge between a decision and a run. A1, A2 and A6 run on either. A1 is the first one to prove the bridge
on, because it is short.

First, the runner itself is idle because it never started, not because of the queue. `bootstrap.ps1` plus the
Notion token in `.env` on Garrett's PC is the only thing between today and A1 running tonight.

## 7. What the prototype is (branch `cece/sleepy-bohr-0brphq`, not merged)

| file | what |
|---|---|
| `Tools/NCA/lab/nca3d_trainer.py` | Garrett's trainer. Model, targets, loss, metrics and GIF unchanged. Adds the Compute contract: a CLI taking the row's Config, `status.json`, checkpoint and resume, exit 0 on SIGINT/SIGTERM/SIGBREAK, cuda > mps > cpu, `--require-gpu`. `run(config)` is kept, so the runner works as before |
| `Tools/NCA/lab/pareto_complexity.py` | the diamond optimizer, as in Drive |
| `Tools/NCA/lab/diamond_nca.py` | **A4, new** (the runner parked it as not implemented): genome (channels, hidden), complexity = parameters, score = a short training's stability IoU; resumable through `evals.json` |
| `Tools/NCA/lab/notion_writeback.py` | `push` (Running, then Done + the runner's Result line + the GIF) and `steer` (§5). Dry run unless `--apply`. Stdlib only. Token from `NOTION_TOKEN` or a git-ignored `.env`, never printed |
| `Tools/NCA/lab/selftest.py` | the checks below |
| `Docs/Compute/jobs/nca-lab-nca3d.json` | unit `lab-a1` → row A1, `proposed` |
| `Docs/Compute/jobs/nca-lab-diamond.json` | unit `lab-a4` → row A4, `proposed` |

`runner.py`, `jobs.py`, `notion_client.py` and `bootstrap.ps1` stay in Drive: they are the lab's, not Compute's.
Nothing under `Tools/Compute/` changed.

Checks (`python3 Tools/NCA/lab/selftest.py`; set `NCA_LAB_PYTHON` to an interpreter with torch for steps 5-6):
1. The diamond optimizer on its toy problem: it grows past 12 features, then compresses to exactly 12.
2. `diamond_nca` with a synthetic score: a run stopped half way and resumed ends where an unbroken run ends, with no
   cached evaluation re-run. Negative control: clearing the cache fails it.
3. Notion, dry: the runner's Result line; Kill dropped and Prioritize first (negative control: a surviving Kill
   fails); `push` writes Running once, then Done once with the GIF; `steer` keeps a unit's id.
4. The job files validate under Compute's own rules, are on a declared plan, are not `ready`, name scripts that
   exist, require `cuda`, and keep every file they resume from.
5. torch: the trainer, interrupted by SIGINT, exits 0 with a checkpoint and resumes to the end.
6. torch: **Compute's own `worker.command_session`** runs the A1 job's argv (tiny config, CPU), is stopped by a fake
   "person came back", then a second session resumes and finishes the unit with `result.json`, `growth.gif` and
   `model.pt` kept. No agent is installed, nothing is pushed, and `FROGLET_COMPUTE_HOME` is a temporary folder.

## 8. Open questions (only Shombith can answer; nothing moves until he does)

1. **Take the lab on at all, while paused?** Both jobs stay `proposed` until Compute is switched back on and he says
   yes. Is `plan: bioforms` right for flora prismscapes, or should `ai_plan.json` `other_plans` gain an
   `nca-ecology-lab` entry?
2. **GPU capability.** Should `capabilities()` learn `mps` (and maybe VRAM), so a job can say "a GPU" instead of
   "CUDA"? Today it leaves Macs out.
3. **Steering without a new epoch.** Could units leave the epoch hash (or be hashed per unit), so that reordering or
   dropping a unit keeps the others' relay? And could a command job's unit list, as data, come from a ref
   other than bleeding-edge (`compute-queue`)? Then a Kill would not wait for a merge.
4. **Bigger relays.** Is raising `max_mb` (or git-LFS / R2 for unit files) acceptable for a pool checkpoint around
   110 MB? A2's regeneration training would keep its pool across computers.
5. **A post-step hook.** Is the "writeback runs on one computer" pattern (§3) what he wants, or would he rather
   Compute grew an `on_done` that the console or hub runs centrally? Either way, no token goes on contributors'
   computers.
6. **GPU etiquette.** Is SIGINT within about 15 s of input enough for a GPU job on someone's gaming PC, or should GPU
   jobs default to a `--between` night window?
