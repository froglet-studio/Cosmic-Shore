using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Den Builder scoring - a TIMED round, highest wins, on <see cref="CosmicShore.Data.ScoringMetric.VolumeRemaining"/>:
    /// the mass a domain has STANDING in its own colour when the clock runs out. Everything the Goby &amp; Pistol lays is
    /// the pair's (<c>BoostRingBuilder.LayOne</c> with the pilot's name), so <c>StatsManager</c> counts it as it counts
    /// every trail: its dens (the Mass ability, built from the energy the Pistol earned jumping the trail), its perches
    /// and its islands' ground. A rival ring that smashes it, or a grazer that eats it, takes it off.
    ///
    /// <para><b>Glasshouse's rule, reused.</b> <see cref="GlasshouseScoringRuleSO"/> already scores exactly this stock
    /// (timed, never ends on a target, points-mode reveal, every line an authored template); its breakdown column is fed
    /// by the controller (<see cref="DenBuilderController"/>: each pilot's standing PRISM count from the whistle's
    /// snapshot), so Den Builder inherits it and authors its own words on its own asset.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Den Builder", fileName = "DenBuilderScoringRule")]
    public class DenBuilderScoringRuleSO : GlasshouseScoringRuleSO
    {
    }
}
