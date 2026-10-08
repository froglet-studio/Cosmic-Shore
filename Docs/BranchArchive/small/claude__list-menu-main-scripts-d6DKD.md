# Branch archive: `claude/list-menu-main-scripts-d6DKD`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-01 by Claude
- **Unmerged commits:** 1
- **Forked from:** `965f6e9da` (2026-03-01, Merge pull request #300 from froglet-studio/claude/camera-transition-priority-)
- **Tip:** `6c1a3706a`
- **Files touched (2):**
  - `CLAUDE.md`
  - `README.md`

### `6c1a3706a` — docs: add complete scene script inventories for Bootstrap and Menu_Main

_Claude, 2026-03-01 12:10:43 +0000_

```text
Document all MonoBehaviour scripts in both scenes with categorized tables:
- Bootstrap: 68 MonoBehaviours, 35 unique types, 33 GameObjects, 8 prefab instances
- Menu_Main: 1,282 MonoBehaviours, 89 unique types, 386 GameObjects

CLAUDE.md gets full inventories with per-category tables and instance counts.
README.md gets summary reference tables linking to CLAUDE.md for details.
```

```text
 CLAUDE.md | 223 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 README.md |  24 ++++++++++
 2 files changed, 247 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 269 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 23a02a85e..b2acc2ed0 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -941,6 +941,229 @@ public interface IScreen
 - **Unsubscribe from events** — always pair event subscriptions in `OnEnable`/`OnDisable` or `Start`/`OnDestroy`
 - **Use `[Inject]` for audio** — prefer `[Inject] AudioSystem` via Reflex DI over `[RequireComponent(typeof(MenuAudio))]` + `GetComponent` for new code
 
+### Scene Script Inventories
+
+Complete MonoBehaviour script references for each scene. Use these inventories to understand what systems are active in each scene and where to find or add components.
+
+#### Bootstrap Scene (`Assets/_Scenes/Bootstrap.unity`)
+
+**68 MonoBehaviours** | **35 unique script types** (23 first-party + 12 Unity/third-party) | **33 GameObjects** | **8 prefab instances**
+
+The Bootstrap scene hosts all persistent manager/service GameObjects (marked `DontDestroyOnLoad`) that survive across scene loads, plus the splash screen UI and camera rig.
+
+##### Root GameObjects & Prefab Instances
+
+| Root GameObject | Source | Role |
+|---|---|---|
+| `AppManager` | Prefab (`_Prefabs/CORE/`) | DI root, orchestrator, `SceneTransitionManager` |
+| `EventSystem` | Scene | `EventSystem` + `InputSystemUIInputModule` |
+| `Camera` | Scene | Main camera with `CinemachineBrain` + URP data |
+| `NetworkManager` | Prefab (`_Prefabs/CORE/`) | Netcode `NetworkManager` |
+| `AudioSystem` | Prefab (`_Prefabs/CORE/`) | `AudioSystem` (Wwise integration) |
+| `PrismManagers` | Scene | `PrismFactory`, `PrismScaleManager`, `MaterialStateManager` + 9 child pool objects |
+| `ThemeManager` | Scene | `ThemeManager` |
+| `CameraManager` | Scene | `CameraManager` + Cinemachine virtual cameras (CM PlayerCam, CM EndCam, CM DeathCam, CM Main Menu) + camera follow/look targets |
+| `GameSettings` | Scene | `GameSetting` |
+| `MultiplayerSetup` | Scene | `MultiplayerSetup` |
+| `UGSStatsManager` | Scene | `UGSStatsManager` |
+| `GamepadDebugger` | Scene | `GamepadDebugger` |
+| `PlayerDataService` | Scene | `PlayerDataService` |
+| `SceneLoader` | Scene | `SceneLoader` + `NetworkObject` |
+| `CaptainManager` | Scene | `CaptainManager` |
+| `IAPManager` | Scene | `IAPManager` |
+| `Canvas - Splash Screen` | Scene | Splash UI: `CanvasScaler`, loading panel (`Image`), status text (`TextMeshProUGUI`, `LayoutElement`) |
+| `PostProcessing` | Prefab (`_Prefabs/Environment/`) | `PostProcessingManager` |
+| `CallToActionManager` | Prefab (`_Prefabs/CORE/`) | CTA system |
+| `DailyChallengeSystem` | Prefab (`_Prefabs/CORE/`) | Daily challenge system |
+| `PartyServices` | Prefab (`_Prefabs/CORE/`) | `HostConnectionService`, `PartyInviteController`, `FriendsInitializer` |
+| `StatsManager` | Prefab (`_Prefabs/CORE/`) | `StatsManager` |
+
+##### First-Party Scripts (by category)
+
+**System & Bootstrap**
+
+| Script | Path | GameObject | Instances |
+|---|---|---|---|
+| `DontDestroyOnLoad` | `_Scripts/Utility/DontDestroyOnLoad.cs` | 15 manager GameObjects | 15 |
+| `SceneLoader` | `_Scripts/System/SceneLoader.cs` | SceneLoader | 1 |
+| `SceneTransitionManager` | `_Scripts/System/Bootstrap/SceneTransitionManager.cs` | AppManager (prefab) | 1 |
+| `AudioSystem` | `_Scripts/System/Audio/AudioSystem.cs` | AudioSystem (prefab) | 1 |
+| `IAPManager` | `_Scripts/System/IAPManager.cs` | IAPManager | 1 |
+| `CaptainManager` | `_Scripts/System/Playfab/Economy/CaptainManager.cs` | CaptainManager | 1 |
+| `GameSetting` | `_Scripts/Controller/Settings/GameSetting.cs` | GameSettings | 1 |
+
+**Managers**
+
+| Script | Path | GameObject | Instances |
+|---|---|---|---|
+| `CameraManager` | `_Scripts/Controller/Managers/CameraManager.cs` | CameraManager | 1 |
+| `ThemeManager` | `_Scripts/Controller/Managers/ThemeManager.cs` | ThemeManager | 1 |
+| `PrismScaleManager` | `_Scripts/Controller/Managers/PrismScaleManager.cs` | PrismManagers | 1 |
+| `MaterialStateManager` | `_Scripts/Controller/Managers/MaterialStateManager.cs` | PrismManagers | 1 |
+| `PostProcessingManager` | `_Scripts/Controller/Managers/PostProcessingManager.cs` | PostProcessing (prefab) | 1 |
+| `StatsManager` | `_Scripts/Controller/Managers/StatsManager.cs` | StatsManager (prefab) | 1 |
+
+**Camera & Animation**
+
+| Script | Path | GameObject | Instances |
+|---|---|---|---|
+| `CustomCameraController` | `_Scripts/Controller/Camera/CustomCameraController.cs` | CM PlayerCam, CM EndCam, CM DeathCam | 5 |
+| `RotateAroundOrigin` | `_Scripts/Controller/Animation/RotateAroundOrigin.cs` | EndCam Follow/Look Target, Main Menu Follow Target | 3 |
+
+**Gameplay & Pools**
+
+| Script | Path | GameObject | Instances |
+|---|---|---|---|
+| `InteractivePrismPoolManager` | `_Scripts/Utility/Effects/InteractivePrismPoolManager.cs` | Per-vessel prism pools (Dolphin, Manta, Rhino, Serpent, Sparrow, Squirrel) + PrismInteractivePool | 7 |
+| `PrismFactory` | `_Scripts/Controller/Prisms/PrismFactory.cs` | PrismManagers | 1 |
+| `PrismExplosionPoolManager` | `_Scripts/Utility/Effects/PrismExplosionPoolManager.cs` | PrismExplosionPool | 1 |
+| `PrismImplosionPoolManager` | `_Scripts/Utility/Effects/PrismImplosionPoolManager.cs` | PrismImplosionPool | 1 |
+| `MultiplayerSetup` | `_Scripts/Controller/Multiplayer/MultiplayerSetup.cs` | MultiplayerSetup | 1 |
+
+**Services & UI**
+
+| Script | Path | GameObject | Instances |
+|---|---|---|---|
+| `PlayerDataService` | `_Scripts/UI/Views/PlayerDataService.cs` | PlayerDataService | 1 |
+| `UGSStatsManager` | `_Scripts/UI/UGSStatsManager.cs` | UGSStatsManager | 1 |
+| `GamepadDebugger` | `_Scripts/Utility/GamepadDebugger.cs` | GamepadDebugger | 1 |
+
+##### Unity/Third-Party Components
+
+| Component | Package | Instances |
+|---|---|---|
+| `UniversalAdditionalCameraData` | URP | 4 (Camera, CM PlayerCam, CM EndCam, CM DeathCam) |
+| `CinemachineCamera` | Cinemachine | 3 (CM Main Menu, CM EndCam, CM DeathCam) |
+| `CinemachineBrain` | Cinemachine | 1 (Camera) |
+| `CinemachineFollow` | Cinemachine | 1 (CM Main Menu) |
+| `CinemachineRotationComposer` | Cinemachine | 1 (CM Main Menu) |
+| `NetworkObject` | Netcode | 1 (SceneLoader) |
+| `EventSystem` | EventSystems | 1 |
+| `InputSystemUIInputModule` | Input System | 1 |
+| `CanvasScaler` | UnityEngine.UI | 1 |
+| `Image` | UnityEngine.UI | 1 (LoadingPanel) |
+| `LayoutElement` | UnityEngine.UI | 1 (Status Text) |
+| `TextMeshProUGUI` | TextMeshPro | 1 (Status Text) |
+
+#### Menu_Main Scene (`Assets/_Scenes/Menu_Main.unity`)
+
+**1,282 MonoBehaviours** | **89 unique script types** (63 first-party + 26 Unity/third-party) | **386 GameObjects**
+
+The Menu_Main scene hosts the main menu UI, the autopilot vessel, and the Cinemachine camera system. All persistent managers from Bootstrap are already alive via `DontDestroyOnLoad`.
+
+##### Core Game Systems (on "Game" GameObject)
+
+| Script | Path |
+|---|---|
+| `MainMenuController` | `_Scripts/System/MainMenuController.cs` |
+| `MenuServerPlayerVesselInitializer` | `_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs` |
+| `ClientPlayerVesselInitializer` | `_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs` |
+| `MenuCrystalClickHandler` | `_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs` |
+| `MainMenuCameraController` | `_Scripts/Controller/Camera/MainMenuCameraController.cs` |
+| `NetcodeHooks` | `_Scripts/Utility/Network/NetcodeHooks.cs` |
+| `NetworkCrystalManager` | `_Scripts/Controller/Environment/FlowField/NetworkCrystalManager.cs` |
+| `FlowFieldData` | `_Scripts/Controller/Environment/FlowField/FlowFieldData.cs` |
+
+##### Screen Controllers
+
+| Script | Path | GameObject |
+|---|---|---|
+| `ScreenSwitcher` | `_Scripts/UI/ScreenSwitcher.cs` | Screens |
+| `HomeScreen` | `_Scripts/UI/Screens/HomeScreen.cs` | HomeScreen |
+| `ArcadeScreen` | `_Scripts/UI/Screens/ArcadeScreen.cs` | ArcadeScreen |
+| `StoreScreen` | `_Scripts/UI/Screens/StoreScreen.cs` | ArkScreen |
+| `HangarScreen` | `_Scripts/UI/Screens/HangarScreen.cs` | Hangar Screen |
+| `LeaderboardsMenu` | `_Scripts/UI/Screens/LeaderboardsMenu.cs` | PortScreen |
+| `EpisodeScreen` | `_Scripts/UI/Screens/EpisodeScreen.cs` | EpisodeScreen |
+| `ProfileScreen` | `_Scripts/UI/Views/ProfileScreen.cs` | ProfileScreen |
+
+##### Modals
+
+| Script | Path | GameObject |
+|---|---|---|
+| `ArcadeGameConfigureModal` | `_Scripts/UI/Modals/ArcadeGameConfigureModal.cs` | ArcadeGameConfigureModal |
+| `DailyChallengeModal` | `_Scripts/UI/Modals/DailyChallengeModal.cs` | DailyChallengeModal |
```

</details>
