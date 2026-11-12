using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Kelpwood's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetKelpwoodBiteTarget"/> (FrogletTools > Game Modes >
    /// End Game Conditions) - never a per-scene field.
    /// </summary>
    public class KelpwoodBiteTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "KelpwoodBiteMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetKelpwoodBiteTarget()
                : EndConditionOverridesSO.DefaultKelpwoodBiteTarget;
        }
    }
}
