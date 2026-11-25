using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lantern Run's garden ruins (LANTERNRUN.md, "The ruins"): the SAME ruins Spearline lays round its racing line at
    /// I4 (<see cref="SpearlineRuins"/>: seven kinds, one to a leg, a clear lane down the flown line, nothing at a
    /// ring), laid from Lantern Run's rings. This file adds no ruin of its own. It only decides which of those prisms
    /// Lantern Run leaves out, because this course carries something Spearline's does not: the prism MASS clusters the
    /// Angler paints (<see cref="LanternRunCourse.Clusters"/>).
    ///
    /// <para><b>A ruin never stands in a cluster.</b> A standing ruin prism that would reach within
    /// <see cref="ClusterKeep"/> of a cluster's ball is left out. The turning wheel is laid whole or not at all: if
    /// its sweep would pass through a cluster, the whole wheel is left out.</para>
    ///
    /// <para>Pure: a function of the rings and the rung, so every peer lays the same ruins from the broadcast course
    /// (<c>LanternRunRuinsTests</c>). Nothing here touches a vessel or changes a cluster.</para>
    /// </summary>
    public static class LanternRunRuins
    {
        /// <summary>u a ruin prism's surface keeps off a cluster's ball, so the cluster reads as its own thing and a
        /// brush aimed at it does not light the ruin first.</summary>
        public const float ClusterKeep = 12f;

        /// <summary>
        /// Every ruin prism of one lap of <paramref name="rings"/> on this rung into <paramref name="parts"/>, the
        /// turning ones named in <paramref name="wheels"/>, and for each part whether Lantern Run lays it
        /// (<paramref name="laid"/>, the same length as <paramref name="parts"/>). The lists are cleared first. A part
        /// that is not laid keeps its place in the list, so a wheel's <c>First</c> and <c>Count</c> stay true.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> rings, int intensity,
                               IReadOnlyList<LanternRunCourse.Cluster> clusters,
                               List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels, List<bool> laid)
        {
            parts.Clear();
            wheels.Clear();
            laid.Clear();
            if (rings == null || rings.Count < 3) return;
            SpearlineRuins.Lay(rings, SpearlineRuins.ForIntensity(intensity), parts, wheels);

            float ball = LanternRunCourse.ClusterRadius(intensity) + ClusterKeep;
            for (int i = 0; i < parts.Count; i++) laid.Add(!ReachesCluster(parts[i], clusters, ball));

            for (int w = 0; w < wheels.Count; w++)
            {
                var wheel = wheels[w];
                int end = Mathf.Min(wheel.First + wheel.Count, parts.Count);
                bool turnsFree = true;
                for (int i = wheel.First; i < end && turnsFree; i++)
                    turnsFree = !SweepReachesCluster(parts[i], wheel, clusters, ball);
                for (int i = wheel.First; i < end; i++) laid[i] = turnsFree;
            }
        }

        /// <summary>Does a standing <paramref name="part"/> come within <paramref name="ball"/> of a cluster's centre,
        /// anywhere along its length (its half thickness counted)?</summary>
        public static bool ReachesCluster(in SpearlineRuinPart part, IReadOnlyList<LanternRunCourse.Cluster> clusters,
                                          float ball)
        {
            if (clusters == null) return false;
            float reach = ball + 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
            int samples = 1 + Mathf.CeilToInt(part.Scale.z / 10f);
            for (int c = 0; c < clusters.Count; c++)
            {
                for (int k = 0; k < samples; k++)
                {
                    float along = samples == 1 ? 0f : (k / (float)(samples - 1) - 0.5f) * part.Scale.z;
                    if ((part.Position + part.Forward * along - clusters[c].Centre).magnitude < reach) return true;
                }
            }
            return false;
        }

        /// <summary>Does <paramref name="part"/> of a turning <paramref name="wheel"/> pass within
        /// <paramref name="ball"/> of a cluster's centre at ANY angle? The part sweeps a circle about the wheel's axis,
        /// so the test is on distance along the axis and distance out from it.</summary>
        public static bool SweepReachesCluster(in SpearlineRuinPart part, in SpearlineRuinWheel wheel,
                                               IReadOnlyList<LanternRunCourse.Cluster> clusters, float ball)
        {
            if (clusters == null) return false;
            float reach = ball + 0.5f * Mathf.Max(part.Scale.x, Mathf.Max(part.Scale.y, part.Scale.z));
            Along(part.Position, wheel, out float partAlong, out float partOut);
            for (int c = 0; c < clusters.Count; c++)
            {
                Along(clusters[c].Centre, wheel, out float along, out float outward);
                if (Mathf.Abs(along - partAlong) < reach && Mathf.Abs(outward - partOut) < reach) return true;
            }
            return false;
        }

        static void Along(Vector3 p, in SpearlineRuinWheel wheel, out float along, out float outward)
        {
            Vector3 rel = p - wheel.Centre;
            along = Vector3.Dot(rel, wheel.Axis);
            outward = (rel - wheel.Axis * along).magnitude;
        }
    }
}
