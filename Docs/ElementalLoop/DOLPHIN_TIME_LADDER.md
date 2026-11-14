# Dolphin Time ladder: Switchback I1-I4 (elemental loop step 2)

2026-11-13. Step 1 (fun) is signed off by Garrett (#1946). This is step 2 for the Dolphin and Time: do I1-I4 serve
intro, fun, challenging and very hard, each adding one demand without a cliff (PLAYBOOK step 2)? What it found, the one
rung fixed, and the four-rung playtest that closes the step.

## What was measured

**Human flights** (`python3 Tools/Build/rung_bars.py --vessel Dolphin`, the bugledger branches). Before this change the
check counted every Switchback flight, including the 50 or so flown on the Classic ladder Blast Run replaced on
2026-10-29, so its I1 FAIL and I4 pass were about a course nobody flies now. It now counts the shipped family only
(the same rule as `learn_drives_from_flights.SHIPPED_FAMILY`). On Blast Run:

| Rung | Flights | Finished | Notes |
|---|---|---|---|
| I1 intro | 4 | 0 | all four Yash with the AI toggle ("mixed"); the race ended when an AI won at 73-182 s |
| I2 fun | 4 | 4 | median 213 s |
| I3 challenging | 0 | - | nobody has flown Blast Run I3 to the end |
| I4 very hard | 6 | 0 | every one lost to the AI after 6-16 gates; Garrett left two I4 runs early (28 s, 74 s) |

A Switchback race ends when the first domain's lead runner threads the last gate, so "finished" here means beating the
AI seats (default difficulty Medium). Ten races is too few to settle a bar, and the I1 four are not a person's verdict
(the AI held the hull for part of each), so the course itself was measured offline too.

**The course, offline** (`bash Tools/Build/switchback_sim_harness/run.sh eval`, the shipped layout and AI, 24 courses a
rung). To read the course rather than the AI's per-rung pace, every rung was flown with the same nose follow (2.2/s):

| Rung | Median s | Prism hits a race | Dead stops (Danger) a race | Step from the rung below |
|---|---|---|---|---|
| I1 | 81 | 13 | 4.7 | |
| I2 | 128 | 17 | 0.9 | +58 % time: the hairpins (the drift) and denser fields |
| I3 | 119 | 22 | 3.5 | flat in time; adds rotors and danger |
| I4 (gauntlet) | 185 | 143 | 28 | +55 % time, 6.5x the hits, 8x the dead stops |

I4 was a cliff, not one more demand: the gauntlet added no bore, sealed eyes, zig-zag keys, plugged tunnels, more shapes
and +50 % prisms in one step, and no person had finished it. That made it the worst rung.

## The fix (I4 only)

Taking the gauntlet's pieces out one at a time offline (I4, 24 courses, Easy / Medium / Legendary) showed where the
dead stops came from:

| Variant | Easy s | Medium s | Legendary s | Dead stops a race (E / M / L) |
|---|---|---|---|---|
| shipped | 185 | 172 | 157 | 40 / 24 / 12 |
| plug walls without Danger hearts | 187 | 174 | 153 | 40 / 21 / 8 |
| **seals without the Danger heart** | **151** | **159** | **149** | **2 / 5 / 4.5** |
| field without danger shapes | 189 | 174 | 156 | 40 / 25 / 12 |
| no plug walls at all | 185 | 172 | 140 | 41 / 22 / 7 |

The seal's Danger heart was about nine in ten of the dead stops: every blast that missed its key meant two 3 s dead
stops at that gate. **The change: a seal is nine plain cubes.** You still have to blast it open or ram through it at a
plain hit's slow per cube; everything else in Yash's gauntlet (no bore, the keys, the tunnels, the plug walls and their
hearts, the counts, the rays) is unchanged. A game mode never changes the vessel, and this changes only the course.

With ramming cheaper the Easy AI (which threads) became faster than Medium, so the gauntlet's AI nose follow
(`SwitchbackController.aiNoseFollowGauntlet`) went from 1.9 / 2.8 / 3.5 / 5 to 1.5 / 2.2 / 3.5 / 6, which puts the four
levels about 10 s apart again. The shipped ladder after the change (each rung's own AI rates, 48 courses):

| Rung | Easy | Medium | Hard | Legendary |
|---|---|---|---|---|
| I1 | 88 | 82 | 69 | 61 |
| I2 | 130 | 117 | 109 | 99 |
| I3 | 130 | 120 | 111 | 101 |
| I4 | 176 (was 185) | 167 (was 172) | 155 | 141 (was 157) |

All 768 races finish. These are model seconds; real races have run up to about 1.6x the model. I4 is still the hardest
rung by far: about 40 % slower than I3 for the Medium AI and five times its plain hits.

Held by `SwitchbackBlastRunTests.TheEyesAreSealed` (now asserts no Danger heart; restoring the heart fails it) and
`SwitchbackAISteeringTests.Switchback_AINoseLag_TheGauntletHasItsOwnRow`; the 76 shipped Switchback layout and planner
tests pass headless. Scarab Rally turns the gauntlet off and is unchanged. Not run in the Unity editor.

## What is still open on the ladder

- **I3 adds no time over I2** (both about 120 s for the Medium AI): it adds rotors and danger instead. Whether that reads
  as "challenging" is the playtest's call; if it reads as "the same as I2", I3 is the next rung to shape.
- **I1 and the AI seats.** "A first-time pilot finishes" depends on the AI level the seats fly, because the race ends
  when an AI wins. No pure human Blast Run I1 race is on the ledger yet.
- **The real-game pace** of the I4 gauntlet (5,000 prisms, 600 colliders near the path) has never been measured in Unity.

## Garrett's four-rung playtest (closes step 2)

About 12 minutes. In the editor's Arcade, pick **Switchback** with the Dolphin, AI difficulty left at its default, seed
RANDOM. Fly each rung once, in order, to the end of the race, then stop Play (stopping mid-race is not counted).
When Play stops after each race the Fun Card asks three taps (fun, and whether the rung felt right); answer it:

1. **I1 (intro)**: should feel like learning to fly through gates; easy to finish.
2. **I2 (fun)**: the rung you signed off; the hairpins want a drift.
3. **I3 (challenging)**: tighter rings, rotors and Danger prisms; should push you without stopping you.
4. **I4 (very hard)**: the gauntlet; take each key and blast the sealed door. Changed today: ramming a seal now slows
   you instead of a dead stop. Should be beatable with effort.

The card's "fun: yes" and "right" on all four rungs signs step 2 off (`fun_cards.py apply`) and sends the Dolphin's Time
AI to training (step 3). A rung that reads too easy or too hard: say which and how it felt, and that rung is next.

## Re-measured after the sight lines (#1981, 2026-11-14)

#1981 hung every door behind its ring and moved the keys nearer. The same run (shipped code, 48 courses a cell, model
seconds) after it:

| Rung | Easy | Medium | Hard | Legendary |
|---|---|---|---|---|
| I1 | 85 | 77 | 70 | 60 |
| I2 | 130 | 118 | 111 | 98 |
| I3 | 129 | 121 | 109 | 102 |
| I4 | 184 | 166 | 156 | 141 |

Every race finishes, the four AI levels keep their order on every rung, and I4's dead stops stay at 3-6 a race. The
ladder's shape is unchanged (I3 still flat in time against I2), so the playtest above stands as written.
