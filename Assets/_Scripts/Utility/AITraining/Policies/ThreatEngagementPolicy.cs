using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Steers toward the highest-severity threat when the genome's "engage" tendency
    /// is high; steers away when "evade" tendency is high. The two are independent so
    /// the search can find Joust-style ramming pilots OR cautious pilots on the same
    /// scenario.
    /// </summary>
    public class ThreatEngagementPolicy : IDecisionPolicy
    {
        public string ModuleName => "ThreatEngagement";

        const string GeneEngage = "threat.engage";
        const string GeneEvade = "threat.evade";
        const string GeneRange = "threat.engagement_range";
        const string GeneSteerWeight = "threat.steer_weight";

        float _engage;
        float _evade;
        float _range;
        float _steerWeight;

        public void RegisterGenes()
        {
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneEngage, 0f, 1f, 0.3f), defaultEnabled: false);
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneEvade, 0f, 1f, 0.3f), defaultEnabled: false);
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneRange, 20f, 200f, 80f), defaultEnabled: false);
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneSteerWeight, 0.1f, 1f, 0.5f), defaultEnabled: false);
        }

        public void OnEpisodeStart(TrainingGenome genome)
        {
            _engage = genome.Get(GeneEngage);
            _evade = genome.Get(GeneEvade);
            _range = genome.Get(GeneRange);
            _steerWeight = genome.Get(GeneSteerWeight);
        }

        public DecisionOutput Decide(DecisionContext ctx)
        {
            if (ctx.Threats.Count == 0) return DecisionOutput.Zero;

            ThreatInfo top = default;
            float topSev = -1f;
            foreach (var t in ctx.Threats)
            {
                if (t.Range > _range) continue;
                if (t.Severity > topSev) { topSev = t.Severity; top = t; }
            }
            if (topSev < 0f) return DecisionOutput.Zero;

            Vector3 dir = (top.Position - ctx.Position).normalized;
            // Net pull is engage - evade. Equal weights = no contribution.
            float net = _engage - _evade;
            if (Mathf.Abs(net) < 0.01f) return DecisionOutput.Zero;

            Vector3 steerDir = dir * Mathf.Sign(net);
            Vector3 cross = Vector3.Cross(ctx.Forward, steerDir);
            Vector3 localCross = ctx.Vessel.Transform.InverseTransformDirection(cross);
            float yaw = Mathf.Clamp(localCross.y * Mathf.Abs(net) * 100f, -1f, 1f);
            float pitch = Mathf.Clamp(localCross.x * Mathf.Abs(net) * 100f, -1f, 1f);

            return new DecisionOutput
            {
                SteerLocal = new Vector2(yaw, pitch),
                SteerWeight = _steerWeight,
                RequestRam = net > 0.5f && top.Range < _range * 0.3f
            };
        }
    }
}
