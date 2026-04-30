// MultiplayerJoustEndGameController.cs
using System.Collections;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Utility
{
    public class MultiplayerJoustEndGameController : EndGameCinematicController
    {
        [Header("Joust")]
        [SerializeField] private JoustCollisionTurnMonitor joustTurnMonitor;

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

            if (!joustTurnMonitor) yield break;

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) yield break;

            int needed = joustTurnMonitor.CollisionsNeeded;
            int myJousts = localStats.JoustCollisions;

            bool didWin = DetermineLocalPlayerWon();

            // "Best" jousts on each side for the delta label. In team games we compare
            // the winner's finish count to the best non-winning-team player.
            var winnerStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == gameData.WinnerName);
            int winnerJousts = winnerStats?.JoustCollisions ?? 0;
            int bestLosingJousts = gameData.RoundStatsList
                .Where(s => s.Domain != gameData.WinnerDomain)
                .Select(s => s.JoustCollisions)
                .DefaultIfEmpty(0)
                .Max();
            int joustDifference = Mathf.Abs(winnerJousts - bestLosingJousts);

            string headerText = didWin ? "VICTORY" : "DEFEAT";
            string label;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                label        = $"WON BY {joustDifference} JOUST{(joustDifference != 1 ? "S" : "")}";
                displayValue = Mathf.FloorToInt(localStats.Score); // seconds → int, same as HexRace
                formatAsTime = true;
            }
            else
            {
                int joustsLeft = Mathf.Max(0, needed - myJousts);
                label        = $"LOST BY {joustDifference} JOUST{(joustDifference != 1 ? "S" : "")}";
                displayValue = joustsLeft;
                formatAsTime = false;
            }

            CSDebug.Log($"[JoustEndGame] Local='{localName}' Domain={localStats.Domain} Jousts={myJousts}/{needed} " +
                      $"didWin={didWin} WinnerName='{gameData.WinnerName}' WinnerDomain={gameData.WinnerDomain} " +
                      $"diff={joustDifference} RawScore={localStats.Score:F2} DisplayValue={displayValue} " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}({s.Domain}):{s.Score:F2}({s.JoustCollisions}j)"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
    }
}
