using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Triggers/cancels drift mode. Drift is the LeftStickAction in the input pipeline
    /// — emitting it asks the vessel to perform whatever its drift implementation is.
    /// </summary>
    public class DriftPolicy : IDecisionPolicy
    {
        public string ModuleName => "Drift";

        const string GeneEnter = "drift.enter_dot";
        const string GeneExit = "drift.exit_seconds";
        const string GeneSpeedFloor = "drift.speed_floor";

        float _enterDot;
        float _exitSeconds;
        float _speedFloor;
        float _driftSince;

        public void RegisterGenes()
        {
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneEnter, 0.85f, 0.99f, 0.9f), defaultEnabled: false);
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneExit, 0.2f, 3f, 1f), defaultEnabled: false);
            GeneRegistry.Register(ModuleName, new GeneSpec(GeneSpeedFloor, 0f, 30f, 5f), defaultEnabled: false);
        }

        public void OnEpisodeStart(TrainingGenome genome)
        {
            _enterDot = genome.Get(GeneEnter);
            _exitSeconds = genome.Get(GeneExit);
            _speedFloor = genome.Get(GeneSpeedFloor);
            _driftSince = -1f;
        }

        public DecisionOutput Decide(DecisionContext ctx)
        {
            var output = DecisionOutput.Zero;

            bool wantDrift = ctx.HasTarget
                          && ctx.DotForwardObjective >= _enterDot
                          && ctx.Speed >= _speedFloor;

            if (wantDrift && !ctx.IsDrifting)
            {
                output.RequestDrift = true;
                output = output.RequestStart(InputEvents.LeftStickAction);
                _driftSince = ctx.EpisodeTime;
            }
            else if (ctx.IsDrifting)
            {
                if (_driftSince > 0f && (ctx.EpisodeTime - _driftSince) > _exitSeconds)
                {
                    output = output.RequestStop(InputEvents.LeftStickAction);
                    _driftSince = -1f;
                }
            }
            return output;
        }
    }
}
