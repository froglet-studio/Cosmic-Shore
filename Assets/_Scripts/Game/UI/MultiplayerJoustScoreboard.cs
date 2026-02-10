using System;
using System.Linq;
using CosmicShore.Game.Arcade;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class MultiplayerJoustScoreboard : Scoreboard
    {
        [Header("References")]
        [SerializeField] private MultiplayerJoustController joustController;

        protected override void ShowMultiplayerView()
        {
            // Standard Sort: Lowest Score (Time) is Top. Losers (99999) are Bottom.
            gameData.RoundStatsList.Sort((a, b) => a.Score.CompareTo(b.Score));
            
            var winner = gameData.RoundStatsList[0];
            SetBannerForDomain(winner.Domain);
            
            FormatMultiplayerJoustScores();
            
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        protected override void DisplayPlayerScores() { }

        void FormatMultiplayerJoustScores()
        {
            var playerScores = gameData.RoundStatsList;
            int needed = joustController.joustTurnMonitor.CollisionsNeeded;

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

                if (!PlayerScoreTextFields[i]) continue;
        
                var stats = playerScores[i];
        
                // Calculate jousts left
                int current = stats.JoustCollisions;
                int joustsLeft = Mathf.Max(0, needed - current);
                
                // If no jousts left, player won - show time
                // If jousts left > 0, player lost - show jousts left
                if (joustsLeft == 0)
                {
                    // Winner: Show Time
                    TimeSpan t = TimeSpan.FromSeconds(stats.Score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
                }
                else
                {
                    // Loser: Show Jousts Left
                    string s = joustsLeft == 1 ? "" : "s";
                    PlayerScoreTextFields[i].text = $"{joustsLeft} Joust{s} Left";
                }
            }
    
            // Cleanup
            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i]) PlayerNameTextFields[i].text = "";
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i]) 
                    PlayerScoreTextFields[i].text = "";
            }
        }
    }
}