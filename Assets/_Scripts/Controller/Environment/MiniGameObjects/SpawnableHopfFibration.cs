using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spawns prisms along fibers of the Hopf fibration: S³ → S².
    ///
    /// Each point on the 2-sphere has a corresponding great circle (fiber) on the 3-sphere.
    /// Points on the same latitude circle of S² produce fibers that form a torus in R³
    /// after stereographic projection. The result is nested, interlocking tori - one of
    /// the most beautiful structures in mathematics.
    ///
    /// The Hopf fibration is fundamental in gauge theory (it's the simplest nontrivial
    /// principal U(1)-bundle) and appears naturally in the geometry of spinors, Berry phase,
    /// and the Bloch sphere representation of qubits.
    /// </summary>
    public class SpawnableHopfFibration : SpawnableBase
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
                 "straight line and a circle - the axis and outermost ring of the structure.")]
        [SerializeField] bool includePolarFibers = true;

        [Tooltip("Add a Villarceau circle set - diagonal slices through the tori that reveal " +
                 "additional linked circles at oblique angles. Bridges between tori for connected paths.")]
        [SerializeField] bool includeVillarceauCircles = true;

        [SerializeField] int villarceauFibers = 4;
        [SerializeField] int villarceauBlocks = 28;

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var trailDataList = new List<SpawnTrailData>();
            var fibres = new List<(SpawnPoint[] points, float theta, float phi)>();

            // --- Main fibration: sample latitude bands on S² ---
            for (int band = 0; band < latitudeBands; band++)
            {
                // theta ∈ (0, π) - colatitude on S²
                // Exclude exact poles (handled separately) to avoid degenerate fibers
                float theta = Mathf.PI * (band + 1f) / (latitudeBands + 1f);

                Domains bandDomain = colorByLatitude && bandDomains.Length > 0
                    ? bandDomains[band % bandDomains.Length]
                    : domain;

                for (int fiber = 0; fiber < fibersPerBand; fiber++)
                {
                    // phi ∈ [0, 2π) - longitude on S²
                    float phi = 2f * Mathf.PI * fiber / fibersPerBand;

                    var points = GenerateFiberPoints(theta, phi, blocksPerFiber, blockScale);
                    if (points.Count > 0)
                    {
                        var fitted = FitFlushLoop(points);
                        fibres.Add((fitted, theta, phi));
                        trailDataList.Add(new SpawnTrailData(fitted, false, bandDomain));
                    }
                }
            }
            ClearFibreCrossings(fibres);

            // --- Polar fibers ---
            if (includePolarFibers)
            {
                // North pole (theta=0): projects to a circle at the "equator" of the projection
                var northPoints = GeneratePolarFiberPoints(isNorth: true);
                if (northPoints.Count > 0)
                    trailDataList.Add(new SpawnTrailData(FitFlushLoop(northPoints), false, Domains.Gold));

                // South pole (theta=π): projects to a line through the origin (the axis)
                var southPoints = GeneratePolarFiberPoints(isNorth: false);
                if (southPoints.Count > 0)
                    trailDataList.Add(new SpawnTrailData(FitFlushLoop(southPoints), false, Domains.Gold));
            }

            // --- Villarceau circles: oblique linked circles ---
            if (includeVillarceauCircles)
            {
                for (int v = 0; v < villarceauFibers; v++)
                {
                    float thetaV = Mathf.PI * 0.35f; // mid-latitude slice
                    float phiV = 2f * Mathf.PI * v / villarceauFibers;

                    var points = GenerateVillarceauFiberPoints(thetaV, phiV);
                    if (points.Count > 0)
                        trailDataList.Add(new SpawnTrailData(FitFlushLoop(points), false, Domains.Ruby));
                }
            }

            return trailDataList.ToArray();
        }

        /// <summary>
        /// Generate spawn points along a single Hopf fiber corresponding to the point (theta, phi) on S².
        ///
        /// The fiber parameterization:
        ///   z₁ = cos(θ/2) · e^{i(t + φ/2)}
        ///   z₂ = sin(θ/2) · e^{i(t - φ/2)}
        /// where t ∈ [0, 2π) traces the fiber circle on S³.
        ///
        /// We then stereographically project (x₁,x₂,x₃,x₄) ∈ S³ → R³.
        /// </summary>
        List<SpawnPoint> GenerateFiberPoints(float theta, float phi, int blockCount, Vector3 scale)
        {
            var points = new List<SpawnPoint>();

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

                // CreateBlock used flip=true (default), so forward = position - lookTarget
                var rotation = SpawnPoint.LookRotation(lookTarget, position, Vector3.up);

                points.Add(new SpawnPoint(position, rotation, scale));

                prevPosition = position;
            }

            return points;
        }

        /// <summary>
        /// Polar fibers are special cases:
        /// - North pole (θ=0): z₁ = e^{it}, z₂ = 0 → a great circle in the (x₁,x₂) plane
        /// - South pole (θ=π): z₁ = 0, z₂ = e^{it} → a great circle in the (x₃,x₄) plane
        /// After stereographic projection, one becomes a finite circle and the other
        /// passes through infinity (appears as a long line).
        /// </summary>
        List<SpawnPoint> GeneratePolarFiberPoints(bool isNorth)
        {
            var points = new List<SpawnPoint>();
            int count = blocksPerFiber * 2; // More blocks for the polar fibers since they're prominent

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

                // CreateBlock used flip=true (default), so forward = position - lookTarget
                var rotation = SpawnPoint.LookRotation(lookTarget, position, Vector3.up);

                // The south fibre lies in the (x3, x4) plane, which CONTAINS the projection centre
                // (0, 0, 0, projectionPole), so it does not project to a circle: it lands on the z
                // axis TWICE - once from the arc nearer than the pole (pole - x4 > 0) and once from
                // the arc beyond it - and the far arc's blocks sat on the near arc's line, two
                // pairs with coincident centres among them (the origin is hit at t = pi/2 and
                // 3pi/2). No length fixes a shared centre, so the far arc is shifted sideways by
                // one block width: the two passes lie side by side, faces touching, every block
                // still on its own z. Only that arc moves (23 of 128 blocks at the shipped 64 per
                // fibre); the north fibre's plane misses the centre and is a true circle.
                if (!isNorth && projectionPole - x4 < 0f)
                    position += (rotation * Vector3.right) * (blockScale.x * 1.5f);

                points.Add(new SpawnPoint(position, rotation, blockScale * 1.5f));
            }

            return points;
        }

        /// <summary>
        /// Villarceau circles are diagonal cross-sections of a torus that produce
        /// perfect circles. They reveal hidden symmetry: each Villarceau circle is
        /// itself a Hopf fiber, but from a rotated fibration. The effect is additional
        /// linked circles cutting through the tori at oblique angles.
        /// </summary>
        List<SpawnPoint> GenerateVillarceauFiberPoints(float theta, float phi)
        {
            var points = new List<SpawnPoint>();

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

                // CreateBlock used flip=true (default), so forward = position - lookTarget
                var rotation = SpawnPoint.LookRotation(lookTarget, position, Vector3.up);

                points.Add(new SpawnPoint(position, rotation, blockScale * 0.75f));
            }

            return points;
        }

        /// <summary>How far under exact contact a fitted block stops, so float noise in the poses
        /// can never flip two touching faces into a clip.</summary>
        const float FlushMargin = 0.998f;

        /// <summary>
        /// FLUSH, not overlapping (Docs/ECOSYSTEM.md 49.10). Every fibre is a closed loop of equal
        /// steps on S³, and stereographic projection stretches it: in R³ the blocks bunch where a
        /// fibre passes near the axis and spread where it swings wide, so a fixed-length block
        /// crossed its neighbours on the tight side of every torus (the polar circle's 9 u blocks
        /// sat 4.6 u apart). Each block keeps its cross-section and pose and takes the longest
        /// length it can share with each neighbour along its loop and only touch
        /// (<see cref="FlushPairLength"/>), the tighter side kept; the loop wraps, last to first.
        /// A neighbour across a projection's infinity is thousands of units away and asks nothing.
        /// </summary>
        static SpawnPoint[] FitFlushLoop(List<SpawnPoint> points)
        {
            int n = points.Count;
            var fitted = new SpawnPoint[n];
            if (n == 0) return fitted;
            float prevPair = FlushPairLength(points[n - 1], points[0]);
            for (int i = 0; i < n; i++)
            {
                var p = points[i];
                float nextPair = FlushPairLength(p, points[(i + 1) % n]);
                float length = Mathf.Min(p.Scale.z, FlushMargin * Mathf.Min(prevPair, nextPair));
                fitted[i] = new SpawnPoint(p.Position, p.Rotation, new Vector3(p.Scale.x, p.Scale.y, length));
                prevPair = nextPair;
            }
            return fitted;
        }

        /// <summary>
        /// Where two fibres of DIFFERENT bands pass each other. On S³ no two fibres meet, but the
        /// projection is not conformal from an interior pole, and it squeezes them where both cross
        /// the plane x3 = 0 (z = 0 in R³): fibre (θ, φ) crosses it at t = φ/2 ± π/2, at a radius of
        /// <c>s cos(θ/2) / (pole ∓ sin(θ/2))</c> on a bearing set by φ - so two bands whose
        /// crossing radii nearly agree, on fibres whose bearings line up, pass a block's width
        /// apart (the shipped prefab: bands 1 and 3 at 247.0 u and 244.8 u, two block pairs
        /// crossing at 71° by 0.03 u). Those crossings are found in closed form - 2 per fibre, a
        /// distance test per pair of them, nothing per block - and only the blocks nearest each
        /// close crossing are narrowed, just enough (<see cref="ClearCrossing"/>). A fibre whose
        /// projection dropped a sample is skipped (its indices no longer map to t).
        /// </summary>
        void ClearFibreCrossings(List<(SpawnPoint[] points, float theta, float phi)> fibres)
        {
            var crossings = new List<(int fibre, int index, Vector3 at)>(fibres.Count * 2);
            for (int f = 0; f < fibres.Count; f++)
            {
                var (points, theta, phi) = fibres[f];
                int n = points.Length;
                if (n != blocksPerFiber || n == 0) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    float t = Mathf.Repeat(phi * 0.5f + side * 0.5f * Mathf.PI, 2f * Mathf.PI);
                    int index = Mathf.RoundToInt(t * n / (2f * Mathf.PI)) % n;
                    crossings.Add((f, index, FiberPoint(theta, phi, t)));
                }
            }

            float reach = blockScale.magnitude;   // two half-diagonals: any closer pair may touch
            for (int a = 0; a < crossings.Count; a++)
            for (int b = a + 1; b < crossings.Count; b++)
            {
                var ca = crossings[a]; var cb = crossings[b];
                if (ca.fibre == cb.fibre || (ca.at - cb.at).sqrMagnitude > reach * reach) continue;
                var pa = fibres[ca.fibre].points; var pb = fibres[cb.fibre].points;
                for (int da = -1; da <= 1; da++)
                for (int db = -1; db <= 1; db++)
                    ClearCrossing(ref pa[(ca.index + da + pa.Length) % pa.Length],
                                  ref pb[(cb.index + db + pb.Length) % pb.Length]);
            }
        }

        /// <summary>
        /// Narrow two crossing blocks (x and y together, length untouched) by the smallest factor
        /// that leaves them only touching on one separating axis - the larger of the two closed
        /// forms: the common perpendicular of their long axes (where two blocks crossing at an
        /// angle actually meet; their lengths project to nothing on it) and the line between their
        /// centres (blocks nearly parallel). Leaves a pair that already clears alone. A narrower
        /// cross-section only loosens each block against its own fibre neighbours.
        /// </summary>
        static void ClearCrossing(ref SpawnPoint a, ref SpawnPoint b)
        {
            Vector3 d = b.Position - a.Position;
            Vector3 za = a.Rotation * Vector3.forward, zb = b.Rotation * Vector3.forward;
            float k1 = ClearFactor(a, b, d, Vector3.Cross(za, zb));
            float k2 = ClearFactor(a, b, d, d);
            if (k1 >= 1f || k2 >= 1f) return;                // already clear on one of them
            float best = Mathf.Max(k1, k2);
            if (best <= 0f) return;                          // no closed form separates them here
            float k = FlushMargin * best;
            a.Scale = new Vector3(a.Scale.x * k, a.Scale.y * k, a.Scale.z);
            b.Scale = new Vector3(b.Scale.x * k, b.Scale.y * k, b.Scale.z);
        }

        /// <summary>The factor on both blocks' cross-sections at which they only touch on
        /// <paramref name="raw"/>: what the separation along it leaves after their lengths' reach,
        /// over their cross-sections' reach. 0 when the axis is degenerate or cannot separate.</summary>
        static float ClearFactor(in SpawnPoint a, in SpawnPoint b, Vector3 d, Vector3 raw)
        {
            if (raw.sqrMagnitude < 1e-8f) return 0f;
            Vector3 m = raw.normalized;
            Quaternion ra = a.Rotation, rb = b.Rotation;
            float room = Mathf.Abs(Vector3.Dot(d, m))
                       - 0.5f * (a.Scale.z * Mathf.Abs(Vector3.Dot(ra * Vector3.forward, m)) + b.Scale.z * Mathf.Abs(Vector3.Dot(rb * Vector3.forward, m)));
            float need = 0.5f * (a.Scale.x * Mathf.Abs(Vector3.Dot(ra * Vector3.right, m)) + a.Scale.y * Mathf.Abs(Vector3.Dot(ra * Vector3.up, m))
                               + b.Scale.x * Mathf.Abs(Vector3.Dot(rb * Vector3.right, m)) + b.Scale.y * Mathf.Abs(Vector3.Dot(rb * Vector3.up, m)));
            if (need <= 1e-6f) return room >= 0f ? float.PositiveInfinity : 0f;
            return Mathf.Max(0f, room / need);
        }

        /// <summary>A main fibre's point at parameter t - <see cref="GenerateFiberPoints"/>'s map.</summary>
        Vector3 FiberPoint(float theta, float phi, float t)
        {
            float c = Mathf.Cos(theta * 0.5f), s = Mathf.Sin(theta * 0.5f), h = phi * 0.5f;
            return StereographicProject(c * Mathf.Cos(t + h), c * Mathf.Sin(t + h), s * Mathf.Cos(t - h), s * Mathf.Sin(t - h));
        }

        /// <summary>
        /// The longest length two neighbouring blocks (+z along the fibre) can BOTH take and still
        /// only touch: separated on the plane normal to the chord between their centres, each box
        /// reaches <c>half-length |z.n| + half-width |x.n| + half-height |y.n|</c> along it, and the
        /// two reaches may sum to the chord and no more. Closed form; for blocks laid down a curve
        /// the chord is the separating axis.
        /// </summary>
        static float FlushPairLength(SpawnPoint a, SpawnPoint b)
        {
            Vector3 d = b.Position - a.Position;
            float dist = d.magnitude;
            if (dist < 1e-5f) return 0f;
            Vector3 n = d / dist;
            Quaternion ra = a.Rotation, rb = b.Rotation;
            float lateral = 0.5f * (a.Scale.x * Mathf.Abs(Vector3.Dot(ra * Vector3.right, n)) + b.Scale.x * Mathf.Abs(Vector3.Dot(rb * Vector3.right, n)))
                          + 0.5f * (a.Scale.y * Mathf.Abs(Vector3.Dot(ra * Vector3.up, n)) + b.Scale.y * Mathf.Abs(Vector3.Dot(rb * Vector3.up, n)));
            float along = Mathf.Abs(Vector3.Dot(ra * Vector3.forward, n)) + Mathf.Abs(Vector3.Dot(rb * Vector3.forward, n));
            if (along < 1e-5f) return float.PositiveInfinity;
            return 2f * Mathf.Max(0f, dist - lateral) / along;
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(latitudeBands, fibersPerBand, blocksPerFiber, projectionScale,
                System.HashCode.Combine(projectionPole, includePolarFibers, includeVillarceauCircles,
                    villarceauFibers, villarceauBlocks, blockScale, seed));
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
