# Lava-Lamp Mode (Menu Freestyle Merge) - Reference

> Extracted verbatim from `CLAUDE.md` (2026-07-23) so the root file stays a lean
> rules-and-routing dictionary. This is the canonical home of this content now -
> update it here, and keep the corresponding CLAUDE.md digest in sync.

### Lava-Lamp Mode (Menu Freestyle Merge)

**Naming: "lava lamp" and "freestyle" are the same thing.** When viewed from the menu (autopilot vessels drifting behind the UI) it is called the *lava lamp*; when the player takes control and flies it is called *freestyle*. One system, two names. BOTH standalone freestyle games are retired and must not be reintroduced: the old arcade "Freestyle" (`GameModes.Freestyle = 7`) and the standalone multiplayer sandbox (`MultiplayerFreestyle = 28`, deleted 2026-07-21). The lava lamp is the only freestyle — party members fly it together in Menu_Main.

Lava-lamp mode hosts freestyle gameplay directly in Menu_Main: the autopilot vessel becomes playable when the player enters freestyle mode. Game UI panels (MiniGameHUD, Scoreboard, Vessel Selection, Vessel HUDs, PlayerScoreCards, EndShapeDetailHUD) live under Menu_Main's "Game UI" container and fade in/out with the freestyle toggle.

#### Design Principles

- **Individual panels, not GameCanvas prefab**: Extract needed UI panels as scene-level objects under "Game UI" — do not instantiate the full `GameCanvas.prefab`. The GameCanvas prefab bundles a `Canvas` + `CanvasScaler` + `GraphicRaycaster` root that would conflict with Menu_Main's existing Canvas.
- **Reuse existing SOAP pipeline**: `MenuCrystalClickHandler` already toggles autopilot↔freestyle with CanvasGroup fading. "Game UI" `CanvasGroup` is already wired into its `freestyleCanvasGroups[]` array. `MainMenuController` already has `MainMenuState.Freestyle`. No new states or SOAP events needed.
- **Network-aware vessel selection**: Use `MenuVesselSelectionPanelController` — it delegates vessel swaps to `MenuServerPlayerVesselInitializer` via the Netcode despawn/spawn/RPC pipeline so changes replicate to all clients. (The legacy singleplayer `VesselSelectionPanelController` was deleted with the SP path.)
- **Phased rollout**: Phase 1 (core HUD + vessel selection), Phase 2 (shape drawing), Phase 3 (scoring).

#### Current "Game UI" Container

The existing "Game UI" in Menu_Main has two children:

```
Game UI [RectTransform, CanvasGroup]                    ← already in freestyleCanvasGroups[]
├── MiniGameHUD [RectTransform, CanvasGroup, MenuMiniGameHUD]
│   └── Volume / Pause Button [Image, Button, MenuAudio]
│       └── MenuMiniGameHUD.Awake() wires onClick → vesselSelectionPanel.Open() + Hide()
│
└── Vessel Selection Panel [CanvasGroup, VesselSelectionPanelUI, MenuVesselSelectionPanelController]
    ├── Buttons (Resume, Close) → onClick includes MenuMiniGameHUD.Show()
    └── Menu [GridLayout, 6× ShipCardView]
```

`MenuMiniGameHUD` (`_Scripts/UI/MenuMiniGameHUD.cs`) is a slim alternative to the full `MiniGameHUD` for menu freestyle mode. It provides the Volume/Pause icon button that opens the `MenuVesselSelectionPanelController` panel, vessel HUD reparenting via the `onShipHUDInitialized` SOAP event, and runtime PauseMenu prefab instantiation. The button is visible when Game UI fades in during freestyle, hidden when returning to menu. The full `MiniGameHUD` can replace this when Phase 2/3 features (shape drawing, scoring) are needed.

