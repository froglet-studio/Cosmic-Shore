# Menu Navigation & Lava-Lamp Mode

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

### Menu Screen Navigation (Menu_Main Scene)

The main menu uses a horizontal sliding panel system managed by `ScreenSwitcher`. Screen panels are laid out side-by-side and the container slides left/right to reveal each screen.

How the strip is positioned (fixed 2026-10-12, after it broke on the first iPhone build and at 4:3):
- It sits in a runtime `Screens Viewport`, a full-canvas `RectMask2D` child of the root canvas
  placed just before `Safe Area`.
- Each screen gets a full-screen SLOT, and its content is laid out in the safe rect within that
  slot.
- Full-width background images bleed to the slot edges (`SafeAreaBleed`). So do the NavBar band
  and modal backgrounds. A notched phone therefore looks like the desktop.
- The strip slides one slot per screen by `anchoredPosition`. Never set `transform.position`,
  which is absolute screen pixels.
- It re-lays out whenever the slot width or the safe insets change.

Details: `Docs/UI_ARCHITECTURE_AUDIT.md` §1.3.

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
| Ark (ARK) — the Elemental Converter (2026-10-13; the arcade moved to `ModalWindows.ARCADE`) | `ElementalConverterView` (+ the disabled legacy `StoreScreen`) | Yes | `OnScreenEnter()` → idle crystals; CONVERT plays the omni combine (`Docs/Studios/OMNI_COMBINE_LAB.md` § In the game) |
| Store | `StoreScreen` (extends `View`) | No | `Start()` + `OnEnable()` events |
| Port (PORT) — faction messages and the weekly leaderboard (opened 2026-10-27, `Docs/MENU_REDESIGN_B.md` §90; the PlayFab-era `LeaderboardsMenu` panel is switched off) | `PortScreen` (`FactionMessagesPane`, `PortLeaderboardPane`) | Yes | `OnScreenEnter()` → the shown tab; the messages tab joins its Vivox channel while shown |
| Hangar | `HangarScreen` | Yes | `OnScreenEnter()` → `LoadView()` |
| Episodes | `EpisodeScreen` | No | Lazy `LoadView()` on panel toggle |

#### ScreenSwitcher

`ScreenSwitcher` (`Assets/_Scripts/UI/ScreenSwitcher.cs`) is the central navigation hub:

- Maps `MenuScreens` enum values to screen panel `RectTransform`s via inspector-configured `ScreenEntry` list
- Handles horizontal slide animations between screens
- Manages a modal window stack (`PushModal`/`PopModal`) for overlay modals
- Persists return-to-screen/modal state via `PlayerPrefs` across scene reloads
- Coming back from a match, re-opens the launch panel of the card it was launched from (`MenuReturnCard`, `ReturnToLaunchedCardCoroutine`), not HOME (`Docs/ArcadeLaunch/ARCHITECTURE.md` §3.2.1)
- Notifies `IScreen` implementors on navigation transitions
- Supports gamepad left/right trigger navigation: between screens with no modal open, and (rev 7) between the home hub's windows (Toy Box, Arcade, Arena, Voyage - the Mission window renamed in Rev 13 - `hubWindowOrder`) while one of them is the top modal, stopping at the ends and skipping a window its hub entry refuses (`SlideHubWindow`, `CanOpenHubWindow`). `HubWindowNav` draws the strip and edge arrows (`Docs/MENU_REDESIGN_B.md` §47.2). Since Rev 13 the top bar has four tabs (Hangar, Ark, Home, Port): the Profile screen has no tab (`navHiddenScreens`), LT / RT page past it, and it opens from the Home pilot or Y and closes with Back / B (`Docs/MENU_REDESIGN_B.md` §69). `ModalStackChanged` fires after every stack change
- On a device tier that sets `PlatformProfileSO.DeactivateMenuWhileFlying` (MobileLow phones only), DEACTIVATES the active non-HOME screen roots and the nav bar once the enter-freestyle blend settles, and reactivates exactly those at the start of the exit (`HandleFreestyleSettled` / `RestoreMenuAfterFlying`). HOME stays active: the party-invite popup and `HomeScreen`'s profile subscription must keep listening in flight. Anything under a screen root that subscribes in `Start` and unsubscribes in `OnDisable` breaks under this - subscribe in `OnEnable`. Every other tier hides them by CanvasGroup alone, as before. `Docs/PLATFORM_UNIFICATION.md` §3.6

**Adding a new screen**: Create a `MonoBehaviour` implementing `IScreen` if it needs enter/exit lifecycle. Add a `ScreenEntry` in the `ScreenSwitcher` inspector mapping. The switcher will discover and call the `IScreen` automatically.

