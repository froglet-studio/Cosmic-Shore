using System.Collections;
using System.Collections.Generic;
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
        [Tooltip("Maximum LIVE prisms this flora can hold. Consumption frees budget — a grazed " +
                 "flora regrows toward this cap instead of staying a permanent un-growing fragment.")]
        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] float leafChance = 0.05f;
        [SerializeField] float leafChanceIncrement = 0.01f;

        [SerializeField] bool isCrystaltropic = true;
        [SerializeField] BranchingFlora SecondarySpawn;
        [SerializeField] bool hasPlantedSecondary;
        [SerializeField] bool plantAroundCrystal = true;
        [SerializeField] float branchingScaleFactor = 14f;
        public Vector3 goal;

        // List, not HashSet: avoids boxed reflection-Equals on the struct (no
        // IEquatable before) and LINQ enumerator allocs in the per-growPeriod loop.
        // Entries are unique by construction (each wraps a fresh Instantiate).
        readonly List<Branch> activeBranches = new List<Branch>();
        readonly List<Branch> newBranchesScratch = new List<Branch>();
        readonly List<Branch> branchesToRemoveScratch = new List<Branch>();

        [SerializeField] float plantRadius = 75f;
        [SerializeField] float noLeafFailsafeSeconds = 8f;
        [SerializeField] bool guaranteeInitialLeaf = true;

        Coroutine noLeafFailsafeRoutine;
        struct Branch : System.IEquatable<Branch>
        {
            public GameObject gameObject;
            public int depth;

            public bool Equals(Branch other) =>
                gameObject == other.gameObject && depth == other.depth;

            public override bool Equals(object obj) => obj is Branch other && Equals(other);

            public override int GetHashCode() => gameObject ? gameObject.GetInstanceID() : 0;
        }

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell);

            if (isCrystaltropic)
                goal = cellData.CrystalTransform.position;

            SeedBranches();
            SafeLookRotation.TrySet(transform, cellData.CrystalTransform.position, transform);

            if (guaranteeInitialLeaf)
                SpawnOneLeafOnAnyTrunk();

            if (noLeafFailsafeRoutine != null) StopCoroutine(noLeafFailsafeRoutine);
            noLeafFailsafeRoutine = StartCoroutine(KillIfStillNoLeaves(noLeafFailsafeSeconds));
        }
        
        void SpawnOneLeafOnAnyTrunk()
        {
            if (activeBranches.Count == 0) return;

            // pick any trunk
            var trunk = activeBranches[0];

            var go = Instantiate(
                healthPrism,
                trunk.gameObject.transform.position + (branchingScaleFactor * trunk.gameObject.transform.forward),
                trunk.gameObject.transform.rotation,
                trunk.gameObject.transform
            ).gameObject;

            var hp = go.GetComponent<HealthPrism>();
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
            // (Was: a lifetime spawn counter that never decremented — see AssembledFlora.)
            if (healthTracker != null && healthTracker.Count >= maxTotalSpawnedObjects) return;

            // Frenzy gate: growth runs at a steady rate until Frenzy, then pauses and
            // resumes when an active force (fauna grazing / vessel abilities) brings the
            // cell back below the Frenzy exit threshold. Cell.FloraGrowingEnabled is the
            // single source of truth — no early growth cap.
            if (cell && !cell.FloraGrowingEnabled) return;

            // Reawakening: re-seed trunk branches when all of them have grown out or
            // been consumed, so the flora keeps producing instead of going inert.
            // Guarded on having surviving prisms — BranchingFlora seeds its first
            // trunks in Initialize() AFTER the first synchronous Grow() tick, so an
            // unguarded reseed here would double-seed every new flora.
            if (activeBranches.Count == 0)
            {
                if (healthTracker != null && healthTracker.Count > 0)
                    SeedBranches();
                return;
            }

            var newBranches = newBranchesScratch;
            var branchesToRemove = branchesToRemoveScratch;
            newBranches.Clear();
            branchesToRemove.Clear();
            foreach (Branch branch in activeBranches)
            {
                if (Random.value < growthChance && branch.depth < maxDepth)
                {
                    Branch newBranch = new Branch();
                    if (Random.value < leafChance)
                    {
                        newBranch.gameObject = Instantiate(healthPrism, branch.gameObject.transform.position + (branchingScaleFactor * branch.gameObject.transform.forward), branch.gameObject.transform.rotation).gameObject; // TODO: position and orient leaf
                        ScaleAndPositionBranch(ref newBranch, branch);
                        var newHealthblock = newBranch.gameObject.GetComponent<HealthPrism>();
                        AddHealthBlock(newHealthblock);
                        newHealthblock.Initialize();
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

            activeBranches.AddRange(newBranches);
        }

        public override void Plant()
        {
            if (plantAroundCrystal)
            {
                // Disperse across the cell (fraction of membrane radius — see Flora base)
                // instead of the legacy fixed plantRadius huddle around the crystal.
                float radius = ResolvePlantRadius(legacyRadius: plantRadius);
                transform.position = cellData.CrystalTransform.position + (radius * Random.onUnitSphere);
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

        private Quaternion RandomVectorRotation(float minBranchAngle, float maxBranchAngle) // TODO: move to utility class
        {
            float altitude = Random.Range(minBranchAngle, maxBranchAngle);
            float azimuth = Random.Range(0f, 360f);

            Quaternion rotation = Quaternion.Euler(0f, 0f, azimuth) * Quaternion.Euler(altitude, 0f, 0f);
            return rotation;
        }
    }
}

