# Thornbush - the Shrike's Mass game

`GameModes.Thornbush = 103`, hint toast `GameToastSituation.ThornbushHint = 178` (the ids
[`GAME_KIT_ROSTER.md`](../../../../Docs/ElementalLoop/GAME_KIT_ROSTER.md) row 13 reserved). Shrike only. Built 2026-11-15
on `claude/shrike-games`, beside [Larder](LARDER.md) (Charge) and [Lancefall](LANCEFALL.md) (Space).

**Status: authored headless, NOT yet run in the Unity editor.** It was checked by its generator's `--check` and the
static gates. Nobody has played it, and every number below is a first cut.

> *Build in the thorn bush. Dock on a stick and press Mass to leave a nest. Hold the most mass in your colour when the
> clock runs out.*

## 1. What it scores

Dock on a stick, press Mass, and the Shrike leaves a **nest**: a cup of super-shielded blocks (12 at Mass 0, 32 at
Mass 10, 4.5 u each) crowned with a missile plant (`SHRIKE.md` §3). The Shrike keeps at most three; a fourth retires the
oldest. The score is the mass standing in your colour at the whistle, nests and trail:
`ScoringMetric.VolumeRemaining`, a live stock. Every nest block is laid in the pilot's name
(`BoostRingBuilder.LayOne(..., _status.PlayerName, ...)`), so `StatsManager` counts it in when it is laid and out when it
breaks, exactly like the trail. **The controller credits nothing**; it zeroes the stat at the start, calls lead changes
and decides the winner.

What takes mass away from a rival:

- **Missiles** crack shields and break what is under them: paint a rival (Charge mode) and every plant launches.
- **The Void Lance** breaks trail blocks, and with Void Pierce (Space 5) nest blocks outright.

The round is **90 s** (`thornbushRoundSeconds`, `EndConditionOverridesSO`; `ThornbushTimeTurnMonitor`). Highest wins.

The comeback rate is **0.0012** per unit of volume. The generator's reference domain is one Shrike's whole round read
off the shipped config: 3 nests x 1,094 + 510 of trail = 3,791. A quarter of that behind buys 1.14 element levels, and
Mass is the element that grows a nest, so the comeback reaches the score. The generator also asserts that one nest is
worth more than four times its share of the round's trail, so this stays a nest game and not a trail game.

## 2. The arena is Jackstraw's heap, referenced

The four Jackstraw cells (the Thresher's Charge game) are referenced read-only: sticks and jacks round clearings, red
danger tips and ironwood hearts at the higher rungs. A nest needs a perch, and a heap of sticks is a thorn bush: every
stick is one. The heap grows with intensity (radius 360, 470, 570, 680 u, read off each rung's `SpawnableJackstraw`
prefab). The pilots spawn just outside it (450, 560, 660, 770 u) and the crystals (5, 4, 3, 2) float among the sticks
within 250 u.

## 3. The kit stays exactly as it is

Nothing under the Shrike's code, prefab or config was edited (the LOCKED vessel rule). The nest, its size, its cap of
three and its plant are the Shrike's own. The levers are the heap, the crystals and the AI.

## 4. The AI: a builder that only steers

`ThornbushBuilder` (on the shared `ShrikeSeats`) answers `AIPilot.SetExternalTargetProvider` and nothing else. The
Shrike's own autopilot presses Mass after a few seconds docked (once the sling has reloaded), paints a rival in its cone
and lances rival mass on its line. So the builder picks where to go:

- **Perch** (most of the time): a point in the heap at least `aiPerchSpacing` (120 u) from its own standing nests, held
  for the rung's `aiPerchSeconds` or until it docks.
- **Raid** (the rung's `aiRaidChance`): the nearest rival nest, found by one `PrismSpatialIndex` sphere query a second
  for rival super-shielded blocks, nose on it for `aiRaidSeconds`, so the hull's own autopilot lances and paints there.

| Ladder (AI difficulty Easy to Legendary) | Values |
|---|---|
| `aiPerchSeconds` | 9, 7, 5.5, 4 |
| `aiRaidChance` | 0, 0.2, 0.35, 0.5 |
| `aiAimError` (u) | 40, 25, 12, 4 |
| `aiRaidSeconds` | 3, 4, 5, 6 |

Difficulty is the seat's AI level, never the intensity. The heap's radius is the map, so the controller reads it by
intensity and hands it to the builder.

## 5. Files

| What | Where |
|---|---|
| Controller, settings, rule, AI | `Thornbush/ThornbushController.cs`, `ThornbushSettingsSO.cs`, `ThornbushScoringRuleSO.cs`, `ThornbushBuilder.cs` |
| Turn monitor | `TurnMonitors/ThornbushTimeTurnMonitor.cs` |
| Shared Shrike AI seat | `ShrikeGames/ShrikeSeats.cs` |
| Generator | `Tools/Build/author_thornbush_assets.py` (`--check`), shared reads in `shrike_games_common.py` |
| Scene | `MinigameThornbush.unity`, cloned from Glasshouse's |

## 6. What only the editor can tell

- Whether 90 s is long enough for three nests and a rebuild, and whether missiles crack nests fast enough to matter.
- Whether a docked Shrike reliably finds a stick in the heap, or the AI circles the clearings.
- Whether the final snapshot (sent with the whistle, so a late missile cannot change the board) matches what players saw.
