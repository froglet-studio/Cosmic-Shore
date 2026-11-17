# Hedgerow: the Serpent's Mass game (GameModes 94)

Row 4 of `Docs/ElementalLoop/GAME_KIT_ROSTER.md`. Built 2026-11-15 for Garrett's "make all the missing games you can".
**Nothing here has run in the Unity Editor yet** (see `Docs/UNITY_VERIFICATION_CHECKLIST.md`).

## The game

A timed steal in a bare nucleus cell. Hedges of loose neutral (Blue, no-team) bricks stand round the core: straight
rows of the Serpent trail's own 3 x 6 x 0.5 prism, laid by `HedgerowController` on every peer from the intensity alone
(`HedgerowField`). The Serpent's Mass ability is the whole game:

- **Stop dead** (the stance) just short of a hedge, flying across it. Your latest trail prism becomes a held seed and a
  wall grows in the plane across your heading, stealing loose mass from its EDGE for as long as you stay stopped
  (`SERPENT_SEED_WALL.md`, "The stance's wall" and "Growth"). Because it steals from its edge, it creeps along the
  hedge it is eating, and every brick it takes turns your colour.
- **Fly on** and the wall stands as it is; stop again to grow another. The cloak grows the same wall from a
  super-shielded seed while you fly on.
- **Rivals' trails and bricks are loose mass too**: a wall grown beside them eats them the same way.
- **Omni crystals** respawn inside the nucleus; each one re-shields every wall you are growing.

The score is the volume standing in your colour at the whistle (`VolumeRemaining`, timed, highest wins): your trail,
your walls and everything they ate. The platform credits every steal to its thief (`StatsManager`), so the mode adds
nothing to the score. Because the brick grows with Mass (x2 a side at Mass 10), a Serpent with more Mass walls more
volume per brick; the comeback system's element levels are the catch-up.

Nothing about the Serpent changes (the LOCKED vessel rule).

## The ladder (intensity is the map)

| Rung | Name | Hedges | Bricks per hedge | Out from the core | Omni crystals |
|---|---|---|---|---|---|
| I1 | Hedgerow | 8 | 32 long x 2 high | a third of the way to the membrane | 4 |
| I2 | Bocage | 12 | 32 x 2 | about halfway | 3 |
| I3 | Thicket | 12 | 40 x 3 | about halfway | 3 |
| I4 | Labyrinth | 16 | 40 x 3 | out by the membrane | 2 |

Hedges sit at least three steal radii apart, so one wall eats one hedge. The generator checks it, along with the
membrane clearance, the collider budget and that the C# table matches its twin.

## The AI (difficulty is the seat's AI level)

`HedgerowWarden` steers through `AIPilot.SetExternalTargetProvider` and presses only the stop toggle, found by
capability (`ToggleTranslationModeActionSO`). The autopilot's own ability cycler is kept off the stance and the cloak.

1. Pick the nearest hedge with prisms left. Raid instead, on the densest rival mass (`Cell.GetExplosionTarget`), while
   its domain trails the best rival by `aiRaidDeficitFraction` or in the last `aiLateRaidSeconds`.
2. Line up `aiRunUp` back along the hedge's approach (outward from the core), then run in across it.
3. Stop `aiStopShort` (22 u) short, so the seed is inside the wall's 40 u steal reach. Hold for `aiHoldSeconds`, or
   until the hedge is eaten, then fly on.

| Ladder | Easy | Medium | Hard | Legendary |
|---|---|---|---|---|
| aiRaidDeficitFraction | never | 0.3 | 0.15 | 0.05 |
| aiLateRaidSeconds | never | 15 | 25 | 35 |
| aiRetargetSeconds | 12 | 9 | 7 | 5 |
| aiHoldSeconds | 8 | 12 | 15 | 18 |

**What the AI does not model:** the cloak (a human's edge), shooting a rival's seed, and breaking off when a rival
closes on it while it is stopped.

## Files

- `Hedgerow/HedgerowController.cs`, `HedgerowField.cs`, `HedgerowWarden.cs`, `HedgerowSettingsSO.cs`,
  `HedgerowScoringRuleSO.cs`; `TurnMonitors/HedgerowTimeTurnMonitor.cs`; `Tests/Editor/HedgerowTests.cs`.
- Every asset is written by `Tools/Build/author_hedgerow_assets.py` (`--check` is the drift gate). Re-run it rather than
  hand-editing. The card background is rendered by `render_card_backgrounds.py` (`/cardart`).

## Editor checks still owed

- A stopped Serpent short of a hedge grows a wall that eats along it, and the bricks turn its colour.
- The score counts walled bricks (the steal credit) and the rival trail a wall eats.
- The AI lines up, stops, holds and flies on; it never cloaks.
- Two Serpents walling the same hedge share it sensibly.
