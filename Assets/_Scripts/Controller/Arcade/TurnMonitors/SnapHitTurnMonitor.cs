using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Snap's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetSnapHitTarget"/> (FrogletTools > Game Modes > End Game Conditions) - never a
    /// per-scene field.
    /// </summary>
    public class SnapHitTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "SnapHitMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSnapHitTarget()
                : EndConditionOverridesSO.DefaultSnapHitTarget;
        }
    }
}
