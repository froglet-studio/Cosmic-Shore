using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for The Bends. All the machinery is
    /// <see cref="CombatPointTurnMonitorBase"/>'s; the one thing this mode owns is WHICH target
    /// it races to - bends, where one opposing pilot caught in a Dolphin crystal blast is worth
    /// 10 (see <see cref="BendsScoringRuleSO"/>), so the default 60 is six clean hits.
    /// </summary>
    public class BendsPointTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "BendsPointMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetBendsPointTarget()
                : EndConditionOverridesSO.DefaultBendsPointTarget;
        }
    }
}
