
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
    /// FOUNDER logs. Diagnostics, not gameplay: the Gyroid Lab exists to observe this system,
    /// and its second playtest produced a symptom (a crystal/spindle flood outrunning grown
    /// prisms) that code reading alone could not attribute - so the decisive sites now report.
    /// Volume is naturally low (births and mints are rare events in a healthy colony; a SPAMMING
    /// counter in the console IS the diagnosis). Always compiled - plain CSDebug, no editor-only
    /// guards (Docs/CONDITIONAL_COMPILATION.md).
    /// </summary>
    public static class GyroidColonyDiagnostics
    {
        public static int Births;
        public static int BirthsHeldByMaturity;
        public static int ReseedMintsBlocked;
        public static int DeclinedForeignSites;
        public static int GrownPrisms;
        public static int SeededDaughterPrisms;
        public static int UnreservedSpawns;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Births = 0;
            BirthsHeldByMaturity = 0;
            ReseedMintsBlocked = 0;
            DeclinedForeignSites = 0;
            GrownPrisms = 0;
            SeededDaughterPrisms = 0;
            UnreservedSpawns = 0;
        }

        public static string Summary =>
            $"claims={GyroidOctagonRegistry.ClaimCount} births={Births} heldByMaturity={BirthsHeldByMaturity} " +
            $"grown={GrownPrisms} seeded={SeededDaughterPrisms} foreignDeclines={DeclinedForeignSites} " +
            $"mintsBlocked={ReseedMintsBlocked} UNRESERVED={UnreservedSpawns} prismsPerCrystal={(GyroidOctagonRegistry.ClaimCount > 0 ? (float)(GrownPrisms + SeededDaughterPrisms) / GyroidOctagonRegistry.ClaimCount : 0f):F1}";
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

            // Pacing overrides - the prefab's throttles are shared by every biome's plants,
            // so a cell that wants speed (the Gyroid Lab) authors it in config instead.
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
                // of re-offering it every tick; the site's fresh reservation lapses on its
                // TTL, the standard skip-after-claim path.
                if (OctagonMode && growthInfo is GyroidGrowthInfo gyroidInfo &&
                    !OwnsLatticeSite(gyroidInfo.Position))
                {
                    (branch.assembler as GyroidAssembler)?.DeclineGrowthSite(gyroidInfo.Site);
                    GyroidColonyDiagnostics.DeclinedForeignSites++;
                    continue;
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

                // Claim lapsed during a long hold — drop it (see MaxOrderAgeSeconds);
                // the next grow tick re-decides this branch.
                if (Time.time - order.decidedAt > MaxOrderAgeSeconds) continue;

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
            // hard-destroyed with it (a pop-out). Claimed sites release via the
            // reservation TTL, same as any skipped-after-claim site.
            pendingSpawns.Clear();
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
        }

        void PlaceCrystalAtCenter()
        {
            if (_octagonCenter.HasValue && crystal)
                crystal.transform.position = _octagonCenter.Value;
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
                // the report while the husk lingers.
                CSDebug.Log($"[GyroidColony] FOUNDER {name} claimed centre {center} " +
                            $"(prism {type} at {position})");
                InvokeRepeating(nameof(LogColonyHeartbeat), 5f, 5f);
                return;
            }

            float dedupe = GyroidOctagonData.CenterDedupeRadius;
            if ((center - _octagonCenter.Value).sqrMagnitude < dedupe * dedupe &&
                _ringMembers.Count < 8)
                _ringMembers.Add(new RingMember { Position = position, Rotation = rotation, Type = type });
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
            if (dMine > GyroidOctagonData.TerritoryRadius) return false;

            float foreignSqr = GyroidOctagonRegistry.NearestForeignClaimSqr(site, this);
            if (foreignSqr >= float.MaxValue) return true;
            return dMine <= Mathf.Sqrt(foreignSqr) + GyroidOctagonData.OwnershipEpsilon;
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

            position = default;
            rotation = default;
            up = null;
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
        /// The octagon colony's reproduction: project the measured neighbour table from every
        /// ring prism this plant has grown, and take the first neighbouring octagon that is
        /// UNCLAIMED and whose seed site is free. The daughter is planted with her root (and
        /// crystal) at that octagon's CENTRE and her first prism at a real member of its ring -
        /// "calculate where the neighbouring crystals belong, check if one is already there,
        /// and plant where there is not". One candidate per call; the base's per-birth loop
        /// (OffspringPerBirth) drains as many as this tick may plant, and the armed quota
        /// retries the rest on later ticks.
        /// </summary>
        bool TryResolveOctagonOffspring(out Vector3 position, out Quaternion rotation, out Vector3? up)
        {
            position = default;
            rotation = default;
            up = null;

            // A claim stranded by a failed spawn (host torn down mid-birth) would block that
            // octagon forever - release it before claiming a new one.
            if (_hasPendingOffspringClaim)
            {
                GyroidOctagonRegistry.Release(_pendingOffspringCenter, this);
                _hasPendingOffspringClaim = false;
            }

            if (!_octagonCenter.HasValue || _ringMembers.Count == 0) return false;

            // A gyroid reproduces only when it has FULLY GROWN all its spindles and health
            // prisms (see OctagonMature). The quota may have been banked long before - the
            // armed retry simply waits here until the plant's own frontier is done.
            if (!OctagonMature)
            {
                GyroidColonyDiagnostics.BirthsHeldByMaturity++;
                return false;
            }

            var spatialIndex = PrismSpatialIndex.EnsureInstance();

            for (int m = 0; m < _ringMembers.Count; m++)
            {
                var member = _ringMembers[m];
                if (!GyroidOctagonData.TryGetNeighbors(member.Type, out var neighbors)) continue;

                for (int n = 0; n < neighbors.Length; n++)
                {
                    Vector3 center = member.Position + member.Rotation * neighbors[n].Center;
                    if (GyroidOctagonRegistry.IsClaimed(center)) continue;

                    Vector3 seedPos = member.Position + member.Rotation * neighbors[n].SeedPosition;
                    Quaternion seedRot = member.Rotation * neighbors[n].SeedRotation;

                    // Claim the centre FIRST - it is released on any later failure. The seed
                    // reservation must come second: a reservation abandoned on a lost claim
                    // race would sit until its TTL, and every neighbouring branch that probes
                    // the site meanwhile marks it PERMANENTLY skipped (GetGrowthInfo treats a
                    // reserve-failure as 'occupied for real') - a transient race would punch a
                    // lasting hole in the surface.
                    if (!GyroidOctagonRegistry.TryClaim(center, this)) continue;

                    // The seed site must be free mass-wise too (a boundary prism this plant
                    // already grew can sit exactly there). TryReserve doubles as the check and
                    // the claim - the daughter's first prism consumes it on register.
                    if (spatialIndex != null && spatialIndex.IsAvailable &&
                        !spatialIndex.TryReserve(seedPos, 3f))
                    {
                        GyroidOctagonRegistry.Release(center, this);
                        continue;
                    }

                    _pendingOffspringCenter = center;
                    _hasPendingOffspringClaim = true;
                    GyroidColonyDiagnostics.Births++;
                    CSDebug.Log($"[GyroidColony] BIRTH #{GyroidColonyDiagnostics.Births} by {name}: " +
                                $"prisms={healthTracker.Count} ring={_ringMembers.Count} " +
                                $"idle={_idleGrowTicks} -> daughter at {center} seed {neighbors[n].SeedType}");
                    _pendingOffspringSeed = new GyroidGrowthInfo
                    {
                        CanGrow = true,
                        Position = seedPos,
                        Rotation = seedRot,
                        BlockType = neighbors[n].SeedType,
                        IsDangerous = true,
                        Depth = depth,
                    };

                    position = center;
                    rotation = seedRot;
                    return true;
                }
            }

            return false;
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
                else if (_donatedGrowth != null)
                {
                    assembled.SeedFromGrowth(_donatedGrowth);
                }
            }
            _pendingOffspringSeed = null;
            _donatedGrowth = null;
        }

        protected override void OnDestroy()
        {
            // The colony's claim on its octagon dies with it, so a grazed-out window becomes
            // plantable again and neighbours recolonise the hole. A pending daughter claim that
            // never transferred is released too.
            if (_octagonCenter.HasValue)
                GyroidOctagonRegistry.Release(_octagonCenter.Value, this);
            if (_hasPendingOffspringClaim)
                GyroidOctagonRegistry.Release(_pendingOffspringCenter, this);
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
            if (crystalGrowth <= 0f || OctagonMode) return;

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
                if (seeded != null && _seedGrowth.IsDangerous)
                {
                    newHealthPrism.MakeDangerous();
                    // The octagon daughter's seed IS her first ring member.
                    if (OctagonMode && _seedGrowth is GyroidGrowthInfo sg)
                    {
                        RegisterDangerPrism(sg.BlockType, _seedGrowth.Position, _seedGrowth.Rotation);
                        GyroidColonyDiagnostics.SeededDaughterPrisms++;
                        _lastGrowthTime = Time.time;
                    }
                }
            }

            newHealthPrism.Initialize();

            Assembler newAssembler = newHealthPrism.GetComponent<Assembler>();
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
        /// • <see cref="SchwarzPAssembler"/> - its own <c>TryPreviewLattice</c>: the same seed
        ///   anchor, tangent sites, Newton projection and parallel-transported heading the live
        ///   growth uses, with the occupancy claims replaced by a local visited set.
        /// </summary>
        public override bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into)
        {
            if (into == null || budget <= 0 || !healthPrism) return false;

            Vector3 scale = LeafSize != Vector3.zero ? LeafSize : Vector3.one;

            if (healthPrism.TryGetComponent(out GyroidAssembler gyroid))
                return PreviewGyroid(gyroid, scale, budget, into);
            if (healthPrism.TryGetComponent(out SchwarzPAssembler schwarz))
                return schwarz.TryPreviewLattice(budget, scale, into);
            if (healthPrism.TryGetComponent(out WallAssembler wall))
                return PreviewWall(wall, scale, budget, into);

            return false;
        }

        static readonly CornerSiteType[] GyroidSites =
        {
            CornerSiteType.TopRight, CornerSiteType.TopLeft,
            CornerSiteType.BottomLeft, CornerSiteType.BottomRight,
        };

        static bool PreviewGyroid(GyroidAssembler prototype, Vector3 scale, int budget, List<SpawnPoint> into)
        {
            float separation = prototype.SeparationDistance;
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
