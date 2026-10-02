using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Steers the vessel toward the current target. This is the primary objective
    /// pursuit policy — without it the vessel just drifts.
    ///
    /// Pure pursuit orbits a target that sits inside the vessel's turning circle
    /// (Dubins reachability). While trapped, this policy steers an escape heading
    /// instead — the same extend-and-reattack <see cref="AIPilot"/> uses — then
    /// resumes pursuit once there is runway.
    ///
    /// Genes:
    ///   target.aggressiveness — how hard the cross-product steering pulls
    ///   target.deadzone       — angle dot threshold below which we ease steering
    ///   target.lead_seconds   — how far ahead to aim along target velocity
    ///   target.steer_weight   — blend weight vs other steer policies
    /// </summary>
    public class TargetSeekingPolicy : IDecisionPolicy
    {
        public string ModuleName => "TargetSeeking";

        const string GeneAggressiveness = "target.aggressiveness";
        const string GeneDeadzone = "target.deadzone";
        const string GeneLead = "target.lead_seconds";
        const string GeneSteerWeight = "target.steer_weight";

        // AIPilot caps the same divisor at its _maxDistance (50). Dividing by the raw squared
        // range instead made the turn vanish past a few units: a crystal 100 units off gave
        // under one degree of input, so the vessel flew straight past every crystal on the track.
        public const float MaxSteerDivisor = 50f;

        // Mirror AIPilot's orbit-break defaults — crystal collect radius + hull margin.
        public const float CaptureRadius = 18f;
        const float OrbitBreakAwayBias = 0.35f;
        const float ApproachRunSeconds = 1.5f;
        const float OrbitBreakMinSeconds = 0.6f;
        const float OrbitBreakMaxSeconds = 6f;
        const float BreakOffClosingCosine = 0.5f;
        const float OrbitSweepDegrees = 540f;
        const float OrbitProgressFraction = 0.9f;
        const float OrbitTargetJumpFraction = 1.6f;

        // Floor matches AIPilot's hardcoded aggressiveness=100f. Archive gen-2 shipped 20,
        // which is a ~5× weaker turn — Get() clamps on read so old chromosomes still turn.
        public const float AggressivenessFloor = 100f;
        public const float SteerWeightFloor = 0.75f;

        float _aggressiveness;
        float _deadzone;
        float _lead;
        float _steerWeight;

        OrbitDetector _orbitDetector;
        bool _extending;
        float _extendElapsed;
        Vector3 _lastTarget;

        public void RegisterGenes()
        {
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneAggressiveness, AggressivenessFloor, 200f, 120f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneDeadzone, 0.85f, 0.97f, 0.94f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneLead, 0f, 1.5f, 0.25f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneSteerWeight, SteerWeightFloor, 1f, 0.95f));
        }

        public void OnEpisodeStart(TrainingGenome genome)
        {
            _aggressiveness = genome.Get(GeneAggressiveness);
            _deadzone = genome.Get(GeneDeadzone);
            _lead = genome.Get(GeneLead);
            _steerWeight = genome.Get(GeneSteerWeight);
            EndOrbitBreak();
            _orbitDetector.Reset();
            _lastTarget = Vector3.zero;
        }

        public DecisionOutput Decide(DecisionContext ctx)
        {
            if (!ctx.HasTarget) return DecisionOutput.Zero;

            // A drift locks Course; the turning-circle model does not describe it.
            if (ctx.IsDrifting)
            {
                EndOrbitBreak();
                return SteerToward(ctx, AimPoint(ctx));
            }

            Vector3 aim = AimPoint(ctx);
            Vector3 toObjective = aim - ctx.Position;

            // Objective jump (crystal collected → next) invalidates break-off / sweep state.
            if ((_lastTarget - aim).sqrMagnitude > 1f)
            {
                EndOrbitBreak();
                _orbitDetector.Reset();
            }
            _lastTarget = aim;

            UpdateOrbitBreak(ctx, toObjective);

            Vector3 steerPoint = _extending
                ? ctx.Position + PursuitReachability.EscapeDirection(
                      toObjective, Heading(ctx), OrbitBreakAwayBias) * EscapeLegLength(ctx)
                : aim;

            return SteerToward(ctx, steerPoint);
        }

        Vector3 AimPoint(DecisionContext ctx) =>
            ctx.TargetPosition + ctx.TargetVelocity * _lead;

        static Vector3 Heading(DecisionContext ctx)
        {
            // Course is the direction of TRAVEL (drift can diverge from the nose).
            Vector3 course = ctx.VesselStatus != null ? ctx.VesselStatus.Course : Vector3.zero;
            if (course.sqrMagnitude > 1e-4f) return course.normalized;
            if (ctx.Velocity.sqrMagnitude > 1e-4f) return ctx.Velocity.normalized;
            return ctx.Forward;
        }

        float MinTurnRadius(DecisionContext ctx)
        {
            var xf = ctx.VesselStatus != null ? ctx.VesselStatus.VesselTransformer : null;
            return xf != null ? xf.MinTurnRadius : 0f;
        }

        float RequiredApproachRun(DecisionContext ctx)
        {
            float radius = MinTurnRadius(ctx);
            float geometric = PursuitReachability.GuaranteedReachableSeparation(radius, CaptureRadius);
            if (float.IsInfinity(geometric)) geometric = MaxSteerDivisor;
            float tactical = Mathf.Abs(ctx.Speed) * ApproachRunSeconds;
            return Mathf.Max(geometric, tactical);
        }

        float EscapeLegLength(DecisionContext ctx)
        {
            float run = RequiredApproachRun(ctx);
            return run > 1f ? run * 2f : MaxSteerDivisor;
        }

        void UpdateOrbitBreak(DecisionContext ctx, Vector3 toObjective)
        {
            float radius = MinTurnRadius(ctx);

            if (_extending)
            {
                _extendElapsed += Time.deltaTime;
                bool hasRunway = toObjective.magnitude >= RequiredApproachRun(ctx);
                if ((hasRunway && _extendElapsed >= OrbitBreakMinSeconds) ||
                    _extendElapsed >= OrbitBreakMaxSeconds)
                    EndOrbitBreak();
                return;
            }

            // Never peel off while genuinely closing (range falling ≈ closing cosine).
            bool closing = toObjective.sqrMagnitude > 1e-4f &&
                           Vector3.Dot(toObjective.normalized, Heading(ctx)) > BreakOffClosingCosine;

            bool unreachable = !closing && PursuitReachability.IsInsideTurningCircle(
                toObjective, Heading(ctx), radius, CaptureRadius);
            bool orbiting = _orbitDetector.Tick(toObjective, OrbitSweepDegrees,
                                                OrbitProgressFraction, OrbitTargetJumpFraction);

            if (!unreachable && !orbiting) return;

            _extending = true;
            _extendElapsed = 0f;
            _orbitDetector.Reset();
        }

        void EndOrbitBreak()
        {
            if (!_extending) return;
            _extending = false;
            _extendElapsed = 0f;
            _orbitDetector.Reset();
        }

        DecisionOutput SteerToward(DecisionContext ctx, Vector3 steerPoint)
        {
            Vector3 toTarget = steerPoint - ctx.Position;
            float dist = toTarget.magnitude;
            if (dist < 1e-3f) return DecisionOutput.Zero;

            Vector3 dir = toTarget / dist;
            float dotForward = Vector3.Dot(dir, ctx.Forward);

            // Aimed well enough: contribute ZERO steer but keep FULL blend weight.
            // Cutting weight here (old 0.25×) let ObstacleAvoidance own the blend exactly when
            // the nose was on the crystal — the AI peeled off every close approach.
            // Never ease during a break-off — that would cancel the escape run.
            if (!_extending && dotForward >= _deadzone)
                return new DecisionOutput { SteerWeight = _steerWeight };

            Vector3 cross = Vector3.Cross(ctx.Forward, dir);
            Vector3 localCross = ctx.Vessel.Transform.InverseTransformDirection(cross);

            float sqr = Mathf.Clamp(toTarget.sqrMagnitude, 1f, MaxSteerDivisor);
            float angle = Mathf.Asin(Mathf.Clamp(localCross.sqrMagnitude * _aggressiveness / sqr, -1f, 1f)) * Mathf.Rad2Deg;

            float yaw = Mathf.Clamp(angle * localCross.y, -1f, 1f);
            float pitch = Mathf.Clamp(angle * localCross.x, -1f, 1f);

            return new DecisionOutput
            {
                SteerLocal = new Vector2(yaw, pitch),
                SteerWeight = _steerWeight
            };
        }
    }
}
