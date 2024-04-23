using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CosmicShore
{
    public class BranchingFlora : Flora
    {
        [SerializeField] float growthChance = 0.2f;
        [SerializeField] float minBranchAngle = 20f;
        [SerializeField] float maxBranchAngle = 40f;
        [SerializeField] int minBranches = 1; // don't set to 0
        [SerializeField] int maxBranches = 3;
        [SerializeField] float minBranchLengthMultiplier = 0.6f;
        [SerializeField] float maxBranchLengthMultiplier = 0.8f;
        [SerializeField] int maxDepth = 3;
        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] float leafChance = 0.1f;
        bool endbranch = false;

        HashSet<Branch> activeBranches = new HashSet<Branch>();

        enum BranchType 
        {
            HealthBlock,
            Spindle,
        }

        struct Branch
        {
            public GameObject gameObject;
            public int depth;
        }

        private int spawnedItemCount = 0;
        private Vector3 currentDisplacement;
        private Quaternion currentRotation;

        protected override void Start()
        {
            base.Start();
            currentDisplacement = transform.position;
            currentRotation = Quaternion.identity;
            activeBranches.Add(new Branch { gameObject = gameObject, depth = 0 });
        }

        public override void Grow()
        {
            Debug.Log("Growing");
            if (spawnedItemCount >= maxTotalSpawnedObjects) return;

            List<Branch> newBranches = new List<Branch>();
            List<Branch> branchesToRemove = new List<Branch>();

            foreach (Branch branch in activeBranches)
            {
                Debug.Log("Considering Branching");
                if (Random.value < growthChance && branch.depth < maxDepth)
                {
                    Branch newBranch = new Branch();
                    if (Random.value < leafChance)
                    {
                        Debug.Log("Leafing");
                        newBranch.gameObject = Instantiate(healthBlock, branch.gameObject.transform.position, branch.gameObject.transform.rotation).gameObject; // TODO: position and orient leaf
                        newBranch.depth = branch.depth + 1;
                        spawnedItemCount++;
                    }
                    else
                    {
                        Debug.Log("Branching");
                        int numBranches = Random.Range(minBranches, maxBranches + 1);
                        for (int i = 0; i < numBranches; i++)
                        {
                            float branchAngle = Random.Range(minBranchAngle, maxBranchAngle);
                            float branchAngleRad = branchAngle * Mathf.Deg2Rad;
                            Vector3 branchDirection = Quaternion.Euler(0f, branchAngleRad * Mathf.Rad2Deg, 0f) * branch.gameObject.transform.forward;
                            float branchLengthMultiplier = Random.Range(minBranchLengthMultiplier, maxBranchLengthMultiplier);
                            float branchLength = branch.depth == 0 ? branchLengthMultiplier : branchLengthMultiplier / branch.depth;
                            newBranch.gameObject = Instantiate(spindle, branch.gameObject.transform.position, branch.gameObject.transform.rotation).gameObject;
                            newBranch.gameObject.transform.position += branchDirection * branchLength;
                            newBranch.gameObject.transform.rotation = Quaternion.LookRotation(branchDirection);
                            newBranch.depth = branch.depth + 1;
                            spawnedItemCount++;
                            newBranches.Add(newBranch);
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


        private Vector3 RandomVectorRotation(Vector3 vector, out Quaternion rotation)
        {
            float altitude = Random.Range(70f, 90f);
            float azimuth = Random.Range(0f, 360f);

            rotation = Quaternion.Euler(0f, azimuth, 0f) * Quaternion.Euler(altitude, 0f, 0f);
            Vector3 newVector = rotation * vector;
            return newVector;
        }
    }
}

