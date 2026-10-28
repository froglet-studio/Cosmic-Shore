# Froglet Compute: handoff to Shombith (2026-10-26)

Garrett asked for this system (a queue of learning searches the team's idle computers work through, a board, a scout
that finds new work) and, the same day, handed it to you entirely: it is tooling, and tooling is yours (CLAUDE.md).
Everything is on `cece/clever-ritchie-q8270q`, one commit (`d15a561106`), **not merged**. Nothing in it changes a game
file, a C# file, a hook or `.claude/settings.json`. Every decision below is yours to make, including "no".

Read in this order: `Docs/Compute/README.md` (the design, section 12 is the honest verification status), this file,
`Docs/AI_SYSTEM/REGATTA_BALANCE_PLAN.md` (the first plan it serves; Yash follows it).

## 1. What is on the branch

| | |
|---|---|
| `Tools/Compute/` | the CLI (`compute.py`), worker, searchers (sep-CMA-ES, MAP-Elites), board, scout, OS installers, self-test, 7 evaluator modules. Python 3 stdlib; numpy for the Regatta jobs, torch for the NCA job |
| `Docs/Compute/` | README, 12 job files, generated BOARD / OPPORTUNITIES (+ json), `BRANCH` (the branch name the sweep merges until this is on `bleeding-edge`) |
| 5 new skills | `/compute`, `/compute-job`, `/compute-scout`, `/regatta-balance`, `/bioform-forge` |
| 8 skills touched (short, additive) | vessel-ai §7, labmaker §2.3, arcadegame §5, arenagame §5, fauna intro, element-ability-table, ship §3.57, team-tasks "Never" |
| other docs | `CLAUDE.md` index entry, `Docs/claude/DOCUMENTATION_INDEX.md`, `Docs/AI_SYSTEM/README.md`, `Docs/AI_SYSTEM/REGATTA_BALANCE_PLAN.md`, `Docs/Reports/COMPUTE.md` + the `compute` row in `Docs/Reports/areas.json` (owner: you) |

## 2. Verified, and not

**Verified** (Linux cloud session, Python 3.13): `python3 Tools/Compute/compute.py self-test` green - the searchers
(with a shuffled-loss negative control), the Python port of the Regatta course generator against the C# harness's own
output (every gate, lane point and measurement at all four intensities, plus a negative control), every evaluator's
self-test, every job file, and an end-to-end run of two simulated machines against a throwaway bare remote (both
publish, the second starts from the first one's best, the board merges them, the scratch checkout stays clean and is
not made sparse). Two real local sessions: `drive-rhino-headlong` and `bioform-forge`.

**Not verified:** the Windows and macOS installers (`schtasks /SC ONIDLE`, LaunchAgent), Windows/macOS idle and
battery detection (ctypes / `ioreg` / `pmset`), a real push of a `compute/<user>/<host>` branch to origin, any torch
job (the NCA command job and its checkpoint relay between machines), the self-update path (supervisor re-running from a
`bleeding-edge` worktree) since `Tools/Compute` is not on `bleeding-edge` yet.

## 3. What to review first (where the risk is)

1. **The person's checkout.** `worker.prepare_target` and `compute._home_worktree` add sparse worktrees under
   `~/.froglet-compute/` with `git worktree add` + `git sparse-checkout`. On git 2.43 this enabled
   `extensions.worktreeConfig` in the shared repo config (harmless, but a repo-level change), and the main checkout
   stayed non-sparse. Check this on Windows git, and on the oldest git anyone uses.
2. **Pushes.** `compute_lib.publish` builds one commit with a private index (`GIT_INDEX_FILE`) and pushes it to
   `refs/heads/compute/<user>/<host>` only. That is one new branch per machine on origin; decide whether that
   namespace is acceptable (the alternative is a separate results repo).
3. **The installers.** They run `compute.py run --when-idle 10` on every idle period from the person's checkout, with
   their git credentials. `install --dry-run` prints exactly what each OS gets.
4. **Self-update.** Between sessions the worker runs `Tools/Compute` from `origin/bleeding-edge` (`--home`). That
   means whatever merges to `bleeding-edge` runs on everyone's machine at idle. Decide whether that is right, or pin
   it (`--home` / `--no-update`).

### Review round 1 (Shombith's session, 2026-10-26, branch `claude/eager-franklin-e6g77h`)

