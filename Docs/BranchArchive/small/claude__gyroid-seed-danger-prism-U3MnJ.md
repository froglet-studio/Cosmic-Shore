# Branch archive: `claude/gyroid-seed-danger-prism-U3MnJ`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-01 by Claude
- **Unmerged commits:** 3
- **Forked from:** `1a310a189` (2026-03-31, Update Menu_Main.unity)
- **Tip:** `be54155d9`
- **Files touched (2):**
  - `Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs`

### `83f15b5a5` — feat(gyroid): seed octagon danger prisms as individual flora per octagon

_Claude, 2026-04-01 18:19:22 +0000_

```text
Each octagon in the gyroid surface now spawns its own AssembledFlora
with a crystal at the octagon center, instead of all prisms belonging
to one large flora. When a danger prism type (GEs/DE/EG/EsD) is grown:

1. Calculate the octagon center from the prism's bond site geometry
2. Search for an existing crystal/flora near that center
3. If found, the prism joins that existing flora as a health block
4. If not found, instantiate a new gyroid flora prefab at the center
   with a crystal at local (0,0,0), and adopt this prism as its first
   health block

GyroidAssembler changes:
- Add IsOctagonDangerType() static helper
- Add TryAttachToOrCreateFlora() for seed danger prisms in Start()
- Add CalculateOctagonCenter() using bond site centroid
- Add FindNearbyAssembledFlora() crystal proximity search
- Add octagonCrystalDetectionRadius serialized field (default 15)

AssembledFlora changes:
- Add gyroidFloraPrefab serialized field for self-referencing prefab
- Route danger prism growth through SpawnOctagonDangerPrism()
- Add CreateOctagonFlora() to instantiate new flora at octagon center
- Add AdoptPrismIntoFlora() to handle spindle/health block setup
- Add AddActiveBranch() so adopted prisms continue growing
```

