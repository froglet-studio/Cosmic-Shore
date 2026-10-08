# Branch archive: `claude/restore-urchin-grizzly-hmdfu`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-07-16 by Claude
- **Unmerged commits:** 6
- **Forked from:** `8a3d6d0d3` (2026-04-16, Merge pull request #483 from froglet-studio/claude/update-skimmer-prism-effect)
- **Tip:** `adaeba561`
- **Files touched (80):**
  - `Assets/_Prefabs/Spaceships/Grizzly.prefab`
  - `Assets/_Prefabs/Spaceships/Urchin.prefab`
  - `Assets/_SO_Assets/Abilities/AbilityGrizzlyChargeShot.asset`
  - `Assets/_SO_Assets/Abilities/AbilityGrizzlyChargeShot.asset.meta`
  - `Assets/_SO_Assets/Abilities/AbilityGrizzlyDetonate.asset`
  - `Assets/_SO_Assets/Abilities/AbilityGrizzlyDetonate.asset.meta`
  - `Assets/_SO_Assets/Abilities/AbilityGrizzlySpin.asset`
  - `Assets/_SO_Assets/Abilities/AbilityGrizzlySpin.asset.meta`
  - `Assets/_SO_Assets/Abilities/AbilityGrizzlyTurretMode.asset`
  - `Assets/_SO_Assets/Abilities/AbilityGrizzlyTurretMode.asset.meta`
  - `Assets/_SO_Assets/Abilities/AbilityUrchinBarrage.asset`
  - `Assets/_SO_Assets/Abilities/AbilityUrchinBarrage.asset.meta`
  - `Assets/_SO_Assets/Abilities/AbilityUrchinDetach.asset`
  - `Assets/_SO_Assets/Abilities/AbilityUrchinDetach.asset.meta`
  - `Assets/_SO_Assets/Abilities/AbilityUrchinGhost.asset`
  - `Assets/_SO_Assets/Abilities/AbilityUrchinGhost.asset.meta`
  - `Assets/_SO_Assets/Abilities/AbilityUrchinVolley.asset`
  - `Assets/_SO_Assets/Abilities/AbilityUrchinVolley.asset.meta`
  - `Assets/_SO_Assets/Camera/GrizzlyCameraSettingsSO.asset`
  - `Assets/_SO_Assets/Camera/GrizzlyCameraSettingsSO.asset.meta`
  - `Assets/_SO_Assets/Camera/UrchinCameraSettingsSO.asset`
  - `Assets/_SO_Assets/Camera/UrchinCameraSettingsSO.asset.meta`
  - `Assets/_SO_Assets/Classes/SO_Class_Grizzly.asset`
  - `Assets/_SO_Assets/Classes/SO_Class_Urchin.asset`
  - `Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset`
  - `Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/GrizzlyImpactorDataContainer.asset`
  - `Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/GrizzlyImpactorDataContainer.asset.meta`
  - `Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/UrchinImpactorDataContainer.asset`
  - `Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/UrchinImpactorDataContainer.asset.meta`
  - `Assets/_SO_Assets/Games/ArcadeGameFreestyle.asset`
  - `Assets/_SO_Assets/ShipActions/Grizzly.meta`
  - `Assets/_SO_Assets/ShipActions/Grizzly/GrizzlyChargedShotAction.asset`
  - `Assets/_SO_Assets/ShipActions/Grizzly/GrizzlyChargedShotAction.asset.meta`
  - `Assets/_SO_Assets/ShipActions/Grizzly/GrizzlyDetonateAction.asset`
  - `Assets/_SO_Assets/ShipActions/Grizzly/GrizzlyDetonateAction.asset.meta`
  - `Assets/_SO_Assets/ShipActions/Grizzly/GrizzlySpinAroundAction.asset`
  - `Assets/_SO_Assets/ShipActions/Grizzly/GrizzlySpinAroundAction.asset.meta`
  - `Assets/_SO_Assets/ShipActions/Grizzly/GrizzlyTurretModeAction.asset`
  - `Assets/_SO_Assets/ShipActions/Grizzly/GrizzlyTurretModeAction.asset.meta`
  - `Assets/_SO_Assets/ShipActions/Urchin.meta`
  - … and 40 more

### `8e7d4d38e` — Restore Urchin and Grizzly to playable class list

_Claude, 2026-02-21 23:29:19 +0000_

```text
Add SO_Class_Urchin and SO_Class_Grizzly to SO_Classlist_Classes ShipList,
making both vessels selectable in the Hangar, Loadout, and Main Menu screens.
Both ships already had complete prefabs, captains, abilities, and games
configured — they were only missing from the Classes ship list.
```

```text
 Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset | 2 ++
 1 file changed, 2 insertions(+)
```

### `30beed62d` — Add Urchin and Grizzly prefabs to Vessel Prefab Container

_Claude, 2026-02-23 19:42:06 +0000_

```text
The VesselPrefabContainer ScriptableObject was missing entries for the
Urchin and Grizzly ship prefabs, causing "No Vessel Prefabs found matching
the needed vessel type!" errors when trying to spawn these ships in
freestyle mode.
```

```text
 Assets/_SO_Assets/Vessel Prefab Container.asset | 2 ++
 1 file changed, 2 insertions(+)
```

### `b3e861d51` — Restore Urchin and Grizzly ship abilities, camera settings, and HUD stubs

_Claude, 2026-02-24 16:15:43 +0000_

```text
- Create 4 unique ability SOs for Urchin (Barrage, Ghost, Volley, Detach) based on
  actual prefab mechanics, replacing borrowed Serpent ability placeholders
- Create 4 unique ability SOs for Grizzly (Charge Shot, Spin, Detonate, Turret Mode)
  based on actual prefab mechanics, replacing single Serpent ability placeholder
- Update SO_Class_Urchin and SO_Class_Grizzly to reference their own abilities
- Create camera settings SOs for both ships and wire them into prefabs
- Add minimal HUD controller and view scripts for both ships
- Update Grizzly description to reflect its charge-shot/turret gameplay
```

```text
 Assets/_Prefabs/Spaceships/Grizzly.prefab                          |  2 +-
 Assets/_Prefabs/Spaceships/Urchin.prefab                           |  2 +-
 Assets/_SO_Assets/Abilities/AbilityGrizzlyChargeShot.asset         | 21 +++++++++++++++++++++
 Assets/_SO_Assets/Abilities/AbilityGrizzlyChargeShot.asset.meta    |  8 ++++++++
 Assets/_SO_Assets/Abilities/AbilityGrizzlyDetonate.asset           | 20 ++++++++++++++++++++
 Assets/_SO_Assets/Abilities/AbilityGrizzlyDetonate.asset.meta      |  8 ++++++++
 Assets/_SO_Assets/Abilities/AbilityGrizzlySpin.asset               | 20 ++++++++++++++++++++
 Assets/_SO_Assets/Abilities/AbilityGrizzlySpin.asset.meta          |  8 ++++++++
 Assets/_SO_Assets/Abilities/AbilityGrizzlyTurretMode.asset         | 20 ++++++++++++++++++++
 Assets/_SO_Assets/Abilities/AbilityGrizzlyTurretMode.asset.meta    |  8 ++++++++
 Assets/_SO_Assets/Abilities/AbilityUrchinBarrage.asset             | 20 ++++++++++++++++++++
 Assets/_SO_Assets/Abilities/AbilityUrchinBarrage.asset.meta        |  8 ++++++++
 Assets/_SO_Assets/Abilities/AbilityUrchinDetach.asset              | 20 ++++++++++++++++++++
 Assets/_SO_Assets/Abilities/AbilityUrchinDetach.asset.meta         |  8 ++++++++
 Assets/_SO_Assets/Abilities/AbilityUrchinGhost.asset               | 20 ++++++++++++++++++++
 Assets/_SO_Assets/Abilities/AbilityUrchinGhost.asset.meta          |  8 ++++++++
 Assets/_SO_Assets/Abilities/AbilityUrchinVolley.asset              | 20 ++++++++++++++++++++
 Assets/_SO_Assets/Abilities/AbilityUrchinVolley.asset.meta         |  8 ++++++++
 Assets/_SO_Assets/Camera/GrizzlyCameraSettingsSO.asset             | 26 ++++++++++++++++++++++++++
 Assets/_SO_Assets/Camera/GrizzlyCameraSettingsSO.asset.meta        |  8 ++++++++
 Assets/_SO_Assets/Camera/UrchinCameraSettingsSO.asset              | 26 ++++++++++++++++++++++++++
 Assets/_SO_Assets/Camera/UrchinCameraSettingsSO.asset.meta         |  8 ++++++++
 Assets/_SO_Assets/Classes/SO_Class_Grizzly.asset                   |  9 ++++++---
 Assets/_SO_Assets/Classes/SO_Class_Urchin.asset                    |  8 ++++----
 Assets/_Scripts/VesselHUD/Controller/GrizzlyVesselHUDController.cs | 18 ++++++++++++++++++
 Assets/_Scripts/VesselHUD/Controller/UrchinVesselHUDController.cs  | 18 ++++++++++++++++++
 Assets/_Scripts/VesselHUD/View/GrizzlyVesselHUDView.cs             | 16 ++++++++++++++++
 Assets/_Scripts/VesselHUD/View/UrchinVesselHUDView.cs              | 16 ++++++++++++++++
 28 files changed, 373 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/VesselHUD/Controller/GrizzlyVesselHUDController.cs b/Assets/_Scripts/VesselHUD/Controller/GrizzlyVesselHUDController.cs
new file mode 100644
index 000000000..0349b8974
--- /dev/null
+++ b/Assets/_Scripts/VesselHUD/Controller/GrizzlyVesselHUDController.cs
@@ -0,0 +1,18 @@
+using UnityEngine;
+
+namespace CosmicShore.Game
+{
+    public class GrizzlyVesselHUDController : VesselHUDController
+    {
+        [Header("View")]
+        [SerializeField] private GrizzlyVesselHUDView view;
+
+        public override void Initialize(IVesselStatus vesselStatus)
+        {
+            base.Initialize(vesselStatus);
+
+            if (!view)
+                view = View as GrizzlyVesselHUDView;
+        }
+    }
+}
diff --git a/Assets/_Scripts/VesselHUD/Controller/UrchinVesselHUDController.cs b/Assets/_Scripts/VesselHUD/Controller/UrchinVesselHUDController.cs
new file mode 100644
index 000000000..bb6550937
--- /dev/null
+++ b/Assets/_Scripts/VesselHUD/Controller/UrchinVesselHUDController.cs
@@ -0,0 +1,18 @@
+using UnityEngine;
+
+namespace CosmicShore.Game
+{
+    public class UrchinVesselHUDController : VesselHUDController
+    {
+        [Header("View")]
+        [SerializeField] private UrchinVesselHUDView view;
+
+        public override void Initialize(IVesselStatus vesselStatus)
+        {
+            base.Initialize(vesselStatus);
+
+            if (!view)
+                view = View as UrchinVesselHUDView;
+        }
+    }
+}
diff --git a/Assets/_Scripts/VesselHUD/View/GrizzlyVesselHUDView.cs b/Assets/_Scripts/VesselHUD/View/GrizzlyVesselHUDView.cs
new file mode 100644
index 000000000..c967bd974
--- /dev/null
+++ b/Assets/_Scripts/VesselHUD/View/GrizzlyVesselHUDView.cs
@@ -0,0 +1,16 @@
+using UnityEngine;
+
+namespace CosmicShore.Game
+{
+    public class GrizzlyVesselHUDView : VesselHUDView
+    {
+        public override void Initialize()
+        {
+            foreach (var h in highlights)
+            {
+                if (h.image)
+                    h.image.enabled = false;
+            }
+        }
+    }
+}
diff --git a/Assets/_Scripts/VesselHUD/View/UrchinVesselHUDView.cs b/Assets/_Scripts/VesselHUD/View/UrchinVesselHUDView.cs
new file mode 100644
index 000000000..f68e04de5
--- /dev/null
+++ b/Assets/_Scripts/VesselHUD/View/UrchinVesselHUDView.cs
@@ -0,0 +1,16 @@
+using UnityEngine;
+
+namespace CosmicShore.Game
+{
+    public class UrchinVesselHUDView : VesselHUDView
+    {
+        public override void Initialize()
+        {
+            foreach (var h in highlights)
+            {
+                if (h.image)
+                    h.image.enabled = false;
+            }
+        }
+    }
+}
```

</details>

### `c795ec5b9` — Restore Urchin and Grizzly to full playability in the modern vessel architecture

_Claude, 2026-06-12 00:27:06 +0000_

```text
Both prefabs predate the ShipActionSO + ActionExecutor refactor: their legacy
MonoBehaviour actions were never invoked, vesselType was unset (so the spawner
could not find them), VesselController was missing entirely, guns had no
projectile factory, and resources were empty. This restores each ship's
signature kit as first-class citizens of the new action system.

New action code (ported from the legacy ShipAction implementations):
- GhostActionSO + GhostActionExecutor: Urchin's phasing intangibility
- DetachActionSO: Urchin's break-away from attached prisms
- ChargedFireGunActionSO + ChargedFireGunActionExecutor: Grizzly's
  hold-to-charge cannon with scale-by-charge shots, stop/detonate while a
  shot is in flight
- DetonateProjectilesActionSO + DetonateProjectilesActionExecutor: Grizzly's
  remote detonation
- SpinAroundActionSO: Grizzly's instant 180 flat spin
- ToggleTurretModeActionSO + ToggleTurretModeActionExecutor: Grizzly's
  plant-as-turret mode with doubled resource regen while stationary
- Gun.DetonateProjectile() implemented (was a logging stub)

Prefab rewiring (both ships):
- VesselController, ActionExecutorRegistry, VesselImpactor,
  NetworkVesselImpactor, ImpactCollider, and HUD controller added
- VesselStatus wired: shipInstance, vesselType (4/5), name, HUD,
  near-field skimmer, orientation handle
- ResourceSystem populated (Urchin: Ammo; Grizzly: Energy + Ammo)
- R_VesselActionHandler input mappings + shared input event channels
- Ship geometries registered for customization and ghost phasing

Urchin loadout: right stick = dual-gun Barrage (energized full-auto),
left stick = prism Volley (UrchinBlock pool), button 1 = Ghost,
button 2 = Detach.
Grizzly loadout: right stick = Charged Shot, left stick = Detonate,
button 1 = Turret Mode, button 2 = Spin Around.

Both vessels added to the Freestyle roster (unlocked by default via
SO_Vessel.isLocked = false) with per-ship impactor effect containers.
```

```text
 Assets/_SO_Assets/ShipActions/Urchin/UrchinGhostAction.asset          |  15 ++
 Assets/_SO_Assets/ShipActions/Urchin/UrchinGhostAction.asset.meta     |   8 +
 Assets/_SO_Assets/ShipActions/Urchin/UrchinVolleyAction.asset         |  22 +++
 Assets/_SO_Assets/ShipActions/Urchin/UrchinVolleyAction.asset.meta    |   8 +
 Assets/_Scripts/Game/Projectiles/Gun.cs                               |   6 +-
 .../Game/Ship/R_ShipActions/Data Containers/ChargedFireGunActionSO.cs |  32 ++++
 .../Ship/R_ShipActions/Data Containers/ChargedFireGunActionSO.cs.meta |  11 ++
 .../Game/Ship/R_ShipActions/Data Containers/DetachActionSO.cs         |  19 ++
 .../Game/Ship/R_ShipActions/Data Containers/DetachActionSO.cs.meta    |  11 ++
 .../Ship/R_ShipActions/Data Containers/DetonateProjectilesActionSO.cs |  13 ++
 .../R_ShipActions/Data Containers/DetonateProjectilesActionSO.cs.meta |  11 ++
 .../_Scripts/Game/Ship/R_ShipActions/Data Containers/GhostActionSO.cs |  18 ++
 .../Game/Ship/R_ShipActions/Data Containers/GhostActionSO.cs.meta     |  11 ++
 .../Game/Ship/R_ShipActions/Data Containers/SpinAroundActionSO.cs     |  17 ++
 .../Ship/R_ShipActions/Data Containers/SpinAroundActionSO.cs.meta     |  11 ++
 .../Ship/R_ShipActions/Data Containers/ToggleTurretModeActionSO.cs    |  24 +++
 .../R_ShipActions/Data Containers/ToggleTurretModeActionSO.cs.meta    |  11 ++
 .../Game/Ship/R_ShipActions/Executors/ChargedFireGunActionExecutor.cs | 194 ++++++++++++++++++++
 .../Ship/R_ShipActions/Executors/ChargedFireGunActionExecutor.cs.meta |  11 ++
 .../Ship/R_ShipActions/Executors/DetonateProjectilesActionExecutor.cs |  33 ++++
 .../R_ShipActions/Executors/DetonateProjectilesActionExecutor.cs.meta |  11 ++
 .../_Scripts/Game/Ship/R_ShipActions/Executors/GhostActionExecutor.cs | 101 +++++++++++
 .../Game/Ship/R_ShipActions/Executors/GhostActionExecutor.cs.meta     |  11 ++
 .../Ship/R_ShipActions/Executors/ToggleTurretModeActionExecutor.cs    | 102 +++++++++++
 .../R_ShipActions/Executors/ToggleTurretModeActionExecutor.cs.meta    |  11 ++
 .../_Scripts/VesselHUD/Controller/GrizzlyVesselHUDController.cs.meta  |  11 ++
 .../_Scripts/VesselHUD/Controller/UrchinVesselHUDController.cs.meta   |  11 ++
 Assets/_Scripts/VesselHUD/View/GrizzlyVesselHUDView.cs.meta           |  11 ++
 Assets/_Scripts/VesselHUD/View/UrchinVesselHUDView.cs.meta            |  11 ++
 50 files changed, 1505 insertions(+), 34 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 471 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Projectiles/Gun.cs b/Assets/_Scripts/Game/Projectiles/Gun.cs
index b058a98e7..14260aa4a 100644
--- a/Assets/_Scripts/Game/Projectiles/Gun.cs
+++ b/Assets/_Scripts/Game/Projectiles/Gun.cs
@@ -77,8 +77,10 @@ namespace CosmicShore.Game.Projectiles
 
         public void DetonateProjectile()
         {
-            CSDebug.Log("Gun DetonateProjectile called");
-            // Example: if (_lastProjectile is ExplodableProjectile ep) ep.Detonate();
+            if (!_lastProjectile) return;
+
+            _lastProjectile.ReturnToFactory();
+            _lastProjectile = null;
         }
         #endregion
 
diff --git a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/ChargedFireGunActionExecutor.cs b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/ChargedFireGunActionExecutor.cs
new file mode 100644
index 000000000..9136f888c
--- /dev/null
+++ b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/ChargedFireGunActionExecutor.cs
@@ -0,0 +1,194 @@
+using System;
+using System.Threading;
+using Cysharp.Threading.Tasks;
+using CosmicShore.App.Systems.Audio;
+using CosmicShore.Core;
+using CosmicShore.Game;
+using CosmicShore.Game.Projectiles;
+using UnityEngine;
+
+/// <summary>
+/// Hold-to-charge cannon. Holding the action builds energy; releasing fires a
+/// projectile whose scale grows with the accumulated charge. Pressing while a
+/// charged shot is in flight stops it; releasing while one is in flight detonates it.
+/// </summary>
+public sealed class ChargedFireGunActionExecutor : ShipActionExecutorBase
+{
+    /// <summary>Static event: a charged shot was fired. Param = player name.</summary>
+    public static event Action<string> OnChargedShotFired;
+
+    /// <summary>Charge amount changed (0-1 of the energy resource). For HUD binding.</summary>
+    public event Action<float> OnChargeChanged;
+
+    [Header("Scene Refs")]
+    [SerializeField] Gun gun;
+    [SerializeField] Transform projectileContainer;
+
+    IVesselStatus _status;
+    ResourceSystem _resources;
+
+    CancellationTokenSource _chargeCts;
+    CancellationTokenSource _watchCts;
+
+    public override void Initialize(IVesselStatus shipStatus)
+    {
+        _status = shipStatus;
+        _resources = shipStatus.ResourceSystem;
+
+        if (gun != null)
+            gun.Initialize(shipStatus);
+    }
+
+    void OnDisable()
+    {
+        CancelCharge();
+        CancelWatch();
+    }
+
+    public void Begin(ChargedFireGunActionSO so, IVesselStatus status)
+    {
+        if (so == null || _resources == null || !gun)
+            return;
+
+        if (_status is { HasLiveProjectiles: true })
+        {
+            gun.StopProjectile();
+            _status.HasLiveProjectiles = false;
+            CancelWatch();
+            return;
+        }
+
+        CancelCharge();
+        _chargeCts = CancellationTokenSource.CreateLinkedTokenSource(
+            this.GetCancellationTokenOnDestroy());
+        ChargeAsync(so, _chargeCts.Token).Forget();
+    }
+
+    public void Release(ChargedFireGunActionSO so, IVesselStatus status)
+    {
+        if (so == null || _resources == null || !gun)
+            return;
+
+        if (_status is { HasLiveProjectiles: true })
+        {
+            gun.DetonateProjectile();
+            _status.HasLiveProjectiles = false;
+            CancelWatch();
+            return;
+        }
+
+        CancelCharge();
+
+        var energy = _resources.Resources[so.EnergyResourceIndex];
+        var ammo = _resources.Resources[so.AmmoResourceIndex];
+        var charge = energy.CurrentAmount;
+
+        if (charge > 0f && ammo.CurrentAmount > charge)
+        {
+            _resources.ChangeResourceAmount(so.AmmoResourceIndex, -charge);
+
+            var inheritedDirection = _status is { IsAttached: false, IsTranslationRestricted: false }
+                ? _status.Course
+                : gun.transform.forward;
+
+            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.GunFire);
+            OnChargedShotFired?.Invoke(_status?.PlayerName);
+
+            gun.FireGun(
+                projectileContainer ? projectileContainer : gun.transform,
+                so.ProjectileSpeed,
+                inheritedDirection * (_status?.Speed ?? 0f),
+                so.ProjectileScale * charge,
+                true,
+                so.ProjectileTime,
+                charge);
+
+            StartWatchingProjectiles();
+        }
+
+        _resources.ResetResource(so.EnergyResourceIndex);
+        OnChargeChanged?.Invoke(0f);
+    }
+
+    async UniTaskVoid ChargeAsync(ChargedFireGunActionSO so, CancellationToken token)
+    {
+        const float chargePeriod = 0.1f;
+        var index = so.EnergyResourceIndex;
+
+        try
+        {
+            while (!token.IsCancellationRequested)
+            {
+                var resource = _resources.Resources[index];
+                if (resource.CurrentAmount >= resource.MaxAmount)
+                    break;
+
+                await UniTask.Delay(
+                    TimeSpan.FromSeconds(chargePeriod),
```

</details>

### `adaeba561` — Address review findings: real detonation, watcher allocs, HUD stub cleanup

_Claude, 2026-07-16 01:19:58 +0000_

```text
- Projectile.Detonate(): end the flight through the projectile's end-effect
  pipeline (same path as natural expiry), so remote detonation produces the
  configured AOE explosion instead of a silent despawn
- ProjectileImpactor.ExecuteEndEffects() now reports whether any effect ran;
  Projectile falls back to an immediate factory return when a prefab has no
  end effects, so detonated or expired projectiles can no longer be stranded
- ChargedFireGunActionExecutor: poll the live-projectile watcher on a 0.2s
  interval with GetComponentInChildren instead of allocating a component
  array every frame (charged shots can fly until detonated); adopt the
  status-parameter convention used by sibling executors
- Slim the Urchin/Grizzly HUD controller stubs to typed subclasses — the
  base controller already handles view discovery and input highlights
```

```text
 Assets/_Scripts/Game/ImpactEffects/Impactors/ProjectileImpactor.cs                | 10 +++++++---
 Assets/_Scripts/Game/Projectiles/Gun.cs                                           |  2 +-
 Assets/_Scripts/Game/Projectiles/Projectile.cs                                    | 20 ++++++++++++++++++--
 Assets/_Scripts/Game/Ship/R_ShipActions/Executors/ChargedFireGunActionExecutor.cs | 14 ++++++++++++--
 Assets/_Scripts/VesselHUD/Controller/GrizzlyVesselHUDController.cs                | 17 +++++------------
 Assets/_Scripts/VesselHUD/Controller/UrchinVesselHUDController.cs                 | 17 +++++------------
 6 files changed, 48 insertions(+), 32 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 176 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/ImpactEffects/Impactors/ProjectileImpactor.cs b/Assets/_Scripts/Game/ImpactEffects/Impactors/ProjectileImpactor.cs
index 8180b215a..154c85a08 100644
--- a/Assets/_Scripts/Game/ImpactEffects/Impactors/ProjectileImpactor.cs
+++ b/Assets/_Scripts/Game/ImpactEffects/Impactors/ProjectileImpactor.cs
@@ -18,13 +18,17 @@ namespace CosmicShore.Game
             
         }
 
-        public void ExecuteEndEffects()
+        /// <returns>True when at least one end effect ran. Callers that rely on end
+        /// effects to return the projectile to its pool must handle a false return.</returns>
+        public bool ExecuteEndEffects()
         {
             if (projectileImpactorDataContainer.ProjectileEndEffects.Length <= 0)
-                return;
-            
+                return false;
+
             foreach (var effect in projectileImpactorDataContainer.ProjectileEndEffects)
                 effect.Execute(this, this);     // here we are passing itself as impactee, coz it doesn't have any impactee.
+
+            return true;
         }
         
         protected override void AcceptImpactee(IImpactor impactee)
diff --git a/Assets/_Scripts/Game/Projectiles/Gun.cs b/Assets/_Scripts/Game/Projectiles/Gun.cs
index 14260aa4a..26727711b 100644
--- a/Assets/_Scripts/Game/Projectiles/Gun.cs
+++ b/Assets/_Scripts/Game/Projectiles/Gun.cs
@@ -79,7 +79,7 @@ namespace CosmicShore.Game.Projectiles
         {
             if (!_lastProjectile) return;
 
-            _lastProjectile.ReturnToFactory();
+            _lastProjectile.Detonate();
             _lastProjectile = null;
         }
         #endregion
diff --git a/Assets/_Scripts/Game/Projectiles/Projectile.cs b/Assets/_Scripts/Game/Projectiles/Projectile.cs
index 064a4c78a..c4010f292 100644
--- a/Assets/_Scripts/Game/Projectiles/Projectile.cs
+++ b/Assets/_Scripts/Game/Projectiles/Projectile.cs
@@ -157,6 +157,20 @@ namespace CosmicShore.Game.Projectiles
             }
         }
 
+        /// <summary>
+        /// Ends the flight immediately, running the same end effects (explosion,
+        /// factory return) as natural expiry. No-op once the projectile is pooled.
+        /// </summary>
+        public void Detonate()
+        {
+            if (!isActiveAndEnabled) return;
+
+            Stop();
+
+            if (!projectileImpactor || !projectileImpactor.ExecuteEndEffects())
+                ReturnToFactory();
+        }
+
         private async UniTaskVoid MoveProjectileAsync(float projectileTime, CancellationToken token)
         {
             float elapsedTime = 0f;
@@ -183,8 +197,10 @@ namespace CosmicShore.Game.Projectiles
                     await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
                 }
 
-                projectileImpactor.ExecuteEndEffects();
-                // ReturnToFactory(); // handled by end effects (delayed)
+                // End effects handle the (delayed) factory return; without any
+                // configured, return immediately so the projectile is not stranded.
+                if (!projectileImpactor.ExecuteEndEffects())
+                    ReturnToFactory();
             }
             catch (OperationCanceledException) { }
             catch (Exception ex)
diff --git a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/ChargedFireGunActionExecutor.cs b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/ChargedFireGunActionExecutor.cs
index 9136f888c..cc4592a74 100644
--- a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/ChargedFireGunActionExecutor.cs
+++ b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/ChargedFireGunActionExecutor.cs
@@ -47,6 +47,7 @@ public sealed class ChargedFireGunActionExecutor : ShipActionExecutorBase
 
     public void Begin(ChargedFireGunActionSO so, IVesselStatus status)
     {
+        if (status != null) _status = status;
         if (so == null || _resources == null || !gun)
             return;
 
@@ -66,6 +67,7 @@ public sealed class ChargedFireGunActionExecutor : ShipActionExecutorBase
 
     public void Release(ChargedFireGunActionSO so, IVesselStatus status)
     {
+        if (status != null) _status = status;
         if (so == null || _resources == null || !gun)
             return;
 
@@ -153,13 +155,21 @@ public sealed class ChargedFireGunActionExecutor : ShipActionExecutorBase
         if (_status == null || !projectileContainer)
             return;
 
+        // Charged shots can fly until detonated, so poll on an interval instead of
+        // every frame — GetComponentInChildren avoids the per-check array allocation.
+        const float pollPeriod = 0.2f;
+
         try
         {
             _status.HasLiveProjectiles = true;
             while (!token.IsCancellationRequested &&
-                   projectileContainer.GetComponentsInChildren<Projectile>().Length > 0)
+                   projectileContainer.GetComponentInChildren<Projectile>())
             {
-                await UniTask.Yield(PlayerLoopTiming.Update, token);
+                await UniTask.Delay(
+                    TimeSpan.FromSeconds(pollPeriod),
+                    DelayType.DeltaTime,
+                    PlayerLoopTiming.Update,
+                    token);
             }
         }
         catch (OperationCanceledException)
diff --git a/Assets/_Scripts/VesselHUD/Controller/GrizzlyVesselHUDController.cs b/Assets/_Scripts/VesselHUD/Controller/GrizzlyVesselHUDController.cs
index 0349b8974..fed351150 100644
--- a/Assets/_Scripts/VesselHUD/Controller/GrizzlyVesselHUDController.cs
+++ b/Assets/_Scripts/VesselHUD/Controller/GrizzlyVesselHUDController.cs
@@ -1,18 +1,11 @@
-using UnityEngine;
-
 namespace CosmicShore.Game
 {
+    /// <summary>
+    /// Grizzly-specific HUD controller. The base class drives input highlights and
+    /// view lifecycle; Grizzly-specific bindings (charge meter, turret state, etc.)
+    /// land here as the HUD design fills out.
+    /// </summary>
     public class GrizzlyVesselHUDController : VesselHUDController
     {
-        [Header("View")]
-        [SerializeField] private GrizzlyVesselHUDView view;
-
-        public override void Initialize(IVesselStatus vesselStatus)
-        {
-            base.Initialize(vesselStatus);
-
-            if (!view)
-                view = View as GrizzlyVesselHUDView;
-        }
     }
 }
diff --git a/Assets/_Scripts/VesselHUD/Controller/UrchinVesselHUDController.cs b/Assets/_Scripts/VesselHUD/Controller/UrchinVesselHUDController.cs
index bb6550937..0651f5dc0 100644
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
