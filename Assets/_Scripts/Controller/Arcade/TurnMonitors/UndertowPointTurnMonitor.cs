using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Undertow. All the machinery is <see cref="CombatPointTurnMonitorBase"/>'s
    /// (the third mode on it, after Dog Fight and The Bends); the one thing this mode owns is
    /// WHICH target it races to. Note the base ends the turn through the mode's OWN rule, and
    /// <see cref="UndertowScoringRuleSO"/> folds creature kills into the domain score there - so
    /// the "point target" this resolves is against bends PLUS kills, exactly as the goal row
    /// shows it.
    /// </summary>
    public class UndertowPointTurnMonitor : CombatPointTurnMonitorBase
    {
        protected override string LogTag => "UndertowPointMonitor";

        protected override int ResolvePointTarget()
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetUndertowPointTarget()
                : EndConditionOverridesSO.DefaultUndertowPointTarget;
        }
    }
}
