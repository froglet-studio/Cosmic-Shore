using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerDogFightEndGameController : EndGameCinematicController
    {
        [Header("References")]
        [SerializeField] private MultiplayerDogFightController dogFightController;

        [Header("Display")]
        [Tooltip("Noun used in score display (e.g. Hit, Joust)")]
        [SerializeField] string scoreNoun = "Hit";

        protected override bool DetermineLocalPlayerWon()
        {
            var localName = gameData.LocalPlayer?.Name;
            return dogFightController != null
                && dogFightController.ResultsReady
                && dogFightController.WinnerName == localName;
        }

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            if (!dogFightController || !dogFightController.joustTurnMonitor) yield break;

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) yield break;

            int needed = dogFightController.joustTurnMonitor.CollisionsNeeded;
            int myHits = localStats.JoustCollisions;

            bool didWin = dogFightController.ResultsReady &&
                          dogFightController.WinnerName == localName;

            var opponentStats   = gameData.RoundStatsList.FirstOrDefault(s => s.Name != localName);
            int opponentHits    = opponentStats?.JoustCollisions ?? 0;
            int hitDifference   = Mathf.Abs(myHits - opponentHits);

            string nounUpper = scoreNoun.ToUpper();
            string headerText = didWin ? "VICTORY" : "DEFEAT";
            string label;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                string plural = hitDifference != 1 ? "S" : "";
                label        = $"WON BY {hitDifference} {nounUpper}{plural}";
                displayValue = Mathf.FloorToInt(localStats.Score);
                formatAsTime = true;
            }
            else
            {
                int hitsLeft = Mathf.Max(0, needed - myHits);
                string plural = hitDifference != 1 ? "S" : "";
                label        = $"LOST BY {hitDifference} {nounUpper}{plural}";
                displayValue = hitsLeft;
                formatAsTime = false;
            }

            CSDebug.Log($"[DogFightEndGame] Local='{localName}' Hits={myHits}/{needed} " +
                      $"didWin={didWin} WinnerName='{dogFightController.WinnerName}' " +
                      $"diff={hitDifference} RawScore={localStats.Score:F2} DisplayValue={displayValue} " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F2}({s.JoustCollisions}h)"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
    }
}