```text
 Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs              | 111 +++++++++++++++++++++++++-
 .../_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs   | 135 +++++++++++++++++++++++++++++++-
 2 files changed, 242 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 314 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs b/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
index 8b8a1b9dd..371774645 100644
--- a/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
+++ b/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
@@ -61,8 +61,8 @@ namespace CosmicShore.Gameplay
 
         public GyroidBlockType BlockType = GyroidBlockType.AB;
         public int depth = -1;
-        
-        
+
+
         public override int Depth
         {
             get => depth;
@@ -75,9 +75,27 @@ namespace CosmicShore.Gameplay
         [SerializeField] int colliderTheshold = 1;
         [SerializeField] float radius = 40f;
 
+        [Header("Octagon Flora Seeding")]
+        [Tooltip("Radius to search for an existing crystal when seeding an octagon danger prism")]
+        [SerializeField] float octagonCrystalDetectionRadius = 15f;
+
+        public float OctagonCrystalDetectionRadius => octagonCrystalDetectionRadius;
+
         private const int MaxColliders = 10; // This one only detects 10 collider at the same frame
         private readonly Collider[] _colliders = new Collider[MaxColliders];
 
+        /// <summary>
+        /// Returns true if the given block type is one of the 4 octagon danger prism types.
+        /// These types form the octagonal openings in the gyroid surface.
+        /// </summary>
+        public static bool IsOctagonDangerType(GyroidBlockType blockType)
+        {
+            return blockType == GyroidBlockType.GEs
+                || blockType == GyroidBlockType.DE
+                || blockType == GyroidBlockType.EG
+                || blockType == GyroidBlockType.EsD;
+        }
+
         void Start()
         {
             Prism = GetComponent<Prism>();
@@ -87,8 +105,97 @@ namespace CosmicShore.Gameplay
                 if (isSeed)
                 {
                     Prism.Domain = Domains.Blue;
+                    if (IsOctagonDangerType(BlockType))
+                    {
+                        TryAttachToOrCreateFlora();
+                    }
+                }
+            }
+        }
+
+        /// <summary>
+        /// For octagon danger prisms: checks if a crystal/flora exists near the octagon center.
+        /// If found, attaches this prism to that flora. If not, creates a new gyroid flora
+        /// with a crystal at the octagon center, making this prism its first health block.
+        /// </summary>
+        void TryAttachToOrCreateFlora()
+        {
+            var healthPrism = GetComponent<HealthPrism>();
+            if (!healthPrism) return;
+
+            // Calculate the octagon center: the centroid of this prism's 4 bond sites in world space
+            Vector3 octagonCenter = CalculateOctagonCenter();
+
+            // Search for an existing flora crystal near the octagon center
+            var existingFlora = FindNearbyAssembledFlora(octagonCenter, octagonCrystalDetectionRadius);
+            if (existingFlora != null)
+            {
+                // Join the existing flora
+                healthPrism.LifeForm = existingFlora;
+                existingFlora.AddHealthBlock(healthPrism);
+                return;
+            }
+
+            // No existing flora found — the parent AssembledFlora.Grow() handles creation
+            // of new flora via CreateOctagonFlora(). When called from Start() on a seed,
+            // we signal that this prism needs its own flora by storing the center position.
+            OctagonCenterPosition = octagonCenter;
+            NeedsOctagonFlora = true;
+        }
+
+        /// <summary>
+        /// When true, this danger prism needs a new octagon flora created for it.
+        /// Set by TryAttachToOrCreateFlora() when no existing flora is found.
+        /// Read by AssembledFlora to trigger flora creation.
+        /// </summary>
+        [HideInInspector] public bool NeedsOctagonFlora;
+
+        /// <summary>
+        /// The calculated octagon center position in world space.
+        /// </summary>
+        [HideInInspector] public Vector3 OctagonCenterPosition;
+
+        /// <summary>
+        /// Calculates the approximate octagon center by averaging the 4 bond site
+        /// positions in world space. The crystal should be placed at this position.
+        /// </summary>
+        Vector3 CalculateOctagonCenter()
+        {
+            Vector3 sum = Vector3.zero;
+            int count = 0;
+
+            foreach (CornerSiteType site in new[] {
+                CornerSiteType.TopLeft, CornerSiteType.TopRight,
+                CornerSiteType.BottomLeft, CornerSiteType.BottomRight })
+            {
+                if (GyroidBondMateDataContainer.BondMateDataMap.TryGetValue((BlockType, site), out var data))
+                {
+                    Vector3 localBondSite = data.DeltaPosition * separationDistance;
+                    sum += transform.TransformPoint(localBondSite);
+                    count++;
+                }
+            }
+
+            return count > 0 ? sum / count : transform.position;
+        }
+
+        /// <summary>
+        /// Searches for an existing AssembledFlora whose crystal is near the given position.
+        /// </summary>
+        static AssembledFlora FindNearbyAssembledFlora(Vector3 position, float detectionRadius)
+        {
+            var hits = Physics.OverlapSphere(position, detectionRadius);
+            foreach (var hit in hits)
+            {
+                var crystal = hit.GetComponent<Crystal>();
+                if (crystal != null)
+                {
+                    var flora = crystal.GetComponentInParent<AssembledFlora>();
+                    if (flora != null)
+                        return flora;
                 }
             }
+            return null;
         }
 
         public override void StartBonding()
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
index b6cbef030..ce2b6a25b 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
@@ -35,7 +35,7 @@ namespace CosmicShore.Gameplay
                 assembler = healthPrism.GetComponent<Assembler>();
             }
         }
-        
```

</details>

### `b2f9b456b` — fix(gyroid): make AddActiveBranch private to match Branch struct visibility

_Claude, 2026-04-01 19:08:27 +0000_

```text
 Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
index ce2b6a25b..f2c753cb3 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
@@ -325,7 +325,7 @@ namespace CosmicShore.Gameplay
         /// Adds a branch to the active branch set. Used by octagon flora adoption
         /// to register danger prisms as growth points in the target flora.
         /// </summary>
-        public void AddActiveBranch(Branch branch)
+        void AddActiveBranch(Branch branch)
         {
             activeBranches.Add(branch);
         }
```

</details>

### `be54155d9` — fix(gyroid): make FindNearbyAssembledFlora public for cross-class access

_Claude, 2026-04-01 19:11:20 +0000_

```text
 Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs b/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
index 371774645..64ad6205d 100644
--- a/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
+++ b/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
@@ -182,7 +182,7 @@ namespace CosmicShore.Gameplay
         /// <summary>
         /// Searches for an existing AssembledFlora whose crystal is near the given position.
         /// </summary>
-        static AssembledFlora FindNearbyAssembledFlora(Vector3 position, float detectionRadius)
+        public static AssembledFlora FindNearbyAssembledFlora(Vector3 position, float detectionRadius)
         {
             var hits = Physics.OverlapSphere(position, detectionRadius);
             foreach (var hit in hits)
```

</details>
