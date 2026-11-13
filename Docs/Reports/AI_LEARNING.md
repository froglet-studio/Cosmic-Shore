# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-13 22:19 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**23 new flight(s)** since the last learn.

## BroodRush

New flights: Swordfish 3.

- **Swordfish: measured, not written** - no measure for RampBoostAIPolicySO yet (its policy RhinoAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Manta | I1 | 3 | 0 | - | 1 of 1 | 2026-11-02 16:18 |
| Rhino | I1 | 1 | 0 | - | 1 of 1 | 2026-11-02 16:21 |
| Swordfish | I2 | 4 | 0 | - | 0 of 1 | 2026-11-12 21:58 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## DogFight

New flights: Sparrow 1.

- **Sparrow: measured, not written** - SparrowAIBoostPolicy is switched off in DogFight (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 5 | 0 | - | - | 2026-11-10 23:08 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## GrizzlyTime

New flights: Grizzly 2.

- **Grizzly: measured, not written** - its AI bombs through GrizzlyTriggerBombConfigSO's autopilot fields, which do not load per mode, and jtgjones's grizzly-v3 is reworking the bomb triggers (15 unmerged commits): learn it once that lands.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Grizzly | I1 | 1 | 0 | - | - | 2026-10-26 06:27 |
| Grizzly | I4 | 9 | 5 | 106 s | 5 of 7 | 2026-11-12 21:52 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
grizzly       11      9    4.6    51       2%     2%    2/11   0: 17, 1: 13, 2: 11, 6: 7
```

## Headlong

New flights: Rhino 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Rhino | I1 | 1 | 1 | 85 s | 1 of 1 | 2026-11-12 22:52 |
| Rhino | I4 | 1 | 1 | 146 s | 1 of 1 | 2026-11-12 22:56 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
rhino          4      6    4.0   294      10%     1%    0/4    0: 4, 2: 3, 3: 3, 1: 2
```

## Jackstraw

New flights: Thresher 8.

- **Thresher: measured, not written** - its controller steers the AI (JackstrawHunter, at its rival: SetExternalTargetProvider), not at the crystal the learner measures against, and the mode's AI is Shombith's: reported, never written.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Thresher | I1 | 11 | 0 | - | - | 2026-11-12 21:57 |
| Thresher | I3 | 1 | 0 | - | - | 2026-11-10 05:27 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Kelpwood

New flights: Angler 2.

- **Angler: measured, not written** - no measure for AnglerLeapfrogAIPolicySO yet (its policy AnglerAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Angler | I4 | 3 | 0 | - | 1 of 1 | 2026-11-12 21:53 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## ScarabRally

New flights: Scarab 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Scarab | I1 | 3 | 1 | 130 s | 1 of 1 | 2026-11-12 21:54 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
scarab         3     11    7.2   220       0%     0%    0/3    3: 3, 5: 3, 8: 3, 11: 1
```

## Waystation

New flights: TigerMoth 3.

**No learner reads these yet** (the sweep's to-do): TigerMoth.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 2 | 1 | 205 s | - | 2026-10-21 15:32 |
| TigerMoth | I1 | 1 | 1 | 170 s | 1 of 1 | 2026-11-01 17:45 |
| TigerMoth | I4 | 7 | 2 | 143 s | 2 of 4 | 2026-11-12 19:23 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
tigermoth     10     22    6.3    45      10%     1%    2/10   0: 10, 1: 9, 7: 8, 2: 7
butterfly      2      0    0.0    50      24%     3%    0/2    0: 2
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Headlong.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_ScarabRally.md`

## What the sweep judged (2026-11-13 22:17 UTC)

- **Headlong (Rhino), 2 new races (owner, 2026-11-12, on `bleeding-edge` a25dd24ff1):** the person won both; the AI
  Rhinos stalled badly.

  | Race | I | Rivals | Person | AI rings (of 40) | Longest gap at one ring |
  |---|---:|---|---|---|---|
  | 22:52 | 1 | Easy | 40 in 85.1 s | 12 / 9 | 23 s / 30 s |
  | 22:56 | 4 | Legendary | 40 in 145.9 s | 4 / 4 (1.8 a minute) | 64 s / 56 s |

  Headlong's Rhinos fly the AI plan's promoted trained drive (`RhinoAIBoostPolicy_Headlong`, lead-on B, `c39317a6b6`;
  the model raced it at 81-102 s a race, `Docs/Reports/TRAINED_DRIVES_PROMOTED.md`) with the Time-mode nose lead
  (`4ee1ac567a`). The build's commit a25dd24ff1 is not on origin (a local commit), so whether it carried both cannot be
  checked; bleeding-edge has had them since 2026-11-08. Numbers from races cannot fix this and the drive is the AI plan's
  (Garrett's): **not touched, under "Waiting on"**. Nothing written (2 finished races, below 3).
- **Scarab Rally (Scarab), 2 new races:** both left before the end; nothing written.
- **Jackstraw (8, Thresher):** report only (its hunter steers the AI). Reported only: Brood Rush (Swordfish 3),
  Dog Fight (Sparrow 1), Grizzly Time (2), Kelpwood (Angler 2). **Waystation (Tiger Moth 3):** no Tiger Moth learner.
- No AI numbers changed; the promoted trained drives are untouched (`promote_trained_drives.py --check` OK).

## Waiting on

- **Headlong's Rhino AI (Garrett, the AI plan's promoted drive):** at Legendary I4 it threaded 4 of 40 rings in 146 s
  in the real game, stalling 56-64 s at one ring, against 81-102 s a race in the model. Diagnose in the real game
  (`/vessel-ai`) before the next round grows from this genome.
- **Hood and Jackstraw's AIs (Shombith):** report only while their controllers steer the AI.
- **Trained drives against races (Garrett, the AI plan):** when a promoted variant should learn from people again.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation, Dustup) is still to build.
