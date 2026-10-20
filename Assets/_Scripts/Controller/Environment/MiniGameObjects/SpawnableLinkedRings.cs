using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spawns prisms along multiple great circles at different orientations, forming
    /// an interlocking cage of rings.
    ///
    /// Ring orientations are distributed using a Fibonacci spiral on the sphere for
    /// near-uniform coverage. The result looks like an armillary sphere - overlapping
    /// circular rails at every angle, perfect for finding creative flight paths through
    /// the intersections.
    /// </summary>
    public class SpawnableLinkedRings : SpawnableBase
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

        /// <summary>Blocks are sized and placed to this fraction of their exact flush clearance,
        /// so float noise never turns two faces that touch into a clip.</summary>
        const float FlushMargin = 0.995f;

        /// <summary>The smallest axis the prism prefab lays (its PrismScaleAnimator window grows
        /// anything thinner back to this), so a block is never shortened under it.</summary>
        const float MinPrismAxis = 0.5f;

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var rings = new SpawnPoint[numberOfRings][];
            var normals = new Vector3[numberOfRings];
            var ringDomainList = new Domains[numberOfRings];

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

                ringDomainList[ring] = colorByRing && ringDomains.Length > 0
                    ? ringDomains[ring % ringDomains.Length]
                    : domain;

                var points = new SpawnPoint[blocksPerRing];

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

                    // CreateBlock used flip=true (default), so forward = position - nextPosition
                    var rotation = SpawnPoint.LookRotation(nextPosition, position, Vector3.up);

                    points[i] = new SpawnPoint(position, rotation, blockScale);
                }

                rings[ring] = points;
                normals[ring] = normal;
            }

            var planeOffsets = SeparateCoplanarRings(rings, normals);
            // A slide can carry a block onto a third ring's block at the same crossing, so sweep
            // again until a sweep parts nothing.
            var slid = new int[numberOfRings][];
            for (int ring = 0; ring < numberOfRings; ring++) slid[ring] = new int[blocksPerRing];
            for (int sweep = 0; sweep < MaxSweeps && ResolveCrossings(rings, normals, planeOffsets, slid); sweep++) { }

            var trailDataList = new SpawnTrailData[numberOfRings];
            for (int ring = 0; ring < numberOfRings; ring++)
                trailDataList[ring] = new SpawnTrailData(rings[ring], false, ringDomainList[ring]);
            return trailDataList;
        }

        /// <summary>
        /// The Fibonacci spiral puts its first and last ring normals on the two poles, and a great
        /// circle with normal -n is the circle with normal n: the shipped 14 rings laid ring 13
        /// exactly on ring 0, every block on a block (36 coincident pairs). A ring that repeats an
        /// earlier ring's plane is laid beside it instead, offset along the normal by exactly the two
        /// rings' reach along it so the rails lie flush side by side - the smallest move that parts
        /// them (end to end along the ring would be twice as far).
        /// </summary>
        float[] SeparateCoplanarRings(SpawnPoint[][] rings, Vector3[] normals)
        {
            var shift = new float[rings.Length];          // each ring's offset along its own normal
            for (int r = 1; r < rings.Length; r++)
            {
                for (int q = 0; q < r; q++)
                {
                    float cos = Vector3.Dot(normals[r], normals[q]);
                    if (Mathf.Abs(cos) < 1f - 1e-5f) continue;
                    // Along ring q's normal: beyond q's own offset by the two rails' reach.
                    float gap = (ReachAlong(rings[q], normals[q]) + ReachAlong(rings[r], normals[q])) / FlushMargin;
                    shift[r] = (shift[q] + gap) * Mathf.Sign(cos);
                }
                if (shift[r] == 0f) continue;
                Vector3 offset = normals[r] * shift[r];
                for (int i = 0; i < rings[r].Length; i++) rings[r][i].Position += offset;
            }
            return shift;                                   // each ring's plane: dot(x, normal) = shift
        }

        /// <summary>How far a ring's blocks reach out of its plane along a direction (the most of any).</summary>
        static float ReachAlong(SpawnPoint[] ring, Vector3 n)
        {
            float reach = 0f;
            foreach (var p in ring) reach = Mathf.Max(reach, new Box(p).Support(n));
            return reach;
        }

        /// <summary>
        /// Every two rings cross twice, and at a crossing the blocks of both rails can run through
        /// each other (78 clipping pairs in the shipped prefab beside the coplanar ring). Per
        /// crossing pair, the cheapest flush fix, from the poses alone:
        ///
        ///   - shorten ONE of the two along its rail, just enough to part it from the other - the
        ///     one that keeps more of its volume; the other keeps blockScale;
        ///   - where neither can be shortened enough (both straddle the crossing point, so each
        ///     is centred inside the other's rail), slide the one that has less far to go along
        ///     its own rail, away from the crossing, just far enough to part them. A ring's blocks
        ///     are spaced three block lengths apart, so a slide never reaches the next block.
        ///
        /// Only blocks within reach of another ring's plane are compared, so this is a handful of
        /// pairs per crossing, not every pair of blocks. Returns whether it parted any.
        /// </summary>
        bool ResolveCrossings(SpawnPoint[][] rings, Vector3[] normals, float[] offsets, int[][] slid)
        {
            bool parted = false;
            var boxes = new Box[rings.Length][];
            for (int r = 0; r < rings.Length; r++)
            {
                boxes[r] = new Box[rings[r].Length];
                for (int i = 0; i < rings[r].Length; i++) boxes[r][i] = new Box(rings[r][i]);
            }
            var nearP = new List<int>();
            var nearQ = new List<int>();
            for (int p = 0; p < rings.Length; p++)
            {
                for (int q = p + 1; q < rings.Length; q++)
                {
                    // The blocks of each ring that reach the other ring's rail.
                    Vector3 np = normals[p], nq = normals[q];
                    float reachP = 0f, reachQ = 0f;
                    foreach (var box in boxes[p]) reachP = Mathf.Max(reachP, box.Support(nq));
                    foreach (var box in boxes[q]) reachQ = Mathf.Max(reachQ, box.Support(np));
                    nearP.Clear(); nearQ.Clear();
                    for (int i = 0; i < boxes[p].Length; i++)
                        if (Mathf.Abs(Vector3.Dot(boxes[p][i].Center, nq) - offsets[q]) <= boxes[p][i].Support(nq) + reachQ) nearP.Add(i);
                    for (int j = 0; j < boxes[q].Length; j++)
                        if (Mathf.Abs(Vector3.Dot(boxes[q][j].Center, np) - offsets[p]) <= boxes[q][j].Support(np) + reachP) nearQ.Add(j);

                    foreach (int i in nearP)
                        foreach (int j in nearQ)
                        {
                            if (!Box.Clips(boxes[p][i], boxes[q][j])) continue;
                            Part(ref rings[p][i], ref rings[q][j], ref slid[p][i], ref slid[q][j]);
                            boxes[p][i] = new Box(rings[p][i]);
                            boxes[q][j] = new Box(rings[q][j]);
                            parted = true;
                        }
                }
            }
            return parted;
        }

        /// <summary>At most this many crossing sweeps.</summary>
        const int MaxSweeps = 4;

        /// <summary>Part two clipping blocks: shorten the one that loses less, else slide the one
        /// that moves less (see ResolveCrossings). A block slides one way only (slid holds the way
        /// it went, +1 or -1 along its length): where three rings meet, a block between two others
        /// would otherwise be pushed back and forth, so the second of them yields instead.</summary>
        static void Part(ref SpawnPoint pa, ref SpawnPoint pb, ref int slidA, ref int slidB)
        {
            var a = new Box(pa);
            var b = new Box(pb);
            float ka = Box.LengthFactor(a, b), kb = Box.LengthFactor(b, a);
            float lossA = (1f - ka) * Volume(pa.Scale), lossB = (1f - kb) * Volume(pb.Scale);
            bool canA = ka * Mathf.Abs(pa.Scale.z) >= MinPrismAxis, canB = kb * Mathf.Abs(pb.Scale.z) >= MinPrismAxis;
            if (canA && (!canB || lossA <= lossB)) { pa.Scale.z *= ka; return; }
            if (canB) { pb.Scale.z *= kb; return; }

            int wayA = Vector3.Dot(a.Center - b.Center, a.Z) >= 0f ? 1 : -1;
            int wayB = Vector3.Dot(b.Center - a.Center, b.Z) >= 0f ? 1 : -1;
            float ta = slidA == -wayA ? float.PositiveInfinity : Box.SlideDistance(a, b);
            float tb = slidB == -wayB ? float.PositiveInfinity : Box.SlideDistance(b, a);
            if (ta <= tb) { pa.Position += a.Z * (wayA * ta); slidA = wayA; }
            else { pb.Position += b.Z * (wayB * tb); slidB = wayB; }
        }

        static float Volume(Vector3 s) => Mathf.Abs(s.x * s.y * s.z);

        /// <summary>A block as its oriented box.</summary>
        readonly struct Box
        {
            public readonly Vector3 Center, X, Y, Z, Half;
            public Box(in SpawnPoint p)
            {
                Center = p.Position;
                X = p.Rotation * Vector3.right; Y = p.Rotation * Vector3.up; Z = p.Rotation * Vector3.forward;
                Half = 0.5f * new Vector3(Mathf.Abs(p.Scale.x), Mathf.Abs(p.Scale.y), Mathf.Abs(p.Scale.z));
            }

            public float Support(Vector3 l) =>
                Mathf.Abs(Vector3.Dot(X, l)) * Half.x + Mathf.Abs(Vector3.Dot(Y, l)) * Half.y + Mathf.Abs(Vector3.Dot(Z, l)) * Half.z;

            Vector3 Axis(int i) => i == 0 ? X : i == 1 ? Y : Z;

            /// <summary>The 15 separating-axis candidates of two boxes.</summary>
            static Vector3 Candidate(int i, in Box a, in Box b) =>
                i < 3 ? a.Axis(i) : i < 6 ? b.Axis(i - 3) : Vector3.Cross(a.Axis((i - 6) / 3), b.Axis((i - 6) % 3));

            public static bool Clips(in Box a, in Box b)
            {
                Vector3 d = b.Center - a.Center;
                for (int i = 0; i < 15; i++)
                {
                    Vector3 l = Candidate(i, a, b);
                    if (l.sqrMagnitude < 1e-10f) continue;
                    if (Mathf.Abs(Vector3.Dot(d, l)) >= a.Support(l) + b.Support(l)) return false;
                }
                return true;
            }

            /// <summary>The largest factor (at most 1) on a's length, its z, at which a is flush
            /// with b - closed form: on each candidate axis the clearance is linear in it.</summary>
            public static float LengthFactor(in Box a, in Box b)
            {
                Vector3 d = b.Center - a.Center;
                float best = 0f;
                for (int i = 0; i < 15; i++)
                {
                    Vector3 l = Candidate(i, a, b);
                    if (l.sqrMagnitude < 1e-10f) continue;
                    float length = Mathf.Abs(Vector3.Dot(a.Z, l)) * a.Half.z;
                    float gap = Mathf.Abs(Vector3.Dot(d, l)) - b.Support(l) - (a.Support(l) - length);
                    if (gap < 0f) continue;
                    if (length <= gap) return 1f;
                    best = Mathf.Max(best, gap / length);
                }
                return best * FlushMargin;
            }

            /// <summary>How far a must slide along its own length, away from b, to part from it -
            /// closed form: on each candidate axis the clearance grows linearly with the slide.</summary>
            public static float SlideDistance(in Box a, in Box b)
            {
                Vector3 d = b.Center - a.Center;
                Vector3 dir = a.Z * -Mathf.Sign(Vector3.Dot(d, a.Z));      // a moves this way
                float best = float.PositiveInfinity;
                for (int i = 0; i < 15; i++)
                {
                    Vector3 l = Candidate(i, a, b);
                    if (l.sqrMagnitude < 1e-10f) continue;
                    float dl = Vector3.Dot(d, l), rate = -Vector3.Dot(dir, l) * Mathf.Sign(dl == 0f ? 1f : dl);
                    if (rate <= 1e-6f) continue;                             // this axis does not open up
                    float need = a.Support(l) + b.Support(l) - Mathf.Abs(dl);
                    best = Mathf.Min(best, Mathf.Max(0f, need) / rate);
                }
                return float.IsPositiveInfinity(best) ? 0f : best / FlushMargin;
            }
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(numberOfRings, blocksPerRing, ringRadius, blockScale, seed);
        }
    }
}
