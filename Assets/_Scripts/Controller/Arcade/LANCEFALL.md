# Lancefall - the Shrike's Space game

`GameModes.Lancefall = 104`, hint toast `GameToastSituation.LancefallHint = 179` (the ids
[`GAME_KIT_ROSTER.md`](../../../../Docs/ElementalLoop/GAME_KIT_ROSTER.md) row 14 reserved). Shrike only. Built 2026-11-15
on `claude/shrike-games`, beside [Larder](LARDER.md) (Charge) and [Thornbush](THORNBUSH.md) (Mass).

**Status: authored headless, NOT yet run in the Unity editor.** It was checked by its generator's `--check` (and one
watched failure) and the static gates. Nobody has played it, and every number below is a first cut.

> *Break the plates hanging in the cell. A lance at a plate's face takes a tile; come round to its edge and lay the
> lance along it, and it breaks a whole strip. The first team to break the target wins.*

## 1. Why plates

The Void Lance is a 2,400 u hitscan that breaks every block it passes, super-shielded blocks only with Void Pierce
(`ShrikeExecutor.FireLance`, `SHRIKE.md` §3). Against a 3D clump that is a skewer. Against a **plate** - one tile deep,
two dozen tiles across - it is a choice: fire at the face and break a tile, or come round to the plate's edge, lay the
beam in its plane, and break a whole row. **Coming round to the edge is the skill**, the inverse of Shatterplane, where
the Brittle Star squares up to the face.

Measured by the generator's strip model (a line 24 u off the plate's centre, in its plane, beam radius 1.5 u at Space 0):

| Rung | Plates | Edge-on strip | Face-on |
|---|---|---|---|
| I1 Billboards | ten, facing straight out at the spawn ring | 25 tiles | 1 |
| I2 Gallery | twelve at mixed angles | 23 | 1 |
| I3 Shelves | five stacks of three, 70 u apart (one plane is one plate) | 23 | 1 per plate in line |
| I4 Reliquary | eight, ribbed and danger-rimmed (ribs stop nothing, never break) | 22 | 1 |

I1's plates face the pilots, so the very first lesson is to swing round. Space widens the beam (1.5 to 5 u), so a
Space-heavy pilot takes two rows at once off the centre line.

The score is mass taken off the board (`ScoringMetric.PrismsDestroyed`, Rampage's rule), so `ModeGenre` reads Space
through the metric with no row of its own. The plates are neutral, so every tile a lance or a missile breaks counts.
First DOMAIN to `lancefallPrismTarget` (**600**, `EndConditionOverridesSO`) wins, golf-timed. The generator asserts that
three domains' targets fit inside every rung's breakable plates (the smallest, I4, holds 3,232), that an edge-on lance
takes at least ten times a face-on one, and that a perfect pilot still needs at least fifteen strips (it needs 24 to
27, about 30 s of cooldowns, so a real match is a minute or two). The comeback rate is **0.008**: a quarter of the target
behind buys 1.2 element levels.

## 2. The arena is Shatterplane's, referenced

The four Shatterplane cells (the Brittle Star's Space game) are referenced read-only, with their spawn ring (780 u)
and the scene's crystal manager (5, 4, 3, 2 crystals per intensity; every one is a missile plant and a step of Space).
The Brittle Star's own crystal field and its resized nucleus are its controller's, not the cell's, so neither comes
with the plates: Lancefall leaves the nucleus its standard size and only clears `NucleusIsControlZone` (play geometry,
not a claim).

## 3. The kit stays exactly as it is

Nothing under the Shrike's code, prefab or config was edited (the LOCKED vessel rule). The levers are the plates, the
crystals and the AI.

## 4. The AI: an edger that only steers

`LancefallEdger` (on the shared `ShrikeSeats`) answers `AIPilot.SetExternalTargetProvider` and nothing else. The
Shrike's own autopilot picks Space mode and holds the trigger while two or more prisms not its own lie on the lance's
line, so the edger brings the nose into a plate's plane:

1. **A crystal within the rung's detour range**: to the crystal.
2. **Out of the plate's plane** (more than `aiEdgeDegrees`) or inside its rim: to the **station**, the point in the
   plate's plane `aiStandoff` out from its centre on the hull's side (`EdgeSteer`, pure).
3. **Edge-on**: nose at the plate's centre, offset `aiChordOffset` (24 u) across the approach and by the rung's aim
   error in the plane. The offset is there because I4's ribs run through every plate's centre row; the generator
   fails if the offset lands on a rib row.

| Ladder (AI difficulty Easy to Legendary) | Values |
|---|---|
| `aiRetargetSeconds` | 2.4, 1.8, 1.2, 0.8 |
| `aiStandoff` (u) | 200, 240, 280, 320 |
| `aiEdgeDegrees` | 20, 14, 9, 5 |
| `aiAimError` (u) | 50, 30, 15, 5 |
| `aiCrystalDetourRange` (u) | 150, 220, 300, 400 |

A plate stops being a target when no neutral, non-super-shielded prism stands within its radius.

## 5. Files

| What | Where |
|---|---|
| Controller, settings, rule, AI | `Lancefall/LancefallController.cs`, `LancefallSettingsSO.cs`, `LancefallScoringRuleSO.cs`, `LancefallEdger.cs` |
| Turn monitor | `TurnMonitors/LancefallPrismTurnMonitor.cs` |
| Shared Shrike AI seat | `ShrikeGames/ShrikeSeats.cs` |
| Generator | `Tools/Build/author_lancefall_assets.py` (`--check`), the plate census in `shatterplane_budget.py` |
| Scene | `MinigameLancefall.unity`, cloned from Shatterplane's |

## 6. What only the editor can tell

- Whether a human finds the edge readable (the plate is thin seen edge-on), and whether 600 is a one-to-two-minute race.
- Whether the autopilot's two-prism lance threshold fires too eagerly on a face (one tile per shot) and burns the
  cooldown before the edger is lined up.
- Whether the lance's `RayHits` meets the row the strip model counts, at the plate's real tile thickness.
