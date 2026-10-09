using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure arithmetic of the Stoat's FIELD DIPOLE and PATHFINDER (<c>R_VesselActions/STOAT_DIPOLE.md</c>),
    /// ported from the round-15 field trajectory of the Stoat Flight Studio
    /// (<c>Docs/Studios/StoatFlightStudio.html</c>: <c>dipoleOffsets</c>, <c>fieldAccel</c>,
    /// <c>fieldSubstep</c>, <c>fieldPath</c>). No scene, no time, no input device: every method is a
    /// function of its arguments, so <c>StoatDipoleTests</c> hold it directly and the executors only
    /// apply it.
    ///
    /// <para><b>Frames.</b> Unity's forward is +z (the studio's was −z); the maths is written against
    /// <c>Vector3.forward</c> and a rotation, so it is frame-free.</para>
    /// </summary>
    public static class StoatDipoleMath
    {
        // ------------------------------------------------------------------ placement

        /// <summary>
        /// Where the two triggers put the poles relative to each other, in the frame the pair was
        /// opened in. <see cref="Lateral"/> is signed: positive puts the SINK (the black hole) on the
        /// hull's RIGHT. <see cref="Longitudinal"/> is never negative: the sink is always the nearer
        /// pole and the source (the white hole) the further, so a pilot shoots into the sink and out of
        /// the source.
        /// </summary>
        public readonly struct Separation
        {
            public readonly float Lateral;
            public readonly float Longitudinal;
            public Separation(float lateral, float longitudinal) { Lateral = lateral; Longitudinal = Mathf.Max(0f, longitudinal); }
            public float Magnitude => Mathf.Sqrt(Lateral * Lateral + Longitudinal * Longitudinal);
        }

        /// <summary>
        /// The two triggers, each on the squeeze curve (<c>t^exponent</c>): their DIFFERENCE sets the
        /// sideways separation (up to <paramref name="sidewaysMax"/> for one full trigger, the sink on the
        /// deeper trigger's side), their SUM the lengthways one (up to <paramref name="lengthwaysMax"/>
        /// with both full). One full trigger alone gives a slight diagonal (half the lengthways
        /// separation); both full put the sink dead ahead and the source beyond it on the same line.
        /// </summary>
        public static Separation TargetSeparation(float left01, float right01, float exponent, float sidewaysMax, float lengthwaysMax)
        {
            float e = Mathf.Max(0.01f, exponent);
            float a = Mathf.Pow(Mathf.Clamp01(left01), e), b = Mathf.Pow(Mathf.Clamp01(right01), e);
            return new Separation((b - a) * Mathf.Max(0f, sidewaysMax), 0.5f * (a + b) * Mathf.Max(0f, lengthwaysMax));
        }

        /// <summary>Eases a separation toward its target at <paramref name="followRate"/> per second (both components).</summary>
        public static Separation Follow(Separation current, Separation target, float followRate, float dt)
        {
            float k = 1f - Mathf.Exp(-Mathf.Max(0.1f, followRate) * Mathf.Max(0f, dt));
            return new Separation(Mathf.Lerp(current.Lateral, target.Lateral, k), Mathf.Lerp(current.Longitudinal, target.Longitudinal, k));
        }

        /// <summary>
        /// The two poles about the pair's fixed middle, in the frame it was opened in: the sink half the
        /// separation toward its side and back toward the pilot, the source mirrored through the middle.
        /// </summary>
        public static void PolePositions(Vector3 middle, Vector3 right, Vector3 forward, Separation s, out Vector3 sink, out Vector3 source)
        {
            var half = right * (0.5f * s.Lateral) - forward * (0.5f * s.Longitudinal);
            sink = middle + half;
            source = middle - half;
        }

        /// <summary>
        /// Let go of both triggers and the poles fall back together; the pair annihilates once their
        /// horizons touch (or they are within a unit, so a tap that never opened them ends at once).
        /// </summary>
        public static bool ShouldAnnihilate(bool anyTriggerHeld, float separation, float sinkHorizon, float sourceHorizon) =>
            !anyTriggerHeld && separation <= Mathf.Max(1f, 0.5f * (sinkHorizon + sourceHorizon));

        // ------------------------------------------------------------------ the field

        /// <summary>
        /// One dipole as a field: the sink's pull GM/(r − r_s)² (Paczyński–Wiita, the law
        /// <c>BlackHolePhysics</c> uses on prisms) and the source's push GM·r/(r² + ε²)^1.5, softened over
        /// <see cref="SourceSoftening"/>, the sum capped at <see cref="AccelerationCap"/>.
        /// </summary>
        public struct Field
        {
            public Vector3 Sink, Source;
            public float SinkGM, SinkHorizon, SourceGM, SourceHorizon, SourceSoftening, AccelerationCap;
            public bool Exists => SinkGM > 0f || SourceGM > 0f;
        }

        public static Vector3 FieldAcceleration(Vector3 p, in Field f)
        {
            var a = Vector3.zero;
            var toSink = f.Sink - p;
            float r = toSink.magnitude + 1e-6f;
            float x = Mathf.Max(r - f.SinkHorizon, 0.05f * Mathf.Max(f.SinkHorizon, 1e-3f));
            a += toSink * (f.SinkGM / (x * x) / r);
            var toSource = f.Source - p;
            float e = Mathf.Max(1e-3f, f.SourceSoftening);
            float q = toSource.sqrMagnitude + e * e;
            a -= toSource * (f.SourceGM / (q * Mathf.Sqrt(q)));
            return f.AccelerationCap > 0f ? Vector3.ClampMagnitude(a, f.AccelerationCap) : a;
        }

        // ------------------------------------------------------------------ flight in the field

        /// <summary>A hull flying in the field: where it is, which way it points, and the gravity speed the
        /// field has added along its nose (the engine's own speed is passed in separately).</summary>
        public struct Body
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float GravitySpeed;
            /// <summary>Total radians the FIELD has turned this body (steering not counted) — the warp measure.</summary>
            public float Bend;
        }

        /// <summary>The numbers one step of field flight reads.</summary>
        public struct FlightSettings
        {
            /// <summary>The fastest the field may turn the nose, rad/s.</summary>
            public float TurnCap;
            /// <summary>How hard the engine pulls the gravity speed back toward cruise, /s (0 = pure gravity).</summary>
            public float Grip;
            /// <summary>The gravity speed's ceiling (the studio's 4 × cruise).</summary>
            public float GravitySpeedCeiling;
        }

        /// <summary>
        /// One step of flight in the field (the studio's <c>fieldSubstep</c>): the field's component ALONG
        /// the nose changes the gravity speed (floored so the hull never flies backward), the PERPENDICULAR
        /// component turns the nose at a/v (capped), and the body moves on along its new nose. The engine
        /// speed is the caller's. Both the flight and the prediction call this, so the line shows the
        /// flight's own law.
        /// </summary>
        public static void Step(ref Body body, in Field field, float engineSpeed, float h, in FlightSettings s)
        {
            var fwd = body.Rotation * Vector3.forward;
            if (field.Exists)
            {
                var a = FieldAcceleration(body.Position, field);
                float v = Mathf.Max(engineSpeed + body.GravitySpeed, 5f);
                float along = Vector3.Dot(a, fwd);
                float g = Mathf.Clamp(body.GravitySpeed + along * h, -Mathf.Max(0f, engineSpeed - 8f), Mathf.Max(0f, s.GravitySpeedCeiling));
                body.GravitySpeed = g * Mathf.Exp(-Mathf.Max(0f, s.Grip) * h);
                var perp = a - fwd * along;
                float am = perp.magnitude;
                if (am > 1e-4f)
                {
                    float angle = Mathf.Min(am / v, Mathf.Max(0f, s.TurnCap)) * h;
                    var axis = Vector3.Cross(fwd, perp);
                    if (axis.sqrMagnitude > 1e-12f)
                    {
                        body.Rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis.normalized) * body.Rotation;
                        body.Bend += angle;
                        fwd = body.Rotation * Vector3.forward;
                    }
                }
            }
            body.Position += fwd * (Mathf.Max(0f, engineSpeed + body.GravitySpeed) * h);
        }

        /// <summary>
        /// Through the wormhole (<c>BlackHoleVesselPull.TryCarryThrough</c>'s rule): a body inside the
        /// sink's horizon comes out at the point reflection through the source, just outside the source's
        /// horizon (<paramref name="exitGap"/> horizons), heading kept.
        /// </summary>
        public static Vector3 ExitPoint(Vector3 position, in Field f, float exitGap)
        {
            var off = position - f.Sink;
            float dist = Mathf.Max(off.magnitude, 1e-3f);
            return f.Source - off * (Mathf.Max(dist, Mathf.Max(1f, exitGap) * f.SourceHorizon) / dist);
        }

        // ------------------------------------------------------------------ the pathfinder

        public enum PathEnd { Open = 0, Loop = 1, Hole = 2 }

        /// <summary>The numbers the prediction reads.</summary>
        public struct PathSettings
        {
            public float Length;          // u of path
            public float Step;            // u between two points
            public float NoseOffset;      // u ahead of the hull before the first drawn point
            public float LoopMargin;      // the line stops where it comes back within this of itself …
            public float MinLoop;         // … at least this far on (so it never touches its own neighbours)
            public float WarpDegrees;     // the poles must bend the path at least this much to count as WARPED
            public float ExitGap;         // where the wormhole puts you out, in source horizons
            public bool PortalOpen;       // the sink carries you through (else the path stops at the horizon)
        }

        public struct PathResult
        {
            public int Count;             // points written
            public PathEnd End;
            public int LoopIndex;         // with End == Loop: the earlier point the path came back to
            public int JumpCount;         // passes through the wormhole
            public float BendDegrees;     // how far the poles turned the path in total
            public bool Warped;           // the pathfinder's verdict: the boost is on
        }

        /// <summary>
        /// The pathfinder's line (the studio's <c>fieldPath</c>): the hull's own flight run forward on the
        /// current inputs — its engine speed held, its present steering rate held
        /// (<paramref name="bodyAngularVelocity"/>, rad/s in the hull's own axes) — through the field,
        /// from the hull (the first <see cref="PathSettings.NoseOffset"/> u are flown but not written).
        /// It follows the pilot through the wormhole, stops at <see cref="PathSettings.Length"/> or where
        /// it would loop back onto itself, and reports whether the poles WARP it at all: a total bend of
        /// <see cref="PathSettings.WarpDegrees"/> or more, or a pass through the wormhole. Steering alone
        /// can never warp it. <paramref name="points"/> receives the line; <paramref name="jumps"/> the
        /// index of the first point after each pass (the line is not joined across one).
        /// </summary>
        public static PathResult PredictPath(Body start, float engineSpeed, Vector3 bodyAngularVelocity, in Field field, bool hasField,
            in FlightSettings flight, in PathSettings s, Vector3[] points, int[] jumps)
        {
            var result = new PathResult { LoopIndex = -1 };
            if (points == null || points.Length < 2) return result;
            var body = start;
            body.Bend = 0f;
            float ds = Mathf.Max(0.5f, s.Step);
            int cap = Mathf.Min(points.Length - 1, Mathf.CeilToInt(Mathf.Max(ds, s.Length) / ds));
            float margin2 = s.LoopMargin * s.LoopMargin;
            int skip = Mathf.Max(2, Mathf.CeilToInt(s.MinLoop / ds));
            float gFar = body.GravitySpeed;
            // a local function may not capture an `in` parameter (CS1628): copy them
            var f = hasField ? field : default;
            var fl = flight;
            float spin = bodyAngularVelocity.magnitude;
            var spinAxis = spin > 1e-6f ? bodyAngularVelocity / spin : Vector3.up;

            bool StepBy(ref Body b, float dist)
            {
                float v = Mathf.Max(5f, engineSpeed + b.GravitySpeed);
                float t = dist / v;
                int k = 1;
                if (hasField)
                {
                    var a = FieldAcceleration(b.Position, f);
                    float r = Vector3.Distance(b.Position, f.Sink);
                    k = Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(a.magnitude / v, fl.TurnCap) * t / 0.02f));
                    k = Mathf.Max(k, Mathf.CeilToInt(dist / Mathf.Max(0.1f, 0.2f * (r - f.SinkHorizon))));
                    k = Mathf.Min(k, 96);
                }
                float h = t / k;
                for (int i = 0; i < k; i++)
                {
                    if (spin > 1e-6f) b.Rotation = b.Rotation * Quaternion.AngleAxis(spin * h * Mathf.Rad2Deg, spinAxis);
                    Step(ref b, f, engineSpeed, h, fl);
                    if (hasField)
                    {
                        float r = Vector3.Distance(b.Position, f.Sink);
                        if (r > 4f * f.SinkHorizon) gFar = b.GravitySpeed;
                        if (r <= f.SinkHorizon) return true;
                    }
                }
                return false;
            }

            for (float pre = 0f, nose = Mathf.Max(0f, s.NoseOffset); pre < nose - 1e-4f;)
            {
                float d = Mathf.Min(ds, nose - pre); pre += d;
                if (StepBy(ref body, d)) break;
            }
            points[0] = body.Position;
            int n = 1;
            for (; n <= cap; n++)
            {
                bool inHole = StepBy(ref body, ds);
                points[n] = body.Position;
                if (inHole)
                {
                    if (!s.PortalOpen || n >= cap || jumps == null || result.JumpCount >= jumps.Length) { result.End = PathEnd.Hole; n++; break; }
                    body.Position = ExitPoint(body.Position, f, s.ExitGap);
                    body.GravitySpeed = gFar;
                    n++;
                    points[n] = body.Position;
                    jumps[result.JumpCount++] = n;
                }
                bool hit = false;
                for (int j = 0; j <= n - skip && !hit; j++)
                    if ((points[j] - body.Position).sqrMagnitude <= margin2) { hit = true; result.LoopIndex = j; }
                if (hit) { result.End = PathEnd.Loop; n++; break; }
            }
            result.Count = Mathf.Min(n, cap + 1);
            result.BendDegrees = body.Bend * Mathf.Rad2Deg;
            result.Warped = hasField && (result.JumpCount > 0 || result.End == PathEnd.Hole || result.BendDegrees >= s.WarpDegrees);
            return result;
        }

        // ------------------------------------------------------------------ the autopilot

        /// <summary>
        /// Which triggers an autopilot pulls for a target at <paramref name="localTarget"/> (the hull's own
        /// frame): nothing when it is nearer than <paramref name="minDistance"/> or more than
        /// <paramref name="maxDegrees"/> off the nose (the pair is laid AHEAD, so it cannot help behind);
        /// BOTH when it is within <paramref name="straightDegrees"/> — the sink dead ahead, the wormhole
        /// shoots the hull at the target and the warp boosts it; otherwise the trigger on the target's
        /// side, so the sink sits that way and the field turns the nose toward it.
        /// </summary>
        public static bool AutopilotTriggers(Vector3 localTarget, float minDistance, float straightDegrees, float maxDegrees,
            out bool left, out bool right)
        {
            left = right = false;
            if (localTarget.sqrMagnitude < minDistance * minDistance) return false;
            float off = Vector3.Angle(Vector3.forward, localTarget);
            if (off > maxDegrees) return false;
            if (off <= straightDegrees) { left = right = true; return true; }
            if (localTarget.x >= 0f) right = true; else left = true;
            return true;
        }

        // ------------------------------------------------------------------ the dots

        /// <summary>
        /// Lays the pathfinder's dots along a polyline AS IT IS SEEN (screen space): one dot at the first
        /// visible point, then one every <paramref name="spacing"/> pixels of on-screen length. A point
        /// marked not <paramref name="visible"/> (behind the camera) breaks the line, and so does a
        /// <paramref name="breakBefore"/> point (the far side of a pass through the wormhole: the line is not
        /// joined across it); the next visible point restarts it with a dot. Returns the dots written to
        /// <paramref name="dots"/> (its length caps them); <paramref name="sourceIndex"/> receives which
        /// segment each came from, for its fade.
        /// </summary>
        public static int LayDots(Vector2[] screen, bool[] visible, bool[] breakBefore, int count, float spacing,
            Vector2[] dots, int[] sourceIndex)
        {
            if (screen == null || dots == null || count <= 0) return 0;
            spacing = Mathf.Max(0.5f, spacing);
            int n = 0;
            bool have = false;
            float carry = 0f;
            Vector2 prev = default;
            for (int i = 0; i < count && n < dots.Length; i++)
            {
                if (visible != null && !visible[i]) { have = false; continue; }
                var p = screen[i];
                if (!have || (breakBefore != null && breakBefore[i]))
                {
                    dots[n] = p;
                    if (sourceIndex != null) sourceIndex[n] = i;
                    n++;
                    have = true;
                    carry = spacing;
                    prev = p;
                    continue;
                }
                var d = p - prev;
                float len = d.magnitude;
                float t = carry;
                for (; t <= len && n < dots.Length; t += spacing)
                {
                    dots[n] = prev + d * (t / Mathf.Max(len, 1e-6f));
                    if (sourceIndex != null) sourceIndex[n] = i;
                    n++;
                }
                carry = t - len;
                prev = p;
            }
            return n;
        }

        // ------------------------------------------------------------------ the boost

        /// <summary>
        /// The warp boost's multiplier: it rises toward <paramref name="boost"/> at <paramref name="riseRate"/>
        /// per second while the path is warped and falls back to 1 over <paramref name="fadeSeconds"/> once it
        /// is not.
        /// </summary>
        public static float StepBoost(float current, bool warped, float boost, float riseRate, float fadeSeconds, float dt)
        {
            current = Mathf.Max(1f, current);
            if (warped) return current + (Mathf.Max(1f, boost) - current) * (1f - Mathf.Exp(-Mathf.Max(0.1f, riseRate) * dt));
            return 1f + (current - 1f) * Mathf.Exp(-dt / Mathf.Max(0.01f, fadeSeconds));
        }
    }
}
