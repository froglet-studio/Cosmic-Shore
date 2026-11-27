# The six-vessel matrix: Squirrel, Sparrow, Dolphin, Swordfish, Thresher, Tiger Moth

**2026-11-27. Yash's current focus: complete these six vessels.** Garrett's elemental loop still covers every vessel
(`CLAUDE.md`, top priority); this is Yash's focus inside it, not a change to it.

Read from the shipped files on `origin/Ys-bleeding-edge` at `57268fb3f3`, not from memory. Nothing was
run in Unity. "Unconfirmed" means the files I read did not settle it. The Swordfish is audited in more depth in
[`SWORDFISH_COMPLETION.md`](SWORDFISH_COMPLETION.md) (2026-11-26; its closing note lists what landed since).

"Complete" here means: every ability and level-5 upgrade seen working by a person, a full game kit, I1-I4 signed
off for each game, and an AI for each element.

## 1. Abilities and level-5 upgrades

Source: `Assets/Resources/ElementalAbilityMaps/<Vessel>.asset`. Every upgrade unlocks at level 5 and relocks below 4.
"Gate" is the `IsUpgradeActive` call that switches the upgrade on (paths under `Assets/_Scripts/`).

### Squirrel

| Element | Ability | What the element scales | Level-5 upgrade | Gate |
|---|---|---|---|---|
| Charge | Crystal Joust: overtake a creature's crystal or a rival pilot while faster | lowers the joust speed, 90 u/s to 45 u/s at level 10 | Shepherd: jousting your own domain's creature nourishes it | wired: `VesselWitherLifeformByCrystalEffectSO.cs:74`, `SquirrelVesselWitherLifeformByCrystalEffect.asset` `allyUpgradeElement: 1` |
| Mass | Boost Ring: a ring of prisms ahead | shortens the cooldown, x1 to x0.5 | Twin Rings: a second ring | wired: `SquirrelTubeActionExecutor.cs:164` |
| Space | Steal: skim a prism to take it | grows the skimmer, 15 to 30 | Iron Grip: shielded prisms are stolen and keep their shield | wired: `SkimmerStealPrismEffectSO.cs:32`, `SkimmerStealPrismEffect.asset` `superStealElement: 3`, in the Squirrel's skimmer container |
| Time | Skimming: skim prisms for boost | skim energy up to x2, cruise a fifth faster | Live Wire: danger prisms pay x10 | wired: `SkimmerBoostPrismEffectSO.cs:72`, `SkimmerBoostPrismEffect.asset` `dangerBonusElement: 4`, same container |
| Omni | "bursts into a ring of shielded blocks" | - | - | - |

### Sparrow

| Element | Ability | What the element scales | Level-5 upgrade | Gate |
|---|---|---|---|---|
| Charge | Skyburst Rockets, stocked by breaking hostile prisms | widens the prism blast | Domain-Safe Skybursts | wired: `FireGunActionExecutor.cs:305` |
| Mass | Turret Stance: stop and fire prisms | stretches the fired prisms; they swell x3 to x6 in flight | Shielded Prisms | wired: `FullAutoBlockShootActionExecutor.cs:211` |
| Space | Pulsefire Cannons | the map says range; **unconfirmed** (the codex table says the range is fixed and Space widens the skimmer) | Piercing Bullets | wired: `FullAutoActionExecutor.cs:211`, `FullAutoBlockShootActionExecutor.cs:195` |
| Time | Afterburner: hold to boost, one roll a press | boost speed; the roll snaps (half the time at 10) | Elemental Ward: no debuffs while boosting | wired: `VesselElementalImmunity.cs:136`, `Sparrow.prefab:1628` `upgradeGate: 4` |
| Omni | "reloads its missiles" | - | - | label likely stale: the codex table says the crystal now wards for 8 s and rockets reload from prisms. **Unconfirmed** |

### Dolphin

