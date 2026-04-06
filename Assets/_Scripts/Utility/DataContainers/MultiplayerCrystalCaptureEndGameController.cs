// MultiplayerCrystalCaptureEndGameController.cs
using System.Collections;
using System.Linq;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class MultiplayerCrystalCaptureEndGameController : EndGameCinematicController
    {
        protected override bool DetermineLocalPlayerWon()
        {
            var localName = gameData.LocalPlayer?.Name;
            return !string.IsNullOrEmpty(gameData.WinnerName)
                && gameData.WinnerName == localName;
        }

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) yield break;

            bool didWin = !string.IsNullOrEmpty(gameData.WinnerName)
                && gameData.WinnerName == localName;

            string headerText = didWin ? "VICTORY" : "DEFEAT";

            // Crystal difference between local player and opponent
            int myScore = (int)localStats.Score;
            int opponentScore = gameData.RoundStatsList
                .Where(s => s.Name != localName)
                .Select(s => (int)s.Score)
                .DefaultIfEmpty(0)
                .Max();

            int crystalDifference = Mathf.Abs(myScore - opponentScore);

            string label = didWin
                ? $"WON BY {crystalDifference} CRYSTAL{(crystalDifference != 1 ? "S" : "")}"
                : $"LOST BY {crystalDifference} CRYSTAL{(crystalDifference != 1 ? "S" : "")}";

            CSDebug.Log($"[CrystalCapture] Local='{localName}' myScore={myScore} " +
                      $"opponentScore={opponentScore} didWin={didWin} diff={crystalDifference} " +
                      $"WinnerName='{gameData.WinnerName}' " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score}"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                myScore,
                cinematic.scoreRevealSettings,
                false
            );
        }
    }
}
