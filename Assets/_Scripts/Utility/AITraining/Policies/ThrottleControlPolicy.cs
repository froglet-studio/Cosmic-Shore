using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Decides forward throttle. Three competing signals:
    ///   - base cruise throttle
    ///   - additional ramp over time so the AI doesn't crawl
    ///   - brake response when a prism is close in front
    /// </summary>
    public class ThrottleControlPolicy : IDecisionPolicy
    {
        public string ModuleName => "ThrottleControl";

        const string GeneBase = "throttle.base";
        const string GeneRamp = "throttle.ramp_per_second";
        const string GeneRampCap = "throttle.ramp_cap";
        const string GeneBrakeThreshold = "throttle.brake_distance";
        const string GeneBrakeStrength = "throttle.brake_strength";
        const string GeneRamThreshold = "throttle.ram_dot";

        float _base;
        float _ramp;
        float _rampCap;
        float _brakeDist;
        float _brakeStrength;
        float _ramDot;

        float _currentRamp;

        public void RegisterGenes()
        {
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneBase, 0.3f, 1f, 0.65f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneRamp, 0f, 0.05f, 0.005f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneRampCap, 0f, 0.4f, 0.2f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneBrakeThreshold, 0f, 40f, 12f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneBrakeStrength, 0f, 1f, 0.5f));
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneRamThreshold, 0.7f, 0.99f, 0.94f));
        }

        public void OnEpisodeStart(TrainingGenome genome)
        {
            _base = genome.Get(GeneBase);
            _ramp = genome.Get(GeneRamp);
            _rampCap = genome.Get(GeneRampCap);
            _brakeDist = genome.Get(GeneBrakeThreshold);
            _brakeStrength = genome.Get(GeneBrakeStrength);
            _ramDot = genome.Get(GeneRamThreshold);
            _currentRamp = 0f;
        }

        public DecisionOutput Decide(DecisionContext ctx)
        {
            _currentRamp = Mathf.Min(_currentRamp + _ramp * Time.deltaTime, _rampCap);
            float throttle = Mathf.Clamp01(_base + _currentRamp);

            // Brake when there's a prism close in front of us.
            float closestFwdRange = float.PositiveInfinity;
            foreach (var p in ctx.NearbyPrisms)
            {
                if (p.Range > _brakeDist) continue;
                Vector3 toP = p.Position - ctx.Position;
                if (Vector3.Dot(toP.normalized, ctx.Forward) < 0.5f) continue;
                if (p.Range < closestFwdRange) closestFwdRange = p.Range;
            }
            if (!float.IsPositiveInfinity(closestFwdRange))
            {
                float t = 1f - Mathf.Clamp01(closestFwdRange / _brakeDist);
                throttle = Mathf.Lerp(throttle, throttle * (1f - _brakeStrength), t);
            }

            // Ram bonus: if we're locked on the target, push to full speed.
            if (ctx.HasTarget && ctx.DotForwardObjective >= _ramDot)
                throttle = 1f;

            return new DecisionOutput { Throttle = throttle, ThrottleWeight = 1f };
        }
    }
}