| Element | Ability | What the element scales | Level-5 upgrade | Gate |
|---|---|---|---|---|
| Charge | Echo Sight: hold to light what the next blast will destroy | blast thickness, x0.75 to x1.5 | Pilot Echo: the sight shows vessels too | wired: `EchoSightActionExecutor.cs:373` |
| Mass | Crystal Seeding: passive, a crystal seeded on a timer | recharge 15 s, half at 10 | Claimed Seed: seeds are team-locked | wired: `DeployTeamCrystalActionExecutor.cs:284` |
| Space | Echo Obliteration: the cone blast on a crystal | reach, x2 at 10 | Clean Blast: spares your own domain | wired: `VesselExplosionByCrystalEffectSO.cs:212` |
| Time | Drift Charge: drift to bank boost | bank fill rate, x1 to x2.5 | Drift Ward: no elemental debuff while drifting | wired: `VesselElementalImmunity.cs:136`, `Dolphin.prefab:1832` `upgradeGate: 4` |
| Omni | "triggers its blast" | - | - | - |

### Swordfish

Full audit: [`SWORDFISH_COMPLETION.md`](SWORDFISH_COMPLETION.md) section 1.

| Element | Ability | What the element scales | Level-5 upgrade | Gate |
|---|---|---|---|---|
| Charge | Energize: hold both triggers to light the blade | petals a sword hit takes | Sword Steal | wired: `VesselOvertakeBySkimmerEffectSO.cs:256` |
| Mass | Afterimage: the cooling blade leaves prism copies | afterimage volume | Danger Afterimages | wired: `SwordfishSwordAfterimageConfigSO.cs:91` |
| Space | Blade Reach | blade 24 to 40 | Shield Breaker | wired: `ShieldSkimmerScaleConfigSO.cs:197` |
| Time | Quickdraw | hold 1 s to 0.4 s, cooldown 5 s to 2 s, swipe recovery 0.35 s to 0.14 s | Flurry | wired: `SwordfishShieldSwipeConfigSO.cs:142` |
| Omni | Surge | - | - | - |

### Thresher

| Element | Ability | What the element scales | Level-5 upgrade | Gate |
|---|---|---|---|---|
| Charge | Wrecking Ball: passive; red at smash speed | lowers the smash speed, x1 to x0.6 (`ThresherConfig.asset` `smashSpeedScale`) | Magnet: the free ball is drawn to the nearest rival | wired: `ThresherExecutor.cs:325` |
| Mass | Mass Mode: the ball builds, steals and shields | trail prism size, x1 to x2 | Armoured Trail: laid shielded | wired: `ThresherExecutor.cs:327` |
| Space | Winch: chain rigidity; chain kills bank blast energy | largest blast, x4 to x8 diameter | Breaker: a red ball breaks super-shields | wired: `ThresherExecutor.cs:329` |
| Time | Plant: brake, pivot and launch | spin-up rate and cap, x1 to x1.75 | Anchor Ward: danger prisms cannot drain a planted Thresher | wired: `ThresherExecutor.cs:334` |
| Omni | "windmills the ball round the ship" | - | - | - |

### Tiger Moth

| Element | Ability | What the element scales | Level-5 upgrade | Gate |
|---|---|---|---|---|
| Charge | Scale Dust: in Dust mode the dust steals a rival's petals | shred rate, 1 a second to 10 a second | Toll Gate: your wormhole strips rivals who thread it | wired: `TigerMothDropActionSO.cs:164` |
| Mass | Mass / Dust Mode: Mass mode paints a wide wake | wake width, x5 to x20 at Mass 15 | Gilded Wake: Mass mode lays shielded prisms | wired: `SpreadWingsActionSO.cs:99` |
| Space | Dust Reach: the dust works on prisms | capsule length | Diamond Dust: sometimes raises a super-shield on your plain mass | wired: `SkimmerScaleDustPrismEffectSO.cs:196`, `ButterflyScaleDustPrismEffect.asset` `superShieldUpgradeElement: 3` |
| Time | Drop: hold to fall through everything | recharge, half at Time 10 | Afterburn: 3 s incorporeal with a boost after the drop | wired: `TigerMothDropActionSO.cs:156` |
| Omni | **GAP: the map has no `omniAbilityLabel`** | - | - | the codex table calls it "Explosion" (a dust bloom), from `BUTTERFLY.md` |

All 24 upgrades are wired through `IsUpgradeActive`. None is recorded as seen working by a person in the files read.

## 2. Games

Cards: `Assets/_SO_Assets/Games/ArcadeGame*.asset`. Every card runs I1-I4 and is locked to its vessel. "Domains" is
min-max teams. Game docs: `Assets/_Scripts/Controller/Arcade/<GAME>.md`. Loop marks from
`Docs/ElementalLoop/STATUS.md` (steps 1 fun, 2 intensities, 3 AI): `D` done, `b` built, `.` todo, `R` redo,
`x` no game, `-` excused.

