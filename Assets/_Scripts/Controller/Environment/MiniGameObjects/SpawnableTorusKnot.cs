using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spawns prisms along a (p,q)-torus knot - a curve that winds p times around the
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

            // Strand phases are spread over ONE SYMMETRY PERIOD of the knot, 2π·gcd(p,q)/p, not
            // over the whole minor circle. Shifting the minor phase by a multiple of that period
            // gives back the SAME curve (t → t + 2πk/p moves the minor angle by 2πqk/p), so the
            // old 2π·s/strands put every strand of the shipped (3,5) knot with 3 strands on one
            // curve: three domains' prisms interleaved along a single tube, each clipping the
            // other two (Tools/Build/audit_prism_clipping.py). Inside one period every phase is a
            // distinct curve parallel to the rest on the torus, so the strands never meet.
            float period = 2f * Mathf.PI * Gcd(Mathf.Abs(p), Mathf.Abs(q)) / Mathf.Max(1, Mathf.Abs(p));
            for (int s = 0; s < strands; s++)
            {
                float strandOffset = period * s / strands;
                Domains strandDomain = colorByStrand && strandDomains.Length > 0
                    ? strandDomains[s % strandDomains.Length]
                    : domain;

                int n = blocksAlongKnot;
                var positions = new Vector3[n];
                var rotations = new Quaternion[n];
                for (int i = 0; i < n; i++)
                {
                    float t = 2f * Mathf.PI * i / n;
                    float tNext = 2f * Mathf.PI * (i + 1) / n;

                    Vector3 position = EvaluateKnot(t, strandOffset);
                    Vector3 nextPosition = EvaluateKnot(tNext, strandOffset);

                    positions[i] = position;
                    rotations[i] = SpawnPoint.LookRotation(nextPosition, position, Vector3.up);
                }

                // FLUSH, not overlapping (Docs/ECOSYSTEM.md 49.10). The strand is a closed loop of
                // blocksAlongKnot equal steps in t, and the knot's speed swings with cos(q t): on the
                // shipped (3,5) knot at 500 blocks the step runs 2.05-3.57 u against a 4 u block, so
                // every block crossed both neighbours. Each block keeps its cross-section and pose
                // and takes the longest length it can share with each neighbour and only touch
                // (FlushPairLength), the tighter side kept - the loop wraps, last block to first.
                // Measured: 1,500 clipping pairs -> 0, blocks 2.0-3.5 u long, volume -33% (the
                // step, not the block, was always the knot's real pitch).
                var points = new SpawnPoint[n];
                float prevPair = FlushPairLength(positions[n - 1], rotations[n - 1], positions[0], rotations[0]);
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    float nextPair = FlushPairLength(positions[i], rotations[i], positions[j], rotations[j]);
                    float length = Mathf.Min(blockScale.z, FlushMargin * Mathf.Min(prevPair, nextPair));
                    points[i] = new SpawnPoint(positions[i], rotations[i], new Vector3(blockScale.x, blockScale.y, length));
                    prevPair = nextPair;
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

        /// <summary>How far under exact contact a fitted block stops, so float noise in the poses
        /// can never flip two touching faces into a clip.</summary>
        const float FlushMargin = 0.998f;

        /// <summary>
        /// The longest length two neighbouring blocks (this shape's cross-section, +z down the
        /// knot) can BOTH take and still only touch: separated on the plane normal to the chord
        /// between their centres, each box reaches <c>half-length |z.n| + half-width |x.n| +
        /// half-height |y.n|</c> along it, and the two reaches may sum to the chord and no more.
        /// Closed form; for blocks laid down a curve the chord is the separating axis.
        /// </summary>
        float FlushPairLength(Vector3 pa, Quaternion ra, Vector3 pb, Quaternion rb)
        {
            Vector3 d = pb - pa;
            float dist = d.magnitude;
            if (dist < 1e-5f) return 0f;
            Vector3 n = d / dist;
            float lateral = 0.5f * blockScale.x * (Mathf.Abs(Vector3.Dot(ra * Vector3.right, n)) + Mathf.Abs(Vector3.Dot(rb * Vector3.right, n)))
                          + 0.5f * blockScale.y * (Mathf.Abs(Vector3.Dot(ra * Vector3.up, n)) + Mathf.Abs(Vector3.Dot(rb * Vector3.up, n)));
            float along = Mathf.Abs(Vector3.Dot(ra * Vector3.forward, n)) + Mathf.Abs(Vector3.Dot(rb * Vector3.forward, n));
            if (along < 1e-5f) return float.PositiveInfinity;
            return 2f * Mathf.Max(0f, dist - lateral) / along;
        }

        static int Gcd(int a, int b)
        {
            while (b != 0) (a, b) = (b, a % b);
            return Mathf.Max(1, a);
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
