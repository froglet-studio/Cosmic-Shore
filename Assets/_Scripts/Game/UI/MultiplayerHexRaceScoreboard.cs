using System;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class MultiplayerHexRaceScoreboard : Scoreboard
    {
        protected override void ShowMultiplayerView()
        {
            DetermineWinnerAndSetBanner();
            DisplayPlayerScores(); // Use the standard player display
            FormatMultiplayerTimeScores();
            
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        void DetermineWinnerAndSetBanner()
        {
            // [Visual Note] Uses DomainStats calculated in the Controller sync
            if (gameData.DomainStatsList == null || gameData.DomainStatsList.Count == 0)
            {
                if (BannerText) BannerText.text = "GAME OVER";
                return;
            }

            var winnerDomain = gameData.DomainStatsList[0].Domain;
            SetBannerForDomain(winnerDomain);
        }
        
        protected override void DisplayPlayerScores() 
        {
            // Standard implementation handles naming and basic score field population
            base.DisplayPlayerScores();
        }

        void FormatMultiplayerTimeScores()
        {
            var playerScores = gameData.RoundStatsList;

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (!PlayerScoreTextFields[i]) continue;
                
                float score = playerScores[i].Score;

                if (score < 10000f)
                {
                    TimeSpan t = TimeSpan.FromSeconds(score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
                }
                else
                {
                    int crystalsLeft = (int)(score - 10000f);
                    PlayerScoreTextFields[i].text = $"{crystalsLeft} Crystals Left";
                }
            }
        }
    }
}