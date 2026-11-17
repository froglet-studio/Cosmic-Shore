# Trawl (`GameModes.Trawl = 118`)

The Jacks' **SPACE** game: trawl a field of prisms with the net. Garrett asked for every missing elemental game on
2026-11-15 ("make all the missing games you can"); the game kit roster (`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 28)
named this one and reserved its ids.

While the jacks net (their flying mode), everything that crosses the mouth between the two fish is caught and
**dragged**. A dragged prism that runs into another destroys both, scored to the net (JACKS.md §1.1, the prism-motion
fundamental in `Docs/PRISM_MOTION.md`). Every hostile prism destroyed is one point. The first **domain** to the target
wins, golf-timed.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameTrawl.unity` (cloned from `MinigameHarvest.unity`) |
| Controller | `TrawlController : MultiplayerDomainGamesController` (Harvest's shape, without its team crystals and reaper) |
| AI | `TrawlSkipper`, the hull's race coach (`IJacksRaceCoach`) that never offers a herding cycle (below) |
| Metric | `ScoringMetric.PrismsDestroyed` (5), reused |
| Rule | `TrawlScoringRuleSO : HarvestScoringRuleSO`, Trawl's own words on its asset |
| Turn monitor | `TrawlPrismTurnMonitor` (Harvest's shape), target from `GetTrawlPrismTarget` |
| Target | `EndConditionOverridesSO.trawlPrismTarget` (**300**, a first cut: nobody has played it yet) |
| Comeback | rate **0.03**: a quarter behind (75 prisms) buys 2.25 element levels, Harvest's own ratio |
| Arena | **Harvest's**, referenced: its four `Harvest Cell Config`s (IntensityWise), its sheaves, chaff and bales |
| Start levels | none: every element at rest (the net is the Jacks' base flight, no upgrade needed) |
| Min players / domains | 1 / 1: a race against the field, not a duel |
| Objective arrow | `HostileMassObjectiveProvider`: the densest stand of hostile, unshielded mass |
| Toasts | 83 (every 60 prisms), 128-130 (shared race beats via `DomainRaceToasts`), 193 (the hint, reserved), 30 (comeback) |
| Generator | `Tools/Build/author_trawl_assets.py` (`--check`) |
| Card art | not rendered yet (below) |

## The loop

1. **Net.** Take off and the jacks fly as a pair with the net between them. Open the fish wide for a bigger mouth.
2. **Sweep.** Fly the mouth through a packed stand. What it catches is dragged along, and a dragged prism that hits
   another breaks both: a dense stand chains.
3. **Read the colours.** A prism in your own colour scores nothing. A shielded prism bounces instead of breaking
   (the LOCKED prism-motion rule), so Harvest's shielded cores from I2 are walls, not points.
4. **Ram nothing.** The netting hull passes through prisms (`IHullContactGate`): the net is the only way to score.
5. **Herding is legal and pointless.** A person can still land and herd (nothing about the Jacks changes, the LOCKED
   vessel rule), but the herd scores nothing here.

## The field

Harvest's field never regrows, so the generator asserts that three domains' targets fit in the smallest rung's
**unshielded hostile pool** for every playable colour (`harvest_budget.py`, Harvest's exact census). Measured on
2026-11-15:

| | Harvest rung | Prisms | Smallest unshielded hostile pool | 3 x target |
|---|---|---:|---:|---:|
| I1 | Stubble: 10 big neutral sheaves, chaff, 4 bales | 4,058 | 4,058 | 900 |
| I2 | Sheaves: shielded cores, a quarter in a team colour | 4,129 | 2,509 | 900 |
| I3 | Stooks: a quarter each Jade, Ruby, Gold and neutral | 4,448 | 2,234 | 900 |
| I4 | Granary: danger stalks and live elemental creatures | 4,561 | 2,307 | 900 |

**Why Harvest's arena.** It already authors a dense, colour-laddered, non-regrowing field of loose prisms, which is
exactly what a net sweeps; forking it would have been a second copy. Harvest's team Space crystals are the Thresher's
blast, laid by Harvest's controller, so Trawl does not lay them.

## AI: the skipper

`TrawlSkipper` is set as every AI Jacks' race coach (`JacksExecutor.RaceCoach`, the seam Decoy Run built). Its
`TryNextCycle` always returns false, so the AI never herds: it stays in its netting mode for the whole match instead of
its shipped 14 s herd / 6 s net rhythm, which is what a person does in this game.

Its flight is the hull's own `AIPilot`, steered through `SetExternalTargetProvider` at
`Cell.GetExplosionTarget(domain)` (the densest hostile, unshielded mass, the query the objective arrow reads) plus an
aim error, and on past it by `aiOvershoot` (160 u) along the line it came in on, so the trailing net sweeps the stand.
It presses nothing. Difficulty is the seat's AI level (`AIDifficultyFor`), never the intensity; the generator fails if
the skipper reads `SelectedIntensity`, presses an input or offers a cycle.

| Ladder (Easy, Medium, Hard, Legendary) | Values |
|---|---|
| `aiRetargetSeconds` | 7, 6, 5, 4 |
| `aiAimError` (u) | 70, 40, 18, 6 |

Nothing has been trained here. Per the AI plan (`Docs/AI_SYSTEM/AI_PLAN.md`) this is the Jacks' Space arcade game,
where its space genome would train.

## Platform changes (all additive)

- `GameModes.Trawl = 118` (reserved by the roster), `EnumIntegrityTests` count +1.
- `GameToastSituation.TrawlHint = 193` (reserved by the roster).
- `EndConditionOverridesSO.trawlPrismTarget` (+ `Build` twin, getter, `TryGetAuthoredTurnTarget` row, the three
  build-value rows) and its rows in the End Game Conditions window.
- `MiniGameHUD`: Trawl reuses `HostileMassObjectiveProvider`. `author_preview_spawns.SCENE_FOR_MODE` maps 118.

## Verification status

Authored headless; **not run in the Unity editor and not compiled**. This session's container could not install a
.NET SDK (the network policy refused Microsoft's download hosts), so neither `unity_refcompile` nor the card renderer
ran. Run offline on 2026-11-15: `author_trawl_assets.py --check` (and watched it fail on a mutated settings asset),
`author_harvest_assets.py --check` (the donor is untouched), `arcade_mode_lib.py --self-test`, the switch-label,
enum-reference, console-logging, conditional-compilation, self-referential-local and using-directive gates,
`check_gamelist_scenes.py`, `author_preview_spawns.py --check`, `author_preview_intensities.py --check`. The hand
tests are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- **Card art.** The card still wears the shared placeholder: run `/cardart` (`render_card_backgrounds.py Trawl`) where a
  .NET 8 SDK is installed. Harvest's field is a spawnable, so it may need a `recipe()` branch like Harvest's own.
- **The target (300) is a first cut.** How fast a net chains through a stand is a playtest question; the pool
  assertion only proves the field cannot run dry.
- **The skipper flies one line per retarget.** It does not turn back to re-sweep a stand, and it does not avoid
  Harvest's danger stalks at I4.
- **Multiplayer nets.** The net's drag is decided where the hull is simulated; a second human as a client is untested.
