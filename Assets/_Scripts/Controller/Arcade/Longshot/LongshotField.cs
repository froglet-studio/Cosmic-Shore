using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Longshot's field, kept pure so the edit-mode tests and the generator's twin can hold it without a scene.
    ///
    /// <para><b>A field is LANCES of neutral prisms</b> laid by the mode on every peer from the intensity alone (no seed,
    /// no random draw): single-file lines of <see cref="Rung.LanceLength"/> cubes, <see cref="Spacing"/> apart, their
    /// midpoints on a golden-angle spiral a band of the way from the nucleus to the membrane, tangent to that sphere. From
    /// I3 a lance is BENT into <see cref="Rung.Segments"/> straight runs, zig-zagging <see cref="KneeDegrees"/> either side
    /// of its line in the sphere's tangent plane.</para>
    ///
    /// <para><b>Why lines.</b> The Serpent's sniper round is one hitscan cone (1.5 degrees, from 6 u wide) that breaks
    /// EVERY hostile prism inside it, neutral ones included (SERPENT_SNIPER_SCOPE.md), on a 12 s recovery. Fired along a
    /// lance it takes the whole run; fired across it, a prism or two. Lining up is the skill - the Space twin of
    /// Shatterplane's squaring up.</para>
    /// </summary>
    public static class LongshotField
    {
        /// <summary>A lance prism's side, u (a cube).</summary>
        public const float PrismSize = 6f;

        /// <summary>Centre-to-centre distance between neighbouring prisms along a lance, u.</summary>
        public const float Spacing = 12f;

        /// <summary>How far each run of a bent lance turns off the lance's line, degrees.</summary>
        public const float KneeDegrees = 30f;

        /// <summary>Lances keep this far outside the nucleus.</summary>
        public const float InnerClearance = 150f;

        /// <summary>Lances keep this far inside the membrane.</summary>
        public const float OuterClearance = 150f;

        /// <summary>One rung of the field.</summary>
        public readonly struct Rung
        {
            public readonly int LanceCount;
            public readonly int LanceLength;
            public readonly int Segments;
            public readonly float BandFraction;

            public Rung(int lanceCount, int lanceLength, int segments, float bandFraction)
            {
                LanceCount = lanceCount;
                LanceLength = lanceLength;
                Segments = segments;
                BandFraction = bandFraction;
            }
        }

        /// <summary>I1 Range, I2 Gallery, I3 Dogleg, I4 Thornfield (LONGSHOT.md). The generator mirrors this table.</summary>
        public static Rung ForIntensity(int intensity) => Mathf.Clamp(intensity, 1, 4) switch
        {
            1 => new Rung(8, 48, 1, 0.35f),
            2 => new Rung(12, 48, 1, 0.50f),
            3 => new Rung(12, 48, 2, 0.50f),
            _ => new Rung(16, 48, 3, 0.60f),
        };

        /// <summary>Every prism the rung lays.</summary>
        public static int TotalPrisms(Rung rung) => rung.LanceCount * rung.LanceLength;

        /// <summary>Every straight run the rung lays (the AI's targets).</summary>
        public static int TotalSegments(Rung rung) => rung.LanceCount * Mathf.Max(1, rung.Segments);

        /// <summary>The radius every lance's midpoint sits at, from the cell's centre.</summary>
        public static float LanceRadius(Rung rung, float nucleusRadius, float membraneRadius)
        {
            float inner = nucleusRadius + InnerClearance;
            float outer = Mathf.Max(inner, membraneRadius - OuterClearance);
            return Mathf.Lerp(inner, outer, Mathf.Clamp01(rung.BandFraction));
        }

        /// <summary>The way lance <paramref name="lance"/> runs (unit, tangent to the sphere at its midpoint): a
        /// reference tangent turned about the outward normal by the golden angle per lance.</summary>
        public static Vector3 LanceDirection(Rung rung, int lance)
        {
            const float golden = 2.39996323f;
            Vector3 n = ChumReef.SpiralDirection(lance, rung.LanceCount);
            Vector3 axis = Mathf.Abs(n.y) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 t = Vector3.Cross(n, axis).normalized;
            Vector3 b = Vector3.Cross(n, t);
            float a = lance * golden;
            return (t * Mathf.Cos(a) + b * Mathf.Sin(a)).normalized;
        }

        /// <summary>One straight run of a lance: where it starts and ends (relative to the cell's centre) and how many
        /// prisms it carries.</summary>
        public readonly struct Segment
        {
            public readonly Vector3 Start;
            public readonly Vector3 End;
            public readonly int Lance;
            public readonly int Count;

            public Segment(Vector3 start, Vector3 end, int lance, int count)
            {
                Start = start;
                End = end;
                Lance = lance;
                Count = count;
            }
        }

        /// <summary>One prism to lay, relative to the cell's centre, and the run it belongs to (an index into the
        /// segment list <see cref="Lay"/> fills).</summary>
        public readonly struct Piece
        {
            public readonly Vector3 Position;
            public readonly int Segment;

            public Piece(Vector3 position, int segment)
            {
                Position = position;
                Segment = segment;
            }
        }

        /// <summary>Every prism and every straight run of the rung (both cleared first). Pure: the same lists on every
        /// peer. A lance's runs zig-zag about its line and the whole lance is centred on its midpoint.</summary>
        public static void Lay(Rung rung, float nucleusRadius, float membraneRadius, List<Piece> pieces,
                               List<Segment> segments)
        {
            pieces.Clear();
            segments.Clear();
            float radius = LanceRadius(rung, nucleusRadius, membraneRadius);
            int runs = Mathf.Max(1, rung.Segments);
            int perRun = rung.LanceLength / runs;
            for (int l = 0; l < rung.LanceCount; l++)
            {
                Vector3 n = ChumReef.SpiralDirection(l, rung.LanceCount);
                Vector3 baseDir = LanceDirection(rung, l);
                Vector3 mid = n * radius;

                // Walk the polyline from the origin, then shift it so its prisms are centred on the midpoint.
                int firstPiece = pieces.Count;
                int firstSegment = segments.Count;
                Vector3 at = Vector3.zero;
                for (int r = 0; r < runs; r++)
                {
                    int count = r == runs - 1 ? rung.LanceLength - perRun * (runs - 1) : perRun;
                    float turn = runs == 1 ? 0f : (r % 2 == 0 ? KneeDegrees : -KneeDegrees);
                    Vector3 dir = Quaternion.AngleAxis(turn, n) * baseDir;
                    Vector3 start = at + (r == 0 ? Vector3.zero : dir * Spacing);
                    for (int k = 0; k < count; k++)
                        pieces.Add(new Piece(start + dir * (k * Spacing), segments.Count));
                    Vector3 end = start + dir * ((count - 1) * Spacing);
                    segments.Add(new Segment(start, end, l, count));
                    at = end;
                }

                Vector3 centroid = Vector3.zero;
                for (int i = firstPiece; i < pieces.Count; i++) centroid += pieces[i].Position;
                centroid /= Mathf.Max(1, pieces.Count - firstPiece);
                Vector3 shift = mid - centroid;
                for (int i = firstPiece; i < pieces.Count; i++)
                    pieces[i] = new Piece(pieces[i].Position + shift, pieces[i].Segment);
                for (int s = firstSegment; s < segments.Count; s++)
                {
                    var seg = segments[s];
                    segments[s] = new Segment(seg.Start + shift, seg.End + shift, seg.Lance, seg.Count);
                }
            }
        }

        /// <summary>The sniper's own cone test (<c>PrismSpatialIndex.ConeContains</c>, restated in Vector3 so a test can
        /// hold it): is <paramref name="p"/> inside the cone from <paramref name="apex"/> along unit
        /// <paramref name="dir"/>?</summary>
        public static bool InCone(Vector3 p, Vector3 apex, Vector3 dir, float range, float halfAngleDegrees,
                                  float minRadius)
        {
            Vector3 rel = p - apex;
            float t = Vector3.Dot(rel, dir);
            if (t < 0f || t > range) return false;
            float allowed = Mathf.Max(minRadius, t * Mathf.Tan(halfAngleDegrees * Mathf.Deg2Rad));
            float perpSq = Mathf.Max(0f, rel.sqrMagnitude - t * t);
            return perpSq <= allowed * allowed;
        }
    }
}
