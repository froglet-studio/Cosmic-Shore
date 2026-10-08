# Branch archive: `claude/game-reward-system-crystals-3dl6pf`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Unified crystal reward service and displays**

Built one central RewardService that pays out every crystal reward. It points every crystal balance at the same wallet, removes the per-scene payout tables in favour of a RewardTable asset, adds two on-screen reward displays and an editor tool that places them, and documents the economy. It also carries the first version of the Arkway toy (a corridor of cells an Ark sails through).

- **Status:** Unique work
- **Areas:** Economy/crystal rewards, Game HUD reward displays, Editor tooling, Toys (Arkway)
- **Already in bleeding-edge:** The Arkway landed and was later merged into the Wander toy ('bc6b98d52 feat(toys): merge Wanderway and Arkway into one Wander toy'). No reward system found in bleeding-edge: RewardService, RewardTableSO, RewardGrant, RewardKind and RewardGrantedChannel.asset are all absent.
- **Risk if deleted:** high
- **Suggestion (2026-10-08):** keep — The unified crystal reward economy exists nowhere else.

## Evidence

- **Last commit:** 2026-09-01 by Claude
- **Unmerged commits:** 11
- **Forked from:** `10f96acb8` (2026-08-31, Merge pull request #821 from froglet-studio/claude/scarab-vessel-polish-k9mds6)
- **Tip:** `091967302`
- **Files touched (76):**
  - `.claude/skills/ecology/SKILL.md`
  - `Assets/Resources/Channels/RewardGrantedChannel.asset`
  - `Assets/Resources/Channels/RewardGrantedChannel.asset.meta`
  - `Assets/Resources/RewardTable.asset`
  - `Assets/Resources/RewardTable.asset.meta`
  - `Assets/Resources/Toybox.asset`
  - `Assets/_Prefabs/GameCanvas-HexRace.prefab`
  - `Assets/_SO_Assets/Toys/Toy_Arkway.asset`
  - `Assets/_SO_Assets/Toys/Toy_Arkway.asset.meta`
  - `Assets/_Scenes/Multiplayer Scenes/ArcadeGameMultiplayer2v2CoOpVsAI.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameAstroLeague.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameNucleusRush.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameSalvo.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameScarabScramble.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity`
  - `Assets/_Scripts/Controller/Environment/Ark.cs`
  - `Assets/_Scripts/Controller/Environment/Ark.cs.meta`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs`
  - `Assets/_Scripts/Controller/Toys/ArkwayRun.cs`
  - `Assets/_Scripts/Controller/Toys/ArkwayRun.cs.meta`
  - `Assets/_Scripts/Controller/Toys/ArkwayToy.cs`
  - `Assets/_Scripts/Controller/Toys/ArkwayToy.cs.meta`
  - `Assets/_Scripts/Controller/Toys/ArkwayVoyageHud.cs`
  - `Assets/_Scripts/Controller/Toys/ArkwayVoyageHud.cs.meta`
  - `Assets/_Scripts/Controller/Toys/CellConveyor.cs`
  - `Assets/_Scripts/Controller/Toys/CellConveyor.cs.meta`
  - `Assets/_Scripts/Controller/Toys/ToyContext.cs`
  - `Assets/_Scripts/Controller/Toys/ToyboxController.cs`
  - `Assets/_Scripts/Data/Enums/RewardKind.cs`
  - `Assets/_Scripts/Data/Enums/RewardKind.cs.meta`
  - `Assets/_Scripts/Data/Structs/RewardGrant.cs`
  - `Assets/_Scripts/Data/Structs/RewardGrant.cs.meta`
  - … and 36 more

### `2c2a41a65` — feat(toys): the Arkway — a corridor of cells an Ark sails (the cellular Wanderway)

_Claude, 2026-09-01 03:00:47 +0000_

```text
The Arkway is the Wanderway's proposition raised one level: instead of a belt
of prism assemblies, it recycles whole CELLS. Fly the toy and a voyage begins —
three real satellite Cells stand at once (previous / current / next), drawn
shuffle-bag from the cell selector's own rotation, thinned by
SatellitePrismStride — and an Ark sails the corridor at its own unhurried pace.
A stepping stone toward faction missions.

The Ark is a new FUNDAMENTAL (added at the prompter's explicit request, per the
CLAUDE.md curation process): a prism-bodied mothership that wears a domain,
travels the hypersea, and lives or dies by the food web. Its hull is ordinary
grazeable conserved mass laid through the canonical PrismTrailBuilder path, so
the whole protect-the-Ark mechanic is composition, not construction: traversal
cells set NucleusIsControlZone = false (whole-cell VOLUME control + the legacy
opposing-domain diet), fauna waves spawn in the controlling colour, and
therefore taking a cell's volume IS protecting the Ark — its own domain's fauna
cannot eat it, opposing waves hunt it. Last hull prism lost = the voyage resets.
Players are leashed to a cell radius of the Ark (telegraphed countdown, then a
recall to its side); exits are the disembark dinghy trailing the Ark, a second
toy pass, leaving freestyle, or the Ark falling.

Three small platform capabilities carry it:
- Cell.SatelliteEcologyEnabled — the one opt-in through the mode preview's
  structure-only satellite gate (a traversal cell RUNS its life spawner).
- Cell.RuntimePopulationScale — runtime population multiplier composed into
  ResolveFauna/FloraPopulation on the profile scaler's own contract.
  Production gating only; identity at 1; never culls.
- PrismSpatialIndex.NotifyCellChanged — the mover's cell re-bind (UpdatePosition
  re-buckets but never re-bound the cell; nothing that moved crossed a cell
  before the Ark).

