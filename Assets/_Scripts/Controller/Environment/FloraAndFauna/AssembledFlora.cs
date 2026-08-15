
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
            if (healthTracker != null && healthTracker.Count >= maxTotalSpawnedObjects) return;

            // Frenzy gate: flora grow at a steady rate until the cell crosses into Frenzy,
            // then freeze, resuming automatically when an active force (fauna grazing /
            // vessel abilities) brings the count back below the Frenzy exit threshold.
            // Cell.FloraGrowingEnabled is the single source of truth - no early growth cap
            // (that staggered self-limit was a cheat; the food web is the only down-force).
            if (cell && !cell.FloraGrowingEnabled) return;

            // Reawakening: a flora whose active branches were all consumed or exhausted
            // (a fully-grown gyroid has none) re-sprouts from surviving prisms instead
            // of staying a permanent un-growing fragment. Growth continues next cycle
            // from the re-seeded branches. Guarded on having surviving prisms so a
            // not-yet-seeded flora doesn't spuriously seed here before Plant()/Initialize
            // finish their own setup.
            if (activeBranches.Count == 0)
            {
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

                pendingSpawns.Enqueue(new GrowOrder { parent = branch, info = growthInfo, decidedAt = Time.time });
                itemsSpawned++;
            }

            foreach (Branch branch in branchesToRemove)
            {
                activeBranches.Remove(branch);
            }

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
            if (growthInfo.IsDangerous) newHealthPrism.MakeDangerous();
            newHealthPrism.Initialize();

            newBranch.gameObject = newSpindle.gameObject;
            newBranch.assembler = newAssembler;
            newBranch.depth = order.parent.depth + 1;

            activeBranches.Add(newBranch);

            // Growth is this plant's feeding - it is what funds an offspring (Flora.NotifyGrew,
            // Docs/ECOSYSTEM.md §32). Counted where the prism actually lands, not where the
            // order was decided, so a dropped or lapsed order funds nothing.
            NotifyGrew();

            // Parent retirement, evaluated after the child exists — same ordering
            // as the old inline loop.
            if (order.parent.depth >= maxDepth - 1 || order.parent.assembler.IsFullyBonded())
                activeBranches.Remove(order.parent);
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
        /// Programs the daughter with the donated growth order, in the window between its
        /// variant/level being applied and <c>Initialize</c> - the only point at which a plant's
        /// growth rule can still be seeded.
        /// </summary>
        protected override void ConfigureOffspring(Flora child)
        {
            if (_donatedGrowth != null && child is AssembledFlora assembled)
                assembled.SeedFromGrowth(_donatedGrowth);
            _donatedGrowth = null;
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
        }

        public Assembler CreateNewAssembler()
        {
            CSDebug.Log("New Assembler");
            var newSpindle = AddSpindle();

            HealthPrism newHealthPrism = Instantiate(healthPrism, transform.position, transform.rotation);
            AddHealthBlock(newHealthPrism);
            newHealthPrism.transform.SetParent(newSpindle.transform, false);
            newHealthPrism.LifeForm = this;

            // Seeded from a parent's donated bond site: the site's own block type decides what
            // this prism IS, and whether it is one of the danger types - read off the SAME
            // GrowthInfo the parent's assembler produced, so the daughter's first prism is
            // indistinguishable from the one the parent would have grown there. Applied before
            // Initialize, exactly as ExecuteGrowOrder does for an ordinary child.
            if (_seedGrowth != null)
            {
                var seeded = AssemblerFactory.ProgramAssembler(newHealthPrism.gameObject, _seedGrowth);
                if (seeded != null && _seedGrowth.IsDangerous) newHealthPrism.MakeDangerous();
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
