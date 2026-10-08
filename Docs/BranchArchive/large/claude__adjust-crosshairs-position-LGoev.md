# Branch archive: `claude/adjust-crosshairs-position-LGoev`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Spider swinging vessel prototype**

A prototype for a new vessel class first called 'Froglet' and then 'Spider'. It swings through the world on two tethers, Spider-Man style, and fires the tethers at prisms through on-screen crosshairs that later became 'spinneret arms'. A lightsaber sweep slices through prisms. The branch includes a HUD, a Swing ability, prefab, captains and game-mode wiring, plus some unrelated cleanup of leftover Odin Inspector code.

- **Status:** Unique work
- **Areas:** Vessels (Spider), Vessel HUD, Abilities (Swing/tether), Prisms, Soap plugin cleanup
- **Already in bleeding-edge:** None found. bleeding-edge's VesselClassType has no Spider (12 is Scarab, 13 is Butterfly), and SwingingVesselTransformer, SwingActionSO and SpiderVesselHUD* are absent. The branch also predates the Game/ to Controller/ folder restructure.
- **Risk if deleted:** medium
- **Suggestion (2026-10-08):** keep — This is the only copy of a complete swinging-vessel prototype; if the design is ever revisited it would be costly to rebuild, though it would need a heavy port onto the current layout.

## Evidence