**Freestyle input ownership + HUD-after-swap (do not regress).** The menu ("appshell") and the vessel both poll the one gamepad, so ownership must be exclusive: in freestyle `ScreenSwitcher.HandleEnterFreestyle` sets `EventSystem.sendNavigationEvents = false` (restored on exit) so the pad flies the ship and no longer double-drives the UI selection ring / Submit on the still-touch-interactable vessel HUD (`ScreenSwitcher.Update` screen-nav was already gated on `_isInFreestyle`; the vessel is paused in menu state). `MenuMiniGameHUD.Update` polls **gamepad Start** while in freestyle → `MenuCrystalClickHandler.ToggleTransition()`, the pad counterpart to the on-screen Volume/Pause exit. On a runtime **vessel swap**, `VesselController.Initialize` creates the new HUD hidden and the swap never re-enters freestyle, so `ClientPlayerVesselInitializer.ReInitializePair` re-raises `GameDataSO.OnPlayerPairInitialized` and `MenuMiniGameHUD` re-shows the local HUD (gated on freestyle + local player) — the `onShipHUDInitialized`/`ShipHUD` reparent path is dead for menu vessels (no `ShipHUD` on the vessel prefabs). See `Docs/ToySystem/ARCHITECTURE.md`.

#### Phase 1: Core Freestyle HUD (target hierarchy)

```
Game UI [RectTransform, CanvasGroup]
├── MiniGameHUD [CanvasGroup, MiniGameHUD, MiniGameHUDView, SOAP listeners]
│   ├── ReadyButton [INACTIVE — no countdown in lava-lamp]
│   ├── Volume / Pause Button
│   ├── Scoreboard (inline score TMP)
│   ├── RoundTime (rotating circles + countdown TMP)
│   ├── LifeFormCounter (rotating circles + counter TMP)
│   ├── ThumbCursors (LeftCursor, RightCursor — ThumbCursor)
│   └── PlayerScoreContainer [Transform — for dynamically instantiated PlayerScoreCards]
│
│   (No toast panel here. `GameEventFeed` was retired with the game-toast system;
│    its replacement — `NotificationUI.prefab` [GameToastController + GameToastView] —
│    is per-scene and is NOT instanced in Menu_Main, so the lava lamp shows no toasts.)
│
├── Vessel Selection Panel [CanvasGroup, VesselSelectionPanelUI, MenuVesselSelectionPanelController]
│   ├── Buttons (Resume, Close)
│   └── Menu [GridLayout, 6× ShipCardView]
│
├── ScoreboardController [Scoreboard.cs — hidden by default, no OnShowGameEndScreen in basic freestyle]
│   ├── SinglePlayerView
│   ├── MultiplayerView (4 player rows, winner banner)
│   └── Buttons (PlayAgain, Home)
│
└── EndGameShapePanel [EndShapeDetailHUD — INACTIVE, Phase 2]
    ├── Shape stats (name, time, par, accuracy, star rating)
    ├── ScreenShotButton
    └── ExitShapeButton
```

#### MiniGameHUD Configuration for Menu

| Setting | Value | Rationale |
|---|---|---|
| `enablePreGameCinematic` | `false` | No cinematic in menu freestyle |
| `isAIAvailable` | `false` | No AI score tracking in basic lava-lamp (Phase 3) |
| `minConnectingSeconds` | `0` | No connecting panel delay |
| `preGameCinematic` | `null` | Not needed |
| `onMoundDroneSpawned` | `null` | No drones in menu |
| `onQueenDroneSpawned` | `null` | No drones in menu |
| `scoreboard` | Wire to ScoreboardController | Present but hidden |

**SOAP events to wire on MiniGameHUD GO:**
- `EventListenerPipData` → `onShipHUDInitialized` (vessel HUD reparenting)
- `EventListenerBool` → optional, for turn visibility toggling

#### Vessel HUD Lifecycle in Menu

Vessel HUDs reparent into "Game UI" automatically through the existing SOAP pipeline — no code changes needed:

```
Vessel spawned (MenuServerPlayerVesselInitializer)
  └─ ShipHUD.Start() [on vessel prefab]
      └─ onShipHUDInitialized.Raise(ShipHUDData)
          └─ MiniGameHUD.OnShipHUDInitialized()
              └─ Reparents HUD children under transform.parent (= "Game UI")
```

HUD children persist across freestyle toggles. Their visibility is controlled by the "Game UI" `CanvasGroup.alpha` that `MenuCrystalClickHandler` already fades.

Per-vessel HUD controllers (`IVesselHUDController` implementors):

| Vessel | Controller | View |
|---|---|---|
| Manta | `MantaHUDController` | `MantaHUDView` |
| Rhino | `RhinoHUDController` | `RhinoHUDView` |
| Serpent | `SerpentHUDController` | `SerpentHUDView` |
| Sparrow | `SparrowHUDController` | `SparrowHUDView` |
| Dolphin | — | `DolphinHUDView` |
| Squirrel | — | `SquirrelHUDView` |

