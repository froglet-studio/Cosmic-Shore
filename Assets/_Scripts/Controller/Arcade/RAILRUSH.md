# Railrush - the Urchin's Charge game (`GameModes.Railrush = 124`)

**Urchin-only. First DOMAIN to land the hit target (20 spike hits) wins.** A joust on rails: grind a rail where it
wears your colour, fly off its open end at speed, and lance a rival with a chain-spike volley that carries your speed
with it.

Ids: `GameModes` 124 and hint toast `RailrushHint` 199, the numbers `Docs/ElementalLoop/GAME_KIT_ROSTER.md` reserved
for row 34 (2026-11-15). Authored headless by `Tools/Build/author_railrush_assets.py`. **Nothing here has been run in
Unity.**

---

## 1. Why this game, and why not a joust

The roster pitched the Urchin's Charge game as a joust (the platform's skimmer overtake, `JOUST.md`), on the claim
that the Urchin never touches a rival hull. Both halves turned out wrong when read off the assets:

| claim | what the assets say |
|---|---|
| "every hull can joust" | The Urchin's skimmer wears `NoEffectsSkimmerImpactorDataContainer` (empty). The joust effect, `VesselExplosionBySkimmerEffect`, sits only on the Squirrel, Stoat, Shrike, Brittle Star and Thresher skimmer containers. The Urchin cannot score a joust. Giving it one is a vessel change, which a mode may never make (LOCKED, Shombith 2026-10-31). |
| "its Charge ability never touches a rival hull" | Its Charge ability, Chain Spikes, does. Every spike projectile (plain, energized, super-energized) carries `UrchinSpikeProjectileImpactContainer`, whose ship effect `VesselCombatHitBySpike` reports a **Bullet-class combat hit** on `Event_CombatHitStats` (and ejects petals, the PvP rule). Every spike hit is already counted (`BulletHitsLanded`) in every mode. |

So the Charge game pays the Urchin's own Charge verb, and keeps the roster's idea (ride the rail, launch at speed past
a rival) through the vessel's physics: **a spike's velocity is its muzzle speed plus the hull's**
(`URCHIN_CHAIN_SPIKES.md`, round 7). With the shipped numbers (`projectileSpeed` 60, `projectileTime` 2 s):

| thrown at | hull speed | spike reach |
|---|---|---|
| cruise | 65 u/s | 250 u |
| a friendly third of rail | 300 u/s | 720 u |
| off the open end (the 1.2x kick) | 360 u/s | 840 u |

The rail is the lance. The generator re-reads all of this every run (skimmer container, spike containers, hit class,
channel, spike speed and time) and fails if any of it moves.

## 2. The loop

Spawn on the Switchyard's equatorial ring. The arrow points at the nearest rival (Dog Fight's provider). Latch onto a
rail: your colour's third grinds at 300, a rival's crawls at 20 while you steal it, and a spike tap ahead makes it
yours. Riding refills spike ammo. At the open end you launch along the rail's tangent at 360. A volley thrown now
reaches about three times as far as one thrown at cruise, so the play is to pick the rail whose launch line points at
a rival, and fire as you leave it. Every spike in a rival hull is one point and knocks petals loose (the PvP rule), so
a hit also weakens the rival's own spikes (Chain Spikes scales with Charge). The burrs on every launch line are cover,
and a surface to marble-roll over to recharge.

## 3. What is reused, and what Railrush owns

**Reused, unchanged:** Hijack's arena (the four `Switchyard Cell Config` assets, referenced, not copied), Hijack's
replicated prism ownership (`HijackOwnershipLedger`, which keeps ride speed agreeing between machines), Hijack's
rail-riding AI (approach, ride, launch, the stall escape), the shared Urchin AI driver (`UrchinAutopilotDriver.TrySpike`),
the shared combat-point monitor (`CombatPointTurnMonitorBase`), Dog Fight's objective provider and the race-beat toasts
(128-130).

**Owned:**

