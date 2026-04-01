using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class DogFightEndGameController : EndGameCinematicController
    {
        [Header("References")]
        [SerializeField] private DogFightController dogFightController;

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

            if (!dogFightController || !dogFightController.dogFightTurnMonitor) yield break;

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) yield break;

            int needed = dogFightController.dogFightTurnMonitor.HitsNeeded;
            int myHits = localStats.DogFightHits;

            bool didWin = dogFightController.ResultsReady &&
                          dogFightController.WinnerName == localName;

            var opponentStats  = gameData.RoundStatsList.FirstOrDefault(s => s.Name != localName);
            int opponentHits   = opponentStats?.DogFightHits ?? 0;
            int hitDifference  = Mathf.Abs(myHits - opponentHits);

            string headerText = didWin ? "VICTORY" : "DEFEAT";
            string label;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                label        = $"WON BY {hitDifference} HIT{(hitDifference != 1 ? "S" : "")}";
                displayValue = Mathf.FloorToInt(localStats.Score);
                formatAsTime = true;
            }
            else
            {
                int hitsLeft = Mathf.Max(0, needed - myHits);
                label        = $"LOST BY {hitDifference} HIT{(hitDifference != 1 ? "S" : "")}";
                displayValue = hitsLeft;
                formatAsTime = false;
            }

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
    }
}
