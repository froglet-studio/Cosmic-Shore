# Riposte - the Swordfish's Charge game

> **Status.** Authored headless on 2026-11-15 (`claude/swordfish-game-kit`), for Garrett's ask to build every missing
> elemental game, on the ids `Docs/ElementalLoop/GAME_KIT_ROSTER.md` reserved for it (row 7: `GameModes` 97, hint toast
> 172). **2026-11-25 (Yash's Swordfish pass):** its own AI pilots and ramp tuning, measured in the real-game judge
> (Amoebius, headless) on I1-I4, then compiled, tested and smoke-played once in the Unity editor; see sections 3-5.
> **Nobody has flown it.**

`GameModes.Riposte = 97` is the **Swordfish's Charge card**: working other pilots over (`CombatPoints`).

## 1. Why this game, for this hull

The Swordfish's **Charge** card is **Energize** and its PvP: a sword hit on a rival hull takes x1 -> x2.5 petals across
Charge 0-10, knocked loose as crystals at the strike, and at Charge 5 **Sword Steal** pockets them instead
(`SwordfishSwordStealBySkimmerEffect`). The hit itself is `VesselCombatHitBySword`: a **Strike**-class combat hit,
once per victim per 0.5 s (1.4 s until the 2026-11-23 Broadside knee), and **only while the Swordfish is faster than its victim** (`requireFasterThanVictim`).

The Swordfish's speed is its **ramp**, which winds up to 840 u/s only on a straight line, and the ramp burns **fuel that
only the bill's slices earn** (0.004 a prism, 0.1 a second at full ramp; an empty tank tops out at 125 u/s,
`RHINO_RAMP_BOOST.md` "Fuel"). So the duel is about lines and about the sticks: cut for fuel, wind up somewhere straight,
then run the bill through a rival who is slower than you.

Riposte puts that duel in **Jackstraw's heap of pick-up sticks** (the Thresher's Charge arena, `JACKSTRAW.md`, its four
cells referenced read-only). The sticks are the fuel and they break straight lines, and every one of them is something
the Swordfish answers differently:

| In the heap | What the Swordfish does |
|---|---|
| a plain stick | the blade cuts it (fuel), and the hull rams it for free (no fuel): cut a lane and wind up in it |
| a shielded jack knot (I2+) | two strokes: the first pops the shield |
| a red (danger) tip (I3+) | ramming it debuffs every element for 4 s (the fleet's danger debuff) |
| ironwood (I4, super-shielded) | binds a blade that is not energized, dragging the turn to 30%; an energized blade with Shield Breaker (Space 5) pops it |

The heap thins as the match goes on (cut sticks do not grow back), so the late game has longer lines and less fuel.
The crystals floating in the clearings are where a struck (silenced) pilot refuels.

The vessel is untouched (the LOCKED mode rule). The mode's levers are the heap, the crystals and the AI.

## 2. What is reused, and the seams

- **`RiposteController : JackstrawController`.** The round, the crystals laid on every peer, the server winner, the
  snapshot with the breakdown and the replay are Jackstraw's. Two seams were added to Jackstraw for Riposte, neither
  changing Jackstraw's behaviour: `HintSituation` (its own hint) and the AI seam (`ArmAI`, `DisarmAI`, `TickAI`,
  `AIArmed`, with `Settings`, `Heap()`, `ArenaCentre` and `NearestLiveCrystal` readable by a subclass).
  `JackstrawHunter.ChooseRival` is a shared static both modes' pilots call, and `SpawnableJackstraw.HeapRadius`
  publishes the heap's size. *Affects: the Thresher's Jackstraw (Shombith's, seams only).*
- **`RiposteScoringRuleSO : JackstrawScoringRuleSO`**, Kelpwood's pattern: a Strike pays its points, every other class
  nothing, every line an authored template; Riposte's words are on its own asset.
- **`RiposteSettings.asset` is a `RiposteSettingsSO : JackstrawSettingsSO`**: Jackstraw's fields (the crystals, the
  rival choice, the lead; miss radius 24 / 14 / 6 / 0 u by AI level because a bill leads the hull) plus the duelists'.
- **`RiposteStrikeTurnMonitor`**: the shared combat-point monitor on `EndConditionOverridesSO.riposteStrikeTarget` (6).
- The scene is Jackstraw's, cloned with the identity, the settings and rule, the monitor, fresh Netcode ids and four
  Swordfish AI seats.

## 3. The AI (`RiposteDuelist`, `RiposteDuel`; Plan step: charge-arcade)

**What was wrong (measured 2026-11-25).** Under Jackstraw's hunter the four AI hulls flew at 30 u/s (the platform's 0.6
skill throttle on a 50 u/s hull), met in the middle and circled each other. The ramp never wound up, because the
hull's own ramp policy presses only inside 12 degrees with 1.5 s of runway and brakes for its objective. Whoever was a
hair faster at the first touch struck, the victim was silenced, and the 0.5 s latch let the rest land: 36 matches of
36 ended, but in 10-63 s (median 21 s), the loser on a median of 2 strikes, and Jade won 27 of 36.

**The duelist** flies three long straight legs, chosen by pure functions (`RiposteDuel`, held by `RiposteDuelTests`):

