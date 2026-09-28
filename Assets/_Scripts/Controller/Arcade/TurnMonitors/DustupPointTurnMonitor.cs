using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Dustup's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetDustupPointTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class DustupPointTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "DustupPointMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetDustupPointTarget()
                : EndConditionOverridesSO.DefaultDustupPointTarget;
        }
    }
}
