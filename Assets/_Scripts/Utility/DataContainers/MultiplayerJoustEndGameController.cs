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

            var localName = gameData.LocalPlayer?.Name;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localName);
            if (localStats == null) yield break;

            if (!joustController || !joustController.joustTurnMonitor) yield break;

            int needed = joustController.joustTurnMonitor.CollisionsNeeded;

            // Winner = index 0 after ascending sort (lowest score = fastest time wins)
            bool didWin = gameData.RoundStatsList.Count > 0 &&
                          gameData.RoundStatsList[0].Name == localName;

            string headerText = didWin ? "VICTORY" : "DEFEAT";
            string label;
            int displayValue;
            bool formatAsTime;

            if (didWin)
            {
                label = "FINISH TIME";
                displayValue = (int)localStats.Score;
                formatAsTime = true;
            }
            else
            {
                int joustsLeft = Mathf.Max(0, needed - localStats.JoustCollisions);
                label = $"JOUST{(joustsLeft != 1 ? "S" : "")} LEFT";
                displayValue = joustsLeft;
                formatAsTime = false;
            }

            Debug.Log($"[JoustEndGame] Local='{localName}' Collisions={localStats.JoustCollisions} " +
                      $"Needed={needed} didWin={didWin} Score={localStats.Score} " +
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