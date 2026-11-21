# Pacify a world (DONE)

Branch `claude/voyage-pacify` (off `claude/voyage-fauna-params`), commit f5d4a1523, pushed. Compile clean; EditMode
ArkModel/ArkFall/ArkPacing 34/34 pass. `ArkEscortRun.cs` is NOT edited: everything lives in `ArkEscortRun.Metrics.cs`
(called from the existing 1 Hz sample hook).

## What the ecology already had

- Fauna spawn in `Cell.ControllingDomain` = the live `DominantDomain` (volume leader of the nucleus claim, or the whole
  cell without a nucleus), falling back to the escort's hostile `StartingController`. So a squad that out-masses a world's
  nucleus already flips NEW spawns to its colour (emergent).
- `Cell.SetModeControlOverride(domain)` pins control AND re-colours the live swarm (`Fauna.SetTeam`), the sanctioned
  "controller re-colour" path (no deaths, one fauna colour per cell). It replicates through `CellNetworkSync`.
- Missing piece: outside the nucleus, herbivores graze ANY domain, so a turned swarm would still eat the squad's Ark.

## What was added

- `Cell.SparedDomain` / `Cell.SparesOwnMass(eater, prey)`: in a pacified world, that domain's fauna do not graze that
  domain's mass anywhere (read by `Cell.IsPreyForHerbivore`, used by LightFauna/worm/NCA/etc. through
  `Fauna.IsPreyForMe`, and by the Boid forager's own diet). Recorded as a fenced carve-out in `Docs/ECOSYSTEM.md` §0.y.
- `ArkEscortSO.pacifyShare` (default 0.6; 0 = off) and param key `escort.pacifyShare` (0-1).
- `ArkEscortRun.TryPacify` (1 Hz, server only, every standing escort world including ones ahead of the Ark): once the
  squad's share of the world's control volume (`GetControlVolume`) reaches pacifyShare, the world is pacified once:
  `SetModeControlOverride(squad)` (live fauna re-coloured, later spawns in the squad's domain), `SparedDomain = squad`,
  `ArmourIsFood = false`. The escort pilots stop hunting turned creatures automatically (`CountThreats` counts only
  other-domain fauna).
- Invariants: no imposed death (re-colour, never cull); mass conserved; one fauna colour per cell (the controller's); no
  Ark weighting (the rule names a domain). A pacified world whose only food left is the squad's own mass starves its
  fauna through the ordinary starvation clock.

## New JSON keys

Leg: `Pacified` (bool), `PacifiedAt` (Time.time like `EnteredAt`, -1 never), `PacifyShare` (squad share at the moment),
`PacifiedAhead` (pacified before the Ark entered), `FaunaTurned` (creatures re-coloured), `SquadShareAtExit`.
Run: `PacifyShare` (the line used), `PacifiedLegs`. `ark_escort_report.py` adds a per-world pacification table
(share of runs pacified, mean seconds after entry, `a` when pacified ahead).

## Not done / caveats

- No sim run yet: whether the AI Squirrel actually reaches 60% of a nucleus claim in time is untested; the escort pilot
  has no "race ahead to the nucleus" or shepherd mode (brief T6 suggests ~30 lines in `ArkEscortPilot`; left for the
  branch that owns the pilot).
- A pacified world stays pinned for the rest of the run (no re-hostiling if the squad's mass is eaten later).
- Creatures that refuse a re-colour (`AcceptsTeamRecolour` false) stay hostile and are not counted in `FaunaTurned`.
- "Destroyers reduce hostile mass" needs no code: hostile-domain volume destroyed lowers the denominator of the share.

## Tuning: pacification must be earned (branch `claude/voyage-pacify-tune`, off voyage-night)

Gates (all must hold, checked at 1 Hz on the server, for any standing escort world):
- the world started **hostile** (a friendly world's nucleus is the squad's colour from its own flora; holding it is not a turn);
- squad share of the world's control volume >= `escort.pacifyShare` (default raised 0.6 -> **0.75**);
- squad control volume >= `escort.pacifyMinVolume` (new, default **25000**);
- squad seats have spent >= `escort.pacifyNucleusSeconds` (new, default **40** s, total) inside its nucleus (membrane
  without a nucleus). This replaces "Ark entered OR dwell": the Ark always enters every world, so it discriminated nothing.

The Squirrel cultivator (`SquirrelRole.ShouldCultivate`) now lays on until the run reports the world pacified
(`ArkEscortRun.IsPacified`, `PacifyMinVolume`) instead of stopping as soon as it leads an empty nucleus.
New leg JSON: `NucleusSeconds`, `PeakSquadControl`, `PeakShare`; run: `PacifyMinVolume`, `PacifyNucleusSeconds`.
`D:\VoyageLogs\run_sim.ps1` takes an optional `-Params path.json` (appends `-arkEscortParams`).

Sims (d4, one at a time, nothing else running; `D:\VoyageLogs\pacify_tune.jsonl`):

| run | gates | pacified | peak squad control per hostile world (Garland / Scarab / Swarm / Maw) | nucleus s |
|---|---|---|---|---|
| Sparrow x3 | first cut (40k, dwell only ahead) | 1 (Boneyard, friendly, at entry) | 18.8k / 14.0k / 17.0k / share 0.24 | 22 / 22 / 56 / 49 |
| Squirrel x3 + `params_squirrel_cultivate.json` | first cut | 0 | 31.9k / 22.1k / 11.8k / share 0.30 | 66 / 99 / 45 / 93 |
| Squirrel x3 + params | **final** (hostile only, 25k, 40 s always) | **1: Scarab Reef, 176 s after entry** | 19.6k / 26.0k / 12.4k / share 0.23 | 60 / 131 / 18 / 54 |

With the final gates the Sparrow run's numbers pacify nothing (no hostile world reached 25k). The Maw is not
pacifiable by either squad (its hostile nucleus holds ~2.3M volume; share stays under 0.3). The Sparrow run was not
repeated on the final gates; its recorded peaks are all below them. Single runs are noisy: re-check over a few seeds.
Params file used: `{"ai.squirrel.cultivate": 1, "ai.squirrel.cultivateWhenThreatsBelow": 3}`
(`D:\VoyageLogs\params_squirrel_cultivate.json`).

Commits f52ced559 + c7001d212 on `claude/voyage-pacify-tune`, pushed. EditMode 40/40 pass, no `error CS`.
Note: after a `-force-d3d11` sim, a `-nographics` test run hung for 20+ min importing `.urtshader` files; running the
tests with `-batchmode -force-d3d11` (no `-nographics`) passed in a few minutes.
