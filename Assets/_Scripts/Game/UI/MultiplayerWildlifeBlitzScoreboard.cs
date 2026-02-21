using System;
using CosmicShore.Game.Arcade;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Unified scoreboard for Wildlife Blitz (solo and co-op multiplayer).
    /// In co-op all players share the same team result (win/loss).
    /// Shows each player's name and individual kill contribution.
    /// In solo mode falls back to the base single-player view.
    /// </summary>
    public class WildlifeBlitzScoreboard : Scoreboard
    {
        [Header("Wildlife Blitz References")]
        [SerializeField] private WildlifeBlitzController blitzController;

        protected override void ShowMultiplayerView()
        {
            if (blitzController != null && blitzController.ResultsReady)
            {
                bool didWin = blitzController.DidWin;
                if (BannerText)
                    BannerText.text = didWin ? "VICTORY — ALL CLEAR" : "DEFEAT — TIME'S UP";

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
                    TimeSpan t = TimeSpan.FromSeconds(score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2} — {kills} Kills";
                }
                else
                {
                    PlayerScoreTextFields[i].text = $"{kills} Kills";
                }
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