- **Last commit:** 2026-06-29 by Claude
- **Unmerged commits:** 28
- **Forked from:** `9db9f3980` (2026-06-29, Merge pull request #568 from froglet-studio/claude/sqr-root-optimization-s93r0)
- **Tip:** `d464d20e1`
- **Files touched (48):**
  - `Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs`
  - `Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs`
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
  - `Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs`
  - `Assets/_Scripts/Game/Managers/PrismAOERegistry.cs`
  - `Assets/_Scripts/Game/Prisms/PrismFactory.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/SwingActionSO.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/SwingActionSO.cs.meta`
  - `Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs`
  - `Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs.meta`
  - … and 8 more

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

### `0e787ad4e` — Rework spider controls: screen-space crosshairs, sphere-projected flight, circle radius adjustment

_Claude, 2026-02-26 00:19:57 +0000_

```text
Crosshairs: default to halfway between screen center and horizontal edge.
Thumbstick inward moves toward vessel, outward toward screen edge. Uses
EasedJoystickPosition for per-side screen-space positioning and
Camera.ScreenPointToRay for tether fire direction.

Single-tether: pitch/yaw/roll still rotate the vessel normally. Forward
is projected onto the sphere tangent plane. Course snaps to the projected
course on tether attach, then lerps toward projected forward each frame
(drift-like momentum feel constrained to the tether sphere).

Dual-tether: same rotation controls. Forward is projected onto the
intersection circle tangent. Angular velocity lerps toward the desired
direction (drift feel on a circle). XDiff adjusts both tether lengths
to change circle radius while keeping the center fixed: inward = up to
2x home radius, outward = toward zero.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 368 +++++++++++++++++++++++++++++------------------
 1 file changed, 230 insertions(+), 138 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 649 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index e4bfc2eda..b6ab01bed 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -10,27 +10,34 @@ using CosmicShore.Utilities;
 ///
 /// Controls overview:
 ///   Free flight (no triggers held):
-///     Standard two-thumb flying. XDiff spreads cursors instead of throttle.
-///     Speed is a fixed cruise speed. Course (movement direction) is fully
-///     decoupled from forward — joystick rotation only changes the vessel's
-///     facing direction, not its trajectory. Only tethers change course
-///     (via swing momentum on release).
+///     Standard two-thumb flying. Speed is a fixed cruise speed. Course
+///     (movement direction) is fully decoupled from forward — joystick
+///     rotation only changes the vessel's facing direction. Only tethers
+///     change course (via swing momentum on release).
 ///
 ///   Pull a trigger:
 ///     Fires a visible tether from that side's cursor. If it hits a prism the tether
 ///     anchors there. If it reaches max range a new prism is spawned as the anchor.
 ///
 ///   Hold one trigger (single anchor):
-///     That stick's two axes control position on the sphere surrounding the anchor
-///     (azimuth + elevation). The other stick controls the cursor for a second tether.
+///     Vessel is constrained to the sphere around the anchor. Pitch, yaw,
+///     and roll still rotate the vessel normally. The vessel's forward
+///     direction is projected onto the sphere's tangent plane to determine
+///     movement direction, with drift-like course lerp for momentum feel.
 ///
 ///   Hold both triggers (dual anchor):
-///     Vessel is constrained to the intersection circle of the two anchor spheres.
-///     YSum controls position along the circle. Vessel reorients so its up-forward
-///     plane aligns with the circle plane.
+///     Vessel is constrained to the intersection circle of both anchor
+///     spheres. Same rotation controls. Forward is projected onto the
+///     circle tangent. XDiff adjusts tether lengths to change the circle
+///     radius (thumbs inward = larger, outward = smaller).
 ///
 ///   Release a trigger:
 ///     Detach tether, carry swing momentum into the next state.
+///
+///   Crosshairs:
+///     Each crosshair defaults to halfway between screen center and the
+///     horizontal screen edge. Thumbstick inward moves toward vessel,
+///     outward moves toward screen edge.
 /// </summary>
 public class SwingingVesselTransformer : VesselTransformer
 {
@@ -42,13 +49,15 @@ public class SwingingVesselTransformer : VesselTransformer
 
     [Header("Swing")]
     [SerializeField] float swingAngularSpeed = 2.5f;
-    [SerializeField] float cursorSpreadMax = 30f;
+
+    [Header("Course Lerp")]
+    [Tooltip("How fast the tethered course catches up to the projected forward (drift feel).")]
+    [SerializeField] float courseLerp = 1.5f;
 
     [Header("Free Flight")]
     [SerializeField] float cruiseSpeed = 25f;
 
     [Header("Crosshair")]
-    [SerializeField] float cursorDistance = 8f;
     [SerializeField] float crosshairSize = 32f;
     [SerializeField] float crosshairThickness = 3f;
     [SerializeField] Color crosshairColor = new Color(0f, 1f, 1f, 0.85f);
@@ -81,10 +90,7 @@ public class SwingingVesselTransformer : VesselTransformer
     public bool IsSwinging => currentState != SwingState.FreeFlight;
 
     /// <summary>Called by SwingActionSO.StartAction — enables swing mode.</summary>
-    public void StartSwing()
-    {
-        // Swing is input-driven; nothing extra needed on start.
-    }
+    public void StartSwing() { }
 
     /// <summary>Called by SwingActionSO.StopAction — releases all tethers.</summary>
     public void ReleaseSwing()
@@ -101,12 +107,16 @@ public class SwingingVesselTransformer : VesselTransformer
```

</details>

### `25f939936` — Fix teardown crashes: guard disposed NativeArrays and null playerScoreContainer

_Claude, 2026-02-26 01:33:56 +0000_

```text
PrismAOERegistry: All public methods that access _spatial or _damage
NativeArrays now check IsCreated before indexing. During scene teardown,
Spindle.OnDisable cascades through LifeForm.Die → Prism.SetupDestruction
→ MarkDestroyed after the registry's OnDestroy already disposed the arrays.

MiniGameHUDView: ClearPlayerList now null-checks playerScoreContainer
before iterating. Prevents UnassignedReferenceException when the container
is not wired in the inspector (e.g. Freestyle scene HUD).
```

```text
 Assets/_Scripts/Game/Managers/PrismAOERegistry.cs   | 7 ++++++-
 Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs | 1 +
 2 files changed, 7 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs b/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
index 06b71ccba..0a7294480 100644
--- a/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
+++ b/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
@@ -194,6 +194,7 @@ namespace CosmicShore.Game
         public void Unregister(int index)
         {
             if (index < 0 || index >= _highWaterMark) return;
+            if (!_spatial.IsCreated) return;
             var s = _spatial[index];
             s.Flags = 0; // clear all flags including IsActive
             _spatial[index] = s;
@@ -204,6 +205,7 @@ namespace CosmicShore.Game
         public void MarkDestroyed(int index)
         {
             if (index < 0 || index >= _highWaterMark) return;
+            if (!_spatial.IsCreated) return;
             var s = _spatial[index];
             s.Flags |= PrismFlags.Destroyed;
             _spatial[index] = s;
@@ -212,6 +214,7 @@ namespace CosmicShore.Game
         public void UpdateShieldState(int index, bool shielded, bool superShielded)
         {
             if (index < 0 || index >= _highWaterMark) return;
+            if (!_spatial.IsCreated) return;
             var s = _spatial[index];
             // Clear shield bits, then set
             s.Flags = (byte)(s.Flags & ~(PrismFlags.IsShielded | PrismFlags.IsSuperShielded));
@@ -223,6 +226,7 @@ namespace CosmicShore.Game
         public void UpdateDomain(int index, int domain)
         {
             if (index < 0 || index >= _highWaterMark) return;
+            if (!_damage.IsCreated) return;
             var d = _damage[index];
             d.Domain = domain;
             _damage[index] = d;
@@ -234,6 +238,7 @@ namespace CosmicShore.Game
         public void UpdateVolume(int index, float volume)
         {
             if (index < 0 || index >= _highWaterMark) return;
+            if (!_damage.IsCreated) return;
             var d = _damage[index];
             d.Volume = volume;
             _damage[index] = d;
@@ -272,7 +277,7 @@ namespace CosmicShore.Game
             IVessel vessel,
             HashSet<int> alreadyHit)
         {
-            if (_highWaterMark == 0) return true;
+            if (_highWaterMark == 0 || !_spatial.IsCreated) return true;
 
             // --- Phase 1: Burst job over hot spatial data ---
             _hitIndices.Clear();
diff --git a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
index 94cdd45b6..a9e94dc3a 100644
--- a/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
+++ b/Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs
@@ -125,6 +125,7 @@ namespace CosmicShore.Game.UI
 
         public void ClearPlayerList()
         {
+            if (playerScoreContainer == null) return;
             foreach (Transform child in playerScoreContainer)
             {
                 Destroy(child.gameObject);
```

</details>

### `c3dfe9fc4` — Fix tether firing direction: raycast through crosshair to hit prisms

_Claude, 2026-02-26 01:43:31 +0000_

```text
GetCursorDirection now raycasts from the camera through the crosshair
screen position on the TrailBlocks layer. The tether aims at whatever
prism is under the crosshair rather than a fixed far point on the ray,
so nearby prisms can be targeted accurately.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 13 ++++++++++++-
 1 file changed, 12 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index b6ab01bed..f868a03e8 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -320,6 +320,9 @@ public class SwingingVesselTransformer : VesselTransformer
 
     /// <summary>
     /// World-space direction for tether firing based on crosshair screen position.
+    /// Raycasts from the camera through the crosshair into the scene so that
+    /// the tether aims at whatever prism (or geometry) is under the crosshair.
+    /// Falls back to a far point on the ray if nothing is hit.
     /// </summary>
     Vector3 GetCursorDirection(bool left)
     {
@@ -328,7 +331,15 @@ public class SwingingVesselTransformer : VesselTransformer
 
         Vector3 screenPos = GetCrosshairScreenPosition(left);
         Ray ray = cam.ScreenPointToRay(screenPos);
-        Vector3 worldTarget = ray.GetPoint(200f);
+
+        // Raycast through the crosshair — TrailBlocks layer contains prisms
+        Vector3 worldTarget;
+        int layerMask = 1 << TrailBlocksLayer;
+        if (Physics.Raycast(ray, out var hit, maxTetherLength * 2f, layerMask))
+            worldTarget = hit.point;
+        else
+            worldTarget = ray.GetPoint(maxTetherLength);
+
         Vector3 dir = worldTarget - transform.position;
         return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
     }
```

</details>

### `49c85fcf6` — Fix tether aiming: use gameplay camera from CameraManager, not Camera.main

_Claude, 2026-02-26 01:54:56 +0000_

```text
Camera.main doesn't return the active gameplay camera — the player camera
lives on "CM PlayerCam" managed by CameraManager. Added GetGameplayCamera()
that resolves the active camera via CameraManager.Instance.GetActiveController(),
falling back to Camera.main only if the manager isn't available.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 19 ++++++++++++++++---
 1 file changed, 16 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index f868a03e8..a00470580 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -2,6 +2,7 @@ using UnityEngine;
 using UnityEngine.UI;
 using CosmicShore.Core;
 using CosmicShore.Game;
+using CosmicShore.Game.CameraSystem;
 using CosmicShore.Utility;
 using CosmicShore.Utilities;
 
@@ -318,15 +319,27 @@ public class SwingingVesselTransformer : VesselTransformer
         }
     }
 
+    /// <summary>
+    /// Returns the active gameplay camera from CameraManager.
+    /// Falls back to Camera.main if the manager isn't available.
+    /// </summary>
+    Camera GetGameplayCamera()
+    {
+        var controller = CameraManager.Instance?.GetActiveController();
+        if (controller is CustomCameraController ccc)
+            return ccc.Camera;
+        return Camera.main;
+    }
+
     /// <summary>
     /// World-space direction for tether firing based on crosshair screen position.
-    /// Raycasts from the camera through the crosshair into the scene so that
-    /// the tether aims at whatever prism (or geometry) is under the crosshair.
+    /// Raycasts from the gameplay camera through the crosshair into the scene so
+    /// the tether aims at whatever prism is under the crosshair.
     /// Falls back to a far point on the ray if nothing is hit.
     /// </summary>
     Vector3 GetCursorDirection(bool left)
     {
-        var cam = Camera.main;
+        var cam = GetGameplayCamera();
         if (cam == null) return transform.forward;
 
         Vector3 screenPos = GetCrosshairScreenPosition(left);
```

</details>

### `f37ee0f8b` — Replace crosshairs with spinneret arms; remove throttle for displacement-based speed

_Claude, 2026-02-26 17:31:05 +0000_

```text
- Replace screen-space crosshair UI with world-space capsule arms extending
  from vessel to aim point. Tether fires from arm tip, eliminating parallax.
- Remove all throttle/cruise speed. Speed is purely displacement-based:
  single tether redirects without changing speed, dual-tether XDiff pumping
  is the only way to gain/lose speed.
- Tether lengths lerp between current and target during dual-anchor mode
  for smooth radius transitions.
- Angular velocity adjusts on radius change to preserve linear speed.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 414 ++++++++++++++++++++++++-----------------------
 1 file changed, 212 insertions(+), 202 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 726 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index a00470580..0a3f01312 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -1,5 +1,4 @@
 using UnityEngine;
-using UnityEngine.UI;
 using CosmicShore.Core;
 using CosmicShore.Game;
 using CosmicShore.Game.CameraSystem;
@@ -9,36 +8,39 @@ using CosmicShore.Utilities;
 /// <summary>
 /// Spider vessel transformer: dual-tether swinging through the hypersea.
 ///
+/// Speed model:
+///   The spider has NO throttle. Speed is purely displacement-based.
+///   The only way to gain speed is dual-tether pumping (XDiff changes
+///   tether lengths). Single tether redirects momentum without changing
+///   speed. Free flight coasts at current velocity.
+///
 /// Controls overview:
 ///   Free flight (no triggers held):
-///     Standard two-thumb flying. Speed is a fixed cruise speed. Course
-///     (movement direction) is fully decoupled from forward — joystick
-///     rotation only changes the vessel's facing direction. Only tethers
-///     change course (via swing momentum on release).
+///     Sticks control orientation only (pitch/yaw/roll). Vessel coasts
+///     at whatever speed it had. Course is decoupled from forward.
 ///
 ///   Pull a trigger:
-///     Fires a visible tether from that side's cursor. If it hits a prism the tether
-///     anchors there. If it reaches max range a new prism is spawned as the anchor.
+///     Fires a tether from the spinneret arm tip on that side. If it hits
+///     a prism, the tether anchors. At max range, a new prism is spawned.
 ///
 ///   Hold one trigger (single anchor):
-///     Vessel is constrained to the sphere around the anchor. Pitch, yaw,
-///     and roll still rotate the vessel normally. The vessel's forward
-///     direction is projected onto the sphere's tangent plane to determine
-///     movement direction, with drift-like course lerp for momentum feel.
+///     Vessel is constrained to the sphere around the anchor. Maintains
+///     current speed. Forward is projected onto the tangent plane. Course
+///     lerps toward projected forward (drift feel). Only direction changes.
 ///
 ///   Hold both triggers (dual anchor):
-///     Vessel is constrained to the intersection circle of both anchor
-///     spheres. Same rotation controls. Forward is projected onto the
-///     circle tangent. XDiff adjusts tether lengths to change the circle
-///     radius (thumbs inward = larger, outward = smaller).
+///     Vessel is on the intersection circle of both spheres. XDiff sets
+///     a target circle radius (inward=larger, outward=smaller). Actual
+///     radius lerps toward target. Displacement from radius change + angular
+///     motion determines speed. This is the ONLY way to change speed.
 ///
 ///   Release a trigger:
-///     Detach tether, carry swing momentum into the next state.
+///     Detach tether, carry velocity into the next state.
 ///
-///   Crosshairs:
-///     Each crosshair defaults to halfway between screen center and the
-///     horizontal screen edge. Thumbstick inward moves toward vessel,
-///     outward moves toward screen edge.
+/// Spinneret arms:
+///     World-space arms extend from the vessel toward each side's aim
+///     target. The arm tip IS the tether fire point — no parallax.
+///     Arms replace screen-space crosshairs entirely.
 /// </summary>
 public class SwingingVesselTransformer : VesselTransformer
 {
@@ -48,20 +50,19 @@ public class SwingingVesselTransformer : VesselTransformer
     [SerializeField] float tetherRadius = 0.08f;
     [SerializeField] Material tetherMaterial;
 
-    [Header("Swing")]
-    [SerializeField] float swingAngularSpeed = 2.5f;
+    [Header("Spinneret Arms")]
+    [Tooltip("Distance from vessel center to the spinneret tip.")]
+    [SerializeField] float armLength = 5f;
+    [Tooltip("Visual thickness of the arm capsule.")]
+    [SerializeField] float armRadius = 0.04f;
```

</details>

### `a2b83c9be` — Make spinneret arms extend to screen edge, controlled by XDiff

_Claude, 2026-02-26 21:30:52 +0000_

```text
- Remove fixed armLength; compute dynamically from camera frustum at
  vessel depth using FOV + aspect ratio.
- XDiff controls arm length: 0 (inward) = retracted at vessel center,
  0.5 (neutral) = halfway to screen edge, 1 (outward) = full screen edge.
- Arms always point toward their respective screen edge (left arm → left
  edge, right arm → right edge).
- GetArmDirection() projects screen-edge position onto vessel's depth
  plane for accurate world-space direction.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 136 ++++++++++++++++++++++++++++++++---------------
 1 file changed, 94 insertions(+), 42 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 198 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index 0a3f01312..476225e90 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -38,9 +38,10 @@ using CosmicShore.Utilities;
 ///     Detach tether, carry velocity into the next state.
 ///
 /// Spinneret arms:
-///     World-space arms extend from the vessel toward each side's aim
-///     target. The arm tip IS the tether fire point — no parallax.
-///     Arms replace screen-space crosshairs entirely.
+///     World-space arms extend from the vessel toward each side's screen
+///     edge. XDiff controls arm length: 0 (inward) = retracted, 0.5 (neutral)
+///     = halfway to edge, 1 (outward) = full screen edge. The arm tip IS the
+///     tether fire point — no parallax. Arms replace crosshairs entirely.
 /// </summary>
 public class SwingingVesselTransformer : VesselTransformer
 {
@@ -51,8 +52,6 @@ public class SwingingVesselTransformer : VesselTransformer
     [SerializeField] Material tetherMaterial;
 
     [Header("Spinneret Arms")]
-    [Tooltip("Distance from vessel center to the spinneret tip.")]
-    [SerializeField] float armLength = 5f;
     [Tooltip("Visual thickness of the arm capsule.")]
     [SerializeField] float armRadius = 0.04f;
 
@@ -285,41 +284,67 @@ public class SwingingVesselTransformer : VesselTransformer
     //  SPINNERET AIMING
     // ==================================================================
 
+    Camera GetGameplayCamera()
+    {
+        var controller = CameraManager.Instance?.GetActiveController();
+        if (controller is CustomCameraController ccc)
+            return ccc.Camera;
+        return Camera.main;
+    }
+
     /// <summary>
-    /// Screen-space aim position for a given side.
-    /// Neutral: halfway between screen center and horizontal edge.
-    /// Inward (toward center): moves toward vessel. Outward: toward edge.
+    /// Computes the world-space half-width of the camera frustum at the vessel's depth.
+    /// This is how far (in world units) the screen edge is from the vessel's position
+    /// projected onto the camera's horizontal plane.
     /// </summary>
-    Vector3 GetAimScreenPosition(bool left)
+    float GetScreenEdgeDistance()
     {
-        float sw = Screen.width;
-        float sh = Screen.height;
+        var cam = GetGameplayCamera();
+        if (cam == null) return 20f; // sane fallback
 
-        if (left)
-        {
-            float stickX = InputStatus?.EasedLeftJoystickPosition.x ?? 0f;
-            float x = sw * 0.25f * (stickX + 1f);
-            return new Vector3(x, sh * 0.5f, 0f);
-        }
-        else
-        {
-            float stickX = InputStatus?.EasedRightJoystickPosition.x ?? 0f;
-            float x = sw * (0.75f + 0.25f * stickX);
-            return new Vector3(x, sh * 0.5f, 0f);
-        }
+        float depth = Vector3.Dot(
+            transform.position - cam.transform.position,
+            cam.transform.forward);
+        depth = Mathf.Max(depth, 0.1f);
+
+        if (cam.orthographic)
+            return cam.orthographicSize * cam.aspect;
+
+        float halfHeight = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
+        return halfHeight * cam.aspect;
     }
 
-    Camera GetGameplayCamera()
```

</details>

### `eb82fb21c` — Implement Option B: angular momentum conservation for dual-tether pumping

_Claude, 2026-02-27 01:28:05 +0000_

```text
Physics model:
- L = omega * h² (angular momentum per unit mass) is conserved.
- Shrinking radius → omega scales as 1/h² → linear speed scales as 1/h.
  (Ice skater pulling arms in to spin faster.)
- Active contraction injects pump energy: L += pumpGain * |dh/dt| * |omega|.
  Faster spinning rewards more energy per pump, creating a skill-based
  acceleration curve where each pump cycle ratchets L upward.
- Minimum circle radius (minCircleRadius) prevents singularity at r→0.
- Player releases tethers at short radius to carry peak velocity into
  free flight.

Tuning parameters exposed in Inspector:
- pumpGain (0.5): energy injected per unit of contraction rate × angular speed
- minCircleRadius (1.0): floor radius to cap maximum angular velocity
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 101 ++++++++++++++++++++++++++---------------------
 1 file changed, 57 insertions(+), 44 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 178 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index 476225e90..a4c242a5c 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -31,8 +31,10 @@ using CosmicShore.Utilities;
 ///   Hold both triggers (dual anchor):
 ///     Vessel is on the intersection circle of both spheres. XDiff sets
 ///     a target circle radius (inward=larger, outward=smaller). Actual
-///     radius lerps toward target. Displacement from radius change + angular
-///     motion determines speed. This is the ONLY way to change speed.
+///     radius lerps toward target. Angular momentum L = omega * r² is
+///     conserved: shrinking the radius spins you faster (ice skater effect,
+///     v ∝ 1/r). Active contraction injects pump energy into L, so each
+///     pump cycle ratchets speed upward. This is the ONLY way to change speed.
 ///
 ///   Release a trigger:
 ///     Detach tether, carry velocity into the next state.
@@ -63,6 +65,12 @@ public class SwingingVesselTransformer : VesselTransformer
     [Tooltip("How fast the actual tether length lerps toward the target during dual-anchor pumping.")]
     [SerializeField] float tetherLengthLerpSpeed = 3f;
 
+    [Header("Angular Momentum (Option B)")]
+    [Tooltip("Energy injected per unit of contraction rate × angular velocity. Higher = faster speed gain per pump.")]
+    [SerializeField] float pumpGain = 0.5f;
+    [Tooltip("Minimum circle radius to prevent singularity at r→0.")]
+    [SerializeField] float minCircleRadius = 1f;
+
     [Header("Anchor Prism")]
     [SerializeField] Vector3 anchorPrismScale = new(6f, 6f, 6f);
     [SerializeField] PrismEventChannelWithReturnSO prismSpawnChannel;
@@ -114,6 +122,7 @@ public class SwingingVesselTransformer : VesselTransformer
     // Circle navigation (dual anchor)
     float circleAngle;
     float circleAngularVelocity;
+    float angularMomentum; // L = omega * h² (signed, per unit mass)
 
     // Dual-anchor geometry
     float dualAnchorA;
@@ -751,11 +760,11 @@ public class SwingingVesselTransformer : VesselTransformer
         // speed stays unchanged — single tether never changes speed
     }
 
-    // ---- Dual anchor: pump tether lengths to gain speed ----
+    // ---- Dual anchor: angular momentum conservation + pump injection ----
     //
-    // XDiff sets target circle radius. Actual radius lerps toward target.
-    // The displacement from angular motion + radius change IS the speed.
-    // This is the ONLY way the spider changes speed.
+    // L = omega * h² is conserved. Shrinking radius → omega ∝ 1/h² → v ∝ 1/h.
+    // Active contraction injects energy: L += pumpGain * |dh/dt| * |omega|.
+    // Each pump cycle ratchets L upward. Release at short radius for max speed.
 
     void DualAnchorMove()
     {
@@ -777,15 +786,39 @@ public class SwingingVesselTransformer : VesselTransformer
             return;
         }
 
-        // XDiff → target radius: inward(0)→2× home, neutral(0.5)→1× home, outward(1)→~0
+        float dt = Time.deltaTime;
+
+        // XDiff → target radius: inward(0)→2× home, neutral(0.5)→1× home, outward(1)→near zero
         float xDiff = InputStatus?.XDiff ?? 0.5f;
         float radiusMult = Mathf.Clamp(2f * (1f - xDiff), 0.05f, 2f);
-        float targetH = dualAnchorHomeH * radiusMult;
+        float targetH = Mathf.Max(dualAnchorHomeH * radiusMult, minCircleRadius);
 
         // Lerp actual radius toward target
         float oldH = currentH;
-        currentH = Mathf.Lerp(currentH, targetH, tetherLengthLerpSpeed * Time.deltaTime);
-        float h = Mathf.Max(currentH, 0.01f);
+        currentH = Mathf.Lerp(currentH, targetH, tetherLengthLerpSpeed * dt);
+        currentH = Mathf.Max(currentH, minCircleRadius);
+        float h = currentH;
+
+        // --- Angular momentum pump injection ---
+        // When the player contracts radius (dH < 0), inject energy into L.
+        // Injection scales with contraction rate × current angular speed,
+        // so faster spinning rewards more energy per pump.
+        float dH = currentH - oldH;
```

</details>

### `f99f2320a` — Remove stale ODIN_INSPECTOR defines from Android scripting symbols

_Claude, 2026-02-27 06:37:05 +0000_

```text
Odin Inspector is not installed, but its define symbols were left in the
Android platform scripting defines, causing CS0246 errors when the Soap
plugin's #if ODIN_INSPECTOR guards activated and tried to reference
Sirenix types.
```

```text
 ProjectSettings/ProjectSettings.asset | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `7f3eecf5e` — Remove dead Odin Inspector attribute blocks from Soap plugin

_Claude, 2026-02-27 06:43:00 +0000_

```text
The #if ODIN_INSPECTOR guards were activating despite Odin not being
installed (likely cached defines). Since Odin is not present in the
project, remove the conditional attribute blocks entirely to eliminate
the CS0246 Sirenix reference errors.
```

```text
 Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs                         | 3 ---
 Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs | 3 ---
 2 files changed, 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs b/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs
index ac3f8d8f4..0612ec63a 100644
--- a/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs
+++ b/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableEvents/ScriptableEvent.cs
@@ -7,9 +7,6 @@ using Object = UnityEngine.Object;
 
 namespace Obvious.Soap
 {
-#if ODIN_INSPECTOR
-    [Sirenix.OdinInspector.DrawWithUnity]
-#endif
     public abstract class ScriptableEvent<T> : ScriptableEventBase, IDrawObjectsInInspector
     {
         [Tooltip("Value used when raising the event in editor.")]
diff --git a/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs b/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs
index 00fc8cf3e..08ac30ca4 100644
--- a/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs
+++ b/Assets/Plugins/Obvious/Soap/Core/Runtime/ScriptableVariables/VariableReferences/VariableReference.cs
@@ -1,8 +1,5 @@
 ﻿namespace Obvious.Soap
 {
-#if ODIN_INSPECTOR
-    [Sirenix.OdinInspector.DrawWithUnity]
-#endif
     [System.Serializable]
     public abstract class VariableReference<V, T> where V : ScriptableVariable<T>
     {
```

</details>

### `a51aeca2b` — Fix tether fire direction: shoot away from camera, not away from vessel

_Claude, 2026-02-27 07:03:28 +0000_

```text
The tethers were aiming at the screen edge (lateral direction) because
GetSpinneretAim used GetAimTarget which raycasted through x=0 or
x=screenWidth. Changed to raycast from camera through the arm tip's
screen projection so tethers fire forward into the scene from wherever
the arm tip currently is.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 35 +++++++++++++++--------------------
 1 file changed, 15 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index a4c242a5c..15b09702f 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -351,24 +351,6 @@ public class SwingingVesselTransformer : VesselTransformer
         return new Vector3(x, sh * 0.5f, 0f);
     }
 
-    /// <summary>
-    /// Computes the world-space aim target by raycasting from the gameplay
-    /// camera through the screen-edge position on this arm's side.
-    /// </summary>
-    Vector3 GetAimTarget(bool left)
-    {
-        var cam = GetGameplayCamera();
-        if (cam == null) return transform.position + transform.forward * maxTetherLength;
-
-        Vector3 screenPos = GetAimScreenPosition(left);
-        Ray ray = cam.ScreenPointToRay(screenPos);
-
-        int layerMask = 1 << TrailBlocksLayer;
-        if (Physics.Raycast(ray, out var hit, maxTetherLength * 2f, layerMask))
-            return hit.point;
-        return ray.GetPoint(maxTetherLength);
-    }
-
     /// <summary>
     /// Computes the arm direction (unit vector) for a given side.
     /// The arm always points toward its screen edge from the vessel center.
@@ -399,6 +381,8 @@ public class SwingingVesselTransformer : VesselTransformer
     /// <summary>
     /// Computes the spinneret arm tip position and the aim target.
     /// The tip extends from vessel center toward the screen edge, scaled by XDiff.
+    /// The aim target is along the camera ray through the tip — tethers fire
+    /// "into the screen" (away from camera) from the arm tip, not laterally.
     /// </summary>
     (Vector3 tipPos, Vector3 aimTarget) GetSpinneretAim(bool left)
     {
@@ -406,8 +390,19 @@ public class SwingingVesselTransformer : VesselTransformer
         Vector3 dir = GetArmDirection(left);
         Vector3 tip = transform.position + dir * len;
 
-        Vector3 target = GetAimTarget(left);
-        return (tip, target);
+        var cam = GetGameplayCamera();
+        if (cam == null)
+            return (tip, tip + transform.forward * maxTetherLength);
+
+        // Ray from camera through the arm tip's screen projection —
+        // this sends the tether forward into the scene from the tip.
+        Vector3 tipScreen = cam.WorldToScreenPoint(tip);
+        Ray ray = cam.ScreenPointToRay(tipScreen);
+
+        int layerMask = 1 << TrailBlocksLayer;
+        if (Physics.Raycast(ray, out var hit, maxTetherLength * 2f, layerMask))
+            return (tip, hit.point);
+        return (tip, ray.GetPoint(maxTetherLength));
     }
 
     /// <summary>
```

</details>

### `72fe66627` — Fix cursor drift: pin arms to horizontal screen axis, fire from camera

_Claude, 2026-02-27 09:23:00 +0000_

```text
Replaced the world-space depth-plane intersection approach (which
drifted as the vessel moved) with a screen-space approach:
- Cursor positions are computed by lerping horizontally from the
  vessel's screen X to the screen edge, then converting back to world
  space via ScreenToWorldPoint. This locks them to a stable horizontal
  axis on screen.
- Tether fire direction is now (cursorWorldPos - cameraPos), sending
  tethers forward into the scene from the cursor position.
```

```text
 Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs | 127 ++++++++++++++---------------------------------
 1 file changed, 38 insertions(+), 89 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 171 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
index 15b09702f..9deb2dc2c 100644
--- a/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/SwingingVesselTransformer.cs
@@ -301,128 +301,77 @@ public class SwingingVesselTransformer : VesselTransformer
         return Camera.main;
     }
 
-    /// <summary>
-    /// Computes the world-space half-width of the camera frustum at the vessel's depth.
-    /// This is how far (in world units) the screen edge is from the vessel's position
-    /// projected onto the camera's horizontal plane.
-    /// </summary>
-    float GetScreenEdgeDistance()
-    {
-        var cam = GetGameplayCamera();
-        if (cam == null) return 20f; // sane fallback
-
-        float depth = Vector3.Dot(
-            transform.position - cam.transform.position,
-            cam.transform.forward);
-        depth = Mathf.Max(depth, 0.1f);
-
-        if (cam.orthographic)
-            return cam.orthographicSize * cam.aspect;
-
-        float halfHeight = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
-        return halfHeight * cam.aspect;
-    }
-
-    /// <summary>
-    /// Computes the arm length for a given side based on XDiff.
-    /// XDiff controls both arms symmetrically: 0.5 (neutral) = halfway to edge,
-    /// 0 (inward) = arms at vessel center, 1 (outward) = arms at screen edge.
-    /// </summary>
-    float GetArmLength()
-    {
-        float xDiff = InputStatus?.XDiff ?? 0.5f;
-        float edgeDist = GetScreenEdgeDistance();
-        // XDiff 0→0, 0.5→half, 1→full edge
-        return edgeDist * xDiff;
-    }
-
-    /// <summary>
-    /// Screen-space aim position for a given side — the arm points from vessel
-    /// center toward the screen edge on that side.
-    /// </summary>
-    Vector3 GetAimScreenPosition(bool left)
-    {
-        float sh = Screen.height;
-        float sw = Screen.width;
-
-        // Arms point toward their respective screen edges
-        // Left arm → left edge, right arm → right edge
-        float x = left ? 0f : sw;
-        return new Vector3(x, sh * 0.5f, 0f);
-    }
+    // ==================================================================
+    //  CURSOR POSITIONING (screen-space horizontal axis)
+    // ==================================================================
 
     /// <summary>
-    /// Computes the arm direction (unit vector) for a given side.
-    /// The arm always points toward its screen edge from the vessel center.
+    /// Computes the world-space cursor position for a given side.
+    /// Cursors are pinned to the vessel's horizontal screen axis.
+    /// XDiff controls position: 0 = at vessel, 1 = at screen edge.
     /// </summary>
-    Vector3 GetArmDirection(bool left)
+    Vector3 GetCursorWorldPosition(bool left)
     {
         var cam = GetGameplayCamera();
-        if (cam == null) return left ? -transform.right : transform.right;
-
-        Vector3 screenEdge = GetAimScreenPosition(left);
-        Ray edgeRay = cam.ScreenPointToRay(screenEdge);
-
-        // Intersect with the plane at the vessel's depth
-        Vector3 camFwd = cam.transform.forward;
-        float depth = Vector3.Dot(transform.position - cam.transform.position, camFwd);
```

</details>

### `d5ed1a2aa` — fix(spider): retract lightsaber tether on release instead of popping out

_Claude, 2026-06-21 14:24:42 +0000_

```text
Continuity-of-existence law (CLAUDE.md, newly locked upstream): nothing
player-visible may instant-appear/disappear. The tether ignite already
grows from extension=0, but release hard-disabled the capsule renderer —
a visible cyan beam popping out. Now the far end slides back to the
spinneret over tetherRetractDuration (config-driven, 0 disables); re-fire
cancels an in-flight retract.
```

```text
 Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs | 42 +++++++++++++++++++++++++++++++++++++++-
 1 file changed, 41 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 85 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs b/Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs
index 9aac40c41..70a83b3db 100644
--- a/Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs
@@ -42,6 +42,8 @@ namespace CosmicShore.Gameplay
         [SerializeField] float maxTetherLength = 150f;
         [SerializeField] float tetherRadius = 0.08f;
         [SerializeField] Material tetherMaterial;
+        [Tooltip("Seconds the tether takes to retract to the arm on release. Continuity-of-existence law: a player-visible beam must never instant-pop out. The ignite already grows from zero; this animates the retract.")]
+        [SerializeField] float tetherRetractDuration = 0.15f;
 
         [Header("Spinneret Arms")]
         [Tooltip("Visual thickness of the arm capsule.")]
@@ -96,6 +98,10 @@ namespace CosmicShore.Gameplay
             public Transform capsule;
             public MeshRenderer capsuleRenderer;
             public float sweepPulse;
+            // Continuity-of-existence retract: far end slides back to the arm.
+            public bool isRetracting;
+            public float retractTimer;
+            public Vector3 retractEnd;
         }
 
         // ---- Public API ----
@@ -395,6 +401,7 @@ namespace CosmicShore.Gameplay
         void FireTetherFromTip(ref TetherState tether, Vector3 tipPosition, Vector3 aimTarget)
         {
             tether.isFiring = true;
+            tether.isRetracting = false; // re-fire cancels any in-flight retract
             tether.extension = 0f;
             tether.fireOrigin = tipPosition;
             Vector3 dir = aimTarget - tipPosition;
@@ -404,11 +411,31 @@ namespace CosmicShore.Gameplay
 
         void ReleaseTether(ref TetherState tether)
         {
+            // Continuity of existence: the lightsaber retracts to the spinneret
+            // rather than vanishing. Capture the current far end and let
+            // UpdateTetherVisual slide the visible beam back to the arm tip.
+            bool wasVisible = tether.capsuleRenderer != null && tether.capsuleRenderer.enabled;
+            if (wasVisible && tetherRetractDuration > 0f)
+            {
+                if (tether.isAnchored && tether.anchor != null)
+                    tether.retractEnd = tether.anchor.position;
+                else if (tether.isFiring)
+                    tether.retractEnd = tether.fireOrigin + tether.fireDirection * tether.extension;
+                // else: already retracting — keep the existing retractEnd target
+
+                tether.retractTimer = tetherRetractDuration;
+                tether.isRetracting = true;
+            }
+            else if (tether.capsuleRenderer != null)
+            {
+                tether.capsuleRenderer.enabled = false;
+                tether.isRetracting = false;
+            }
+
             tether.isAnchored = false;
             tether.isFiring = false;
             tether.anchor = null;
             tether.sweepPulse = 0f;
-            tether.capsuleRenderer.enabled = false;
         }
 
         // ==================================================================
@@ -648,6 +675,19 @@ namespace CosmicShore.Gameplay
                 end = tether.anchor.position;
             else if (tether.isFiring)
                 end = tether.fireOrigin + tether.fireDirection * tether.extension;
+            else if (tether.isRetracting)
+            {
+                tether.retractTimer -= Time.deltaTime;
+                if (tether.retractTimer <= 0f)
+                {
+                    tether.isRetracting = false;
+                    tether.capsuleRenderer.enabled = false;
+                    return;
+                }
+                // Far end slides from its release point back to the arm tip.
+                float t = tetherRetractDuration > 0f ? tether.retractTimer / tetherRetractDuration : 0f;
```

</details>

### `d464d20e1` — feat(spider): lightsaber sweep queries PrismSpatialIndex, gains blade kerf

_Claude, 2026-06-29 17:44:57 +0000_

```text
The sweep used Physics.RaycastNonAlloc, which is structurally blind to
freshly-laid prisms (colliders are disabled for ~0.6s after spawn, see
Docs/SPATIAL_INDEX.md) — swinging through a just-laid trail passed
through it ghostlike. It also had zero width, so it only cut prisms whose
centre a thin ray happened to cross.

Now SweepTether queries PrismSpatialIndex.QuerySphere over the region
swept this frame (allocation-free, sees ALL live registered mass), then
point-to-segment tests each candidate against the sub-stepped tether line
within a configurable blade kerf (sweepBladeRadius). The blade now cuts
everything it visually sweeps, fresh mass included — on-vision for two
lightsabers that slice through everything. Firing/anchor-finding rays stay
physics (narrow-phase is the blessed use; must not latch unsettled prisms).
```

```text
 Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs | 84 +++++++++++++++++++++++++++++-----------
 1 file changed, 61 insertions(+), 23 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 134 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs b/Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs
index 70a83b3db..9325df36e 100644
--- a/Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs
+++ b/Assets/_Scripts/Controller/Vessel/SwingingVesselTransformer.cs
@@ -1,3 +1,4 @@
+using System.Collections.Generic;
 using UnityEngine;
 using CosmicShore.Data;
 using CosmicShore.ScriptableObjects;
@@ -66,7 +67,9 @@ namespace CosmicShore.Gameplay
         [Header("Lightsaber Sweep")]
         [Tooltip("Anchored tethers destroy every prism they sweep through (except the anchors).")]
         [SerializeField] bool sweepDestroysPrisms = true;
-        [Tooltip("Max world-distance the vessel can move between sweep sub-casts. Lower = denser coverage at high speed.")]
+        [Tooltip("Blade kerf — a prism whose centre is within this distance of the taut tether line gets sliced. The blade has thickness, so it cuts everything it visually sweeps (a zero-width ray would miss prism centres it grazes).")]
+        [SerializeField] float sweepBladeRadius = 2.5f;
+        [Tooltip("Max world-distance the vessel can move between sweep sub-steps. Lower = denser coverage at high speed.")]
         [SerializeField] float sweepStepDistance = 4f;
         [Tooltip("How fast the kill-pulse on the tether visual decays.")]
         [SerializeField] float sweepPulseDecay = 4f;
@@ -157,7 +160,9 @@ namespace CosmicShore.Gameplay
         Vector3? pendingRightSpawnPos;
 
         Material sharedTetherMaterial;
-        static readonly RaycastHit[] sweepHits = new RaycastHit[32];
+        // Reused scratch for the spatial-index sweep query (allocation-free; the
+        // tick consumes it within one call, so one shared list is safe).
+        static readonly List<Prism> sweepScratch = new(128);
 
         int trailBlocksLayer = -1;
         int TrailBlocksLayer
@@ -592,48 +597,81 @@ namespace CosmicShore.Gameplay
         // ==================================================================
 
         /// <summary>
-        /// While anchored, the taut tether destroys every prism it passes
-        /// through — except the anchors themselves. The vessel's movement this
-        /// frame is sub-sampled so fast swings don't skip prisms between
-        /// frames: the tether is cast from interpolated vessel positions to
-        /// the anchor.
+        /// While anchored, the taut tether destroys every prism it sweeps
+        /// through — except the anchors themselves. Queried through
+        /// PrismSpatialIndex rather than physics raycasts: the index sees ALL
+        /// live prism mass, including freshly-laid trail whose colliders are
+        /// disabled for the first ~0.6s after spawn (a raycast would pass
+        /// through that fresh mass ghostlike — see Docs/SPATIAL_INDEX.md). One
+        /// sphere query covers the region swept this frame; each candidate is
+        /// then point-to-segment tested against the (sub-stepped) tether line,
+        /// so fast swings don't skip prisms between frames.
         /// </summary>
         void SweepTether(ref TetherState tether, Vector3 prevPos)
         {
             if (!tether.isAnchored || tether.anchor == null) return;
 
+            var index = PrismSpatialIndex.Instance;
+            if (index == null) return; // no spatial index in this scene → no registered prism mass to slice
+
             Vector3 anchorPos = tether.anchor.position;
-            float moved = Vector3.Distance(prevPos, transform.position);
+            Vector3 currStart = transform.position;
+            float moved = Vector3.Distance(prevPos, currStart);
             int steps = Mathf.Clamp(Mathf.CeilToInt(moved / Mathf.Max(sweepStepDistance, 0.5f)), 1, 4);
 
             Transform otherAnchor = (tether.anchor == leftTether.anchor) ? rightTether.anchor : leftTether.anchor;
-            int layerMask = 1 << TrailBlocksLayer;
+
+            // One query bounding the whole swept region (current tether line +
+            // the vessel's per-frame travel + blade kerf). By the triangle
+            // inequality this contains every prism within sweepBladeRadius of
+            // any sub-stepped tether line, so the per-candidate test below
+            // never misses one.
+            Vector3 mid = (currStart + anchorPos) * 0.5f;
+            float queryRadius = Vector3.Distance(currStart, anchorPos) * 0.5f + sweepBladeRadius + moved;
+            index.QuerySphere(mid, queryRadius, sweepScratch);
+            if (sweepScratch.Count == 0) return;
+
+            float bladeSq = sweepBladeRadius * sweepBladeRadius;
             bool killedSomething = false;
 
-            for (int s = 1; s <= steps; s++)
```

</details>

_Also contains 4 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
