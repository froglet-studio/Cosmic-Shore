# Voyage Codex and the Milestone 2 design

Shombith, 2026-11-24. The source copies of two artifacts. Edit the artifact, then copy it here.

- `VoyageCodex.html` is the Voyage Codex: https://claude.ai/artifact/S4aH7rcMZhd6xrSen9oZSD. It covers the four voyage types and the twelve named voyages: lore, givers, recommended squads, how a run plays, and how each changes by difficulty.
- `Milestone2Design.html` is the Milestone 2 gameplay design and plan: https://claude.ai/artifact/41YTUVhkuX1wPDwJQhub3g. It records Shombith's seven answers.

Decisions these pages hold (Shombith, 2026-11-24):
- **Voyages use difficulty 1-9 only.** Never I1-I4 intensity; that ladder belongs to the arcade games. Difficulty is how dangerous the sea is, never a timer.
- **Salvage, Escort, Bounty and Hold the Core are voyage types.** A type is never a player-facing name. Players sail named voyages, each tied to a place, such as Spindrift's Last Glitter, a salvage run at the end of Aurum Banks.
- **Length:** a run lasts 3 to 5 minutes on average, over 1 to 6 cells.
- **Salvage collects loose crystals.**
- **Pilots:** pilot moods include refusals.
- **The detour is a squad vote.**

| Type | Voyage | Place | Difficulty |
|---|---|---|---|
| Salvage | Spindrift's Last Glitter | Spindrift Shallows | 2 |
| Salvage | The Glassmere Shatter | Glassmere | 4 |
| Salvage | Embers of Cinder Shoal | Cinder Shoal | 6 |
| Escort | Lanterns Home | Lantern Reach to Celadon Harbour | 2 |
| Escort | The Tollmoor Crossing | Celadon Harbour to Tollmoor | 4 |
| Escort | Through the Maw's Teeth | Gnawreach to The Maw | 8 |
| Bounty | Hush the Quiet Spear | Magpie Hollow | 2 |
| Bounty | Black Tide Rising | Gullbone Landing | 4 |
| Bounty | The Grin Runs Last | Ironmouth | 8 |
| Hold the Core | The Verdigris Vigil | Verdigris | 1 |
| Hold the Core | Ironmouth Holds | Ironmouth | 4 |
| Hold the Core | The Ninefold Stand | The Ninefold Shell | 8 |

The game code that applies these decisions is changed in its own PRs into `voyages`; see `Docs/Voyages/MILESTONE_2.md`.
