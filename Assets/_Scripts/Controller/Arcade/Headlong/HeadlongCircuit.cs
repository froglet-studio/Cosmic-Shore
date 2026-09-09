using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for one generated Headlong circuit. Every number is stated against the RHINO's
    /// own flight model rather than picked by eye - see <see cref="ForIntensity"/> and
    /// HEADLONG.md.
    /// </summary>
    public struct HeadlongCircuitSettings
    {
        public int GateCount;
        public float RingRadius;          // the mouth
        public float BaseRadius;          // radius of the circuit's underlying circle
        public float RadialPerturbation;  // how far a gate may be pushed in/out of that circle
        public float LateralPerturbation; // ...and out of its plane
        public float CornerRadiusFactor;  // every corner must fit >= this x the flat-out radius
        public float InnerRadius;         // circuit stays outside this (the nucleus)
        public float OuterRadius;         // ...and inside this (the membrane, with margin)
        public float AxisJitterDegrees;
        public float MaxPresentDegrees;
        public Vector3 FirstGateDirection; // the spawn formation's POLE - see Generate()

        // ── The vessel this course is cut for ────────────────────────────────
        // Read from Rhino.prefab and RhinoRampBoostAction.asset rather than remembered; see
        // R_VesselActions/RHINO_RAMP_BOOST.md, which derives all four.

        /// <summary>`DefaultThrottleScaler 50 x maxBoostMultiplier 18 + DefaultMinimumSpeed 10`.</summary>
        public const float RhinoTopSpeed = 910f;

        /// <summary>`RotationThrottleScaler` on Rhino.prefab.</summary>
        public const float RhinoRotationThrottleScaler = 0.4f;

        /// <summary>`YawScaler`/`PitchScaler` on Rhino.prefab (the vessel authors both at 90).</summary>
        public const float RhinoTurnScaler = 90f;

        /// <summary>
        /// How much of the stick a pilot may spend while still holding the ramp boost.
        ///
        /// <para>The gesture that engages it requires
        /// <c>(1 - XDiff) + |YDiff| + |YSum| + |XSum| &lt; 0.3</c> in every
        /// <c>IInputStrategy.PerformSpeedAndDirectionalEffects</c>, so at full throttle the whole
        /// budget is rotation. 0.28 rather than 0.30 because a pilot who spends the last
        /// hundredth drops the boost.</para>
        /// </summary>
        public const float BoostStickBudget = 0.28f;

        /// <summary>
        /// THE number this whole mode is built around: the tightest circle a Rhino can fly at
        /// top speed WITHOUT dropping the ramp boost. ~410 u.
        ///
        /// <para>Turn rate is linear in stick, so a pilot holding the boost turns at
        /// <c>BoostStickBudget x omega(v)</c> and therefore flies a circle
        /// <c>1 / BoostStickBudget</c> times wider than the vessel's absolute minimum. Sizing a
        /// corner just inside this makes it takeable flat out; sizing one outside it forces the
        /// pilot to choose between the line and the boost, which is the mode.</para>
        /// </summary>
        public static float FlatOutRadius =>
            RaceCourseGeometry.MinTurnRadius(RhinoTopSpeed, RhinoRotationThrottleScaler, RhinoTurnScaler)
            / BoostStickBudget;

        /// <summary>
        /// The shipped circuit shape per intensity. **INTENSITY IS HOW MANY CORNERS YOU CAN TAKE
        /// WITHOUT LIFTING**, not how big the arena is - the mode runs one cell and one gate
        /// count, and what climbs is the corner budget.
        ///
        /// <para><c>CornerRadiusFactor</c> is a multiple of <see cref="FlatOutRadius"/>: at 1.50
        /// every corner has half again the room a flat-out Rhino needs, and at 0.70 several of
        /// them cannot be held at 910 u/s at all - you brake, turn, and pay 6.1 s to wind the
        /// ramp back up. The perturbation climbs alongside it so the higher levels genuinely
        /// PRODUCE sharper corners rather than merely permitting them.</para>
        ///
        /// <para>Gate COUNT is deliberately constant: it is the end-game target (authored once in
        /// <c>EndConditionOverridesSO</c>, read by both the monitor and the controller), so a
        /// match is the same length at every level and the four are comparable. Same reasoning as
        /// Switchback, and as Rampage's identical forest.</para>
        ///
        /// <para>Every row is MEASURED - <c>HeadlongCircuitTests</c> sweeps 400 seeds of each and
        /// asserts the corner floor holds, the circuit closes, no gate leaves the shell and no two
        /// mouths overlap.</para>
        /// </summary>
        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new HeadlongCircuitSettings
            {
                GateCount = 8,
                // A regular octagon at 800 has 612u legs and 45 degree corners, which fits a
                // 739u turn - 1.8x the flat-out radius. That is the relaxation's floor case and
                // it is why the generator can never fail.
                BaseRadius = 800f,
                CornerRadiusFactor = new[] { 1.50f, 1.15f, 0.90f, 0.70f }[i - 1],
                RadialPerturbation = new[] { 120f, 170f, 215f, 260f }[i - 1],
                LateralPerturbation = new[] { 120f, 170f, 215f, 260f }[i - 1],
                // Wider than Switchback's ladder at every step: a Rhino arrives at up to 910 u/s
                // against a Dolphin's 347, so it crosses a mouth in a third of the time and has a
                // third of the lateral authority to correct with on the way in.
                RingRadius = new[] { 96f, 72f, 54f, 40f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                MaxPresentDegrees = new[] { 45f, 50f, 55f, 60f }[i - 1],
            };
        }
    }

    /// <summary>
    /// Builds a Headlong circuit: a CLOSED loop of gates a Rhino races laps of, cut so that its
    /// corners sit at a chosen multiple of the vessel's flat-out turn radius.
    ///
    /// <para><b>Why a closed loop rather than Switchback's open chain.</b> The whole point of the
    /// Rhino is that its turn radius CONVERGES with speed (RHINO_RAMP_BOOST.md), so the
    /// interesting question is not "can you reach the next gate" but "can you keep the throttle
    /// buried through the corner". That question only has a stable answer if every vertex is a
    /// corner - an open chain's first and last gates are straight-through and its walk can always
    /// escape a tight spot by wandering, which is exactly the pressure this mode wants to remove.
    /// A circuit also gives laps for free, which is what makes a gate index a RACE.</para>
    ///
    /// <para><b>The generator cannot fail, by construction.</b> It does not walk and backtrack; it
    /// starts from a regular N-gon - whose corners are all <c>360/N</c> and whose inscribed turn
    /// radius is a known 1.8x the flat-out radius - perturbs every vertex, and RELAXES the
    /// perturbation toward zero until the corner floor and the shell both hold. The base case is
    /// always legal, so there is no null return, no re-roll and no seed that produces a course
    /// nobody can fly. (Switchback's walk CAN fail and correctly returns null; it is solving a
    /// different problem, where the constraint is reachability rather than shape.)</para>
    ///
    /// <para><b>Pure and deterministic</b> - see <see cref="RaceCourseGeometry"/>. The server still
    /// SENDS the resulting geometry rather than the seed (<c>HeadlongController</c>), so peers
    /// cannot disagree even if a transcendental differs in its last bit.</para>
    /// </summary>
    public static class HeadlongCircuit
    {
        /// <summary>
        /// How many times the perturbation is shrunk before falling back to the bare N-gon.
        /// Each step multiplies by <see cref="RelaxationRate"/>, so 48 steps reach 2e-3 of the
        /// authored perturbation - far past the point where the base case dominates.
        /// </summary>
        const int RelaxationSteps = 48;
        const float RelaxationRate = 0.88f;

        /// <summary>
        /// The circuit, in CELL-LOCAL coordinates (the caller adds the cell centre). Never null
        /// and never shorter than <c>GateCount</c> - see the class docs.
        /// </summary>
        public static List<RaceGate> Generate(int seed, HeadlongCircuitSettings s)
        {
            int n = Mathf.Max(3, s.GateCount);
            var rng = new RaceCourseGeometry.Rng(seed);

            // A random plane through the cell centre, and an even ring of vertices on it.
            Vector3 normal = RaceCourseGeometry.SafeNormalize(
                new Vector3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f)), Vector3.up);
            Vector3 u = RaceCourseGeometry.Perpendicular(normal);
            Vector3 w = Vector3.Cross(normal, u);
            float phase = rng.Range(0f, 2f * Mathf.PI);

            var basePts = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                float a = phase + 2f * Mathf.PI * i / n;
                basePts.Add(u * (s.BaseRadius * Mathf.Cos(a)) + w * (s.BaseRadius * Mathf.Sin(a)));
            }

            // One perturbation per vertex, drawn once and then scaled as a whole. Scaling the
            // WHOLE set rather than re-rolling is what makes the relaxation monotone: every step
            // moves strictly toward the legal base case, so it terminates.
            var radial = new float[n];
            var lateral = new float[n];
            for (int i = 0; i < n; i++)
            {
                radial[i] = rng.Range(-s.RadialPerturbation, s.RadialPerturbation);
                lateral[i] = rng.Range(-s.LateralPerturbation, s.LateralPerturbation);
            }

            float floor = s.CornerRadiusFactor * HeadlongCircuitSettings.FlatOutRadius;
            float scale = 1f;
            List<Vector3> pts = null;

            for (int step = 0; step <= RelaxationSteps; step++)
            {
                pts = Build(basePts, normal, radial, lateral, scale);
                if (IsLegal(pts, floor, s.InnerRadius, s.OuterRadius, s.RingRadius)) break;
                scale *= RelaxationRate;
                if (step == RelaxationSteps) pts = Build(basePts, normal, radial, lateral, 0f);
            }

            // GATE 0 SITS ON THE SPAWN FORMATION'S POLE, and that is a fairness rule rather than
            // a layout preference: pilots spawn on an equatorial ring around the cell, so every
            // one of them is equidistant from a point on that ring's axis. Put the first gate
            // anywhere else and whoever spawned nearest it starts the lap ahead.
            //
            // Applied as a RIGID rotation of the finished circuit, which is why it is free: every
            // property the relaxation just established - corner radii, turn angles, leg lengths,
            // distance from the cell centre - is rotation-invariant.
            Vector3 pole = RaceCourseGeometry.SafeNormalize(s.FirstGateDirection, Vector3.up);
            Quaternion align = Quaternion.FromToRotation(
                RaceCourseGeometry.SafeNormalize(pts[0], pole), pole);
            for (int i = 0; i < n; i++) pts[i] = align * pts[i];

            // Each gate faces the flow BISECTOR of its corner, and the jitter that makes it
            // "randomly oriented" is spent from what is LEFT of the presentation cap after the
            // corner has taken its half - so a sharp corner plus full jitter can never stand a
            // gate edge-on to the line you arrive on. Switchback's rule, and on a circuit it
            // applies at EVERY vertex because every vertex is a corner.
            var gates = new List<RaceGate>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 inbound = RaceCourseGeometry.SafeNormalize(pts[i] - pts[(i - 1 + n) % n], Vector3.forward);
                Vector3 outbound = RaceCourseGeometry.SafeNormalize(pts[(i + 1) % n] - pts[i], inbound);
                Vector3 axis = RaceCourseGeometry.SafeNormalize(inbound + outbound, inbound);
                float halfTurn = RaceCourseGeometry.Angle(inbound, outbound) * 0.5f;
                float jitter = Mathf.Max(0f, Mathf.Min(s.AxisJitterDegrees, s.MaxPresentDegrees - halfTurn));
                gates.Add(new RaceGate(pts[i], RaceCourseGeometry.Deflect(ref rng, axis, jitter), s.RingRadius));
            }
            return gates;
        }

        static List<Vector3> Build(List<Vector3> basePts, Vector3 normal,
                                   float[] radial, float[] lateral, float scale)
        {
            var pts = new List<Vector3>(basePts.Count);
            for (int i = 0; i < basePts.Count; i++)
            {
                Vector3 outward = RaceCourseGeometry.SafeNormalize(basePts[i], Vector3.right);
                pts.Add(basePts[i] + outward * (radial[i] * scale) + normal * (lateral[i] * scale));
            }
            return pts;
        }

        static bool IsLegal(List<Vector3> pts, float cornerFloor, float inner, float outer, float ringRadius)
        {
            int n = pts.Count;
            // No two mouths within a ring DIAMETER of each other, so one pass can never thread
            // two gates and the wrong one can never be the nearer.
            float minSeparation = ringRadius * 2f;
            for (int i = 0; i < n; i++)
            {
                float r = pts[i].magnitude;
                if (r < inner || r > outer) return false;

                if (RaceCourseGeometry.CornerRadius(pts[(i - 1 + n) % n], pts[i], pts[(i + 1) % n]) < cornerFloor)
                    return false;

                for (int j = i + 1; j < n; j++)
                    if ((pts[i] - pts[j]).sqrMagnitude < minSeparation * minSeparation) return false;
            }
            return true;
        }
    }
}
