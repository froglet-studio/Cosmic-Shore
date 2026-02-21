using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// EndGame cinematic controller for multiplayer co-op Wildlife Blitz.
    /// Reads authoritative results from MultiplayerWildlifeBlitzMiniGame.
    /// Follows the same pattern as MultiplayerJoustEndGameController / HexRaceEndGameController.
    /// </summary>
    public class MultiplayerWildlifeBlitzEndGameController : EndGameCinematicController
    {
        [Header("Wildlife Blitz References")]
        [SerializeField] private MultiplayerWildlifeBlitzMiniGame blitzController;

        protected override bool DetermineLocalPlayerWon()
        {
            return blitzController != null
                && blitzController.ResultsReady
                && blitzController.DidCoOpWin;
        }

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            if (!blitzController || !blitzController.ResultsReady) yield break;

            bool didWin = blitzController.DidCoOpWin;
            float finishTime = blitzController.FinishTime;

            // Build per-player kill summary
            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            int localKills = localStats?.BlocksDestroyed ?? 0;
            int totalKills = gameData.RoundStatsList.Sum(s => s.BlocksDestroyed);

            string headerText = didWin ? "VICTORY" : "DEFEAT";
            string label;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                label = $"CO-OP CLEAR — {totalKills} KILLS";
                displayValue = Mathf.FloorToInt(finishTime);
                formatAsTime = true;
            }
            else
            {
                label = $"SURVIVED — {totalKills} KILLS";
                displayValue = totalKills;
                formatAsTime = false;
            }

            Debug.Log($"[WildlifeBlitzEndGame] Local='{localName}' Kills={localKills}/{totalKills} " +
                      $"didWin={didWin} Time={finishTime:F2} " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F2}({s.BlocksDestroyed}k)"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
    }
}
