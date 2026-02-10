using System;
using System.Linq;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Scoreboard for multiplayer joust mode.
    /// Winner shows race time, losers show jousts remaining.
    /// </summary>
    public class MultiplayerJoustScoreboard : Scoreboard
    {
        protected override void ShowMultiplayerView()
        {
            DetermineWinnerAndSetBanner();
            FormatMultiplayerJoustScores();
            
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        void DetermineWinnerAndSetBanner()
        {
            var playerScores = gameData.RoundStatsList;
            if (playerScores == null || playerScores.Count == 0) return;

            // Winner has the lowest score (race time)
            var sortedPlayers = playerScores.OrderBy(p => p.Score).ToList();
            var winner = sortedPlayers[0];

            SetBannerForDomain(winner.Domain);
        }
        
        protected override void DisplayPlayerScores() { }

        void FormatMultiplayerJoustScores()
        {
            var playerScores = gameData.RoundStatsList;

            // Sort by score (lowest = winner with time, highest = most jousts left)
            playerScores.Sort((a, b) => a.Score.CompareTo(b.Score));

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

                if (!PlayerScoreTextFields[i]) continue;
                
                float score = playerScores[i].Score;

                if (score < 10000f)
                {
                    // Winner: Display race time
                    TimeSpan t = TimeSpan.FromSeconds(score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
                }
                else
                {
                    // Loser: Display jousts remaining
                    int joustsLeft = (int)(score - 10000f);
                    PlayerScoreTextFields[i].text = joustsLeft == 1 
                        ? "1 Joust Left" 
                        : $"{joustsLeft} Jousts Left";
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