| piece | file |
|---|---|
| controller | `Railrush/RailrushController.cs` : `HijackController` |
| scoring rule | `Railrush/RailrushScoringRuleSO.cs` (CombatPoints; a Bullet hit pays `spikeHitPoints`, every other class 0) |
| turn monitor | `TurnMonitors/RailrushHitTurnMonitor.cs` (target `railrushHitTarget`, default 20) |
| generator | `Tools/Build/author_railrush_assets.py` |
| scene | `_Scenes/Multiplayer Scenes/MinigameRailrush.unity` (Hijack's, cloned through `lib.committed_scene`) |
| card / rule / toasts / preview | `ArcadeGameRailrush`, `RailrushScoringRule`, `GameToastConfig_Railrush`, `ModePreview_Railrush` |
| card art | `_Graphics/ARCADE/CardBackgrounds/Railrush.png` (`/cardart`, the yard from the far side) |

### The platform change: seams on `HijackController`

`HijackController` gained protected seams whose defaults are exactly what it did before, so Hijack is unchanged:
`ZeroScoredStats`, `ReadScoredStat` / `WriteScoredStat` (the stat that picks the winner's representative and the first
replicated number), `ReadBreakdownStat` / `WriteBreakdownStat` (a second replicated number for the results breakdown;
Hijack replicates nothing extra), `TryOverrideAITarget` (a subclass takes the AI's steering before the heist plan
runs), `ChooseRail` (now `protected virtual`), `SpikeVolley` (now `protected`) and `Update` (now `protected virtual`).
`SyncFinalScores_ClientRpc` carries the breakdown array beside the scored one.

*General rule: a second game on a shipped arena inherits the arena's controller through seams that default to the
original, rather than copying its replication.* (The Waystation Prototype rule from `/arcadegame` §3b, applied to a
mode whose arena needs its controller's RPCs.)

## 4. Scoring

`RailrushScoringRuleSO`: first domain whose summed `CombatPoints` reaches the target wins; golf-timed like Jackstraw
(winners carry their finish time, losers a sentinel for the hits their domain still needed). The breakdown column is
each pilot's `BulletHitsLanded`. `VesselCombatHitBySpike` latches one hit per victim per 0.12 s, so a close volley
pays one to three points, not one per spike.

**Target 20 is an estimate** pending a timed match. Comeback rate 0.25 per point behind: a quarter of the race (5 hits)
buys 1.25 element levels (the generator asserts at least 1).

## 5. The AI

The heist AI flies the rails as it does in Hijack. Railrush changes two of its choices, keyed on the seat's AI
difficulty (Easy, Medium, Hard, Legendary), never on the intensity:

- **Which rail.** `ChooseRail` scores a rail by how well its launch line (its far-end tangent) points at the nearest
  rival past its end, times how much of it is the pilot's colour, over its distance. With no rival it falls back to
  Hijack's loot choice so the AI keeps moving.
- **When to hunt.** On its chosen rail the AI keeps the grind (the ride constrains position, not attitude, and turning
  off the rail throws away the launch) and fires only when a rival is inside the fire cone ahead and inside the spike's
  current reach. Off the rail it takes the steering once a rival is inside 1.5x that reach, aims through the rival's
  lead point (overshooting by 120 u so it does not orbit, `Docs/AI_ORBIT_BREAK.md`) and fires inside the cone. The
  reach grows with the hull's speed, so a launched AI engages from about three times as far as a cruising one.

| ladder (Easy -> Legendary) | values |
|---|---|
| `aiHuntRange` | 500, 650, 800, 950 u |
| `aiLeadSeconds` | 0, 0.15, 0.3, 0.45 s |
| `aiFireConeDegrees` | 30, 24, 18, 14 |

Fire cadence and ammo floor are Hijack's (`aiSpikeIntervalSeconds` 2 s, `aiMinSpikeAmmo` 0.15), through the shared
driver's capability lookup. The AI's reach model reads `aiSpikeMuzzleSpeed` 60 and `aiSpikeFlightSeconds` 2, which the
generator checks against `UrchinSpikeAction.asset`.

There is no offline race model: this is a Charge game, not a Time race, so it is not a Compute training ground.

## 6. The ladder

Hijack's: intensity scales burr mass and nothing else (2,772 / 3,978 / 7,218 / 9,930 prisms). The rails, launch lines
and spawn ring never move. In Railrush bigger burrs are more cover on the launch lines and more to roll over, at the
same lanes.

## 7. Verification status

Authored headless on 2026-11-15, **not yet run in the editor**. What did run:

- `author_railrush_assets.py --check` passes, and was watched failing once (a mutated AI field in the scene).
- The card renders (`render_card_backgrounds.py Railrush`), and Hijack's card still matches byte for byte.
- The offline gates and the reference compile listed in the PR.

To test by hand: start Railrush (2 players, 2 domains) against one AI. Check that a spike volley into the AI hull
counts on the scoreboard, that the end screen shows the hit breakdown on the client as well as the host, that an AI
launches off a rail and fires at you, and that Hijack still plays exactly as before.

## 8. Known limitations

- The target and the AI numbers are first guesses. A timed match should set them.
- `aiFireConeDegrees` assumes the spike shotgun (`ConcentricRings`) leaves along the nose. The Overcharge burst (L5) is
  omnidirectional, so a charged AI volley would be wasted cone logic; the AI only taps, it never charges.
