# Branch archive: `claude/add-swinging-vessel-iHrfl`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-02-25 by Claude
- **Unmerged commits:** 11
- **Forked from:** `d55e2a687` (2026-02-21, fix squirrel hitting own rings)
- **Tip:** `785dfe684`
- **Files touched (42):**
  - `Assets/_Prefabs/Spaceships/Spider.prefab`
  - `Assets/_Prefabs/Spaceships/Spider.prefab.meta`
  - `Assets/_SO_Assets/Camera/SpiderCameraSettingsSO.asset`
  - `Assets/_SO_Assets/Camera/SpiderCameraSettingsSO.asset.meta`
  - `Assets/_SO_Assets/Captains/Arcade/SO_Captain_Arcade_Default_Spider.asset`
  - `Assets/_SO_Assets/Captains/Arcade/SO_Captain_Arcade_Default_Spider.asset.meta`
  - `Assets/_SO_Assets/Captains/Arcade/SO_Captain_Arcade_Freestyle_Spider.asset`
  - `Assets/_SO_Assets/Captains/Arcade/SO_Captain_Arcade_Freestyle_Spider.asset.meta`
  - `Assets/_SO_Assets/Captains/Elemental/ElementalCaptains.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Charge.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Charge.asset.meta`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Mass.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Mass.asset.meta`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Space.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Space.asset.meta`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Time.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Time.asset.meta`
  - `Assets/_SO_Assets/Cinematics/VesselIconLibrary.asset`
  - `Assets/_SO_Assets/Classes/SO_Class_Spider.asset`
  - `Assets/_SO_Assets/Classes/SO_Class_Spider.asset.meta`
  - `Assets/_SO_Assets/Classes/SO_Classlist_All.asset`
  - `Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset`
  - `Assets/_SO_Assets/Games/ArcadeGameFreestyle.asset`
  - `Assets/_SO_Assets/Games/ArcadeGameHexRace.asset`
  - `Assets/_SO_Assets/Games/ArcadeGameMultiplayerCrystalCapture.asset`
  - `Assets/_SO_Assets/HUD/SpiderSilhouetteConfig.asset`
  - `Assets/_SO_Assets/HUD/SpiderSilhouetteConfig.asset.meta`
  - `Assets/_SO_Assets/ShipActions/Spider.meta`
  - `Assets/_SO_Assets/ShipActions/Spider/SwingAction.asset`
  - `Assets/_SO_Assets/ShipActions/Spider/SwingAction.asset.meta`
  - `Assets/_SO_Assets/Vessel Prefab Container.asset`
  - `Assets/_Scripts/Game/Prisms/PrismFactory.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/SwingActionSO.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/SwingActionSO.cs.meta`
  - `Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs`
  - `Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs.meta`
  - `Assets/_Scripts/Models/Enums/ShipActions.cs`
  - `Assets/_Scripts/Models/Enums/VesselClassType.cs`
  - `Assets/_Scripts/VesselHUD/Controller/SpiderVesselHUDController.cs`
  - `Assets/_Scripts/VesselHUD/Controller/SpiderVesselHUDController.cs.meta`
  - … and 2 more

### `b5473c367` — Add Froglet vessel class with Spider-Man-style swinging mechanics

_Claude, 2026-02-21 21:54:32 +0000_

```text
Introduces a new "Froglet" vessel type that swings between prisms like
a pendulum. Key components:

- SwingingVesselTransformer: extends VesselTransformer with pendulum
  physics — gravity, rope constraints, tangential steering, and
  momentum-based fling on release. Auto-spawns an anchor prism via
  the event channel when no nearby prism is found.
- SwingActionSO: ScriptableObject action that triggers StartSwing on
  button press and ReleaseSwing on release.
- Froglet added to VesselClassType enum (12) and PrismType enum.
- PrismFactory updated with frogletPrismPool slot and spawn handler.
```

```text
 Assets/_Scripts/Game/Prisms/PrismFactory.cs                           |  19 ++-
 .../_Scripts/Game/Ship/R_ShipActions/Data Containers/SwingActionSO.cs |  19 +++
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs                | 253 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Models/Enums/VesselClassType.cs                       |   1 +
 4 files changed, 290 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 321 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Prisms/PrismFactory.cs b/Assets/_Scripts/Game/Prisms/PrismFactory.cs
index ce0bdacec..a61f51d9a 100644
--- a/Assets/_Scripts/Game/Prisms/PrismFactory.cs
+++ b/Assets/_Scripts/Game/Prisms/PrismFactory.cs
@@ -15,7 +15,8 @@ namespace CosmicShore.Game
         Interactive,
         Explosion,
         Implosion,
-        Grow
+        Grow,
+        Froglet
     }
     
     public class PrismFactory : MonoBehaviour
