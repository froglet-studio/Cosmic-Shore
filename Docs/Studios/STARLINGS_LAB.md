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

## The Time race: the Starlings' Time arcade game (2026-11-02, `feature/starlings-time-race`)

Garrett, 2026-11-02: "There should be different arcade games for different elements, so it should be fine if someone
picks all Time cards on all four intensity levels of the Time arcade game. The skill should be in picking Time cards
best suited to the random factors of the race. The race should still be about getting through the switches fastest,
because then the Starlings could be taken to Regatta."

So the lab's game is now a **Time race**: a closed loop of switch rings flown three laps, as in Regatta. While racing,
the swarm's objective is always the next switch; it threads one when its sampled centroid passes within the ring. The
mode sets the objective only: the swarm's flight, its cards and the phase cycle are unchanged, and the phases still
run on their timers and deal the hands. An omnicrystal always lies mid-leg ahead of the swarm, so hands keep coming
(with hands only on the timers, a race dealt one card and left no choice). Each race draws its course from its seed
and shows it before the start:

| Course | Shape |
|---|---|
| Open water | 6 switches on a wide loop: long straights |
| Bends | 8 switches weaving in and out: medium turns |
| Hairpins | 8 switches that zigzag between the middle and the edge: sharp turns |

Intensity narrows the switches (90 / 75 / 60 / 50 u for I1-I4) and adds rivals that hang round them and shoot
starlings (0 / 1 / 2 / 3). Pick *Time race* in the Game menu and a course; the Scorecard tab has **Run the time race**;
`__lab.runRace({ course, intensity, seeds })` is the headless form.

### Which Time card wins (every build is all Time; they differ in the card they take first)

Seconds to finish three laps, lower wins, Time cards at level 5; I2 on 8 seeds, I1, I3 and I4 on 6.

| Course | I | No cards | Skip Ahead | Agility | Quickening | Hurry Up | Blink Step | Dragonfly |
|---|---|---|---|---|---|---|---|---|
| Open water | I1 | 126.1 | **99.1** | 108.6 | 102.8 | 110.1 | 109.2 | 143.1 |
| Open water | I2 | 128.5 | **98.1** | 109.3 | 128.8 | 107.9 | 114.0 | 139.1 |
| Open water | I3 | 130.4 | 108.4 | 121.8 | **104.6** | 113.1 | 122.7 | 159.1 |
| Open water | I4 | 131.7 | 123.2 | **119.3** | 129.5 | 119.8 | 127.7 | 255.4 |
| Bends | I1 | 160.5 | 139.2 | **130.5** | 173.6 | 159.2 | 184.5 | 224.1 |
| Bends | I2 | 178.4 | 174.2 | **155.9** | 221.0 | 201.3 | 202.3 | 252.2 |
| Bends | I3 | 194.2 | 162.8 | **159.0** | 204.2 | 171.4 | 176.9 | 231.2 |
| Bends | I4 | 238.4 | 190.5 | **164.7** | 290.8 | 263.5 | 207.5 | 408.9 |
| Hairpins | I1 | 219.4 | **150.9** | 152.0 | 218.1 | 182.1 | 197.6 | 230.5 |
| Hairpins | I2 | 297.1 | 186.1 | **174.1** | 273.5 | 220.8 | 218.0 | 239.3 |
| Hairpins | I3 | 308.1 | 187.7 | **178.4** | 308.9 | 272.3 | 227.9 | 354.2 |
| Hairpins | I4 | 339.2 | 220.7 | **187.5** | 349.5 | 266.9 | 272.9 | 331.0 |

What it shows:

1. **The card choice is the skill.** Skip Ahead wins the open loops at low intensity and Agility wins every course with
   turns. A player who takes the right card for the course (the *Time, reads the course* build: Skip Ahead on Open
   water, Agility otherwise) is best or close to best in every cell.
2. **Speed without turning is a trap.** Dragonfly is the worst Time card on every course, and Quickening is worse than
   no cards on Hairpins: the swarm overshoots the switch and circles back (worst races of 450-600 s). That is a real
   decision, and a lesson about the cards.
3. **The Starlings turn badly.** Without cards, Hairpins take 220-340 s against 126-132 s for Open water. In Regatta
   the swarm will need Agility, or the Regatta course will need to suit it; that is for the Regatta balance plan
   (`Docs/AI_SYSTEM/REGATTA_BALANCE_PLAN.md`).
4. Hurry Up and Blink Step never win: Hurry Up only speeds the hands, and the race does not count starlings lost.

### Before this: the Time trail (2026-11-01, PR #1691)

The first proposal laid each phase's objective at a station and timed four laps of the cycle, with courses that each
rewarded a different element (Long haul for Time, Fortress for Space, Chase for Charge; Mass won none). Garrett's
2026-11-02 direction (each element has its own arcade game, and the Time game is a switch race) replaced it.

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

- **Time race (2026-11-02, `feature/starlings-time-race`):** the trail is replaced by the switch race: three laps of switch rings, a random course each race (Open water, Bends, Hairpins), I1-I4 switch sizes and rivals, all-Time builds that differ in the card they take first, and `runRace`.
- **Time trail (2026-11-01, `feature/starlings-time-game`):** the Game menu's Time trail, stations drawn on the stage, `runTrail` and its scorecard; round 2 adds the Long haul, Fortress and Chase courses, the favourite-card AI and the Reads-the-course AI (above).
- **Round 1 (2026-10-20, `claude/project-thread-pkoq70`):** the first build covers the passive 3D swarm, the joystick view, the four-phase cycle, hands of 4 with level stacking, the protected minimum, 24 wired cards, I1 to I4, a six-build scorecard and the decision log.
