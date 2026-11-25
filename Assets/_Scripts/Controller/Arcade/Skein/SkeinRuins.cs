using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skein's garden ruins (SKEIN.md section 20): the SAME ruins Spearline lays round its racing line at I4
    /// (<see cref="SpearlineRuins"/>: seven kinds, a clear lane down the flown line between two rings, nothing at a
    /// ring), laid from Skein's rings. This file adds no ruin of its own. It decides which of those prisms Skein lays,
    /// because this course carries two things Spearline's does not.
    ///
    /// <para><b>The cable.</b> Skein is ridden, not flown: the rails and the launches between them are the racing line,
    /// and Spearline's lane only covers the straight flight from ring to ring. So a standing ruin prism that would reach
    /// within <see cref="CableKeep"/> of a rail prism or of a launch's path is left out, and a turning wheel is laid
    /// whole or not at all: if its sweep would pass through the cable, the whole wheel is left out.</para>
    ///
    /// <para><b>One long lap.</b> Skein's 24 rings are flown once, so seven ruins on the first seven legs would all
    /// stand in the first third of the race. Skein keeps the ruin of every <see cref="LegStride"/>-th leg instead:
    /// seven ruins spread over legs 0 to 18, and still every kind once (3 and 7 share no factor).</para>
    ///
    /// <para>Pure: a function of the rings, the rung and the cable, so every peer lays the same ruins from the
    /// broadcast course (<c>SkeinRuinsTests</c>). Nothing here touches a vessel or changes a rail.</para>
    /// </summary>
    public static class SkeinRuins
    {
        /// <summary>u a ruin prism's surface keeps off every rail prism's centre and every launch's path: over the
        /// 24 u ride envelope the cable itself is proved against (skein_budget.py), so a rider never brushes a ruin.</summary>
        public const float CableKeep = 30f;

        /// <summary>Skein keeps the ruin of every this-many-th leg.</summary>
        public const int LegStride = 3;

        /// <summary>u between the points a launch's path is sampled at.</summary>
        public const float LaunchStep = 8f;

        /// <summary>Does leg <paramref name="leg"/> carry one of the rung's <paramref name="ruins"/> ruins?</summary>
        public static bool Carries(int leg, int ruins) => leg % LegStride == 0 && leg / LegStride < ruins;

        /// <summary>Which ruin of the race leg <paramref name="leg"/> carries (0 first), for its colour.</summary>
        public static int RuinOf(int leg) => leg / LegStride;

        /// <summary>
        /// The points no ruin may stand near, in world space, appended to <paramref name="into"/>: every rail prism's
        /// centre, and every launching rail's straight path off its last prism for <paramref name="launchLength"/> u
        /// (the course's <c>EndAimMax</c>, the farthest a launch is aimed).
        /// </summary>
        public static void KeepOff(SkeinCourse.SkeinBuild build, float launchLength, Vector3 centre, List<Vector3> into)
        {
            for (int r = 0; r < build.Positions.Count; r++)
            {
                var rail = build.Positions[r];
                for (int k = 0; k < rail.Length; k++) into.Add(rail[k] + centre);
                if (!build.Rails[r].Launches || rail.Length == 0) continue;
                Vector3 from = rail[rail.Length - 1] + centre;
                Vector3 heading = build.Rotations[r][rail.Length - 1] * Vector3.forward;
                for (float d = LaunchStep; d <= launchLength; d += LaunchStep) into.Add(from + heading * d);
            }
        }

        /// <summary>
        /// <see cref="KeepOff"/> for the cable the arena lays from <paramref name="cableSeed"/>: the first of the seed
        /// and its five re-rolls that lays a full course, as <c>SpawnableSkein.BuildEnvironment</c> and
        /// <see cref="SkeinCourseSource"/> walk them. False (and nothing appended) if none does.
        /// </summary>
        public static bool TryKeepOff(int cableSeed, in SkeinCourseSettings settings, Vector3 centre, List<Vector3> into)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var build = SkeinCourse.BuildAll(unchecked(cableSeed + attempt * 7919), settings);
                if (build == null) continue;
                KeepOff(build, settings.EndAimMax, centre, into);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Every ruin prism of <paramref name="rings"/> on this rung into <paramref name="parts"/>, the turning ones
        /// named in <paramref name="wheels"/>, and for each part whether Skein lays it (<paramref name="laid"/>, the
        /// same length as <paramref name="parts"/>). The lists are cleared first. A part that is not laid keeps its
        /// place in the list, so a wheel's <c>First</c> and <c>Count</c> stay true.
        /// </summary>
        public static void Lay(IReadOnlyList<RaceGate> rings, int intensity, IReadOnlyList<Vector3> keepOff,
                               List<SpearlineRuinPart> parts, List<SpearlineRuinWheel> wheels, List<bool> laid)
        {
            parts.Clear();
            wheels.Clear();
            laid.Clear();
            var settings = SpearlineRuins.ForIntensity(intensity);
            int ruins = settings.PerLap;
            if (ruins <= 0 || rings == null || rings.Count < 3) return;

            // Spearline lays leg after leg until it has its count, so ask for every leg and keep each third one.
            settings.PerLap = rings.Count;
            SpearlineRuins.Lay(rings, settings, parts, wheels);

            for (int i = 0; i < parts.Count; i++)
                laid.Add(Carries(parts[i].Leg, ruins) && !ReachesCable(parts[i], keepOff, CableKeep));

            for (int w = 0; w < wheels.Count; w++)
            {
                var wheel = wheels[w];
                int end = Mathf.Min(wheel.First + wheel.Count, parts.Count);
                bool turnsFree = wheel.First < end && Carries(parts[wheel.First].Leg, ruins);
                for (int i = wheel.First; i < end && turnsFree; i++)
                    turnsFree = !SweepReachesCable(parts[i], wheel, keepOff, CableKeep);
                for (int i = wheel.First; i < end; i++) laid[i] = turnsFree;
            }
        }

        /// <summary>Does a standing <paramref name="part"/>'s surface come within <paramref name="keep"/> of a
        /// keep-off point, anywhere along its length (its half thickness counted)?</summary>
        public static bool ReachesCable(in SpearlineRuinPart part, IReadOnlyList<Vector3> keepOff, float keep)
        {
            if (keepOff == null) return false;
            float reach = keep + 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
            float reachSqr = reach * reach;
            int samples = 1 + Mathf.CeilToInt(part.Scale.z / 6f);
            for (int k = 0; k < samples; k++)
            {
                Vector3 at = part.Position + part.Forward * ((k / (float)(samples - 1) - 0.5f) * part.Scale.z);
                for (int c = 0; c < keepOff.Count; c++)
                    if ((at - keepOff[c]).sqrMagnitude < reachSqr) return true;
            }
            return false;
        }

        /// <summary>Does <paramref name="part"/> of a turning <paramref name="wheel"/> pass within
        /// <paramref name="keep"/> of a keep-off point at ANY angle? The part sweeps a circle about the wheel's axis, so
        /// the test is on distance along the axis and distance out from it.</summary>
        public static bool SweepReachesCable(in SpearlineRuinPart part, in SpearlineRuinWheel wheel,
                                             IReadOnlyList<Vector3> keepOff, float keep)
        {
            if (keepOff == null) return false;
            float reach = keep + 0.5f * Mathf.Max(part.Scale.x, Mathf.Max(part.Scale.y, part.Scale.z));
            Along(part.Position, wheel, out float partAlong, out float partOut);
            for (int c = 0; c < keepOff.Count; c++)
            {
                Along(keepOff[c], wheel, out float along, out float outward);
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
