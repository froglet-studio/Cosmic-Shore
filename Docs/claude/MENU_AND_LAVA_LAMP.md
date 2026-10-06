# Menu Navigation & Lava-Lamp Mode

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

### Menu Screen Navigation (Menu_Main Scene)

The main menu uses a horizontal sliding panel system managed by `ScreenSwitcher`. Screen panels are laid out side-by-side and the container slides left/right to reveal each screen.

#### IScreen Interface

All menu screens that need lifecycle notifications implement `IScreen` (`Assets/_Scripts/UI/Interfaces/IScreen.cs`):

```csharp
public interface IScreen
{
    void OnScreenEnter();  // Called when this screen becomes active
    void OnScreenExit();   // Called when navigating away from this screen
}
```

`ScreenSwitcher` discovers `IScreen` components on screen root GameObjects (via `GetComponentInChildren<IScreen>`) at startup and caches them in a dictionary. On navigation, it calls `OnScreenExit()` on the outgoing screen and `OnScreenEnter()` on the incoming screen automatically — no hard-coded screen references needed.

**Current `IScreen` implementors**: `HangarScreen`, `LeaderboardsMenu`

#### Screen Inventory

| Screen | Class | Extends `IScreen` | Init Pattern |
|---|---|---|---|
| Home | `HomeScreen` | No | `Start()` |
| Arcade (ARK) | `ArcadeScreen` | No | `Start()` |
| Store | `StoreScreen` (extends `View`) | No | `Start()` + `OnEnable()` events |
| Port (Leaderboards) | `LeaderboardsMenu` | Yes | `OnScreenEnter()` → `LoadView()` |
| Hangar | `HangarScreen` | Yes | `OnScreenEnter()` → `LoadView()` |
| Episodes | `EpisodeScreen` | No | Lazy `LoadView()` on panel toggle |

#### ScreenSwitcher

`ScreenSwitcher` (`Assets/_Scripts/UI/ScreenSwitcher.cs`) is the central navigation hub:

- Maps `MenuScreens` enum values to screen panel `RectTransform`s via inspector-configured `ScreenEntry` list
- Handles horizontal slide animations between screens
- Manages a modal window stack (`PushModal`/`PopModal`) for overlay modals
- Persists return-to-screen/modal state via `PlayerPrefs` across scene reloads
- Notifies `IScreen` implementors on navigation transitions
- Supports gamepad left/right trigger navigation
- On a device tier that sets `PlatformProfileSO.DeactivateMenuWhileFlying` (MobileLow phones only), DEACTIVATES the active non-HOME screen roots and the nav bar once the enter-freestyle blend settles, and reactivates exactly those at the start of the exit (`HandleFreestyleSettled` / `RestoreMenuAfterFlying`). HOME stays active: the party-invite popup and `HomeScreen`'s profile subscription must keep listening in flight. Anything under a screen root that subscribes in `Start` and unsubscribes in `OnDisable` breaks under this - subscribe in `OnEnable`. Every other tier hides them by CanvasGroup alone, as before. `Docs/PLATFORM_UNIFICATION.md` §3.6

**Adding a new screen**: Create a `MonoBehaviour` implementing `IScreen` if it needs enter/exit lifecycle. Add a `ScreenEntry` in the `ScreenSwitcher` inspector mapping. The switcher will discover and call the `IScreen` automatically.

#### Reusable UI Components

- **`ProfileDisplayWidget`** (`Assets/_Scripts/UI/Elements/ProfileDisplayWidget.cs`) — Displays player name + avatar. Uses `[Inject] PlayerDataService` and subscribes to `OnProfileChanged`. Drop onto any menu screen that needs profile display — replaces inline profile display logic.
- **`NavLink` / `NavGroup`** (`Assets/_Scripts/UI/Elements/`) — Tab navigation within a screen. `NavGroup` discovers child `NavLink` components and manages selection state with crossfade animations.
- **`ModalWindowManager`** (`Assets/_Scripts/UI/Modals/ModalWindowManager.cs`) — Base class for modal windows. Caches `ScreenSwitcher` reference at startup. Handles open/close animations, audio, and modal stack integration.

