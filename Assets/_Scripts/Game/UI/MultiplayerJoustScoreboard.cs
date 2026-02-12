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
            // [Visual Note] Use DomainStats calculated in Controller to set the correct banner.
            if (gameData.DomainStatsList != null && gameData.DomainStatsList.Count > 0)
            {
                SetBannerForDomain(gameData.DomainStatsList[0].Domain);
            }

            FormatMultiplayerJoustScores();
            
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        protected override void DisplayPlayerScores() 
        { 
            // Standard implementation handles naming and basic field population.
            base.DisplayPlayerScores();
        }

        void FormatMultiplayerJoustScores()
        {
            if (!joustController || !joustController.joustTurnMonitor)
            {
                Debug.LogError("[MultiplayerJoustScoreboard] JoustController or TurnMonitor is null!");
                return;
            }

            // [Visual Note] Controller handles sorting based on UseGolfRules. 
            // Do NOT sort locally to avoid UI flipping.
            var playerScores = gameData.RoundStatsList;
            int needed = joustController.joustTurnMonitor.CollisionsNeeded;

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

                if (!PlayerScoreTextFields[i]) continue;
        
                var stats = playerScores[i];
                int current = stats.JoustCollisions;
                int joustsLeft = Mathf.Max(0, needed - current);

                if (joustsLeft == 0)
                {
                    // Winner finished the jousts: Show completion time
                    TimeSpan t = TimeSpan.FromSeconds(stats.Score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
                }
                else
                {
                    // Loser has jousts remaining
                    string s = joustsLeft == 1 ? "" : "s";
                    PlayerScoreTextFields[i].text = $"{joustsLeft} Joust{s} Left";
                }
            }
    
            // Cleanup unused slots in the UI
            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i]) PlayerNameTextFields[i].text = "";
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i]) 
                    PlayerScoreTextFields[i].text = "";
            }
        }
    }
}