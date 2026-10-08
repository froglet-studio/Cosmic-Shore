# Branch archive: `claude/extend-ai-training-duration-vfMGG`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Evolutionary AI pilot training (overnight runs)**

Built an evolutionary AI training system: AI pilots have a 'genome' of tuning values, a population competes, fitness is tracked, and the best genomes are kept, with unattended overnight training and a play-along mode. It also improved AI flying in HexRace (prism skimming, collision prediction, crystal targeting by team). Same commits as claude/ai-pilot-intensity-levels-XK6ST minus its final hardening commit.

- **Status:** Redone elsewhere
- **Areas:** AI opponents, AI training/evolution, HexRace
- **Already in bleeding-edge:** Concept landed in a different form: bleeding-edge commits 'feat(ai): train a Skim Race Squirrel intensity-4 genome', 'feat(port): population tournament eval; document 12-generation training result', and Docs/AI_TRAINING_CONSOLIDATION.md; the Unity-side PilotGenome/PilotEvolution/AITrainingController classes do not exist.
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — Strict subset of claude/ai-pilot-intensity-levels-XK6ST, and AI genome training was redone (Port + Skim Race pipeline).

## Evidence

- **Last commit:** 2026-02-25 by Claude
- **Unmerged commits:** 21
- **Forked from:** `65ea946e7` (2026-02-18, Fix Main Menu Mouse Hover Issue)
- **Tip:** `5a87f0adb`
- **Files touched (20):**
  - `Assets/_Prefabs/Spaceships/Squirrel.prefab`
  - `Assets/_SO_Assets/Tools.meta`
  - `Assets/_SO_Assets/Tools/PilotEvolution.asset`
  - `Assets/_SO_Assets/Tools/PilotEvolution.asset.meta`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameHexRace.unity`
  - `Assets/_Scripts/Game/AI/AIPilot.cs`
  - `Assets/_Scripts/Game/AI/AITrainingController.cs`
  - `Assets/_Scripts/Game/AI/AITrainingController.cs.meta`
  - `Assets/_Scripts/Game/AI/PilotEvolution.cs`
  - `Assets/_Scripts/Game/AI/PilotEvolution.cs.meta`
  - `Assets/_Scripts/Game/AI/PilotFitnessTracker.cs`
  - `Assets/_Scripts/Game/AI/PilotFitnessTracker.cs.meta`
  - `Assets/_Scripts/Game/AI/PilotGenome.cs`
  - `Assets/_Scripts/Game/AI/PilotGenome.cs.meta`
  - `Assets/_Scripts/Game/AI/PlayAlongTrainingController.cs`
  - `Assets/_Scripts/Game/AI/PlayAlongTrainingController.cs.meta`
  - `Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Vessel Prism Effects/VesselResetBoostPrismEffectSO.cs`
  - `Assets/_Scripts/Game/Player/TrainingPlayerSpawnerAdapter.cs`
  - `Assets/_Scripts/Game/Player/TrainingPlayerSpawnerAdapter.cs.meta`

### `a1990b17b` — Add intensity-scaled AI pilot behavior for hexrace prism skimming

_Claude, 2026-02-18 22:42:56 +0000_

```text
Intensity 1: Unchanged crystal-only seeking (current baseline behavior)
Intensity 2+: AI scans for nearby prisms on TrailBlocks layer via OverlapSphere,
blends between prism-seeking and crystal-seeking based on distance to crystal
and current boost level. Forward raycasts provide collision avoidance.
Intensity 3+: Trail-alignment scoring prefers prism lines to follow, peripheral
rays improve obstacle awareness.
Intensity 4: Full diagonal raycast coverage for tight spaces, widest scan cone,
fastest scan rate, most aggressive prism-seeking when boost is low.

Crystal bias increases when close to crystal or near max boost.
Prism bias increases when far from crystal and low on boost.
Removes unused CornerBehaviors/AvoidanceBehavior dead code.
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs | 373 ++++++++++++++++++++++++++++++++++++++++++++++---------------------
 1 file changed, 255 insertions(+), 118 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 493 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index a4b0cc095..387a3ae45 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -19,7 +19,7 @@ namespace CosmicShore.Game.AI
     {
         [SerializeField]
         CellRuntimeDataSO cellData;
-        
+
         [SerializeField] float skillLevel = 1;
 
         [SerializeField] float defaultThrottleHigh = .6f;
@@ -61,13 +61,17 @@ namespace CosmicShore.Game.AI
 
         [SerializeField] private ActionExecutorRegistry actionExecutorRegistry;
 
-        enum Corner 
-        {
-            TopRight,
-            BottomRight,
-            BottomLeft,
-            TopLeft,
-        };
+        [Header("Intensity Scaling")]
+        [SerializeField] IntVariable selectedIntensity;
+
+        [Header("Prism Seeking")]
+        [SerializeField] float prismDetectionRadius = 120f;
+        [SerializeField] float collisionAvoidanceDistance = 30f;
+
+        int Intensity => selectedIntensity != null ? Mathf.Clamp(selectedIntensity.Value, 1, 4) : 1;
+
+        // Intensity-derived parameters (0 = no effect at intensity 1, 1 = full effect at intensity 4)
+        float IntensityT => (Intensity - 1) / 3f;
 
         IVessel vessel;
         IVesselStatus VesselStatus => vessel.VesselStatus;
@@ -81,31 +85,18 @@ namespace CosmicShore.Game.AI
         float _maxDistance = 50f;
         float _maxDistanceSquared;
 
+        Vector3 _crystalTargetPosition;
         Vector3 _targetPosition;
         Vector3 _distance;
         bool LookingAtCrystal;
 
-        Dictionary<Corner, AvoidanceBehavior> CornerBehaviors;
-
-        #region Avoidance Stuff
-        const float Clockwise = -1;
-        const float CounterClockwise = 1;
-        struct AvoidanceBehavior
-        {
-            public float width;
-            public float height;
-            public float spin;
-            public Vector3 direction;
-
-            public AvoidanceBehavior(float width, float height, float spin, Vector3 direction)
-            {
-                this.width = width;
-                this.height = height;
-                this.spin = spin;
-                this.direction = direction;
-            }
-        }
-        #endregion
+        // Prism seeking state
+        int _trailBlockLayer;
+        Vector3 _bestPrismTarget;
+        bool _hasPrismTarget;
+        float _prismScanTimer;
+        float _prismScanInterval = 0.25f;
+        static readonly Collider[] _prismScanResults = new Collider[64];
 
         public bool AutoPilotEnabled { get; private set; }
 
@@ -141,7 +132,7 @@ namespace CosmicShore.Game.AI
                 }
```

</details>

### `26fc14177` — Fix AI pilot: crystal is always primary target, prisms are lateral nudges only

_Claude, 2026-02-19 03:30:34 +0000_

```text
The previous version blended between prism and crystal as competing targets,
causing intensity 2-4 to orbit prisms and never reach crystals. Now:

- Crystal direction is always the primary heading at all intensity levels
- Prisms only apply a small perpendicular nudge to the crystal-bound path,
  steering the ship to graze prisms along the route without detouring
- Only considers prisms that are between the ship and crystal (dotCrystal > 0.3)
  and ahead of the ship (dotForward > 0.2), preventing backwards attraction
- Prisms past the crystal distance are excluded entirely
- Nudge magnitude is small: ~4 degrees at intensity 2, ~14 degrees at intensity 4
- Nudge fades out within 40 units of crystal and when boost is near max
- Collision avoidance weight reduced from 0.6 to max 0.15 to prevent overriding
  the crystal heading
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs | 164 ++++++++++++++++++++++++++++++-------------------------------------
 1 file changed, 74 insertions(+), 90 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 235 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index 387a3ae45..eb79bc8e8 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -186,24 +186,36 @@ namespace CosmicShore.Game.AI
                 return;
 
             // At intensity 1, behave exactly as the original crystal-only AI.
-            // At higher intensities, blend prism-seeking and collision avoidance.
+            // At higher intensities, the crystal remains the primary target but
+            // the AI applies small lateral nudges to graze prisms along the way.
             if (Intensity <= 1)
             {
                 UpdateIntensity1();
                 return;
             }
 
