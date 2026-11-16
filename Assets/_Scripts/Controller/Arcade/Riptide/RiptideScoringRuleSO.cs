using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Riptide scoring - Shatterplane's rule (Rampage's, on <see cref="CosmicShore.Data.ScoringMetric.PrismsDestroyed"/>),
    /// inherited: first DOMAIN to <see cref="CosmicShore.Utility.GameDataSO.PrismTargetCount"/> hostile prisms,
    /// golf-timed, team-major results. The score is mass taken off the board, so the card's genre reads Space through the
    /// metric (<c>ModeGenre</c>).
    ///
    /// <para>What counts is exactly what Rampage counts: every prism the pair destroys that is not its own domain's -
    /// Shatterplane's neutral plate tiles, a rival pair's islands, perches and dens. The Goby &amp; Pistol breaks mass two
    /// ways, both its own kit: a vortex ring's collapse smashes every prism it carried (Snap), and the Pistol's body
    /// breaks what it touches. The undertow (Space) pulls loose mass into the Pistol's plane, which is what puts it in
    /// reach. The pair's own trail is its own colour and never scores, so laying mass to break it again is worthless by
    /// construction - the game stays a Space game.</para>
    ///
    /// <para>Only the words are the mode's own, authored on its own asset (Kelpwood's pattern over Jackstraw's rule).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Riptide", fileName = "RiptideScoringRule")]
    public class RiptideScoringRuleSO : ShatterplaneScoringRuleSO
    {
    }
}
