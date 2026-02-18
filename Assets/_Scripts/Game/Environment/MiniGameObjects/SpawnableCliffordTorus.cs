using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Spawns prisms on the Clifford torus — a flat torus embedded in the 3-sphere S³,
    /// then stereographically projected to R³.
    ///
    /// The Clifford torus is the set:
    ///   { (cos u, sin u, cos v, sin v) / √2  :  u,v ∈ [0, 2π) }  ⊂  S³ ⊂ R⁴
    ///
    /// Unlike an ordinary torus in R³, the Clifford torus has zero intrinsic curvature —
    /// it's genuinely flat, like a sheet of paper rolled into a donut shape in 4D.
    /// After stereographic projection it becomes a Dupin cyclide — a smooth, swoopy
    /// surface perfect for carving along.
    ///
    /// This is fundamentally different from the Hopf fibration: the Hopf spawner places
    /// blocks along individual fibers (circles), while this places blocks on the 2D
    /// surface that those fibers collectively trace out.
    /// </summary>
    public class SpawnableCliffordTorus : SpawnableAbstractBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(2f, 2f, 2f);

        [Header("Surface Resolution")]
        [Tooltip("Samples around the first circle (u direction).")]
        [SerializeField] int uSamples = 36;

        [Tooltip("Samples around the second circle (v direction).")]
        [SerializeField] int vSamples = 36;

        [Header("Projection")]
        [Tooltip("Scale of the stereographic projection.")]
        [SerializeField] float projectionScale = 80f;

        [Tooltip("Projection pole offset. Controls shape distortion.")]
        [Range(0.01f, 0.99f)]
        [SerializeField] float projectionPole = 0.8f;

        [Header("Visual")]
        [Tooltip("Color blocks based on position along the v parameter for visual flow.")]
        [SerializeField] bool colorByV = true;
        [SerializeField] Domains[] vDomains = new Domains[]
        {
            Domains.Blue,
            Domains.Jade,
            Domains.Gold,
            Domains.Ruby,
        };

        static int ObjectsSpawned = 0;

        public override GameObject Spawn()
        {
            var container = new GameObject($"CliffordTorus{ObjectsSpawned++}");
            int blockIndex = 0;

            float invSqrt2 = 1f / Mathf.Sqrt(2f);

            for (int iu = 0; iu < uSamples; iu++)
            {
                float u = 2f * Mathf.PI * iu / uSamples;

                for (int iv = 0; iv < vSamples; iv++)
                {
                    float v = 2f * Mathf.PI * iv / vSamples;

                    // Clifford torus in S³
                    float x1 = invSqrt2 * Mathf.Cos(u);
                    float x2 = invSqrt2 * Mathf.Sin(u);
                    float x3 = invSqrt2 * Mathf.Cos(v);
                    float x4 = invSqrt2 * Mathf.Sin(v);

                    Vector3 position = StereographicProject(x1, x2, x3, x4);

                    if (float.IsInfinity(position.x) || float.IsNaN(position.x))
                        continue;

                    // Compute neighbor for orientation
                    float uNext = 2f * Mathf.PI * (iu + 1) / uSamples;
                    Vector3 neighbor = StereographicProject(
                        invSqrt2 * Mathf.Cos(uNext),
                        invSqrt2 * Mathf.Sin(uNext),
                        invSqrt2 * Mathf.Cos(v),
                        invSqrt2 * Mathf.Sin(v)
                    );

                    if (float.IsInfinity(neighbor.x) || float.IsNaN(neighbor.x))
                        neighbor = position + Vector3.forward;

                    // Color bands by v-parameter
                    int bandIndex = iv * vDomains.Length / vSamples;
                    Domains blockDomain = colorByV && vDomains.Length > 0
                        ? vDomains[bandIndex % vDomains.Length]
                        : domain;

                    var trail = GetOrCreateTrail(iu);

                    CreateBlock(position, neighbor,
                        $"{container.name}::SURFACE::{blockIndex}",
                        trail, blockScale, prism, container, blockDomain);

                    blockIndex++;
                }
            }

            return container;
        }

        public override GameObject Spawn(int intensityLevel)
        {
            return Spawn();
        }

        Trail GetOrCreateTrail(int index)
        {
            while (trails.Count <= index)
                trails.Add(new Trail());
            return trails[index];
        }

        Vector3 StereographicProject(float x1, float x2, float x3, float x4)
        {
            float denominator = projectionPole - x4;

            if (Mathf.Abs(denominator) < 0.01f)
                denominator = 0.01f * Mathf.Sign(denominator);

            float invDenom = projectionScale / denominator;
            return new Vector3(x1 * invDenom, x2 * invDenom, x3 * invDenom);
        }
    }
}
