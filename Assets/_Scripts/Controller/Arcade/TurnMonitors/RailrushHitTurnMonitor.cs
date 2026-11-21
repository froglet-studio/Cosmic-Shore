using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Railrush's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetRailrushHitTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class RailrushHitTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "RailrushHitMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetRailrushHitTarget()
                : EndConditionOverridesSO.DefaultRailrushHitTarget;
        }
    }
}
