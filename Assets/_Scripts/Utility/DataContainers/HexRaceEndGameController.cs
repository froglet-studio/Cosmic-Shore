using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class HexRaceEndGameController : EndGameCinematicController
    {
        [Header("Hex Race")]
        [SerializeField] private HexRaceController hexRaceController;

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var localName = gameData.LocalPlayer?.Name;
            if (string.IsNullOrEmpty(localName))
            {
                Debug.LogError("[HexRaceEndGame] LocalPlayer.Name is null or empty.");
                yield break;
            }

            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null)
            {
                Debug.LogError($"[HexRaceEndGame] Could not find RoundStats for '{localName}'. " +
                               $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                yield break;
            }

            // Single source of truth — the controller received this authoritatively from the server
            bool didWin = hexRaceController != null
                && hexRaceController.RaceResultsReady
                && hexRaceController.WinnerName == localName;

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
                label = "CRYSTALS LEFT";
                displayValue = Mathf.Max(0, (int)(localStats.Score - 10000f));
                formatAsTime = false;
            }

            Debug.Log($"[HexRaceEndGame] Local='{localName}' Score={localStats.Score} didWin={didWin} " +
                      $"WinnerName='{hexRaceController?.WinnerName}' " +
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