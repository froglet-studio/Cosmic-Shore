# Froglet Compute: the team's idle hardware, searching for the game

Some of our work is bottlenecked on compute, not on people. Two kinds are already here: training the vessel AIs
(searching a drive's numbers so it races well at every intensity) and growing bioforms (the bodies a swarm switches
into, and the neural cellular automata that grow them). Both are the same shape: a **parametric solution space** and a
**loss function** that says how good a point in it is. Froglet Compute turns that shape into a queue that every
collaborator's computer works through whenever its person is away.

**Owner: Shombith** (tooling; Garrett handed it to him on 2026-10-26 - start at `HANDOFF.md`). Asked for by Garrett, 2026-10-26: "a skill that any contributor can reference that will retrieve the highest priority
compute tasks and allow contributors to set and forget on their hardware", that "suggests useful spaces to explore if
there is nothing in the backlog", and that keeps finding new opportunities. Skills: `/compute` (join, run, read the
board), `/compute-job` (define a space and a loss), `/compute-scout` (find what to search next), `/regatta-balance`
(the three-stage Regatta plan), `/bioform-forge` (swarm bodies and NCA creatures).

## 1. Start (any collaborator, any OS, about two minutes)

**The easy way: the Install button on the Compute Console** (section 13) shows one line to paste from your
Cosmic-Shore folder. On macOS or Linux:

```
git fetch origin <ref> && git show origin/<ref>:Tools/Compute/setup.py | python3 -
```

(`python -` and `;` instead of `&&` in Windows PowerShell; `<ref>` is the compute branch until Froglet Compute is on
`bleeding-edge`.) `Tools/Compute/setup.py` copies the code into `~/.froglet-compute/app`, installs the worker
(`--repo` your checkout, `--home` and `--queue` the ref), and does a two-minute first run. Add `--nights`, `--always`,
`--no-first-run`, `--dry-run` or `--uninstall` after the dash. By hand, from a checkout of the branch:

```
python3 Tools/Compute/compute.py doctor      # what this machine can run, and what to install to run more
python3 Tools/Compute/compute.py top         # the queue, ranked, and what this machine would run next
python3 Tools/Compute/compute.py install     # done: it works whenever you have been idle 10 minutes
```

`install` registers the worker with the OS (a Task Scheduler ON IDLE task on Windows, a LaunchAgent on macOS, a
systemd user service or a crontab line on Linux). `uninstall` removes exactly that. To run by hand instead:
`compute.py run` (it waits for idle), or `compute.py run --always` on a box nobody sits at. `--between 22:00-07:00`
(local time, on `run` or `install`) limits either to those hours: outside them it waits, and a running session
checkpoints and stops when the window closes. One job by hand: `run --once --job <id> --max-evals N`; one point:
`eval <job> --params '{...}'`. Python 3.8+ is all it needs; numpy unlocks the Regatta jobs, torch the NCA trainings, Node.js the lab scorecards.

## 2. What it does on your computer, and what it never does

