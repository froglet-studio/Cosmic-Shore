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

            return new DecisionOutput
            {
                SteerLocal = new Vector2(yaw, pitch),
                SteerWeight = _steerWeight
            };
        }
    }
}
