# Riposte - the Swordfish's Charge game

> **Status.** Authored headless on 2026-11-15 (`claude/swordfish-game-kit`), for Garrett's ask to build every missing
> elemental game, on the ids `Docs/ElementalLoop/GAME_KIT_ROSTER.md` reserved for it (row 7: `GameModes` 97, hint toast
> 172). **2026-11-25 (Yash's Swordfish pass):** its own AI pilots and ramp tuning, measured in the real-game judge
> (Amoebius, headless) on I1-I4, then compiled, tested and smoke-played once in the Unity editor; see sections 3-5.
> The same day it got its own arena, the Lists (section 4), judged headless on I1-I4 and **not yet opened in the
> Unity editor**. **Nobody has flown it.**

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

Riposte puts that duel in a heap of **Jackstraw's pick-up sticks** (the Thresher's Charge arena, `JACKSTRAW.md`), laid
out as a jousting yard of its own (section 4). The sticks are the fuel and they break straight lines, and every one of them is something
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
- The scene is Jackstraw's, cloned with the identity, the settings and rule, the monitor, fresh Netcode ids, four
  Swordfish AI seats, and Riposte's own four cells and spawn rings.
- **`SpawnableRiposteYard : SpawnableJackstraw`**: the base's clearings and heap radius, its own pieces (section 4).

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

**It reads the Lists** (`RiposteLanes`, held by `RiposteLanesTests`; 2026-11-25). Each leg rolls the rung's
`duelLaneUse`; on a yes:

| Leg | What it does with the arena |
|---|---|
| **Reap** | flies the nearest fuel rail nobody has found cut: to its near end, then straight along it to its far end. A run that earns under 0.02 of a tank marks the rail cut for every AI for the rest of the match. With no rail left it crosses the heap as before. |
| **Extend** | runs out to the mouth (the refuel clearing) of a lane that lies within 60 degrees of its nose; with none ahead it runs straight out as before. It never turns hard to find a lane. |
| **Charge** | after a run-out that reached a lane's mouth, it flies down the lane toward the centre, winding the ramp up on the straight, until its rival is within 320 u or it reaches the centre clearing; then it turns onto the rival. |