### Squirrel - loop: Time `b b b`, Charge `b b .`, Mass `x x x` (kit missing), Space excused

| Element | Game (id) | Seats / domains | Objective and scoring | Environment I1 / I2 / I3 / I4 | AI |
|---|---|---|---|---|---|
| Time | Skim Race (33) | 1-12 on the card (the doc says 1-4) / not set | collect every ring first; race time, golf | flat octagon / tilted undulating loop / dumbbell circuit / "Relativity", five lobes, with garden ruins | `SkimRaceAIConfig`, trained, model only, editor check pending |
| Charge | Joust (34) | 2-12 / 2+ | joust points for overtaking rivals; golf | **unconfirmed**: `JOUST.md` describes no change by intensity | **unconfirmed**; loop step 3 is todo |
| Mass | none | | | | |
| Space | excused | | | | |

### Sparrow - loop: Time `b b b`, Charge `b b .`, Mass `x x x`, Space `b b .`

| Element | Game (id) | Seats / domains | Objective and scoring | Environment I1 / I2 / I3 / I4 | AI |
|---|---|---|---|---|---|
| Time | Breakwater (50) | 2-9 / 2-3 | fourteen walled stations in order; time, golf | port 72 / 60 / 50 / 42 u, sharper corners each rung | `SparrowAIBoostPolicy_Breakwater`, trained, model only; pace by difficulty set 2026-11-17; the course rungs do not slow the AI |
| Charge | Dog Fight (41) | 2-9 / 2-3 | guns 1 point, rockets 50; first domain to the target | the Boneyard: 9,043 / 16,100 / 24,807 / 34,654 prisms | hand-set lead pursuit; not editor-verified |
| Mass | none | | | | |
| Space | Salvo (44) | 2-9 / 2-3 | first domain to destroy 700 hostile prisms; team rocket reload | the same Boneyard configs | platform default, hand-set; not editor-verified |
| Space | Wildlife Liberation (40) | 1-9 / 2-3 | first domain to the kill count | three cages: 9,206 / 12,696 / 13,244 / 13,956 prisms, openings shrink (outer 251 u to 67 u) | hand-set room patrol; not editor-verified |

### Dolphin - loop: Time `D b b`, Charge `b b .`, Mass excused, Space `b b b`

| Element | Game (id) | Seats / domains | Objective and scoring | Environment I1 / I2 / I3 / I4 | AI |
|---|---|---|---|---|---|
| Time | Switchback (45) | 2-9 / 2-3 | gates in order, lead runner; time, golf | mouth 72 / 48 / 32 / 20 u; hairpins from I2; Blast Run walls, bore 30 / 26 / 22 / 18 u, movers from I2; I4 gauntlet | trained drive, promoted 2026-11-08, model only; fun signed by Garrett 2026-11-13 |
| Charge | The Bends (42) | 2-9 / 2-3 | catch a rival pilot in the blast; three bends win | Rampage's forest, same four configs | hand-set drift aim; not editor-verified |
| Space | Rampage (2) | 1-9 / not set | first domain to smash 2,000 rival prisms (card text) | forest density x5.00 / x3.67 / x2.33 / x1.00, crystals 2 a player / 1 each / one fewer / exactly one; I4 is the play-tested arena | trained on the volume books 2026-11-21 |

### Swordfish - loop: `b b b` on all four elements

| Element | Game (id) | Seats / domains | Objective and scoring | Environment I1 / I2 / I3 / I4 | AI |
|---|---|---|---|---|---|
| Time | Spearline (96) | 4 / 2-3 | five laps, sealed rings count only through a cut hole; time, golf | seals a lap 2 / 3 / 4 / 5, armour none / 120 / 180 / 240 deg; garden: fuel hedges at I1, pillars and danger by I4, ruins at I4 | trained boost policy + coach; judged headless; raced in Unity before the garden |
| Charge | Riposte (97) | 4 / 2-3 | six strikes win; golf | the Lists: 480 / 1,110 / 2,090 / 3,410 prisms | `RiposteDuelist`, hand-set; judged headless; a human seat the AI holds now duels too (`126073453e`); not watched |
| Mass | Palisade (98) | 4 / 2-3 | most standing volume when the clock ends; re-judged 2026-11-27: Afterimage is about 90% of the score (`07ada7c6bf`) | picket lanes, frames, rubble: 78 / 57 / 38 / 38 arena prisms, more shielded and danger each rung; crystals 24 / 16 / 10 / 6 | sword hand trained 2026-11-19; re-judged headless with the arena 2026-11-27; never run in Unity |
| Space | Cleave (39) | 4 / 2-3 | first domain to cut 300 / 400 / 550 / 600 prisms (`a7a530d779`, aimed at a 90 s match); golf | Panes / Swell / Cage / Twistbands, with lances | ran all four rungs in Unity at 1x on the OLD targets (280 / 208 / 161 / 152 s); not re-run on the new ones |

