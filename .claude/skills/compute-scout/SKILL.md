---
name: compute-scout
description: Find what the team's idle hardware should search next - the Froglet Compute scout. Reads the repo through lenses (every vessel's Time games x I1-I4 coverage, the Regatta balance plan's stages, swarm bioform diversity, hand-rolled sweeps, labs with headless scorecards, numbers set by hand or by eye, the board's health), ranks the opportunities, queues the mechanical ones, and proposes new searches - including a search that answers someone's problem they never thought of as a search. Use on "/compute-scout", "find compute opportunities", "what else could we be learning", "the queue is empty", "suggest something to explore", "what is underplayed / untrained", "could a search solve this", in the daily scout sweep (Docs/Compute/README.md section 9), and whenever a session notices a problem shaped like a space and a loss.
---

# /compute-scout: what is worth searching next

The queue should never be empty and never be stale. The scout is how a session (or the daily sweep) keeps it full of
work that matters. Read `Docs/Compute/README.md` first; `/compute-job` is how an idea becomes a job.

## 1. Run the lenses

```
python3 Tools/Compute/compute.py board --write        # first, so the board lens is current
python3 Tools/Compute/compute.py scout --write        # Docs/Compute/OPPORTUNITIES.md + opportunities.json
python3 Tools/Compute/compute.py scout --propose      # queue the mechanical ones (drive jobs whose model exists)
```

| Lens | Notices | Turns into |
|---|---|---|
| `coverage` | every vessel x its Time (race) games x I1-I4: people's races, learned drives, offline models | a drive job where a model exists; a `build` (write a model) where none does - an underplayed minigame or intensity can only learn from self-play |
| `regatta` | the three-stage Regatta plan: which stage is live, which hull lacks a model for its own Time game | stage-1 model builds; unblocking stage 2 and 3 when the one before converges |
| `bioforms` | how many bodies a swarm can switch into, per majority element; trained NCA creatures not in the game | forge runs for the thin elements; NCA trainings; ports |
| `sweeps` | `--sweep` grids, `itertools.product`, `tuneall` already in `Tools/Build` | an evaluator around the author's own loss |
| `labs` | labs with a headless `runBatch` scorecard | a node evaluator over the lab's dials |
| `by-hand` | recent docs saying "set by hand", "by eye", "a guess", "reasoned, not measured" | a search that answers someone's open question |
| `plans` | every arena card is a mixed fleet to balance; Regatta's plan (Time) and Broadside's (Charge, `SILENCE.md` section 6, Garrett) are set, the rest unproposed | a plan's evaluators and jobs, the Regatta way (`/regatta-balance`) |
| `board` | converged-not-promoted, evaluators failing on every point, a top job no machine is on | promotion (owner's call), a fix, a nudge to `/compute` |

## 2. Think past the lenses (the part only a session can do)

The lenses find what looks like a search. Your job is the rest: read the last day of merges
(`python3 .claude/hooks/team_activity.py --peek 12`), `Docs/QA/DEV_TASKS.md`, `Docs/BRANCH_PARTS_STATUS.md`, the
open Froglet Tasks, and ask of each problem: **is there a space of choices and a number that says which is better?**

- "The Thresher's sling feels random at I3" -> the sling's timing fields x the ThresherTime race time.
- "Nobody can tell which Tandava form reads best" -> MAP-Elites over form genomes with a legibility measure.
- "The ecology collider budget keeps getting blown" -> spawn-profile counts x (collider count, liveliness).
- "Card art takes a person an evening" -> camera pose x a composition score over `/cardart`'s offline render.
- "This mode's end target is reasoned, not measured" -> the target x a sim's match length distribution.

Write each as a `proposed` job (`compute.py new <id> --evaluator <module-to-build>`) whose `goal` names whose problem
it answers and why a search fits, whose `promote` names the owner, and whose `value` you can defend. Never `ready`
until the evaluator exists and a short run improved on the baseline (`/compute-job` section 6).

## 3. Rank honestly

`value` (0-10) is what a converged answer is worth to the game, not how interesting the search is:

| Value | When |
|---|---|
| 9-10 | it unblocks a plan someone is following now (a Regatta stage), or fixes an AI that cannot finish |
| 7-8 | a cell people will play and nobody has trained; diversity the game visibly lacks (a thin element's bodies) |
| 5-6 | checking a person-learned or hand-set number; a lab's dials |
| 2-4 | a curiosity, a geometry fit, an idea without an owner yet |

The priority formula multiplies by how many jobs wait on it, whether a person asked, how long nobody worked it, and
divides by machines already on it (`compute.py top --why`).

## 4. What the scout never does

- **Never promotes** a converged result: it lists it for the owner (`/compute` section 4).
- **Never files a team ticket.** A hand test or decision a search leaves for a person may be SUGGESTED through
  `/team-tasks suggest`, only with the session's person's yes (CLAUDE.md).
- **Never re-tunes someone's area.** A job whose answer would change another collaborator's numbers names them as
  `area_owner`, and the job stays a proposal for them.
- **Never pushes game code to `compute-queue`.** That branch carries `Docs/Compute/**` only (README section 7).

## 5. The daily sweep

A Claude scheduled task does README section 9 once a day (the scout, the board, the mechanical proposals, reasoned
`proposed` jobs, pushed to `compute-queue`). Change the sweep by editing that section, never the task.