| Leg | Flies to | Ends when |
|---|---|---|
| **Reap** | the far side of the heap, up to 35 degrees off the line through its centre: the bill cuts sticks on the way | the tank holds the rung's share (a line that ends short is followed by another) |
| **Charge** | through the led rival (off by the rung's miss) and 150-450 u past them | the rival is behind the nose inside 240 u, or the hulls are inside 80 u and this one is not the faster, or 6 s |
| **Extend** | straight out 120-420 u, inside the arena | it gets there or 6 s; then Reap if the tank is under the rung's share, else Charge |

Fuel is checked between legs only: a charge in flight is never abandoned for sticks. Silenced, it runs for a crystal
by the rung's chance, as Jackstraw's hunter does.

- **Input only, vessel untouched.** The duelist hands `AIPilot` a steering point and a throttle floor
  (`SetExternalTargetProvider`, `SetThrottleFloor`). The sword is swung by the hull's own hand
  (`SwordfishSwordAutopilot`). The ramp is pressed by the hull's own boost policy, in its Riposte tuning:
  `Resources/AIBoostPolicies/SwordfishAIBoostPolicy_Riposte` (`AIBoostPolicySO.ForMode`): straight inside 30 degrees,
  0.6 s of runway, and a corner speed above the ramp's top so it never brakes before its rival. The generator fails if
  the duelist presses an input, changes a resource or reads the intensity.
- **By AI level** (Easy / Medium / Hard / Legendary; `RiposteSettings.asset`): throttle floor 0.7 / 0.85 / 1 / 1; cuts
  for fuel under 0 / 0.03 / 0.06 / 0.1 of a tank and until 0 / 0.08 / 0.15 / 0.25 (Easy never cuts on purpose);
  charge overshoot 150 / 250 / 350 / 450 u; run-out 120 / 220 / 320 / 420 u. Hand-set from the judge, not a search
  (Froglet Compute is paused); they are the seed a `charge-arcade` search grows from.

## 4. Ladder

The heap's own (`JACKSTRAW.md` §2): loose sticks, then jacks, then red tips, then ironwood, 408 / 1,122 / 2,114 /
3,566 prisms, with the platform's standard element ladder on top (I2: every hull starts at Charge 5, so Sword Steal is
on; I3: team Charge crystals; I4: Charge fauna). The target is the same 6 strikes at every rung.

Real-game judge (`Tools/Compute/evaluators/regatta_judge.py`, mode `Riposte`, Amoebius headless, four shipped Swordfish
AIs 2v2, seeds 1-2, working tree on c519b8bc03, 2026-11-25). Seconds to the sixth strike, two matches a cell:

| AI level | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Easy | 113 / 150 | 68 / 95 | 165 / 85 | 202 / 198 |
| Medium | 107 / 49 | 109 / 84 | 162 / 88 | 133 / 75 |
| Hard | 64 / 77 | 96 / 97 | 152 / 73 | 108 / 125 |
| Legendary | 97 / 39 | 103 / 26 | 71 / 52 | 179 / 40 |

Easy, Medium and Legendary: 24 of 24 matches ended with a winner; median 96 s; the loser's median score 3 of 6;
Jade won 11, Ruby 13. Hard was judged later the same day on 7d9e3ed2b9: 8 of 8 ended, the loser on 2-5, Jade won 6. A hull's
speed in a match: median 45-117 u/s, 90th percentile 125-290, peak 165-540 (the old hunter: 30 u/s throughout).
Read it as: the AI finishes every rung, a harder AI tends to win sooner, and I4 is the longest rung for an Easy seat.
Two matches a cell cannot rank I1-I3 against each other: the spread inside a cell is as wide as the gap between rungs.

## 5. Verification status

- **Headless:** `author_riposte_assets.py --check` (30 files; watched failing on a mutated settings value);
  `bash Tools/Build/riposte_harness/run.sh --self-test` (the shipped `RiposteDuelTests`, 14 of 14, five negative
  controls fire); the static gates; the whole of `Assets/_Scripts` compiled by the Amoebius live build with these files.
- **Real game, no Unity:** the table in section 4. The judge needed two engine changes: `RaceJudge` takes
  `"mode": "Riposte"` (and `Jackstraw`) as a combat-point game, and `FixedString128Bytes.CopyFromTruncated`, without
  which the Amoebius build of this branch failed.
- **2026-11-25, Unity 6000.3.17f1 (the open editor):** the project compiles with these files, `RiposteDuelTests`
  pass 14 of 14 in the editor's test runner, and the settings asset and the ramp variant load as their types. The mode
  smoke test played Riposte once (I1, three Legendary AI seats 2v1, clock 4x): scene loaded, all three seats AI and
  moving, a winner in 30.5 game seconds, no error or exception in the match. The run is still marked failed for
  three errors outside the match: two at boot (a command of mine that timed out, and the Cloud Code `NotFound` other
  modes' runs show) and one at exit (`NetworkCrystalManager.RespawnCrystal_ServerRpc` called after the network
  stopped, when an omni crystal was touched during teardown; platform code, not Riposte's). One 2v1 match at 4x
  is not a time to compare with the judge's 2v2 table.
- **The same day, I2-I4 at clock 1x, four AI seats 2v2, one match a cell** (the mode smoke test on a copy of its
  config; seconds to the sixth strike): Legendary 25 / 35 / 67, Hard 20 / 33 / 21. Every match ended with a winner,
  four seats AI and moving, no error in the match or at exit; the only error is the Cloud Code `NotFound` at boot.
  **Finding:** the editor's matches are shorter than the judge's for the same cell (judge Hard 73-152 s on I2-I4,
  Legendary 26-179 s). One match a cell cannot say why; the judge's lengths are not yet shown to be Unity's.
- **Not done:** no I1 match in the editor at clock 1x; nobody has flown it; 3v3 and three-domain matches were not judged; no search has tuned the duelist.
- **Open questions a flight answers:** whether 6 strikes is the right length with a person in it (AI matches run
  26-202 s); whether a silenced pilot under a 0.5 s sword latch can get away (the AI now runs out after each pass, a
  person may not); whether the heap, cut 140-2,800 prisms a match, still gives fuel late on.
- Card art is rendered (`CardBackgrounds/Riposte.png`).
