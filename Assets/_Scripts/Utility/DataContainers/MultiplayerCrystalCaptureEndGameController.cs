// MultiplayerCrystalCaptureEndGameController.cs
using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCrystalCaptureEndGameController : EndGameCinematicController
    {
        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) yield break;

            // Winner = index 0 after sort (UseGolfRules=false → descending, highest crystals first)
            bool didWin = gameData.RoundStatsList.Count > 0 &&
                          gameData.RoundStatsList[0].Name == localName;

            string headerText = didWin ? "VICTORY" : "DEFEAT";

            // Crystal difference between winner and loser
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

            Debug.Log($"[CrystalCapture] Local='{localName}' myScore={myScore} " +
                      $"opponentScore={opponentScore} didWin={didWin} diff={crystalDifference} " +
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