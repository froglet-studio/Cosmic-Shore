using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skein: a golf-timed race through an ORDERED course of rings threaded along a knot cable.
    ///
    /// <para><b>It inherits the gate-race rule wholesale, and that is the point.</b> Both modes are
    /// the same shape of race - every pilot flies the SAME ordered course, so a domain's progress
    /// is its LEAD RUNNER's rather than the sum of its pilots' (<c>DomainValue</c> ->
    /// <see cref="ScoringMetrics.BestByDomain"/>), and a trailing teammate's own remainder is what
    /// their goal row must show (<c>RemainingForPlayer</c>). Re-deriving either here would be two
    /// implementations of one rule that must never disagree.</para>
    ///
    /// <para>What differs is only the WORDING. The TARGET is deliberately not re-overridden:
    /// both modes read <c>GameDataSO.SwitchTargetCount</c>, which each mode's own turn monitor
    /// publishes from its own <c>EndConditionOverridesSO</c> key - so the two are independently
    /// authorable without a second override here that would be byte-identical to the one it
    /// shadows.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Skein", fileName = "SkeinScoringRule")]
    public class SkeinScoringRuleSO : GateRaceScoringRuleSO
    {
        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "CABLE TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "RINGS LEFT", RemainingForPlayer(gameData, localStats), false);
    }
}
