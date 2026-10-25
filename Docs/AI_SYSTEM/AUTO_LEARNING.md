# The AIs learn on their own (LOCKED rule, Yash, 2026-10-26)

> "I don't wanna click any commands or anything. Never. This is a rule. ... every time I'm playing a new game, the
> record should just come here and then you evaluate what type of data the new game has ... and then relearn its AI
> navigation in order to win the race."

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
| It reaches the person's editor | the person pulls `Ys-bleeding-edge` as usual | - |

`ai_learn.py` remembers what it has learned from (`Docs/AI_SYSTEM/ai_learning_seen.json`), so an hour with no new
race writes nothing. A learner writes a hull's numbers only once it has three finished races of that hull in that mode
(its own rule), so one race only counts toward the three.

## 2. What the sweep does (the scheduled task's whole instruction)

1. `git fetch origin`, check out `Ys-bleeding-edge`, merge `origin/Ys-bleeding-edge` (merge, never rebase).
2. `python3 Tools/Build/ai_learn.py`. **0 new flights: stop.** No commit, no message.
3. `python3 Tools/Build/ai_learn.py --run`, then read `Docs/Reports/AI_LEARNING.md`.
4. For each mode with new flights, judge whether the AI races well enough to win against the person:
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
- **A mode another collaborator owns the AI of** goes in `ai_learn.HOLD` with the reason, so it is reported and never
  written, until they say so. Today: Warpline (the Stoat's AI waits for Garrett's configuration, issue 1198).

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
| A learner for a hull that has none (Sparrow, Scarab, Serpent, Butterfly, ...) | **no** | built once per hull by a session (§3) |
