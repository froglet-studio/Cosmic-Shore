// MultiplayerJoustScoreboard.cs
using System;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    public class MultiplayerJoustScoreboard : Scoreboard
    {
        [Header("Joust")]
        [SerializeField] private JoustCollisionTurnMonitor joustTurnMonitor;

        protected override void ShowMultiplayerView()
        {
            // Sort ascending (golf rules): winners with completion times first, losers with penalty scores last
            if (gameData.RoundStatsList is { Count: > 0 })
            {
                gameData.RoundStatsList.Sort((a, b) => a.Score.CompareTo(b.Score));
                SetBannerForDomain(gameData.RoundStatsList[0].Domain);
            }
            else if (BannerText) BannerText.text = "GAME OVER";

            FormatMultiplayerJoustScores();
            PopulateTeamScorecards();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView)  MultiplayerView.gameObject.SetActive(true);
        }

        void FormatMultiplayerJoustScores()
        {
            if (!joustTurnMonitor)
            {
                CSDebug.LogError("[MultiplayerJoustScoreboard] JoustTurnMonitor is null!");
                return;
            }

            var playerScores = gameData.RoundStatsList;
            int needed = joustTurnMonitor.CollisionsNeeded;

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

                if (!PlayerScoreTextFields[i]) continue;

                var stats = playerScores[i];
                int joustsLeft = Mathf.Max(0, needed - stats.JoustCollisions);
                bool thisPlayerWon = joustsLeft == 0;

                if (thisPlayerWon)
                {
                    TimeSpan t = TimeSpan.FromSeconds(stats.Score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
                }
                else
                {
                    string plural = joustsLeft == 1 ? "" : "s";
                    PlayerScoreTextFields[i].text = $"{joustsLeft} Joust{plural} Left";
                }
            }

            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i]) PlayerNameTextFields[i].text = "";
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = "";
            }
        }
    }
}
