using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Spawns prisms along multiple great circles at different orientations, forming
    /// an interlocking cage of rings.
    ///
    /// Ring orientations are distributed using a Fibonacci spiral on the sphere for
    /// near-uniform coverage. The result looks like an armillary sphere — overlapping
    /// circular rails at every angle, perfect for finding creative flight paths through
    /// the intersections.
    /// </summary>
    public class SpawnableLinkedRings : SpawnableAbstractBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(2f, 2f, 4f);

        [Header("Ring Structure")]
        [Tooltip("Number of rings. Each ring is a great circle at a unique orientation.")]
        [SerializeField] int numberOfRings = 14;

        [Tooltip("Prism blocks per ring.")]
        [SerializeField] int blocksPerRing = 36;

        [Tooltip("Radius of each ring.")]
        [SerializeField] float ringRadius = 70f;

        [Header("Visual")]
        [SerializeField] bool colorByRing = true;
        [SerializeField] Domains[] ringDomains = new Domains[]
        {
            Domains.Blue,
            Domains.Jade,
            Domains.Gold,
            Domains.Ruby,
        };

        static int ObjectsSpawned = 0;

        public override GameObject Spawn()
        {
            var container = new GameObject($"LinkedRings{ObjectsSpawned++}");
            int blockIndex = 0;

            // Golden angle for near-uniform orientation distribution
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (int ring = 0; ring < numberOfRings; ring++)
            {
                // Fibonacci sphere: distribute ring normals uniformly
                float y = 1f - (2f * ring / (float)(numberOfRings - 1));
                float radiusAtY = Mathf.Sqrt(1f - y * y);
                float theta = goldenAngle * ring;

                Vector3 normal = new Vector3(
                    radiusAtY * Mathf.Cos(theta),
                    y,
                    radiusAtY * Mathf.Sin(theta)
                ).normalized;

                // Build a rotation that aligns the XZ plane to be perpendicular to this normal
                Quaternion ringRotation = Quaternion.FromToRotation(Vector3.up, normal);

                Domains ringDomain = colorByRing && ringDomains.Length > 0
                    ? ringDomains[ring % ringDomains.Length]
                    : domain;

                var trail = new Trail();
                trails.Add(trail);

                for (int i = 0; i < blocksPerRing; i++)
                {
                    float angle = 2f * Mathf.PI * i / blocksPerRing;
                    float angleNext = 2f * Mathf.PI * ((i + 1) % blocksPerRing) / blocksPerRing;

                    // Circle in the XZ plane, then rotate to the ring's orientation
                    Vector3 localPos = new Vector3(
                        ringRadius * Mathf.Cos(angle),
                        0f,
                        ringRadius * Mathf.Sin(angle)
                    );
                    Vector3 localNext = new Vector3(
                        ringRadius * Mathf.Cos(angleNext),
                        0f,
                        ringRadius * Mathf.Sin(angleNext)
                    );

                    Vector3 position = ringRotation * localPos;
                    Vector3 nextPosition = ringRotation * localNext;

                    CreateBlock(position, nextPosition,
                        $"{container.name}::RING{ring}::{blockIndex}",
                        trail, blockScale, prism, container, ringDomain);

                    blockIndex++;
                }
            }

            return container;
        }

        public override GameObject Spawn(int intensityLevel)
        {
            return Spawn();
        }
    }
}
