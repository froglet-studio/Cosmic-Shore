using System.Collections.Generic;
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

        [SerializeField] float minBranchLengthMultiplier = 0.6f;
        [SerializeField] float maxBranchLengthMultiplier = 0.8f;
        [SerializeField] int maxDepth = 10;
        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] float leafChance = 0.05f;
        [SerializeField] float leafChanceIncrement = 0.01f;

        [SerializeField] bool isCrystaltropic = true;
        [SerializeField] BranchingFlora SecondarySpawn;
        [SerializeField] bool hasPlantedSecondary;
        [SerializeField] bool plantAroundCrystal = true;
        public Vector3 goal;

        HashSet<Branch> activeBranches = new HashSet<Branch>();

        [SerializeField] float plantRadius = 75f;

        struct Branch
        {
            public GameObject gameObject;
            public int depth;
        }

        private int spawnedItemCount = 0;

        protected override void Start()
        {
            base.Start();
            if (isCrystaltropic)
            {
                goal = node.GetCrystal().transform.position;
            }
            //activeBranches.Add(new Branch { gameObject = gameObject, depth = 0 }); // add trunk
            SeedBranches(); // add more truncks
            transform.rotation = Quaternion.LookRotation(node.GetCrystal().transform.position);
        }

        void SeedBranches()
        {
            for (int i = 0; i < Random.Range(minTrunks, maxTrunks); i++)
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
            if (spawnedItemCount >= maxTotalSpawnedObjects) return;

            List<Branch> newBranches = new List<Branch>();
            List<Branch> branchesToRemove = new List<Branch>();
            foreach (Branch branch in activeBranches)
            {
                if (Random.value < growthChance && branch.depth < maxDepth)
                {
                    Branch newBranch = new Branch();
                    if (Random.value < leafChance)
                    {
                        newBranch.gameObject = Instantiate(healthBlock, branch.gameObject.transform.position + (spindle.cylinder.transform.localScale.y * branch.gameObject.transform.forward), branch.gameObject.transform.rotation).gameObject; // TODO: position and orient leaf
                        ScaleAndPositionBranch(ref newBranch, branch);
                        var newHealthblock = newBranch.gameObject.GetComponent<HealthBlock>();
                        AddHealthBlock(newHealthblock);
                        if (SecondarySpawn && !hasPlantedSecondary)
                        {
                            Debug.Log("SecondarySpawn");
                            var distance = newHealthblock.transform.position - crystal.transform.position;
                            var newLifeform = Instantiate(SecondarySpawn, crystal.transform.position + (2 * distance), Quaternion.LookRotation(-distance), this.transform);
                            newLifeform.node = node;
                            newLifeform.Team = Team;
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
                            newBranch.gameObject = Instantiate(spindle, branch.gameObject.transform.position + (spindle.cylinder.transform.localScale.y * branch.gameObject.transform.forward), branch.gameObject.transform.rotation).gameObject;
                            ScaleAndPositionBranch(ref newBranch, branch);

                            if (goal != Vector3.zero) newBranch.gameObject.transform.rotation = Quaternion.LookRotation(goal - transform.position) * RandomVectorRotation(minBranchAngle, maxBranchAngle);   
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
            if (plantAroundCrystal) transform.position = node.GetCrystal().transform.position + (plantRadius * Random.onUnitSphere);
        }

        void ScaleAndPositionBranch(ref Branch newBranch, Branch branch)
        {
            newBranch.gameObject.transform.position = branch.depth <= 1 ? branch.gameObject.transform.position + (spindle.cylinder.transform.localScale.y * branch.gameObject.transform.forward) :
                                                                                               branch.gameObject.transform.position + (spindle.cylinder.transform.localScale.y / (branch.depth - 1) * branch.gameObject.transform.forward);
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

