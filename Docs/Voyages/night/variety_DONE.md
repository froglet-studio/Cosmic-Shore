# Voyage variety (DONE)

Branch `claude/voyage-variety` (off `origin/claude/voyage-night`, 47cf1a5fb), commit f92309b19, pushed. Compile clean;
EditMode `ArkModel|ArkFall|ArkPacing|ArkVariety|Escort|Voyage` 66/66 (`-batchmode -force-d3d11`). Paths under `Assets/_Scripts/`.

## What a seed decides (`Controller/Voyages/ArkEscortVariety.cs`, `Roll(escort, difficulty, seed)`, pure)

1. **Which worlds, in what order.**
   - `ArkEscortSO.worldsPerVoyage` (default **4** of the 5 legs; 0 = all) are drawn by `Leg.Weight`.
   - The hungriest world (highest `Threat`, The Maw) is always sailed, and always last.
   - The rest are ordered by threat, each jittered by up to `rampJitter` (0.15), so near-equal worlds trade places and the ramp holds.
2. **Which variant of each world.** `Leg.Variants` holds weighted `WorldVariant` entries {Name, World, Ladder, Weight, ThreatScale}.
   - An empty list keeps the leg as authored.
   - It's data only: when the escort-owned world configs from `claude/voyage-fauna-threats` land, each leg gets its variants in `ArkEscort.asset`, with no code change.
3. **0-2 modifiers**, drawn with `modifierCountWeights` (0.3 / 0.45 / 0.25), each modifier by its `Weight`, gated by `MinDifficulty`, and never two of the same kind. Authored in `ArkEscort.asset`:

| Name | Kind | Factor | From d |
|---|---|---|---|
| SWARMING SEAS | Population (every world's seeds and caps) | x1.3 | 1 |
| CALM WATERS | Population | x0.8 | 1 |
| SWIFT FAUNA | FaunaSpeed (`speedScale` on every species) | x1.25 | 1 |
| THIN REACTOR | RegrowSeconds (slower plate regrowth) | x1.6 | 2 |
| RAVENOUS | ArmourBite (`biteScale`) | x2 | 3 |
| RED TIDE | AllHostile (every world starts hostile) | - | 2 |
| BECALMED | ArkCrawl (crawl and entry speed) | x0.75 | 1 |
| FERTILE WORLDS | PacifyShare (easier to pacify) | x0.85 | 1 |

"Frenzy", "rich crystals" and "early second wave" were left out. A forced Frenzy would be an imposed phase, and the crystal and wave knobs aren't exposed for the escort yet. The modifier kind enum (`ArkEscortSO.ModifierKind`) is the place to add them.

The seed also drives the corridor's headings (`_cfg.Seed`) and each world's hostile roll (`_rng`), so one number is the whole voyage.

## Where it applies

- Every run now sails a runtime copy of the escort (`ArkEscortRun.Metrics.cs` `UseRuntimeEscort`). After any named overrides, `RollAndApplyVoyage` (`ArkEscortRun.Variety.cs`) replaces the copy's legs with the rolled ones and applies each modifier:
  - fauna modifiers go through the `FaunaRuntimeTuning` added on fauna-params (new `speedScale` and `biteScale` multipliers, applied once per creature);
  - population goes through the per-world scales;
  - the rest change the tiers, legs and pacing of the copy.
- **Card:** the Ark escort card shows the rolled voyage under its line, for example "Garland Shallows > Scarab Reef > The Swarm > The Maw | SWIFT FAUNA". The roll comes from `ArkEscortRun.PreviewVoyage`, which picks `NextVoyageSeed` once per card. Set sail sails exactly that roll, and the next card shows a new one. While underway, the card shows the running voyage (`VoyageHarbour.EscortVoyage`).
- **Departure banner:** the modifiers are named on a second line.
- **JSON:** `VoyageSeed`, `Worlds` (in order, variant names) and `Modifiers` (names), alongside the existing per-leg reports.
- **Params for the farm:** `voyage.seed` (int), `voyage.modifiers` (bitmask over the asset's modifier list in the order above: 1 = SWARMING SEAS, 2 = SWIFT FAUNA, 4 = THIN REACTOR, 8 = RAVENOUS, 16 = RED TIDE, 32 = BECALMED, 64 = FERTILE WORLDS, 128 = CALM WATERS; -1 = roll; 0 = none) and `voyage.worlds` (worlds per voyage, 0 = all).

## Co-op

- The roll is a pure function of the escort, the difficulty and the seed, and on `claude/voyage-coop` that seed already travels in `EscortSail`.
- **When the two branches merge**, `RollAndApplyVoyage` must take the course's seed on a follower. Its first line chooses `_voyageSeed`, and `BeginCoop` sets `_seed`, so make them one field:
  - `_voyageSeed = _following ? _seed : (params ?? NextVoyageSeed ?? new)`;
  - the leader sends `_voyageSeed` as the course `Seed`.
- Both branches also replace the same `_rng` / `_cfg.Seed` lines in `ArkEscortRun.SetSail` (a trivial conflict).

## Sims