In an arena with no lanes (Jackstraw's heap) the lists are empty and it flies as before.

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
  charge overshoot 150 / 250 / 350 / 450 u; run-out 120 / 220 / 320 / 420 u; reads the arena on 0.25 / 0.5 /
  0.85 / 1 of its legs. Hand-set from the judge, not a search
  (Froglet Compute is paused); they are the seed a `charge-arcade` search grows from.

## 4. The arena: the Lists (`SpawnableRiposteYard`, `RiposteYard`), by intensity

Yash, 2026-11-25: every piece is placed so it gives the Swordfish an advantage or a disadvantage, and the rungs differ
in how much help against how much cost. Riposte no longer plays in Jackstraw's cells. It has its own four
(`Cell Configs/Riposte Cell`), each a `SpawnableRiposteYard` prefab: a `SpawnableJackstraw` subclass (so the
controller floats the crystals in its clearings and the AI reads its radius as before) that lays its own pieces.
Jackstraw's files are read, never edited.

| Piece | Where it stands (the rule, `RiposteYard`) | What it is for |
|---|---|---|
| **Lane** | a straight tube no stick enters, from the centre clearing out through a refuel clearing | the ramp winds up only on a straight line, and a sword hit lands only on a slower hull |
| **Rail** | pickets (half sticks, 15 u apart) parallel to a lane, 6 u outside its wall, evenly round it | the bill cuts them lengthwise: ramp fuel without leaving the line |
| **Refuel clearing** | at the end of every lane, 10 u inside the heap's edge | a struck (silenced) pilot runs straight down a lane to the crystals |
| **Gate** | a jack on a lane's axis, between the two clearings, its four cross arms shielded | breaks the line: thread a quadrant, strike it twice, or go round and lose the wind-up |
| **Thorns** | red tips on every loose stick within the thorn band of a lane wall | drifting off the line rams a tip: every element debuffed for 4 s |
| **Ironwood core** | an ironwood heart in every loose stick centred inside the core radius | where the lanes meet, a blade that is not energized binds; Shield Breaker (Space 5) cuts it |
| **Scatter** | Jackstraw's loose sticks and jacks, between the lanes | fuel for a pilot who leaves the lanes, and what breaks a line taken across them |

| | Name | Heap | Lanes / refuels | Lane radius | Rails a lane | Gates | Loose sticks / jacks | Thorn band | Ironwood core | Prisms | Rail | Shielded | Danger |
|---|---|---:|---|---:|---:|---|---|---:|---:|---:|---:|---:|---:|
| I1 | The Lists | r 420 | 4 / 4 | 45 | 4 | none | 24 / 0 | none | none | 480 | 192 | 0 | 0 |
| I2 | Barriers | r 500 | 4 / 4 | 40 | 3 | 1 in every other lane | 60 / 4 | none | none | 1,110 | 204 | 46 | 0 |
| I3 | Thorn Lists | r 600 | 4 / 6 | 34 | 2 | 1 in every lane | 120 / 10 | 70 u | none | 2,090 | 216 | 94 | 28 |
| I4 | Ironwood | r 700 | 3 / 8 | 28 | 1 | 2 in every lane | 210 / 20 | 100 u | r 300 | 3,410 | 84 | 222 | 48 |

I1 mostly helps and teaches the Charge hit: four wide straights, 40% of its prisms are fuel rails, nothing shielded
or red. I4 mostly demands the read: three narrow lanes for eight refuels, 2% rails, 8% shielded or red. The
generator fails unless the lanes narrow, each lane loses a rail, the rail share falls and the shielded-and-danger
share rises on every step. Spawn ring: heap radius + 90 (510 / 590 / 690 / 790). The platform's standard element
ladder still sits on top (I2: every hull starts at Charge 5; I3: team Charge crystals; I4: Charge fauna), and the
target is the same 6 strikes at every rung.

Every draw is `Hash01` of an integer key and every rejection is geometry, so the arena is the same on every peer.
Every prism is neutral (`Domains.Blue`), drawn by the worn colour set. `Tools/Build/riposte_yard_budget.py` mirrors
the C# for the census and the placement checks; `RiposteYardTests` holds each rule.

**Real-game judge on the Lists** (Amoebius headless, four shipped Swordfish AIs 2v2, seeds 1-2, working tree on
b2818ecc1d, 2026-11-25). Seconds to the sixth strike, two matches a cell:

| AI level | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Easy | 58 / 140 | 47 / none in 300 | 122 / 86 | 150 / 133 |
| Medium | 83 / 15 | 67 / 124 | 54 / 41 | 67 / 187 |
| Hard | 116 / 87 | 35 / 158 | 156 / 151 | 136 / 71 |
| Legendary | 130 / 97 | 102 / 211 | 99 / 74 | 138 / 63 |

31 of 32 matches ended with a winner (one Easy I2 match stood 4-4 at the 300 s limit); 15-211 s, median 99 s; by
rung the median is 92 / 102 / 92 / 135 s. That table is the duelist BEFORE it read the lanes. With the lane-reading
duelist (section 3), same judge and seeds, working tree on 0f31da4d4c:

| AI level | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Easy | 114 / 161 | 102 / 100 | 127 / 96 | 240 / 287 |
| Medium | 43 / 20 | 46 / 67 | 175 / 102 | 88 / 38 |
| Hard | 111 / 81 | 190 / 38 | 104 / 112 | 218 / 151 |
| Legendary | 72 / 120 | 85 / 91 | 247 / 98 | 137 / 44 |

32 of 32 ended with a winner; 20-287 s, median 102 s; by rung 96 / 88 / 108 / 144 s; Jade 16, Ruby 16; the
loser's median score 4 of 6. Read it as: reading the lanes breaks nothing, every rung still finishes, and I4 is
still the longest. **It does not show the AI flies better for it**: match length and the score spread are the
same within two matches a cell, and the judge's per-seat counts (prisms hit, crystals) do not separate a rail run
from a heap crossing. Whether a seat is visibly on a rail or in a lane has not been watched in a match; the
rules are held by tests only. It does not show that the rungs feel different either.

The earlier table, in Jackstraw's heap (same judge, working tree on c519b8bc03 and 7d9e3ed2b9 for Hard), for
comparison:

| AI level | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Easy | 113 / 150 | 68 / 95 | 165 / 85 | 202 / 198 |
| Medium | 107 / 49 | 109 / 84 | 162 / 88 | 133 / 75 |
| Hard | 64 / 77 | 96 / 97 | 152 / 73 | 108 / 125 |
| Legendary | 97 / 39 | 103 / 26 | 71 / 52 | 179 / 40 |

## 5. Verification status

- **Headless:** `author_riposte_assets.py --check` (50 files; watched failing on a mutated settings value; the
  arena's placement checks are run against a deliberately broken arena inside the check and must fire);
  `bash Tools/Build/riposte_harness/run.sh --self-test` (the shipped `RiposteDuelTests`, `RiposteYardTests` and
  `RiposteLanesTests`, 31 of 31, fourteen negative controls fire); the static gates; the whole of `Assets/_Scripts` compiled by the Amoebius live build with these files.
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
  Legendary 26-179 s).
- **Why (three more Hard matches at 1x, every seat sampled every 3 s):** the smoke test's match is not the judge's.
  The judge seats four mode AIs 2v2. The smoke test seats three teams (the host on Jade, two Ruby, one Gold), and
  the host's seat is a human seat handed to the stock autopilot, which `RiposteDuelist` does not arm (it arms
  `IsInitializedAsAI` seats, as Jackstraw's hunter does). That hull flew at 31-47 u/s with no ramp, alone against
  a Ruby pair, and the duelists prefer a human seat (`aiHumanFocus` 2); it was silenced in 4 of 18 samples in one
  match and 3 of 10 in another. The three duelists flew as in the judge: Reap, Charge and Extend in turn, fuel
  0-1, speeds to 451 u/s. Lengths also swing match to match: Hard I3 ran 33, 94 and 32 s, Hard I2 20 and 56 s.
  So the editor times say nothing against the judge's table; a 2v2 match of four mode AIs has not been played in
  Unity, because the smoke test cannot seat one.
- **A human seat the AI holds now duels** (2026-11-27, the obvious default taken for Yash): a human's hull the
  platform autopilot flies when the countdown ends (the smoke test's host seat, an idle guest) is armed as a duelist
  like an AI seat, and a hull handed over later (PilotControl's AI-flies toggle, an idle guest the server adopts)
  is armed then (`RiposteController : IAIPilotArming`, `RiposteDuelist.ArmOne`), as Switchback's race does. It flies
  at the match's AI level and stays a human seat to the rivals' targeting (`aiHumanFocus`). Not run in Unity yet;
  whether the hull's ramp uses the Riposte boost variant when it was not spawned as an AI is not checked.
- **The Lists (the arena of section 4) and the lane-reading duelist compile in Unity** (2026-11-25, the open
  editor, ba00cb40a1: `recompile` up to date with no errors, `RiposteYard`, `RiposteLanes` and `SpawnableRiposteYard`
  load as types). **Not yet verified in Unity:** `RiposteYardTests` (10) and `RiposteLanesTests` (7) in the test
  runner (the first try aborted because another session's Play run was live), the C# prism count per rung against
  the Python census (480 / 1,110 / 2,090 / 3,410), and any match played in the Lists. Headless, the Amoebius live
  build compiles both and the judge played 64 matches in the Lists.
- **Open: the Lists have no baked layout.** `bake_prism_layouts.py` and the prism clip audit run on Linux only, so
  the four `SpawnableRiposteYard` prefabs are laid live, with a warning, until someone runs the bake on Linux.
- **Not done:** no I1 match in the editor at clock 1x; no 2v2 match of four mode AIs in the editor; nobody has flown it; 3v3 and three-domain matches were not judged; no search has tuned the duelist.
- **Open questions a flight answers:** whether 6 strikes is the right length with a person in it (AI matches run
  26-202 s); whether a silenced pilot under a 0.5 s sword latch can get away (the AI now runs out after each pass, a
  person may not); whether the heap, cut 140-2,800 prisms a match, still gives fuel late on.
- Card art is rendered (`CardBackgrounds/Riposte.png`).
