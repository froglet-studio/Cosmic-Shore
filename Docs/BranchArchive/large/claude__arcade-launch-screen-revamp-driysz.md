# Branch archive: `claude/arcade-launch-screen-revamp-driysz`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-09-02 by Claude
- **Unmerged commits:** 19
- **Forked from:** `10f96acb8` (2026-08-31, Merge pull request #821 from froglet-studio/claude/scarab-vessel-polish-k9mds6)
- **Tip:** `3b74b892a`
- **Files touched (55):**
  - `.claude/skills/ecology/SKILL.md`
  - `Assets/Resources/Toybox.asset`
  - `Assets/_SO_Assets/Host Connection Data/HostConnectionData.asset`
  - `Assets/_SO_Assets/Toys/Toy_Arkway.asset`
  - `Assets/_SO_Assets/Toys/Toy_Arkway.asset.meta`
  - `Assets/_Scripts/Controller/Environment/Ark.cs`
  - `Assets/_Scripts/Controller/Environment/Ark.cs.meta`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs`
  - `Assets/_Scripts/Controller/Managers/CameraManager.cs`
  - `Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs`
  - `Assets/_Scripts/Controller/Multiplayer/NetworkSceneObjectGuard.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Controller/Party/FriendsInitializer.cs`
  - `Assets/_Scripts/Controller/Party/HostConnectionService.cs`
  - `Assets/_Scripts/Controller/Party/Interfaces/IInviteService.cs`
  - `Assets/_Scripts/Controller/Party/Services/AcceptanceSignalService.cs`
  - `Assets/_Scripts/Controller/Party/Services/InviteService.cs`
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
  - `Assets/_Scripts/Editor/StrayNetworkObjectValidator.cs`
  - `Assets/_Scripts/Editor/ToyboxSetupTool.cs`
  - `Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs`
  - `Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs.meta`
  - `Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs`
  - … and 15 more

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

### `414eb22f9` — fix(party): recycle an idle Relay allocation before a guest can join it

_Claude, 2026-09-01 18:22:41 +0000_

```text
Root cause of "invite accepted, guest kicked out after ~30s" and of the
cross-globe join failures: the eager per-user party session allocates a
Relay slot the moment a player enters Menu_Main, and Relay reclaims an
allocation that sits with no peers for a few minutes ("player timed out
due to inactivity" / "Relay allocation is invalid" in the host log). The
UGS session keeps advertising the dead allocation, so a guest connects to
it, NGO scene sync never completes ([Deferred OnSpawn] on every scene
object), IsConnectedClient never turns true, and the accept flow bounces
the guest at step 3. Nothing in the project observed AllocationInvalid
and nothing kept the allocation alive, which is why "restart the game"
(a fresh allocation) was the only thing that ever fixed it.

HostConnectionService now recycles its own party session from the
refresh tick when it has sat IDLE_SESSION_RECYCLE_SECONDS (240s) with no
remote members, no outgoing invites, no connected NGO clients and no
transition in flight - leave + create + republish, so the advertised
join code is always one the Relay still honours. Recycling is skipped
the moment anyone is in or on their way in.

Verification: parse-checked + conditional-compilation gate only. NOT
editor-verified - needs a host idling >4 min, then a guest accepting.
```

```text
 Assets/_Scripts/Controller/Party/HostConnectionService.cs | 89 +++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 89 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 114 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 93998a1ea..82b948e09 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -121,6 +121,28 @@ namespace CosmicShore.Gameplay
         /// </summary>
         private const float SESSION_CREATION_GRACE_PERIOD_SECONDS = 4f;
 
+        /// <summary>
+        /// How long an IDLE party session (hosting, nobody connected, no invite outstanding) may
+        /// sit before it is recycled.
+        ///
+        /// <para><b>Why this exists.</b> Under the locked eager-Relay design every player creates
+        /// a Relay-backed party session on entering Menu_Main, and it then sits with ZERO peers
+        /// until somebody accepts an invite. Unity Relay reclaims an allocation that carries no
+        /// traffic, and when it does the UGS SESSION stays perfectly valid - so every
+        /// session-level self-heal in this class keeps passing while the transport underneath is
+        /// dead. Observed in the field (host log):
+        ///   "Received error message from Relay: player timed out due to inactivity."
+        ///   "Relay allocation is invalid. See ... RelayConnectionStatus.AllocationInvalid"
+        /// After that the advertised session id points at nothing: a guest connects, NGO
+        /// synchronisation never completes, their ClientRpcs are deferred and dropped, and they
+        /// bounce - which is why the only known workaround was restarting the game (a restart
+        /// mints a fresh allocation).
+        ///
+        /// <para>Comfortably under Relay's reclaim window, so the allocation is replaced before
+        /// it can go stale rather than after.</para>
+        /// </summary>
+        private const float IDLE_SESSION_RECYCLE_SECONDS = 240f;
+
         /// <summary>
         /// <see cref="ReconcilePartyMembersNow"/> retries the refresh+sync this many
         /// times to absorb UGS leave-propagation lag, stopping early once the roster
@@ -1206,6 +1228,11 @@ namespace CosmicShore.Gameplay
                         TryRaiseIncomingInvite(invite);
                 }
 
+                // Keep the idle Relay allocation from going stale under us (see
+                // IDLE_SESSION_RECYCLE_SECONDS). Before the acceptance scan, so a recycle can
+                // never land between a guest reading the session id and joining it.
+                await RecycleIdlePartySessionIfStaleAsync();
+
                 // Acceptance-signal scan. Must run BEFORE the JOINED_PARTY_KEY scan
                 // because recipients won't set joined_party until after they read the
                 // real session id. Gated on outgoing-invite count - no work to do if
@@ -1747,6 +1774,68 @@ namespace CosmicShore.Gameplay
         /// Reentrant: callers from inside <see cref="RefreshAsync"/> (mutex already
         /// held) skip re-acquiring; external callers acquire normally.
         /// </summary>
+        /// <summary>
+        /// Replace an IDLE party session before Unity Relay reclaims its allocation.
+        ///
+        /// <para>Runs only when this player is hosting, NOBODY is connected, and no invite is
+        /// outstanding - so it can never disturb a live party, and can never race a guest who is
+        /// mid-join off an advertised session id. Those two conditions are what make recycling
+        /// safe rather than disruptive; without them this would be a reconnect storm.</para>
+        ///
+        /// <para>Recreating changes the session id, so the new one is republished to the presence
+        /// lobby immediately (the same republish the acceptance handshake uses). With no invites
+        /// outstanding there is nothing else holding the old id.</para>
+        /// </summary>
+        private async UniTask RecycleIdlePartySessionIfStaleAsync()
+        {
+            var session = _partySessionService.ActiveSession;
+            if (session == null) return;
+
+            // Hosting only: a GUEST's "session" is the host's, and leaving it here would eject
+            // this player from the party they are in.
+            if (!connectionData.IsPartyHost) return;
+
+            // Idle only. A connected peer keeps the allocation alive by definition, and an
+            // outstanding invite means a guest may be reading this id right now.
+            if (connectionData.RemotePartyMemberCount > 0) return;
+            if (_inviteService.OutgoingCount > 0) return;
+
+            // Netcode must also be quiet - ConnectedClients covers a peer whose UGS membership
+            // has not been reconciled into PartyMembers yet.
+            var nm = NetworkManager.Singleton;
+            if (nm != null && nm.IsServer && nm.ConnectedClientsIds.Count > 1) return;
+
```

</details>

### `13326a581` — fix(party): let a host re-invite a guest, see every accepter, and outlive the join watchdog

_Claude, 2026-09-01 18:39:30 +0000_

```text
Three defects on the invite/accept path, all of which read as "invites keep
failing until we restart the game" and "the 3rd player can never get in":

- HostConnectionService.TryRaiseIncomingInvite keyed its dedup on the SENDER
  and kept that record forever. Accepting (flagged before the join is even
  attempted) or declining one invite from a host made every later invite
  from that host read as a "PENDING -> real id transition" of the old one -
  same host, same session id - and swallowed it silently. A guest whose
  join bounced could therefore never be re-invited by that host until the
  host restarted (new session id). ForgetWithdrawnInvite now drops the
  record two ticks after the host's invite line for us is gone (cleared on
  join, cancelled, or expired), so the next line from that host is a new
  invite; an unresolved invite whose line vanished was withdrawn, so the
  popup is dismissed too.

- AcceptanceSignalService.ScanForSignals returned the FIRST accepter only,
  and an accepter stays in the invited set until the join is corroborated
  (or 60s) - so with two invites out the first accepter masked the second's
  signal for up to a minute. It returns every accepter; the host handles
  them in one pass.

- RepublishWithRealIdAsync wrote the unchanged invite composite back to the
  lobby (refresh + save) on EVERY refresh tick while any accepter sat in
  the invited set, because under the eager-Relay design nothing is ever
  PENDING. UpdatePayloadsWithRealSessionId now reports what it patched and
  a 0 skips the write - that traffic is what tripped the lobby rate limit
  exactly while a guest was joining.

- ClientPlayerVesselInitializer.RosterPullRetryLoop started in
  OnNetworkSpawn (during Netcode sync) but was sized against a watchdog
  that starts after IsConnectedClient, so on a slow link the recovery loop
  died while the bounce timer was still counting. 40 x 1.5s outlives both
  watchdogs whatever the sync took; it is cancelled the moment the local
  pair resolves.

Verification: Roslyn parse-check + conditional-compilation gate only. NOT
editor-verified - needs a 3-player MPPM/live retest (invite, bounce,
re-invite; two accepters in flight).
```

```text
 .../_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs  | 14 ++++--
 Assets/_Scripts/Controller/Party/HostConnectionService.cs             | 88 ++++++++++++++++++++++++++++++---
 Assets/_Scripts/Controller/Party/Interfaces/IInviteService.cs         |  8 ++-
 Assets/_Scripts/Controller/Party/Services/AcceptanceSignalService.cs  | 34 ++++++++++---
 Assets/_Scripts/Controller/Party/Services/InviteService.cs            |  3 +-
 5 files changed, 127 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 282 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs
index 5b7047fdc..6321378cf 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs
@@ -279,9 +279,17 @@ namespace CosmicShore.Gameplay
             // or the loop that is supposed to recover the join gives up while the watchdog that
             // BOUNCES the player is still counting. At the shipped 4 x 1500ms the retry died at
             // 6s against a 10s watchdog - four seconds in which nothing was retrying and the only
-            // possible outcome was a bounce to the solo menu. 24s against a 30s watchdog keeps
-            // the recovery alive for the whole window, which is what a high-RTT joiner needs.
-            const int maxAttempts = 16;
+            // possible outcome was a bounce to the solo menu.
+            //
+            // The two clocks do not even START together: this loop starts in OnNetworkSpawn, which
+            // on a joining client runs DURING Netcode synchronization, while the watchdog starts
+            // only after IsConnectedClient - i.e. after synchronization completes, plus the
+            // connect wait. So "24s against a 30s watchdog" was still short by the whole sync
+            // time on the one link that needs it. 60s outlives every watchdog in the project
+            // (connect 30s + ready 30s) whatever the sync took; the loop is cancelled the moment
+            // the local pair resolves, and on despawn, so the cap only ever bounds a join that
+            // was already lost.
+            const int maxAttempts = 40;
             const int intervalMs = 1500;
 
             for (int attempt = 0; attempt < maxAttempts && !_localPairResolved; attempt++)
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 82b948e09..ed0d3a249 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -241,6 +241,13 @@ namespace CosmicShore.Gameplay
         // ─────────────────────────────────────────────────────────────────────
 
         private PartyInviteData? _lastFiredInvite;
+
+        /// <summary>
+        /// Consecutive refresh ticks on which the sender of <see cref="_lastFiredInvite"/> had no
+        /// invite line for us. Two in a row are required before the record is dropped, so a single
+        /// stale lobby snapshot (a presence-lobby converge mid-tick) cannot flicker a live invite.
+        /// </summary>
+        private int _inviteMissTicks;
         /// <summary>
         /// True after the local user has accept/decline/left for <see cref="_lastFiredInvite"/>.
         /// Kept alongside the cached invite so the SDK-side dedup guard still
@@ -1220,13 +1227,18 @@ namespace CosmicShore.Gameplay
                 if (connectionData.OnlinePlayers != null)
                     RefreshOnlinePlayersDiff();
 
-                // Scan composite invite_payloads for lines targeting us.
+                // Scan composite invite_payloads for lines targeting us - and notice when the
+                // line behind the invite we last surfaced is GONE (see ForgetWithdrawnInvite).
+                bool lastHostStillInviting = false;
                 foreach (var p in _lobbyService.ActiveLobby.Players)
                 {
                     if (p.Id == connectionData.LocalPlayerId) continue;
-                    if (TryFindIncomingInvite(p, out var invite))
-                        TryRaiseIncomingInvite(invite);
+                    if (!TryFindIncomingInvite(p, out var invite)) continue;
+                    if (_lastFiredInvite.HasValue && _lastFiredInvite.Value.HostPlayerId == invite.HostPlayerId)
+                        lastHostStillInviting = true;
+                    TryRaiseIncomingInvite(invite);
                 }
+                ForgetWithdrawnInvite(lastHostStillInviting);
 
                 // Keep the idle Relay allocation from going stale under us (see
                 // IDLE_SESSION_RECYCLE_SECONDS). Before the acceptance scan, so a recycle can
@@ -1239,25 +1251,28 @@ namespace CosmicShore.Gameplay
                 // we haven't sent any invites.
                 if (_inviteService.OutgoingCount > 0)
                 {
-                    string acceptingId = _acceptanceService.ScanForSignals(
+                    var accepters = _acceptanceService.ScanForSignals(
                         _lobbyService.ActiveLobby,
                         connectionData.LocalPlayerId,
                         _inviteService.OutgoingTargets);
 
-                    if (acceptingId != null)
+                    if (accepters.Count > 0)
                     {
                         // Every player hosts their own Relay session from menu entry
                         // (eager creation), so the session already exists before the
```

</details>

### `42a454a7d` — fix(arcade): replicate the open lobby as state so a client is always pulled into the card

_Claude, 2026-09-01 18:39:30 +0000_

```text
Open / close / intensity / roster travelled as one-shot ClientRpcs, and a
ClientRpc reaches exactly the clients that are synchronized when it is
sent: a guest still inside Netcode scene synchronization when the host
opened a card had the RPC deferred and dropped ("[Deferred OnSpawn]" in the
joiner's log), and a guest who joined AFTER the host opened a card was never
told at all - so the client sat on the lava lamp while the host looked at a
lobby, and re-opening the card was the only way to reach them.

ArcadeConfigSyncManager now holds the whole lobby in one server-written
NetworkVariable<LobbySnapshot> (card, intensity, seats, domain count, the
placed AI domains, and a generation that climbs on every open so a
close-and-reopen is a new open even on a peer that never saw the close). A
late joiner receives the value with the spawn and applies it in
OnNetworkSpawn; every other change is diffed against the previous value
and raised through the SAME C# events the modal already listens to, so the
modal's handlers are unchanged. The modal additionally asks for a replay
when it subscribes, for the re-enabled-mid-lobby case.

The ready-up head-count is read live off the connected clients rather than
frozen at commit, so a member who joins mid-lobby is waited for and one who
leaves stops being waited for; only the count is re-announced on a
departure - a launch is caused by a press, never by someone leaving.

Verification: Roslyn parse-check + conditional-compilation gate only. NOT
editor-verified - retest: host opens a card, guest joins afterwards and is
pulled in; host closes and reopens; host changes intensity / places AI with
a guest in the lobby.
```

```text
 Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs | 279 ++++++++++++++++++++++++++++++------
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs             |   7 +
 2 files changed, 246 insertions(+), 40 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 403 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs b/Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs
index 890207fee..c08c8225d 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ArcadeConfigSyncManager.cs
@@ -22,6 +22,22 @@ namespace CosmicShore.Gameplay
     ///
     /// Place on a scene-level GameObject in Menu_Main alongside the existing
     /// ServerPlayerVesselInitializer hierarchy.
+    ///
+    /// <para>
+    /// <b>The open lobby is replicated STATE, not a one-shot message.</b> Open / close /
+    /// intensity / roster used to travel as ClientRpcs, and a ClientRpc reaches exactly the
+    /// clients that are synchronized at the instant it is sent: a guest still inside Netcode
+    /// scene synchronization when the host opened a card had the RPC deferred and then dropped
+    /// (the "[Deferred OnSpawn]" lines in a joiner's log), and a guest who joined AFTER the
+    /// host opened a card was never told at all - so the client sat on the lava lamp while the
+    /// host looked at a lobby, and "come out of the card and click it again" was the only way
+    /// to reach them. <see cref="LobbySnapshot"/> in a server-written NetworkVariable is the
+    /// whole answer: every peer holds the current lobby, a late joiner receives it with the
+    /// spawn and applies it in <see cref="OnNetworkSpawn"/>, and the C# events this class has
+    /// always raised are now DERIVED by diffing the previous value against the new one, so the
+    /// modal did not have to change. The ready-up count stays an RPC - it is a transient
+    /// acknowledgement, not a fact a late joiner needs to catch up on.
+    /// </para>
     /// </summary>
     public class ArcadeConfigSyncManager : NetworkBehaviour
     {
@@ -72,8 +88,105 @@ namespace CosmicShore.Gameplay
         public IReadOnlyList<ArcadeGamePick> GamePicks => _gamePicksView;
         readonly List<ArcadeGamePick> _gamePicksView = new();
 
+        /// <summary>
+        /// The host's open lobby as one value: which card, at what intensity, how many seats,
+        /// how many domains, and which domains the host has placed an AI on. <c>Generation</c>
+        /// climbs on every OPEN so a close-and-reopen of the same card still reads as a new
+        /// open on a peer that never saw the close. AI placements ride as four fixed slots
+        /// rather than an array so the struct stays unmanaged (a NetworkVariable compares and
+        /// copies it by value): a match seats at most <c>ArcadeGameConfigureModal.MaxMatchSeats</c>
+        /// (4), and one of those is always the host, so four slots is one more than can ever be
+        /// used.
+        /// </summary>
+        public struct LobbySnapshot : INetworkSerializable, System.IEquatable<LobbySnapshot>
+        {
+            public const int MaxAiSlots = 4;
+
+            public int  Generation;
+            public bool IsOpen;
+            public int  GameMode;
+            public int  Intensity;
+            public int  PlayerCount;
+            public int  MaxPlayers;
+            public int  DomainCount;
+            public int  AiCount;
+            public int  Ai0, Ai1, Ai2, Ai3;
+
+            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
+            {
+                serializer.SerializeValue(ref Generation);
+                serializer.SerializeValue(ref IsOpen);
+                serializer.SerializeValue(ref GameMode);
+                serializer.SerializeValue(ref Intensity);
+                serializer.SerializeValue(ref PlayerCount);
+                serializer.SerializeValue(ref MaxPlayers);
+                serializer.SerializeValue(ref DomainCount);
+                serializer.SerializeValue(ref AiCount);
+                serializer.SerializeValue(ref Ai0);
+                serializer.SerializeValue(ref Ai1);
+                serializer.SerializeValue(ref Ai2);
+                serializer.SerializeValue(ref Ai3);
+            }
+
+            public bool Equals(LobbySnapshot o) =>
+                Generation == o.Generation && IsOpen == o.IsOpen && GameMode == o.GameMode &&
+                Intensity == o.Intensity && PlayerCount == o.PlayerCount && MaxPlayers == o.MaxPlayers &&
+                DomainCount == o.DomainCount && SameAi(o);
+
+            public bool SameAi(LobbySnapshot o) =>
+                AiCount == o.AiCount && Ai0 == o.Ai0 && Ai1 == o.Ai1 && Ai2 == o.Ai2 && Ai3 == o.Ai3;
+
+            /// <summary>The placed AI domains as the modal consumes them (Domains as ints, placement order).</summary>
```

</details>

### `0c438ade0` — docs(party): record B11-B13 (idle Relay allocation, swallowed re-invite, dropped open RPC)

_Claude, 2026-09-01 18:40:32 +0000_

```text
 Docs/ArcadeLaunch/ARCHITECTURE.md |  34 +++++++++++++++++++++++
 Docs/PartySystem/ARCHITECTURE.md  |  13 +++++++++
 Docs/PartySystem/BUGS.md          | 102 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 3 files changed, 149 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 186 lines)</summary>

```diff
diff --git a/Docs/ArcadeLaunch/ARCHITECTURE.md b/Docs/ArcadeLaunch/ARCHITECTURE.md
index 1c1d42a6e..7441e2c19 100644
--- a/Docs/ArcadeLaunch/ARCHITECTURE.md
+++ b/Docs/ArcadeLaunch/ARCHITECTURE.md
@@ -83,6 +83,40 @@ Two consequences worth knowing:
 *also* carry an inspector `onClick` to it, and a player can double-click — the second call is a
 no-op rather than a second sting and a second `ConfirmLocalPlayerReady`.
 
+### 3.1 The open lobby is replicated STATE, not a message
+
+The commit used to be announced to clients as a one-shot `ClientRpc`, and so were close,
+intensity and roster changes. A ClientRpc reaches exactly the clients that are
+synchronized at the instant it is sent — a guest still inside Netcode scene
+synchronization when the host opened a card had it deferred and dropped, and a guest who
+joined AFTER the host opened a card was never told at all. The client sat on the lava lamp
+while the host looked at a lobby, and re-opening the card was the only way to reach them.
+
+`ArcadeConfigSyncManager` now holds the lobby in one server-written
+`NetworkVariable<LobbySnapshot>`: the card, the intensity, the seat count, the domain
+count, the placed AI domains (four fixed slots — a match seats `MaxMatchSeats` = 4 and one
+of them is always the host — so the struct stays unmanaged), and a **generation** that
+climbs on every open, so a close-and-reopen of the same card is a new open even on a peer
+that never saw the close land. A late joiner receives the value with the spawn and applies
+it in `OnNetworkSpawn`; every other change is DIFFED against the previous value and raised
+through the same C# events (`OnConfigOpenedOnClient` / `OnConfigClosedOnClient` /
+`OnIntensityChangedOnClient` / `OnRosterChangedOnClient`) the modal already listened to, so
+nothing in the modal's handlers changed. An open is followed by the roster when the host
+has already placed AI, so a late joiner's chips are right on the first frame. The modal
+asks for a replay when it subscribes (`ReplayLobbyToSubscribers`) for the
+re-enabled-mid-lobby case.
+
+Two things stay RPCs on purpose: the ready-up count (a transient acknowledgement, not a
+fact a late joiner needs to catch up on) and the legacy screen-change notification (the
+one-panel layout never sends it). The ready-up head-count is read LIVE off the connected
+clients rather than frozen at commit — a member who joins mid-lobby is a human whose press
+the launch must wait for, and one who leaves must stop being waited on; a departure
+re-announces the count and never launches, because a launch is something a press causes.
+
+General rule: **anything a peer must be able to catch up on is state, not an event.** A
+message is right for "this just happened"; it is wrong for "this is the case now", because
+the peers that most need "now" are the ones that were not listening when it was sent.
+
 ## 4. The controls block: the mode's abilities — and the icon animates like the game
 
 `VesselControlsPanel` draws two kinds of row.
diff --git a/Docs/PartySystem/ARCHITECTURE.md b/Docs/PartySystem/ARCHITECTURE.md
index 0eceffc61..3ddaf35d0 100644
--- a/Docs/PartySystem/ARCHITECTURE.md
+++ b/Docs/PartySystem/ARCHITECTURE.md
@@ -401,3 +401,16 @@ predicate") — the matrix decides *what to do*, NetDiag only decides *what to l
 - `../PresenceSystem/ARCHITECTURE.md` — presence-lobby layer
 - `../NetworkDiagnostics/ARCHITECTURE.md` — NetDiag overlay used by all party catches
 - `../THREADING.md` — main-thread affinity rules (mandatory for every UGS / Netcode await)
+
+
+## Relay allocation lifetime (added 2026-09-01)
+
+The eager per-user session allocates a Relay slot on Menu_Main entry, and **Relay reclaims
+an allocation whose host sends nothing for a few minutes** — which a host with zero peers
+always does, because it has no connection to send on. The session then advertises a dead
+join code and every guest bounces at the connect watchdog (`BUGS.md` B11). The design is
+unchanged; what it needed was a lifetime rule: `HostConnectionService` recycles its own
+solo session (`IDLE_SESSION_RECYCLE_SECONDS`, 240s of no members, no outgoing invites, no
+connected Netcode clients, no transition in flight) from the refresh tick, before the
+acceptance scan, so the join code a guest reads is always one the Relay still honours.
+Nothing about this fires once anyone is in or on their way in.
diff --git a/Docs/PartySystem/BUGS.md b/Docs/PartySystem/BUGS.md
index 1702bc0db..36075b67c 100644
--- a/Docs/PartySystem/BUGS.md
+++ b/Docs/PartySystem/BUGS.md
@@ -404,6 +404,15 @@ the invite-chain S10 (VP2 invites VP3 from inside VP1's party); confirm
 no premature `OnClientReady` (FLOW-6 raise must follow the local
 `InitializePair` log) and no `[FLOW-5]`/roster-pull stall.
 
+**Update (2026-09-01).** Two more second-joiner-specific holes closed, both on the
+INVITE side rather than the spawn side — see B12 (a re-invite to a guest who once
+accepted/declined was swallowed forever) and the `AcceptanceSignalService.ScanForSignals`
+change (it returned the FIRST accepter only, so with two invites out the first accepter
+masked the second's signal until the first join was corroborated or the invite expired).
+The spawn-side latch also gained a bounded re-arm (`ServerPlayerVesselInitializer`,
```

</details>

### `1852185be` — Revert "fix(party): recycle an idle Relay allocation before a guest can join it"

_Claude, 2026-09-01 19:27:01 +0000_

```text
This reverts commit 414eb22f9a76aecddb67e983395c8f4d681f21de.
```

```text
 Assets/_Scripts/Controller/Party/HostConnectionService.cs | 89 ---------------------------------------------
 1 file changed, 89 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 114 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index ed0d3a249..232e8229f 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -121,28 +121,6 @@ namespace CosmicShore.Gameplay
         /// </summary>
         private const float SESSION_CREATION_GRACE_PERIOD_SECONDS = 4f;
 
-        /// <summary>
-        /// How long an IDLE party session (hosting, nobody connected, no invite outstanding) may
-        /// sit before it is recycled.
-        ///
-        /// <para><b>Why this exists.</b> Under the locked eager-Relay design every player creates
-        /// a Relay-backed party session on entering Menu_Main, and it then sits with ZERO peers
-        /// until somebody accepts an invite. Unity Relay reclaims an allocation that carries no
-        /// traffic, and when it does the UGS SESSION stays perfectly valid - so every
-        /// session-level self-heal in this class keeps passing while the transport underneath is
-        /// dead. Observed in the field (host log):
-        ///   "Received error message from Relay: player timed out due to inactivity."
-        ///   "Relay allocation is invalid. See ... RelayConnectionStatus.AllocationInvalid"
-        /// After that the advertised session id points at nothing: a guest connects, NGO
-        /// synchronisation never completes, their ClientRpcs are deferred and dropped, and they
-        /// bounce - which is why the only known workaround was restarting the game (a restart
-        /// mints a fresh allocation).
-        ///
-        /// <para>Comfortably under Relay's reclaim window, so the allocation is replaced before
-        /// it can go stale rather than after.</para>
-        /// </summary>
-        private const float IDLE_SESSION_RECYCLE_SECONDS = 240f;
-
         /// <summary>
         /// <see cref="ReconcilePartyMembersNow"/> retries the refresh+sync this many
         /// times to absorb UGS leave-propagation lag, stopping early once the roster
@@ -1240,11 +1218,6 @@ namespace CosmicShore.Gameplay
                 }
                 ForgetWithdrawnInvite(lastHostStillInviting);
 
-                // Keep the idle Relay allocation from going stale under us (see
-                // IDLE_SESSION_RECYCLE_SECONDS). Before the acceptance scan, so a recycle can
-                // never land between a guest reading the session id and joining it.
-                await RecycleIdlePartySessionIfStaleAsync();
-
                 // Acceptance-signal scan. Must run BEFORE the JOINED_PARTY_KEY scan
                 // because recipients won't set joined_party until after they read the
                 // real session id. Gated on outgoing-invite count - no work to do if
@@ -1848,68 +1821,6 @@ namespace CosmicShore.Gameplay
         /// Reentrant: callers from inside <see cref="RefreshAsync"/> (mutex already
         /// held) skip re-acquiring; external callers acquire normally.
         /// </summary>
-        /// <summary>
-        /// Replace an IDLE party session before Unity Relay reclaims its allocation.
-        ///
-        /// <para>Runs only when this player is hosting, NOBODY is connected, and no invite is
-        /// outstanding - so it can never disturb a live party, and can never race a guest who is
-        /// mid-join off an advertised session id. Those two conditions are what make recycling
-        /// safe rather than disruptive; without them this would be a reconnect storm.</para>
-        ///
-        /// <para>Recreating changes the session id, so the new one is republished to the presence
-        /// lobby immediately (the same republish the acceptance handshake uses). With no invites
-        /// outstanding there is nothing else holding the old id.</para>
-        /// </summary>
-        private async UniTask RecycleIdlePartySessionIfStaleAsync()
-        {
-            var session = _partySessionService.ActiveSession;
-            if (session == null) return;
-
-            // Hosting only: a GUEST's "session" is the host's, and leaving it here would eject
-            // this player from the party they are in.
-            if (!connectionData.IsPartyHost) return;
-
-            // Idle only. A connected peer keeps the allocation alive by definition, and an
-            // outstanding invite means a guest may be reading this id right now.
-            if (connectionData.RemotePartyMemberCount > 0) return;
-            if (_inviteService.OutgoingCount > 0) return;
-
-            // Netcode must also be quiet - ConnectedClients covers a peer whose UGS membership
-            // has not been reconciled into PartyMembers yet.
-            var nm = NetworkManager.Singleton;
-            if (nm != null && nm.IsServer && nm.ConnectedClientsIds.Count > 1) return;
-
```

</details>

### `feaf2de70` — fix(party): keep presence live in a match, publish "in game", and trace every join

_Claude, 2026-09-01 22:54:46 +0000_

```text
- HostConnectionService.Update returned outside Menu_Main, so a player in a match
  never refreshed the presence lobby, never expired an invite and never published
  anything; and the launch-time publish ran before the scene changed, so the match
  name it published was empty. The lobby now ticks at a tenth of the menu cadence in
  a game scene (10s) and publishes again once the game scene is active.
- FriendsInitializer.SetPresenceInGame/InMenu/InParty had no caller outside the
  tests; they now follow sceneLoaded.
- MultiplayerSetup logs a [NetTrace] line on both peers for network start/stop,
  every NetworkSceneManager scene event, and every connected client - a handful of
  lines per join, so the next failing join names the half that stalled (host never
  sent Synchronize, or the client never finished its scene load).
- Docs: B11 (idle recycle) recorded as reverted and why; B14 records the live
  retest finding that a host whose NetworkManager was restarted in-process cannot
  get a new guest through synchronization, with the discriminating test; B15 the
  presence fix.

Verification: Roslyn parse-check + conditional-compilation gate only. NOT
editor-verified.
```

```text
 Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs | 82 +++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Party/FriendsInitializer.cs     | 25 +++++++++++++
 Assets/_Scripts/Controller/Party/HostConnectionService.cs  | 31 +++++++++++++++--
 Docs/PartySystem/ARCHITECTURE.md                           | 28 +++++++++------
 Docs/PartySystem/BUGS.md                                   | 83 ++++++++++++++++++++++++++++++++++++++++++--
 5 files changed, 234 insertions(+), 15 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 357 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
index e9f13f3c3..3077dd25a 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
@@ -67,9 +67,86 @@ namespace CosmicShore.Gameplay
                 networkManager.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
                 networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
                 networkManager.OnTransportFailure         -= OnTransportFailure;
+                UnhookJoinTrace(networkManager);
             }
         }
 
+        // --------------------------
+        // Join trace
+        // --------------------------
+
+        // A join that fails at "Netcode client never connected" has exactly two silent halves:
+        // the host's approval + synchronize send, and the client's synchronize + scene load.
+        // Neither side logged either, so a failed join produced nothing but the bounce. These
+        // hooks log the connection and scene-event milestones on BOTH sides - a handful of lines
+        // per join, never per frame - so the next failing log names the half that stalled.
+        NetworkSceneManager _tracedSceneManager;
+
+        void HookJoinTrace(NetworkManager nm)
+        {
+            nm.OnClientConnectedCallback += OnClientConnectedTrace;
+            nm.OnServerStarted           += OnNetworkStartedTrace;
+            nm.OnClientStarted           += OnNetworkStartedTrace;
+            nm.OnServerStopped           += OnNetworkStoppedTrace;
+            nm.OnClientStopped           += OnNetworkStoppedTrace;
+        }
+
+        void UnhookJoinTrace(NetworkManager nm)
+        {
+            nm.OnClientConnectedCallback -= OnClientConnectedTrace;
+            nm.OnServerStarted           -= OnNetworkStartedTrace;
+            nm.OnClientStarted           -= OnNetworkStartedTrace;
+            nm.OnServerStopped           -= OnNetworkStoppedTrace;
+            nm.OnClientStopped           -= OnNetworkStoppedTrace;
+            if (_tracedSceneManager != null)
+            {
+                _tracedSceneManager.OnSceneEvent -= OnSceneEventTrace;
+                _tracedSceneManager = null;
+            }
+        }
+
+        void OnNetworkStartedTrace()
+        {
+            var nm = networkManager;
+            if (nm == null) return;
+            // The scene manager is rebuilt on every Start*, so re-hook per start.
+            var sm = nm.SceneManager;
+            if (sm != null && !ReferenceEquals(sm, _tracedSceneManager))
+            {
+                if (_tracedSceneManager != null) _tracedSceneManager.OnSceneEvent -= OnSceneEventTrace;
+                _tracedSceneManager = sm;
+                sm.OnSceneEvent += OnSceneEventTrace;
+            }
+            CSDebug.Log($"[NetTrace] Network started - IsHost={nm.IsHost} IsServer={nm.IsServer} IsClient={nm.IsClient} " +
+                        $"activeScene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
+                        $"sceneCount={UnityEngine.SceneManagement.SceneManager.sceneCount}");
+        }
+
+        void OnNetworkStoppedTrace(bool wasHost)
+        {
+            CSDebug.Log($"[NetTrace] Network stopped (wasHost={wasHost}).");
+            if (_tracedSceneManager != null)
+            {
+                _tracedSceneManager.OnSceneEvent -= OnSceneEventTrace;
+                _tracedSceneManager = null;
+            }
+        }
+
+        void OnClientConnectedTrace(ulong clientId)
+        {
+            var nm = networkManager;
+            if (nm == null) return;
+            string peers = nm.IsServer ? $" connected={nm.ConnectedClientsIds.Count}" : string.Empty;
+            CSDebug.Log($"[NetTrace] Client {clientId} connected (synchronized) - seen by {(nm.IsServer ? "server" : "client")}{peers}.");
+        }
```

</details>

### `ac0b86205` — fix(party): strip stray NetworkObjects so a restarted host can take a guest again

_Claude, 2026-09-01 23:24:41 +0000_

```text
Root cause of "invites keep failing until we restart the game", of the guest bounce
at 30s, and of B14's restarted-host finding.

Netcode adopts every un-spawned NetworkObject in a loaded scene as an IN-SCENE
PLACED object the moment a machine becomes a server, and indexes in-scene objects
by (GlobalObjectIdHash, sceneHandle). Every instance of one prefab carries the same
hash, so the second un-spawned instance makes PopulateScenePlacedObjects throw -
which leaves that NetworkManager's scene manager half-built and stops every later
guest from completing synchronization. The guest only ever sees the far end of it:
deferred spawn messages for objects it never received, then the connect timeout.

The instances are the fauna. QuadFish, TadPoleFauna, MassSharkFauna and
MassBrittlestarFauna each carry a root NetworkObject as their replication opt-in
(FaunaConfigurationSO.NetworkSynced), and every shipped config leaves that opt-in
off - so each creature the lava lamp spawns is an un-spawned NetworkObject and
Menu_Main holds a dozen of them. A cold-booted host never noticed: it starts in the
Authentication scene, before any fauna exist. Every in-place restart (party leave,
failed-join bounce) loads Menu_Main locally FIRST and starts the host into a scene
already full of identical-hash strays.

- NetworkSceneObjectGuard.NeutralizeStray strips the network layer from anything
  that will never be spawned; FaunaNetworkSync.ServerSpawn calls it on both of its
  declining branches, so strays never accumulate.
- NetworkSceneObjectGuard.Sweep removes the surplus of any duplicate (hash, scene)
  group, and runs before each of the four calls that start a NetworkManager: party
  create, party join, offline StartHost, and game-scene matchmaking.
- FrogletTools > Validation > Audit Stray Network Objects reports the authoring
  combination so the guard never has to fire.
- Docs: BUGS.md B16 (root cause + retest), B14 marked resolved by it, plus the
  architecture, ecosystem-sync and CLAUDE.md anti-pattern entries.

GlobalObjectIdHash is internal in Netcode, so the sweep reads it by reflection -
once, at transition boundaries only, degrading to a no-op with one warning if a
future Netcode renames it. The birth-time strip needs no reflection.

Verification: Roslyn parse-check + conditional-compilation gate only. NOT
editor-verified.
```

```text
 .../_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs |  23 +++-
 Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs            |   4 +
 Assets/_Scripts/Controller/Multiplayer/NetworkSceneObjectGuard.cs     | 198 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Party/HostConnectionService.cs             |  12 ++
 Assets/_Scripts/Editor/StrayNetworkObjectValidator.cs                 |  78 +++++++++++++
 Assets/_Scripts/System/OfflineModeService.cs                          |   1 +
 CLAUDE.md                                                             |   1 +
 Docs/ECOSYSTEM_NETWORK_SYNC.md                                        |  16 +++
 Docs/PartySystem/ARCHITECTURE.md                                      |  15 +++
 Docs/PartySystem/BUGS.md                                              |  71 ++++++++++++
 10 files changed, 417 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 530 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs
index ccd846e9c..931f17649 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs
@@ -152,11 +152,30 @@ namespace CosmicShore.Gameplay
             // per creature is cheap for 32 sharks and is not for 893 quadfish, and the same
             // prefab serves both. A species with no config (a toy release, a manager-spawned
             // drone) is never replicated.
+            // Every branch below that declines to spawn means THIS creature will never be
+            // network-spawned by this peer - ServerSpawn is called once, at birth. Its
+            // NetworkObject is therefore dead weight AND a hazard: Netcode's server-start sweep
+            // adopts every un-spawned NetworkObject as an IN-SCENE object, and a second instance
+            // of the same prefab then collides in the scene-object index and breaks
+            // synchronization for every joining player. The menu's fauna are exactly this case -
+            // NetworkSynced is off, so a dozen identical-hash strays swim in Menu_Main. Strip the
+            // network layer at birth instead. See Docs/PartySystem/BUGS.md B16.
             var cfg = spawned.SourceConfig;
-            if (!cfg || !cfg.NetworkSynced) return;
+            if (!cfg || !cfg.NetworkSynced)
+            {
+                NetworkSceneObjectGuard.NeutralizeStray(spawned.gameObject, "fauna species is not network-synced");
+                return;
+            }
 
             var nm = NetworkManager.Singleton;
-            if (nm == null || !nm.IsListening || !nm.IsServer) return;
+            if (nm == null || !nm.IsListening || !nm.IsServer)
+            {
+                // A client's own locally-simulated fauna, or anything spawned while no session is
+                // live. The replicated copies arrive from the server as their own objects.
+                NetworkSceneObjectGuard.NeutralizeStray(spawned.gameObject, "fauna spawned outside a live server session");
+                return;
+            }
+
             if (!spawned.TryGetComponent(out NetworkObject netObj)) return;
             if (netObj.IsSpawned) return;
 
diff --git a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
index 3077dd25a..45af2fb5f 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
@@ -311,6 +311,10 @@ namespace CosmicShore.Gameplay
                 await UniTask.WaitUntil(() => !networkManager.IsListening);
             }
 
+            // Netcode adopts every un-spawned NetworkObject in the scene the moment this
+            // machine becomes a server or a client (see NetworkSceneObjectGuard).
+            NetworkSceneObjectGuard.Sweep("before game session create/join");
+
             // Query sessions for this game mode & player count
             var sessions = await QuerySessions();
 
diff --git a/Assets/_Scripts/Controller/Multiplayer/NetworkSceneObjectGuard.cs b/Assets/_Scripts/Controller/Multiplayer/NetworkSceneObjectGuard.cs
new file mode 100644
index 000000000..bb60ea921
--- /dev/null
+++ b/Assets/_Scripts/Controller/Multiplayer/NetworkSceneObjectGuard.cs
@@ -0,0 +1,198 @@
+using System;
+using System.Collections.Generic;
+using System.Reflection;
+using CosmicShore.Utility;
+using Unity.Netcode;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Removes STRAY NetworkObjects - prefab instances that were created with plain
+    /// <c>Instantiate</c> and never network-spawned - before a NetworkManager can adopt them.
+    ///
+    /// <para><b>Why this exists.</b> When a server starts, Netcode sweeps the loaded scenes and
+    /// adopts every NetworkObject it finds that is not already spawned, treating each one as an
+    /// IN-SCENE PLACED object. Netcode then indexes in-scene objects by
+    /// <c>(GlobalObjectIdHash, sceneHandle)</c>, and that index must be unique - a second object
+    /// with the same pair makes <c>NetworkSceneManager.PopulateScenePlacedObjects</c> THROW.
+    /// Every instance of one prefab carries the SAME hash, so N un-spawned instances of the same
+    /// prefab in one scene is a guaranteed exception the moment a host starts or a client
+    /// synchronizes.</para>
+    ///
```

</details>

### `6d9de6dd7` — fix(tools): report the staged fauna replication rig as information, not 42 warnings

_Claude, 2026-09-01 23:42:12 +0000_

```text
The audit flagged all 42 fauna configs at warning level for a state that is
correct by design: every fauna prefab carries the full staged replication rig
(NetworkObject + FaunaNetworkSync) and every config leaves NetworkSynced off,
which is the documented pre-rollout state in Docs/ECOSYSTEM_NETWORK_SYNC.md.
Nothing there needs changing, and 42 warnings say the opposite.

- Reports per PREFAB, not per config: four prefabs referenced by 42 configs is
  one fact, not 42.
- Severity now matches the finding: INFO for a staged rig with the opt-in off
  (the guard strips creatures at birth, nothing to do); WARNING only for a
  prefab carrying a NetworkObject with NO rig behind it, which is liability with
  no rollout intent; ERROR for a config opted IN whose prefab cannot honour it.
- NetworkSceneObjectGuard.NeutralizeStray drops its per-call array allocation
  (reused list): it runs once per creature birth and a heavy cell seeds hundreds
  in a frame.

The fauna prefabs and the network prefab list are deliberately left alone - the
rig is staged infrastructure, and stripping it would undo the rollout design.

Verification: Roslyn parse-check + conditional-compilation gate only. NOT
editor-verified.
```

```text
 Assets/_Scripts/Controller/Multiplayer/NetworkSceneObjectGuard.cs | 12 +++++--
 Assets/_Scripts/Editor/StrayNetworkObjectValidator.cs             | 71 +++++++++++++++++++++++++------------
 Docs/ECOSYSTEM_NETWORK_SYNC.md                                    |  7 ++++
 3 files changed, 64 insertions(+), 26 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 153 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/NetworkSceneObjectGuard.cs b/Assets/_Scripts/Controller/Multiplayer/NetworkSceneObjectGuard.cs
index bb60ea921..7d2d50429 100644
--- a/Assets/_Scripts/Controller/Multiplayer/NetworkSceneObjectGuard.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/NetworkSceneObjectGuard.cs
@@ -53,6 +53,9 @@ namespace CosmicShore.Gameplay
         /// <summary>Hashes already reported, so a recurring stray logs once rather than per sweep.</summary>
         static readonly HashSet<uint> _reported = new();
 
+        /// <summary>Reused by <see cref="NeutralizeStray(NetworkObject,string)"/> - see the note there.</summary>
+        static readonly List<NetworkBehaviour> _behaviourScratch = new();
+
         /// <summary>
         /// Strips the network layer from a GameObject that will never be network-spawned, so
         /// Netcode's start-up sweep cannot adopt it as an in-scene object. Safe to call on
@@ -80,14 +83,17 @@ namespace CosmicShore.Gameplay
             // NetworkBehaviours first: a NetworkBehaviour whose NetworkObject vanished logs an
             // error on its next enable/disable, and they are inert on an object that will never
             // be spawned anyway.
-            var behaviours = go.GetComponentsInChildren<NetworkBehaviour>(true);
-            for (int i = 0; i < behaviours.Length; i++)
+            // Reused list, not the allocating overload: this runs once per creature BIRTH and a
+            // heavy cell seeds hundreds of them in one frame.
+            go.GetComponentsInChildren(true, _behaviourScratch);
+            for (int i = 0; i < _behaviourScratch.Count; i++)
             {
-                var behaviour = behaviours[i];
+                var behaviour = _behaviourScratch[i];
                 // Leave anything belonging to a NESTED NetworkObject alone - it is not ours to strip.
                 if (!behaviour || behaviour.NetworkObject != netObj) continue;
                 UnityEngine.Object.DestroyImmediate(behaviour);
             }
+            _behaviourScratch.Clear();
 
             UnityEngine.Object.DestroyImmediate(netObj);
 
diff --git a/Assets/_Scripts/Editor/StrayNetworkObjectValidator.cs b/Assets/_Scripts/Editor/StrayNetworkObjectValidator.cs
index 5d2005ea3..f2a110b0a 100644
--- a/Assets/_Scripts/Editor/StrayNetworkObjectValidator.cs
+++ b/Assets/_Scripts/Editor/StrayNetworkObjectValidator.cs
@@ -1,5 +1,6 @@
 using System.Collections.Generic;
 using System.Linq;
+using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using Unity.Netcode;
 using UnityEditor;
@@ -38,12 +39,13 @@ namespace CosmicShore.Editor
                 if (go && go.GetComponent<NetworkObject>()) networked.Add(go);
             }
 
-            Debug.Log($"[StrayNetworkObjects] {networked.Count} prefab(s) carry a root NetworkObject.");
+            // Group by PREFAB, never by config: four fauna prefabs are referenced by 42 configs,
+            // and a line per config reports one fact 42 times. The prefab is the thing an author
+            // would actually change.
+            var staged = new Dictionary<GameObject, List<string>>();   // rig present, opt-in off - fine
+            var bare = new Dictionary<GameObject, List<string>>();     // NetworkObject, no rig - a real stray
+            var broken = new Dictionary<GameObject, List<string>>();   // opt-in ON, rig incomplete - dead opt-in
 
-            // The fauna case: a species prefab carries a NetworkObject as its replication opt-in,
-            // but the CONFIG leaves that opt-in off - so every creature the cell spawns is a
-            // stray. This is the exact combination that shipped in the menu's lava lamp.
-            int flagged = 0;
             foreach (var guid in AssetDatabase.FindAssets("t:FaunaConfigurationSO"))
             {
                 var path = AssetDatabase.GUIDToAssetPath(guid);
@@ -51,28 +53,51 @@ namespace CosmicShore.Editor
                 if (!cfg || !cfg.FaunaPrefab) continue;
 
                 var prefabGo = cfg.FaunaPrefab.gameObject;
-                if (!prefabGo.GetComponentInChildren<NetworkObject>(true)) continue;
-                if (cfg.NetworkSynced) continue;
+                bool hasNetObj = prefabGo.GetComponentInChildren<NetworkObject>(true);
+                bool hasRig = prefabGo.GetComponentInChildren<FaunaNetworkSync>(true);
 
-                flagged++;
-                Debug.LogWarning(
-                    $"[StrayNetworkObjects] '{cfg.name}' spawns '{prefabGo.name}', which carries a " +
-                    "NetworkObject, but NetworkSynced is OFF - every creature it spawns is an un-spawned " +
-                    "NetworkObject. NetworkSceneObjectGuard strips these at birth, so this is not fatal; " +
-                    "it is dead weight on the prefab. Either turn NetworkSynced on, or remove the " +
```

</details>

### `3b74b892a` — docs(party): mark B12-B14/B16 live-verified

_Claude, 2026-09-02 00:23:56 +0000_

```text
The join sequence that failed every time - party up, play a game, return, leave,
re-invite - now completes on both machines, and the stray-NetworkObject audit
reports only the staged fauna rollout state. B14's restarted-host finding is
recorded as resolved by B16's root cause. B11 stays reverted.
```

```text
 Docs/PartySystem/BUGS.md | 13 +++++++++----
 1 file changed, 9 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/PartySystem/BUGS.md b/Docs/PartySystem/BUGS.md
index 32b2ffc9b..b1b5a204f 100644
--- a/Docs/PartySystem/BUGS.md
+++ b/Docs/PartySystem/BUGS.md
@@ -874,7 +874,7 @@ in the party without a bounce. Watch for one `Recycling idle party session` line
 
 ---
 
-## B12 — A host can never re-invite a guest who once accepted or declined 🟢 (fixed 2026-09-01, live retest pending)
+## B12 — A host can never re-invite a guest who once accepted or declined 🟢 (fixed 2026-09-01, live-verified 2026-09-02)
 
 **Symptom.** Guest B accepts A's invite and bounces (B11, or any join failure). A
 invites B again: B's popup never appears, forever. Same if B DECLINED. Only a host
@@ -905,7 +905,7 @@ cancels it → A invites B again → popup appears and the join completes.
 
 ---
 
-## B13 — Open-lobby ClientRpc dropped on a syncing / late-joining client 🟢 (fixed 2026-09-01, live retest pending)
+## B13 — Open-lobby ClientRpc dropped on a syncing / late-joining client 🟢 (fixed 2026-09-01, live-verified 2026-09-02)
 
 **Symptom.** Host opens a card; the guest stays on the lava lamp. "If the host comes
 out of the card and clicks again the client should be pulled in" — and even that only
@@ -934,7 +934,7 @@ an AI with a guest in the lobby → guest's row and chips follow.
 
 ---
 
-## B14 — A host whose NetworkManager was RESTARTED in-process cannot get a new guest through synchronization 🔴 (open, 2026-09-01 — trace landed, logs needed)
+## B14 — A host whose NetworkManager was RESTARTED in-process cannot get a new guest through synchronization 🟢 (root cause = B16; fixed + LIVE-VERIFIED 2026-09-02)
 
 **Symptom (live retest, 2026-09-01, post-fix branch).** First run: A and B form a party,
 play a game, come back. Then (a) A leaves the lobby and quits, B stays; A relaunches; B
@@ -1008,7 +1008,7 @@ follows `sceneLoaded` into In Game / In Party / In Menu.
 
 ---
 
-## B16 — Un-spawned fauna NetworkObjects break synchronization for every guest 🟢 (root cause; fixed 2026-09-01, live retest pending)
+## B16 — Un-spawned fauna NetworkObjects break synchronization for every guest 🟢 (root cause; fixed + LIVE-VERIFIED 2026-09-02)
 
 **Symptom.** A guest accepts an invite, sits on the splash for 30s and is bounced
 ("Couldn't join"). The guest's log shows `[Netcode] [Deferred OnSpawn] Messages were received
@@ -1065,6 +1065,11 @@ followed "playing a game" (a game is what you leave), and why the reverted idle-
 once, at transition boundaries only, and degrading to a no-op with one warning if a future
 Netcode renames it. The birth-time strip needs no reflection and is the primary defence.
 
+**Verified live (2026-09-02).** The sequence that had failed every time - party up, play a
+game, return, leave, re-invite - now joins cleanly on the reporter's two machines, and the
+audit reports the fauna prefabs as the staged-rollout state with no unstaged strays. The
+invite/accept path also stayed fast, so B12 and B13 are confirmed with it.
+
 **Retest.** (1) A and B fresh-boot, party up, play a game, return, **A leaves**, then A invites
 B → the join should complete. (2) Repeat with B leaving, and with both leaving. (3) With a
 guest in the party, watch for `[NetworkSceneObjectGuard]` warnings — one per prefab is expected
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
