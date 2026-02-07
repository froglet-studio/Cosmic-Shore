namespace CosmicShore.Game.UI
{
    public class MultiplayerHexRaceScoreboard : Scoreboard
    {
        protected override void ShowMultiplayerView()
        {
            base.ShowMultiplayerView();
            
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);

            FormatMultiplayerTimeScores();
        }

        void FormatMultiplayerTimeScores()
        {
            var playerScores = gameData.RoundStatsList;
            playerScores.Sort((a, b) => a.Score.CompareTo(b.Score));

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = FormatScore(playerScores[i].Score);
                    
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;
            }
        }

        string FormatScore(float score)
        {
            // FIX: If score < 10000, player FINISHED - show their completion time
            if (score < 10000f)
            {
                var t = System.TimeSpan.FromSeconds(score);
                return $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
            }
            
            // If score >= 10000, player DIDN'T finish - show crystals remaining
            return (score - 10000f).ToString("0") + " Left";
        }
    }
}