# Swordfish: what is left before it is a complete vessel

Read-only audit, 2026-11-26, of `origin/Ys-bleeding-edge` at `2a42ec5d0c`. Nothing was run in Unity for this report.
Every claim names the file or commit it rests on. "Unconfirmed" means I could not check it from the files.

States: **verified** = seen working by a person in the game; **built** = in the tree, tests or AI runs only;
**missing** = not in the tree.

**Short answer.** Nothing about the Swordfish is verified by a person. All four abilities, all four level-5 upgrades
and all four games are built and wired. The loop board shows `b b b` for every element and no `D`
(`Docs/ElementalLoop/STATUS.md` line 30). What is truly missing is small: sound slots for the abilities, fresh card
art, baked layouts for Riposte's arena, and a set of decisions. The rest is flying it.

Owner: the Swordfish kit is Garrett's (`SWORDFISH.md`, approvals in `Docs/Approvals/Garrett/`). The four games' recent
work is Yash's.

## 1. Abilities and level-5 upgrades

Source: `Assets/Resources/ElementalAbilityMaps/Swordfish.asset` (labels, descriptions, unlock 5 / relock below 4).

| Element | Ability | Scales with the element | Level-5 upgrade | Gate in code | Gate in asset | State |
|---|---|---|---|---|---|---|
| Charge | Energize | petals a sword hit takes | Sword Steal | `VesselOvertakeBySkimmerEffectSO.cs:256` | `SwordfishSwordStealBySkimmerEffect.asset` `stealUpgradeElement: 1`, in `SwordfishForceFieldSkimmerImpactorDataContainer.asset` | built |
| Mass | Afterimage | afterimage volume | Danger Afterimages | `SwordfishSwordAfterimageConfigSO.cs:91`, used at `SwordfishSwordAfterimage.cs:172` | `SwordfishSwordAfterimageConfig.asset` `dangerUpgradeElement: 2` | built |
| Space | Blade Reach | blade 24 to 40 | Shield Breaker | `ShieldSkimmerScaleConfigSO.cs:197`, used in `ShieldSkimmerScaleDriver.cs:185-187, 447` and `SwordfishSkimmerDamagePrismEffectSO.cs:130` | `ShieldSkimmerScaleConfig.asset` `superShieldUpgradeElement: 3` | built |
| Time | Quickdraw | hold, cooldown and swipe recovery | Flurry | `SwordfishShieldSwipeConfigSO.cs:142`, used at `ShieldSwipeActionExecutor.cs:444` | `SwordfishShieldSwipeConfig.asset` `flurryUpgradeElement: 4` | built |
| Omni | Surge | - | - | `SwordfishVesselSurgeByCrystalEffectSO.cs`, `SwordfishSurgeSwordRing.cs` | `SwordfishVesselSurgeByCrystalEffect.asset` | built |

- All four upgrades go through `IsUpgradeActive` (the replicated bit), none reads a raw level.
- Edit-mode tests exist for Quickdraw, the afterimage, the Surge ring, the HUD, the combos and the bill pose
  (`Assets/_Scripts/Tests/Editor/Swordfish*Tests.cs`). No test names Sword Steal, Danger Afterimages or Shield Breaker
  on the Swordfish (only `SwordfishQuickdrawTests` and `RhinoLevelFiveKeysTests` touch an upgrade gate).
- **Never seen in play.** `SWORDFISH.md` "In-editor verification" (7 steps) and four checklist sections are still
  marked not editor-verified: `Docs/UNITY_VERIFICATION_CHECKLIST.md` lines 1025, 1044, 1897, 1950, and 25213 (the rename).
- Open kit question for Garrett (`SPEARLINE.md` section 6): Quickdraw does nothing in a race, because recovery starts
  on release and a held stroke never meets it.

## 2. HUD, icons, sound, text

