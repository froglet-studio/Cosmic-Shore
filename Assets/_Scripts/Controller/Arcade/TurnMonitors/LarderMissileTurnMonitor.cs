using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Larder's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetLarderMissileTarget"/> (FrogletTools > Game Modes > End Game Conditions) -
    /// never a per-scene field.
    /// </summary>
    public class LarderMissileTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "LarderMissileMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetLarderMissileTarget()
                : EndConditionOverridesSO.DefaultLarderMissileTarget;
        }
    }
}
