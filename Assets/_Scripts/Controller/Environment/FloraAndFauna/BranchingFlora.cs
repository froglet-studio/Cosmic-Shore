using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    public class BranchingFlora : Flora
    {
        [SerializeField] float growthChance = 0.2f;
        [SerializeField] float minBranchAngle = -40f;
        [SerializeField] float maxBranchAngle = 40f;
        [Range(1, 30)]
        [SerializeField] int minBranches = 1;
        [Range(1, 30)]
        [SerializeField] int maxBranches = 2;
        [SerializeField] int minTrunks = 1;
        [SerializeField] int maxTrunks = 1;

        [SerializeField] int maxDepth = 10;
        [Tooltip("Maximum LIVE prisms this flora can hold. Consumption frees budget - a grazed " +
                 "flora regrows toward this cap instead of staying a permanent un-growing fragment.")]
        [SerializeField] int maxTotalSpawnedObjects = 1000;

        /// <summary>The live-prism budget this individual resolved to - the base reads it for
        /// the reproduction maturity gate (see <see cref="Flora.PrismBudget"/>).</summary>
        protected override int PrismBudget => maxTotalSpawnedObjects;
        [SerializeField] float leafChance = 0.05f;
        [SerializeField] float leafChanceIncrement = 0.01f;

        [SerializeField] bool isCrystaltropic = true;
        [SerializeField] BranchingFlora SecondarySpawn;
        [SerializeField] bool hasPlantedSecondary;
        [SerializeField] bool plantAroundCrystal = true;
        [SerializeField] float branchingScaleFactor = 14f;
        public Vector3 goal;

        HashSet<Branch> activeBranches = new HashSet<Branch>();

        [SerializeField] float plantRadius = 75f;
        [SerializeField] float noLeafFailsafeSeconds = 8f;
        [SerializeField] bool guaranteeInitialLeaf = true;

        Coroutine noLeafFailsafeRoutine;
        struct Branch
        {
            public GameObject gameObject;
            public int depth;
        }

        /// <summary>
        /// Branching layer of the variant expression: the live-prism budget. Without this the
        /// config's <see cref="FloraVariantTuning.MaxTotalSpawnedObjects"/> was silently inert on
        /// every branching species (only <see cref="AssembledFlora"/> read it), so a cell could
        /// author a per-plant budget, see nothing change, and get the prefab's own — 5000 for both
        /// CactiFlora and PineFlora, i.e. a handful of plants able to eat a whole arena's phase
        /// ladder on their own. A tuning field that appears on every flora config has to mean the
        /// same thing on every flora.
        /// </summary>
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

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell);

            // CrystalTransform is null in a cell that holds no crystal (it logs and returns null),
            // so resolve it ONCE and fall back to the plant's own growth axis - a crystal-less
            // cell should grow an unaimed plant, not throw on the first one it seeds.
            var crystalTransform = cellData ? cellData.CrystalTransform : null;
            Vector3 aim = crystalTransform ? crystalTransform.position : transform.position + GrowthUp;

            if (isCrystaltropic)
                goal = aim;

            SeedBranches();
            SafeLookRotation.TrySet(transform, aim, transform);

            if (guaranteeInitialLeaf)
                SpawnOneLeafOnAnyTrunk();

            if (noLeafFailsafeRoutine != null) StopCoroutine(noLeafFailsafeRoutine);
            noLeafFailsafeRoutine = StartCoroutine(KillIfStillNoLeaves(noLeafFailsafeSeconds));
        }
        
        void SpawnOneLeafOnAnyTrunk()
        {
            if (activeBranches.Count == 0) return;

            // pick any trunk
            var trunk = activeBranches.First();

            var hp = EnvironmentPrismPool.Get(
                healthPrism,
                trunk.gameObject.transform.position + (branchingScaleFactor * trunk.gameObject.transform.forward),
                trunk.gameObject.transform.rotation,
                trunk.gameObject.transform);
            if (!hp) return;

            hp.LifeForm = this;
            hp.ChangeTeam(domain);

            AddHealthBlock(hp);
            hp.Initialize("flora");
        }

        IEnumerator KillIfStillNoLeaves(float seconds)
        {
            if (seconds <= 0f) yield break;

            yield return new WaitForSeconds(seconds);
            if (!this) yield break;

            var leaves = GetComponentsInChildren<HealthPrism>(true);
            if (leaves != null && leaves.Length != 0) yield break;
            CSDebug.LogWarning($"{name}: BranchingFlora had no HealthPrisms after {seconds}s. Auto-dying.");
            Die();
        }
        void SeedBranches()
        {
            for (int i = 0; i < Random.Range(minTrunks, maxTrunks + 1); i++)
            {
                Branch branch = new Branch();
                branch.gameObject = Instantiate(spindle, transform.position, transform.rotation).gameObject;
                branch.gameObject.transform.rotation = RandomVectorRotation(0,180);
                branch.gameObject.transform.parent = transform;
                branch.depth = 0;
                activeBranches.Add(branch);
                AddSpindle(branch.gameObject.GetComponent<Spindle>());
            }
        }

        public override void Grow()
        {
            // Live-prism budget: consumption frees budget so a grazed flora regrows.
            // (Was: a lifetime spawn counter that never decremented - see AssembledFlora.)
            if (healthTracker != null && healthTracker.Count >= maxTotalSpawnedObjects) return;

            // Frenzy gate: growth runs at a steady rate until Frenzy, then pauses and
            // resumes when an active force (fauna grazing / vessel abilities) brings the
            // cell back below the Frenzy exit threshold. Cell.FloraGrowingEnabled is the
            // single source of truth - no early growth cap.
            if (cell && !cell.FloraGrowingEnabled) return;

            // Reawakening: re-seed trunk branches when all of them have grown out or
            // been consumed, so the flora keeps producing instead of going inert.
            // Guarded on having surviving prisms - BranchingFlora seeds its first
            // trunks in Initialize() AFTER the first synchronous Grow() tick, so an
            // unguarded reseed here would double-seed every new flora.
            if (activeBranches.Count == 0)
            {
                if (healthTracker != null && healthTracker.Count > 0)
                    SeedBranches();
                return;
            }

            List<Branch> newBranches = new List<Branch>();
            List<Branch> branchesToRemove = new List<Branch>();
            foreach (Branch branch in activeBranches)
            {
                if (Random.value < growthChance && branch.depth < maxDepth)
                {
                    Branch newBranch = new Branch();
                    if (Random.value < leafChance)
                    {
                        newBranch.gameObject = EnvironmentPrismPool.Get(healthPrism, branch.gameObject.transform.position + (branchingScaleFactor * branch.gameObject.transform.forward), branch.gameObject.transform.rotation).gameObject; // TODO: position and orient leaf
                        ScaleAndPositionBranch(ref newBranch, branch);
                        var newHealthblock = newBranch.gameObject.GetComponent<HealthPrism>();
                        AddHealthBlock(newHealthblock);
                        newHealthblock.Initialize();
                        // Growth is this plant's feeding - see Flora.NotifyGrew.
                        NotifyGrew();
                        if (SecondarySpawn && !hasPlantedSecondary)
                        {
                            var distance = newHealthblock.transform.position - crystal.transform.position;
                            var newLifeform = Instantiate(SecondarySpawn, crystal.transform.position + (2 * distance), Quaternion.identity, this.transform);
                            SafeLookRotation.TrySet(newLifeform.transform, -distance, newLifeform);
                            newLifeform.cell = cell;
                            newLifeform.domain = domain;
                            newLifeform.goal = newHealthblock.transform.position;
                            hasPlantedSecondary = true;
                            newLifeform.hasPlantedSecondary = true;
                        }
                    }
                    else
                    {
                        int numBranches = Random.Range(minBranches, maxBranches + 1);
                        for (int i = 0; i < numBranches; i++)
                        {
                            newBranch.gameObject = Instantiate(spindle, branch.gameObject.transform.position + (branchingScaleFactor * branch.gameObject.transform.forward), branch.gameObject.transform.rotation).gameObject;
                            ScaleAndPositionBranch(ref newBranch, branch);

                            if (goal != Vector3.zero && SafeLookRotation.TryGet(goal - transform.position, out var branchRotation, newBranch.gameObject))
                                newBranch.gameObject.transform.rotation = branchRotation * RandomVectorRotation(minBranchAngle, maxBranchAngle);   
                            else newBranch.gameObject.transform.localRotation = RandomVectorRotation(minBranchAngle, maxBranchAngle); //* branch.gameObject.transform.rotation;
                         

                            AddSpindle(newBranch.gameObject.GetComponent<Spindle>());
                            newBranches.Add(newBranch);
                            leafChance += leafChanceIncrement;
                        }
                    }
                    
                    branchesToRemove.Add(branch);
                }
            }

            foreach (Branch branch in branchesToRemove)
            {
                activeBranches.Remove(branch);
            }

            activeBranches.UnionWith(newBranches);
        }

        public override void Plant()
        {
            // A pinned position (the Lifeform Matrix toy's spawn-here stations) wins over dispersal.
            if (TryGetPlantPositionOverride(out var pinned))
            {
                transform.position = pinned;
                return;
            }

            if (plantAroundCrystal)
            {
                // Disperse across the cell (fraction of membrane radius - see Flora base)
                // instead of the legacy fixed plantRadius huddle around the crystal. The shell
                // is measured from the CELL CENTRE (Flora.ResolvePlantCenter), so a mode whose
                // crystal roams can't drag the planting shell outside the membrane with it.
                float radius = ResolvePlantRadius(legacyRadius: plantRadius);
                transform.position = ResolvePlantCenter() + (radius * Random.onUnitSphere);
            }
        }

        void ScaleAndPositionBranch(ref Branch newBranch, Branch branch)
        {
            newBranch.gameObject.transform.position = branch.depth <= 1 ? branch.gameObject.transform.position + (branchingScaleFactor * branch.gameObject.transform.forward) :
                                                                                               branch.gameObject.transform.position + (branchingScaleFactor / (branch.depth - 1) * branch.gameObject.transform.forward);
            newBranch.gameObject.transform.localScale = branch.depth == 0 ? spindle.transform.localScale :
                                                                             spindle.transform.localScale / branch.depth;
            newBranch.gameObject.transform.parent = branch.gameObject.transform;

            newBranch.depth = branch.depth + 1;
        }

        /// <summary>
        /// Pure preview of the branch walk - see <see cref="Flora.TryPreviewGrowth"/>. Mirrors
        /// <see cref="Grow"/>'s rule (branch, or leaf with <c>leafChance</c> which climbs by
        /// <c>leafChanceIncrement</c>; children step <c>branchingScaleFactor</c> forward, foreshortened
        /// past depth 1; scale falls as 1/depth) with two deliberate differences, neither of which a
        /// thumbnail can show: <c>growthChance</c> is skipped (it paces growth over time, it does not
        /// change the shape a branch eventually takes) and there is no Frenzy gate or prism budget.
        ///
        /// Spindles are reported as well as leaves: the branch structure IS the silhouette here,
        /// and a leaves-only preview would read as scattered confetti.
        /// </summary>
        public override bool TryPreviewGrowth(int budget, int seed, List<SpawnPoint> into)
        {
            if (into == null || budget <= 0) return false;

            // System.Random, never UnityEngine.Random: a preview must not advance a shared
            // deterministic sequence the simulation is drawing from.
            var rng = new System.Random(seed);
            Vector3 spindleScale = spindle ? spindle.transform.localScale : Vector3.one;
            Vector3 leafScale = LeafSize != Vector3.zero ? LeafSize : Vector3.one;

            var frontier = new List<(Vector3 pos, Quaternion rot, int depth)>();
            int trunks = Mathf.Max(1, minTrunks + rng.Next(0, Mathf.Max(1, maxTrunks - minTrunks + 1)));
            for (int i = 0; i < trunks; i++)
                frontier.Add((Vector3.zero, RandomRotation(rng, 0f, 180f), 0));

            float leaf = leafChance;
            var next = new List<(Vector3 pos, Quaternion rot, int depth)>();

            while (frontier.Count > 0 && into.Count < budget)
            {
                next.Clear();
                foreach (var (pos, rot, depth) in frontier)
                {
                    if (into.Count >= budget) break;
                    if (depth >= maxDepth) continue;

                    // Same step the real ScaleAndPositionBranch takes.
                    float step = depth <= 1 ? branchingScaleFactor : branchingScaleFactor / (depth - 1);
                    Vector3 childPos = pos + rot * Vector3.forward * step;
                    Vector3 childScale = depth == 0 ? spindleScale : spindleScale / depth;

                    if (rng.NextDouble() < leaf)
                    {
                        into.Add(new SpawnPoint(childPos, rot, leafScale));
                        continue; // a leaf terminates the branch, as in Grow()
                    }

                    into.Add(new SpawnPoint(childPos, rot, childScale));

                    int branches = Mathf.Max(1, minBranches + rng.Next(0, Mathf.Max(1, maxBranches - minBranches + 1)));
                    for (int b = 0; b < branches && into.Count < budget; b++)
                    {
                        Quaternion childRot = rot * RandomRotation(rng, minBranchAngle, maxBranchAngle);
                        next.Add((childPos, childRot, depth + 1));
                        leaf += leafChanceIncrement;
                    }
                }

                (frontier, next) = (next, frontier);
            }

            return into.Count > 0;
        }

        /// <summary>Seeded mirror of <see cref="RandomVectorRotation"/> (no UnityEngine.Random).</summary>
        static Quaternion RandomRotation(System.Random rng, float minAngle, float maxAngle)
        {
            float altitude = minAngle + (float)rng.NextDouble() * (maxAngle - minAngle);
            float azimuth = (float)rng.NextDouble() * 360f;
            return Quaternion.Euler(0f, 0f, azimuth) * Quaternion.Euler(altitude, 0f, 0f);
        }

        private Quaternion RandomVectorRotation(float minBranchAngle, float maxBranchAngle) // TODO: move to utility class
        {
            float altitude = Random.Range(minBranchAngle, maxBranchAngle);
            float azimuth = Random.Range(0f, 360f);

            Quaternion rotation = Quaternion.Euler(0f, 0f, azimuth) * Quaternion.Euler(altitude, 0f, 0f);
            return rotation;
        }
    }
}

