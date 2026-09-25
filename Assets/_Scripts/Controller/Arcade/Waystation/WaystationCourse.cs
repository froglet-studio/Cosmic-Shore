using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The shape of one Waystation course, per intensity. See <see cref="ForIntensity"/>, the
    /// tables' derivation in <c>Tools/Build/waystation_course.py</c>, and WAYSTATION.md.
    /// </summary>
    public struct WaystationCourseSettings
    {
        /// <summary>
        /// Rings in each cluster: <c>RingsPerCluster - 1</c> COIL rings plus ONE exit gate. The
        /// ring target is rounded up to a whole number of these, because a half-built cluster
        /// would end the race in the middle of one.
        /// </summary>
        public int RingsPerCluster;

        /// <summary>Total rings asked for. The course lays
        /// <c>ceil(RingTarget / RingsPerCluster) * RingsPerCluster</c>.</summary>
        public int RingTarget;

        public float RingRadius;

        /// <summary>The coil's radius. A ring sits on this circle about the cluster's centre, so
        /// this IS the flight path's radius of curvature through the cluster and therefore the
        /// number the Butterfly's own turning circle is measured against.</summary>
        public float CoilRadius;

        /// <summary>Degrees around the coil from one ring to the next.</summary>
        public float CoilStepDegrees;

        /// <summary>How far the coil advances along its axis per ring — what makes it a coil
        /// rather than a circle the pilot would fly round twice.</summary>
        public float CoilPitch;

        /// <summary>How far the exit gate sits ahead of the last coil ring.</summary>
        public float ExitLead;

        /// <summary>How far the exit gate's POSITION may turn off the last coil leg. This bounds
        /// the corner into the exit gate, so it is measured against the hull's turning circle -
        /// it is deliberately clamped against the last coil CHORD rather than that ring's
        /// TANGENT, because a corner is measured on chords and clamping the tangent bounded the
        /// wrong angle (measured: the real corner ran 45% under the cap).</summary>
        public float ExitApproachDegrees;

        /// <summary>Distance between consecutive CLUSTER CENTRES, as a chord. Crossed by a FOLD,
        /// so the ceiling is the fold's own reach and the floor is far enough that flying it
        /// instead is a real loss.</summary>
        public float MinHop, MaxHop;

        /// <summary>How far the exit gate may FACE from the direction to the next cluster's
        /// centre. Exact: the gate's facing is that direction deflected by this and nothing
        /// else. It is the mode's difficulty — at 0 the fold would aim itself.</summary>
        public float ExitConeDegrees;

        /// <summary>How far the chain of cluster centres may wander off its own heading per
        /// hop.</summary>
        public float WanderDegrees;

        /// <summary>Shell the course is laid inside, from the controller.</summary>
        public float InnerRadius, OuterRadius;

        /// <summary>
        /// The cluster-centre sphere's radius as a fraction of <see cref="OuterRadius"/>. EVERY
        /// cluster centre sits exactly on that sphere, which is what makes the chain of clusters
        /// contain itself with no clamp, no rejection and no retry: a geodesic step cannot leave
        /// the sphere it walks on.
        /// </summary>
        public float CentreSphereOuterFraction;

        /// <summary>Direction of the FIRST cluster's centre from the cell centre. Pilots spawn on
        /// an equatorial ring, so its POLE is the only direction every pilot is equidistant
        /// from.</summary>
        public Vector3 FirstClusterDirection;

        /// <summary>
        /// The four rungs. <b>Intensity is HOW MUCH COIL THERE IS BETWEEN FOLDS, how tight it is
        /// wound, and how hard the exit line is</b> — rings per cluster 3 -> 6, coil radius
        /// 160 -> 120, and the exit cone 40 -> 90 degrees. The hop opens with it so a longer
        /// cluster is not also a shorter jump.
        ///
        /// <para><b>Three numbers are NOT tables, and that is the point.</b> The exit lead (120),
        /// the exit approach cap (45) and the wander (80) are one value each because they are
        /// each pinned by something that does not vary with intensity: the lead by how far the
        /// structure may reach before it leaves the cell, the approach cap by the hull's turning
        /// circle, the wander by "a fold is never behind you". A table there would be four copies
        /// of one constraint.</para>
        ///
        /// <para><b>Every number is MEASURED, not chosen.</b>
        /// <c>Tools/Build/waystation_course.py</c> mirrors this file, sweeps 200 seeds x 4
        /// intensities and FAILS the build unless: every corner clears the Butterfly's own
        /// turning circle by 20% (measured 1.21x-1.70x), every ring's whole MOUTH stays inside
        /// the cell's shell (measured [502, 1057] against [412, 1060]), every fold gap is inside
        /// the fold's RESTING reach (measured up to 1332 against 1800 — a mode whose top rung
        /// needed a crystal would play differently depending on one), every fold gap is LONGER
        /// than the cluster it leaves (so folding always beats flying it), no two clusters
        /// interpenetrate, and the exit gate's facing is within the authored cone exactly.</para>
        /// </summary>
        public static WaystationCourseSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4) - 1;
            return new WaystationCourseSettings
            {
                RingsPerCluster = new[] { 3, 4, 5, 6 }[i],
                RingRadius = new[] { 56f, 50f, 44f, 40f }[i],
                CoilRadius = new[] { 160f, 150f, 135f, 120f }[i],
                CoilStepDegrees = new[] { 70f, 75f, 80f, 85f }[i],
                CoilPitch = new[] { 70f, 70f, 60f, 55f }[i],
                MinHop = new[] { 700f, 760f, 820f, 880f }[i],
                MaxHop = new[] { 950f, 1000f, 1080f, 1150f }[i],
                ExitConeDegrees = new[] { 40f, 55f, 72f, 90f }[i],

                ExitLead = 120f,
                ExitApproachDegrees = 45f,
                WanderDegrees = 80f,
                CentreSphereOuterFraction = 0.68f,
                FirstClusterDirection = Vector3.up,
            };
        }
    }

    /// <summary>
    /// Waystation's course: a chain of CLUSTERS — coils of rings you weave — laid a FOLD apart,
    /// each ending in an EXIT GATE you line up on. Pure geometry over
    /// <see cref="RaceCourseGeometry"/>: no <c>UnityEngine.Random</c>, no <c>System.Random</c>,
    /// no <c>Time</c>, no scene access, so a seed reproduces the same course on every runtime and
    /// the whole thing is measurable offline.
    ///
    /// <para><b>It cannot fail.</b> Switchback's walk solves REACHABILITY and can legitimately
    /// return null; this one solves nothing. There is no retry, no back-off and no shortened
    /// course, and the ring count the controller reports is always the one it asked for.</para>
    ///
    /// <para><b>Containment is a property of the CONSTRUCTION, not of a clamp.</b> Every cluster
    /// centre lies exactly on one sphere and a hop is a GEODESIC step on it, so the chain cannot
    /// wander out of the cell however long it runs. That replaced a deflect-and-clamp walk, and
    /// the two things that walk got wrong both generalise. <b>A positional clamp is not a
    /// containment strategy for a walk</b>: pulling a step's endpoint back into the shell
    /// silently SHORTENS the leg, and a leg is exactly what the corner radius is measured on — so
    /// the clamp that kept rings in the cell was also producing corners at 38 units against a
    /// hull that needs 85 (measured; here every corner is 1.21x-1.70x the hull's circle). And <b>a
    /// single capped re-aim cannot contain anything</b>: the chain went on adding a thousand units
    /// per hop and reached 3,576 from the cell centre inside a 1,200 membrane, because a bounded
    /// turn cannot undo an unbounded walk.</para>
    ///
    /// <para><b>The exit gate is its own gate, and it has to be.</b> The first cut made the last
    /// COIL ring the aiming device by solving the coil's roll, which is one <c>atan2</c> and
    /// looked elegant. It cannot work: a coil ring's facing is its own tangent, whose tilt off the
    /// coil plane is fixed by <c>pitch / (CoilRadius * step)</c>, so it can be rolled in azimuth
    /// and never in elevation — measured at up to 167 degrees off the fold against an authored
    /// 72-degree cone. The exit gate is laid separately, one bounded turn off the coil's last leg,
    /// FACING the next cluster's centre from ITS OWN POSITION (a fold starts where the pilot is,
    /// so facing it from the cluster's centre would have been the wrong question), which makes
    /// the offset exactly the authored cone rather than approximately it.</para>
    /// </summary>
    public static class WaystationCourse
    {
        /// <summary>
        /// Clusters needed for <paramref name="ringTarget"/> rings, and therefore the real ring
        /// count. Public because the CONTROLLER reports the rounded number as the race length —
        /// a target naming a ring the course never laid is a match that cannot end.
        /// </summary>
        public static int ClusterCount(int ringTarget, int ringsPerCluster)
        {
            int per = Mathf.Max(1, ringsPerCluster);
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, ringTarget) / (float)per));
        }

        public static List<RaceGate> Generate(int seed, WaystationCourseSettings s)
        {
            int per = Mathf.Max(1, s.RingsPerCluster);
            int coilRings = Mathf.Max(1, per - 1);
            int clusters = ClusterCount(s.RingTarget, per);

            float outer = Mathf.Max(1f, s.OuterRadius);
            float centreRadius = Mathf.Clamp(outer * s.CentreSphereOuterFraction,
                                             Mathf.Min(s.InnerRadius, outer), outer);
            float coilStep = s.CoilStepDegrees * Mathf.Deg2Rad;

            var rng = new RaceCourseGeometry.Rng(seed);
            var gates = new List<RaceGate>(clusters * per);

            Vector3 pole = RaceCourseGeometry.SafeNormalize(s.FirstClusterDirection, Vector3.up);
            Vector3 centre = pole * centreRadius;

            // The first hop leaves along an arbitrary TANGENT at the pole. It has to be tangential
            // rather than radial, because every hop after it is a rotation of the centre about an
            // axis perpendicular to both - a radial "heading" has no such axis.
            Vector3 tangent = RaceCourseGeometry.Deflect(ref rng, RaceCourseGeometry.Perpendicular(pole), 180f);

            // Pilots spawn on the pole's equatorial ring and fly INWARD into cluster 0, so that is
            // the axis its coil is threaded along.
            Vector3 coilAxis = -pole;

            for (int c = 0; c < clusters; c++)
            {
                bool hasNext = c + 1 < clusters;
                Vector3 next;

                if (hasNext)
                {
                    Vector3 radial = RaceCourseGeometry.SafeNormalize(centre, pole);
                    tangent = RaceCourseGeometry.Deflect(ref rng, tangent, s.WanderDegrees);
                    // Re-project: a deflection tilts out of the tangent plane, and a hop that is
                    // not a rotation IN that plane is a hop that leaves the sphere.
                    tangent = RaceCourseGeometry.SafeNormalize(
                        tangent - radial * Vector3.Dot(tangent, radial),
                        RaceCourseGeometry.Perpendicular(radial));

                    float hop = rng.Range(s.MinHop, s.MaxHop);
                    float sweep = 2f * Mathf.Asin(Mathf.Min(1f, hop / (2f * centreRadius)));
                    Vector3 axis = RaceCourseGeometry.SafeNormalize(Vector3.Cross(radial, tangent),
                                                                   RaceCourseGeometry.Perpendicular(radial));
                    next = RaceCourseGeometry.RotateAbout(centre, axis, sweep);
                    tangent = RaceCourseGeometry.SafeNormalize(
                        RaceCourseGeometry.RotateAbout(tangent, axis, sweep), tangent);
                }
                else
                {
                    // The terminal cluster still lays an exit gate - there is nothing to fold to,
                    // so it is aimed STRAIGHT ON. A gate aimed back into its own coil is unflyable.
                    next = centre + coilAxis * s.MinHop;
                }

                LayCluster(ref rng, gates, centre, next, coilAxis, coilRings, coilStep, s);

                if (hasNext)
                {
                    // The pilot FOLDS the chord, so the heading they carry into the next cluster -
                    // and therefore that coil's axis - is the chord, not whatever the walk ended on.
                    coilAxis = RaceCourseGeometry.SafeNormalize(next - centre, coilAxis);
                    centre = next;
                }
            }

            return gates;
        }

        /// <summary>
        /// One cluster: <paramref name="coilRings"/> rings on a coil about
        /// <paramref name="centre"/> threaded along <paramref name="coilAxis"/>, then the exit
        /// gate. A coil ring FACES the coil's own tangent, which is the direction the pilot is
        /// travelling when they reach it.
        /// </summary>
        static void LayCluster(ref RaceCourseGeometry.Rng rng, List<RaceGate> into, Vector3 centre,
                               Vector3 next, Vector3 coilAxis, int coilRings, float coilStep,
                               in WaystationCourseSettings s)
        {
            Vector3 axis = RaceCourseGeometry.SafeNormalize(coilAxis, Vector3.up);
            Vector3 u = RaceCourseGeometry.Perpendicular(axis);
            Vector3 w = RaceCourseGeometry.SafeNormalize(Vector3.Cross(axis, u), u);

            float theta0 = rng.Range(0f, 2f * Mathf.PI);
            Vector3 last = Vector3.zero, lastLeg = axis;

            for (int k = 0; k < coilRings; k++)
            {
                float theta = theta0 + k * coilStep;
                Vector3 rim = u * Mathf.Cos(theta) + w * Mathf.Sin(theta);
                Vector3 p = centre + rim * s.CoilRadius
                            + axis * ((k - (coilRings - 1) * 0.5f) * s.CoilPitch);

                // The coil's tangent, which is what the pilot is flying when they arrive.
                Vector3 t = (u * -Mathf.Sin(theta) + w * Mathf.Cos(theta)) * (s.CoilRadius * coilStep)
                            + axis * s.CoilPitch;

                into.Add(new RaceGate(p, RaceCourseGeometry.SafeNormalize(t, axis), s.RingRadius));

                if (k > 0) lastLeg = RaceCourseGeometry.SafeNormalize(p - last, lastLeg);
                last = p;
            }

            if (coilRings == 1) lastLeg = axis;

            Vector3 toNext = RaceCourseGeometry.SafeNormalize(next - last, lastLeg);
            Vector3 exit = last + RaceCourseGeometry.ClampTurn(lastLeg, toNext, s.ExitApproachDegrees)
                                  * s.ExitLead;
            Vector3 facing = RaceCourseGeometry.Deflect(
                ref rng, RaceCourseGeometry.SafeNormalize(next - exit, toNext), s.ExitConeDegrees);

            into.Add(new RaceGate(exit, facing, s.RingRadius));
        }
    }
}
