using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade
{
    public class NeedleThreadEndGameController : EndGameCinematicController
    {
        [Header("Needle Thread")]
        [SerializeField] private NeedleThreadController needleThreadController;

        protected override bool DetermineLocalPlayerWon()
        {
            var localName = gameData.LocalPlayer?.Name;
            return needleThreadController != null
                && needleThreadController.RaceResultsReady
                && needleThreadController.WinnerName == localName;
        }

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var localName = gameData.LocalPlayer?.Name;
            if (string.IsNullOrEmpty(localName))
            {
                CSDebug.LogError("[NeedleThreadEndGame] LocalPlayer.Name is null or empty.");
                yield break;
            }

            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null)
            {
                CSDebug.LogError($"[NeedleThreadEndGame] Could not find RoundStats for '{localName}'. " +
                               $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                yield break;
            }

            bool didWin = needleThreadController != null
                && needleThreadController.RaceResultsReady
                && needleThreadController.WinnerName == localName;

            string headerText = didWin ? "VICTORY" : "DEFEAT";
            string label;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                label = "RACE TIME";
                displayValue = (int)localStats.Score;
                formatAsTime = true;
            }
            else
            {
                label = "ECHO HITS LEFT";
                displayValue = Mathf.Max(0, (int)(localStats.Score - 10000f));
                formatAsTime = false;
            }

            CSDebug.Log($"[NeedleThreadEndGame] Local='{localName}' Score={localStats.Score} didWin={didWin} " +
                      $"WinnerName='{needleThreadController?.WinnerName}' " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score}"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
    }
}
