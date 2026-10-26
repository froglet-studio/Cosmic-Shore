using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One prism a Switchback feature lays: pose, size and kind.</summary>
    public readonly struct SwitchbackFeaturePrism
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly PrismKind Kind;

        public SwitchbackFeaturePrism(Vector3 position, Quaternion rotation, Vector3 scale, PrismKind kind)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Kind = kind;
        }
    }

    /// <summary>
    /// What Switchback hangs on its course at intensities 3 and 4 (Yash, 2026-10-26,
    /// <c>Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md</c> §3.3), as a pure function of the course: the same
    /// course gives the same prisms on every peer, and the tests hold it without a scene.
    /// <list type="bullet">
    /// <item><b>I3, skim walls:</b> on every second leg, a wall of plain prisms beside the straight line, over the
    /// leg's middle half - skim along it to bank charge, then spend it down the rest of the straight.</item>
    /// <item><b>I4, danger on the fast line:</b> on every leg, a short cluster of danger prisms just off the
    /// straight line at its middle, on alternating sides: the shortest line brushes them, a wider one is safe and
    /// slower.</item>
    /// </list>
    /// Intensities 1 and 2 lay nothing. The first leg (from the start) is always left clear.
    ///
    /// <para>The course is random, so another leg or another gate can pass anywhere near a feature. A prism is
    /// dropped when it would sit in a gate's mouth (<see cref="MouthClearance"/>), closer to ANY leg's line than its
    /// own offset allows (so a wall never stands across another leg), or inside the nucleus.</para>
    /// </summary>
    public static class SwitchbackFeatures
    {
        public const float WallOffset = 26f;        // u from the straight gate-to-gate line to the wall
        public const float WallSpacing = 14f;       // u between wall prisms
        public const float WallStart = 0.25f;       // share of the leg where the wall begins...
        public const float WallEnd = 0.75f;         // ...and ends
        public const float WallMinLeg = 400f;       // a leg shorter than this gets no wall
        public static readonly Vector3 WallPrismScale = new(5f, 8f, 12f);

        public const float DangerOffset = 12f;      // u from the straight line: the shortest line brushes it
        public const float DangerSpacing = 10f;     // u between the cluster's prisms, along the leg
        public const int DangerPerLeg = 3;
        public static readonly Vector3 DangerPrismScale = new(8f, 8f, 8f);

        public const float MouthClearance = 30f;    // u a feature prism keeps outside every gate's rim
        public const float LineClearanceShare = 0.9f; // of its own offset, from every leg's straight line

        /// <summary>Whether <paramref name="intensity"/> lays anything (3 and 4).</summary>
        public static bool HasFeatures(int intensity) => intensity >= 3;

        /// <summary>
        /// The prisms for <paramref name="course"/> at <paramref name="intensity"/>; empty below 3. A prism closer than
        /// <paramref name="clearRadius"/> to <paramref name="centre"/> (the cell's) is dropped: legs between gates on the
        /// inner shell can pass near the nucleus.
        /// </summary>
        public static List<SwitchbackFeaturePrism> Layout(IReadOnlyList<RaceGate> course, int intensity,
                                                          Vector3 centre = default, float clearRadius = 0f)
        {
            var prisms = new List<SwitchbackFeaturePrism>(256);
            if (course == null || course.Count < 3 || !HasFeatures(intensity)) return prisms;

            for (int k = 1; k < course.Count - 1; k++)
            {
                Vector3 a = course[k].Position, b = course[k + 1].Position;
                Vector3 leg = b - a;
                float length = leg.magnitude;
                if (length < 1f) continue;
                Vector3 dir = leg / length;
                // A side that is the same on every peer: the shared perpendicular, flipped per leg.
                Vector3 side = RaceCourseGeometry.Perpendicular(dir) * (k % 2 == 0 ? 1f : -1f);
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.Cross(side, dir));

                if (intensity == 3)
                {
                    if (k % 2 == 0 || length < WallMinLeg) continue;
                    float from = length * WallStart, to = length * WallEnd;
                    for (float d = from; d <= to; d += WallSpacing)
                        TryAdd(prisms, course, centre, clearRadius, WallOffset,
                               new SwitchbackFeaturePrism(a + dir * d + side * WallOffset, rot, WallPrismScale,
                                                          PrismKind.Plain));
                }
                else
                {
                    float mid = length * 0.5f;
                    for (int j = 0; j < DangerPerLeg; j++)
                    {
                        float d = mid + (j - (DangerPerLeg - 1) * 0.5f) * DangerSpacing;
                        TryAdd(prisms, course, centre, clearRadius, DangerOffset,
                               new SwitchbackFeaturePrism(a + dir * d + side * DangerOffset, rot, DangerPrismScale,
                                                          PrismKind.Danger));
                    }
                }
            }
            return prisms;
        }

        static void TryAdd(List<SwitchbackFeaturePrism> prisms, IReadOnlyList<RaceGate> course, Vector3 centre,
                           float clearRadius, float offset, in SwitchbackFeaturePrism prism)
        {
            Vector3 p = prism.Position;
            if ((p - centre).sqrMagnitude < clearRadius * clearRadius) return;
            for (int g = 0; g < course.Count; g++)
                if ((p - course[g].Position).magnitude < course[g].Radius + MouthClearance) return;
            float lineClearance = offset * LineClearanceShare;
            for (int k = 0; k + 1 < course.Count; k++)
                if (DistanceToSegment(p, course[k].Position, course[k + 1].Position) < lineClearance) return;
            prisms.Add(prism);
        }

        /// <summary>The distance from <paramref name="p"/> to the segment a-b: what the tests measure clearance with.</summary>
        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
