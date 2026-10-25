using CosmicShore.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class SpawnableBatman : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

        protected override SpawnPoint[] GeneratePoints()
        {
            Func<float, float> outerWing = (x) => (float)(2 * Math.Sqrt(-Math.Abs(Math.Abs(x) - 1) * Math.Abs(3 - Math.Abs(x)) / ((Math.Abs(x) - 1) * (3 - Math.Abs(x)))) * (1 + Math.Abs(Math.Abs(x) - 3) / (Math.Abs(x) - 3)) * Math.Sqrt(1 - Math.Pow((x / 7), 2)) + (5 + 0.97f * (Math.Abs(x - 0.5f) + Math.Abs(x + 0.5f)) - 3 * (Math.Abs(x - 0.75f) + Math.Abs(x + 0.75f))) * (1 + Math.Abs(1 - Math.Abs(x)) / (1 - Math.Abs(x))));
            Func<float, float> head1 = (x) => (float)(9 - 8 * Math.Abs(x));
            Func<float, float> head2 = (x) => (float)(3 * Math.Abs(x) + .75);
            Func<float, float> head3 = (x) => (float)(2.25);
            Func<float, float> bottom = (x) => (float)(Math.Abs(x / 2) - 0.0913722 * (Math.Pow(x, 2)) - 3 + Math.Sqrt(1 - Math.Pow((Math.Abs(Math.Abs(x) - 2) - 1), 2)));
            Func<float, float> innerWing = (x) => (float)((2.71052 + (1.5 - .5 * Math.Abs(x)) - 1.35526 * Math.Sqrt(4 - Math.Pow((Math.Abs(x) - 1), 2))) * Math.Sqrt(Math.Abs(Math.Abs(x) - 1) / (Math.Abs(x) - 1)) + 0.9);

            var pointsList = new List<SpawnPoint>();
            var strokes = new List<Stroke>();

            // Outer Wings
            for (float block = 3.2f; block < 7; block += .2f)
            {
                var x = block * 5;
                var y = outerWing(block) * 5;

                var position = new Vector3(x, y, 0);
                Add(pointsList, strokes, Stroke.OuterRightUpper, position);

                if (block > 4)
                {
                    position = new Vector3(x, -y, 0);
                    Add(pointsList, strokes, Stroke.OuterRightLower, position);
                }

                position = new Vector3(-x, y, 0);
                Add(pointsList, strokes, Stroke.OuterLeftUpper, position);

                if (block > 4)
                {
                    position = new Vector3(-x, -y, 0);
                    Add(pointsList, strokes, Stroke.OuterLeftLower, position);
                }
            }

            // Inner Wings
            for (float block = 1.2f; block <= 3; block += .2f)
            {
                var x = block * 5;
                var y = innerWing(block) * 5;

                var position = new Vector3(x, y, 0);
                Add(pointsList, strokes, Stroke.InnerRight, position);

                position = new Vector3(-x, y, 0);
                Add(pointsList, strokes, Stroke.InnerLeft, position);
            }

            // Bottom
            for (float block = 0; block <= 4; block += .2f)
            {
                var x = block * 5;
                var y = (bottom(block) * 5) - 2;

                var position = new Vector3(x, y, 0);
                Add(pointsList, strokes, Stroke.BottomRight, position);

                position = new Vector3(-x, y, 0);
                Add(pointsList, strokes, Stroke.BottomLeft, position);
            }

            // Head
            for (float block = .75f; block <= 1; block += .025f)
            {
                var x = block * 5;
                var y = (head1(block) * 5) + 5;

                var position = new Vector3(x, y, 0);
                Add(pointsList, strokes, Stroke.EarRight, position);
                position = new Vector3(-x, y, 0);
                Add(pointsList, strokes, Stroke.EarLeft, position);
            }
            // Head2
            for (float block = .5f; block <= .75; block += .025f)
            {
                var x = block * 5;
                var y = (head2(block) * 5) + 5;

                var position = new Vector3(x, y, 0);
                Add(pointsList, strokes, Stroke.HeadSlopeRight, position);

                position = new Vector3(-x, y, 0);
                Add(pointsList, strokes, Stroke.HeadSlopeLeft, position);
            }
            // Head3
            for (float block = -.5f; block <= .5f; block += .025f)
            {
                var x = block * 5;
                var y = (head3(block) * 5) + 5;

                var position = new Vector3(x, y, 0);
                Add(pointsList, strokes, Stroke.HeadTop, position);
            }

            var points = pointsList.ToArray();
            MakeFlush(points, strokes);
            return points;
        }

        /// <summary>The curves the emblem is drawn in; each is one stroke of blocks, in order.</summary>
        enum Stroke
        {
            OuterRightUpper = 0, OuterRightLower = 1, OuterLeftUpper = 2, OuterLeftLower = 3,
            InnerRight = 4, InnerLeft = 5, BottomRight = 6, BottomLeft = 7,
            EarRight = 8, EarLeft = 9, HeadSlopeRight = 10, HeadSlopeLeft = 11, HeadTop = 12,
        }

        static void Add(List<SpawnPoint> points, List<Stroke> strokes, Stroke stroke, Vector3 position)
        {
            points.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));
            strokes.Add(stroke);
        }

        /// <summary>The smallest axis the prism prefab lays (its PrismScaleAnimator window grows
        /// anything thinner back to this).</summary>
        const float MinPrismAxis = 0.5f;

        /// <summary>Two stroke ends closer than this fraction of a block's in-plane half size are one
        /// point drawn twice: no in-plane size parts them.</summary>
        const float CopyFraction = 0.5f;

        /// <summary>Blocks are sized to this fraction of their exact flush size, so float noise
        /// never turns two faces that touch into a clip.</summary>
        const float FlushMargin = 0.995f;

        /// <summary>
        /// Size the emblem's unit blocks FLUSH (Docs/ECOSYSTEM.md §49.10), from the strokes the
        /// points were drawn in. Every block lies in the emblem's plane with its local x out of it,
        /// so neighbours part in the plane: each block shrinks on its in-plane axes - first just the
        /// one that reaches furthest towards the neighbour, both only where one is not enough - by
        /// the common factor that makes it flush with the block before and after it on its stroke,
        /// and with the end blocks of the other strokes where two strokes meet. Two strokes that
        /// end on the same point (the two halves of the bottom at x = 0, the ears where they meet
        /// the head's slopes, the upper and lower outer wing at each tip, 0.04 apart) laid a block
        /// on a block - the shipped emblem's 4 coincident pairs and 2 near-coincident ones: the
        /// later copy is laid against the first, offset out of the plane by one block thickness,
        /// the smallest move that parts them. Where only one of a pair can shrink far enough (an
        /// ear's end against the head's slope), it alone does.
        ///
        /// The head's slopes and its top are drawn with steps of 0.395 and 0.125 units - finer than
        /// the prism prefab's smallest axis (0.5) - so no size this side of that lets those blocks
        /// lie flush; only fewer blocks would. They keep their full size, and their pairs are left
        /// to the clip audit's report rather than shrunk to no purpose.
        /// </summary>
        static void MakeFlush(SpawnPoint[] points, List<Stroke> strokes)
        {
            int n = points.Length;

            // Stroke ends.
            var ends = new List<int>();
            for (int i = 0; i < n; i++)
            {
                bool first = i == FirstOf(strokes, i), last = i == LastOf(strokes, i);
                if (first || last) ends.Add(i);
            }

            // A block laid on another stroke's block: lay it flush on top of it.
            for (int e = 0; e < ends.Count; e++)
                for (int f = 0; f < e; f++)
                {
                    int i = ends[e], j = ends[f];
                    if (strokes[i] == strokes[j]) continue;
                    var a = new Box(points[j]);
                    var b = new Box(points[i]);
                    float apart = (points[i].Position - points[j].Position).magnitude;
                    if (apart > CopyFraction * Mathf.Min(a.Half.y, a.Half.z)) continue;
                    points[i].Position += a.X * ((a.Half.x + b.Support(a.X)) / FlushMargin);
                }

            var factors = new Vector3[n];
            for (int i = 0; i < n; i++) factors[i] = Vector3.one;
            for (int i = 0; i + 1 < n; i++)
            {
                // the next block on the same stroke
                int next = NextOf(strokes, i);
                if (next >= 0) Shrink(points, factors, i, next);
            }
            for (int e = 0; e < ends.Count; e++)
                for (int f = 0; f < e; f++)
                    if (strokes[ends[e]] != strokes[ends[f]]) Shrink(points, factors, ends[f], ends[e]);

            for (int i = 0; i < n; i++) points[i].Scale = Vector3.Scale(points[i].Scale, factors[i]);
        }

        static int FirstOf(List<Stroke> strokes, int i) { int k = i; for (int j = i - 1; j >= 0; j--) if (strokes[j] == strokes[i]) k = j; return k; }
        static int LastOf(List<Stroke> strokes, int i) { int k = i; for (int j = i + 1; j < strokes.Count; j++) if (strokes[j] == strokes[i]) k = j; return k; }
        static int NextOf(List<Stroke> strokes, int i) { for (int j = i + 1; j < strokes.Count; j++) if (strokes[j] == strokes[i]) return j; return -1; }

        /// <summary>Shrink two blocks' in-plane axes until flush, if that can be done above the
        /// prism prefab's smallest axis; otherwise leave them.</summary>
        static void Shrink(SpawnPoint[] points, Vector3[] factors, int i, int j)
        {
            var a = new Box(points[i]);
            var b = new Box(points[j]);
            if (!Box.Clips(a, b)) return;
            Vector3 d = b.Center - a.Center;
            // One axis each first (the one reaching furthest towards the other), then both in-plane.
            int ia = a.Furthest(d, 1, 2), ib = b.Furthest(d, 1, 2);
            float k = Box.SharedFactor(a, b, 1 << ia, 1 << ib);
            int maskA = 1 << ia, maskB = 1 << ib;
            if (!Fits(points[i].Scale, maskA, k) || !Fits(points[j].Scale, maskB, k))
            {
                // Both in-plane axes of both, then of one alone (the other keeps its size).
                (maskA, maskB, k) = (0b110, 0b110, Box.SharedFactor(a, b, 0b110, 0b110));
                if (!Fits(points[i].Scale, maskA, k) || !Fits(points[j].Scale, maskB, k))
                    (maskA, maskB, k) = (0b110, 0, Box.SharedFactor(a, b, 0b110, 0));
                if (!Fits(points[i].Scale, maskA, k))
                    (maskA, maskB, k) = (0, 0b110, Box.SharedFactor(a, b, 0, 0b110));
                if (!Fits(points[j].Scale, maskB, k)) return;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                if ((maskA & (1 << axis)) != 0) factors[i][axis] = Mathf.Min(factors[i][axis], k);
                if ((maskB & (1 << axis)) != 0) factors[j][axis] = Mathf.Min(factors[j][axis], k);
            }
        }

        static bool Fits(Vector3 scale, int mask, float k)
        {
            if (k <= 0f) return false;
            for (int axis = 0; axis < 3; axis++)
                if ((mask & (1 << axis)) != 0 && Mathf.Abs(scale[axis]) * k < MinPrismAxis) return false;
            return true;
        }

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

            public Vector3 Axis(int i) => i == 0 ? X : i == 1 ? Y : Z;

            public float Support(Vector3 l) =>
                Mathf.Abs(Vector3.Dot(X, l)) * Half.x + Mathf.Abs(Vector3.Dot(Y, l)) * Half.y + Mathf.Abs(Vector3.Dot(Z, l)) * Half.z;

            /// <summary>Of axes i and j, the one that reaches further along d.</summary>
            public int Furthest(Vector3 d, int i, int j) =>
                Mathf.Abs(Vector3.Dot(Axis(i), d)) * Half[i] >= Mathf.Abs(Vector3.Dot(Axis(j), d)) * Half[j] ? i : j;

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

            /// <summary>The largest common factor (at most 1) on a's masked axes and b's masked axes
            /// at which the two are flush - closed form: on each of the 15 candidate axes the
            /// clearance is linear in it. 0 when no factor parts them.</summary>
            public static float SharedFactor(in Box a, in Box b, int maskA, int maskB)
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
                return best * FlushMargin;
            }
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seed);
        }
    }
}
