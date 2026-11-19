# Palisade - the Swordfish's Mass game

> **Status.** Authored headless on 2026-11-15 (`claude/swordfish-game-kit`), for Garrett's ask to build every missing
> elemental game, on the ids `Docs/ElementalLoop/GAME_KIT_ROSTER.md` reserved for it (row 8: `GameModes` 98, hint toast
> 173). **Nothing here has been run in the Unity editor, and nobody has flown it.**

`GameModes.Palisade = 98` is the **Swordfish's Mass card**: walls you swing into place (`VolumeRemaining`, timed).
Its Space card is Cleave, its Time card Spearline (96) and its Charge card Riposte (97).

## 1. Why this game, for this hull

The Swordfish's **Mass** ability is **Afterimage** (`SwordfishSwordAfterimage`, `RHINO_SWORD_AFTERIMAGE.md`): for 5 s
after the blade LEAVES the energized state it leaves prism copies of itself where it swings (up to 16 a window), Mass
scales their volume x1 -> x2.5, and at Mass 5 (**Danger Afterimages**) they are laid as danger prisms. The same blade
cuts any rival prism it reaches (a shielded one in two strokes).

So Palisade is a **timed wall war**: the score is the prism volume a domain has STANDING when the clock runs out (the
Swordfish's trail and its afterimages). The sword both builds and raids, and the mode's swing is that the two happen in
one move: an energized run through a rival's wall cuts it down, and the afterimages fall as the blade cools, right
where it was. At Mass 5 the walls are danger, so a rival who rams through to cut them pays the 4 s debuff.

The vessel is untouched (the LOCKED mode rule). The mode's levers are the bare arena, the crystals and the AI.

## 2. What is reused

- **Tapestry's shape** (the Butterfly's timed mass war, `TAPESTRY.md`): the bare Barren cell (no food web: a grazer
  would eat the trailing team's mass, an anti-comeback current), the peer-local crystal scatter (a Mass crystal is a
  bigger wall), the timed monitor as the only end, the server lead sampler, the snapshot carrying volume and prisms.
- **`PalisadeScoringRuleSO : TapestryScoringRuleSO`**: the same rule, its results lines authored templates on its own
  asset (`{volume} Volume`, `{prisms} prisms`).
- **`PalisadeController`** is Tapestry's controller without the Butterfly's mode press (the Swordfish has no mode to
  drive; its sword is the vessel's own hand), posting the shared lead change (130) and a once-a-match hint (173).
- **`PalisadeTimeTurnMonitor`** reads `EndConditionOverridesSO.palisadeRoundSeconds` (90).
- The scene is Tapestry's, cloned with the identity, the settings and rule, fresh Netcode ids and four Swordfish seats.

## 3. The AI

Steering only, through `SetExternalTargetProvider`. On a 6 s clock each AI **builds** (a slow orbit on its own great
circle, detouring for crystals) or **raids** (straight at the densest opposing mass, `Cell.GetExplosionTarget`) when it
trails the best rival by 10%, and always in the last 25 s. The sword is `SwordfishSwordAutopilot` on the prefab: it
swings at hostile prisms in reach and energizes in a crowd, so a raid cuts and then lays afterimages on its own.
**Watch item:** a building AI lays only its trail (afterimages need an energize, and the sword hand energizes only in a
crowd of hostile prisms). If AIs lose to humans by building too little, the fix is in the mode's AI (raid earlier), not
in the vessel.

### 3.1 The sword hand trained here (2026-11-19, elemental loop step 3)

The watch item above was the gap. Raced on the Brood Rush volume books (each pilot's VC + VD, `brood_judge.py --mode
Palisade`, Amoebius headless, Swordfish against Swordfish), the shipped sword hand barely energized, so the Swordfish
laid trail and almost no afterimages. Its own numbers on `SwordfishSwordAutopilot` (`Swordfish.prefab`) were searched in
Palisade and Cleave together: `reachMultiple` 1.1 -> 6, `hardenedSearchMultiple` 1.6 -> 9, `energizeCrowd` 6 -> 1. The
hand now swings and energizes whenever there is anything near, which keeps the afterimage window open.

| Mean seat VC + VD (2 seeds, 4 seats) | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Palisade shipped -> trained | 5.8k -> 9.8k | 8.8k -> 12.9k | 5.8k -> 12.6k | 8.8k -> 14.5k |
| Cleave shipped -> trained | 7.5k -> 21.1k | 13.0k -> 25.1k | 26.3k -> 33.3k | 31.2k -> 38.7k |

I2 on fresh seeds 11-12, the other rungs on seeds 21-22. Knee (seeds 1-2, I2, Palisade / Cleave): reach 2 at crowd 6 +19% / +46%; at crowd 1, reach
3 +51% / +97%, 4.5 +55% / +115%, 6 +86% / +130%. The hand is the vessel's own AI, so every mode flies it. Regatta, which fuels the Swordfish's ramp from its slices, is unchanged (shuffle, 3 seeds: card 71.9 -> 72.7 s, L5 65.8 -> 65.1 s); Broadside and Spearline were not re-judged. Raw:
`/mnt/project-files/regatta-flywheel/mass/`.

## 4. Ladder

Scarcity, Tapestry's: elemental crystals 24 / 16 / 10 / 6 by intensity in the same bare cell.

## 5. Comeback

Sized against a volume read off the shipped Swordfish (`author_palisade_assets.py` prints the model): the trail block on
`Swordfish.prefab` at half the 840 u/s ramp, plus one window of Mass 0 afterimages every 15 s, for a domain of two who
build half the round. A quarter of that behind buys 1.5 element levels; the generator asserts at least 1.

**What the model says about the score.** In it the trail is about 189 vol/s and the afterimages about 31 vol/s (one
16-image window of 28.8 each every 15 s, Mass 0), so most of a domain's standing volume is TRAIL and the afterimages
are the big, Mass-scaled pieces of it. Mass 10 makes the afterimages x2.5 (about a quarter of the score). If the
playtest says the walls should be more of the game, the levers are the mode's (a reward for afterimage volume, or a
cell that eats trail faster than walls), never the vessel's trail or afterimage numbers.

## 6. Verification status

- Authored headless: `author_palisade_assets.py --check`, the reference compile and the static gates (see the PR).
- **Not run in Unity, not flown, no card art rendered yet.** Hand test: two Swordfish on two teams at I1; confirm the
  afterimages count in the score, a lit run through a rival's wall cuts it and leaves yours, and the whistle at 90 s
  shows each pilot's volume and prisms.
- Card art: `/cardart` needs a `Palisade` recipe (the bare cell with afterimage walls).