-            ScanForPrisms();
-            _targetPosition = ComputeBlendedTarget();
-            Vector3 avoidanceSteer = ComputeCollisionAvoidance();
-
+            // Always head toward the crystal
+            _targetPosition = _crystalTargetPosition;
             _distance = _targetPosition - transform.position;
+
+            if (_distance.magnitude < float.Epsilon)
+                return;
+
             Vector3 desiredDirection = _distance.normalized;
 
-            // Blend in avoidance steering based on intensity
-            float avoidanceWeight = IntensityT * 0.6f;
+            // Apply a small lateral nudge to pass near prisms that are along our route
+            ScanForPrisms();
+            Vector3 prismNudge = ComputePrismNudge(desiredDirection);
+            if (prismNudge.sqrMagnitude > 0.001f)
+                desiredDirection = (desiredDirection + prismNudge).normalized;
+
+            // Subtle collision avoidance - small corrections, never overrides crystal heading
+            Vector3 avoidanceSteer = ComputeCollisionAvoidance();
             if (avoidanceSteer.sqrMagnitude > 0.001f)
-                desiredDirection = (desiredDirection * (1f - avoidanceWeight) + avoidanceSteer.normalized * avoidanceWeight).normalized;
+            {
+                float avoidanceWeight = Mathf.Lerp(0.05f, 0.15f, IntensityT);
+                desiredDirection = (desiredDirection + avoidanceSteer * avoidanceWeight).normalized;
+            }
 
             LookingAtCrystal = Vector3.Dot(desiredDirection, VesselStatus.Course) >= .9f;
             if (LookingAtCrystal && drift && !VesselStatus.IsDrifting)
@@ -215,10 +227,6 @@ namespace CosmicShore.Game.AI
             else if (LookingAtCrystal && VesselStatus.IsDrifting) desiredDirection *= -1;
             else if (VesselStatus.IsDrifting) vessel.StopShipControllerActions(InputEvents.LeftStickAction);
 
-
-            if (_distance.magnitude < float.Epsilon)
-                return;
-
             ApplySteering(desiredDirection, _distance.sqrMagnitude);
         }
 
@@ -271,33 +279,30 @@ namespace CosmicShore.Game.AI
             throttle += throttleIncrease * Time.deltaTime;
         }
 
-        #region Prism Seeking (Intensity 2+)
+        #region Prism Skimming - Lateral Nudge (Intensity 2+)
 
         void ScanForPrisms()
         {
             _prismScanTimer -= Time.deltaTime;
             if (_prismScanTimer > 0f) return;
-
-            // Scan more frequently at higher intensity
-            _prismScanTimer = Mathf.Lerp(_prismScanInterval * 2f, _prismScanInterval * 0.5f, IntensityT);
+            _prismScanTimer = _prismScanInterval;
 
             float scanRadius = Mathf.Lerp(prismDetectionRadius * 0.4f, prismDetectionRadius, IntensityT);
             int trailBlockMask = 1 << _trailBlockLayer;
             int hitCount = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, _prismScanResults, trailBlockMask);
 
```

</details>

### `c220676ad` — Fix AI to fly parallel to prisms with standoff distance, not into them

_Claude, 2026-02-19 03:50:07 +0000_

```text
Three fixes:
1. Standoff distance: nudge now aims to bring the path within skim range
   of prisms (12 units), not to their center. Once within standoff range,
   nudge returns zero - the skimmer trigger handles the rest naturally.
2. Minimum scan distance: ignores prisms closer than 20 units so the AI
   plans a flyby path instead of yanking toward nearby blocks and crashing.
3. QueryTriggerInteraction.Collide: all Physics.Raycast and OverlapSphere
   calls now explicitly detect trigger colliders (prisms use triggers).

Nudge reduced to ~5-10 degree max deviation. Tighter crystal-direction
filter (dotCrystal > 0.5, dotForward > 0.3) prevents side-attraction.
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs | 103 ++++++++++++++++++++++++++++++++++++-------------------------------
 1 file changed, 55 insertions(+), 48 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 199 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index eb79bc8e8..7dae12b21 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -64,8 +64,9 @@ namespace CosmicShore.Game.AI
         [Header("Intensity Scaling")]
         [SerializeField] IntVariable selectedIntensity;
 
-        [Header("Prism Seeking")]
+        [Header("Prism Skimming")]
         [SerializeField] float prismDetectionRadius = 120f;
+        [SerializeField] float skimStandoffDistance = 12f;
         [SerializeField] float collisionAvoidanceDistance = 30f;
 
         int Intensity => selectedIntensity != null ? Mathf.Clamp(selectedIntensity.Value, 1, 4) : 1;
@@ -279,7 +280,7 @@ namespace CosmicShore.Game.AI
             throttle += throttleIncrease * Time.deltaTime;
         }
 
-        #region Prism Skimming - Lateral Nudge (Intensity 2+)
+        #region Prism Skimming - Flyby Nudge (Intensity 2+)
 
         void ScanForPrisms()
         {
@@ -289,14 +290,16 @@ namespace CosmicShore.Game.AI
 
             float scanRadius = Mathf.Lerp(prismDetectionRadius * 0.4f, prismDetectionRadius, IntensityT);
             int trailBlockMask = 1 << _trailBlockLayer;
-            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, _prismScanResults, trailBlockMask);
+            int hitCount = Physics.OverlapSphereNonAlloc(
+                transform.position, scanRadius, _prismScanResults,
+                trailBlockMask, QueryTriggerInteraction.Collide);
 
             _hasPrismTarget = false;
             if (hitCount == 0) return;
 
-            // Find the best prism that is roughly along our route to the crystal.
-            // We want prisms that are ahead of us AND between us and the crystal,
-            // so that nudging toward them doesn't pull us off-course.
+            // Find the best prism that is along our route to the crystal.
+            // Only consider prisms that are well ahead (20+ units) so we plan
+            // a flyby path rather than yanking toward a nearby prism.
             Vector3 myPos = transform.position;
             Vector3 toCrystal = _crystalTargetPosition - myPos;
             float crystalDist = toCrystal.magnitude;
@@ -311,24 +314,22 @@ namespace CosmicShore.Game.AI
 
                 Vector3 toPrism = col.transform.position - myPos;
                 float dist = toPrism.magnitude;
-                if (dist < 3f || dist > crystalDist) continue; // Skip if too close or past the crystal
+
+                // Ignore prisms that are too close (can't adjust in time),
+                // behind us, or past the crystal
+                if (dist < 20f || dist > crystalDist) continue;
 
                 Vector3 dirToPrism = toPrism / dist;
 
-                // Must be in the forward hemisphere toward the crystal
+                // Must be ahead and roughly toward the crystal
                 float dotCrystal = Vector3.Dot(crystalDir, dirToPrism);
-                if (dotCrystal < 0.3f) continue; // Only prisms roughly toward the crystal
+                if (dotCrystal < 0.5f) continue;
 
-                // Must also be ahead of us
                 float dotForward = Vector3.Dot(transform.forward, dirToPrism);
-                if (dotForward < 0.2f) continue;
-
-                // Score: prefer prisms that are close to the line between us and crystal
-                // and that are relatively close (so we can reach them without a big detour)
-                float lineProximity = dotCrystal; // Higher = more aligned with crystal direction
-                float closeness = 1f - Mathf.Clamp01(dist / scanRadius);
+                if (dotForward < 0.3f) continue;
 
