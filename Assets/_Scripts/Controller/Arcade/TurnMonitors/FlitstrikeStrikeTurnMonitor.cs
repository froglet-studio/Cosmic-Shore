using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Flitstrike's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetFlitstrikeStrikeTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class FlitstrikeStrikeTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "FlitstrikeStrikeMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetFlitstrikeStrikeTarget()
                : EndConditionOverridesSO.DefaultFlitstrikeStrikeTarget;
        }
    }
}