@@ -31,7 +32,8 @@ namespace CosmicShore.Game
         [SerializeField] private InteractivePrismPoolManager squirrelPrismPool;
         [SerializeField] private InteractivePrismPoolManager rhinoPrismPool;
         [SerializeField] private InteractivePrismPoolManager interactivePrismPool;
-        
+        [SerializeField] private InteractivePrismPoolManager frogletPrismPool;
+
         [SerializeField] private PrismExplosionPoolManager explosionPool;
         [SerializeField] private PrismImplosionPoolManager implosionPool;
         // Add more later: PrismShockwavePoolManager, PrismDisintegrationPoolManager, etc.
@@ -107,6 +109,10 @@ namespace CosmicShore.Game
                     spawned = SpawnGrow(data);
                     break;
 
+                case PrismType.Froglet:
+                    spawned = SpawnFrogletPrism(data);
+                    break;
+
                 // Add more cases here later
                 // case "Shockwave":
                 //     spawned = SpawnShockwave(data.OwnTeam, data.Position, data.Rotation);
@@ -168,6 +174,15 @@ namespace CosmicShore.Game
             var prism = rhinoPrismPool.Get(data.SpawnPosition, data.Rotation, rhinoPrismPool.transform);
             return prism ? prism.gameObject : null;
         }
+
+        GameObject SpawnFrogletPrism(PrismEventData data)
+        {
+            // Falls back to interactive pool if no dedicated froglet pool is set
+            var pool = frogletPrismPool != null ? frogletPrismPool : interactivePrismPool;
+            if (pool == null) { Debug.LogWarning("[PrismFactory] No pool available for Froglet prism."); return null; }
+            var prism = pool.Get(data.SpawnPosition, data.Rotation, pool.transform);
+            return prism ? prism.gameObject : null;
+        }
         
         GameObject SpawnExplosion(PrismEventData data)
         {
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
new file mode 100644
index 000000000..85592d9e9
--- /dev/null
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -0,0 +1,253 @@
+using UnityEngine;
+using CosmicShore.Core;
+using CosmicShore.Game;
+using CosmicShore.Utilities;
+
+/// <summary>
+/// Vessel transformer that adds Spider-Man-style swinging mechanics.
+/// When swinging is activated, the vessel latches onto the nearest prism
+/// (or spawns one if none exist) and swings around it like a pendulum.
+/// Releasing the swing flings the vessel forward with accumulated momentum.
+/// </summary>
+public class SwingingVesselTransformer : VesselTransformer
+{
+    [Header("Swing Settings")]
+    [SerializeField] float swingSearchRadius = 150f;
+    [SerializeField] float ropeLength = 40f;
+    [SerializeField] float swingGravity = 30f;
+    [SerializeField] float swingDamping = 0.98f;
+    [SerializeField] float releaseBoostMultiplier = 2f;
+    [SerializeField] float minFlingSpeed = 20f;
+    [SerializeField] float steerTorque = 15f;
+
+    [Header("Anchor Prism Spawning")]
```

</details>

### `ed11741c0` — Rename Froglet vessel class to Spider

_Claude, 2026-02-21 22:47:45 +0000_

```text
 Assets/_Scripts/Game/Prisms/PrismFactory.cs     | 16 ++++++++--------
 Assets/_Scripts/Models/Enums/VesselClassType.cs |  2 +-
 2 files changed, 9 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Prisms/PrismFactory.cs b/Assets/_Scripts/Game/Prisms/PrismFactory.cs
index a61f51d9a..0b01a621b 100644
--- a/Assets/_Scripts/Game/Prisms/PrismFactory.cs
+++ b/Assets/_Scripts/Game/Prisms/PrismFactory.cs
@@ -16,7 +16,7 @@ namespace CosmicShore.Game
         Explosion,
         Implosion,
         Grow,
-        Froglet
+        Spider
     }
     
     public class PrismFactory : MonoBehaviour
@@ -32,7 +32,7 @@ namespace CosmicShore.Game
         [SerializeField] private InteractivePrismPoolManager squirrelPrismPool;
         [SerializeField] private InteractivePrismPoolManager rhinoPrismPool;
         [SerializeField] private InteractivePrismPoolManager interactivePrismPool;
-        [SerializeField] private InteractivePrismPoolManager frogletPrismPool;
+        [SerializeField] private InteractivePrismPoolManager spiderPrismPool;
 
         [SerializeField] private PrismExplosionPoolManager explosionPool;
         [SerializeField] private PrismImplosionPoolManager implosionPool;
@@ -109,8 +109,8 @@ namespace CosmicShore.Game
                     spawned = SpawnGrow(data);
                     break;
 
-                case PrismType.Froglet:
-                    spawned = SpawnFrogletPrism(data);
+                case PrismType.Spider:
+                    spawned = SpawnSpiderPrism(data);
                     break;
 
                 // Add more cases here later
@@ -175,11 +175,11 @@ namespace CosmicShore.Game
             return prism ? prism.gameObject : null;
         }
 
