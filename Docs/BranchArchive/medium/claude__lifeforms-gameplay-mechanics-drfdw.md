# Branch archive: `claude/lifeforms-gameplay-mechanics-drfdw`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-04-29 by Claude
- **Unmerged commits:** 6
- **Forked from:** `807d502fb` (2026-04-30, Merge branch 'bleeding-edge' into claude/fix-squirrel-stats-display-Il9I9)
- **Tip:** `3a56165cf`
- **Files touched (15):**
  - `Assets/Plugins/FMOD.meta`
  - `Assets/Plugins/FMOD/platforms.meta`
  - `Assets/Plugins/FMOD/platforms/win.meta`
  - `Assets/Plugins/FMOD/platforms/win/lib.meta`
  - `Assets/Plugins/FMOD/platforms/win/lib/x86_64.meta`
  - `Assets/Plugins/FMOD/platforms/win/lib/x86_64/fmodstudioL.dll`
  - `Assets/Plugins/FMOD/platforms/win/lib/x86_64/fmodstudioL.dll.meta`
  - `Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/HealthBlockTracker.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFaunaManager.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFaunaManagerDataSO.cs`
  - `Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs`

### `1666d2661` — feat(cell): cap live prism count with FloraGrowingEnabled gate

_Claude, 2026-04-29 19:03:02 +0000_

```text
Menu_Main / Freestyle ran the autopilots indefinitely and the gyroid
flora kept growing until prism counts overwhelmed the renderer. Two
root causes plus a missing global cap:

1) AssembledFlora.spawnedItemCount was never incremented, so the
   per-flora maxTotalSpawnedObjects=750 cap was dead code. Each flora
   grew without bound.
2) GyroidAssembler.GetGrowthInfo sized its Physics.CheckBox overlap
   probe with `Prism.transform.localScale / 2f`. Prism.PrismScaleAnimator
   sets transform.localScale to Vector3.zero in Awake and grows toward
   the authored target, so the box was near-zero for the entire grow-in
   window and missed real overlaps. Newly-spawned siblings also have
   their collider disabled for the 0.6s of CreateBlockCoroutine waitTime,
   so they were invisible to CheckBox even when sized correctly. Result:
   prisms stacked on top of each other.
3) Nothing tracked total prism count across multiple flora.
   IntensityWiseLifeSpawner reseeded a fresh MassGyroidFlora every 30s
   regardless of how many existed.

Fix:

Cell now tracks LiveBlockCount via a HashSet<Prism> populated by
AddBlock/RemoveBlock with dedup. A serialized maxLiveBlocks cap (1500
default) plus a resumeGrowingBelow hysteresis floor (1300 default)
drive Cell.FloraGrowingEnabled, which flips false when over budget and
true when consumption brings the count back down. AddBlock/RemoveBlock
use `block is null` rather than `!block` so destroyed Unity refs still
decrement the count via the matching path.

HealthBlockTracker takes an optional Cell reference and forwards
HashSet add/remove transitions to Cell.AddBlock/RemoveBlock, so dedup
stays at the LifeForm boundary and Cell.LiveBlockCount counts unique
prisms. CleanupDeadRefs forwards each dead ref to the cell before
discarding, so destroyed prisms that bypass the normal Damage path
(scene unload, parent destruction) don't leak the count upward.
LifeForm passes its bound Cell into the tracker and also drains
healthTracker.All into cell.RemoveBlock from HandleTurnEnded, since the
turn-end path Destroys gameObjects without running Die().

AssembledFlora.Grow now increments spawnedItemCount per spawn (the
broken cap from before, now functional) and early-returns when
cell.FloraGrowingEnabled is false. IntensityWiseLifeSpawner.SpawnFloraTypeLoop
skips the SpawnFlora call (but keeps looping) when the cell is over
budget, so existing flora keep their state and reseeding resumes
naturally once consumption clears the floor.

GyroidAssembler.GetGrowthInfo now sizes the CheckBox by Prism.TargetScale
(authored size from PrismScaleAnimator) plus a 0.25-unit pad. The pad
is intentional: it covers the ~0.6s window where a freshly-spawned
sibling has its collider disabled and is invisible to the physics
probe, so two children spawned in the same growth tick can't both
claim adjacent positions before either becomes visible.

No data-file or fauna-behavior changes. The previous attempt on
claude/fix-gyroid-overflow-crash-LoOGN took a much wider blast radius
(staggered aggression levels with hysteresis, fauna pack offsets, data
asset rewires, shield material timing) and surfaced wiring fragility.
This change keeps the surface narrow: five files, one knob
(maxLiveBlocks) on the Cell prefab.
```