#### Menu Screen Patterns to Follow

- **Implement `IScreen`** for any screen that needs to refresh data when navigated to — do not add direct screen references to `ScreenSwitcher`
- **Use `ProfileDisplayWidget`** for profile display instead of duplicating `PlayerDataService` subscription logic
- **Cache component lookups** — use `Start()` or `Awake()` for `GetComponent` calls, not per-frame or per-event
- **Unsubscribe from events** — always pair event subscriptions in `OnEnable`/`OnDisable` or `Start`/`OnDestroy`
- **Use `[Inject]` for audio** — prefer `[Inject] AudioSystem` via Reflex DI over `[RequireComponent(typeof(MenuAudio))]` + `GetComponent` for new code

### Lava-Lamp Mode (Menu Freestyle Merge)

**Naming: "lava lamp" and "freestyle" are the same thing.** When viewed from the menu (autopilot vessels drifting behind the UI) it is called the *lava lamp*; when the player takes control and flies it is called *freestyle*. One system, two names. The old standalone arcade game named "Freestyle" (`GameModes.Freestyle = 7`, `MinigameFreestyle.unity`, `SinglePlayerFreestyleController`) was a vestige of the pre-lava-lamp era and has been removed — do not reintroduce it. `MultiplayerFreestyle (28)` is a separate multiplayer sandbox game and still exists.

Lava-lamp mode hosts freestyle gameplay directly in Menu_Main: the autopilot vessel becomes playable when the player enters freestyle mode. Game UI panels (MiniGameHUD, Scoreboard, Vessel Selection, Vessel HUDs, PlayerScoreCards) live under Menu_Main's "Game UI" container and fade in/out with the freestyle toggle.

#### Design Principles

- **Individual panels, not GameCanvas prefab**: Extract needed UI panels as scene-level objects under "Game UI" — do not instantiate the full `GameCanvas.prefab`. The GameCanvas prefab bundles a `Canvas` + `CanvasScaler` + `GraphicRaycaster` root that would conflict with Menu_Main's existing Canvas.
- **Reuse existing SOAP pipeline**: `MenuCrystalClickHandler` already toggles autopilot↔freestyle with CanvasGroup fading. "Game UI" `CanvasGroup` is already wired into its `freestyleCanvasGroups[]` array. `MainMenuController` already has `MainMenuState.Freestyle`. No new states or SOAP events needed.
- **There is NO vessel-selection panel, and there must never be another one**: changing your hull in freestyle is the **Vessel Changer TOY** (fly it, or open it in the menu Toy Box — two inputs to one declaration, `Docs/ToySystem/ARCHITECTURE.md` § "One declaration"). A scene-authored panel of vessel cards was retired 2026-09-23 having been **inactive in the scene with no caller for its `Open()`**, and its card grid had never gained the Scarab or the Butterfly — which is the whole argument: *a roster authored as scene objects is a roster nobody updates, and the only reason nobody noticed is that nobody could open it.*
- **Phased rollout**: Phase 1 (core HUD + vessel selection), Phase 3 (scoring). Phase 2 scored shape drawing was **deleted 2026-08-25** (C15); the painting toy is the successor.

#### Current "Game UI" Container

The existing "Game UI" in Menu_Main has two children:

```
Game UI [RectTransform, CanvasGroup]                    ← already in freestyleCanvasGroups[]
└── MiniGameHUD [RectTransform, CanvasGroup, MenuMiniGameHUD]
    └── Volume / Pause Button [Image, Button, MenuAudio]
        └── MenuMiniGameHUD.Awake() wires onClick → MenuCrystalClickHandler.ToggleTransition()
            (EXITS freestyle — it has never opened a panel, whatever this file used to say)
```

