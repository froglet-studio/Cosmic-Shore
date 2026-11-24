# Time card tuning harness (Starlings Lab, Time race)

Headless runs of the built lab (`../../StarlingsLab.html`) in Playwright, four workers at a time. Job and result files
go to `$STARLINGS_TUNING_OUT` (default `/tmp/starlings_tuning`), never into the repo. Write-up and results:
`Docs/Studios/STARLINGS_LAB.md` § "Tuning the 20 Time cards".

| Script | What it measures |
|---|---|
| `run.py <tag> <n> [tune.json]` | random picks vs the look-ahead picker on n races; the look-ahead's card usage shares |
| `tune.py <rounds> <n>` | scales each card's knob until the look-ahead picks use every card about equally (writes `k.json`, `tune.json`) |
| `strat.py <n> <tune.json> <tag> <rounds>` | fits a strategy (card priority from course + hazards) from a tuning run's look-ahead picks, then races random / a fixed list / the strategy / the look-ahead on n fresh races |
| `combos.py <nenv> [tune.json]` | every pair of Time cards at L10 on the same races: best and worst pairs, how often each card is in a race's winning pair |
| `solo.py <nenv> <level> [tune.json]` | each card alone against no cards |
| `solohz.py <hazard> <nenv> <level> [tune.json]` | each card alone in races with only that hazard |
| `hz.py <n>` | what each hazard alone costs a swarm with no cards |

`tune.json` is `{card id: {stat index: [a, b]}}`, applied through the lab's `__lab.TUNE` before a race; the deck's own
numbers live in `Docs/Studios/SwarmCardVessel/cards.py` (run its `build.py`, then the lab's).
