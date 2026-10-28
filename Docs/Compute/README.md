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
  `ai`.
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
- **On the console**: New run (title bar, Runs, Home, and "Run this my way" on every search) is a form: pick the
  search, set each setting to search / narrower range / hold at, change how it is scored, tries, where, note; or
  describe it in words and let Claude fill the form in. It makes a token: a command to paste for your computer, or
  for the team "File it now" (starts a cloud session that runs `request new --token ... --push`) or the same command
  yourself. The terminal has `run <search> [--tries N] [--fix a=v] [--range a=lo:hi] [--arg k=v] [--note ...] [--team]`
  and `requests`.
- **Where results go**: each computer writes `requests/<id>.json` (its tries and best) to its results branch and
  runs the search as `<job>~<id>`, so a manual run never mixes with the job's own search; the evaluator still scores
  against the job's full space, so the numbers compare. Runs on the console shows every request, its state, tries,
  and its best against today's value under the same settings. `compute.py request list` and `request cancel <id>`.

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
