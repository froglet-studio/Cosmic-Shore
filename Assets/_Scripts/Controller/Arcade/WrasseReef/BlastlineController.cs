using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Blastline - the Wrasse and Grouper's SPACE game (WRASSEREEF.md): Rampage's living forest, read-only, stocked with
    /// Space crystals. Loop Space motes and a blast goes off where the loop closed, destroying hostile prisms and sparing
    /// yours (the more motes, the bigger the blast); the kite's head pops what it touches between blasts. Hostile prisms
    /// destroyed score; the first DOMAIN to the target wins (golf-timed), on <see cref="ScoringMetric.PrismsDestroyed"/>.
    /// </summary>
    public class BlastlineController : WrasseReefControllerBase
    {
        protected override bool UseGolfRules => true;
        protected override WrasseGrouperGuide.Plan AIPlan => WrasseGrouperGuide.Plan.Clean;
        protected override GameToastSituation HintSituation => GameToastSituation.BlastlineHint;

        protected override void ZeroCounters(IRoundStats stats)
        {
            stats.HostilePrismsDestroyed = 0;
            stats.HostileVolumeDestroyed = 0f;
        }

        protected override float MetricOf(IRoundStats stats) => stats.HostilePrismsDestroyed;

        protected override void ReadSnapshot(IRoundStats stats, out float metric, out float breakdown)
        {
            metric = stats.HostilePrismsDestroyed;
            breakdown = stats.HostileVolumeDestroyed;
        }

        protected override void WriteSnapshot(IRoundStats stats, float metric, float breakdown)
        {
            stats.HostilePrismsDestroyed = (int)metric;
            stats.HostileVolumeDestroyed = breakdown;
        }
    }
}
