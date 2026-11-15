# Warren (`GameModes.Warren = 106`)

The Meerkat's **MASS** game (`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 16). It is a timed burrow war in an empty cell.
**The score is the prism volume standing in your colour when the clock runs out** (`ScoringMetric.VolumeRemaining`).
That volume is your trail, plus the **exit portal** every soliton rings its way out with: six of your prisms on a
hexagon, bigger with Mass (`MeerkatExitPortal`, MEERKAT.md §10.4). The only other mass in the cell is your rivals', so
the raid is the Meerkat's own Space. A soliton formed beside a rival's mass swallows it and fires it out of the way
out, and every fired prism that runs into another breaks both.

| | |
|---|---|
| Scene | `MinigameWarren.unity`, cloned from `MinigameGlasshouse.unity` (timed `VolumeRemaining`) |
| Controller | `Warren/WarrenController.cs` (Glasshouse's without the garden; the lead sampler kept for the lead-change toast) |
| Metric | `ScoringMetric.VolumeRemaining` (12), reused; `WarrenScoringRuleSO` shows prisms standing as the breakdown |
| Turn monitor | `TurnMonitors/WarrenTimeTurnMonitor.cs` (time-based) |
| Round length | `EndConditionOverridesSO.warrenRoundSeconds` (**90**) |
| Comeback | **0.000189** per u³, sized from the Meerkat's own trail read off `Meerkat.prefab`. One pilot at cruise lays about 354 u³/s; a reference domain is two pilots laying half of 90 s (~31,800 u³). A quarter of it behind buys 1.5 levels |
| Arena | the **Barren** cell at every rung (Tapestry's reasoning: nothing to hide behind, nothing to score but pilots' mass) |
| The ladder | I1 Burrow (six omni crystals), I2 Warren (four), I3 Danger Warren (three, everyone starts at **Mass 5**, so every exit portal is a Danger Portal), I4 Robbers' Warren (two, **Mass 5 and Space 5**, so a soliton STEALS what it swallows). Starting levels go through the card's `StartingElements` |
| Players | 2-4, 2-3 domains (the raid needs a rival) |
| AI | `MeerkatArcadeSteer` with `PickBurrow`: by `aiRaidChance` it raids just behind the nearest rival (their freshest trail), otherwise it takes an omni crystal within `aiCrystalReach`, or else lays in open space between the nucleus and three quarters of the membrane |
| Objective arrow | `BendsObjectiveProvider` (the nearest rival, the raid) |
| Toasts | 181 hint, 130 lead change (the controller's own sampler), 30 comeback |
| Generator | `Tools/Build/author_warren_assets.py` (`--check`) |

## What to watch in the first playtest

- **The trail dominates.** A portal is six prisms per soliton. A pilot's trail is about 354 u³ every second. Unless
  portals are much bigger than that in practice, the score is mostly "who flew the most and lost the least". Raiding
  matters, because it removes a rival's trail. Whether the exit portal is worth forming solitons for is the open
  question.
- Whether I4's Steal makes raiding too strong (a stolen prism turns your colour, so a raid both subtracts and adds).