#### Reusable UI Components

- **`ProfileDisplayWidget`** (`Assets/_Scripts/UI/Elements/ProfileDisplayWidget.cs`) — Displays player name + avatar. Uses `[Inject] PlayerDataService` and subscribes to `OnProfileChanged`. Drop onto any menu screen that needs profile display — replaces inline profile display logic. (retired 2026-10-25: on no scene or asset, `Docs/Reports/DEAD_CODE_M8.md`)
- **`NavLink` / `NavGroup`** (`Assets/_Scripts/UI/Elements/`) — Tab navigation within a screen. `NavGroup` discovers child `NavLink` components and manages selection state with crossfade animations.
- **`ModalWindowManager`** (`Assets/_Scripts/UI/Modals/ModalWindowManager.cs`) — Base class for modal windows. Caches `ScreenSwitcher` reference at startup. Handles open/close animations, audio, and modal stack integration.

#### Style-B component kit (the menu redesign)

The menu is being rebuilt in the style-B "Hangar bay" look one item at a time. Status and rules
live in `Docs/MENU_REDESIGN_B.md`. Build new menu UI from the kit:

- **Buttons:** `KitButton` / `KitButtonSmall` (§10.1 variants × transparent/opaque).
- **Selectable tiles:** `KitTile` / `KitHexNavTile`.
- **Labels:** `KitText`, a §4 role plus a §11 colour token.
- **Static chrome:** `KitThemedGraphic`.
- **Hover on a non-`KitButton` surface** (an EventTrigger tab, a keycap on a plain Button):
  `KitHoverTint`.
- **Show / hide:** `KitVisibility.SetShown(CanvasGroup, bool)` - never `SetActive`; a `LayoutElement`
  on the same object leaves the layout while hidden.
- **Focus in a scroll view:** `KitScrollIntoView` on each control. **Lists spawned by other code:**
  `KitListCounter` (count, header, empty line). **Rows rebuilt under focus:** `KitFocusKeeper`.
- **A window's Back button:** `ModalWindowCloseButton` (closes like gamepad B).

All of these are in `Assets/_Scripts/UI/Kit/` and `Assets/_Prefabs/UI Elements/Kit/`. Colours
come from `UIThemeSO` tokens, never literals. The kit owns every interaction state, including the
CTA gamepad focus ring.

**The top nav is style B (item 1, 2026-10-14).** `Safe Area/NavBar` is an 84-tall band (`Band` +
1 px `Rule`, both full width, so `SafeAreaBleed` takes them to the screen edges as it took the old
gradient) with one centred row under `NavBarButtons`: the `LeftArrrow` Button wearing a `KitKeycap`
"LT", five `KitTopNavTab` instances (`HangarLink`..`ProfileLink`, text labels, each with its
`EventTrigger` -> `ScreenSwitcher.OnClick<X>Nav`), and `RightArrow` / "RT". `NavActiveImages` /
`NavInactiveImages` point into the instances, so `UpdateNavBar` is unchanged; `NavBarLine` and
`NavBarLineSprites` are deliberately empty. A locked tab is drawn by an authored
`MenuAvailabilityView` (padlock + dimmed word) that `disabledScreens` drives at runtime (no tab is locked since
PORT opened, §90). The scene's
nav is generated: change it through `Tools/Build/author_menu_topnav.py` (`--check`, `--self-test`),
not by hand. Details: `Docs/MENU_REDESIGN_B.md` §10.

**The Hangar is style B (item 2, 2026-10-14): one view, three zones.** `Screens/Hangar Screen` holds
one `HangarView` prefab instance (generated by `Tools/Build/author_menu_hangar.py`): a vessel list
of `HangarVesselTile`s (a `KitTile` variant, owned first), the selected vessel (cover-fitted
`PreviewImage`, name, description, unlock button with its price) and every ability, with the
spend-crystals popup over it. There is no grid-to-detail hop: a tile is selected by click OR by
gamepad / keyboard focus arriving on it (`ISelectHandler`), so focus never lands on a hidden panel.
`HangarScreen` still owns the list (`RebuildList` on unlock, owned first);
`HangarVesselDetailView` owns everything else and toggles by CanvasGroup only. Details:
`Docs/MENU_REDESIGN_B.md` §11.