| Item | State | Rests on |
|---|---|---|
| HUD variant, 14 icon parts wired | built | each `AbilityIcons/Swordfish/Parts/*.png` guid appears once in `SwordfishHUDVariant.prefab`; `SwordfishVesselHUDView.cs` |
| Surge (omni) icon | built | `Core/Swordfish_Surge.png`, one reference in the HUD prefab |
| The four composed icons (`Swordfish_Energize/Afterimage/BladeReach/Quickdraw.png`) | referenced by no asset | `git grep` of their guids: none. May be by design (the HUD draws parts); unconfirmed whether the hangar's upgrade page needs them |
| Authored text: labels and descriptions, ability and upgrade | built | the ability map asset |
| Sound: 24 combo slots | placeholder | all point at `event:/SFX/Oneshots/Gameplay sfx/Skim`; listed in `Docs/AudioSystem/sound_placeholders.json` with targets `SFX/Vessels/Swordfish/Combo ...` |
| Sound: drift release | placeholder | same ledger, target `SFX/Vessels/Swordfish/Drift release` |
| Sound: energize, swipe, afterimage, Surge, shield break, steal | empty slots (2026-11-27) | `energizeEvent`/`surgeEvent`/`shieldBreakEvent` on `ShieldSkimmerScaleConfigSO`, `swipeEvent` on `SwordfishShieldSwipeConfigSO`, `afterimageLaidEvent` on `SwordfishSwordAfterimageConfigSO`, `stealEvent` on `VesselOvertakeBySkimmerEffectSO`; silent until filled; ledger targets `SFX/Vessels/Swordfish/...`; not yet compiled in the editor |
| Engine sound | Rhino's | `Swordfish.prefab:1379` `space ship engine main Rhino` |
| Class card, hangar art, model | built | `SO_Class_Swordfish.asset`, `Swordfish_Hangar_Preview.png`, `Swordfish.fbx` |
| Crystal hull fusion bakes | Rhino's, stale | `SWORDFISH.md` Follow-ups |
| Vessel Studio page | missing | no Swordfish page under `Docs/Studios/VesselStudio/` |

## 3. The four games

Registrations checked for all four and present: `GameModes` id, `EndConditionOverridesSO` and its window,
`MiniGameHUD` objective provider, scene in `EditorBuildSettings`, card in `ArcadeGames.asset`, scoring rule, turn
monitor, toast config in the library, mode preview, generator, card background.

