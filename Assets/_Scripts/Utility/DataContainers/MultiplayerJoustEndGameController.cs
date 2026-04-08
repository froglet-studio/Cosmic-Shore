// MultiplayerJoustEndGameController.cs
using System.Collections;
using System.Linq;
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
            var localName = gameData.LocalPlayer?.Name;
            return !string.IsNullOrEmpty(gameData.WinnerName)
                && gameData.WinnerName == localName;
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

            bool didWin = !string.IsNullOrEmpty(gameData.WinnerName)
                && gameData.WinnerName == localName;

            var opponentStats   = gameData.RoundStatsList.FirstOrDefault(s => s.Name != localName);
            int opponentJousts  = opponentStats?.JoustCollisions ?? 0;
            int joustDifference = Mathf.Abs(myJousts - opponentJousts);

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

            CSDebug.Log($"[JoustEndGame] Local='{localName}' Jousts={myJousts}/{needed} " +
                      $"didWin={didWin} WinnerName='{gameData.WinnerName}' " +
                      $"diff={joustDifference} RawScore={localStats.Score:F2} DisplayValue={displayValue} " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F2}({s.JoustCollisions}j)"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
    }
}
