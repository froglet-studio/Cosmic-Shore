using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Spawns prisms along a (p,q)-torus knot — a curve that winds p times around the
    /// hole of a torus and q times through it.
    ///
    /// Torus knots are the simplest nontrivial knots and produce beautiful braided paths
    /// that are perfect for skimming. A (2,3) trefoil creates a three-lobed clover;
    /// a (3,5) creates a dense weave. Multiple strands at rotational offsets fill the
    /// arena with parallel rails.
    ///
    /// The parametrization on a torus (R, r):
    ///   x(t) = (R + r·cos(q·t)) · cos(p·t)
    ///   y(t) = (R + r·cos(q·t)) · sin(p·t)
    ///   z(t) = r · sin(q·t)
    /// where t ∈ [0, 2π) traces one full period when gcd(p,q) = 1.
    /// </summary>
    public class SpawnableTorusKnot : SpawnableBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(2f, 2f, 4f);

        [Header("Knot Parameters")]
        [Tooltip("Winds around the torus hole. Together with q, defines the knot type. " +
                 "gcd(p,q) must be 1 for a true knot.")]
        [SerializeField] int p = 3;

        [Tooltip("Winds through the torus hole.")]
        [SerializeField] int q = 5;

        [Header("Torus Dimensions")]
        [SerializeField] float majorRadius = 60f;
        [SerializeField] float minorRadius = 25f;

        [Header("Density")]
        [Tooltip("Total prism blocks along the knot curve.")]
        [SerializeField] int blocksAlongKnot = 500;

        [Tooltip("Number of parallel strands offset around the minor circle. " +
                 "Creates parallel rails for richer paths.")]
        [SerializeField] int strands = 3;

        [Header("Visual")]
        [SerializeField] bool colorByStrand = true;
        [SerializeField] Domains[] strandDomains = new Domains[]
        {
            Domains.Blue,
            Domains.Jade,
            Domains.Gold,
        };

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var trailDataList = new List<SpawnTrailData>();

            for (int s = 0; s < strands; s++)
            {
                float strandOffset = 2f * Mathf.PI * s / strands;
                Domains strandDomain = colorByStrand && strandDomains.Length > 0
                    ? strandDomains[s % strandDomains.Length]
                    : domain;

                var points = new SpawnPoint[blocksAlongKnot];

                for (int i = 0; i < blocksAlongKnot; i++)
                {
                    float t = 2f * Mathf.PI * i / blocksAlongKnot;
                    float tNext = 2f * Mathf.PI * (i + 1) / blocksAlongKnot;

                    Vector3 position = EvaluateKnot(t, strandOffset);
                    Vector3 nextPosition = EvaluateKnot(tNext, strandOffset);

                    var rotation = SpawnPoint.LookRotation(nextPosition, position, Vector3.up);

                    points[i] = new SpawnPoint(position, rotation, blockScale);
                }

                trailDataList.Add(new SpawnTrailData(points, false, strandDomain));
            }

            return trailDataList.ToArray();
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(p, q, majorRadius, minorRadius, blocksAlongKnot, strands, seed, blockScale);
        }

        Vector3 EvaluateKnot(float t, float strandOffset)
        {
            float r = majorRadius + minorRadius * Mathf.Cos(q * t + strandOffset);
            return new Vector3(
                r * Mathf.Cos(p * t),
                r * Mathf.Sin(p * t),
                minorRadius * Mathf.Sin(q * t + strandOffset)
            );
        }
    }
}
