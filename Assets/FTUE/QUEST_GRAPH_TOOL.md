# Quest Graph Tool

One unified system for the FTUE, quest progression, and CTA breadcrumbs: quests are authored
as **phase graphs** in a visual editor, executed by a runner that drives the existing runtime
systems, with every node the player completes persisted to UGS.

**The graph is the source of truth.** Guidance nodes light `CallToActionSystem` breadcrumbs
(the progression service's automatic frontier breadcrumb is suppressed while a quest runs),
gate nodes listen to `GameModeProgressionService`'s own events (intensity tiers, claims), and
progression writes go through the service so the quest-track UI stays in sync.

---

## Data model

```
QuestSO ("MainQuest")                 — ordered list of phases + designer notes
 └─ phases: List<QuestPhaseGraphSO>   — one graph per phase (name + notes + nodes)
     └─ nodes: List<QuestNodeSO>      — sub-assets; edges by stable nodeId + port name
```

A `QuestPhaseEndNode` ends a phase (runner advances to the next); a `QuestEndNode` completes
the whole quest.

## Authoring

- **`FrogletTools ▸ Quest Graph Editor`** — the visual editor:
  - **Left:** quests with their ordered phases (add / reorder ▲▼ / remove ✕) + standalone graphs.
  - **Canvas:** wheel = cursor-anchored zoom · middle/alt-drag = pan · **F** = frame · Delete = delete node ·
    right-click = add node · drag from an output port to connect — release on empty canvas to
    **spawn-and-connect** a new node.
  - **Right:** quest notes, phase notes, the selected node's typed fields, per-port connections,
    and live validation (unreachable nodes, dangling edges, missing dialogue sets, no-terminal phases).
  - Node headers are color-coded by category — toggleable **legend** bottom-left; hover a node
    header or port for a tooltip.
  - New quests/phases are created at the **default location** (`Assets/FTUE/DataContainer/
    Quests|Phases`) — no file dialogs.
- **`FrogletTools ▸ Quest Graph ▸ Create Main Quest (Default Content)`** — generates the whole
  6-phase Main Quest from the design map (see below), with per-phase designer notes baked in.
- **`FrogletTools ▸ Quest Graph ▸ Setup Runner In Scene`** — drops/wires `QuestGraphRunner`
  into the open scene (Menu_Main) and auto-resolves references.

## Node types

| Category (color) | Node | What it does |
|---|---|---|
| Flow | **PlayIntro / PlayOutro** | Captain cinematic in/out |
| Flow | **Wait** | Fixed real-time delay |
| Presentation | **ShowInstruction** | Typewriter text; wait-for-Next or show-and-continue |
| Presentation | **Dialogue** | Plays a `DialogueSet` (any channel incl. **Reward**); advances on `OnDialogueFinished` |
| Gameplay | **EnterFreestyle** | Forces the menu vessel into player control |
| Gameplay | **Navigate** | Force-navigates a screen / the arcade modal |
| Gameplay | **LockModes** | Locks all game cards except the tutorial game |
| Gate | **ExitFreestyle** | Waits for return-to-menu ("press back") |
| Gate | **WaitForInput** | Waits for accepted control inputs (single press) |
| Gate | **WaitForGameLaunch / WaitForGamePlayed** | Game launched / finished — Played supports mode + min-intensity filters |
| Gate | **WaitForIntensity** | Mode reaches an intensity tier (the progression gate) |
| Gate | **WaitForModeUnlocked** | Mode becomes unlocked — i.e. the player **claims** it on the quest track |
| Gate | **WaitForUserAction** | Generic `UserActionType` gate (e.g. `UnlockVessel` from the hangar UI) |
| Guidance | **HighlightCTA** | Lights a CTA breadcrumb (+ dependency path) and waits for its completion action |
| Progression | **UnlockMode** | Direct unlock write via `GameModeProgressionService.UnlockMode` |
| Terminal | **PhaseEnd / End** | Ends the phase / completes the quest |

## The Main Quest (default content = the design map)

```
P0 Onboarding & Crystal Capture: intro → freestyle controls (throttle, steer) → captain dialogue
   → back to menu → CTA arcade → lock to CC → play CC@2 → CTA profile → maps/intensity-4 dialogue
   → play CC@3 → social-UI tour → PhaseEnd
P1 Unlock HexRace:  WaitIntensity(CC,4) → CTA profile → explainer → WaitModeUnlocked(HexRace)=claim
   → reward dialogue → CTA play HexRace → played → PhaseEnd
P2 Unlock Joust:     same pattern (HexRace→Joust)
P3 Unlock Maelstrom: same pattern (Joust→Tournament, card CTA PlayGameMaelstrom)
P4 Vessel Tour:      CTA hangar → tour dialogue → WaitForUserAction(UnlockVessel) → reward → PhaseEnd
P5 Finale:           CTA episodes → closing dialogue → End (quest complete)
```

Interactive/editable reference map (browser): the "Main Quest Progression Map" artifact.

## Runtime

`QuestGraphRunner` (Menu_Main):
- Starts after `GameData.OnClientReady` (vessel exists); also via `FTUEEventManager.InitializeFTUE`.
- Gated by `QuestProgressStore.IsCompleted(questId)`; `debugForceRun` / `debugDisable` for testing;
  `debugPhaseOverride` runs a single phase graph without persistence.
- **Resume:** every completed node is recorded; the runner resumes at the saved phase + node.
- **Breadcrumb authority:** sets `GameModeProgressionService.BreadcrumbSuppressed` while running,
  restores it on completion/teardown.

## Persistence (UGS)

`QuestProgressCloudData` (key `QUEST_GRAPH_PROGRESS`, via `UGSDataService.QuestGraphRepo`):
per-quest records `{ Completed, CurrentPhaseIndex, CurrentNodeId, CompletedNodeIds["phase/nodeId"] }`
with a `PlayerPrefs` mirror for offline/pre-load gating. Every node advance marks the repo dirty
(debounced save). `QuestProgressStore` is the single read/write gate.

## Scene/UI wiring still needed (game-side)

- `CallToActionTarget` components for the new targets: **ProfileMenu (500)**, **EpisodeMenu (600)**,
  **PlayGameMaelstrom (437)** — plus the existing arcade/hangar/game-card targets.
- `ScreenSwitcher` now fires `ViewProfileMenu` on Profile navigation; the episodes screen and the
  hangar's vessel-unlock flow must fire `ViewEpisodeMenu` / `UnlockVessel` (use `UserActionTrigger`
  or `UserActionSystem.Instance.CompleteAction`).
- DialogueSets for the Dialogue nodes (incl. Reward-channel sets for unlock reveals).
- UI-side intensity locking for the "play at intensity N" beats (the graph verifies via the
  WaitGamePlayed filter).

## Notes

- The legacy `TutorialFlowController` / `TutorialSequenceSet` scaffold remains untouched and unused
  by the runner; retire it once the Main Quest ships.
- Node ids are stable GUIDs — UGS records survive renames/reorders. Don't hand-edit `nodeId`.