`MenuMiniGameHUD` (`_Scripts/UI/MenuMiniGameHUD.cs`) is a slim alternative to the full `MiniGameHUD` for menu freestyle mode. It provides the Volume/Pause icon button that EXITS freestyle (`MenuCrystalClickHandler.ToggleTransition`), vessel HUD reparenting via the `onShipHUDInitialized` SOAP event, and runtime PauseMenu prefab instantiation. The button is visible when Game UI fades in during freestyle, hidden when returning to menu. The full `MiniGameHUD` can replace this when Phase 3 scoring is needed.

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
│   ├── NotificationUI [GameToastController + GameToastView]
│   └── PlayerScoreContainer [Transform — for dynamically instantiated PlayerScoreCards]
│
└── ScoreboardController [Scoreboard.cs — hidden by default, no OnShowGameEndScreen in basic freestyle]
    ├── SinglePlayerView
    ├── MultiplayerView (4 player rows, winner banner)
    └── Buttons (PlayAgain, Home)
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
| Dolphin | `DolphinVesselHUDController` | `DolphinVesselHUDView` |
| Squirrel | — | `SquirrelHUDView` |

HUD prefab variants at `_Prefabs/UI Elements/VesselHUD/` (e.g., `MantaHUDVariant.prefab`, `DolphinHUDVariant.prefab`).

#### Vessel selection — RETIRED (2026-09-23)

**Changing your hull in freestyle is the Vessel Changer TOY and nothing else.** The scene-authored
`Vessel Selection Panel` (`MenuVesselSelectionPanelController` + `VesselSelectionPanelUI` +
`ShipCardView`, plus the never-referenced singleplayer twin `VesselSelectionPanelController`) is
deleted, along with its 21-GameObject subtree in Menu_Main.

**It was already unreachable, and that is the finding worth keeping.** Measured before deleting:
its GameObject carried `m_IsActive: 0`, `Awake` called `ui.Hide()`, `Open()` had **zero** callers
in C# and **zero** persistent UnityEvent listeners — the only two wired listeners were its own
internal Close and Resume buttons. So the deletion is provably a runtime no-op, and this file was
wrong about it in three separate places (it claimed the freestyle Volume/Pause button opened the
panel; that button has only ever called `MenuCrystalClickHandler.ToggleTransition`).

Its card grid held **seven hand-placed cards** — Rhino, Dolphin, Manta, Squirrel, Serpent,
Sparrow, Urchin — and had never gained the **Scarab** or the **Butterfly**. That is the general
rule this retirement exists to enforce: **a roster authored as scene objects is a roster nobody
updates**, and the only reason its staleness never surfaced is that nobody could open it. A
surface that offers a vessel, a world, a lifeform or a painting **derives its list from the live
system that owns those things** — for hulls, `ToyVesselRoster` (CONTRACT.md §1.12) — and it is
reached through the toy that owns the action, so the world station and the menu window cannot
disagree.

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
          ├─ FadeToSavedMenuAlphas()
          │   ├─ menuCanvasGroups[] → restore to saved alphas
          │   └─ freestyleCanvasGroups[] → fade to 0 ("Game UI" hidden)
          └─ Wait cameraTransitionDuration
