# Cave-in (`GameModes.CaveIn = 107`)

The Meerkat's **SPACE** game (`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 17: "the tunnel sucks in prisms and fires
them out to smash others"). Neutral prism plates hang in the cell. Flying into a plate breaks nothing. A soliton formed
beside one **swallows** the prisms around it (`MeerkatSwallow`) and fires them out of its way out. Every fired prism
that runs into another destroys both, credited to the soliton's maker (`PrismMotion.Annihilate`). Tunnel into a plate,
and what comes out of the far end caves in the plate behind it. First DOMAIN to the target of hostile prisms wins,
golf-timed.

| | |
|---|---|
| Scene | `MinigameCaveIn.unity`, cloned from `MinigameShatterplane.unity` (cells, spawn ring and omni crystals kept) |
| Controller | `CaveIn/CaveInController.cs` (Shatterplane's without the crystal field and the nucleus resize, which are the Brittle Star ring's levers) |
| Metric | `ScoringMetric.PrismsDestroyed` (5), Rampage's rule (`CaveInScoringRuleSO : RampageScoringRuleSO`). A Meerkat's own trail and portals are its own colour and never score, so the game stays a Space game |
| Turn monitor | `TurnMonitors/CaveInPrismTurnMonitor.cs` |
| Target | `EndConditionOverridesSO.caveInPrismTarget` (**1000**, one value for every rung): 30% of the smallest rung's breakable prisms (the Reliquary's 3,232), rounded. An estimate |
| Comeback | **0.005** a prism: a quarter-of-target deficit buys 1.25 levels |
| Arena | Shatterplane's four cell configs (`shatterplane_budget.py` is the census): I1 Billboards (ten plates facing out), I2 Gallery (twelve at mixed angles), I3 Shelves (stacks of three parallel plates: one tunnel, three plates), I4 Reliquary (ribbed plates behind super-shielded grilles that never break, and live elemental fauna) |
| Players | 1-4, 2-3 domains (a race against the arena; a solo pilot races the AI) |
| AI | `MeerkatArcadeSteer` with `PickTunnel`. The AI mines one plate for `aiChoicesPerPlate` choices, steering for fresh points across its face (so its solitons form against it). Then it moves on: by `aiLineUpChance` to the plate lined up behind the last one, the way its fired prisms were going, otherwise to the nearest plate. It skips the grilles, takes omni crystals within reach, and now and then raids a rival's trail (also hostile mass) |
| Objective arrow | `HostileMassObjectiveProvider` (Shatterplane's: the densest standing hostile mass) |
| Toasts | 182 hint, 83 every 200 prisms, 128/129/130 race beats, 30 comeback |
| Generator | `Tools/Build/author_cavein_assets.py` (`--check`) |

## What to watch in the first playtest

- How many prisms one soliton swallows from a plate (`suckRadius` is 40 in the shipped config) and how many of the
  fired ones actually strike another. That ratio sets whether 1000 is two minutes or ten.
- Whether Space 5's Steal hurts here. A stolen prism is your colour, so when it strikes a plate, only the plate prism
  scores.
