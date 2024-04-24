using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

        [SerializeField] float minBranchLengthMultiplier = 0.6f;
        [SerializeField] float maxBranchLengthMultiplier = 0.8f;
        [SerializeField] int maxDepth = 10;
        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] float leafChance = 0.05f;

        HashSet<Branch> activeBranches = new HashSet<Branch>();

        struct Branch
        {
            public GameObject gameObject;
            public int depth;
        }

        private int spawnedItemCount = 0;

        protected override void Start()
        {
            base.Start();
            activeBranches.Add(new Branch { gameObject = gameObject, depth = 0 });
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
                            Vector3 branchDirection = Quaternion.Euler(0f, branchAngleRad * Mathf.Rad2Deg, 0f) * (branch.gameObject.transform.forward);
                            float branchLengthMultiplier = Random.Range(minBranchLengthMultiplier, maxBranchLengthMultiplier);
                            //float branchLength = branch.depth == 0 ? branchLengthMultiplier * spindle.Length :
                            //                                         branchLengthMultiplier * spindle.Length;// / branch.depth;
                            Debug.Log($"branch depth {branch.depth}");
                            Debug.Log($"spindleLength {spindle.Length}");
                            newBranch.gameObject = Instantiate(spindle, branch.gameObject.transform.position + (spindle.cylinder.transform.localScale.y * branch.gameObject.transform.forward), branch.gameObject.transform.rotation).gameObject;
                            newBranch.gameObject.transform.position = branch.depth < 1 ? branch.gameObject.transform.position + (spindle.cylinder.transform.localScale.y * branch.gameObject.transform.forward) :
                                                                                               branch.gameObject.transform.position + (spindle.cylinder.transform.localScale.y / (branch.depth - 1) * branch.gameObject.transform.forward) ;
                            newBranch.gameObject.transform.localScale = branch.depth == 0 ? spindle.transform.localScale :
                                                                                             spindle.transform.localScale / branch.depth;
                            newBranch.gameObject.transform.parent = branch.gameObject.transform;
                            newBranch.gameObject.transform.rotation = Quaternion.LookRotation(branchDirection);
                            newBranch.depth = branch.depth + 1;
                            spawnedItemCount++;
                            newBranches.Add(newBranch);
                            leafChance += 0.01f;
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

