# Palisade - the Swordfish's Mass game

> **Status.** Authored headless on 2026-11-15 (`claude/swordfish-game-kit`), for Garrett's ask to build every missing
> elemental game, on the ids `Docs/ElementalLoop/GAME_KIT_ROSTER.md` reserved for it (row 8: `GameModes` 98, hint toast
> 173). **Nothing here has been run in the Unity editor, and nobody has flown it.** Reviewed on 2026-11-25 for the
> Swordfish's voyage kit: where it stands is section 7, and the Mass-Space merge that will replace it is section 8.
> The same day it got an arena of its own (section 9): picket lanes, frames and rubble, shaped by intensity.

`GameModes.Palisade = 98` is the **Swordfish's Mass card**: walls you swing into place (`VolumeRemaining`, timed).
Its Space card is Cleave, its Time card Spearline (96) and its Charge card Riposte (97).

## 1. Why this game, for this hull

The Swordfish's **Mass** ability is **Afterimage** (`SwordfishSwordAfterimage`, `RHINO_SWORD_AFTERIMAGE.md`): for 5 s
after the blade LEAVES the energized state it leaves prism copies of itself where it swings (up to 16 a window), Mass
scales their volume x1.7 -> x4.25 (x1 -> x2.5 until 2026-11-21), and at Mass 5 (**Danger Afterimages**) they are laid as danger prisms. The same blade
cuts any rival prism it reaches (a shielded one in two strokes).

So Palisade is a **timed wall war**: the score is the prism volume a domain has STANDING when the clock runs out (the
Swordfish's trail and its afterimages). The sword both builds and raids, and the mode's swing is that the two happen in
one move: an energized run through a rival's wall cuts it down, and the afterimages fall as the blade cools, right
where it was. At Mass 5 the walls are danger, so a rival who rams through to cut them pays the 4 s debuff.

The vessel is untouched (the LOCKED mode rule). The mode's levers are the arena (section 9), the crystals and the AI.

## 2. What is reused

- **Tapestry's shape** (the Butterfly's timed mass war, `TAPESTRY.md`): the bare Barren cell (no food web of its own
  at I1-I3; at I4 the platform's element ladder adds Mass fauna, section 4), the peer-local crystal scatter (a Mass
  crystal is a bigger wall), the timed monitor as the only end, the server lead sampler, the snapshot carrying volume
  and prisms.
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

### 3.2 The AI takes its team's Mass crystals (2026-11-25)

