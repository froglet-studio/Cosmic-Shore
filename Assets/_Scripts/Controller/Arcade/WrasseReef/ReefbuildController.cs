using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Reefbuild - the Wrasse and Grouper's MASS game (WRASSEREEF.md): a TIMED reef war in the bare Barren cell, stocked
    /// with Mass crystals. Loop Mass motes and the kite lays a cluster of your prisms where the loop closed (the more
    /// motes, the far bigger the cluster); the Grouper's own trail adds to it. Rival kites pop and blast what you built.
    /// The domain with the most volume STANDING at the whistle wins, on <see cref="ScoringMetric.VolumeRemaining"/>.
    /// </summary>
    public class ReefbuildController : WrasseReefControllerBase
    {
        protected override bool UseGolfRules => false;
        protected override bool IsTimed => true;
        protected override WrasseGrouperGuide.Plan AIPlan => WrasseGrouperGuide.Plan.Build;
        protected override GameToastSituation HintSituation => GameToastSituation.ReefbuildHint;

        protected override void ZeroCounters(IRoundStats stats)
        {
            stats.VolumeRemaining = 0f;
            stats.PrismsRemaining = 0;
        }

        protected override float MetricOf(IRoundStats stats) => stats.VolumeRemaining;

        protected override void ReadSnapshot(IRoundStats stats, out float metric, out float breakdown)
        {
            metric = stats.VolumeRemaining;
            breakdown = stats.PrismsRemaining;
        }

        protected override void WriteSnapshot(IRoundStats stats, float metric, float breakdown)
        {
            stats.VolumeRemaining = metric;
            stats.PrismsRemaining = (int)breakdown;
        }
    }
}