-        GameObject SpawnFrogletPrism(PrismEventData data)
+        GameObject SpawnSpiderPrism(PrismEventData data)
         {
-            // Falls back to interactive pool if no dedicated froglet pool is set
-            var pool = frogletPrismPool != null ? frogletPrismPool : interactivePrismPool;
-            if (pool == null) { Debug.LogWarning("[PrismFactory] No pool available for Froglet prism."); return null; }
+            // Falls back to interactive pool if no dedicated spider pool is set
+            var pool = spiderPrismPool != null ? spiderPrismPool : interactivePrismPool;
+            if (pool == null) { Debug.LogWarning("[PrismFactory] No pool available for Spider prism."); return null; }
             var prism = pool.Get(data.SpawnPosition, data.Rotation, pool.transform);
             return prism ? prism.gameObject : null;
         }
diff --git a/Assets/_Scripts/Models/Enums/VesselClassType.cs b/Assets/_Scripts/Models/Enums/VesselClassType.cs
index 3cc22c398..62bc826d5 100644
--- a/Assets/_Scripts/Models/Enums/VesselClassType.cs
+++ b/Assets/_Scripts/Models/Enums/VesselClassType.cs
@@ -18,5 +18,5 @@ public enum VesselClassType
     Falcon = 9,
     Shrike = 10,
     Sparrow = 11,
-    Froglet = 12,
+    Spider = 12,
 }
\ No newline at end of file
```

</details>

### `4aba0ddec` — Add Swing action enum value and Spider HUD controller/view

_Claude, 2026-02-21 23:28:31 +0000_

```text
- Add Swing = 21 to ShipActions enum
- Create SpiderVesselHUDController with swing state tracking
- Create SpiderVesselHUDView with swing indicator display
```

```text
 Assets/_Scripts/Models/Enums/ShipActions.cs                       |  3 ++-
 Assets/_Scripts/VesselHUD/Controller/SpiderVesselHUDController.cs | 37 +++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/VesselHUD/View/SpiderVesselHUDView.cs             | 30 ++++++++++++++++++++++++++++++
 3 files changed, 69 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 92 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Models/Enums/ShipActions.cs b/Assets/_Scripts/Models/Enums/ShipActions.cs
index deeb90dbd..117f14379 100644
--- a/Assets/_Scripts/Models/Enums/ShipActions.cs
+++ b/Assets/_Scripts/Models/Enums/ShipActions.cs
@@ -21,5 +21,6 @@ public enum ShipActions
     SpeedTubes = 17,
     Bouncy = 18,
     MachCone = 19,
