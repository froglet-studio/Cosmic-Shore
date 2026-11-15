# Den Builder - the Goby & Pistol's Mass game

`GameModes.DenBuilder = 115`, hint toast `DenBuilderHint = 190` (both reserved in
`Docs/ElementalLoop/GAME_KIT_ROSTER.md`, row 25). Authored by `Tools/Build/author_den_builder_assets.py` (shared pieces
in `goby_games_lib.py`). Built 2026-11-15. **Authored headless, not yet run in the editor.**

## The loop, in the pair's own terms

A timed build in Glasshouse's grazed cell. The **Goby** darts and perches, laying the Pistol's islands. The **Pistol**
rides them Tiny Wings style: every trail block it flies over instead of riding gives it **energy** (the nth block of a
flight gives 1 + 0.2 n, so big jumps pay), and a fall into a gap loses it all. At each perch it rests and builds a
**den** from that energy (the Mass ability, Dens: one prism per 8 energy, x1 -> x2.5 with Mass, at most 80; at Mass 5,
Fortress, danger and super-shielded prisms too). The mass standing in your colour when the clock runs out wins.

## What it owns, and what it reuses

| Decision | Den Builder |
|---|---|
| Metric | `VolumeRemaining` (12), counted by `StatsManager` for every prism the pair lays (`BoostRingBuilder.LayOne` with the pilot's name): dens, perches and the islands' ground. `DenBuilderScoringRuleSO` is Glasshouse's rule inherited (timed, highest wins, points-mode reveal) with its own words; the breakdown is each pilot's standing prism count from the whistle's snapshot. |
| Target | None: TIMED. `EndConditionOverridesSO.denBuilderRoundSeconds` (150), read by `DenBuilderTimeTurnMonitor`. Longer than the arcade's 90 s because a den needs a ridden island first (900-1800 u of hills). The score is a live stock that grazers and rival rings eat, so a race to a number would end on a value that was true for one frame (Tapestry's reason). |
| Comeback | 0.0005 per unit of volume behind, against a reference of about 9,000 standing per domain (three or four dens of ~40 prisms of 64 u^3, plus trail): a quarter behind buys ~1.1 element levels. An estimate. |
| Arena | **Glasshouse's four cells, referenced.** Their herds graze whatever stands outside the NUCLEUS whatever its colour, and the nucleus is a sanctuary, so a den built in the open is food and one built near the core is safe but crowded - the arena's choice of where to perch, not the vessel's. Glasshouse's own species is planted only from an Angler's seed (`SpawnProbability: 0`), so no garden grows here. I1-I4 add grazers (6 -> 15) and thin the omni crystals (4 -> 2). |
| AI | **None of the mode's own.** An AI pair is its seat's crew: the Goby lays islands, the Pistol jumps them and builds at every perch. |
| Lead toast | A timed mode has no target, so the controller samples the lead itself (Glasshouse's sampler, three serialized fields on the controller) and posts the shared 130. |
| Min players / domains | 1 / 1: the score is what you build, so a solo run is a real game against the grazers. |

Nothing about the pair changes. The scene is Glasshouse's, cloned through `lib.committed_scene`: its timed monitor
swapped for Den Builder's (same block shape), the controller and rule swapped, the donor's settings and arena-cell
fields replaced by the lead fields, the four AI templates turned from Anglers into Gobies.

## Verification status

Authored headless: the generator's `--check`, the reference compile and the static gates. **Not run in Unity.** What
only a person can judge: whether 150 s is the right length, whether the grazers eat dens fast enough to matter (or so
fast it is hopeless), and whether `StatsManager` credits den prisms as expected (it does for every `LayOne` with a
pilot name in code; nobody has watched the number move).

## Known limitations

- The islands' trail is mass too, so a pair that only lays islands still scores a little. Dens are worth far more per
  prism (64 u^3 against about 1.3), so the den loop is the game.
