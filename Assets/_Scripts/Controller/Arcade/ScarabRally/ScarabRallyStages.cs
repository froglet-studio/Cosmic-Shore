using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One prism a Scarab Rally berm lays: pose, size, kind, and the hairpin gate it belongs to.</summary>
    public readonly struct ScarabRallyBermPrism
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly PrismKind Kind;
        public readonly int Gate;

        public ScarabRallyBermPrism(Vector3 position, Quaternion rotation, Vector3 scale, PrismKind kind, int gate)
        {
            Position = position; Rotation = rotation; Scale = scale; Kind = kind; Gate = gate;
        }
    }

    /// <summary>
    /// Scarab Rally's course features (SCARABRALLY.md "The stages"), stated against the Scarab's own handling and nothing
    /// else - the mode adds no ability and changes no number of the vessel. Pure, so the tests and the offline harness
    /// (<c>Tools/Build/scarab_rally_harness</c>) run it without Unity.
    ///
    /// <para><b>The berm (I2+).</b> A hairpin ring turns the course 90-150 degrees. Flat out the Scarab turns on a
    /// ~102 u circle (<see cref="ScarabRallyCourse.FullThrottleRadius"/>), so a steered hairpin swings out past the ring
    /// along the line it arrived on. The berm is a wall of prisms standing across that line,
    /// <see cref="BermSetback"/> past the ring, on the outside of the turn: the steered circle runs into it. Two lines
    /// miss it - a LIFT (shed speed until the circle fits inside the setback, then wind back up: 2.4 s from rest) or a
    /// DRIFT (LT: the course slides on through the ring while the nose swings onto the next ring, and letting go points
    /// the whole speed where the nose points). The drift keeps the speed; that is the verb the berm asks for. Plain
    /// prisms at I2-I3 (a hit breaks them and costs a moment), danger at I4 (a hit stops the hull dead).</para>
    ///
    /// <para><b>The Time crystal on the drift line.</b> A drift let go at the ring leaves along the next leg from the
    /// ring itself; a steered circle or a lift rejoins that leg far down it. So each hairpin's Time crystal sits on the
    /// next leg <see cref="ExitCrystalLead"/> past the ring: the drift collects it, the circle swings past it. Time is
    /// the Scarab's own throttle ceiling (216 to 324 u/s) and, at Time 5, its Snap Dash - the race's reward for reading
    /// it is the vessel's own speed. I1 has no hairpins, so its crystals sit on the line of every
    /// <see cref="StraightCrystalEvery"/>th leg and teach that Time is speed.</para>
    /// </summary>
    public static class ScarabRallyStages
    {
        // ── geometry ────────────────────────────────────────────────────

        /// <summary>A corner at least this sharp is a hairpin (the course's hairpins are 90-150 degrees; its ordinary
        /// corners at most 55).</summary>
        public const float HairpinDegrees = 85f;

        /// <summary>u past the hairpin ring, along the line the pilot arrived on, where the berm stands: 0.8 of the
        /// full-throttle circle, so the steered circle (which swings a whole radius past the ring) runs into it and a
        /// circle a lift has shrunk to under ~80 u does not.</summary>
        public static float BermSetback => 0.8f * ScarabRallyCourse.FullThrottleRadius;

        /// <summary>u the berm reaches past the ring's rim toward the inside of the turn, beyond where the steered
        /// circle crosses it (~41 u in from the line at Time 0; ~27 at Time 10).</summary>
        public const float BermInsideReach = 70f;

        /// <summary>u the berm reaches past the ring's rim on the outside of the turn.</summary>
        public const float BermOutsideReach = 14f;

        /// <summary>u the berm reaches past the ring's rim above and below the plane of the turn.</summary>
        public const float BermDepthReach = 14f;

        /// <summary>The cell the berm's prisms sit on, and each prism's size (2 u between neighbours: no hull fits
        /// through).</summary>
        public const float BermPitch = 20f;
        public const float BermPrismSize = 18f;
        public const float BermPrismThickness = 6f;

        /// <summary>u a berm prism keeps off every ring's rim (Switchback's mouth clearance) and off every leg's line
        /// other than the arriving one it is built to stop.</summary>
        public const float RimClearance = 30f;
        public const float LineClearance = 30f;

        /// <summary>u down the next leg from a hairpin ring to its Time crystal.</summary>
        public const float ExitCrystalLead = 70f;

        /// <summary>I1's straight crystals: on the midpoint of every Nth leg, from the third.</summary>
        public const int StraightCrystalEvery = 4;

        // ── the ladder ───────────────────────────────────────────────────

        static int I(int intensity) => Mathf.Clamp(intensity, 1, 4) - 1;

        /// <summary>Whether this intensity lays berms on its hairpins: I2 (where the hairpins arrive) and up.</summary>
        public static bool HasBerms(int intensity) => intensity >= 2;

        /// <summary>The berm's prisms: plain at I2-I3 (a hit breaks them and costs a moment), danger at I4.</summary>
        public static PrismKind BermKind(int intensity) => intensity >= 4 ? PrismKind.Danger : PrismKind.Plain;

        // ── reading the course ───────────────────────────────────────────

        /// <summary>The turn the course makes at gate <paramref name="g"/> (degrees), 0 at either end.</summary>
        public static float TurnDegrees(IReadOnlyList<RaceGate> course, int g)
        {
            if (course == null || g <= 0 || g >= course.Count - 1) return 0f;
            return RaceCourseGeometry.Angle(course[g].Position - course[g - 1].Position,
                                            course[g + 1].Position - course[g].Position);
        }

        public static bool IsHairpin(IReadOnlyList<RaceGate> course, int g) => TurnDegrees(course, g) >= HairpinDegrees;

        /// <summary>The hairpin's frame: <paramref name="x"/> the arriving line, <paramref name="y"/> the inside of the
        /// turn (toward the next ring, square to x), <paramref name="z"/> square to both. False for a straight.</summary>
        public static bool Frame(IReadOnlyList<RaceGate> course, int g, out Vector3 x, out Vector3 y, out Vector3 z)
        {
            x = y = z = default;
            if (course == null || g <= 0 || g >= course.Count - 1) return false;
            x = (course[g].Position - course[g - 1].Position).normalized;
            Vector3 dOut = (course[g + 1].Position - course[g].Position).normalized;
            y = dOut - x * Vector3.Dot(dOut, x);
            if (y.sqrMagnitude < 1e-6f) return false;
            y = y.normalized;
            z = Vector3.Cross(x, y);
            return true;
        }

        /// <summary>
        /// Every berm on <paramref name="course"/> at <paramref name="intensity"/>, appended to
        /// <paramref name="prisms"/>: one wall per hairpin, square to the arriving line, <see cref="BermSetback"/> past
        /// the ring. Prisms that would sit inside the nucleus (<paramref name="centre"/>, <paramref name="clearRadius"/>),
        /// near any ring's rim or on any other leg's line are left out.
        /// </summary>
        public static void Berms(IReadOnlyList<RaceGate> course, int intensity, Vector3 centre, float clearRadius,
                                 List<ScarabRallyBermPrism> prisms)
        {
            if (course == null || course.Count < 3 || !HasBerms(intensity)) return;
            var kind = BermKind(intensity);
            var scale = new Vector3(BermPrismSize, BermPrismSize, BermPrismThickness);
            for (int g = 1; g < course.Count - 1; g++)
            {
                if (!IsHairpin(course, g) || !Frame(course, g, out var x, out var y, out var z)) continue;
                float r = course[g].Radius;
                Vector3 origin = course[g].Position + x * BermSetback;
                var rotation = Quaternion.LookRotation(x, z);
                float y0 = -(r + BermOutsideReach), y1 = r + BermInsideReach;
                float z0 = -(r + BermDepthReach), z1 = r + BermDepthReach;
                for (float b = y0 + BermPitch * 0.5f; b <= y1; b += BermPitch)
                    for (float c = z0 + BermPitch * 0.5f; c <= z1; c += BermPitch)
                    {
                        Vector3 p = origin + y * b + z * c;
                        if (!Clear(course, g, centre, clearRadius, p)) continue;
                        prisms.Add(new ScarabRallyBermPrism(p, rotation, scale, kind, g));
                    }
            }
        }

        static bool Clear(IReadOnlyList<RaceGate> course, int g, Vector3 centre, float clearRadius, Vector3 p)
        {
            float half = BermPrismSize * 0.5f;
            if (clearRadius > 0f && (p - centre).magnitude < clearRadius + half) return false;
            for (int i = 0; i < course.Count; i++)
                if (DistanceToMouth(p, course[i]) < RimClearance + half) return false;
            // Every leg but the one it stands across (g-1 -> g, which it is beyond by construction).
            for (int i = 1; i < course.Count; i++)
            {
                if (i == g) continue;
                if (SwitchbackFeatures.DistanceToSegment(p, course[i - 1].Position, course[i].Position) < LineClearance + half)
                    return false;
            }
            return true;
        }

        /// <summary>The distance from <paramref name="p"/> to <paramref name="gate"/>'s mouth: the disc its rim bounds.</summary>
        public static float DistanceToMouth(Vector3 p, in RaceGate gate)
        {
            Vector3 axis = gate.Axis.sqrMagnitude > 1e-6f ? gate.Axis.normalized : Vector3.up;
            Vector3 d = p - gate.Position;
            float h = Vector3.Dot(d, axis);
            float rho = (d - axis * h).magnitude;
            float out_ = Mathf.Max(0f, rho - gate.Radius);
            return Mathf.Sqrt(out_ * out_ + h * h);
        }

        /// <summary>Where each Time crystal sits: a hairpin's on the next leg <see cref="ExitCrystalLead"/> past the
        /// ring; at I1 (no hairpins) the midpoint of every <see cref="StraightCrystalEvery"/>th leg.</summary>
        public static void Crystals(IReadOnlyList<RaceGate> course, int intensity, List<Vector3> crystals)
        {
            if (course == null || course.Count < 3) return;
            if (intensity <= 1)
            {
                for (int g = 2; g < course.Count; g += StraightCrystalEvery)
                    crystals.Add((course[g - 1].Position + course[g].Position) * 0.5f);
                return;
            }
            for (int g = 1; g < course.Count - 1; g++)
            {
                if (!IsHairpin(course, g)) continue;
                Vector3 leg = course[g + 1].Position - course[g].Position;
                float lead = Mathf.Min(ExitCrystalLead, leg.magnitude * 0.5f);
                crystals.Add(course[g].Position + leg.normalized * lead);
            }
        }

        // ── the steered circle and the drift, for the tests and the AI ───

        /// <summary>The turn rate (deg/s) of a Scarab drifting at <paramref name="speed"/>: the sharp tier (both drift
        /// actions on LT, ScarabSharpDriftAction's Mult 1.8 on Pitch/YawScaler) - what a full trigger gives.</summary>
        public const float SharpDriftTurnMultiplier = 1.8f;

        public static float SharpDriftTurnRate(float speed) =>
            speed * ScarabRallyCourse.RotationThrottleScaler + ScarabRallyCourse.TurnScaler * SharpDriftTurnMultiplier;

        /// <summary>Where the steered circle crosses the berm's plane, as distance toward the inside of the turn from
        /// the arriving line (a circle of <paramref name="radius"/> that starts at the ring heading along x). Negative
        /// infinity when the circle never reaches the plane (radius under the setback): a lift that fits.</summary>
        public static float SteeredCrossing(float radius)
        {
            float b = BermSetback;
            if (radius <= b) return float.NegativeInfinity;
            return radius - Mathf.Sqrt(radius * radius - b * b);
        }

        /// <summary>The fastest speed whose flat-out circle fits inside the setback: the speed a lift has to shed to.</summary>
        public static float LiftSpeed()
        {
            float lo = 0f, hi = ScarabRallyCourse.TopSpeed;
            for (int k = 0; k < 40; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (ScarabRallyCourse.CornerRadiusAt(mid) < BermSetback) lo = mid; else hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// The AI's drift (server, AI hulls only - SCARABRALLY.md "AI"): whether a hull <paramref name="secondsToPlane"/>
        /// from a hairpin that turns <paramref name="turnDegrees"/> should squeeze LT now, so its nose has swung onto the
        /// next ring by the time it slides through this one. <paramref name="minTurnDegrees"/> is its difficulty's
        /// threshold (<see cref="AIDriftMinTurn"/>); a line that misses the mouth is left to the juke first.
        /// </summary>
        public static bool ShouldStartDrift(float turnDegrees, float speed, float secondsToPlane, bool threadsMouth,
                                            float minTurnDegrees, float leadSeconds)
        {
            if (turnDegrees < minTurnDegrees || !threadsMouth || secondsToPlane <= 0.05f || speed < 1f) return false;
            float swing = turnDegrees / SharpDriftTurnRate(speed);
            return secondsToPlane <= swing + leadSeconds;
        }

        /// <summary>The sharpest hairpin each AI difficulty will NOT drift (it steers it, and meets the berm): Easy
        /// never drifts, Medium drifts 110 degrees and up, Hard 95, Legendary every hairpin.</summary>
        public static float AIDriftMinTurn(int level) => level switch
        {
            2 => 110f,
            3 => 95f,
            4 => HairpinDegrees,
            _ => float.PositiveInfinity,
        };

        /// <summary>
        /// Where a hull flying <paramref name="velocity"/> from <paramref name="position"/> crosses the plane of
        /// <paramref name="gate"/>: seconds to it, and whether it is inside the mouth by <paramref name="margin"/>.
        /// False when it is not closing on the plane.
        /// </summary>
        public static bool PlaneCrossing(Vector3 position, Vector3 velocity, in RaceGate gate, float margin,
                                         out float seconds, out bool inside)
        {
            seconds = 0f; inside = false;
            Vector3 axis = gate.Axis.sqrMagnitude > 1e-6f ? gate.Axis.normalized : velocity.normalized;
            float closing = Vector3.Dot(velocity, axis);
            float ahead = Vector3.Dot(gate.Position - position, axis);
            if (Mathf.Abs(closing) < 1f || ahead / closing <= 0f) return false;
            seconds = ahead / closing;
            Vector3 off = position + velocity * seconds - gate.Position;
            off -= axis * Vector3.Dot(off, axis);
            inside = off.magnitude <= Mathf.Max(0f, gate.Radius - margin);
            return true;
        }
    }
}