From I3 the platform lays each team its own Mass crystals (section 4). A building AI detoured only for the mode's
scatter, so it flew past them and played I3 exactly as it played I2. `TryNearestCrystal` now also reads the ladder's
crystals its own team can take (`Crystal.Active`, `ElementLadderCrystals.IsLadderCrystal`, `Crystal.CanBeCollected`,
the gate race AI's test), within the same `aiCrystalReach`. Steering only, as before. Not compiled or run yet
(section 6).

## 4. Ladder

Four rungs, from two sources. The card authors no `StartingElements` row and the game reads as Mass
(`VolumeRemaining`, `ModeGenre`), so the platform's standard element ladder applies (`ElementLadder`,
`Docs/ElementalLoop/PLAYBOOK.md` step 2); on top of it the mode thins its own scatter (Tapestry's scarcity).
`PalisadeLadderTests` reads each of those facts off the shipped assets.

| Rung | The platform adds | Mode scatter | What it asks of the Mass ability |
|---|---|---|---|
| I1 intro | nothing: Mass at rest | 24 crystals | Afterimages are plain walls, x1.7 volume. Learn that the blade leaves them as it cools. |
| I2 fun | every hull starts at Mass 5 | 16 | Danger Afterimages from the first swing, about x3 volume: a wall now punishes the rival who rams it. |
| I3 challenging | 4 team Mass crystals a team, regrowing after 12 s | 10 | Mass can be pushed toward 10 (x4.25), but only by the team that keeps visiting its own crystals. |
| I4 very hard | Mass fauna in the cell's colour | 6 | The Barren cell has a nucleus, so by `Cell.IsPreyForHerbivore` walls outside it are grazed and walls inside it are not: where you swing them starts to matter. |

The third source is the arena, which trades help for cost rung by rung (section 9).

Read from the code and assets, not observed: nobody has seen I2's danger walls, I3's crystals or I4's fauna in this
scene. I4 is the rung to watch. Tapestry kept fauna out of this cell because a grazer can eat the trailing team's mass;
with the nucleus the diet is colour-blind outside it, which should be fair, but the AI's building orbit (540 u) may sit
outside the nucleus and lose its walls. If so the fix is the mode AI's orbit at I4, not the fauna.

## 5. Comeback

Sized against a volume read off the shipped Swordfish (`author_palisade_assets.py` prints the model): the trail block on
`Swordfish.prefab` at half the 840 u/s ramp, plus one window of Mass 0 afterimages every 15 s, for a domain of two who
build half the round. A quarter of that behind buys 1.5 element levels; the generator asserts at least 1.

**What the score is made of, measured (2026-11-27).** The generator's model above says the trail is about 189 vol/s
and Afterimage only 22% of the score at I1. **That is wrong.** Counted in the real game code (the Brood Rush judge in
Amoebius, headless, every prism standing at the whistle split by the trail that laid it), Afterimage is **88-91% of
every pilot's standing volume on every rung**: at I1 a seat holds about 13,000, of which about 1,600 is trail. The
trail is laid but is small and is cut; the walls are what stands. So the game already scores the Mass ability, and no
scoring change was made (the merge in section 8 would replace one anyway). Why the model's trail rate is about ten
times too high is not known; the comeback rate it sets still holds, because the measured domain volume at I1 (about
26,000) is the model's reference (26,410).

## 6. Verification status

- Authored headless: `author_palisade_assets.py --check` (25 files match, 2026-11-25), the reference compile and the
  static gates of the first PR.
- Card art is rendered (`CardBackgrounds/Palisade.png`).
- **Compiled and tested in Unity (2026-11-27, the open editor, 6000.3.17f1):** no compile errors, every Palisade type
  loads, `PalisadeLadderTests` 7 of 7 and `PalisadeArenaTests` 11 of 11 pass, and `PalisadeSettings` loads with its arena
  prism. **Played all-AI in Unity at clock 1x, I1-I4 (2026-11-27):** each rung ran its full 90 s match with 4 Legendary
  Swordfish AIs, all 4 seats moving, a winner called, no errors in the match (the checklist). Nobody has flown it. The hand tests are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`,
  "Palisade: the Swordfish's Mass game" and "Palisade's arena".
- **2026-11-25 changes are not compiled in Unity**: the AI's team-crystal detour (3.2), `PalisadeLadderTests`, and
  the arena's laying and lane runs (`PalisadeController.Arena.cs`, section 9). The reference compile does not run on
  the Windows machine they were written on, and no Unity or Amoebius run was made.
- The arena's LAYOUT is compiled and tested headless: `bash Tools/Build/palisade_harness/run.sh --self-test`
  compiles the shipped `PalisadeArena.cs` and `PalisadeArenaTests.cs` with Roslyn and runs them (11 tests, 14
  negative controls, all firing).

## 7. Where it stands (2026-11-27, elemental loop steps 1-3)

| Step | State | What is missing |
|---|---|---|
| 1 fun, one intensity | built, never flown | A person's fun card. Measured, Afterimage is about 90% of the score (section 5). |
| 2 I1-I4 | built, each rung distinct in assets and in the judge (7.1) | Nobody has seen a rung in this scene; a person's sign-off on all four. |
| 3 AI on I1-I4 | re-judged headless with the arena, 2026-11-27 (7.1) | Done: one all-AI editor match per rung at 1x, 2026-11-27 (the checklist). A person's flight. |

### 7.1 The re-judge (2026-11-27, Plan step: mass-arcade)

`brood_judge.judge(mode="Palisade")`, Amoebius headless on this checkout, four Legendary Swordfish AIs in two teams of
two, seeds 1-3 with the arena (and, for the effect of the arena, the same with `arenaPrism` overridden to none).
Every seat of every match played the full 90 s (48 of 48 with the arena, 48 of 48 without).

| Mean seat | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| VC + VD, with the arena | 18,422 | 25,146 | 26,753 | 26,722 |
| VC + VD, bare cell | 15,378 | 22,540 | 22,880 | 20,853 |
| VD (cut), with the arena | 1,181 | 1,707 | 2,531 | 1,839 |
| Standing at the whistle, with the arena | 13,757 | 17,495 | 15,528 | 12,469 |
| Afterimage share of what stands | 87.9% | 91.0% | 90.7% | 89.5% |
| Elemental crystals taken (seeds 1-2) | 5.1 | 4.1 | 2.6 | 1.8 |

The arena raises every rung's VC + VD (12-28%) and doubles what is cut at I3. The rungs read apart for the AI: I1 is the
low-Mass round, I2 and I3 the most walls, and I4 the least left standing (its rubble, tips and fauna), with crystals
taken falling with the scatter. The AI's team-crystal detour (3.2) works: they take crystals on every rung. VC and VD
are the volume books, which also count arena prisms cut, so VD includes the arena. 2,016 judged pilot-seconds; nothing
here was played in the Unity editor.

## 8. The Mass-Space merge (`Docs/ArcadeRestructure/PLAN.md`, Garrett 2026-11-17)

Each vessel's Mass and Space games are to become ONE Mass-Space game on Brood Rush's rule (fauna born under your
nucleus claim score your team), in teams of up to three, in an environment that learns from the old games' I1-I4
environments. The Swordfish is in the first group. For Palisade that means:

- **Goes:** this card, the standing-volume rule, the 90 s clock and the bare arena. Palisade has no environment of its
  own for the merged game to learn from; Cleave's cage does.
- **Stays:** the sword hand trained here (3.1, it is the vessel's AI), the build-or-raid policy and the team-crystal
  detour as the AI's Mass half, and the ladder (the merged game uses the same standard rungs).
- **So:** work that only polishes this scoring rule is likely thrown away; work on the AI and on how the Afterimage
  reads is kept. Nothing was merged here: Palisade and Cleave are still two cards.

## 9. The arena (Yash, 2026-11-25)

"The environmental objects and shapes are placed so they give an advantage or a disadvantage and also look good."
Palisade played in the bare Barren cell; it now has a place of its own. Three pieces, each there for what the
Swordfish's kit does with it (`Palisade/PalisadeArena.cs`, pure; `PalisadeController.Arena.cs` lays it):

- **Picket lane.** A straight row of thin posts (8 x 8 x 34 u, 30 u apart) across the cell's radius, 640 u out. A pilot
  runs beside it, 14 u to its outer side, so the blade (24 u at rest) reaches every post and the hull touches none.
  Slicing them fuels the ramp and lights the sword, so the afterimage window opens as the row ends. A shielded post
  takes two strokes; a missed one is a ram.
- **Frame.** 90 u past each row's last post, on its axis: a hoop of 12 shielded stones round a 60 u mouth, each stone
  long down the lane so the hoop is a collar with depth. An afterimage wall laid in it is flanked by shielded mass, so
  a raid has to come through the mouth. A broken frame keeps its outer arc and loses the side facing the cell's
  centre, where the traffic is; from I3 its two ends are danger spikes.
- **Rubble.** Loose chunks (14-22 u) hashed into the shell between 600 and 950 u, at least 90 u from every lane and
  60 u from each other. Shielded, and a third of them danger at I4. They break a ramp line and cost a stroke or a
  debuff.

| Rung | What stands (prisms) | Helps the Swordfish | Costs it |
|---|---|---|---|
| I1 | 3 lanes of 14 plain posts, 3 closed frames (78) | 540 u of straight fuel a lane, the blade lit at a closed frame: teaches Afterimage | Nothing |
| I2 | 3 lanes of 10, every 5th post shielded; frames three quarters closed (57) | 420 u a lane, cover on three sides | 6 shielded posts; a raid can enter the frame from the centre's side |
| I3 | 2 lanes of 8, every 3rd shielded; half frames with danger tips; 10 rubble (38) | 360 u a lane, half cover | 4 shielded posts, 4 danger tips at the mouth, 10 shielded chunks in the ramp lines |
| I4 | 2 lanes of 6, every 2nd shielded; third frames with danger tips; 18 rubble, 6 danger (38) | 300 u a lane, a sliver of cover | Half the posts shielded, tips, 18 chunks: where a wall goes is the read (and the fauna graze outside the nucleus) |

Counted from the shipped C# by `palisade_harness/run.sh --census`.

**Rules it keeps.**
- Laid on every peer the same way: the layout is a pure function of the rung, every draw a hash of an integer, so no
  message is sent. It is laid when the countdown ends, with the crystal scatter, because the intensity is synced by
  then. So it appears at GO, not behind the loading screen (a look question for a person, section 6).
- **It counts for no team.** Every prism is `Domains.Blue`, the platform's no-team colour, with owner id
  `PalisadeArena`: `StatsManager.PrismCreated` credits a roster name, and this is none, so no team's standing volume
  includes it. Blue is hostile to every blade (`StatsManager.IsFriendlyEnvironmentPrism`). Cutting it does book VD in
  the volume books (`VolumeLedger`), which Palisade's score does not read but the Brood Rush judge does: a re-judge
  will read higher for that reason alone.
- Colours are the Blue domain's own materials from the worn colour set; no colour is authored here. The prisms are the
  lit environment prism (`SpawnablePrism.prefab`, on `PalisadeSettings.arenaPrism`), none thinner than 8 u.
- The vessel is untouched. The generator fails if an arena source mentions an input, an element scale or a vessel
  status, or lays in any colour but Blue.
- Nothing regrows. A lane is fuel once; its frame stands until someone cuts it (two strokes a stone).

**The AI uses it (the watch item of section 3).** A building AI used to orbit empty space and lay only trail, because
the sword hand lights near hostile prisms and there were none. It now runs the lanes (`TryLaneTarget`): to a lane's
run start, beside the row to its far end, out through the frame, then the next lane; seat N starts on lane N. Each leg
is given up after 16 s. Crystals still come first, and a raid is unchanged. Steering only.

**Not known yet.** Nothing here has been seen in the game. In particular: whether the rows read as pickets and the
frames as gates from a pilot's eye; whether the AI's blade really takes the posts from 14 u; whether a raid
(`Cell.GetExplosionTarget`, the densest mass not of the raider's colour) now aims at the arena's plain posts instead
of a rival's walls; whether 80 neutral prisms move the cell's phase or the I4 fauna's spawn threshold; and the card's
background, which still shows the bare cell and needs `/cardart` re-run.
