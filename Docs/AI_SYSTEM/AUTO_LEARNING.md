# The AIs learn on their own (LOCKED rule, Yash, 2026-10-26)

> "I don't wanna click any commands or anything. Never. This is a rule. ... every time I'm playing a new game, the
> record should just come here and then you evaluate what type of data the new game has ... and then relearn its AI
> navigation in order to win the race."

**Where the AIs learn (Yash, 2026-10-27):** in each vessel's OWN minigames, its Time minigame first (Warpline for the
Stoat, Switchback for the Dolphin), never in Regatta. The arena games, Regatta among them, are rebuilt later from the
skills learned there (`MINIGAME_TRAINING_PLAN.md`), so Regatta races are reported and never learned from (`HOLD`, §3).

**No person runs a learner, a report or a test command for AI learning, and no session asks them to.** A person plays;
everything after that happens by itself. A session that would otherwise end with "run `learn_...py`" runs it itself,
or leaves it to the sweep below.

## 1. The loop

| Step | Who does it | Where |
|---|---|---|
| A person plays any mode in the Unity editor | the person | - |
| The race is recorded | `HumanFlightRecorder` (every mode, every match) | `flight_<Mode>_<Vessel>_<time>.json` |
| It is sent, with no question | `BugHuntAutoSend` after Play | `origin/bugledger/<user>`, `BugLedger/shared/reports/<user>/flights/` |
| New flights are found and learned from | the **AI learning sweep** (§2), a Claude scheduled task, every hour | `Tools/Build/ai_learn.py --run` |
| The AI's numbers change for that mode only | the learners (`<asset>_<Mode>` variants) | `Assets/Resources/AIBoostPolicies/`, `Assets/Resources/RaceLines/`, `SkimRaceAIConfig_Regatta` |
| What changed, and whether the AI now races like the person | the sweep | `Docs/Reports/AI_LEARNING.md` |
| It reaches the person's editor | `Ys-bleeding-edge` is merged into `bleeding-edge` (a pull request), and the person pulls it. Yash's editor ran `bleeding-edge` on 2026-10-26 (his load reports' `gitBranch`), so a sweep change reaches his races only after that merge | - |

`ai_learn.py` remembers what it has learned from (`Docs/AI_SYSTEM/ai_learning_seen.json`), so an hour with no new
race writes nothing. Only a race a learner actually read is remembered as learned: one in a mode on hold or of a hull
no learner covers waits in the file's `waiting` list with its reason and is learned from once the reason goes away (the
hold lifts, a vessel gains a learner), and one whose learner failed is tried again next hour. A learner writes a hull's numbers only once it has three finished races of that hull in that mode
(its own rule), so one race only counts toward the three.

## 2. What the sweep does (the scheduled task's whole instruction)

The scheduled task "AI learning sweep" (`trig_01VieYjD52nneXYEXHM8nUKY`, every hour at :20 UTC, created 2026-10-25 on
Yash's request) wakes the session "AI learning sweep (hourly)" (`session_01WGSY8UTqMrqdcy2tWPMkBD`). Change what the
sweep does HERE, not in the task: the task says only "do section 2 of AUTO_LEARNING.md".


1. `git fetch origin`, check out `Ys-bleeding-edge`, merge `origin/Ys-bleeding-edge` (merge, never rebase).
2. `python3 Tools/Build/ai_learn.py`. **0 new flights: stop.** No commit, no message.
3. `python3 Tools/Build/ai_learn.py --run`, then read `Docs/Reports/AI_LEARNING.md`.
4. For each mode with new flights, judge whether the AI races well enough to win against the person. **A race counts
   only if the editor that flew it HAD the AI's change**, not just if it came later: the play session's load report
   (`BugLedger/shared/reports/<user>/load/load_<scene>_<time>_*.json`, the nearest one before the race) names its
   `gitBranch` and `gitCommitHash`; `git merge-base --is-ancestor <the AI's commit> <that hash>`. A race flown on a
   build without the change measures the OLD AI (2026-10-26: Breakwater's new boost bands were 70 minutes old on
   `Ys-bleeding-edge` and absent from Yash's `bleeding-edge` build).
   The rival report's circling counts only time near the ring while NOT closing on it (a slow hull on long legs spends
   seconds inside 250 u on a straight approach), and "boosted" needs the boosting or discharging bit as well:
   - It finishes, its rings per minute are near the person's, and it barely circles or stalls: the learned numbers
     are the whole job.
   - It circles, stalls, threads few rings or is far slower: numbers alone will not fix that. Diagnose it with the
     `/vessel-ai` skill (`DIAGNOSIS_PLAYBOOK.md`) and fix it in the AI layer on `Ys-bleeding-edge`, following CLAUDE.md:
     `who_works_where.py query` first; an "Affects:" line; never re-tune another collaborator's work (write it in the
     report under "Waiting on" instead); the reference compile and the offline harnesses before committing; never
     claim a Unity run, add a `Docs/UNITY_VERIFICATION_CHECKLIST.md` entry.
   - A vessel or mode under "No learner reads these yet": note it in the report under "To build". A learner for a new
     hull is built in a normal session (§3), not by the sweep.