- **Never touches your checkout.** A job's files are exported from git's object store (`git archive`) into private
  folders under `~/.froglet-compute/wt/`; results live in `~/.froglet-compute/results/`. No worktree, no
  sparse-checkout: your working tree, branch, index, stash, worktree list and `.git/config` are never written (the
  first version used sparse worktrees, which wrote `extensions.worktreeConfig` into the person's config). The
  self-test proves it (`compute.py self-test`: the scratch checkout stays clean, gains no worktree and no config
  extension).
- **Gets out of your way.** It waits until you have been idle `--when-idle` minutes (10) on mains power, runs at the
  lowest OS priority (nice 19 / IDLE_PRIORITY_CLASS) on all cores but one, and the moment you touch the keyboard or
  mouse it checkpoints and stops. Idle time comes from the OS (Windows GetLastInputInfo, macOS HIDIdleTime, Linux
  xprintidle); where the OS will not say, it runs at the lowest priority and says so once.
- **Publishes to exactly one place**: `refs/heads/compute/<you>/<this machine>`, one commit per session, built with git
  plumbing (a private index; `write-tree`; `commit-tree`; `push`). It never pushes anywhere else, never opens a pull
  request, and never changes a game file. Its git identity is `froglet-compute (<you>)`.
- **Updates itself**: between sessions the supervisor exports `Tools/Compute` from `origin/bleeding-edge` (`--home`)
  and runs that, so a machine set up once keeps running the team's newest code. `--no-update` runs your checkout's.
  (Until 2026-10-26 this path never ran: its existence check read `git cat-file -e`'s empty output as "missing". The
  self-test's second machine now runs through it.)
- **Runs only code the team already runs** (section 7, `compute_lib.untrusted`): a job's code comes from
  `bleeding-edge` or `ai` (no person's own branch), and a command job (an argv run as you) only from a job file
  on `bleeding-edge`.
- **Stops cleanly**: `compute.py uninstall`, then delete `~/.froglet-compute`. (A machine that ran the first version
  also has `extensions.worktreeConfig = true` in its `.git/config` and worktrees under `~/.froglet-compute/wt/`:
  `git worktree prune` after deleting the folder; the extension is harmless on any git from 2.20 on.)

## 3. The pieces

| File | What it is |
|---|---|
| `Tools/Compute/compute.py` | the one command: top, run, install, doctor, board, scout, eval, promote, new, validate, self-test |
| `Tools/Compute/compute_lib.py` | jobs (loading, validation, priority, epochs, convergence), the machine (capabilities, idle, battery), git transport |
| `Tools/Compute/optimizers.py` | the searchers, pure Python: separable CMA-ES with IPOP restarts, MAP-Elites, random; JSON state for resume |
| `Tools/Compute/worker.py` | the session: pick a job, export its files, run it on a process pool (search) or as a checkpointing subprocess (command), yield, publish |
| `Tools/Compute/board.py` | every machine's results merged: `Docs/Compute/BOARD.md` + `board.json` (generated) |
| `Tools/Compute/scout.py` | the opportunity lenses: `Docs/Compute/OPPORTUNITIES.md` + `opportunities.json` (generated) |
| `Tools/Compute/install.py` | the OS registration for set-and-forget |
| `Tools/Compute/ai_branch.py` | keeps `origin/ai`, the AI's own branch, current (section 7.1) |
| `Tools/Compute/console.py`, `console/index.html` | the Compute Console: one static page and the `console.json` it reads (section 13) |
| `Tools/Compute/selftest.py` | every piece, then two simulated machines against a throwaway remote |
| `Tools/Compute/evaluators/` | the loss functions, one module each (section 6) |
| `Docs/Compute/jobs/*.json` | the queue (section 4) |

## 4. A job

One JSON file per job in `Docs/Compute/jobs/<id>.json` (`compute.py new <id> --evaluator <module>` scaffolds one;
`compute.py validate` checks every file; `/compute-job` is how to write a good one).

| Field | Meaning |
|---|---|
| `id`, `title`, `goal` | lower-kebab id; one line; the ambitious goal it serves (the board shows it) |
| `asked_by`, `area_owner` | who asked (raises priority x1.3) and whose area its RESULT changes (told when it is promoted, section 8) |
| `plan`, `stage` | a multi-stage plan it belongs to (`regatta-balance`, `bioforms`) and its stage |
| `kind` | `search` (an evaluator scores points the searcher proposes) or `command` (a long training run in exclusive units) |
| `status` | `ready` (runnable), `proposed` (needs a person's or session's yes), `blocked` (waits; `blocked_reason` says on what), `paused`, `converged`, `promoted`, `retired` |
| `value` | 0-10: what a converged answer is worth to the game |
| `ref` | the git ref the job's GAME code is read at (`origin/ai` for AI jobs, so they race the newest AI; `origin/bleeding-edge` otherwise) |
| `paths` | the folders the job needs from `ref`, exported (the evaluator's own `PATHS` are added) |
| `requires` | capabilities: `python3`, `numpy`, `torch`, `cuda`, `node`, `dotnet8`, `bash` |
| `after` | jobs that must converge first (a plan's stages) |
| `evaluator` | `{module, args, timeout}` for a search job |
| `space` | the parameters: `{name, type: float/int/choice, lo, hi, log, choices, init}`; `init` is the SHIPPED value |
| `algorithm` | `{name: sep-cmaes / map-elites / random, ...}`; MAP-Elites adds `descriptors` (`{name, lo, hi, bins}`) and `worst` |
| `convergence` | `{min_evals, window_evals, min_improvement}`: converged when the team's best has not improved by `min_improvement` (relative) over the last `window_evals` |
| `command` | for a command job: `argv` (with `{python} {out} {root} {tools} {threads} {unit.<arg>}`), `units`, `resume`, `resume_if`, `progress`, `score`, `keep`, `lease_hours`, `max_mb` |
| `promote` | how a converged answer reaches the game, by which tool, and who must agree |

**Priority** (what the next machine-hour on a job is worth; `top --why` prints every factor):
`value x (1 + 0.5 x jobs waiting on it) x 1.3 if a person asked x (1 + days since anyone worked it / 7, at most 2)
/ (1 + machines on it now)`. Jobs that cannot run here (status, `after`, missing capability, converged) sink with
their reason shown.

**Epochs.** Results are comparable only when the space, the evaluator (its `VERSION`) and its input files (`inputs()`,
hashed from the job's checkout) are the same. Change any of them and a new epoch starts; the old one stays on the
board as history. So a sim edited on `ai` never mixes its numbers with the old sim's.

## 5. How the search is shared

- **Search jobs are islands.** Every machine runs its own searcher (separate seeds). It starts from the best point ANY
  machine has published for that epoch (the second machine in the self-test logs "starting from the team's best"),
  and every restart takes the team's best half the time. Nothing is locked; more machines means more islands.
- **MAP-Elites archives merge** cell by cell: each machine adopts every published elite that beats its own.
- **Command jobs are exclusive units with relay.** A machine claims a unit by publishing its heartbeat before it
  starts (the lease lasts `lease_hours` past the last heartbeat). It publishes the unit's checkpoint files every ten
  minutes; when a person comes back the run is stopped with SIGINT/CTRL_BREAK and resumes later - on this machine or,
  once the lease lapses, on whichever machine picks the unit up, from the furthest checkpoint any machine published.
- **What a machine publishes** (`compute/<user>/<host>`): `heartbeat.json` (capabilities, the current job, totals) and
  per job and epoch `summary.json` (evaluations, seconds, the improvement history, the shipped values' score, the
  top errors), `best.json`, `archive.json` (MAP-Elites) and `unit-<id>.json` plus `files/<unit>/` (command jobs).

## 6. An evaluator (the contract)

A module in `Tools/Compute/evaluators/`. The worker imports it in each pool process; it is handed the job's checkout.

| Name | Required | Meaning |
|---|---|---|
| `VERSION` | yes | bump it whenever the loss changes meaning (starts a new epoch) |
| `PATHS` | yes | what it reads from the job's export of its ref |
| `evaluate(params, ctx) -> dict` | yes | `{loss, metrics, valid, descriptors}`; LOWER loss is better; deterministic for the same params; an exception is caught and recorded as an invalid point |
| `inputs(args) -> [paths]` | recommended | the files whose content defines the loss (hashed into the epoch) |
| `setup(ctx)` | optional | once per process (load courses, tables) |
| `baseline(args) -> params` | optional | the shipped values, scored once per epoch per machine (else the space's `init`) |
| `self_test() -> [failures]` | yes | it reproduces something known, and a negative control fails (`compute.py self-test` runs it) |

`ctx = {root: the job's checkout, args: evaluator.args + _space + _job, tools: Tools/Compute}`. Evaluators import the
sims from `<root>/Tools/Build`, so a job pinned to `ai` scores that branch's sims. **A model's limits
travel with its numbers**: every evaluator puts them in `metrics.limits`, and nothing promotes a number without them.

The evaluators today:

| Module | Loss | Notes |
|---|---|---|
| `drive_fit` | mean ln(race seconds) over I1-I4 (x course seeds) for a hull's AI drive, + a pull toward the shipped values | stage 1 of `/regatta-balance`; models: `rhino_drive`, `manta_drive`, `dolphin_drive` |
| `regatta_course` | ln(slowest / fastest AI) on one intensity's Regatta circuit + rail, length and character penalties | stage 2; the circuit is `course_port.py` (gated against the C# harness) |
| `regatta_elements` | mean over intensities of ln(slowest / fastest) + minimality x mean level | stage 3; regatta_balance.py's competent-pilot model |
| `bioform_forms` | what a swarm needs from a body (size, integrity, one animal, keepable, lively, loops) | MAP-Elites over 22 genome numbers; bakes SwarmPlanJson + NCA prisms targets |
| `toy` | a known minimum | the self-test's end-to-end job |
| `course_port` (library) | - | `HeadlongCircuit.Generate` + `RegattaCourse.BuildRails` in Python, float32 per op; `--self-test` matches every gate, lane point and measurement of `regatta_course_harness` at all four intensities |
| `race_models` (library) | - | races any hull on any circuit with the shipped Tools/Build sims (`use_course` swaps the sims' course reader) |

## 7. The queue refs, and who writes them

Workers read jobs from `origin/bleeding-edge`, `origin/ai` and `origin/compute-queue` (a job on two refs: the later
`updated` wins). A job is added like any other change: on a branch that merges into one of those. AI jobs ride `ai`
(section 7.1). `compute-queue` is the scout sweep's
own branch (section 9): it carries only `Docs/Compute/**` - job files, the board, the opportunities - never game code,
so it needs no pull request. Before this system is on `bleeding-edge`, test a queue with `--queue <your branch>`.

**What a worker will execute** is narrower than what it reads (`compute_lib.CODE_REFS`, `COMMAND_REFS`,
`untrusted`), because every idle machine runs it with its person's credentials:

- a job's `ref` (where its sims and command come from) must be `origin/bleeding-edge` or `origin/ai`, the branches
  the team shares; a job pointing anywhere else, a person's own branch included, shows `not run:` in `top` with the
  reason;
- a `command` job runs only when its job file came from `bleeding-edge` (reviewed pull requests). From `ai`,
  `compute-queue` or a `--queue` test branch it is refused, since those are pushed without review and an argv is
  arbitrary code;
- jobs read from the working tree (`--local`, the self-test) are the person's own and are not restricted.

### 7.1 The `ai` branch (Shombith, 2026-10-28)

AI work used to live on `Ys-bleeding-edge`, Yash's own integration branch, which also carries his unpromoted
features (on 2026-10-28: 74 commits ahead of `bleeding-edge`, the Meerkat, the Vessel Studio, shaders, pilot swap, and
no AI change `bleeding-edge` lacked). `origin/ai` is the AI's own branch: `bleeding-edge` plus AI work, owned by no
person. Every compute AI job reads it.

- `python3 Tools/Compute/ai_branch.py status` shows how far `ai` is behind and what AI work is waiting;
  `sync --push` brings it in: it merges `bleeding-edge` into `ai`, then replays ONLY the AI paths (`AI_PATHS` in the
  tool) that changed on `Ys-bleeding-edge` since the last sync (the `AI-Sync-Ys:` trailer). Plumbing only: nobody's
  checkout is touched. A file edited differently on both sides stops it; that one is resolved by a pull request into
  `ai`. **The VM's hub runs this sync every hour on its own** (section 15); a person runs it only to see it work.
- The hourly AI learning sweep (`Docs/AI_SYSTEM/AUTO_LEARNING.md`, Yash's) still pushes to `Ys-bleeding-edge`, so the
  sync is the bridge until the sweep pushes to `ai`. Moving the sweep is the next step and needs Yash.
- `ai` reaches `bleeding-edge` by an ordinary pull request.
- Compute promotions (section 8) are written on `ai`.

## 8. Promotion: a converged answer reaches the game only with a person's yes

A job's result is a **proposal**. `compute.py promote <id>` prints the team's best (and for a bioform job, `--out`
bakes the chosen elites). **Any of the core four (Garrett, Caleb, Shombith, Yash; `Docs/TEAM.json` `core`) can
approve a promotion** (Shombith, 2026-10-28). The approver says yes in their own session, which counts as the
confirmation CLAUDE.md asks for before re-tuning another collaborator's work. The job's `area_owner` is told in the
commit, not asked. Then that session:

1. writes the numbers with the area's own tool (a learner's writer, a generator such as `author_regatta_assets.py`),
   never by hand, with an `Affects: <owner>'s <area>` line and `Approved-by: <name>`;
2. runs the area's gates and harnesses, and adds the editor check to `Docs/UNITY_VERIFICATION_CHECKLIST.md` (an
   offline model is not the game; the editor is the measurement);
3. sets the job's `status` to `promoted` (or `retired` with why), and lifts any job `after` it out of `blocked`.

The AI drives keep their two sources straight: where people play a mode, the hourly AI learning sweep imitates them
(`Docs/AI_SYSTEM/AUTO_LEARNING.md`); compute fills what nobody plays and reports where a searched drive beats the
person-learned one in the model. Neither overwrites the other without Yash.

## 9. The scout sweep (a scheduled task's whole instruction)

A Claude scheduled task runs this section once a day; change what it does HERE, not in the task.

1. `git fetch origin`. Check out `compute-queue` (create it from `origin/bleeding-edge` if it does not exist) and
   merge `origin/bleeding-edge` into it (merge, never rebase). If `Tools/Compute/compute.py` is not on
   `bleeding-edge` yet, merge the branch named in `Docs/Compute/BRANCH` instead and say so in the commit.
2. `python3 Tools/Compute/compute.py self-test --quick`. Red: stop; fix nothing here, report it in the commit message
   of an otherwise empty run (do not push a red queue).
3. `python3 Tools/Compute/compute.py board --write` and `python3 Tools/Compute/compute.py scout --write --propose`
   (the mechanical opportunities become `ready` jobs; their results are only proposals).
4. Read `Docs/Compute/OPPORTUNITIES.md` and think past the lenses: is there a problem in a recent doc, ticket or
   commit (the `by-hand` lens, `Docs/QA/DEV_TASKS.md`, `Docs/BRANCH_PARTS_STATUS.md`, the last day's merges) that a
   search would answer, though nobody asked for one? Write each as a `proposed` job (`compute.py new`) whose `goal`
   says whose problem it answers and why a search fits, and whose `promote` names the owner. Never `ready` - a person
   or a session flips it once the evaluator exists (`/compute-job`).
5. For each converged job, write one line under "Waiting on" in the board's commit message: it waits on any of the
   core four. Never promote here.
6. Commit only `Docs/Compute/**` (`chore(compute): board and opportunities (<n> jobs, <m> machines)`) and push to
   `compute-queue` (fetch, merge, push; retry on a race). Never push to `bleeding-edge`, never open a pull request,
   never file a team ticket (a session may only SUGGEST one, CLAUDE.md).

## 10. Its sibling in Unity: the genetic trainer

`Assets/_Scripts/Utility/AITraining` (studyholic, Garrett and Shombith; `Docs/AI_SYSTEM/AI_TRAINING_PLAN.md`) is an
in-editor evolutionary trainer with its own overnight queue (FrogletTools > AI > AI Training > Schedule): it trains
`TrainingPilot` genomes in the REAL game, per vessel, mode and intensity, into a `TrainingArchiveSO`. Froglet Compute
does not replace or wrap it. The two split the work by what each can see: compute searches offline models fast and in
parallel across every machine (any OS, no Unity); the trainer searches the game itself, one editor at a time. A person
who leaves Unity open overnight runs the trainer's queue; a machine without Unity (or a Unity machine on a night the
trainer is idle) runs this queue - not both at once on one machine, they compete for the same cores. Driving the
trainer from a compute command job (Unity batchmode, a shared archive) is a `build` opportunity for its owners to
decide, not something to start without them.

## 11. What this is not

- **Not the game.** Every evaluator is an offline model. A number from it says where to look; the editor says
  whether it is true (`Docs/UNITY_VERIFICATION_CHECKLIST.md`). The models' blind spots are in `race_models.MODEL_LIMITS`
  and in every result.
- **Not a replacement for people's play.** The AI learns how to fly from how people fly; compute fills the cells
  nobody plays and checks the cells they do.
- **Not a GPU farm.** Torch jobs use whatever torch finds (`cuda` is a capability a job can require); there is no
  multi-machine gradient sharing. Search jobs are embarrassingly parallel islands; trainings are relayed units.

## 13. The Compute Console (Shombith, 2026-10-28)

**https://claude.ai/artifact/54nfQFCxNBFHYQ2mCErPt1** (private until shared from its Share menu). A macOS-style window
in Cosmic Shore's style foundation (`Docs/STYLE_FOUNDATION.md`: the deep-space neutrals, Jade for the selection, CTA
green for the one call to action, Gold for "waits for a person", corner slivers, Aldrich and Chakra Petch).

It opens on Home, and a first visit starts a guided tour (nine stops; the Tour button replays it). The Install
button (title bar, Home, Computers) opens a sheet with the one line for macOS, Windows or Linux (section 1).

| View | What it shows |
|---|---|
| Home | one sentence on what is happening, Needs you (results ready for a yes, the ai branch behind, races not learned yet), Running now, the best finds, how it works in four steps |
| Runs | each computer's live run (a progress bar from the progress it publishes every few minutes, tries, best now) and the recent runs log (when, which search, how long, tries, best before and after, why it ended) |
| Searches | every job, grouped by what it waits for: the best find against today's value as a percentage, a best-so-far line; a search's sheet adds race time per intensity, its runs, the settings found, the model's limits, and "Yes, put it in" / "Not this one" |
| Computers | every `compute/<user>/<host>` branch: running now or not, last heard, runs, cores, what it can run |
| Recordings | every race the editor sent (`origin/bugledger/*`), learned / waiting / not seen yet by the AI learning sweep, by vessel and mode |
| AI branch | `origin/ai` against bleeding-edge and Ys-bleeding-edge's AI work, and its recent commits |
| Terminal (`⌘J`) | `help`, `status`, `jobs`, `job <id>`, `open`, `machines`, `recordings`, `ai`, `git log`, `git branches`, `prs`, `approve` / `decline`; `ask <question>` (Claude reads the console's data); `task <prompt>` (starts a Claude Code cloud session on the repo on the viewer's account, after a typed `yes`: GitHub, pushes, anything a session does), `sync`, `refresh`, `sessions`, `env` |

**How it is built.** No server. `python3 Tools/Compute/compute.py console --out DIR [--prs FILE] [--url URL]` reads
git (the queue, every machine's results, the bugledger recordings and the AI sweep's `ai_learning_seen.json`, the
`ai` branch, recent commits) and writes `DIR/console.json` beside a copy of the page. The page `fetch`es
`console.json` from its own folder. claude.ai pages cannot reach GitHub, so the git data is a snapshot as of the
last build; the terminal's `refresh` starts a session that rebuilds it (open PRs from the GitHub tools) and
republishes both files to the URL. A claude.ai page has no GitHub connector here, so live GitHub work goes through
`task`.

**Approvals.** On claude.ai the page keeps decisions in its own store: `approvals/<viewer id>` holds
`decisions: {job: {decision, epoch, loss, note, at}}` (each person writes only their own; everyone reads them), and
`processed/<job>` (owner-written) records what a session did with them. A decision names the exact result (epoch and
best loss), so a newer best needs a new yes. A session acts on them with `compute.py console --approvals <the
collection exported with ArtifactData> --profiles <the ids' profiles>`: only a valid approval from one of the core
four, for the current epoch and best, is acted on (section 8). The button shows only to the core four by display
name; the check in `console.py` is the one that counts.

**Moving it off claude.ai.** `compute.py console --standalone --out DIR` writes a complete HTML document; `DIR`
then serves as is from GitHub Pages (a private Pages site needs GitHub Enterprise Cloud), Cloudflare Pages, or
`python3 -m http.server`. Everything but the claude.ai-only parts works there: approvals, `ask`, `task` and
`sessions` say they need the claude.ai console. Moving approvals or tasks to such a host needs a backend of its own
(for example a Cloudflare Worker with a GitHub App), which is a separate decision.

## 14. Manual runs: your instructions for one search (Shombith, 2026-10-28)

Besides the automatic queue (idle, `--between`, `--always`), anyone can run one search their way
(`Tools/Compute/manual_runs.py`):

- **What a request can say**: which search; how many tries; settings held at a value (`--fix name=value`), settings
  searched in a narrower range (`--range name=low:high`), the evaluator's own settings set differently (`--arg
  intensities=[4]`); which computer (`any` or `<person>/<computer>`); and a note in your words. Only what the job
  already allows: values inside its bounds, settings it has, evaluator settings it already sets. Never new code.
- **Now, on your computer**: `compute.py run --request <id|token|file>` runs it at once, without waiting for idle.
- **For the team's computers**: `compute.py request new <search> --tries 800 --fix brakeLeadSeconds=0.5
  --arg 'intensities=[4]' --note "..." --push` files `Docs/Compute/requests/<id>.json` on the `compute-queue` branch
  (git plumbing; the checkout is not touched). Every worker takes the oldest open request it may run before its
  automatic queue, under its own idle rules, until the tries are spent across all of them.
- **On the console**: New run (title bar, also on a phone; Runs; Home; "Run this my way" on every search; any cell of
  the vessel x mode grid at the top of Searches) picks in steps: **vessel** (all 15 hulls on the fleet roster),
  **game mode** (every arcade and arena card that seats it, from `Docs/AI_SYSTEM/vessel_mode_chart.json`), the
  **intensities** I1-I4, **tries** on a slider, then optionally each setting on a slider (drag the ends to narrow it,
  or hold it at one value; the white tick is today's value), **where**, and a note. Blue pairs run on the offline race
  models (Rhino, Manta and Dolphin on Regatta, Headlong and Redline); purple pairs play real matches through the Unity
  trainer on a PC with Unity (section 16), and the run asks for an intensity and a number of matches instead of
  tries and settings; gold pairs have no trainer scenario yet and say so instead of making a run. Pick a vessel and mode and the console finds
  the search behind it: the vessel's own drive job with its `mode` changed. **Run on this computer** gives one line
  for PowerShell or Terminal (`compute.py run --request <token>`). For the team's computers, the live console (section
  15) files it in one tap; the claude.ai snapshot offers "File it now" (a cloud session) or the same command to paste.
  The terminal keeps `run <search> [--tries N] [--fix a=v] [--range a=lo:hi] [--arg k=v] [--note ...] [--team]` and
  `requests`.
- **What an evaluator lets a request change**: its `REQUEST_ARGS` (`drive_fit`: `mode` among the three modelled
  courses, `intensities` a subset of 1-4, `seeds` a subset of 1-10, `limit` 60-1200 s). A request that sets anything
  else, or a value outside these, is refused by `manual_runs.problems()`, on the console, the hub and every worker.
- **Where results go**: each computer writes `requests/<id>.json` (its tries and best) to its results branch and
  runs the search as `<job>~<id>`, so a manual run never mixes with the job's own search; the evaluator still scores
  against the job's full space, so the numbers compare. Runs on the console shows every request, its state, tries,
  and its best against today's value under the same settings. `compute.py request list` and `request cancel <id>`.

## 15. The live console and the always-on VM (Shombith, 2026-10-28)

The claude.ai console is a snapshot: a page there can only read the files published with it. **The hub**
(`compute.py hub`, `Tools/Compute/hub.py`) is the same page served live from one always-on computer: it rebuilds the
data from git every minute, lays every computer's heartbeat on top the moment it arrives (a working computer posts one
every ~20 s), and files manual runs on `compute-queue` from any browser or phone, guarded by the team key. The page
notices it is on a hub (`api/status`), shows **LIVE** in the title bar and refreshes every 15 s.

**The agent keeps up with the branch by itself** (2026-11-01: a run at two intensities sat "waiting" because
dragonsweapon's agent still ran the code from before intensity lists and skipped it as invalid): every half hour the
resident agent fetches the install ref, and when its `Tools/Compute` differs from the copy it runs, it exports the
newer one under `~/.froglet-compute/fresh`, releases its single-instance lock, starts that copy with the same
arguments and exits. The Install line is for joining and for changing flags, never for keeping up.

**The look is Silver Screen** (Shombith, 2026-11-01, his site's theme): black and white like a film still, no colour
cast in the chrome, movie-poster headings (Bebas Neue), typewriter labels (Space Mono), the one accent kept for the
primary action and live data, the status colours kept so a dot still reads, a still silver halo on the ground. One
stylesheet at the top of `index.html`; light follows the system in greys. **The film stills** (Shombith, 2026-11-01: "where
are the background images"): one still of the game behind each view and a band on every Vessel AIs card, the mode's
card art in black and white (`Tools/Compute/console/stills/<view>.jpg`, made from `Assets/_Graphics/ARCADE/CardBackgrounds`
with Pillow: grayscale, autocontrast, 1152 px wide, 30-55 KB each), faded into the ground so the lists stay readable.
The hub serves them beside the page; the claude.ai snapshot carries them as published files; a page without them
shows the plain ground.

**What a sent run looks like, from Send to its first report** (Shombith, 2026-10-31: "there is no live bar"; design
board Rev 41, `Docs/MENU_REDESIGN_B.md` section 110): Send closes the sheet and opens Runs with one line saying where
the run went. The agent that takes it posts a heartbeat at once with `phase: starting` and the unit, so the run is on
Runs within a minute as **Starting** with a sliding bar until the first evaluation or match reports (a Unity run spends
its first minute or two opening Unity); the computer shows **Starting** too, never "Working" over "Nothing is running".
A sent run that has not started says why, from the computer's own heartbeat (on another run, in use and its owner does
not allow a start, offline, outside its hours). On the live console New run's Where lists the computers first and picks
the best one (free, else in use with a start allowed, else the team queue); the button says what will happen ("Start on
dragonsweapon now", "Send to dragonsweapon · when free"). Stop asks first.

**The VM: Oracle Cloud Always Free** (an Ampere A1 ARM machine, up to 4 cores and 24 GB free for good; we use 2 and 12):

1. Oracle console, Compute > Instances > Create instance. Image **Canonical Ubuntu 24.04**; shape
   **VM.Standard.A1.Flex**, 2 OCPUs, 12 GB; keep "Assign a public IPv4 address"; add your SSH public key (or let it
   generate one and save the private key). "Out of capacity" is common on A1: try another availability domain, or
   again later.
2. Networking > Virtual cloud networks > the instance's VCN > its subnet's security list > Add ingress rules: source
   `0.0.0.0/0`, TCP, destination ports **80** and **443**.
3. A GitHub fine-grained token: Settings > Developer settings > Fine-grained tokens, resource owner
   `froglet-studio`, only `Cosmic-Shore`, **Contents: read and write**. Pick an expiry you will remember: when it
   lapses the VM stops publishing (put a new one in by running the script again). An organisation owner may have to
   approve it; the script checks that it can push and stops with a message when it cannot.
4. `ssh -i <key> ubuntu@<public ip>` (Windows has `ssh` built in; on macOS/Linux `chmod 600 <key>` first, or ssh
   refuses a downloaded key), then:
   ```
   read -rs GITHUB_TOKEN && export GITHUB_TOKEN
   curl -fsSL -o setup-vm.sh \
     https://raw.githubusercontent.com/froglet-studio/Cosmic-Shore/claude/eager-franklin-e6g77h/Tools/Compute/vm/setup-vm.sh
   bash setup-vm.sh
   ```
   (once Froglet Compute is on bleeding-edge, `bleeding-edge` replaces the branch in that address, and the script picks
   bleeding-edge by itself).
   (paste the token after the first line and press Enter: it is not shown and stays out of the shell's history).
   `Tools/Compute/vm/setup-vm.sh` waits for the new instance's own updates, installs git, Python, numpy and Caddy;
   opens 80/443 in the image's firewall (iptables; it does not touch ufw, which Oracle's images leave off); makes a
   `froglet` service user with a partial, sparse clone of the one branch (only Tools/Compute, Tools/Build,
   Docs/Compute, Docs/AI_SYSTEM, Docs/TEAM.json; files over 1 MB stay on GitHub); writes the team key; runs the hub
   and a worker that works all the time as systemd services (`froglet-hub`, `froglet-compute`, user `vm`, computer
   `oracle`); and puts the hub behind HTTPS at `https://<ip-with-dashes>.sslip.io` (or `COMPUTE_DOMAIN`). It checks
   that the token can push, then prints the address, the team key and the line that puts a team computer on the live
   console. If a step fails it says which; running it again is safe, updates everything and keeps the key
   (`sudo cat /srv/froglet/.froglet-compute/hub.json` shows it later).
   On the VM a run sent from the console starts within 15 minutes, when the current job re-ranks (`--session 15`),
   and "Now" is allowed there (the computer is the team's; `COMPUTE_REMOTE_START=0` turns that off). The hub
   restarts every 6 hours to pick up new code.
   **The hub keeps the `ai` branch current by itself**: every hour it runs `ai_branch.py sync --push` with its
   token (bleeding-edge merged in, Yash's AI paths replayed; section 14) and the AI branch page says when it last did
   and whether it worked (`api/status` carries `ai_synced`). Nobody types `sync`. The app (section 18) never syncs.
5. On each team computer, from its Cosmic-Shore folder, the printed line (`setup.py ... --hub <address> --hub-key
   <key>`), with the same `--nights` or `--always` it was installed with: the computer's progress then shows on the
   live console within seconds. The Install sheet on the live console adds these flags itself once the team key is
   entered.
6. The live console is the printed address, not the claude.ai page: the claude.ai snapshot cannot reach the hub and
   stays a snapshot (its outbox and the hourly sync keep working). Share the address and the key with the team only.

What it is not: the VM is ARM Linux with no GPU, so it runs the offline searches, not the Unity game. The purple pairs
(the game itself, section 16) run on team PCs with Unity. sslip.io certificates come from Let's Encrypt
and can hit its weekly limit for the shared domain; a domain of our own (`COMPUTE_DOMAIN`, an A record to the VM) avoids
that. Logs: `journalctl -u froglet-hub -u froglet-compute -f`.

## 16. Runs in the real game: the Unity trainer, headless (Shombith, 2026-10-28)

**Modes Compute skips:** `Docs/Compute/skip_modes.json` names game modes Compute leaves out of every job, the
console's vessel x mode grid and the scout (Astro League since 2026-10-29: "not needed anymore", Shombith). The game
keeps them. `make_unity_job.py` and the scout read the file, so a mode returns by deleting its line.

The offline models are fast (hundreds of races a minute, any computer) but each one is a hand-made copy of one vessel
on one course, so only 5 of the 58 vessel x mode pairs have one (`compute.py console` counts them). Everything else has to be played in the game. The
game already has an AI trainer (`Assets/_Scripts/Utility/AITraining`, studyholic, Garrett and Shombith): all-AI
matches, a population of AIs, the best kept. Froglet Compute now starts it with nobody there:

- **`unity-train`** (`Docs/Compute/jobs/unity-train.json`) is a command job with one unit per vessel, mode and
  intensity: 57 units over 36 modes (`make_unity_job.py --check`; the trainer's catalog holds 37 scenarios, Astro League is skipped; the three Time races of 2026-10-31, Serpentine, Scarab Rally and Brittle Star Time, joined on 2026-11-01), every multiplayer card with a scoring rule, with every hull a
  card seats where the scenario lets the hull change (Regatta, Broadside, Crystal Capture, Brood Rush, Astro League).
  `Tools/Compute/make_unity_job.py` writes the units from the trainer's own catalog (`--check`; the self-test runs
  it). The trainer gained 22 modes for this on 2026-10-28 (its README, "The other 22 cards"): 13 gate races that
  seek the next ring, and their recipes read each mode's own ScoringMetric. Not covered: Maelstrom (chains other
  modes) and Grizzly Charge (in development). It needs a computer with the project's exact Unity (capability
  `unity`; `compute.py unity --find` says which editor, or why not). Status `proposed`: manual runs only, until a
  first run on a real PC has been looked at.
- **`Tools/Compute/unity_run.py`** runs one unit: keeps Froglet Compute's own copy of the project in
  `~/.froglet-compute/unity/Cosmic-Shore` (a `git clone --shared` of the person's repository; its Library stays, so
  only the first run imports everything, an hour or more), copies in `Tools/Compute/unity/FrogletComputeTraining.cs`,
  puts back the unit's saved trainer session, and starts `Unity -batchmode -executeMethod ...FrogletComputeTraining.Run`.
  That entry does what the trainer window's Learn button does, at up to 4x game speed with audio and camera off, and
  writes `status.json` (live), `result.json` and `genome.json`. The person's checkout and their open Unity are never
  touched; an automatic run waits while their own Unity has the project open (the two would race), a manual run goes
  ahead.
- **Watching it**: `compute.py run --request <token> --watch` (the console's "Show the game", on by default) opens
  Unity with its window instead of `-batchmode`, camera and sound on, at game speed: the AIs play in the Game view.
  Without it the run has no window and plays at up to 4x. **On any computer** (Shombith, 2026-11-01: "show the game
  for another PC as well"): a sent run carries `args.watch: true`, the worker starts that unit with
  `FROGLET_COMPUTE_UNITY_WATCH=1`, and Unity opens its window on the computer that plays it.
- **A watched run is a performance report** (Shombith, 2026-11-01: "so the game collects performance data using the
  bug reporting tool"): 20 s into the match and every 5 minutes after, the trainer runs the game's own `diag` (the
  DiagnosticsHUD, `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md`) for 60 s, tagged
  `froglet-compute-<scenario>`, so a perf report (fps, frame times, spikes, draws, memory) lands in
  `Documents/CosmicShore Diagnostics`; when the run ends, before Unity quits, it runs the bug ledger's "Send to Claude"
  (`BugLedgerSender`, `/bug-ledger`), which pushes the run's console errors and those reports to `bugledger/<user>`,
  and writes `<out>/bugledger.json` (sent / nothing new / failed, and why). A headless run records none of this: its
  camera is off, so its frames mean nothing.
- **Every intensity, one match**: New run's Intensity takes any set of levels (All 4 is one chip); a request with
  `intensity: [1, 2, 3, 4]` is one unit per level, played in order, the matches per level, done when every level is.
  Matches go down to 1 (a run of one match is the quick look at a pair in the game).
- **What a run records, with the game's own tools (Shombith, 2026-10-29)**: every match the trainer plays already
  records a replay (the Theater, `Docs/REPLAYS.md`). `FrogletComputeTraining.cs` points it at `<out>/replays` and
  gives the copy a ReplayConfig of its own (keep 2, Cloud Save list 0, so training never fills a person's MY REPLAYS
  on other devices); the replay of the match where the best fitness rose is copied to `replays/best`. Yash's flight
  recorder (`HumanFlightRecorder`) takes the trainer's seat for a human one (the trainer turns the hull's AIPilot off,
  so `held` reads 0): each flight it saves is moved at once into `<out>/ai-flights` (renamed `aiflight_*`, out of the
  folder and the pattern people's races are sent from) so the learning sweep never learns the AI from the AI. When a
  person watches (Show the game), the Third Eye opens on the match (`FrogletTools/AI/Third Eye`; `]` / Tab picks the
  next pilot, C the camera). `<out>/recordings.json` lists it all, is published with the unit, and the console's
  Recordings page lists every run's replays (the files stay on the computer that played; how to watch one is on the
  page). Every game type is reached by name: a version of the game without one still trains and says what it
  skipped in `recordings.json`. Measured so far: the file compiles in the reference compile (editor config, a planted
  error caught as a negative control); it has not run in the Unity editor yet.
  Since 2026-10-29 the game marks the trainer's seat itself (`Docs/AI_SYSTEM/AUTO_LEARNING.md` section 8): the
  recorder stores no trainer match, every learner skips a record not flown by a person, the Third Eye draws the
  trainer's aim and refuses its You / AI toggle on a trainer seat, and a watched run saves a still of the Third Eye's
  view every minute and when the best fitness rises (`<out>/stills`, the newest 20, listed in `recordings.json`).
  The move of a flight into `ai-flights` stays for a game version from before.
- **First real run (Shombith's PC, 2026-10-28)**: Dolphin in Switchback at I2, 32 matches, the import included, in 27
  minutes, result published. Every AI scored 0 gates: the trainer built each new pilot's target sensor before the
  scenario's target mode was set, so every pilot sought crystals (`TrainingPilot.BeginEpisode` now applies it). That
  also hit the enemy-seeking modes (Joust, Dog Fight, The Bends) whenever a match reloads its scene.
- **A manual run** for a purple pair names the pair, the intensity and the number of matches
  (`args: {mode, vessel, intensity}`, `budget.tries` = matches); `manual_runs.problems()` refuses anything else.
  The console's New run gives the line for PowerShell or Terminal, or sends it to the team's computers with Unity.
- **A pasted line always runs the newest code**: commands typed into `~/.froglet-compute/app` (`run --request`,
  `unity`, `request`, `doctor`) fetch the install ref first and, when its Tools/Compute is newer than the installed
  copy (`app/.tree`, written by the setup), run that newer copy from `~/.froglet-compute/fresh`. A run made by a newer
  console no longer fails with "no search named unity-train" on an older install (Shombith hit it 2026-10-28). The
  capability cache is asked again when Froglet Compute learns a new capability (`CAPS_VERSION`).
- **Before the merge**: the project copy is built from `origin/ai`, which has the trainer's 22 new modes only once
  this branch is merged. Until then the job lays the trainer's folders (`Assets/_Scripts/Utility/AITraining/`,
  `Assets/_SO_Assets/AI Training/`) from this computer's install ref over that copy, and only while `ai` has not
  touched them since the two parted (`unity_run.py --overlay-ref`): newer work on `ai` is never replaced.
- **Trust**: a command job runs from bleeding-edge, or from the ref this computer's Froglet Compute was installed from
  (config.json "ref"): that code already runs on it, so the same ref's command job asks for nothing more. That is how
  `unity-train` runs before Froglet Compute is merged. Its code ref is `origin/ai`.
- **Results**: the unit's `genome.json` is a proposal. Import it on the AI Training window's Archive tab for that
  vessel, mode and intensity; the trainer's owners and the AI owner (Yash) decide whether it deploys.
- **What is left**: 4 pairs (Maelstrom x3, Grizzly Charge). The 22 new scenarios' weights and seek modes are first
  passes, none flown yet; gate races aim at the ring's centre (Regatta's own AI aims through it, RegattaRaceLine). A
  dedicated Linux game build (no editor, no licence on
  each machine) would let x86 Linux servers run these too; not built.

## 17. The mothership: every computer, live, and runs sent to the one you pick (Shombith, 2026-10-28)

**The agent.** Each registered computer runs one agent (`compute.py run`, the supervisor loop; `Tools/Compute/agent.py`)
from login, on every OS: Windows a `FrogletCompute.vbs` in the Startup folder (it replaced the Task Scheduler ON IDLE
task, which only existed while the computer was idle, so the console could neither see the computer nor start a run on
it while its person used it), macOS a LaunchAgent, Linux a systemd user service. It holds a lock, so a computer has one
agent. It still works only when its person is away, at the lowest priority.

**What the console shows for each computer** (Computers page, and the first three on Home):

| Status | Means |
|---|---|
| Working | a session runs (a search or a game), with its progress |
| Free | its person is away: it works on the queue |
| In use | its person is using it |
| Outside its hours | it was installed with set hours (`--nights`, `--between`) and it is not one of them |
| Offline | nothing heard for 3 minutes (live console) or 45 minutes (git) |

and its OS, cores, memory, free disk, whether it has the project's Unity (and whether its person has Unity open), and
whether its owner lets runs start at once. The agent reports to the hub every minute (live), and to git when the
status changes (at most every ten minutes) and every half hour, so the claude.ai snapshot shows the fleet too.

**Nobody types a command on the other computer.** Its owner sets it up once (the Install line); from then on its agent
starts at login and polls the team's queue. Sending it a run means putting a request on that queue, which happens
where YOU are: one tap on the live console (the hub on the VM, any phone), or from the claude.ai snapshot through
"File it now" (a cloud session runs `request new --push`) or that same line in your own terminal. The agent on the
picked computer sees the request and starts it under the rules below.

**Sending a run to a computer** (New run, or "Send a run" on its card): Where = that computer, then When:
- **When it's free**: once its person is away (the default; every computer).
- **Now**: at once, even while its person uses it. Only on a computer whose owner allowed it:
  `compute.py remote-start on` there, or the setup with `--allow-remote-start` (off by default: a teammate's computer
  is theirs). Elsewhere the choice is greyed out and says so.
- **At a time**: not before the time picked (`not_before`), then as above.
The agent looks for such runs every minute through the hub (every 3 minutes through git without one). In a terminal:
`compute.py request new <job> --machine <person>/<computer> --start now --at 22:00 --push`.

**Stopping a run**: Stop on the computer's card or on the run (Runs page) marks it cancelled (`POST /api/cancel` on the
live console, `compute.py request cancel <id>` otherwise); the computer working on it checks every 30 s and stops as it
does for a returning person (`agent.StopWhenCancelled`), keeping what it found.

**Stop reaches every run.** A run started on its own computer from a token (`run --request <token>`, the New run
"This computer" line) is filed on compute-queue as it starts, aimed at that computer and marked `here` (no agent and no
other computer picks it up), so Stop and `request cancel` find it like any sent run. Ctrl+C in its window still stops
it at once. Before 2026-10-29 such a run was never on the queue and Stop could not reach it.

**Without the hub: the page's outbox and the hourly sync.** The claude.ai console cannot push to git itself, and
starting a cloud session from it can be blocked for a viewer. So when there is no hub, **Send** (New run) and **Stop**
write to the page's own store: `outbox/<viewer id>` holds `{items: {<item id>: {kind: run|stop, token|id, title, at}}}`
(each viewer appends to their own; Contributors and up). Once an hour a scheduled Claude Code session (Shombith's
account) exports `outbox` and `sent` with ArtifactData, runs `compute.py request outbox <outbox> --sent <sent> --out
<done> --push --local` (a run goes on compute-queue after the same checks as `request new`; a stop cancels; anything else is
refused and says why), writes each result to `sent/<item id>` (owner-written), rebuilds the page with
`compute.py console --sync-every hour` and republishes it. Runs shows what waits for that sync. The live console (the
VM) needs none of this: there Send and Stop act in seconds.

**Three kinds of work, tagged everywhere** (Runs, Home, the computer cards):
| Tag | What it is |
|---|---|
| Offline model | a computer searching the offline race model (Python, no Unity) |
| Unity · AI vs AI | a computer playing the real game in Unity with no window (the AI trainer, `unity-train`) |
| Played by <person> | a person playing in the Unity editor: the race is recorded and the hourly AI learning sweep learns from it |

**The hub** gained `GET /api/requests` (the open runs, whole, for the agents), `GET /api/requests/<id>` and
`POST /api/cancel`, all behind the team key. Without the hub everything above works through git, minutes slower.

**Blue pairs in the real game**: a pair the offline models cover (Rhino, Manta, Dolphin on the three courses) that the
Unity trainer also has a scenario for offers both in New run: "Offline race model" or "The real game (Unity)".

**People's races per pair**: New run says how often people raced the chosen vessel in that mode, and how many races the
hourly AI learning sweep (Yash's, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) learned from. That sweep is how the game's own AI
(AIPilot and its drives) learns from play: recordings go to `bugledger/<user>`, the sweep learns them into
`Ys-bleeding-edge`, and `ai_branch.py sync` brings AI files to `ai`. The Unity trainer (TrainingPilot genomes) is a
second, separate AI: it does not learn from recordings yet.

**Verification**: the self-test's end to end runs a hub and an agent: the agent outside its hours shows so live, takes
a run started from the console at once (its owner allowed it), and stops a long run stopped from the console; with
remote start off the run waits (the negative control). The page was checked in Chromium at desktop and phone widths
with five computers in every status. Not run yet: the Windows Startup launcher on a real Windows login.

## 12. Verification status (2026-10-26)

Run in the cloud session that built it (Linux, 4 cores, Python 3.13, numpy, no torch, no .NET):
`compute.py self-test` green (the searchers search and resume and a shuffled-loss control fails; `course_port` matches
the C# harness at every gate and lane point of all four intensities and a one-degree corner change is seen; every
evaluator's self-test; every job validates; two simulated machines against a throwaway remote publish, the second
starts from the first one's best, the board merges them, the checkout stays clean and not sparse). Local sessions:
`drive-rhino-headlong` 154 evaluations in 110 s (the shipped Rhino drive did not finish Headlong I4 on any of the 3
course seeds in the model; the search's best finished I1-I3 about a third faster); `bioform-forge` 1,512 evaluations in
33 s filled 96 of 120 cells. **Not run:** the OS installers on Windows and macOS (written to each OS's documented
command; the Linux crontab path was not exercised either), a push to `compute/*` on the real origin, any torch job.
The first person to `install` on Windows or macOS: run `compute.py doctor`, then `compute.py run --once` by hand, and
tell the tooling owner (Shombith) if anything misbehaves.

**Review round 1 (Shombith's session, 2026-10-26)**: jobs now run from `git archive` exports instead of sparse
worktrees (the old path wrote `extensions.worktreeConfig` into the person's `.git/config`, reproduced on git 2.43);
the self-update path was dead and now runs; the trust rules in section 7 were added. The self-test gained checks for
each (no worktree added, no config extension, box2 runs through the self-updating supervisor, seven trust cases), and
all of them fail against the previous code (the negative control). `drive-rhino-headlong` (44 evaluations) and
`bioform-forge` (216) ran through the export path locally. Still not run: Windows, macOS, a real push, torch.

**2026-10-28**: the first real push to origin worked. The cloud session published four jobs as
`compute/shombith/cloud-session` (`drive-rhino-headlong` 154 evaluations, `drive-manta-redline` 120, `bioform-forge`
1,512, `drive-rhino-regatta` 88), and the Compute Console reads them. Still not run: Windows, macOS, torch.

**2026-10-28, later**: the hub's end to end joined the self-test (a wrong key is refused, a run outside its search
is refused, a good run is filed on the throwaway remote's `compute-queue`, a worker's heartbeat shows live). The
console's New run and vessel x mode grid were checked in Chromium at 1360 and 390 px with the hub's endpoints mocked:
no script errors, no sideways scroll, the run posted is the one picked. `setup-vm.sh` passes `bash -n` but has not run
on a real VM yet.

**2026-10-28, Unity runs**: `FrogletComputeTraining.cs` compiles against the real Unity references with 0 errors
(`unity_refcompile --config editor`; a planted error in it failed the same run). The self-test's "unity runs" drives
the whole path against a fake Unity that keeps the same contract. **Not run: a real Unity.** The first person with
Unity 6000.3.17f1: the steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md` ("Froglet Compute runs the AI trainer").

## 18. The app: Froglet Compute on your own computer (Shombith, 2026-10-29)

"I am waiting to start it on my PC. I think it should be a PC application." The claude.ai console cannot reach a
computer: a run sent from it waits for the hourly sync, then for the computer to be free, and "This computer" gave a
line to paste. **The app** is the live console (the hub, section 15) on your own computer:

- **Open it**: double-click **Froglet Compute** on the desktop (the one-line setup puts it there, step 5), or run
  `python3 ~/.froglet-compute/app/Tools/Compute/compute.py app` (Windows: `python`). It opens the console in your
  browser at `http://127.0.0.1:8721`; close its window to quit it. `compute.py app --shortcut` makes the launcher again.
- **Start a run here**: New run, pick the vessel and mode, "This computer", **Start it on this computer**: it starts at
  once (a Unity run with "Show the game" opens Unity and the Third Eye), reports to the app live, and Stop is under
  Runs. Runs sent to other computers go straight onto the team queue (no hourly wait).
- **Safe by design**: it listens on 127.0.0.1 only and answers only requests addressed to that name (a web page
  elsewhere cannot reach it through a name that resolves here); the page it serves carries its key, so its buttons
  need no prompt, and every change still needs that key. A run is checked against its search exactly as the hub does.
- The claude.ai console stays the team's snapshot (and its outbox for a phone); the VM (section 15) stays the live
  console for everyone once it exists. Self-test: `compute.py self-test` "end to end" starts the app, checks the
  key in its page, a refused host name, a refused run without the key or outside its search, a run started here that
  finishes and reports live, and the desktop launcher (a negative control: with the host check removed it fails).

## 19. Follow-ups (debt this work created or saw; measured, not fixed here)

- **`unity-train` publishes no replay or still**: `_save_unit` keeps top-level files under `max_mb` 8 only
  (`worker.py`), so `<out>/replays` and `<out>/stills` stay on the computer that played; `recordings.json` lists them
  (section 16). A viewer on the console needs a store the files can go to (the VM's disk, or the artifact's assets).
- **The move of a trainer flight into `<out>/ai-flights` is a stopgap** for a game version from before 2026-10-29
  (`FrogletComputeTraining.MoveNewestFlight`); once every PC trains on a build whose recorder refuses trainer matches,
  delete it and the `ai-flights` row in `recordings.json`.
- **Both VM services ran `--quiet`** until 2026-10-29, so a failed push left nothing in `journalctl`; `setup-vm.sh`
  dropped the flag, and `compute_lib.log` still swallows a quiet worker's failures elsewhere (`install.py`'s task line
  passes `--quiet`). Decide whether a worker's failures should always reach its log.
- **`sync_job GUARDED` edit in `/ship-quick` broke the triplicate list** and was reverted (PR 1636's follow-ups);
  `ai_branch_sync.py` still names three places the learners' output lands.
- **`hub.py` and `console.py` read the whole of every results branch on each rebuild** (`gather`: `git show` per
  unit file). Two machines take seconds; measure at ten before the VM serves the team.
- **The console's "People raced this N times" counts flights by vessel and mode only** (`peopleRaces`), so a trainer
  flight stored before 2026-10-29 counts until `recordings()` reads `not_a_person` (it reads the seen list, not the
  learners' gate).

## 20. Update AI, and the people's seed: the two learning paths joined (Shombith, 2026-11-01)

Shombith's question: "a new game mode is created and users play it a few times, that data is recorded automatically
and reused by the AI in a compute run, and each vessel has an Update AI button which pushes its AI according to the
difficulty levels it trained on." Until today the two halves did not meet: the hourly AI learning sweep (Yash's,
`Docs/AI_SYSTEM/AUTO_LEARNING.md`) learned from people's races into the drive numbers, and a Unity trainer run bred its
own population from the gene registry's defaults; nothing from a run reached the game. Now:

- **The people's seed** (`Tools/Compute/people_seed.py`): before every Unity trainer run, `unity_run.py` reads the same
  flight records the sweep learns from (every `bugledger/<user>` branch, every flight the AI did not fly), keeps this
  vessel-and-mode's, and measures the gene-shaped quantities a person's flying shows: the throttle they hold when not
  boosting, the charge they wait for before a boost, how long they hold it, how long they wait between boosts. With
  two or more races (and three or more presses for a boost gene) it writes `<out>/seed.json` in the trainer's own
  genome shape and starts Unity with `-trainSeed`. `FrogletComputeTraining.ApplySeed` lays those genes over the
  registry defaults for the FIRST population of a fresh scenario (`TrainingSessionStateSO.PendingSeed`); a resumed
  population keeps what it learned, and a pair nobody has raced starts from the defaults as before. `status.json`'s
  `seed` says what was taken ("3 gene(s) from the people's races: 6 race(s) by shombith: ..."), the unit keeps the
  file, and the console's Vessel AIs page shows it. The seed is where the search starts, never where it must stay.
- **Update AI** (`Tools/Compute/archive_update.py`, the console's **Vessel AIs** page): the game already deploys
  trained AIs. `TrainingDeploymentService` installs the genome `Assets/_SO_Assets/AI Training/Archive.asset` holds for
  a (vessel, mode, intensity) on every AI seat of a normal match (intensity 4 is the trained vector; 1-3 dither it, or
  fly their own entry when `UseStoredGenomeForLowerIntensity`). A compute run writes its best genome only into the
  PC's project copy. The page lists, per vessel, every (mode, intensity) a run trained, the best fitness across every
  machine and epoch (`jobs/unity-train*/<epoch>/files/<unit>/genome.json` and its `status.json`), against the archive's
  entry on `origin/ai`. **Update AI** writes the newer ones in: one commit on the `ai` branch (plumbing, so it runs on
  the VM's bare clone) that upserts the vessel's entries in the exact YAML shape Unity writes, with `Approved-by:
  <name>`; the name must be one of the core four (section 8: a promotion is a person's yes; the page asks once and
  remembers). On the live console the hub does it (`POST /api/update-ai`, the team key, one at a time per vessel,
  `api/status` carries `updates`); on the claude.ai snapshot the press goes to the outbox and the hourly sync applies
  it (`compute.py request outbox`, kind `update-ai`); from a checkout, `archive_update.py update --vessel X --by NAME
  --push`. `ai` reaches `bleeding-edge` by its ordinary pull request, so the shipped game changes only when that merges.
- **What a person judges:** the page shows the trainer's fitness, which is the scenario's own score (gates threaded
  minus a time penalty, combat points, and so on). A run that scored nothing still has a "best" genome, so the first
  rows (2026-10-28 to 10-30, before the next-gate fix) read negative; Update AI is a yes to THAT genome, not to the
  idea. The archive entry's Notes name the unit, the machine, the episodes and the approver.

- **The AI flies where people fly** (Shombith, 2026-11-01, the step after the seed): the seed also carries the
  **people's lines**, the paths of their five best races of the pair (most rings, then longest; this intensity's
  first) resampled every 10 units (`linePoints` / `lineStarts`), and every fitness profile now has a third row,
  **PeopleLine** at weight 0.5 (`author_training_scenarios.py --refresh-profiles` wrote it into all 25 recipe
  profiles, guids kept): `PeopleLineFitness` samples the pilot's distance to the nearest point of any line every
  0.25 s and returns minus the mean, so among AIs that score the same the trainer keeps the one that flies where
  people fly, and a path 40 units off costs 20, under one gate, so the score still decides. With no line loaded
  (nobody raced the pair, or a run outside Compute) the term is exactly 0. `PeopleLineTests` pins it.

Gates: `people_seed.py --self-test` (two races seed the throttle and boost genes; one race, AI-held samples and two
presses do not; the pair filter and the trainer's token), `archive_update.py --self-test` (entries round-trip in
Unity's shape, replace and append, the best epoch wins across automatic and manual runs, only the core four promote),
both in `compute.py self-test`; `archive_update.py update` without `--push` built a real commit on `origin/ai` for the
Sparrow (one entry added, one file changed, parent `origin/ai`). The trainer compiled against the Unity references.
Not played in the editor: the checklist has the steps.
