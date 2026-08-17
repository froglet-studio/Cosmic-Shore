
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    public class GrowthInfo
    {
        public bool CanGrow;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsDangerous;
        public int Depth;
    }

    /// <summary>
    /// Colony-wide event counters for the gyroid octagon population, plus a 5s heartbeat the
    /// FOUNDER logs. Diagnostics, not gameplay: this colony's playtests kept producing symptoms
    /// (a crystal/spindle flood outrunning grown prisms; "twinned" surfaces that would not mate)
    /// which code reading alone could not attribute - so the decisive sites now report.
    /// Volume is naturally low (births and mints are rare events in a healthy colony; a SPAMMING
    /// counter in the console IS the diagnosis). Always compiled - plain CSDebug, no editor-only
    /// guards (Docs/CONDITIONAL_COMPILATION.md).
    /// </summary>
    /// <summary>
    /// Colony-wide event counters for the Schwarz P TILE population (Docs/ECOSYSTEM.md 33.6).
    /// A separate class from <see cref="GyroidColonyDiagnostics"/> on purpose: the two colonies
    /// can live in one cell, and a merged counter is unattributable.
    ///
    /// <para>Deliberately smaller than the gyroid's. Every counter that colony needs for FRAME
    /// COHERENCE - ring poison, ring-coherence error, seed-handoff error, declined foreign
    /// sites - measures a defect class a tile colony cannot have: a tile is an exact integer
    /// address, so two plants either agree or belong to different lattices. What is left is
    /// what can still go wrong: a birth that should not have happened, a mint that would go
    /// off-lattice, and a site that grew without a reservation.</para>
    ///
    /// <para>Always compiled - plain CSDebug, no editor-only guards
    /// (Docs/CONDITIONAL_COMPILATION.md).</para>
    /// </summary>
    public static class SchwarzPColonyDiagnostics
    {
        public static int Founders;
        public static int Births;
        public static int ReseedMintsBlocked;
        public static int UnreservedSpawns;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForDomainReload()
        {
            Founders = Births = ReseedMintsBlocked = UnreservedSpawns = 0;
        }

        /// <summary>
        /// The heartbeat. Healthy readings, so a playtest is decisive rather than suggestive:
        /// <c>plants</c> climbs by one per birth and equals the live claim count;
        /// <c>frontier</c> grows when a plant completes and shrinks by one per birth;
        /// <c>prismsPerPlant</c> settles at exactly SiteCount(level) - 36 at the shipped
        /// separation - because a tile is a closed set of sites, not a spread;
        /// <c>mintsBlocked</c> and <c>unreserved</c> must both stay 0.
        /// </summary>
        public static string Summary(Cell cell, FloraConfigurationSO species, int livePrisms)
        {
            int plants = SchwarzPTileRegistry.ClaimCount;
            return $"[SchwarzPColony] founders={Founders} plants={plants} births={Births} " +
                   $"frontier={SchwarzPColonyFrontier.TotalCount} " +
                   $"prismsPerPlant={(plants > 0 ? (float)livePrisms / plants : 0f):F1} " +
                   $"mintsBlocked={ReseedMintsBlocked} unreserved={UnreservedSpawns}";
        }
    }

    public static class GyroidColonyDiagnostics
    {
        public static int Births;
        public static int ReseedMintsBlocked;
        public static int DeclinedForeignSites;
        public static int GrownPrisms;
        public static int SeededDaughterPrisms;
        public static int UnreservedSpawns;

        // Lattice-misalignment gate (born as the "twinning" auditor): a healthy gyroid's
        // closest prism pair is 6.6u apart, and TryReserve blocks within ~3.1u - so a NEW site
        // with an existing prism 3.1-5.5u away is a misaligned lattice domain being born. Such
        // sites are now DECLINED (misaligned colonies stop at a clean interface instead of
        // interpenetrating); these count what was blocked, split by GROWN sites (bond-table
        // continuation) and daughter SEED sites (the neighbour-table handoff), because that
        // split is what points at the faulty mechanism.
        public static int GrownSiteDefects;
        public static int SeedSiteDefects;
        public static int RotationFallbacks;
        public static float MaxRingCoherenceError;
        const int DefectLogLimit = 24;
        static int _defectLogs;

        public static void ReportDefect(string kind, Vector3 position, string owner)
        {
            if (_defectLogs >= DefectLogLimit) return;
            _defectLogs++;
            CSDebug.LogWarning($"[GyroidColony] LATTICE MISALIGNMENT ({kind}) at {position} by {owner} - " +
                               $"an existing prism sits 3.1-5.5u away (healthy minimum is 6.6u), " +
                               $"so the site was declined rather than grown. " +
                               $"({_defectLogs}/{DefectLogLimit} logged)");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Births = 0;
            ReseedMintsBlocked = 0;
            DeclinedForeignSites = 0;
            GrownPrisms = 0;
            SeededDaughterPrisms = 0;
            UnreservedSpawns = 0;
            GrownSiteDefects = 0;
            SeedSiteDefects = 0;
            RotationFallbacks = 0;
            RingPoisonRejected = 0;
            IncoherentHandoffs = 0;
            MaxRingCoherenceError = 0f;
            MaxSeedHandoffError = 0f;
            _defectLogs = 0;
        }

        /// <summary>Danger prisms whose self-computed centre landed in the poison band
        /// (coherent-membership tolerance .. claim-dedupe radius) of a plant's claimed centre -
        /// a MISALIGNED lattice frame knocking on a ring. Rejected from membership.</summary>
        public static int RingPoisonRejected;

        /// <summary>Daughter seed handoffs whose seed pose failed to recompute the adopted
        /// centre (> 1u). Non-zero means the neighbour tables (or their composition) are wrong
        /// IN-ENGINE - the defect class that shipped as the z-mirror table corruption.</summary>
        public static int IncoherentHandoffs;

        /// <summary>Worst daughter seed-handoff centre error observed (healthy &lt; 0.1u).</summary>
        public static float MaxSeedHandoffError;

        public static string Summary =>
            $"claims={GyroidOctagonRegistry.ClaimCount} births={Births} frontier={GyroidColonyFrontier.TotalCount} " +
            $"grown={GrownPrisms} seeded={SeededDaughterPrisms} foreignDeclines={DeclinedForeignSites} " +
            $"mintsBlocked={ReseedMintsBlocked} UNRESERVED={UnreservedSpawns} " +
            $"BLOCKED grown={GrownSiteDefects} seed={SeedSiteDefects} poison={RingPoisonRejected} " +
            $"HANDOFF bad={IncoherentHandoffs} errMax={MaxSeedHandoffError:F2} rotFallbacks={RotationFallbacks} " +
            $"ringErrMax={MaxRingCoherenceError:F2} prismsPerCrystal={(GyroidOctagonRegistry.ClaimCount > 0 ? (float)(GrownPrisms + SeededDaughterPrisms) / GyroidOctagonRegistry.ClaimCount : 0f):F1}";
    }

    /// <summary>
    /// A flora that uses an <c cref="Assembler">Assembler</c> to define its growth pattern
    /// </summary>
    public class AssembledFlora : Flora
    {
        struct Branch
        {
            public GameObject gameObject;
            public int depth;
            public Assembler assembler;

            public Branch(HealthPrism healthPrism)
            {
                gameObject = healthPrism.gameObject;
                depth = 0;
                assembler = healthPrism.GetComponent<Assembler>();
            }
        }
        
        /// <summary>
        /// The max recursion depth of the assembler
        /// </summary>
        [SerializeField] int depth = 50;
        [Tooltip("Maximum LIVE prisms this flora can hold. Consumption frees budget - a grazed " +
                 "flora regrows toward this cap instead of staying a permanent un-growing fragment.")]
        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] int maxDepth = 30;
        [SerializeField] int itemsPerGrow = 5;
        [SerializeField] int randomItems = 2;
        [SerializeField] float crystalGrowth = 1.01f;
        [Tooltip("How many surviving prisms to re-sprout growth branches from when every active " +
                 "branch has been consumed or exhausted (a mature gyroid has none). This is what " +
                 "lets a grazed flora 'reawaken' instead of sitting as a dead fragment.")]
        [SerializeField] int reseedBranchCount = 3;

        [Tooltip("Grow-order instantiations executed per frame. The grow TICK decides up to " +
                 "itemsPerGrow children at once (claiming their sites in the spatial index); " +
                 "instantiating them all in that one frame was a multi-ms burst (prism + spindle " +
                 "prefab per child). Draining the decided orders at this rate spreads the cost " +
                 "over a few frames of the 3s grow period — pacing only, throughput preserved.")]
        [SerializeField, Min(1)] int maxSpawnsPerFrame = 1;

        /// <summary>The live-prism budget this individual resolved to - the base reads it for
        /// the reproduction maturity gate (see <see cref="Flora.PrismBudget"/>).</summary>
        protected override int PrismBudget => maxTotalSpawnedObjects;

        /// <summary>Assembled layer of the variant expression: the live-prism budget
        /// (Mass gyroid 1500 / Space 800 - the per-element density identity).</summary>
        public override void ApplyVariantTuning(FloraVariantTuning tuning)
        {
            base.ApplyVariantTuning(tuning);
            if (tuning == null) return;
            if (tuning.MaxTotalSpawnedObjects >= 0) maxTotalSpawnedObjects = tuning.MaxTotalSpawnedObjects;
            // Cell density scalar, applied AFTER the absolute so it scales whatever budget won.
            // Round half UP explicitly: Mathf.RoundToInt is banker's rounding, which would turn
            // an authored 150 x 0.9 into 134 on one species and 135 on the next.
            if (tuning.MaxTotalSpawnedObjectsScale > 0f)
                maxTotalSpawnedObjects = Mathf.Max(1, Mathf.FloorToInt(
                    maxTotalSpawnedObjects * tuning.MaxTotalSpawnedObjectsScale + 0.5f));

            // Per-element lattice scale. Stored rather than applied here: the assemblers
            // that need it do not exist yet (they are created per prism), so every creation
            // site pushes it - see ApplyLatticeSpacing.
            if (tuning.LatticeScale > 0f) _latticeScaleOverride = tuning.LatticeScale;

            // Pacing overrides - the prefab's throttles are shared by every biome's plants,
            // so a cell that wants a different pace authors it in config instead.
            if (tuning.ItemsPerGrow >= 1) itemsPerGrow = tuning.ItemsPerGrow;
            if (tuning.RandomItems >= 0) randomItems = tuning.RandomItems;
            if (tuning.MaxSpawnsPerFrame >= 1) maxSpawnsPerFrame = tuning.MaxSpawnsPerFrame;
            if (tuning.MaturationSeconds >= 0f) _maturationSeconds = tuning.MaturationSeconds;
        }

        // A growth decision made at the grow tick, executed by the per-frame drain.
        // The site is already claimed (GetGrowthInfo → TryReserve), which is what
        // makes the deferred Instantiate safe against siblings growing into it.
        struct GrowOrder
        {
            public Branch parent;
            public GrowthInfo info;
            public float decidedAt;
        }

        // A Frenzy hold can keep orders queued longer than the spatial-index
        // reservation TTL; executing an order whose claim lapsed can overlap
        // another grower's spawn on the same site. Orders older than this are
        // dropped at drain (one second of safety margin under the TTL) — the next
        // grow tick simply re-decides them.
        const float MaxOrderAgeSeconds = PrismSpatialIndex.ReservationTtlSeconds - 1f;

        readonly Queue<GrowOrder> pendingSpawns = new Queue<GrowOrder>();

        HashSet<Branch> activeBranches = new HashSet<Branch>();

        Assembler assembler;

        public static class AssemblerFactory
        {
            public static Assembler ProgramAssembler(GameObject gameObject, GrowthInfo growthInfo)
            {
                if (growthInfo is GyroidGrowthInfo)
                {
                    var newAssembler = gameObject.GetComponent<GyroidAssembler>();
                    // Copy properties from growthInfo.assembler to newAssembler
                    newAssembler.BlockType = ((GyroidGrowthInfo)growthInfo).BlockType;
                    newAssembler.Depth = growthInfo.Depth;
                    // Copy other properties as needed
                    return newAssembler;
                }
                else if (growthInfo is SchwarzPGrowthInfo schwarzPGrowthInfo)
                {
                    var newAssembler = gameObject.GetComponent<SchwarzPAssembler>();
                    newAssembler.Program(schwarzPGrowthInfo);
                    return newAssembler;
                }
                // Add other assembler types here as needed
                else
                {
                    var newAssembler = gameObject.GetComponent<Assembler>();
                    // Copy properties from growthInfo.assembler to newAssembler
                    newAssembler.Depth = growthInfo.Depth;
                    // Copy other properties as needed
                    return newAssembler;
                }
            }
        }

        public override void Grow()
        {
            // Population duties run BEFORE the early returns below, because the plants that
            // matter most to reproduction are exactly the ones those returns retire from
            // growing: a complete plant (budget-full, branchless) must still contribute its
            // frontier once and keep a hand on the population's reproduction clock.
            if (OctagonMode) TickOctagonPopulation();
            if (TileColonyMode) TickTilePopulation();

            // Live-prism budget: the flora can hold at most maxTotalSpawnedObjects LIVE
            // prisms. Consumption frees budget, so a grazed flora regrows. (Was: a
            // lifetime spawn counter that never decremented - a fully-grown flora could
            // never grow again even after fauna ate most of it, which is exactly the
            // "ungrowing gyroid fragments" failure observed in-game.)
            if (healthTracker != null && healthTracker.Count >= maxTotalSpawnedObjects)
            {
                _idleGrowTicks++;   // budget-full is a finished frontier (see OctagonMature)
                return;
            }

            // Frenzy gate: flora grow at a steady rate until the cell crosses into Frenzy,
            // then freeze, resuming automatically when an active force (fauna grazing /
            // vessel abilities) brings the count back below the Frenzy exit threshold.
            // Cell.FloraGrowingEnabled is the single source of truth - no early growth cap
            // (that staggered self-limit was a cheat; the food web is the only down-force).
            // Deliberately does NOT touch the idle counter: a PAUSED plant is not a
            // finished one, and maturity must not accrue while the cell holds growth shut.
            if (cell && !cell.FloraGrowingEnabled) return;

            // Reawakening: a flora whose active branches were all consumed or exhausted
            // (a fully-grown gyroid has none) re-sprouts from surviving prisms instead
            // of staying a permanent un-growing fragment. Growth continues next cycle
            // from the re-seeded branches. Guarded on having surviving prisms so a
            // not-yet-seeded flora doesn't spuriously seed here before Plant()/Initialize
            // finish their own setup.
            if (activeBranches.Count == 0)
            {
                _idleGrowTicks++;   // decided nothing this tick either way
                if (healthTracker != null && healthTracker.Count > 0)
                    ReseedBranches();
                return;
            }

            List<Branch> branchesToRemove = new List<Branch>();

            // Decision pass only: pick branches, claim sites, enqueue. The heavyweight
            // Instantiate work (prism + spindle prefab per child — the multi-ms
            // AssembledFlora.GrowCoroutine burst in captures) drains at
            // maxSpawnsPerFrame in Update. Plain enumeration replaces the old
            // ElementAt(i) walk — LINQ ElementAt on a HashSet is O(i) per call
            // (O(n²) per tick) and allocates an enumerator every call; enumeration
            // order is identical.
            float itemsSpawned = 0;
            int skippedItems = 0;
            foreach (Branch branch in activeBranches)
            {
                if (itemsSpawned >= itemsPerGrow) break;

                if (!branch.assembler || branch.depth >= maxDepth)
                {
                    continue;
                }

                // Randomly skip grow sites BEFORE GetGrowthInfo: a successful
                // GetGrowthInfo claims the site in the PrismSpatialIndex, and a
                // skipped-after-claim site would hold its reservation until TTL,
                // blocking siblings from growing there in the meantime.
                if (skippedItems < randomItems && Random.value < 0.5f)
                {
                    skippedItems++;
                    continue;
                }

                var growthInfo = branch.assembler.GetGrowthInfo();
                if (!growthInfo.CanGrow)
                {
                    branchesToRemove.Add(branch);
                    continue;
                }

                // Octagon colony: a site in a NEIGHBOURING plant's territory is not this
                // plant's to grow - that plant lays the same world position from its own
                // lattice. Decline marks the bond site filled so the branch moves on instead
                // of re-offering it every tick. The reservation GetGrowthInfo just made is
                // released HERE, not left to its TTL: the owner's frontier reaches this same
                // world site within seconds (grow cadence 0.1-1s vs a 5s TTL), its TryReserve
                // fails against the orphan, and GetGrowthInfo treats reserve-failure as
                // 'occupied for real' - permanently bonding the owner's site. Left in place,
                // that race punched a lasting hole at roughly every seam site the NON-owner
                // probed first: missing danger prisms, open ring arcs, crystals apparently
                // outside their octagons (the fifth Lab playtest). The validated colony sim
                // had perfect-information reservations - this release is what makes Unity
                // match the model that was proven correct.
                if (OctagonMode && growthInfo is GyroidGrowthInfo gyroidInfo &&
                    !OwnsLatticeSite(gyroidInfo.Position))
                {
                    (branch.assembler as GyroidAssembler)?.DeclineGrowthSite(gyroidInfo.Site);
                    PrismSpatialIndex.Instance?.ReleaseReservation(gyroidInfo.Position);
                    GyroidColonyDiagnostics.DeclinedForeignSites++;
                    continue;
                }

                // Lattice-misalignment GATE: TryReserve (inside GetGrowthInfo) already proved
                // no prism within ~3.1u, and a coherent lattice's closest non-bonded pair is
                // 6.6u - so mass within 5.5u of this site belongs to a lattice frame MISALIGNED
                // with this plant's (an independent founder, a toy planting). Growing it would
                // mint a visible twin, so the site is DECLINED like a foreign one: colonies
                // that cannot mate stop at a clean interface instead of interpenetrating.
                // (Was audit-only for one playtest, which attributed the defect geography.)
                if (OctagonMode && growthInfo.CanGrow)
                {
                    var idx = PrismSpatialIndex.Instance;
                    if (idx != null && idx.IsAvailable && idx.IsAnyPrismWithin(growthInfo.Position, 5.5f))
                    {
                        GyroidColonyDiagnostics.GrownSiteDefects++;
                        GyroidColonyDiagnostics.ReportDefect("grown-blocked", growthInfo.Position, name);
                        if (growthInfo is GyroidGrowthInfo gi)
                            (branch.assembler as GyroidAssembler)?.DeclineGrowthSite(gi.Site);
                        idx.ReleaseReservation(growthInfo.Position);
                        continue;
                    }
                }

                pendingSpawns.Enqueue(new GrowOrder { parent = branch, info = growthInfo, decidedAt = Time.time });
                itemsSpawned++;
            }

            foreach (Branch branch in branchesToRemove)
            {
                activeBranches.Remove(branch);
            }

            // The maturity clock: a tick that decided at least one child is a growing plant;
            // a run of deciding-nothing ticks is a finished one (see OctagonMature).
            if (itemsSpawned > 0) _idleGrowTicks = 0;
            else _idleGrowTicks++;

            GrowCrystal();
        }

        void Update()
        {
            if (pendingSpawns.Count == 0) return;

            // Parity with the old WaitForSeconds-driven grow loop: growth froze at
            // timeScale 0 (menu pause), so the drain does too.
            if (Time.timeScale <= 0f) return;

            // Frenzy gate re-checked at drain time: orders decided just before the
            // cell crossed into Frenzy WAIT here (sites stay claimed) and execute
            // when growing re-enables — same freeze-and-resume the tick gate gives.
            if (cell && !cell.FloraGrowingEnabled) return;

            int spawned = 0;
            while (spawned < maxSpawnsPerFrame && pendingSpawns.Count > 0)
            {
                var order = pendingSpawns.Dequeue();

                // Order held too long (see MaxOrderAgeSeconds) — drop it, and release its
                // reservation NOW rather than letting it lapse: the next grow tick re-decides
                // this branch, offers the same site, and would fail TryReserve against its OWN
                // stale claim — GetGrowthInfo then marks the site permanently bonded, so a
                // long Frenzy hold would quietly kill every site that had an order pending.
                if (Time.time - order.decidedAt > MaxOrderAgeSeconds)
                {
                    PrismSpatialIndex.Instance?.ReleaseReservation(order.info.Position);
                    continue;
                }

                ExecuteGrowOrder(order);
                spawned++;
            }
        }

        protected override void Die(string killerName = "")
        {
            // The old inline path stopped instantiating on death for free —
            // Die's StopAllCoroutines killed GrowCoroutine, the only spawn site.
            // The drain is not a coroutine, so without this a dying flora kept
            // executing pending orders through its wither window: a fresh spindle
            // registering on the corpse stalls DieCoroutine's empty-tracker wait
            // (zombie flora), and a child parented under an evaporating spindle is
            // hard-destroyed with it (a pop-out). Claimed sites are released as the
            // queue drains - an orphan reservation blocks a neighbour's TryReserve for
            // its whole TTL, and that neighbour's reserve-failure permanently bonds
            // the site (see the decline path in Grow).
            while (pendingSpawns.Count > 0)
                PrismSpatialIndex.Instance?.ReleaseReservation(pendingSpawns.Dequeue().info.Position);
            base.Die(killerName);
        }

        void ExecuteGrowOrder(GrowOrder order)
        {
            // Parent spindle may have been consumed in the few frames between
            // decision and drain — the claimed site simply expires with its
            // reservation TTL, exactly like the old skip-after-claim path.
            if (!order.parent.gameObject || !order.parent.assembler) return;

            var growthInfo = order.info;

            HealthPrism newHealthPrism = Instantiate(healthPrism, growthInfo.Position, growthInfo.Rotation);
            AddHealthBlock(newHealthPrism);
            Branch newBranch = new Branch(newHealthPrism);

            var newAssembler = AssemblerFactory.ProgramAssembler(newHealthPrism.gameObject, growthInfo);
            ApplyLatticeSpacing(newAssembler);
            if (newAssembler == null)
            {
                CSDebug.LogError("Failed to create assembler");
                return;
            }

            Spindle newSpindle = Instantiate(spindle, order.parent.gameObject.transform);
            newSpindle.LifeForm = this;
            newSpindle.transform.position = newHealthPrism.transform.position;
            newSpindle.transform.rotation = newHealthPrism.transform.rotation;

            newHealthPrism.transform.SetParent(newSpindle.transform, false);
            newHealthPrism.transform.localPosition = Vector3.zero;
            newHealthPrism.transform.localRotation = Quaternion.identity;
            if (growthInfo.IsDangerous)
            {
                newHealthPrism.MakeDangerous();
                // Octagon colony: a danger prism is (or discovers) this plant's ring.
                if (OctagonMode && growthInfo is GyroidGrowthInfo g)
                    RegisterDangerPrism(g.BlockType, growthInfo.Position, growthInfo.Rotation);
            }
            newHealthPrism.Initialize();

            newBranch.gameObject = newSpindle.gameObject;
            newBranch.assembler = newAssembler;
            newBranch.depth = order.parent.depth + 1;

            activeBranches.Add(newBranch);

            // Growth is this plant's feeding - it is what funds an offspring (Flora.NotifyGrew,
            // Docs/ECOSYSTEM.md §32). Counted where the prism actually lands, not where the
            // order was decided, so a dropped or lapsed order funds nothing.
            NotifyGrew();
            _lastGrowthTime = Time.time;
            if (OctagonMode) GyroidColonyDiagnostics.GrownPrisms++;

            // Parent retirement, evaluated after the child exists — same ordering
            // as the old inline loop.
            if (order.parent.depth >= maxDepth - 1 || order.parent.assembler.IsFullyBonded())
                activeBranches.Remove(order.parent);
        }

        // -------------------------------------------------------------------
        //  The octagon colony (gyroid only) - a plant IS an octagon-owner
        //  (Docs/ECOSYSTEM.md §32.2)
        //
        //  The gyroid's four danger block types close into rings of exactly eight
        //  danger prisms (measured off the bond table - GyroidOctagonData). Each
        //  ring is one lifeform: its crystal sits at the ring's centre and never
        //  grows, its territory is the 24-prism patch around that centre, and
        //  reproduction is planting a daughter at a NEIGHBOURING ring's centre
        //  with a real member of that ring as her first prism - so the colony
        //  tiles the surface, one octagon each, and the superstructure is the
        //  gyroid with a heart in every window.
        // -------------------------------------------------------------------

        struct RingMember
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public GyroidBlockType Type;
        }

        // This plant's octagon centre (world). A daughter is handed hers before Initialize;
        // a founder discovers its own from the first danger prism it grows.
        Vector3? _octagonCenter;

        // The danger prisms of MY ring that have grown so far (max 8). Pose snapshots, not
        // object references - a ring prism can be eaten, but the SITE it defined does not move,
        // and reproduction needs the frame, not the mass.
        readonly List<RingMember> _ringMembers = new(8);

        // Daughter handoff, stashed by the octagon placement and consumed by ConfigureOffspring.
        GyroidGrowthInfo _pendingOffspringSeed;
        Vector3 _pendingOffspringCenter;
        bool _hasPendingOffspringClaim;

        // "Fully grown" includes the BLOOM: a prism exists (and is counted) the moment it is
        // decided, but its grow-in animation takes seconds, and a plant must not reproduce
        // while any of its prisms is still mid-bloom - that is exactly the "generations
        // cascading before their prisms are full size" failure of the third Lab playtest.
        // Config: FloraVariantTuning.MaturationSeconds.
        float _maturationSeconds = 4f;
        float _lastGrowthTime;

        // Consecutive grow ticks that decided nothing with nothing pending - the plant's own
        // frontier is exhausted (every site grown, occupied, or declined as foreign). This is
        // the honest "fully grown" signal for a patch whose final size legitimately varies
        // (22-28): a fixed prism-count test would stall a plant whose near ring-arc was grown
        // by its parent under the boundary epsilon, and a ring-complete test the same.
        int _idleGrowTicks;

        float _latticeScaleOverride = -1f;
        float? _latticeScaleCached;

        /// <summary>
        /// How far this element's lattice is stretched relative to the one the octagon tables
        /// were MEASURED on (<see cref="GyroidOctagonData.MeasuredSeparation"/>).
        ///
        /// <para>Every length in <see cref="GyroidOctagonData"/> - the ring radius, the
        /// neighbouring centres and seed positions, the territory radius, the tolerances - is a
        /// world distance measured at separationDistance 3, so an element that widens its
        /// lattice must widen them all by the same ratio or its founder computes a ring centre
        /// that does not exist, its ownership gate refuses its own sites, and its daughters are
        /// planted at a fraction of the right distance. Rotations are scale-free and are used
        /// unchanged.</para>
        ///
        /// <para>1 for every element that keeps the prefab's spacing, so this is inert for the
        /// three that do.</para>
        ///
        /// <para>Read off the AUTHORED scale rather than the assembler, deliberately: the only
        /// GyroidAssembler in reach here is the healthPrism PREFAB's, whose separationDistance
        /// is the unscaled 3 - the scale is applied to each spawned instance. Asking the prefab
        /// would therefore return 1 for a scaled element and silently defeat the whole
        /// correction.</para>
        ///
        /// <para>GYROID ONLY, and fenced to say so: the ratio is against a GyroidOctagonData
        /// constant, so on a Schwarz P plant - whose lattice scale multiplies a tile PERIOD and
        /// is a different quantity entirely - the number would be meaningless. Every reader
        /// today is inside the octagon path; this makes a future one safe rather than subtly
        /// wrong.</para>
        /// </summary>
        float LatticeScale
        {
            get
            {
                if (_latticeScaleCached.HasValue) return _latticeScaleCached.Value;
                if (!OctagonMode) { _latticeScaleCached = 1f; return 1f; }
                float prefabSeparation = healthPrism && healthPrism.TryGetComponent(out GyroidAssembler ga)
                    ? ga.SeparationDistance : GyroidOctagonData.MeasuredSeparation;
                float authored = _latticeScaleOverride > 0f ? _latticeScaleOverride : 1f;
                _latticeScaleCached = authored * prefabSeparation / GyroidOctagonData.MeasuredSeparation;
                return _latticeScaleCached.Value;
            }
        }

        /// <summary>Pushes this element's authored lattice scale onto a freshly created
        /// assembler. Both species read it before their first growth probe.</summary>
        void ApplyLatticeSpacing(Assembler target)
        {
            if (_latticeScaleOverride <= 0f || target == null) return;
            if (target is GyroidAssembler gyroid) gyroid.ApplyLatticeScale(_latticeScaleOverride);
            else if (target is SchwarzPAssembler schwarz) schwarz.ApplyLatticeScale(_latticeScaleOverride);
        }

        bool? _octagonModeCached;

        /// <summary>
        /// True when this species is a gyroid lattice colony: its health prism carries a
        /// <see cref="GyroidAssembler"/>, so the measured octagon tables apply. Everything the
        /// octagon mode does is gated here - a Schwarz-P (or any future assembled species)
        /// keeps the generic frontier behaviour untouched.
        /// </summary>
        bool OctagonMode => _octagonModeCached ??= healthPrism && healthPrism.GetComponent<GyroidAssembler>();

        /// <summary>This plant's octagon centre, once known (founder: after its first danger prism).</summary>
        public Vector3? OctagonCenter => _octagonCenter;

        /// <summary>
        /// Hands a daughter her octagon centre. The claim was already made by the parent (so a
        /// sibling can't race it between placement and spawn) - the daughter adopts it here.
        /// Call before <see cref="Flora.Initialize"/>.
        /// </summary>
        public void AdoptOctagonCenter(Vector3 center)
        {
            _octagonCenter = center;
            GyroidOctagonRegistry.TransferClaim(center, this);
        }

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell);
            // A daughter knows her centre before her crystal exists; the founder's crystal is
            // moved when its centre is discovered (RegisterDangerPrism).
            PlaceCrystalAtCenter();
            PlaceCrystalAtTileCentre();
        }

        void PlaceCrystalAtCenter()
        {
            if (_octagonCenter.HasValue && crystal)
                crystal.transform.position = _octagonCenter.Value;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  The tile colony (Schwarz P) - a plant IS a tile-owner
        //
        //  The same population model as the gyroid's octagon colony (§32.7), applied
        //  to the Schwarz P surface's own non-Euclidean tile (§33): one plant grows
        //  exactly ONE tile of the {6,4} tiling, keeps its crystal on that tile's
        //  flat point, and on completing itself offers its unclaimed face-adjacent
        //  tiles to the population's frontier. Reproduction is a POPULATION event -
        //  one birth per fauna-wave period, popped at random - so the colony wanders
        //  instead of inflating as a ball.
        //
        //  What it does NOT need, because a tile is an exact integer address:
        //  discovery (a prism is stamped with its tile at birth), ring membership, a
        //  coherence tolerance, a poison band, a territory radius, an ownership
        //  epsilon, a nearest-foreign-claim scan, and a transcribed seed rotation.
        //  Every one of those exists in the gyroid path to paper over float drift in
        //  a lattice that has no addressing.
        // ─────────────────────────────────────────────────────────────────────────

        SchwarzPSurfaceFrame _tileFrame;
        Vector3Int _homeTile;
        bool _hasHomeTile;

        SchwarzPGrowthInfo _pendingTileSeed;
        SchwarzPSurfaceFrame _pendingOffspringFrame;
        Vector3Int _pendingOffspringTile;
        bool _hasPendingTileClaim;

        bool _tileFrontierContributed;
        bool? _tileColonyModeCached;

        /// <summary>
        /// True when this species is a Schwarz P tile colony: its health prism carries a
        /// <see cref="SchwarzPAssembler"/>. Deliberately a SECOND gate rather than a widened
        /// <see cref="OctagonMode"/> - the gyroid branches type-test <c>GyroidGrowthInfo</c>
        /// inside, so one shared gate would let a Schwarz plant through into code that
        /// silently no-ops on it.
        /// </summary>
        bool TileColonyMode => _tileColonyModeCached ??= healthPrism && healthPrism.GetComponent<SchwarzPAssembler>();

        /// <summary>
        /// Hands a daughter her lattice and her tile. The claim was already made by the parent
        /// (so a sibling can't race it between placement and spawn) - the daughter adopts it
        /// here. Call before <see cref="Flora.Initialize"/>.
        /// </summary>
        public void AdoptHomeTile(SchwarzPSurfaceFrame frame, Vector3Int tile)
        {
            _tileFrame = frame;
            _homeTile = tile;
            _hasHomeTile = frame != null;
            if (_hasHomeTile) SchwarzPTileRegistry.TransferClaim(frame, tile, this);
        }

        void PlaceCrystalAtTileCentre()
        {
            if (_hasHomeTile && _tileFrame != null && crystal)
                crystal.transform.position = _tileFrame.TileCenterWorld(_homeTile);
        }

        /// <summary>
        /// Founder path: adopt the lattice this plant's own seed prism anchored, and claim its
        /// tile. A daughter already has both (<see cref="AdoptHomeTile"/>) and skips this.
        /// Runs from the grow tick rather than Initialize because a founder's frame is built
        /// lazily by its assembler's first growth probe.
        /// </summary>
        void BindHomeTile()
        {
            if (_hasHomeTile || healthTracker == null) return;

            foreach (var prism in healthTracker.All)
            {
                if (!prism || !prism.TryGetComponent(out SchwarzPAssembler assembler)) continue;
                var frame = assembler.Frame;
                if (frame == null) continue;

                // If the tile is already held, this founder landed on top of another colony's
                // lattice. It keeps growing unclaimed - the spatial index still stops its
                // prisms overlapping anyone - it simply never joins the reproduction pool.
                if (!SchwarzPTileRegistry.TryClaim(frame, assembler.Tile, this)) return;

                frame.Cell ??= cell;
                _tileFrame = frame;
                _homeTile = assembler.Tile;
                _hasHomeTile = true;
                PlaceCrystalAtTileCentre();
                SchwarzPColonyDiagnostics.Founders++;
                CSDebug.Log($"[SchwarzPColony] FOUNDER {name}: tile {_homeTile} " +
                            $"level {frame.Level} ({SchwarzPTileData.SiteCount(frame.Level)} sites) " +
                            $"lattice #{frame.GetHashCode():X}");
                return;
            }
        }

        /// <summary>
        /// A tile plant is COMPLETE when every site of its tile is occupied - an exact test,
        /// because a site belongs to exactly one tile. The pacing conjuncts are the gyroid's
        /// and are kept for the same reasons: a plant with orders still queued has not
        /// finished, and the maturation window lets its prisms finish blooming before it
        /// parents (a plant that reproduces mid-bloom reads as popping in).
        /// </summary>
        bool TileColonyComplete
        {
            get
            {
                if (!_hasHomeTile || _tileFrame == null || healthTracker == null) return false;
                if (pendingSpawns.Count > 0) return false;
                if (_idleGrowTicks < 2) return false;
                if (Time.time - _lastGrowthTime < _maturationSeconds) return false;

                int sites = SchwarzPTileData.SiteCount(_tileFrame.Level);
                for (int s = 0; s < sites; s++)
                    if (!_tileFrame.IsOccupied(new SchwarzPSiteKey(_homeTile, s)))
                        return false;
                return true;
            }
        }

        /// <summary>
        /// The population-level reproduction drive, run from every tile plant's grow tick.
        /// Contribute once on completion, then ride the cell's fauna-wave cadence for one
        /// birth per cycle. Mirrors <see cref="TickOctagonPopulation"/>.
        /// </summary>
        void TickTilePopulation()
        {
            BindHomeTile();

            // No lineage (a toy clone, a microscene release) or no cell = grows, coordinates
            // nothing. Both are required: the frontier book is keyed by (cell, species).
            if (!SourceConfig || !cell) return;

            if (!_tileFrontierContributed && TileColonyComplete)
            {
                ContributeTileFrontier();
                _tileFrontierContributed = true;
            }

            float period = cell.CurrentFaunaSpawnPeriod;
            if (period <= 0f) period = 30f;

            if (!SchwarzPColonyFrontier.TryBeginCycle(cell, SourceConfig, period, PopulationCycleStagger))
                return;

            // Production gates checked BEFORE popping, so a capped or Frenzy-frozen colony
            // skips the cycle without burning frontier entries - nothing is culled, nothing is
            // lost, the sites are simply still there next cycle.
            if (!cell.FloraPlantingEnabled || cell.IsFloraAtCap(SourceConfig)) return;

            while (SchwarzPColonyFrontier.TryPopRandom(cell, SourceConfig, out var entry))
            {
                if (SchwarzPTileRegistry.IsClaimed(entry.Frame, entry.Tile)) continue;

                // The contributor is the lineage donor. If the food web took it since it
                // offered this tile, the tile is still real lattice - the ticking plant stands
                // in as donor.
                var donor = entry.Contributor ? entry.Contributor : this;
                if (donor.TrySpawnTileDaughter(entry.Frame, entry.Tile)) break;
            }
        }

        /// <summary>
        /// Offers this plant's six unclaimed face-adjacent tiles to the population's frontier.
        ///
        /// <para>Six, not twelve. The bond graph also reaches six EDGE-diagonal tiles (a tile's
        /// hexagon meets those only at a 4-fold corner, by a single bond), and iterating bonds
        /// blindly would offer them too. The {6,4} tiling's adjacency is the six shared EDGES -
        /// the six faces of the half-period cube - so the colony grows through faces and the
        /// surface it builds stays the simple-cubic tiling the tile is defined by.</para>
        /// </summary>
        void ContributeTileFrontier()
        {
            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var delta = axis == 0 ? new Vector3Int(sign, 0, 0)
                              : axis == 1 ? new Vector3Int(0, sign, 0)
                                          : new Vector3Int(0, 0, sign);
                    var neighbour = SchwarzPTileData.NeighbourTile(_homeTile, delta);
                    if (SchwarzPTileRegistry.IsClaimed(_tileFrame, neighbour)) continue;
                    SchwarzPColonyFrontier.Contribute(cell, SourceConfig, _tileFrame, neighbour, this);
                }
            }

            CSDebug.Log($"[SchwarzPColony] COMPLETE {name}: tile {_homeTile} " +
                        $"prisms={healthTracker.Count} - frontier now " +
                        $"{SchwarzPColonyFrontier.Count(cell, SourceConfig)} open tiles");
        }

        /// <summary>
        /// Executes the population's chosen birth on THIS plant as lineage donor: claims the
        /// tile, reserves the daughter's seed site, stages the handoff and spawns her through
        /// the one canonical spawn path. All-or-nothing - any failure releases everything it
        /// took, because an orphan claim or reservation punches a permanent hole.
        /// </summary>
        public bool TrySpawnTileDaughter(SchwarzPSurfaceFrame frame, Vector3Int tile)
        {
            if (frame == null) return false;

            // Claim the tile FIRST - released on any later failure. The seed reservation must
            // come second: a reservation abandoned on a lost claim would sit until its TTL,
            // permanently blocking every frontier that probes the site meanwhile.
            if (!SchwarzPTileRegistry.TryClaim(frame, tile, this)) return false;

            var seed = SchwarzPAssembler.BuildSeed(frame, tile, depth);
            if (seed == null)
            {
                SchwarzPTileRegistry.Release(frame, tile, this);
                return false;
            }

            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            bool reserved = false;
            if (spatialIndex != null && spatialIndex.IsAvailable)
            {
                // The seed site must be free mass-wise. TryReserve doubles as the check and
                // the claim - the daughter's first prism consumes it on register. The radius
                // is the lattice's own, so it scales with periodScale instead of a magic
                // number: below half the spacing, so a legitimate neighbouring site is never
                // blocked, above any drift so a duplicate always is.
                if (!spatialIndex.TryReserve(seed.Position, frame.WorldSpacing * 0.45f))
                {
                    SchwarzPTileRegistry.Release(frame, tile, this);
                    return false;
                }
                reserved = true;
            }

            _pendingOffspringFrame = frame;
            _pendingOffspringTile = tile;
            _hasPendingTileClaim = true;
            _pendingTileSeed = seed;

            bool born = TrySpawnOneOffspring();
            if (born)
            {
                SchwarzPColonyDiagnostics.Births++;
                CSDebug.Log($"[SchwarzPColony] BIRTH #{SchwarzPColonyDiagnostics.Births} donor {name}: " +
                            $"daughter on tile {tile} (frontier " +
                            $"{SchwarzPColonyFrontier.Count(cell, SourceConfig)} open)");
            }
            else
            {
                // SpawnFlora refused (cap raced shut, planting froze, prefab torn down) -
                // release everything this attempt took so nothing strands.
                SchwarzPTileRegistry.Release(frame, tile, this);
                if (reserved) spatialIndex.ReleaseReservation(seed.Position);
                _pendingTileSeed = null;
                _hasPendingTileClaim = false;
                _pendingOffspringFrame = null;
            }
            return born;
        }

        /// <summary>
        /// The tile colony's placement is STAGED, never searched: the population pops one
        /// random open tile per cycle and <see cref="TrySpawnTileDaughter"/> stages its seed
        /// pose before spawning. This just reports it, which is what keeps the per-plant quota
        /// machinery (<c>Flora.TryReproduce</c>) harmlessly inert for colony species.
        /// </summary>
        bool TryResolveTileOffspring(out Vector3 position, out Quaternion rotation, out Vector3? up)
        {
            position = default;
            rotation = default;
            up = null;
            if (_pendingTileSeed == null) return false;

            position = _pendingTileSeed.Position;
            rotation = _pendingTileSeed.Rotation;
            return true;
        }

        /// <summary>
        /// Every danger prism this plant grows passes through here. A founder discovers (and
        /// claims) its octagon centre from the first one; after that, prisms of MY ring are
        /// recorded as the frames reproduction will project the neighbour tables from. A danger
        /// prism belonging to a DIFFERENT octagon (a boundary prism this plant happened to win)
        /// is ordinary mass - it defines no frame here.
        /// </summary>
        void RegisterDangerPrism(GyroidBlockType type, Vector3 position, Quaternion rotation)
        {
            if (!GyroidOctagonData.TryGetOwnCenterOffset(type, out var localOffset)) return;
            localOffset *= LatticeScale;

            Vector3 center = position + rotation * localOffset;

            if (!_octagonCenter.HasValue)
            {
                // Founder discovery. If the claim fails, this prism is part of an octagon some
                // other plant already owns - keep growing; a later danger prism may land on an
                // unclaimed ring.
                if (!GyroidOctagonRegistry.TryClaim(center, this)) return;
                _octagonCenter = center;
                PlaceCrystalAtCenter();
                _ringMembers.Add(new RingMember { Position = position, Rotation = rotation, Type = type });
                // A discovered (not inherited) centre marks a FOUNDER - it takes on the colony
                // heartbeat. Invoke (not a coroutine) so Die's StopAllCoroutines can't silence
                // the report while the husk lingers. lineage=False names a TOY planting (the
                // Lifeform Matrix stations spawn AssembledFlora clones with no species config)
                // - every founder is an independent lattice FRAME, and frames that were never
                // projected from one another cannot mate, so knowing where each frame came
                // from is the first question of any "colonies don't match up" report.
                CSDebug.Log($"[GyroidColony] FOUNDER {name} claimed centre {center} " +
                            $"(prism {type} at {position}, lineage={(SourceConfig ? SourceConfig.name : "NONE/toy")})");
                InvokeRepeating(nameof(LogColonyHeartbeat), 5f, 5f);
                return;
            }

            float err = (center - _octagonCenter.Value).magnitude;
            if (err < GyroidOctagonData.RingMemberToleranceRadius * LatticeScale && _ringMembers.Count < 8)
            {
                _ringMembers.Add(new RingMember { Position = position, Rotation = rotation, Type = type });
                // Coherence telemetry: a genuine ring member computes the claimed centre to
                // well under 1u (measured founder coherence 0.08). A growing max here means
                // frames are drifting apart - the precursor of visible twinning.
                if (err > GyroidColonyDiagnostics.MaxRingCoherenceError)
                    GyroidColonyDiagnostics.MaxRingCoherenceError = err;
            }
            else if (err < GyroidOctagonData.CenterDedupeRadius * LatticeScale)
            {
                // The poison band: near enough to be "this octagon" by claim-identity
                // standards, far too incoherent to be a real member (the fifth Lab playtest
                // admitted one at 11.79u and reproduction then projected the neighbour tables
                // from a foreign frame - a chimera lineage). Membership is a COHERENCE test;
                // this prism is a misaligned lattice's mass and defines no frame here.
                GyroidColonyDiagnostics.RingPoisonRejected++;
            }
        }

        /// <summary>
        /// True when this octagon plant has FULLY GROWN - it holds a real patch AND its frontier
        /// is exhausted (a run of grow ticks decided nothing, with no orders pending). Only a
        /// mature plant may reproduce: without this gate a colonising species runs generations
        /// ahead of its own growth - each half-grown plant minting 8 crystal-bearing daughters
        /// per birth - and the cell fills with an exponential cloud of hearts and seed spindles
        /// long before the surface behind them exists (the first Gyroid Lab playtest).
        ///
        /// Deliberately NOT a fixed prism count or a ring-complete test: patch sizes
        /// legitimately vary (22-28), and a plant whose near ring-arc was grown by its parent
        /// under the boundary epsilon would never satisfy either - a permanent stall. Frontier
        /// exhaustion is the one signal every complete plant reaches.
        /// </summary>
        bool OctagonMature =>
            healthTracker != null &&
            healthTracker.Count >= GyroidOctagonData.PatchPrisms - 6 &&
            pendingSpawns.Count == 0 &&
            _idleGrowTicks >= 2 &&
            Time.time - _lastGrowthTime >= _maturationSeconds;

        void LogColonyHeartbeat()
        {
            CSDebug.Log($"[GyroidColony] t={Time.time:F0}s {GyroidColonyDiagnostics.Summary} | " +
                        $"founder: prisms={(healthTracker != null ? healthTracker.Count : -1)} " +
                        $"ring={_ringMembers.Count} idle={_idleGrowTicks} " +
                        $"branches={activeBranches.Count} pending={pendingSpawns.Count} mature={OctagonMature}");
        }

        /// <summary>
        /// The ownership gate - what makes many small plants TILE the surface instead of racing
        /// each other over it. A site is this plant's to grow iff it lies within its own
        /// territory radius AND no other claimed octagon centre is meaningfully nearer. The
        /// epsilon deliberately lets both owners contest an exactly-equidistant boundary site -
        /// the spatial-index reservation then keeps whichever grows first, which is how the
        /// measured 22-28 patch spread arises (GyroidOctagonData.OwnershipEpsilon).
        /// </summary>
        bool OwnsLatticeSite(Vector3 site)
        {
            if (!_octagonCenter.HasValue) return true;   // founder bootstrap: no territory yet

            float dMine = Vector3.Distance(site, _octagonCenter.Value);
            if (dMine > GyroidOctagonData.TerritoryRadius * LatticeScale) return false;

            float foreignSqr = GyroidOctagonRegistry.NearestForeignClaimSqr(site, this);
            if (foreignSqr >= float.MaxValue) return true;
            return dMine <= Mathf.Sqrt(foreignSqr) + GyroidOctagonData.OwnershipEpsilon * LatticeScale;
        }

        // -------------------------------------------------------------------
        //  Reproduction - the lattice frontier IS the reproduction frontier
        //  (Docs/ECOSYSTEM.md §32.2)
        // -------------------------------------------------------------------

        // The growth order this plant donated to its next offspring: decided by the parent's own
        // assembler, so the daughter's first prism lands on the exact bond site the parent would
        // have grown into next, wearing the block type that site calls for. That - and nothing
        // else - is what makes a population of small plants add up to ONE continuous gyroid.
        GrowthInfo _donatedGrowth;

        // The growth order THIS plant was seeded from, consumed by CreateNewAssembler.
        GrowthInfo _seedGrowth;

        /// <summary>
        /// Seeds this plant's first prism from a parent's growth order - its position and
        /// rotation are already the flora's (pinned by <see cref="Flora.Plant"/> and the spawn
        /// rotation); this carries the lattice STATE that a bare position cannot: which block
        /// type the site calls for and whether it is one of the danger types. Call before
        /// <see cref="Initialize"/>.
        /// </summary>
        public void SeedFromGrowth(GrowthInfo growth) => _seedGrowth = growth;

        /// <summary>
        /// Hands the daughter a real bond site off this plant's own frontier.
        ///
        /// <para>A gyroid does not want its children scattered around it - it wants them exactly
        /// where its own next prism would have gone, or the superstructure stops being a gyroid.
        /// So an offspring is not placed near the parent, it is placed AT a growth order the
        /// parent asks its own assembler for: the same call, against the same bond table, with
        /// the same <c>PrismSpatialIndex.TryReserve</c> claim that stops two growers filling one
        /// site. The parent simply hands the result to a new plant instead of growing it
        /// itself.</para>
        ///
        /// <para>Returns false when the frontier is closed (every branch fully bonded or
        /// depth-exhausted). The birth is then skipped rather than misplaced - the base keeps
        /// the plant ARMED, so it will try again the moment anything changes.</para>
        /// </summary>
        protected override bool TryResolveOffspringPlacement(
            out Vector3 position, out Quaternion rotation, out Vector3? up)
        {
            if (OctagonMode)
                return TryResolveOctagonOffspring(out position, out rotation, out up);
            if (TileColonyMode)
                return TryResolveTileOffspring(out position, out rotation, out up);

            position = default;
            rotation = default;
            up = null;
            // A donated order stranded by a failed spawn (ConfigureOffspring never consumed
            // it) leaves its reservation live - release it, or the donor's own re-decision of
            // that site fails against its own claim and permanently bonds the site.
            if (_donatedGrowth != null)
                PrismSpatialIndex.Instance?.ReleaseReservation(_donatedGrowth.Position);
            _donatedGrowth = null;

            if (activeBranches.Count == 0) return false;

            GrowthInfo growth = null;
            foreach (var branch in activeBranches)
            {
                if (!branch.assembler || branch.depth >= maxDepth) continue;

                var info = branch.assembler.GetGrowthInfo();
                if (!info.CanGrow) continue;

                growth = info;
                break;
            }

            if (growth == null) return false;

            // The donated site is spoken for. The donor branch keeps its own bookkeeping: the
            // site is not marked bonded here, so if this plant ever grows again it will simply
            // re-decide that site, fail the reservation against the daughter's prism, and mark
            // it bonded then - the same self-correction any contested site already gets.
            _donatedGrowth = growth;
            position = growth.Position;
            rotation = growth.Rotation;

            // Deliberately NOT clamped into the planting band: a lattice site is the whole
            // point, and a daughter nudged off it would break the surface. The colony is still
            // bounded - a plant outside the membrane cannot grow (Cell.ContainsPosition rejects
            // its prisms) and the species' population cap bounds the spread.
            return true;
        }

        /// <summary>
        /// The octagon colony's placement is STAGED, never searched: reproduction is a
        /// POPULATION event (see <see cref="TickOctagonPopulation"/>) - the colony pops one
        /// random frontier octagon per cycle and <see cref="TrySpawnFrontierDaughter"/> stages
        /// the validated claim + seed on the lineage donor before spawning through the one
        /// canonical path. This override just hands the staged target back to that path. A
        /// plant with nothing staged has nothing to place, which is also what makes the
        /// per-plant quota machinery (<c>Flora.TryReproduce</c>) harmlessly inert for octagon
        /// colonies: its quota arms and stays armed, but placement always declines it.
        /// </summary>
        bool TryResolveOctagonOffspring(out Vector3 position, out Quaternion rotation, out Vector3? up)
        {
            position = default;
            rotation = default;
            up = null;

            if (!_hasPendingOffspringClaim || _pendingOffspringSeed == null) return false;

            position = _pendingOffspringCenter;
            rotation = _pendingOffspringSeed.Rotation;
            return true;
        }

        // "A few frames" behind the fauna wave boundary, so the colony's birth never lands on
        // the same frame as a wave's instantiation burst.
        const float PopulationCycleStagger = 0.35f;

        // One-shot: a plant contributes its frontier to the population book exactly once, at
        // the moment it first reads as fully grown.
        bool _frontierContributed;

        /// <summary>
        /// The population-level reproduction drive (Docs/ECOSYSTEM.md §32.7, organic-growth
        /// pass), run from every octagon plant's grow tick. Two duties:
        ///
        /// <para><b>Contribute:</b> the first tick on which this plant reads as FULLY GROWN
        /// (<see cref="OctagonMature"/>), it offers every unclaimed neighbouring ring centre -
        /// with the full seed pose projected from its own measured ring - to
        /// <see cref="GyroidColonyFrontier"/>. Completing growth is what earns a place in the
        /// reproduction pool, so only complete lifeforms ever parent.</para>
        ///
        /// <para><b>Cycle:</b> the whole population births ONE new lifeform per cycle, and the
        /// cycle rides the cell's fauna-wave cadence (<see cref="Cell.CurrentFaunaSpawnPeriod"/>,
        /// staggered a few frames so births never share a frame with a wave burst). The chosen
        /// site is a UNIFORMLY RANDOM pop across every complete plant's frontier - which is
        /// what de-spheres the colony: growth wanders organically the way the old single-plant
        /// gyroid wandered prism by prism, now at the level of whole flora. Any plant's tick
        /// may cross the clock boundary; the first one owns the cycle (main-thread, one at a
        /// time - no race by construction).</para>
        /// </summary>
        void TickOctagonPopulation()
        {
            // No lineage (a toy clone, a microscene release) or no cell = grows, coordinates
            // nothing. Both are required: the frontier book is keyed by (cell, species).
            if (!SourceConfig || !cell) return;

            if (!_frontierContributed && OctagonMature && _ringMembers.Count > 0)
            {
                ContributeFrontier();
                _frontierContributed = true;
            }

            // The cell's fauna wave period is the ecosystem heartbeat; a cell with no spawn
            // profile falls back to the platform default rather than never reproducing.
            float period = cell.CurrentFaunaSpawnPeriod;
            if (period <= 0f) period = 30f;

            if (!GyroidColonyFrontier.TryBeginCycle(cell, SourceConfig, period, PopulationCycleStagger))
                return;

            // Production gates checked BEFORE popping, so a capped or Frenzy-frozen colony
            // skips the cycle without burning frontier entries (nothing is culled, nothing is
            // lost - the sites are simply still there next cycle).
            if (cell && (!cell.FloraPlantingEnabled || cell.IsFloraAtCap(SourceConfig))) return;

            // One BIRTH per cycle; stale entries (claimed since offered, misaligned-frame
            // conflicts) are discarded along the way - each is a point lookup against the
            // claim book, not a mass sweep.
            while (GyroidColonyFrontier.TryPopRandom(cell, SourceConfig, out var entry))
            {
                if (GyroidOctagonRegistry.IsClaimed(entry.Center)) continue;

                // The contributor is the lineage donor. If the food web took it since it
                // offered this site, the site is still real lattice - the ticking plant
                // stands in as donor ("the chosen one can seed off ANY complete plant").
                var donor = entry.Contributor ? entry.Contributor : this;
                if (donor.TrySpawnFrontierDaughter(entry.Center, entry.Seed)) break;
            }
        }

        /// <summary>
        /// Projects the measured neighbour table from every ring prism this plant grew and
        /// offers each unclaimed neighbouring octagon - centre plus the full seed pose of one
        /// real member of that ring - to the population's frontier book. "Calculate where the
        /// neighbouring crystals belong, check whether one is already there, and remember
        /// where there is not."
        /// </summary>
        void ContributeFrontier()
        {
            for (int m = 0; m < _ringMembers.Count; m++)
            {
                var member = _ringMembers[m];
                if (!GyroidOctagonData.TryGetNeighbors(member.Type, out var neighbors)) continue;

                for (int n = 0; n < neighbors.Length; n++)
                {
                    Vector3 center = member.Position + member.Rotation * (neighbors[n].Center * LatticeScale);
                    if (GyroidOctagonRegistry.IsClaimed(center)) continue;

                    GyroidColonyFrontier.Contribute(cell, SourceConfig, center, new GyroidGrowthInfo
                    {
                        CanGrow = true,
                        Position = member.Position + member.Rotation * (neighbors[n].SeedPosition * LatticeScale),
                        Rotation = member.Rotation * neighbors[n].SeedRotation,
                        BlockType = neighbors[n].SeedType,
                        IsDangerous = true,
                        Depth = depth,
                    }, this);
                }
            }

            CSDebug.Log($"[GyroidColony] MATURE {name}: prisms={healthTracker.Count} " +
                        $"ring={_ringMembers.Count} - frontier now {GyroidColonyFrontier.Count(cell, SourceConfig)} open sites");
        }

        /// <summary>
        /// Executes the population's chosen birth on THIS plant as lineage donor: claims the
        /// octagon centre, reserves + misalignment-gates the seed site, stages the handoff and
        /// spawns the daughter through the one canonical spawn path. All-or-nothing - any
        /// failure releases everything it took (an orphan claim or reservation punches
        /// permanent holes, see the decline path in Grow). Returns false so the cycle may try
        /// another candidate.
        /// </summary>
        public bool TrySpawnFrontierDaughter(Vector3 center, GyroidGrowthInfo seed)
        {
            if (seed == null) return false;

            // Claim the centre FIRST - released on any later failure. The seed reservation
            // must come second: a reservation abandoned on a lost claim would sit until its
            // TTL, permanently bonding every frontier that probes the site meanwhile.
            if (!GyroidOctagonRegistry.TryClaim(center, this)) return false;

            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            if (spatialIndex != null && spatialIndex.IsAvailable)
            {
                // The seed site must be free mass-wise (a boundary prism can sit exactly
                // there). TryReserve doubles as the check and the claim - the daughter's
                // first prism consumes it on register.
                if (!spatialIndex.TryReserve(seed.Position, 3f))
                {
                    GyroidOctagonRegistry.Release(center, this);
                    return false;
                }

                // Lattice-misalignment GATE, seed flavour: standing mass 3.1-5.5u from the
                // seed site is a MISALIGNED frame's territory (coherent minimum is 6.6u) - a
                // daughter seeded onto it would twin her whole subtree.
                if (spatialIndex.IsAnyPrismWithin(seed.Position, 5.5f))
                {
                    GyroidColonyDiagnostics.SeedSiteDefects++;
                    GyroidColonyDiagnostics.ReportDefect("seed-blocked", seed.Position, name);
                    GyroidOctagonRegistry.Release(center, this);
                    spatialIndex.ReleaseReservation(seed.Position);
                    return false;
                }
            }

            _pendingOffspringCenter = center;
            _hasPendingOffspringClaim = true;
            _pendingOffspringSeed = new GyroidGrowthInfo
            {
                CanGrow = true,
                Position = seed.Position,
                Rotation = seed.Rotation,
                BlockType = seed.BlockType,
                IsDangerous = true,
                Depth = depth,
            };

            bool born = TrySpawnOneOffspring();
            if (born)
            {
                GyroidColonyDiagnostics.Births++;
                CSDebug.Log($"[GyroidColony] BIRTH #{GyroidColonyDiagnostics.Births} donor {name}: " +
                            $"daughter at {center} seed {seed.BlockType} " +
                            $"(frontier {GyroidColonyFrontier.Count(cell, SourceConfig)} open)");
            }
            else
            {
                // SpawnFlora refused (cap raced shut, planting froze, prefab torn down) -
                // release everything this attempt took so nothing strands.
                GyroidOctagonRegistry.Release(center, this);
                if (spatialIndex != null && spatialIndex.IsAvailable)
                    spatialIndex.ReleaseReservation(seed.Position);
                _pendingOffspringSeed = null;
                _hasPendingOffspringClaim = false;
            }
            return born;
        }

        /// <summary>
        /// Programs the daughter with the donated growth order, in the window between its
        /// variant/level being applied and <c>Initialize</c> - the only point at which a plant's
        /// growth rule can still be seeded. The octagon path also hands over the claimed centre.
        /// </summary>
        protected override void ConfigureOffspring(Flora child)
        {
            if (child is AssembledFlora assembled)
            {
                if (_pendingOffspringSeed != null)
                {
                    assembled.SeedFromGrowth(_pendingOffspringSeed);
                    if (_hasPendingOffspringClaim)
                    {
                        assembled.AdoptOctagonCenter(_pendingOffspringCenter);
                        _hasPendingOffspringClaim = false;
                    }
                }
                else if (_pendingTileSeed != null)
                {
                    // The tile colony's handoff: her lattice, her tile, her first prism.
                    // AdoptHomeTile must precede Initialize - Initialize is what puts her
                    // crystal on the tile's flat point.
                    assembled.SeedFromGrowth(_pendingTileSeed);
                    if (_hasPendingTileClaim)
                    {
                        assembled.AdoptHomeTile(_pendingOffspringFrame, _pendingOffspringTile);
                        _hasPendingTileClaim = false;
                        _pendingOffspringFrame = null;
                    }
                }
                else if (_donatedGrowth != null)
                {
                    assembled.SeedFromGrowth(_donatedGrowth);
                }
            }
            _pendingOffspringSeed = null;
            _pendingTileSeed = null;
            _donatedGrowth = null;
        }

        protected override void OnDestroy()
        {
            // The colony's claim on its octagon dies with it, so a grazed-out window becomes
            // plantable again and neighbours recolonise the hole. A pending daughter claim that
            // never transferred is released too.
            if (_octagonCenter.HasValue)
                GyroidOctagonRegistry.Release(_octagonCenter.Value, this);
            if (_hasHomeTile)
                SchwarzPTileRegistry.Release(_tileFrame, _homeTile, this);
            if (_hasPendingTileClaim)
            {
                SchwarzPTileRegistry.Release(_pendingOffspringFrame, _pendingOffspringTile, this);
                if (_pendingTileSeed != null)
                    PrismSpatialIndex.Instance?.ReleaseReservation(_pendingTileSeed.Position);
            }
            if (_hasPendingOffspringClaim)
            {
                GyroidOctagonRegistry.Release(_pendingOffspringCenter, this);
                if (_pendingOffspringSeed != null)
                    PrismSpatialIndex.Instance?.ReleaseReservation(_pendingOffspringSeed.Position);
            }
            base.OnDestroy();
        }

        /// <summary>
        /// Re-sprout growth branches from surviving prisms - each surviving prism still
        /// carries its Assembler, so wrapping it in a Branch puts it back in the grow
        /// rotation. Survivors are sampled RANDOMLY (not first-N) so repeated reseeds
        /// don't keep picking the same fully-bonded prisms; ones next to consumed gaps
        /// have room to grow and heal the wound. Branches that turn out to be fully
        /// bonded are culled by the normal Grow() loop. Falls back to a fresh root
        /// assembler when nothing usable survives.
        /// </summary>
        void ReseedBranches()
        {
            var survivors = new List<HealthPrism>();
            foreach (var hp in healthTracker.All)
                if (hp) survivors.Add(hp);

            if (survivors.Count == 0)
            {
                // An octagon plant must NOT mint a fresh root here: its root sits at the ring
                // CENTRE - the middle of the window, not a lattice site - so the minted prism
                // would be off-lattice, its growth sites garbage, and with the plant unable to
                // die (nothing removed a prism through the tracked path) this repeats every
                // grow tick: a spindle-minting loop. A prismless octagon plant just waits;
                // grazing that empties it kills it through the normal lethality path instead.
                if (TileColonyMode)
                {
                    // Same reason as the octagon plant below: a tile plant's root sits at
                    // its tile's flat point - the crystal's seat, not a prism site - so a
                    // minted prism would be off-lattice with garbage growth sites, and the
                    // plant cannot die (nothing left through the tracked path), so it would
                    // repeat every grow tick.
                    SchwarzPColonyDiagnostics.ReseedMintsBlocked++;
                    CSDebug.LogWarning($"[SchwarzPColony] {name}: ZERO surviving prisms with a " +
                                       $"live tracker - off-lattice reseed mint BLOCKED");
                    return;
                }
                if (OctagonMode)
                {
                    GyroidColonyDiagnostics.ReseedMintsBlocked++;
                    CSDebug.LogWarning($"[GyroidColony] {name}: ZERO surviving prisms with a live " +
                                       $"tracker (count={(healthTracker != null ? healthTracker.Count : -1)}) - " +
                                       $"off-lattice reseed mint BLOCKED");
                    return;
                }
                CreateNewAssembler();
                return;
            }

            // Partial Fisher-Yates: try a bounded number of random survivors, keep the
            // ones that still carry an Assembler. Bounded so the cold reseed path never
            // does an unbounded GetComponent sweep over a 1000-prism flora.
            int target = Mathf.Max(1, reseedBranchCount);
            int attempts = Mathf.Min(survivors.Count, target * 3);
            int seeded = 0;
            for (int i = 0; i < attempts && seeded < target; i++)
            {
                int pick = Random.Range(i, survivors.Count);
                (survivors[i], survivors[pick]) = (survivors[pick], survivors[i]);
                if (!survivors[i].TryGetComponent<Assembler>(out _)) continue;
                activeBranches.Add(new Branch(survivors[i]));
                seeded++;
            }

            if (seeded == 0)
                CreateNewAssembler();
        }

        void GrowCrystal()
        {
            // The octagon colony's crystal is FIXED: it marks the ring's centre and never
            // grows (the gyroid prefab also authors crystalGrowth 0 - belt and braces, since
            // a growing crystal would drift out of scale with the window it sits in).
            if (crystalGrowth <= 0f || OctagonMode || TileColonyMode) return;

            if (crystal)
            {
                crystal.GrowCrystal(1, crystal.transform.localScale.x + crystalGrowth);
            }
        }

        public override void RemoveSpindle(Spindle spindle)
        {
            base.RemoveSpindle(spindle);
            Branch found = default;
            bool hasMatch = false;
            foreach (var item in activeBranches)
            {
                if (item.gameObject != spindle.gameObject) continue;
                found = item;
                hasMatch = true;
                break;
            }
            if (hasMatch) activeBranches.Remove(found);
        }

        public override void Plant()
        {
            assembler = CreateNewAssembler();
            // Disperse across the cell (fraction of membrane radius - see Flora base)
            // instead of the old hard-coded 200m huddle around the crystal. Dispersed,
            // domain-coherent flora clusters are what give fauna schools of different
            // domains genuinely different anti-domain density targets. A pinned position
            // (the Lifeform Matrix toy's spawn-here stations) wins over dispersal.
            if (TryGetPlantPositionOverride(out var pinned))
            {
                transform.position = pinned;
            }
            else
            {
                // Shell measured from the CELL CENTRE (Flora.ResolvePlantCenter), not the
                // crystal - see BranchingFlora.Plant.
                float radius = ResolvePlantRadius(legacyRadius: 200f);
                transform.position = ResolvePlantCenter() + radius * Random.onUnitSphere;
            }

            // The move above dragged the seed prism (a child) with it, but the prism REGISTERED
            // with the spatial index at its pre-move position inside CreateNewAssembler -
            // occupancy and AOE read the STORED position, not the transform, so without this the
            // seed's real location reads as empty space that another grower may fill (and its
            // registered location blocks a site nothing occupies). Same contract as
            // GyroidAssembler.MoveMateToSite. Pre-dates this branch for every dispersed
            // assembled flora; it matters now because plants finally share boundaries.
            if (assembler != null && assembler.Prism)
                assembler.Prism.NotifyPositionChanged();
        }

        public Assembler CreateNewAssembler()
        {
            CSDebug.Log("New Assembler");
            var newSpindle = AddSpindle();

            // A SEEDED plant's first prism lands at the exact pose its parent's lattice
            // dictates - which for the octagon colony is a member of the daughter's own ring,
            // 10-20u from her root (the root, and the crystal with it, sit at the octagon
            // CENTRE). The spindle is posed first because SetParent(worldPositionStays: false)
            // snaps the prism to the spindle's pose. An unseeded founder keeps the legacy
            // shape: first prism at the flora root.
            if (_seedGrowth != null)
            {
                newSpindle.transform.position = _seedGrowth.Position;
                newSpindle.transform.rotation = _seedGrowth.Rotation;
            }

            HealthPrism newHealthPrism = Instantiate(healthPrism, newSpindle.transform.position, newSpindle.transform.rotation);
            AddHealthBlock(newHealthPrism);
            // Zero the locals AFTER SetParent - worldPositionStays:false KEEPS the local
            // values, which at this point are the world coordinates Instantiate assigned, so
            // without the reset the prism lands at spindle.pos + spindle.rot * worldPos. The
            // legacy code only worked because a spawner flora ran this while still parked at
            // the cell centre (world ~zero, stale local ~zero); any plant created at a real
            // position - an octagon daughter at her centre, a Lifeform Matrix station - had
            // its seed prism thrown ~2x its own distance from the origin, where the octagon
            // ownership gate then declined every site and the plant never grew.
            // (ExecuteGrowOrder always did this correctly; this path just never copied it.)
            newHealthPrism.transform.SetParent(newSpindle.transform, false);
            newHealthPrism.transform.localPosition = Vector3.zero;
            newHealthPrism.transform.localRotation = Quaternion.identity;
            newHealthPrism.LifeForm = this;

            // Seeded from a parent's donated bond site: the site's own block type decides what
            // this prism IS, and whether it is one of the danger types - read off the SAME
            // GrowthInfo the parent's assembler produced, so the daughter's first prism is
            // indistinguishable from the one the parent would have grown there. Applied before
            // Initialize, exactly as ExecuteGrowOrder does for an ordinary child.
            if (_seedGrowth != null)
            {
                var seeded = AssemblerFactory.ProgramAssembler(newHealthPrism.gameObject, _seedGrowth);
                ApplyLatticeSpacing(seeded);
                if (seeded != null && _seedGrowth.IsDangerous)
                {
                    newHealthPrism.MakeDangerous();
                    // The octagon daughter's seed IS her first ring member.
                    if (OctagonMode && _seedGrowth is GyroidGrowthInfo sg)
                    {
                        RegisterDangerPrism(sg.BlockType, _seedGrowth.Position, _seedGrowth.Rotation);
                        // Handoff coherence assert: the seed pose must recompute the centre
                        // this daughter ADOPTED, because they were projected from the same
                        // parent frame through the same measured tables. A miss here is not a
                        // runtime accident - it means the tables (or their composition) are
                        // wrong IN-ENGINE, the exact defect class that shipped as the z-mirror
                        // corruption of GyroidOctagonData (12 of 16 seed rotations wrong by up
                        // to 179 deg; every daughter seeded through them twinned her subtree).
                        // Loud and first-birth-fast, so a future table regression cannot cost
                        // another five playtests.
                        if (_octagonCenter.HasValue &&
                            GyroidOctagonData.TryGetOwnCenterOffset(sg.BlockType, out var ownOff))
                        {
                            float handoffErr = Vector3.Distance(
                                _seedGrowth.Position + _seedGrowth.Rotation * (ownOff * LatticeScale),
                                _octagonCenter.Value);
                            if (handoffErr > GyroidColonyDiagnostics.MaxSeedHandoffError)
                                GyroidColonyDiagnostics.MaxSeedHandoffError = handoffErr;
                            if (handoffErr > 1f)
                            {
                                GyroidColonyDiagnostics.IncoherentHandoffs++;
                                CSDebug.LogError($"[GyroidColony] INCOHERENT SEED HANDOFF: {name} " +
                                    $"seed {sg.BlockType} recomputes its centre {handoffErr:F2}u away " +
                                    $"from the adopted centre - GyroidOctagonData's tables are wrong " +
                                    $"in-engine (regenerate: Tools/Build/measure_gyroid_octagons.py)");
                            }
                        }
                        GyroidColonyDiagnostics.SeededDaughterPrisms++;
                        _lastGrowthTime = Time.time;
                    }
                }
            }

            newHealthPrism.Initialize();

            Assembler newAssembler = newHealthPrism.GetComponent<Assembler>();
            ApplyLatticeSpacing(newAssembler);   // founder: before its first growth probe
            newAssembler.Prism = newHealthPrism;
            newAssembler.Spindle = newSpindle;

            // A fresh depth budget per plant. Depth used to be the lattice's global size bound
            // (one flora grew the whole gyroid), but a plant is now ONE UNIT CELL bounded by its
            // own prism budget - so inheriting the parent's remaining depth would make each
            // generation smaller until the colony stalled. The population cap is what bounds the
            // colony now (Docs/ECOSYSTEM.md §32.2).
            newAssembler.Depth = depth;
            _seedGrowth = null;

            Branch newBranch = new Branch(newHealthPrism);
            newBranch.gameObject = newSpindle.gameObject;
            newBranch.assembler = newAssembler;
            newBranch.depth = 0;

            activeBranches.Add(newBranch);

            return newAssembler;
        }

        /// <summary>
        /// Pure preview of the LATTICE this flora assembles - see <see cref="Flora.TryPreviewGrowth"/>.
        /// The growth rule here belongs to the <see cref="Assembler"/> on the health prism, so this
        /// walks that assembler's own bonding geometry and spawns nothing:
        ///
        /// • <see cref="GyroidAssembler"/> - the real bond-mate table
        ///   (<see cref="GyroidBondMateDataContainer"/>): each site's delta position/up/forward,
        ///   composed exactly as <c>CalculateGlobalBondSite</c> + <c>CalculateRotation</c> do, so the
        ///   preview IS a patch of the gyroid the species actually grows.
        /// • <see cref="WallAssembler"/> - its four in-plane bond offsets
        ///   (±up, ±right by half-extent + separation).
        /// • <see cref="SchwarzPAssembler"/> - its own <c>TryPreviewLattice</c>: the same
        ///   subdivision level, the same measured tile sites and the same integer tile
        ///   arithmetic (<see cref="SchwarzPTileData"/>) the live growth uses, with the
        ///   occupancy claims replaced by a local visited set.
        /// </summary>
        public override bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into)
        {
            if (into == null || budget <= 0 || !healthPrism) return false;

            Vector3 scale = LeafSize != Vector3.zero ? LeafSize : Vector3.one;

            // The prototypes here are the PREFAB's assemblers, which carry the unscaled
            // spacing - the authored LatticeScale is applied to each spawned instance. Pass it
            // in, or a scaled element previews its new prism on the old lattice (Space's 46u
            // strut on a 7.8u gyroid reads as a solid block).
            float latticeScale = _latticeScaleOverride > 0f ? _latticeScaleOverride : 1f;

            if (healthPrism.TryGetComponent(out GyroidAssembler gyroid))
                return PreviewGyroid(gyroid, scale, budget, into, latticeScale);
            if (healthPrism.TryGetComponent(out SchwarzPAssembler schwarz))
                return schwarz.TryPreviewLattice(budget, scale, into, latticeScale);
            if (healthPrism.TryGetComponent(out WallAssembler wall))
                return PreviewWall(wall, scale, budget, into);

            return false;
        }

        static readonly CornerSiteType[] GyroidSites =
        {
            CornerSiteType.TopRight, CornerSiteType.TopLeft,
            CornerSiteType.BottomLeft, CornerSiteType.BottomRight,
        };

        static bool PreviewGyroid(GyroidAssembler prototype, Vector3 scale, int budget,
                                  List<SpawnPoint> into, float latticeScale = 1f)
        {
            float separation = prototype.SeparationDistance * Mathf.Max(latticeScale, 1e-4f);
            var frontier = new Queue<(Vector3 pos, Quaternion rot, GyroidBlockType type)>();
            var occupied = new HashSet<Vector3Int>();

            frontier.Enqueue((Vector3.zero, Quaternion.identity, prototype.BlockType));
            occupied.Add(Quantize(Vector3.zero, separation));
            into.Add(new SpawnPoint(Vector3.zero, Quaternion.identity, scale));

            while (frontier.Count > 0 && into.Count < budget)
            {
                var (pos, rot, type) = frontier.Dequeue();

                foreach (var site in GyroidSites)
                {
                    if (into.Count >= budget) break;
                    if (!GyroidBondMateDataContainer.BondMateDataMap.TryGetValue((type, site), out var bond)) continue;

                    // transform.ToGlobal(local) == position + rotation * local (unscaled basis).
                    Vector3 childPos = pos + rot * (bond.DeltaPosition * separation);

                    var key = Quantize(childPos, separation);
                    if (!occupied.Add(key)) continue; // the site is already filled - as in the real bond

                    Vector3 forward = rot * (bond.DeltaForward + Vector3.forward);
                    Vector3 up = rot * (bond.DeltaUp + Vector3.up);
                    if (forward.sqrMagnitude < 1e-6f || up.sqrMagnitude < 1e-6f) continue;
                    Quaternion childRot = Quaternion.LookRotation(forward, up);

                    into.Add(new SpawnPoint(childPos, childRot, scale));
                    frontier.Enqueue((childPos, childRot, bond.BlockType));
                }
            }

            return into.Count > 1;
        }

        static bool PreviewWall(WallAssembler prototype, Vector3 scale, int budget, List<SpawnPoint> into)
        {
            float separation = prototype.SeparationDistance;
            float stepX = scale.x + separation;
            float stepY = scale.y + separation;

            // The wall bonds ±up / ±right in its own plane: a square sheet, grown outward from the
            // seed so a partial budget still reads as a wall rather than a stripe.
            int side = Mathf.Max(1, Mathf.FloorToInt(Mathf.Sqrt(budget)));
            int half = side / 2;
            for (int y = -half; y <= half && into.Count < budget; y++)
            for (int x = -half; x <= half && into.Count < budget; x++)
                into.Add(new SpawnPoint(new Vector3(x * stepX, y * stepY, 0f), Quaternion.identity, scale));

            return into.Count > 0;
        }

        /// <summary>Lattice-cell key, so a site already bonded is never filled twice.</summary>
        static Vector3Int Quantize(Vector3 position, float separation)
        {
            float cell = Mathf.Max(0.01f, separation * 0.5f);
            return new Vector3Int(
                Mathf.RoundToInt(position.x / cell),
                Mathf.RoundToInt(position.y / cell),
                Mathf.RoundToInt(position.z / cell));
        }
    }
}