### Thresher - loop: Time `b b b`, Charge `b b .`, Mass `b b .`, Space `b b .`

| Element | Game (id) | Seats / domains | Objective and scoring | Environment I1 / I2 / I3 / I4 | AI |
|---|---|---|---|---|---|
| Time | Thresher Time (66) | 2-9 / 2-3 | two laps of swinging rings, lead runner; time, golf | the Belfry: hard corners 0 / 1 / 2 / 3 of 8, mouth 96 / 72 / 58 / 46 u, maces, ruins at I4 | `ThresherAIBoostPolicy_ThresherTime`, trained, model only; cruise pace with slings on sharp corners |
| Charge | Jackstraw (85) | 2-9 / 2-3 | a strike is a point; first domain to 3; golf | stick heap: 408 / 1,122 / 2,114 / 3,566 prisms, danger tips and shielded sticks from I2 | `JackstrawHunter`, hand-set per level, nothing trained; not run in Unity |
| Mass | Haul (93) | 1-9 / 1-3 | most volume in your colour when the clock ends | bales: 640 / 896 / 2,478 / 3,186 prisms, shielded crust from I3, omni crystals 4 / 3 / 3 / 2 | hand-set steering (haul or raid); not run in Unity |
| Space | Harvest (88) | 1-9 / 2-3 | first domain to destroy 750 hostile prisms; golf | sheaves: 4,058 / 4,129 / 4,448 / 4,561 prisms; shields from I2, team Space crystals at I3, danger spokes and fauna at I4 | `HarvestReaper`, hand-set per level, nothing trained; not run in Unity |

### Tiger Moth - loop: Time `R b .`, Charge `b b .`, Mass `b b b`, Space `b b b`

| Element | Game (id) | Seats / domains | Objective and scoring | Environment I1 / I2 / I3 / I4 | AI |
|---|---|---|---|---|---|
| Time | Waystation (58) | 2-9 / 2-3 | ring clusters joined by Drop hops; time, golf | rings a cluster 3 / 4 / 5 / 6, mouth 56 / 50 / 44 / 40 u, longer hops; ruins at I4 | trained 2026-11-17 (151-323 s); loop step 3 still todo; **the vessel's Time is flagged for redesign** |
| Charge | Dustup (59) | 2-9 / 2-3 | 3.2 petals shredded is one dusting; first domain to 10; golf | Dog Fight's Boneyard configs | hand-set hover hunt; not opened in the editor |
| Mass | Tapestry (60) | 2-9 / 2-3 | most standing volume at the clock (90 s) | bare cell; crystals 24 / 16 / 10 / 6 | trained on the volume books 2026-11-21; not opened in the editor |
| Space | Sirocco (61) | 1-9 / 2-3 | first domain to destroy 600 hostile prisms; golf | Rampage's forest: 295 / 217 / 137 / 59 plants | searched 2026-11-21, settings unchanged; the target is never reached in a 200 s 1v1 |

Only the Swordfish has an AI recorded on all four elements. Only one step for these six is signed off by a person:
the Dolphin's Time fun pass (Garrett, 2026-11-13).

## 3. What is left, per vessel

