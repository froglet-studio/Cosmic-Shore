# The AI plan: one AI per vessel and element, chosen in the squad (LOCKED, Garrett 2026-11-02)

**Every AI training effort on Cosmic Shore works toward this plan and nothing else**: compute searches, the AI learning
sweep's learners, Unity trainer runs, and a session tuning an AI by hand. The steps are below (section 4) and
machine-readable in [`ai_plan.json`](ai_plan.json), which Froglet Compute enforces (section 6). The other AI docs are
how a step is done. [`MINIGAME_TRAINING_PLAN.md`](MINIGAME_TRAINING_PLAN.md) (Yash) is how each vessel trains in its
own games, [`REGATTA_BALANCE_PLAN.md`](REGATTA_BALANCE_PLAN.md) is the Time arena's three stages,
[`AUTO_LEARNING.md`](AUTO_LEARNING.md) is how people's play reaches the AIs, and
[`../ElementalAbilitySystem/SILENCE.md`](../ElementalAbilitySystem/SILENCE.md) section 6 is the Charge balance plan.
Where one of them disagrees with this page, this page wins and the disagreement goes to section 7.

Garrett, 2026-11-02:

> "We need our planning and learning to be working towards an AI training system that produces all the AI capabilities
> we need. The culminating experience is voyages. The AI you are playing against or with will be determined by the
> choices during squad configuration: vessel, element, pilot, artifact. [...] selecting a squirrel with the time element
> and a pilot who can spot omnicrystals will have your AI squadmate denying crystals to everyone. Swap out for charge and
> a pilot who sees opposing vessels and they will be jousting players much more often. [...] arcade game intensity
> levels -> arena game for the same element -> voyages."

## 0. In one paragraph

Every seat in a squad that no person holds is flown by an AI (`Docs/Voyages/VOYAGES.md` section 2). The cards on that
seat choose the AI:

- **The vessel and the element choose the genome.** Each vessel has one AI per element, and each element is a set of
  skills:
  - **Time:** reach objectives quickly.
  - **Charge:** joust and strip petals.
  - **Mass** and **Space:** to be named.
- **The pilot biases that genome's decisions** toward what its callsign sees (`Docs/Pilots/CALLSIGNS.md` section 4,
  rule 4).
- **The artifact comes later.**

The genomes are trained up a ladder:

1. Each vessel learns an element in that element's **arcade game(s)**, scored on all four intensities at once. The
   result is one universal genome for that vessel and element.
2. The **arena game** for that element is balanced against those fixed genomes: first its course or features, then the
   vessels' elemental baseline.
3. **Voyages** fly the genomes, chosen and biased by the squad's cards.

The Time element is the live step. Charge is next. Mass and Space come after.

## 1. Where it ends: the squad seat's AI

| Card | What it does to the AI | State |
|---|---|---|
| Vessel | its kit; one AI per element per vessel | the Time AIs are in training (section 5) |
| Element | which genome flies: the skills that element's arcade game taught | Time live; Charge next |
| Pilot | a bias on that genome's choices toward what the callsign sees. Examples: OPAL (omni crystals) values them more and denies them to rivals; BANDIT (opposing vessels) goes looking for jousts | not built (step `pilot-bias`) |
| Artifact | later | - |

A pilot never changes what the vessel can do. A game mode never changes the vessel (CLAUDE.md, LOCKED), and neither
does a pilot. A pilot only changes **what the AI goes after**. For a human, the sight touches nothing but photons; for
the AI, the same callsign weights its targets.

## 2. The ladder, per element

| Element | Arcade games (train here, I1-I4) | Arena game (balance here, against fixed AIs) | Plan |
|---|---|---|---|
| Time | each vessel's own Time card: Skim Race (Squirrel), Headlong (Rhino), Redline (Manta), Switchback (Dolphin), Skein (Urchin), Slingshot (Stoat), Warpline (Meerkat) - `MINIGAME_TRAINING_PLAN.md` section 2 | Regatta | `REGATTA_BALANCE_PLAN.md` |
| Charge | each vessel's Charge card (mirror matches) | Broadside | `SILENCE.md` section 6 |
| Mass | each vessel's Mass card | to be named | - |
| Space | each vessel's Space card | to be named | - |

"An element's arcade game" is the card whose genre petal is that element (`ModeGenre.cs`).

## 3. The rules (LOCKED)

1. **Name the step.** Every AI training effort serves one step of section 4.
   - A compute job carries `plan` and `stage` (or `plan_step`). No worker runs a job off the plan, and
     `compute.py validate` and the self-test fail on one (section 6).
   - A learner, a Unity trainer run or a hand tuning names its step in its commit: `Plan step: time-arcade`.
   - Work that serves no step is a proposal to change this page, made to Garrett, before it is built.
2. **One genome per vessel per element.** The deliverable of an arcade step is ONE AI per vessel for that element,
   scored on a composite over all four intensities of that element's arcade game(s):
   - the mean over intensities plus 0.5 × the worst, so no intensity is given up for the others;
   - per-intensity files and per-mode variants (`_I<n>`, `_<Mode>`) are seeds and data, not deliverables.
