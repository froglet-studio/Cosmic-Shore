# Starlings Lab: round 1 (2026-10-20)

*Renamed from the Krill on 2026-10-20 (Garrett: the swarm is a swarm of vessels, so it becomes the Starlings; the Jacks and the Marlin will herd actual fauna). Same lab, same link.*

**The Starlings** is the passive swarm card vessel (cards: [SWARM_CARD_VESSEL.md](SWARM_CARD_VESSEL.md)). This lab is where it is first played.

**Live page:** https://claude.ai/artifact/QsSVVTcxL4PxXsrK2MBeVY (private to Garrett until shared)
**Source:** `Docs/Studios/StarlingsLab/src/lab.html` → `python3 Docs/Studios/StarlingsLab/build.py` → `Docs/Studios/StarlingsLab.html`. The card numbers are read from `Docs/Studios/SwarmCardVessel/cards.json`, the same source as the [deck page](https://claude.ai/artifact/QB2oYkL8P8Cv7KPGyhdHp2).
**Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/StarlingsLab.html` (PASS on desktop and phone)
**Status:** lab only. Nothing is in Unity. There is no class id, prefab or model yet.

## What you do in it

You never steer the starlings. They roam the whole cell in 3D on their own.

- **The view.** Drag anywhere to turn your view. It works like a joystick: push to spin it about its own right and up axes, and it stops when you let go. Arrow keys also work. The view follows a sampled centroid (at most 12 members). Things near your viewing plane draw sharp and the rest fade, as a stand-in for the Wrasse's 2.5D look.
- **The phase cycle.** The cycle runs Omnicrystal, then Elemental crystals, then Opposing vessels, then Opposing mass. Each phase ends on its timer or when its goal is met. The phase bar shows the phase, the time left and the goal's progress.
- **Hands.** When the Omnicrystal phase ends, with a crystal or without one, you get four cards, one per element. Pick one. The swarm keeps going while you read. A card you already hold adds its level, up to 15.
- **The minimum size.** At or below the protected minimum (6), rival shots stop killing starlings.
- **Cards.** The header's Cards menu can hand the picking to an AI (random, or one element first) so you can just watch.

**Wired cards:** 24 of the 80, 6 per element. Hands draw only from these.

| Element | Cards the lab runs |
|---|---|
| Mass | Mound Builders, Danger Drip, Carapace, Brood, Big Family, Snowball |
| Time | Quickening, Agility, Dragonfly, Skip Ahead, Hurry Up, Blink Step |
| Space | Hole Punch, Wide Bite, Annex, Shield Eaters, Black Hole, Split |
| Charge | Lasers, EMP, Tesla Coil, Short Circuit, Sting, Chain Lightning |

**Intensity I1 to I4** sets the rivals (1/2/3/4), enemy prism clusters (3/5/7/9 of 30 each), the shielded share (0/15/30/45%), the rival fire interval (4/3/2.2/1.6 s) and the fauna count (4/6/9/12).

## The lab's proposals, not Garrett's numbers

- Phase timers are 30 / 25 / 25 / 25 s. Y = 5 elemental crystals and X = 30 enemy prisms.
- The swarm starts at 120 members, with a cap of 200 and a protected minimum of 30 (5x the first round's 24, 40 and 6, Garrett 2026-10-21: "the swarm feels like a small pack not a group of starlings in a murmuration"). Big Family still adds its card numbers (+6 cap at L1) on top.
- Starlings cruise at 120 u/s, double the cards' 60 u/s baseline, so a phase can cross the 900 u cell. They turn at 70°/s.
- **Bare-swarm verbs** (what the swarm does with no cards):
  - Touching a crystal collects it, and each elemental crystal hatches one starling.
  - Touching a plain enemy prism breaks it at 0.5 per second within 25 u.
  - Touching a rival nips a twentieth of a petal a second.
- **Rivals:**
  - They cruise at 90 u/s, wander, and lay their own prisms.
  - They pick up crystals.
  - They shoot the nearest starling within 220 u, which hits half the time.
  - EMP silences their shooting. Nothing stuns them.
- Fauna graze your prisms.

## Measured: the scorecard

**Setup:**
- Each run is 300 s at I2, seeds 1 to 3 averaged.
- Every element's upgrade level is set to 5 so a few picks show.
- The AI picks by each row's rule.
- Produced by **Scorecard → Run the scorecard** (`window.__lab.runBatch()`). Two calls and two page loads give the same rows.

**Columns:**
- **Time:** mean seconds the Elemental crystals phase took. Lower wins.
- **Space:** enemy prisms destroyed or stolen.
- **Mass:** your prisms standing. A shielded one counts twice.
- **Charge:** petals taken + 3 per knockout + 0.25 per fauna.

| Build | Time (s) | Space | Mass | Charge | Hands | Starlings at end |
|---|---|---|---|---|---|---|
| No cards | 15.7 | 73.7 | 0 | 0.1 | 4.7 | 18 |
| Random picks | 16.3 | 90.3 | 160.7 | 0.8 | 4.7 | 28.3 |
| Mass first | 16.0 | 98 | **415.3** | 0.9 | 5 | 31.7 |
| Time first | **14.2** | 91 | 0 | 0.4 | 5.3 | 20.7 |
| Space first | 17.9 | **271.3** | 6 | 0.2 | **11.3** | 26.3 |
| Charge first | 17.1 | 78.7 | 0 | **43.1** | 5.7 | 27 |

Each element's build wins its own column, as the element jobs intend.

## The Time trail (proposal, 2026-11-01, `feature/starlings-time-game`)

The pilot never steers, so a Starlings time game cannot be a race the player drives. The proposal: the mode lays each
phase's objective at its own **station** on a course (a wall of CLUSTER+10 enemy prisms at each Mass station, Y+3
crystals at each Elemental one, the omnicrystal and the rivals near theirs), and the clock runs until the swarm has
worked all 8 stations, two laps of the cycle. Every leg still ends early on its goal or late on its timer, so each
element shortens a different leg. The vessel is unchanged (the LOCKED rule): only the course belongs to the mode.
Pick *Time trail* in the Game menu; the Scorecard tab has **Run the time trail**; `__lab.runTrail({ seeds })` is the
headless form.

Measured on 30 courses (seeds 11-40), every element at level 5, AI picks a card and then the draw element by the row's
rule. Lower wins. Legs are seconds spent on that kind of station, summed over both laps.

| Build | Trail (s) | Omni | Elemental | Vessel | Mass | Hands | Wins (of 30) |
|---|---|---|---|---|---|---|---|
| No cards | 88.9 | 6.9 | 11.9 | 35.8 | 34.2 | 4.7 | |
| Random picks | 78.4 | 7.3 | 11.6 | 29.9 | 29.6 | 4.2 | |
| Mass first | 86.3 | 7.1 | 12.2 | 33.8 | 33.2 | 4.4 | 1 |
| Time first | 81.3 | 7.2 | 11.1 | 30.4 | 32.6 | 4.5 | 3 |
| Space first | 81.0 | 7.7 | 12.1 | 39.4 | **21.8** | 5.6 | 9 |
| Charge first | **65.9** | 7.1 | 11.9 | **18.5** | 28.3 | 3.6 | 17 |
| Rotate elements | 75.1 | 7.6 | 11.4 | 28.9 | 27.3 | 4.5 | |

What it shows:

1. **Time is no longer the only answer.** Time first wins 3 courses of 30. With element hands, the draw element is a
   real choice, which was the point of PR #1677.
2. **Charge is the new default.** The Opposing vessels legs are the longest (36 s of 89 with no cards) and only Charge
   shortens them, halving them to 18.5 s. Space halves nothing but takes 12 s off the Mass legs. These two are the
   real contest.
3. **Mass and Time have no leg of their own.** Mass makes nothing shorter (survival is not timed), and the Elemental
   and Omni legs are already short (12 s and 7 s over both laps), so Time's Hurry Up has little to cut.
4. Mixing pays: Rotate elements (75 s) beats every single-element build except Charge.

Levers that stay within the rule (track, environment, AI, never the kit): longer travel between stations (Time's
speed cards pay), a hazard leg that costs birds and a bird count needed to finish (Mass pays), fewer or slower rivals
at Vessel stations (Charge's edge shrinks), and courses that vary which legs are long so the right build changes per
course.

## Found

1. **Space first gets more than twice the hands (11.3 against about 5).** Space cards meet the Opposing mass goal early, so the cycle comes round faster and deals more hands. In this design, the element that ends phases early also earns cards fastest. That makes it the strongest pick for a player who wants more picks. Either the hand rate needs a cap, or the other phases need cards that end them early too.
2. **Without Charge, the Opposing vessels phase almost never ends early** (Charge 0.1 to 0.9 in every other build). The bare nip is too weak to debuff a vessel in 25 s. Either the goal is too hard for a swarm without Charge, or that phase is simply Charge's to shorten.
3. **Time's lead is the smallest** (14.2 s against 15.7 s with no cards). Most of a crystal phase is spent crossing the cell, and speed cards at level 5 shave about 10%. Time may need a card that changes the route as well as the speed.
4. **Mass is all or nothing.** No build that skips Mass keeps any prisms, because fauna graze what little is laid. Mass's job is only visible if you pick it.

## Decision needed (Garrett)

1. Play it on your phone held sideways, with "You pick". Does watching and picking feel good, with no steering at all?
2. Should ending a phase early speed up hands (finding 1), or should hands come on a fixed clock?
3. Are 30 / 25 / 25 / 25 s, Y = 5 and X = 30 about right? The Phases tab has sliders.
4. Before Unity: who builds the starling model? One small body drawn per member is the plan, with its four element morphs made through `/vessel-model`.

## What this lab does NOT model

- The view is a 2D canvas projection with a depth fade, not the Wrasse's renderer.
- Rivals are scripted. They use no abilities, and they never stun.
- A prism is a point with no volume.
- Only 24 of the 80 cards are wired, and some side effects are simplified (milestones such as Mound Builders' wall at L5 are not modelled).
- The game has no measurement of this vessel yet, so nothing here covers collider budget, phone frame time or networking.

## Round log

- **Time trail (2026-11-01, `feature/starlings-time-game`):** the Game menu's Time trail, stations drawn on the stage, `runTrail` and its scorecard; measured on 30 courses (above).
- **Round 1 (2026-10-20, `claude/project-thread-pkoq70`):** the first build covers the passive 3D swarm, the joystick view, the four-phase cycle, hands of 4 with level stacking, the protected minimum, 24 wired cards, I1 to I4, a six-build scorecard and the decision log.
