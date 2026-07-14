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
  - **Left:** quests with their ordered phases (add / reorder ▲▼ / remove ✕) and per-quest /
    per-phase **enable toggles** (test harness).
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
| Flow | **Wait** | Fixed real-time delay |
| Presentation | **ShowInstruction** | Typewriter text; wait-for-Next or show-and-continue |
| Presentation | **Dialogue** | Shows the dialogue panel with lines authored ON the node (speaker, optional portrait override) — self-contained, no DialogueSet/DialogueManager |
| Gameplay | **EnterFreestyle** | Forces the menu vessel into player control |
| Gameplay | **Navigate** | Force-navigates a screen / the arcade modal |
| Gameplay | **LockModes** | Locks all game cards except the tutorial game (one-shot; prefer SetArcadeConstraints — it survives card-grid repopulation and scene reloads) |
| Gameplay | **SetArcadeConstraints** | Funnels the arcade: one clickable game card, one selectable intensity, player count defaulted to max, domain count defaulted (e.g. 3). Static — survives the Menu→game→Menu round-trip; author a Clear node when the funnel ends |
| Gameplay | **LockNavigation** | Disables every footer nav button except the Arcade button (or unlocks all). Buttons auto-wired by the Phase 0 wirer; always restored on quest teardown |
| Gate | **ExitFreestyle** | Passive: waits for the player's own return-to-menu (the volume/pause button calls ToggleTransition — it IS the taught exit). `forceExit`: drives the return itself |
| Gate | **WaitForInput** | Waits for accepted control inputs (single press) |
| Gate | **WaitForGameLaunch / WaitForGamePlayed** | Game launched / finished — Played supports mode + min-intensity filters |
| Gate | **WaitForIntensity** | Mode reaches an intensity tier (the progression gate) |
| Gate | **WaitForModeUnlocked** | Mode becomes unlocked — i.e. the player **claims** it on the quest track |
| Gate | **WaitForUserAction** | Generic `UserActionType` gate (e.g. `UnlockVessel` from the hangar UI) |
| Gate | **WaitForDrift** | Local vessel `IsDrifting` (LT+RT) sustained for a hold time; success haptic |
| Gate | **WaitForSkim** | Counts prisms skimmed via the skim-boost SOAP channel (local vessel, boost-increase filtered); the live "n / target" count is appended to the active instruction set's own text; per-skim + completion haptics |
| Guidance | **HighlightCTA** | Lights a CTA breadcrumb (+ dependency path) and waits for its completion action |
| Progression | **UnlockMode** | Direct unlock write via `GameModeProgressionService.UnlockMode` |
| Terminal | **PhaseEnd / End** | Ends the phase / completes the quest |

## The Main Quest (default content = the design map)

```
P0 Onboarding & Crystal Capture: camera → player vessel (enter freestyle on menu ready; vessel
   HUD hidden + A/X/B suppressed) → speed up / slow down / look around / drift L+R / skim×10
   (counter) → tap VOLUME button (passive exit — never forced) → nav locked to Arcade + dialogue
   → arcade funnel (CC only, intensity 2, max players, 3 domains) → play CC@2 → nav unlocked
   → CTA profile → maps/intensity-4 dialogue → funnel intensity 3 → play CC@3 → funnel cleared
   → social-UI tour → PhaseEnd
P1 Unlock HexRace:  WaitIntensity(CC,4) → CTA profile → explainer → WaitModeUnlocked(HexRace)=claim
   → reward dialogue → CTA play HexRace → played → PhaseEnd
P2 Unlock Joust:     same pattern (HexRace→Joust)
P3 Unlock Maelstrom: same pattern (Joust→Tournament, card CTA PlayGameMaelstrom)
P4 Vessel Tour:      CTA hangar → tour dialogue → WaitForUserAction(UnlockVessel) → reward → PhaseEnd
P5 Finale:           CTA episodes → closing dialogue → End (quest complete)
```

Interactive/editable reference map (browser): the "Main Quest Progression Map" artifact.

## Test harness

- **Quest / Phase / Node enable toggles** — checkboxes in the left panel (quest, phase), on the
  node card header, in the right-panel inspectors, and in the node context menu. The runner
  never starts a disabled quest, skips disabled phases, and passes straight through disabled
  nodes (following their `next` edge) — so you can mute any beat without unwiring it.
- **Reset ALL Player Progress** (quest inspector) — clears the quest's PlayerPrefs mirror; in
  Play mode also resets mode/intensity progression, vessel unlocks, and arcade constraints
  (plus the UGS cloud records when the backend gate is open).
