# The elemental vessel loop (TOP PRIORITY, Garrett 2026-11-12)

**Anyone with cycles works here first.** Run `python3 Tools/Build/elemental_loop.py next`, take the top action nobody
holds (claim the vessel's lane with `/swimlanes`), do it, and record it with `elemental_loop.py set`. The board is
[`STATUS.md`](STATUS.md) (generated), the sign-offs are [`loop.json`](loop.json), and `/elemental-loop` is the skill
that walks a session through a step.

This page is the process. It sits above the existing plans and does not replace them: the AI plan
([`../AI_SYSTEM/AI_PLAN.md`](../AI_SYSTEM/AI_PLAN.md)) is how steps 3-5 are done, the Mass and Space games plan
([`../MassAndSpaceGames/PLAN.md`](../MassAndSpaceGames/PLAN.md)) fills the game kit, and
[`../AI_SYSTEM/REGATTA_BALANCE_PLAN.md`](../AI_SYSTEM/REGATTA_BALANCE_PLAN.md) and
[`../ElementalAbilitySystem/SILENCE.md`](../ElementalAbilitySystem/SILENCE.md) section 6 are the Time and Charge arenas.
Where one of them disagrees with this page, this page wins and the disagreement goes to section 9.

## 0. Garrett's words (2026-11-12, the brief)

> 1. tune the vessel's elemental abilities and elemental games (1 intensity level for each element) together,
>    optimizing for fun. 2. then make the intensities level to produce variance across fun optimized environments that
>    serve the goals of each intensity level (intro, fun, challenging, very hard) and demonstrate the capabilities of
>    the vessel. 3. train AI on all four intensity levels for an element and couple that training to that element.
>    Voyage squads and arena games will use these elemental behaviors. 4. all elementally trained AI are used as a fixed
>    baseline pool for training an interesting environment creator that can make random balanced environments for an
>    elemental arena game. 5 after step four plateaus it will expose vessel imbalance which is used to tune vessels and
>    their elements and sent back to step 2-4. until there is no need for another step 5.
>
> With each iterative loop it should need less human involvement in the loop. then new vessels follow step 1. and run
> through the whole loop, which may induce rebalancing on more than just their vessel, which will disrupt the meta on an
> optimized schedule before it gets stale.
>
> Not all vessels will have a mass game e.g. dolphin. Some might not have a space game (squirrel doesn't, but it could
> have a stealing game). Every vessel should have at least 3 arcade games: time game (racing), a charge game (pvp), and
> at least a Mass (creation) or Space (destruction) game. Then we must polish at least three multi-vessel arena games:
> regatta (time), the charge one, and brood rush. Brood rush is both mass and space together. Thus the time arena game
> allows us to balance the mobility of each vessel and the charge arena game allows us to balance the pvp ability of
> each vessel, but mass and space are coupled such that you can be bad at one and good at the other because it is your
> net affect on the control of the space that gets balanced. so each vessel will lie naturally somewhere on the
> cultivator/destroyer spectrum and that will be measured for each vessel from how things shake out, not explicitly set.
>
> [...] I need your help in making sure that pushing every vessel through this plan in a systematic way remains the
> highest priority for anyone who has any cycles.

## 1. The loop at a glance

```
             per vessel, per element                         per arena game (all vessels at once)
 ┌──────────────────────────────────────────────┐   ┌───────────────────────────────────────────────┐
 │ 0 kit      Time + Charge + (Mass or Space)    │   │ 4 environment creator, trained against the    │
 │ 1 fun      abilities + game, one intensity    │──▶│   fixed pool of step-3 AIs, until it plateaus │
 │ 2 ladder   I1 intro, I2 fun, I3 challenging,  │   │ 5 the plateau's leftover = vessel imbalance:  │
 │            I4 very hard                        │   │   tune the vessel and its elements            │
 │ 3 AI       one genome on I1-I4, per element    │   └───────────────┬───────────────────────────────┘
 └──────────────────────▲───────────────────────┘                   │
                        └──────── redo steps 2-4 for what step 5 tuned ┘   (until no step 5 is needed)
```

| Element | Arcade games (steps 1-3, one per vessel at least) | Arena game (steps 4-5) | What the arena balances |
|---|---|---|---|
| Time | the vessel's racing game | **Regatta** | mobility |
| Charge | the vessel's pvp game | **Broadside** | pvp: petals taken (PvP is petals only, LOCKED) |
| Mass | the vessel's creation game (or a stealing game) | **Brood Rush** (Mass and Space together) | **net control of space**: mass you hold minus mass taken from you; each vessel's place on the cultivator/destroyer spectrum is measured, never set |
| Space | the vessel's destruction game | **Brood Rush** | (the same) |

What the element of a game is comes from the game itself (`ModeGenre.TryElementsFor`, read by
`Tools/Analytics/server/mode_elements.py`), never from a list typed into a doc.

## 2. Step 0: the game kit

**Rule.** Every vessel has at least three arcade games: a Time game, a Charge game, and a Mass or a Space game. A
vessel whose kit has no real mass-making is excused from Mass (the Dolphin); one with no destroyer verb is excused from
Space (the Squirrel, which may get a stealing game, and stealing scores as Mass). Excuses are in `loop.json`
`no_game_ok`, so they are visible and deliberate. Time and Charge are never excused.

**Measured, not typed.** `elemental_loop.py` reads every card on the arcade roster and counts a shared card (Scurry,
Maelstrom's genreless cards excluded) for every hull it seats. The board's "Kit missing" column is the step-0 work.

**How a missing game is made:** `/arcadegame` (the mode recipe), the prompts in `../MassAndSpaceGames/`, `/cardart`.
The vessel kit never changes for a mode (CLAUDE.md, LOCKED): a game is interesting through its track, environment,
intensity ladder and AI.

## 3. The steps

Each step has a start condition, the work, the exit (what "done" means), who signs it off, and what exists to do it.
A step is `built` when it exists but nobody has signed it off against its exit; most of the Time element is `built`
today because it was made before this loop. **Only `done` counts as through.** Garrett's playtest results win over any
model number (standing rule).

### Step 1: fun (the vessel's elemental abilities and its game, together, at one intensity)

- **Starts when** the vessel has a game for the element (step 0).
- **Work.** Pick the fun intensity (I2 unless the game says otherwise). Tune the vessel's abilities for that element
  and the game together until it is fun to fly. Score it on the six fun dimensions of
  [`../AI_SYSTEM/TIME_GAME_FUN_SCORES.md`](../AI_SYSTEM/TIME_GAME_FUN_SCORES.md) (Verb, Flow, Ladder, Feedback,
  Rivals, Fairness; Ladder is read in step 2) from the recorded flights (`VESSEL_MODE_CHART.md` §3), the mode doc and
  the model, and raise the lowest floor first. Show it (a still or a clip, `Docs/claude/VIDEOS_AND_PREVIEWS.md`), then
  hand a person a one-line playtest.
- **Ability changes are vessel changes.** They are made on the vessel, never through the mode (LOCKED). They go
  through without waiting: each one is queued for Garrett's `/approve` (and the vessel owner's), which never slows
  anything or anyone down (Garrett, 2026-11-12: "I will catch problems in vessel tunings eventually"). Use the Vessel Studio for the feel if the person wants it (one line,
  never required), and `/artifact-to-unity` to bring numbers in.
- **Exit (done). ONE human flew it and signs it off as fun** (Garrett, 2026-11-12). Only then is it flagged for
  intensity production (step 2). Score the six fun dimensions to find what to fix, but the sign-off is the bar. Record
  `set <Vessel> <Element> 1 done --by <who> --kind human --evidence "<playtest / report>"`. **The fun card records it
  without typing** (2026-11-12): when Play stops after an arcade match, the editor asks "was it fun?" (yes / almost /
  no, and what held it back), "was this rung right?" (too easy / right / too hard) and an optional line, and sends the
  answer with the Bug Hunt send; `python3 Tools/Build/fun_cards.py apply` turns a "yes" into this sign-off.
- **Tools:** `/vessel`, `/vessel-studio`, `/arcadegame`, `/element-ability-table`, `VESSEL_MODE_CHART.md`,
  `TIME_GAME_FUN_SCORES.md`, the fun card and `fun_cards.py`.

### Step 2: the intensity ladder (I1 intro, I2 fun, I3 challenging, I4 very hard)

- **Starts when** step 1 is done for that vessel and element.
- **Work.** Shape the four rungs so each one serves its goal and shows something the vessel can do that the rung
  below did not ask for. Each rung adds one demand, with no cliff. Variance: each rung is a family of fun environments
  (seeded courses, spawn profiles), not one map, so the AI in step 3 cannot memorise a track.
- **Exit (done), proposed bars, measured from recorded flights and the AI:** I1: a first-time pilot finishes. I2: the
  step-1 fun bar holds. I3: a skilled pilot finishes, the Medium AI often does not. I4: the best humans and the
  Legendary AI finish near the limit. **ONE human plays the rungs and signs them off as fun** (Garrett, 2026-11-12);
  only then is the vessel and element sent to AI training (step 3). The fun card signs it off when one person
  answered "fun: yes" and "right" on all four rungs (`fun_cards.py apply`). **The bars are checked from the recorded
  flights** by `python3 Tools/Build/rung_bars.py` (Garrett, 2026-11-12: defaults, then learned): each rung's least
  finish rate and median time band start from `rung_bars.json`'s defaults (I1 90%, I2 75%, I3 50%, I4 25% of human
  races that run to the end), a game may override them there, and once three runs of a rung were rated "right" on the
  card its bar is what those runs showed. A higher rung finished more often than the one below fails the ladder.
