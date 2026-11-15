using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cleaner Ram - the Wrasse and Grouper's CHARGE game (WRASSEREEF.md): a reef stocked with Charge crystals in the
    /// bare Barren cell. Loop Charge motes to power the kite's ram, then fly the Grouper so a rival crosses the kite's
    /// board and ram it: every ram is a Strike-class combat hit (<c>KiteNavigator.Steal</c>), one point. The first
    /// DOMAIN to the target wins (golf-timed), on <see cref="ScoringMetric.CombatPoints"/>.
    /// </summary>
    public class CleanerRamController : WrasseReefControllerBase
    {
        protected override bool UseGolfRules => true;
        protected override WrasseGrouperGuide.Plan AIPlan => WrasseGrouperGuide.Plan.Hunt;
        protected override GameToastSituation HintSituation => GameToastSituation.CleanerRamHint;

        protected override void ZeroCounters(IRoundStats stats)
        {
            stats.CombatPoints = 0;
            stats.StrikeHitsLanded = 0;
        }

        protected override float MetricOf(IRoundStats stats) => stats.CombatPoints;

        protected override void ReadSnapshot(IRoundStats stats, out float metric, out float breakdown)
        {
            metric = stats.CombatPoints;
            breakdown = stats.StrikeHitsLanded;
        }

        protected override void WriteSnapshot(IRoundStats stats, float metric, float breakdown)
        {
            stats.CombatPoints = (int)metric;
            stats.StrikeHitsLanded = (int)breakdown;
        }
    }
}
