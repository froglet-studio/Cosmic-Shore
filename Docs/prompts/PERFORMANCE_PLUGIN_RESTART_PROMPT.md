# Prompt: restart the performance review with the unity-perf plugin

Paste everything below the line into a NEW session on branch `perf/performance-optimization`.

---

You are continuing the performance effort on **Cosmic Shore** (Unity 6000.3, URP, Burst 1.8.29), on branch
**`perf/performance-optimization`**. Push only to that branch (fetch and MERGE, never rebase, when the remote
moved; another session also pushes there). Do not open a PR. You cannot run Unity: the human runs the editor
and sends `diag` / `prof` JSON and screenshots. Give the human short numbered steps, one action each.

## Why this is a new session

The previous session could not use the **unity-perf** plugin (skills `/unity-performance`, `/csharp-zero-gc`,
`/code-standards`) or **Unity's official plugin**. Neither was enabled on the account, and plugins load only
when a session starts. The human switched to this session to run the performance review with those plugins.

## Step 0: confirm the plugins (before anything else)

1. Call `ListPlugins` with keyword `unity`, and check your skill list for `unity-performance`,
   `csharp-zero-gc` and `code-standards`.
2. Tell the human in one line which are active.
3. If unity-perf is NOT active, render its install card (`SearchPlugins` -> `SuggestPluginInstall`; id
   `plugin_017a77bCxxukLBBYTKt4yMgf`; Unity's is `plugin_014AH5hoCLbfMtN3eRfR7BYD`), and stop there.

## Read first

1. `Docs/claude/PLUGINS_AND_CONNECTORS.md`: the plugin rule (the repo's measured rules win over a plugin's
   general advice; a finding is a hypothesis until a Release `diag` / `prof` measures it), and "The
   performance restart", which holds the order and the starting numbers.
2. `Docs/PERFORMANCE_OPTIMIZATION.md`: the record. §3.5 lists hypotheses already measured dead; §4.7 is the
   test protocol. The newest rows of the dated log (2026-10-07 and 10-08) are the recent work.
3. `Docs/SKIM_RACE_AI.md` §8.0i-§8.0k: the AI seat stagger, and the Burst root cause and fix.
4. `CLAUDE.md` and `Docs/claude/ANTI_PATTERNS.md`, `Docs/claude/SHADERS_AND_PERFORMANCE.md`.

## What this branch has done (so you do not redo it)

- **Instruments:** console commands `diag` (per-system marker timings, p50/p95, run environment, JSON),
  `prof` (the Profiler hierarchy as JSON; flags Burst jobs that ran as managed code), `freeze` / `ab`
  (same-state A/B), `renderers`, and `burst` (Burst's switches, a probe job that reports BURST or MANAGED, and
  Burst's own refusal reason read from Editor.log).
- **Skim Race AI:** allocation-free steering, float planner loops, cheaper obstacle gather, pooled skim beams,
  prism pool prewarm; seats no longer re-plan in the same frame (`SkimRaceReplanGate`, spike frames halved,
  confirmed in the editor).
- **Burst root cause, fixed 2026-10-08, NOT yet confirmed in Unity:** `SubstrateAgentJob` and `SwarmPoseJob`
  called `System.MathF.Sqrt/Sin/Cos/Acos/Exp/Pow`, InternalCalls Burst cannot link. Every Assembly-CSharp job
  shares one Burst library, so EVERY game job ran as managed code. Both kernels now use `(float)System.Math`
  helpers; two text gates forbid MathF externs.
- **Merged** the latest `Ys-bleeding-edge` (which contains the latest `bleeding-edge`) on 2026-10-08 and
  re-checked everything: gates, harnesses, unity_refcompile, and this effort's edit-mode suites run with no
  Editor via `Tools/Build/prisma_edit_mode_tests` (175/175).
- **Open:** the port's live compile has 9 upstream engine gaps (not ours); Rampage i1 `cells` JSON; Dog Fight
  fuze check. Do not build lever L8 without the human's go-ahead.

## Starting numbers (Skim Race I2, 2 AI, editor in Release, 2026-10-08, jobs still managed)

65.8 fps; frame avg 15.2 ms, p95 19.9, p99 22.9; CPU busy 13.1 ms, GPU 3.7 ms; GC 30 KB/frame;
`SkimRace.Pilot.Decide` avg 2.35 / p95 4.83 ms; `TrackMpc` avg 1.45 / p95 3.64; `ShellContact.Query` 0.39 ms
(expected ~0.1 under Burst). `scan_perframe_allocations.py --gc-kb 30`: 4 large allocations in
Update/LateUpdate, 8 in per-frame coroutines, 62 `yield return new` inside loops.

## The task

1. Ask the human for the Burst test first, if not already sent: Skim Race in Release, F7, `burst` (expect
   `Run() BURST, Schedule() BURST`), then `diag` and `prof` JSON. Use them as the baseline.
2. Run **`/unity-performance`** on the hot paths with that baseline. Report the ranked findings (by measured
   ms), record them in `Docs/PERFORMANCE_OPTIMIZATION.md`, and stop for the human.
3. Then **`/csharp-zero-gc`** against the 30 KB/frame and the scanner list. Same: report, record, stop.
4. Then **`/code-standards`** over `git diff origin/bleeding-edge...HEAD`, reconciled with CLAUDE.md's Code
   Style and ANTI_PATTERNS. Where a plugin and this repo disagree, the repo wins; note the disagreement in
   `Docs/claude/PLUGINS_AND_CONNECTORS.md`.
5. Change code only where a Release capture can measure the gain. Every C# commit states that `/verify-unity`
   was unavailable (if it was) and files the change in `Docs/UNITY_VERIFICATION_CHECKLIST.md`. No model names
   in commits. Tests go under an `Editor/` folder.
