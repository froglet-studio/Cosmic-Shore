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
  - Every 15 s (a shrinking bar across the top) the injection ports turn 60° and the AP refill.
  - The Ratel moves prisms, injects at ports and makes patterns. Each costs 1 AP.
  - A made pattern sends one of its prisms, drawn at random, to the battery, where its domain is a slip charge. It also
    fires that card's own effect.
  - A pattern made three times is replaced.
  - Every 4 patterns the Ratel draws a new ability and a new port pattern. The clock stops while they are shown.
  - **Printer is retired from the draw, for now** (Garrett 2026-10-22: its free prism at the start of every spin "is too
    confusing to players on mobile"). The config's `retiredAbilities` lists it, and the lab's `RETIRED_ABILITIES` holds the
    same list (`author_honeyguide_assets.py --check` fails if they differ). To bring it back, empty both lists. The cards
    that grant free prisms (Universal Translator, 5D Printer) still do.

## Try it

1. Open the Toybox and use the **Vessel Changer**. Pick **Honeyguide and Ratel**.
2. Fly it. Ram trails of the domains the battery holds, with the nose along the trail, to slip.
3. Press **D-pad left / right** or **1 / 2** (a two-finger swipe on a phone), double-tap the small board, or tap **Fly** (left of the Ratel's big board) to swap seats; past the Ratel the gesture goes on to the next hull (`Docs/CREW_STATIONS.md`). A second person can play the Ratel from their own machine while you fly: their moves are applied to your board and the board is sent back to them. The AI takes the empty seat:
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
   - Right-click a prism to delete it (1 AP); on a phone, hold the prism still (`longPressSeconds`, 0.45 s). Right-click a card to
     read its details; any click closes them (a phone cannot open card details yet).
   - Free prisms (Printer, Translator, 5D Printer) each come in a random domain and ride the cursor until placed: click an
     empty cell.
   - The Ratel's ability (at the start and each new one) and every pattern made are announced in the game's toast, top
     right under Settings (`ToastNotificationAPI`), worded for both seats.
5. **RT** pecks and **LT** flits. When the Ratel makes an upgrade card, the toast says which trigger it took, and the chip
   above the small board shows it with the seconds it has left.

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
  corner instead, the Fly button's twin, so a phone can always get back to the Ratel. The D-pad and 1 / 2 still swap.

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
| The model's puppetry (wings, tail, turret, from measured motion) | `HoneyguideBody.cs` |
| Edit-mode tests (golden from the lab's core) | `Assets/_Scripts/Tests/Editor/HoneyguideTests.cs` |

## The cards

All 39 cards keep their tabletop names. Each card's effect is the lab's conversion (`EFFECTS` in the page). The effect
text the player reads is the `effectText` field on each card in the config. Timed effects last `passiveSeconds`, and a
repeat raises their level up to `passiveMaxLevel`.

No card is parked. Thirteen of them are **trigger upgrades**, below.

## The triggers (lab round 6)

Garrett, 2026-10-21: "the right trigger should always be a weak attack power that gets upgraded sometimes. the left
should be a movement power that works similar. the 39 patterns should still be unique."

- **RT Peck**: a weak shot while held, through the hull's own gun (`peckAction`, 2.5 a second, 45 u, no spray).
- **LT Flit**: a short throw 18 u ahead, every 2 s.
- An **upgrade card** takes its trigger over for `activeSeconds` (20 s), then the base power comes back. The same card
  again refreshes it and grows its reach by `activeLevelGain` a level, up to III. Another card on the same trigger
  replaces it. The `upgrades` table on the config is the lab's `ACTIVES`, held to it by `author_honeyguide_assets.py`.

| Card | Trigger | Use | What it does |
|---|---|---|---|
| Hard Light Lasso | RT | press | Hooks the nearest hostile prism ahead (90 u, 30°) and reels the hull in, ploughing; a burst where it arrives |
| Genesis Lance | RT | press | Shatters the line 120 u ahead and lays a lane of the newest charge's domain along it |
| Little Bang Grenade | RT | press | Lobbed 55 u ahead; bursts 0.6 s later (22 u) |
| Sepia Drone | RT | press | Zaps the nearest hostile prism within 50 u three times a second, for 8 s |
| Particle Harmonizer | RT | hold | Turns hostile prisms in a 20° cone to the newest charge's domain, 8 a second |
| Fugacity Discharger | RT | charge | Let go: shatters hostile prisms within 12 to 40 u by charge; 0.05 s of boost each (up to 1.5 s) |
| Hexapod Eyelet Jet | LT | press | A 50 u jet in a quarter of a second, ploughing |
| Thruster Swarm | LT | press | A quick 20 u thrust, again every 0.4 s, ploughing |
| Blink Drive | LT | press | Blinks 60 u ahead |
| All Terrain Portals | LT | press | 30 u ahead; landing in empty space goes 30 u again |
| Rosen Bridge | LT | press | To the farthest prism of the newest charge's domain within 300 u and 35° |
| FTL Chromatool | LT | press | Swaps with the nearest prism ahead within 60 u; it becomes a charge |
| Monte Cannon | LT | press | Three hops, each to the farthest prism ahead within 40 u |

While an AI flies the hull (an AI crew, or you on the Ratel's seat) the crew presses the triggers itself, input-only: the
lab's `activeWanted`, with "ahead" standing in for the lab's destination (freestyle has none).

## Choices made in the port (not in the lab)

- **Peck is the Sparrow's gun** at the lab's rate and range, so it is the real networked projectile.
- **The jets, Flit and the lasso's reel are rides** on the slip's rail (the transformer stands down for the ride), not
  teleports; the jumps (Blink, Portals, Rosen, FTL, Monte) are `SetPose`, as in the lab.
- **A boost effect fires at once** (`IsBoosting` for its seconds, capped at `boostCap`).
- **Fractal Frame** recolours the hull's own trail prisms as the prism controller lays them (`OnBlockSpawned`).
- **The Space card is the Slip**, and at Space 5 Pecks pierce (the gun's own Space upgrade). The Time card is the Sparrow's
  Afterburner, unchanged.
- **No new visual effects yet.** A shattered prism shows the game's own break; the lance's lane and the harmonizer's
  recolour show as prisms. The grenade, drone, lasso rope and lance beam have no effect of their own (a team task).
- **Local only, as the Wrasse's seat is.** The Dynamo, its AI and the effects run on the machine that flies the hull.
  Another player sees the hull fly, slip and lay prisms, but not the board. Networking the Ratel seat is a follow-up.
- **FMOD:** `patternMadeSfx`, `slipSfx`, `jumpSfx`, `flitSfx`, `upgradeSfx`, `spinSfx`, `revealSfx` and each upgrade's
  `sfx` ship empty, to be wired by the audio owner.
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

## Not verified in the Editor

This port was compiled against Unity's reference assemblies (player and editor configs) but not opened in the Editor.
The parity gate, `author_honeyguide_assets.py --check` and the vessel gates pass.
`Docs/UNITY_VERIFICATION_CHECKLIST.md` lists what to check by hand.