3. **Grow from every genome.** A new round's first generation is every genome that exists for that vessel and element,
   plus mutations of each:
   - shipped files, people-learned files and the team's best so far;
   - never from scratch while a genome exists. `optimizers.SepCMAES.seed` does this; a job's evaluator reads the
     genomes from the shipped assets (`seeds(args, root)`).
4. **The arena is searched against fixed AIs; no AI is trained inside it.**
   - An arena step searches the arena (course, features, elemental baselines) with every vessel's promoted universal
     genome fixed.
   - An AI is never trained in the arena game. This is Yash's rule too, `MINIGAME_TRAINING_PLAN.md`: never in Regatta.
   - Nothing is balanced against an AI that has not converged. SILENCE section 6: "never balance against an AI that
     has not plateaued".
5. **Balance in this order:** the arena to the vessels, then the vessels to their elements (each vessel's elemental
   baseline).
6. **People's play is training data, automatically.** `AUTO_LEARNING.md` stays LOCKED. Never ask a person to run a
   learner. Their flights feed the genomes' rounds as seeds and as checks.
7. **Promotion is never automatic.**
   - Any of the core four approves.
   - The AI owner, Yash, is told.
   - The answer is written with the area's own writer, never by hand.
   - The editor is the measurement, and every promotion adds an editor check to
     `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 4. The steps

`ai_plan.json` is the same list; change both together.

| Step | Element | What it produces | State (2026-11-02) |
|---|---|---|---|
| `time-arcade` | Time | each vessel's universal Time genome (section 5) | **live** |
| `time-arena-course` | Time | Regatta's course features, searched against the fixed Time genomes: the course that levels the fastest and slowest | blocked on `time-arcade` (jobs `regatta-course-i1..i4`, which wait on the Squirrel, Rhino and Manta Time genomes) |
| `time-arena-elements` | Time | the smallest elemental tuning that closes what the course leaves: each vessel's Time baseline | blocked (job `regatta-elements`) |
| `charge-arcade` | Charge | each vessel's universal Charge genome, from mirror matches in its Charge arcade game, scored on petals taken | next: needs the per-attacker petal ledger (SILENCE section 6) |
| `charge-arena` | Charge | Broadside balanced until every vessel's petal plateau is the same | blocked |
| `mass-arcade`, `mass-arena` | Mass | the same two steps for Mass | later |
| `space-arcade`, `space-arena` | Space | the same two steps for Space | later |
| `pilot-bias` | any | per callsign, the bias over an element genome's target choices | later |
| `squad-ai` | any | a squad seat flies the genome its vessel and element choose, biased by its pilot; voyage policies (`VOYAGES.md` section 6.6) are built from these | later |

## 5. `time-arcade`: where each vessel stands

| Vessel | Its Time game | The universal genome's search | State |
|---|---|---|---|
| Squirrel | Skim Race | `skimrace-squirrel-time`: one policy for I1-I4 with the composite loss, its first generation grown from the three shipped genomes (the general `SkimRaceAIConfig`, `_I1`, `_I2`) | ready (see the measurements below) |
| Rhino | Headlong | `drive-rhino-headlong` (mean ln seconds over I1-I4) | ready |
| Manta | Redline | `drive-manta-redline` (mean ln seconds over I1-I4) | ready |
| Dolphin | Switchback | Yash's learner writes a drive per intensity (`_Switchback_I<n>`, `AUTO_LEARNING.md` section 7). The Blast Run AI was rebuilt in four steps on 2026-11-02 (Yash, `MINIGAME_TRAINING_PLAN.md` section 9.1). Its universal search is `switchback-dolphin-time` (2026-11-02): one charge drive for I1-I4 with the composite loss, its first generation grown from the four shipped genomes (the mode-wide drive, `_I1`, `_I2`, `_I4`), raced on `Tools/Build/switchback_sim_harness` (the shipped course, Blast Run layout, planner and avoidance) by the `switchback_fit` evaluator | ready |
| Urchin | Skein | none | build: a rail-ride model of Skein |
| Thresher | Thresher Time | Claude sessions with Yash (`MINIGAME_TRAINING_PLAN.md` section 12): the sweep's `ThresherAIBoostPolicy_ThresherTime` from people's races, the aimed launch (2026-11-02); no offline model, so no search yet | build: a Thresher Time model (added to this table 2026-11-02 by Yash's session; the Thresher is next after the Stoat in Yash's order) |
| Stoat | Slingshot | Claude sessions with Yash (`MINIGAME_TRAINING_PLAN.md` section 8) | held for the Stoat's configuration |
| Meerkat | Warpline | Claude sessions with Yash (section 8) | building |

**Measured for the Squirrel** (2026-11-02, the shipped pilot core in the Skim Race sim, 6 races per track, seedbase
5000; the per-track score is time over ideal, 1.0 being ideal, averaged over races plus half the worst race, so 1.5
is the floor):

| Genome | I1 | I2 | I3 | I4 | Composite |
|---|---:|---:|---:|---:|---:|
| General `SkimRaceAIConfig` | 2.47 | 2.89 | 4.56 | 2.79 | **5.46** |
| `SkimRaceAIConfig_I1` | 2.42 | 4.37 | 7.61 (4/6 finish) | 3.84 | 8.36 |
| `SkimRaceAIConfig_I2` | 3.56 | 3.00 | 5.71 | 5.77 (5/6 finish) | 7.40 |
| What the game flies today (each intensity's own file where it fits) | 2.42 | 3.00 | 4.56 | 2.79 | 5.47 |

The general policy is already the best all-rounder. Each per-intensity file wins only on its own track, and only by a
little. I3 is where the Squirrel is weakest: its winners take 2.7× the ideal time.

## 6. How the plan is held to

| Where | What enforces it |
|---|---|
| Froglet Compute | Every job names a `plan`. If that plan is one of `ai_plans` in `ai_plan.json`, the job must serve a step (`plan_step`, or a step whose `jobs` match its plan and stage); `other_plans` lists the plans that are not AI work (bioforms). A worker never runs an off-plan job, retired ones included (`worker.ranked`: "off the AI plan"); `compute.py validate` and `compute.py self-test` fail on one, and the self-test proves the gate can fail (`compute_lib.plan_problem`) |
| Sessions | CLAUDE.md § AI training serves one plan; `/vessel-ai`, `/compute-job`, `/compute-scout` and `/regatta-balance` send every AI session here first |
| Commits | `Plan step: <id>` in any commit that trains or tunes an AI |
| Retired work | `ai_plan.json` `retired_jobs`: the three drives searched inside Regatta (rule 4) and the two one-intensity Squirrel searches (rule 2). Their answers are not promoted |

Not mechanically gated yet: the AI learning sweep's learners (`Tools/Build/ai_learn.py`, Yash) and the Unity genetic
trainer (studyholic, Garrett, Shombith). Decision 2 below proposes the same gate for them.

## 7. Decisions waiting

1. **Per-intensity files** (Yash). **Answered in part (Yash, 2026-11-02): the sweep keeps learning them from people's
   races, and they are the seeds of the vessel's universal search, never its result** (`switchback-dolphin-time` is the
   first such search). Still open: retiring each one once the universal genome beats it on its own track, which Yash
   decides from the promotion re-race. Under rule 2 they are seeds. Should they be retired once the universal genome beats
   each one on its own track? And should the sweep keep writing `_I<n>` files, or feed the vessel's universal search
   instead?
2. **A step for every learner** (Yash, Shombith). Each learner in `ai_learn.py` would declare the step it serves, and
   the sweep would refuse one that serves none: the compute gate's twin.
3. **The Mass and Space skills, and their arena games** (Garrett): what each element's arcade games teach.
4. **How a pilot's bias is represented** (Garrett, with Yash for the AI and Shombith for the callsigns). The proposal is
   per-callsign weights on the values an AI's planner gives to targets (OPAL: omni crystals up, and taking them from
   rivals; BANDIT: rivals' hulls), learned in the element's arcade and arena games on top of the element genome.
5. **The Squirrel in the Regatta course search.** `regatta_course` races the Squirrel on `regatta_balance.py`'s
   rail-skim model, not its AI. `time-arena-course` needs the Skim Race pilot core on Regatta's course, a build the
   scout lists.
6. **The per-mode Regatta variants** (`SkimRaceAIConfig_Regatta`, `MantaSoarAITuning_Regatta`, learned from people's
   Regatta flights). Under rule 4, Regatta flies each vessel's universal Time genome; the variants stay until the
   course step promotes, which is Yash's call.

7. **Switchback's difficulty table** (Yash, the AI owner). Switchback sets each AI pilot's nose lag from a row per
   intensity and difficulty, tuned to Yash's time bands (`SWITCHBACK.md` "Lag by difficulty"). It is a difficulty
   setting, not a genome, but it is per intensity, and its Legendary flies faster than the shipped pilot. Should a
   universal Dolphin Time genome carry one nose rate per level instead, once that genome exists?

## 8. Status log

| Date | What moved |
|---|---|
| 2026-11-02 | Dolphin (`time-arcade`, Yash): the universal search `switchback-dolphin-time` is queued (Yash's answer to decision 1: the learned files seed it). A first local run beat today's drives at every difficulty on fresh courses (composite at Hard 1.36 vs 1.39); not promoted (`MINIGAME_TRAINING_PLAN.md` section 9.1) |
| 2026-11-02 | Dolphin (`time-arcade`, Yash): the Switchback Blast Run AI rebuilt in four steps and its difficulty set by AI nose lag (offline I4 140 / 129 / 120 / 112 s, Easy to Legendary); `switchback_sim_harness` is the Switchback course port the Dolphin's row asked for. The question it raises is decision 7 |
| 2026-11-02 | Plan set (Garrett). `ai_plan.json` and the compute gate built. `skimrace-squirrel-time` (Squirrel, I1-I4 composite, seeded from three genomes) replaces the two one-intensity Squirrel searches. The three Regatta-trained drive searches are retired. The Regatta course searches now wait on the Squirrel, Rhino and Manta Time genomes |
