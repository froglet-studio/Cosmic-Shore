using System.Collections;
using System.Linq;
using CosmicShore.Gameplay;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Utility
{
    public class HexRaceEndGameController : EndGameCinematicController
    {
        [Header("Hex Race — SOAP Data")]
        [Tooltip("Server-authoritative winner name, written by the game controller's SyncFinalScores_ClientRpc.")]
        [SerializeField] StringVariable raceWinnerName;
        [Tooltip("True once final scores have been synced to all clients.")]
        [SerializeField] BoolVariable raceResultsReady;

        protected override bool DetermineLocalPlayerWon()
        {
            var localName = gameData.LocalPlayer?.Name;
            return raceResultsReady && raceResultsReady.Value
                && raceWinnerName && raceWinnerName.Value == localName;
        }

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            var localName = gameData.LocalPlayer?.Name;
            if (string.IsNullOrEmpty(localName))
            {
                CSDebug.LogError("[HexRaceEndGame] LocalPlayer.Name is null or empty.");
                yield break;
            }

            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null)
            {
                CSDebug.LogError($"[HexRaceEndGame] Could not find RoundStats for '{localName}'. " +
                               $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                yield break;
            }

            // Single source of truth — written by the game controller's SyncFinalScores_ClientRpc
            bool didWin = raceResultsReady && raceResultsReady.Value
                && raceWinnerName && raceWinnerName.Value == localName;

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

            CSDebug.Log($"[HexRaceEndGame] Local='{localName}' Score={localStats.Score} didWin={didWin} " +
                      $"WinnerName='{(raceWinnerName ? raceWinnerName.Value : "null")}' " +
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