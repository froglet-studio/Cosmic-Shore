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
        public float LateralPerturbation; // ...and out of its plane
        public float CornerRadiusFactor;  // hard SAFETY floor: no corner tighter than this x FOR
        public float[] CornerProfile;     // the TURN ANGLES this lap is built to, in degrees
        public float RadialSwing;         // how far a vertex may be driven in/out to cut a corner
        public float AngularSpread;       // how uneven the gate spacing may become
        public float InnerRadius;         // circuit stays outside this (the nucleus)
        public float OuterRadius;         // ...and inside this (the membrane, with margin)
        public float AxisJitterDegrees;
        public float MaxPresentDegrees;
        public Vector3 FirstGateDirection; // the spawn formation's POLE - see Generate()

        // ── The vessel this course is cut for ────────────────────────────────
        // Read from Rhino.prefab and RhinoRampBoostAction.asset rather than remembered; see
        // R_VesselActions/RHINO_RAMP_BOOST.md, which derives all four.

        /// <summary>`DefaultThrottleScaler` on Rhino.prefab.</summary>
        public const float RhinoThrottleScaler = 50f;

        /// <summary>`DefaultMinimumSpeed` on Rhino.prefab.</summary>
        public const float RhinoMinimumSpeed = 10f;

        /// <summary>`maxBoostMultiplier` on RhinoRampBoostAction.asset.</summary>
        public const float RhinoMaxBoostMultiplier = 24f;

        /// <summary>`straightnessGraceBand` on RhinoRampBoostAction.asset — the deviation at
        /// which the graded ramp contributes nothing.</summary>
        public const float RhinoGraceBand = 1f;

        /// <summary>`RhinoThrottleScaler x RhinoMaxBoostMultiplier + RhinoMinimumSpeed`.</summary>
        public const float RhinoTopSpeed =
            RhinoThrottleScaler * RhinoMaxBoostMultiplier + RhinoMinimumSpeed;

        /// <summary>`RotationThrottleScaler` on Rhino.prefab.</summary>
        public const float RhinoRotationThrottleScaler = 0.5f;

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
        /// Deviation at which the ramp's FULL-POWER plateau ends —
        /// <c>StraightLineGesture.EngageThreshold</c>, restated as a compile-time constant so the
        /// generator and its tests stay pure. Past it the graded ramp trades speed for stick
        /// continuously instead of dropping off a cliff.
        /// </summary>
        public const float BoostPlateauDeviation = 0.3f;

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

        // ── The CURVE the course is actually cut against ─────────────────────
        // FlatOutRadius is one point on it. Since the ramp became GRADED
        // (RHINO_RAMP_BOOST.md) a pilot no longer chooses between the line and the boost - they
        // choose how much boost the corner is worth - so a corner is characterised by the SPEED
        // it costs, and these three functions are how a generator asks.
        //
        // The composition is exact for a single-axis turn at full throttle, which is what a corner
        // is: there the gesture's deviation IS the stick fraction (StraightLineGesture), turn rate
        // is linear in stick, and the ramp's multiplier is linear in deviation.

        /// <summary>Sustained speed a Rhino settles at while holding stick fraction
        /// <paramref name="stick"/> with the throttle buried.</summary>
        public static float SpeedAtStick(float stick)
        {
            float straight01 = 1f - Mathf.Clamp01(
                (stick - BoostPlateauDeviation) / Mathf.Max(1e-4f, RhinoGraceBand - BoostPlateauDeviation));
            float multiplier = Mathf.Lerp(1f, RhinoMaxBoostMultiplier, straight01);
            return RhinoThrottleScaler * multiplier + RhinoMinimumSpeed;
        }

        /// <summary>Radius of the circle a Rhino flies while holding <paramref name="stick"/> —
        /// <c>v / (stick x omega(v))</c>, the sustained corner it can hold there.</summary>
        public static float CornerRadiusAtStick(float stick)
        {
            if (stick <= 1e-4f) return float.PositiveInfinity;
            float v = SpeedAtStick(stick);
            return RaceCourseGeometry.MinTurnRadius(v, RhinoRotationThrottleScaler, RhinoTurnScaler) / stick;
        }

        /// <summary>
        /// The fastest a Rhino can take a corner of <paramref name="radius"/>, by inverting
        /// <see cref="CornerRadiusAtStick"/>. Bisection rather than algebra because the composed
        /// function is a ratio of two linear-in-stick terms and the closed form is unreadable;
        /// it is monotone over (0, 1], which is asserted by RhinoRampGradingTests.
        /// </summary>
        public static float FastestSpeedForCorner(float radius)
        {
            if (radius >= CornerRadiusAtStick(BoostPlateauDeviation)) return RhinoTopSpeed;
            float lo = BoostPlateauDeviation, hi = 1f;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (CornerRadiusAtStick(mid) > radius) lo = mid; else hi = mid;
            }
            return SpeedAtStick(hi);
        }

        /// <summary>
        /// The shipped circuit shape per intensity. **INTENSITY IS WHAT MIX OF CORNERS A LAP
        /// ASKS FOR**, not how big the arena is - the mode runs one cell and one gate count, and
        /// what climbs is the cornering demand.
        ///
        /// <para><b>A FLOOR IS NOT A DESIGN.</b> The first cut of this ladder authored only
        /// <c>CornerRadiusFactor</c>, a lower bound the relaxation enforced, and trusted a rising
        /// random perturbation to "genuinely PRODUCE sharper corners". Measured over 600 seeds x
        /// 8 corners, it did not: the median corner sat near the base octagon's own 0.92 x
        /// BaseRadius at every level, and <b>93% of intensity-4 corners were takeable at full
        /// speed with no lift at all</b> - which is precisely the play-test report that nothing
        /// here was worth mastering. A symmetric perturbation makes as many corners wider as
        /// narrower, and the floor then deletes the courses that got interesting. So the profile
        /// below is a TARGET the generator solves for, and the floor is demoted to what it always
        /// really was: a safety limit.</para>
        ///
        /// <para><c>CornerProfile</c> is one target TURN ANGLE per gate, dealt around the lap so
        /// the demanding ones sit apart and then rotated per seed - so which corner is the
        /// hairpin is the seed's business, and that a lap contains one is not. Every row spans
        /// from a sweeper you rebuild the ramp on to the level's hardest corner, because a lap of
        /// eight identical corners teaches one thing however hard they are. The right-hand column
        /// is the speed each costs, via <see cref="FastestSpeedForCorner"/>:</para>
        ///
        /// <code>
        ///      turn angles (deg), dealt so the big ones sit apart     corners that COST speed,
        ///                                                            median over 600 seeds
        ///   1: 125  90  60  40  25  12   5   3     one, at 99% of top    321u
        ///   2: 120  95  70  40  20   8   5   2     one, at 82%           224u
        ///   3: 135 110  80  25   6   2   1   1     two, at 65% / 96%     166u  305u
        ///   4: 150 122  88   0   0   0   0   0     three, 37% / 64% / 91%  107u  165u  268u
        /// </code>
        ///
        /// <para>Each row sums to 360 because a closed lap does. Level 4 spends its whole budget
        /// on three corners and is therefore a TRIANGLE with gates down its sides: three real
        /// braking zones and three long straights to wind the ramp back up, which is the most
        /// demanding shape eight gates can make.</para>
        ///
        /// <para>Gate COUNT is deliberately constant: it is the end-game target (authored once in
        /// <c>EndConditionOverridesSO</c>, read by both the monitor and the controller), so a
        /// match is the same length at every level and the four are comparable. Same reasoning as
        /// Switchback, and as Rampage's identical forest.</para>
        ///
        /// <para>Every row is MEASURED - <c>HeadlongCircuitTests</c> sweeps 400 seeds of each and
        /// asserts the profile is HIT, the safety floor holds, the circuit closes, no gate leaves
        /// the shell and no two mouths overlap.</para>
        /// </summary>
        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new HeadlongCircuitSettings
            {
                GateCount = 8,
                // A regular octagon at 800 has 612u legs and 45 degree corners, which fits a
                // 739u turn - 2.1x the flat-out radius. That is the relaxation's base case and
                // it is why the generator can never fail.
                BaseRadius = 800f,
                CornerProfile = new[]
                {
                    new[] { 125f,  90f,  60f,  40f,  25f,  12f,   5f,   3f },
                    new[] { 120f,  95f,  70f,  40f,  20f,   8f,   5f,   2f },
                    new[] { 135f, 110f,  80f,  25f,   6f,   2f,   1f,   1f },
                    new[] { 150f, 122f,  88f,   0f,   0f,   0f,   0f,   0f },
                }[i - 1],
                // The SAFETY floor, a little under each level's own hardest target: a corner the
                // solver overshot is still one a Rhino can hold, at roughly a quarter of top
                // speed at level 4. It is not the design - CornerProfile is.
                CornerRadiusFactor = new[] { 0.62f, 0.42f, 0.28f, 0.20f }[i - 1],
                // How hard the generator is allowed to work to hit the profile. A tight corner
                // needs a vertex driven OUT between two driven IN and its two gates pulled
                // angularly TOGETHER - radius alone cannot do it (on a circle the corner radius
                // is just BaseRadius x cos(half the gap), so a small gap gives a small turn AND a
                // short leg and the two cancel).
                RadialSwing = 0.42f,
                AngularSpread = new[] { 1.2f, 2.0f, 2.8f, 3.6f }[i - 1],
                LateralPerturbation = new[] { 120f, 170f, 215f, 260f }[i - 1],
                // Wider than Switchback's ladder at every step: a Rhino arrives at up to
                // 1210 u/s against a Dolphin's 347, so it crosses a mouth in a quarter of the
                // time and has a quarter of the lateral authority to correct with on the way in.
                RingRadius = new[] { 96f, 72f, 58f, 46f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                // The presentation cap must COVER half the level's hardest turn, because a gate
                // faces the BISECTOR of its corner - so a 149 degree hairpin presents its mouth
                // 74.4 degrees off the line you arrive on however the jitter is spent, and no
                // authoring can improve on that. The measured worst halfTurn over 600 seeds is
                // 43.6 / 60.3 / 70.8 / 74.4, and each cap sits just above its own. Get this
                // wrong and the jitter budget (`cap - halfTurn`) clamps to zero at every real
                // corner, so the gates that most need to face you are the ones that stop being
                // oriented at all. Level 1 and 2 keep meaningful jitter everywhere; 3 and 4
                // spend most of theirs on the sweepers, which is the correct place for it.
                MaxPresentDegrees = new[] { 50f, 64f, 74f, 78f }[i - 1],
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
        /// How many times the shape is relaxed toward the neutral ring before falling back to it
        /// outright. Each step multiplies by <see cref="RelaxationRate"/>, so 48 steps reach 2e-3
        /// - far past the point where the base case dominates.
        /// </summary>
        const int RelaxationSteps = 48;
        const float RelaxationRate = 0.88f;

        /// <summary>Gauss-Seidel sweeps over the vertices while solving the corner profile. A
        /// vertex's sharpness moves its two gate GAPS, so its neighbours' corners move with it;
        /// three sweeps is measured to be past convergence for eight vertices.</summary>
        const int ProfileSweeps = 6;
        const int BisectionSteps = 24;

        /// <summary>Keeps a solved radius off the shell walls, so the profile solve does not
        /// hand the legality pass a course it is guaranteed to have to relax.</summary>
        const float ShellMargin = 24f;

        /// <summary>
        /// The circuit, in CELL-LOCAL coordinates (the caller adds the cell centre). Never null
        /// and never shorter than <c>GateCount</c> - see the class docs.
        /// </summary>
        public static List<RaceGate> Generate(int seed, HeadlongCircuitSettings s)
        {
            int n = Mathf.Max(3, s.GateCount);
            var rng = new RaceCourseGeometry.Rng(seed);

            // A random plane through the cell centre, and the phase of the ring on it.
            Vector3 normal = RaceCourseGeometry.SafeNormalize(
                new Vector3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f)), Vector3.up);
            Vector3 u = RaceCourseGeometry.Perpendicular(normal);
            Vector3 w = Vector3.Cross(normal, u);
            float phase = rng.Range(0f, 2f * Mathf.PI);

            // WHICH corner is the hairpin is the seed's business; THAT the lap contains one is
            // not. Shuffling the authored profile is the whole of that distinction.
            float[] targets = ShuffledTargets(ref rng, n, s);

            // Out-of-plane offsets, drawn once and scaled as a whole by the relaxation - the same
            // monotonicity argument the original generator used, and the reason it terminates.
            var lateral = new float[n];
            for (int i = 0; i < n; i++)
                lateral[i] = rng.Range(-s.LateralPerturbation, s.LateralPerturbation);

            // SOLVE the shape to the profile. `sharp[i]` in [0,1] drives vertex i outward and
            // pulls its two gate gaps closed together, which is monotone-decreasing in corner
            // radius - so one bisection per vertex, swept until the neighbours settle.
            var sharp = new float[n];
            for (int i = 0; i < n; i++) sharp[i] = 0.5f;
            SolveProfile(sharp, targets, s, n);

            float floor = s.CornerRadiusFactor * HeadlongCircuitSettings.FlatOutRadius;
            float scale = 1f;
            List<Vector3> pts = null;

            for (int step = 0; step <= RelaxationSteps; step++)
            {
                pts = Build(sharp, lateral, u, w, normal, phase, s, n, scale);
                if (IsLegal(pts, floor, s.InnerRadius, s.OuterRadius, s.RingRadius)) break;
                scale *= RelaxationRate;
                if (step == RelaxationSteps) pts = Build(sharp, lateral, u, w, normal, phase, s, n, 0f);
            }

            // GATE 0 SITS ON THE SPAWN FORMATION'S POLE, and that is a fairness rule rather than
            // a layout preference: pilots spawn on an equatorial ring around the cell, so every
            // one of them is equidistant from a point on that ring's axis. Put the first gate
            // anywhere else and whoever spawned nearest it starts the lap ahead.
            //
            // Applied as a RIGID rotation of the finished circuit, which is why it is free: every
            // property the solve just established - corner radii, turn angles, leg lengths,
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

        /// <summary>
        /// The level's authored TURN ANGLES, Fisher-Yates shuffled. A profile shorter or longer
        /// than the gate count is cycled, so the two can be authored independently.
        ///
        /// <para><b>Turn angle rather than corner radius, and that is a feasibility argument
        /// rather than a preference.</b> A closed loop turns through 360 degrees in total, so a
        /// profile stated in angles is satisfiable BY CONSTRUCTION as long as it sums to about
        /// that - while a profile stated in radii can quietly ask for eight corners each tighter
        /// than the ring can give, at which point every vertex saturates the solver together, the
        /// contrast vanishes and the generator hands back the neutral ring. The first cut asked
        /// exactly that and shipped 100% free corners at every intensity while looking correct.
        /// The mean is fixed at 360/N = 45 degrees, so a hairpin is PAID FOR in kinks, which is
        /// why every row below ends in near-straights.</para>
        /// </summary>
        static float[] ShuffledTargets(ref RaceCourseGeometry.Rng rng, int n, HeadlongCircuitSettings s)
        {
            var profile = s.CornerProfile;
            var targets = new float[n];
            for (int i = 0; i < n; i++)
                targets[i] = profile != null && profile.Length > 0
                    ? profile[i % profile.Length]
                    : 360f / n;
            // SPREAD the demanding corners around the lap instead of shuffling freely. Two
            // hairpins landing next to each other is not merely a worse rhythm, it is
            // geometrically self-defeating: a sharp corner is built by driving one vertex out
            // between two pulled in, so two adjacent vertices both asking to be the spike cancel
            // and the solver settles for two medium corners (measured: adjacent 135 and 100
            // degree targets both came out at ~92). Dealing the sorted profile alternately into
            // even then odd slots puts the two biggest turns half a lap apart by construction;
            // the rotation and the direction flip are what is left for the seed.
            System.Array.Sort(targets);
            System.Array.Reverse(targets);
            var placed = new float[n];
            int slot = 0;
            for (int k = 0; k < n; k++)
            {
                placed[slot] = targets[k];
                slot += 2;
                if (slot >= n) slot = 1;
            }
            int rotate = Mathf.Clamp((int)rng.Range(0f, n), 0, n - 1);
            bool flip = rng.Range(0f, 1f) < 0.5f;
            for (int i = 0; i < n; i++)
                targets[i] = placed[(flip ? (n - i) % n : i + rotate) % n];
            return targets;
        }

        /// <summary>
        /// Drive each vertex's sharpness to the corner radius the profile asked for. Solved on
        /// the FLAT ring (no lateral offsets, no relaxation): out-of-plane displacement only ever
        /// opens a corner slightly, and solving against the flat shape keeps the target the thing
        /// the ladder is authored in.
        /// </summary>
        static void SolveProfile(float[] sharp, float[] targets, HeadlongCircuitSettings s, int n)
        {
            for (int sweep = 0; sweep < ProfileSweeps; sweep++)
            {
                for (int i = 0; i < n; i++)
                {
                    // Monotone increasing: more sharpness = more turn. Bisect rather than solve,
                    // because the turn angle is a ratio of two terms that both move with the same
                    // parameter.
                    float lo = 0f, hi = 1f;
                    for (int step = 0; step < BisectionSteps; step++)
                    {
                        float mid = 0.5f * (lo + hi);
                        sharp[i] = mid;
                        if (FlatTurnDegrees(sharp, s, n, i) < targets[i]) lo = mid; else hi = mid;
                    }
                    sharp[i] = 0.5f * (lo + hi);
                }
            }
        }

        /// <summary>Turn angle at <paramref name="i"/> on the flat, un-relaxed ring.</summary>
        static float FlatTurnDegrees(float[] sharp, HeadlongCircuitSettings s, int n, int i)
        {
            var pts = FlatRing(sharp, s, n, 1f);
            return RaceCourseGeometry.Angle(pts[i] - pts[(i - 1 + n) % n], pts[(i + 1) % n] - pts[i]);
        }

        /// <summary>
        /// The ring in its own plane, as (x, y) pairs packed into <see cref="Vector3"/> with z=0.
        ///
        /// <para>Two things vary per vertex and BOTH are needed. Radius alone cannot cut a tight
        /// corner: on a circle the corner radius is exactly <c>BaseRadius x cos(gap/2)</c>, so a
        /// narrow gap shortens the leg and softens the turn in the same proportion and they
        /// cancel. Driving a vertex OUT while pulling its two gaps CLOSED is what makes a
        /// hairpin - the leg stays short while the turn goes past 120 degrees.</para>
        /// </summary>
        static Vector3[] FlatRing(float[] sharp, HeadlongCircuitSettings s, int n, float scale)
        {
            // Gap i sits between vertex i and vertex i+1, and is squeezed by whichever of the two
            // is sharper - a hairpin needs BOTH its legs short.
            var gap = new float[n];
            float total = 0f, meanSharp = 0f;
            for (int i = 0; i < n; i++)
            {
                float squeeze = Mathf.Max(sharp[i], sharp[(i + 1) % n]);
                gap[i] = 1f + s.AngularSpread * scale * (1f - squeeze);
                total += gap[i];
                meanSharp += sharp[i];
            }
            meanSharp /= n;

            // BOTH shape terms are CONTRAST, measured against the ring's own mean, and that is
            // load-bearing rather than tidy. The first cut drove radius absolutely
            // (`1 + swing x (2a - 1)`), so when the profile asked for more sharpness than the
            // geometry could deliver every vertex saturated at a=1 together, the whole ring
            // inflated to the shell, and the generator shipped a LARGER regular octagon - gentler
            // corners than the base case, in the name of tightening them (measured: every corner
            // 2.3-2.9x the flat-out radius at every intensity, 100% of them free). A closed loop
            // turns through exactly 360 degrees whatever you ask of it, so "make every corner
            // sharp" has no answer and only the DIFFERENCES between vertices can mean anything.
            float inner = s.InnerRadius + ShellMargin, outer = s.OuterRadius - ShellMargin;
            var pts = new Vector3[n];
            float angle = 0f;
            for (int i = 0; i < n; i++)
            {
                float r = s.BaseRadius * (1f + 2f * s.RadialSwing * scale * (sharp[i] - meanSharp));
                if (outer > inner) r = Mathf.Clamp(r, inner, outer);
                pts[i] = new Vector3(r * Mathf.Cos(angle), r * Mathf.Sin(angle), 0f);
                angle += 2f * Mathf.PI * gap[i] / total;
            }
            return pts;
        }

        /// <summary>The solved ring lifted into the cell, with the out-of-plane offsets applied at
        /// the relaxation's current scale.</summary>
        static List<Vector3> Build(float[] sharp, float[] lateral, Vector3 u, Vector3 w, Vector3 normal,
                                   float phase, HeadlongCircuitSettings s, int n, float scale)
        {
            var flat = FlatRing(sharp, s, n, scale);
            var pts = new List<Vector3>(n);
            float cp = Mathf.Cos(phase), sp = Mathf.Sin(phase);
            for (int i = 0; i < n; i++)
            {
                float x = flat[i].x * cp - flat[i].y * sp;
                float y = flat[i].x * sp + flat[i].y * cp;
                pts.Add(u * x + w * y + normal * (lateral[i] * scale));
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
