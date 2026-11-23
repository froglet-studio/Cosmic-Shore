using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Corona's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetCoronaBloomTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class CoronaBloomTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "CoronaBloomMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetCoronaBloomTarget()
                : EndConditionOverridesSO.DefaultCoronaBloomTarget;
        }
    }
}
