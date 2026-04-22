using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Gameplay;
using System.Linq;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Generates points along a kinky line that can branch into tree structures.
    /// </summary>
    public class BranchingLineGenerator : SpawnableBase
    {
        [Header("Branching Line")]
        [SerializeField] int count = 10;
        [SerializeField] float stepLength = 400f;
        [SerializeField] float branchProbability = 0.2f;
        [SerializeField] int minBranches = 1;
        [SerializeField] int maxBranches = 3;
        [SerializeField] int minBranchAngle = 20;
        [SerializeField] int maxBranchAngle = 20;
        [SerializeField] float minBranchLengthMultiplier = 0.6f;
        [SerializeField] float maxBranchLengthMultiplier = 0.8f;
        [SerializeField] int maxDepth = 3;
        [SerializeField] int maxTotalPoints = 100;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new List<SpawnPoint>();
            var currentPos = origin;
            var currentRot = Quaternion.identity;

            for (int i = 0; i < count && points.Count < maxTotalPoints; i++)
            {
                // Try branching
                if ((float)rng.NextDouble() < branchProbability && maxDepth > 0)
                {
                    int numBranches = rng.Next(minBranches, maxBranches + 1);
                    for (int b = 0; b < numBranches && points.Count < maxTotalPoints; b++)
                    {
                        float branchAngle = rng.Next(minBranchAngle, maxBranchAngle + 1);
                        Vector3 branchDir = Quaternion.Euler(0f, branchAngle, 0f) * currentRot * Vector3.forward;
                        float lengthMult = (float)rng.NextDouble() *
                                           (maxBranchLengthMultiplier - minBranchLengthMultiplier)
                                           + minBranchLengthMultiplier;
                        float branchLength = stepLength * lengthMult;

                        var branchPos = currentPos + branchDir * branchLength;
                        var branchRot = Quaternion.LookRotation(branchDir, Vector3.up);
                        points.Add(new SpawnPoint(branchPos, branchRot));

                        GenerateBranches(points, branchPos, branchDir, branchLength, maxDepth - 1);
                    }
                }

                // Main line step
                float altitude = (float)rng.NextDouble() * 20f + 70f;
                float azimuth = (float)rng.NextDouble() * 360f;
                currentRot = Quaternion.Euler(0f, 0f, azimuth) * Quaternion.Euler(0f, altitude, 0f);
                currentPos += currentRot * (stepLength * Vector3.forward);

                points.Add(new SpawnPoint(currentPos, currentRot));
            }

            return points.ToArray();
        }

        private void GenerateBranches(List<SpawnPoint> points, Vector3 parentPos,
            Vector3 direction, float length, int depth)
        {
            if (depth <= 0 || points.Count >= maxTotalPoints)
                return;

            if ((float)rng.NextDouble() < branchProbability)
            {
                int numBranches = rng.Next(minBranches, maxBranches + 1);
                for (int i = 0; i < numBranches && points.Count < maxTotalPoints; i++)
                {
                    float branchAngle = rng.Next(minBranchAngle, maxBranchAngle + 1);
                    Vector3 branchDir = Quaternion.Euler(0f, branchAngle, 0f) * direction;
                    float lengthMult = (float)rng.NextDouble() *
                                       (maxBranchLengthMultiplier - minBranchLengthMultiplier)
                                       + minBranchLengthMultiplier;
                    float branchLength = length * lengthMult;

                    var branchPos = parentPos + branchDir * branchLength;
                    var branchRot = Quaternion.LookRotation(branchDir, Vector3.up);
                    points.Add(new SpawnPoint(branchPos, branchRot));

                    GenerateBranches(points, branchPos, branchDir, branchLength, depth - 1);
                }
            }
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(
                System.HashCode.Combine(count, stepLength, branchProbability, minBranches, maxBranches),
                System.HashCode.Combine(minBranchAngle, maxBranchAngle, maxDepth, maxTotalPoints, seed, origin)
            );
        }
    }
}
