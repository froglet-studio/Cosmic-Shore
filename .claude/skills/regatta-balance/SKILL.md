---
name: regatta-balance
description: Guide the three-stage Regatta balance plan (Garrett 2026-10-26; Yash follows it, Claude guides) - stage 1 trains each vessel's AI to race its own Time games well at I1-I4, stage 2 learns the Regatta course that minimises the gap between the fastest and slowest trained AIs, stage 3 learns the smallest elemental tuning that closes what is left and makes it each vessel's mobility baseline. Says which stage is live, what blocks the next, which compute jobs serve it, how each stage's answer is promoted and by whom. Use on "/regatta-balance", "the regatta plan", "balance regatta", "level the field", "fastest vs slowest AI", "regatta course search", "elemental baseline", "minimal elemental tuning", "what's next for regatta", or any session Yash starts on Regatta's AI, course or balance.
---

# /regatta-balance: three searches to a level Regatta

The plan, its numbers and its status log: `Docs/AI_SYSTEM/REGATTA_BALANCE_PLAN.md` (read it first, every time). The
compute side: `/compute` (run, board, promote) and `/compute-job` (a new model or job). The AI rules: `/vessel-ai`
(input-only, difficulty edits belief, the testing ladder). The Regatta mode: `Assets/_Scripts/Controller/Arcade/REGATTA.md`.
The person who follows the plan is **Yash**; Garrett set it. A session guides: it says where the plan stands, does the
next step it can, and leaves the decisions to them.

## 1. Where does it stand? (start of every session)

```
git fetch origin && python3 Tools/Compute/compute.py board --queue Ys-bleeding-edge bleeding-edge compute-queue
python3 Tools/Compute/compute.py scout | sed -n '/The Regatta balance plan/,/^## /p'
```

Then tell Yash, in three or four lines: the live stage; per job of that stage, its best against the shipped values and
whether it converged; what blocks the next stage; the ONE next step and who takes it.

## 2. Stage 1: an AI per hull, on its own Time games, I1-I4

- **Jobs** (`plan: regatta-balance, stage: 1`): `drive-<hull>-<mode>`, scored by `drive_fit` (mean ln race seconds
  over I1-I4 and course seeds). Own games: Rhino Headlong, Manta Redline (ready); Regatta variants for the Rhino,
  Manta, Dolphin (ready).
- **Missing models are the real work of this stage** (each is a session, `/compute-job` section 7 "a port with a
  parity gate"): the Dolphin's Switchback (port `SwitchbackCourse`; `switchback_dolphin_sim.py` has the drive), the
  Urchin's Skein (a rail-ride model), the Squirrel's Skim Race (wrap `skimrace_sim_harness`'s `tuneall` as a command
  job; needs `dotnet8`). The Stoat is **held** for Garrett's configuration (issue 1198): build nothing for it.
- **People's play stays the primary teacher** where they play (`AUTO_LEARNING.md`). Compute fills the intensities
  nobody plays and checks the person-learned drives; when the two disagree, show Yash both numbers and the races.
- **Promotion (Yash's call)**: `compute.py promote drive-<hull>-<mode>`; with his yes, write the fields into
  `<policy>_<Mode>` with the learner's writer on `Ys-bleeding-edge`, run the gates, add an editor race to
  `Docs/UNITY_VERIFICATION_CHECKLIST.md`, set the job `promoted`.
- **Done when** every Regatta hull (but the held Stoat) has a promoted drive that finishes all four intensities of its
  own game in the model AND an editor race agrees.

## 3. Stage 2: the course that levels the field

- **Jobs** `regatta-course-i1..i4` (`regatta_course`: ln(slowest/fastest) with rail, length and character penalties).
- **Unblock** (a session, once stage 1 is done): set each job's `evaluator.args.roster` to the promoted drive assets,
  set `status: ready`, push on `Ys-bleeding-edge`. Tell Yash the stage moved.
- **Promotion (Yash's call)**: the settings become `RegattaCourse.ForIntensity`'s row and seed (Yash's code; the
  generator is shared with Headlong and Redline through `HeadlongCircuit`, so change only Regatta's settings); then
  `regatta_course_harness/run.sh` -> `regatta_course_measurements.json`, `export_course.sh` -> `course.js`,
  `author_regatta_assets.py --check`, `course_port.py --self-test` (it must still match the regenerated harness), and
  editor races of the trained AIs.
- **Done when** each intensity's spread is at the agreed target (decision 1 in the plan; proposed 1.15x) or converged.

## 4. Stage 3: the smallest elemental baseline

- **Job** `regatta-elements` (`regatta_elements`: mean ln spread + minimality x mean |level|; one Time level per hull).
- **Unblock**: put the promoted course settings and seeds into `evaluator.args.courses` / `seeds`, `status: ready`.
- **Promotion (every affected hull's owner)**: `who_works_where.py query` on each hull's assets names them; the levels
  become the vessels' elemental baselines (`author_regatta_assets.py` StartingElements and the vessel's assets),
  then `regatta_balance.py` and editor races confirm. Use `/element-ability-table` to say what each level does on
  each hull before anyone agrees.

## 5. Keep the plan doc true

Every session that moves the plan adds a row to the plan's status log and updates its stage tables (the tables are
the truth people read). A session that changes an evaluator bumps its `VERSION` and says why in the log.
