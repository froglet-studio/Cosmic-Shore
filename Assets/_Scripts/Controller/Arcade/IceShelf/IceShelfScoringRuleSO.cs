using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Ice Shelf scoring - a TIMED round, highest wins, on <see cref="ScoringMetric.VolumeRemaining"/>: the mass a domain
    /// has STANDING in its own colour when the clock runs out, as <see cref="IceShelfCensus"/> measures it. Glasshouse's
    /// rule reused (timed, every line an authored template, ties to the Jade -> Ruby -> Gold enum order), with Ice
    /// Shelf's own words on its own asset - Kelpwood's pattern. Its second column, the "garden" one in Glasshouse, is the
    /// part of a pilot's volume still in their shield, carried or dropped.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Ice Shelf", fileName = "IceShelfScoringRule")]
    public class IceShelfScoringRuleSO : GlasshouseScoringRuleSO
    {
    }
}
