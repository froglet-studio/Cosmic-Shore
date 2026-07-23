# Menu Screen Navigation (Menu_Main) - Reference

> Extracted verbatim from `CLAUDE.md` (2026-07-23) so the root file stays a lean
> rules-and-routing dictionary. This is the canonical home of this content now -
> update it here, and keep the corresponding CLAUDE.md digest in sync.

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