HUD prefab variants at `_Prefabs/UI Elements/VesselHUD/` (e.g., `MantaHUDVariant.prefab`, `DolphinHUDVariant.prefab`).

#### Vessel Selection Panel (Network-Aware)

The Vessel Selection Panel in Menu_Main uses `MenuVesselSelectionPanelController`
(network-aware): vessel swaps go through `MenuServerPlayerVesselInitializer.RequestSwap()`
(Netcode despawn/spawn pipeline, replicates to all clients) and freestyle control is
restored after the swap delay. The legacy singleplayer variant
(`VesselSelectionPanelController` + `VesselSpawner`) was deleted with the SP path.

The panel opens from a button in the freestyle HUD. While open, the vessel flies on autopilot. On "Resume", if a different vessel is selected, it requests a network swap and waits `restoreFreestyleDelayMs` (600ms) before restoring player control.

#### SOAP Event Flow (Freestyle Toggle with Game UI)

```
Player taps freestyle button
  └─ MenuCrystalClickHandler.ToggleTransition()
      ├─ TransitionToFreestyle():
      │   ├─ Vessel.ToggleAIPilot(false), InputController.SetPause(false)
      │   ├─ freestyleEvents.OnEnterFreestyle.Raise()
      │   │   └─ MainMenuController → TransitionTo(Freestyle)
      │   ├─ FadeBetweenStates(menuAlpha=0, freestyleAlpha=1)
      │   │   ├─ menuCanvasGroups[] → fade to 0 (menu screens, nav bar)
      │   │   └─ freestyleCanvasGroups[] → fade to 1 ("Game UI" + contents)
      │   │       └─ MiniGameHUD, Vessel HUD children, Vessel Selection Button all become visible
      │   └─ Wait cameraTransitionDuration (parallel with fade)
      │
      └─ TransitionToMenu():
          ├─ InputController.SetPause(true), Vessel.ToggleAIPilot(true)
          ├─ freestyleEvents.OnExitFreestyle.Raise()
          │   └─ MainMenuController → TransitionTo(Ready)
          │   └─ MenuVesselSelectionPanelController → ui.Hide() (auto-close panel)
          ├─ FadeToSavedMenuAlphas()
          │   ├─ menuCanvasGroups[] → restore to saved alphas
          │   └─ freestyleCanvasGroups[] → fade to 0 ("Game UI" hidden)
          └─ Wait cameraTransitionDuration
```

#### Scoreboard in Menu Context

The `Scoreboard` component is present but hidden in basic lava-lamp mode. It subscribes to `OnShowGameEndScreen` to show and `OnResetForReplay` to hide. Since no game controller raises `OnShowGameEndScreen` during basic freestyle, the scoreboard stays inactive.

When scoring is enabled (Phase 3), a game controller can raise `OnShowGameEndScreen` to display results. The scoreboard renders the per-player card layout from `gameData.Results` (solo play renders as a single card - `IsMultiplayerMode` is retired).

#### Phase 2: Shape Drawing (Deferred)

Shape drawing requires additional scene infrastructure beyond UI panels. The scripts all still exist; their scene wiring lived in the removed `MinigameFreestyle.unity` (recover the reference setup from git history when porting):

| Dependency | Purpose | Script Location |
|---|---|---|
| `ShapeDrawingManager` | Orchestrates shape preview → draw → score flow | `_Scripts/Controller/Environment/MiniGameObjects/` |
| `SegmentSpawner` | Spawns trail segments with shape triggers | `_Scripts/Controller/Environment/MiniGameObjects/` |
| `ShapeDrawingCrystalManager` | Manages crystals during shape mode | `_Scripts/Controller/Environment/MiniGameObjects/` |
| `Spawnable*` objects | Shape definitions (Arrow, Circle, Diamond, etc.) | `_Prefabs/Spawnables/` |
| `EndShapeDetailHUD` | Shows shape results (name, time, accuracy, stars) | `_Scripts/UI/` |

The removed `SinglePlayerFreestyleController` (git history) managed the freestyle↔shape-drawing transitions (collision detection, environment teardown/restore, camera swaps). For lava-lamp, a `MenuFreestyleController` would adapt this flow for the menu context.

**Shape Drawing State Flow:**
```
Freestyle → ShapeCollision → FreezePlayer → NukeEnvironment → ShapePreview
  → ReadyButton → Countdown → DrawingMode → ShapeComplete → EndShapeDetailHUD
  → ExitButton → RestoreEnvironment → ConnectingFlow → ReadyButton → Freestyle
```

