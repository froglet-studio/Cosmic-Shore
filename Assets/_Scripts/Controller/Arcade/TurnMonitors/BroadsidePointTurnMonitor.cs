using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Broadside. All the machinery is
    /// <see cref="CombatPointTurnMonitorBase"/>'s - target resolution from
    /// <see cref="EndConditionOverridesSO"/> (FrogletTools ▸ Game Modes ▸ End Game Conditions;
    /// never a per-scene field, per the /EndGameConditions skill), the NetworkVariable sync, the
    /// publish to <c>GameDataSO.CombatPointTargetCount</c>, and the delegation of the end
    /// condition to the mode's own ScoringRule.
    ///
    /// The one thing this mode owns is WHICH target it races to: a mixed-fleet point total
    /// where the price of a hit depends on the VERB that landed it, never on the hull
    /// (see <see cref="BroadsideScoringRuleSO"/>).
    /// </summary>
    public class BroadsidePointTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "BroadsidePointMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetBroadsidePointTarget()
                : EndConditionOverridesSO.DefaultBroadsidePointTarget;
        }
    }
}
