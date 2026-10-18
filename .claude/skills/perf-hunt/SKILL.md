---
name: perf-hunt
description: The automated performance resolve loop, the twin of /bug-ledger - read what the daily performance sweep found (Tools/Build/perf_hunt.py: the static allocation gates, every collaborator's shared editor captures judged against their baselines, the Amoebius marker allocations), rank it by MEASURED cost, fix the top item the way the perf record's lever table and dead-list allow, prove it with a before/after measurement, and close it out everywhere. Use on "/perf-hunt", "run the performance sweep", "what regressed", "is performance worse than yesterday", "fix the top performance item", "the performance report", a pasted capture_*.json, or a scheduled run (once a day from bleeding-edge). Also the generic performance checklist every C# change in this repo is held to.
---

# Perf hunt — measure, rank, fix, prove, close out

**The one rule (CLAUDE.md § Debugging Methodology): profile first; a finding is a hypothesis until a
measurement says what it costs, and a fix is not a fix until a second measurement says what it saved.**
Everything here is built so a session never has to guess and never has to ask a person to transcribe a
number: the editor shares its captures by itself, the sweep judges them, and the record keeps the deltas.

## 0. What runs on its own (and where to read it)

| Runs | What | Reads |
|---|---|---|
| **Every day** (`.github/workflows/perf-daily.yml`, 05:40 UTC, `bleeding-edge`) | `perf_hunt.py sweep` (static gates, every shared capture vs baseline) and `perf_hunt.py amoebius` (the game's own markers in Amoebius, allocations vs baseline) | the workflow run's `perf-sweep` artifact; red = a regression. Its `publish` job then COMMITS `Docs/perf/status.json` + `Docs/Reports/PERFORMANCE.md` to bleeding-edge (or to `perf-status` if protection refuses), which the editor's Project Record reads |
| **After every play test** (editor, when the person turned on *Auto-publish* or pressed Share on the Bug Hunt & Performance badge) | `BugHuntReports` copies `capture_*.json` into `BugLedger/shared/reports/<user>/perf/` on their branch | `perf_hunt.py collect` (every origin branch) |
| **Any session** | `perf_hunt.py sweep --write --tasks` refreshes `Docs/Reports/PERFORMANCE.md` and `Docs/perf/status.json`, and files one team task per NEW regression | `Docs/Reports/PERFORMANCE.md`, `Docs/TEAM_TASKS.md` (area Performance) |
| **When someone asks** "performance status", "what next", "what to test" | `.claude/hooks/health_status.py` hands the session `Docs/perf/status.json` | `python3 Tools/Build/perf_hunt.py status [--refresh]` |

**Where people see it: three surfaces, no more** (Yash, 2026-10-15: no extra editor UI; 2026-10-17: the after-play
confirmation and its Project Record). In the editor, the Scene view's **Bug Hunt & Performance badge**
(`BugHuntPerfBadge`, Docs/DIAGNOSTICS.md): the counts, the first next step, the Send to Claude / Release buttons and,
while playing, the auto-capture countdown (`PerfAutoCapture`: no Capture button). After Play, `BugHuntAutoSend` sends
new errors, captures, flights and network logs by itself and shows a small **confirmation** (no question,
`BugHuntSendConfirmation`) that expands into the **Project Record**: each branch on the performance path
(`branchFlow` in `status.json`: Ys-bleeding-edge first, then bleeding-edge) read from its own `Docs/perf/status.json`,
plus the person's sends; also at FrogletTools > Diagnostics > Project Record. Everything else is answered in Claude from
the status file. Do not add another window, menu tool or dashboard for this system; extend the status file (the Project
Record and the badge read it) instead. **Every sweep or status run writes `status.json`; commit it on the branch you work
on, so the Project Record shows it.**

Baselines: `Docs/perf/baselines.json` (editor captures, keyed `scene|label`) and `Docs/perf/amoebius_baselines.json`
(per mode). The first VALID measurement of a key is adopted; `--adopt` replaces one after a confirmed improvement.
Thresholds (`THRESHOLDS` in the script): CPU busy +15 % and ≥ 0.5 ms, p95 +15 %, GC +25 % and ≥ 2 KB/frame, a diag
marker +30 % and ≥ 0.2 ms, an Amoebius allocation +10 % and ≥ 0.05 KB/frame; Amoebius milliseconds are only WATCHED.

## 1. Start from the measurements, never from a guess

```
git fetch origin
python3 Tools/Build/perf_hunt.py sweep --write            # gates + every shared capture vs baseline
python3 Tools/Build/perf_hunt.py amoebius --no-build      # the engine's markers (needs a built Amoebius player)
python3 Tools/Build/perf_capture_report.py <capture.json> # one capture, judged in full
```

- **Validity first.** A capture taken in Debug code optimization, with Deep Profile, under a frame cap, with Burst
  off, with managed job time, or with a second camera enabled (the Third Eye window) is NOT a number. The reader says
  so in its first section; the sweep refuses to compare it. Ask for a clean one (the team task template is in
  `Docs/TEAM_TASKS.md`, area Performance) rather than reading a tainted one.
- **An Amoebius allocation is the game's only when the engine API does not copy.** Before filing a game task from the
  Amoebius run, read the marker's code: if its bytes come from an engine stand-in that copies on every call (today:
  `Mesh.SetVertices/SetUVs/SetTriangles(List)`, because the renderer keys on array identity), it belongs in
  `Docs/perf/amoebius_known.json` with the evidence and a task for the engine's owner (Shombith), not in a game fix.
  The sweep lists known entries as "engine shim" and never counts them.
- **No shared capture yet?** The person has not shared: play the scene in Release past its auto-capture time (the editor
  captures by itself; the badge counts down), then stop Play (it goes to Claude by itself; the file
  lands on their `bugledger/<user>` branch). Say that in one line. Never ask for pasted JSON when the pipe exists;
  accept a paste when they offer one.

## 2. Rank by measured cost, then pick with the record

1. Rank what the sweep shows by **ms per frame** (CPU busy, then the diag markers, then the Profiler's top self time)
   and by **KB per frame** (the Profiler's top allocators; the Amoebius allocating list, which carries over to Unity).
2. Read `Docs/PERFORMANCE_OPTIMIZATION.md` **§3.3** (the lever table: what each known cost is, its expected gain and
   its go/no-go measurement) and **§3.5** (measured DEAD: never redo those). A new finding not on the table gets a row
   there before it gets code.
3. Pick the top item whose go/no-go is met. One item per commit. State the number you expect to move and by how much.
4. Team rule: `python3 Tools/Build/who_works_where.py query <files>` first; a fix in another collaborator's area that
   is more than local (one function, a data repair) is a `team_tasks.py add` for its owner with the measurement attached,
   not a commit. Write `Affects: <person>'s <area>` in the commit otherwise.

## 3. Fix - the checklist every performance change in this repo is held to

Load `/unity-performance` for the method and `/csharp-zero-gc` for an allocation item; the repo's measured rules win
where they disagree (`Docs/claude/PLUGINS_AND_CONNECTORS.md`). Before writing code, go down this list:

- **Hot path named.** Every per-frame cost has a `ProfilerMarker` named `System.Part` so `diag`, `prof` and the Amoebius
  run can see it. If the thing you are fixing has none, add the marker in the same commit - that is how the "after" gets
  measured.
- **No per-frame allocation.** No `new`, LINQ, closures, `string` building, boxing, `params` (`Mathf.Max(a,b,c)` ->
  `MathfNoAlloc`), `GetComponent` in Update, `FindObjects*` in Update, `yield return new WaitFor*` inside a loop
  (cache it), `foreach` over an interface-typed collection. Gates: `check_mathf_params_alloc.py`,
  `scan_perframe_allocations.py`, both in `perf_hunt.py gates`.
- **Pool, don't instantiate.** Prisms, projectiles, beams and effects come from their pools (`PoolRefill` / `PoolActivate`
  markers show the cost); `Instantiate` in a per-frame path is a finding.
- **Burst jobs stay Burst.** No `System.MathF`, managed types, strings or `try` in a job (`check_burst_*.py`); the
  `burst` console command and `prof`'s managed-job rows prove it. A job that runs managed costs 10-20x.
- **Render: sharedMaterial + MaterialPropertyBlock**, never `renderer.material`; instanced path for anything numerous;
  transparent overdraw is the phone GPU's cost (spindles, snow), not the prism count (`Docs/PERFORMANCE_OPTIMIZATION.md` §3.6).
- **Physics: the collider budget** (ECOSYSTEM rule: ≤ ~1,500 active colliders per cell; `PrismColliderLodManager`); no
  `OverlapSphere` allocation forms; triggers only where a contact matters.
- **Work in the right phase and at the right rate.** Spread sweeps over frames (the collider-LOD sweep spikes 15.9 ms
  every 0.25 s at 69k prisms - lever L11); stagger re-plans (`SkimRaceReplanGate`); nothing per-frame that can be
  per-event; nothing logged per frame or per contact, ever.
- **Memory is a performance number on a phone.** A GameObject per thing (prisms: ~2-4 KB each) is the ceiling for
  100k; prefer data in arrays / entities for anything numerous.
- **Ecology, party, scoring, prism animation and elemental abilities are LOCKED systems**: read their doc and skill
  before touching them; a perf change there keeps every invariant (mass conservation, the clock-material law, ...).

## 4. Prove it - before and after, same state, same protocol

- Unity: a Release `capture <label> 15` before and after in the SAME scenario state (`freeze on` + `ab` for a same-state
  A/B, `Docs/PERFORMANCE_OPTIMIZATION.md` §4.5); paste both files to `perf_capture_report.py before.json after.json`.
  Noise is ±5-10 %; a smaller claimed win needs repeats.
- Amoebius: `perf_hunt.py amoebius --no-build` before and after; allocations repeat to ~1 %, so a KB/frame win there
  is real on day one. Milliseconds there only order things.
- The gates: `perf_hunt.py gates` green; `unity_refcompile --config player-dev` for the C#; `/verify-unity` when the
  CLI exists, else say so and file the check in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- Write the delta into `Docs/PERFORMANCE_OPTIMIZATION.md` §2 (the dated row: what, the before/after numbers, the
  protocol, the commit). A number without its protocol is not a result.

## 5. Close out everywhere

`/bug-closeout` for the records: the team task the sweep filed (`team_tasks.py done <id> --note "<before -> after>"`),
the perf record row, the lever table (§3.3: move the row to done or to §3.5 if it measured dead), and
`perf_hunt.py compare <after.json> --adopt` / `amoebius --adopt` so the baseline is the new normal. Commit the
baselines with the fix. A regression that was NOT this change's (the base moved) is still a row: say what moved it.

## 6. The scheduled run (once a day, from bleeding-edge)

The GitHub Action does the measuring; a scheduled Claude session does the resolving:

1. `git fetch origin` and `git checkout Ys-bleeding-edge` (the working branch); `python3 Tools/Build/perf_hunt.py
   sweep --write --tasks` (and `--amoebius --no-build` after `dotnet build Port/src/CosmicShore.Player -c Release`).
2. For each NEW regression: §2-§5 above, one commit each, on `Ys-bleeding-edge`, team rules applied.
3. Nothing regressed is a valid run: commit the refreshed `Docs/Reports/PERFORMANCE.md` anyway so the report shows the
   sweep is alive, and say in one line what was measured and that nothing moved.
4. Never adopt a WORSE baseline to make a run green. A regression nobody can fix today stays a task on the board.
