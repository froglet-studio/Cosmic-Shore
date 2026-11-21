using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Skim along nearby friendly trails to charge boost / pick up prism gains
    /// without colliding into them. Adds a lateral bias when a prism is at the
    /// genome-configured standoff range — gentle attraction rather than blunt
    /// avoidance.
    /// </summary>
    public class SkimPolicy : IDecisionPolicy
    {
        public string ModuleName => "Skim";

        const string GeneStandoff = "skim.standoff";
        const string GeneRadius = "skim.radius";
        const string GeneStrength = "skim.strength";
        const string GeneFriendlyOnly = "skim.friendly_only";

        float _standoff;
        float _radius;
        float _strength;
        bool _friendlyOnly;

        public void RegisterGenes()
        {
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneStandoff, 4f, 30f, 12f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneRadius, 20f, 200f, 80f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneStrength, 0.0f, 0.3f, 0.1f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneFriendlyOnly, 0f, 1f, 1f));
        }

        public void OnEpisodeStart(TrainingGenome genome)
        {
            _standoff = genome.Get(GeneStandoff);
            _radius = genome.Get(GeneRadius);
            _strength = genome.Get(GeneStrength);
            _friendlyOnly = genome.Get(GeneFriendlyOnly) >= 0.5f;
        }

        public DecisionOutput Decide(DecisionContext ctx)
        {
            if (ctx.NearbyPrisms.Count == 0) return DecisionOutput.Zero;

            Vector3 bias = Vector3.zero;
            int contributors = 0;

            foreach (var p in ctx.NearbyPrisms)
            {
                if (p.Range > _radius) continue;
                if (_friendlyOnly && p.IsHostile) continue;

                Vector3 toPrism = p.Position - ctx.Position;
                float dist = toPrism.magnitude;
                if (dist < 1e-3f) continue;

                Vector3 dir = toPrism / dist;
                // Pull in when farther than standoff, push out when closer — a soft attractor.
                float pull = (dist - _standoff) / Mathf.Max(_standoff, 1f);
                pull = Mathf.Clamp(pull, -1f, 1f);
                bias += dir * pull;
                contributors++;
            }

            if (contributors == 0) return DecisionOutput.Zero;
            bias /= contributors;

            Vector3 cross = Vector3.Cross(ctx.Forward, bias);
            Vector3 localCross = ctx.Vessel.Transform.InverseTransformDirection(cross);

            float yaw = Mathf.Clamp(localCross.y * _strength * 50f, -1f, 1f);
            float pitch = Mathf.Clamp(localCross.x * _strength * 50f, -1f, 1f);

            return new DecisionOutput
            {
                SteerLocal = new Vector2(yaw, pitch),
                SteerWeight = _strength    // weak by design — should not overpower target seek
            };
        }
    }
}
