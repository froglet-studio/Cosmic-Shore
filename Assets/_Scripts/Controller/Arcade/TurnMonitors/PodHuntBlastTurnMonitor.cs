using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pod Hunt's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetPodHuntBlastTarget"/> (FrogletTools > Game Modes > End Game Conditions) -
    /// never a per-scene field.
    /// </summary>
    public class PodHuntBlastTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "PodHuntBlastMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetPodHuntBlastTarget()
                : EndConditionOverridesSO.DefaultPodHuntBlastTarget;
        }
    }
}
