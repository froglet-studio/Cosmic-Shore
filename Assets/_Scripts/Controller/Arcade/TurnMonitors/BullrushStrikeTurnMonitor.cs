using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Bullrush's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetBullrushGoreTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class BullrushStrikeTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "BullrushStrikeMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetBullrushGoreTarget()
                : EndConditionOverridesSO.DefaultBullrushGoreTarget;
        }
    }
}