- **Backend gate** — `ProgressionBackendGate.CloudEnabled` (in `_Scripts/System/Progression/`)
  is currently **false**: quest progress is PlayerPrefs-only and mode/intensity progression is
  session-local (fresh every play). Flip it to `true` to restore cloud sync once the FTUE is
  signed off. The quest inspector shows a LOCAL-ONLY banner while the gate is closed.
- **Rename Asset** (quest inspector) — renames the QuestSO asset in place.
- **Panels** — both sidebars hide/show from the toolbar (◧ Quests / Inspector ◨) and resize by
  dragging their inner edge; the on-canvas node-color legend toggles with "Node Colors". All
  persisted per user.
- **Checkpoint view** — the canvas overlays the player's LOCAL progress: ✓ on every node the
  player completed, an amber **▶ NEXT** banner on the node the quest resumes at, a ▶ marker on
  the current phase in the quest list, and a "PLAYER PROGRESS" readout in the quest inspector.
  Updates live during Play mode (~2×/s). Reset clears it.
- **Force-Advance** (quest inspector, Play mode) — completes the ▶ NEXT node as if the player
  did it (skip a game, a gate, a dialogue) so a full test pass doesn't require replaying every
  beat. Persists progress exactly like a real advance.
- **PlayGame user action** — `SceneLoader.LaunchGame` completes `UserActionType.PlayGame` at
  every game launch (while the menu listeners are still alive). Play-game CTA nodes complete
  on launch; the following WaitForGamePlayed gate holds until the run actually finishes.
- **Arcade funnel persistence** — `QuestArcadeConstraints` is persisted in PlayerPrefs with the
  quest cursor, so a play-session restart mid-quest keeps the arcade funnel (one card, one
  intensity, authored player/domain counts) instead of silently unlocking everything.
- **GameModeProgressionService** — the wirer now creates + wires it in Menu_Main if missing
  (it previously existed in NO scene, so `Instance` was null: no card locks, no play tracking,
  and played-game gates could never resume).
- Runner-side: `debugDisable`, `debugForceRun` (ignore completion + saved progress), and
  `debugPhaseOverride` (run one phase graph without persistence).

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

## UI contracts (drop-in components for the hand-built UI)

| Component | Drop on | Drives |
|---|---|---|
| `QuestInstructionView` | a Menu_Main panel (CanvasGroup + TMP text) | **ShowInstruction** nodes — plain text + optional haptic pulse; prompt persists while gates hold |
| `QuestDialoguePanelView` | the captain dialogue panel (portrait, TMP body, Next/Skip buttons) | **Dialogue** nodes drive it directly via `PlayLines` (lines authored on the node) — typewriter, next-to-advance, skip-fast-forwards. Scene instance preferred; runner can instantiate a prefab fallback |
| `QuestRewardRevealView` (`IDialogueView`) | the reward panel inside the profile screen (icon, title, description, rarity, Continue) | Reward-channel **Dialogue** nodes via the resolver's Reward slot — shows the set's `RewardData` |
| `QuestToastNotifier` | any Menu_Main object | Toasts: mode unlocked, intensity tier, quest-track objective met, phase complete, quest complete (templates editable) |
| `UserActionTrigger` (+ new `triggerOnEnable`) | the episode panel (or any panel) | Fires its `UserActionType` (e.g. `ViewEpisodeMenu`) when the panel opens — completes CTAs + gates |

ShowInstruction node fields: `text`, `haptic` (NiceVibrations preset via `HapticType`),
`panelKey` (a hand-built CanvasGroup panel registered on `QuestInstructionView` — icons + text,
animatable), `minDisplaySeconds`, `hideOnAdvance`. Gate nodes (WaitForInput / WaitForDrift /
WaitForSkim) pulse a configurable `successHaptic` when the player performs the correct action.

Control icon sprites (white, tintable, animatable parts) live at
`Assets/_Graphics/UI/ControlIcons/`: Thumbstick_Base/Cap, Arrow_Chevron, Thumbsticks_Outward/
Inward, Thumbstick_Look, Trigger_LT/RT, Button_B.

## Scene/UI wiring still needed (game-side)

- `CallToActionTarget` components for the new targets: **ProfileMenu (500)**, **EpisodeMenu (600)**,
  **PlayGameMaelstrom (437)** — plus the existing arcade/hangar/game-card targets.
- `ScreenSwitcher` now fires `ViewProfileMenu` on Profile navigation; the episodes screen and the
  hangar's vessel-unlock flow must fire `ViewEpisodeMenu` / `UnlockVessel` (use `UserActionTrigger`
  or `UserActionSystem.Instance.CompleteAction`).
- UI-side intensity locking for the "play at intensity N" beats (the graph verifies via the
  WaitGamePlayed filter).

## Notes

- Node ids are stable GUIDs — UGS records survive renames/reorders. Don't hand-edit `nodeId`.