| Risk | Found | Done |
|---|---|---|
| 1. The person's checkout | Reproduced on git 2.43: `sparse-checkout init` in a worktree writes `extensions.worktreeConfig = true` into the person's own `.git/config`, which `uninstall` never removed; every job also added a worktree to their repository | Jobs and the self-update now use `compute_lib.export_tree` (`git archive` into `~/.froglet-compute`). Nothing is written to the repository but objects and the `compute/*` remote-tracking refs. The self-test checks both |
| 2. Pushes | `publish` is sound (plumbing, private index, one ref). `commit-tree` ignores `commit.gpgsign` (tested), so signing cannot hang a background worker | none needed |
| 3. The installers | Not runnable here. Reading them: Linux under systemd has no `DISPLAY`, so idle is unknown and the worker runs while the person works (at nice 19, all cores but one). On Windows the task runs `python.exe`, which opens a console window. The Windows idle tick wrapped after 49.7 days of uptime | Tick wrap fixed. The other two are listed under Open below; they need a Windows or Linux desktop to test |
| 4. Self-update | It never ran: `_home_worktree` read `git cat-file -e`'s empty output as "missing", so every worker ran the person's own checkout | Fixed. Box2 in the self-test now runs through it. Whether it should follow `bleeding-edge` is still the decision below |
| new: what runs | Any queue ref could make every idle machine execute code: a command job's `argv` from `compute-queue` (pushed without review), and a job `ref` naming any branch whose `Tools/Build` the evaluator then imports | `compute_lib.untrusted` (README section 7): code only from `bleeding-edge` / `Ys-bleeding-edge`; command jobs only from a job file on `bleeding-edge`. Seven cases in the self-test, with negative controls |

Open: the Linux idle source under systemd (try `loginctl` `IdleSinceHint`, or wait instead of running when idle is
unknown), `pythonw.exe` for the Windows task, `nca-train-forged` points at `origin/cece/gifted-curie-x2cpd0` (not a
code ref, so it is now refused, and `Tools/NCA` is not on `bleeding-edge`), and `nca3d.py` resumes with
`torch.load` on checkpoints relayed from other machines' `compute/*` branches (unsafe on torch < 2.6 unless
`weights_only=True`). Fix both NCA points before that job goes `ready`.

### Foundations (Shombith leads Froglet Compute, 2026-10-28)

- **`origin/ai`, the AI's own branch**, created from `bleeding-edge` (measured: `Ys-bleeding-edge` had no AI change
  `bleeding-edge` lacked; its 74 extra commits were the Meerkat, the Vessel Studio, shaders and pilot swap).
  `Tools/Compute/ai_branch.py` keeps it current (README section 7.1); its self-test proves an AI change comes over, a
  non-AI change does not (a negative control that fails when every path counts as AI), a second sync is a no-op and a
  both-sides edit is refused.
- Every AI job now reads `origin/ai`; `Ys-bleeding-edge` is no longer a queue ref or a code ref. `drive-rhino-headlong`
  ran on `ai` with the same epoch and shipped score (4.6172) as on `Ys-bleeding-edge`.
- Waiting on Yash: moving the hourly AI learning sweep and his AI sessions to `ai`. Until then the sync carries their
  AI work across.

### The Compute Console (2026-10-28)