-                float score = lineProximity * 2f + closeness;
+                // Score: prisms closest to the line toward crystal score highest
+                float score = dotCrystal * 2f + (1f - dist / scanRadius);
 
                 if (score > bestScore)
                 {
@@ -339,37 +340,41 @@ namespace CosmicShore.Game.AI
```

</details>

### `ab725a001` — Add evolutionary AI pilot system with genome, population, and fitness tracking

_Claude, 2026-02-19 14:58:33 +0000_

```text
New files:
- PilotGenome: 12-gene parameter set (standoff distance, nudge strength,
  detection radius, avoidance weight, throttle, etc.) with uniform crossover,
  gaussian mutation, and random/copy constructors.
- PilotEvolution: ScriptableObject managing a population of genomes with
  tournament selection, elite preservation, and generational evolution.
  Context menu items for Initialize, Evolve, and Log Population.
- PilotFitnessTracker: MonoBehaviour that subscribes to crystal collection
  and prism collision events, tracks boost time and race duration, and
  reports a weighted fitness score to PilotEvolution after each race.

AIPilot changes:
- Loads a genome from PilotEvolution at Initialize when intensity >= 2.
- All tunable parameters (scan radius, standoff, nudge strength, thresholds,
  avoidance, throttle) now read from genome-aware G_ accessors that lerp
  from inspector defaults toward genome values by IntensityT.
- Intensity 1 unchanged. Intensity 2 = 33% genome. Intensity 4 = 100% genome.
- Starts/stops PilotFitnessTracker alongside the autopilot.

Usage: Create a PilotEvolution asset, assign to AIPilot's Evolution field,
right-click > Initialize Population. Each race evaluates one genome and
evolves the population after a full pass.
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs             |  78 +++++++++++++++++++++++-------
 Assets/_Scripts/Game/AI/PilotEvolution.cs      | 145 +++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/AI/PilotFitnessTracker.cs | 104 +++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/AI/PilotGenome.cs         | 117 ++++++++++++++++++++++++++++++++++++++++++++
 4 files changed, 426 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 560 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index 7dae12b21..b2001708e 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -64,16 +64,37 @@ namespace CosmicShore.Game.AI
         [Header("Intensity Scaling")]
         [SerializeField] IntVariable selectedIntensity;
 
-        [Header("Prism Skimming")]
+        [Header("Prism Skimming (defaults, overridden by genome at intensity 4)")]
         [SerializeField] float prismDetectionRadius = 120f;
         [SerializeField] float skimStandoffDistance = 12f;
         [SerializeField] float collisionAvoidanceDistance = 30f;
 
+        [Header("Evolution")]
+        [SerializeField] PilotEvolution evolution;
+
         int Intensity => selectedIntensity != null ? Mathf.Clamp(selectedIntensity.Value, 1, 4) : 1;
 
         // Intensity-derived parameters (0 = no effect at intensity 1, 1 = full effect at intensity 4)
         float IntensityT => (Intensity - 1) / 3f;
 
+        // Active genome parameters (loaded from evolution or defaults)
+        PilotGenome _activeGenome;
+        PilotFitnessTracker _fitnessTracker;
+
+        // Genome-aware accessors: lerp from inspector defaults toward genome values by IntensityT.
+        // At intensity 2 (IntensityT=0.33) mostly defaults; at intensity 4 (IntensityT=1) fully genome.
+        float G_PrismDetectionRadius => _activeGenome != null ? Mathf.Lerp(prismDetectionRadius, _activeGenome.prismDetectionRadius, IntensityT) : prismDetectionRadius;
+        float G_SkimStandoffDistance => _activeGenome != null ? Mathf.Lerp(skimStandoffDistance, _activeGenome.skimStandoffDistance, IntensityT) : skimStandoffDistance;
+        float G_MinPrismScanDistance => _activeGenome != null ? Mathf.Lerp(20f, _activeGenome.minPrismScanDistance, IntensityT) : 20f;
+        float G_MaxNudgeStrength => _activeGenome != null ? Mathf.Lerp(0.15f, _activeGenome.maxNudgeStrength, IntensityT) : Mathf.Lerp(0.05f, 0.15f, IntensityT);
+        float G_DotCrystalThreshold => _activeGenome != null ? Mathf.Lerp(0.5f, _activeGenome.dotCrystalThreshold, IntensityT) : 0.5f;
+        float G_DotForwardThreshold => _activeGenome != null ? Mathf.Lerp(0.3f, _activeGenome.dotForwardThreshold, IntensityT) : 0.3f;
+        float G_CollisionAvoidanceDistance => _activeGenome != null ? Mathf.Lerp(collisionAvoidanceDistance, _activeGenome.collisionAvoidanceDistance, IntensityT) : collisionAvoidanceDistance;
+        float G_AvoidanceWeight => _activeGenome != null ? Mathf.Lerp(0.15f, _activeGenome.avoidanceWeight, IntensityT) : Mathf.Lerp(0.05f, 0.15f, IntensityT);
+        float G_CrystalFadeDistance => _activeGenome != null ? Mathf.Lerp(50f, _activeGenome.crystalFadeDistance, IntensityT) : 50f;
+        float G_BoostFadeStrength => _activeGenome != null ? Mathf.Lerp(0.6f, _activeGenome.boostFadeStrength, IntensityT) : 0.6f;
+        float G_ThrottleRampRate => _activeGenome != null ? Mathf.Lerp(throttleIncrease, _activeGenome.throttleRampRate, IntensityT) : throttleIncrease;
+
         IVessel vessel;
         IVesselStatus VesselStatus => vessel.VesselStatus;
         IInputStatus _inputStatus => VesselStatus.InputStatus;
@@ -156,12 +177,32 @@ namespace CosmicShore.Game.AI
             throttle = defaultThrottle;
 
             _trailBlockLayer = LayerMask.NameToLayer("TrailBlocks");
+
+            LoadGenome();
+        }
+
+        void LoadGenome()
+        {
+            if (evolution != null && Intensity >= 2)
+            {
+                _activeGenome = evolution.GetNextGenome();
+                throttle = _activeGenome.throttleBase;
+
+                _fitnessTracker = GetComponent<PilotFitnessTracker>();
+            }
+            else
+            {
+                _activeGenome = null;
+            }
         }
 
         public void StartAIPilot()
         {
             AutoPilotEnabled = true;
 
+            if (_fitnessTracker != null)
+                _fitnessTracker.StartTracking(VesselStatus);
+
             foreach (var ability in abilities)
             {
                 StartCoroutine(UseAbilityCoroutine(ability));
@@ -172,6 +213,9 @@ namespace CosmicShore.Game.AI
         {
             AutoPilotEnabled = false;
 
```

</details>

### `0118bb5f9` — wire up pilot evolution

_Garrett Milliron, 2026-02-19 11:49:22 -0500_

```text
 Assets/_Prefabs/Spaceships/Squirrel.prefab               | 26 +++++++++++++++++++
 Assets/_SO_Assets/Tools.meta                             |  8 ++++++
 Assets/_SO_Assets/Tools/PilotEvolution.asset             | 63 ++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_SO_Assets/Tools/PilotEvolution.asset.meta        |  8 ++++++
 Assets/_Scenes/Singleplayer Scenes/MinigameHexRace.unity |  2 ++
 Assets/_Scripts/Game/AI/PilotEvolution.cs.meta           |  2 ++
 Assets/_Scripts/Game/AI/PilotFitnessTracker.cs.meta      |  2 ++
 Assets/_Scripts/Game/AI/PilotGenome.cs.meta              |  2 ++
 8 files changed, 113 insertions(+)
```

### `655a246dd` — Add diagnostic logging, SetDirty for SO persistence, and lower evolution guard

_Claude, 2026-02-19 16:57:20 +0000_

```text
- Add Debug.Log statements to AIPilot genome loading and PilotEvolution
  evaluation/fitness tracking for runtime diagnostics
- Call EditorUtility.SetDirty() after population mutations so the
  ScriptableObject saves properly in the Unity editor
- Lower Evolve() minimum population guard from 4 to 2 to allow evolution
  with smaller populations
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs        |  3 +++
 Assets/_Scripts/Game/AI/PilotEvolution.cs | 18 +++++++++++++++++-
 2 files changed, 20 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 101 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index b2001708e..38c7d4be3 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -189,10 +189,13 @@ namespace CosmicShore.Game.AI
                 throttle = _activeGenome.throttleBase;
 
                 _fitnessTracker = GetComponent<PilotFitnessTracker>();
+                Debug.Log($"[AIPilot] Loaded genome at intensity {Intensity} (IntensityT={IntensityT:F2}). " +
+                    $"FitnessTracker found: {_fitnessTracker != null}");
             }
             else
             {
                 _activeGenome = null;
+                Debug.Log($"[AIPilot] No genome loaded. Intensity={Intensity}, evolution={(evolution != null ? "assigned" : "null")}");
             }
         }
 
diff --git a/Assets/_Scripts/Game/AI/PilotEvolution.cs b/Assets/_Scripts/Game/AI/PilotEvolution.cs
index c893101ed..eb8d5545e 100644
--- a/Assets/_Scripts/Game/AI/PilotEvolution.cs
+++ b/Assets/_Scripts/Game/AI/PilotEvolution.cs
@@ -1,6 +1,9 @@
 using System.Collections.Generic;
 using System.Linq;
 using UnityEngine;
+#if UNITY_EDITOR
+using UnityEditor;
+#endif
 
 namespace CosmicShore.Game.AI
 {
@@ -39,6 +42,7 @@ namespace CosmicShore.Game.AI
 
             generation = 0;
             evaluationIndex = 0;
+            MarkDirty();
         }
 
         /// <summary>
@@ -49,6 +53,7 @@ namespace CosmicShore.Game.AI
             if (population.Count == 0) InitializePopulation();
 
             var genome = population[evaluationIndex % population.Count];
+            Debug.Log($"[PilotEvolution] Serving genome {evaluationIndex % population.Count} of {population.Count} (gen {generation})");
             return genome;
         }
 
