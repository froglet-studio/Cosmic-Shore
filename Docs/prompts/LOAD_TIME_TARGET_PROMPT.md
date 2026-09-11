# Prompt — publish a load-time target and a harness that measures every mode

Paste everything below into a fresh session.

---

Checklist item **D2** is *"Load time, every game mode at every intensity. Measure cold boot to menu
and menu to first playable frame across the full mode and intensity matrix, then reduce it. One
calendar week, with a published load-time target set alongside the frame and memory targets."*

**The target does not exist.** Nothing in the repository states what a good cold boot is, so there
is nothing to measure against and nothing for Definition of Done #5 to test. That target is D2's
actual deliverable, and it is the half that can be settled from the repository.

The matrix has also grown. D2 was sized at 8.0 person-days when the game had roughly seven modes.
It now has **19 launchable modes × 4 intensities = 76 combinations**, and the heaviest of them are
new: Rampage intensity 1 is, by its own documentation, the heaviest cell in any arcade mode and is
explicitly **not profiled**.

Read `Docs/STEAM_RELEASE_TASKS.md` (item R7), `Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md`
§5 and `Docs/PERFORMANCE_OPTIMIZATION.md` §2–§3 first.

## What already exists — do not rebuild it

The Performance Benchmark tool has a **Load Time Insights** tab that already does the attribution
half of this task: it records `load_*.json` reports under
`{persistentDataPath}/Benchmarks/LoadInsights/` and renders a donut chart plus tables of what
actually consumed the load. The instrumentation seam is `LoadInsights.Measure`.

Its stated limitation is the thing to fix: *"Load Time Insights attribution is only as complete as
the `LoadInsights.Measure` placement."* So the job is **placement and coverage**, not a new tool.

`BenchmarkBuildAutoRunner` already exists for driving the tool from a build, which is the seam for
sweeping a 76-cell matrix without a human clicking through it.

## What to build

**1 · Publish the target.** Write it into `Docs/PERFORMANCE_OPTIMIZATION.md` beside the frame and
memory targets, as two separate numbers — they are different problems with different owners:

- **Cold boot → main menu.** Bootstrap, DI registration, auth, splash, scene load. Broadly constant
  across modes.
- **Menu → first playable frame.** Per mode, per intensity. This is where the arena build lives and
  where the spread is.

Propose real numbers rather than placeholders, and justify them from what the project already
knows. Useful anchors, all in-repo: the connecting panel's own hold is capped at **45 s** before it
releases loud (`Docs/CONNECTING_PANEL.md`), which is a stated upper bound on the worst case, not a
target; the watched-hold lay slice is **25 ms** with an **18 ms** creation budget, deliberately
slower than the unwatched **250 ms / 512-completions** tempo, which tells you the build is
*already* being traded against frame rate while the panel is up; and the heaviest authored
environment is Atlantis at ~69k prisms against the Lattice cell's ~42,840 grown.

Say plainly which modes you expect to fail the target. A target every mode already meets is not a
target.

**2 · Cover the matrix.** Audit `LoadInsights.Measure` placement against the phases the connecting
panel already distinguishes — it reads real counters and its own progress is documented as
monotonic-by-construction, so its phase boundaries are a ready-made taxonomy. Fill gaps so a report
attributes the whole interval rather than leaving an unexplained remainder.

**3 · Make the sweep runnable unattended.** 76 combinations is not a hand-click job. Extend
`BenchmarkBuildAutoRunner` (or document how to drive it) so one run produces one report per cell,
and write the result into a table a human can read top-down by worst cell.

## Constraints

- **This is measurement, not optimisation.** Do not change load behaviour in this branch. If you
  find something egregious, record it in `Docs/PERFORMANCE_OPTIMIZATION.md` §4 as a backlog entry
  with its root cause and leave it.
- **Verify Burst compilation is ON before trusting any number you take.** `Docs/PERFORMANCE_OPTIMIZATION.md`
  §0 records this as the first rule of every perf session: it was off on a developer machine for an
  entire investigation, ran every job as managed IL at roughly 20× cost, and invalidated four
  captures. The tell in the Hierarchy view is a job showing as `ExecuteJobFunction.Invoke()`.
- Do not re-measure into the doc's existing tables — its numbers were verified 2026-07-08 and are
  two months and ~15 modes stale. Add a dated section; do not silently overwrite history.
- The actual sweep needs a machine and, for the floor numbers, the GTX 1060-class box (item H8).
  Author the harness and the target; hand the run over.

## Definition of done

1. Two published targets — cold boot → menu, and menu → first playable — with the reasoning, in
   `Docs/PERFORMANCE_OPTIMIZATION.md`.
2. A named list of modes/intensities expected to fail, so the run has a hypothesis.
3. `LoadInsights.Measure` coverage gaps closed, or the remaining gap stated explicitly.
4. A documented way to sweep all 76 cells unattended and emit one comparable report per cell.
5. No change to load behaviour in the diff.
6. `Docs/STEAM_RELEASE_TASKS.md` R7 updated to note that the target is published and the
   measurement is handed to H8.
