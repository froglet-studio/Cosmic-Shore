using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Riposte's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetRiposteStrikeTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class RiposteStrikeTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "RiposteStrikeMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetRiposteStrikeTarget()
                : EndConditionOverridesSO.DefaultRiposteStrikeTarget;
        }
    }
}
