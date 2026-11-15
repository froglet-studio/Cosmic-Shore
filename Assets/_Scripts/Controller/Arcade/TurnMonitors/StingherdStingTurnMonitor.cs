using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Stingherd's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetStingherdStingTarget"/> (FrogletTools > Game Modes > End Game Conditions) -
    /// never a per-scene field.
    /// </summary>
    public class StingherdStingTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "StingherdStingMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetStingherdStingTarget()
                : EndConditionOverridesSO.DefaultStingherdStingTarget;
        }
    }
}
