# FTUE Graph Tool

A ScriptableObject + node-graph system for authoring the First-Time-User-Experience as a
flowchart, driving gameplay actions, embedding dialogue, and binding into the CTA system.

It **extends** the existing `Assets/FTUE` scaffold (the `TutorialStep`/handler/`FTUEIntroAnimator`
pieces are reused) and **replaces** the flat `TutorialSequenceSet` list — which could not express
branches — with a real graph.

---

## Authoring

1. **Create a graph:** `FrogletTools ▸ FTUE ▸ Create Default FTUE Graph` builds the canonical
   flow (below) as `Assets/FTUE/DataContainer/FTUEGraph_Default.asset`. Or make an empty one from
   the editor's **+ New Graph**.
2. **Edit it:** `FrogletTools ▸ FTUE ▸ Graph Editor`.
   - **Left** — pick / create / delete graph assets.
   - **Center** — drag node headers to arrange; **click an output port then a node** to connect;
     middle-drag to pan; right-click a node for options.
   - **Right** — the selected node's typed fields, its per-port **Connections** popups (the
     reliable way to wire edges), **Set As Entry Node**, and live **Validation**
     (unreachable nodes, dangling edges, empty text, missing dialogue set).
3. **Assign the dialogue set** on the `Captain Intro` node (author it in the Dialogue Editor first).

## Node types

| Node | What it does | Backed by |
|---|---|---|
| **PlayIntro / PlayOutro** | Captain slide-in / slide-out cinematic | `FTUEIntroAnimator` |
| **ShowInstruction** | Typewriter instruction; wait-for-Next or show-and-continue | `TutorialUIView.ShowStep` |
| **Dialogue** | Play an embedded `DialogueSet` | `DialogueManager.PlayDialogueSet` + new `OnDialogueFinished` |
| **EnterFreestyle** | Force the menu vessel into freestyle control | `MenuCrystalClickHandler.ToggleTransition` |
| **ExitFreestyle** | Wait for the player to return to the menu ("press back") | `OnMenuStateTransitionEnd` |
| **WaitForInput** | Wait until the player performs a control input | shared `OnButtonPressed` SOAP channel |
| **HighlightCTA** | Light a CTA target and wait for the user to satisfy it | `CallToActionSystem` + `UserActionSystem` |
| **LockModes** | Lock all arcade cards except the tutorial game | game-card buttons |
| **Navigate** | Force navigation to a menu screen / arcade modal | `ScreenSwitcher` |
| **WaitForGameLaunch / WaitForGamePlayed** | Gate on launch / finish of a game | `GameDataSO.OnLaunchGame` / `OnMiniGameEnd` |
| **Wait** | Fixed real-time delay | — |
| **SetPhase** | Record + persist the reached `TutorialPhase` | persistence |
| **End** | Mark the FTUE complete (persist + gate future runs) | `FTUECompletionStore` |

Branching: nodes carry named output ports. The base set is linear (`next`); the framework
supports extra ports (e.g. `onTimeout`) without a schema change.

## Canonical flow (the default graph)

```
Intro Cinematic → Enter Freestyle
  → Prompt: Throttle → Wait: Full Throttle
  → Prompt: Steer    → Wait: Steer
  → Captain Intro (DialogueSet)
  → Prompt: Head Back → Wait: Return To Menu
  → Prompt: Open Arcade → CTA: Arcade
  → Lock To Tutorial Game → CTA: Play Tutorial Game
  → Wait: Game Launched → Advance To Phase 2 → Wait: Game Finished
  → FTUE Complete
```

## Runtime

`FTUEGraphRunner` (drop on the Menu_Main "Game" object) loads a graph and drives it:

- Starts after the menu autopilot vessel exists (`GameData.OnClientReady`) so freestyle/input
  steps have a live vessel; also startable via `FTUEEventManager.InitializeFTUE`.
- Gates on `FTUECompletionStore` — runs only if not already completed (or `debugForceRun`).
- Each node runs its own `Execute`, then the runner follows the edge for the port it advanced on.
- Per-node event subscriptions are tracked and torn down on every advance/stop, so nothing leaks
  onto a persistent SOAP asset.

### Scene wiring

Run `FrogletTools ▸ FTUE ▸ Setup Runner In Scene` with **Menu_Main open**. It adds the runner and
auto-resolves what it can (graph, `GameDataSO`, freestyle events, input-pressed channel,
`MenuCrystalClickHandler`, `TutorialUIView`, `FTUEIntroAnimator`, `DialogueManager`,
`ScreenSwitcher`, game cards). It logs anything left to wire by hand. The `onButtonPressed` field
must be the **same** `ScriptableEventInputEvents` asset the vessel's `InputStatus` raises.

## Persistence (cloud)

Completion is stored per-account in UGS Cloud Save (`FTUECloudData`, key `FTUE_PROGRESS`, via
`UGSDataService.FtueRepo`) with a `PlayerPrefs` mirror for offline / pre-load gating.
`FTUECompletionStore.IsCompleted()` is the single gate; `MarkCompleted()` / `SaveProgress()` write
both. Reset cloud data through `UGSDataService.ResetAllDataAsync`; `FTUECompletionStore.ResetLocal()`
clears only the local mirror (debug).

## Notes / follow-ups

- **Input fidelity is single-press** (v1 decision) — a step completes on the first matching
  `InputEvents`. Hold-duration / analog-threshold steps would need a small evaluator.
- **Dialogue completion** uses the new additive `DialogueManager.OnDialogueFinished` event (no polling).
- The old `TutorialFlowController` / `TutorialSequenceSet` path is left intact and unused by the
  runner; migrate remaining content into a graph and retire it when ready.
