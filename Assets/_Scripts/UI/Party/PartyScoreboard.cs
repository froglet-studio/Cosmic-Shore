using System.Linq;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Final scoreboard for party mode.
    /// Shows games won per player instead of individual game scores.
    /// Displayed after all 5 rounds are complete via the normal OnShowGameEndScreen event.
    /// </summary>
    public class PartyScoreboard : Scoreboard
    {
        [Header("Party")]
        [SerializeField] PartyGameController partyController;

        protected override void ShowMultiplayerView()
        {
            if (partyController == null)
            {
                base.ShowMultiplayerView();
                return;
            }

            var winner = partyController.GetPartyWinner();
            if (winner != null)
                SetBannerForDomain(winner.Domain);
            else if (BannerText)
                BannerText.text = "PARTY OVER";

            DisplayPartyScores();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        void DisplayPartyScores()
        {
            var players = partyController.PlayerStates
                .OrderByDescending(p => p.GamesWon)
                .ToList();

            for (int i = 0; i < PlayerNameTextFields.Count; i++)
            {
                if (i < players.Count)
                {
                    if (PlayerNameTextFields[i])
                        PlayerNameTextFields[i].text = players[i].PlayerName;
                    if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                        PlayerScoreTextFields[i].text = $"{players[i].GamesWon} wins";
                }
                else
                {
                    if (PlayerNameTextFields[i])
                        PlayerNameTextFields[i].text = "";
                    if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                        PlayerScoreTextFields[i].text = "";
                }
            }
        }
    }
}
