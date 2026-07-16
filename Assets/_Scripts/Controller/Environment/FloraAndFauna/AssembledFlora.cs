
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

        /// <summary>Assembled layer of the variant expression: the live-prism budget
        /// (Mass gyroid 1500 / Space 800 - the per-element density identity).</summary>
        public override void ApplyVariantTuning(FloraVariantTuning tuning)
        {
            base.ApplyVariantTuning(tuning);
            if (tuning == null) return;
            if (tuning.MaxTotalSpawnedObjects >= 0) maxTotalSpawnedObjects = tuning.MaxTotalSpawnedObjects;
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

            // Parent retirement, evaluated after the child exists — same ordering
            // as the old inline loop.
            if (order.parent.depth >= maxDepth - 1 || order.parent.assembler.IsFullyBonded())
                activeBranches.Remove(order.parent);
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
            // domains genuinely different anti-domain density targets.
            float radius = ResolvePlantRadius(legacyRadius: 200f);
            transform.position = cellData.CrystalTransform.position + radius * Random.onUnitSphere;
        }

        public Assembler CreateNewAssembler()
        {
            CSDebug.Log("New Assembler");
            var newSpindle = AddSpindle();

            HealthPrism newHealthPrism = Instantiate(healthPrism, transform.position, transform.rotation);
            AddHealthBlock(newHealthPrism);
            newHealthPrism.transform.SetParent(newSpindle.transform, false);
            newHealthPrism.LifeForm = this;
            newHealthPrism.Initialize();

            Assembler newAssembler = newHealthPrism.GetComponent<Assembler>();
            newAssembler.Prism = newHealthPrism;
            newAssembler.Spindle = newSpindle;
            newAssembler.Depth = depth;

            Branch newBranch = new Branch(newHealthPrism);
            newBranch.gameObject = newSpindle.gameObject;
            newBranch.assembler = newAssembler;
            newBranch.depth = 0;

            activeBranches.Add(newBranch);

            return newAssembler;
        }
    }
}
