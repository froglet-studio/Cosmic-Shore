# Branch archive: `fmod-2024-master`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Early 2024 multiplayer prototype plus FMOD project**

A spring-2024 branch that experimented with the first networked multiplayer: making ships network objects, a lobby using Unity Gaming Services, a server-synced timer, and reworked input for multiplayer testing. It also imported PlayFab editor extensions and added an FMOD audio project file. All of this was built on the old 2024 code layout (Game/Managers/GameManager etc.) that no longer exists.

- **Status:** Redone elsewhere
- **Areas:** multiplayer/netcode, lobby, input, PlayFab, FMOD audio
- **Already in bleeding-edge:** Multiplayer was later rebuilt from scratch: bleeding-edge has Assets/_Scripts/Controller/Multiplayer/* (ServerPlayerVesselInitializer, NetworkPlayerClientCache) and a full Party/Lobby system (Assets/_Scripts/Controller/Party/*); FMOD audio is in use throughout. No NetworkTimer or the branch's NetworkPlayer.cs exists in bleeding-edge.
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — Early multiplayer prototype on a long-gone 2024 codebase, fully replaced by the current Netcode/Party architecture.

## Evidence

- **Last commit:** 2024-04-24 by simoesnm
- **Unmerged commits:** 16
- **Forked from:** `5a203e29a` (2024-04-23, Merge branch 'master' of https://github.com/froglet-studio/Star-Writer)
- **Tip:** `f8ad0ddc6`
- **Files touched (1465):**
  - `.gitignore`
  - `Assets/DefaultNetworkPrefabs.asset`
  - `Assets/DefaultNetworkPrefabs.asset.meta`
  - `Assets/DefaultVolumeProfile.asset`
  - `Assets/DefaultVolumeProfile.asset.meta`
  - `Assets/PlayFabEditorExtensions/Editor/PlayFabEditor.cs`
  - `Assets/PlayFabEditorExtensions/Editor/Resources/PlayFabEditorPrefsSO.asset`
  - `Assets/PlayFabEditorExtensions/Editor/Resources/PlayFabEditorPrefsSO.asset.meta`
  - `Assets/PlayFabEditorExtensions/Editor/Scripts/Components/SubMenuComponent.cs`
  - `Assets/PlayFabEditorExtensions/Editor/Scripts/Components/TitleDataEditor.cs`
  - `Assets/PlayFabEditorExtensions/Editor/Scripts/Components/TitleDataViewer.cs`
  - `Assets/PlayFabEditorExtensions/Editor/Scripts/Components/TitleInternalDataEditor.cs`
  - `Assets/PlayFabEditorExtensions/Editor/Scripts/Panels/PlayFabEditorAuthenticate.cs`
  - `Assets/PlayFabEditorExtensions/Editor/Scripts/Panels/PlayFabEditorHelpMenu.cs`
  - `Assets/PlayFabEditorExtensions/Editor/Scripts/Panels/PlayFabEditorMenu.cs`
  - `Assets/PlayFabEditorExtensions/Editor/Scripts/Panels/PlayFabEditorSDKTools.cs`
  - `Assets/PlayFabEditorExtensions/Editor/Scripts/Utils/PlayFabEditorVersion.cs`
  - `Assets/PlayFabEditorExtensions/Editor/UI/Images/borderimg.png`
  - `Assets/PlayFabEditorExtensions/Editor/UI/Images/borderimg.png.meta`
  - `Assets/PlayFabEditorExtensions/Editor/UI/Images/bordernewimg.jpg`
  - `Assets/PlayFabEditorExtensions/Editor/UI/Images/bordernewimg.jpg.meta`
  - `Assets/PlayFabEditorExtensions/Editor/UI/Images/checkmark.png`
  - `Assets/PlayFabEditorExtensions/Editor/UI/Images/checkmark_hover.jpg`
  - `Assets/PlayFabEditorExtensions/Editor/UI/Images/checkmark_hover.jpg.meta`
  - `Assets/PlayFabEditorExtensions/Editor/UI/Images/checkmark_on.png`
  - `Assets/PlayFabEditorExtensions/Editor/UI/Images/dashboardIcon.png`
  - `Assets/PlayFabEditorExtensions/Editor/UI/Images/dashboardIconHover.png`
  - `Assets/PlayFabEditorExtensions/Editor/UI/PlayFabStyles.guiskin`
  - `Assets/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset`
  - `Assets/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset.meta`
  - `Assets/Resources/DefaultNetworkPrefabs.asset`
  - `Assets/Resources/DefaultNetworkPrefabs.asset.meta`
  - `Assets/Resources/InputSystem.inputsettings.asset`
  - `Assets/Resources/InputSystem.inputsettings.asset.meta`
  - `Assets/Resources/UnityPlayerAccountSettings.asset`
  - `Assets/Resources/UnityPlayerAccountSettings.asset.meta`
  - `Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat`
  - `Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`
  - `Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset`
  - `Assets/Unity Assests/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset`
  - … and 1425 more

### `a167bffa0` — Trying out shinny new version

_Echo Yin, 2024-03-11 15:40:34 -0400_

```text
 Assets/_Graphics/Materials/TransparentGreenBlockMaterial.mat          |     8 +-
 Assets/_Graphics/Materials/YellowAccentShipMaterial.mat               |     8 +-
 Assets/_Graphics/Materials/YellowConicExplosionMaterial.mat           |     9 +-
 Assets/_Graphics/Materials/YellowShipMaterial.mat                     |     8 +-
 Assets/_Graphics/Materials/YellowSkimmerMaterial.mat                  |    12 +-
 Assets/_Graphics/URP_Asset.asset                                      |    66 +-
 Assets/_Graphics/URP_Asset_Renderer.asset                             |    27 +-
 Assets/_Graphics/UniversalRenderPipelineGlobalSettings.asset          |    45 +-
 .../_Graphics/Video/Minigames/MiniGamePreview_Texture.renderTexture   |     6 +-
 Assets/_Graphics/Video/Ships/HangerPreview_Texture.renderTexture      |     6 +-
 Assets/_Models/Materials/Octagonal Sphere-Material.003.mat            |    14 +-
 Assets/_Models/Materials/masscrystal-Material.001.mat                 |    14 +-
 Assets/_Prefabs/Environment/Materials/FX/FxBoostMaterial.mat          |     9 +-
 Assets/_Prefabs/Environment/Materials/FX/vfxgraph_arclightning.vfx    |  1115 ++-
 .../_Prefabs/Environment/Materials/Octagonal Sphere-Material.003.mat  |    14 +-
 .../Testing Materials and prefabs/Test Material Blue.mat              |    14 +-
 .../Testing Materials and prefabs/Test Material Green.mat             |    14 +-
 .../Testing Materials and prefabs/Test Material Red.mat               |    14 +-
 .../Testing Materials and prefabs/Test Material White.mat             |    14 +-
 .../Integrations/Playfab/Authentication/AuthenticationView.cs         |     2 -
 Packages/manifest.json                                                |    34 +-
 Packages/packages-lock.json                                           |   183 +-
 ProjectSettings/MultiplayerManager.asset                              |     7 +
 .../{boot.config => Packages/com.unity.services.core/Settings.json}   |     0
 ProjectSettings/ProjectSettings.asset                                 |    81 +-
 ProjectSettings/ProjectVersion.txt                                    |     4 +-
 ProjectSettings/URPProjectSettings.asset                              |     2 +-
 ProjectSettings/UnityConnectSettings.asset                            |     2 +-
 ProjectSettings/VFXManager.asset                                      |     8 +-
 155 files changed, 50547 insertions(+), 2464 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 479 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationView.cs b/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationView.cs
index 63383c90f..b49ca2e9b 100644
--- a/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationView.cs
+++ b/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationView.cs
@@ -80,8 +80,6 @@ namespace CosmicShore.Integrations.Playfab.Authentication
                 registerButton.onClick.AddListener(RegisterButton_OnClick);
             
             AuthenticationManager.Instance.AnonymousLogin();
-            AuthenticationManager.OnLoginSuccess += AuthenticationManager.Instance.LoadPlayerProfile;
-            // AuthenticationManager.LoginError +=
 
             AuthenticationManager.OnProfileLoaded += InitializePlayerDisplayNameView;
         }
diff --git a/Packages/manifest.json b/Packages/manifest.json
index 7eb089e5a..97586d455 100644
--- a/Packages/manifest.json
+++ b/Packages/manifest.json
@@ -1,32 +1,32 @@
 {
   "dependencies": {
     "com.unity.2d.sprite": "1.0.0",
-    "com.unity.adaptiveperformance": "4.0.1",
-    "com.unity.adaptiveperformance.samsung.android": "4.0.2",
+    "com.unity.adaptiveperformance": "5.0.1",
+    "com.unity.adaptiveperformance.samsung.android": "5.0.0",
     "com.unity.ads": "4.4.2",
-    "com.unity.cinemachine": "2.8.9",
-    "com.unity.collab-proxy": "2.0.7",
+    "com.unity.ai.navigation": "2.0.0",
+    "com.unity.cinemachine": "2.9.5",
+    "com.unity.collab-proxy": "2.2.0",
+    "com.unity.device-simulator.devices": "1.0.0",
     "com.unity.feature.mobile": "1.0.0",
     "com.unity.ide.rider": "3.0.28",
     "com.unity.ide.visualstudio": "2.0.22",
-    "com.unity.ide.vscode": "1.2.5",
-    "com.unity.inputsystem": "1.5.1",
-    "com.unity.mobile.android-logcat": "1.3.2",
-    "com.unity.mobile.notifications": "2.1.1",
+    "com.unity.inputsystem": "1.7.0",
+    "com.unity.mobile.android-logcat": "1.4.0",
+    "com.unity.mobile.notifications": "2.3.1",
     "com.unity.nuget.newtonsoft-json": "3.2.1",
     "com.unity.performance.profile-analyzer": "1.2.2",
     "com.unity.purchasing": "4.10.0",
-    "com.unity.recorder": "3.0.3",
-    "com.unity.render-pipelines.universal": "12.1.11",
-    "com.unity.services.analytics": "4.4.2",
+    "com.unity.recorder": "5.0.0",
+    "com.unity.render-pipelines.universal": "16.0.5",
+    "com.unity.services.analytics": "5.1.0",
     "com.unity.services.relay": "1.0.5",
-    "com.unity.test-framework": "1.1.33",
-    "com.unity.textmeshpro": "3.0.8",
-    "com.unity.timeline": "1.6.5",
-    "com.unity.ugui": "1.0.0",
-    "com.unity.visualeffectgraph": "12.1.11",
-    "com.veriorpies.parrelsync": "https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync",
+    "com.unity.test-framework": "1.3.9",
+    "com.unity.timeline": "1.8.6",
+    "com.unity.ugui": "2.0.0",
+    "com.unity.visualeffectgraph": "16.0.5",
     "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.15.1",
+    "com.unity.modules.accessibility": "1.0.0",
     "com.unity.modules.ai": "1.0.0",
     "com.unity.modules.androidjni": "1.0.0",
     "com.unity.modules.animation": "1.0.0",
diff --git a/Packages/packages-lock.json b/Packages/packages-lock.json
index fb7f644a8..fe26f56ff 100644
--- a/Packages/packages-lock.json
+++ b/Packages/packages-lock.json
@@ -7,21 +7,20 @@
       "dependencies": {}
     },
     "com.unity.adaptiveperformance": {
-      "version": "4.0.1",
+      "version": "5.0.1",
       "depth": 0,
       "source": "registry",
       "dependencies": {
```

</details>

### `f7592bb54` — Eliminate deprecated methods

_Echo Yin, 2024-03-11 16:04:41 -0400_

```text
 Assets/_Scripts/App/Systems/RewindSystem/RewindSystem.cs                   |  2 +-
 Assets/_Scripts/App/UI/ModalWindowManager.cs                               |  2 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs |  2 +-
 Assets/_Scripts/Game/IO/ControllerButtonPress.cs                           |  2 +-
 Assets/_Scripts/Game/Managers/GameManager.cs                               |  4 ++--
 Assets/_Scripts/Game/Ship/BoidController.cs                                |  2 +-
 Assets/_Scripts/Game/Ship/ShipActions/MoundDronesShipAction.cs             |  2 +-
 Assets/_Scripts/Integrations/Firebase/Controller/UnityAnalytics.cs         | 26 +++++++++++++-------------
 Packages/manifest.json                                                     |  2 +-
 Packages/packages-lock.json                                                |  2 +-
 10 files changed, 23 insertions(+), 23 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 175 lines)</summary>

```diff
diff --git a/Assets/_Scripts/App/Systems/RewindSystem/RewindSystem.cs b/Assets/_Scripts/App/Systems/RewindSystem/RewindSystem.cs
index 063f91ce1..9f7360b20 100644
--- a/Assets/_Scripts/App/Systems/RewindSystem/RewindSystem.cs
+++ b/Assets/_Scripts/App/Systems/RewindSystem/RewindSystem.cs
@@ -126,7 +126,7 @@ namespace CosmicShore.App.Systems.RewindSystem
         }
         private void Awake()
         {
-            _rewoundObjects = FindObjectsOfType<RewindBase>().ToList();
+            _rewoundObjects = FindObjectsByType<RewindBase>(FindObjectsSortMode.None).ToList();
 
             if (Instance != null && Instance != this)
             {
diff --git a/Assets/_Scripts/App/UI/ModalWindowManager.cs b/Assets/_Scripts/App/UI/ModalWindowManager.cs
index 4b5e50e4c..ebce3ba15 100644
--- a/Assets/_Scripts/App/UI/ModalWindowManager.cs
+++ b/Assets/_Scripts/App/UI/ModalWindowManager.cs
@@ -60,7 +60,7 @@ namespace CosmicShore.App.UI
                 isOn = false;
             }
 
-            StartCoroutine("DisableWindow");
+            StartCoroutine(DisableWindow());
         }
 
         IEnumerator DisableWindow()
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
index 63d5e0125..067322ed2 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
@@ -51,7 +51,7 @@ public class BoidSImulationController : MonoBehaviour
     private void InitializeTrailBlocks()
     {
         // Assuming you have a method or a way to get all non-boid trail blocks in the scene
-        TrailBlock[] trailBlocks = FindObjectsOfType<TrailBlock>();
+        TrailBlock[] trailBlocks = FindObjectsByType<TrailBlock>(FindObjectsSortMode.None);
         foreach (var block in trailBlocks)
         {
             if (!block.CompareTag("Fauna")) // Assuming "Fauna" is the tag for boid trail blocks
diff --git a/Assets/_Scripts/Game/IO/ControllerButtonPress.cs b/Assets/_Scripts/Game/IO/ControllerButtonPress.cs
index 07e421c08..12928b18b 100644
--- a/Assets/_Scripts/Game/IO/ControllerButtonPress.cs
+++ b/Assets/_Scripts/Game/IO/ControllerButtonPress.cs
@@ -15,7 +15,7 @@ namespace CosmicShore.Game.IO
         Button button;
         void Start()
         {
-            eventSystem = FindObjectOfType<EventSystem>();
+            eventSystem = FindAnyObjectByType<EventSystem>();
             button = GetComponent<Button>();
         }
 
diff --git a/Assets/_Scripts/Game/Managers/GameManager.cs b/Assets/_Scripts/Game/Managers/GameManager.cs
index 04fb0e6ea..cbc4127f5 100644
--- a/Assets/_Scripts/Game/Managers/GameManager.cs
+++ b/Assets/_Scripts/Game/Managers/GameManager.cs
@@ -71,8 +71,8 @@ namespace CosmicShore.Core
         public void WaitOnAILoading(AIPilot aiPilot)
         {
             // TODO: P1 elemental crystals, FindObjectOfType may no work anymore for this
-            aiPilot.CrystalTransform = FindObjectOfType<Crystal>().transform;
-            aiPilot.flowFieldData = FindObjectOfType<FlowFieldData>();
+            aiPilot.CrystalTransform = FindAnyObjectByType<Crystal>().transform;
+            aiPilot.flowFieldData = FindAnyObjectByType<FlowFieldData>();
         }
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Game/Ship/BoidController.cs b/Assets/_Scripts/Game/Ship/BoidController.cs
index 2c220f96e..f583a204e 100644
--- a/Assets/_Scripts/Game/Ship/BoidController.cs
+++ b/Assets/_Scripts/Game/Ship/BoidController.cs
@@ -21,7 +21,7 @@ namespace CosmicShore
         void Start()
         {
         inputController = ship.InputController;
-        globalGoal = FindObjectOfType<Crystal>().transform;
+        globalGoal = FindAnyObjectByType<Crystal>().transform;
         container = new GameObject("BoidContainer");
         container.transform.SetParent(ship.Player.transform);
```

</details>

### `617a54139` — Get rid of old profile loading bug

_Echo Yin, 2024-03-11 16:15:12 -0400_

```text
 Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationManager.cs | 19 +++++--------------
 Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationView.cs    |  3 ---
 Packages/manifest.json                                                       |  2 +-
 Packages/packages-lock.json                                                  |  2 +-
 4 files changed, 7 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 89 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationManager.cs b/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationManager.cs
index 34b529eab..92cfdb714 100644
--- a/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationManager.cs
+++ b/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationManager.cs
@@ -32,32 +32,23 @@ namespace CosmicShore.Integrations.Playfab.Authentication
 
         public static List<string> Adjectives;
         public static List<string> Nouns;
-        
-        // public AuthenticationManager(PlayFabAccount account, UserProfile profile, PlayerSession session)
-        // {
-        //     PlayFabAccount = account;
-        //     UserProfile = profile;
-        //     PlayerSession = session;
-        // }
+
         public override void Awake()
         {
             base.Awake();
+            PlayFabAccount = new();
+            UserProfile = new();
+            PlayerSession = new();
             AnonymousLogin();
-            OnLoginSuccess += LoadPlayerProfile;
         }
 
         private void OnEnable()
         {
-            PlayFabAccount = new();
-            UserProfile = new();
-            PlayerSession = new();
+            OnLoginSuccess += LoadPlayerProfile;
         }
 
         private void OnDestroy()
         {
-            PlayFabAccount = null;
-            UserProfile = null;
-            PlayerSession = null;
             OnLoginSuccess -= LoadPlayerProfile;
         }
 
diff --git a/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationView.cs b/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationView.cs
index b49ca2e9b..4a145bb26 100644
--- a/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationView.cs
+++ b/Assets/_Scripts/Integrations/Playfab/Authentication/AuthenticationView.cs
@@ -5,7 +5,6 @@ using PlayFab.ClientModels;
 using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
-using VContainer;
 
 namespace CosmicShore.Integrations.Playfab.Authentication
 {
@@ -78,8 +77,6 @@ namespace CosmicShore.Integrations.Playfab.Authentication
             
             if(registerButton!=null)
                 registerButton.onClick.AddListener(RegisterButton_OnClick);
-            
-            AuthenticationManager.Instance.AnonymousLogin();
 
             AuthenticationManager.OnProfileLoaded += InitializePlayerDisplayNameView;
         }
diff --git a/Packages/manifest.json b/Packages/manifest.json
index 92722fe04..59252316c 100644
--- a/Packages/manifest.json
+++ b/Packages/manifest.json
@@ -1,7 +1,7 @@
 {
   "dependencies": {
     "com.unity.2d.sprite": "1.0.0",
-    "com.unity.adaptiveperformance": "5.0.1",
+    "com.unity.adaptiveperformance": "5.0.2",
     "com.unity.adaptiveperformance.samsung.android": "5.0.0",
     "com.unity.ads": "4.4.2",
     "com.unity.ai.navigation": "2.0.0",
diff --git a/Packages/packages-lock.json b/Packages/packages-lock.json
index be70cc69c..d0805a355 100644
--- a/Packages/packages-lock.json
+++ b/Packages/packages-lock.json
```

</details>

### `c9dedfa60` — Integrate interface with singleplayer ships and network ships

_Echo Yin, 2024-03-12 21:16:38 -0400_

```text
 Assets/_Scripts/App/UI/Elements/MultiplayerView.cs.meta           |    2 +
 Assets/_Scripts/Game/Environment/FlowField/Crystal.cs             |   12 +-
 Assets/_Scripts/Game/Managers/StatsManager.cs                     |    4 +-
 Assets/_Scripts/Game/Projectiles/AOEExplosion.cs                  |    2 +-
 Assets/_Scripts/Game/Projectiles/FakeCrystal.cs                   |    2 +-
 Assets/_Scripts/Game/Ship/ElementalShipComponent.cs               |    2 +-
 Assets/_Scripts/Game/Ship/IShip.cs                                |   75 +
 Assets/_Scripts/Game/Ship/IShip.cs.meta                           |    3 +
 Assets/_Scripts/Game/Ship/Ship.cs                                 |   27 +-
 Assets/_Scripts/Game/Ship/ShipActions/GhostAction.cs              |    2 +-
 Assets/_Scripts/Game/Ship/ShipActions/ShipAction.cs               |    4 +-
 Assets/_Scripts/Game/Ship/ShipActions/ToggleGyroAction.cs         |    2 +-
 Assets/_Scripts/Game/Ship/ShipActions/ZoomOutAction.cs            |    4 +-
 Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs                 |    2 +-
 Assets/_Scripts/Game/Ship/ShipGeometry.cs                         |    2 +-
 Assets/_Scripts/Game/Ship/ShipNetwork.cs                          |  503 ++++++
 Assets/_Scripts/Game/Ship/ShipNetwork.cs.meta                     |   11 +
 Assets/_Scripts/Multiplayer/AwsLambda.meta                        |    8 +
 Assets/_Scripts/Multiplayer/{ => AwsLambda}/ClientManager.cs      |    0
 Assets/_Scripts/Multiplayer/{ => AwsLambda}/ClientManager.cs.meta |    0
 Assets/_Scripts/Multiplayer/{ => AwsLambda}/SharedData.cs         |    0
 Assets/_Scripts/Multiplayer/{ => AwsLambda}/SharedData.cs.meta    |    0
 Assets/_Scripts/Multiplayer/UnityMultiplayer.meta                 |    8 +
 Assets/_Scripts/Multiplayer/UnityMultiplayer/TestRelay.cs         |   64 +
 Assets/_Scripts/Multiplayer/UnityMultiplayer/TestRelay.cs.meta    |    2 +
 Assets/_Scripts/Utility/ClassExtensions/DebugLogExtensions.cs     |    1 +
 Packages/manifest.json                                            |    5 +
 Packages/packages-lock.json                                       |   80 +-
 ProjectSettings/EditorSettings.asset                              |   11 +-
 41 files changed, 8122 insertions(+), 609 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1216 lines)</summary>

```diff
diff --git a/Assets/_Scripts/App/UI/Elements/MultiplayerView.cs b/Assets/_Scripts/App/UI/Elements/MultiplayerView.cs
new file mode 100644
index 000000000..d28db919e
--- /dev/null
+++ b/Assets/_Scripts/App/UI/Elements/MultiplayerView.cs
@@ -0,0 +1,58 @@
+using CosmicShore.Utility.ClassExtensions;
+using Unity.Netcode;
+using UnityEngine;
+using UnityEngine.UI;
+using CosmicShore.Game.Arcade;
+using UnityEngine.SceneManagement;
+
+namespace CosmicShore
+{
+    public class MultiplayerView : MonoBehaviour
+    {
+        [Header("UI Buttons")]
+        [SerializeField] private Button joinGameButton;
+        [SerializeField] private Button hostGameButton;
+        [SerializeField] private Button spectateGameButton;
+        
+        [Header("Vessel Settings")]
+        [SerializeField] private SO_Vessel hostVessel;
+        [SerializeField] private SO_Vessel clientVessel;
+        
+        private void Awake()
+        {
+            hostGameButton.onClick.AddListener(HostGame);
+            joinGameButton.onClick.AddListener(JoinGame);
+            spectateGameButton.onClick.AddListener(SpectateGame);
+        }
+
+        private void HostGame()
+        {
+            this.LogWithClassMethod("", "Hosting a game.");
+            LoadMiniGame(ShipTypes.Manta, hostVessel, 1,1);
+            NetworkManager.Singleton.StartHost();
+        }
+
+        private void JoinGame()
+        {
+            this.LogWithClassMethod("", "Joining a game as client.");
+            LoadMiniGame(ShipTypes.Rhino, clientVessel, 1,1);
+            NetworkManager.Singleton.StartClient();
+        }
+
+        private void SpectateGame()
+        {
+            this.LogWithClassMethod("", "Join a game as spectator.");
+            NetworkManager.Singleton.StartClient();
+        }
+
+        private void LoadMiniGame(ShipTypes type, SO_Vessel vessel, int intensity, int playerCount)
+        {
+            MiniGame.PlayerShipType = type;
+            MiniGame.PlayerVessel = vessel;
+            MiniGame.IntensityLevel = intensity;
+            MiniGame.NumberOfPlayers = playerCount;
+
+            SceneManager.LoadScene("MinigameCellularDuel");
+        }
+    }
+}
diff --git a/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
index c54ab022b..0cc9b2c5d 100644
--- a/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
@@ -67,7 +67,7 @@ namespace CosmicShore.Environment.FlowField
             collisions.Clear();
         }
 
-        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties, Ship ship)
+        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties, IShip ship)
         {
             foreach (CrystalImpactEffects effect in crystalImpactEffects)
             {
@@ -97,7 +97,7 @@ namespace CosmicShore.Environment.FlowField
 
         protected virtual void Collide(Collider other)
```

</details>

### `c7d2ad15f` — Fix ship instance not found in ship actions

_Echo Yin, 2024-03-12 21:39:53 -0400_

```text
 Assets/_Scripts/Game/Ship/ShipActions/ToggleGyroAction.cs | 11 ++++++++++-
 1 file changed, 10 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/ShipActions/ToggleGyroAction.cs b/Assets/_Scripts/Game/Ship/ShipActions/ToggleGyroAction.cs
index d47a9fa19..abe337fb9 100644
--- a/Assets/_Scripts/Game/Ship/ShipActions/ToggleGyroAction.cs
+++ b/Assets/_Scripts/Game/Ship/ShipActions/ToggleGyroAction.cs
@@ -1,3 +1,4 @@
+using CosmicShore.Core;
 using CosmicShore.Game.IO;
 
 public class ToggleGyroAction : ShipAction
@@ -6,7 +7,15 @@ public class ToggleGyroAction : ShipAction
 
     protected override void Start()
     {
-        if (ship.isActiveAndEnabled) inputController = ship.InputController;
+        if (ship == null)
+        {
+            ship = GetComponentInParent<Ship>();
+            if (ship == null)
+            {
+                ship = GetComponentInParent<ShipNetwork>();
+            }
+            inputController = ship.InputController;
+        }
     }
 
     public override void StartAction()
```

</details>

### `4341686e5` — Update unity player account settings

_Echo Yin, 2024-03-13 11:54:51 -0400_

```text
 Assets/Resources/UnityPlayerAccountSettings.asset      | 19 +++++++++++++++++++
 Assets/Resources/UnityPlayerAccountSettings.asset.meta |  8 ++++++++
 2 files changed, 27 insertions(+)
```

### `5656ba9e9` — Swap network behaviour on ship

_Echo Yin, 2024-03-13 14:51:21 -0400_

```text
 Assets/_Prefabs/Spaceships/MantaNetwork.prefab                        | 3157 -------------------------------
 Assets/_Prefabs/Spaceships/MantaNetwork.prefab.meta                   |    7 -
 Assets/_Prefabs/Spaceships/RhinoNetwork.prefab                        | 1916 -------------------
 Assets/_Prefabs/Spaceships/RhinoNetwork.prefab.meta                   |    7 -
 Assets/_Scenes/Menu_Main.unity                                        |    2 +-
 Assets/_Scripts/Game/Environment/FlowField/Crystal.cs                 |    6 +-
 Assets/_Scripts/Game/Managers/StatsManager.cs                         |    4 +-
 Assets/_Scripts/Game/Projectiles/AOEExplosion.cs                      |    4 +-
 Assets/_Scripts/Game/Projectiles/ExplodableProjectile.cs              |    1 -
 Assets/_Scripts/Game/Projectiles/Projectile.cs                        |    2 +-
 Assets/_Scripts/Game/Ship/ElementalShipComponent.cs                   |    6 +-
 Assets/_Scripts/Game/Ship/IShip.cs                                    |  147 +-
 Assets/_Scripts/Game/Ship/Ship.cs                                     |    2 +-
 Assets/_Scripts/Game/Ship/ShipActions/ShipAction.cs                   |    4 +-
 Assets/_Scripts/Game/Ship/ShipActions/ToggleGyroAction.cs             |   17 +-
 Assets/_Scripts/Game/Ship/ShipGeometry.cs                             |    2 +-
 Assets/_Scripts/Game/Ship/ShipNetwork.cs                              |  503 -----
 Assets/_Scripts/Game/Ship/ShipNetwork.cs.meta                         |   11 -
 .../Integrations/Playfab/Authentication/AuthenticationManager.cs      |    3 +-
 19 files changed, 97 insertions(+), 5704 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 873 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
index 0cc9b2c5d..fa2cac313 100644
--- a/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
@@ -67,7 +67,7 @@ namespace CosmicShore.Environment.FlowField
             collisions.Clear();
         }
 
-        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties, IShip ship)
+        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties, Ship ship)
         {
             foreach (CrystalImpactEffects effect in crystalImpactEffects)
             {
@@ -97,7 +97,7 @@ namespace CosmicShore.Environment.FlowField
 
         protected virtual void Collide(Collider other)
         {
-            IShip ship;
+            Ship ship;
             Projectile projectile;
             if (IsShip(other.gameObject))
             {
@@ -159,7 +159,7 @@ namespace CosmicShore.Environment.FlowField
             UpdateSelfWithNode();
         }
 
-        protected void Explode(IShip ship)
+        protected void Explode(Ship ship)
         {
             tempMaterial = new Material(explodingMaterial);
             var spentCrystal = Instantiate(SpentCrystalPrefab);
diff --git a/Assets/_Scripts/Game/Managers/StatsManager.cs b/Assets/_Scripts/Game/Managers/StatsManager.cs
index 0bb97999b..a34af0f13 100644
--- a/Assets/_Scripts/Game/Managers/StatsManager.cs
+++ b/Assets/_Scripts/Game/Managers/StatsManager.cs
@@ -16,7 +16,7 @@ public class StatsManager : Singleton<StatsManager>
 
     bool RecordStats = true;
 
-    public void CrystalCollected(IShip ship, CrystalProperties crystalProperties)
+    public void CrystalCollected(Ship ship, CrystalProperties crystalProperties)
     {
         if (!RecordStats)
             return;
@@ -32,7 +32,7 @@ public class StatsManager : Singleton<StatsManager>
         playerStats[ship.Player.PlayerName] = roundStats;
     }
 
-    public void SkimmerShipCollision(Ship skimmingShip, IShip ship)
+    public void SkimmerShipCollision(Ship skimmingShip, Ship ship)
     {
         if (!RecordStats)
             return;
diff --git a/Assets/_Scripts/Game/Projectiles/AOEExplosion.cs b/Assets/_Scripts/Game/Projectiles/AOEExplosion.cs
index 47887792a..ed2a0a097 100644
--- a/Assets/_Scripts/Game/Projectiles/AOEExplosion.cs
+++ b/Assets/_Scripts/Game/Projectiles/AOEExplosion.cs
@@ -28,9 +28,9 @@ namespace CosmicShore.Game.Projectiles
         protected static GameObject container;
 
         // Material and Team
-        [HideInInspector] public Material Material { get; set; }
+        public Material Material { get; set; }
         [HideInInspector] public Teams Team;
-        [HideInInspector] public IShip Ship;
+        [HideInInspector] public Ship Ship;
         [HideInInspector] public bool AnonymousExplosion;
 
         protected virtual void Start()
diff --git a/Assets/_Scripts/Game/Projectiles/ExplodableProjectile.cs b/Assets/_Scripts/Game/Projectiles/ExplodableProjectile.cs
index 74ce5fcc7..a4d5b8718 100644
--- a/Assets/_Scripts/Game/Projectiles/ExplodableProjectile.cs
+++ b/Assets/_Scripts/Game/Projectiles/ExplodableProjectile.cs
@@ -1,4 +1,3 @@
-using CosmicShore.Core;
 using UnityEngine;
 
 namespace CosmicShore.Game.Projectiles
diff --git a/Assets/_Scripts/Game/Projectiles/Projectile.cs b/Assets/_Scripts/Game/Projectiles/Projectile.cs
index ead0c0d3f..248e28ce0 100644
```

</details>

### `41cab5b6b` — Import playfab extensions

_Echo Yin, 2024-03-13 15:39:16 -0400_

```text
 Assets/PlayFabEditorExtensions/Editor/PlayFabEditor.cs                |   4 +-
 .../Editor/Resources/PlayFabEditorPrefsSO.asset                       |   8 +-
 .../Editor/Resources/PlayFabEditorPrefsSO.asset.meta                  |   2 +-
 .../Editor/Scripts/Components/SubMenuComponent.cs                     |  61 ++++++++++-
 .../Editor/Scripts/Components/TitleDataEditor.cs                      |   1 +
 .../Editor/Scripts/Components/TitleDataViewer.cs                      | 109 ++++++++++++++++++-
 .../Editor/Scripts/Components/TitleInternalDataEditor.cs              |   1 +
 .../Editor/Scripts/Panels/PlayFabEditorAuthenticate.cs                | 178 ++++++++++++++++++++++++++++++--
 .../Editor/Scripts/Panels/PlayFabEditorHelpMenu.cs                    |  96 +++++++++++++++--
 .../Editor/Scripts/Panels/PlayFabEditorMenu.cs                        |   6 +-
 .../Editor/Scripts/Panels/PlayFabEditorSDKTools.cs                    | 153 ++++++++++++++++++++++++++-
 .../Editor/Scripts/Utils/PlayFabEditorVersion.cs                      |   2 +-
 Assets/PlayFabEditorExtensions/Editor/UI/Images/borderimg.png         | Bin 0 -> 368 bytes
 Assets/PlayFabEditorExtensions/Editor/UI/Images/borderimg.png.meta    | 140 +++++++++++++++++++++++++
 Assets/PlayFabEditorExtensions/Editor/UI/Images/bordernewimg.jpg      | Bin 0 -> 1611 bytes
 Assets/PlayFabEditorExtensions/Editor/UI/Images/bordernewimg.jpg.meta | 127 +++++++++++++++++++++++
 Assets/PlayFabEditorExtensions/Editor/UI/Images/checkmark.png         | Bin 2763 -> 3047 bytes
 Assets/PlayFabEditorExtensions/Editor/UI/Images/checkmark_hover.jpg   | Bin 0 -> 2410 bytes
 .../PlayFabEditorExtensions/Editor/UI/Images/checkmark_hover.jpg.meta | 127 +++++++++++++++++++++++
 Assets/PlayFabEditorExtensions/Editor/UI/Images/checkmark_on.png      | Bin 55178 -> 55037 bytes
 Assets/PlayFabEditorExtensions/Editor/UI/Images/dashboardIcon.png     | Bin 49180 -> 49158 bytes
 .../PlayFabEditorExtensions/Editor/UI/Images/dashboardIconHover.png   | Bin 49499 -> 49468 bytes
 Assets/PlayFabEditorExtensions/Editor/UI/PlayFabStyles.guiskin        |  22 ++--
 Assets/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset |   2 +-
 .../Shared/Public/Resources/PlayFabSharedSettings.asset.meta          |   4 +-
 25 files changed, 997 insertions(+), 46 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 994 lines)</summary>

```diff
diff --git a/Assets/PlayFabEditorExtensions/Editor/PlayFabEditor.cs b/Assets/PlayFabEditorExtensions/Editor/PlayFabEditor.cs
index 3abb445ca..7eb4c519e 100644
--- a/Assets/PlayFabEditorExtensions/Editor/PlayFabEditor.cs
+++ b/Assets/PlayFabEditorExtensions/Editor/PlayFabEditor.cs
@@ -341,13 +341,13 @@ namespace PlayFab.PfEditor
                     // clear blocking requests
                     ProgressBar.UpdateState(ProgressBar.ProgressBarStates.error);
                     ClearBlockingRequest();
-                    Debug.LogError(string.Format("PlayFab EditorExtensions: Caught an error:{0}", status));
+                    Debug.LogError(string.Format("<color=white>PlayFab EditorExtensions: Caught an error:</color>{0}", status));
                     break;
 
                 case EdExStates.OnWarning:
                     ProgressBar.UpdateState(ProgressBar.ProgressBarStates.warning);
                     ClearBlockingRequest();
-                    Debug.LogWarning(string.Format("PlayFab EditorExtensions: {0}", status));
+                    Debug.LogWarning(string.Format("<color=white>PlayFab EditorExtensions:</color>{0}",  status ));
                     break;
 
                 case EdExStates.OnSuccess:
diff --git a/Assets/PlayFabEditorExtensions/Editor/Scripts/Components/SubMenuComponent.cs b/Assets/PlayFabEditorExtensions/Editor/Scripts/Components/SubMenuComponent.cs
index 914308b49..badc30dfb 100644
--- a/Assets/PlayFabEditorExtensions/Editor/Scripts/Components/SubMenuComponent.cs
+++ b/Assets/PlayFabEditorExtensions/Editor/Scripts/Components/SubMenuComponent.cs
@@ -11,9 +11,56 @@ namespace PlayFab.PfEditor
         GUIStyle selectedStyle;
         GUIStyle defaultStyle;
         GUIStyle bgStyle;
+        private static int focusIndex = 0;
+
+        //changes local
+        public static void InputSubMenuStudiosHandler()
+        {
+            var e = Event.current;
+            if (e.type == EventType.KeyUp)
+            {
+                if (e.keyCode == KeyCode.RightArrow)
+                {
+                    switch (focusIndex)
+                    {
+                        case 0:
+                            GUI.FocusControl("project");
+                            focusIndex = 1;
+                            break;
+                        case 1:
+                            GUI.FocusControl("studios");
+                            focusIndex = 2;
+                            break;
+                        case 2:
+                            GUI.FocusControl("API");
+                            focusIndex = 0;
+                            break;
+                    }
+                }
+                else if (e.keyCode == KeyCode.LeftArrow)
+                {
+                    switch (focusIndex)
+                    {
+                        case 0:
+                            GUI.FocusControl("API");
+                            focusIndex = 2;
+                            break;
+                        case 1:
+                            GUI.FocusControl("project");
+                            focusIndex = 0;
+                            break;
+                        case 2:
+                            GUI.FocusControl("studios");
+                            focusIndex = 1;
+                            break;
+                    }
+                }
+            }
+        }
 
         public void DrawMenu()
         {
+            InputSubMenuStudiosHandler();
             selectedStyle = selectedStyle ?? PlayFabEditorHelper.uiStyle.GetStyle("textButton_selected");
             defaultStyle = defaultStyle ?? PlayFabEditorHelper.uiStyle.GetStyle("textButton");
```

</details>

### `646538764` — Apply network object and network transform to the ships

_Echo Yin, 2024-03-13 19:55:43 -0400_

```text
 Assets/Resources/DefaultNetworkPrefabs.asset                          |  16 +
 Assets/Resources/DefaultNetworkPrefabs.asset.meta                     |   8 +
 Assets/Resources/InputSystem.inputsettings.asset                      |  36 ++
 Assets/Resources/InputSystem.inputsettings.asset.meta                 |   8 +
 Assets/_Graphics/Screenshots/SquareIconRenderTexture.renderTexture    |   6 +-
 Assets/_Graphics/Screenshots/WideIconRenderTexture.renderTexture      |   6 +-
 Assets/_Prefabs/Spaceships/Dolphin.prefab                             | 430 +++++++++++++++---
 Assets/_Prefabs/Spaceships/Grizzly.prefab                             | 318 +++++++++++--
 Assets/_Prefabs/Spaceships/Manta.prefab                               | 495 ++++++++++++++++----
 Assets/_Prefabs/Spaceships/Rhino.prefab                               | 258 +++++++++--
 Assets/_Prefabs/Spaceships/Serpent.prefab                             | 490 ++++++++++++++++----
 Assets/_Prefabs/Spaceships/Squirrel.prefab                            | 487 ++++++++++++++++----
 Assets/_Prefabs/Spaceships/Termite.prefab                             | 504 ++++++++++++++++-----
 Assets/_Prefabs/Spaceships/Urchin.prefab                              | 517 +++++++++++++++++----
 Assets/_Scenes/Menu_Main.unity                                        |   2 +-
 Assets/_Scenes/TestScenes/MultiplayerTests/ClientManager.prefab       |  46 ++
 Assets/_Scenes/TestScenes/MultiplayerTests/ClientManager.prefab.meta  |   7 +
 Assets/_Scenes/TestScenes/MultiplayerTests/NetworkPlayer.cs           |  59 +++
 Assets/_Scenes/TestScenes/MultiplayerTests/NetworkPlayer.cs.meta      |   2 +
 Assets/_Scenes/TestScenes/MultiplayerTests/Player.prefab              | 174 +++++++
 Assets/_Scenes/TestScenes/MultiplayerTests/Player.prefab.meta         |   7 +
 Assets/_Scenes/TestScenes/MultiplayerTests/UrlGet.unity               | 300 +++++++++++--
 Assets/_Scripts/Game/Ship/Ship.cs                                     |   8 +-
 .../_Scripts/Multiplayer/UnityMultiplayer/ClientNetworkTransform.cs   |  13 +
 .../Multiplayer/UnityMultiplayer/ClientNetworkTransform.cs.meta       |   2 +
 ProjectSettings/EditorBuildSettings.asset                             |  12 +-
 ProjectSettings/NetcodeForGameObjects.asset                           |  17 +
 ProjectSettings/Packages/com.unity.services.core/Settings.json        |   1 +
 ProjectSettings/QualitySettings.asset                                 | 122 ++++-
 32 files changed, 4474 insertions(+), 708 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 124 lines)</summary>

```diff
diff --git a/Assets/_Scenes/TestScenes/MultiplayerTests/NetworkPlayer.cs b/Assets/_Scenes/TestScenes/MultiplayerTests/NetworkPlayer.cs
new file mode 100644
index 000000000..30d1635dd
--- /dev/null
+++ b/Assets/_Scenes/TestScenes/MultiplayerTests/NetworkPlayer.cs
@@ -0,0 +1,59 @@
+using System;
+using CosmicShore.Utility.ClassExtensions;
+using Unity.Netcode;
+
+namespace CosmicShore
+{
+    public class NetworkPlayer : NetworkBehaviour
+    {
+        private int _counter = 0;
+        // Start is called before the first frame update
+        private void Awake()
+        {
+            ++_counter;
+            this.LogWithClassMethod("Awake", _counter.ToString());
+        }
+
+        private void OnEnable()
+        {
+            ++_counter;
+            this.LogWithClassMethod("OnEnable", _counter.ToString());
+        }
+
+        private void OnValidate()
+        {
+            ++_counter;
+            this.LogWithClassMethod("OnValidate", _counter.ToString());
+        }
+
+        void Start()
+        {
+            ++_counter;
+            this.LogWithClassMethod("Start", _counter.ToString());
+        }
+
+        public override void OnNetworkSpawn()
+        {
+            ++_counter;
+            this.LogWithClassMethod("OnNetworkSpawn", _counter.ToString());
+        }
+
+        public override void OnNetworkDespawn()
+        {
+            ++_counter;
+            this.LogWithClassMethod("OnNetworkDespawn", _counter.ToString());
+        }
+
+        void OnDestory()
+        {
+            ++_counter;
+            
+        }
+
+        void OnDisable()
+        {
+            ++_counter;
+            this.LogWithClassMethod("OnDisable", _counter.ToString());
+        }
+    }
+}
diff --git a/Assets/_Scripts/Game/Ship/Ship.cs b/Assets/_Scripts/Game/Ship/Ship.cs
index 1085cf74b..c7616f51b 100644
--- a/Assets/_Scripts/Game/Ship/Ship.cs
+++ b/Assets/_Scripts/Game/Ship/Ship.cs
@@ -6,6 +6,7 @@ using UnityEngine;
 using CosmicShore.Game.AI;
 using CosmicShore.Game.Projectiles;
 using CosmicShore.Models.ScriptableObjects;
+using Unity.Netcode;
 
 namespace CosmicShore.Core
 {
@@ -26,7 +27,7 @@ namespace CosmicShore.Core
     [RequireComponent(typeof(ResourceSystem))]
     [RequireComponent(typeof(TrailSpawner))]
```

</details>

### `1c36d904f` — Rework input controls for multiplayer testing

_Echo Yin, 2024-03-18 12:58:52 -0400_

```text
 .../Editor/Resources/PlayFabEditorPrefsSO.asset                       |    8 +-
 Assets/_Scenes/Menu_Main.unity                                        |    2 +-
 Assets/_Scenes/TestScenes/MultiplayerTests/MantaNetwork.prefab        | 2982 +++++++++++++++++++++++++++++++
 Assets/_Scenes/TestScenes/MultiplayerTests/MantaNetwork.prefab.meta   |    7 +
 Assets/_Scenes/TestScenes/MultiplayerTests/RhinoNetwork.prefab        | 1603 +++++++++++++++++
 Assets/_Scenes/TestScenes/MultiplayerTests/RhinoNetwork.prefab.meta   |    7 +
 Assets/_Scenes/TestScenes/MultiplayerTests/UrlGet.unity               |   90 +
 Assets/_Scripts/Game/IO/_Input Mapping/InputActionsAsset.cs           | 1214 ++++++++++++-
 Assets/_Scripts/Game/IO/_Input Mapping/InputActionsAsset.cs.meta      |   11 +-
 Assets/_Scripts/Game/IO/_Input Mapping/InputActionsAsset.inputactions |  875 +++++++++
 Assets/_Scripts/Game/IO/_Input Mapping/InputReader.cs                 |   65 +
 Assets/_Scripts/Game/IO/_Input Mapping/InputReader.cs.meta            |    2 +
 Assets/_Scripts/Game/Ship/Ship.cs                                     |    4 +-
 Assets/_Scripts/Game/Ship/TrailSpawner.cs                             |    1 -
 14 files changed, 6851 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/Ship.cs b/Assets/_Scripts/Game/Ship/Ship.cs
index c7616f51b..04c6744be 100644
--- a/Assets/_Scripts/Game/Ship/Ship.cs
+++ b/Assets/_Scripts/Game/Ship/Ship.cs
@@ -24,9 +24,7 @@ namespace CosmicShore.Core
         public List<ShipAction> ClassActions;
     }
 
-    [RequireComponent(typeof(ResourceSystem))]
-    [RequireComponent(typeof(TrailSpawner))]
-    [RequireComponent(typeof(ShipStatus))]
+
     public class Ship : NetworkBehaviour
     {
         [SerializeField] List<ImpactProperties> impactProperties;
diff --git a/Assets/_Scripts/Game/Ship/TrailSpawner.cs b/Assets/_Scripts/Game/Ship/TrailSpawner.cs
index 876c362c3..8b9f78ec5 100644
--- a/Assets/_Scripts/Game/Ship/TrailSpawner.cs
+++ b/Assets/_Scripts/Game/Ship/TrailSpawner.cs
@@ -4,7 +4,6 @@ using System.Collections;
 using System.Collections.Generic;
 using UnityEngine;
 
-[RequireComponent(typeof(Ship))]
 public class TrailSpawner : MonoBehaviour
 {
     public delegate void BlockCreationHandler(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ);
```

</details>

### `eb356eb8b` — Update prefabs and input readers

_Echo Yin, 2024-03-18 19:55:14 -0400_

```text
 .gitignore                                                            |   10 +
 Assets/_Prefabs/Spaceships/Dolphin.prefab                             |   54 -
 Assets/_Prefabs/Spaceships/Grizzly.prefab                             |   54 -
 Assets/_Prefabs/Spaceships/Manta.prefab                               |   95 -
 Assets/_Prefabs/Spaceships/Rhino.prefab                               |   54 -
 Assets/_Prefabs/Spaceships/Serpent.prefab                             |   74 -
 Assets/_Prefabs/Spaceships/Squirrel.prefab                            |   54 -
 Assets/_Prefabs/Spaceships/Termite.prefab                             |   54 -
 Assets/_Prefabs/Spaceships/Urchin.prefab                              |   54 -
 Assets/_Prefabs/UI Elements/Main Menu Screens/Main Menu Screen.prefab | 7835 ++++++++++++++++++++++++++++++-
 Assets/_Scenes/Menu_Main.unity                                        |  329 +-
 Assets/_Scripts/Game/IO/_Input Mapping/InputReader.cs                 |  106 +-
 Assets/_Scripts/Game/Ship/Ship.cs                                     |    7 +-
 Packages/manifest.json                                                |   13 +-
 Packages/packages-lock.json                                           |    7 +
 ProjectSettings/PackageManagerSettings.asset                          |   19 +-
 16 files changed, 8110 insertions(+), 709 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/Ship.cs b/Assets/_Scripts/Game/Ship/Ship.cs
index 04c6744be..85fef79be 100644
--- a/Assets/_Scripts/Game/Ship/Ship.cs
+++ b/Assets/_Scripts/Game/Ship/Ship.cs
@@ -25,7 +25,7 @@ namespace CosmicShore.Core
     }
 
 
-    public class Ship : NetworkBehaviour
+    public class Ship : MonoBehaviour
     {
         [SerializeField] List<ImpactProperties> impactProperties;
         public CameraManager CameraManager { get; private set; }
@@ -198,11 +198,6 @@ namespace CosmicShore.Core
             }
         }
 
-        public override void OnNetworkSpawn()
-        {
-            // if(IsClient)
-        }
-
         bool CheckIfUsingGamepad()
         {
             return UnityEngine.InputSystem.Gamepad.current != null;
diff --git a/Packages/manifest.json b/Packages/manifest.json
index a50259bb6..99dbfada7 100644
--- a/Packages/manifest.json
+++ b/Packages/manifest.json
@@ -30,6 +30,7 @@
     "com.unity.timeline": "1.8.6",
     "com.unity.ugui": "2.0.0",
     "com.unity.visualeffectgraph": "16.0.5",
+    "com.veriorpies.parrelsync": "https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync",
     "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.15.1",
     "com.unity.modules.accessibility": "1.0.0",
     "com.unity.modules.ai": "1.0.0",
@@ -64,15 +65,5 @@
     "com.unity.modules.wind": "1.0.0",
     "com.unity.modules.xr": "1.0.0"
   },
-  "scopedRegistries": [
-    {
-      "name": "package.openupm.com",
-      "url": "https://package.openupm.com",
-      "scopes": [
-        "com.vovgou.loxodon-framework",
-        "com.vovgou.loxodon-framework-nlog",
-        "com.vovgou.loxodon-framework-textformatting"
-      ]
-    }
-  ]
+  "scopedRegistries": []
 }
diff --git a/Packages/packages-lock.json b/Packages/packages-lock.json
index 8d27b576b..6fcafaafa 100644
--- a/Packages/packages-lock.json
+++ b/Packages/packages-lock.json
@@ -448,6 +448,13 @@
         "com.unity.render-pipelines.core": "16.0.5"
       }
     },
+    "com.veriorpies.parrelsync": {
+      "version": "https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync",
+      "depth": 0,
+      "source": "git",
+      "dependencies": {},
+      "hash": "d1ef610e9fe3ad0b490cce91594449fc0af809a6"
+    },
     "jp.hadashikick.vcontainer": {
       "version": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.15.1",
       "depth": 0,
```

</details>

### `d4c3b5205` — Add lobby handler for joining the using unity service

_Echo Yin, 2024-03-19 17:29:20 -0400_

```text
 Assets/_Prefabs/CORE/{Relay.prefab => LobbyHandler.prefab}            | 13 +++--
 Assets/_Prefabs/CORE/{Relay.prefab.meta => LobbyHandler.prefab.meta}  |  0
 Assets/_Scenes/Menu_Main.unity                                        | 14 ++---
 .../_Scripts/Multiplayer/UnityMultiplayer/ClientNetworkTransform.cs   | 13 -----
 Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby.meta               |  3 ++
 Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/EncryptionType.cs  | 27 ++++++++++
 .../Multiplayer/UnityMultiplayer/Lobby/EncryptionType.cs.meta         |  3 ++
 Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/LobbyHandler.cs    | 92 +++++++++++++++++++++++++++++++++
 .../{TestRelay.cs.meta => Lobby/LobbyHandler.cs.meta}                 |  0
 Assets/_Scripts/Multiplayer/UnityMultiplayer/Netcode.meta             |  3 ++
 .../Multiplayer/UnityMultiplayer/Netcode/ClientNetworkTransform.cs    | 20 +++++++
 .../UnityMultiplayer/{ => Netcode}/ClientNetworkTransform.cs.meta     |  0
 Assets/_Scripts/Multiplayer/UnityMultiplayer/TestRelay.cs             | 64 -----------------------
 13 files changed, 165 insertions(+), 87 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 247 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Multiplayer/UnityMultiplayer/ClientNetworkTransform.cs b/Assets/_Scripts/Multiplayer/UnityMultiplayer/ClientNetworkTransform.cs
deleted file mode 100644
index 945a4e804..000000000
--- a/Assets/_Scripts/Multiplayer/UnityMultiplayer/ClientNetworkTransform.cs
+++ /dev/null
@@ -1,13 +0,0 @@
-using Unity.Netcode.Components;
-using UnityEngine;
-
-namespace CosmicShore
-{
-    [DisallowMultipleComponent]
-    public class ClientNetworkTransform : NetworkTransform {
-        protected override bool OnIsServerAuthoritative()
-        {
-            return false;
-        }
-    }
-}
diff --git a/Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/EncryptionType.cs b/Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/EncryptionType.cs
new file mode 100644
index 000000000..b7731d07b
--- /dev/null
+++ b/Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/EncryptionType.cs
@@ -0,0 +1,27 @@
+using System;
+
+namespace CosmicShore.Multiplayer.UnityMultiplayer.Lobby
+{
+    [Serializable]
+    public enum EncryptionType
+    {
+        // Datagram Transport Layer Security
+        // UDP is a connectionless protocol, meaning it does not establish a dedicated connection before transmitting data, unlike TCP which is connection-oriented.
+        // This lack of connection in UDP makes it challenging to implement traditional TLS, which relies on the concept of sessions and a reliable transport layer.
+        // DTLS addresses this challenge by incorporating the features of TLS while accommodating the unreliable nature of UDP.
+        // It provides similar security features as TLS, including encryption, data integrity, and authentication, but it does so in a way that is suitable for datagram-based communication.
+        DTLS,
+        
+        // Web Socket Secure
+        // The WebSocket protocol itself provides a full-duplex communication channel over a single TCP connection,
+        // enabling bidirectional communication between a client and a server in real-time.
+        // However, the initial WebSocket protocol (ws://) does not provide inherent security mechanisms,
+        // leaving data transmitted over WebSocket connections vulnerable to interception and tampering.
+
+        // WebSocket Secure (WSS) addresses this security concern by layering TLS/SSL encryption over WebSocket connections.
+        // When using WSS, the WebSocket handshake process is similar to that of HTTPS,
+        // where the client and server negotiate a secure connection by exchanging cryptographic parameters and certificates during the handshake phase.
+        // Once the secure connection is established, all data exchanged between the client and the server over the WebSocket connection is encrypted and protected from eavesdropping or tampering.
+        WSS 
+    }
+}
\ No newline at end of file
diff --git a/Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/LobbyHandler.cs b/Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/LobbyHandler.cs
new file mode 100644
index 000000000..0a8608a52
--- /dev/null
+++ b/Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/LobbyHandler.cs
@@ -0,0 +1,92 @@
+using System;
+using System.Threading.Tasks;
+using CosmicShore.Integrations.Playfab.Authentication;
+using CosmicShore.Utility.Singleton;
+using Unity.Services.Authentication;
+using Unity.Services.Core;
+using UnityEngine;
+using Random = UnityEngine.Random;
+
+namespace CosmicShore.Multiplayer.UnityMultiplayer.Lobby
+{
+    
+    public class LobbyHandler : SingletonPersistent<LobbyHandler>
+    {
+        #region Properties
+
+        [Header("Lobby General Info")]
+        [SerializeField] private string lobbyName = "Lobby";
+        [SerializeField] private int maxPlayers = 4;
+        [SerializeField] private string keyJoinCode = "RelayJoinCode";
+        [SerializeField] private bool isPrivate = false;
```

</details>

### `fd89f0fd0` — Add network timer for server sync

_Echo Yin, 2024-03-19 18:31:06 -0400_

```text
 Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/LobbyHandler.cs |  3 ++-
 Assets/_Scripts/Utility/Timers.meta                                |  3 +++
 Assets/_Scripts/Utility/Timers/NetworkTimer.cs                     | 31 +++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Timers/NetworkTimer.cs.meta                |  3 +++
 Assets/_Scripts/Utility/Timers/Timer.cs                            |  7 +++++++
 Assets/_Scripts/Utility/Timers/Timer.cs.meta                       |  3 +++
 6 files changed, 49 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/LobbyHandler.cs b/Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/LobbyHandler.cs
index 0a8608a52..045392e40 100644
--- a/Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/LobbyHandler.cs
+++ b/Assets/_Scripts/Multiplayer/UnityMultiplayer/Lobby/LobbyHandler.cs
@@ -80,7 +80,8 @@ namespace CosmicShore.Multiplayer.UnityMultiplayer.Lobby
             if (!AuthenticationService.Instance.IsSignedIn)
             {
                 await AuthenticationService.Instance.SignInAnonymouslyAsync();
-                _displayName = AuthenticationService.Instance.PlayerId;
+                _playerId = AuthenticationService.Instance.PlayerId;
+                _displayName = AuthenticationService.Instance.PlayerName;
             }
         }
 
diff --git a/Assets/_Scripts/Utility/Timers/NetworkTimer.cs b/Assets/_Scripts/Utility/Timers/NetworkTimer.cs
new file mode 100644
index 000000000..26bf8e41b
--- /dev/null
+++ b/Assets/_Scripts/Utility/Timers/NetworkTimer.cs
@@ -0,0 +1,31 @@
+﻿namespace CosmicShore.Utility.Timers
+{
+    public class NetworkTimer
+    {
+        private float _timer;
+        public float ServerDeltaTime { get; }
+        public int CurrentTick { get; private set; }
+
+        public NetworkTimer(float serverTickRate)
+        {
+            ServerDeltaTime = 1.0f / serverTickRate;
+        }
+
+        public void Update(float deltaTime)
+        {
+            _timer += deltaTime;
+        }
+
+        public bool ShouldTick()
+        {
+            if (_timer >= ServerDeltaTime)
+            {
+                _timer -= ServerDeltaTime;
+                CurrentTick++;
+                return true;
+            }
+
+            return false;
+        }
+    }
+}
\ No newline at end of file
diff --git a/Assets/_Scripts/Utility/Timers/Timer.cs b/Assets/_Scripts/Utility/Timers/Timer.cs
new file mode 100644
index 000000000..e3543ec53
--- /dev/null
+++ b/Assets/_Scripts/Utility/Timers/Timer.cs
@@ -0,0 +1,7 @@
+﻿namespace CosmicShore.Utility.Timers
+{
+    public class Timer
+    {
+        
+    }
+}
\ No newline at end of file
```

</details>

### `21ce518eb` — fmod project

_simoesnm, 2024-04-24 11:13:07 +0100_

```text
 .../Metadata/EventFolder/{994c8b52-190e-41c3-b02b-4e5c660e2a99}.xml   |  14 ++
 .../Metadata/EventFolder/{fa9e02e1-0af8-4826-ab92-3e41fe66b88d}.xml   |   8 +
 .../Metadata/Group/{011bab65-1b76-42fc-b5ca-a70e9d3fb744}.xml         |  24 ++
 .../Metadata/Group/{5d196af1-43e4-42d0-982f-59d7f1831f54}.xml         |  45 ++++
 .../Metadata/Group/{6353bf48-1894-4134-b44a-6805a437f800}.xml         |  26 +++
 .../Metadata/Group/{645cc6d7-1443-414a-ab24-0a982127a5e9}.xml         |  54 +++++
 .../Metadata/Group/{6ec080b0-bb49-4dff-b1ca-e73542ed7362}.xml         |  42 ++++
 .../Metadata/Group/{ebd44021-f393-4dfa-b0d1-5ec608dd4c11}.xml         |  26 +++
 .../Metadata/Group/{f27ca79b-f929-4114-bb37-b456a8f25a77}.xml         |  53 +++++
 CosmicShore_FMODProject/Metadata/Master.xml                           |  28 +++
 CosmicShore_FMODProject/Metadata/Mixer.xml                            |  11 +
 .../ParameterPreset/{60b431dd-7a37-4155-87fe-b4a29cb26c74}.xml        |  36 +++
 .../ParameterPreset/{70764633-c12f-4292-8f20-7b9a2a19ec2e}.xml        |  33 +++
 .../ParameterPresetFolder/{7fdef1a1-3cc3-47d0-a5f2-ef4c23d07104}.xml  |   4 +
 .../Metadata/Platform/{c3d47388-4085-43fd-9c58-aeb3b1a79bb1}.xml      |  17 ++
 .../ProfilerFolder/{c543cc10-6177-4c79-bcc1-334be9320aa9}.xml         |   4 +
 .../{175e3c0f-44a9-4099-8a5b-944f7d11ee06}/session.graphdata          | Bin 0 -> 680 bytes
 .../{175e3c0f-44a9-4099-8a5b-944f7d11ee06}.xml                        | 180 +++++++++++++++
 .../Metadata/Return/{69bf5510-6d06-40f0-955f-4ea648a2c8eb}.xml        |  36 +++
 .../Metadata/SandboxFolder/{9ac3d082-7143-4272-b7ca-4897c83b8dd6}.xml |   4 +
 .../Metadata/SandboxScene/{52fa0510-b194-4241-9184-be28cbef26e4}.xml  |  91 ++++++++
 .../Metadata/Snapshot/{37022dc7-6018-41cd-9dc7-4293b7f00ee4}.xml      | 115 ++++++++++
 .../Metadata/Snapshot/{e3cbc610-f409-423e-b52d-0dd68d9452ed}.xml      |  76 +++++++
 .../Metadata/Snapshot/{eddf0604-4624-488a-9161-db5ec7a907a2}.xml      | 377 +++++++++++++++++++++++++++++++
 .../Metadata/SnapshotGroup/{9ee4206f-b779-41d9-a295-2de06de2ebdd}.xml |  13 ++
 .../Metadata/SnapshotGroup/{c9e7f01d-c932-4d2f-a577-55cb594b4c8f}.xml |   8 +
 .../Metadata/SnapshotGroup/{ff2e7e9c-f6ba-4702-87cd-d79f707e72e8}.xml |  12 +
 CosmicShore_FMODProject/Metadata/Tags.xml                             |   8 +
 CosmicShore_FMODProject/Metadata/Workspace.xml                        |  36 +++
 1179 files changed, 9931 insertions(+)
```

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
