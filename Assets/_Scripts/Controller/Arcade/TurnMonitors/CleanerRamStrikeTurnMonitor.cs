using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cleaner Ram's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetCleanerRamStrikeTarget"/> (FrogletTools > Game Modes > End Game Conditions)
    /// - never a per-scene field.
    /// </summary>
    public class CleanerRamStrikeTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "CleanerRamStrikeMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetCleanerRamStrikeTarget()
                : EndConditionOverridesSO.DefaultCleanerRamStrikeTarget;
        }
    }
}
