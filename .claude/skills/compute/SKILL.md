---
name: compute
description: Froglet Compute - put this computer's idle time to work on the team's highest-priority learning searches (AI drives, the Regatta course, elemental balance, swarm bioforms, NCA creature trainings) and read what every machine has found. Retrieves the ranked queue, sets a machine up to run whenever its person is away (set and forget, any OS), checks what it can run, shows the board, and says how a converged answer reaches the game. Use on "/compute", "what should my computer work on", "use my idle time / overnight / when I'm away", "set and forget", "contribute compute", "run the training", "the compute queue / board", "is my machine working", "stop the worker", "uninstall compute", or any question about Tools/Compute or Docs/Compute.
---

# /compute: the team's idle hardware, searching for the game

The design is `Docs/Compute/README.md`; read it before changing anything under `Tools/Compute/`. This skill is what a
session does for its person. The family: `/compute-job` (write a search: a space and a loss), `/compute-scout` (find
what to search next), `/regatta-balance` (the three-stage Regatta plan), `/bioform-forge` (swarm bodies, NCA creatures).

## 1. "What should my computer work on?" (retrieve the queue)

```
python3 Tools/Compute/compute.py top --why
```

It fetches the queue refs and every machine's results, ranks every job (`priority = value x unblocks x asked x
starvation / crowding`, each factor printed), and names the job THIS machine would take next and why the others wait
(a status, a stage not reached, a missing capability). Tell the person the top three in plain words: what each one
is for, who asked, and what its best answer so far is against today's shipped numbers. If nothing is runnable, run
`/compute-scout`: it suggests spaces worth exploring (an underplayed minigame or intensity, more bioform diversity).

## 2. Set and forget (the person says yes once)

Ask once ("Shall I set this machine to work on the queue whenever you've been idle 10 minutes?"), then:

```
python3 Tools/Compute/compute.py doctor     # capabilities; what to install to run more (numpy, torch, Node.js)
python3 Tools/Compute/compute.py install    # Windows: Task Scheduler on-idle task; macOS: LaunchAgent; Linux: systemd user unit
```

Say in two lines what it will and will not do (README section 2): it waits for 10 minutes idle on mains power, runs
at the lowest priority on all cores but one, stops the moment they touch the machine, never touches their checkout,
publishes only to `compute/<them>/<machine>`, and updates itself from `bleeding-edge`. `compute.py uninstall` undoes it.

- A box nobody sits at (a VM): `compute.py install --always`, or `run --always` in `tmux`/`screen`. The team's
  always-on VM (Oracle Always Free) also serves the **live console** (`compute.py hub`): `Tools/Compute/vm/setup-vm.sh`,
  README section 15. A computer joins the live console with `setup.py ... --hub <address> --hub-key <key>`.
- The fleet and sending runs: the console's Computers page (working / free / in use / outside hours / offline);
  a run sent to one computer starts when free, at a time, or at once where `compute.py remote-start on` was run; Stop
  cancels it (README section 17).
- A vessel and mode the offline models do not cover: the `unity-train` job plays it in the real game through the AI
  trainer, on a PC with Unity (`compute.py unity --find`; README section 16).
- Only at night, or any set hours: add `--between 22:00-07:00` (local time) to `run` or `install`.
- A manual run (someone's instructions for one search): `compute.py run --request <id|token>` now on this computer,
  or `compute.py request new <search> --tries N --fix a=v --range a=lo:hi --arg k=v --note "..." --push` for the team
  (README section 14). A token pasted from the console's New run is `request new --token <it> --push`.
- Joining from a person's machine: the console's Install button, or from their Cosmic-Shore folder
  `git fetch origin <ref> && git show origin/<ref>:Tools/Compute/setup.py | python3 -` (README section 1).
- The board people read is the **Compute Console** (README section 13, https://claude.ai/artifact/54nfQFCxNBFHYQ2mCErPt1).
  To refresh it: `compute.py console --out <dir> --prs <open PRs as JSON> --url <its url>`, then republish the
  artifact from `<dir>/index.html` with `console.json` beside it. Act on its approvals with
  `compute.py console --approvals <file> --profiles <file>` (only the VALID rows).
  File what people sent from the page (its `outbox`) with `compute.py request outbox <outbox.json> --sent <sent.json>
  --out <done.json> --push --local`, then write each result to `sent/<item id>` (README section 17).
- A laptop that should also work on battery: `--on-battery`. Fewer cores: `--cores N`.
- One job only (the person cares about one plan): `run --job <id>`.
- Never install without the person's yes; never install on a machine you are not running on.

**Unity open and nobody at it overnight?** The real-game counterpart is the genetic trainer's own overnight queue
(FrogletTools > AI > AI Training > Schedule; `Assets/_Scripts/Utility/AITraining/README.md`, studyholic / Garrett /
Shombith). Run one or the other on a machine, not both (README section 10).

A cloud session (this container) is a poor worker: it is reclaimed when idle. Run `self-test` or a short local
session there to check a change, never `install`.

## 3. "Is it working? What has everyone found?"

```
python3 Tools/Compute/compute.py board              # print it
python3 Tools/Compute/compute.py board --write      # Docs/Compute/BOARD.md + board.json (the scout sweep does this daily)
```

Per job: its best loss against the shipped values', evaluations, machine-hours, which machines, converged or not; per
plan, each stage; every machine's last heartbeat. Local logs: `~/.froglet-compute/worker.log` (installed) or the
terminal. A machine that published "evaluator failing" stopped that job on purpose: read `summary.json`'s `errors`
(`git show origin/compute/<user>/<host>:jobs/<job>/<epoch>/summary.json`) and fix the evaluator (`/compute-job`).

## 4. A job converged: promote it (never automatic)

```
python3 Tools/Compute/compute.py promote <job> [--out <dir>]
```

It prints the team's best (and bakes bioform elites with `--out`) and the job's `promote` line: what the answer
changes, with which tool, and who must agree. Then follow README section 8: a yes from any of the core four (Garrett, Caleb,
Shombith, Yash) in their own session, the area owner told (`python3 Tools/Build/who_works_where.py query <paths>`),
the area's own writer (never a hand edit), its gates, an
`Affects: <owner>'s <area>` line, an editor check in `Docs/UNITY_VERIFICATION_CHECKLIST.md`, and the job's `status`
set to `promoted` (unblocking whatever waits `after` it).