```

#### Scoreboard in Menu Context

The `Scoreboard` component is present but hidden in basic lava-lamp mode. It subscribes to `OnShowGameEndScreen` to show and `OnResetForReplay` to hide. Since no game controller raises `OnShowGameEndScreen` during basic freestyle, the scoreboard stays inactive.

When scoring is enabled (Phase 3), a game controller can raise `OnShowGameEndScreen` to display results. The scoreboard supports both `SinglePlayerView` and `MultiplayerView` automatically based on `gameData.IsMultiplayerMode`.

#### Phase 2: Shape Drawing — deleted (C15, 2026-08-25)

The scored shape-drawing minigame (`ShapeDrawingManager` + `ShapeDrawingCrystalManager` + `EndShapeDetailHUD` + `ShapeScoreDisplay` + `ShapeScoreData`) was **deleted**, not deferred. It was unreachable after `MinigameFreestyle.unity` was removed; migrating its per-frame transform Lerp would have shipped an untested clock path (`Docs/PRISM_ANIMATION.md` C15). Recover from git if a scored minigame is wanted.

**The painting toy is the successor** — scoreless connect-the-dots in the toybox (`PaintingToy` / `ShapeDefinition` via `PaintingDefinitionSO.sourceShape`).

**Still in the tree:** `SegmentSpawner` (SkimRace live; also lays trail segments that can carry `ShapeCollisionTrigger`), `SpawnableShapeBase` + spawnable shapes, `ShapeSign` / `ShapeCollisionTrigger` / `SpawnableShapeSign` / `ModeSelectTrigger`, `ShapeDefinition`. SOAP events `EventOnShapeGameModeStarted` / `EventOnShapePrismReturnToPool` stay on live prism prefabs (inert — **never Raise them**; they dump every listener to `Prism.ReturnToPool`).

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
| Vessel selection (the ONLY one) | `VesselChangerToy.cs` + `ToyVesselRoster.cs` | `_Scripts/Controller/Toys/` |
| Minigame HUD controller | `MiniGameHUD.cs` | `_Scripts/UI/` |
| Minigame HUD view | `MiniGameHUDView.cs` | `_Scripts/UI/View/` |
| Scoreboard (end-game results) | `Scoreboard.cs` | `_Scripts/UI/` |
| Player score card (per-player) | `PlayerScoreCard.cs` | `_Scripts/UI/` |
| Vessel HUD reparenting bridge | `VesselHUD.cs` (class: `ShipHUD`) | `_Scripts/Controller/Vessel/` |
| Freestyle SOAP events container | `MenuFreestyleEventsContainerSO.cs` | `_Scripts/ScriptableObjects/` |
| VesselHUD prefab variants | `*HUDVariant.prefab` | `_Prefabs/UI Elements/VesselHUD/` |
| PlayerScoreCard prefab | `PlayerScoreCard.prefab` | `_Prefabs/UI Elements/In Game/` |

#### Lava-Lamp Patterns to Follow

- **No new `MainMenuState` values** — `Freestyle` already exists and covers the lava-lamp gameplay phase
- **"Game UI" CanvasGroup controls all game panel visibility** — individual panels should not manage their own top-level visibility during freestyle toggles; the parent CanvasGroup handles fade in/out
- **Vessel HUD reparenting is automatic** — do not manually instantiate or position vessel HUDs; the `onShipHUDInitialized` → `MiniGameHUD.OnShipHUDInitialized()` pipeline handles it
- **Never author a panel of vessel/world/lifeform cards in a scene** — the roster goes stale the day a vessel ships and nothing says so. Hull selection is the Vessel Changer toy, reachable both by flying it and from the menu Toy Box; both read `ToyVesselRoster`
- **Mass is conserved in the menu too** — the lava-lamp vessel is the freestyle gameplay vessel, so its trail follows the universal conserved-mass rules: no trail caps, prism TTLs, or idle cullers (a `maxTrailBlocks` ring-buffer cap was added for menu perf and reverted — see "Don't cheat emergence"). Manage menu-idle prism growth with fauna cleanup or by pausing the spawner. That is exactly what MobileLow phones do: the lava lamp lays no trail and freestyle trail waits above 10,000 cell prisms, through a creation-side hold (`VesselPrismController.SetTierHold`, driven by `MenuCrystalClickHandler`; a mode preview and a Wanderway run are exempt) — nothing laid is removed (`Docs/PLATFORM_UNIFICATION.md` §3.6)
- **Scoreboard hidden until needed** — do not show the scoreboard in basic freestyle; let the SOAP event system activate it when a game controller raises `OnShowGameEndScreen`
- **Phase 3 panels start inactive** — PlayerScoreCards are dynamically instantiated only when turns are active. The scored Phase 2 HUD (`EndShapeDetailHUD`) was deleted with C15.
