using CosmicShore.Game.UI;

namespace CosmicShore.Game.Arcade
{
    public class BlockBanditScoreboard : Scoreboard
    {
        protected override void ShowMultiplayerView()
        {
            if (gameData.RoundStatsList is { Count: > 0 })
            {
                // Sort by blocks stolen descending — most blocks stolen wins
                gameData.RoundStatsList.Sort((a, b) => b.PrismStolen.CompareTo(a.PrismStolen));
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

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

                if (PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = $"{playerScores[i].PrismStolen} Blocks Stolen";
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
