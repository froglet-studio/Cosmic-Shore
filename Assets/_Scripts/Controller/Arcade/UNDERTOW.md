# Undertow — Technical Documentation

## Overview

Undertow is the **Scarab-only cavitation duel**, and The Bends for the hull whose blast is a
sideways **plate** rather than a cone. Two to four Scarabs hunt each other through Wildlife
Liberation's caged arena with no guns and no hoops: the juke dash's **cavitation plate** — a
cylinder sweeping out from the hull along the dash and MIRRORED behind it, so it drags whatever is
behind you forward through you (SCARAB.md §3.7, §3.9) — is the only weapon, and two things it does
score:

| what the plate catches | what happens | points |
|---|---|---|
| an **opposing pilot** | the plate's all-element decaying debuff — every element stripped for four seconds, one **BEND** | **3** |
| a **creature** (its heart inside the plate) | the Squirrel's joust: the heart is freed, the body unravels from the heart outward and stands as a skeleton, one **KILL** | **1** |

First DOMAIN to **12 points** wins — four clean bends, twelve kills, or any mix. A pilot is worth
three creatures so the wildlife half can shorten a match but never decide one on its own.

So a round reads: **find a rival through the cages → dash beside them → the plate takes them, and
everything behind you → drag the swarm through it on the way to the next.**

## Why this needed no new weapon, resource, or ability

The Scarab already had everything: the plate fires off a committed juke, on its own CHARGE-scaled
cooldown (2.5 s at rest), and its container has debuffed any pilot it engulfs since the hull
shipped (`ScarabCavitationDebuffByExplosionEffect`, −0.5 on every element over 4 s). What was
missing was that nothing COUNTED the debuff and nothing let the plate reach a creature's heart.

## The platform change: the plate scores, and kills through a heart

`ScarabCavitationExplosionImpactorDataContainer` gains two effects, beside the two it carried:

1. **`VesselCombatHitByCavitation`** (`VesselCombatHitByExplosionEffectSO`, `hitClass = Debuff`)
   — the same script The Bends stamped for the Dolphin's cone. `requireDebuffableVictim` on: the
   score must follow the effect, so a pilot warded against Explosion-class debuffs
   (`ResourceSystem.IsImmuneTo`) pays no point. `requireOwningMachine` on: the plate exists on
   exactly one machine today (the local pilot's; the host's for an AI — `ScarabJukeController`
   gates the fire path on `IsLocalPilot`), so this is belt-and-braces, but it is exactly what
   stops a future replay of the blast onto a second machine from double-crediting. Its
   `sameVictimCooldownSeconds` (1) matches the debuff effect's own cooldown, because the plate
   resolves its vessel contacts over several frames and both effects need one per-victim window.
2. **`ScarabCavitationWitherLifeformEffect`** (`ExplosionWitherLifeformByCrystalEffectSO`,
   `faunaOnly`, own-domain NOT spared) — the Sparrow warhead's creature kill, on a plate.
   `ExplosionImpactor.SweepLifeformHearts` already runs on the cylinder path with the plate's own
   narrowphase; it was skipped only because the container authored no lifeform-crystal effects.
   Wildlife is quarry whatever colour it wears, for the reason Wildlife Liberation records: fauna
   spawn in ONE colour, so sparing your own would switch off half the mode for whoever shares the
   swarm's colour — and the plate's prism half already killed creatures of every colour by
   shredding their bodies.

Both land **platform-wide** — the plate now scores a Debuff-class hit in every mode (paid only
where `PointsForCombatHit` is non-zero: here) and kills a creature through its heart in every
mode (Scramble's and Tollway's cleanup crews were already dying to the plate's prism half; now a
creature whose heart is swept but whose body is not also dies). Counted everywhere, scored in one
place — the split Dog Fight established.

## The second platform change: an AI Scarab can dash

