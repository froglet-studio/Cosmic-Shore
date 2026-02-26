using CosmicShore.Game.Ship;
using CosmicShore.Game.Environment;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game.Environment
{
    public class SpawnableFiveRings : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
        [SerializeField] int blocksPerRing = 12;
        [SerializeField] float ringRadius = 10f;
        [SerializeField] Vector3 scale = new Vector3(4, 4, 9);

        public override GameObject Spawn(int intensity = 1)
        {
            // Modify properties based on intensity level before generating
            ringRadius = 150 + intensity * 5;
            blocksPerRing = 20 + intensity * 5;
            InvalidateCache();
            return base.Spawn(intensity);
        }

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var trailDataList = new List<SpawnTrailData>();

            // The shared point will be at local origin
            Vector3 sharedPoint = Vector3.zero;

            // Define central axis for five-fold symmetry
            Vector3 centralAxis = Vector3.forward;

            // Angle for five-fold symmetry
            float angleStep = 360f / 5; // 72 degrees

            // Create the five rings
            for (int ringIndex = 0; ringIndex < 5; ringIndex++)
            {
                // Calculate the rotation around the central axis
                float rotationAngle = ringIndex * angleStep;

                // Create a base vector in XZ plane
                Vector3 baseVector = Mathf.Cos(Mathf.Deg2Rad * rotationAngle) * Vector3.right +
                                     Mathf.Sin(Mathf.Deg2Rad * rotationAngle) * Vector3.up;

                // Create rotation axis perpendicular to baseVector and centralAxis
                Vector3 planeNormal = Vector3.Cross(baseVector, centralAxis).normalized;

                // Ring center is exactly 1 radius away from shared point
                Vector3 ringCenter = sharedPoint + ringRadius * baseVector;

                // Generate ring points
                var points = GenerateRingPoints(sharedPoint, ringCenter, planeNormal);
                trailDataList.Add(new SpawnTrailData(points, true, domain));
            }

            return trailDataList.ToArray();
        }

        private SpawnPoint[] GenerateRingPoints(Vector3 sharedPoint, Vector3 ringCenter, Vector3 planeNormal)
        {
            var pointsList = new List<SpawnPoint>();

            // Vector from ring center to shared point (this is in the ring's plane)
            Vector3 toSharedPoint = (sharedPoint - ringCenter).normalized;

            // Vector perpendicular to both the normal and the toSharedPoint vector
            // This gives us the second basis vector in the ring's plane
            Vector3 perpVector = Vector3.Cross(planeNormal, toSharedPoint).normalized;

            // Create blocks around the ring
            for (int block = 0; block < blocksPerRing; block++)
            {
                // Calculate angle for this block - start at 0 so first point is at shared point
                float angle = (float)block / blocksPerRing * Mathf.PI * 2;

                if (Mathf.Cos(angle) > .8f) continue;

                // Calculate position on the ring using parametric equation of a circle
                Vector3 position = ringCenter +
                                   ringRadius * Mathf.Cos(angle) * toSharedPoint +
                                   ringRadius * Mathf.Sin(angle) * perpVector;

                // For the look direction, use the previous point (matching old nextBlock = (block - 1) % blocksPerRing)
                int nextBlock = (block - 1) % blocksPerRing;
                float nextAngle = (float)nextBlock / blocksPerRing * Mathf.PI * 2;

                Vector3 nextPosition = ringCenter +
                                       ringRadius * Mathf.Cos(nextAngle) * toSharedPoint +
                                       ringRadius * Mathf.Sin(nextAngle) * perpVector;

                // Old CreateBlock uses flip=true by default: forward = position - lookPosition
                var rotation = SpawnPoint.LookRotation(nextPosition, position, Vector3.up);

                pointsList.Add(new SpawnPoint(position, rotation, scale));
            }

            return pointsList.ToArray();
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seed, blocksPerRing, ringRadius, scale);
        }
    }
}
