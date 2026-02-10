using System;
using System.Linq;

namespace CosmicShore.Game.UI
{
    public class MultiplayerHexRaceScoreboard : Scoreboard
    {
        protected override void ShowMultiplayerView()
        {
            DetermineWinnerAndSetBanner();
            FormatMultiplayerTimeScores();
            
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        void DetermineWinnerAndSetBanner()
        {
            var playerScores = gameData.RoundStatsList;
            if (playerScores == null || playerScores.Count == 0) return;

            var sortedPlayers = playerScores.OrderBy(p => p.Score).ToList();
            var winner = sortedPlayers[0];

            var localPlayerName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            SetBannerForDomain(winner.Domain);
        }
        
        protected override void DisplayPlayerScores() { }

        void FormatMultiplayerTimeScores()
        {
            var playerScores = gameData.RoundStatsList;

            playerScores.Sort((a, b) => a.Score.CompareTo(b.Score));

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

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
                    PlayerScoreTextFields[i].text = $"{crystalsLeft} Left";
                }
            }
            
            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i]) PlayerNameTextFields[i].text = "";
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i]) PlayerScoreTextFields[i].text = "";
            }
        }
    }
}