The console (README section 13, https://claude.ai/artifact/54nfQFCxNBFHYQ2mCErPt1) is built from git by
`Tools/Compute/console.py`, so the same two files can move to GitHub Pages or Cloudflare (`--standalone`). Its first
data came from four jobs this cloud session ran and pushed to origin as `compute/shombith/cloud-session`, which was the
first real push to `compute/*` (it worked). Verified here: the page renders with no console error at desktop and
phone widths (no sideways scroll at 400 px), the terminal's offline commands, the approval check's self-test
(core four only, same epoch, same best), and an empty read of both store collections. Not verified: `task`
(`create_session`'s answer shape is read defensively, since creating a session only to learn its shape was not
worth it), `sessions` and `ask` inside the claude.ai viewer, and a real approval round trip.

### Install, runs and the tour (2026-10-28)

- One-line setup (`Tools/Compute/setup.py`, README section 1) behind the console's Install button; dry-run from the
  pushed branch and a first session from the installed copy both ran here. Not run on Windows or macOS.
- Workers publish progress every few minutes while a session runs and keep a runs log; the console's Runs view and
  Home show both.
- Fixed: `--max-evals` counted the machine's total tries on a search, so a second session stopped at once (a
  setup's first run would have done nothing on a machine that had worked the search before). It is now per session;
  the self-test's second box1 session proves it (and fails against the old check).

### Windows install fix and manual runs (2026-10-28)

- Shombith's first Windows install failed: Task Scheduler's /TR takes 261 characters and the worker's full command
  was longer. The task now runs `"<pythonw>" "<~/.froglet-compute/worker.pyw>"`; it also passed `/I 10.0` (needs a
  whole number) and would have opened console windows. Self-tested with a Store Python path; not yet run on Windows.
- Manual runs (README section 14): a real request was filed on origin's `compute-queue`
  (`r20261028-125904-5357`, the Rhino Regatta drive with the brake held at 0.5 s on I4 only); a worker took it ahead of
  its queue, ran 308 tries and recorded best 3.416 against today's 4.113 under the same settings (in the model).
  A token made by the console's form ran through the backend unchanged.

### Vessel and mode picks, the live console and the VM (2026-10-28)

- New run now picks a vessel (15 hulls), a game mode (every arcade and arena card that seats it), intensities and
  tries on colourful sliders, and gives a PowerShell or Terminal line at once. Searches opens with a vessel x mode
  grid. Only 5 of the 61 offered pairs run today (Rhino, Manta, Dolphin on the three modelled courses); the rest are
  marked "needs the game" (README section 14).
- The hub (`compute.py hub`) serves the console live: heartbeats every ~20 s, runs filed from a phone with the team
  key. `Tools/Compute/vm/setup-vm.sh` sets it up on Oracle's Always Free ARM VM with HTTPS (README section 15).
  Self-tested end to end against a throwaway remote (wrong key 401, out-of-bounds run 400, good run filed, a worker
  shows live). The page was checked in Chromium at desktop and phone widths with the hub mocked. **Not yet run on a
  real VM.**

### Runs in the real game (2026-10-28)

- `unity-train` + `unity_run.py` + `unity/FrogletComputeTraining.cs` (README section 16): Froglet Compute starts the
  game's own AI trainer in a Unity with no window, in its own copy of the project, on PCs with Unity. 21 more pairs
  (purple on the console). Compiled against the real Unity references; self-tested with a fake Unity; **not yet run
  in a real Unity** (`Docs/UNITY_VERIFICATION_CHECKLIST.md`). Job status `proposed` until then.

## 4. Decisions that are yours

- **Merge or not**, and onto what (`bleeding-edge` directly, or through your own branch).
- **The daily scout sweep** (README section 9): a scheduled Claude task that regenerates the board and opportunities,
  queues mechanical jobs and pushes `Docs/Compute/**` to a new branch, `compute-queue`. Not created: Garrett asked
  for periodic scouting, but a recurring task that pushes to the shared repo is yours to approve. If yes, ask a
  session to create it ("create the daily scout sweep from Docs/Compute/README.md section 9").
- **Whether `compute-queue` should exist at all** (`compute_lib.QUEUE_REFS` reads `bleeding-edge`,
  `Ys-bleeding-edge` and `compute-queue`).
- **The relation to the Unity genetic trainer** (`Assets/_Scripts/Utility/AITraining`, studyholic / Garrett / you).
  README section 10 positions compute as its offline sibling, not a replacement. Open ticket T-20261015-0064 ("extend
  the genetic trainer rather than build a second one") is related: this is not a second Unity trainer, but you may
  want to say so on the ticket, or fold the two.
- **What stays**: the five skills and eight skill edits are independent; dropping any of them only needs the index
  lines in `CLAUDE.md` / `DOCUMENTATION_INDEX.md` adjusted.

## 5. People who wait on it (no action needed from you to start)

- **Yash**: the Regatta plan's stage-1 drive results are proposals for him (`/regatta-balance`); stages 2-3 are
  `blocked` until stage 1 is promoted. Decision 1 in the plan (what spread counts as level) is his and Garrett's.
- **Garrett**: which forged swarm bodies are worth wiring into a swarm or training as NCA creatures (`/bioform-forge`).

## 6. Known gaps (the scout lists them as `build` opportunities)

- No offline race model yet for the Dolphin in Switchback, the Urchin in Skein, the Squirrel in Skim Race (stage 1).
- Stage 3 runs on the competent-pilot model (`regatta_balance.py`); the AI race models do not take an element level.
- The bioform quality measure does not run the real swarm core (`swarm_core_harness` needs `dotnet8`).
- The `by-hand` and `labs` lenses are noisy; tune or drop them.
- A session deck button ("Compute": `compute.py top`) was not added; the deck is Yash's area and TypeScript.

## 7. Check it yourself in two minutes

```
git fetch origin cece/clever-ritchie-q8270q && git checkout cece/clever-ritchie-q8270q
python3 Tools/Compute/compute.py self-test
python3 Tools/Compute/compute.py doctor
python3 Tools/Compute/compute.py top --local --why
python3 Tools/Compute/compute.py run --local --no-push --always --once --job bioform-forge --max-evals 500
python3 Tools/Compute/compute.py install --dry-run
```
