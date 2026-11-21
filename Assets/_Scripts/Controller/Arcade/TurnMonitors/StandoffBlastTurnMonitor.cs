using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Standoff's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetStandoffBlastTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class StandoffBlastTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "StandoffBlastMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetStandoffBlastTarget()
                : EndConditionOverridesSO.DefaultStandoffBlastTarget;
        }
    }
}
