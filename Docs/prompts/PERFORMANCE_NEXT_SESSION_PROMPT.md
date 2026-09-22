# Prompt — re-plan the performance work, then measure it the simple way

Paste everything below into a fresh session.

---

You are continuing the performance effort on **Cosmic Shore**, on branch
**`claude/bold-fermi-54nlts`** (PR #899, https://github.com/froglet-studio/Cosmic-Shore/pull/899 —
pushing to the branch updates it; do not open another PR). You cannot run Unity. The human runs
the editor and sends you results. **Plan first, then tests.** Do not ask the human to measure
anything until steps 1–3 are done and the plan is written down.

## Read first, in this order

1. `Docs/PERFORMANCE_OPTIMIZATION.md` — the whole thing. It is short on purpose: state, history
   since day 1, the ranked lever list (§3.3), the hypotheses already **measured dead** (§3.5 —
   do not redo any of them), and the measuring method (§4).
2. `CLAUDE.md` → "Ecosystem Design Principles" (any fix touching flora, fauna or cells goes
   through the `/ecology` skill) and "Anti-Patterns to Avoid".
3. Only when a claim needs its evidence: `Docs/archive/PERFORMANCE_LOG_2026.md` (frozen; do not
   append to it).

## Why this session exists

The last session spent a week on A/B tests whose arms were not comparable. The boot world grew
between arms (24,243 → 49,116 prisms), the editor window lost focus, and the human transcribed
screenshots by hand. Meanwhile bleeding-edge **replaced the boot world** (Garland; Lattice is now
opt-in), so the thing being measured is no longer what players see first. This session fixes the
plan and the method before measuring anything.

## Step 1 — merge bleeding-edge and get it compiling

The branch forked from `1f160508f` (2026-09-14). bleeding-edge is ~1,390 files ahead.

1. `git fetch origin bleeding-edge` then `git merge origin/bleeding-edge` (a merge commit, never
   a rebase — the branch is published).
2. A dry run predicted conflicts in **`AGENTS.md`** and
   **`Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs`**, and possibly
   `CLAUDE.md`. For the docs, keep both sides. For `LifeForm.cs`, keep bleeding-edge's logic and
   re-apply this branch's change on top: the coroutine wait caching from commit `7efa64a6b`
   (`perf(ecology): stop the two forever-coroutines allocating every tick`) and the comment
   repoint to `Docs/archive/PERFORMANCE_LOG_2026.md`.
3. Run the repo's six out-of-editor gates (`Tools/Build/check_*.py`) and a Roslyn syntax parse of
   every file the merge touched on both sides. Then ask the human for **one** editor compile
   before anything else. Report the result plainly.
4. Push (`git push -u origin claude/bold-fermi-54nlts`).

## Step 2 — rewrite the plan in `Docs/PERFORMANCE_OPTIMIZATION.md` §3

On the merged tree, not from memory:

1. **Confirm the boot world.** Read `Cell.ResolveBootIndex` and which `CellConfigDataSO` sets
   `BootDefault`. Record what Menu_Main boots into and its authored size.
2. **Re-check the levers.** For every row of §3.3, confirm the code it names still exists as
   described on the merged tree (bleeding-edge changed `Spindle.cs`, `PrismRenderService.cs`,
   `Flora.cs`). Mark each row still-true / changed / gone.
3. **Propose the target and the scenario set** (§3.1). The draft is: 60 fps in a Development
   build on the reference PC; scenarios S1 Garland menu, S2 Lattice via Cell Selector at 8 min,
   S3 Rampage intensity 1, S4 Scurry intensity 4, S5 Wildlife Liberation. Adjust only with a
   reason written down.
4. Commit the revised plan as its own docs commit. **Then show the human the plan in five lines
   and ask them to confirm the target and the scenario list.** This is a real decision for them.
   Use the AskUserQuestion tool.

## Step 3 — build the two small tools that make measurement simple

Both are DiagnosticsHUD console commands, under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, following
the existing pattern (`RendererHideSwitch` in `RendererCensus.cs`, the `diag` command in
`DiagnosticsHUD.cs`). Read `Docs/CONDITIONAL_COMPILATION.md` before writing either.

1. **`freeze on|off`** holds a growing world still so two arms can share their state. It pauses
   flora growth and reproduction and fauna spawning for every Cell. It is **production gating
   only**: it creates nothing, removes nothing and changes no timer that would later "catch up".
   Run it through the `/ecology` skill protocol and state the collider-budget impact (none). Find
   the existing seam before adding one. Frenzy already freezes planting and growth, so look at
   how `Cell` gates that.
2. **`ab "<command A>" "<command B>" [seconds] [rounds]`** runs arm A, arm B, A, B … for `rounds`,
   records each arm for `seconds` after a settle delay, and prints **one line**:
   `ab: CPU busy B−A = −3.8 ms ±0.4 · GPU … · draws … · GC/f … (3 rounds, frozen: yes|no)`.
   It also writes both arms into one `diag`-style JSON. It warns loudly when the prism entity
   count or enabled-renderer count drifts more than 5% between arms (the exact failure the last
   A/B had), and when the frame looks capped or throttled (reuse `FrameBoundness`).
   Put the statistics (mean, spread, delta) in a pure static class and cover it with edit-mode
   tests under an `Editor/` folder, including a negative control.

Compile-check both in a stub harness (see the `asset-surgery` skill §4; dotnet is installable)
and run the six gates. Commit, push. Update `BENCHMARK_TOOL.md` and §4.5–§4.6 of the perf doc.

## Step 4 — hand the human ONE measurement pass

Only after the human confirms the plan. Give them a single numbered list, no alternatives, one
block per scenario:

1. Pre-flight, once: Burst on, Deep Profile off, `fps uncap`, click inside the Game view.
2. For each scenario: load it, wait the stated time, then
   (a) a Profiler **Hierarchy** screenshot expanded to the leaves under `PlayerLoop`,
   (b) the same frame sorted by **GC Alloc**,
   (c) a **Timeline** screenshot of the render block showing which worker jobs run during the
       main thread's `Idle`,
   (d) `diag <scenario> 15`, pasting the `.txt`, not a screenshot.
3. For S2 only: `freeze on`, then `ab "renderers hide Spindle" "renderers show" 10 3`, and paste
   the one line.

Then stop and wait for the results.

## Step 5 — when results arrive

For each scenario, record the top rows and the `diag` averages in §1 of the perf doc with the
commit SHA. Re-rank §3.3 by measured size in the scenarios that **miss the target**. Recommend
exactly one lever to build next, with its expected saving and how the `ab` command will prove it.
Do not start building it without the human's go-ahead.

## Rules that are easy to break here

- **Do not redo a §3.5 hypothesis.** Prism draw calls, the prototype re-key, chunk locality,
  opaque material conversions and render-path GC are all measured dead.
- **A live HUD row is one frame.** Quote `diag` or `ab` averages only.
- **A measurement describes one tree.** Stamp every number with the commit SHA.
- **Nothing may pop in or out** (continuity law). A fix that hides spindles must fade them. It
  may not toggle them.
- **Mass is conserved.** No culling, no timers, no caps that drop work. Pacing and pausing
  production are allowed.
- Every UGS/Netcode `await` uses `.AsMainThread()`. New tests go under an `Editor/` folder.
- Commit with conventional messages. Push only to `claude/bold-fermi-54nlts`.
- **Talk to the human in short numbered steps with one clear action each.** They asked for that
  explicitly. No menus of options unless a decision is genuinely theirs.
