// MultiplayerJoustScoreboard.cs
using System;
using System.Linq;
using CosmicShore.Game.Arcade;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.UI
{
    public class MultiplayerJoustScoreboard : Scoreboard
    {
        [Header("References")]
        [SerializeField] private MultiplayerJoustController joustController;

        [Header("Display")]
        [Tooltip("Noun used in loser row (e.g. Joust, Hit)")]
        [SerializeField] string scoreNoun = "Joust";

        protected override void ShowMultiplayerView()
        {
            // Use the authoritative WinnerName from the joust controller
            // instead of sorting by score, which can be unreliable if
            // scores are reset before the scoreboard displays.
            if (joustController != null && joustController.ResultsReady)
            {
                var winnerStats = gameData.RoundStatsList
                    .FirstOrDefault(s => s.Name == joustController.WinnerName);
                if (winnerStats != null)
                    SetBannerForDomain(winnerStats.Domain);
                else if (BannerText) BannerText.text = "GAME OVER";
            }
            else if (gameData.RoundStatsList is { Count: > 0 })
            {
                gameData.RoundStatsList.Sort((a, b) => a.Score.CompareTo(b.Score));
                SetBannerForDomain(gameData.RoundStatsList[0].Domain);
            }
            else if (BannerText) BannerText.text = "GAME OVER";

            FormatMultiplayerJoustScores();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView)  MultiplayerView.gameObject.SetActive(true);
        }

        void FormatMultiplayerJoustScores()
        {
            if (!joustController || !joustController.joustTurnMonitor)
            {
                CSDebug.LogError("[MultiplayerJoustScoreboard] JoustController or TurnMonitor is null!");
                return;
            }

            var playerScores = gameData.RoundStatsList;
            int needed = joustController.joustTurnMonitor.CollisionsNeeded;

            // Use the authoritative WinnerName to determine who won
            string winnerName = joustController.ResultsReady ? joustController.WinnerName : "";

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

                if (!PlayerScoreTextFields[i]) continue;

                var stats = playerScores[i];
                int joustsLeft = Mathf.Max(0, needed - stats.JoustCollisions);
                bool thisPlayerWon = stats.Name == winnerName;

                if (thisPlayerWon)
                {
                    // Winner row shows finish time
                    TimeSpan t = TimeSpan.FromSeconds(stats.Score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
                }
                else
                {
                    // Loser row shows remaining count
                    string plural = joustsLeft == 1 ? "" : "s";
                    PlayerScoreTextFields[i].text = $"{joustsLeft} {scoreNoun}{plural} Left";
                }
            }

            // Clear unused slots
            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i]) PlayerNameTextFields[i].text = "";
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = "";
            }
        }
    }
}