Each list is in order. "Editor" needs Unity (one run at a time, on Yash's go). "Person" needs someone to fly or decide.

**Squirrel**
1. Decide the Mass game (Garrett or Yash): a new Squirrel Mass arcade game, or an excuse. Scurry is now a Party game and counts for no kit.
2. If a game: build it with `/arcadegame` (Boost Ring is the Mass verb). No editor to author; editor to verify.
3. Find or build Joust's intensity ladder and its AI (both unconfirmed). Loop Charge step 3.
4. Write Joust's card description (it reads "Joust joust joust"). A game asset; Yash's go.
5. Person flies each ability and upgrade, then Skim Race and Joust at one intensity (fun cards), then I1-I4.
6. Editor check of the trained Skim Race AI.

**Sparrow**
1. Decide the Mass game: Turret Stance is the Mass verb and no card uses it.
2. Settle the two map questions: does Space scale cannon range, and is the omni label "reloads its missiles" still true.
3. AI for Charge (Dog Fight) and Space (Salvo, Wildlife Liberation) through loop step 3; both are hand-set and unjudged.
4. Person flies abilities and upgrades, then fun cards for Breakwater, Dog Fight and one Space game, then I1-I4.
5. Breakwater: the course rungs do not slow the AI, so the ladder for a person is unmeasured.

**Dolphin**
1. Time step 2: fly Switchback I1-I4 (fun is already signed).
2. AI for Charge (The Bends) through loop step 3.
3. Person flies abilities and upgrades, fun cards for The Bends and Rampage, then I1-I4.
4. Editor check of the trained Switchback and Rampage AIs.

**Swordfish**: the ordered list is in [`SWORDFISH_COMPLETION.md`](SWORDFISH_COMPLETION.md) section 6. First: compile, then one all-AI match per rung in Spearline, Riposte and Palisade, and Cleave again on its new targets.

**Thresher**
1. Compile and run Jackstraw, Haul and Harvest in the editor: none has been run in Unity.
2. One all-AI match per rung in each, at clock 1x.
3. AI for Charge, Mass and Space through loop step 3 (all hand-set; no judge exists for Jackstraw or Harvest).
4. Person flies abilities and upgrades, four fun cards, then I1-I4. Targets (3 strikes, 750 prisms) are unplayed guesses.
5. Editor soak of the trained Thresher Time AI.

**Tiger Moth**
1. Time redesign (Garrett, 2026-11-18): the vessel cannot be brought inside the Regatta field. Blocks Time steps 1-3.
2. Add `omniAbilityLabel` to `TigerMoth.asset`. A game asset; Garrett's `/approve`.
3. Open Dustup, Tapestry and Sirocco in the editor: none has been opened, and their C# has had no type check.
4. AI for Charge (Dustup) through loop step 3. Sirocco's 600 target is never reached by the AI.
5. Three cards say "Butterflies", one says "Tiger Moths": pick one name. Game assets; Yash's go.
6. Person flies abilities and upgrades, fun cards, then I1-I4.

## 4. Gaps and corrections

Gaps found (not changed: they are game assets or need a decision):

- `TigerMoth.asset` has no `omniAbilityLabel`.
- `ArcadeGameJoust.asset` description is "Joust joust joust".
- `ArcadeGameDustup/Tapestry/Sirocco.asset` say "Butterflies"; `ArcadeGameWaystation.asset` says "Tiger Moths".
- `ArcadeGameSkimRace.asset` seats 1-12; `SKIMRACE.md` says 1-4 players.
- Sparrow: the map and the codex table disagree on what Space scales and on the omni crystal.
- Squirrel Mass: the map says the ring is "conserved prism mass"; the codex table says "danger prisms". Unconfirmed.
- The published Fleet Codex is stamped 2026-11-13 (`bleeding-edge` at `27ee35db8`), two weeks behind.

Docs corrected in this change:

- `Docs/FleetCodex/abilities.json`: Dolphin Time was "Charge Fill Rate" (the map says Drift Charge) and Swordfish Time
  was "Ramp Spool" (the map says Quickdraw). The generator drops an entry whose name differs from the map, so both
  were missing from the page. Thresher Charge said Charge steals more petals; the asset says it lowers the smash speed.
- `Docs/FleetCodex/README.md` and `Docs/ElementalLoop/GAME_KIT_ROSTER.md` called Scurry the Squirrel's Mass game; the
  playbook (Garrett, 2026-11-17) makes Scurry a Party game and the board shows the Squirrel's Mass as missing.
- `Assets/_Scripts/Controller/Arcade/RIPOSTE.md` section 5 said the Lists were not compiled by Unity; the checklist
  (`ba00cb40a1`) records that the open editor compiled them. Another session corrected it first (`d3649dd000`), so
  this change does not touch it.
