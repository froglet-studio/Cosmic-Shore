# The Honeyguide and Ratel

A two-seat crew vessel on the Sparrow's hull: `VesselClassType.Honeyguide = 21`. It carries *Slipstream*, the tabletop game
Garrett and friends designed and played from 2019 to 2022. The design was made and decided in the Honeyguide Lab
(`Docs/Studios/HoneyguideLab.html`, write-up `Docs/Studios/HONEYGUIDE_LAB.md`, pattern list
`Docs/Studios/HONEYGUIDE_PATTERNS.md`). The lab is the record. This port is its clone in the game, as of lab round 4.

- **The Honeyguide** flies the hull. It uses the Sparrow's airframe, its numbers and its two triggers (the guns). Its Time
  ability is the **Slip**. Touch a 1-D trail of a domain the battery holds, with the nose within `aimCone` (45°) of the
  prism's long axis either way. The hull then jumps (`IVessel.SetPose`) to the end of the trail it points at. That end is
  clipped to `slipReach`, and the hull lands `exitOffset` past it, aligned with the trail. The slip spends that domain's
  charge.
- **The Ratel** plays the **Dynamo**, a hex puzzle (`DynamoCore`):
  - Every 15 s (a shrinking bar across the top) the injection ports turn 60° and the AP refill.
  - The Ratel moves prisms, injects at ports and makes patterns. Each costs 1 AP.
  - A made pattern sends one of its prisms, drawn at random, to the battery, where its domain is a slip charge. It also
    fires that card's own effect.
  - A pattern made three times is replaced.
  - Every 4 patterns the Ratel draws a new ability and a new port pattern. The clock stops while they are shown.

## Try it

1. Open the Toybox and use the **Vessel Changer**. Pick **Honeyguide and Ratel**.
2. Fly it. Ram trails of the domains the battery holds, with the nose along the trail, to slip.
3. Press **D-pad left / right** or **1 / 2**, or double-tap the small board, to swap seats. The AI takes the empty seat:
   - the hull's own `AIPilot` flies while you play the Ratel;
   - the Ratel AI (the lab's `aiPlan`, input-only, Hard by default) plays while you fly.
4. To play the Ratel:
   - Drag a prism along empty cells to move it (1 AP a step).
   - Tap a port to inject its colour (1 AP).
   - Tap a lit card to make its pattern (1 AP).
   - Free prisms (Printer, Translator, 5D Printer): tap an empty cell. Tap the AP row to change the colour.
5. Stored jumps (Blink, All Terrain Portals, Rosen Bridge, FTL Chromatool, Monte Cannon) and Thruster dashes are used with
   **R**, pad **Y**, or a tap on the chip by the small board.

## Where things are

| Part | File |
|---|---|
| Rules and AI (engine-free, held to the page by `Tools/Build/dynamo_lab/parity.cjs`) | `DynamoCore.cs` |
| Every number, card and word | `HoneyguideConfigSO.cs`, `Assets/_SO_Assets/VesselActions/Honeyguide/HoneyguideConfig.asset` |
| Seats, swap, Ratel AI, events | `HoneyguideCrew.cs` |
| Slip, card effects, jumps | `HoneyguideCrew.Pilot.cs` |
| The board (runtime overlay canvas, touch and mouse) | `HoneyguideDynamoView.cs` |
| Prefab, class, map, registrations, config from the lab | `Tools/Build/author_honeyguide_assets.py` (`--check`, `--self-test`) |
| Edit-mode tests (golden from the lab's core) | `Assets/_Scripts/Tests/Editor/HoneyguideTests.cs` |

## The cards

All 39 cards keep their tabletop names. Each card's effect is the lab's conversion (`EFFECTS` in the page). The effect
text the player reads is the `effectText` field on each card in the config. Timed effects last `passiveSeconds`, and a
repeat raises their level up to `passiveMaxLevel`.

**Parked (8):**
- Fugacity Discharger
- Hard Light Lasso
- Eyelet Jet
- Sepia Drone
- Particle Harmonizer
- Genesis Lance
- Little Bang Grenade
- Frame Dragger

These change what the triggers' shots do, or what domain the trail is laid in. The Sparrow's guns do not take upgrades
yet, so these cards are never drawn. Each has a `parked` checkbox in the config. Untick it once its effect is built.

## Choices made in the port (not in the lab)

- **The triggers are the Sparrow's guns.** The lab's Bolt and Burst stand-ins are not built.
- **A boost effect fires at once** (`IsBoosting` for its seconds, capped at `boostCap`).
- **Dashes use the jump input.**
- **The AI takes a stored jump after 0.6 s.**
- **Local only, as the Wrasse's seat is.** The Dynamo, its AI and the effects run on the machine that flies the hull.
  Another player sees the hull fly, slip and lay prisms, but not the board. Networking the Ratel seat is a follow-up.
- **FMOD:** `patternMadeSfx`, `slipSfx`, `jumpSfx`, `spinSfx` and `revealSfx` ship empty, to be wired by the audio owner.
- **Effect numbers the lab keeps in code** (for example `dashDistance` 20, `noodleCount` 5 and `etiReach` 40) are fields
  on the config, set to the lab's values.

## Not verified in the Editor

This port was compiled against Unity's reference assemblies (player and editor configs) but not opened in the Editor.
The parity gate, `author_honeyguide_assets.py --check` and the vessel gates pass.
`Docs/UNITY_VERIFICATION_CHECKLIST.md` lists what to check by hand.
