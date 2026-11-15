# Pod Hunt (`GameModes.PodHunt = 99`)

The Orca's **CHARGE** game (the game kit roster, `Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 9, built 2026-11-15 on
Garrett's "make all the missing games you can"): a pod of crewed hulls hunting each other through the Boneyard's
wreckage. Every **blast** that reaches a rival hull is one point: a gunner's missile (direct, blast or shockwave) or
the bomb's burst. The guns score nothing; they break the cover a rival hides behind. The first **domain** to the
target wins, golf-timed.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigamePodHunt.unity` (cloned from `MinigameKelpwood.unity`) |
| Controller | `PodHuntController : MultiplayerDomainGamesController` |
| AI | the crew's own gunners (`OrcaCrewAI`), plus `OrcaArcadeHelm` (goal Hunt), which only steers |
| Metric | `ScoringMetric.CombatPoints` (8), reused. The genre badge reads Charge through `ModeGenre`'s metric fallback |
| Rule | `PodHuntScoringRuleSO`: MissileDirect, MissileBlast and MissileShockwave pay **1**, every other class **0** (an exhaustive switch, Dog Fight's reason) |
| Turn monitor | `PodHuntBlastTurnMonitor : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.podHuntBlastTarget` (**15**, a first cut: nobody has played it yet) |
| Comeback | rate **0.3**: a quarter behind (3.75 blasts) buys 1.1 element levels |
| Arena | the Boneyard's four cells, REFERENCED (Salvo's and Dog Fight's arena): wreckage inside r 520, no nucleus |
| Spawn | ring floor 700 at every rung (Salvo's), outside the wreck and inside the AI's ~720 u missile reach |
| Omni crystals | the scene's `NetworkCrystalManager`: 4 / 3 / 3 / 2 in a 420 u ball. An omni crystal reloads every gunner seat (`OrcaCrew.OnOmniCrystal`) |
| Objective arrow | `BendsObjectiveProvider`: the nearest rival pilot |
| Toasts | 128-130 (shared race beats via `DomainRaceToasts`), 174 (the hint, new), 30 (comeback) |
| Settings | `PodHuntSettings.asset` (`OrcaArcadeSettingsSO`, shared by the three Orca games) |
| Generator | `Tools/Build/author_pod_hunt_assets.py` (`--check`), on `orca_games_common.py` |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/PodHunt.png` (`/cardart`: the Boneyard environment at intensity 2) |

## The loop, in the Orca's own terms

The Orca is four seats on one hull (`ORCA_CREW.md`): the helm flies and raises the danger shield, the sniper and
the fast gun are guns, and the bomb seat throws the bomb. Each gunner seat carries a magazine of missiles. A missile
flown into a hull is a point. So is the bomb's burst when it catches one. The game is getting a missile to the far
side of the wreck while yours are coming back at you: break their cover with the guns, take an omni crystal to
reload all three seats at once, and raise the shield when a missile locks you.

Intensity is the Boneyard's own ladder (9,043 prisms of wreckage at I1 to 34,654 at I4, with more danger traps), and
the omni crystals thin from four to two.

## AI

Nothing about how an Orca fights changes. The crew's AI already aims, fires and flies missiles at hulls, prisms and
crystals. What the mode adds is the **helm**: `OrcaArcadeHelm` sets each AI hull's steering target through
`AIPilot.SetExternalTargetProvider` (Corona's shape). In Hunt it stands the hull off its cheapest rival (closest, a
human weighted by `aiHumanFocus`), swung 35 degrees to one side, at the rung's `aiStandoff`; when every magazine is
under `aiRefuelBelowMissiles` it flies to the nearest omni crystal instead. Its ladders key on the seat's AI
difficulty, never the intensity.

## Platform changes (all additive)

- `GameModes.PodHunt = 99` and `GameToastSituation.PodHuntHint = 174`, the ids the game kit roster reserved;
  `EnumIntegrityTests` counts all three Orca modes.
- `EndConditionOverridesSO.podHuntBlastTarget` (+ `Build` twin, getter, `TryGetAuthoredTurnTarget` row, the three
  build-value rows) and its row in the End Game Conditions window.
- `MiniGameHUD`: Pod Hunt reuses `BendsObjectiveProvider`.

## Verification status

Authored headless; **not yet run in the Unity editor**. See the PR and `Docs/UNITY_VERIFICATION_CHECKLIST.md` for
what ran offline and the hand tests.

## Known limitations

- **The AI helm raises its shield only against an incoming missile** (the crew AI's own rule). A mode cannot change
  that without changing the vessel; a coach hook for it is the Orca owner's call.
- Whether a client's Orca crew fires its own seats was not checked in a networked match.
- The target (15) and the comeback rate are first cuts. Nothing measures blasts per minute yet.
