using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace CosmicShore
{


    public class BranchingFlora : Flora
    {
        [SerializeField] float growthChance = 0.2f;
        [SerializeField] float minBranchAngle = -40f;
        [SerializeField] float maxBranchAngle = 40f;
        [SerializeField] int minBranches = 1; // don't set to 0
        [SerializeField] int maxBranches = 2;
        [SerializeField] int minTrunks = 1;
        [SerializeField] int maxTrunks = 1;

        [SerializeField] float minBranchLengthMultiplier = 0.6f;
        [SerializeField] float maxBranchLengthMultiplier = 0.8f;
        [SerializeField] int maxDepth = 10;
        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] float leafChance = 0.05f;
        [SerializeField] float leafChanceIncrement = 0.01f;

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
                        AddHealthBlock(newBranch.gameObject.GetComponent<HealthBlock>());
                    }
                    else
                    {
                        int numBranches = Random.Range(minBranches, maxBranches + 1);
                        for (int i = 0; i < numBranches; i++)
                        {
                            newBranch.gameObject = Instantiate(spindle, branch.gameObject.transform.position + (spindle.cylinder.transform.localScale.y * branch.gameObject.transform.forward), branch.gameObject.transform.rotation).gameObject;
                            ScaleAndPositionBranch(ref newBranch, branch);

                            newBranch.gameObject.transform.rotation = Quaternion.LookRotation(node.GetCrystal().transform.position - transform.position) * RandomVectorRotation(minBranchAngle, maxBranchAngle); // crysaltropism

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
            transform.position = node.GetCrystal().transform.position + (plantRadius * Random.onUnitSphere);
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

