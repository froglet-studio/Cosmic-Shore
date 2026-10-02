using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Pushes steering away from prisms in the vessel's near forward arc.
    /// Operates in the same local-cross-product space as TargetSeekingPolicy
    /// so the two outputs blend naturally rather than fighting each other.
    /// </summary>
    public class ObstacleAvoidancePolicy : IDecisionPolicy
    {
        public string ModuleName => "ObstacleAvoidance";

        const string GeneRadius = "avoid.radius";
        const string GeneStrength = "avoid.strength";
        const string GeneStandoff = "avoid.standoff";
        const string GeneArcDot = "avoid.arc_dot";
        const string GeneSteerWeight = "avoid.steer_weight";

        float _radius;
        float _strength;
        float _standoff;
        float _arcDot;
        float _steerWeight;

        public void RegisterGenes()
        {
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneRadius, 10f, 120f, 40f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneStrength, 0.02f, 0.6f, 0.18f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneStandoff, 2f, 30f, 10f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneArcDot, 0.0f, 0.95f, 0.4f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneSteerWeight, 0.1f, 1f, 0.6f));
        }

        public void OnEpisodeStart(TrainingGenome genome)
        {
            _radius = genome.Get(GeneRadius);
            _strength = genome.Get(GeneStrength);
            _standoff = genome.Get(GeneStandoff);
            _arcDot = genome.Get(GeneArcDot);
            _steerWeight = genome.Get(GeneSteerWeight);
        }

        public DecisionOutput Decide(DecisionContext ctx)
        {
            // Once the nose is already on a crystal, stand down entirely — trail prisms near a
            // collectable would otherwise peel the approach off every time. Checked before the
            // prism loop so an aimed crystal chase never pays for (or depends on) Vessel.
            if (ctx.HasTarget && ctx.TargetKind == TargetKind.Crystal
                && ctx.DotForwardObjective >= 0.85f)
                return DecisionOutput.Zero;

            if (ctx.NearbyPrisms.Count == 0) return DecisionOutput.Zero;

            Vector3 push = Vector3.zero;
            int contributors = 0;

            foreach (var p in ctx.NearbyPrisms)
            {
                if (p.Range > _radius) continue;

                Vector3 toPrism = p.Position - ctx.Position;
                float dist = toPrism.magnitude;
                if (dist < 1e-3f) continue;

                Vector3 dir = toPrism / dist;
                float fwdDot = Vector3.Dot(dir, ctx.Forward);
                if (fwdDot < _arcDot) continue;     // not in front, ignore

                // Inverse-square push, capped so a single very close prism doesn't peg the steering.
                float intensity = Mathf.Clamp01(_standoff / Mathf.Max(dist, 1f));
                push -= dir * intensity;
                contributors++;
            }

            if (contributors == 0) return DecisionOutput.Zero;

            push /= contributors;
            Vector3 cross = Vector3.Cross(ctx.Forward, push);
            Vector3 localCross = ctx.Vessel.Transform.InverseTransformDirection(cross);

            float yaw = Mathf.Clamp(localCross.y * _strength * 100f, -1f, 1f);
            float pitch = Mathf.Clamp(localCross.x * _strength * 100f, -1f, 1f);

            // Archive gen-2 shipped avoid.steer_weight ≈ 0.95 against target.steer_weight ≈ 0.58,
            // so blended steering was mostly "don't hit trails" and crystals were under-aimed.
            // While pursuing a crystal, keep avoidance as a veto bias rather than the primary aim.
            float weight = _steerWeight;
            if (ctx.HasTarget && ctx.TargetKind == TargetKind.Crystal)
                weight = Mathf.Min(weight, 0.35f);

            return new DecisionOutput
            {
                SteerLocal = new Vector2(yaw, pitch),
                SteerWeight = weight
            };
        }
    }
}
