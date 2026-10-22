using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    public class SpawnableEllipsoid : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")]
        [Header("Trail")]
        [SerializeField] protected Prism prism;

        [Header("Spawnable Properties")]
        public float maxlength;
        public float maxwidth;
        public float maxheight;

        protected float length;
        protected float width;
        protected float height;

        /// <summary>
        /// Safe accessor for the prism prefab's localScale.
        /// Returns Vector3.one when prism is null (internal-node mode with children),
        /// producing uniform point scales that avoid shearing nested child geometry.
        /// </summary>
        protected Vector3 PrismScale => prism ? prism.transform.localScale : Vector3.one;

        protected override SpawnTrailData[] GenerateTrailData()
        {
            length = (float)rng.Next(1, 100) / 100f * maxlength;
            width = (float)rng.Next(1, 100) / 100f * maxwidth;
            height = (float)rng.Next(1, 100) / 100f * maxheight;

            int blockCount = 30;

            // Ring 1: XY plane (Jade)
            var points1 = new SpawnPoint[blockCount];
            for (int block = 0; block < blockCount; block++)
            {
                var t = (float)block / (float)blockCount * Mathf.PI * 2;
                var x = (width / 2) * Mathf.Cos(t);
                var y = (height / 2) * Mathf.Sin(t);
                var position = new Vector3(x, y, 0);
                var lookPosition = block == 0 ? position : points1[block - 1].Position;
                var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
                points1[block] = new SpawnPoint(position, rotation, PrismScale);
            }

            // Ring 2: XZ plane (Ruby)
            var points2 = new SpawnPoint[blockCount];
            for (int block = 0; block < blockCount; block++)
            {
                var t = (float)block / (float)blockCount * Mathf.PI * 2;
                var x = (width / 2) * Mathf.Cos(t);
                var z = (length / 2) * Mathf.Sin(t);
                var position = new Vector3(x, 0, z);
                var lookPosition = block == 0 ? position : points2[block - 1].Position;
                var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
                points2[block] = new SpawnPoint(position, rotation, PrismScale);
            }

            // Ring 3: YZ plane (Gold)
            var points3 = new SpawnPoint[blockCount];
            for (int block = 0; block < blockCount; block++)
            {
                var t = (float)block / (float)blockCount * Mathf.PI * 2;
                var y = (height / 2) * Mathf.Cos(t);
                var z = (length / 2) * Mathf.Sin(t);
                var position = new Vector3(0, y, z);
                var lookPosition = block == 0 ? position : points3[block - 1].Position;
                var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
                points3[block] = new SpawnPoint(position, rotation, PrismScale);
            }

            MakeFlush(points1, points2, points3);

            return new[]
            {
                new SpawnTrailData(points1, true, Domains.Jade),
                new SpawnTrailData(points2, true, Domains.Ruby),
                new SpawnTrailData(points3, true, Domains.Gold),
            };
        }

        /// <summary>The smallest axis the prism prefab lays (its PrismScaleAnimator window grows
        /// anything thinner back to this), so a flush block is never sized under it.</summary>
        protected const float MinPrismAxis = 0.5f;

        /// <summary>Blocks are sized to this fraction of their exact flush size, so float noise
        /// never turns two faces that touch into a clip.</summary>
        protected const float FlushMargin = 0.995f;

        /// <summary>
        /// Size the three rings FLUSH (Docs/ECOSYSTEM.md §49.10). Each block sits on its point of the
        /// ellipse, turned along the ring, and the random ellipse is often too small round for 30
        /// blocks at full size: each block is sized flush with the blocks up to RingReach before
        /// and after it, by the common factor on whichever axes of the pair keep the most volume
        /// (PrismFit.Shrink). The rings cross where the ellipse meets
        /// its axes, at a quarter, a half and three quarters of the way round, and there the blocks
        /// of the two rings shrink the same way. The XY and XZ rings both START at (width/2, 0, 0)
        /// and both pass (-width/2, 0, 0) at their half-way block, laying a block on a block - the
        /// shipped prefab's 2 coincident pairs - and the YZ ring's first and half-way blocks sit a
        /// fraction of a block from the XY ring's pair either side of its quarter points. No size
        /// parts those: the later ring's block is laid against the other, moved straight out from
        /// the centre by the two blocks' reach, the smallest move that parts them and the only free
        /// side (both rings run on inwards from there).
        /// </summary>
        static void MakeFlush(SpawnPoint[] ring1, SpawnPoint[] ring2, SpawnPoint[] ring3)
        {
            var rings = new[] { ring1, ring2, ring3 };
            foreach (var ring in rings)
                for (int step = 1; step <= RingReach; step++)
                    for (int i = 0; i < ring.Length; i++)
                    {
                        int j = (i + step) % ring.Length;                // the rings are loops
                        if (j != i) PrismFit.Shrink(ref ring[i], ref ring[j], MinPrismAxis, FlushMargin);
                    }

            // Where each pair of rings meets, as the quarter of the way round each ring is there:
            // XY and XZ on the x axis, XY and YZ on the y axis, XZ and YZ on the z axis.
            foreach (var (a, qa, b, qb) in Crossings)
                foreach (int i in Around(rings[a].Length, qa))
                    foreach (int j in Around(rings[b].Length, qb))
                        if (!PrismFit.Shrink(ref rings[a][i], ref rings[b][j], MinPrismAxis, FlushMargin))
                            PrismFit.LayOutside(rings[a][i], ref rings[b][j], MinPrismAxis, FlushMargin);
        }

        /// <summary>How many blocks along its ring a block is sized against, either way: a wide
        /// block on a tight bend reaches past its next neighbour.</summary>
        const int RingReach = 3;

        static readonly (int ringA, int quarterA, int ringB, int quarterB)[] Crossings =
        {
            (0, 0, 1, 0), (0, 2, 1, 2),     // (+-width/2, 0, 0)
            (0, 1, 2, 0), (0, 3, 2, 2),     // (0, +-height/2, 0)
            (1, 1, 2, 1), (1, 3, 2, 3),     // (0, 0, +-length/2)
        };

        /// <summary>The blocks of a ring of n round the point a quarter-turn count of the way round
        /// (two either side, wrapping).</summary>
        static IEnumerable<int> Around(int n, int quarter)
        {
            int c = n * quarter / 4;
            for (int k = -2; k <= 2; k++) yield return ((c + k) % n + n) % n;
        }

        /// <summary>
        /// Flush sizing between two laid blocks, in closed form from their poses. A block is its
        /// oriented box; on each of the 15 separating axes of two boxes the clearance is linear in
        /// a common factor on chosen axes of each, so the largest flush factor is the best of 15
        /// ratios - no search, no iteration. Shared by the shapes that size their blocks pairwise
        /// along known neighbours (SpawnableComet).
        /// </summary>
        internal static class PrismFit
        {
            readonly struct Box
            {
                public readonly Vector3 Center, X, Y, Z, Half;
                /// <summary>The box as the prefab lays it: no axis under minAxis.</summary>
                public Box(in SpawnPoint p, float minAxis)
                {
                    Center = p.Position;
                    X = p.Rotation * Vector3.right; Y = p.Rotation * Vector3.up; Z = p.Rotation * Vector3.forward;
                    Half = 0.5f * new Vector3(Mathf.Max(Mathf.Abs(p.Scale.x), minAxis), Mathf.Max(Mathf.Abs(p.Scale.y), minAxis),
                                              Mathf.Max(Mathf.Abs(p.Scale.z), minAxis));
                }

                public Vector3 Axis(int i) => i == 0 ? X : i == 1 ? Y : Z;

                public float Support(Vector3 l) =>
                    Mathf.Abs(Vector3.Dot(X, l)) * Half.x + Mathf.Abs(Vector3.Dot(Y, l)) * Half.y + Mathf.Abs(Vector3.Dot(Z, l)) * Half.z;
            }

            static Vector3 Candidate(int i, in Box a, in Box b) =>
                i < 3 ? a.Axis(i) : i < 6 ? b.Axis(i - 3) : Vector3.Cross(a.Axis((i - 6) / 3), b.Axis((i - 6) % 3));

            static bool Clips(in Box a, in Box b)
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

            /// <summary>The largest common factor (at most 1) on a's and b's masked axes at which the
            /// two are flush; 0 when none parts them.</summary>
            static float SharedFactor(in Box a, in Box b, int maskA, int maskB, float margin)
            {
                Vector3 d = b.Center - a.Center;
                float best = 0f;
                for (int i = 0; i < 15; i++)
                {
                    Vector3 l = Candidate(i, a, b);
                    if (l.sqrMagnitude < 1e-10f) continue;
                    float gap = Mathf.Abs(Vector3.Dot(d, l)), reach = 0f;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        float ra = Mathf.Abs(Vector3.Dot(a.Axis(axis), l)) * a.Half[axis];
                        float rb = Mathf.Abs(Vector3.Dot(b.Axis(axis), l)) * b.Half[axis];
                        if ((maskA & (1 << axis)) != 0) reach += ra; else gap -= ra;
                        if ((maskB & (1 << axis)) != 0) reach += rb; else gap -= rb;
                    }
                    if (gap < 0f) continue;
                    if (reach <= gap) return 1f;
                    best = Mathf.Max(best, gap / reach);
                }
                return best * margin;
            }

            static bool Fits(Vector3 scale, int mask, float k, float minAxis)
            {
                if (k <= 0f) return false;
                for (int axis = 0; axis < 3; axis++)
                    if ((mask & (1 << axis)) != 0 && Mathf.Abs(scale[axis]) * k < Mathf.Min(minAxis, Mathf.Abs(scale[axis]))) return false;
                return true;
            }

            /// <summary>
            /// Shrink two clipping blocks until flush, by a common factor on some of the axes of one
            /// or both - whichever choice of axes keeps the most volume without taking an axis under
            /// minAxis (an axis already under it cannot shrink: the prefab grows it back). Left as
            /// they are when none parts them, and then false. The centres and rotations never move.
            /// </summary>
            public static bool Shrink(ref SpawnPoint pa, ref SpawnPoint pb, float minAxis, float margin)
            {
                var a = new Box(pa, minAxis);
                var b = new Box(pb, minAxis);
                if (!Clips(a, b)) return true;

                // Any set of axes of either or both (63 ways), each costing the volume the factor
                // takes off every block it shrinks; take the cheapest that fits.
                float volumeA = Mathf.Abs(pa.Scale.x * pa.Scale.y * pa.Scale.z), volumeB = Mathf.Abs(pb.Scale.x * pb.Scale.y * pb.Scale.z);
                int bestA = 0, bestB = 0;
                float bestK = 0f, bestLoss = float.PositiveInfinity;
                for (int maskA = 0; maskA < 8; maskA++)
                    for (int maskB = 0; maskB < 8; maskB++)
                    {
                        if (maskA == 0 && maskB == 0) continue;
                        float k = SharedFactor(a, b, maskA, maskB, margin);
                        if (!Fits(pa.Scale, maskA, k, minAxis) || !Fits(pb.Scale, maskB, k, minAxis)) continue;
                        float loss = volumeA * (1f - Pow(k, Bits(maskA))) + volumeB * (1f - Pow(k, Bits(maskB)));
                        if (loss < bestLoss) (bestLoss, bestK, bestA, bestB) = (loss, k, maskA, maskB);
                    }
                if (bestK <= 0f) return false;
                Apply(ref pa, bestA, bestK);
                Apply(ref pb, bestB, bestK);
                return true;
            }

            static int Bits(int mask) => (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);
            static float Pow(float k, int n) => n == 0 ? 1f : n == 1 ? k : n == 2 ? k * k : k * k * k;

            static void Apply(ref SpawnPoint p, int mask, float k)
            {
                for (int axis = 0; axis < 3; axis++)
                    if ((mask & (1 << axis)) != 0) p.Scale[axis] *= k;
            }

            /// <summary>Lay b flush against a on the side away from the origin (the outside of the
            /// shape) - for a block on (or within a block's size of) another's centre, which no size
            /// parts.</summary>
            public static void LayOutside(in SpawnPoint pa, ref SpawnPoint pb, float minAxis, float margin)
            {
                Vector3 n = pa.Position.sqrMagnitude > 1e-8f ? pa.Position.normalized : Vector3.up;
                var a = new Box(pa, minAxis);
                var b = new Box(pb, minAxis);
                pb.Position += n * ((a.Support(n) + b.Support(n)) / margin);
            }
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(maxlength, maxwidth, maxheight, seed);
        }
    }
}
