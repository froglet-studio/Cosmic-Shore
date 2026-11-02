using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Gulag's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetSerpentGulagSnipeTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class SerpentGulagSnipeTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "SerpentGulagSnipeMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSerpentGulagSnipeTarget()
                : EndConditionOverridesSO.DefaultSerpentGulagSnipeTarget;
        }
    }
}