**The Arcade is style B (item 3, 2026-10-14), with the shared party panel.** `ArcadeScreenModal`
stretches the safe area and holds one `ArcadeView` instance under `Arcade_Panel/Explore` (generated
by `Tools/Build/author_menu_arcade.py`): a dimmed nav band, the header with Back + B keycap
(`ModalWindowCloseButton`, the same close as gamepad B), the PARTY panel on the left and the explore
scroll - weekly challenge and Maelstrom feature cards, ALL GAMES, 28 `ArcadeGameCard`s
(`KitGameCard`, a `GameCard` on a `KitTile`). `ArcadeExploreView` stays on the scene's `Explore`
(`ArcadeDPadNav` is retired from every window); cards are the scroll's Selectables and `KitScrollIntoView` keeps the focused one
visible. Sibling order is focus order: the cards come before the party panel and Back comes last.
The party panel is `Main Menu Screens/Party/PartyPanel.prefab`: `ArcadeLobbyList` +
`FriendsListPanel` (embedded) on one root, four `KitPartySlot` seats, Leave, then INVITES and ONLINE
in one scroll - the old friends overlay is gone from the Arcade. Arena, Toy Box and Mission still
carry the legacy pair until item 8. Details: `Docs/MENU_REDESIGN_B.md` §12.

#### Menu Screen Patterns to Follow

- **Implement `IScreen`** for any screen that needs to refresh data when navigated to — do not add direct screen references to `ScreenSwitcher`
- Do not duplicate `PlayerDataService` subscription logic for profile display (`ProfileDisplayWidget` was retired 2026-10-25)
- **Cache component lookups** — use `Start()` or `Awake()` for `GetComponent` calls, not per-frame or per-event
- **Unsubscribe from events** — always pair event subscriptions in `OnEnable`/`OnDisable` or `Start`/`OnDestroy`
- **Use `[Inject]` for audio** — prefer `[Inject] AudioSystem` via Reflex DI over `[RequireComponent(typeof(MenuAudio))]` + `GetComponent` for new code

### Lava-Lamp Mode (Menu Freestyle Merge)

**Naming: "lava lamp" and "freestyle" are the same thing.** When viewed from the menu (autopilot vessels drifting behind the UI) it is called the *lava lamp*; when the player takes control and flies it is called *freestyle*. One system, two names. The old standalone arcade game named "Freestyle" (`GameModes.Freestyle = 7`, `MinigameFreestyle.unity`, `SinglePlayerFreestyleController`) was a vestige of the pre-lava-lamp era and has been removed — do not reintroduce it. Its multiplayer sibling `MultiplayerFreestyle (28)` was the prototype that grew into the lava lamp, freestyle, toybox and lobby; it was removed 2026-10-08 (scene, controller, card, preview) and 28 stays reserved. Online Duel for the Cell (29) went the same day; Brood Rush replaced it.

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

**No entry behind the boot veil (do not regress, 2026-10-22).** The primary fix for a stray click launching freestyle during the Garland intro is the base's: the boot world builds behind `EnvironmentLoadVeil`'s opaque cover (`dc023d00d`) and Home's full-screen surface flies only on a double tap (`DoubleTapRelay`, `01403d976`). Routes that bypass both - the pad's Select, Submit, the toybox modals - are covered by `MenuCrystalClickHandler.ToggleTransition`, which refuses to ENTER freestyle while `EnvironmentLoadVeil.IsCovering`. Exits are never gated; new entry routes go through `ToggleTransition` so they inherit it.

**Freestyle input ownership + HUD-after-swap (do not regress).** The menu ("appshell") and the vessel both poll the one gamepad, so ownership must be exclusive: in freestyle `ScreenSwitcher.HandleEnterFreestyle` sets `EventSystem.sendNavigationEvents = false` (restored on exit) so the pad flies the ship and no longer double-drives the UI selection ring / Submit on the still-touch-interactable vessel HUD (`ScreenSwitcher.Update` screen-nav was already gated on `_isInFreestyle`; the vessel is paused in menu state). `MenuMiniGameHUD.Update` polls **gamepad Select / View** (`OverviewGesture`; it was Start until 2026-10-21) while in freestyle → `MenuCrystalClickHandler.ToggleTransition()`, the pad counterpart to the on-screen Volume/Pause exit. On a runtime **vessel swap**, `VesselController.Initialize` creates the new HUD hidden and the swap never re-enters freestyle, so `ClientPlayerVesselInitializer.ReInitializePair` re-raises `GameDataSO.OnPlayerPairInitialized` and `MenuMiniGameHUD` re-shows the local HUD (gated on freestyle + local player) — the `onShipHUDInitialized`/`VesselHUD` reparent path is dead for menu vessels (no `VesselHUD` on the vessel prefabs). See `Docs/ToySystem/ARCHITECTURE.md`.

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
  └─ VesselHUD.Start() [on vessel prefab]
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
| Dolphin | `DolphinHUDController` | `DolphinVesselHUDView` |
| Squirrel | — | `SquirrelHUDView` |