```text
 Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs              | 13 ++++++--
 Assets/_Scripts/Controller/Environment/Cell.cs                        | 56 +++++++++++++++++++++++++++++----
 .../_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs   |  7 +++++
 .../Controller/Environment/FloraAndFauna/HealthBlockTracker.cs        | 29 ++++++++++++++---
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs      | 11 +++++--
 Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs    |  5 +++
 6 files changed, 106 insertions(+), 15 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 269 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs b/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
index 8b8a1b9dd..8633f675a 100644
--- a/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
+++ b/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
@@ -116,15 +116,22 @@ namespace CosmicShore.Gameplay
                 var newPosition = CalculateGlobalBondSite(bondMateData.Substrate);
                 var newRotation = CalculateRotation(CreateGyroidBondMate(this, BlockType, growthSite));
 
-                // Check if there is already a block at the new position using Physics.CheckBox
-                if (!Physics.CheckBox(newPosition, Prism.transform.localScale / 2f))
+                // Use TargetScale (authored size) instead of transform.localScale, which is
+                // the *current* animating scale and starts at Vector3.zero in
+                // PrismScaleAnimator.Awake. Using localScale meant the parent was checking
+                // a near-zero box for the entire grow-in window and missed real overlaps,
+                // so siblings were stacking on top of each other. Pad by 0.25 to also cover
+                // the ~0.6s window where a freshly-spawned sibling has its collider disabled
+                // (Prism.CreateBlockCoroutine waitTime) and is invisible to Physics.CheckBox.
+                Vector3 checkHalfExtents = Prism.TargetScale * 0.5f + Vector3.one * 0.25f;
+                if (!Physics.CheckBox(newPosition, checkHalfExtents))
                     return new GyroidGrowthInfo
                     {
                         CanGrow = true,
                         Position = newPosition,
                         Rotation = newRotation,
                         BlockType = bondMateData.BlockType,
-                        IsDangerous = 
+                        IsDangerous =
                             bondMateData.BlockType == GyroidBlockType.GEs ||
                             bondMateData.BlockType == GyroidBlockType.DE ||
                             bondMateData.BlockType == GyroidBlockType.EG ||
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 29888f655..d1d022aef 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -26,6 +26,12 @@ namespace CosmicShore.Gameplay
 
         [SerializeField] float nucleusScaleMultiplier = 1f;
 
+        [Header("Prism Population Control")]
+        [Tooltip("Hard cap on live tracked prisms in this cell. When exceeded, FloraGrowingEnabled flips false so flora stops adding more growth and IntensityWiseLifeSpawner stops seeding new plants.")]
+        [SerializeField] int maxLiveBlocks = 1500;
+        [Tooltip("Hysteresis floor. Flora resumes growing once consumption brings the count back down to this number.")]
+        [SerializeField] int resumeGrowingBelow = 1300;
+
 
         CellConfigDataSO cellConfigData => runtime ? runtime.Config : null;
         GameObject membrane;
@@ -48,6 +54,13 @@ namespace CosmicShore.Gameplay
         readonly Dictionary<Domains, float> teamVolumes = new();
 
         readonly List<GameObject> spawnedLifeForms = new();
+        readonly HashSet<Prism> trackedBlocks = new();
+        bool floraGrowingEnabled = true;
+
+        public int LiveBlockCount => trackedBlocks.Count;
+        public int MaxLiveBlocks => maxLiveBlocks;
+        public bool FloraGrowingEnabled => floraGrowingEnabled;
+
         SnowChanger spawnedCytoplasm;
 
         readonly ICellLifeSpawner intensitySpawner = new IntensityWiseLifeSpawner();
@@ -124,6 +137,8 @@ namespace CosmicShore.Gameplay
                 if (spawnedLifeForms[i]) Destroy(spawnedLifeForms[i]);
             }
             spawnedLifeForms.Clear();
+            trackedBlocks.Clear();
+            floraGrowingEnabled = true;
 
             if (spawnedCytoplasm)
             {
@@ -177,6 +192,8 @@ namespace CosmicShore.Gameplay
         void Initialize()
         {
             spawnedLifeForms.Clear();
+            trackedBlocks.Clear();
+            floraGrowingEnabled = true;
 
             // Bind runtime -> this cell
             runtime.Cell = this;
@@ -309,16 +326,43 @@ namespace CosmicShore.Gameplay
 
         public void AddBlock(Prism block)
         {
-            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
-            foreach (var t in teams)
-                if (t != block.Domain) countGrids[t].AddBlock(block);
+            // Use C# null check (`is null`) rather than `!block` so destroyed Unity refs
+            // can still be removed from trackedBlocks via the matching RemoveBlock path.
+            if (block is null) return;
+            if (!trackedBlocks.Add(block)) return; // already counted
+
+            if (block)
+            {
+                Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
+                foreach (var t in teams)
+                    if (t != block.Domain) countGrids[t].AddBlock(block);
+            }
+
+            UpdateGrowGate();
         }
 
         public void RemoveBlock(Prism block)
         {
-            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
-            foreach (Domains t in teams)
-                if (t != block.Domain) countGrids[t].RemoveBlock(block);
+            if (block is null) return;
+            if (!trackedBlocks.Remove(block)) return; // not counted
+
+            if (block)
+            {
+                Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
+                foreach (Domains t in teams)
+                    if (t != block.Domain) countGrids[t].RemoveBlock(block);
+            }
+
+            UpdateGrowGate();
+        }
+
+        void UpdateGrowGate()
+        {
+            int count = trackedBlocks.Count;
+            if (floraGrowingEnabled && count >= maxLiveBlocks)
+                floraGrowingEnabled = false;
+            else if (!floraGrowingEnabled && count <= resumeGrowingBelow)
+                floraGrowingEnabled = true;
         }
 
         public Vector3 GetExplosionTarget(Domains domain) => countGrids[domain].FindDensestRegion();
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
index b6cbef030..e3a83398b 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
@@ -80,6 +80,12 @@ namespace CosmicShore.Gameplay
         {
             if (spawnedItemCount >= maxTotalSpawnedObjects) return;
 
+            // Cell-level prism budget. When the cell is over its prism cap
+            // (Cell.maxLiveBlocks), pause growth until consumption brings the count
+            // back below the hysteresis floor (Cell.resumeGrowingBelow). This keeps
+            // the gyroid flora from snowballing past what the renderer can sustain.
+            if (cell && !cell.FloraGrowingEnabled) return;
+
             List<Branch> newBranches = new List<Branch>();
             List<Branch> branchesToRemove = new List<Branch>();
 
@@ -136,6 +142,7 @@ namespace CosmicShore.Gameplay
 
                 newBranches.Add(newBranch);
                 itemsSpawned++;
```