## 5. Rules

- **The person's checkout is sacred.** Never run the worker in a way that writes their tree; never point `--home` or
  `--queue` at a branch the person has not agreed to run.
- **A result is a proposal.** Compute never changes a game file. Promotion goes through the owner of the area
  (CLAUDE.md: never re-tune another collaborator's work without them; the AI is Yash's, UGS/backend Shombith's).
- **The editor is the measurement.** Every evaluator is an offline model with its limits in `metrics.limits`;
  quote them with any number you report.
- **No new dependency without saying so.** Python 3 is required; numpy, torch, Node.js and .NET are optional and
  unlock jobs; tell the person what each one unlocks before they install it.
- **Tooling is Shombith's area** (Docs/TEAM.md): a change to `Tools/Compute` or these skills carries
  `Affects: Shombith's tooling`.
- **After changing `Tools/Compute`:** `python3 Tools/Compute/compute.py self-test` (about 10 s; `--quick` skips the
  end-to-end run) must be green before the commit.

## 6. Traps (measured; add to this list, never re-learn)

- **`python3 Tools/Compute/selftest.py` run directly does NOTHING** (exit 0 in 0.05 s): the self-test is
  `python3 Tools/Compute/compute.py self-test` (`--quick` skips the end to end). Three commits on 2026-10-29 cited
  "compute self-test green" from the bare file. A gate that cannot fail is not a gate: time it, and read what it printed.
- **The console built in a fresh session (the hourly sync) read no job** while Froglet Compute lives on its own branch:
  `QUEUE_REFS` are bleeding-edge, `ai`, `compute-queue`, and a worker's `config.json` adds the branch, but a new cloud
  session has no config.json. `console.default_queue` adds `setup.APP_REF` until `Docs/Compute/jobs` is on
  bleeding-edge. Symptom: "Searches 0" and "no trainer yet" on every vessel (2026-10-29).
- **A page on claude.ai cannot reach a person's computer, and neither can a cloud session push to `ai`**: a run sent
  from the snapshot waits for the hourly sync, then for the computer to be free. The answer is the always-on VM (README
  section 15), not an app on the person's PC (section 18 exists, and it only helps the PC it runs on).
- **`ai_branch.py` sync, two false stops and four real losses (2026-10-29)**: Yash merges Ys-bleeding-edge into
  bleeding-edge himself, so `ai` takes his work through the bleeding-edge merge and the replay from the old trailer read
  his own edits as a conflict; the first fix then lost work in scratch repos (a rename on Ys carried a file over a fix
  made on `ai`, a deletion on `ai` undone, a rollback overwritten, work dropped after a partial promotion). git 2.43's
  `merge-tree -Xno-renames` does NOT turn rename detection off; the replay is now per file, by blob, with no rename
  guessing. Test any change to it with the self-test AND the 40 scratch scenarios' shape (a promoted Ys that keeps
  editing, an older Ys head promoted, criss-cross merges), and run 20 random ten-week histories with an oracle after
  every sync before pushing `origin/ai`.
- **The AI trainer's seat reads as a human's** to anything that asks the AIPilot (it is switched off): mark the trainer
  through `PilotControl.IsHeldByTrainer`, and remember the reload tail after an episode (the trainer released, the turn
  still running) belongs to the trainer too. Seven trainer matches were stored and sent as Shombith's races before this
  (`learn_from_flights.NOT_A_PERSON` names them).
- **A Compute Unity run on a watched PC sends its reports through the person's own editor**: `BugHuntAutoSend` is
  active in watch mode (not batchmode); whether it pushes from Compute's project copy under the person's name is
  unverified. Check `origin/bugledger/<user>` after the first watched run.

