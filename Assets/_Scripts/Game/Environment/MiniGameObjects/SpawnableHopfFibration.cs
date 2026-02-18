using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Spawns prisms along fibers of the Hopf fibration: S³ → S².
    ///
    /// Each point on the 2-sphere has a corresponding great circle (fiber) on the 3-sphere.
    /// Points on the same latitude circle of S² produce fibers that form a torus in R³
    /// after stereographic projection. The result is nested, interlocking tori — one of
    /// the most beautiful structures in mathematics.
    ///
    /// The Hopf fibration is fundamental in gauge theory (it's the simplest nontrivial
    /// principal U(1)-bundle) and appears naturally in the geometry of spinors, Berry phase,
    /// and the Bloch sphere representation of qubits.
    /// </summary>
    public class SpawnableHopfFibration : SpawnableAbstractBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(2f, 2f, 4f);

        [Header("Fibration Structure")]
        [Tooltip("Number of latitude bands on S² to sample fibers from. Each band produces a torus.")]
        [SerializeField] int latitudeBands = 6;

        [Tooltip("Number of fibers per latitude band. More fibers = denser tori.")]
        [SerializeField] int fibersPerBand = 20;

        [Tooltip("Number of prism blocks along each fiber circle.")]
        [SerializeField] int blocksPerFiber = 28;

        [Tooltip("Radius of the overall structure after stereographic projection.")]
        [SerializeField] float projectionScale = 80f;

        [Tooltip("Offset the stereographic projection pole to avoid singularities. " +
                 "Values near 1.0 produce tighter inner tori; values near 0 spread everything out.")]
        [Range(0.01f, 0.99f)]
        [SerializeField] float projectionPole = 0.85f;

        [Header("Visual")]
        [Tooltip("Color each latitude band with a different domain for visual distinction.")]
        [SerializeField] bool colorByLatitude = true;

        [SerializeField]
        Domains[] bandDomains = new Domains[]
        {
            Domains.Blue,
            Domains.Jade,
            Domains.Gold,
            Domains.Ruby,
            Domains.Blue,
            Domains.Jade,
        };

        [Header("Extras")]
        [Tooltip("Include the polar fibers (north and south pole of S²). These project to a " +
                 "straight line and a circle — the axis and outermost ring of the structure.")]
        [SerializeField] bool includePolarFibers = true;

        [Tooltip("Add a Villarceau circle set — diagonal slices through the tori that reveal " +
                 "additional linked circles at oblique angles. Bridges between tori for connected paths.")]
        [SerializeField] bool includeVillarceauCircles = true;

        [SerializeField] int villarceauFibers = 4;
        [SerializeField] int villarceauBlocks = 28;

        static int ObjectsSpawned = 0;

        public override GameObject Spawn()
        {
            GameObject container = new GameObject();
            container.name = "HopfFibration" + ObjectsSpawned++;

            int trailIndex = 0;

            // --- Main fibration: sample latitude bands on S² ---
            for (int band = 0; band < latitudeBands; band++)
            {
                // theta ∈ (0, π) — colatitude on S²
                // Exclude exact poles (handled separately) to avoid degenerate fibers
                float theta = Mathf.PI * (band + 1f) / (latitudeBands + 1f);

                Domains bandDomain = colorByLatitude && bandDomains.Length > 0
                    ? bandDomains[band % bandDomains.Length]
                    : domain;

                for (int fiber = 0; fiber < fibersPerBand; fiber++)
                {
                    // phi ∈ [0, 2π) — longitude on S²
                    float phi = 2f * Mathf.PI * fiber / fibersPerBand;

                    var trail = new Trail();
                    trails.Add(trail);

                    SpawnFiber(container, trail, theta, phi, blocksPerFiber, blockScale, bandDomain, ref trailIndex);
                }
            }

            // --- Polar fibers ---
            if (includePolarFibers)
            {
                // North pole (theta=0): projects to a circle at the "equator" of the projection
                var northTrail = new Trail();
                trails.Add(northTrail);
                SpawnPolarFiber(container, northTrail, isNorth: true, ref trailIndex);

                // South pole (theta=π): projects to a line through the origin (the axis)
                var southTrail = new Trail();
                trails.Add(southTrail);
                SpawnPolarFiber(container, southTrail, isNorth: false, ref trailIndex);
            }

            // --- Villarceau circles: oblique linked circles ---
            if (includeVillarceauCircles)
            {
                for (int v = 0; v < villarceauFibers; v++)
                {
                    float thetaV = Mathf.PI * 0.35f; // mid-latitude slice
                    float phiV = 2f * Mathf.PI * v / villarceauFibers;

                    // Tilt the fiber sampling to get Villarceau-style diagonal circles
                    var trail = new Trail();
                    trails.Add(trail);

                    SpawnVillarceauFiber(container, trail, thetaV, phiV, ref trailIndex);
                }
            }

            return container;
        }

        public override GameObject Spawn(int intensityLevel)
        {
            return Spawn();
        }

        /// <summary>
        /// Spawn prisms along a single Hopf fiber corresponding to the point (theta, phi) on S².
        ///
        /// The fiber parameterization:
        ///   z₁ = cos(θ/2) · e^{i(t + φ/2)}
        ///   z₂ = sin(θ/2) · e^{i(t - φ/2)}
        /// where t ∈ [0, 2π) traces the fiber circle on S³.
        ///
        /// We then stereographically project (x₁,x₂,x₃,x₄) ∈ S³ → R³.
        /// </summary>
        void SpawnFiber(GameObject container, Trail trail, float theta, float phi,
            int blockCount, Vector3 scale, Domains fiberDomain, ref int blockIndex)
        {
            float cosHalfTheta = Mathf.Cos(theta * 0.5f);
            float sinHalfTheta = Mathf.Sin(theta * 0.5f);
            float halfPhi = phi * 0.5f;

            Vector3 prevPosition = Vector3.zero;

            for (int i = 0; i < blockCount; i++)
            {
                float t = 2f * Mathf.PI * i / blockCount;

                // S³ coordinates via Hopf fiber parameterization
                // z₁ = cos(θ/2) · e^{i(t + φ/2)},  z₂ = sin(θ/2) · e^{i(t - φ/2)}
                // Writing z₁ = x₁ + ix₂, z₂ = x₃ + ix₄:
                float x1 = cosHalfTheta * Mathf.Cos(t + halfPhi);
                float x2 = cosHalfTheta * Mathf.Sin(t + halfPhi);
                float x3 = sinHalfTheta * Mathf.Cos(t - halfPhi);
                float x4 = sinHalfTheta * Mathf.Sin(t - halfPhi);

                Vector3 position = StereographicProject(x1, x2, x3, x4);

                if (float.IsInfinity(position.x) || float.IsNaN(position.x))
                    continue;

                Vector3 lookTarget;
                if (i == 0)
                {
                    // Peek at next position for first block orientation
                    float tNext = 2f * Mathf.PI / blockCount;
                    float nx1 = cosHalfTheta * Mathf.Cos(tNext + halfPhi);
                    float nx2 = cosHalfTheta * Mathf.Sin(tNext + halfPhi);
                    float nx3 = sinHalfTheta * Mathf.Cos(tNext - halfPhi);
                    float nx4 = sinHalfTheta * Mathf.Sin(tNext - halfPhi);
                    lookTarget = StereographicProject(nx1, nx2, nx3, nx4);
                }
                else
                {
                    lookTarget = prevPosition;
                }

                CreateBlock(position, lookTarget,
                    $"{container.name}::FIBER::{blockIndex}",
                    trail, scale, prism, container, fiberDomain);

                prevPosition = position;
                blockIndex++;
            }
        }

        /// <summary>
        /// Polar fibers are special cases:
        /// - North pole (θ=0): z₁ = e^{it}, z₂ = 0 → a great circle in the (x₁,x₂) plane
        /// - South pole (θ=π): z₁ = 0, z₂ = e^{it} → a great circle in the (x₃,x₄) plane
        /// After stereographic projection, one becomes a finite circle and the other
        /// passes through infinity (appears as a long line).
        /// </summary>
        void SpawnPolarFiber(GameObject container, Trail trail, bool isNorth, ref int blockIndex)
        {
            int count = blocksPerFiber * 2; // More blocks for the polar fibers since they're prominent
            Domains polarDomain = Domains.Gold;

            for (int i = 0; i < count; i++)
            {
                float t = 2f * Mathf.PI * i / count;

                float x1, x2, x3, x4;
                if (isNorth)
                {
                    x1 = Mathf.Cos(t);
                    x2 = Mathf.Sin(t);
                    x3 = 0f;
                    x4 = 0f;
                }
                else
                {
                    x1 = 0f;
                    x2 = 0f;
                    x3 = Mathf.Cos(t);
                    x4 = Mathf.Sin(t);
                }

                Vector3 position = StereographicProject(x1, x2, x3, x4);

                if (float.IsInfinity(position.x) || float.IsNaN(position.x))
                    continue;

                // Look toward next point
                float tNext = 2f * Mathf.PI * ((i + 1) % count) / count;
                float nx1, nx2, nx3, nx4;
                if (isNorth)
                {
                    nx1 = Mathf.Cos(tNext); nx2 = Mathf.Sin(tNext); nx3 = 0f; nx4 = 0f;
                }
                else
                {
                    nx1 = 0f; nx2 = 0f; nx3 = Mathf.Cos(tNext); nx4 = Mathf.Sin(tNext);
                }
                Vector3 lookTarget = StereographicProject(nx1, nx2, nx3, nx4);

                if (float.IsInfinity(lookTarget.x) || float.IsNaN(lookTarget.x))
                    lookTarget = position + Vector3.forward;

                CreateBlock(position, lookTarget,
                    $"{container.name}::POLAR::{blockIndex}",
                    trail, blockScale * 1.5f, prism, container, polarDomain);

                blockIndex++;
            }
        }

        /// <summary>
        /// Villarceau circles are diagonal cross-sections of a torus that produce
        /// perfect circles. They reveal hidden symmetry: each Villarceau circle is
        /// itself a Hopf fiber, but from a rotated fibration. The effect is additional
        /// linked circles cutting through the tori at oblique angles.
        /// </summary>
        void SpawnVillarceauFiber(GameObject container, Trail trail,
            float theta, float phi, ref int blockIndex)
        {
            float cosHalfTheta = Mathf.Cos(theta * 0.5f);
            float sinHalfTheta = Mathf.Sin(theta * 0.5f);

            // Apply a rotation in S³ to get Villarceau-type circles
            // This is a right-isoclinic rotation: (z₁, z₂) → (z₁·e^{iα}, z₂·e^{iβ})
            // with α ≠ β, which maps fibers of one Hopf fibration to fibers of a different one
            float alpha = phi;
            float beta = phi * 1.618f; // Golden ratio offset for maximal visual variety

            for (int i = 0; i < villarceauBlocks; i++)
            {
                float t = 2f * Mathf.PI * i / villarceauBlocks;

                float x1 = cosHalfTheta * Mathf.Cos(t + alpha);
                float x2 = cosHalfTheta * Mathf.Sin(t + alpha);
                float x3 = sinHalfTheta * Mathf.Cos(t + beta);
                float x4 = sinHalfTheta * Mathf.Sin(t + beta);

                Vector3 position = StereographicProject(x1, x2, x3, x4);

                if (float.IsInfinity(position.x) || float.IsNaN(position.x))
                    continue;

                float tNext = 2f * Mathf.PI * ((i + 1) % villarceauBlocks) / villarceauBlocks;
                Vector3 lookTarget = StereographicProject(
                    cosHalfTheta * Mathf.Cos(tNext + alpha),
                    cosHalfTheta * Mathf.Sin(tNext + alpha),
                    sinHalfTheta * Mathf.Cos(tNext + beta),
                    sinHalfTheta * Mathf.Sin(tNext + beta)
                );

                if (float.IsInfinity(lookTarget.x) || float.IsNaN(lookTarget.x))
                    lookTarget = position + Vector3.forward;

                CreateBlock(position, lookTarget,
                    $"{container.name}::VILLARCEAU::{blockIndex}",
                    trail, blockScale * 0.75f, prism, container, Domains.Ruby);

                blockIndex++;
            }
        }

        /// <summary>
        /// Stereographic projection from S³ ⊂ R⁴ to R³.
        /// Projects from the pole (0,0,0,poleW) where poleW is set by projectionPole.
        ///
        /// The formula: (x₁,x₂,x₃,x₄) → scale · (x₁, x₂, x₃) / (poleW - x₄)
        ///
        /// This is conformal (angle-preserving) and maps circles on S³ to circles in R³,
        /// which is why the Hopf fibers remain perfect circles after projection.
        /// </summary>
        Vector3 StereographicProject(float x1, float x2, float x3, float x4)
        {
            float denominator = projectionPole - x4;

            // Clamp to avoid near-infinity projections near the pole
            if (Mathf.Abs(denominator) < 0.01f)
                denominator = 0.01f * Mathf.Sign(denominator);

            float invDenom = projectionScale / denominator;
            return new Vector3(x1 * invDenom, x2 * invDenom, x3 * invDenom);
        }
    }
}
