using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Serpent Charge: the SNIPE target - how many rival pilots a domain must strip
    /// with the Sniper Shot to win (default 8) - is resolved server-side from
    /// <see cref="EndConditionOverridesSO"/> (FrogletTools > Game Modes > End Game Conditions; never a
    /// per-scene field), replicated, and published to <c>GameDataSO.CombatPointTargetCount</c>. The
    /// turn ends when <see cref="SerpentChargeScoringRuleSO"/> reports a domain's CombatPoints have
    /// reached it. Dustup's shape on the shared <see cref="CombatPointTurnMonitorBase"/>.
    /// </summary>
    public class SerpentChargeSnipeTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "SerpentChargeSnipeMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSerpentChargeSnipeTarget()
                : EndConditionOverridesSO.DefaultSerpentChargeSnipeTarget;
        }
    }
}