@@ -58,11 +63,13 @@ namespace CosmicShore.Game.AI
         public void AdvanceEvaluation()
         {
             evaluationIndex++;
+            Debug.Log($"[PilotEvolution] Advanced to eval index {evaluationIndex}/{population.Count}");
             if (evaluationIndex >= population.Count)
             {
                 Evolve();
                 evaluationIndex = 0;
             }
+            MarkDirty();
         }
 
         /// <summary>
@@ -76,6 +83,7 @@ namespace CosmicShore.Game.AI
             genome.evaluationCount++;
             // Running average
             genome.fitness += (fitness - genome.fitness) / genome.evaluationCount;
+            Debug.Log($"[PilotEvolution] Genome {evaluationIndex % population.Count} fitness={genome.fitness:F2} (eval #{genome.evaluationCount})");
         }
 
         /// <summary>
@@ -84,7 +92,7 @@ namespace CosmicShore.Game.AI
         [ContextMenu("Evolve")]
         public void Evolve()
         {
-            if (population.Count < 4) return;
+            if (population.Count < 2) return;
 
             generation++;
 
@@ -116,6 +124,7 @@ namespace CosmicShore.Game.AI
```

</details>

### `489797b35` — modify pilot evolution

_Garrett Milliron, 2026-02-19 12:51:22 -0500_

```text
 Assets/_SO_Assets/Tools/PilotEvolution.asset | 14 ++++++++++++++
 1 file changed, 14 insertions(+)
```

### `3641a86b6` — Fix genome loading blocked by Intensity >= 2 guard

_Claude, 2026-02-19 18:37:49 +0000_

```text
The LoadGenome() method required Intensity >= 2 to load genomes, but
selectedIntensity defaults to 1 when not assigned, preventing genomes
from ever loading. Since the genome-aware accessors already scale via
IntensityT (which is 0 at intensity 1), the guard was redundant.

- Remove Intensity >= 2 check; load genome whenever evolution SO is
  assigned
- Make throttle override respect IntensityT like all other genome
  params (was setting throttle directly from genome, bypassing scaling)
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs | 5 ++---
 1 file changed, 2 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index 38c7d4be3..4f535df08 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -183,10 +183,10 @@ namespace CosmicShore.Game.AI
 
         void LoadGenome()
         {
-            if (evolution != null && Intensity >= 2)
+            if (evolution != null)
             {
                 _activeGenome = evolution.GetNextGenome();
-                throttle = _activeGenome.throttleBase;
+                throttle = Mathf.Lerp(defaultThrottle, _activeGenome.throttleBase, IntensityT);
 
                 _fitnessTracker = GetComponent<PilotFitnessTracker>();
                 Debug.Log($"[AIPilot] Loaded genome at intensity {Intensity} (IntensityT={IntensityT:F2}). " +
@@ -195,7 +195,6 @@ namespace CosmicShore.Game.AI
             else
             {
                 _activeGenome = null;
-                Debug.Log($"[AIPilot] No genome loaded. Intensity={Intensity}, evolution={(evolution != null ? "assigned" : "null")}");
             }
         }
 
```

</details>

### `78726e06d` — Add automated overnight AI training loop with multi-pilot evolution

_Claude, 2026-02-20 01:38:12 +0000_

```text
Core changes to support 3 AI pilots racing simultaneously with shared
PilotEvolution:

- PilotEvolution: replace GetNextGenome/ReportFitness/AdvanceEvaluation
  with CheckoutGenome/ReturnFitness pattern. Each CheckoutGenome call
  returns a different genome and advances the index, so multiple pilots
  get unique genomes. Evolution triggers automatically when a full
  generation of evaluations completes.

- AIPilot: call LoadGenome() on each StartAIPilot (not just Initialize)
  so each race gets a fresh genome from the population.

- PilotFitnessTracker: store genome index from checkout and use
  ReturnFitness(index, fitness) to report to the correct genome.

- VesselResetBoostPrismEffectSO: change OnPrismCollision from Action to
  Action<string> passing player name, fixing a bug where all fitness
  trackers counted every pilot's prism collisions.

- HexRaceScoreTracker: update HandlePrismCollision to filter by player.

New files:

- AITrainingController: auto-restarts races, auto-clicks Ready, logs
  generation progress, has race timeout safety. Drop into HexRace scene.

- TrainingPlayerSpawnerAdapter: spawns only AI players (no human),
  for AI-only training races.
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs                                    |  11 ++-
 Assets/_Scripts/Game/AI/AITrainingController.cs                       | 128 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/AI/PilotEvolution.cs                             |  59 +++++++++------
 Assets/_Scripts/Game/AI/PilotFitnessTracker.cs                        |  31 ++++----
 Assets/_Scripts/Game/Arcade/HexRaceScoreTracker.cs                    |   7 +-
 .../EffectsSO/Vessel Prism Effects/VesselResetBoostPrismEffectSO.cs   |   6 +-
 Assets/_Scripts/Game/Player/TrainingPlayerSpawnerAdapter.cs           |  29 ++++++++
 7 files changed, 226 insertions(+), 45 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 381 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index 4f535df08..9774c3519 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -185,12 +185,14 @@ namespace CosmicShore.Game.AI
         {
             if (evolution != null)
             {
-                _activeGenome = evolution.GetNextGenome();
+                _activeGenome = evolution.CheckoutGenome(out int genomeIndex);
                 throttle = Mathf.Lerp(defaultThrottle, _activeGenome.throttleBase, IntensityT);
 
                 _fitnessTracker = GetComponent<PilotFitnessTracker>();
-                Debug.Log($"[AIPilot] Loaded genome at intensity {Intensity} (IntensityT={IntensityT:F2}). " +
-                    $"FitnessTracker found: {_fitnessTracker != null}");
+                if (_fitnessTracker != null)
+                    _fitnessTracker.SetGenomeIndex(genomeIndex);
+
+                Debug.Log($"[AIPilot] {VesselStatus.PlayerName} loaded genome {genomeIndex} at intensity {Intensity} (IntensityT={IntensityT:F2})");
             }
             else
             {
@@ -202,6 +204,9 @@ namespace CosmicShore.Game.AI
         {
             AutoPilotEnabled = true;
 
+            // Reload genome each race so each start gets the next genome from the population
+            LoadGenome();
+
             if (_fitnessTracker != null)
                 _fitnessTracker.StartTracking(VesselStatus);
 
diff --git a/Assets/_Scripts/Game/AI/AITrainingController.cs b/Assets/_Scripts/Game/AI/AITrainingController.cs
new file mode 100644
index 000000000..496117859
--- /dev/null
+++ b/Assets/_Scripts/Game/AI/AITrainingController.cs
@@ -0,0 +1,128 @@
+using System.Collections;
+using CosmicShore.Soap;
+using Obvious.Soap;
+using UnityEngine;
+
+namespace CosmicShore.Game.AI
+{
+    /// <summary>
+    /// Automates AI training races for overnight evolutionary learning.
+    /// Drop this into a HexRace scene alongside the existing game controller.
+    ///
+    /// Setup:
+    /// 1. Replace MiniGamePlayerSpawnerAdapter with TrainingPlayerSpawnerAdapter
+    ///    and configure 3 AI entries in _initializeDatas (IsAI=true, AllowSpawning=true).
+    /// 2. On the game controller, set numberOfRounds = 1, numberOfTurnsPerRound = 1.
+    /// 3. Optionally reduce CountdownTimer.countdownDuration for faster cycles.
+    /// 4. Assign the same PilotEvolution SO to this controller and to each AI vessel's
+    ///    AIPilot + PilotFitnessTracker.
+    /// 5. Press Play and walk away.
+    /// </summary>
+    public class AITrainingController : MonoBehaviour
+    {
+        [Header("References")]
+        [SerializeField] GameDataSO gameData;
+        [SerializeField] Arcade.MiniGameControllerBase gameController;
+        [SerializeField] PilotEvolution evolution;
+
+        [Header("Training Config")]
+        [SerializeField] int maxRaces = 10000;
+        [SerializeField] float delayBetweenRaces = 1f;
+        [SerializeField] float raceTimeoutSeconds = 300f;
+
+        int _racesCompleted;
+        float _raceStartTime;
+        bool _raceActive;
+
+        void OnEnable()
+        {
+            gameData.OnMiniGameEnd += OnRaceEnd;
+            gameData.OnMiniGameRoundStarted.OnRaised += OnRoundStarted;
+            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
+        }
```

</details>

### `d0195baa2` — Refine genome search space: drop noise params, add steering, fix intensity

_Claude, 2026-02-20 02:20:23 +0000_

```text
Genome audit found that 3 params were low-signal modifiers-on-modifiers
adding noise without meaningful behavioral variation, while the
highest-impact parameter (steering aggressiveness) was hardcoded at 100.

Genome changes (12 → 10 params):
- Remove dotForwardThreshold (redundant with dotCrystalThreshold)
- Remove crystalFadeDistance (subtle nudge modifier)
- Remove boostFadeStrength (subtle nudge modifier)
- Add steeringAggressiveness [20-200] replacing hardcoded 100f in
  ApplySteering — controls turn sharpness, one of the most impactful
  behavioral parameters

AIPilot changes:
- Wire G_SteeringAggressiveness into ApplySteering
- Simplify ComputePrismNudge (remove crystal/boost fade scaling)
- Hardcode forward-arc check at 0.2 (was evolvable dotForwardThreshold)

Training fix:
- AITrainingController now sets gameData.SelectedIntensity to 4 on
  enable. At intensity 1, the Update() early-returns to UpdateIntensity1
  which uses ZERO genome parameters — all genomes behave identically.
  Intensity 4 activates all genome-aware code paths (prism skimming,
  collision avoidance, steering) with full genome expression (IntensityT=1).
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs              | 19 ++++++-------------
 Assets/_Scripts/Game/AI/AITrainingController.cs | 13 +++++++++----
 Assets/_Scripts/Game/AI/PilotEvolution.cs       |  3 ++-
 Assets/_Scripts/Game/AI/PilotGenome.cs          | 56 +++++++++++++++++++++++--------------------------------
 4 files changed, 40 insertions(+), 51 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 216 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index 9774c3519..bd087434a 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -88,11 +88,9 @@ namespace CosmicShore.Game.AI
         float G_MinPrismScanDistance => _activeGenome != null ? Mathf.Lerp(20f, _activeGenome.minPrismScanDistance, IntensityT) : 20f;
         float G_MaxNudgeStrength => _activeGenome != null ? Mathf.Lerp(0.15f, _activeGenome.maxNudgeStrength, IntensityT) : Mathf.Lerp(0.05f, 0.15f, IntensityT);
         float G_DotCrystalThreshold => _activeGenome != null ? Mathf.Lerp(0.5f, _activeGenome.dotCrystalThreshold, IntensityT) : 0.5f;
-        float G_DotForwardThreshold => _activeGenome != null ? Mathf.Lerp(0.3f, _activeGenome.dotForwardThreshold, IntensityT) : 0.3f;
         float G_CollisionAvoidanceDistance => _activeGenome != null ? Mathf.Lerp(collisionAvoidanceDistance, _activeGenome.collisionAvoidanceDistance, IntensityT) : collisionAvoidanceDistance;
         float G_AvoidanceWeight => _activeGenome != null ? Mathf.Lerp(0.15f, _activeGenome.avoidanceWeight, IntensityT) : Mathf.Lerp(0.05f, 0.15f, IntensityT);
-        float G_CrystalFadeDistance => _activeGenome != null ? Mathf.Lerp(50f, _activeGenome.crystalFadeDistance, IntensityT) : 50f;
-        float G_BoostFadeStrength => _activeGenome != null ? Mathf.Lerp(0.6f, _activeGenome.boostFadeStrength, IntensityT) : 0.6f;
+        float G_SteeringAggressiveness => _activeGenome != null ? Mathf.Lerp(100f, _activeGenome.steeringAggressiveness, IntensityT) : 100f;
         float G_ThrottleRampRate => _activeGenome != null ? Mathf.Lerp(throttleIncrease, _activeGenome.throttleRampRate, IntensityT) : throttleIncrease;
 
         IVessel vessel;
@@ -310,8 +308,8 @@ namespace CosmicShore.Game.AI
             Vector3 crossProduct = Vector3.Cross(transform.forward, desiredDirection);
             Vector3 localCrossProduct = transform.InverseTransformDirection(crossProduct);
 
-            aggressiveness = 100f;
-            float angle = Mathf.Asin(Mathf.Clamp(localCrossProduct.sqrMagnitude * aggressiveness / Mathf.Min(sqrMagnitude, _maxDistance), -1f, 1f)) * Mathf.Rad2Deg;
+            float steering = G_SteeringAggressiveness;
+            float angle = Mathf.Asin(Mathf.Clamp(localCrossProduct.sqrMagnitude * steering / Mathf.Min(sqrMagnitude, _maxDistance), -1f, 1f)) * Mathf.Rad2Deg;
 
             if (VesselStatus.IsSingleStickControls)
             {
@@ -375,8 +373,9 @@ namespace CosmicShore.Game.AI
                 float dotCrystal = Vector3.Dot(crystalDir, dirToPrism);
                 if (dotCrystal < G_DotCrystalThreshold) continue;
 
+                // Must be in our forward arc (not behind us)
                 float dotForward = Vector3.Dot(transform.forward, dirToPrism);
-                if (dotForward < G_DotForwardThreshold) continue;
+                if (dotForward < 0.2f) continue;
 
                 // Score: prisms closest to the line toward crystal score highest
                 float score = dotCrystal * 2f + (1f - dist / scanRadius);
@@ -415,13 +414,7 @@ namespace CosmicShore.Game.AI
             float gapToClose = perpDist - standoff;
             float gapNorm = Mathf.Clamp01(gapToClose / (G_PrismDetectionRadius * 0.5f));
 
-            // Scale by crystal proximity and boost
-            float distToCrystal = (_crystalTargetPosition - transform.position).magnitude;
-            float crystalFade = Mathf.Clamp01(distToCrystal / G_CrystalFadeDistance);
-            float boostNorm = Mathf.Clamp01((VesselStatus.BoostMultiplier - 1f) / 4f);
-            float boostFade = 1f - boostNorm * G_BoostFadeStrength;
-
-            float nudgeMag = G_MaxNudgeStrength * gapNorm * crystalFade * boostFade;
+            float nudgeMag = G_MaxNudgeStrength * gapNorm;
 
             return nudgeDir * nudgeMag;
         }
diff --git a/Assets/_Scripts/Game/AI/AITrainingController.cs b/Assets/_Scripts/Game/AI/AITrainingController.cs
index 496117859..9a5a5d0fd 100644
--- a/Assets/_Scripts/Game/AI/AITrainingController.cs
+++ b/Assets/_Scripts/Game/AI/AITrainingController.cs
@@ -1,6 +1,5 @@
 using System.Collections;
 using CosmicShore.Soap;
-using Obvious.Soap;
 using UnityEngine;
 
 namespace CosmicShore.Game.AI
@@ -29,6 +28,7 @@ namespace CosmicShore.Game.AI
         [SerializeField] int maxRaces = 10000;
         [SerializeField] float delayBetweenRaces = 1f;
         [SerializeField] float raceTimeoutSeconds = 300f;
+        [SerializeField, Range(1, 4)] int trainingIntensity = 4;
 
         int _racesCompleted;
         float _raceStartTime;
@@ -36,6 +36,10 @@ namespace CosmicShore.Game.AI
 
         void OnEnable()
         {
+            // Set intensity so all genome-aware code paths are active
+            if (gameData.SelectedIntensity != null)
+                gameData.SelectedIntensity.Value = trainingIntensity;
```

</details>

### `a53f64654` — add meta files

_Garrett Milliron, 2026-02-19 23:16:48 -0500_

```text
 Assets/_Scripts/Game/AI/AITrainingController.cs.meta             | 2 ++
 Assets/_Scripts/Game/Player/TrainingPlayerSpawnerAdapter.cs.meta | 2 ++
 2 files changed, 4 insertions(+)
```

### `5c7972e65` — wire up scene

_Garrett Milliron, 2026-02-19 23:32:40 -0500_

```text
 Assets/_SO_Assets/Tools/PilotEvolution.asset             | 16 +++------
 Assets/_Scenes/Singleplayer Scenes/MinigameHexRace.unity | 90 +++++++++++++++++++++++++++++++++++++++++++++-
 2 files changed, 93 insertions(+), 13 deletions(-)
```

### `a52c825c7` — Fix AI crystal targeting to respect domain ownership in HexRace

_Claude, 2026-02-20 05:30:59 +0000_

```text
AI pilots now only target crystals they can actually collect, matching
Crystal.CanBeCollected() logic. Previously all Buff items were targeted
regardless of domain, causing all pilots to chase the same crystal.
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs | 15 +++++++++++----
 1 file changed, 11 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index bd087434a..726a18fd7 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -140,10 +140,17 @@ namespace CosmicShore.Game.AI
 
             foreach (var item in cellItems)
             {
-                // Debuffs are disguised as desireable to the other team
-                // So, if it's good, or if it's bad but made by another team, go for it
-                if (item.ItemType != ItemType.Buff &&
-                    (item.ItemType != ItemType.Debuff || item.ownDomain == VesselStatus.Domain)) continue;
+                // Buffs: only target crystals we can actually collect (domain must match or be None)
+                if (item.ItemType == ItemType.Buff)
+                {
+                    if (item.ownDomain != Domains.None && item.ownDomain != VesselStatus.Domain) continue;
+                }
+                // Debuffs are disguised as desirable to the other team — go for enemy debuffs
+                else if (item.ItemType == ItemType.Debuff)
+                {
+                    if (item.ownDomain == VesselStatus.Domain) continue;
+                }
+                else continue;
                 var sqDistance = Vector3.SqrMagnitude(item.transform.position - transform.position);
                 if (sqDistance < (MinDistance * MinDistance))
                 {
```

</details>

### `3ddd4bd4d` — evolution update

_Garrett Milliron, 2026-02-20 02:33:03 -0500_

```text
 Assets/_SO_Assets/Tools/PilotEvolution.asset | 240 +++++++++++++++++++++++++++++++++++++++++++++++++++------
 1 file changed, 216 insertions(+), 24 deletions(-)
```

### `24a76216b` — Add two training modes: spectator (unattended) and play-along

_Claude, 2026-02-20 20:55:31 +0000_

```text
Fixes three bugs preventing overnight training from working:

1. Double genome checkout: Initialize() called LoadGenome() for each pilot,
   then StartAIPilot() called it again. Removed from Initialize() so genomes
   are only checked out when pilots actually start racing.

2. Score limit never triggers game end: CrystalCollisionTurnMonitor only
   checks LocalPlayer stats, which is null in all-AI mode. AITrainingController
   now directly checks all players' CrystalsCollected each frame.

3. Camera not set up: VesselCameraCustomizer.Initialize() only runs for
   IsLocalUser. Spectator mode now explicitly initializes camera on the
   first AI vessel.

Two training controllers:
- AITrainingController: Fully automated spectator mode. Auto-ready,
  auto-detect race completion, auto-restart, spectator camera. Walk away.
- PlayAlongTrainingController: Human races alongside evolving AI.
  Standard turn monitor works (tracks human). Auto-restarts between races.
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs                     |  2 --
 Assets/_Scripts/Game/AI/AITrainingController.cs        | 91 ++++++++++++++++++++++++++++++++++++------------
 Assets/_Scripts/Game/AI/PlayAlongTrainingController.cs | 66 +++++++++++++++++++++++++++++++++++
 3 files changed, 135 insertions(+), 24 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 237 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index 726a18fd7..50351e5fe 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -182,8 +182,6 @@ namespace CosmicShore.Game.AI
             throttle = defaultThrottle;
 
             _trailBlockLayer = LayerMask.NameToLayer("TrailBlocks");
-
-            LoadGenome();
         }
 
         void LoadGenome()
diff --git a/Assets/_Scripts/Game/AI/AITrainingController.cs b/Assets/_Scripts/Game/AI/AITrainingController.cs
index 9a5a5d0fd..e1d624b81 100644
--- a/Assets/_Scripts/Game/AI/AITrainingController.cs
+++ b/Assets/_Scripts/Game/AI/AITrainingController.cs
@@ -5,14 +5,13 @@ using UnityEngine;
 namespace CosmicShore.Game.AI
 {
     /// <summary>
-    /// Automates AI training races for overnight evolutionary learning.
-    /// Drop this into a HexRace scene alongside the existing game controller.
+    /// Fully automated spectator training mode.
+    /// Runs AI-only races in a loop with no human input required.
     ///
     /// Setup:
-    /// 1. Replace MiniGamePlayerSpawnerAdapter with TrainingPlayerSpawnerAdapter
-    ///    and configure 3 AI entries in _initializeDatas (IsAI=true, AllowSpawning=true).
-    /// 2. On the game controller, set numberOfRounds = 1, numberOfTurnsPerRound = 1.
-    /// 3. Optionally reduce CountdownTimer.countdownDuration for faster cycles.
+    /// 1. Use TrainingPlayerSpawnerAdapter with 3 AI entries (IsAI=true).
+    /// 2. Set numberOfRounds = 1, numberOfTurnsPerRound = 1 on the game controller.
+    /// 3. Assign the CrystalCollisionTurnMonitor so this controller knows the crystal target.
     /// 4. Assign the same PilotEvolution SO to this controller and to each AI vessel's
     ///    AIPilot + PilotFitnessTracker.
     /// 5. Press Play and walk away.
@@ -23,20 +22,22 @@ namespace CosmicShore.Game.AI
         [SerializeField] GameDataSO gameData;
         [SerializeField] Arcade.MiniGameControllerBase gameController;
         [SerializeField] PilotEvolution evolution;
+        [SerializeField] Arcade.CrystalCollisionTurnMonitor turnMonitor;
 
         [Header("Training Config")]
         [SerializeField] int maxRaces = 10000;
         [SerializeField] float delayBetweenRaces = 1f;
-        [SerializeField] float raceTimeoutSeconds = 300f;
+        [SerializeField] float raceTimeoutSeconds = 120f;
         [SerializeField, Range(1, 4)] int trainingIntensity = 4;
 
         int _racesCompleted;
         float _raceStartTime;
         bool _raceActive;
+        bool _cameraInitialized;
+        int _crystalTarget;
 
         void OnEnable()
         {
-            // Set intensity so all genome-aware code paths are active
             if (gameData.SelectedIntensity != null)
                 gameData.SelectedIntensity.Value = trainingIntensity;
 
@@ -54,8 +55,7 @@ namespace CosmicShore.Game.AI
 
         void OnRoundStarted()
         {
-            // Ready button is about to be shown by SetupNewTurn().
-            // Auto-click after one frame so the turn setup completes first.
+            // Auto-click Ready after one frame so SetupNewTurn completes first.
             StartCoroutine(AutoClickReady());
         }
 
@@ -69,6 +69,65 @@ namespace CosmicShore.Game.AI
         {
             _raceActive = true;
             _raceStartTime = Time.time;
+
+            // Cache the crystal target from the turn monitor (set during StartMonitor)
+            if (turnMonitor != null)
+            {
```

</details>

### `1f7da8088` — wire up

_Garrett Milliron, 2026-02-21 12:42:34 -0500_

```text
 Assets/_Scenes/Singleplayer Scenes/MinigameHexRace.unity    | 1 +
 Assets/_Scripts/Game/AI/PlayAlongTrainingController.cs.meta | 2 ++
 2 files changed, 3 insertions(+)
```

### `148b1ba8f` — Enhance AI with collision prediction, skimming bias, drift control, and spectator camera

_Claude, 2026-02-21 21:46:53 +0000_

```text
AI Improvements:
- Collision threat prediction: scales from 0-1 based on proximity to obstacles
- Throttle braking: reduces speed proportionally to collision threat (configurable response)
- Drift control for evasion: triggers double drift at high threat, releases when safe
- Skimming bias: angles toward boost-giving paths when far from crystal, direct line when close
  Uses distance falloff so nearby crystals prioritize direct path
- Single drift for trajectory setup: can set up next crystal path while hitting current one

Genome Changes (13 → 16 parameters):
- Added throttleBrakeResponse [0.1-1.0]: controls how aggressively to brake before collisions
- Added driftThreshold [0.2-1.0]: collision threat level to trigger emergency drift maneuver
- Added skimBiasStrength [0.5-2.0]: bias toward skimming paths vs direct routes

AIPilot:
- Exposed CurrentGenomeIndex property so spectator camera can display which genome each pilot uses
- ComputeCollisionAvoidance now returns threat level (magnitude of avoidance vector)
- ApplyCollisionBraking() new method: throttles down + triggers drift at high threat
- Updated drift logic: single drift for crystal alignment, double drift for collision evasion
- Drift releases automatically when threat drops and not aligned with crystal

Spectator Training UI:
- Suppress Ready button during training (doesn't distract from continuous racing)
- Suppress end game cinematic and scoreboard (runs continuously without interruption)
- Leader-following camera: tracks first-place racer
- Brief show of other racers: cycles through each pilot every N seconds for variety
- Genome index display: shows which genome index (0-19) each pilot is using
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs              | 73 ++++++++++++++++++++++++++++++++++++++++++++++++++++---
 Assets/_Scripts/Game/AI/AITrainingController.cs | 64 ++++++++++++++++++++++++++++++++++++++++++++----
 Assets/_Scripts/Game/AI/PilotGenome.cs          | 19 +++++++++++++++
 3 files changed, 148 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 352 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index 50351e5fe..716f7cdad 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -80,6 +80,7 @@ namespace CosmicShore.Game.AI
         // Active genome parameters (loaded from evolution or defaults)
         PilotGenome _activeGenome;
         PilotFitnessTracker _fitnessTracker;
+        int _currentGenomeIndex = -1;
 
         // Genome-aware accessors: lerp from inspector defaults toward genome values by IntensityT.
         // At intensity 2 (IntensityT=0.33) mostly defaults; at intensity 4 (IntensityT=1) fully genome.
@@ -92,6 +93,9 @@ namespace CosmicShore.Game.AI
         float G_AvoidanceWeight => _activeGenome != null ? Mathf.Lerp(0.15f, _activeGenome.avoidanceWeight, IntensityT) : Mathf.Lerp(0.05f, 0.15f, IntensityT);
         float G_SteeringAggressiveness => _activeGenome != null ? Mathf.Lerp(100f, _activeGenome.steeringAggressiveness, IntensityT) : 100f;
         float G_ThrottleRampRate => _activeGenome != null ? Mathf.Lerp(throttleIncrease, _activeGenome.throttleRampRate, IntensityT) : throttleIncrease;
+        float G_ThrottleBrakeResponse => _activeGenome != null ? Mathf.Lerp(0.5f, _activeGenome.throttleBrakeResponse, IntensityT) : 0.5f;
+        float G_DriftThreshold => _activeGenome != null ? Mathf.Lerp(0.6f, _activeGenome.driftThreshold, IntensityT) : 0.6f;
+        float G_SkimBiasStrength => _activeGenome != null ? Mathf.Lerp(1f, _activeGenome.skimBiasStrength, IntensityT) : 1f;
 
         IVessel vessel;
         IVesselStatus VesselStatus => vessel.VesselStatus;
@@ -118,6 +122,10 @@ namespace CosmicShore.Game.AI
         float _prismScanInterval = 0.25f;
         static readonly Collider[] _prismScanResults = new Collider[64];
 
+        // Collision avoidance state
+        float _collisionThreat; // 0-1 scale of collision proximity/urgency
+        float _lastCollisionBrakeTime;
+
         public bool AutoPilotEnabled { get; private set; }
 
         private void OnEnable()
@@ -189,6 +197,7 @@ namespace CosmicShore.Game.AI
             if (evolution != null)
             {
                 _activeGenome = evolution.CheckoutGenome(out int genomeIndex);
+                _currentGenomeIndex = genomeIndex;
                 throttle = Mathf.Lerp(defaultThrottle, _activeGenome.throttleBase, IntensityT);
 
                 _fitnessTracker = GetComponent<PilotFitnessTracker>();
@@ -200,9 +209,12 @@ namespace CosmicShore.Game.AI
             else
             {
                 _activeGenome = null;
+                _currentGenomeIndex = -1;
             }
         }
 
+        public int CurrentGenomeIndex => _currentGenomeIndex;
+
         public void StartAIPilot()
         {
             AutoPilotEnabled = true;
@@ -258,28 +270,55 @@ namespace CosmicShore.Game.AI
 
             Vector3 desiredDirection = _distance.normalized;
 
+            // Compute collision threat and take evasive action if needed
+            Vector3 avoidanceSteer = ComputeCollisionAvoidance();
+            _collisionThreat = avoidanceSteer.magnitude;
+
+            // Apply throttle braking based on collision threat
+            if (_collisionThreat > 0.001f)
+            {
+                ApplyCollisionBraking(_collisionThreat, _distance.magnitude);
+            }
+
             // Apply a small lateral nudge to pass near prisms that are along our route
             ScanForPrisms();
             Vector3 prismNudge = ComputePrismNudge(desiredDirection);
             if (prismNudge.sqrMagnitude > 0.001f)
+            {
+                // Bias skim nudge based on angle to crystal: far away = stronger bias toward skim paths
+                float distToCrystal = _distance.magnitude;
+                float maxSkimBiasDistance = 100f;
+                float skimBiasFalloff = Mathf.Clamp01(1f - (distToCrystal / maxSkimBiasDistance));
+                prismNudge *= G_SkimBiasStrength * Mathf.Lerp(0.3f, 1f, skimBiasFalloff);
+
                 desiredDirection = (desiredDirection + prismNudge).normalized;
```

</details>

### `d941fac0d` — Fix missing using directive for ScriptableEventBool

_Claude, 2026-02-25 10:32:20 +0000_

```text
 Assets/_Scripts/Game/AI/AITrainingController.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AITrainingController.cs b/Assets/_Scripts/Game/AI/AITrainingController.cs
index 6bd0670a5..6c6306575 100644
--- a/Assets/_Scripts/Game/AI/AITrainingController.cs
+++ b/Assets/_Scripts/Game/AI/AITrainingController.cs
@@ -1,5 +1,6 @@
 using System.Collections;
 using CosmicShore.Soap;
+using Obvious.Soap;
 using UnityEngine;
 
 namespace CosmicShore.Game.AI
```

</details>

### `c7f2cdae8` — Fix GetComponent call on IVessel interface

_Claude, 2026-02-25 10:37:20 +0000_

```text
Get AIPilot from vessel's GameObject instead of directly from IVessel interface
```

```text
 Assets/_Scripts/Game/AI/AITrainingController.cs | 12 ++++++++----
 1 file changed, 8 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AITrainingController.cs b/Assets/_Scripts/Game/AI/AITrainingController.cs
index 6c6306575..f74aa6952 100644
--- a/Assets/_Scripts/Game/AI/AITrainingController.cs
+++ b/Assets/_Scripts/Game/AI/AITrainingController.cs
@@ -124,11 +124,15 @@ namespace CosmicShore.Game.AI
             vessel.VesselStatus.VesselCameraCustomizer.Initialize(vessel);
 
             // Display genome index for this pilot
-            var aiPilot = vessel.GetComponent<AIPilot>();
-            if (aiPilot != null)
+            var vesselGO = vessel.VesselStatus.GameObject;
+            if (vesselGO != null)
             {
-                int genomeIndex = aiPilot.CurrentGenomeIndex;
-                Debug.Log($"[AITraining] Camera on {vessel.VesselStatus.PlayerName} (Genome #{genomeIndex})");
+                var aiPilot = vesselGO.GetComponent<AIPilot>();
+                if (aiPilot != null)
+                {
+                    int genomeIndex = aiPilot.CurrentGenomeIndex;
+                    Debug.Log($"[AITraining] Camera on {vessel.VesselStatus.PlayerName} (Genome #{genomeIndex})");
+                }
             }
         }
 
```

</details>

### `7736f0110` — Fix: access AIPilot directly from IVesselStatus

_Claude, 2026-02-25 10:38:46 +0000_

```text
IVesselStatus has an AIPilot property, so use that instead of
trying to GetComponent through the GameObject
```

```text
 Assets/_Scripts/Game/AI/AITrainingController.cs | 12 ++++--------
 1 file changed, 4 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AITrainingController.cs b/Assets/_Scripts/Game/AI/AITrainingController.cs
index f74aa6952..0d30a945e 100644
--- a/Assets/_Scripts/Game/AI/AITrainingController.cs
+++ b/Assets/_Scripts/Game/AI/AITrainingController.cs
@@ -124,15 +124,11 @@ namespace CosmicShore.Game.AI
             vessel.VesselStatus.VesselCameraCustomizer.Initialize(vessel);
 
             // Display genome index for this pilot
-            var vesselGO = vessel.VesselStatus.GameObject;
-            if (vesselGO != null)
+            var aiPilot = vessel.VesselStatus.AIPilot;
+            if (aiPilot != null)
             {
-                var aiPilot = vesselGO.GetComponent<AIPilot>();
-                if (aiPilot != null)
-                {
-                    int genomeIndex = aiPilot.CurrentGenomeIndex;
-                    Debug.Log($"[AITraining] Camera on {vessel.VesselStatus.PlayerName} (Genome #{genomeIndex})");
-                }
+                int genomeIndex = aiPilot.CurrentGenomeIndex;
+                Debug.Log($"[AITraining] Camera on {vessel.VesselStatus.PlayerName} (Genome #{genomeIndex})");
             }
         }
 
```

</details>

### `5a87f0adb` — Fix AI training to run indefinitely for overnight unattended sessions

_Claude, 2026-02-25 11:03:54 +0000_

```text
The training loop was breaking because AITrainingController called
InvokeGameTurnConditionsMet(), which triggered the standard EndTurn →
EndRound → EndGame → cinematic chain. The EndGameCinematicController
then blocked forever waiting for a Continue button press that never
comes in AI-only mode.

Fix: bypass the standard end-game flow entirely. When a race finishes,
AITrainingController now directly calls ResetForReplay() (which handles
fitness reporting via player reset, stats cleanup, and track rebuild)
instead of going through the cinematic-triggering chain. Also disables
any EndGameCinematicController in the scene on Start as a safety net.

Additional overnight stability improvements:
- Increased max races from 10k to 100k
- Increased timeout from 120s to 180s
- Reduced delay between races from 1s to 0.5s
- Periodic GC.Collect every 50 races to prevent memory pressure
- Session elapsed time in progress logs
```

```text
 Assets/_Scripts/Game/AI/AITrainingController.cs | 128 +++++++++++++++++++++++++++++-------------------------
 1 file changed, 70 insertions(+), 58 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 251 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AITrainingController.cs b/Assets/_Scripts/Game/AI/AITrainingController.cs
index 0d30a945e..4cf39bc50 100644
--- a/Assets/_Scripts/Game/AI/AITrainingController.cs
+++ b/Assets/_Scripts/Game/AI/AITrainingController.cs
@@ -1,4 +1,5 @@
 using System.Collections;
+using CosmicShore.Game.Cinematics;
 using CosmicShore.Soap;
 using Obvious.Soap;
 using UnityEngine;
@@ -8,6 +9,8 @@ namespace CosmicShore.Game.AI
     /// <summary>
     /// Fully automated spectator training mode.
     /// Runs AI-only races in a loop with no human input required.
+    /// Bypasses the standard EndGame → cinematic flow entirely so races
+    /// can loop indefinitely for overnight unattended training.
     ///
     /// Setup:
     /// 1. Use TrainingPlayerSpawnerAdapter with 3 AI entries (IsAI=true).
@@ -27,13 +30,17 @@ namespace CosmicShore.Game.AI
         [SerializeField] ScriptableEventBool toggleReadyButtonEvent;
 
         [Header("Training Config")]
-        [SerializeField] int maxRaces = 10000;
-        [SerializeField] float delayBetweenRaces = 1f;
-        [SerializeField] float raceTimeoutSeconds = 120f;
+        [SerializeField] int maxRaces = 100000;
+        [SerializeField] float delayBetweenRaces = 0.5f;
+        [SerializeField] float raceTimeoutSeconds = 180f;
         [SerializeField, Range(1, 4)] int trainingIntensity = 4;
         [SerializeField] float cameraShowOthersInterval = 4f;
         [SerializeField] float cameraShowOthersDuration = 1.5f;
 
+        [Header("Stability")]
+        [Tooltip("Run GC.Collect every N races to prevent memory pressure during overnight runs.")]
+        [SerializeField] int gcCollectInterval = 50;
+
         int _racesCompleted;
         float _raceStartTime;
         bool _raceActive;
@@ -41,47 +48,42 @@ namespace CosmicShore.Game.AI
         int _crystalTarget;
         float _nextShowOtherTime;
         int _currentShowOtherIndex;
+        float _sessionStartTime;
 
         void OnEnable()
         {
             if (gameData.SelectedIntensity != null)
                 gameData.SelectedIntensity.Value = trainingIntensity;
 
-            gameData.OnMiniGameEnd += OnRaceEnd;
             gameData.OnMiniGameRoundStarted.OnRaised += OnRoundStarted;
             gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
-            gameData.OnWinnerCalculated += SkipEndGameScreen;
-            gameData.OnShowGameEndScreen.OnRaised += SkipScoreboard;
+
+            _sessionStartTime = Time.realtimeSinceStartup;
         }
 
         void OnDisable()
         {
-            gameData.OnMiniGameEnd -= OnRaceEnd;
             gameData.OnMiniGameRoundStarted.OnRaised -= OnRoundStarted;
             gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
-            gameData.OnWinnerCalculated -= SkipEndGameScreen;
-            gameData.OnShowGameEndScreen.OnRaised -= SkipScoreboard;
-        }
-
-        void SkipEndGameScreen()
-        {
-            // Prevent victory lap and cinematic sequence
-            Debug.Log("[AITraining] Skipping end game cinematic");
         }
 
-        void SkipScoreboard()
+        void Start()
         {
-            // Prevent scoreboard from showing
-            Debug.Log("[AITraining] Skipping scoreboard display");
```

</details>
