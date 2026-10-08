# Branch archive: `claude/nifty-bohr-61x85r`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-25 by Claude
- **Unmerged commits:** 2
- **Forked from:** `2f2a9e55b` (2026-06-25, Merge pull request #564 from froglet-studio/claude/bold-noether-sli4g9)
- **Tip:** `a77184727`
- **Files touched (6):**
  - `Assets/_Scripts/Controller/Player/MenuOfflineVesselSpawner.cs`
  - `Assets/_Scripts/System/AppManager.cs`
  - `Assets/_Scripts/System/Bootstrap/BootstrapConfigSO.cs`
  - `Assets/_Scripts/Utility/DataContainers/SceneNameListSO.cs`
  - `Assets/link.xml`
  - `Docs/WebGLBuild/SETUP.md`

### `c50066a89` — docs(webgl): add offline main-menu WebGL build blueprint

_Claude, 2026-06-25 16:54:51 +0000_

```text
Captures the scoped-down WebGL plan: dedicated Menu_Main_WebGL scene on
this branch, fully offline (no auth/Relay/party/analytics), non-networked
lava-lamp vessel via PlayerSpawner, toned-down Blob-cell ecosystem, silent
FMOD. Splits code work (this branch) from Unity-Editor scene/build work,
with grounded file:line refs and an executable Editor checklist.
```

```text
 Docs/WebGLBuild/SETUP.md | 283 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 283 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 289 lines)</summary>

```diff
diff --git a/Docs/WebGLBuild/SETUP.md b/Docs/WebGLBuild/SETUP.md
new file mode 100644
index 000000000..ea60d9b4a
--- /dev/null
+++ b/Docs/WebGLBuild/SETUP.md
@@ -0,0 +1,283 @@
+# WebGL Main-Menu Build — Setup Blueprint
+
+**Branch:** `claude/nifty-bohr-61x85r` (web build lives only here; never merges to the native game).
+**Deliverable:** a WebGL build that boots straight into a stripped-down main menu (the real menu look + a
+working Settings button) with the lava-lamp autopilot vessel drifting behind it, fully **offline** — no UGS
+auth, no Relay, no party/presence, no analytics, no audio.
+
+This is a **WebGL target build**, not a gameplay build. Launching actual games is out of scope (the website
+"custom game" integration is a separate, later phase — see §10).
+
+---
+
+## 0. Decisions (locked)
+
+| Decision | Choice |
+|---|---|
+| Networking on WebGL | **Fully offline shell** — no auth/UGS/Relay/party/analytics |
+| Behind-menu visual | **Lava-lamp vessel** via a non-networked spawn path + **toned-down ecosystem** |
+| Audio | **Silent for v1** (FMOD guarded off; 0 sound banks exist anyway) |
+| Isolation | **Dedicated `Menu_Main_WebGL` scene** + **dedicated branch** |
+| Menu scope | **Real menu, stripped** — keep the polished look, nav, and Settings; disable/empty the online screens |
+
+---
+
+## 1. Why a separate scene + branch (and what each actually buys)
+
+- **The branch is what gives isolation.** Every change here — including edits to shared startup code —
+  lives only on this branch and never reaches the native game. So we don't need the config-flag gymnastics a
+  shared-codebase approach would require; we can edit freely.
+- **The scene gives a clean strip surface.** We hard-strip multiplayer/party/instrumentation by *omission*
+  (don't place those GameObjects) rather than by guards.
+- **What a new scene does NOT save us from:** the menu UI still `[Inject]`s `GameSetting` / `AudioSystem`
+  (so we keep a `ContainerScope` + the Bootstrap DI graph), the lava-lamp vessel still needs the non-networked
+  spawn path below, and the project-wide WebGL hygiene (§7) still applies. The ongoing cost is **scene drift**:
+  `Menu_Main_WebGL` won't auto-track future native-menu changes. Acceptable for a web portal snapshot.
+
+---
+
+## 2. The hard platform facts (why the native flow can't run on WebGL)
+
+1. **WebGL cannot be a Netcode host/server.** Browsers can't open listening sockets — NGO on WebGL is
+   *client-only*. The native design is "eager per-user Relay" (every player hosts their own Relay session on
+   entering the menu). That design **physically cannot execute on WebGL.**
+2. **Transport is UDP/DTLS.** `NetworkManager.prefab` has `m_UseWebSockets:0`; sessions use default
+   `.WithRelayNetwork()` (dtls). WebGL has no UDP.
+3. **`Menu_Main` loads only via `NetworkManager.SceneManager.LoadScene`, gated on a live host**
+   (`AuthenticationSceneController.LoadMainMenuNetworkedAsync` → `WaitForRelayReadyAsync`). No host → the menu
+   never loads.
+4. **The splash/black overlay is only released by `GameDataSO.OnClientReady`**, which only fires after a
+   *networked* Player+vessel spawn (`SceneLoader.FadeFromSplashOnReady`). No networked vessel → permanent
+   black screen even if the scene loads.
+
+**The unlock:** nearly all of the UGS/Relay/instrumentation startup hangs off the **`AuthenticationData.OnSignedIn`
+event**. If auth never signs in on this branch, `HostConnectionService` never creates a Relay session, the
+Friends/Analytics facades never do their UGS work, and no host ever starts — the whole stack stays dormant on
+its own. So the offline path reduces to: **(a) don't sign in, (b) load `Menu_Main_WebGL` with a plain scene
+load, (c) spawn the lava-lamp vessel non-networked and raise `OnClientReady` to reveal the menu.**
+
+---
+
+## 3. Division of labor
+
+| Area | Who | Where |
+|---|---|---|
+| Code changes (startup redirect, offline spawner, package guards) | **Claude (this branch)** | `Assets/_Scripts/**` |
+| `link.xml`, WebGL build profile/player settings | Claude drafts / you apply in Editor | `Assets/link.xml`, `ProjectSettings` |
+| Ecosystem SO tuning | Claude (via `/ecology` protocol) | `Assets/_SO_Assets/Cell Configs/Blob Cell/*` |
+| **Scene duplication + GameObject stripping + wiring** | **You, in the Unity Editor** | `Menu_Main_WebGL.unity` |
+| **VFX Graph audit** (compute → CPU/Shuriken) | **You, in the Unity Editor** | menu VFX assets |
+| **The WebGL build itself + in-browser test** | **You, machine with Unity + WebGL module** | Build Profiles |
+
+> ⚠️ This repo is checked out in a headless container with **no Unity Editor**. Scene editing, the VFX audit,
+> and the actual `.wasm` build cannot be produced/verified here — they need a Unity install with the WebGL
+> build support module. The code + checklists below are written so you can execute the Editor side as a script.
+
+---
+
+## 4. Code changes (Claude — this branch)
+
+All grounded in current code (line refs as of this writing). Snippets are illustrative — **verify signatures
+when you compile in the Editor.**
+
+### 4.1 Offline flag — `BootstrapConfigSO.cs`
+Add a flag, exposed as a platform-aware runtime property so WebGL auto-goes offline and you can force it
+in-editor for testing:
+
+```csharp
+[Header("WebGL / Offline")]
+[SerializeField, Tooltip("Boot straight into the offline main-menu shell: no auth, no Relay, no party.")]
+bool _offlineMenuShell;
+
+// WebGL can never host Netcode, so always offline there regardless of the flag.
+public bool OfflineMenuShell => _offlineMenuShell || Application.platform == RuntimePlatform.WebGLPlayer;
+```
+
+### 4.2 Don't sign in — `AppManager.StartAuthentication()` (`AppManager.cs:535-557`)
+When `bootstrapConfig.OfflineMenuShell`, skip `authenticationServiceFacade.StartAuthentication()` entirely and
+instead publish a synthetic "offline" `AuthenticationData` (signed-out-but-ready) so SOAP readers
+(`PlayerDataService`, profile widgets) resolve a value instead of NRE-ing. No `OnSignedIn` ⇒ no host, no party,
+no Friends sync, no analytics-on-signin. This single change cascades off most of the networking/instrumentation.
+
+### 4.3 Redirect the boot to the WebGL menu scene — `SplashToAuthFlow.RunSplashFlowAsync()` (`SplashToAuthFlow.cs:95-104`)
+Today it *always* routes through the Authentication scene (to start the host). When offline, skip the auth
+scene and load the WebGL menu directly with a **plain** transition (not Netcode):
+
+```csharp
+if (bootstrapConfig.OfflineMenuShell)
+{
+    await LoadSceneWithTransitionAsync(_sceneNames.MainMenuWebGLScene); // plain SceneTransitionManager load
+    return;
+}
+// ...existing auth-scene routing unchanged for native...
+```
+
+Add `MainMenuWebGLScene` to `SceneNameListSO` and to the WebGL build profile's scene list. This bypasses
+`AuthenticationSceneController.LoadMainMenuNetworkedAsync` and its Relay-gated networked load completely.
+
+### 4.4 Non-networked lava-lamp vessel — new `MenuOfflineVesselSpawner.cs`
+Reuse the existing single-player spawn (`PlayerSpawner.SpawnPlayerAndShip`, `PlayerSpawner.cs:23-44`) +
+replicate autopilot activation (`MenuServerPlayerVesselInitializer.ActivateAutopilot`,
+`MenuServerPlayerVesselInitializer.cs:219-233`). Pattern mirrors `MiniGamePlayerSpawnerAdapter`:
+
+```csharp
+// Assets/_Scripts/Controller/Player/MenuOfflineVesselSpawner.cs  (sketch — verify APIs in-editor)
+public class MenuOfflineVesselSpawner : MonoBehaviour
+{
+    [SerializeField] PlayerSpawner playerSpawner;
+    [Inject] GameDataSO gameData;
+    [Inject] SceneTransitionManager sceneTransition;
+
+    void Start()   => gameData.OnInitializeGame.OnRaised += SpawnMenuVessel;
+    void OnDisable()=> gameData.OnInitializeGame.OnRaised -= SpawnMenuVessel;
+
+    void SpawnMenuVessel()
+    {
+        var data = new IPlayer.InitializeData {
+            vesselClass   = gameData.selectedVesselClass.Value, // Squirrel (set by AppManager.ConfigureGameData)
+            PlayerName    = "Pilot",
+            AvatarId      = 0,
+            AllowSpawning = true,
+            IsAI          = false,
+        };
+
+        var player = playerSpawner.SpawnPlayerAndShip(data);
```

</details>

### `a77184727` — feat(webgl): offline main-menu boot path scaffolding

_Claude, 2026-06-25 17:13:19 +0000_

```text
Lands the code side of the offline WebGL main-menu shell (branch-only; native
flow unchanged when OfflineMenuShell is false / non-WebGL):

- BootstrapConfigSO.OfflineMenuShell (auto-true on WebGL — it can't host Netcode)
- AppManager: skip StartAuthentication() and boot straight to the WebGL menu
  scene when offline. No OnSignedIn => no Relay host/party/friends/analytics.
- SceneNameListSO.MainMenuWebGLScene ("Menu_Main_WebGL")
- MenuOfflineVesselSpawner: non-networked lava-lamp vessel via PlayerSpawner +
  autopilot + InvokeClientReady (reuses the existing splash-release wiring)
- Assets/link.xml: preserve Assembly-CSharp + Newtonsoft for IL2CPP stripping

Scene duplication/strip/wiring, ecosystem .asset tuning, and the build are
Unity-Editor steps tracked in Docs/WebGLBuild/SETUP.md. Code compiled/verified
in the Editor on pull, not here (headless, no Unity).
```

```text
 Assets/_Scripts/Controller/Player/MenuOfflineVesselSpawner.cs | 74 +++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/System/AppManager.cs                          | 32 ++++++++++++++++--
 Assets/_Scripts/System/Bootstrap/BootstrapConfigSO.cs         | 12 +++++++
 Assets/_Scripts/Utility/DataContainers/SceneNameListSO.cs     |  5 +++
 Assets/link.xml                                               | 15 +++++++++
 Docs/WebGLBuild/SETUP.md                                      | 20 +++++++++++
 6 files changed, 156 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 219 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/MenuOfflineVesselSpawner.cs b/Assets/_Scripts/Controller/Player/MenuOfflineVesselSpawner.cs
new file mode 100644
index 000000000..8d8cf670e
--- /dev/null
+++ b/Assets/_Scripts/Controller/Player/MenuOfflineVesselSpawner.cs
@@ -0,0 +1,74 @@
+using CosmicShore.Core;
+using CosmicShore.Utility;
+using Cysharp.Threading.Tasks;
+using Reflex.Attributes;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Offline (non-networked) lava-lamp vessel spawner for the WebGL main-menu shell
+    /// (<c>Menu_Main_WebGL</c>). Replaces the Netcode <see cref="MenuServerPlayerVesselInitializer"/>:
+    /// WebGL cannot be a Netcode host, so the autopilot vessel cannot spawn through the
+    /// server/Relay pipeline. Instead it spawns through the single-player <see cref="PlayerSpawner"/>
+    /// path (the same one the arcade single-player adapter uses), activates autopilot exactly like the
+    /// networked menu initializer, and raises <see cref="GameDataSO.OnClientReady"/> so the existing
+    /// splash-release wiring reveals the menu.
+    ///
+    /// Lives only on the WebGL branch. Conserved-mass rules still apply to the trail it lays — no
+    /// caps/TTL/decay; bound prism growth via the tuned Blob cell or by throttling the spawner.
+    /// </summary>
+    public class MenuOfflineVesselSpawner : PlayerSpawnerAdapterBase
+    {
+        [Inject] SceneTransitionManager _sceneTransitionManager;
+
+        // Subscribe in Start (not OnEnable): [Inject] fields are populated after Awake but before
+        // Start (matches MiniGamePlayerSpawnerAdapter). If MainMenuController raises OnInitializeGame
+        // before this Start runs, give this component an earlier Script Execution Order than
+        // MainMenuController (verify in the Editor).
+        void Start()
+        {
+            AddSpawnPosesToGameData();
+            _gameData.OnInitializeGame.OnRaised += SpawnMenuVessel;
+        }
+
+        void OnDisable()
+        {
+            if (_gameData != null)
+                _gameData.OnInitializeGame.OnRaised -= SpawnMenuVessel;
+        }
+
+        void SpawnMenuVessel()
+        {
+            var data = new IPlayer.InitializeData
+            {
+                // Squirrel by default — set by AppManager.ConfigureGameData / MainMenuController.
+                vesselClass   = _gameData.selectedVesselClass.Value,
+                PlayerName    = "Pilot",
+                AvatarId      = 0,
+                IsAI          = false,
+                AllowSpawning = true,
+            };
+
+            var player = _playerSpawner.SpawnPlayerAndShip(data);
+            if (player == null)
+            {
+                CSDebug.LogError("[MenuOfflineVesselSpawner] Failed to spawn the offline menu vessel.");
+                return;
+            }
+
+            _gameData.AddPlayer(player);
+
+            // Autopilot — mirrors MenuServerPlayerVesselInitializer.ActivateAutopilot.
+            player.StartPlayer();
+            player.Vessel.ToggleAIPilot(true);
+            player.InputController.SetPause(true);
+
+            // Reveal the menu. InvokeClientReady reuses the existing OnClientReady splash-release
+            // (SceneLoader.FadeFromSplashOnReady) and drives MainMenuController.HandleMenuReady.
+            // The explicit FadeFromBlack is a safety net: SceneLoader only auto-arms the fade for a
+            // scene literally named "Menu_Main", and this scene is "Menu_Main_WebGL".
+            _gameData.InvokeClientReady();
+            _sceneTransitionManager?.FadeFromBlack().Forget();
+        }
+    }
+}
diff --git a/Assets/_Scripts/System/AppManager.cs b/Assets/_Scripts/System/AppManager.cs
index fd0755c32..191e77e74 100644
--- a/Assets/_Scripts/System/AppManager.cs
+++ b/Assets/_Scripts/System/AppManager.cs
@@ -126,6 +126,17 @@ namespace CosmicShore.Core
         /// </summary>
         public static bool HasBootstrapped => _hasBootstrapped;
 
+        /// <summary>
+        /// True when the app should boot the offline main-menu shell instead of the
+        /// auth → Relay-host → networked-menu path. WebGL can never be a Netcode host, so it is
+        /// always offline; on other platforms it follows the BootstrapConfigSO flag (for Editor testing).
+        /// When true, <see cref="StartAuthentication"/> is skipped (no UGS sign-in ⇒ no host, party,
+        /// friends, or analytics) and <see cref="RunBootstrapAsync"/> loads the offline WebGL menu scene.
+        /// </summary>
+        bool IsOfflineMenuShell =>
+            (_bootstrapConfig != null && _bootstrapConfig.OfflineMenuShell)
+            || Application.platform == RuntimePlatform.WebGLPlayer;
+
         #region Unity Lifecycle
 
         void Awake()
@@ -149,7 +160,12 @@ namespace CosmicShore.Core
 
             ConfigureGameData();
             StartNetworkMonitor();
-            StartAuthentication();
+
+            // Offline WebGL shell: skip UGS sign-in entirely. No OnSignedIn ⇒ no Relay host,
+            // no party/presence lobby, no Friends sync, no analytics-on-signin — the whole
+            // networking/instrumentation stack stays dormant on its own.
+            if (!IsOfflineMenuShell)
+                StartAuthentication();
 
             _cts = new CancellationTokenSource();
             RunBootstrapAsync(_cts.Token).Forget();
@@ -239,7 +255,19 @@ namespace CosmicShore.Core
                 OnBootstrapComplete?.Invoke();
                 applicationStateMachine?.TransitionTo(ApplicationState.Authenticating);
 
-                string targetScene = _sceneNames != null ? _sceneNames.AuthenticationScene : "Authentication";
+                // Offline WebGL shell: bypass the Authentication scene (and its Relay-gated networked
+                // Menu_Main load) and boot straight into the offline menu scene. Authenticating→MainMenu
+                // is a valid transition; we pass through it without doing any UGS/Netcode work.
+                string targetScene;
+                if (IsOfflineMenuShell)
+                {
+                    applicationStateMachine?.TransitionTo(ApplicationState.MainMenu);
+                    targetScene = _sceneNames != null ? _sceneNames.MainMenuWebGLScene : "Menu_Main_WebGL";
+                }
+                else
+                {
+                    targetScene = _sceneNames != null ? _sceneNames.AuthenticationScene : "Authentication";
+                }
                 Log($"Loading scene: {targetScene}");
 
                 // Use SceneTransitionManager if available (provides fade transitions).
diff --git a/Assets/_Scripts/System/Bootstrap/BootstrapConfigSO.cs b/Assets/_Scripts/System/Bootstrap/BootstrapConfigSO.cs
index c71212283..26b87d66a 100644
--- a/Assets/_Scripts/System/Bootstrap/BootstrapConfigSO.cs
+++ b/Assets/_Scripts/System/Bootstrap/BootstrapConfigSO.cs
@@ -28,11 +28,23 @@ namespace CosmicShore.Core
         [SerializeField, Tooltip("Log detailed bootstrap timing to the console.")]
         bool _verboseLogging;
 
+        [Header("WebGL / Offline")]
+        [SerializeField, Tooltip("Boot straight into the offline main-menu shell: no auth, no Relay, " +
+            "no party/presence, no analytics. WebGL is always offline regardless of this flag because it " +
+            "cannot be a Netcode host. Toggle on to force the offline shell in the Editor for testing.")]
+        bool _offlineMenuShell;
```

</details>
