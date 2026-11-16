# Bullrush - the Rhino's Charge game

`GameModes.Bullrush = 121`, hint toast `BullrushHint = 196` (both reserved for it by
`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 31, 2026-11-15). Generator: `Tools/Build/author_bullrush_assets.py`.

Garrett, 2026-11-15: *"build any missing games ... four games (one for each element is ideal)"*. Bullrush is the
Rhino's Charge game, the roster's "joust on Ramp Spool's top speed".

## The loop, in the Rhino's own terms

The Rhino's Charge ability is the **Gore** (`RhinoGoreBySkimmerEffectSO`, `RHINO_PLOUGH.md` § the 2026-11-12 rework).
Ramming a rival vessel is a scored Strike (`VesselCombatHitByGore`: `hitClass: 5`, `requireFasterThanVictim: 1`,
once per 1.4 s per victim) that knocks the victim's petals loose, and it lands only:

- while the Rhino is **faster than its victim** (so it is a ram the Rhino drives, from behind or head-on at speed,
  never a rival backing into it), and
- while the Rhino is **above the gore speed** (`RhinoGoreSpeed`): 150 u/s at Charge 0, three times the 50 u/s cruise,
  down to 50 u/s at Charge 10.

So a gore at low Charge needs the **Ramp Spool** (Time): the ramp only winds up on a straight line at full throttle,
and turning bleeds it. Charge is how short the wind-up can be. That is the joust: find a rival, get a straight run at
them, arrive faster than they are flying. Each gore is **one point**; the first **domain** to the target (5) wins
(golf-timed, like Jackstraw).

> The roster row said "the plough never touches a hull". That was written before #1927 landed the Gore; the Gore IS a
> speed joust on rival hulls, so this game scores it directly. No platform joust effect was added to the Rhino.

## What is reused, and why

| Piece | From | Why |
|---|---|---|
| Match shape, crystals, hunters | `JackstrawController` (`BullrushController` subclasses it) | The same game: a Strike race on a hull's existing contact hit. Bullrush adds only its opening hint. |
| Rule | a `JackstrawScoringRuleSO` asset (`BullrushScoringRule`) | Pays a Strike `strikePoints` (1), nothing else; the words are the Rhino's. |
| Settings | a `JackstrawSettingsSO` asset (`BullrushSettings`) | Crystals per clearing, the hunters' ladders, with a Rhino's numbers. |
| Arena | Jackstraw's four heap cells, referenced | See below. |
| Scene | cloned from `MinigameJackstraw.unity` | controller, monitor, settings and rule swapped; four Rhino AI templates. |
| Monitor | `BullrushStrikeTurnMonitor : CombatPointTurnMonitorBase` | Reads `EndConditionOverridesSO.GetBullrushGoreTarget()`. |
| Objective arrow | `BendsObjectiveProvider` | The nearest pilot this player may gore. |

**Why Jackstraw's heap.** For a Thresher the heap is a price (every stick its ball breaks costs speed). For a Rhino it
is cover: the plough throws every stick aside at no cost in speed (the Rhino carries no speed-by-prism effect), Carry
breaks the ones it throws into others, and the red-tipped danger sticks still sting. The heap hides rivals, and the
clearings and the runs between them are where a straight wind-up fits. Intensity is the heap's (34 loose sticks; then
jacks, red tips and ironwood; up to 230 sticks and 26 jacks at I4). A Rhino-shaped arena (lanes for the run-up) is the
obvious next step if play shows the heap does not ask the joust's question.

## The AI

`JackstrawHunter`, steering only (the hull's own `RampBoostAIPolicySO` presses the ramp on straights), keyed on each
seat's AI difficulty, never the intensity. The Rhino's numbers: a longer hunt range (it needs the run-up), less lead
(it rams from behind rather than swinging ahead) and a longer overshoot (a gore is a ram through the rival). Silenced
hunters refuel at the clearings' crystals. Nothing presses the plough steer (it has no AI policy yet).

## Platform rows it added

`GameModes.Bullrush`, `GameToastSituation.BullrushHint`, `EndConditionOverridesSO.bullrushGoreTarget` (+ Build twin,
getter, `TryGetAuthoredTurnTarget`, live/build rows, window rows), `MiniGameHUD` objective row,
`author_preview_spawns.SCENE_FOR_MODE`, the card on the Arcade grid, always unlocked, the build scene list, the toast
and preview libraries.

## Verification status

Authored headless, **not yet run in the editor**. Nothing in Unity has seen it: open it from the Arcade grid, check a
gore at cruise does not score at Charge 0 and does at Charge 10, and that the AI winds up before it rams.
