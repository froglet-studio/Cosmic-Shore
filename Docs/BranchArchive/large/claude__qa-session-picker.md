# Branch archive: `claude/qa-session-picker`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-09-01 by Claude
- **Unmerged commits:** 13
- **Forked from:** `10f96acb8` (2026-08-31, Merge pull request #821 from froglet-studio/claude/scarab-vessel-polish-k9mds6)
- **Tip:** `1ca8eaf40`
- **Files touched (44):**
  - `.claude/skills/ecology/SKILL.md`
  - `Assets/Resources/Toybox.asset`
  - `Assets/_SO_Assets/Host Connection Data/HostConnectionData.asset`
  - `Assets/_SO_Assets/Toys/Toy_Arkway.asset`
  - `Assets/_SO_Assets/Toys/Toy_Arkway.asset.meta`
  - `Assets/_Scripts/Controller/Environment/Ark.cs`
  - `Assets/_Scripts/Controller/Environment/Ark.cs.meta`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Managers/CameraManager.cs`
  - `Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Party/FriendsInitializer.cs`
  - `Assets/_Scripts/Controller/Party/HostConnectionService.cs`
  - `Assets/_Scripts/Controller/Party/Services/PartySessionService.cs`
  - `Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs`
  - `Assets/_Scripts/Controller/Player/Player.cs`
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
  - `Assets/_Scripts/Editor/Codex/ToolCodexHarvester.cs`
  - `Assets/_Scripts/Editor/QA/QASessionWindow.cs`
  - `Assets/_Scripts/Editor/ToyboxSetupTool.cs`
  - `Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs`
  - `Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs.meta`
  - `Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs`
  - `Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs`
  - `Assets/_Scripts/UI/Elements/FriendsListPanel.cs`
  - `Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs`
  - `Assets/_Scripts/UI/Modals/ModalWindowManager.cs`
  - `Assets/_Scripts/UI/ScreenSwitcher.cs`
  - `Assets/_Scripts/Utility/DataContainers/HostConnectionDataSO.cs`
  - `CLAUDE.md`
  - `Docs/ECOSYSTEM.md`
  - … and 4 more

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

### `4fcc597b1` — fix(party): a party is always x/4, and a guest follows the host onto the arcade screen

_Claude, 2026-09-01 17:26:24 +0000_

```text
Two reported defects, plus a test that was already red on bleeding-edge.

1. "Why is the lobby showing 1/6" - maxPartySlots is TRANSPORT CAPACITY
   (raised 4->6 in 83ada380 on purpose, one spare seat of anti-flicker
   headroom so a transient double-count cannot throw the fourth invite
   out as "party full"), and its own comment says "Capacity, NOT the
   UI's party size". But it was ALSO what the lobby rendered, what
   PARTY_MAX_KEY published to every peer, and what presence carried -
   so everyone read 1/6, and the LOBBY FULL badge waited for a fifth
   and sixth member the design never seats. Split the two questions:
   HostConnectionDataSO.PartyDisplaySlots (4, clamped to capacity) is
   now what the UI, the published property and presence use, and
   HasOpenDisplaySlots gates the invite affordance while HasOpenSlots
   stays the looser transport check. The remote row IGNORES a peer's
   published max and draws the local display size, so a peer on an
   older build still publishing 6 can never put "x/6" on screen.

2. "Once the host clicks a card the client should be transferred to
   that card in the app shell" - ScreenSwitcher.NavigateTo refuses ARK
   outright for a guest ("Arcade is host-only in multiplayer
   sessions"), so the card modal opened over whatever screen the guest
   was on and closing it left them somewhere unrelated. Being PULLED by
   the host is a different act from browsing, so it gets its own entry
   point (FollowHostToArcadeScreen) rather than a hole in the guard -
   nothing on a guest's own UI calls it - and the client's config-open
   handler moves the shell before opening the modal.

3. HostConnectionDataSOTests asserted MaxPartySlots == 4 against a
   shipped default of 6 (83ada380 changed the field, not the test), so
   the edit-mode suite was failing before this branch. Now asserts the
   capacity, the display size, and display <= capacity.

Verification: Roslyn parse-check clean on all nine touched files;
conditional-compilation gate OK (1857 files); every renamed/added
member checked against its declaring file. NOT verified in the Unity
editor - re-test: the lobby and every online row read x/4; a guest is
carried to the arcade screen when the host opens a card.
```

```text
 Assets/_SO_Assets/Host Connection Data/HostConnectionData.asset   |  1 +
 Assets/_Scripts/Controller/Party/FriendsInitializer.cs            |  6 ++++--
 Assets/_Scripts/Controller/Party/HostConnectionService.cs         |  4 +++-
 Assets/_Scripts/Controller/Party/Services/PartySessionService.cs  |  3 ++-
 Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs |  4 +++-
 Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs         | 24 ++++++++++++++++++++----
 Assets/_Scripts/UI/Elements/FriendsListPanel.cs                   | 15 ++++++++++++---
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs             |  8 ++++++++
 Assets/_Scripts/UI/Modals/ModalWindowManager.cs                   |  4 ++++
 Assets/_Scripts/UI/ScreenSwitcher.cs                              | 17 +++++++++++++++++
 Assets/_Scripts/Utility/DataContainers/HostConnectionDataSO.cs    | 24 ++++++++++++++++++++++++
 11 files changed, 98 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 237 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/FriendsInitializer.cs b/Assets/_Scripts/Controller/Party/FriendsInitializer.cs
index cc4740e9f..8c6e4bd1c 100644
--- a/Assets/_Scripts/Controller/Party/FriendsInitializer.cs
+++ b/Assets/_Scripts/Controller/Party/FriendsInitializer.cs
@@ -152,7 +152,8 @@ namespace CosmicShore.Gameplay
             var partySessionId = _partyQuery?.ActivePartySessionId ?? "";
             int memberCount = hostConnectionData != null && hostConnectionData.PartyMembers != null
                 ? hostConnectionData.PartyMembers.Count : 0;
-            int maxSlots = hostConnectionData != null ? hostConnectionData.MaxPartySlots : 0;
+            // The party size players SEE (4), never the transport capacity (6).
+            int maxSlots = hostConnectionData != null ? hostConnectionData.PartyDisplaySlots : 0;
 
             await friendsService.SetPresenceAsync(
                 Availability.Online,
@@ -172,7 +173,8 @@ namespace CosmicShore.Gameplay
             var partySessionId = _partyQuery?.ActivePartySessionId ?? "";
             int memberCount = hostConnectionData != null && hostConnectionData.PartyMembers != null
                 ? hostConnectionData.PartyMembers.Count : 0;
-            int maxSlots = hostConnectionData != null ? hostConnectionData.MaxPartySlots : 0;
+            // The party size players SEE (4), never the transport capacity (6).
+            int maxSlots = hostConnectionData != null ? hostConnectionData.PartyDisplaySlots : 0;
 
             await friendsService.SetPresenceAsync(
                 Availability.Busy,
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 23536be2d..93998a1ea 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -1922,8 +1922,10 @@ namespace CosmicShore.Gameplay
             {
                 lobby.CurrentPlayer.SetProperty(PARTY_COUNT_KEY,
                     new PlayerProperty(currentCount.ToString(), VisibilityPropertyOptions.Public));
+                // Displayed party size (4), not transport capacity (6) - publishing the
+                // capacity is what made every remote row read "1/6".
                 lobby.CurrentPlayer.SetProperty(PARTY_MAX_KEY,
-                    new PlayerProperty(connectionData.MaxPartySlots.ToString(), VisibilityPropertyOptions.Public));
+                    new PlayerProperty(connectionData.PartyDisplaySlots.ToString(), VisibilityPropertyOptions.Public));
                 lobby.CurrentPlayer.SetProperty(MATCH_NAME_KEY,
                     new PlayerProperty(currentMatch ?? string.Empty, VisibilityPropertyOptions.Public));
                 // Identity reconciliation: rides the same single save so a rename
diff --git a/Assets/_Scripts/Controller/Party/Services/PartySessionService.cs b/Assets/_Scripts/Controller/Party/Services/PartySessionService.cs
index e12a10721..5963ff38d 100644
--- a/Assets/_Scripts/Controller/Party/Services/PartySessionService.cs
+++ b/Assets/_Scripts/Controller/Party/Services/PartySessionService.cs
@@ -349,7 +349,8 @@ namespace CosmicShore.Gameplay
         private Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
         {
             int partyCount = _connectionData.PartyMembers != null ? _connectionData.PartyMembers.Count : 0;
-            int partyMax   = _connectionData.MaxPartySlots;
+            // Displayed party size, not transport capacity - see PresenceLobbyService.
+            int partyMax   = _connectionData.PartyDisplaySlots;
 
             return new Dictionary<string, PlayerProperty>
             {
diff --git a/Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs b/Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs
index c701e3b69..d79449512 100644
--- a/Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs
+++ b/Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs
@@ -340,7 +340,9 @@ namespace CosmicShore.Gameplay
         internal Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
         {
             int partyCount = _connectionData.PartyMembers != null ? _connectionData.PartyMembers.Count : 0;
-            int partyMax   = _connectionData.MaxPartySlots;
+            // The DISPLAYED party size, never the transport capacity: this value is what every
+            // other peer renders as "N/M" and what their LOBBY FULL badge compares against.
+            int partyMax   = _connectionData.PartyDisplaySlots;
 
             var props = new Dictionary<string, PlayerProperty>
             {
diff --git a/Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs b/Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs
index 1066ea9da..648e4c733 100644
--- a/Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs
+++ b/Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs
@@ -176,13 +176,29 @@ namespace CosmicShore.Tests
 
         #endregion
 
-        #region MaxPartySlots
+        #region Party slots
 
```

</details>

### `934218c2f` — fix(party): the joining client's vessel spawn could never retry, so every slow join bounced

_Claude, 2026-09-01 17:32:39 +0000_

```text
Reported: an invite is sent and accepted, the HOST can see the joining
player's object - and then that player is kicked back to their own menu
before they ever land in the party.

The join's success signal is GameDataSO.OnClientReady, raised when the
guest's own vessel pair initialises; PartyInviteController waits 30s for
it and bounces on timeout. So the guest is bounced whenever the HOST
never spawns their vessel.

That is exactly what happens on a slow link. Player.OnNetworkSpawn
raises the spawn event behind a ONE-SHOT latch (_spawnEventRaised).
ServerPlayerVesselInitializer receives it, waits ~2s for the
owner-written NetName / vessel type to replicate, and on timeout drops
the player from _processedPlayers and returns - logging "Will retry on
deferred event". But the deferred re-raise is guarded by that same
latch, which THIS event already spent: the branch is only ever reached
after the raise that started the handler. So nothing retried, no vessel
was ever spawned, OnClientReady never fired, and the watchdog bounced a
player the host could see perfectly well. On a LAN the values land
inside 2s and it never fires - it is latency-shaped, like the five
defects in 83ada380.

Fix: Player.ReArmDeferredSpawnEvent() (server-only) clears the latch and
immediately re-tests, since the values may have landed during the
spawner's own retry loop, leaving no future replication callback to ride.
The spawner calls it in the give-up branch, making its own comment true.

Re-arming re-enters the handler, so it is BOUNDED: MaxSpawnReArms (6)
per player, ~2.2s each, so ~13s of extra replication budget on top of
the first pass, then one loud error naming the cause. The counter clears
on spawn success and on despawn.

Verification: Roslyn parse-check clean on both files;
conditional-compilation gate OK. NOT verified in the Unity editor -
re-test: invite + accept across a real link; the guest should land in
the party with a vessel instead of bouncing, and a slow join should log
the re-arm rather than the bounce.
```

```text
 .../_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs  | 41 +++++++++++++++++++++++++++++++--
 Assets/_Scripts/Controller/Player/Player.cs                           | 29 +++++++++++++++++++++++
 2 files changed, 68 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 113 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
index b988b6d20..15423267f 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
@@ -124,6 +124,16 @@ namespace CosmicShore.Gameplay
         /// </summary>
         readonly HashSet<ulong> _preparedForScene = new();
 
+        /// <summary>
+        /// How many times the spawn event has been re-armed for a player whose owner-written
+        /// values had not replicated yet, keyed by NetworkObjectId. Bounds the re-raise loop.
+        /// </summary>
+        readonly Dictionary<ulong, int> _spawnReArms = new();
+
+        /// <summary>Re-arm budget per player. Each round costs the ~2.2s readiness wait below,
+        /// so this covers roughly 13 further seconds of replication delay before giving up.</summary>
+        const int MaxSpawnReArms = 6;
+
         protected virtual void Awake()
         {
             _netcodeHooks = GetComponent<NetcodeHooks>();
@@ -213,6 +223,7 @@ namespace CosmicShore.Gameplay
                 clientPlayerVesselInitializer.OnRosterRequested = null;
             _processedPlayers.Clear();
             _preparedForScene.Clear();
+            _spawnReArms.Clear();
             _cellSpawnRingBuilt = false; // a replay re-spawns the cell; rebuild against the new nucleus
 
             _cts?.Cancel();
@@ -329,15 +340,41 @@ namespace CosmicShore.Gameplay
 
                 if (!IsReadyToSpawn(player))
                 {
-                    // Still not ready after retries - remove from processed so the
-                    // deferred spawn event (Player.TryRaiseDeferredSpawnEvent) can retry.
+                    // Still not ready after retries - remove from processed so the deferred spawn
+                    // event can retry, and RE-ARM that event. Dropping the processed entry alone
+                    // was not enough and silently could not work: the spawn-event latch is
+                    // one-shot and this branch is only ever reached AFTER it was spent (the raise
+                    // is what started this handler), so the "will retry" below was a promise
+                    // nothing could keep. A joining client then never got a vessel, its
+                    // OnClientReady never fired, and its 30s join watchdog bounced it back to its
+                    // own menu while the host sat there seeing the player object just fine.
                     _processedPlayers.Remove(player.NetworkObjectId);
+
+                    // BOUNDED: re-arming re-raises the event, which re-enters this handler, so an
+                    // owner whose values never arrive at all would spin here forever. Each round
+                    // costs ~2.2s of real waiting, so a handful of them covers a long link
+                    // (~13s on top of the 2s first pass) and then stops. Cleared when the player
+                    // finally spawns or despawns, so a later scene starts fresh.
+                    _spawnReArms.TryGetValue(player.NetworkObjectId, out int reArms);
+                    if (reArms < MaxSpawnReArms)
+                    {
+                        _spawnReArms[player.NetworkObjectId] = reArms + 1;
+                        player.ReArmDeferredSpawnEvent();
+                    }
+                    else
+                    {
+                        Debug.LogError($"[FLOW-5] [ServerVesselInit] Player {ownerClientId} never became " +
+                                       $"spawn-ready after {MaxSpawnReArms} re-arms - giving up. That client " +
+                                       "will bounce: its owner-written NetName / vessel type never replicated.");
+                    }
                     Debug.LogWarning($"<color=#FFA500>[FLOW-5] [ServerVesselInit] Player {ownerClientId} NOT ready after {maxRetries * retryIntervalMs}ms - VesselType={player.NetDefaultVesselType.Value}, Name='{player.NetName.Value}'. Will retry on deferred event.</color>");
                     return;
                 }
             }
 
             CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-5] [ServerVesselInit] Player ready! Spawning vessel for {player.NetName.Value} (type={player.NetDefaultVesselType.Value})</color>");
+            // Readiness reached: this player owes no more re-arms.
+            _spawnReArms.Remove(player.NetworkObjectId);
             await OnPlayerReadyToSpawnAsync(player, ct);
         }
 
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index e8c345bbf..35e42fe14 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -808,6 +808,35 @@ namespace CosmicShore.Gameplay
         /// replicate, check if we can now raise the spawn event that was deferred
         /// in OnNetworkSpawn because the owner block was skipped.
```

</details>

### `81a6e4b3e` — fix(analytics): adopt sign-in/network state that was announced before the facade existed

_Claude, 2026-09-01 18:00:31 +0000_

```text
Garrett's crash log ends with, at app quit:

  [Analytics] DROPPING EVENTS - UGS sign-in has not completed.
  Nothing will reach UGS or PostHog until this is resolved.

That message reports the FIRST failed condition, so it proves the age
gate, eligibility, consent-decided and consent-granted all passed and it
stopped exactly at _signedIn - while the same session was demonstrably
signed in (it was sitting in a presence lobby as a guest). So the whole
play session produced no telemetry, to either sink.

Cause: AnalyticsServiceFacade is a LAZY DI singleton, constructed
whenever something first injects it - routinely AFTER sign-in and the
network probe have completed. It only ever subscribed to
AuthData.OnSignedIn / NetworkData.OnNetworkFound, with no
already-in-that-state fallback, so those one-shots had already fired,
nothing raises them again, and _signedIn / _isConnected stayed false for
the session. StartCollectionIfReady then never starts and every event is
dropped. This is the exact anti-pattern CLAUDE.md records against
AuthenticationSceneController ("a fast path that skips the
ANNOUNCEMENT"), one layer over.

Both flags mirror independently readable state (AuthenticationData
.IsSignedIn, NetworkMonitorData.IsOnline), so the constructor now
reconciles against it after subscribing. Idempotent: the handlers only
set a flag and re-run the guarded StartCollectionIfReady, so a real
event arriving later is a no-op.

Note PlayerDataService is NOT exposed to this - it polls
AuthenticationService.IsSignedIn directly.

Verification: Roslyn parse-check clean; conditional-compilation gate OK.
NOT verified in the Unity editor - re-test: play, quit, and confirm the
DROPPING EVENTS warning is gone and events reach UGS/PostHog.
```

```text
 Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs | 34 ++++++++++++++++++++++++++++++++++
 1 file changed, 34 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs b/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
index a18c92eb2..c15234dbe 100644
--- a/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
+++ b/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
@@ -171,6 +171,40 @@ namespace CosmicShore.Core
             GameSetting.OnChangeHapticsLevel            += v => HandleSettingChanged("haptics_level", v);
             FavoriteSystem.OnFavoriteChanged            += HandleFavoriteChanged;
             UGSCloudSaveProvider.OnSaveFailed           += HandleCloudSaveFailed;
+
+            // Adopt what the one-shot events have ALREADY announced. This facade is a LAZY DI
+            // singleton, so it is constructed whenever something first injects it - which is
+            // routinely AFTER sign-in and after the network probe have completed. Subscribing
+            // is then not enough: OnSignedIn / OnNetworkFound have already fired, nothing will
+            // raise them again this session, and _signedIn / _isConnected stay false forever -
+            // so StartCollectionIfReady never starts and EVERY event is dropped, to UGS and
+            // PostHog alike. Observed in the field: a session that was demonstrably signed in
+            // (it was in a presence lobby) still logged "DROPPING EVENTS - UGS sign-in has not
+            // completed" at quit, leaving that whole play session with no telemetry at all.
+            //
+            // Both flags mirror state that is independently readable, so the correct fix is to
+            // reconcile with it rather than to hope for the raise. Idempotent by construction:
+            // the handlers only set a flag and re-run the guarded StartCollectionIfReady, so a
+            // genuine event arriving afterwards is a no-op.
+            ReconcileWithAlreadyAnnouncedState();
+        }
+
+        /// <summary>
+        /// Catch up on <see cref="HandleSignedIn"/> / <see cref="HandleNetworkFound"/> when the
+        /// events that would have called them fired before this facade existed.
+        /// </summary>
+        void ReconcileWithAlreadyAnnouncedState()
+        {
+            var auth = AuthData;
+            if (auth != null && auth.IsSignedIn && !_signedIn)
+            {
+                Log("Sign-in had already completed before this facade was constructed - adopting it.");
+                HandleSignedIn();
+            }
+
+            var network = NetworkData;
+            if (network != null && network.IsOnline && !_isConnected)
+                HandleNetworkFound();
         }
 
         #region Consent & collection lifecycle
```

</details>

### `f1c5556c0` — fix(camera): windowed-camera teardown survives a destroyed controller

_Claude, 2026-09-01 18:07:07 +0000_

```text
From Garrett's host log, on scene close:

  [ModePreview] Unwinding the ScarabScramble preview hit: The object of
  type 'CosmicShore.Gameplay.CustomCameraController' has been destroyed
  but you are still trying to access it.

EndWindowedPlayerCamera guards with `_playerCamera?.` - but
_playerCamera is an ICameraController, an INTERFACE reference, and the
null-conditional does not route through Unity's overloaded ==. A
DESTROYED CustomCameraController is therefore still non-null there, the
call goes through, and it throws. This path runs exactly while things
are being destroyed (play-mode exit, scene close), so it has to be
destroy-safe.

The throw aborted the mode preview's unwind partway, leaving the rest of
that teardown unrun - which is the likely source of the "Some objects
were not cleaned up when closing the scene" error logged one second
later in the same session.

Fixed with the project's documented idiom - test the OBJECT, never the
interface ref (the same rule ModePreviewSession.Alive and
Cell.SetVesselTrailsDetached already follow).

Verification: Roslyn parse-check clean; conditional-compilation gate OK.
NOT verified in the Unity editor - re-test: enter a preview, exit play
mode while it is up, and confirm neither the destroyed-object error nor
the scene-cleanup error appears.
```

```text
 Assets/_Scripts/Controller/Managers/CameraManager.cs | 14 ++++++++++++--
 1 file changed, 12 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Managers/CameraManager.cs b/Assets/_Scripts/Controller/Managers/CameraManager.cs
index d9e4cc8c5..335d5d7e7 100644
--- a/Assets/_Scripts/Controller/Managers/CameraManager.cs
+++ b/Assets/_Scripts/Controller/Managers/CameraManager.cs
@@ -341,14 +341,24 @@ namespace CosmicShore.Gameplay
             }
 
             _playerFollowTarget = _windowedPreviousTarget;
-            _playerCamera?.SetFollowTarget(_windowedPreviousTarget);
+
+            // Test the OBJECT, never the interface reference. ICameraController is an interface,
+            // and `?.` on an interface ref does NOT route through Unity's overloaded == - so a
+            // DESTROYED CustomCameraController is still non-null here and every call below throws
+            // "has been destroyed but you are still trying to access it". That is what aborted the
+            // mode-preview unwind on scene close, leaving the rest of the teardown unrun
+            // (reported as "[ModePreview] Unwinding the ScarabScramble preview hit: ..."). This
+            // path runs precisely when things are being destroyed, so it must be destroy-safe.
+            var playerCamera = _playerCamera is UnityEngine.Object pcObj && pcObj ? _playerCamera : null;
+
+            playerCamera?.SetFollowTarget(_windowedPreviousTarget);
             _windowedPreviousTarget = null;
 
             // Only stand it down if it is not the view the game is actually using: in a gameplay
             // scene this same rig IS the screen camera, and a preview must never be able to
             // switch it off there.
             if (_activeController != _playerCamera)
-                _playerCamera?.Deactivate();
+                playerCamera?.Deactivate();
         }
 
         public ICameraController GetActiveController() => _activeController;
```

</details>

### `a4c420650` — fix(qa): every tester inherited the newest session file, whoever it belonged to

_Claude, 2026-09-01 19:02:05 +0000_

```text
Found in the first walkthrough with a new user. Both reported symptoms were the
same bug.

The window opened whichever results file sorted LAST. Exactly one exists in the
repo -- Caleb's 2026-08-14 session -- so on bleeding-edge every tester, on every
machine, was dropped into it:

  * it is pinned to claude/qa-backlog-7mvlsr, so step 2 instructed the tester to
    check out a branch that no longer exists;
  * it is fully submitted and applied, so its only row is FROZEN and renders as
    read-only text -- which is why there was NO WAY TO RECORD A VERDICT anywhere
    on screen;
  * hasSession was true, so the create-a-session screen never appeared and there
    was no way out of it.

The backlog itself is fine and no items were touched. Verified: zero mentions of
any branch name in QA_BACKLOG.md and no item that asks a tester to check anything
out. The "go and look at that branch" instruction came from the stale SESSION,
not from the items, so dropping the top P0s would have lost real coverage
(Windows player, menu camera rig, scoring mirror, prism occlusion, speed tunnel)
for a misdiagnosis.

The window now owns which session it is on:

  * a session picker in step 1, always visible, listing every session (the
    sessions[] payload field has existed since the first cut and was never drawn);
  * "New session..." available at any time, not only when zero sessions exist;
  * the active file is remembered in EditorPrefs, and a remembered file that no
    longer exists falls back to THIS tester's own newest session, never a
    stranger's;
  * a warning when the open session belongs to someone else, because its verdicts
    are frozen and its build is theirs.

Two more defects found while confirming the first, both latent until now because
only one session file had ever existed:

  * create() silently OVERWROTE a same-day file. A second session the same day is
    ordinary -- a different build, or the retest the frozen-verdict rule requires
    be a new file -- and the overwrite would have destroyed verdicts already
    published and frozen in the ledger. It now suffixes -2, -3, ...
  * every set/remove/attach targeted the NEWEST file rather than the one on
    screen, so with two sessions open a verdict could land in the wrong one. The
    window now passes --file on every command.

session selftest 30 -> 33 checks (pins the no-clobber rule); C# real-compiled
(Roslyn + stubs, exit 0); conditional-compilation gate clean.
```

```text
 Assets/_Scripts/Editor/QA/QASessionWindow.cs | 148 +++++++++++++++++++++++++++++++++++++++++++++++++++++----
 Tools/QA/session.py                          |  21 +++++++-
 2 files changed, 160 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 250 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/QA/QASessionWindow.cs b/Assets/_Scripts/Editor/QA/QASessionWindow.cs
index 408a0df0f..5665484dc 100644
--- a/Assets/_Scripts/Editor/QA/QASessionWindow.cs
+++ b/Assets/_Scripts/Editor/QA/QASessionWindow.cs
@@ -107,6 +107,8 @@ namespace CosmicShore.Editor.QA
         string _pythonVersion;
         string _lastError;
         string _newTester = "";
+        string _activeSession = "";
+        bool _startingNew;
         Vector2 _scroll;
         int _addIndex;
         bool _busy;
@@ -321,6 +323,15 @@ var sb = new StringBuilder();
                 if (!string.IsNullOrEmpty(_pythonPrefix)) sb.Append(_pythonPrefix).Append(' ');
                 sb.Append("Tools/QA/session.py");
                 foreach (var a in args) sb.Append(' ').Append(Quote(a));
+
+                // ALWAYS name the file. Without --file the CLI falls back to the NEWEST
+                // results file, so a tester viewing one session could have their verdict
+                // written into somebody else's — and on a fresh clone the window adopted
+                // whichever session happened to sort last (which is how a finished
+                // session pinned to a deleted branch became everyone's default view).
+                if (!string.IsNullOrEmpty(_activeSession) && args.Length > 0 && args[0] != "new")
+                    sb.Append(" --file ").Append(Quote(_activeSession));
+
                 sb.Append(" --json");
 
                 if (!TryRun(_python, sb.ToString(), out var stdout, out var stderr, out _))
@@ -616,6 +627,9 @@ var sb = new StringBuilder();
 
         static string Quote(string s) => "\"" + (s ?? "").Replace("\"", "\\\"") + "\"";
 
+        const string TesterPrefKey = "CosmicShore.QA.Tester";
+        const string SessionPrefKey = "CosmicShore.QA.ActiveSession";
+
         void Refresh()
         {
             if (!FindPython(out _python, out _pythonPrefix, out _pythonVersion))
@@ -623,6 +637,56 @@ var sb = new StringBuilder();
                 _python = null;
                 return;
             }
+
+            _newTester = EditorPrefs.GetString(TesterPrefKey, _newTester ?? "");
+            _activeSession = EditorPrefs.GetString(SessionPrefKey, "");
+            Run("state");
+
+            // A remembered session that no longer exists (a fresh clone, someone else's
+            // machine) must not pin the window to nothing. Prefer this tester's own file;
+            // otherwise show the create screen rather than adopting a stranger's session.
+            if (_state != null && !SessionExists(_activeSession))
+            {
+                _activeSession = MineOrNone();
+                EditorPrefs.SetString(SessionPrefKey, _activeSession ?? "");
+                Run("state");
+            }
+        }
+
+        bool SessionExists(string file)
+        {
+            if (string.IsNullOrEmpty(file) || _state?.sessions == null) return false;
+            foreach (var s in _state.sessions) if (s == file) return true;
+            return false;
+        }
+
+        /// <summary>Newest session belonging to this tester, or null — never someone else's.</summary>
+        string MineOrNone()
+        {
+            if (_state?.sessions == null || string.IsNullOrWhiteSpace(_newTester)) return null;
+            var slug = Slug(_newTester);
+            string best = null;
+            foreach (var s in _state.sessions)
+                if (s.IndexOf("-" + slug, System.StringComparison.OrdinalIgnoreCase) >= 0)
+                    best = s;   // sessions[] is sorted, so the last match is the newest
+            return best;
+        }
+
+        static string Slug(string name)
+        {
```

</details>

### `40e85aceb` — fix(qa): step 2 read as a destination; it is a record

_Claude, 2026-09-01 19:37:54 +0000_

```text
Reported from the walkthrough: "the branch listed under 'Your session says you
are testing' is the branch I need to switch to in order to test this item."

It is not, and the wording invited that reading. The Branch row is stamped from
`git rev-parse --abbrev-ref HEAD` when the session is CREATED -- it records where
the tester was, and it is what a dev task means by "failed on X at Y". The tests
themselves are not branch-specific: they cover work that has already merged, and
they are run from whatever integration branch the tester works on. Reading the
row as a destination sends someone to check out a feature branch that has usually
been deleted.

  "Your session says you are testing:"  ->  "This session was started on:"
  "Unity actually has open:"            ->  "Unity has open right now:"

The branch-differs advice is re-ordered to match what is usually true. A
different branch normally means the tester moved on, so the lead is now the
existing "record the build I am on" button. Going back is the minority case,
kept for when someone really was asked to test one branch -- and that is where
the requested "Copy branch name" button lives, since that is the only place
pasting it into GitHub Desktop is the right action.

When the recorded branch no longer exists (the normal fate of a merged one) the
window now says so and points at "New session..." instead of giving instructions
that cannot be followed. state() gains sessionBranchExists, checked against both
refs/heads and refs/remotes/origin.

No backlog items were touched. QA-BUILD-COMPILE has not been on the list since it
was archived: the walkthrough was seeing it as a FROZEN ROW inside the previous
tester's session, which is the stale-session bug fixed in the previous commit.
QA_BACKLOG.md has no section for it and its one remaining mention is prose inside
QA-BUILD-WINDOWS-PLAYER explaining why the two are separate.

C# real-compiled (Roslyn + stubs, exit 0); 68 python selftest checks green;
conditional-compilation gate clean.
```

```text
 Assets/_Scripts/Editor/QA/QASessionWindow.cs | 60 ++++++++++++++++++++++++++++++++++++++++------------------
 Tools/QA/session.py                          | 19 ++++++++++++++++++-
 2 files changed, 60 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 133 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/QA/QASessionWindow.cs b/Assets/_Scripts/Editor/QA/QASessionWindow.cs
index 5665484dc..5781cf2c0 100644
--- a/Assets/_Scripts/Editor/QA/QASessionWindow.cs
+++ b/Assets/_Scripts/Editor/QA/QASessionWindow.cs
@@ -79,7 +79,8 @@ namespace CosmicShore.Editor.QA
             public string root;
             public string head;
             public string branch;         // what git has checked out right now
-            public string sessionBranch;  // what the session form says is under test
+            public string sessionBranch;  // the branch this session was STARTED on (a record)
+            public bool sessionBranchExists;   // false once that branch is deleted
             public string preconditions;
             public bool hasSession;
             public string sessionFile;
@@ -966,12 +967,19 @@ var sb = new StringBuilder();
                 var sameBranch = !string.IsNullOrEmpty(_state.sessionBranch) &&
                                  _state.sessionBranch == _state.branch;
 
-                EditorGUILayout.LabelField("Your session says you are testing:",
+                // "was STARTED on", not "you are testing". This row is a RECORD of where
+                // the tester was when they opened the session — it is what a dev task
+                // means by "failed on X at Y". It is NOT an instruction to go to that
+                // branch: the tests are not branch-specific, they are of work that has
+                // already merged, and you run them on whatever integration branch you
+                // are working from. Reading it as a destination sent a walkthrough
+                // hunting for a feature branch that had already been deleted.
+                EditorGUILayout.LabelField("This session was started on:",
                     EditorStyles.miniBoldLabel);
                 EditorGUILayout.LabelField("      " + _state.sessionBranch +
                     "      version " + _state.commit, EditorStyles.boldLabel);
                 EditorGUILayout.Space(2);
-                EditorGUILayout.LabelField("Unity actually has open:",
+                EditorGUILayout.LabelField("Unity has open right now:",
                     EditorStyles.miniBoldLabel);
                 EditorGUILayout.LabelField("      " + _state.branch +
                     "      version " + _state.head, EditorStyles.boldLabel);
@@ -988,21 +996,37 @@ var sb = new StringBuilder();
 
                 if (!sameBranch)
                 {
-                    EditorGUILayout.LabelField(
-                        "You are on the wrong branch, so this is not the code your " +
-                        "tests are about. Switch before you run anything:",
-                        Body);
-                    EditorGUILayout.Space(4);
-                    EditorGUILayout.LabelField("In GitHub Desktop:",
-                        EditorStyles.miniBoldLabel);
-                    EditorGUILayout.LabelField(
-                        "1.  Click  Fetch origin  (top bar).\n" +
-                        "2.  Click  Current Branch  (top bar) and choose  " +
-                        _state.sessionBranch + "\n" +
-                        "3.  Click  Pull origin  (top bar). If it is not offered, you " +
-                        "are already up to date.\n" +
-                        "4.  Come back to Unity, wait for it to finish importing, then " +
-                        "press Refresh at the top of this window.", Body);
+                    // The tests are NOT branch-specific — they cover work that has already
+                    // merged, and you run them from whatever integration branch you work
+                    // on. So a different branch usually just means "you moved on", and
+                    // the answer is to record the build you are really on, not to go
+                    // back. Going back is impossible anyway once that branch is deleted,
+                    // which is the normal fate of a merged one.
+                    if (!_state.sessionBranchExists)
+                    {
+                        EditorGUILayout.LabelField(
+                            "The branch this session was started on no longer exists — it " +
+                            "was merged and deleted. Nothing is wrong: this session is " +
+                            "finished history. Use \"New session…\" in step 1 to start " +
+                            "one on the build you have open now.", Body);
+                    }
+                    else
+                    {
+                        EditorGUILayout.LabelField(
+                            "You are on a different branch than this session was started " +
+                            "on. The tests are not tied to a branch, so if you are simply " +
+                            "working from a newer one, use the button below. Go back only " +
+                            "if you were asked to test that specific branch:", Body);
+                        EditorGUILayout.Space(4);
+                        EditorGUILayout.LabelField(
+                            "To go back: in GitHub Desktop click Current Branch, then " +
+                            "Fetch origin and Pull origin once you are on it.",
```

</details>

### `1ca8eaf40` — fix(qa): the test preview looked like an opened test, so nobody pressed Add

_Claude, 2026-09-01 23:38:37 +0000_

```text
Second walkthrough, with the head engineer. Everything up to step 4 worked --
he created his own session, step 2 matched on bleeding-edge, 64 tests listed --
and then he could not record a verdict.

He had SELECTED a test in the dropdown and read the whole preview, but never
pressed Add, so no row existed. A verdict control only exists on a ROW, so there
was nothing to record with, and the only feedback was a red "results table: has
no item rows" that described the file rather than telling him what to do.

That is the UI's fault. The preview renders the item completely -- numbered
steps, PASS WHEN, FAIL WHEN, known exceptions -- so it reads as an opened test,
and the only Add button sat at the TOP of it, several hundred pixels above where
the reading ends.

Three changes, all pointing at the same gap:

  * the same Add action repeated at the END of the preview, where the reading
    actually finishes: "Add this test to my session", followed by "...then a
    verdict box appears in step 4 above";
  * the preview is now labelled as one -- "This is a PREVIEW -- the test is not
    in your list yet" -- instead of implying it is open;
  * the empty step 4 says what to do rather than what is absent: choose a test,
    read it, press ADD, and the PASS / FAIL / BLOCKED box appears here.

submit.py's blocking message is reworded for the window, which is where it is
read: "no tests have been added to this session yet" with the fix "pick a test
from the list and press Add".

Everything else in that walkthrough worked, including the whole session-picker
fix from the previous two commits: New session, own file, matching branch and
commit, green step 2.

C# real-compiled (Roslyn + stubs, exit 0); 68 python selftest checks green;
conditional-compilation gate clean.
```

```text
 Assets/_Scripts/Editor/QA/QASessionWindow.cs | 31 ++++++++++++++++++++++++++++---
 Tools/QA/submit.py                           |  8 ++++++--
 2 files changed, 34 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/QA/QASessionWindow.cs b/Assets/_Scripts/Editor/QA/QASessionWindow.cs
index 5781cf2c0..d2ee42c4a 100644
--- a/Assets/_Scripts/Editor/QA/QASessionWindow.cs
+++ b/Assets/_Scripts/Editor/QA/QASessionWindow.cs
@@ -924,8 +924,10 @@ var sb = new StringBuilder();
                 "You do not have to run every test, or run them in one sitting. If you " +
                 "cannot judge one, that is a real answer — mark it BLOCKED and say why.");
             if (_state.rows.Length == 0)
-                EditorGUILayout.HelpBox("No tests picked yet — choose one from the " +
-                    "list at the bottom to see what it involves.", MessageType.Info);
+                EditorGUILayout.HelpBox("No tests in your session yet. Choose one from " +
+                    "the list at the bottom, read what it involves, then press ADD — the " +
+                    "box for recording PASS / FAIL / BLOCKED appears here once you do.",
+                    MessageType.Info);
 
             foreach (var row in _state.rows) DrawRow(row);
 
@@ -1315,9 +1317,32 @@ var sb = new StringBuilder();
                         "opinion), but it is not work that is waiting for you.",
                         MessageType.Warning);
                 EditorGUILayout.LabelField(
-                    "Here is what that one involves. Press Add to put it in your list:",
+                    "This is a PREVIEW — the test is not in your list yet.",
                     EditorStyles.wordWrappedMiniLabel);
                 DrawInstructions(chosen);
+
+                // The SAME action, repeated at the END of the preview. The panel above
+                // is long enough to scroll past the dropdown's little Add button, and
+                // it reads so completely (steps, PASS, FAIL) that it looks like the
+                // test is already open — a first walkthrough read all of it, found no
+                // verdict control, and could not finish, because a verdict only exists
+                // on a ROW and nothing had been added. Put the call to action where the
+                // reading actually ends.
+                EditorGUILayout.Space(4);
+                using (new EditorGUILayout.HorizontalScope())
+                {
+                    if (FrogletEditorPalette.ColorButton(
+                            "Add this test to my session", FrogletEditorPalette.Ok, 220f, 26f,
+                            "Adds " + ids[_addIndex] + " as a row you can record a verdict on"))
+                    {
+                        var picked = ids[_addIndex];
+                        _addIndex = 0;
+                        Defer(() => Run("set", "--item", picked, "--verdict", ""));
+                    }
+                    EditorGUILayout.LabelField(
+                        "…then a verdict box appears in step 4 above.",
+                        EditorStyles.wordWrappedMiniLabel);
+                }
             }
         }
 
diff --git a/Tools/QA/submit.py b/Tools/QA/submit.py
index b04797016..780ac76b1 100644
--- a/Tools/QA/submit.py
+++ b/Tools/QA/submit.py
@@ -112,8 +112,12 @@ def validate(text, fname, known, archived, applied_items, head=None,
             "the table; without them the whole file is parsed and scratch notes "
             "become verdicts"))
     if not rows:
-        problems.append(Problem(True, "results table", "has no item rows",
-                                "add one row per item you ran"))
+        # Worded for the window, which is where this is read: "has no item rows"
+        # described the FILE and left a first-time tester with nothing to act on.
+        problems.append(Problem(
+            True, "results table", "no tests have been added to this session yet",
+            "pick a test from the list and press Add — a verdict box appears once "
+            "it is in your list"))
 
     seen = set()
     for item_id, verdict, notes in rows:
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
