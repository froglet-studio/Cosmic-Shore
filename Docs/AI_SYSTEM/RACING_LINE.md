# Learning the Squirrel's Regatta line from Yash's flights, and the data a human-like AI needs

Yash, 2026-10-16: *"take the average of the datas and understand how the path can be navigated. Sometimes even I make
mistakes, hit things, so you have to ignore those datas. Maybe I can give you 100 gameplays and you can figure out the
best way to create the AI... boosters, all the other abilities, the drifting... a very dynamic AI that feels like playing
with another human being."*

This page records three things: what the 14 races recorded so far teach, how the line is built, and the captures that
come next. The tools are `Tools/Build/racing_line.py` (the line), `situation_features.py` (situations, labels and
choices) and `learn_from_flights.py` (the Skim Race pilot's numbers). None of them changes the game. A learned value
reaches the AI only through a reviewed Unity port that is tested against today's AI in the editor
(`AI_TRAINING_PLAN.md` L4).

## 1. How the line is built (`racing_line.py`)

1. **Split each race into legs.** A leg runs from one ring to the next, and the start is a leg of its own. Regatta I1
   has 8 rings, so a 3-lap race has 24 legs plus the start.
2. **Drop every leg with a mistake.** That means a rail hit (the recorder's Slowed bit), a missed mouth, or the AI
   holding the hull. A dropped leg never touches the line.
3. **Keep the faster half of the clean legs on each leg.** A clean but slow leg (hesitating, lining up again) teaches
   caution rather than the line.
4. **Average the kept legs into one line.** Each kept leg is resampled at 32 even steps along its length, and the
   steps are averaged. The result for each leg is where to fly, how fast, and where boost, drift and skims happen.
   The **spread** is how far apart the kept runs were: a narrow spread means you have settled that leg, a wide one
   means you have not.
5. **Output:** `Docs/AI_SYSTEM/racing_lines/regatta_<hull>_I<n>.json` (schema `cosmic-shore.racing-line/1`).
6. **Trust threshold:** a leg's line is trusted from 5 clean legs (`MIN_CLEAN`).

## 2. What the 14 races teach (Squirrel, I1, against Medium Dolphin and Manta; measured 2026-10-16)

`python3 Tools/Build/racing_line.py --dir <the 14 flights>`: 315 legs, 222 of them clean.

| | Seconds |
|---|---:|
| **Ideal lap**: your fastest clean time on each leg, added up | **21.4** |
| Your best actual lap | 24.2 |
| A typical clean lap: the median clean time on each leg, added up | 28.4 |
| Your median actual lap (27 laps) | 34.3 |

The faster half of your clean legs holds about **200 u/s** on every leg. Your races average 112–150 u/s, so the time
goes in the mistakes and in the legs where you slow down, not in the line.

| Leg (to ring) | Runs | Clean | Best s | Median s | Runs with a hit | One mistake costs |
|---|---:|---:|---:|---:|---:|---:|
| start | 14 | 11 | 4.73 | 5.08 | 21% | 1.0 s |
| 0 | 27 | 18 | 2.84 | 3.73 | 33% | 2.1 s |
| **1** | 41 | 24 | 2.98 | 4.36 | **41%** | 1.9 s |
| 2 | 41 | 29 | 2.32 | 3.19 | 28% | 2.5 s |
| 3 | 41 | 29 | 2.55 | 3.48 | 28% | 3.5 s |
| 4 | 41 | 33 | 2.70 | 3.40 | 20% | **4.0 s** |
| **5** | 41 | 24 | 2.99 | 3.59 | **41%** | 3.0 s |
| 6 | 40 | 30 | 2.51 | 3.50 | 25% | 2.2 s |
| 7 | 29 | 24 | 2.52 | 3.16 | 17% | 1.4 s |

- **Mistakes cluster on two legs.** Legs 1 and 5 have a hit in 41% of runs. A hit costs 1–4 s on that leg.
- **All nine lines are trusted** (each has at least 5 clean legs). The kept runs sit 11–17 u apart at their widest,
  about one rail-prism length, so your line is consistent.
- **Your style:** you race the ring line and touch rails now and then; you rarely ride your own rail (within skim reach
  only 4–25% of the time). You barely drift (0–2% of a leg), and you never pressed boost. The "boost" share the line
  reports is the Squirrel's skim speed-up being on, not a button press.
- **Unknown until more captures (§4):** riding your own rail, drifting and abilities, because they are not in the data.

## 3. How the AI is built from this (proposed; nothing ported yet)

| Layer | Learned from | What it gives the AI |
|---|---|---|
| **Where**: the line | `racing_line.py`, the averaged faster-half line per leg | the path and the speed to hold on it |
| **When**: the choices | `situation_features.py`: skim taken or skipped, commit point, look-ahead, boost | the rules for each situation |
| **Feel**: like a person | the spread and the kept runs themselves | each race it flies one of your kept runs, offset within the spread, instead of the same average every lap |
| **Difficulty** | the hit share and mistake cost per leg | Easy and Medium make mistakes where YOU make them (legs 1 and 5) and lose about what you lose; Hard flies the kept line clean |

- **Input-only, as every AI here** (`/vessel-ai` §1): the line is a target the pilot steers for; it never moves the hull.
- **The Unity side** is the Squirrel's Skim Race pilot, which already flies Regatta. Porting is team task
  T-20261015-7fc3 (Garrett with Yash).
- **Before anything ships:** race the learned Squirrel against today's Squirrel in the editor, many seeds, and report
  the finish-time distribution (`/vessel-ai` §3.2).

## 4. The data we need next (the capture plan)

The recorder already records, ten times a second:
- position, heading, speed;
- every stick, the throttle and both triggers;
- boost, drift, slowed and attached state, boost charge and the element levels;
- the camera;
- every rival's position, velocity and rings;
- the 12 nearest crystals.

Once per race it records every ring, every rail and each rail prism destroyed or restored. **New (2026-10-16):**
- **Button presses** (`buttonEvents`): every ability and stick action pressed or released, with its time.
- **Labels** (`flightrec tag <word>` in the HUD console): every flight after the command carries `"tag"` until you clear
  it with `flightrec tag`.
- **The last ring is counted** (finished races read 24/24).
- **The hull and the race target are named when recording starts**, so leaving mid-race no longer writes hull "Unknown".

What to fly, in order:

| # | Tag | What to fly | How many | Teaches |
|---|---|---|---:|---|
| 1 | *(none)* | Your normal best racing, Squirrel, I1, to the finish | 20 more | a line from about 15+ kept runs per leg, and how your choices vary |
| 2 | `rail` | Deliberately ride your own rail and skim it as long as you can | 10 | rail riding: skim height, the commit point, the rail guard margins (unlearnable today) |
| 3 | `drift` | Use drift through every turn, even if it is slower at first | 10 | whether drift lines are faster on any leg; the AI only uses drift where your drift legs beat your plain ones |
| 4 | `abilities` | Use every button the Squirrel has, whenever it might help | 10 | when each ability pays (needs the new `buttonEvents`) |
| 5 | `hard` | Normal racing with the rivals on **Hard** | 10 | racing close to fast rivals: passing, giving way, the line under pressure |
| 6 | *(none)* | I2, I3, I4 (different courses) | 10 each | a line per course, and what carries over between courses |

- **Store every race when Unity asks**, including the bad ones. Mistakes are measured too (they tune Easy and Medium);
  they are just never averaged into the line.
- **Rough budget:** about 100 races in all, the "100 gameplays" Yash offered.
- **Where to stop:** when adding 5 more races moves no leg's line by more than its spread, that course is learned.

**Two things recordings cannot teach:**
1. **A technique nobody flew.** If drift is never flown well, no amount of data finds the drift line. That needs the AI
   trying it in a test range (T-20261015-2082, the AI Lab).
2. **Other modes' scenes.** Regatta's whole scene is rings, rails, rivals and crystals, and all of it is recorded.
   A mode with creatures or other pilots' trails needs a nearby-prism and creature snapshot added to the recorder first.

## 5. Re-running

```bash
python3 Tools/Build/racing_line.py --self-test
python3 Tools/Build/racing_line.py --dir <folder of flight_*.json>           # report only
python3 Tools/Build/racing_line.py --dir <folder> --write                    # + Docs/AI_SYSTEM/racing_lines/*.json
```

Flights stored with Store AI record are on `origin/bugledger/<user>` under `BugLedger/shared/reports/<user>/flights/`.
