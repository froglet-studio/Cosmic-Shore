# Krill Lab: round 1 (2026-10-20)

**The Krill** is the passive swarm card vessel (cards: [SWARM_CARD_VESSEL.md](SWARM_CARD_VESSEL.md)). This lab is where it is first played.

**Live page:** https://claude.ai/artifact/QsSVVTcxL4PxXsrK2MBeVY (private to Garrett until shared)
**Source:** `Docs/Studios/KrillLab/src/lab.html` → `python3 Docs/Studios/KrillLab/build.py` → `Docs/Studios/KrillLab.html`. The card numbers are read from `Docs/Studios/SwarmCardVessel/cards.json`, the same source as the [deck page](https://claude.ai/artifact/QB2oYkL8P8Cv7KPGyhdHp2).
**Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/KrillLab.html` (PASS on desktop and phone)
**Status (2026-10-20):** in Unity on `claude/project-thread-ytyab2`, not yet editor-verified. Class 21, `Krill.fbx`, `Krill.prefab`, in the Toybox Vessel Changer. Design and files: `Assets/_Scripts/Controller/Vessel/R_VesselActions/KRILL.md`; generator `Tools/Build/author_krill_assets.py`.

## What you do in it

You never steer the krill. They roam the whole cell in 3D on their own.

- **The view.** Drag anywhere to turn your view. It works like a joystick: push to spin it about its own right and up axes, and it stops when you let go. Arrow keys also work. The view follows a sampled centroid (at most 12 members). Things near your viewing plane draw sharp and the rest fade, as a stand-in for the Wrasse's 2.5D look.
- **The phase cycle.** The cycle runs Omnicrystal, then Elemental crystals, then Opposing vessels, then Opposing mass. Each phase ends on its timer or when its goal is met. The phase bar shows the phase, the time left and the goal's progress.
- **Hands.** When the Omnicrystal phase ends, with a crystal or without one, you get four cards, one per element. Pick one. The swarm keeps going while you read. A card you already hold adds its level, up to 15.
- **The minimum size.** At or below the protected minimum (6), rival shots stop killing krill.
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
- The swarm starts at 24 members, with a cap of 40 and a protected minimum of 6.
- Krill cruise at 120 u/s, double the cards' 60 u/s baseline, so a phase can cross the 900 u cell. They turn at 70°/s.
- **Bare-swarm verbs** (what the swarm does with no cards):
  - Touching a crystal collects it, and each elemental crystal hatches one krill.
  - Touching a plain enemy prism breaks it at 0.5 per second within 25 u.
  - Touching a rival nips a twentieth of a petal a second.
- **Rivals:**
  - They cruise at 90 u/s, wander, and lay their own prisms.
  - They pick up crystals.
  - They shoot the nearest krill within 220 u, which hits half the time.
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

| Build | Time (s) | Space | Mass | Charge | Hands | Krill at end |
|---|---|---|---|---|---|---|
| No cards | 15.7 | 73.7 | 0 | 0.1 | 4.7 | 18 |
| Random picks | 16.3 | 90.3 | 160.7 | 0.8 | 4.7 | 28.3 |
| Mass first | 16.0 | 98 | **415.3** | 0.9 | 5 | 31.7 |
| Time first | **14.2** | 91 | 0 | 0.4 | 5.3 | 20.7 |
| Space first | 17.9 | **271.3** | 6 | 0.2 | **11.3** | 26.3 |
| Charge first | 17.1 | 78.7 | 0 | **43.1** | 5.7 | 27 |

Each element's build wins its own column, as the element jobs intend.

## Found

1. **Space first gets more than twice the hands (11.3 against about 5).** Space cards meet the Opposing mass goal early, so the cycle comes round faster and deals more hands. In this design, the element that ends phases early also earns cards fastest. That makes it the strongest pick for a player who wants more picks. Either the hand rate needs a cap, or the other phases need cards that end them early too.
2. **Without Charge, the Opposing vessels phase almost never ends early** (Charge 0.1 to 0.9 in every other build). The bare nip is too weak to debuff a vessel in 25 s. Either the goal is too hard for a swarm without Charge, or that phase is simply Charge's to shorten.
3. **Time's lead is the smallest** (14.2 s against 15.7 s with no cards). Most of a crystal phase is spent crossing the cell, and speed cards at level 5 shave about 10%. Time may need a card that changes the route as well as the speed.
4. **Mass is all or nothing.** No build that skips Mass keeps any prisms, because fauna graze what little is laid. Mass's job is only visible if you pick it.

## Decision needed (Garrett)

1. Play it on your phone held sideways, with "You pick". Does watching and picking feel good, with no steering at all?
2. Should ending a phase early speed up hands (finding 1), or should hands come on a fixed clock?
3. Are 30 / 25 / 25 / 25 s, Y = 5 and X = 30 about right? The Phases tab has sliders.
4. Before Unity: who builds the krill model? One small body drawn per member is the plan, with its four element morphs made through `/vessel-model`.

## What this lab does NOT model

- The view is a 2D canvas projection with a depth fade, not the Wrasse's renderer.
- Rivals are scripted. They use no abilities, and they never stun.
- A prism is a point with no volume.
- Only 24 of the 80 cards are wired, and some side effects are simplified (milestones such as Mound Builders' wall at L5 are not modelled).
- The game has no measurement of this vessel yet, so nothing here covers collider budget, phone frame time or networking.

## Round log

- **Round 1 (2026-10-20, `claude/project-thread-pkoq70`):** the first build covers the passive 3D swarm, the joystick view, the four-phase cycle, hands of 4 with level stacking, the protected minimum, 24 wired cards, I1 to I4, a six-build scorecard and the decision log.
