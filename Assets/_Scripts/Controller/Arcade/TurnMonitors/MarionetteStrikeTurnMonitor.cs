using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Marionette's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetMarionetteStrikeTarget"/> (FrogletTools > Game Modes > End Game Conditions) -
    /// never a per-scene field.
    /// </summary>
    public class MarionetteStrikeTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "MarionetteStrikeMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetMarionetteStrikeTarget()
                : EndConditionOverridesSO.DefaultMarionetteStrikeTarget;
        }
    }
}
