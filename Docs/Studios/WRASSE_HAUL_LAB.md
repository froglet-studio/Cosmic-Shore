# Wrasse Haul Lab

A browser lab (`/labmaker`) that puts the shipped Wrasse (A) beside the concept in
`Docs/Studios/ONE_THUMB_SEAT_ARCS.md` §2 (B: Hunt, Haul, Sling, Clean). Page: `Docs/Studios/WrasseHaulLab.html`. Live
artifact: https://claude.ai/artifact/R3ZqCPMSM2sJdwsHy5f5nu (private to Garrett until shared from its Share menu; its own
decision log, collection `decisions`, empty at publish).

**The question** (Garrett, 2026-10-29): "the wrass has three fun things to do : attack vessels, prisms, and lasso
elements. but the constant looping gets very repetitive. every vessel must be fun the thousandth time. [...] they are
both elegant in that they can be played on older mobile devices in portrait mode with one thumb and still contribute to
a squad. i want to retain those goals while completing the experience to address their repetitiveness with a more well
rounded arc to their gameplay loop." Then: "wrasse: show me the new concept in an a/b artifact".

**Who else works here:** the Wrasse is Garrett's (`KITE_NAVIGATOR.md`). This lab changes neither the Wrasse Lasso Lab
nor the game. It copies the lasso lab's shipped kite core and adds the concept beside it.

## What the page is

- **A | B in the header, and Side by side** (the desktop default): the same seed and the same AI fly A and B in two
  boards at once. Pick **You fly** to play one yourself; tapping the board takes over the variant shown.
- **Shipped, in both** (the Wrasse Lasso Lab's copy of `KiteNavigatorCore`, line for line): the Sling (`stepKite`
  'sling', `ropeForce`, `rimBounce`), the tail (2.5 u step, a crossing closes a loop), the loop rule (one element fires
  base × 2^(n−1), a mixed loop fizzles), and every number from `KiteNavigatorConfig.asset` (read 2026-10-29: `slingMaxSpeed`
  364, `tailLength` 4.8, 26 motes in clusters of 4, spread 18, the effect caps).
