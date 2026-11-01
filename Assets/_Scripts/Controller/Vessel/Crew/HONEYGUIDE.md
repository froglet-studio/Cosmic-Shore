# The Honeyguide and Ratel

A two-seat crew vessel on the Sparrow's airframe, with its own hull model: `VesselClassType.Honeyguide = 23` (21 until the
2026-10-21 bleeding-edge merge gave 21 to the Starling). It carries *Slipstream*, the tabletop game
Garrett and friends designed and played from 2019 to 2022. The design was made and decided in the Honeyguide Lab
(`Docs/Studios/HoneyguideLab.html`, write-up `Docs/Studios/HONEYGUIDE_LAB.md`, pattern list
`Docs/Studios/HONEYGUIDE_PATTERNS.md`). The lab is the record. This port is its clone in the game, as of lab round 6.

- **The Honeyguide** flies the hull. It uses the Sparrow's airframe and numbers. Its two triggers are its own (round 6,
  below), and its ride is the **Slip**. Touch a 1-D trail of a domain the battery holds, with the nose within `aimCone` (45°) of the
  prism's long axis either way. The hull then **zips** along the trail to the end it points at (`HoneyguideCrew.Zip.cs`:
  `zipSpeed` 480 u/s, ridden `zipLift` above the prisms, `zipExitBoost` out; Garrett 2026-10-21: "not teleport ... a
  satisfying zip over"). That end is clipped to `slipReach`, and the hull leaves `exitOffset` past it, aligned with the
  trail. The slip spends that domain's charge.
