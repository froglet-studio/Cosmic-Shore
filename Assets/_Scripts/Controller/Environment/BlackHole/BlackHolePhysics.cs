using Unity.Burst;
using Unity.Mathematics;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The black hole's equations, as pure functions (Docs/BLACK_HOLE.md §2). Everything the
    /// gravity job, the vessel pull and the edit-mode tests compute goes through here, so there
    /// is exactly one statement of the physics and the tests exercise the code the game runs.
    ///
    /// <para><b>The radial law is the Paczynski-Wiita pseudo-Newtonian potential</b>,
    /// <c>Φ = -GM / (r - r_s)</c>, the standard approximation of Schwarzschild gravity in a
    /// Newtonian integrator: it reproduces the innermost stable circular orbit at <c>3 r_s</c>
    /// and the plunge inside it, which is what makes near-horizon mass SPIRAL IN instead of
    /// orbiting forever the way a 1/r potential would let it. The acceleration is
    /// <c>a = -GM / (r - r_s)^2 r̂</c>; the pole at the horizon is floored so a body that is
    /// about to be captured is integrated rather than flung to infinity by one bad step.</para>
    ///
    /// <para><b>Frame dragging is Lense-Thirring</b>: a hole with dimensionless spin <c>a*</c> turns
    /// the local inertial frame at <c>ω(r) = 2GJ / (c² r³) = a*·c·r_s² / (2 r³)</c> (weak-field Kerr,
    /// with <c>c² = 2GM / r_s</c> from the horizon's own definition), about its spin axis. It is
    /// strong only within a few horizon radii — <c>1/r³</c> — and it twists, it does not swirl:
    /// a body's POSITION is advected by the frame (<c>dx/dt = v + ω × r</c>) while gravity acts on
    /// <c>v</c>, which is exactly the kinematics of a zero-angular-momentum body (its angular
    /// velocity is ω). So mass at rest falls in nearly radially and winds up only in its last few
    /// horizon radii, as it does around a real spinning hole; mass that ARRIVES with angular
    /// momentum (a moving hole, a moving prism) orbits, slingshots or plunges by the potential.
    /// The first version coupled every body viscously to a frame turning at 80% of the local
    /// ORBITAL rate at every radius — an invented swirl, removed 2026-10-08 (Docs/BLACK_HOLE.md §2).</para>
    ///
    /// Every body is a test particle. There is no mass in any equation below because
    /// trajectories do not depend on it.
    /// </summary>
    [BurstCompile]
    public static class BlackHolePhysics
    {
        /// <summary>One gravity well as the job sees it — a snapshot taken on the main thread each frame.</summary>
        public struct Well
        {
            public float3 Position;
            /// <summary>
            /// SIGNED gravitational parameter GM, u^3/s^2: positive pulls (a sink, a black hole),
            /// negative pushes (a source, a white hole — Docs/BLACK_HOLE.md §12). A source never
            /// captures: nothing crosses its horizon inward, and a body inside it is driven out.
            /// </summary>
            public float GM;
            /// <summary>Event-horizon radius r_s, u. Inside it a body is captured.</summary>
            public Horizon Horizon;
            /// <summary>Radius past which this well does not simulate a body, u.</summary>
            public float InfluenceRadius;
            /// <summary>Unit axis the dragged frame rotates about (right-hand rule).</summary>
            public float3 SpinAxis;
            /// <summary>
            /// Lense-Thirring coefficient, u^3/s: the frame turns at <c>FrameDrag / r³</c> rad/s.
            /// <see cref="FrameDragCoefficient"/> builds it from |GM|, r_s and the spin; a source's is
            /// negated, so its frame turns the other way about the same axis.
            /// </summary>
            public float FrameDrag;
            /// <summary>
            /// Plummer core radius ε of a SMOOTH well, u (0 = the black hole's Paczynski-Wiita law):
            /// <c>a = −GM·r / (r² + ε²)^1.5</c> — finite and smooth everywhere, zero at the centre, the
            /// pull of a soft distribution of mass rather than of a point (Docs/CRYSTAL_WORMHOLE.md).
            /// </summary>
            public float Softening;
        }

        /// <summary>
        /// A horizon radius with its pole guard, so the two numbers that must agree live in one
        /// place: <see cref="Radius"/> is where capture happens and <see cref="MinGap"/> is the
        /// smallest <c>(r - r_s)</c> the acceleration is ever evaluated at.
        /// </summary>
        public struct Horizon
        {
            public float Radius;
            public float MinGap;

            public static Horizon Of(float radius)
            {
                radius = math.max(radius, 1e-3f);
                return new Horizon { Radius = radius, MinGap = radius * PoleGuardFraction };
            }
        }

        /// <summary>
        /// Fraction of the horizon radius the pseudo-Newtonian gap is floored at. The pole is
        /// physical (the potential really diverges at r_s) but a body crossing the horizon is
        /// captured on that step, so the only thing the floor changes is the size of its last
        /// kick before it vanishes.
        /// </summary>
        public const float PoleGuardFraction = 0.25f;

        /// <summary>Paczynski-Wiita acceleration of a test body at <paramref name="p"/> toward the well.</summary>
        public static float3 Acceleration(in float3 p, in Well w)
        {
            float3 r = p - w.Position;
            float d = math.length(r);
            if (!(d > 1e-6f)) return float3.zero;        // dead centre: no direction to pull along
            if (w.Softening > 0f)
            {
                float q = d * d + w.Softening * w.Softening;
                return r * (-w.GM / (q * math.sqrt(q)));
            }
            float gap = math.max(d - w.Horizon.Radius, w.Horizon.MinGap);
            float mag = w.GM / (gap * gap);
            return r * (-mag / d);
        }

        /// <summary>Escape speed at distance <paramref name="d"/> under the pseudo-Newtonian potential.</summary>
        public static float EscapeSpeed(float d, float gm, in Horizon h)
        {
            float gap = math.max(d - h.Radius, h.MinGap);
            return math.sqrt(2f * gm / gap);
        }

        /// <summary>
        /// Circular-orbit speed at distance <paramref name="d"/>: <c>v² / d = GM / (d - r_s)²</c>.
        /// Only stable outside <c>3 r_s</c> (the ISCO), which is the whole point of the potential.
        /// </summary>
        public static float CircularSpeed(float d, float gm, in Horizon h)
        {
            float gap = math.max(d - h.Radius, h.MinGap);
            return math.sqrt(gm * d) / gap;
        }

        /// <summary>Innermost stable circular orbit of the pseudo-Newtonian potential.</summary>
        public static float IscoRadius(in Horizon h) => 3f * h.Radius;

        /// <summary>
        /// The speed of light the horizon implies: <c>r_s = 2GM / c²</c>, so <c>c = √(2GM / r_s)</c>.
        /// The game's numbers are free to choose GM and r_s; this keeps every relativistic
        /// formula consistent with the pair it was given.
        /// </summary>
        public static float LightSpeed(float gm, float horizonRadius) =>
            math.sqrt(2f * math.max(gm, 0f) / math.max(horizonRadius, 1e-3f));

        /// <summary>
        /// The Lense-Thirring coefficient <c>K</c> of a hole with dimensionless spin
        /// <paramref name="spin"/> (a*, 0 = Schwarzschild, ≤ 0.998): the frame turns at
        /// <c>ω(r) = K / r³</c> with <c>K = 2GJ/c² = a*·c·r_s² / 2</c>.
        /// </summary>
        public static float FrameDragCoefficient(float gm, float horizonRadius, float spin) =>
            math.clamp(spin, 0f, 0.998f) * LightSpeed(gm, horizonRadius) * horizonRadius * horizonRadius * 0.5f;

        /// <summary>
        /// Angular velocity of the dragged frame at distance <paramref name="d"/>, rad/s —
        /// <c>FrameDrag / d³</c>, evaluated no closer than the horizon (inside it a body is
        /// captured, and the weak-field law is not asked to describe what it cannot).
        /// </summary>
        public static float FrameAngularVelocity(float d, in Well w) =>
            w.FrameDrag == 0f ? 0f : w.FrameDrag / math.pow(math.max(d, w.Horizon.Radius), 3f);

        /// <summary>
        /// Velocity of the dragged frame at <paramref name="p"/>: a rotation about the spin axis at
        /// <see cref="FrameAngularVelocity"/>. Zero on the axis itself (there is no tangent there)
        /// and zero for a non-rotating hole. A moving hole drags nothing along with it — it pulls.
        /// </summary>
        public static float3 FrameVelocity(in float3 p, in Well w)
        {
            float3 r = p - w.Position;
            float d = math.length(r);
            if (!(d > 1e-6f) || w.FrameDrag == 0f) return float3.zero;
            return math.cross(w.SpinAxis, r) * FrameAngularVelocity(d, w);
        }

        /// <summary>Everything one integration step reads besides the wells.</summary>
        public struct StepParams
        {
            public float ReleaseDamping;
            public float ReleaseSpeed;
            public int MaxSubsteps;
        }

        /// <summary>One body's verdict after a step.</summary>
        public enum Verdict : byte
        {
            Free = 0,
            Captured = 1,
            Released = 2,
        }

        /// <summary>
        /// Advance one test body by <paramref name="dt"/> under every well in
        /// <paramref name="wells"/>. Semi-implicit Euler, substepped near a horizon so the pole is
        /// integrated and not jumped. Gravity (every well, summed) acts on the velocity; the
        /// dominant well's dragged frame advects the position (Lense-Thirring, see the class
        /// summary). Returns the verdict; <paramref name="capturedBy"/> names the well on capture.
        ///
        /// A body outside EVERY well's influence sphere is damped toward rest and released once it
        /// is slower than the release speed — so mass a hole flung clear settles instead of
        /// coasting across the arena forever, and a field with no holes left in it goes quiet.
        /// </summary>
        public static Verdict Step(ref float3 p, ref float3 v, in NativeWells wells, in StepParams prm,
            float dt, out int capturedBy)
        {
            capturedBy = -1;
            if (wells.Count <= 0)
            {
                return Coast(ref p, ref v, prm, dt);
            }

            // Substep count from the closest approach: a body moving (or about to accelerate)
            // across a quarter of its horizon gap per step is stepped finer.
            float minGap = float.MaxValue;
            float3 a0 = float3.zero;
            bool inside = false;
            for (int i = 0; i < wells.Count; i++)
            {
                var w = wells[i];
                float d = math.length(p - w.Position);
                if (d <= w.InfluenceRadius) inside = true;
                minGap = math.min(minGap, math.max(d - w.Horizon.Radius, w.Horizon.MinGap));
                a0 += Acceleration(p, w);
            }
            float speedScale = math.length(v) + math.sqrt(math.length(a0) * math.max(minGap, 1e-3f));
            int steps = (int)math.ceil(dt * speedScale / (0.2f * math.max(minGap, 1e-3f)));
            steps = math.clamp(steps, 1, math.max(1, prm.MaxSubsteps));
            float h = dt / steps;

            for (int s = 0; s < steps; s++)
            {
                float3 a = float3.zero;
                float bestMag = 0f;
                int dominant = -1;
                inside = false;
                for (int i = 0; i < wells.Count; i++)
                {
                    var w = wells[i];
                    float3 r = p - w.Position;
                    float d = math.length(r);
                    // Only a SINK captures. A source's horizon is where its push is strongest: a body
                    // inside it (one carried through a dipole's throat) is driven straight out.
                    if (w.GM > 0f && d <= w.Horizon.Radius)
                    {
                        capturedBy = i;
                        return Verdict.Captured;
                    }
                    if (d <= w.InfluenceRadius) inside = true;
                    float3 ai = Acceleration(p, w);
                    float mag = math.lengthsq(ai);
                    if (mag > bestMag) { bestMag = mag; dominant = i; }
                    a += ai;
                }

                if (!inside && prm.ReleaseDamping > 0f)
                    v *= math.exp(-prm.ReleaseDamping * h);

                v += a * h;

                // Frame dragging advects the POSITION (a zero-angular-momentum body turns with the
                // frame at ω). Dominant well rather than summed: two frames rotating about two
                // centres do not add up to a frame about anything.
                float3 frame = dominant >= 0 ? FrameVelocity(p, wells[dominant]) : float3.zero;
                p += (v + frame) * h;
            }

            if (!inside && math.lengthsq(v) < prm.ReleaseSpeed * prm.ReleaseSpeed)
                return Verdict.Released;
            return Verdict.Free;
        }

        /// <summary>A body with no well left to pull it: damp, drift, release when slow.</summary>
        static Verdict Coast(ref float3 p, ref float3 v, in StepParams prm, float dt)
        {
            if (prm.ReleaseDamping > 0f) v *= math.exp(-prm.ReleaseDamping * dt);
            p += v * dt;
            return math.lengthsq(v) < prm.ReleaseSpeed * prm.ReleaseSpeed ? Verdict.Released : Verdict.Free;
        }

        /// <summary>
        /// A fixed-capacity, blittable view of the live wells so the job carries them by value
        /// (a NativeArray would need its own safety handle per job; four float4-sized records do
        /// not). Capacity mirrors <c>PRISM_GRAVITY_WARP_SLOTS</c> — a hole the shader cannot
        /// warp around is not one the field should pull toward either.
        /// </summary>
        public struct NativeWells
        {
            public const int Capacity = 4;

            public Well W0, W1, W2, W3;
            public int Count;

            public Well this[int i]
            {
                get
                {
                    switch (i)
                    {
                        case 0: return W0;
                        case 1: return W1;
                        case 2: return W2;
                        default: return W3;
                    }
                }
            }

            public void Add(in Well w)
            {
                if (Count >= Capacity) return;
                switch (Count)
                {
                    case 0: W0 = w; break;
                    case 1: W1 = w; break;
                    case 2: W2 = w; break;
                    default: W3 = w; break;
                }
                Count++;
            }
        }
    }
}