5. Commit the learned files, `Docs/Reports/AI_LEARNING.md` and `Docs/AI_SYSTEM/ai_learning_seen.json` to
   `Ys-bleeding-edge` (`chore(ai-learn): <modes> from <n> new races`), and push (fetch, merge, push; retry on a race).
   Never push to `bleeding-edge`, never open a pull request.

## 3. What a session owes the loop

- **A new learner joins `ai_learn.py`** (`plan` and `run_learners`), with a case in its `--self-test`. A learner
  nobody's sweep runs is a learner that never runs.
- **A new vessel or racing mode** is read by the sweep from its first recorded race (it shows under "No learner reads
  these yet" until a learner covers it).
- **A mode another collaborator owns the AI of, or one not trained now,** goes in `ai_learn.HOLD` with the reason, so
  it is reported and never written, until they say so. Today: **Regatta** (the AIs train in their own minigames; Regatta
  is rebuilt from those skills later, Yash 2026-10-27). Warpline, held for the Stoat's configuration since 2026-10-17,
  was lifted by Yash on 2026-10-26.

## 4. What is automatic, and what is not yet

| | Automatic | How |
|---|---|---|
| Recording every race | yes | `HumanFlightRecorder` |
| Sending races and console errors | yes | `BugHuntAutoSend` |
| Fixing the console errors people hit | yes, daily | the "Bug Hunt daily sweep" scheduled task (`/bug-ledger` §6) |
| Relearning the AI from new races | yes, hourly | this document |
| Judging and fixing an AI that cannot race | yes, in the same sweep | §2 step 4 |
| Offline checks (gates, generators, harnesses) | in every sweep that changes code | §2 step 4 |
| Unity EditMode / PlayMode tests | **no** | no Unity runner is attached to CI (`unity-ci.yml`, since 2026-10-13); they need an editor |
| Learning a NEW vessel | yes, when its prefab names a boost policy of a known class | `learn_drives_from_flights.py` reads every vessel's prefab (§5) |

## 5. Which learner covers which vessel (2026-10-26)

`learn_drives_from_flights.py` no longer lists its hulls by hand. For a hull it does not special-case, it reads the
vessel's prefab: the boost policy its AIPilot names, that policy's class, and the ability each input is bound to. The
class says which recorded signal is the verb and which of the policy's fields the verb writes (`POLICY_CLASSES`). A race
measures against the next ring; a mode without rings against the nearest crystal, or the nearest rival in a duel
(`RIVAL_TARGET_MODES`). A match without rings counts once it ran to its end. An existing variant is the start of a
write, so a field set there by hand survives (`SparrowAIBoostPolicy_Breakwater`'s rocket).

| Vessel | Learner | The verb | What it writes |
|---|---|---|---|
| Dolphin | drives (ChargeBoost) | discharge (state bit) | when to discharge, at what charge; in Switchback also one variant per intensity (`_I<n>`, `PER_INTENSITY_MODES`, `MINIGAME_TRAINING_PLAN.md` §3.6) |
| Rhino | drives (RampBoost) | ramp (state bit) | ramp start / stop, corner speed |
| Manta | drives (Soar) | both triggers | the two Soar bands (needs real Soaring: mean ≥ 0.05) |
| Stoat | drives (AIModeTuning) | its pair | its three configs (`<config>_Warpline`; `_Regatta` from before 2026-10-27); in Warpline never `autopilotLetGoNear` (set from the model, 250 u: `WARPLINE.md` §3.2) |
| Sparrow | drives (HoldBoost) | boost (state bit); the rocket (a SkyBurst press, 2026-10-26) | start / stop bands, shortest hold and rest; the rocket's range and aim (`rocketMinRange` / `rocketMaxRange`: p10 / p90 of the presses' distance to the ring, `rocketAimDegrees`: their median nose angle); in Breakwater never a boost release under 1.25 s (`FLOORS`, the danger weave) |
| Serpent | drives (PelletBoost) | a ConsumeBoost press | start bands, seconds between pellets |
| Scarab | drives (SnapDash) | throttle double-tap at TIME 5 | start bands, dash cooldown |
| Squirrel | drives (SkimRing) outside Regatta and Skim Race; skim line in Regatta (writes) and Skim Race (report only) | a boost-ring press / the skim | ring bands / skim line |
| Every hull in Regatta | race lines (**on hold since 2026-10-27**: reported only) | where it crosses the rings | nothing until Regatta is rebuilt |
| Urchin | report only | rail ride | its riding lives in each mode's scene fields |
| Grizzly | report only | - | its bomb autopilot does not load per mode, and `grizzly-v3` (jtgjones) is reworking the bombs |
| Butterfly | report only | - | its Mass / Dust choice is each mode's controller code; no Dust bit is recorded |
| Wrasse | report only | the crew's saved boost | `KiteNavigator.AIPilotWantsBoost` (`KiteNavigatorConfig.aiBoostRingDistance`), which does not load per mode. Its prefab named `SparrowAIBoostPolicy`, inert on a Wrasse: cleared 2026-10-26 |
| Thresher | drives (ThresherSling, 2026-10-26) | a Plant press | the corner it plants at, the room it needs, the let-go cone, the longest swing, seconds between slings |

A policy switched off in a mode (`disabledInModes`) is reported there, never written: the Sparrow in Dog Fight, the
Squirrel in Joust and Skim Race, the Scarab in Scarab Scramble.

**Skim Race is report only on purpose.** Its per-intensity policies are simulation-tuned to win their tracks
(`author_skimrace_ai_config.py`, which gave Regatta its own copy so Skim Race's calibrated numbers never move). Since
2026-10-26 the recorder copies Skim Race's track into each flight, so the Squirrel's skim is measured there beside the
policy each intensity flies (`LEARNED_FROM_FLIGHTS_SkimRace.md`), as evidence for that tuner.

## 6. Every flight says who won (2026-10-26)

Yash: "record each match's result". A match that ends by its own rules is stored once the mode has published its
ranked results (`GameDataSO.Results`, waited for up to 8 s), with a `result` block: the winning domain and name, the
pilot's domain, `youWon`, and every ranked row (rank, name, domain, score, the mode's score text, is this the pilot).
A match left early writes `"result": null`. So a mode with no finish line (Grizzly Charge, Bloomrush, Dog Fight) can be
judged: `learn_drives_from_flights.py` learns a mode without rings from the matches the person WON once there are three,
and `Docs/Reports/AI_LEARNING.md` shows "You won N of M" per hull and intensity.

## 7. A drive per intensity: which races teach it (2026-10-26)

In a `PER_INTENSITY_MODES` mode (Switchback) each intensity of a policy hull learns its own `<policy>_<mode>_I<n>`
(`MINIGAME_TRAINING_PLAN.md` §3.6). Two rules decide which races teach what (§3.7):
- **Only the intensity's current course.** A flight records its rings; the newest flight's mouth radius decides the
  course, and races on a retired one are left out (`on_current_course`). A variant already written stays until the new
  course has three finished races of its own.
- **The mode-wide variant learns from every intensity**: the fastest race of each first (`fastest_each_intensity`),
  because it is what the intensities without their own fly, and the three fastest overall are always the easiest
  intensity's. Every other mode learns exactly as before.

## 8. Every flight says who flew it; the AI never learns from the AI (Shombith, 2026-10-29)

The AI trainer (an AI Training run, Froglet Compute's Unity runs) switches a hull's AIPilot off and writes the sticks
itself, so the recorder took a trainer match for a person's: Compute's first Unity run (Switchback I2 on Shombith's PC,
2026-10-28) stored seven of them as hand-flown and sent them. They changed no learned number (all unfinished, and the
Switchback learner takes finished Blast Run races only), but they could have. Now:
- `PilotControl.IsHeldByAI` counts the trainer (`IsHeldByTrainer`): its samples carry `held = 1`, and a match the
  trainer flew alone is never stored (a verbose line on the AITraining channel, no warning).
- Every record says who flew it: `"pilot": "human"`, or `"mixed"` when the AI held the hull for part of it.
- `learn_from_flights.not_a_person` is the one gate every learner reads through (`flights_on_branches`,
  `flights_in_dir`): a record whose `pilot` is anything else, and the seven named records from before the field, are
  left out. `ai_learn.py` lists them as waiting, "not a person's flight", so the board says why.
- The Third Eye refuses its You / AI toggle and a take-over on a trainer-held hull (the AIPilot would fight the
  trainer every frame), reads the trainer's aim (`ThirdEyePilots.Read`: its target, range, threats, genome), and has
  `ThirdEyeWindow.WatchPilot(match)` and `SaveView(path)` (the view with trails and aim lines drawn in) for tools;
  Compute's watched runs save a still every minute and when the AI improves (`Docs/Compute/README.md` section 16).
Changing the learners' code makes the next sweep learn every flight again (`learners_fingerprint`); that is by design.

