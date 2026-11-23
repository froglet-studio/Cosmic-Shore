using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shockfront's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetShockfrontRippleTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class ShockfrontRippleTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "ShockfrontRippleMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetShockfrontRippleTarget()
                : EndConditionOverridesSO.DefaultShockfrontRippleTarget;
        }
    }
}
