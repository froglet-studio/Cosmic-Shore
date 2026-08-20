using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Dog Fight. All the machinery is
    /// <see cref="CombatPointTurnMonitorBase"/>'s - target resolution from
    /// <see cref="EndConditionOverridesSO"/> (FrogletTools ▸ Game Modes ▸ End Game Conditions;
    /// never a per-scene field, per the /EndGameConditions skill), the NetworkVariable sync, the
    /// publish to <c>GameDataSO.CombatPointTargetCount</c>, and the delegation of the end
    /// condition to the mode's own ScoringRule.
    ///
    /// The one thing this mode owns is WHICH target it races to: gunnery points, where a bullet
    /// hit is 1 and a missile hit is 50 (see <see cref="DogFightScoringRuleSO"/>).
    /// </summary>
    public class DogFightPointTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "DogFightPointMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetDogFightPointTarget()
                : EndConditionOverridesSO.DefaultDogFightPointTarget;
        }
    }
}
