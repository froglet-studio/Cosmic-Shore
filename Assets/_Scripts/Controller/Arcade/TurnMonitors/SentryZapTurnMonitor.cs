using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Sentry's end condition: the shared combat-point monitor, with the target read from
    /// <see cref="EndConditionOverridesSO.GetSentryZapTarget"/> (FrogletTools > Game Modes > End Game Conditions) -
    /// never a per-scene field.
    /// </summary>
    public class SentryZapTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "SentryZapMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSentryZapTarget()
                : EndConditionOverridesSO.DefaultSentryZapTarget;
        }
    }
}
