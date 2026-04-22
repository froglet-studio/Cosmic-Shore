// MultiplayerCrystalCaptureEndGameController.cs
using System.Collections;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class MultiplayerCrystalCaptureEndGameController : EndGameCinematicController
    {
        protected override bool DetermineLocalPlayerWon()
        {
            var localDomain = gameData.LocalPlayer?.Domain ?? Domains.Unassigned;
            return gameData.WinnerDomain != Domains.Unassigned
                && gameData.WinnerDomain != Domains.None
                && localDomain == gameData.WinnerDomain;
        }

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) yield break;

            bool didWin = DetermineLocalPlayerWon();

            string headerText = didWin ? "VICTORY" : "DEFEAT";

            // Local player's own crystal total — the reveal animation always shows
            // the individual player's contribution, not the team aggregate.
            int myScore = (int)localStats.Score;

            // Team-aware delta: compare winning team's aggregate crystals to the
            // best-opposing team's aggregate. All winning-team members see the same
            // "WON BY N CRYSTALS" figure; losing-team members see "LOST BY N CRYSTALS".
            int winningTeamTotal = gameData.RoundStatsList
                .Where(s => s.Domain == gameData.WinnerDomain)
                .Sum(s => (int)s.Score);
            int bestLosingTeamTotal = gameData.RoundStatsList
                .Where(s => s.Domain != gameData.WinnerDomain)
                .GroupBy(s => s.Domain)
                .Select(g => g.Sum(s => (int)s.Score))
                .DefaultIfEmpty(0)
                .Max();

            int crystalDifference = Mathf.Abs(winningTeamTotal - bestLosingTeamTotal);

            string label = didWin
                ? $"WON BY {crystalDifference} CRYSTAL{(crystalDifference != 1 ? "S" : "")}"
                : $"LOST BY {crystalDifference} CRYSTAL{(crystalDifference != 1 ? "S" : "")}";

            CSDebug.Log($"[CrystalCapture] Local='{localName}' Domain={localStats.Domain} myScore={myScore} " +
                      $"didWin={didWin} diff={crystalDifference} winTeamTotal={winningTeamTotal} bestLossTotal={bestLosingTeamTotal} " +
                      $"WinnerName='{gameData.WinnerName}' WinnerDomain={gameData.WinnerDomain} " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}({s.Domain}):{s.Score}"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                myScore,
                cinematic.scoreRevealSettings,
                false
            );
        }
    }
}