HUD prefab variants at `_Prefabs/UI Elements/VesselHUD/` (e.g., `MantaHUDVariant.prefab`, `DolphinHUDVariant.prefab`).

#### The lava lamp's default hull: Squirrel (2026-10-29)

The hull the lava lamp spawns (the autopilot behind the menu, and the one you fly in freestyle) is
`MainMenuController.menuVesselClass` on Menu_Main: **Squirrel** (6). It was the Meerkat (28) for one day, 2026-10-28
(622e271ca), and went back to Squirrel on 2026-10-29 (Yash: "change the lavalamp default vessel from meerkat to
squirrel"). `ConfigureMenuGameData` writes it to `gameData.selectedVesselClass` and onto the host's
`NetDefaultVesselType`; `MenuServerPlayerVesselInitializer` spawns that. The Vessel Changer toy swaps it at runtime (the
Meerkat is still on its roster). The Meerkat-only menu code (the executor's MenuLive perf guards, the autopilot's
omnicrystal and camera rules) simply does not run for a Squirrel. The Lavalamp wormhole lab
(`Game_TestDesign/LavalampWormholeLab.unity`) is its own scene and still flies the Meerkat.

#### God mode: the lava lamp's inspection camera (2026-10-29)

Fly a free camera through the lava lamp while the AI keeps flying the vessel (Yash: "I dont need a lab to check out
this vessel's ability, I just need a god mode, with which I can fly smoothly in the environment without the vessel, and
have an eye on everything while the Vessel is flown by AI"). This replaces the wormhole lab as the way to watch a hull's
ability; the lab scene was left in place, but nothing needs it any more.

**Shared flight (2026-11-01):** the flight model and its defaults now live in `Controller/Camera/FreeFlyCamera.cs` (a serializable class, field `flight` on `MenuGodModeCamera`). Switchback's AI Diagnose Free fly uses the same class, so both fly identically. Menu_Main serialized no flight values, so nothing was lost.

| Input | Keyboard / mouse | Gamepad |
|---|---|---|
| Toggle god mode | **F2** | **L3** (left-stick click) |
| Move | W A S D | left stick |
| Up / down | E or Space / Q or Ctrl | RT / LT |
| Look | hold the right mouse button and move | right stick |
| Faster / slower | hold Shift / hold Alt | hold RB |
| Cruise speed | mouse wheel (×1.2 per notch, 2 to 2000 u/s) | - |

- **Where it works:** only the settled lava lamp (`MainMenuCameraController` in its `Menu` state: AI flying, the
  player's flight input paused). Not in freestyle or during a menu/freestyle blend; the shortcut does nothing there.
- **How it hooks the camera:** `MenuGodModeCamera` (`_Scripts/Controller/Camera/`, on Menu_Main's **Game** object next
  to the camera rig) borrows the scene's MainCamera with `MainMenuCameraController.BeginInspection()`, starting at the
  camera's current pose (any roll levels out over a moment). While it flies, the rig stops posing the camera. Exit
  calls `EndInspection()`, which re-seeds the rig at the god camera's pose with the config-switch glide raised, so the
  lava-lamp framing glides back instead of cutting. CM PlayerCam and `CameraManager` are untouched; the lava lamp never
  uses them.
- **Click to control still works:** taking the vessel (double tap, the pad's Select) ends god mode first, and the
  enter-freestyle blend starts from the god camera's pose. Leaving the lava lamp any other way (a game launch, the scene
  closing) ends it too.
- **Input:** the vessel gets nothing from god mode; its flight input is already paused in the lava lamp and the AI
  keeps the stick. UI keyboard/pad navigation is switched off while flying (WASD and Space would otherwise move and
  press the menu's selection) and restored on exit. Mouse clicks still reach the UI.
- **Dev switches** (Inspector on the component): `godModeEnabled`, `toggleKey`, `gamepadToggle`, speeds, acceleration
  and damping (the glide), look sensitivity and smoothing, and `showHint` (a one-line controls reminder at the top left
  while flying). Movement runs on unscaled time, so it still flies while the menu is paused.

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

**Still in the tree:** `SegmentSpawner` (SkimRace live; also lays trail segments that can carry `ShapeCollisionTrigger`), `SpawnableShapeBase` + spawnable shapes, `ShapeSign` / `ShapeCollisionTrigger` / `SpawnableShapeSign` / `ModeSelectTrigger`, `ShapeDefinition`. SOAP event ASSETS `EventOnShapeGameModeStarted` / `EventOnShapePrismReturnToPool` are kept but **nothing listens to them any more**: the `EventListenerNoParam` pairs that wired them to `Prism.ReturnToPool` on 8 prism prefabs were stripped (2026-09) because every lattice-flora growth step paid for serializing their UnityEvent lists in `Instantiate.Copy` (`Docs/archive/PERFORMANCE_LOG_2026.md` §0.11.6). Raising them is now a no-op; re-adding a listener to a PRISM prefab is re-adding that cost to every prism laid.

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
| God mode (free inspection camera, F2 / L3) | `MenuGodModeCamera.cs` | `_Scripts/Controller/Camera/` |
| Menu state machine | `MainMenuController.cs` | `_Scripts/System/` |
| Menu vessel spawner (base) | `MenuServerPlayerVesselInitializer.cs` | `_Scripts/Controller/Multiplayer/` |
| Vessel selection (the ONLY one) | `VesselChangerToy.cs` + `ToyVesselRoster.cs` | `_Scripts/Controller/Toys/` |
| Minigame HUD controller | `MiniGameHUD.cs` | `_Scripts/UI/` |
| Minigame HUD view | `MiniGameHUDView.cs` | `_Scripts/UI/View/` |
| Scoreboard (end-game results) | `Scoreboard.cs` | `_Scripts/UI/` |
| Player score card (per-player) | `PlayerScoreCard.cs` | `_Scripts/UI/` |
| Vessel HUD reparenting bridge | `VesselHUD.cs` (class `VesselHUD`; `ShipHUD` until 2026-10-25) | `_Scripts/Controller/Vessel/` |
| Freestyle SOAP events container | `MenuFreestyleEventsContainerSO.cs` | `_Scripts/ScriptableObjects/` |
| VesselHUD prefab variants | `*HUDVariant.prefab` | `_Prefabs/UI Elements/VesselHUD/` |
| PlayerScoreCard prefab | `PlayerScoreCard.prefab` | `_Prefabs/UI Elements/In Game/` |

#### Lava-Lamp Patterns to Follow

- **No new `MainMenuState` values** — `Freestyle` already exists and covers the lava-lamp gameplay phase
- **"Game UI" CanvasGroup controls all game panel visibility** — individual panels should not manage their own top-level visibility during freestyle toggles; the parent CanvasGroup handles fade in/out
- **Vessel HUD reparenting is automatic** — do not manually instantiate or position vessel HUDs; the `onShipHUDInitialized` → `MiniGameHUD.OnShipHUDInitialized()` pipeline handles it
- **Never author a panel of vessel/world/lifeform cards in a scene** — the roster goes stale the day a vessel ships and nothing says so. Hull selection is the Vessel Changer toy, reachable both by flying it and from the menu Toy Box; both read `ToyVesselRoster`
- **Mass is conserved in the menu too** — the lava-lamp vessel is the freestyle gameplay vessel, so its trail follows the universal conserved-mass rules: no trail caps, prism TTLs, or idle cullers (a `maxTrailBlocks` ring-buffer cap was added for menu perf and reverted — see "Don't cheat emergence"). Manage menu-idle prism growth with fauna cleanup or by pausing the spawner. That is exactly what MobileLow phones do: the lava lamp lays no trail and freestyle trail waits above 10,000 cell prisms, through a creation-side hold (`VesselPrismController.SetTierHold`, driven by `MenuCrystalClickHandler`; a mode preview and a Wanderway run are exempt) — nothing laid is removed (`Docs/PLATFORM_UNIFICATION.md` §3.6). **Dead mass is different (2026-10-29, Yash):** a prism that is already DESTROYED goes back to its pool once its death effect has played (`PrismRecycler`; vessel wakes and natural-death skeletons only, held while an Urchin exists - `Docs/PERFORMANCE_OPTIMIZATION.md` §2 row 10-29). That is what keeps an idle lava lamp's GameObject count and memory flat; it removes nothing alive, so it is not a cap or a TTL
- **Scoreboard hidden until needed** — do not show the scoreboard in basic freestyle; let the SOAP event system activate it when a game controller raises `OnShowGameEndScreen`
- **Phase 3 panels start inactive** — PlayerScoreCards are dynamically instantiated only when turns are active. The scored Phase 2 HUD (`EndShapeDetailHUD`) was deleted with C15.
