using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Woodpecker scoring - Rampage's rule on <see cref="ScoringMetric.PrismsDestroyed"/> (first DOMAIN to
    /// <see cref="CosmicShore.Utility.GameDataSO.PrismTargetCount"/> hostile prisms, golf-timed, team-major results),
    /// through <see cref="ShatterplaneScoringRuleSO"/>, whose results and reveal lines are authored fields: Woodpecker
    /// writes its own words on its own asset and adds nothing else.
    ///
    /// <para>What counts is exactly what Rampage counts: every prism a Honeyguide breaks that is not its own domain's -
    /// a Peck round's chip, the Space upgrades on its trigger (Genesis Lance, the grenade, Fugacity, the drone, the
    /// jets), a hive cell the Ratel's chain breaks. The Honeyguide's own trail is its own colour and never scores.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Woodpecker", fileName = "WoodpeckerScoringRule")]
    public class WoodpeckerScoringRuleSO : ShatterplaneScoringRuleSO
    {
    }
}