- **Tools:** `/arcadegame` (the per-mode intensity ladder: four `CellConfigDataSO`s), the End Game Conditions window
  (`/EndGameConditions`), `/ecology` for spawn profiles, `rung_bars.py`, the fun card's rung answer.

### Step 3: the vessel's AI for the element (one genome, all four rungs)

- **Starts when** step 2 is done. (AI already trained on a `built` ladder is kept as a seed; it is re-run once the
  ladder is signed off.)
- **Work.** [`AI_PLAN.md`](../AI_SYSTEM/AI_PLAN.md) rules 1-7, unchanged: one universal genome per vessel per
  element, scored on I1-I4 at once (mean plus half the worst), grown from every genome that exists, never trained
  inside an arena game. Racing AIs never detour for crystals (Yash's rule). Every commit says `Plan step: <id>`.
  While Froglet Compute is paused, searches run in cloud sessions, never on someone's own PC.
- **Exit (done).** The genome passes the four-part "well trained" test (`REGATTA_BALANCE_PLAN.md`), is written by the
  area's writer, and an editor check confirms it within ~10% of the model (a line in
  `Docs/UNITY_VERIFICATION_CHECKLIST.md`). This is the step that already runs with the least human involvement.
- **The result** is three or four AIs per vessel (Time, Charge, and Mass and/or Space). Voyage squads fly them, chosen
  by the seat's vessel and element and biased by its pilot (`AI_PLAN.md` §1, step `pilot-bias`); arena games use them.
