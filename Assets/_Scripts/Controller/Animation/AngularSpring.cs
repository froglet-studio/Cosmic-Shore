using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A damped harmonic spring on one angular channel, stepped by the EXACT closed-form
    /// solution — never an Euler integration. The closed form is what makes it safe as an
    /// animation primitive: it is unconditionally stable through any dt (a pause hitch or a
    /// 10 fps dip cannot explode it), frame-rate independent by construction (stepping 1 s in
    /// one call or in 240 lands on the same state, which <c>AngularSpringTests</c> pins), and
    /// velocity-continuous, so an event can kick a channel with <see cref="AddImpulse"/> and
    /// the motion peaks immediately and settles organically through the same spring.
    ///
    /// Why springs at all (SCARAB.md §3.0): the fleet's `Lerp(current, target, k·dt)` idiom is
    /// frame-rate-dependent and has no velocity, so a flick arrives as an ease-in and an event
    /// flourish has to fight the smoothing. A spring gives the Scarab's parts momentum — the
    /// under-damped legs and antennae genuinely overshoot and settle, which is where the
    /// "alive" read comes from. Damping ratio ζ picks the character: 1 = critically damped
    /// (fast, no overshoot — the horn, the pilot's aim instrument), ~0.6 = a visible settle,
    /// ~0.4 = one or two honest oscillations (antennae).
    /// </summary>
    public static class AngularSpring
    {
        /// <summary>One channel's live state. Position in degrees, velocity in degrees/second.</summary>
        public struct State
        {
            public float Position;
            public float Velocity;
        }

        /// <summary>An event kick: adds velocity, leaves position — the motion peaks at once
        /// and the spring's own damping is the whole decay envelope.</summary>
        public static void AddImpulse(ref State state, float degreesPerSecond) =>
            state.Velocity += degreesPerSecond;

        /// <summary>Snap to the target at rest (initialization, teardown).</summary>
        public static State AtRest(float position) => new State { Position = position, Velocity = 0f };

        /// <summary>
        /// Advance the spring toward <paramref name="target"/> by <paramref name="dt"/> seconds.
        /// <paramref name="omega"/> is the undamped natural frequency in rad/s (higher = stiffer
        /// = faster response); <paramref name="zeta"/> the damping ratio.
        /// </summary>
        public static State Step(State state, float target, float omega, float zeta, float dt)
        {
            if (dt <= 0f || omega <= 0f) return state;

            // Work relative to the equilibrium so the closed forms are homogeneous.
            float x = state.Position - target;
            float v = state.Velocity;
            float newX, newV;

            if (zeta < 0.999f)
            {
                // Under-damped: e^(−ζωt) (A cos ωd t + B sin ωd t), ωd = ω√(1−ζ²).
                float omegaZeta = omega * zeta;
                float omegaD = omega * Mathf.Sqrt(1f - zeta * zeta);
                float e = Mathf.Exp(-omegaZeta * dt);
                float c = Mathf.Cos(omegaD * dt);
                float s = Mathf.Sin(omegaD * dt);
                newX = e * (x * (c + omegaZeta / omegaD * s) + v * (s / omegaD));
                newV = e * (v * (c - omegaZeta / omegaD * s) - x * (omega * omega / omegaD) * s);
            }
            else if (zeta < 1.001f)
            {
                // Critically damped: (A + B t) e^(−ωt).
                float e = Mathf.Exp(-omega * dt);
                newX = e * (x * (1f + omega * dt) + v * dt);
                newV = e * (v * (1f - omega * dt) - x * (omega * omega * dt));
            }
            else
            {
                // Over-damped: two real decay rates.
                float root = omega * Mathf.Sqrt(zeta * zeta - 1f);
                float r1 = -omega * zeta + root;
                float r2 = -omega * zeta - root;
                float e1 = Mathf.Exp(r1 * dt);
                float e2 = Mathf.Exp(r2 * dt);
                float denom = r1 - r2;
                float c1 = (v - r2 * x) / denom;
                float c2 = x - c1;
                newX = c1 * e1 + c2 * e2;
                newV = c1 * r1 * e1 + c2 * r2 * e2;
            }

            return new State { Position = newX + target, Velocity = newV };
        }
    }
}