The Ark moves the way fauna move (container transform + the
Prism.NotifyPositionChanged mover contract per frame, cell re-bind on a coarse
cadence). Cell recycling is gated on the retiring cell's whole membrane sphere
being outside the camera frustum (the microscene conveyor's own removal gate);
voyage end reposes the player home first, withers the Ark back to its pool, and
strikes the corridor pool-safely with the 150-per-frame drain.

Budget: three cells at stride 4 ≈ ≤30k prisms — the Wanderway-stock envelope —
against a bare-canvas home world (the voyage opens with the Wanderway's own
host-cell revert). Record: Docs/ECOSYSTEM.md §41, Docs/ToySystem/ARCHITECTURE.md
§ Arkway, CLAUDE.md fundamentals list.

Verification status: authored and reviewed headless (Roslyn parse +
check_conditional_compilation clean); not yet opened in the Unity editor —
needs an in-editor pass (Setup Freestyle Toybox re-run is idempotent; the
Toy_Arkway.asset + Toybox.asset registration are authored in this commit).
```

```text
 Assets/Resources/Toybox.asset                                        |   1 +
 Assets/_SO_Assets/Toys/Toy_Arkway.asset                              |  36 +++
 Assets/_SO_Assets/Toys/Toy_Arkway.asset.meta                         |   8 +
 Assets/_Scripts/Controller/Environment/Ark.cs                        | 348 +++++++++++++++++++++++
 Assets/_Scripts/Controller/Environment/Ark.cs.meta                   |  11 +
 Assets/_Scripts/Controller/Environment/Cell.cs                       |  52 +++-
 Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs             |  24 ++
 Assets/_Scripts/Controller/Toys/ArkwayRun.cs                         | 497 +++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Toys/ArkwayRun.cs.meta                    |  11 +
 Assets/_Scripts/Controller/Toys/ArkwayToy.cs                         | 192 +++++++++++++
 Assets/_Scripts/Controller/Toys/ArkwayToy.cs.meta                    |  11 +
 Assets/_Scripts/Controller/Toys/ArkwayVoyageHud.cs                   |  91 ++++++
 Assets/_Scripts/Controller/Toys/ArkwayVoyageHud.cs.meta              |  11 +
 Assets/_Scripts/Controller/Toys/CellConveyor.cs                      | 410 +++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Toys/CellConveyor.cs.meta                 |  11 +
 Assets/_Scripts/Controller/Toys/ToyContext.cs                        |   8 +
 Assets/_Scripts/Controller/Toys/ToyboxController.cs                  |   8 +
 Assets/_Scripts/Editor/Codex/ToolCodexHarvester.cs                   |  41 +++
 Assets/_Scripts/Editor/ToyboxSetupTool.cs                            |  34 ++-
 Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs      | 146 ++++++++++
 Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs.meta |  11 +
 CLAUDE.md                                                            |  22 ++
 Docs/ECOSYSTEM.md                                                    | 110 ++++++++
 Docs/ToySystem/ARCHITECTURE.md                                       |  59 +++-
 Docs/ToySystem/BACKLOG.md                                            |  36 +++
 25 files changed, 2175 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2321 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Ark.cs b/Assets/_Scripts/Controller/Environment/Ark.cs
new file mode 100644
index 000000000..40eb8481a
--- /dev/null
+++ b/Assets/_Scripts/Controller/Environment/Ark.cs
@@ -0,0 +1,348 @@
+using System;
+using System.Collections.Generic;
+using System.Threading;
+using CosmicShore.Data;
+using CosmicShore.Utility;
+using Cysharp.Threading.Tasks;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// An <b>Ark</b> - a mothership: a prism-bodied home that travels the hypersea, wears a
+    /// domain, and lives or dies by the food web. The Ark is a first-class fundamental (added at
+    /// the prompter's request - CLAUDE.md, "The fundamentals"): it is the anchor of the faction-
+    /// mission arc, and its first vehicle is the Arkway toy, where it sets the pace of a voyage
+    /// through a corridor of cells.
+    ///
+    /// Everything about it composes with the shipped fundamentals instead of adding parallel
+    /// systems:
+    ///
+    ///   • Its HULL is ordinary conserved prism mass, laid through the canonical
+    ///     <see cref="PrismTrailBuilder"/> path in its owner's domain - so it registers with the
+    ///     spatial index, binds to the cell that contains it, feeds that cell's volume books, and
+    ///     is GRAZEABLE: in a nucleus-less cell, herbivores of another domain eat it and
+    ///     herbivores of its own never do. Protecting an Ark is therefore controlling the cell -
+    ///     no aggro system, no scripted threat.
+    ///   • It MOVES the way a creature moves: the hull prisms ride one container transform, and
+    ///     every frame each prism honours the mover contract
+    ///     (<see cref="Prism.NotifyPositionChanged"/> - spatial index + shell + render entity),
+    ///     exactly as fauna body prisms do. On a coarse cadence each prism also re-binds to the
+    ///     cell that actually contains it (<see cref="PrismSpatialIndex.NotifyCellChanged"/>),
+    ///     so the food web that can see it is always the local one. Between cells it binds to
+    ///     nothing - open water is nobody's feeding ground.
+    ///   • It DIES the way a creature dies - when its last hull prism is destroyed - but it is
+    ///     deliberately NOT a <see cref="LifeForm"/>: no elemental heart (the lifeform-crystal
+    ///     invariant governs lifeforms; an Ark is a vessel-like home, not a creature), no
+    ///     starvation clock, no reproduction. Its only deaths are active forces: fauna
+    ///     consumption and player abilities.
+    ///
+    /// The Ark itself never removes mass and never runs a timer over anyone else's - the one
+    /// clock it owns is its own unhurried course.
+    /// </summary>
+    public sealed class Ark : MonoBehaviour
+    {
+        // ── Hull proportions ─────────────────────────────────────────────────
+        // The hull is a spindle: rings of plates along the keel axis with a lens radius profile,
+        // staggered ring to ring so the plating reads as a shell rather than a stack of hoops.
+        const float PlateSpacing = 9f;                    // arc length per plate around a ring
+        const float RingSpacing = 9.5f;                   // keel distance between rings
+        const float RadiusFactor = 0.22f;                 // max hull radius = length × this
+        static readonly Vector3 PlateScale = new(2.6f, 2.6f, 4.8f);
+        static readonly Vector3 CapScale = new(3.4f, 3.4f, 6.4f);
+
+        /// <summary>Scale a retiring hull prism withers to before returning to the pool
+        /// (the Wanderway tether's own exit - continuity of existence is not waived).</summary>
+        static readonly Vector3 RetiredScale = new(0.02f, 0.02f, 0.02f);
+        const float WitherSeconds = 0.8f;
+
+        const float AliveScanSeconds = 0.5f;              // hull-integrity scan cadence
+        const float CellRebindSeconds = 2.5f;             // cell re-bind + grid re-file cadence
+        const float TurnDegreesPerSecond = 40f;           // how fast the bow swings onto course
+
+        readonly List<Prism> _prisms = new();
+        Trail _trail;
+        float _speed;
+        Vector3 _destination;
+        bool _hasDestination;
+        bool _laying;
+        bool _layComplete;
+        bool _hullLost;
+        bool _retiring;
+        float _nextAliveScanAt;
+        float _nextRebindAt;
+        TMPro.TMP_Text _label;
```

</details>

### `4c0b18468` — fix(toys): ArkwayToyDefinitionSO missing 'using CosmicShore.Data' for ToyCategory

_Claude, 2026-09-01 03:06:43 +0000_

```text
 Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs b/Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs
index b4da949fb..1f0e04f21 100644
--- a/Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs
+++ b/Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs
@@ -1,4 +1,5 @@
 using System.Collections.Generic;
+using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using UnityEngine;
```

</details>

### `c3b555da5` — fix(toys): apply the Arkway adversarial-review findings

_Claude, 2026-09-01 07:00:28 +0000_

```text
Eight findings from the change-set review (5 confirmed by two-skeptic
verification, 3 verified by hand after their verifiers hit the session limit):

- BLOCKER: the leash recall teleported the player exactly onto the disembark
  dinghy's armed trigger (recall point == dinghy point), ending the voyage as
  punishment for straying. Recall now lands on the Ark's FLANK, ~1.46 hull
  lengths from the dinghy.
- Cell.gridTracked now remembers each prism's FILED position and RemoveBlock
  decrements the density grids there, never at a re-read transform.position:
  mass that MOVES between add and remove (the Ark's hull, gyroid bonding)
  decremented the wrong bucket and stranded permanent phantom counts in the
  fauna steering grids — and a destroyed ref skipped grid removal entirely
  (the same leak from another door, now also closed).
- Voyage-end corridor retirement is QUEUED behind the off-screen frustum gate
  (RetireAllWhenUnseen) instead of force-struck: the corridor sits a few
  thousand units out, not the preview's 120k, so an ungated strike could pop
  whole worlds out in view. The next voyage's Begin force-strikes any
  remainder only after its veil is up (unseen by construction), and awaits
  conveyor idle first.
- CellConveyor drain bookkeeping: _draining bool → _drains counter (two
  overlapping drains both cleared the bool, reopening the one-at-a-time
  gate), and Begin refuses while the previous corridor is still retiring.
- TickCorridor's cannot-stand-a-next-cell End no longer falls through into
  TickLeash on the same frame (NRE on the nulled _ark).
- Leash hysteresis band no longer freezes the countdown while silently
  spending the grace: a breach clears only at genuine re-entry, and the
  countdown keeps displaying across the band.
- Ark.RetireAsync's pool-return was dead code (environment-pool prisms carry
  no OnReturnToPool handler): the retire is now honestly documented as the
  environment-mass destroy-drain, the defensive pool branch detaches first,
  and the per-frame sync loops skip devoured prisms (Consume leaves the
  GameObject ACTIVE with destroyed=true).
- Toy_Arkway placement angle 180 → 210: Toy_LifeformMatrix already owns 180,
  and two toys on one angle stack at the same point of the membrane ring.
```

```text
 Assets/_SO_Assets/Toys/Toy_Arkway.asset         |  2 +-
 Assets/_Scripts/Controller/Environment/Ark.cs   | 28 ++++++++++++++++++-----
 Assets/_Scripts/Controller/Environment/Cell.cs  | 44 +++++++++++++++++++++----------------
 Assets/_Scripts/Controller/Toys/ArkwayRun.cs    | 66 ++++++++++++++++++++++++++++++++++++++++++-------------
 Assets/_Scripts/Controller/Toys/CellConveyor.cs | 53 ++++++++++++++++++++++++++++++++++++--------
 Assets/_Scripts/Editor/ToyboxSetupTool.cs       |  4 +++-
 Docs/ECOSYSTEM.md                               | 12 ++++++----
 Docs/ToySystem/ARCHITECTURE.md                  | 11 ++++++----
 8 files changed, 161 insertions(+), 59 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 443 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Ark.cs b/Assets/_Scripts/Controller/Environment/Ark.cs
index 40eb8481a..7178e99c9 100644
--- a/Assets/_Scripts/Controller/Environment/Ark.cs
+++ b/Assets/_Scripts/Controller/Environment/Ark.cs
@@ -232,11 +232,15 @@ namespace CosmicShore.Gameplay
             // each prism's spatial-index position, shell pose and render-entity matrix follow the
             // transform (Prism.NotifyPositionChanged is cheap when the occupancy bucket is
             // unchanged).
+            // NOTE the !destroyed gate: a devoured environment prism never deactivates - Consume
+            // → SetupDestruction leaves the GameObject ACTIVE with destroyed=true, hidden and
+            // collider-less - so activeInHierarchy alone would keep paying sync for every prism
+            // the food web has already taken.
             if (moved)
                 for (int i = 0; i < _prisms.Count; i++)
                 {
                     var prism = _prisms[i];
-                    if (prism && prism.gameObject.activeInHierarchy)
+                    if (prism && !prism.destroyed && prism.gameObject.activeInHierarchy)
                         prism.NotifyPositionChanged();
                 }
 
@@ -251,7 +255,8 @@ namespace CosmicShore.Gameplay
                     for (int i = 0; i < _prisms.Count; i++)
                     {
                         var prism = _prisms[i];
-                        if (prism && prism.gameObject.activeInHierarchy && prism.SpatialIndexId >= 0)
+                        if (prism && !prism.destroyed && prism.gameObject.activeInHierarchy
+                            && prism.SpatialIndexId >= 0)
                             index.NotifyCellChanged(prism.SpatialIndexId);
                     }
             }
@@ -293,10 +298,15 @@ namespace CosmicShore.Gameplay
 
         /// <summary>
         /// End-of-voyage exit: the surviving hull withers out (one grow-clock re-stamp per prism,
-        /// the Wanderway tether's own retirement) and returns to the pool it was laid from, then
-        /// the Ark destroys itself. This is the voyage apparatus being struck by the explicit,
-        /// player-initiated end of the toy - the same event class as a satellite world's strike
-        /// (Docs/ECOSYSTEM.md §19) - never a decay: a live voyage never calls it.
+        /// the Wanderway tether's own retirement) and is then retired the way environment mass
+        /// is retired everywhere - hull prisms come from the environment pool and carry NO
+        /// pool-return handler (the strike partition test is <c>OnReturnToPool != null</c>), so
+        /// they are destroy-drained with the Ark's own root, exactly like a swapped world's
+        /// environment. A prism that DOES wear a return handler is handed back to its pool
+        /// instead - defensive, for a future trail-pooled hull prefab. This is the voyage
+        /// apparatus being struck by the explicit, player-initiated end of the toy - the same
+        /// event class as a satellite world's strike (Docs/ECOSYSTEM.md §19) - never a decay:
+        /// a live voyage never calls it.
         /// </summary>
         public async UniTask RetireAsync(CancellationToken ct)
         {
@@ -334,11 +344,17 @@ namespace CosmicShore.Gameplay
                 cancelled = true; // still hand the pool its prisms back below
             }
 
+            // Pool-carrying prisms (none today - see the summary) go home; everything else is
+            // instantiated-class mass and dies with the root below, ~150 prisms in one frame
+            // (well under the 150-per-frame drain slice a 10-20k world needs).
             for (int i = 0; i < _prisms.Count; i++)
             {
                 var prism = _prisms[i];
                 if (prism && prism.gameObject.activeInHierarchy && prism.OnReturnToPool != null)
+                {
+                    prism.transform.SetParent(null, false);
                     prism.ReturnToPool();
+                }
             }
             _prisms.Clear();
 
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index ec92b8782..a92edb458 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -262,7 +262,14 @@ namespace CosmicShore.Gameplay
         // be led to mass they cannot eat - so RemoveBlock has to know which
         // prisms the grids really hold (the nucleus radius can change between
         // Add and Remove; re-deriving membership would desync bucket counts).
-        readonly HashSet<Prism> gridTracked = new();
+        // Grid-registered prisms → the POSITION their grid entries were filed at. The value is
+        // what keeps Add/Remove symmetric for mass that MOVES (the Ark's hull, gyroid bonding):
```

</details>

### `19a93b27c` — docs(skills): two ecology traps from the Arkway session — mover grid symmetry, environment-prism lifecycle

_Claude, 2026-09-01 15:18:24 +0000_

```text
 .claude/skills/ecology/SKILL.md | 20 +++++++++++++++++++-
 1 file changed, 19 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/ecology/SKILL.md b/.claude/skills/ecology/SKILL.md
index 55d372e64..559a8a6e9 100644
--- a/.claude/skills/ecology/SKILL.md
+++ b/.claude/skills/ecology/SKILL.md
@@ -76,7 +76,25 @@ what the carve-out silently broke — see the traps below.
   restores the envelope exactly - §35's "fit the PRISM, never the pattern"). Full table + the
   clearance and hinge consequences: the `asset-surgery` skill, "Trap: a SHIELD's size is not the
   prism's size".
-- **Static bookkeeping outlives the world it describes.** A `static` registry/claim book/
+- **A positional Add/Remove pair is asymmetric for anything that MOVES.** `Cell.AddBlock` files
+  the fauna density grids at the position read at add time; a `RemoveBlock` that re-reads
+  `transform.position` decrements whichever bucket the prism has since wandered into, stranding
+  a permanent phantom count in the bucket it was actually filed under — fauna then steer at
+  empty space, forever, with nothing logging. Movers exist (fauna bodies are exempt as
+  volume-only, but the Ark's hull and gyroid bonding are grid-tracked movers), so the cell now
+  stores each grid entry's FILED position (`gridTracked` is a `Dictionary<Prism, Vector3>`) and
+  removes there. The general rule: any add/remove bookkeeping keyed on a re-read position is a
+  leak for movers AND for destroyed refs (whose transform is unreadable at remove time) —
+  remember what you filed, remove what you remembered. Docs/ECOSYSTEM.md §41.
+- **An environment prism is not "pooled", and a devoured one never deactivates.** The
+  pooled-vs-instantiated partition everywhere (cell swap, satellite strike, tether recycle) is
+  `Prism.OnReturnToPool != null` — and `EnvironmentPrismPool` never wires it, so every
+  environment-laid prism is INSTANTIATED-class mass (destroy-drained on retirement; the pool's
+  own `TryRelease` is called only by the swap drain). And `Prism.Consume` → `SetupDestruction`
+  leaves the GameObject ACTIVE — `destroyed = true`, render hidden, collider off — so an
+  aliveness test on `activeInHierarchy` alone counts eaten mass as alive, and a per-frame loop
+  gated on it keeps paying for corpses. Test `!prism.destroyed` too, and never write a
+  retirement that waits for a pool return that structurally cannot come. A `static` registry/claim book/
   frontier that coordinates a population survives every cell teardown — `Cell.ResetCell`,
   `Initialize`, and the Cell-Selector world swap all destroy the lifeforms and leave the
   static state standing, so the NEXT world inherits the dead one's entries and acts on
```

</details>

### `e1b27fe7a` — feat(economy): route every crystal reward through one RewardService

_Claude, 2026-09-01 18:35:55 +0000_

```text
The only live earn path in the game wrote the wallet directly from a UI
component, with the payout table serialized on that component and duplicated
across nine gameplay scenes. Everything else that was designed to grant a
reward - quests, daily challenge tiers, training intensity tiers, the daily
free claim - was built and left unwired, and three of those consume a claim
while granting nothing.

This lands the plumbing without changing any payout:

- RewardGrant / RewardGranted / RewardKind / RewardDedupe: producers describe
  WHAT was earned and never touch the wallet.
- RewardTableSO (Resources/RewardTable): the payout numbers, in one asset.
  Values unchanged - Docs/ECONOMY_TABLES.md Table 2, still 200/50/0.
- RewardService: the single grant door. Owns the wallet write, the failure
  boundary, dedupe and the announcement. PlayerDataService remains the sole
  writer of ProfileEconomy; this routes to it. Account-scoped dedupe reuses
  the existing UnlockedRewardIds list, so once-ever rewards need no new cloud
  schema - and that same list is the door skins and toys come through later.
- ScriptableEventRewardGranted: one channel every reward display listens on,
  carrying the before/after balance so two displays cannot disagree.
- Scoreboard now reports a placement and latches its payout once per game,
  instead of computing and granting it itself.

RewardService also records the latest grant and a sequence number, because a
SOAP event reaches only whoever is already listening: the Scoreboard awards
while building its cards and activates its panel afterwards, so the end-game
display is structurally unable to hear the raise. A display compares the
sequence against the last one it showed and catches up.

Verification: the nine new/changed C# files type-check against a stub harness
(Roslyn, real signatures transcribed from the repo), and the gate was proven
to fire on an undeclared body identifier, a wrong member name and a wrong
arity before being trusted. Scoreboard.cs passes the syntax-class pass.
Asset field parity checked against the C#; the generator is idempotent under
--check. Not verified in the Unity editor - no editor available in this
session.
```

```text
 Assets/Resources/Channels/RewardGrantedChannel.asset                  |  27 ++++
 Assets/Resources/Channels/RewardGrantedChannel.asset.meta             |   8 ++
 Assets/Resources/RewardTable.asset                                    |  16 +++
 Assets/Resources/RewardTable.asset.meta                               |   8 ++
 Assets/_Scripts/Data/Enums/RewardKind.cs                              |  37 ++++++
 Assets/_Scripts/Data/Enums/RewardKind.cs.meta                         |   2 +
 Assets/_Scripts/Data/Structs/RewardGrant.cs                           |  92 ++++++++++++++
 Assets/_Scripts/Data/Structs/RewardGrant.cs.meta                      |   2 +
 Assets/_Scripts/Data/Structs/RewardGranted.cs                         |  42 +++++++
 Assets/_Scripts/Data/Structs/RewardGranted.cs.meta                    |   2 +
 Assets/_Scripts/ScriptableObjects/RewardTableSO.cs                    |  96 +++++++++++++++
 Assets/_Scripts/ScriptableObjects/RewardTableSO.cs.meta               |   2 +
 Assets/_Scripts/ScriptableObjects/SOAP/ScriptableRewardGrant.meta     |   8 ++
 .../SOAP/ScriptableRewardGrant/EventListenerRewardGranted.cs          |  28 +++++
 .../SOAP/ScriptableRewardGrant/EventListenerRewardGranted.cs.meta     |   2 +
 .../SOAP/ScriptableRewardGrant/ScriptableEventRewardGranted.cs        |  17 +++
 .../SOAP/ScriptableRewardGrant/ScriptableEventRewardGranted.cs.meta   |   2 +
 Assets/_Scripts/System/Rewards.meta                                   |   8 ++
 Assets/_Scripts/System/Rewards/RewardService.cs                       | 212 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/System/Rewards/RewardService.cs.meta                  |   2 +
 Assets/_Scripts/UI/Elements/RewardPayoutPanel.cs                      | 151 +++++++++++++++++++++++
 Assets/_Scripts/UI/Elements/RewardPayoutPanel.cs.meta                 |   2 +
 Assets/_Scripts/UI/Elements/RewardToastDriver.cs                      |  61 +++++++++
 Assets/_Scripts/UI/Elements/RewardToastDriver.cs.meta                 |   2 +
 Assets/_Scripts/UI/Scoreboard.cs                                      |  67 +++++-----
 Tools/Build/author_reward_assets.py                                   | 196 +++++++++++++++++++++++++++++
 26 files changed, 1054 insertions(+), 38 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1118 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Data/Enums/RewardKind.cs b/Assets/_Scripts/Data/Enums/RewardKind.cs
new file mode 100644
index 000000000..3b5efccc0
--- /dev/null
+++ b/Assets/_Scripts/Data/Enums/RewardKind.cs
@@ -0,0 +1,37 @@
+namespace CosmicShore.Data
+{
+    /// <summary>
+    /// What a reward actually hands the player. This is the axis the reward system dispatches
+    /// on, so adding a member is how the system grows past crystals - never a parallel grant
+    /// path beside <c>RewardService</c>.
+    /// </summary>
+    public enum RewardKind
+    {
+        /// <summary>Soft currency, added to <c>ProfileEconomy.CrystalBalance</c>.</summary>
+        Crystals = 0,
+
+        /// <summary>
+        /// A permanent, non-consumable unlock recorded by id in
+        /// <c>ProfileEconomy.UnlockedRewardIds</c>. This is the door skins and toys come through
+        /// when they land - they are entitlements, and the profile already persists the list.
+        /// </summary>
+        Entitlement = 1,
+    }
+
+    /// <summary>
+    /// How hard a grant refuses to happen twice. A reward that is only ever meant to land once
+    /// carries its own key; the alternative is every producer inventing its own latch, and a
+    /// latch a producer forgets is a duplicate payout the player keeps.
+    /// </summary>
+    public enum RewardDedupe
+    {
+        /// <summary>Grant every time it is asked for. Correct for a repeatable payout.</summary>
+        None = 0,
+
+        /// <summary>
+        /// Grant at most once for this account, ever. Deduped against the same persisted
+        /// <c>UnlockedRewardIds</c> list entitlements use, so this needs no new cloud schema.
+        /// </summary>
+        Account = 1,
+    }
+}
diff --git a/Assets/_Scripts/Data/Structs/RewardGrant.cs b/Assets/_Scripts/Data/Structs/RewardGrant.cs
new file mode 100644
index 000000000..5f9cddeeb
--- /dev/null
+++ b/Assets/_Scripts/Data/Structs/RewardGrant.cs
@@ -0,0 +1,92 @@
+using System;
+
+namespace CosmicShore.Data
+{
+    /// <summary>
+    /// One thing the game is handing the player, described completely enough that
+    /// <c>RewardService</c> can grant it, dedupe it, report it to analytics and hand it to the
+    /// UI without the producer knowing about any of those.
+    ///
+    /// Producers describe WHAT was earned; they never touch the wallet. That split is the whole
+    /// point - before it, the only earn path in the game wrote the wallet directly from a UI
+    /// component, and every other designed reward path was left unwired.
+    ///
+    /// Plain serializable struct with public fields, matching the other SOAP payloads
+    /// (<c>CrystalStats</c>, <c>PrismStats</c>, <c>GameToastData</c>): SOAP's
+    /// <c>ScriptableEvent&lt;T&gt;</c> serializes a <c>_debugValue</c> of this type for the
+    /// inspector's debug-raise, and Unity does not serialize readonly fields. Construct through
+    /// the factories below rather than by hand - they are what make an invalid grant hard to
+    /// build.
+    /// </summary>
+    [Serializable]
+    public struct RewardGrant
+    {
+        /// <summary>Which payout channel this uses. Decides what <see cref="Amount"/> and
+        /// <see cref="EntitlementId"/> mean.</summary>
+        public RewardKind Kind;
+
+        /// <summary>Crystal count for <see cref="RewardKind.Crystals"/>. Ignored otherwise.</summary>
+        public int Amount;
+
+        /// <summary>Unlock id for <see cref="RewardKind.Entitlement"/>. Ignored otherwise.</summary>
```

</details>

### `e368e7c61` — refactor(economy): strip the per-scene crystal payout table

_Claude, 2026-09-01 18:37:17 +0000_

```text
Nine gameplay scenes each carried their own serialized copy of the payout
numbers, and five more surfaces - Salvo, Brood Rush, Cellular Duel, 2v2 Co-op
and GameCanvas-HexRace.prefab - still carried the retired `winnerCrystalReward`
key, which no longer resolves to a field. Those five were therefore paying out
of the C# field initializer while the other nine paid out of their serialized
copy. They agreed only by coincidence; the first retune would have split the
game's economy in two without a diff that mentioned it.

All 14 now read Resources/RewardTable.asset. No number changes.

Done with a scoped, reversible pass rather than by hand: the stripper matches
the key only inside a document whose m_Script is Scoreboard or one of its two
subclasses (which serialize the inherited field under their own guids), reports
any hit under a foreign component instead of touching it, and refuses to write
unless the result differs from the original by exactly the removed lines.

Verified: 14 deletions, 0 insertions, no non-blank insertions anywhere in the
diff, no residual key in any scene or prefab, YAML document count and trailing
newline unchanged in all 14 files, and the pass is idempotent under --check.
```

```text
 Assets/_Prefabs/GameCanvas-HexRace.prefab                             |   1 -
 .../_Scenes/Multiplayer Scenes/ArcadeGameMultiplayer2v2CoOpVsAI.unity |   1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameAstroLeague.unity           |   1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity                 |   1 -
 .../MinigameCrystalCaptureMultiplayer_Gameplay.unity                  |   1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity              |   1 -
 .../Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity  |   1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity        |   1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameNucleusRush.unity           |   1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity               |   1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameRibcage.unity               |   1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameSalvo.unity                 |   1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameScarabScramble.unity        |   1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeLiberation.unity    |   1 -
 Tools/Build/strip_scoreboard_payout_overrides.py                      | 142 ++++++++++++++++++++++++++++++++
 15 files changed, 142 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 148 lines)</summary>

```diff
diff --git a/Tools/Build/strip_scoreboard_payout_overrides.py b/Tools/Build/strip_scoreboard_payout_overrides.py
new file mode 100644
index 000000000..1164d6734
--- /dev/null
+++ b/Tools/Build/strip_scoreboard_payout_overrides.py
@@ -0,0 +1,142 @@
+#!/usr/bin/env python3
+"""Remove the per-scene crystal payout table from every Scoreboard component.
+
+The payout used to be a serialized `List<int> placementCrystalRewards` on the
+Scoreboard, so nine gameplay scenes each carried their own copy of the economy and
+five more surfaces still carried the RETIRED `winnerCrystalReward` key - which no
+longer resolves to anything, so those five were paying out of the C# field
+initializer while the other nine paid out of their serialized copy. They agreed by
+luck; the first retune would have split them.
+
+Both keys now go, and the numbers live in Resources/RewardTable.asset
+(`RewardTableSO`), which every Scoreboard reads.
+
+Scoped by the enclosing m_Script guid - Scoreboard AND its two subclasses, which
+serialize the inherited field under their own guids. A bare line delete would
+happily strip a same-named key from an unrelated component.
+
+    python3 Tools/Build/strip_scoreboard_payout_overrides.py           # write
+    python3 Tools/Build/strip_scoreboard_payout_overrides.py --check   # verify only
+"""
+import pathlib
+import re
+import sys
+
+ROOT = pathlib.Path(__file__).resolve().parents[2]
+ASSETS = ROOT / "Assets"
+
+# Scoreboard.cs + the two subclasses that inherit the field. Read from the .meta
+# files rather than hardcoded, so a rename cannot silently narrow the sweep.
+SCRIPTS = [
+    "_Scripts/UI/Scoreboard.cs",
+    "_Scripts/UI/DuelForCellScoreboard.cs",
+    "_Scripts/UI/CoOpScoreBoard.cs",
+]
+
+DEAD_KEYS = ("placementCrystalRewards", "winnerCrystalReward")
+
+
+def script_guids():
+    guids = {}
+    for rel in SCRIPTS:
+        meta = ASSETS / (rel + ".meta")
+        if not meta.exists():
+            sys.exit(f"missing meta for {rel}")
+        m = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text()[:200], re.M)
+        if not m:
+            sys.exit(f"no guid in {meta}")
+        guids[m.group(1)] = rel
+    return guids
+
+
+def strip(text, guids):
+    """Drop the dead keys, but only inside a document whose m_Script is one of ours.
+
+    Returns (new_text, removed, rejected) - `rejected` is every hit that matched the
+    key name under some OTHER component, which must stay empty for the pass to be
+    provably total.
+    """
+    out, removed, rejected = [], [], []
+    current_guid = None
+
+    for line in text.split("\n"):
+        if line.startswith("--- !u!"):
+            current_guid = None                      # new document, ownership unknown
+        else:
+            m = re.match(r"^  m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", line)
+            if m:
+                current_guid = m.group(1)
+
+        key = re.match(r"^  (\w+):", line)
+        if key and key.group(1) in DEAD_KEYS:
+            if current_guid in guids:
+                removed.append(line.strip())
+                continue                              # drop the line
```

</details>

### `e4e852f7e` — fix(economy): point every crystal balance at the same wallet

_Claude, 2026-09-01 18:39:52 +0000_

```text
The Store read the PlayFab inventory shelf, which has been disabled for as long
as PlayFab has. Its balance was therefore 0 forever, and never even refreshed:
the two CatalogManager events that drove it cannot fire, so the screen showed
whatever placeholder text its prefab shipped with, while the Hangar next door
showed the real cloud-persisted number. One wallet, two answers.

- StoreScreen now reads PlayerDataService and subscribes to its balance event,
  refreshing on screen entry rather than on a catalog load that never happens.
  The count-up roll is kept and re-reads the wallet on its final frame, so a
  reward landing mid-roll cannot leave a stale total on screen. The ticket
  balance is a separate PlayFab currency and is deliberately untouched.
- PlayerDataService.ApplyPendingDebugCrystals goes through AddCrystals instead
  of writing CrystalBalance directly. It was skipping LifetimeCrystalsEarned
  and the analytics event, so debug crystals pushed the balance above the
  lifetime total that is meant to reconcile with it.

PurchaseItemCard still reads the dead shelf and is left alone: it is referenced
by no prefab or scene, and repointing its balance would imply a purchase flow
that does not work.

Verified: the changed StoreScreen members were extracted from the shipped file
by signature and brace matching, then type-checked against real transcribed
signatures. Both files pass the syntax-class pass. Not verified in the Unity
editor - no editor available in this session.
```

```text
 Assets/_Scripts/UI/Screens/StoreScreen.cs     | 38 +++++++++++++++++++++++++++++++-------
 Assets/_Scripts/UI/Views/PlayerDataService.cs |  9 +++++----
 2 files changed, 36 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 100 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Screens/StoreScreen.cs b/Assets/_Scripts/UI/Screens/StoreScreen.cs
index f9cd0a8e8..17326cdc8 100644
--- a/Assets/_Scripts/UI/Screens/StoreScreen.cs
+++ b/Assets/_Scripts/UI/Screens/StoreScreen.cs
@@ -48,7 +48,14 @@ namespace CosmicShore.UI
             //CaptainManager.OnLoadCaptainData += UpdateView;
             CatalogManager.OnLoadInventory += UpdateView;
             CatalogManager.OnInventoryChange += UpdateTicketBalance;
-            CatalogManager.OnCurrencyBalanceChange += UpdateCrystalBalance;
+
+            // Crystals come from PlayerDataService - the live, cloud-persisted wallet the
+            // Hangar and every reward payout use. This screen used to read the PlayFab
+            // inventory shelf instead, which has been disabled for as long as PlayFab has,
+            // so its balance was 0 forever AND never refreshed (the catalog events that
+            // drove it cannot fire). Two screens, two answers, for one wallet.
+            PlayerDataService.OnCrystalBalanceChanged += HandleCrystalBalanceChanged;
+            UpdateCrystalBalance();
         }
 
         void OnDisable()
@@ -56,9 +63,17 @@ namespace CosmicShore.UI
             //CaptainManager.OnLoadCaptainData -= UpdateView;
             CatalogManager.OnLoadInventory -= UpdateView;
             CatalogManager.OnInventoryChange -= UpdateTicketBalance;
-            CatalogManager.OnCurrencyBalanceChange -= UpdateCrystalBalance;
+
+            PlayerDataService.OnCrystalBalanceChanged -= HandleCrystalBalanceChanged;
         }
 
+        void HandleCrystalBalanceChanged(int _) => UpdateCrystalBalance();
+
+        /// <summary>The live wallet. 0 before the profile service exists, which is a real
+        /// state during boot rather than an error.</summary>
+        static int CurrentCrystalBalance =>
+            PlayerDataService.Instance != null ? PlayerDataService.Instance.GetCrystalBalance() : 0;
+
         void Start()
         {
             // Clear out placeholder captain cards
@@ -213,7 +228,14 @@ namespace CosmicShore.UI
 
         void UpdateCrystalBalance()
         {
-            StartCoroutine(UpdateBalanceCoroutine());
+            if (!CrystalBalance) return;
+
+            // Only roll the counter while the screen is live; StartCoroutine on a disabled
+            // object throws, and OnEnable can run before the object is fully active.
+            if (isActiveAndEnabled)
+                StartCoroutine(UpdateBalanceCoroutine());
+            else
+                CrystalBalance.text = CurrentCrystalBalance.ToString();
         }
 
         IEnumerator UpdateBalanceCoroutine()
@@ -222,9 +244,8 @@ namespace CosmicShore.UI
             // seeded), so parse defensively rather than throwing a FormatException that would
             // abort the coroutine.
             int.TryParse(CrystalBalance.text, out var crystalBalance);
-            var newCrystalBalance = CatalogManager.Instance.GetCrystalBalance();
-            CSDebug.Log($"UpdateBalanceCoroutine - initial Balance: {crystalBalance}, new Balance: {newCrystalBalance}");
-            var delta = crystalBalance- newCrystalBalance;
+            var newCrystalBalance = CurrentCrystalBalance;
+            var delta = crystalBalance - newCrystalBalance;
             var duration = 1f;
             var elapsedTime = 0f;
 
@@ -234,7 +255,10 @@ namespace CosmicShore.UI
                 yield return null;
                 elapsedTime += Time.unscaledDeltaTime;
             }
-            CrystalBalance.text = CatalogManager.Instance.GetCrystalBalance().ToString();   
+
+            // Re-read rather than settling on the value captured a second ago: a reward can
+            // land mid-roll, and the final frame must be the wallet, not the stale target.
+            CrystalBalance.text = CurrentCrystalBalance.ToString();
         }
     }
 }
\ No newline at end of file
```

</details>

### `4bf2cfb69` — feat(economy): add the two reward displays and the wirer that places them

_Claude, 2026-09-01 18:46:32 +0000_

```text
The end-game payout panel and the menu reward toast, plus a FrogletTools wirer
that binds them into the scenes.

RewardPayoutPanel replaces the bare "+N" badge on the winning score card, which
stated an amount and nothing else - not that it was the local player's, not what
it took their balance to. It reads RewardGranted and only that, so it cannot
disagree with the wallet write that produced it, and it counts up from the
PRE-grant balance carried on the event rather than re-reading the wallet, which
would already hold the new total. It hides on a CanvasGroup rather than
SetActive because it has to stay active to stay subscribed - and because a
reward popping into existence breaks the same continuity law a prism would.

RewardToastDriver posts through the menu's existing ToastChannel. It is correct
and currently silent: the only producer today raises in a gameplay scene. It is
the surface a daily-challenge or quest payout posts through the day one is
wired, and it deliberately does NOT catch up on a grant it missed - that payout
has already been shown on the end-game screen.

The wirer exists because the end-game scoreboard's wiring is per-SCENE and the
shared prefab is stale against it: GameCanvas.prefab's Scoreboard leaves
playerCardContainer unset and still serializes ten keys the script no longer
declares. Reading the prefab tells you almost nothing about what a scene shows,
so the binding is done against the loaded, merged hierarchy instead of authored
blind into YAML. It adopts before it creates - the scoreboard already carries an
authored Goodies cluster with a CrystalIcon and a CrystalsEarned label, written
since 2021 by the deprecated MiniGame path with a hardcoded 0, and where that
art exists the panel binds to it rather than building a parallel display.

Verification: compiled against a faithful stub harness with signatures
transcribed from the repo, and the gate was proven to fire on a wrong palette
arity and a wrong SerializedObject member before being trusted. That harness
caught two real API mismatches in this file (Banner and ColorButton both take
arguments the first draft omitted) that a no-stub pass swallows entirely, which
is why editor code gets the full harness. Not run in the Unity editor - no
editor available in this session, so the scenes are NOT yet wired.
```

```text
 Assets/_Scripts/Editor/Rewards.meta                       |   8 ++
 Assets/_Scripts/Editor/Rewards/RewardDisplayWirer.cs      | 326 ++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Editor/Rewards/RewardDisplayWirer.cs.meta |   2 +
 Tools/Build/author_reward_assets.py                       |   2 +
 4 files changed, 338 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 352 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/Rewards/RewardDisplayWirer.cs b/Assets/_Scripts/Editor/Rewards/RewardDisplayWirer.cs
new file mode 100644
index 000000000..8823e9bb0
--- /dev/null
+++ b/Assets/_Scripts/Editor/Rewards/RewardDisplayWirer.cs
@@ -0,0 +1,326 @@
+using System;
+using System.Collections.Generic;
+using System.Linq;
+using CosmicShore.Editor.Froglet;
+using CosmicShore.ScriptableObjects;
+using CosmicShore.UI;
+using TMPro;
+using UnityEditor;
+using UnityEditor.SceneManagement;
+using UnityEngine;
+using UnityEngine.SceneManagement;
+
+namespace CosmicShore.Editor.Rewards
+{
+    /// <summary>
+    /// Puts the two reward displays on screen: the end-game payout panel in every gameplay
+    /// scene, and the reward toast driver in Menu_Main.
+    ///
+    /// This is a TOOL rather than hand-authored prefab YAML for one reason that is worth
+    /// stating: the end-game scoreboard's wiring is per-SCENE and the shared prefab is stale
+    /// against it. GameCanvas.prefab's Scoreboard leaves playerCardContainer unset and still
+    /// serializes ten keys the script no longer declares (SingleplayerView, MultiplayerView,
+    /// the four rematch fields...), so reading the prefab tells you almost nothing about what
+    /// a given scene actually shows. Only the merged, loaded hierarchy does - which is what
+    /// this runs against.
+    ///
+    /// Idempotent, and ADOPTS before it creates. The scoreboard already carries an authored
+    /// "Goodies" cluster with a CrystalIcon and a CrystalsEarned label, written since 2021 by
+    /// the deprecated MiniGame path with a hardcoded 0. Where that art exists this binds to it
+    /// rather than building a parallel display, so the payout lands in a slot a designer
+    /// already placed. It creates its own objects only where there is nothing to adopt.
+    /// </summary>
+    public class RewardDisplayWirer : EditorWindow
+    {
+        const string ToolName = "Reward Display Wirer";
+        const string PanelObjectName = "RewardPayout";
+        const string ToastObjectName = "RewardToastDriver";
+        const string MenuScene = "Menu_Main";
+
+        // Names the tool adopts rather than re-creates, in preference order.
+        static readonly string[] AmountLabelNames = { "CrystalsEarned", "RewardAmount" };
+        static readonly string[] BalanceLabelNames = { "CrystalBalance", "RewardBalance" };
+        static readonly string[] IconNames = { "CrystalIcon", "RewardIcon" };
+
+        readonly List<string> _log = new();
+        Vector2 _scroll;
+
+        static readonly FrogletToolShipContext Ship = new(ToolName)
+        {
+            ToolScriptPaths = new[] { "Assets/_Scripts/Editor/Rewards/RewardDisplayWirer.cs" },
+        };
+
+        [MenuItem("FrogletTools/Interface/Wire Reward Displays")]
+        [FrogletTool(FrogletToolCategory.Interface, Importance = 4,
+            Description = "Bind the end-game reward payout panel and the menu reward toast " +
+                          "into every scene that needs them. Idempotent; adopts existing art.")]
+        static void Open() => GetWindow<RewardDisplayWirer>("Reward Displays");
+
+        void OnGUI()
+        {
+            FrogletEditorPalette.Banner("Reward Displays",
+                "Wires RewardPayoutPanel into each gameplay scene's scoreboard and " +
+                "RewardToastDriver into Menu_Main. Safe to re-run.",
+                FrogletEditorPalette.ColorFor(FrogletToolCategory.Interface));
+
+            EditorGUILayout.HelpBox(
+                "Open the scene(s) you want wired, then Wire Open Scenes. " +
+                "Wire All Build Scenes opens each scene in Build Settings in turn and saves it.",
+                MessageType.Info);
+
+            using (new EditorGUILayout.HorizontalScope())
+            {
+                if (FrogletEditorPalette.ColorButton("Wire Open Scenes", FrogletEditorPalette.Ok, 160f))
+                    Run(openOnly: true);
```

</details>

### `091967302` — docs(economy): document the reward system and correct the stale payout claim

_Claude, 2026-09-01 18:49:56 +0000_

```text
Docs/REWARD_SYSTEM.md: the grant door and how to use it, the payout table's new
home, the two displays and why each behaves the way it does, the standing list
of designed paths that grant nothing, the one-balance rule, and an explicit
verification-status section.

Also corrects Docs/ECONOMY_AND_PRICING.md, which still described the payout as
"5 crystals, winner only" via a field that no longer exists. The code default
had been 200/50/0 for some time, so the prose had been wrong rather than
merely stale - which is exactly how five surfaces ended up paying out of the C#
initializer with nobody noticing.

Adds RewardTableTests: the payout policy (last place always earns nothing, a
two-domain runner-up is a loser, a solo field still pays, off-table places earn
nothing rather than throwing) plus an assertion that the SHIPPED asset matches
ECONOMY_TABLES.md Table 2 - so the doc and the asset can no longer drift apart
silently.

Verification: the eight assertions were executed headlessly against the shipped
code and the shipped asset, and the runner was proven to fail when the asset is
mutated. Both generators are idempotent under --check, the conditional-
compilation gate passes, and all 7,293 asset guids are owned exactly once.
```

```text
 Assets/_Scripts/Tests/Editor/RewardTableTests.cs      | 107 ++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/RewardTableTests.cs.meta |   2 +
 CLAUDE.md                                             |   1 +
 Docs/ECONOMY_AND_PRICING.md                           |  15 +++-
 Docs/REWARD_SYSTEM.md                                 | 214 ++++++++++++++++++++++++++++++++++++++++++++++++
 Tools/Build/author_reward_assets.py                   |   1 +
 6 files changed, 336 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 385 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Tests/Editor/RewardTableTests.cs b/Assets/_Scripts/Tests/Editor/RewardTableTests.cs
new file mode 100644
index 000000000..4e96f4699
--- /dev/null
+++ b/Assets/_Scripts/Tests/Editor/RewardTableTests.cs
@@ -0,0 +1,107 @@
+using CosmicShore.Data;
+using CosmicShore.ScriptableObjects;
+using NUnit.Framework;
+using UnityEngine;
+
+namespace CosmicShore.Tests
+{
+    /// <summary>
+    /// The payout policy, and the shipped numbers.
+    ///
+    /// These exist because the economy used to live as a serialized list on a UI component,
+    /// duplicated across nine scenes with five more surfaces on a retired field - a shape in
+    /// which "what does the game actually pay?" had no single answer and no way to assert one.
+    /// The values are Docs/ECONOMY_TABLES.md Table 2.
+    /// </summary>
+    public class RewardTableTests
+    {
+        RewardTableSO _table;
+
+        [SetUp]
+        public void SetUp() => _table = ScriptableObject.CreateInstance<RewardTableSO>();
+
+        [TearDown]
+        public void TearDown() => Object.DestroyImmediate(_table);
+
+        [Test]
+        public void FirstAndSecondPay_LastPlaceNever()
+        {
+            // Three domains: 1st and 2nd pay, last earns nothing.
+            Assert.AreEqual(200, _table.CrystalsForPlace(0, 3), "1st of 3");
+            Assert.AreEqual(50, _table.CrystalsForPlace(1, 3), "2nd of 3");
+            Assert.AreEqual(0, _table.CrystalsForPlace(2, 3), "last of 3");
+        }
+
+        [Test]
+        public void WithTwoDomains_TheRunnerUpIsALoser()
+        {
+            // The intended read: with two domains there is no silver medal. The table WOULD pay
+            // index 1 fifty crystals; the last-place rule overrides it.
+            Assert.AreEqual(200, _table.CrystalsForPlace(0, 2));
+            Assert.AreEqual(0, _table.CrystalsForPlace(1, 2),
+                "with two domains, second place IS last place and must earn nothing");
+        }
+
+        [Test]
+        public void SoloFieldStillPays()
+        {
+            // A one-domain field has no last place to demote - otherwise a solo run against AI
+            // teammates on the same domain would silently pay zero.
+            Assert.AreEqual(200, _table.CrystalsForPlace(0, 1));
+        }
+
+        [Test]
+        public void OffTheTable_EarnsNothingRatherThanThrowing()
+        {
+            Assert.AreEqual(0, _table.CrystalsForPlace(-1, 3), "a domain that never placed");
+            Assert.AreEqual(0, _table.CrystalsForPlace(7, 9), "a place past the end of the table");
+        }
+
+        [Test]
+        public void PlacementGrant_IsRepeatableAndCarriesItsSource()
+        {
+            var grant = _table.PlacementGrant(0, 3, "game_placement");
+            Assert.AreEqual(RewardKind.Crystals, grant.Kind);
+            Assert.AreEqual(200, grant.Amount);
+            Assert.AreEqual("game_placement", grant.Source);
+            Assert.AreEqual(RewardDedupe.None, grant.Dedupe,
+                "a placement is earned every game - deduping it would pay only the first win");
+            Assert.IsTrue(grant.IsPayable);
+        }
+
+        [Test]
+        public void AZeroPayout_IsNotPayableAndSoNeverReachesTheWallet()
+        {
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
