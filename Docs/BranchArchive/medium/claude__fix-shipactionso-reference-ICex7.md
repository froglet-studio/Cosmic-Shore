# Branch archive: `claude/fix-shipactionso-reference-ICex7`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-04-29 by Claude
- **Unmerged commits:** 5
- **Forked from:** `bf0484610` (2026-04-25, update capsule membrane and camera configuration)
- **Tip:** `aa75f8de3`
- **Files touched (8):**
  - `Assets/_Prefabs/Spacevessels/Falcon.prefab`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameWildlifeBlitz.unity`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs`
  - `Assets/_Scripts/System/AppManager.cs`
  - `Assets/_Scripts/UI/View/FalconHUDVessel.cs`
  - `Assets/_Scripts/UI/View/FalconHUDVessel.cs.meta`

### `ca1861bb4` — Commit 4/1/26

_Philip Appoh, 2026-04-29 12:31:04 -0500_

```text
 Assets/_Prefabs/Spacevessels/Falcon.prefab                            | 1665 +++++++++++++++++++------------
 .../Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs   |   65 ++
 Assets/_Scripts/UI/View/FalconHUDVessel.cs                            |   15 +
 Assets/_Scripts/UI/View/FalconHUDVessel.cs.meta                       |    2 +
 4 files changed, 1108 insertions(+), 639 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/View/FalconHUDVessel.cs b/Assets/_Scripts/UI/View/FalconHUDVessel.cs
new file mode 100644
index 000000000..a2e30a48c
--- /dev/null
+++ b/Assets/_Scripts/UI/View/FalconHUDVessel.cs
@@ -0,0 +1,15 @@
+using CosmicShore.Game;
+using System.Linq;
+using UnityEngine;
+
+namespace CosmicShore
+{
+    public class FalconHudView : VesselHUDView
+    {
+        // Start is called once before the first execution of Update after the MonoBehaviour is created
+        public override void Initialize()
+        {
+          
+        }
+    }
+}
```

</details>

### `86eaa5a8e` — fix(falcon): add missing CosmicShore.Gameplay using directive to FalconModeSwitchingFireSO

_Claude, 2026-04-29 17:44:25 +0000_

```text
Resolves CS0246: ShipActionSO is defined in CosmicShore.Gameplay namespace
but was referenced without the corresponding using directive.
```

```text
 Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs | 3 ++-
 1 file changed, 2 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff

```

</details>

### `4b0656cbd` — fix(falcon): add missing CosmicShore.UI using directive to FalconHUDVessel

_Claude, 2026-04-29 17:47:29 +0000_

```text
Resolves CS0246: VesselHUDView is defined in CosmicShore.UI namespace
but was not imported.
```

```text
 Assets/_Scripts/UI/View/FalconHUDVessel.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/View/FalconHUDVessel.cs b/Assets/_Scripts/UI/View/FalconHUDVessel.cs
index a2e30a48c..676602a2d 100644
--- a/Assets/_Scripts/UI/View/FalconHUDVessel.cs
+++ b/Assets/_Scripts/UI/View/FalconHUDVessel.cs
@@ -1,4 +1,5 @@
 using CosmicShore.Game;
+using CosmicShore.UI;
 using System.Linq;
 using UnityEngine;
 
```

</details>

### `d30052b82` — fix(scenes): add ContainerScope to singleplayer game scenes

_Claude, 2026-04-29 18:02:44 +0000_

```text
MinigameFreestyle, MinigameCellularDuel, and MinigameWildlifeBlitz were
missing the Reflex ContainerScope prefab, so [Inject] fields on scene-placed
MonoBehaviours (including MiniGameControllerBase.gameData) were never
populated, causing the 'GameDataSO is not assigned!' error at runtime.
```

```text
 Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity  | 68 ++++++++++++++++++++++++++++++++++++++++
 Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity     | 68 ++++++++++++++++++++++++++++++++++++++++
 Assets/_Scenes/Singleplayer Scenes/MinigameWildlifeBlitz.unity | 68 ++++++++++++++++++++++++++++++++++++++++
 3 files changed, 204 insertions(+)
```

### `aa75f8de3` — fix(bootstrap): fix stale manager references on fast enter play mode

_Claude, 2026-04-29 18:25:51 +0000_

```text
Two related issues caused "[AppManager] GameSetting not found at injection time":

1. EnsurePersistent was adding a custom DontDestroyOnLoad MonoBehaviour
   component whose Awake might not run before Bootstrap scene unloads.
   Now calls UnityEngine.Object.DontDestroyOnLoad directly for immediate
   persistence with no timing dependency.

2. With fast enter play mode (Reload Domain disabled), a persisted
   AppManager retains _resolved=true and fake-null manager references
   from the previous play session. TryResolveManagersEarly() then skips
   re-resolution, leaving gameSetting as a destroyed object reference.
   Reset _resolved=false in Awake() so each play session gets a fresh
   manager lookup even when AppManager persists via DontDestroyOnLoad.
```

```text
 Assets/_Scripts/System/AppManager.cs | 11 +++++++++--
 1 file changed, 9 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/AppManager.cs b/Assets/_Scripts/System/AppManager.cs
index bd73ad510..a1b020392 100644
--- a/Assets/_Scripts/System/AppManager.cs
+++ b/Assets/_Scripts/System/AppManager.cs
@@ -127,6 +127,10 @@ namespace CosmicShore.Core
                 return;
             }
 
+            // Reset _resolved so a DontDestroyOnLoad AppManager that persists
+            // across editor play sessions re-resolves stale manager references.
+            _resolved = false;
+
             DontDestroyOnLoad(gameObject);
             ConfigurePlatform();
             TryResolveManagersEarly();
@@ -423,8 +427,11 @@ namespace CosmicShore.Core
 
         static void EnsurePersistent(Component component)
         {
-            if (!component.TryGetComponent<DontDestroyOnLoad>(out _))
-                component.gameObject.AddComponent<DontDestroyOnLoad>();
+            // Call Unity's DontDestroyOnLoad directly rather than adding a
+            // MonoBehaviour component, so persistence takes effect immediately
+            // (before the component's own Awake would run). This eliminates
+            // the window where Bootstrap unloads before the component persists.
+            UnityEngine.Object.DontDestroyOnLoad(component.gameObject);
         }
 
         #endregion
```

</details>
