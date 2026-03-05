using System.Collections;
using System.Collections.Generic;
using CosmicShore.Game;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore
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

        //[SerializeField] float minBranchLengthMultiplier = 0.6f;
        [SerializeField] float maxBranchLengthMultiplier = 0.8f;
        [SerializeField] int maxDepth = 10;
        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] float leafChance = 0.05f;
        [SerializeField] float leafChanceIncrement = 0.01f;

        [SerializeField] bool isCrystaltropic = true;
        [SerializeField] BranchingFlora SecondarySpawn;
        [SerializeField] bool hasPlantedSecondary;
        [SerializeField] bool plantAroundCrystal = true;
        [SerializeField] float branchingScaleFactor = 14f;
        public Vector3 goal;

        List<Branch> activeBranches = new List<Branch>();
        static readonly List<Branch> s_newBranches = new List<Branch>(16);
        static readonly List<int> s_removeIndices = new List<int>(16);

        [SerializeField] float plantRadius = 75f;
        [SerializeField] float noLeafFailsafeSeconds = 8f;
        [SerializeField] bool guaranteeInitialLeaf = true;

        Coroutine noLeafFailsafeRoutine;
        struct Branch
        {
            public GameObject gameObject;
            public int depth;
        }

        private int spawnedItemCount = 0;

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

            var hp = GetHealthPrism(
                trunk.gameObject.transform.position + (branchingScaleFactor * trunk.gameObject.transform.forward),
                trunk.gameObject.transform.rotation,
                trunk.gameObject.transform
            );
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

        Spindle GetOrCreateSpindle(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (SpindlePoolManager.Instance)
                return SpindlePoolManager.Instance.Get(position, rotation, parent);
            return Instantiate(spindle, position, rotation, parent);
        }

        void SeedBranches()
        {
            for (int i = 0; i < Random.Range(minTrunks, maxTrunks + 1); i++)
            {
                Branch branch = new Branch();
                var newSpindle = GetOrCreateSpindle(transform.position, transform.rotation, transform);
                branch.gameObject = newSpindle.gameObject;
                branch.gameObject.transform.rotation = RandomVectorRotation(0,180);
                branch.depth = 0;
                activeBranches.Add(branch);
                AddSpindle(newSpindle);
            }
        }

        public override void Grow()
        {
            if (spawnedItemCount >= maxTotalSpawnedObjects) return;

            s_newBranches.Clear();
            s_removeIndices.Clear();
            for (int idx = 0; idx < activeBranches.Count; idx++)
            {
                Branch branch = activeBranches[idx];
                if (Random.value < growthChance && branch.depth < maxDepth)
                {
                    Branch newBranch = new Branch();
                    if (Random.value < leafChance)
                    {
                        var leafPrism = GetHealthPrism(branch.gameObject.transform.position + (branchingScaleFactor * branch.gameObject.transform.forward), branch.gameObject.transform.rotation);
                        if (!leafPrism) continue;
                        newBranch.gameObject = leafPrism.gameObject;
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
                            Vector3 branchPos = branch.gameObject.transform.position + (branchingScaleFactor * branch.gameObject.transform.forward);
                            var newSpindle = GetOrCreateSpindle(branchPos, branch.gameObject.transform.rotation);
                            newBranch.gameObject = newSpindle.gameObject;
                            ScaleAndPositionBranch(ref newBranch, branch);

                            if (goal != Vector3.zero && SafeLookRotation.TryGet(goal - transform.position, out var branchRotation, newBranch.gameObject))
                                newBranch.gameObject.transform.rotation = branchRotation * RandomVectorRotation(minBranchAngle, maxBranchAngle);
                            else newBranch.gameObject.transform.localRotation = RandomVectorRotation(minBranchAngle, maxBranchAngle); //* branch.gameObject.transform.rotation;


                            AddSpindle(newSpindle);
                            s_newBranches.Add(newBranch);
                            leafChance += leafChanceIncrement;
                        }
                    }

                    s_removeIndices.Add(idx);
                }
            }

            // Remove in reverse order to preserve indices
            for (int i = s_removeIndices.Count - 1; i >= 0; i--)
                activeBranches.RemoveAt(s_removeIndices[i]);

            activeBranches.AddRange(s_newBranches);
            s_newBranches.Clear();
            s_removeIndices.Clear();
        }

        public override void Plant()
        {
            if (plantAroundCrystal)
                transform.position = cellData.CrystalTransform.position + (plantRadius * Random.onUnitSphere);
        }

        void ScaleAndPositionBranch(ref Branch newBranch, Branch branch)
        {
            newBranch.gameObject.transform.position = branch.depth <= 1 ? branch.gameObject.transform.position + (branchingScaleFactor * branch.gameObject.transform.forward) :
                                                                                               branch.gameObject.transform.position + (branchingScaleFactor / (branch.depth - 1) * branch.gameObject.transform.forward);
            newBranch.gameObject.transform.localScale = branch.depth == 0 ? spindle.transform.localScale :
                                                                             spindle.transform.localScale / branch.depth;
            newBranch.gameObject.transform.parent = branch.gameObject.transform;

            newBranch.depth = branch.depth + 1;
            spawnedItemCount++;
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
