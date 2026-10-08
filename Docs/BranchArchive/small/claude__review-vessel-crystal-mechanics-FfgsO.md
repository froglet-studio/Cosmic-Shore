# Branch archive: `claude/review-vessel-crystal-mechanics-FfgsO`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-01 by Claude
- **Unmerged commits:** 1
- **Forked from:** `1a310a189` (2026-03-31, Update Menu_Main.unity)
- **Tip:** `9b17a950e`
- **Files touched (1):**
  - `Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs`

### `9b17a950e` — feat(crystal): add initial domain field for prefab-driven domain assignment

_Claude, 2026-04-01 22:00:38 +0000_

```text
Adds a serialized `_initialDomain` field to Crystal that, when set to a
value other than None, applies that domain in Start() after the
CrystalManager's default spawn-time domain assignment. This enables
creating prefab variants (e.g. JadeCrystal) with a preconfigured domain
without modifying any CrystalManager logic.
```

```text
 Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs | 7 +++++++
 1 file changed, 7 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
index 649c22412..338733813 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
@@ -40,6 +40,10 @@ namespace CosmicShore.Gameplay
         [SerializeField] protected bool allowVesselImpactEffect = true;
         [SerializeField] bool allowRespawnOnImpact;
 
+        [Header("Initial Domain")]
+        [Tooltip("When not None, the crystal applies this domain on Start, overriding any manager-assigned domain.")]
+        [SerializeField] private Domains _initialDomain = Domains.None;
+
         [Header("Data Containers")]
         [SerializeField] protected ThemeManagerDataContainerSO _themeManagerData;
 
@@ -54,6 +58,9 @@ namespace CosmicShore.Gameplay
         protected virtual void Start()
         {
             crystalProperties.crystalValue = crystalProperties.fuelAmount * transform.lossyScale.x;
+
+            if (_initialDomain != Domains.None)
+                ChangeDomain(_initialDomain);
         }
 
         public void InjectDependencies(CrystalManager cm) => CrystalManager = cm;
```

</details>
