# AI System — Diagnosis Playbook (one AI at a time)

How every AI on `Ys-bleeding-edge` (formerly the `ai-system` branch) gets diagnosed, reviewed and tested, and how a new AI from bleeding-edge is
taken in. The roster of AIs is `ARCHITECTURE.md` §2; the branch mechanics are `BRANCH_WORKFLOW.md`.

The principle: **an AI is diagnosed from the cheapest evidence up**, and every claim about it ("it is
slow", "it is dumb at X", "it got better") names the tier its evidence came from. Tiers 1–3 run in a cloud
session with no editor; tier 4 is the user's editor; tier 5 is the user's hands.

---

## 1. The five tiers of evidence

| Tier | What | Where it runs | Answers |
|---|---|---|---|
| **1. Static gates** | `check_ai_no_state_writes.py --check` (input-only), `check_mathf_params_alloc.py`, `scan_perframe_allocations.py`, console-logging and conditional-compilation gates, the AI's own generator `--check` | Cloud | Does it obey the rules? |
| **2. Compile against real Unity references** | `Tools/Build/unity_refcompile/run.sh` (player and `--config editor`) | Cloud | Does it build? (Not "does it work".) |
| **3. Offline behaviour** | The AI's pure-C# tests run headless; for the Skim Race pilot, the simulator `Tools/Build/skimrace_sim_harness/run.sh` (it compiles the shipped driver: `eval`, `trace`, `tune`, `handicap`, `dts=` for frame times, `SKIMRACE_RUNTIME=mono` for editor-like cost) | Cloud | Does its decision math do what we think, and how long does it take? |
| **4. Editor profiling** | Unity Profiler markers + the in-game console (`~`): `prof` (ranked self time / GC per marker, Burst-vs-managed jobs), `diag <label> <s> m=<markers>` (per-marker avg/p50/p95/max + run conditions), `ab "<A>" "<B>"` (paired A/B), `burst` (is Burst on), `fps uncap`, `freeze on` (hold ecology so the AI's cost stands alone). Benchmark window: FrogletTools ▸ Performance. Skim Race also has FrogletTools ▸ AI ▸ Skim Race AI Benchmark and the manual race recorder (`BenchmarkResults/SkimRaceAI/manual_*.jsonl`) | User's editor | What does it cost in the real game, at what frame rate? |
| **5. Playtest** | The user plays the mode against the AI with the script in §4 and fills the result row | User | Is it fun, fair, and as hard as the difficulty says? |

Rules that came out of the Skim Race work and apply to every AI:
- **Measure in Release** (editor Code Optimization = Release) for cost; Debug inflates managed code ~5×.
  Record which one a number came from (`diag` does it for you).
- **Frame rate is a gameplay input.** Contacts run on the 0.04 s fixed step and trails are laid at most
  once per frame, so an AI tuned at one frame rate can be worse at another. Test at least 30 and 60+ fps
  (`fps uncap`, or a target frame rate) and say which.
- **A behaviour change is A/B'd on the same seeds; a "same behaviour" change is proven identical.**
- **Every marker gets a name in `AI.<Name>.*`** (Skim Race keeps `SkimRace.*`) so one `diag m=` line
  covers it.

## 2. Diagnosing one AI — the procedure

Pick the next AI from the status board (§5). For it:

1. **Read** its row in `ARCHITECTURE.md` §2, its doc, and its code end to end. Write down what it
   decides, from what inputs, how often, and which buttons it presses.
2. **Tier 1–2.** Run the gates and the compile. Fix only what the AI owns.
3. **Instrument** (if it has no markers): a `ProfilerMarker` around its per-frame decision and around any
   search / loop / physics query inside it. Provably no behaviour change; commit alone.
4. **Tier 3.** Run its tests. If its decision math has no test, write one for the part a playtest would
   argue about (Urchin: which rail; Scarab: when to dash; Grizzly: when to fire and detonate). For a
   race-level question (finish times, difficulty ladder) build a simulator only if a test cannot answer
   it — the Skim Race simulator took days, and earned it.
5. **Hand the user a tier-4 + tier-5 script** (§4) for that AI. Add its steps to
   `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
6. **Record**: findings, numbers with their tier and conditions, and the decision in the AI's own doc
   (per-AI section "Diagnosis log"), its status-board row here, and the roster row.
7. **Only then restructure or tune**, as separate commits (`ARCHITECTURE.md` §4; `/refactor` method).

## 3. Intake checklist — a new AI arrived from bleeding-edge

Run after the merge that brought it (`BRANCH_WORKFLOW.md` §4). Intake never changes behaviour.

- [ ] Roster row added (`ARCHITECTURE.md` §2): layer, code, doc, tests, markers, harness, status "Intake".
- [ ] `check_ai_no_state_writes.py --check` passes **with the new files in its scope**. Its `SCOPES` list
      holds only `Controller/AI/SkimRace` and `Utility/AITraining` (2026-10-08), so add the AI's folder or
      files, run it, and review every finding (a reviewed exception goes in the gate's exception list with
      its reason, as the five existing ones do).
- [ ] Per-frame allocation and logging gates pass on its files.
- [ ] Its existing tests compile in the editor config and are listed in the roster.
- [ ] Markers added (step 2.3) — separate commit.
- [ ] A tier-4/5 script for the user (§4), filed in the checklist.
- [ ] Status-board row (§5) with the next action.

## 4. The user's test script (template — one per AI)

Copy into `Docs/UNITY_VERIFICATION_CHECKLIST.md`, filled in:

```
### AI: <name> - <mode(s)> (<date>)
Setup: <mode card>, intensity <n>, difficulty <if offered>, <k> AI, Code Optimization = Release, fps uncapped.
Profile (tier 4):
  1. In the match, open the console and run:  diag <ai-name> 30 m=<AI markers>
  2. Then:  prof <ai-name> 180 root=<top marker>
  3. Expect: <marker> avg < <x> ms, p95 < <y> ms for <k> AI.  Paste the .txt lines into the result row.
Play (tier 5), 3 rounds:
  - Does the AI <objective behaviour 1>?  Y/N, note
  - Does it ever <known failure: orbit / stall / wall-hug / spam an ability>?  Y/N, when
  - Who won, by how much; did it feel <too easy / right / too hard>?
Result row: date | build | fps | diag numbers | pass/fail per line | notes
```

QA results also flow through `/qa-backlog` (`Docs/QA/QA_BACKLOG.md`), which already carries
`QA-AI-SKIMRACE` (PASS 2026-10-08).

## 5. Status board (update on every diagnosis)

Order chosen by reach × risk: the autopilot first (every AI seat in ~30 modes, unmeasured), then the new
intakes, then the restructures.

| # | AI | Tiers done | Next action | Owner of next action |
|---|---|---|---|---|
| 1 | Platform autopilot (`AIPilot`, boost policies) | 2 (not in the input-only gate) | Widen the input-only gate to it; markers (`AI.Autopilot.*`, `AI.Boost.*`); then a `diag` in a 4-AI arena mode | AI session, then user |
| 2 | Urchin rail rider (Hijack, Skein, **Regatta new**) | 2, tests exist (not gated) | Gate scope; markers; review the Regatta rail choice against `UrchinRailAssessmentTests`; user script for Regatta | AI session, then user |
| 3 | Scarab jukes (Scarab Scramble, **new**) | 2, tests exist (not gated) | Gate scope; markers; review `ScarabScrambleJukePlanner`; move it to `Controller/AI/Scarab/` (`ARCHITECTURE.md` §4.3) | AI session |
| 4 | Grizzly AI bombs (**new**) | 2, tests exist (not gated) | Gate scope; markers; review the autopilot branch of `GrizzlyTriggerBombExecutor`; user script in Grizzly Time | AI session, then user |
| 5 | Butterfly mode switch | 2 (not gated) | Gate scope; markers; a test for the mode-switch rule | AI session |
| 6 | Manta turn boost | 2 (not gated) | Gate scope; markers; review | AI session |
| 7 | Skim Race pilot | 1–5 (QA PASS) | Split the core, then Burst the rollouts (`ARCHITECTURE.md` §4.5); Hard vs 2 humans decision still open | AI session, then user |
| 8 | Genetic training stack | 1, 2 | User verdict: keep / editor-only / retire | User |