</details>

### `09a59e678` — fix(flora): make per-flora cap fire and stop gyroid self-overlap

_Claude, 2026-04-29 19:50:55 +0000_

```text
Two bugs that prevented the existing flora caps and overlap probe from
working as intended. Both are fixes to existing behavior, no new
parameters or systems.

AssembledFlora.spawnedItemCount was declared and read in the maxTotalSpawnedObjects
guard at the top of Grow() but never incremented anywhere in the file, so
the per-flora cap was dead code and each AssembledFlora grew unbounded.
Increment it on every successful spawn.

GyroidAssembler.GetGrowthInfo sized its Physics.CheckBox overlap probe
with `Prism.transform.localScale / 2f`. PrismScaleAnimator.Awake sets
transform.localScale to Vector3.zero and grows toward the authored target
over time, so the probe was near-zero for the entire grow-in window and
missed real overlaps — siblings stacked on top of each other. Use
`Prism.TargetScale` (the authored size from PrismScaleAnimator) instead.
Pad by 0.25 to also cover the ~0.6s window where a freshly-spawned sibling
has its collider disabled (Prism.CreateBlockCoroutine waitTime) and is
invisible to Physics.CheckBox; without the pad two children spawned in
the same growth tick can both claim adjacent positions before either
becomes visible to the probe.
```

