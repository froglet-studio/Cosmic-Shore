using System;
using System.Linq;
using CosmicShore.Game.Arcade;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Scoreboard for multiplayer co-op Wildlife Blitz.
    /// All players share the same team result (win/loss).
    /// Shows each player's name and individual kill contribution.
    /// </summary>
    public class MultiplayerWildlifeBlitzScoreboard : Scoreboard
    {
        [Header("Wildlife Blitz References")]
        [SerializeField] private MultiplayerWildlifeBlitzMiniGame blitzController;

        protected override void ShowMultiplayerView()
        {
            if (blitzController != null && blitzController.ResultsReady)
            {
                bool didWin = blitzController.DidCoOpWin;
                if (BannerText)
                    BannerText.text = didWin ? "VICTORY — CO-OP CLEAR" : "DEFEAT — TIME'S UP";

                // For co-op, all players are Jade, so use Jade banner
                SetBannerForDomain(Domains.Jade);
            }
            else
            {
                if (BannerText) BannerText.text = "GAME OVER";
            }

            FormatCoOpScores();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        void FormatCoOpScores()
        {
            var playerScores = gameData.RoundStatsList;

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields.Count > i && PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

                if (!PlayerScoreTextFields[i]) continue;

                float score = playerScores[i].Score;
                int kills = playerScores[i].BlocksDestroyed;

                if (score < 999f)
                {
                    // Won: show finish time + kills
                    TimeSpan t = TimeSpan.FromSeconds(score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2} — {kills} Kills";
                }
                else
                {
                    // Lost: show kill count only
                    PlayerScoreTextFields[i].text = $"{kills} Kills";
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
