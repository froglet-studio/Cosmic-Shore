using CosmicShore.Game.UI;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCrystalCaptureScoreboard : Scoreboard
    {
        protected override void ShowMultiplayerView()
        {
            if (gameData.RoundStatsList is { Count: > 0 })
            {
                gameData.RoundStatsList.Sort((a, b) => b.Score.CompareTo(a.Score));
                SetBannerForDomain(gameData.RoundStatsList[0].Domain);
            }
            else if (BannerText) BannerText.text = "GAME OVER";

            DisplayPlayerScores();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        protected override void DisplayPlayerScores()
        {
            var playerScores = gameData.RoundStatsList;

            // [Visual Note] Loop generates a row per player in the scoreboard canvas. Crystal count is appended with a text suffix for context.
            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

                if (PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = $"{(int)playerScores[i].Score} Crystals";
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