```text
 Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs               | 13 ++++++++++---
 Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs |  1 +
 2 files changed, 11 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs b/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
index 8b8a1b9dd..fc35b51d9 100644
--- a/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
+++ b/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs
@@ -116,15 +116,22 @@ namespace CosmicShore.Gameplay
                 var newPosition = CalculateGlobalBondSite(bondMateData.Substrate);
                 var newRotation = CalculateRotation(CreateGyroidBondMate(this, BlockType, growthSite));
 
-                // Check if there is already a block at the new position using Physics.CheckBox
-                if (!Physics.CheckBox(newPosition, Prism.transform.localScale / 2f))
+                // Use Prism.TargetScale (authored size from PrismScaleAnimator) instead of
+                // transform.localScale, which is the *current* animating value and starts at
+                // Vector3.zero in PrismScaleAnimator.Awake. With localScale the probe was
+                // near-zero through the entire grow-in window and missed real overlaps, so
+                // siblings stacked on top of each other. Pad by 0.25 so the ~0.6s window
+                // where a freshly-spawned sibling has its collider disabled (Prism.CreateBlockCoroutine
+                // waitTime) doesn't slip overlapping children past Physics.CheckBox.
+                Vector3 checkHalfExtents = Prism.TargetScale * 0.5f + Vector3.one * 0.25f;
+                if (!Physics.CheckBox(newPosition, checkHalfExtents))
                     return new GyroidGrowthInfo
                     {
                         CanGrow = true,
                         Position = newPosition,
                         Rotation = newRotation,
                         BlockType = bondMateData.BlockType,
-                        IsDangerous = 
+                        IsDangerous =
                             bondMateData.BlockType == GyroidBlockType.GEs ||
                             bondMateData.BlockType == GyroidBlockType.DE ||
                             bondMateData.BlockType == GyroidBlockType.EG ||
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
index b6cbef030..853e8423b 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
@@ -136,6 +136,7 @@ namespace CosmicShore.Gameplay
 
                 newBranches.Add(newBranch);
                 itemsSpawned++;
+                spawnedItemCount++;
 
                 if (branch.depth >= maxDepth - 1 || branch.assembler.IsFullyBonded())
                 {
```

</details>

### `a899c7b2b` — feat(fauna): scale fauna replenishment with cell prism load

_Claude, 2026-04-29 19:54:07 +0000_

