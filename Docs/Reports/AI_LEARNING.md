# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-26 02:22 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**4 new flight(s)** since the last learn.

## Breakwater

New flights: Sparrow 2.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 5 | 4 | 84 s | - | 2026-10-26 03:35 |
| Sparrow | I3 | 2 | 2 | 209 s | 2 of 2 | 2026-10-26 06:44 |
| Sparrow | I4 | 2 | 2 | 89 s | - | 2026-10-20 16:35 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
sparrow       13     16    6.0    37      41%     2%    0/13   0: 18, 1: 13, 2: 9, 5: 8
```

## GrizzlyTime

New flights: Grizzly 1.

- **Grizzly: measured, not written** - its AI bombs through GrizzlyTriggerBombConfigSO's autopilot fields, which do not load per mode, and jtgjones's grizzly-v3 is reworking the bomb triggers (15 unmerged commits): learn it once that lands.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Grizzly | I1 | 1 | 0 | - | - | 2026-10-26 06:27 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
grizzly        2     12    2.6    56       4%     0%    0/2    0: 3, 2: 3, 6: 1
```

## SkimRace

New flights: Squirrel 1.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 1 | 0 | - | - | 2026-10-19 10:27 |
| Squirrel | I2 | 6 | 0 | - | - | 2026-10-22 22:37 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      17      0    0.0   183       7%     1%    0/17   0: 17
```

## Files the learners changed

- `Assets/Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Breakwater.md`
- `Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md`

## What the sweep judged (2026-10-26 02:21 UTC)

**Which build flew the races.** Yash's editor runs `bleeding-edge` (his load reports: `gitBranch bleeding-edge`,
`gitCommitHash 5771096c45`, the #1560 merge, for every scene played 23:52-01:18 UTC). That build HAS the per-intensity
Dolphin drives (`b1325fa57`), so the 00:21 Switchback judgement stands. It does NOT have Breakwater's new boost bands
(`f8782e92f`), the rocket learning (`0bcb0525b`) or anything the sweep pushed after 23:47 UTC. `AUTO_LEARNING.md` §2
step 4 now judges a race by the build's commit, not its date.

**The rival report over-counted circling** for slow hulls: 250 u from the ring at 37 u/s is about 7 s of a straight
approach, which it counted as circling after 2 s. Fixed (`regatta_rival_report.py`: only while not closing on the
ring; "boosted" also needs the boosting bit, since the Sparrow's multiplier rests at x5). Re-measured: the Breakwater
Sparrows circle 15% of the race (was reported 41%), the Switchback Dolphins 1% (was 18%), so the 00:21 note that the
slow Dolphin "circled 4-19 s" was mostly this artifact: it was slow, not orbiting.

- **Breakwater (Sparrow), I3, 2 races:** measured the OLD bands (above). The AIs threaded every station in order at
  6.0 a minute (Yash 7.3) and boosted 10% of the race (18% of the distance, at 75 u/s against 42), which is exactly
  what `switchback_dolphin_sim.py --mode Breakwater` predicts for the old bands (10%, 129 s a lap); the new bands
  predict 41% and 81 s. Nothing to change until races are flown on a build with `f8782e92f`.
- **Grizzly Time, Skim Race:** report only (the Grizzly's bombs wait on `grizzly-v3`; Skim Race is tuned by
  `author_skimrace_ai_config.py`).

## Waiting on

- **The Sparrow's learned rocket range (Breakwater, Garrett's mode; the rocket learner is `0bcb0525b`'s).** This run
  learned `rocketMaxRange` 718 u from Yash's shots (p90). The rocket's own tooltip puts its reach at 229 u (SkyBurst,
  120 u/s for 3 s), so an AI may fire at a station it cannot reach and the rocket bursts short. Not changed here (not the
  sweep's to re-tune); a ceiling at the rocket's reach in the learner is the likely fix, for whoever owns it.
- Warpline: the Stoat's configuration (Garrett, issue 1198). Grizzly: jtgjones's `grizzly-v3`.
- **Reaching Yash's editor:** the sweep's changes since 23:47 UTC reach his races only once `Ys-bleeding-edge` is merged
  into `bleeding-edge`, which the sweep never does (no pull requests).