- **A, the shipped Wrasse:** a pure loop fires the moment it closes. Motes flow in evenly. A charged head rams the rival.
- **B, the concept**, every number labelled a proposal (prefix `hl`, `sh`):
  - **Hunt:** shoals move like their element. Time darts away when the kite comes within 50 u. Mass shoals are 1.75×
    the size, slow and drift together. Space flickers, then hops 38 u every 2 s. Charge shies from the kite inside 60 u
    and drifts toward the rival. 75% of shoals form round a **world feature** (a flora or crystal marker of their
    element, a stand-in for what the hull's plane cuts).
  - **Haul:** a pure loop cinches its motes into a haul towed 16 u behind the head. Further loops grow it (the exponent
    counts the whole haul); other elements braid in. Each mote adds 0.12 to the kite's mass on the rope and 0.04/s drag.
    A rival, a danger prism, or the head turning back through the haul spills it back as a loose shoal.
  - **Sling:** lift the thumb (mouse: release the button) while the kite swings faster than 220 u/s and the haul flies
    at 1.15× the kite's speed, slowing at 1.8/s. Space blasts where it lands, Mass lays its cluster there, Time thrown
    within 26 u of the Grouper banks boosts (missed, it is a loose shoal again), Charge stays on the tail as a flail
    that rams the rival and pops hostile prisms. All four braided: an Omni sling at ×1.5.
  - **Clean:** fly into the Grouper (board centre) with nothing hauled, or only Time, and the kite docks; the thumb can
    leave the glass. The dock cleans 2 s/s of the Grouper's silence on top of its own run-down. A loop round an ally's
    shadow cleans 5 s off it. Drag out and let go to launch.
- **The stand-in world, the same for both:** 70 hostile prisms flowing down at 40 u/s (8% danger), a rival hull that
  hunts the Grouper and two allies and silences them 4 s a hit (every 3 s at most, up to 15 s).
- **Scorecard:** A and B × a skilled and a rookie AI × 3 seeds × 180 s, plus an idle thumb on each, and the
  **intensity curve**: the skilled AI's thumb speed over one run, 5 s average, A against B. The AIs write the thumb only.
  The panel opens on the numbers below; **Score A against B** re-runs them at your settings.
- **Decisions tab:** the shared log (topics: A or B; the haul; the throw on a fast lift; the shoals; the dock and
  cleaning).

## Measured

`window.__lab.runBatch({seeds: [1, 2, 3], secs: 180})` and `curve('A'|'B', 'skilled', 1)` at the lab defaults
(the page's embedded `MEASURED`). Per minute unless named.

| Variant | Pilot | Thumb mm | Rest % | Docked % | Loops | Haul at throw | Throws | On target % | Value | Prisms | Mass | Boosts | Petals | Silence cleaned s | Spills | Fizzle % |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| A shipped | skilled | 2115 | 0 | 0 | 22.0 | – | – | – | 177.9 | 301.3 | 257.3 | 27.9 | 40.4 | 0 | – | 25 |
| A shipped | rookie | 1784 | 0 | 0 | 18.0 | – | – | – | 136.0 | 281.8 | 212.7 | 18.9 | 30.3 | 0 | – | 30 |
| B haul | skilled | 2209 | 31 | 23 | 9.1 | 3.7 | 2.6 | 48 | 54.9 | 120.1 | 140.7 | 1.9 | 25.8 | 12.5 | 4.2 | 13 |
| B haul | rookie | 1752 | 36 | 26 | 8.6 | 6.3 | 1.8 | 50 | 56.7 | 93.7 | 245.3 | 0.4 | 14.4 | 12.5 | 3.9 | 18 |
| A, idle thumb (control) | – | 0 | 100 | 0 | 0 | – | – | – | 0 | 29.4 | 0 | 0 | 0 | 0 | – | 0 |
| B, idle thumb (control) | – | 0 | 100 | 0 | 0 | – | – | – | 0 | 26.6 | 0 | 0 | 0 | 0 | 0 | 0 |

**Value** is growth^(n−1) per pure loop fired (A) or per element delivered (B), the lasso lab's unit, capped where the
element's own cap stops the game (Space 640/60, Mass 240/6, Time 20 boosts, Charge 30 petals).

**Intensity curve** (skilled, seed 1, 180 s): seconds with the thumb nearly still (under 10 mm in the second): **A 11,
B 29**.

**Noise band** (seeds 4-6 against 1-3, skilled): A value 155 against 178, thumb 1980 against 2115 mm; B value 42.9
against 54.9, thumb 2474 against 2209 mm, rest 24% against 31%. Read differences under about 20% as noise.

**The idle controls** destroy about 27-29 prisms a minute: hostile prisms flow into a still head. That is the floor of
the Prisms column, as in the lasso lab's Breaker.

## What it says

- **B gives the seat a valley.** The thumb is off the glass 31% of the time (36% rookie) against 0% for A, and the kite
  sits in the dock about a quarter of the run. The curve shows it: 29 near-still seconds against 11.
- **B does not save thumb travel; it concentrates it.** The thumb moves about as far per minute (2209 against
  2115 mm), in fewer seconds: when B works, it works harder (throw whirls, homing).
- **B, at these numbers, delivers about a third of A's value per minute** (55 against 178). Loops fall from 22 to 9 a
  minute, and about 4 hauls a minute are spilt against 2.6 thrown.
  - **Spills are not the main cost.** With head and danger spills switched off, B's skilled value is 49.3 and 32.4: the
    AI throws less and rests more (43%, then 64%). The cost is the pacing: time spent homing, docking and whirling up a
    throw.
  - **The Grouper's boosts nearly vanish** (1.9 a minute against 27.9). Time shoals dart away from the kite, and a Time
    haul must be thrown within 26 u of the Grouper or flown home.
- **B is the variant where Mass lands where it counts:** 48-50% of throws land on target (a blast that took 4+ prisms,
  Time caught, Mass by the rival or an ally, Charge on the rival). A has no aim at all.
- **The rookie hauls bigger than the skilled AI** (6.3 against 3.7 motes a throw), because the skilled AI throws early
  when a rival or a danger prism comes near. The press-your-luck reads in the numbers: the skilled AI trades size for
  safety.
- **Silence:** B cleans about 12.5 s a minute off the Grouper and allies. A cleans none.

## Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/WrasseHaulLab.html`: **PASS** at desktop 1600×900 and an
  iPhone 13 (no console errors, no clipping, no sideways scroll on the phone; hooks present; every shipped value inside
  its slider range; the manual clock advances; `runBatch` deterministic within a page and across two loads; the stage
  is not blank). Screenshots read: the desktop shows A and B side by side with each board's label and running numbers;
  the phone shows one board full width under the A | B switch.
- `window.__lab.selfTest()`: 13/13. Idle thumb: no loop, no throw (A and B). A mixed loop fizzles (A and B). A pure
  Space loop of 3 fires on close (A), cinches with no blast (B). A slow lift keeps the haul, a fast lift throws it, and
  the thrown Space haul blasts where it lands. A danger prism spills the haul back as a shoal of 3. A docked kite cleans
  faster than silence runs down; drag out and let go launches. A small circling hand closes loops on the Sling.
- **Planted defects the self-test catches:** `hlThrowSpeed` 0 fails "a slow lift keeps the haul"; `hlCleanRate` 0
  fails the dock test; `mixedLoops` 1 (Majority) fails both fizzle tests.
- The decision log: published with `db` + `user`; `ArtifactData list decisions` returns an empty collection.
- The full scorecard (12 runs × 180 s) plus two curves takes about 3 s headless.

## Found

1. **The haul's exponent breaks the value scale without the caps.** Before value was capped at each element's game cap,
   one seed scored 2138 value in 180 s from one braided haul of 11. The game's caps (Space 640, Mass 240, Time 20
   boosts, Charge 30) hold the effects, but the concept should decide whether a haul's n is capped (or grows by less
   than 2 per mote) before a person finds that one huge haul beats everything.
