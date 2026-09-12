using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tollway scoring: mechanically identical to Astro League's and Scramble's rule — points
    /// (not golf), per-player <c>GoalsScored</c> aggregated by domain, first domain to
    /// <see cref="GameDataSO.GoalTargetCount"/> wins, Score = personal tolls — so it subclasses
    /// rather than re-implements.
    ///
    /// <para>What differs is only what a "goal" IS. In Astro League and Scramble a goal is a ball
    /// through a fixed ring the arena owns. Here it is a ball — <b>anyone's</b> ball — through a
    /// ring a PILOT planted, which pays that pilot's domain and consumes the ring. The metric is
    /// the same because the shape of the race is the same, and reusing it is what gives the mode
    /// its top-bar goal row, its comeback source and its scoreboard ordering for free.</para>
    ///
    /// It exists as its own type so the mode owns its asset (per-mode metric/points fields can
    /// never become shared tuning with Astro League) and so any Tollway-specific reveal wording
    /// has a home.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Tollway", fileName = "TollwayScoringRule")]
    public class TollwayScoringRuleSO : AstroLeagueScoringRuleSO
    {
    }
}
