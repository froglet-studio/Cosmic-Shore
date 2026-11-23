using CosmicShore.Data;
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
    /// hit is 1 and a rocket pays its best tier, 10/20/30 (see <see cref="DogFightScoringRuleSO"/>).
    /// Grizzly Charge races on this same monitor and price list but has its own target key, so
    /// Dog Fight's length can move without moving it.
    /// </summary>
    public class DogFightPointTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "DogFightPointMonitor";

        protected override int ResolvePointTarget()
        {
            bool grizzlyCharge = gameData != null && gameData.GameMode == GameModes.GrizzlyCharge;
            var overrides = EndConditionOverridesSO.Instance;
            if (overrides == null)
                return grizzlyCharge
                    ? EndConditionOverridesSO.DefaultGrizzlyChargePointTarget
                    : EndConditionOverridesSO.DefaultDogFightPointTarget;
            return grizzlyCharge
                ? overrides.GetGrizzlyChargePointTarget()
                : overrides.GetDogFightPointTarget();
        }
    }
}
