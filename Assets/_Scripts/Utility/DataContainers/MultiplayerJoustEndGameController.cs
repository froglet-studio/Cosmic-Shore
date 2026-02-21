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

            // Single source of truth from controller — same pattern as HexRace
            bool didWin = joustController.ResultsReady &&
                          joustController.WinnerName == localName;

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

            Debug.Log($"[JoustEndGame] Local='{localName}' Jousts={myJousts}/{needed} " +
                      $"didWin={didWin} WinnerName='{joustController.WinnerName}' " +
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