```text
Lifeforms control prism count emergently — fauna eat prisms, so a cell
with more prisms (more food) should sustain more fauna, which keeps
consumption in pace with growth. Currently LightFaunaManager.spawnCount
is fixed, so once flora growth outruns the static fauna count there is
nothing in the system that closes the loop. The earlier maxLiveBlocks
cap on Cell tried to close it bespoke; this routes the response through
the Fauna fundamental instead.

Cell now tracks unique prisms via a HashSet<Prism> populated through
Add/RemoveBlock with dedup, exposing LiveBlockCount as a read-only
signal. AddBlock/RemoveBlock use `block is null` rather than `!block`
so destroyed-but-non-null Unity refs still decrement the counter via
the matching path; otherwise LiveBlockCount drifts upward when prisms
are destroyed outside the normal Damage flow.

HealthBlockTracker takes an optional Cell ref and forwards HashSet
add/remove transitions (and dead-ref cleanup) to Cell.AddBlock/RemoveBlock,
keeping dedup at the LifeForm boundary so Cell.LiveBlockCount counts
unique prisms across all lifeforms in the cell. LifeForm passes its
bound Cell into the tracker constructor and drains healthTracker.All
into cell.RemoveBlock from HandleTurnEnded — turn-end Destroys the
gameObject without running Die()/RemoveHealthBlock, so without the
drain Cell.LiveBlockCount drifts upward across rounds.

LightFaunaManager.SpawnGroup and the RemoveFauna replenishment trigger
both go through ComputeBatchSize(), which adds extra fauna per 100
prisms in the host cell. The new SO field
LightFaunaManagerDataSO.extraFaunaPerHundredPrisms tunes the response
slope (default 4 — a 500-prism cell will run with ~30 fauna instead of
the static 10, four times the consumption capacity). A second SO field
maxFaunaPerGroup caps the population if a designer needs a hard ceiling
on total spawn count per manager (0 = no cap, the default).

Default behavior with extraFaunaPerHundredPrisms=4 stays close to the
old static behavior in low-load cells (LiveBlockCount < 25 → 0 extra)
and only kicks in once growth pressure is real, so this is additive on
top of what designers already tune via spawnCount.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                        | 37 +++++++++++++++++++++++++++------
 .../Controller/Environment/FloraAndFauna/HealthBlockTracker.cs        | 29 ++++++++++++++++++++++----
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs      | 12 +++++++++--
 .../Controller/Environment/FloraAndFauna/LightFaunaManager.cs         | 33 +++++++++++++++++++++++++++--
 .../Controller/Environment/FloraAndFauna/LightFaunaManagerDataSO.cs   |  9 +++++++-
 5 files changed, 105 insertions(+), 15 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 249 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index 29888f655..842e821c4 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -48,8 +48,16 @@ namespace CosmicShore.Gameplay
         readonly Dictionary<Domains, float> teamVolumes = new();
 
         readonly List<GameObject> spawnedLifeForms = new();
+        readonly HashSet<Prism> trackedBlocks = new();
         SnowChanger spawnedCytoplasm;
 
+        /// <summary>
+        /// Live count of unique prisms tracked through Add/RemoveBlock. Read-only signal
+        /// for systems that respond to prism load (e.g., LightFaunaManager scales its
+        /// fauna population with this so consumption keeps pace with growth).
+        /// </summary>
+        public int LiveBlockCount => trackedBlocks.Count;
+
         readonly ICellLifeSpawner intensitySpawner = new IntensityWiseLifeSpawner();
         readonly ICellLifeSpawner randomSpawner = new RandomLifeSpawner();
         ICellLifeSpawner activeSpawner;
@@ -124,6 +132,7 @@ namespace CosmicShore.Gameplay
                 if (spawnedLifeForms[i]) Destroy(spawnedLifeForms[i]);
             }
             spawnedLifeForms.Clear();
+            trackedBlocks.Clear();
 
             if (spawnedCytoplasm)
             {
@@ -177,6 +186,7 @@ namespace CosmicShore.Gameplay
         void Initialize()
         {
             spawnedLifeForms.Clear();
+            trackedBlocks.Clear();
 
             // Bind runtime -> this cell
             runtime.Cell = this;
@@ -309,16 +319,31 @@ namespace CosmicShore.Gameplay
 
         public void AddBlock(Prism block)
         {
-            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
-            foreach (var t in teams)
-                if (t != block.Domain) countGrids[t].AddBlock(block);
+            // `is null` (not `!block`) so destroyed-but-non-null Unity refs can still be
+            // removed from trackedBlocks via the matching RemoveBlock path; otherwise
+            // LiveBlockCount drifts upward when prisms die outside the normal flow.
+            if (block is null) return;
+            if (!trackedBlocks.Add(block)) return; // already counted
+
+            if (block)
+            {
+                Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
+                foreach (var t in teams)
+                    if (t != block.Domain) countGrids[t].AddBlock(block);
+            }
         }
 
         public void RemoveBlock(Prism block)
         {
-            Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
-            foreach (Domains t in teams)
-                if (t != block.Domain) countGrids[t].RemoveBlock(block);
+            if (block is null) return;
+            if (!trackedBlocks.Remove(block)) return; // not counted
+
+            if (block)
+            {
+                Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
+                foreach (Domains t in teams)
+                    if (t != block.Domain) countGrids[t].RemoveBlock(block);
+            }
         }
 
         public Vector3 GetExplosionTarget(Domains domain) => countGrids[domain].FindDensestRegion();
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/HealthBlockTracker.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/HealthBlockTracker.cs
index ce9bfe332..088bcae89 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/HealthBlockTracker.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/HealthBlockTracker.cs
@@ -14,21 +14,26 @@ namespace CosmicShore.Gameplay
         readonly HashSet<HealthPrism> healthBlocks = new();
         readonly int healthBlocksForMaturity;
         readonly int minHealthBlocks;
+        readonly Cell cell;
 
         public int Count => healthBlocks.Count;
         public bool IsMature { get; private set; }
 
 
-        public HealthBlockTracker(int healthBlocksForMaturity, int minHealthBlocks)
+        public HealthBlockTracker(int healthBlocksForMaturity, int minHealthBlocks, Cell cell = null)
         {
             this.healthBlocksForMaturity = healthBlocksForMaturity;
             this.minHealthBlocks = minHealthBlocks;
+            this.cell = cell;
         }
 
         public void Add(HealthPrism hp, LifeForm owner, Domains domain)
         {
             if (!hp) return;
-            healthBlocks.Add(hp);
+            // HashSet.Add returns true only on a new entry, so forward only once per prism
+            // and Cell.LiveBlockCount counts unique prisms (not double-counted re-adds).
+            if (healthBlocks.Add(hp) && cell)
+                cell.AddBlock(hp);
             hp.ChangeTeam(domain);
             hp.LifeForm = owner;
             hp.ownerID = $"{owner} + {hp} + {healthBlocks.Count}";
@@ -38,7 +43,8 @@ namespace CosmicShore.Gameplay
         public void Remove(HealthPrism hp, string killerName = "")
         {
             if (!hp) return;
-            healthBlocks.Remove(hp);
+            if (healthBlocks.Remove(hp) && cell)
+                cell.RemoveBlock(hp);
             CleanupDeadRefs();
         }
 
@@ -50,7 +56,22 @@ namespace CosmicShore.Gameplay
 
         public void CleanupDeadRefs()
         {
-            healthBlocks.RemoveWhere(h => !h);
+            // Forward each dead ref to the cell before discarding so Cell.LiveBlockCount
+            // doesn't drift upward when prisms die outside the normal Damage path
+            // (scene unload, parent destruction, AOE chains that bypass HealthPrism).
+            if (cell != null)
+            {
+                healthBlocks.RemoveWhere(h =>
+                {
+                    if (h) return false;
+                    cell.RemoveBlock(h);
+                    return true;
+                });
+            }
+            else
+            {
+                healthBlocks.RemoveWhere(h => !h);
+            }
         }
 
         public void SetTeam(Domains domain)
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs
index 4db4c3e5f..8b2de60f9 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs
@@ -87,11 +87,13 @@ namespace CosmicShore.Gameplay
             if (initialized) return;
             initialized = true;
 
```