Shared with Wrecking Ball and recorded there: `ScarabJukeController.TryAutopilotDash`. Without it
this mode's AI would have been an opponent that could not play — the juke, and so the plate, was
inert under autopilot.

## Scoring: two stats, one fold, no new metric

`UndertowScoringRuleSO` keeps `metric = CombatPoints` (bends, already weighted at 3 by
`CombatHitScoring.Credit`) and folds kills in through **`DomainValue`** — the seam Switchback added
so that a domain's score is read in five places that can never disagree (`Remaining`,
`ResolveWinner`, `ResolvePlacementOrder`, `DomainDelta`, the HUD's domain boxes):

```
DomainValue(d) = Σ CombatPoints(d) + killPoints × Σ LifeformsKilled(d)
```

**The stated cost.** `ScoringRuleSO.LiveMetric` (the per-player HUD card) is not virtual and reads
the single metric, so the per-player card sees BENDS alone (×3) — the comeback deficit USED to as
well, and since 2026-09 reads the rule's `DomainValue` like everything else, while the goal row, the
domain boxes, the end condition and the placement order see bends plus kills. The scoreboard's
secondary line is the honest breakdown ("2 bends · 3 kills"). This is deliberate: the bend is the
act the mode is named for, and a new `ScoringMetric` member for a stat the platform already
counts would be a new metric whose SOURCE is not new — the bar CLAUDE.md sets for one.

Both raw counts travel in the final-score snapshot (`SyncFinalScores_ClientRpc` carries
`CombatPoints`, `DebuffHitsLanded` AND `LifeformsKilled`), or every loser's breakdown would read
0 on a client.

## The arena — Wildlife Liberation's, referenced and read-only

The mode wants exactly what that cell authors: a very heavy swarm of small creatures, bigger ones,
and the biggest and toughest, roaming ONE arena-wide band through three concentric cages at
1050 / 600 / 200 that the plate tears through; and a four-rung intensity ladder (`Wildlife
Liberation Cell Config 1..4`, referenced, never forked — the cell is per-ARENA, not per-mode, the
Salvo-in-the-Boneyard rule). A nucleus-less cell, which forces two scene edits the Bends donor
did not have:

- **the spawn ring** is Wildlife Liberation's own (floor 1150, equatorial) — the donor's
  "500 outside the nucleus" would collapse to 500u with no nucleus, inside the middle cage;
- **`noNucleusSpawnRadius = 480`** — the Dog Fight rule: a cell with no nucleus must author the
  omni-crystal respawn volume, or every crystal falls through to the arena's exact centre. The
  donor's `IntensityScaled` crystal ladder (2p / p / p−1 / 1) is kept; a Scarab forges balls
  from them, which here are a distraction rather than a tool, and a scarce one.

Two consequences worth stating: a ball forged here has no court to bounce off (the drag ramp
settles it — SCARAB.md §4.1c, a soft boundary), and the cages are triad-painted, so the plate
also shreds cage bars; nothing scores for that here (the metric is bends and kills), so the
erosion Wildlife Liberation accepted deliberately is just texture in this mode.

## AI

