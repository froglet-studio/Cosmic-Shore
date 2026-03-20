using System;
using System.Linq;
using CosmicShore.Game.Arcade;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.UI
{
    public class NeedleThreadScoreboard : Scoreboard
    {
        [Header("References")]
        [SerializeField] private NeedleThreadController needleThreadController;

        protected override void ShowMultiplayerView()
        {
            if (needleThreadController != null && needleThreadController.RaceResultsReady)
            {
                var winnerStats = gameData.RoundStatsList
                    .FirstOrDefault(s => s.Name == needleThreadController.WinnerName);
                if (winnerStats != null)
                    SetBannerForDomain(winnerStats.Domain);
                else if (BannerText) BannerText.text = "GAME OVER";
            }
            else if (gameData.RoundStatsList is { Count: > 0 })
            {
                gameData.RoundStatsList.Sort((a, b) => a.Score.CompareTo(b.Score));
                SetBannerForDomain(gameData.RoundStatsList[0].Domain);
            }
            else if (BannerText) BannerText.text = "GAME OVER";

            FormatNeedleThreadScores();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView)  MultiplayerView.gameObject.SetActive(true);
        }

        void FormatNeedleThreadScores()
        {
            var playerScores = gameData.RoundStatsList;

            string winnerName = needleThreadController != null && needleThreadController.RaceResultsReady
                ? needleThreadController.WinnerName
                : "";

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

                if (!PlayerScoreTextFields[i]) continue;

                var stats = playerScores[i];
                bool thisPlayerWon = stats.Name == winnerName;

                if (thisPlayerWon)
                {
                    TimeSpan t = TimeSpan.FromSeconds(stats.Score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
                }
                else
                {
                    int echoHitsLeft = Mathf.Max(0, (int)(stats.Score - 10000f));
                    string plural = echoHitsLeft == 1 ? "" : "s";
                    PlayerScoreTextFields[i].text = $"{echoHitsLeft} Echo Hit{plural} Left";
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
