using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Steers the vessel toward the current target. This is the primary objective
    /// pursuit policy — without it the vessel just drifts.
    ///
    /// Genes:
    ///   target.aggressiveness — how hard the cross-product steering pulls
    ///   target.deadzone       — angle dot threshold below which we stop steering
    ///   target.lead_seconds   — how far ahead to aim along target velocity
    /// </summary>
    public class TargetSeekingPolicy : IDecisionPolicy
    {
        public string ModuleName => "TargetSeeking";

        const string GeneAggressiveness = "target.aggressiveness";
        const string GeneDeadzone = "target.deadzone";
        const string GeneLead = "target.lead_seconds";
        const string GeneSteerWeight = "target.steer_weight";

        float _aggressiveness;
        float _deadzone;
        float _lead;
        float _steerWeight;

        public void RegisterGenes()
        {
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneAggressiveness, 20f, 200f, 100f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneDeadzone, 0.92f, 0.999f, 0.97f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneLead, 0f, 1.5f, 0.25f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneSteerWeight, 0.2f, 1f, 0.9f));
        }

        public void OnEpisodeStart(TrainingGenome genome)
        {
            _aggressiveness = genome.Get(GeneAggressiveness);
            _deadzone = genome.Get(GeneDeadzone);
            _lead = genome.Get(GeneLead);
            _steerWeight = genome.Get(GeneSteerWeight);
        }

        public DecisionOutput Decide(DecisionContext ctx)
        {
            if (!ctx.HasTarget) return DecisionOutput.Zero;

            Vector3 leadPos = ctx.TargetPosition + ctx.TargetVelocity * _lead;
            Vector3 toTarget = leadPos - ctx.Position;
            float dist = toTarget.magnitude;
            if (dist < 1e-3f) return DecisionOutput.Zero;

            Vector3 dir = toTarget / dist;
            float dotForward = Vector3.Dot(dir, ctx.Forward);

            // If we're already pointing within the deadzone, ease off.
            if (dotForward >= _deadzone)
                return new DecisionOutput { SteerWeight = _steerWeight * 0.25f };

            Vector3 cross = Vector3.Cross(ctx.Forward, dir);
            // Inverse-transform so the values match what the existing AIPilot computed —
            // this preserves the calibration of the rest of the input pipeline.
            Vector3 localCross = ctx.Vessel.Transform.InverseTransformDirection(cross);

            float sqr = Mathf.Max(toTarget.sqrMagnitude, 1f);
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
