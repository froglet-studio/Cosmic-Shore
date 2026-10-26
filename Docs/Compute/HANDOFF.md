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