- **The Ratel** plays the **Dynamo**, a hex puzzle (`DynamoCore`):
  - Every 15 s (a shrinking bar across the top) the injection ports turn 60° and the spin gives 3 AP. What is not spent is
    kept, up to 6 (round 7, Garrett 2026-10-29; it was 3, so every spin was use-it-or-lose-it).
  - The Ratel moves prisms, injects at ports and makes patterns. Each costs 1 AP.
  - A made pattern sends one of its prisms, drawn at random, to the battery, where its domain is a slip charge. It also
    fires that card's own effect.
  - A pattern made three times is replaced.
  - Every 4 patterns the Ratel is **offered three abilities and picks one** (round 7, Garrett 2026-10-29), then sees the
    new port pattern. The clock stays stopped while it picks; after `pickSeconds` (10 s) the first offer is taken. The AI
    Ratel picks by `DynamoCore.AIPickOrder` (Overclock, Refund, Free Fire, Long Reach, Capacitor, Wild Core, Swap, Mirror:
    a proposal). `abilityChoices` 1 brings back the random draw.
  - **Printer is retired from the draw, for now** (Garrett 2026-10-22: its free prism at the start of every spin "is too
    confusing to players on mobile"). The config's `retiredAbilities` lists it, and the lab's `RETIRED_ABILITIES` holds the
    same list (`author_honeyguide_assets.py --check` fails if they differ). To bring it back, empty both lists. The cards
    that grant free prisms (Universal Translator, 5D Printer) still do.

## Try it

1. Open the Toybox and use the **Vessel Changer**. Pick **Honeyguide and Ratel**.
2. Fly it. Ram trails of the domains the battery holds, with the nose along the trail, to slip.
3. Press **D-pad left / right** or the **left / right arrow keys** (a two-finger swipe on a phone), double-tap the small board, or tap **Fly** (left of the Ratel's big board) to swap seats; past the Ratel the gesture goes on to the next hull (`Docs/CREW_STATIONS.md`). A second person can play the Ratel from their own machine while you fly: their moves are applied to your board and the board is sent back to them. The AI takes the empty seat:
   - the hull's own `AIPilot` flies while you play the Ratel;
   - the Ratel AI (the lab's `aiPlan`, input-only, Hard by default) plays while you fly.
4. To play the Ratel (the board covers the centre of the screen; **on a phone, turn the phone to portrait**):
   - On a phone the board assumes the phone is held portrait (Garrett 2026-10-22). The screen stays locked to
     landscape, so the board is drawn a quarter turn round and reads upright with the phone upright. Its text
     (`portraitTextMillimetres`, 2.8 mm), cards (`portraitCardMillimetres`, 20 mm tall) and Fly button (above the board, at
     its left) are sized in millimetres for a thumb, inside `portraitRect` of the safe area. `boardOrientation` picks
     PortraitOnPhone (the default: portrait on a touch-first device, landscape elsewhere), Portrait (also for trying it in the
     Editor) or Landscape. The small board while flying stays landscape.
   - Drag a prism along empty cells to move it (1 AP a step).
   - Tap a port to inject its colour (1 AP).
   - Tap a lit card to make its pattern (1 AP).
   - When three abilities are offered, tap one to take it (the clock waits).
   - Right-click a prism to delete it (1 AP); on a phone, hold the prism still (`longPressSeconds`, 0.45 s). Right-click a card to
     read its details; any click closes them (a phone cannot open card details yet).
   - Free prisms (Printer, Translator, 5D Printer) each come in a random domain and ride the cursor until placed: click an
     empty cell.
   - The Ratel's ability (at the start and each new one) and every pattern made are announced in the game's toast, top
     right under Settings (`ToastNotificationAPI`), worded for both seats.
   - Making a pattern has juice (Garrett 2026-10-23): its cells burst one after another in the card's family colour, the
     card pops and the board's edge flashes (`madeJuiceSeconds`, 0.7 s). The prism it takes lifts off its cell and flies on
     an arc to its battery slot, turning from the board's hex prism into the battery's round charge, and lands with a ring
     (`chargeFlightSeconds`, 0.55 s; `HoneyguideMesh.HexToDisc`). Both boards draw it, the small one while flying too.
   - The band between the board and the cards always says what the last pattern made does (`madeLineTemplate`), until
     the next one. It is **lime** (`dynamoLineColour`) when the effect works on the Dynamo itself - its prisms, ports, AP,
     spin clock or battery (`HoneyguideConfigSO.TouchesDynamo`: Amanita Furnace, Big Red Button, Dragon Saddle, Zero Point
     Amplifier, Triplesynth, Universal Translator, Anode Ray, 5D Printer, Square-Peg Hammer, Reactor Cilia,
     Chrono-Compressor, Meta Machine, Mystic Eti, Vorticity Module), cream otherwise. While a free prism rides the cursor
     the free-prism line takes the band, in lime.
5. **Hive mode:** fly the Honeyguide into an Omni crystal (or let its AI). The Ratel's board becomes the hive and a comb of
   prisms appears behind the hull. Drag 3 or more touching cells of one other colour and let go; touch danger and it stings;
   tap **Leave** to end early. The rewards land on the board by themselves.
6. **RT** pecks and **LT** flits. When the Ratel makes an upgrade card, the toast says which trigger it took, and the chip
   above the small board shows it with the seconds it has left.

## Hive mode (Garrett, 2026-10-31; the Ratel Raid Lab round 2b)

"the omnicrystal transforms into a hive. the ratel and the hive are invulnerable until the ratel leaves hive mode, the ratel
enters hivemode after the honeyguide gets an omnicrystal, and the ratel ends their current timer." The design was played and
decided in the Ratel Raid Lab (`Docs/Studios/HoneyguideRaidLab.html`, record `Docs/Studios/HONEYGUIDE_RAID_LAB.md`); its
`hive-core` block is the record, `HiveCore.cs` is its port, held to it by `Tools/Build/hive_lab/parity.cjs`.

- **Opening it.** An Omni crystal the hull collects (`HoneyguideOmniCrystalEffectSO`, in the omni list of the hull's own
  `HoneyguideImpactorDataContainer`) opens hive mode on the machine that runs the Dynamo, once any pick of three in progress
  is done. The current spin completes at once (`hiveCompletesSpin`; `DynamoCore.SpinNow`), and the Dynamo's clock holds.
  One hive at a time: an omnicrystal taken in hive mode opens none.
- **The hive in the world.** The omnicrystal becomes a flat hex comb of real prisms, `hiveOffset` (40 u) behind where it was
  taken, square to the hull's flight, `hiveCellSpacing` apart: each cell's domain, every prism super-shielded while hive
  mode lasts. Ours belong to the pilot, the other domains' to nobody. **Mass:** the hive's prisms are laid by a vessel
  ability (the Wrasse's Mass cluster is the precedent); this is a source of mass, so it is flagged for Garrett.
- **Invulnerable.** The hull holds elemental-debuff immunity (`ResourceSystem.SetElementalDebuffImmunity`) until the Ratel
  leaves hive mode; the strike gate (`CombatHitDrain.TryAdmit`) and the petal loss honour it, and with them the silence a
  hit would bring. The hive is super-shielded.
- **The board.** The comb: the two other domains in their colours, a white ring on a shielded prism, danger in
  `hiveDangerColour` ringed by its domain, our own domain dimmed as the hive's body. Drag a chain through 3 or more
  touching cells of one other domain (back over the last cell drops it); let go to break it. A shield takes two passes; a
  chain that touches danger stings (it breaks with nothing destroyed, and the thumb is stunned `stingSeconds`). The hive's
  real prisms break with their cells, scored to the hull. **Leave** sits at the right of the band above the cards; the band
  shows the rewards earned; the bar across the top is the hive's time left (`raidSeconds`, 15 s).
- **Every hive is solvable.** `HiveCore.MakeHive` lays the other domains as snakes (a crossing of two snakes is a shielded
  cell), puts at least `hiveDangerMin` danger prisms off every snake, holds all three domains and a shield in each, and the
  solver proves a danger-free solution before the hive is used.
- **Rewards, no pick.** Completing a colour earns 2 prisms of it on the Dynamo (`rewardDynamo`) and 1 charge (`rewardBattery`);
  both colours earn our own too. When hive mode ends (cleared, timed out, or Leave) the prisms place themselves on the
  Dynamo's largest planar crystal (every pattern is cut from the hex grid's one 3-colouring, class (q - r) mod 3), which
  lights for `crystalGlowSeconds` with the cells it goes on to. A fresh spin starts.
- **After.** The super shields come off and what is left of the hive stays as ordinary mass: shields where a cell still had
  two hits, danger where it was danger.
- **The AI Ratel** plays it too: `hiveSkills` by `engineerLevel` (Hard plays the solver's plan and never touches danger; Easy
  is the lab's rookie: greedy chains, sometimes cut short, a sloppy thumb). Medium is between them (a proposal).
- **A rider** (the Ratel played on another machine): hive moves travel as relay inputs, and the hive rides in the state copy.
- **The strip.** A thin strip over the Ratel's big board: the Honeyguide, the nearest omnicrystal along it by distance
  (`stripRangeU` at the far end, from `Crystal.Active` four times a second) and the distance in u (`stripOmniTemplate`). In
  hive mode it shows the hive under the hull, ringed (invulnerable).
- **The bars shrink from both sides to the centre**, the spin bar and the hive's alike.

- **Bees guard the left hive** (Garrett 2026-10-31): when hive mode ends, a native bee hive (`BeeHiveFlora`, the
  pollinator fauna, `Docs/ECOSYSTEM.md` §66) is planted in our domain `leftBeeHiveBehind` (12 u) behind the comb, from the
  config's `leftBeeHive` (Bee Hive Flora Mass). Its bees guard the comb and grow their hive from mass stung off opposing
  fauna (a transfer, never minted). The comb's own prisms are trail prisms, not a plant's, so they stay as our mass beside
  the hive instead of becoming its comb. A client's hull plants none (a networked flora is planted by the simulation's
  authority): a follow-up for a hull flown on a client.

**Not in this port:** the Honeyguide's
Mass power making a hive to fly to (it changes the element map: Mass is Flit on LT today, and what happens to Flit is
Garrett's call). The strip shows omnicrystals only until then.

## Very Low: one view, the Ratel first (Garrett, 2026-10-22)

"if the lowest graphics setting are selected don't display pips. start the player in the wrasse and the ratel. in this
setting very little of the game should even need to be processed, so the ratel should run fine on very old phones."
On the Very Low preset (`CrewSingleView`, `Docs/PLATFORM_UNIFICATION.md` §3.10):
- the person starts on the Ratel at every bind, once the hull is theirs with live input (`HoneyguideCrew.ApplyStartSeat`,
  the Wrasse's rule);
- playing the Ratel, the world behind the board is not drawn: the game camera is parked (`PilotCameraPark`, a plain
  backdrop in the board's panel colour, no post-processing or shadows), so the board's canvas is nearly all a frame
  costs. The hull's AI still flies and the match still runs;
- flying the Honeyguide, there is no small board (and no trigger chip with it): a thumb-sized **Ratel** button sits in its
  corner instead, the Fly button's twin, so a phone can always get back to the Ratel. The D-pad and the arrow keys still swap.

## Where things are

| Part | File |
|---|---|
| Rules and AI (engine-free, held to the page by `Tools/Build/dynamo_lab/parity.cjs`) | `DynamoCore.cs` |
| Every number, card and word | `HoneyguideConfigSO.cs`, `Assets/_SO_Assets/VesselActions/Honeyguide/HoneyguideConfig.asset` |
| Seats, swap, Ratel AI, events | `HoneyguideCrew.cs` |
| Slip, card effects, jumps | `HoneyguideCrew.Pilot.cs` |
| The ride (slips, Flit, the jets, the lasso's reel) | `HoneyguideCrew.Zip.cs` |
| RT Peck, LT Flit, the 13 upgrades, the AI's triggers | `HoneyguideCrew.Triggers.cs` |
| The input's two triggers (the prefab maps RightStickAction and LeftStickAction to them) | `HoneyguideTriggerActionSO.cs`, `HoneyguideRTAction.asset`, `HoneyguideLTAction.asset` |
| Peck (the hull's own gun at 2.5 shots a second, 45 u) | `HoneyguidePeckAction.asset` (a `FullAutoActionSO`) |
| The board (runtime overlay canvas, touch and mouse) | `HoneyguideDynamoView.cs` |
| Prefab, class, map, registrations, config from the lab | `Tools/Build/author_honeyguide_assets.py` (`--check`, `--self-test`) |
| The hull model (its own, built by `/vessel-model`) | `Assets/_Models/Vessel Models/Honeyguide.fbx`, `Tools/Build/vessel_model/specs/honeyguide.py`, `Docs/VesselModels/Honeyguide.md` |
| Hive mode: generation, solver, chains, rewards, the planar crystal (engine-free, held to the Ratel Raid Lab by `Tools/Build/hive_lab/parity.cjs`) | `HiveCore.cs` |
| Hive mode in the seat: the omnicrystal, the hive in the world, the AI, the strip, the relay | `HoneyguideCrew.Hive.cs` |
| The omnicrystal's hook (in `HoneyguideImpactorDataContainer`'s omni list) | `HoneyguideOmniCrystalEffectSO.cs`, `HoneyguideOmniCrystalEffect.asset` |
| The model's puppetry (wings, tail, turret, from measured motion) | `HoneyguideBody.cs` |
| Edit-mode tests (golden from the lab's core) | `Assets/_Scripts/Tests/Editor/HoneyguideTests.cs`, `HiveCoreTests.cs` |

## The cards

All 39 cards keep their tabletop names. Each card's effect is the lab's conversion (`EFFECTS` in the page). The effect
text the player reads is the `effectText` field on each card in the config. Timed effects last `passiveSeconds`, and a
repeat raises their level up to `passiveMaxLevel`.

No card is parked. Thirteen of them are **trigger upgrades**, below.

## The triggers (lab round 8, Garrett 2026-11-01)

"right trigger should be a space active ability ... left trigger should be charge active ability ... charge should always
be able to attack vessels and fauna. space should always be able to reduce opposing prism volume ... the ability has a
preview when pulled and then completes when released." And: "the three uses per card is good. each completion of a
pattern should be a stored use of the ability for the honeyguide."

- **Pull to preview, release to fire.** While a trigger is held its preview is drawn in the world (world-space lines in the
  neutral blue, `HoneyguideCrew.Preview.cs`); letting go fires it. **Both triggers down at once cancels**: on a phone that
  is both thumbs lifted. A pull that would find no target draws nothing, and its release fizzles and keeps its use.
- **RT, Space**: **Peck**, a burst of `peckBurst` (3) shots through the hull's own gun (`peckAction`, now 10 a second so
  the burst lasts a quarter of a second), every `peckCooldown` (1 s).
- **LT, Charge**: **Flit**, an 18 u dart that strikes every rival vessel and creature within `flitStrikeRadius` (8 u) of
  its lane, every 2 s.
- **A strike** on a rival vessel is a scored `CombatHitClass.Strike` (petals only, admitted by `CombatHitDrain`, decided
  on the shooter's owner, `GameDataSO.OnCombatHitLanded` raised); on a creature it is a joust (`Fauna.Jousted`).
- **An upgrade card banks `usesPerCard` (3) uses** on its trigger each time it is made. The bank is a stack: the newest
  banked ability is current and fires first; the same ability banked again moves to the top with its uses added. When the
  bank is empty the base power is back. The chip beside the small board shows the current ability and one `tickGlyph` a
  banked use. After any fire a trigger waits `postFireCooldown` (0.25 s).
- **No boost button.** A card's boost burns at once, as before. The prefab's Button2 boost and the AIPilot's boost are gone.
- **Mass is a passive**: every `massHiveSeconds` (45 s, faster by `massHiveLevelGain` a Mass level) a hive is planted in
  the cell at least `hiveMinDistance` (150 u) away, marked by a turning hexagonal frame (`HoneyguideCrew.MassHive.cs`).
  Flying within `hiveReach` (25 u) opens hive mode there, the omnicrystal's path. The strip counts it too.
- **Time is the Slip**, the base the others build on.

The `upgrades` table is the lab's `ACTIVES8`, held to it by `author_honeyguide_assets.py` (trigger, reach, preview):

| Card | Trigger | Preview | What the release does |
|---|---|---|---|
| Genesis Lance | RT | line 120 u | Shatters the line and lays a lane of the newest charge's domain along it |
| Little Bang Grenade | RT | landing ring | Lands 55 u ahead and bursts 0.6 s later (22 u) |
| Particle Harmonizer | RT | 20° cone, 55 u | Up to 12 hostile prisms in the cone, nearest first, turn to your domain |
| Fugacity Discharger | RT | 40 u ring | Every hostile prism inside shatters; 0.05 s of boost each (up to 1.5 s) |
| Sepia Drone | RT | 50 u ring | Zaps the nearest hostile prism three times a second, for 8 s |
| Thruster Swarm | RT | three thrusts | Three ploughing 20 u thrusts, 0.4 s apart |
| Hexapod Eyelet Jet | RT | line 50 u | A ploughing jet in a quarter of a second |
| Hard Light Lasso | LT | rope to its target | Hooks the nearest rival vessel or creature (90 u, 30°), reels in, strikes within 12 u on arrival |
| Blink Drive | LT | landing + strike ring | Blinks 60 u ahead; strikes within 12 u |
| All Terrain Portals | LT | landing + strike ring | 30 u ahead (60 u into empty space); strikes within 12 u |
| Rosen Bridge | LT | landing + strike ring | To the farthest prism of the newest charge's domain within 300 u; strikes within 12 u |
| FTL Chromatool | LT | landing + strike ring | Swaps with the nearest prism ahead within 60 u (it becomes a charge); strikes within 12 u |
| Monte Cannon | LT | every hop + ring | Three hops to the farthest prism ahead within 40 u; strikes within 12 u at every landing |

While an AI flies the hull (an AI crew, or you on the Ratel's seat) the crew pulls the triggers itself, input-only: the
lab's `wanted8`, holding each pull `aiPreviewSeconds`, with "ahead" standing in for the lab's destination (freestyle has
none). The AI pilot does not yet steer to a Mass hive (a follow-up).

## Choices made in the port (not in the lab)

- **Peck is the Sparrow's gun** at the lab's rate and range, so it is the real networked projectile.
- **The jets, Flit and the lasso's reel are rides** on the slip's rail (the transformer stands down for the ride), not
  teleports; the jumps (Blink, Portals, Rosen, FTL, Monte) are `SetPose`, as in the lab.
- **A boost effect fires at once** (`IsBoosting` for its seconds, capped at `boostCap`).
- **Fractal Frame** recolours the hull's own trail prisms as the prism controller lays them (`OnBlockSpawned`).
- **The four cards (round 8)**: Charge is Flit on LT, Mass the hive, Space Peck on RT (at Space 5 Pecks pierce, the gun's
  own Space upgrade), Time the Slip. The ability-row icons are still the Sparrow's (`SparrowHUDVariant`): a Honeyguide HUD
  variant with its own icons is a follow-up.
- **The previews and the hive marker are lines in the world**, drawn only for the pilot on this screen; their look
  (`previewMaterial`, `previewColour`, `hiveMarkerColour`) is a placeholder until the art pass.
- **No new visual effects yet.** A shattered prism shows the game's own break; the lance's lane and the harmonizer's
  recolour show as prisms. The grenade, drone, lasso rope and lance beam have no effect of their own (a team task).
- **Local only, as the Wrasse's seat is.** The Dynamo, its AI and the effects run on the machine that flies the hull.
  Another player sees the hull fly, slip and lay prisms, but not the board. Networking the Ratel seat is a follow-up.
- **FMOD:** `patternMadeSfx`, `slipSfx`, `jumpSfx`, `flitSfx`, `upgradeSfx` (now the bank), `spinSfx`, `revealSfx` and each
  upgrade's `sfx` ship empty, to be wired by the audio owner.
- **Effect numbers the lab keeps in code** (for example `lassoReelSpeed` 220, `noodleCount` 5 and `etiReach` 40) are
  fields on the config, set to the lab's values.

## The hull

The hull draws its own model, `Honeyguide.fbx` (`Docs/VesselModels/Honeyguide.md`): a bird scout with swept wings, a fanned
tail, a sensor beak and the Ratel's hex turret. It sits where the Sparrow's model sat and carries the domain paint; the
Sparrow's mesh is switched off, but its bones, jets, colliders and Animator stay, so it still flies the Sparrow's airframe.
`HoneyguideBody` poses the bones from the hull's measured motion, so every peer sees the same. Element morphs (Charge,
Mass, Space, Time) are driven by the hull's `VesselAnimation`. The class card, inactive icon, silhouette, squad image and
hangar preview are renders of the model (`render_cards.py Honeyguide`). The crystal fusion's four entries are bake-free
until the editor's baker is run.

## A whole arc for the Ratel (2026-10-29)

`Docs/Studios/ONE_THUMB_SEAT_ARCS.md` §3. In: round 7 (the bank of 6 and the pick of three) and hive mode (above).
Its first sketch of the raid (the Honeyguide finds a hostile hive and the Ratel breaks it by chaining) became hive mode.
Still open there: a partial-colour reward, the reward size, the bees and the Mass-power hive.

## Not verified in the Editor

This port was compiled against Unity's reference assemblies (player and editor configs) but not opened in the Editor.
Round 8 (2026-11-01) was NOT compiled: the session had no .NET SDK and no Editor.
The parity gate, `author_honeyguide_assets.py --check` and the vessel gates pass.
`Docs/UNITY_VERIFICATION_CHECKLIST.md` lists what to check by hand.
