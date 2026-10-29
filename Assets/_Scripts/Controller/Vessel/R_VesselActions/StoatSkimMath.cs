using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure arithmetic of the Stoat's SKIM SLING (Time mode since 2026-10-29, <c>STOAT_DIPOLE.md</c> §1q): where the
    /// pair goes, when the TIDEPOOL captures, what a flat or steep entry keeps (the PLUNK), how the spin feeds the ride,
    /// what a release pays and how the spin HOOKS the launch. No scene, no time, no input device: every method is a
    /// function of its arguments, so <c>StoatSkimTests</c> hold it directly and <see cref="StoatSlingExecutor"/> only
    /// applies it.
    ///
    /// <para><b>Names.</b> The SKIMSHELL is the sphere of radius R round the pair's midpoint that the hull rides; the
    /// SKIMLINE is its equator (square to the spin axis); the TIDEPOOL is the capture zone, <c>tidepoolScale</c> × R;
    /// a SKIP is one launch (a chain of them is a skip run).</para>
    ///
    /// <para><b>Handedness.</b> The spin axis is the hull's up for the RIGHT trigger and its down for the LEFT. The spin's
    /// surface flow at a point is <c>spinAxis × r̂</c> (Unity's rotations turn a point the same way), so a left press,
    /// laid to the left, carries the hull round it to the left - the turn the trigger names.</para>
    /// </summary>
    public static class StoatSkimMath
    {
        // ------------------------------------------------------------------ placement

        /// <summary>The pair's midpoint: <paramref name="ahead"/> along the nose and <paramref name="side"/> toward the
        /// pressed side - out in front, where the chase camera sees it.</summary>
        public static Vector3 Midpoint(Vector3 hull, Vector3 forward, Vector3 right, bool left, float ahead, float side) =>
            hull + forward.normalized * Mathf.Max(0f, ahead) + (left ? -right.normalized : right.normalized) * Mathf.Max(0f, side);

        /// <summary>The spin axis: the hull's up for the right trigger (clockwise seen from above), down for the left.</summary>
        public static Vector3 SpinAxis(bool left, Vector3 up)
        {
            var u = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.up;
            return left ? -u : u;
        }

        /// <summary>Each pole's distance from the midpoint along the spin axis: <paramref name="share"/> × R.</summary>
        public static float PoleHalfGap(float radius, float share) => Mathf.Max(0f, radius) * Mathf.Clamp(share, 0.05f, 0.95f);

        // ------------------------------------------------------------------ capture

        /// <summary>The Tidepool's radius: <paramref name="scale"/> × the Skimshell's.</summary>
        public static float TidepoolRadius(float radius, float scale) => Mathf.Max(0f, radius) * Mathf.Max(1f, scale);

        /// <summary>
        /// The Tidepool captures the hull: inside it, AS the hull passes (the centre at least
        /// <paramref name="catchDegrees"/> off the nose), or at once inside the Skimshell itself.
        /// </summary>
        public static bool Captures(Vector3 hull, Vector3 forward, Vector3 centre, float radius, float tidepoolRadius,
            float catchDegrees)
        {
            var to = centre - hull;
            float d = to.magnitude;
            if (d > Mathf.Max(radius, tidepoolRadius)) return false;
            if (d <= radius) return true;
            return Vector3.Angle(forward, to) >= Mathf.Clamp(catchDegrees, 0f, 90f);
        }

        /// <summary>The entry's angle against the Skimshell's surface, degrees: 0 = flat (gliding along it), 90 = straight
        /// in or out (the plunk).</summary>
        public static float IncidenceDegrees(Vector3 heading, Vector3 radial)
        {
            if (heading.sqrMagnitude < 1e-8f || radial.sqrMagnitude < 1e-8f) return 0f;
            float s = Mathf.Abs(Vector3.Dot(heading.normalized, radial.normalized));
            return Mathf.Asin(Mathf.Clamp01(s)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The entry's angle against the Skimshell's surface, degrees, read from the LINE the hull is on rather than the
        /// point it was caught at: a line passing the centre at <c>b</c> crosses the sphere of radius R at acos(b / R) from
        /// the surface. A line that only grazes it (b ≥ R) is flat, 0°; one through the centre is straight in, 90°. The
        /// Tidepool catches you before you reach the surface, so the point's own angle overstates a tangent pass.
        /// </summary>
        public static float EntryIncidenceDegrees(Vector3 hull, Vector3 heading, Vector3 centre, float radius)
        {
            if (heading.sqrMagnitude < 1e-8f || radius <= 0f) return 0f;
            float b = Vector3.Cross(heading.normalized, centre - hull).magnitude;
            return Mathf.Acos(Mathf.Clamp01(b / radius)) * Mathf.Rad2Deg;
        }

        /// <summary>The share of speed an entry keeps: lerp(<paramref name="plunkCarry"/>, 1, cos²θ). Flat keeps it all,
        /// straight in keeps the plunk carry.</summary>
        public static float EntryCarry(float incidenceDegrees, float plunkCarry)
        {
            float c = Mathf.Cos(Mathf.Clamp(incidenceDegrees, 0f, 90f) * Mathf.Deg2Rad);
            return Mathf.Lerp(Mathf.Clamp01(plunkCarry), 1f, c * c);
        }

        // ------------------------------------------------------------------ the ride

        /// <summary>The spin, 0..1, from the squeeze: s^exponent.</summary>
        public static float Spin01(float squeeze01, float exponent) =>
            Mathf.Pow(Mathf.Clamp01(squeeze01), Mathf.Clamp(exponent, 0.25f, 4f));

        /// <summary>The Skimshell's surface speed at the Skimline, u/s.</summary>
        public static float SpinSpeed(float squeeze01, float spinSpeedMax, float exponent) =>
            Mathf.Max(0f, spinSpeedMax) * Spin01(squeeze01, exponent);

        /// <summary>The spin's flow at the surface point along <paramref name="radial"/> (centre → hull), as a direction
        /// scaled by cos(latitude): full on the Skimline, nothing at the poles.</summary>
        public static Vector3 SpinFlow(Vector3 spinAxis, Vector3 radial)
        {
            if (spinAxis.sqrMagnitude < 1e-8f || radial.sqrMagnitude < 1e-8f) return Vector3.zero;
            return Vector3.Cross(spinAxis.normalized, radial.normalized);
        }

        /// <summary>The speed the spin is pulling the ride toward: the surface speed along the hull's heading
        /// (riding with the spin gains, against it bleeds).</summary>
        public static float SpinTarget(float spinSpeed, Vector3 heading, Vector3 flow) =>
            heading.sqrMagnitude < 1e-8f ? 0f : spinSpeed * Vector3.Dot(heading.normalized, flow);

        /// <summary>The ride's spin speed eased toward <paramref name="target"/> at <paramref name="grip"/> per second.</summary>
        public static float EaseSpin(float current, float target, float grip, float dt) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0f, grip) * Mathf.Max(0f, dt)));

        /// <summary>
        /// The ride's heading drawn toward the spin's flow (either way along it: riding against the spin is a choice, not
        /// a mistake to undo) at <paramref name="rate"/> per second - the track precesses onto the Skimline.
        /// </summary>
        public static Vector3 Precess(Vector3 heading, Vector3 flow, float rate, float dt)
        {
            if (heading.sqrMagnitude < 1e-8f || flow.sqrMagnitude < 1e-6f || rate <= 0f || dt <= 0f) return heading;
            var h = heading.normalized;
            var f = flow.normalized;
            if (Vector3.Dot(h, f) < 0f) f = -f;
            return Vector3.Slerp(h, f, 1f - Mathf.Exp(-rate * dt)).normalized;
        }

        // ------------------------------------------------------------------ the launch

        /// <summary>The launch gain G: lerp(min, max, the deepest squeeze of the hold).</summary>
        public static float LaunchGain(float peak01, float min, float max) =>
            Mathf.Lerp(Mathf.Max(0f, min), Mathf.Max(0f, max), Mathf.Clamp01(peak01));

        /// <summary>
        /// The launch speed: min(cap, v_ride × (1 + G × Q' × rate × decay^skip)), with Q' = lerp(miss share, 1, the
        /// timing) - the whole gain for a release in the sweet spot, the miss share outside it.
        /// </summary>
        public static float LaunchSpeed(float rideSpeed, float gain, float timing01, float missShare, float rate,
            float cap, float skipDecay = 1f, int skipIndex = 0, float bonus = 0f)
        {
            float q = Mathf.Lerp(Mathf.Clamp01(missShare), 1f, Mathf.Clamp01(timing01));
            float decay = SkipScale(skipDecay, skipIndex) * (1f + Mathf.Max(0f, bonus));
            float v = Mathf.Max(0f, rideSpeed) * (1f + Mathf.Max(0f, gain) * q * Mathf.Max(1f, rate) * decay);
            return cap > 0f ? Mathf.Min(cap, v) : v;
        }

        /// <summary>The kick a launch adds on top of the engine: what the launch speed is over it, never negative.</summary>
        public static float Kick(float launchSpeed, float engineSpeed) => Mathf.Max(0f, launchSpeed - Mathf.Max(0f, engineSpeed));

        /// <summary>The hook's turn rate at launch, rad/s: <paramref name="curve"/> × the spin's own angular rate
        /// (surface speed / R) - the Magnus curve, the way the ride was turning.</summary>
        public static float HookRate(float curve, float spinSpeed, float radius) =>
            Mathf.Max(0f, curve) * Mathf.Max(0f, spinSpeed) / Mathf.Max(1f, radius);

        /// <summary>The hook's remaining share <paramref name="since"/> seconds after launch: 1 → 0 over
        /// <paramref name="seconds"/>.</summary>
        public static float HookDecay(float since, float seconds) =>
            seconds <= 0f ? 0f : 1f - Mathf.Clamp01(since / seconds);

        /// <summary>The hook's whole turn, degrees, for a given rate decaying linearly to nothing over the seconds.</summary>
        public static float HookTotalDegrees(float rate, float seconds) => 0.5f * Mathf.Max(0f, rate) * Mathf.Max(0f, seconds) * Mathf.Rad2Deg;

        /// <summary>The skip count of a new capture: one more than the last if it came within the skip window of the
        /// last launch (capped), else 0 (a fresh run).</summary>
        public static int SkipIndex(int previous, float sinceLaunch, float window, int maxSkips) =>
            sinceLaunch >= 0f && sinceLaunch <= Mathf.Max(0f, window) ? Mathf.Min(Mathf.Max(0, previous) + 1, Mathf.Max(0, maxSkips)) : 0;

        /// <summary>The share of the gain skip <paramref name="skipIndex"/> pays: decay^n (the first throw, n = 0, pays it all).</summary>
        public static float SkipScale(float skipDecay, int skipIndex) => Mathf.Pow(Mathf.Clamp01(skipDecay), Mathf.Max(0, skipIndex));

        // ------------------------------------------------------------------ the chain (skips)

        /// <summary>
        /// A press <paramref name="sinceLaunch"/> seconds after the last launch CHAINS (lays the next skip on the launch
        /// line) when it is inside the skip window and the run has skips left.
        /// </summary>
        public static bool Chains(float sinceLaunch, float window, int skipIndex, int maxSkips) =>
            sinceLaunch >= 0f && sinceLaunch <= Mathf.Max(0f, window) && Mathf.Max(0, skipIndex) < Mathf.Max(0, maxSkips);

        /// <summary>A chained press inside the first <paramref name="perfectWindow"/> seconds of the window is PERFECT.</summary>
        public static bool Perfect(float sinceLaunch, float perfectWindow) =>
            sinceLaunch >= 0f && sinceLaunch <= Mathf.Max(0f, perfectWindow);

        /// <summary>
        /// Where a launched hull will be after flying <paramref name="distance"/> more along its HOOKED line: the heading
        /// keeps turning about <paramref name="hookAxis"/> at <paramref name="hookRate"/> (rad/s) × the hook's remaining share
        /// (<see cref="HookDecay"/>, from <paramref name="sinceLaunch"/>), at <paramref name="speed"/>. Integrated in
        /// <paramref name="steps"/> equal steps; <paramref name="headingAt"/> is the heading there.
        /// </summary>
        public static Vector3 PredictLine(Vector3 position, Vector3 heading, Vector3 hookAxis, float hookRate, float hookSeconds,
            float sinceLaunch, float speed, float distance, int steps, out Vector3 headingAt)
        {
            var h = heading.sqrMagnitude > 1e-8f ? heading.normalized : Vector3.forward;
            var p = position;
            int n = Mathf.Max(1, steps);
            float ds = Mathf.Max(0f, distance) / n;
            float dt = ds / Mathf.Max(1f, speed);
            float t = Mathf.Max(0f, sinceLaunch);
            bool hooks = hookRate > 0f && hookAxis.sqrMagnitude > 1e-8f;
            for (int i = 0; i < n; i++)
            {
                if (hooks)
                {
                    // Midpoint rule: the turn over this step at the share halfway through it.
                    float deg = hookRate * HookDecay(t + 0.5f * dt, hookSeconds) * dt * Mathf.Rad2Deg;
                    var half = Quaternion.AngleAxis(0.5f * deg, hookAxis);
                    p += half * h * ds;
                    h = (Quaternion.AngleAxis(deg, hookAxis) * h).normalized;
                }
                else p += h * ds;
                t += dt;
            }
            headingAt = h;
            return p;
        }

        /// <summary>
        /// The next skip's Skimshell centre: R to the pressed side of the predicted point, square to the predicted heading
        /// (in the plane of <paramref name="up"/>), so the line passes the shell TANGENT - a flat pass, caught as it goes by
        /// with the whole speed kept (<see cref="EntryIncidenceDegrees"/> = 0).
        /// </summary>
        public static Vector3 ChainCentre(Vector3 point, Vector3 headingAt, Vector3 up, bool left, float radius)
        {
            var h = headingAt.sqrMagnitude > 1e-8f ? headingAt.normalized : Vector3.forward;
            var right = Vector3.Cross(up, h);
            if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(Vector3.up, h);
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            right.Normalize();
            return point + (left ? -right : right) * Mathf.Max(0f, radius);
        }

        // ------------------------------------------------------------------ graze skips

        /// <summary>
        /// A GRAZE: with no trigger held, the hull crosses its own live Skimshell INWARD this frame (from outside R to
        /// inside it) at a shallow angle, under <paramref name="grazeAngle"/> from the surface. A steep crossing passes
        /// through untouched.
        /// </summary>
        public static bool Grazes(bool triggerHeld, float previousDistance, float distance, float radius, Vector3 heading,
            Vector3 radial, float grazeAngle)
        {
            if (triggerHeld || radius <= 0f) return false;
            if (!(previousDistance > radius && distance <= radius)) return false;
            if (heading.sqrMagnitude < 1e-8f || radial.sqrMagnitude < 1e-8f) return false;
            if (Vector3.Dot(heading, radial) >= 0f) return false;   // moving out, not in
            return IncidenceDegrees(heading, radial) < Mathf.Clamp(grazeAngle, 0f, 90f);
        }

        /// <summary>The bounce: the velocity's normal part reflected at <paramref name="restitution"/>, the tangent kept -
        /// v - (1 + e)(v·n)n.</summary>
        public static Vector3 GrazeReflect(Vector3 velocity, Vector3 normal, float restitution)
        {
            if (normal.sqrMagnitude < 1e-8f) return velocity;
            var n = normal.normalized;
            return velocity - (1f + Mathf.Clamp01(restitution)) * Vector3.Dot(velocity, n) * n;
        }

        /// <summary>A graze's kick: <paramref name="gain"/> × the speed, never past the cap (cap ≤ 0 = none).</summary>
        public static float GrazeKick(float speed, float gain, float cap)
        {
            float k = Mathf.Max(0f, speed) * Mathf.Max(0f, gain);
            return cap > 0f ? Mathf.Clamp(k, 0f, Mathf.Max(0f, cap - Mathf.Max(0f, speed))) : k;
        }

        /// <summary>A skip ripple's radius and fade <paramref name="age"/> seconds in: R + growth × age, alpha 1 → 0.</summary>
        public static float RippleRadius(float radius, float growth, float age) => Mathf.Max(0f, radius) + Mathf.Max(0f, growth) * Mathf.Max(0f, age);
        public static float RippleAlpha(float age, float seconds) => seconds <= 0f ? 0f : 1f - Mathf.Clamp01(age / seconds);

        // ------------------------------------------------------------------ rivals (ELEMENTAL_ECONOMY §9b)

        /// <summary>
        /// A rival may ride this Skimshell only by its OWN choice: the rule is on, it is holding its own input, and it
        /// entered flat (incidence ≤ <paramref name="grazeAngle"/>). Anything else leaves it untouched - the Skim Sling
        /// never moves an opposing vessel against its will.
        /// </summary>
        public static bool RivalOptsIn(bool ruleEnabled, bool holdingOwnInput, float incidenceDegrees, float grazeAngle) =>
            ruleEnabled && holdingOwnInput && incidenceDegrees <= Mathf.Clamp(grazeAngle, 0f, 90f);

        // ------------------------------------------------------------------ drawing

        /// <summary>
        /// A closed ring like <see cref="StoatSlingMath.Ring"/>, started <paramref name="phaseDegrees"/> round its axis:
        /// world dots laid from the first point then SCROLL with the phase - the Skimline turning with the spin, in the
        /// world, no screen-space warp.
        /// </summary>
        public static int Ring(Vector3 centre, Vector3 normal, float radius, float phaseDegrees, int segments, Vector3[] points)
        {
            if (points == null || radius <= 0f || normal.sqrMagnitude < 1e-8f) return 0;
            int n = Mathf.Min(points.Length, Mathf.Max(3, segments));
            var axis = normal.normalized;
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized * radius;
            for (int i = 0; i < n; i++) points[i] = centre + Quaternion.AngleAxis(phaseDegrees + 360f * i / n, axis) * u;
            return n;
        }
    }
}
