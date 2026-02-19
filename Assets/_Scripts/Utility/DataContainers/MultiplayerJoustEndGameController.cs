// MultiplayerJoustEndGameController.cs
using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerJoustEndGameController : EndGameCinematicController
    {
        [Header("References")]
        [SerializeField] private MultiplayerJoustController joustController;

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            if (!joustController || !joustController.joustTurnMonitor) yield break;

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) yield break;

            int needed = joustController.joustTurnMonitor.CollisionsNeeded;
            int myJousts = localStats.JoustCollisions;

            // Winner = index 0 after ascending sort (lowest time wins, loser has 99999)
            bool didWin = gameData.RoundStatsList.Count > 0 &&
                          gameData.RoundStatsList[0].Name == localName;

            // Find opponent stats for the difference display
            var opponentStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name != localName);
            int opponentJousts = opponentStats?.JoustCollisions ?? 0;
            int joustDifference = Mathf.Abs(myJousts - opponentJousts);

            string headerText = didWin ? "VICTORY" : "DEFEAT";
            string label;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                // Winner sees their finish time and how many more jousts they had
                label = $"WON BY {joustDifference} JOUST{(joustDifference != 1 ? "S" : "")}";
                displayValue = (int)localStats.Score;
                formatAsTime = true;
            }
            else
            {
                // Loser sees how many jousts they still needed
                int joustsLeft = Mathf.Max(0, needed - myJousts);
                label = $"LOST BY {joustDifference} JOUST{(joustDifference != 1 ? "S" : "")}";
                displayValue = joustsLeft;
                formatAsTime = false;
            }

            Debug.Log($"[JoustEndGame] Local='{localName}' Jousts={myJousts} Needed={needed} " +
                      $"didWin={didWin} diff={joustDifference} Score={localStats.Score} " +
                      $"AllScores=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score}({s.JoustCollisions}j)"))}]");

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
    }
}