- **Tools:** `/vessel-ai`, `/compute-job` (the search shape), `/regatta-balance`, `Tools/Build/ai_learn.py` (people's
  races, automatic).

### Step 4: the environment creator (per arena game)

- **Starts when** every vessel the arena seats is through step 3 for its element (for Brood Rush: Mass and Space).
  Before that, a search here balances against AIs that have not converged, which the AI plan forbids.
- **Work.** Build a generator of arena environments (course, features, prisms, crystals, flora) and train it against
  the fixed pool of every seated vessel's step-3 AIs. Its objective: **balanced** (no vessel systematically ahead;
  Time: each vessel's mean result within ~1.10x; Charge: equal petal plateaus; Brood Rush: equal net control of space)
  and **interesting** (no vessel best everywhere, environments that differ, the fun bar of step 1). It makes a new
  random balanced environment for every match, which is the step past a fixed pool.
- **Plateau** = the objective stops improving over a set budget of generations (the job's convergence rule).
- **Today:** Regatta's course-pool search is the first version (`REGATTA_BALANCE_PLAN.md`, AI-plan step
  `time-arena-course`: best pool 1.104x against the 1.10x bar in the model). Broadside and Brood Rush have none.
- **Exit (done).** Plateaued, and a person playtests a handful of generated environments and agrees they are fair and
  fun.

### Step 5: vessel imbalance, then back to steps 2-4