-    ExplosiveAcorn = 20
+    ExplosiveAcorn = 20,
+    Swing = 21
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/VesselHUD/Controller/SpiderVesselHUDController.cs b/Assets/_Scripts/VesselHUD/Controller/SpiderVesselHUDController.cs
new file mode 100644
index 000000000..ae9cd5000
--- /dev/null
+++ b/Assets/_Scripts/VesselHUD/Controller/SpiderVesselHUDController.cs
@@ -0,0 +1,37 @@
+using UnityEngine;
+
+namespace CosmicShore.Game
+{
+    public sealed class SpiderVesselHUDController : VesselHUDController
+    {
+        [Header("View")]
+        [SerializeField] private SpiderVesselHUDView view;
+
+        private IVesselStatus _vesselStatus;
+        private SwingingVesselTransformer _swinger;
+
+        private bool IsHudAllowed =>
+            _vesselStatus is { IsInitializedAsAI: false, IsLocalUser: true };
+
+        public override void Initialize(IVesselStatus vesselStatus)
+        {
+            base.Initialize(vesselStatus);
+            _vesselStatus = vesselStatus;
+
+            if (!view)
+                view = View as SpiderVesselHUDView;
+
+            if (vesselStatus?.VesselTransformer is SwingingVesselTransformer swinger)
+                _swinger = swinger;
+
+            view?.Initialize();
+        }
+
+        void Update()
+        {
+            if (!IsHudAllowed || !view || _swinger == null) return;
+
+            view.SetSwinging(_swinger.IsSwinging);
+        }
+    }
+}
diff --git a/Assets/_Scripts/VesselHUD/View/SpiderVesselHUDView.cs b/Assets/_Scripts/VesselHUD/View/SpiderVesselHUDView.cs
new file mode 100644
index 000000000..d874f4478
--- /dev/null
+++ b/Assets/_Scripts/VesselHUD/View/SpiderVesselHUDView.cs
@@ -0,0 +1,30 @@
+using UnityEngine;
+using UnityEngine.UI;
+
+namespace CosmicShore.Game
+{
+    public class SpiderVesselHUDView : VesselHUDView
+    {
+        [Header("Spider - Swing Indicator")]
+        [SerializeField] private Image swingIndicator;
+        [SerializeField] private Color swingActiveColor = Color.cyan;
+        [SerializeField] private Color swingInactiveColor = Color.white;
+
+        public override void Initialize()
+        {
+            if (swingIndicator)
+            {
+                swingIndicator.color = swingInactiveColor;
+                swingIndicator.enabled = false;
```

</details>

### `91e4a9d34` — Add Spider vessel Unity assets, prefab, captains, and configuration

_Claude, 2026-02-22 05:06:57 +0000_

```text
Wire up the complete Spider vessel class with all required Unity assets:
- .meta files for SwingActionSO, SwingingVesselTransformer, and HUD scripts
- SwingAction.asset ScriptableObject for the swing ship action
- SO_Class_Spider.asset with vessel class definition (Class=12)
- Spider.prefab based on Squirrel with SwingingVesselTransformer,
  SpiderVesselHUDController, and SwingAction on Button1
- 5 captain SOs (Freestyle + 4 elemental variants)
- SpiderCameraSettingsSO and SpiderSilhouetteConfig
- Registered Spider in class lists, prefab container, elemental captains,
  and vessel icon library
```

```text
 Assets/_Prefabs/Spaceships/Spider.prefab.meta                         |    7 +
 Assets/_SO_Assets/Camera/SpiderCameraSettingsSO.asset                 |   26 +
 Assets/_SO_Assets/Camera/SpiderCameraSettingsSO.asset.meta            |    8 +
 .../Captains/Arcade/SO_Captain_Arcade_Freestyle_Spider.asset          |   32 +
 .../Captains/Arcade/SO_Captain_Arcade_Freestyle_Spider.asset.meta     |    8 +
 Assets/_SO_Assets/Captains/Elemental/ElementalCaptains.asset          |    4 +
 Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Charge.asset   |   31 +
 .../_SO_Assets/Captains/Elemental/SO_Captain_Spider_Charge.asset.meta |    8 +
 Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Mass.asset     |   31 +
 .../_SO_Assets/Captains/Elemental/SO_Captain_Spider_Mass.asset.meta   |    8 +
 Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Space.asset    |   31 +
 .../_SO_Assets/Captains/Elemental/SO_Captain_Spider_Space.asset.meta  |    8 +
 Assets/_SO_Assets/Captains/Elemental/SO_Captain_Spider_Time.asset     |   31 +
 .../_SO_Assets/Captains/Elemental/SO_Captain_Spider_Time.asset.meta   |    8 +
 Assets/_SO_Assets/Cinematics/VesselIconLibrary.asset                  |    4 +
 Assets/_SO_Assets/Classes/SO_Class_Spider.asset                       |   45 +
 Assets/_SO_Assets/Classes/SO_Class_Spider.asset.meta                  |    8 +
 Assets/_SO_Assets/Classes/SO_Classlist_All.asset                      |    1 +
 Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset                  |    1 +
 Assets/_SO_Assets/HUD/SpiderSilhouetteConfig.asset                    |   30 +
 Assets/_SO_Assets/HUD/SpiderSilhouetteConfig.asset.meta               |    8 +
 Assets/_SO_Assets/ShipActions/Spider.meta                             |    8 +
 Assets/_SO_Assets/ShipActions/Spider/SwingAction.asset                |   14 +
 Assets/_SO_Assets/ShipActions/Spider/SwingAction.asset.meta           |    8 +
 Assets/_SO_Assets/Vessel Prefab Container.asset                       |    1 +
 .../Game/Ship/R_ShipActions/Data Containers/SwingActionSO.cs.meta     |    3 +
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs.meta           |    3 +
 .../_Scripts/VesselHUD/Controller/SpiderVesselHUDController.cs.meta   |    3 +
 Assets/_Scripts/VesselHUD/View/SpiderVesselHUDView.cs.meta            |    3 +
 30 files changed, 4240 insertions(+)
```

### `62af61fb6` — Fix SwingingVesselTransformer compile error and wire Spider into game modes

_Claude, 2026-02-22 06:09:43 +0000_

```text
Fix namespace import (CosmicShore.Utilities -> CosmicShore.Utility) to resolve
SafeLookRotation CS0103 errors. Add Spider captain to Hex Race, Freestyle, and
Crystal Capture game assets. Create Default Spider arcade captain asset. Update
SO_Class_Spider with game references.
```

```text
 .../_SO_Assets/Captains/Arcade/SO_Captain_Arcade_Default_Spider.asset  | 32 ++++++++++++++++++++++++++++++++
 .../Captains/Arcade/SO_Captain_Arcade_Default_Spider.asset.meta        |  8 ++++++++
 Assets/_SO_Assets/Classes/SO_Class_Spider.asset                        |  5 ++++-
 Assets/_SO_Assets/Games/ArcadeGameFreestyle.asset                      |  1 +
 Assets/_SO_Assets/Games/ArcadeGameHexRace.asset                        |  1 +
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerCrystalCapture.asset      |  1 +
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs                 |  2 +-
 7 files changed, 48 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index 85592d9e9..3615bdf6b 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -1,7 +1,7 @@
 using UnityEngine;
 using CosmicShore.Core;
 using CosmicShore.Game;
-using CosmicShore.Utilities;
+using CosmicShore.Utility;
 
 /// <summary>
 /// Vessel transformer that adds Spider-Man-style swinging mechanics.
```

</details>

### `c579fc1e4` — Fix CS0246: add missing using for PrismEventChannelWithReturnSO

_Claude, 2026-02-23 19:13:57 +0000_

```text
SwingingVesselTransformer was missing `using CosmicShore.Utilities;`
which contains PrismEventChannelWithReturnSO and PrismEventData.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index 3615bdf6b..d1dfde232 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -2,6 +2,7 @@ using UnityEngine;
 using CosmicShore.Core;
 using CosmicShore.Game;
 using CosmicShore.Utility;
+using CosmicShore.Utilities;
 
 /// <summary>
 /// Vessel transformer that adds Spider-Man-style swinging mechanics.
```

</details>

### `b782eb707` — Rewrite SwingingVesselTransformer with dual-tether swinging mechanics

_Claude, 2026-02-25 00:08:01 +0000_

```text
Complete rewrite of the Spider vessel's movement system:

- Dual tether system: left/right triggers fire visible tethers (LineRenderer)
  from cursor positions. Tethers raycast for prisms; if none hit, spawn one
  at max range.

- Three movement states:
  FreeFlight: dolphin-like drift with fixed cruise speed. XDiff controls
  cursor spread (not throttle). Standard two-thumb rotation.
  SingleAnchor: one trigger held, vessel swings on sphere around anchor.
  That stick's X/Y drive azimuth/elevation. Other stick aims free cursor.
  DualAnchor: both triggers held, vessel constrained to intersection circle
  of two anchor spheres. YSum drives position. Vessel orients so up-forward
  plane aligns with the circle plane.

- Seamless transitions: momentum carries across state changes. Sphere/circle
  angles initialize from current position on each transition.

- Overrides RotateShip and MoveShip (not Update) so base class isActive
  guard, DecayBoost, throttle/velocity modifiers all still function.

- Subscribes to ActionHandler.OnInputEventStarted/Stopped for trigger state.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 612 ++++++++++++++++++++++++++++++++++++-----------
 1 file changed, 475 insertions(+), 137 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 712 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index d1dfde232..1529115f8 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -5,35 +5,82 @@ using CosmicShore.Utility;
 using CosmicShore.Utilities;
 
 /// <summary>
-/// Vessel transformer that adds Spider-Man-style swinging mechanics.
-/// When swinging is activated, the vessel latches onto the nearest prism
-/// (or spawns one if none exist) and swings around it like a pendulum.
-/// Releasing the swing flings the vessel forward with accumulated momentum.
+/// Spider vessel transformer: dual-tether swinging through the hypersea.
+///
+/// Controls overview:
+///   Free flight (no triggers held):
+///     Standard two-thumb flying with drift. XDiff spreads cursors instead of throttle.
+///     Speed is a fixed cruise speed.
+///
+///   Pull a trigger:
+///     Fires a visible tether from that side's cursor. If it hits a prism the tether
+///     anchors there. If it reaches max range a new prism is spawned as the anchor.
+///
+///   Hold one trigger (single anchor):
+///     That stick's two axes control position on the sphere surrounding the anchor
+///     (azimuth + elevation). The other stick controls the cursor for a second tether.
+///
+///   Hold both triggers (dual anchor):
+///     Vessel is constrained to the intersection circle of the two anchor spheres.
+///     YSum controls position along the circle. Vessel reorients so its up-forward
+///     plane aligns with the circle plane.
+///
+///   Release a trigger:
+///     Detach tether, carry swing momentum into the next state.
 /// </summary>
 public class SwingingVesselTransformer : VesselTransformer
 {
-    [Header("Swing Settings")]
-    [SerializeField] float swingSearchRadius = 150f;
-    [SerializeField] float ropeLength = 40f;
-    [SerializeField] float swingGravity = 30f;
-    [SerializeField] float swingDamping = 0.98f;
-    [SerializeField] float releaseBoostMultiplier = 2f;
-    [SerializeField] float minFlingSpeed = 20f;
-    [SerializeField] float steerTorque = 15f;
-
-    [Header("Anchor Prism Spawning")]
-    [SerializeField] float anchorSpawnDistance = 60f;
+    [Header("Tether")]
+    [SerializeField] float tetherSpeed = 300f;
+    [SerializeField] float maxTetherLength = 150f;
+    [SerializeField] float tetherWidth = 0.15f;
+
+    [Header("Swing")]
+    [SerializeField] float swingAngularSpeed = 2.5f;
+    [SerializeField] float cursorSpreadMax = 30f;
+
+    [Header("Free Flight")]
+    [SerializeField] float cruiseSpeed = 25f;
+    [SerializeField] float driftConvergeRate = 1.5f;
+
+    [Header("Anchor Prism")]
     [SerializeField] Vector3 anchorPrismScale = new(6f, 6f, 6f);
     [SerializeField] PrismEventChannelWithReturnSO prismSpawnChannel;
 
-    // Swing state
-    bool isSwinging;
-    Transform anchorTransform;
-    Vector3 swingVelocity;
-    float currentRopeLength;
+    // ---- Internal types ----
 
-    int trailBlocksLayer = -1;
+    enum SwingState { FreeFlight, SingleAnchor, DualAnchor }
+
+    struct TetherState
+    {
+        public bool triggerHeld;
+        public bool isAnchored;
+        public bool isFiring;
```

</details>

### `b63bfe687` — Add StartSwing, ReleaseSwing, and IsSwinging to SwingingVesselTransformer

_Claude, 2026-02-25 00:14:51 +0000_

```text
Fix CS1061 errors where SwingActionSO and SpiderVesselHUDController
reference these missing members.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 20 ++++++++++++++++++++
 1 file changed, 20 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index 1529115f8..698b77647 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -64,6 +64,26 @@ public class SwingingVesselTransformer : VesselTransformer
         public LineRenderer line;
     }
 
+    // ---- Public API ----
+
+    /// <summary>True when the vessel is attached to at least one anchor.</summary>
+    public bool IsSwinging => currentState != SwingState.FreeFlight;
+
+    /// <summary>Called by SwingActionSO.StartAction — enables swing mode.</summary>
+    public void StartSwing()
+    {
+        // Swing is input-driven; nothing extra needed on start.
+    }
+
+    /// <summary>Called by SwingActionSO.StopAction — releases all tethers.</summary>
+    public void ReleaseSwing()
+    {
+        ReleaseTether(ref leftTether);
+        leftTether.triggerHeld = false;
+        ReleaseTether(ref rightTether);
+        rightTether.triggerHeld = false;
+    }
+
     // ---- State ----
 
     TetherState leftTether;
```

</details>

### `a5dc8249f` — Fix cursors, tether visuals, prism lag spike, and free-flight course

_Claude, 2026-02-25 02:48:17 +0000_

```text
- Add cursor spheres that show tether aim directions during free flight,
  positioned via GetCursorDirection at configurable distance
- Replace LineRenderer tethers with capsule mesh primitives for smooth,
  clean tether visuals (configurable radius and material)
- Defer anchor prism spawn to next frame to avoid lag spike when tether
  reaches max range
- Fix free-flight course: vessel now maintains swing momentum direction
  instead of converging toward forward. Forward rotation is independent;
  only tethers change course.
- Add OnDestroy cleanup for unparented cursor/capsule GameObjects
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 189 ++++++++++++++++++++++++++++++++++++-----------
 1 file changed, 145 insertions(+), 44 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 312 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index 698b77647..711756237 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -10,7 +10,8 @@ using CosmicShore.Utilities;
 /// Controls overview:
 ///   Free flight (no triggers held):
 ///     Standard two-thumb flying with drift. XDiff spreads cursors instead of throttle.
-///     Speed is a fixed cruise speed.
+///     Speed is a fixed cruise speed. Course persists from swing momentum —
+///     only tethers change the vessel's course. Forward rotation is independent.
 ///
 ///   Pull a trigger:
 ///     Fires a visible tether from that side's cursor. If it hits a prism the tether
@@ -33,7 +34,8 @@ public class SwingingVesselTransformer : VesselTransformer
     [Header("Tether")]
     [SerializeField] float tetherSpeed = 300f;
     [SerializeField] float maxTetherLength = 150f;
-    [SerializeField] float tetherWidth = 0.15f;
+    [SerializeField] float tetherRadius = 0.08f;
+    [SerializeField] Material tetherMaterial;
 
     [Header("Swing")]
     [SerializeField] float swingAngularSpeed = 2.5f;
@@ -41,7 +43,11 @@ public class SwingingVesselTransformer : VesselTransformer
 
     [Header("Free Flight")]
     [SerializeField] float cruiseSpeed = 25f;
-    [SerializeField] float driftConvergeRate = 1.5f;
+
+    [Header("Cursor")]
+    [SerializeField] float cursorDistance = 8f;
+    [SerializeField] float cursorSize = 0.5f;
+    [SerializeField] Material cursorMaterial;
 
     [Header("Anchor Prism")]
     [SerializeField] Vector3 anchorPrismScale = new(6f, 6f, 6f);
@@ -61,7 +67,8 @@ public class SwingingVesselTransformer : VesselTransformer
         public float extension;
         public Vector3 fireOrigin;
         public Vector3 fireDirection;
-        public LineRenderer line;
+        public Transform capsule;
+        public MeshRenderer capsuleRenderer;
     }
 
     // ---- Public API ----
@@ -100,6 +107,14 @@ public class SwingingVesselTransformer : VesselTransformer
     // Momentum tracking across state transitions
     Vector3 lastVelocity;
 
+    // Cursors
+    Transform leftCursor;
+    Transform rightCursor;
+
+    // Deferred anchor spawns (spread prism creation across frames)
+    Vector3? pendingLeftSpawnPos;
+    Vector3? pendingRightSpawnPos;
+
     int trailBlocksLayer = -1;
     int TrailBlocksLayer
     {
@@ -117,8 +132,15 @@ public class SwingingVesselTransformer : VesselTransformer
     {
         base.Initialize(vessel);
 
-        leftTether.line = CreateTetherLine("LeftTether");
-        rightTether.line = CreateTetherLine("RightTether");
+        CreateTetherCapsule("LeftTether", ref leftTether);
+        CreateTetherCapsule("RightTether", ref rightTether);
+
+        leftCursor = CreateCursor("LeftCursor");
+        rightCursor = CreateCursor("RightCursor");
+
+        // Initialize course to forward so there is a sane default
+        if (VesselStatus != null)
+            VesselStatus.Course = transform.forward;
 
         var handler = VesselStatus?.ActionHandler;
         if (handler != null)
```

</details>

### `f5bebd1b0` — Decouple free-flight course from forward rotation

_Claude, 2026-02-25 03:45:18 +0000_

```text
The vessel's movement direction during free flight is now stored in
an independent freeFlightCourse vector that only changes via swing
momentum when releasing tethers. Joystick rotation controls the
vessel's facing direction but no longer steers its trajectory.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 37 ++++++++++++++++++++++++-------------
 1 file changed, 24 insertions(+), 13 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 82 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index 711756237..c84fa979a 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -9,9 +9,11 @@ using CosmicShore.Utilities;
 ///
 /// Controls overview:
 ///   Free flight (no triggers held):
-///     Standard two-thumb flying with drift. XDiff spreads cursors instead of throttle.
-///     Speed is a fixed cruise speed. Course persists from swing momentum —
-///     only tethers change the vessel's course. Forward rotation is independent.
+///     Standard two-thumb flying. XDiff spreads cursors instead of throttle.
+///     Speed is a fixed cruise speed. Course (movement direction) is fully
+///     decoupled from forward — joystick rotation only changes the vessel's
+///     facing direction, not its trajectory. Only tethers change course
+///     (via swing momentum on release).
 ///
 ///   Pull a trigger:
 ///     Fires a visible tether from that side's cursor. If it hits a prism the tether
@@ -107,6 +109,10 @@ public class SwingingVesselTransformer : VesselTransformer
     // Momentum tracking across state transitions
     Vector3 lastVelocity;
 
+    // Free-flight course direction — fully independent from transform.forward.
+    // Only tethers (swing momentum) change this; joystick rotation does not.
+    Vector3 freeFlightCourse;
+
     // Cursors
     Transform leftCursor;
     Transform rightCursor;
@@ -138,9 +144,12 @@ public class SwingingVesselTransformer : VesselTransformer
         leftCursor = CreateCursor("LeftCursor");
         rightCursor = CreateCursor("RightCursor");
 
-        // Initialize course to forward so there is a sane default
+        // Initialize course to forward so there is a sane default.
+        // freeFlightCourse is the authoritative direction during free flight —
+        // fully decoupled from transform.forward so rotation doesn't steer.
+        freeFlightCourse = transform.forward;
         if (VesselStatus != null)
-            VesselStatus.Course = transform.forward;
+            VesselStatus.Course = freeFlightCourse;
 
         var handler = VesselStatus?.ActionHandler;
         if (handler != null)
@@ -317,14 +326,14 @@ public class SwingingVesselTransformer : VesselTransformer
         switch (next)
         {
             case SwingState.FreeFlight:
-                // Carry swing momentum
+                // Carry swing momentum into free-flight course.
                 if (lastVelocity.sqrMagnitude > 0.01f)
                 {
-                    VesselStatus.Course = lastVelocity.normalized;
+                    freeFlightCourse = lastVelocity.normalized;
                     speed = lastVelocity.magnitude;
-                    if (SafeLookRotation.TryGet(lastVelocity.normalized, out var rot, this, logError: false))
-                        accumulatedRotation = rot;
                 }
+                // Sync VesselStatus.Course for external consumers.
+                VesselStatus.Course = freeFlightCourse;
                 break;
 
             case SwingState.SingleAnchor:
@@ -505,11 +514,13 @@ public class SwingingVesselTransformer : VesselTransformer
 
         VesselStatus.Speed = speed;
 
-        // Course persists from swing momentum — only tethers change it.
-        // Forward rotation is independent (controlled by RotateShip).
+        // Use freeFlightCourse — fully independent from transform.forward.
+        // Only tethers (swing momentum) change this direction.
+        // Forward rotation is cosmetic only (controlled by RotateShip).
+        VesselStatus.Course = freeFlightCourse;
 
-        transform.position += (speed * VesselStatus.Course + velocityShift) * Time.deltaTime;
-        lastVelocity = speed * VesselStatus.Course;
+        transform.position += (speed * freeFlightCourse + velocityShift) * Time.deltaTime;
+        lastVelocity = speed * freeFlightCourse;
     }
```

</details>

### `785dfe684` — Add screen-space crosshair UI and brighten tether capsules

_Claude, 2026-02-25 10:08:52 +0000_

```text
Replace the invisible 3D world-space cursor spheres with screen-space
crosshair indicators (+ shape) that project from the vessel's cursor
direction onto the screen. Each crosshair hides when its tether fires
or anchors.

Tether capsules now default to a bright cyan unlit material when no
tetherMaterial is assigned, so they're clearly visible.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 113 +++++++++++++++++++++++++++++++++++------------
 1 file changed, 84 insertions(+), 29 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 178 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index c84fa979a..e4bfc2eda 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -1,4 +1,5 @@
 using UnityEngine;
+using UnityEngine.UI;
 using CosmicShore.Core;
 using CosmicShore.Game;
 using CosmicShore.Utility;
@@ -46,10 +47,11 @@ public class SwingingVesselTransformer : VesselTransformer
     [Header("Free Flight")]
     [SerializeField] float cruiseSpeed = 25f;
 
-    [Header("Cursor")]
+    [Header("Crosshair")]
     [SerializeField] float cursorDistance = 8f;
-    [SerializeField] float cursorSize = 0.5f;
-    [SerializeField] Material cursorMaterial;
+    [SerializeField] float crosshairSize = 32f;
+    [SerializeField] float crosshairThickness = 3f;
+    [SerializeField] Color crosshairColor = new Color(0f, 1f, 1f, 0.85f);
 
     [Header("Anchor Prism")]
     [SerializeField] Vector3 anchorPrismScale = new(6f, 6f, 6f);
@@ -113,9 +115,10 @@ public class SwingingVesselTransformer : VesselTransformer
     // Only tethers (swing momentum) change this; joystick rotation does not.
     Vector3 freeFlightCourse;
 
-    // Cursors
-    Transform leftCursor;
-    Transform rightCursor;
+    // Screen-space crosshair UI
+    Canvas crosshairCanvas;
+    RectTransform leftCrosshair;
+    RectTransform rightCrosshair;
 
     // Deferred anchor spawns (spread prism creation across frames)
     Vector3? pendingLeftSpawnPos;
@@ -141,8 +144,7 @@ public class SwingingVesselTransformer : VesselTransformer
         CreateTetherCapsule("LeftTether", ref leftTether);
         CreateTetherCapsule("RightTether", ref rightTether);
 
-        leftCursor = CreateCursor("LeftCursor");
-        rightCursor = CreateCursor("RightCursor");
+        CreateCrosshairUI();
 
         // Initialize course to forward so there is a sane default.
         // freeFlightCourse is the authoritative direction during free flight —
@@ -173,8 +175,7 @@ public class SwingingVesselTransformer : VesselTransformer
     {
         if (leftTether.capsule) Destroy(leftTether.capsule.gameObject);
         if (rightTether.capsule) Destroy(rightTether.capsule.gameObject);
-        if (leftCursor) Destroy(leftCursor.gameObject);
-        if (rightCursor) Destroy(rightCursor.gameObject);
+        if (crosshairCanvas) Destroy(crosshairCanvas.gameObject);
     }
 
     void CreateTetherCapsule(string childName, ref TetherState tether)
@@ -189,27 +190,67 @@ public class SwingingVesselTransformer : VesselTransformer
         var mr = go.GetComponent<MeshRenderer>();
         if (tetherMaterial != null)
             mr.sharedMaterial = tetherMaterial;
+        else
+        {
+            // Bright unlit default so tethers are clearly visible without a material assigned
+            var shader = Shader.Find("Universal Render Pipeline/Unlit")
+                      ?? Shader.Find("Unlit/Color");
+            if (shader != null)
+            {
+                var mat = new Material(shader);
+                var bright = new Color(0f, 1f, 1f, 1f);
+                mat.color = bright;
+                mat.SetColor("_BaseColor", bright);
+                mr.material = mat;
+            }
+        }
 
         mr.enabled = false;
         tether.capsule = go.transform;
```

</details>