2. **The head-crossing spill fires on the Sling's whip.** When the rope yanks the kite round, the haul lags and ends up
   ahead of the head, so the head runs into it. Even at 0.6 × the haul's radius (the page's rule) the skilled AI spilt
   13 hauls this way over three 180 s seeds, against 18 to danger prisms and 7 to the rival. Flown by a person this may
   read as unfair: a candidate to drop (`hlHeadSpill` 0).
3. **Time is the hardest element to bank in B.** Darting Time shoals plus "throw it into the Grouper" cut boosts to a
   fifteenth of A's. If the Grouper is to keep its boosts, Time needs an easier delivery (a wider catch, or Time banked
   on cinch as A does).

## Decision needed (designer)

- **A or B** (and which parts of B): the haul that waits for the throw, the throw on a fast lift, the shoals, the dock
  and cleaning. Record it in the page's Decisions tab.
- **Whether B should match A's output.** At these numbers B rests and aims but delivers a third of A's value. Pick the
  balance before the numbers: if the valleys are the point, the peaks have to pay more (bigger caps on a thrown haul,
  a faster throw-up, a dock that also banks).
- **Time's delivery** (Found 3) and **the head spill** (Found 2).

## Not modelled

- The 3D world. Motes, prisms, the rival and the allies live only on the board. The features shoals gather round stand
  in for the flora and crystals the plane cuts.
- The board is the disc of round 6, not round 7's whole-screen rectangle; the board never rotates with the pilot's
  turns; the world flows down at a steady speed.
- Silence is a timer the rival adds to, not `SILENCE.md`'s scored-hit worth. The AOEExplosion, the Mass cluster and the
  boost are a ring, a count and a dot.
- Rivals fighting the kite back, Wrasse Run's rings, the HUD, sound, network play, the two-seat swap.
- The thumb figures assume a 57 mm board (the lasso lab's), so they compare A with B but are not millimetres on your
  phone.
- The AI pilots are lab heuristics. B's rest share is set by its pilot's choice to dock after 70% of throws (50%
  rookie), so the scorecard measures this pilot as much as the concept. A phone in a person's hand decides.

## Next

Garrett flies both on a phone and records the pick. A round 2 on the picked parts: the balance of a thrown haul, Time's
delivery, then `/labmaker` §7 into `KiteNavigatorCore` with a parity gate against this page.