</details>

### `f6297a89d` — fmod stuff

_Garrett Milliron, 2026-04-29 16:00:30 -0400_

```text
 Assets/Plugins/FMOD.meta                                          |   8 ++++++++
 Assets/Plugins/FMOD/platforms.meta                                |   8 ++++++++
 Assets/Plugins/FMOD/platforms/win.meta                            |   8 ++++++++
 Assets/Plugins/FMOD/platforms/win/lib.meta                        |   8 ++++++++
 Assets/Plugins/FMOD/platforms/win/lib/x86_64.meta                 |   8 ++++++++
 Assets/Plugins/FMOD/platforms/win/lib/x86_64/fmodstudioL.dll      | Bin 0 -> 4369920 bytes
 Assets/Plugins/FMOD/platforms/win/lib/x86_64/fmodstudioL.dll.meta |   2 ++
 7 files changed, 42 insertions(+)
```

### `3a56165cf` — fix(flora): drop dead FloraGrowingEnabled gates resurrected by merge

_Claude, 2026-04-29 20:40:17 +0000_

```text
The merge commit 473217f1 ("merge") was rooted on top of 1666d266 — the
earlier kitchen-sink commit that I had force-pushed away from before
re-doing the work as two clean commits. Because nothing in the new tip
touched the lines around the gate insertions in AssembledFlora.cs and
IntensityWiseLifeSpawner.cs, the merge resolved without conflict and
silently took 1666d266's added lines.

The added gates reference Cell.FloraGrowingEnabled, which doesn't exist
on Cell anywhere in the merged tree (Cell.cs has the new clean
LiveBlockCount tracking from feat(fauna), no hysteresis property), so
the merge tip wouldn't compile (CS0117 on two call sites).

Drop the 11 dead lines. The intended emergent regulation now flows
through LightFaunaManager.ComputeBatchSize reading Cell.LiveBlockCount
(see a899c7b2), not a hard gate on flora growth.
```

```text
 Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs | 6 ------
 Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs     | 5 -----
 2 files changed, 11 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
index e3a83398b..853e8423b 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
@@ -80,12 +80,6 @@ namespace CosmicShore.Gameplay
         {
             if (spawnedItemCount >= maxTotalSpawnedObjects) return;
 
-            // Cell-level prism budget. When the cell is over its prism cap
-            // (Cell.maxLiveBlocks), pause growth until consumption brings the count
-            // back below the hysteresis floor (Cell.resumeGrowingBelow). This keeps
-            // the gyroid flora from snowballing past what the renderer can sustain.
-            if (cell && !cell.FloraGrowingEnabled) return;
-
             List<Branch> newBranches = new List<Branch>();
             List<Branch> branchesToRemove = new List<Branch>();
 
diff --git a/Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs b/Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs
index 628b49dff..ad0481645 100644
--- a/Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs
+++ b/Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs
@@ -87,11 +87,6 @@ namespace CosmicShore.Gameplay
                 else
                     yield return null;
 
-                // Don't seed a fresh plant when the cell is already over its prism budget.
-                // Existing flora keep their state and resume growing once consumption
-                // (fauna, vessels) brings Cell.LiveBlockCount back below the hysteresis floor.
-                if (host && !host.FloraGrowingEnabled) continue;
-
                 SpawnFlora(host, floraCfg.FloraPrefab, excluded);
             }
         }
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