#### Phase 3: Scoring & PlayerScoreCards (Deferred)

`PlayerScoreCard`s are instantiated dynamically by `MiniGameHUD` when `OnMiniGameTurnStarted` fires:

- `SetupLocalPlayerCard()` — creates a card for the local player with name, score, domain color, avatar
- `SetupAICards()` — creates cards for AI opponents (when `isAIAvailable=true`)

For lava-lamp scoring, set `isAIAvailable=true` on MiniGameHUD and ensure `gameData.RoundStatsList` is populated. Cards are destroyed on `OnMiniGameTurnEnd`.

#### Lava-Lamp Key Files

| Role | File | Location |
|---|---|---|
| Menu MiniGameHUD (freestyle HUD + vessel change trigger) | `MenuMiniGameHUD.cs` | `_Scripts/UI/` |
| Freestyle toggle (autopilot↔control) | `MenuCrystalClickHandler.cs` | `_Scripts/Controller/Multiplayer/` |
| Menu state machine | `MainMenuController.cs` | `_Scripts/System/` |
| Menu vessel spawner (base) | `MenuServerPlayerVesselInitializer.cs` | `_Scripts/Controller/Multiplayer/` |
| Vessel selection (network-aware) | `MenuVesselSelectionPanelController.cs` | `_Scripts/Controller/Multiplayer/` |
| Vessel selection UI (show/hide) | `VesselSelectionPanelUI.cs` | `_Scripts/UI/` |
| Vessel card (per-vessel button) | `VesselCardView.cs` (class: `ShipCardView`) | `_Scripts/UI/` |
| Minigame HUD controller | `MiniGameHUD.cs` | `_Scripts/UI/` |
| Minigame HUD view | `MiniGameHUDView.cs` | `_Scripts/UI/View/` |
| Scoreboard (end-game results) | `Scoreboard.cs` | `_Scripts/UI/` |
| Player score card (per-player) | `PlayerScoreCard.cs` | `_Scripts/UI/` |
| Shape results panel | `EndShapeDetailHUD.cs` | `_Scripts/UI/` |
| Vessel HUD reparenting bridge | `VesselHUD.cs` (class: `ShipHUD`) | `_Scripts/Controller/Vessel/` |
| Freestyle SOAP events container | `MenuFreestyleEventsContainerSO.cs` | `_Scripts/ScriptableObjects/` |
| Shape drawing manager (Phase 2) | `ShapeDrawingManager.cs` | `_Scripts/Controller/Environment/MiniGameObjects/` |
| VesselHUD prefab variants | `*HUDVariant.prefab` | `_Prefabs/UI Elements/VesselHUD/` |
| PlayerScoreCard prefab | `PlayerScoreCard.prefab` | `_Prefabs/UI Elements/In Game/` |

#### Lava-Lamp Patterns to Follow

- **No new `MainMenuState` values** — `Freestyle` already exists and covers the lava-lamp gameplay phase
- **"Game UI" CanvasGroup controls all game panel visibility** — individual panels should not manage their own top-level visibility during freestyle toggles; the parent CanvasGroup handles fade in/out
- **Vessel HUD reparenting is automatic** — do not manually instantiate or position vessel HUDs; the `onShipHUDInitialized` → `MiniGameHUD.OnShipHUDInitialized()` pipeline handles it
- **Network-aware vessel selection only** — always use `MenuVesselSelectionPanelController` in Menu_Main, never the singleplayer `VesselSelectionPanelController`
- **Mass is conserved in the menu too** — the lava-lamp vessel is the freestyle gameplay vessel, so its trail follows the universal conserved-mass rules: no trail caps, prism TTLs, or idle cullers (a `maxTrailBlocks` ring-buffer cap was added for menu perf and reverted — see "Don't cheat emergence"). Manage menu-idle prism growth with fauna cleanup or by pausing the spawner
- **Scoreboard hidden until needed** — do not show the scoreboard in basic freestyle; let the SOAP event system activate it when a game controller raises `OnShowGameEndScreen`
- **Phase 2/3 panels start inactive** — `EndShapeDetailHUD` GO starts with `SetActive(false)`, activated only by `ShapeDrawingManager` (Phase 2). PlayerScoreCards are dynamically instantiated only when turns are active (Phase 3)