Every AI Scarab HUNTS (`UndertowController.ArmHunters`): `SetExternalTargetProvider` runs its
steering at an intercept point ahead of the nearest opposing pilot (humans preferred ×3, The
Bends' `aiAimHumanFocus`), falling back to the arena centre — where the innermost cage and its
wildlife are — with no rival alive. On its own 0.4 s clock it asks the juke for a committed dash
toward the rival whenever they are within `aiDashRange` (60u), else toward the densest hostile
mass (`Cell.GetExplosionTarget` — in this arena mostly the wildlife's own bodies and the bars
they roam through) within `aiWildlifeDashRange` (50u). The plate is mirrored, so which side the
target is on does not matter; the shove is projected off the course inside the juke as a pilot's
push is. Both ranges track the plate's reach (10× the hull radius, ~45u) and must move with
`ScarabCavitationBlast.radiusPerVesselRadius`.

## Comeback

`ComebackRatePerScoreDeficit = 0.5` against a target of 12, read off the SCORE deficit
(the rule's `DomainValue`, bends + kills since 2026-09): one bend behind (3 points, a quarter of the race) buys 1.5
element levels, two behind buys 3. The generator asserts the quarter-of-target rule. It matters
here as it did in The Bends: the thing a bend TAKES is element levels, so a losing pilot is by
construction also debuffed, and the comeback is what stops that being a spiral.

## Assets

All authored by `Tools/Build/author_undertow_assets.py` (`--check` passes; watched to fail on a
mutated card). Built on `Tools/Build/arcade_mode_lib.py`.

| Asset | Path |
|---|---|
| Card | `_SO_Assets/Games/ArcadeGameUndertow.asset` (Scarab only, 2–4 seats, 2–3 domains, comeback 0.5) |
| Settings | `_SO_Assets/Games/UndertowSettings.asset` |
| Rule | `_SO_Assets/Scoring Rules/UndertowScoringRule.asset` (metric 8, golf, bend 3 / kill 1) |
| Effects | `_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByCavitation.asset`, `_SO_Assets/Effects/Explosion Crystal Effects/ScarabCavitationWitherLifeformEffect.asset` — and the cavitation container re-emitted with both |
| Toasts | `_SO_Assets/Game Toasts/GameToastConfig_Undertow.asset` (82, 84 every 3, 95–97, 98 idle, 30) |
| Preview | `_SO_Assets/Mode Previews/ModePreview_Undertow.asset` (Wildlife Liberation's cells) |
| Scene | `_Scenes/Multiplayer Scenes/MinigameUndertow.unity` — a clone of The Bends' with the identity, the cell list, the AI hull, the spawn ring and the crystal volume swapped |
| Code | `Arcade/Undertow/UndertowController`, `UndertowSettingsSO`, `UndertowScoringRuleSO`; `TurnMonitors/UndertowPointTurnMonitor` (on `CombatPointTurnMonitorBase`) |

Shared-code touchpoints: `GameModes.Undertow = 55`, `EndConditionOverridesSO.undertowPointTarget`
(+ window rows), `ElementalComebackSystem` (CombatPoints), `MiniGameHUD` (The Bends' objective
provider — the nearest bendable pilot), `GameToastSituation` 95–98, `author_preview_spawns`.

## Collider budget

Wildlife Liberation's, unchanged — the arena is referenced. The two new effects add no colliders:
the heart sweep is an `OverlapSphere` the impactor already ran for the crystal forge.

## Verification — compiled and played once; the AI ranges are still geometry

Status at ship: the branch compiles in the editor and the mode was played once by the author
(reported as an excellent start). The AI dash ranges and the per-player HUD card's bends-only
read are unmeasured beyond that one session. The checklist stands as the re-verification pass
after any retune:

1. Compile.
2. Launch Undertow, two seats, one AI. Expect Wildlife Liberation's cages and swarm, pilots on
   the 1150u ring, crystals scattered inside 480u.
3. Dash beside the AI: it should take the debuff (its petals drop) AND the goal row should count
   3. Dash through a swarm: creatures should die the joust (heart freed, body unravels) and the
   row should count 1 each.
4. Watch the AI: it should chase you and visibly dash when close.
5. Regression: in Scarab Scramble, a dash through the cleanup crew now kills them through their
   hearts as well as their bodies — expected; nothing scores for it there.

## Known limitations / follow-ups

- **The per-player HUD card shows bends only** (see Scoring). If it reads wrong in play, the
  honest fix is a `ScoringMetric` for the fold, not a per-player hack.
- **The AI dash ranges are geometry, not measurements** — retune against the plate on screen.
- **A client's plate kill of a `NetworkSynced` shark** goes through `Predated`, which is
  authority-gated (the Wildlife Liberation open item); body-prism kills still credit.
