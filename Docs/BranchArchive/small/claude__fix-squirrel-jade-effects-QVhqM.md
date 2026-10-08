# Branch archive: `claude/fix-squirrel-jade-effects-QVhqM`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-22 by Claude
- **Unmerged commits:** 1
- **Forked from:** `ea2031af2` (2026-04-22, fix(joust): wire CountdownTimer so Go button starts the game)
- **Tip:** `89c695583`
- **Files touched (6):**
  - `Assets/_Prefabs/Projectile/AOERingSpawner.prefab`
  - `Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs`
  - `Assets/_Scripts/Controller/Managers/PrismTeamManager.cs`
  - `Assets/_Scripts/Controller/Managers/ThemeManager.cs`
  - `Assets/_Scripts/Controller/Projectiles/AOEBlockCreation.cs`
  - `Assets/_Scripts/Controller/Vessel/Prism.cs`

### `89c695583` — fix(vessel): squirrel crystal-hit prisms now render in vessel domain

_Claude, 2026-04-22 16:34:57 +0000_

```text
The menu squirrel leaves a jade trail but crystal-hit AOE spawned blue
shielded prisms. Several independent bugs combined to force the domain
back to Blue:

- `Prism.Domain` setter routed through `SetInitialTeam`, a one-shot
  initializer that no-ops when the prism's domain is already set.
  Pooled prisms (AOE spawners) silently kept their previous domain.
- `AOEBlockCreation.CreateBlock` set `IsShielded` and initialized the
  prism but never called `ChangeTeam`, so pooled prisms rendered with
  whatever domain they held before.
- `SpawnableBase.domain` defaulted to `Domains.Blue` and the
  `AOERingSpawner.prefab` baked `domain: 3` (Blue) as the serialized
  value, so fresh spawners started Blue.
- `ThemeManager` mapped `Domains.Unassigned` to `BlueTeamMaterialSet`,
  so any unresolved-domain lookup painted the prism Blue.

Switch the setter to `ChangeTeam`, have `AOEBlockCreation` explicitly
set the block's team, default the spawner base to `Unassigned`, clear
the serialized Blue, and map `Unassigned` to the Jade material set so
fallback lookups match the default team. `SetInitialTeam` had no
remaining callers and was removed.
```

```text
 Assets/_Prefabs/Projectile/AOERingSpawner.prefab                 |  6 +++---
 Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs |  2 +-
 Assets/_Scripts/Controller/Managers/PrismTeamManager.cs          | 12 ------------
 Assets/_Scripts/Controller/Managers/ThemeManager.cs              |  2 +-
 Assets/_Scripts/Controller/Projectiles/AOEBlockCreation.cs       |  3 ++-
 Assets/_Scripts/Controller/Vessel/Prism.cs                       |  2 +-
 6 files changed, 8 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs b/Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs
index 569e64a5a..75495e8e2 100644
--- a/Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs
+++ b/Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs
@@ -28,7 +28,7 @@ namespace CosmicShore.Gameplay
     {
         [Header("Spawnable Base")]
         [SerializeField] protected int seed;
-        [SerializeField] public Domains domain = Domains.Blue;
+        [SerializeField] public Domains domain = Domains.Unassigned;
 
         [Header("Tree Structure")]
         [Tooltip("Child generators to evaluate at each generated point. " +
diff --git a/Assets/_Scripts/Controller/Managers/PrismTeamManager.cs b/Assets/_Scripts/Controller/Managers/PrismTeamManager.cs
index e4a4b436a..57ed55d9e 100644
--- a/Assets/_Scripts/Controller/Managers/PrismTeamManager.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismTeamManager.cs
@@ -39,18 +39,6 @@ namespace CosmicShore.Gameplay
             materialAnimator = GetComponent<MaterialPropertyAnimator>();
         }
 
-        public void SetInitialTeam(Domains domain)
-        {
-            if (currentDomain == Domains.Unassigned)
-            {
-                Domain = domain;
-                materialAnimator.UpdateMaterial(
-                    _themeManagerData.GetTeamTransparentBlockMaterial(domain),
-                    _themeManagerData.GetTeamBlockMaterial(domain)
-                );
-            }
-        }
-
         public void ChangeTeam(Domains newDomain)
         {
             if (Domain != newDomain)
diff --git a/Assets/_Scripts/Controller/Managers/ThemeManager.cs b/Assets/_Scripts/Controller/Managers/ThemeManager.cs
index cc1b8b517..ed96834c4 100644
--- a/Assets/_Scripts/Controller/Managers/ThemeManager.cs
+++ b/Assets/_Scripts/Controller/Managers/ThemeManager.cs
@@ -22,7 +22,7 @@ namespace CosmicShore.Gameplay
                 { Domains.Ruby,   RedTeamMaterialSet },
                 { Domains.Blue,  BlueTeamMaterialSet },
                 { Domains.Gold,  GoldTeamMaterialSet },
-                { Domains.Unassigned,  BlueTeamMaterialSet },
+                { Domains.Unassigned,  GreenTeamMaterialSet },
             };
         }
 
diff --git a/Assets/_Scripts/Controller/Projectiles/AOEBlockCreation.cs b/Assets/_Scripts/Controller/Projectiles/AOEBlockCreation.cs
index 9b034d08b..1934c32bb 100644
--- a/Assets/_Scripts/Controller/Projectiles/AOEBlockCreation.cs
+++ b/Assets/_Scripts/Controller/Projectiles/AOEBlockCreation.cs
@@ -131,10 +131,11 @@ namespace CosmicShore.Gameplay
 
             block.ownerID = OwnerIdBase + ownerSuffix + position;
             block.TargetScale = blockScale;
+            block.ChangeTeam(Domain);
 
             if (shielded)
                 block.prismProperties.IsShielded = true;
-            
+
             block.Initialize(Vessel?.VesselStatus?.PlayerName ?? "UnknownPlayer");
             trail.Add(block);
             return block;
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index a979bc8e6..7124c3723 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -59,7 +59,7 @@ namespace CosmicShore.Gameplay
             get => teamManager?.Domain ?? Domains.Unassigned;
             set
             {
-                if (teamManager) teamManager.SetInitialTeam(value);
+                if (teamManager) teamManager.ChangeTeam(value);
             }
         }
 
```

</details>
