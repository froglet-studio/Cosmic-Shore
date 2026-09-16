using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>How the three rails are hung on the circuit's spine - see <see cref="RegattaCourse.BuildRails"/>.</summary>
    public struct RegattaRailSettings
    {
        /// <summary>Distance of every lane from the spine, in the plane perpendicular to it.
        /// Must sit inside every ring mouth with a prism to spare.</summary>
        public float LaneOffset;

        /// <summary>How many full turns the three lanes make around the spine per lap. At a whole
        /// number the braid closes on itself, and the same whole number is what makes the lanes
        /// FAIR: every lane spends the same arc on the inside of a corner as on the outside.</summary>
        public float TwistTurnsPerLap;

        /// <summary>Nominal centre-to-centre spacing of a lane's prisms. The lane's real spacing is
        /// its length divided by a whole number of prisms, so a loop never ends on a gap.</summary>
        public float PrismSpacing;

        /// <summary>Spine samples per leg (gate to gate). The braid is measured on this polyline
        /// and then resampled at the prism spacing, so it only has to be fine enough that the
        /// polyline's length is the curve's.</summary>
        public int SamplesPerLeg;
    }

    /// <summary>The laid rails: one position/rotation array per lane, in the circuit's own
    /// (cell-local) frame. Rotations look ALONG the lane with +y pointing away from the spine,
    /// so a (w, h, l) prism lays its long axis down the rail.</summary>
    public sealed class RegattaRailLayout
    {
        public readonly List<Vector3[]> LanePositions = new();
        public readonly List<Quaternion[]> LaneRotations = new();
        public readonly List<float> LaneLengths = new();
        public float SpineLength;

        public int PrismCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < LanePositions.Count; i++) n += LanePositions[i].Length;
                return n;
            }
        }
    }

    /// <summary>
    /// The Regatta's course: a closed circuit of switch rings cut for a MIXED fleet, with three
    /// super-shielded rails - one per playable domain - braided along the racing line.
    ///
    /// <para><b>The circuit is Headlong's solver, cut for nobody in particular.</b> Redline and
    /// Headlong each state their corner ladder against ONE hull's turning curve. This card seats
    /// every playable hull, from a 35 u/s Sparrow to a 1210 u/s Rhino, so its ladder is stated
    /// against the FLEET: the corner profiles are Redline's (the shared solver's proven reach at
    /// each rung), the safety floors are set where the fastest hull that cannot slow instantly -
    /// the Scarab at 216 u/s holds ~102 u - can still make the corner, and the mouths are a step
    /// wider than Headlong's because a Rhino crosses one at 1210 u/s. What a corner COSTS is then
    /// a property of the hull that arrives at it: a Rhino lifts and pays five seconds of ramp, a
    /// Manta eases a trigger, an Urchin on its rail pays nothing at all. That difference is the
    /// mode.</para>
    ///
    /// <para><b>The rails are the racing line, made of mass.</b> A closed Hermite spline is run
    /// through the gates with each ring's axis as its tangent, so the spine crosses every ring
    /// plane at the ring's centre, along its axis - which is what lets a rider thread a gate by
    /// riding. Three lanes sit <see cref="RegattaRailSettings.LaneOffset"/> off the spine at 120
    /// degrees, carried on a rotation-minimising frame (so the braid does not twist for no
    /// reason) plus exactly one authored twist per lap (so it does twist for a reason: an inside
    /// lane on one corner is the outside lane on another, and no domain's rail is shorter).
    /// Each lane is one prism per <see cref="RegattaRailSettings.PrismSpacing"/>, super-shielded
    /// in its domain's colour: an Urchin grinds its own colour at 300 u/s, a Squirrel skims any
    /// colour for boost energy, and no cone, gun, plate or spike can remove a prism of it - only
    /// the Rhino's energised sword can, and even then a rider bridges the hole at full pace.</para>
    ///
    /// <para><b>Pure and deterministic</b> (<see cref="RaceCourseGeometry"/>): the arena prefab
    /// lays the rails from a seed authored on the prefab, and the controller re-derives the SAME
    /// gates from the same seed to hang the rings on them. Neither reads the other at runtime;
    /// both read this.</para>
    /// </summary>
    public static class RegattaCourse
    {
        /// <summary>Rings per lap. A property of the ARENA, not of the target: the rails are laid
        /// through exactly this many rings, so the race length must be a multiple of it.</summary>
        public const int RingsPerLap = 8;

        /// <summary>The seed every prefab variant lays from unless one authors its own. One seed
        /// per intensity is derived from it (<see cref="SeedForIntensity"/>) so the four rungs
        /// are four different circuits rather than one circuit at four mouth sizes.</summary>
        public const int DefaultSeed = 20260915;

        /// <summary>The circuit's base circle. A regular octagon at 820 has 628 u legs.</summary>
        public const float BaseRadius = 820f;

        /// <summary>Nucleus.prefab at scale 400: 391.9 u. The controller derives its inner shell as
        /// this x 1.22 and its outer as the membrane's 1200 x 0.9; the arena has to bake the same
        /// two numbers because it lays the rails a second before the cell can answer.</summary>
        public const float NucleusWorldRadius = 391.9f;
        public const float InnerRadius = 478f;
        public const float OuterRadius = 1080f;

        public const int LaneCount = 3;

        /// <summary>Lane k is painted in the k-th playable domain - GameDataSO.ActiveDomains'
        /// order. A two-domain lobby finds the third lane hostile to both sides and, by the
        /// braid's symmetry, no shorter than either of theirs (Hijack's third-colour argument).</summary>
        public static readonly Domains[] LaneDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        public static int SeedForIntensity(int baseSeed, int intensity) =>
            unchecked(baseSeed + 7919 * Mathf.Clamp(intensity, 1, 4));

        /// <summary>
        /// The circuit per intensity. <b>Intensity is what mix of corners a lap asks for</b> -
        /// the rule Headlong and Redline record - and on a mixed grid a corner is a different
        /// price for every hull, so a tighter lap COMPRESSES the fleet: the hulls it costs most
        /// are the fast, straight-line ones, and a rider on its rail pays nothing.
        ///
        /// <code>
        ///      turn angles (deg), dealt so the big ones sit apart      floor    mouth
        ///   1:  90  72  58  48  38  28  16  10                           200u     110
        ///   2: 118  92  66  40  22  12   6   4                           150u      88
        ///   3: 135 112  78  22   7   3   2   1                           120u      72
        ///   4: 150 124  86   0   0   0   0   0                           100u      54
        /// </code>
        ///
        /// <para>The floors are ABSOLUTE and set for the fleet's worst case: a Scarab at its 216
        /// u/s ceiling holds ~102 u and can only shed speed through its 120 u/s^2 coast drag, so
        /// no corner is cut under 100 u; a Manta with one trigger released holds 82 u and a Rhino
        /// at cruise 29 u, so both clear every rung with room. The mouths start a step wider than
        /// Headlong's (96/72/58/46): a Rhino arrives at 1210 u/s and has a quarter of a Manta's
        /// lateral authority to correct with, and the three rails (offset 22 u, half-width 3 u)
        /// must pass inside the tightest mouth with a prism's clearance to spare - 54 leaves 29.
        /// The reach dials (AngularSpread) are Redline's, measured there to actually produce the
        /// profile's corners; the presentation caps cover half the hardest turn at each rung so
        /// the gates that most need to face you still can.</para>
        /// </summary>
        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new HeadlongCircuitSettings
            {
                GateCount = RingsPerLap,
                BaseRadius = BaseRadius,
                CornerProfile = new[]
                {
                    new[] {  90f,  72f,  58f,  48f,  38f,  28f,  16f,  10f },
                    new[] { 118f,  92f,  66f,  40f,  22f,  12f,   6f,   4f },
                    new[] { 135f, 112f,  78f,  22f,   7f,   3f,   2f,   1f },
                    new[] { 150f, 124f,  86f,   0f,   0f,   0f,   0f,   0f },
                }[i - 1],
                CornerRadiusFactor = 0f,
                CornerFloorRadius = new[] { 200f, 150f, 120f, 100f }[i - 1],
                RadialSwing = 0.42f,
                AngularSpread = new[] { 1.2f, 3.0f, 2.8f, 4.8f }[i - 1],
                LateralPerturbation = new[] { 160f, 200f, 240f, 280f }[i - 1],
                RingRadius = new[] { 110f, 88f, 72f, 54f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                MaxPresentDegrees = new[] { 50f, 72f, 76f, 80f }[i - 1],
                InnerRadius = InnerRadius,
                OuterRadius = OuterRadius,
                FirstGateDirection = Vector3.up,   // the equatorial spawn ring's pole
            };
        }

        /// <summary>The shipped braid. Offset 22 u on a 6 u wide prism puts the lanes 38 u apart
        /// and 29 u inside the tightest mouth; 8 u spacing on an 8 u long prism is a continuous
        /// ribbon, which a super-shield's 1.5x reach fuses into one spiked cable per lane.</summary>
        public static RegattaRailSettings DefaultRails => new()
        {
            LaneOffset = 22f,
            TwistTurnsPerLap = 1f,
            PrismSpacing = 8f,
            SamplesPerLeg = 64,
        };

        /// <summary>The rings, cell-local. The arena and the controller both call exactly this.</summary>
        public static List<RaceGate> BuildGates(int baseSeed, int intensity) =>
            HeadlongCircuit.Generate(SeedForIntensity(baseSeed, intensity), ForIntensity(intensity));

        /// <summary>
        /// Hang the three rails on a circuit. See the class summary for the construction; the
        /// contract a test can hold is: every lane crosses every ring plane inside the mouth at
        /// exactly the lane offset, every lane is a closed loop of evenly spaced prisms, and the
        /// three lanes are within a few percent of one length.
        /// </summary>
        public static RegattaRailLayout BuildRails(IReadOnlyList<RaceGate> gates, RegattaRailSettings rs)
        {
            var layout = new RegattaRailLayout();
            int n = gates?.Count ?? 0;
            if (n < 3) return layout;

            int per = Mathf.Max(8, rs.SamplesPerLeg);
            int total = n * per;

            // 1. The spine: a closed Hermite through the gates, tangent = each ring's axis scaled
            //    by the chord so the curve neither overshoots nor cuts the corner.
            var spine = new Vector3[total];
            var tangent = new Vector3[total];
            for (int i = 0; i < n; i++)
            {
                var a = gates[i];
                var b = gates[(i + 1) % n];
                float chord = (b.Position - a.Position).magnitude;
                Vector3 m0 = a.Axis.normalized * chord;
                Vector3 m1 = b.Axis.normalized * chord;
                for (int k = 0; k < per; k++)
                {
                    float t = k / (float)per;
                    spine[i * per + k] = Hermite(a.Position, m0, b.Position, m1, t);
                    tangent[i * per + k] = RaceCourseGeometry.SafeNormalize(
                        HermiteDerivative(a.Position, m0, b.Position, m1, t), a.Axis);
                }
            }

            // 2. Arc length along the closed spine.
            var s = new float[total + 1];
            for (int j = 0; j < total; j++)
                s[j + 1] = s[j] + (spine[(j + 1) % total] - spine[j]).magnitude;
            float length = s[total];
            layout.SpineLength = length;
            if (length < 1f) return layout;

            // 3. A rotation-minimising frame (double reflection), closed by spreading the
            //    mismatch it accumulates round the loop evenly along it.
            var normal = new Vector3[total];
            normal[0] = RaceCourseGeometry.Perpendicular(tangent[0]);
            for (int j = 0; j < total - 1; j++)
                normal[j + 1] = TransportFrame(spine[j], tangent[j], normal[j], spine[j + 1], tangent[j + 1]);
            Vector3 wrapped = TransportFrame(spine[total - 1], tangent[total - 1], normal[total - 1],
                                             spine[0], tangent[0]);
            float mismatch = Vector3.SignedAngle(wrapped, normal[0], tangent[0]) * Mathf.Deg2Rad;
            for (int j = 0; j < total; j++)
            {
                float correction = mismatch * (s[j] / length);
                normal[j] = RaceCourseGeometry.SafeNormalize(
                    RaceCourseGeometry.RotateAbout(normal[j], tangent[j], correction), normal[j]);
            }

            // 4. Each lane as a dense closed polyline, then resampled at the prism spacing.
            float twist = rs.TwistTurnsPerLap * 2f * Mathf.PI;
            for (int lane = 0; lane < LaneCount; lane++)
            {
                var dense = new Vector3[total];
                var radial = new Vector3[total];
                float phase = lane * 2f * Mathf.PI / LaneCount;
                for (int j = 0; j < total; j++)
                {
                    float theta = phase + twist * (s[j] / length);
                    Vector3 bin = Vector3.Cross(tangent[j], normal[j]);
                    radial[j] = normal[j] * Mathf.Cos(theta) + bin * Mathf.Sin(theta);
                    dense[j] = spine[j] + radial[j] * rs.LaneOffset;
                }

                Resample(dense, radial, rs.PrismSpacing, out var positions, out var rotations, out float laneLength);
                layout.LanePositions.Add(positions);
                layout.LaneRotations.Add(rotations);
                layout.LaneLengths.Add(laneLength);
            }

            return layout;
        }

        public static Vector3 Hermite(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return p0 * (2f * t3 - 3f * t2 + 1f) + m0 * (t3 - 2f * t2 + t)
                 + p1 * (-2f * t3 + 3f * t2) + m1 * (t3 - t2);
        }

        public static Vector3 HermiteDerivative(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float t)
        {
            float t2 = t * t;
            return p0 * (6f * t2 - 6f * t) + m0 * (3f * t2 - 4f * t + 1f)
                 + p1 * (-6f * t2 + 6f * t) + m1 * (3f * t2 - 2f * t);
        }

        /// <summary>One double-reflection step of a rotation-minimising frame (Wang et al. 2008):
        /// reflect the previous normal through the chord's bisecting plane, then through the
        /// plane bisecting the reflected and the new tangent.</summary>
        static Vector3 TransportFrame(Vector3 x0, Vector3 t0, Vector3 r0, Vector3 x1, Vector3 t1)
        {
            Vector3 v1 = x1 - x0;
            float c1 = Vector3.Dot(v1, v1);
            if (c1 < 1e-10f) return r0;
            Vector3 rL = r0 - v1 * (2f / c1 * Vector3.Dot(v1, r0));
            Vector3 tL = t0 - v1 * (2f / c1 * Vector3.Dot(v1, t0));
            Vector3 v2 = t1 - tL;
            float c2 = Vector3.Dot(v2, v2);
            Vector3 r1 = c2 < 1e-10f ? rL : rL - v2 * (2f / c2 * Vector3.Dot(v2, rL));
            // Keep it exactly perpendicular to the new tangent; float drift otherwise compounds
            // round a 512-sample loop.
            r1 -= t1 * Vector3.Dot(r1, t1);
            return RaceCourseGeometry.SafeNormalize(r1, RaceCourseGeometry.Perpendicular(t1));
        }

        /// <summary>Walk a closed polyline and drop a prism every <paramref name="spacing"/> - the
        /// spacing adjusted so a whole number of prisms closes the loop exactly.</summary>
        static void Resample(Vector3[] dense, Vector3[] radial, float spacing,
                             out Vector3[] positions, out Quaternion[] rotations, out float laneLength)
        {
            int m = dense.Length;
            var seg = new float[m];
            laneLength = 0f;
            for (int j = 0; j < m; j++)
            {
                seg[j] = (dense[(j + 1) % m] - dense[j]).magnitude;
                laneLength += seg[j];
            }

            int count = Mathf.Max(3, Mathf.RoundToInt(laneLength / Mathf.Max(0.5f, spacing)));
            float step = laneLength / count;
            positions = new Vector3[count];
            var ups = new Vector3[count];

            int j2 = 0;
            float into = 0f;   // distance already consumed on segment j2
            for (int k = 0; k < count; k++)
            {
                float target = k * step;
                while (j2 < m - 1 && into + seg[j2] < target) { into += seg[j2]; j2++; }
                float u = seg[j2] > 1e-6f ? Mathf.Clamp01((target - into) / seg[j2]) : 0f;
                positions[k] = Vector3.Lerp(dense[j2], dense[(j2 + 1) % m], u);
                ups[k] = RaceCourseGeometry.SafeNormalize(
                    Vector3.Lerp(radial[j2], radial[(j2 + 1) % m], u), radial[j2]);
            }

            rotations = new Quaternion[count];
            for (int k = 0; k < count; k++)
            {
                Vector3 forward = RaceCourseGeometry.SafeNormalize(
                    positions[(k + 1) % count] - positions[k], Vector3.forward);
                Vector3 up = ups[k] - forward * Vector3.Dot(ups[k], forward);
                up = RaceCourseGeometry.SafeNormalize(up, RaceCourseGeometry.Perpendicular(forward));
                rotations[k] = Quaternion.LookRotation(forward, up);
            }
        }
    }
}
