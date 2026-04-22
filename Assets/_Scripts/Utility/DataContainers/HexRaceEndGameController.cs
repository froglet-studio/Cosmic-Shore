using System.Collections;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Utility
{
    public class HexRaceEndGameController : EndGameCinematicController
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

            bool didWin = DetermineLocalPlayerWon();

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

            CSDebug.Log($"[HexRaceEndGame] Local='{localName}' Domain={localStats.Domain} Score={localStats.Score} didWin={didWin} " +
                      $"WinnerName='{gameData.WinnerName}' WinnerDomain={gameData.WinnerDomain} " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}({s.Domain}):{s.Score}"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
    }
}