- **Starts when** step 4 has plateaued.
- **Work.** Whatever imbalance the environment creator cannot cancel belongs to the vessels. Read it per vessel (which
  vessel is ahead or behind, in which kinds of environment), propose the smallest tuning of the vessel and its
  elements (each vessel's elemental baseline, `AI_PLAN.md` step `*-arena-elements`), and get it approved. Every tuned
  vessel's affected element goes back to `redo` for steps 2-4.
- **For Brood Rush**, the leftover is read on **net control of space**, so a vessel may be a poor builder and a strong
  destroyer, or the reverse. Its cultivator/destroyer position is reported from the matches (mass made and held against
  rival mass destroyed), never tuned toward a target.
- **Exit.** The plateau is inside the arena's band: **no step 5 is needed**, and the arena step is done until a new
  vessel enters.
- **Approval.** Vessel tuning goes through without waiting and is queued for Garrett's `/approve` (and the vessel
  owner's): he catches problems after the fact, and nothing waits on him (Garrett, 2026-11-12; this replaces the
  Regatta plan's "vessel tuning needs Garrett's yes").

## 4. Running the loop

1. **Order.** `next` ranks by element (Time, then Charge, then Mass and Space), then vessels the arena seats before
   those it does not, then the earliest step. Each vessel and element moves one step at a time. Kit gaps (step 0) are
   listed after; they are the work for anyone who would rather build a game.
2. **Claim.** A vessel is a swim lane, and **every vessel is its own parallel lane through the loop**: `/swimlanes`
   leads with them (Garrett, 2026-11-12: it "should be advancing each vessel as a high priority parallel lane through
   this process"), each with its progress and next step, so several people and sessions move different vessels at
   once. An occupied vessel lane is someone else's; take the next one. Only a lane collision waits (CLAUDE.md).
3. **Catch it early.** A stakeholder who finds a change against the spirit of a vessel they worked on sends it back
   through the loop, and everything built on the change is redone (a retraining at step 3, a new plateau at step 4).
   `/approve` therefore ranks each person's queue by **loop impact**, the work such a catch would send back now
   (`elemental_loop.rework`), which grows while the entry waits: the costly catches are answered before more is built
   on them. File a vessel change with `approvals.py add ... --vessel <V> [--element E] [--loop-step 1|2|3]`.
4. **Record.** Every step's outcome goes into `loop.json` through `elemental_loop.py set ... --evidence` with the doc,
   PR or playtest that proves it, then `--write` the board. `elemental_loop.py --check` is the drift gate.
5. **Less human involvement each loop.** Every `done` records `kind` (human or auto) and its loop number; the board's
   section 5 is the share. The aim, loop by loop: step 3 and step 4 fully automatic (they nearly are), step 2's rung
   bars checked from recorded flights, step 1 scored by a fun model calibrated on the people's sign-offs, and step 5
   proposed automatically with only the approval left to a person.
6. **New vessels** enter at step 0 and step 1 and run the whole loop. When one reaches step 4, the arena re-plateaus
   with it in the pool, and step 5 may tune vessels other than the new one. Those rebalances are batched on a schedule
   so the meta changes before it goes stale and not more often (decision 3).

## 5. What the loop pays for

| Way to play | What it gets |
|---|---|
| Toybox | vessels whose elemental abilities were tuned for fun, element by element |
| Arcade (and the Maelstrom) | three or more polished games per vessel, each with a real I1-I4 ladder |
| Arena | Regatta, Broadside and Brood Rush balanced against trained AIs, with random balanced environments every match |
| Voyages | three or four AIs per vessel; a squad seat's vessel, element, pilot (and later artifact) pick and bias them |

## 6. How this maps onto what exists

| Loop step | Existing plan or tool | AI-plan step |
|---|---|---|
| 0 kit | `../MassAndSpaceGames/PLAN.md`, `/arcadegame` | - |
| 1 fun | `../AI_SYSTEM/TIME_GAME_FUN_SCORES.md` (estimates only, 2026-10-27) | - |
| 2 ladder | each mode's doc, `/arcadegame`, `/EndGameConditions` | - |
| 3 AI | `AI_PLAN.md` rules, `MINIGAME_TRAINING_PLAN.md`, `AUTO_LEARNING.md`, the searches | `time-arcade`, `charge-arcade`, `mass-arcade`, `space-arcade` |
| 4 environment creator | `REGATTA_BALANCE_PLAN.md` (course pool), `regatta_course.py`, `regatta_features.py` | `time-arena-course`, `charge-arena`, `massspace-arena` |
| 5 imbalance | the elemental baselines | `time-arena-elements` (and its Charge and Brood Rush twins) |

## 7. Missing scaffolding (ranked)

1. ~~No recorded fun sign-off exists for any vessel and element.~~ **Built 2026-11-12, not yet answered in the
   editor:** the one-minute fun card (`Assets/_Scripts/Editor/Diagnostics/FunCard.cs`, after every arcade match;
   FrogletTools > Diagnostics > Fun Card) and `Tools/Build/fun_cards.py` (reads the answers from every bugledger
   branch, `apply` records the step 1 and 2 sign-offs). No sign-off exists until someone answers one.
2. ~~No per-rung bars per game.~~ **Built 2026-11-12:** `rung_bars.json` (default bars, per-game overrides) and
   `Tools/Build/rung_bars.py` (checks every arcade game's I1-I4 from the human flights on the bugledger branches,
   learns a rung's bar from three "right" fun cards). First run: of the games with three or more finished-to-the-end
   races on a rung, Switchback I1, Serpent Time I1 and I3, Slingshot I2 and I3, Thresher Time I1 and I2 and Skim Race I1
   and I2 fail their finish-rate bar, and most rungs have too few flights for a verdict. In score games (Grizzly
   Charge, Scarab Scramble) "finished" may not mean a finish; give them a game override or let the card learn them.
3. **Charge AIs need the per-attacker petal ledger** (`SILENCE.md` §6) before `charge-arcade` can be scored.
4. **No Mass or Space AI model or learner exists for any game**, and Brood Rush has no net-control-of-space measure and
   no cultivator/destroyer readout. Both are needed before Brood Rush's step 4.
5. **Environment creators for Broadside and Brood Rush do not exist**; Regatta's is a fixed pool searched in a model,
   not a per-match generator.
6. **Kit gaps:** the board's "Kit missing" column (14 vessels on 2026-11-12, most of them missing a Charge game).
7. **Froglet Compute is paused** (Shombith, 2026-11-06), so steps 3 and 4 run by hand in cloud sessions.

## 8. Rules this loop keeps (from elsewhere, unchanged)

- A game mode never changes the vessel; ability tuning is done on the vessel (CLAUDE.md, LOCKED).
- PvP is petals only (LOCKED). Every colour comes from the colour set (LOCKED).
- No AI is trained inside an arena game; nothing is balanced against an AI that has not converged (`AI_PLAN.md`).
- Racing AIs never go for crystals (Yash). Garrett's playtests win over the model.
- Compute in the cloud, never on Garrett's PC.

## 9. Decisions for Garrett

1. ~~The step-1 fun bar~~ **Decided (Garrett, 2026-11-12):** one human flies it and signs it off as fun before it is
   flagged for intensity production; the intensities also need one human's fun sign-off before AI training.
2. ~~Vessel tuning~~ **Decided (Garrett, 2026-11-12):** vessel tunings (step 1 and step 5) go through and are queued for
   his `/approve`; they never wait on him.
3. **The meta schedule.** How often step-5 rebalances ship (proposed: batched, at most once a month).
4. **Mass and Space AIs.** One genome per element (the AI plan's rule, proposed) that Brood Rush flies situationally,
   or one coupled genome for Brood Rush.
5. **Vessels with no arena seat** (Angler, Brittle Star, Goby, Grizzly, Honeyguide, Jacks, Meerkat, Orca, Shrike,
   Starling, Thresher, Tiger Moth, Wrasse): they still run steps 0-3 for the Toybox, the arcade and voyages; which arena games seat them is yours.

## 10. Log

| Date | What moved |
|---|---|
| 2026-11-12 | **Garrett**: the loop dominates `/swimlanes` and `/approve`. The swim lanes board now opens with every unoccupied vessel as its own loop lane (progress and next step, `elemental_loop.vessel_lanes`); the approval queues rank entries by loop impact, the rework a late "against the vessel's spirit" catch would cause (`elemental_loop.rework`) |
| 2026-11-12 | **Garrett** decided the fun card: it opens in Unity right after Play, after every arcade match (the newest answer per vessel, game and rung wins), and the per-rung bars are defaults, then learned from the card. Built: `FunCard.cs`, the Bug Hunt kind `fun`, `fun_cards.py`, `rung_bars.py`, `rung_bars.json` (scaffolding 1 and 2) |
| 2026-11-12 | **Garrett** decided 1 and 2: one human's fun sign-off gates step 1 to step 2 and step 2 to step 3 (`elemental_loop.py` refuses a step 1 or 2 `done` that is not `--kind human`, and a step `done` before the one below it); vessel tunings go through and queue for his `/approve` without waiting |
| 2026-11-12 | Garrett set the loop as the top priority. This playbook, `loop.json`, `elemental_loop.py` (board, `next`, `set`, `--check`, `--self-test`) and the `/elemental-loop` skill were added. Loop 1 started with the Time element furthest along: steps 1-2 built for every vessel with a Time game, step 3 built (trained, model only) for seven hulls and in progress for the Manta and Urchin, Regatta's step 4 in progress. The AI plan's `mass-arena` and `space-arena` became one step, `massspace-arena` (Brood Rush) |
