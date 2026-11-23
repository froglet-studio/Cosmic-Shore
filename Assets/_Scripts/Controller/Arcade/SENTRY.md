# Sentry (`GameModes.Sentry = 105`)

The Meerkat's **CHARGE** game (`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 15). It is a guard war round a heart of omni
crystals. Turn **Sentinel Mode** on (Y). Every soliton you form then leaves a sentinel standing at its way in, an
afterimage of the hull. The sentinel zaps any rival hull inside its guard about five times a second
(`MeerkatSolitonKit.ZapVessel`, MEERKAT.md §10.3). **Every zap is one point**, and the first DOMAIN to the target wins.
The kit already reports a zap as a Bullet-class combat hit in every mode, so the mode only pays for it.

| | |
|---|---|
| Scene | `MinigameSentry.unity`, cloned from `MinigameCorona.unity` (cells, spawn ring and heart crystals kept) |
| Controller | `Sentry/SentryController.cs : MultiplayerDomainGamesController` (Kelpwood's shape) |
| Metric | `ScoringMetric.CombatPoints`, reused. `SentryScoringRuleSO` pays `zapPoints` for `CombatHitClass.Bullet` only and reads `BulletHitsLanded` as the breakdown |
| Turn monitor | `TurnMonitors/SentryZapTurnMonitor.cs : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.sentryZapTarget` (**40**, an estimate). The generator fails if the zap cooldown stops being 0 (the rate the target was sized for) |
| Comeback | **0.12** a point: a quarter-of-target deficit (10 zaps) buys 1.2 element levels |
| Arena | Corona's four cell configs: I1 Halo (one shell, eight gates), I2 Corona, I3 Chromosphere, I4 Flare (four shells, four narrow rimmed gates, one crystal). Every pilot comes in through the gates for the heart, so the gates and the heart are where a sentinel pays |
| Players | 2-4, 2-3 domains (a duel: you cannot zap a teammate) |
| AI | `MeerkatArcadeSteer` with `PickGuardPost`. The AI either holds the heart (`aiHoldHeartChance`, falling with level) or leads the nearest rival by `aiLeadSeconds`, so its soliton's way in, and the sentinel left there, stands on the rival's line. A human rival counts as `aiHumanFocus` times nearer |
| Objective arrow | `BendsObjectiveProvider` (the nearest rival) |
| Toasts | 180 hint, 128/129/130 race beats (`DomainRaceToasts`), 30 comeback |
| Generator | `Tools/Build/author_sentry_assets.py` (`--check`) |

## What to watch in the first playtest

- Whether a sentinel's guard (1.5 x the opening radius) is big enough to be worth placing on a gate.
- Whether 40 zaps is about 90 s. At five zaps a second, one well-placed sentinel can take a large share quickly.
