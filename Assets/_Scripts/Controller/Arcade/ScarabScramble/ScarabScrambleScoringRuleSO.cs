using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Scarab Scramble scoring: identical mechanics to Astro League's rule — points (not
    /// golf), per-player GoalsScored aggregated by domain, first domain to
    /// <see cref="GameDataSO.GoalTargetCount"/> wins, Score = personal goals — so it
    /// deliberately subclasses rather than re-implements. It exists as its own type so the
    /// mode owns its asset (per-mode metric/points fields can never be shared tuning with
    /// Astro League) and so any future Scramble-specific reveal wording has a home.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/ScarabScramble", fileName = "ScarabScrambleScoringRule")]
    public class ScarabScrambleScoringRuleSO : AstroLeagueScoringRuleSO
    {
    }
}
