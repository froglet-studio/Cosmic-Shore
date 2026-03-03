// MultiplayerCrystalCaptureEndGameController.cs
using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using CosmicShore.Game.Progression;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCrystalCaptureEndGameController : EndGameCinematicController
    {
        protected override bool DetermineLocalPlayerWon()
        {
            var localName = gameData.LocalPlayer?.Name;
            return gameData.RoundStatsList.Count > 0
                && gameData.RoundStatsList[0].Name == localName;
        }

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

            CSDebug.Log($"[CrystalCapture] Local='{localName}' myScore={myScore} " +
                      $"opponentScore={opponentScore} didWin={didWin} diff={crystalDifference} " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score}"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                myScore,
                cinematic.scoreRevealSettings,
                false
            );
        }

        protected override IEnumerator ShowQuestCompletionSequence()
        {
            if (!view || !gameData) yield break;

            var service = GameModeProgressionService.Instance;
            if (service == null) yield break;

            var mode = gameData.GameMode;
            var quest = service.GetQuestForMode(mode);
            if (quest == null || quest.IsPlaceholder) yield break;

            // Use direct score check — the local player's score is readily available
            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList?.FirstOrDefault(s => s.Name == localName);
            int myScore = localStats != null ? (int)localStats.Score : 0;

            bool questMet = service.IsQuestCompleted(mode) || myScore >= quest.TargetValue;

            if (questMet)
            {
                quest.IsCompleted = true;

                // Ensure the service also knows (in case HandleGameEnd hasn't fired yet)
                if (!service.IsQuestCompleted(mode) && myScore > 0)
                    service.ReportQuestStat(mode, myScore);

                view.ShowQuestCompletion($"Quest Complete!\n{quest.DisplayName}");
                CSDebug.Log($"[CrystalCapture] Quest completed — score:{myScore} target:{quest.TargetValue}");
                yield return new WaitForSeconds(2f);
            }
        }
    }
}