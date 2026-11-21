using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Jackstraw's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetJackstrawStrikeTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class JackstrawStrikeTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "JackstrawStrikeMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetJackstrawStrikeTarget()
                : EndConditionOverridesSO.DefaultJackstrawStrikeTarget;
        }
    }
}
