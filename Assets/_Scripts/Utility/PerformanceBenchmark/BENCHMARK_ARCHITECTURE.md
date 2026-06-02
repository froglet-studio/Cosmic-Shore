# Performance Benchmark Tool — Tabs (plain Notion version)

No Mermaid — just text + ASCII so it pastes into Notion cleanly. Each tab below has:
**what it is · what data it captures · a small picture.**

---

## The whole tool in one picture

```
                ┌──────────────── Benchmark Window ────────────────┐
                │ Collect │ Runtime Capture │ Sweep │ History │ Compare │
                └───┬────────────┬────────────┬─────────┬─────────┬─────┘
                    │            │            │         │         │
              one fixed     free-play,     many       saved     A vs B
              run of a      live spike     scenes /    runs      diff
              scene         breakdown      a data set  list
                    │            │            │
                    └────────► Runner (measures during Play) ◄─────┘
                                     │
                   per-frame data  +  spikes (script breakdown)
                                     │
                            Report (JSON)  ──►  saved to History
```

> **Runner** = the one thing that actually measures while the game runs.
> Every tab is just a different way to *start* it or *read* its output.

---

## Tab 1 — Collect

**What:** one controlled run of a single scene — warm up a few seconds, then
measure for a fixed window. Produces a score, hints, and the worst spikes.

**Good for:** a repeatable yardstick. Same scene, same length, every time → you can
Compare runs and catch regressions.

**Honest note:** for *exploring* "why did it just hitch?" this is clunky — fixed
window, you don't pick the moment. That's what **Runtime Capture** is for.
→ Suggestion: keep Collect as the *fixed benchmark*, and use Runtime Capture day-to-day.

**Captures:** fps · frame time (avg/p95/p99) · CPU/GPU split · draws/tris · memory/GC ·
netcode · gameplay load (prisms/VFX/vessels) · top spikes.

```
pick scene + config ─► Start (enters Play) ─► warmup ─► sample N s ─► Report + Score + Hints
```

---

## Tab 2 — Runtime Capture   ⭐ (the one you liked)

**What:** Start/Stop while you **freely play**. Low overhead. Every frame that spikes
is broken down into the **script methods** that caused it (editor noise filtered out).
Shows live on screen, and a **"Copy for Claude"** button dumps a clean text block —
so no more screenshots.

**Good for:** "the game just hitched — what was it?" You play, it watches, it names the scripts.

**Captures per frame:** frame time · CPU main-thread · physics · GC · draws/tris.
**Captures per spike:** frame # · frame ms · top script self-times
(e.g. `ObjectiveIndicator.LateUpdate()  17 ms`).

```
Start ─► play freely ─► [spike?] ─► grab script breakdown ─► live list
                                                   │
                                   Stop ─► save .json  +  Copy-for-Claude text
```

---

## Tab 3 — Sweep   (reworked: a data set, two modes)

**Idea:** build a *data set* of the game's performance, not just one scene.

**Manual mode (build now):** start the sweep, then **play the game yourself**. It records
in the background with **minimal FPS disturbance**, captures **errors / exceptions**, and
lets you **mark moments** ("boss fight", "this felt bad"). Real-play data + annotated
problem spots + error log — the creative one.

**Automatic mode (future):** the tool loads each scene in turn, runs a scripted capture,
moves to the next — hands-off regression sweep. *(parked for later)*

```
Manual:    Start ─► you play ─► [ errors + your marks + frames ] ─► data set
Automatic: for each scene ─► auto-load ─► capture ─► next ─► combined report   (future)
```

**Captures (manual):** everything Runtime Capture does + a per-session **error list**
(message + when) + your **marks** (label + timestamp).

---

## Tab 4 — History

Every saved run, newest first. **Tag** them ("GDC_demo", "after-trail-fix"), reopen, or
send one to Compare. Backed by JSON files on disk.

```
[ run 12  HexRace   12.5 fps   tag: baseline ]
[ run 11  Menu      58 fps     tag: trail-cap ]   ← click → Open / Baseline / Current
```

---

## Tab 5 — Compare

Pick a **baseline** and a **current** run → side-by-side diff of the key numbers
(fps, p99, draws, GC, spikes). Green = better, red = worse. This is how you *prove* a fix worked.

```
                 baseline      current     Δ
   avg fps        12.5          24.0     +11.5  ▲
   p99 ms        168           70        -98    ▲
   GC KB/f       100           40        -60    ▲
```

---

## Where the data lives (all plain text — Claude can read it)

| Source | File path |
| --- | --- |
| Collect / Sweep runs | `persistentDataPath/Benchmarks/*.json` |
| Runtime Capture (CSV logger) | `persistentDataPath/ProfilerCaptures/*.csv  + _summary.txt` |
| Dev-build self-capture | `persistentDataPath/PerfRuns/*.json` |

On Mac (editor): `~/Library/Application Support/<company>/<product>/…`

---

## Proposed tab line-up after the rework

```
Collect  →  keep (fixed benchmark / regressions)
Runtime  →  NEW  (free-play live spike breakdown + Copy-for-Claude)   ⭐
Sweep    →  rework  (manual data set now · automatic later)
History  →  keep
Compare  →  keep
```
