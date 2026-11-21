# AI training data in UGS: a brief for whoever wires it (Shombith first)

**Who to ask:** Yash (the recorder, the AI training tools) or Garrett (AI training owner, the trainer). This page is
what they want you to know before any AI data goes into UGS. Nothing here is decided for you. Where the data lives is
the team's open decision (task T-20261015-0064, `AI_TRAINING_PLAN.md` §5.2), and Shombith owns the UGS layer.

## 1. What the data is

Every race a person flies by hand in the Unity editor is recorded by `HumanFlightRecorder`
(`Assets/_Scripts/Controller/AI/SkimRace/HumanFlightRecorder.cs`): one `flight_<Mode>_<Hull>_<time>.json` per race,
schema `cosmic-shore.flight/3`.

**Ten times a second it records:**
- position, heading, speed, velocity and turn rates;
- every stick, the throttle and both triggers;
- the boost, drift, slowed and attached state, boost charge and the four element levels;
- the camera the pilot looked through;
- every rival's position, velocity and rings;
- the 12 nearest crystals.

**Once per race it records:**
- every ring and every rail, the course seed and intensity;
- rail prisms destroyed or restored;
- the pilot's button presses;
- the rival difficulty.

**Why the team keeps all of it.** The AIs learn from these files at full detail:
- the racing line, averaged per leg from clean laps (`Tools/Build/racing_line.py`);
- skim choices and reaction lag (`situation_features.py`);
- the Squirrel's numbers (`learn_from_flights.py`);
- the vessel × mode record (`vessel_mode_chart.py`).

A line accurate to a few units, or a reaction lag of 0.05 s, needs every sample.

## 2. The rule Yash asked for: keep the data whole

Yash, 2026-10-17: *"in UGS make sure we don't zip or compress the data too much, because these are very useful data
needed for very good precision AI mechanics; keep the data as much as possible unless it's getting too big."*

| Allowed | Not without asking Yash or Garrett |
|---|---|
| **Lossless** compression (gzip, brotli, zstd). The file comes back byte for byte, so no precision is lost. A 0.5 MB flight should shrink several times. | **Lossy** reduction of any kind: fewer decimals than the recorder writes (2, i.e. 0.01 u), a lower sample rate than 10 Hz, dropped columns or rival / crystal arrays, re-encoding floats to smaller types |
| Storing the summary (below) separately so lists are cheap | Keeping only summaries and discarding the full flight |
| Splitting a large flight into chunks | Trimming the start, the end, or "boring" stretches |

**If a flight is ever too big for a store**, chunk it or move it to a file store. Do not thin it out. Ask before
anything lossy.

## 3. Sizes today (measured 2026-10-17)

- **Per flight:** about 0.5 MB of JSON (0.3-1.2 MB depending on race length and the number of rivals).
- **Stored so far:** 67 flights from 4 people, about 33 MB in all.
- **Where they are today:** on each person's `bugledger/<user>` git branch, stored with Unity's "Store AI training
  record?".
- **The capture plan:** about 100 races per person (`RACING_LINE.md` §4), so about 50 MB each before compression.

## 4. What the tools need, and a suggested shape

**Small and listable**, a few hundred bytes per race. Cloud Save fits, the way the match-replay work keeps its
`REPLAY_INDEX` (`claude/match-replays-5t3dbr`):
- `file`, `mode`, `hull`, `intensity`, `date`;
- `finished`, `raceSeconds`, `rings`, `raceTarget`;
- `rivalHulls`, `rivalDifficulty`;
- `schema`, `build`.

`vessel_mode_chart.py` needs only these.

**The full flight**, the whole file unchanged and losslessly compressed at most, in a file store. Options:
- Cloud Save files, if our version has them;
- the R2 bucket the toybox publishes to;
- our own endpoint.

`racing_line.py`, `situation_features.py` and `learn_from_flights.py` read the full file. Each needs only a `.gz`
reader added if the files arrive compressed.

**Players are different from the team:**
- Players' flights need their own opt-in first ("Would you like to train the AIs with your own moves?",
  `AI_TRAINING_PLAN.md` L3 step 2).
- Players' data must never go into git.
- Today's recorder runs in the editor and development builds only.

**Follow `Docs/Analytics/DATA_ARCHITECTURE.md` §2** for Cloud Save naming: SCREAMING_SNAKE_CASE keys, a
`SchemaVersion`, epoch-ms timestamps.

## 5. How this page reaches you

- **A project hook** (`.claude/hooks/ugs_ai_data_nudge.py`) shows a one-line notice the first time a session's prompt
  is about UGS (Cloud Save, Cloud Code, Remote Config, Economy, Leaderboards, Analytics and so on), and points Claude
  here. If the person is Shombith, by their git identity in `Docs/TEAM.json`, the notice names him.
- **`UGSKeys.cs`** and **`DATA_ARCHITECTURE.md`** carry a one-line pointer to this page.