| | Spearline (Time, 96) | Riposte (Charge, 97) | Palisade (Mass, 98) | Cleave (Space, 39) |
|---|---|---|---|---|
| Card seats | 2 to 4 (`MinPlayersAllowed: 2`) | exactly 4 | exactly 4 | exactly 4 |
| Own cell configs | none (unconfirmed which cell it uses) | 4 | none (the Barren cell, per `PALISADE.md`) | 4 + spawn profile |
| Environment by intensity | garden I1-I4 (`fd5145d403`), ruins at I4 | the Lists (`0f31da4d4c`) | lanes, frames, rubble (`a63579354a`) | four arenas + lances (`5269ddfdef`) |
| Compiled in Unity | yes, 2026-11-23, before the garden; garden unconfirmed | arena and lane AI: yes per `ba00cb40a1` (the doc's section 5 still says no) | **no** for the 2026-11-25 changes | yes |
| Unity tests | `SpearlineSealsTests` 11/11; garden tests not run in Unity | `RiposteDuelTests` 14/14; yard and lane tests not run in Unity | none run | `CleaveLadderTests` 20 pass |
| AI match in Unity | with the garden, 4 AI seats, clock 1x: I1 129.6 s, I2 133.1 s, I3 118.0 s, I4 140.6 s, all ended with a winner; fps mean 56-57 (min 32 at I2); no garden part missing; 33 of 33 Spearline edit-mode tests pass in the editor; one boot error each (`[LaunchFlags] ... NotFound`, the backend flags, not Spearline) | I1 at 4x and I2-I4 at 1x, all in the OLD arena and with a wrong seating; none in the Lists | none | all four rungs at 1x, 280/208/161/152 s (`2beef51681`) |
| Headless judge | 24 of 24 finished with the garden | 24 of 24 (old arena), 64 matches in the Lists | 2026-11-19 only, before the arena | not Cleave's score |
| Flown by a person | no | no | no | no since the Swordfish took it |
| Card art | 2026-11-23, older than the garden | 2026-11-23, older than the Lists | 2026-11-23, older than the arena (`PALISADE.md` section 9 asks for a re-render) | 2026-11-23, without lances |
| Toasts | 9 | 6 | 3 | 5, copy "first-pass" |

Known problems, per game:

- **Spearline.** The ladder is flat: Legendary I1-I3 within 4%, and Medium I1 is its slowest rung
  (`SPEARLINE.md` 2b). `spearline_race_model.py --self-test` fails on purpose: five seals at I4 pass the fun bar,
  waiting on a person (section 2). The four editor races with the garden never ran (editor crash). The card's minimum
  is 2 where the other three are 4.
- **Riposte.** The Lists have no baked layout and no clip audit (the tools are Linux-only), so the game lays them live
  with a warning. A 2v2 of four mode AIs has never been played in Unity: the smoke test seats three teams and gives
  the host seat the stock autopilot, which flies slow and is the duelists' preferred target (section 5). 3v3 and
  three-domain matches are not judged. The duelist is hand-set, no search.
- **Palisade.** Never compiled or run in Unity since the arena, the crystal detour and `PalisadeLadderTests` landed.
  The Mass ability is a minority of the score on every rung (22% at I1, at most 41% at I3; section 5), so the game
  teaches Afterimage less than it should. The AI judge is from before two changes (afterimages x1.7, team crystals).
- **Cleave.** The AI finishes every rung but I1 and I2 take two to three times the 90 s aim. 2v2 or three teams is
  undecided. No UGS stats reporter and no end-game controller of its own (`CLEAVE.md` follow-ups). Nobody has watched
  the AI or the lances.
- **Palisade and Cleave both** are due to merge into one Mass-Space game (`Docs/ArcadeRestructure/PLAN.md`, Garrett
  2026-11-17). Work on their scoring and end-game is likely thrown away; arenas and AI carry over.

## 4. AI

| Element | AI | State | Rests on |
|---|---|---|---|
| Time | `SwordfishAIBoostPolicy_Spearline` + `SpearlineController.Coach.cs` | built, judged headless, raced in Unity before the garden | `loop.json` Swordfish Time 3 |
| Charge | `RiposteDuelist` + `SwordfishAIBoostPolicy_Riposte`, lane-reading (`ff55fb013b`) | built, judged headless, not watched | `loop.json` Charge 3 |
| Mass | sword hand + build-or-raid, lane runs | built, judge out of date | `loop.json` Mass 3, `PALISADE.md` 3, 7 |
| Space | sword hand + stations, crystal turns | built, slow at I1-I2 | `loop.json` Space 3, `CLEAVE.md` |

No AI is signed off. No person has watched any of them.

## 5. Loop sign-offs

`Docs/ElementalLoop/loop.json` holds five Swordfish entries, all "built": Time 1 (Garrett, the ramp redesign), Time 3,
Charge 3, Mass 3, Space 3 (Claude). There is **no entry** for Time 2, or for steps 1 and 2 of Charge, Mass and Space;
the board fills those as `b` on its own. No fun card is answered for any Swordfish game. Step 0 (the game kit) is
complete: four games exist. Arenas: Regatta and Brood Rush seat it; steps 4 and 5 are todo.

## 6. TODO, in order

"Editor" = needs the Unity editor (one run at a time, on Yash's go). "Person" = someone must fly it or decide.

| # | What | Files | Done when | Editor | Person |
|---|---|---|---|---|---|
| 1 | Compile the branch and run the Swordfish edit-mode tests | whole project; `Spearline*`, `Riposte*`, `Palisade*`, `CleaveLadder*`, `Swordfish*` tests | 0 compile errors; every test passes; results in the checklist | yes | Yash's go |
| 2 | One all-AI match per rung in the three games not yet run in their new environment, at clock 1x | Spearline (garden), Riposte (the Lists, four mode AIs 2v2), Palisade (arena) | 12 matches end with a winner and no match error; times in each game's doc | yes | Yash's go |
| 3 | Decide the open questions | - | answers written in the docs | no | **Garrett:** seats per mode (queued); Quickdraw in a race; when Palisade and Cleave merge. **Yash:** Cleave 2v2 or three teams; Spearline I4 four or five seals; Spearline card minimum 2 or 4; lower Cleave's I1-I2 targets or change its AI |
| 4 | Fly the vessel in the Toybox Vessel Changer: each ability, then each level-5 upgrade at level 5 | `SWORDFISH.md` "In-editor verification"; checklist lines 1025, 1044, 1897, 1950, 25213 | each of the 4 abilities and 4 upgrades seen working; checklist sections closed | yes | a person flies |
| 5 | Fly each game at one intensity and answer its fun card (loop step 1) | the four game docs; `fun_cards.py apply` | four fun cards recorded in `loop.json` | yes | a person flies |
| 6 | Fix what steps 3-5 show, then fly I1-I4 of each game (loop step 2) | Spearline's flat I1-I3; Palisade's score (trail outweighs Afterimage); Cleave's pace | each rung is harder than the last for a person; step 2 signed in `loop.json` for all four | yes | a person flies |
| 7 | Make Riposte's AI fill a human seat it holds, as Switchback does | `RiposteController.cs`, `RiposteDuelist.cs` | an idle or AI-flown human seat flies the duelist | compile only | Yash decides (open in `RIPOSTE.md` 5) |
| 8 | Re-judge the Palisade AI; decide and fix the Cleave AI's pace; watch each AI once | `brood_judge.py --mode Palisade`; `CleaveController` AI | step 3 signed for all four elements | yes for watching | a person watches |
| 9 | Add empty sound slots for energize, swipe, afterimage, Surge, shield break and steal, and list them | Swordfish executors and configs; `Docs/AudioSystem/sound_placeholders.json` | each has a serialized `EventReference`; the ledger names its target | compile only | Garrett's `/approve` (kit files); real sounds need the sound person |
| 10 | Bake Riposte's arena layouts and run the clip audit | `bake_prism_layouts.py` (Linux only) | four baked prefabs; no live-lay warning | no (needs Linux) | no |
| 11 | Re-render the four card backgrounds | `/cardart`, `Assets/_Graphics/ARCADE/CardBackgrounds/` | each card shows its current arena | no | no |
| 12 | Add tests for Sword Steal, Danger Afterimages and Shield Breaker on the Swordfish | `Assets/_Scripts/Tests/Editor/` | a test fails when each gate is removed | yes (to run) | no |
| 13 | Smaller left-overs: own crystal hull fusion bakes, own engine sound, toast copy read in play, a Vessel Studio page (optional), UGS stats reporter for Cleave | `SWORDFISH.md` Follow-ups, `CLEAVE.md` follow-ups | each closed or dropped by a person | mixed | Garrett / Yash |

Steps 1 and 2 need no flying and unblock everything else. Steps 4 to 6 are the real remaining work and only a person
can do them.

## 7. Landed since this audit (checked 2026-11-27 at `57268fb3f3`)

- TODO 7 done in code: a human seat the AI holds duels in Riposte (`126073453e`). Not run in Unity.
- TODO 11 half done: Riposte's and Cleave's card backgrounds re-rendered (`81fb470183`, `1a30c0aff0`). Spearline's and
  Palisade's are still from 2026-11-23.
- TODO 12 done: `SwordfishLevelFiveGateTests.cs` (`447518919d`). Not run in Unity as far as the commit says.
- Spearline's card now seats exactly 4 (`9ae4252f26`, `934537256a`).
- Cleave's targets are 300 / 400 / 550 / 600 (`a7a530d779`); the AI times in section 3 are on the old targets.
- Palisade re-judged headless with the arena: Afterimage is about 90% of the score, not 22% (`07ada7c6bf`). So the
  "trail outweighs Afterimage" problem in section 3 no longer stands.
- `RIPOSTE.md` section 5 now says the Lists compile in Unity (`d3649dd000`).

The six-vessel view is in [`SIX_VESSEL_MATRIX.md`](SIX_VESSEL_MATRIX.md).
