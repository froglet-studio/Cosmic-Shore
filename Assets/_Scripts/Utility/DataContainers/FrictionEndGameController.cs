// FrictionEndGameController.cs
using System.Collections;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Friction's end-of-run screen. Unlike HexRace (which always has a finisher),
    /// Friction has three outcomes: someone reached the crystal target, the clock ran
    /// out, or the hunters eliminated the whole party. The last two have no winner at
    /// all — <see cref="FrictionController"/> reports them as
    /// <see cref="Domains.Blue"/> — so this controller headlines them separately
    /// instead of crowning whoever happened to rank first.
    /// </summary>
    public class FrictionEndGameController : EndGameCinematicController
    {
        [Header("Friction")]
        [Tooltip("Scene FrictionController — supplies the replicated run outcome. " +
                 "Optional: when unwired, the outcome is inferred from WinnerDomain.")]
        [SerializeField] FrictionController frictionController;

        protected override bool DetermineLocalPlayerWon()
        {
            var localDomain = gameData.LocalPlayer?.Domain ?? Domains.Blue;
            return gameData.WinnerDomain != Domains.Blue
                && localDomain == gameData.WinnerDomain;
        }

        /// <summary>
        /// Reads the replicated outcome off the controller. Falls back to WinnerDomain
        /// when no controller is wired: Blue means nobody reached the target, and the
        /// clock is the more common of the two no-winner causes.
        /// </summary>
        FrictionOutcome ResolveOutcome()
        {
            if (frictionController) return frictionController.Outcome;

            return gameData.WinnerDomain != Domains.Blue
                ? FrictionOutcome.TargetReached
                : FrictionOutcome.TimeExpired;
        }

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view) yield break;

            // Deliberately does NOT bail on a null cinematic — the panel must come up so
            // the Continue button is reachable and the run can hand off to the scoreboard.
            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var localName = gameData.LocalPlayer?.Name;
            if (string.IsNullOrEmpty(localName))
            {
                CSDebug.LogError("[FrictionEndGame] LocalPlayer.Name is null or empty.");
                yield break;
            }

            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null)
            {
                CSDebug.LogError($"[FrictionEndGame] Could not find RoundStats for '{localName}'. " +
                               $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                yield break;
            }

            var outcome = ResolveOutcome();
            bool didWin = outcome == FrictionOutcome.TargetReached && DetermineLocalPlayerWon();

            string headerText;
            string label;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                headerText   = "VICTORY";
                label        = "RUN TIME";
                displayValue = Mathf.Max(0, (int)localStats.Score);
                formatAsTime = true;
            }
            else
            {
                headerText = outcome switch
                {
                    FrictionOutcome.TargetReached       => "DEFEAT",
                    FrictionOutcome.AllHumansEliminated => "ELIMINATED",
                    _                                   => "TIME'S UP",
                };
                label        = "CRYSTALS LEFT";
                displayValue = ResolveCrystalsLeft(localStats);
                formatAsTime = false;
            }

            CSDebug.Log($"[FrictionEndGame] Local='{localName}' Domain={localStats.Domain} Score={localStats.Score} " +
                      $"outcome={outcome} didWin={didWin} WinnerName='{gameData.WinnerName}' " +
                      $"WinnerDomain={gameData.WinnerDomain} Target={gameData.CrystalTargetCount} " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}({s.Domain}):{s.Score}"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic ? cinematic.scoreRevealSettings : new ScoreRevealAnimationSettings(),
                formatAsTime
            );
        }

        /// <summary>
        /// Crystals still owed against the run's target. Derived from the live crystal
        /// count rather than the 10000+N loser-score encoding, so it stays correct even
        /// if the scoring formula changes.
        /// </summary>
        int ResolveCrystalsLeft(IRoundStats stats)
        {
            int target = gameData.CrystalTargetCount;
            if (target > 0)
                return Mathf.Max(0, target - stats.CrystalsCollected);

            return Mathf.Max(0, (int)(stats.Score - 10000f));
        }
    }
}
