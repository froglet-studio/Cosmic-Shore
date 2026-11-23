using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hood's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetSerpentHoodSnipeTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class SerpentHoodSnipeTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "SerpentHoodSnipeMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSerpentHoodSnipeTarget()
                : EndConditionOverridesSO.DefaultSerpentHoodSnipeTarget;
        }
    }
}
