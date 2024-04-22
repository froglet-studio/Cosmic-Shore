using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class BranchingFlora : Flora
    {
        [SerializeField] float branchProbability = 0.2f;
        [SerializeField] float minBranchAngle = 20f;
        [SerializeField] float maxBranchAngle = 40f;
        [SerializeField] int minBranches = 1;
        [SerializeField] int maxBranches = 3;
        [SerializeField] float minBranchLengthMultiplier = 0.6f;
        [SerializeField] float maxBranchLengthMultiplier = 0.8f;
        [SerializeField] int maxDepth = 3;
        [SerializeField] int maxTotalSpawnedObjects = 1000;
        [SerializeField] List<GameObject> branchPrefabs;

        private int spawnedItemCount = 0;
        private Vector3 currentDisplacement;
        private Quaternion currentRotation;

        protected override void Start()
        {
            base.Start();
            currentDisplacement = transform.position;
            currentRotation = Quaternion.identity;
        }

        public override void Grow()
        {
            if (spawnedItemCount >= maxTotalSpawnedObjects)
                return;

            if (Random.value < branchProbability && maxDepth > 0)
            {
                int numBranches = Random.Range(minBranches, maxBranches + 1);

                for (int i = 0; i < numBranches; i++)
                {
                    float branchAngle = Random.Range(minBranchAngle, maxBranchAngle);
                    float branchAngleRad = branchAngle * Mathf.Deg2Rad;

                    Vector3 branchDirection = Quaternion.Euler(0f, branchAngleRad * Mathf.Rad2Deg, 0f) * currentRotation * Vector3.forward;

                    float branchLengthMultiplier = Random.Range(minBranchLengthMultiplier, maxBranchLengthMultiplier);
                    float branchLength = healthBlock.transform.localScale.z * branchLengthMultiplier; // TODO: decrease with depth

                    GameObject branch = SpawnRandomBranch();
                    branch.transform.position = currentDisplacement + branchDirection * branchLength;
                    branch.transform.rotation = Quaternion.LookRotation(branchDirection);

                    BranchingFlora branchingFlora = branch.GetComponent<BranchingFlora>();
                    if (branchingFlora != null)
                    {
                        branchingFlora.GrowBranches(maxDepth - 1, branchDirection, branchLength);
                    }
                }
            }
            else
            {
                SpawnHealthBlock();
            }

            void SpawnHealthBlock()
            {
                Quaternion rotation;
                currentDisplacement += RandomVectorRotation(healthBlock.transform.localScale.z * Vector3.forward, out rotation);
                currentRotation = rotation;

                HealthBlock newHealthBlock = Instantiate(healthBlock, currentDisplacement, currentRotation);
                newHealthBlock.LifeForm = this;
                AddHealthBlock(newHealthBlock);

                spawnedItemCount++;
            }

            Quaternion rotation;
            currentDisplacement += RandomVectorRotation(healthBlock.transform.localScale.z * Vector3.forward, out rotation);
            currentRotation = rotation;

            HealthBlock newHealthBlock = Instantiate(healthBlock, currentDisplacement, currentRotation);
            newHealthBlock.LifeForm = this;
            AddHealthBlock(newHealthBlock);

            spawnedItemCount++;
        }

        private void GrowBranches(int depth, Vector3 direction, float length)
        {
            if (depth <= 0 || spawnedItemCount >= maxTotalSpawnedObjects)
                return;

            if (Random.value < branchProbability)
            {
                int numBranches = Random.Range(minBranches, maxBranches + 1);

                for (int i = 0; i < numBranches; i++)
                {
                    float branchAngle = Random.Range(minBranchAngle, maxBranchAngle);
                    float branchAngleRad = branchAngle * Mathf.Deg2Rad;

                    Vector3 branchDirection = Quaternion.Euler(0f, branchAngleRad * Mathf.Rad2Deg, 0f) * direction;

                    float branchLengthMultiplier = Random.Range(minBranchLengthMultiplier, maxBranchLengthMultiplier);
                    float branchLength = length * branchLengthMultiplier;

                    GameObject branch = SpawnRandomBranch();
                    branch.transform.position = currentDisplacement + branchDirection * branchLength;
                    branch.transform.rotation = Quaternion.LookRotation(branchDirection);

                    BranchingFlora branchingFlora = branch.GetComponent<BranchingFlora>();
                    if (branchingFlora != null)
                    {
                        branchingFlora.GrowBranches(depth - 1, branchDirection, branchLength);
                    }
                }
            }
        }

        private GameObject SpawnRandomBranch()
        {
            int randomIndex = Random.Range(0, branchPrefabs.Count);
            GameObject branchPrefab = branchPrefabs[randomIndex];

            GameObject branch = Instantiate(branchPrefab, transform);
            spawnedItemCount++;

            return branch;
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

