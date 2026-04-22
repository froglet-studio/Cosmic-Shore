using System;
using System.Linq;
using CosmicShore.Game.Arcade;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class DogFightScoreboard : Scoreboard
    {
        [Header("References")]
        [SerializeField] private DogFightController dogFightController;

        protected override void ShowMultiplayerView()
        {
            if (dogFightController != null && dogFightController.ResultsReady)
            {
                var winnerStats = gameData.RoundStatsList
                    .FirstOrDefault(s => s.Name == dogFightController.WinnerName);
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

            FormatDogFightScores();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView)  MultiplayerView.gameObject.SetActive(true);
        }

        void FormatDogFightScores()
        {
            if (!dogFightController || !dogFightController.dogFightTurnMonitor) return;

            var playerScores = gameData.RoundStatsList;
            int needed = dogFightController.dogFightTurnMonitor.HitsNeeded;

            string winnerName = dogFightController.ResultsReady ? dogFightController.WinnerName : "";

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;

                if (!PlayerScoreTextFields[i]) continue;

                var stats = playerScores[i];
                int hitsLeft = Mathf.Max(0, needed - stats.DogFightHits);
                bool thisPlayerWon = stats.Name == winnerName;

                if (thisPlayerWon)
                {
                    TimeSpan t = TimeSpan.FromSeconds(stats.Score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
                }
                else
                {
                    string plural = hitsLeft == 1 ? "" : "s";
                    PlayerScoreTextFields[i].text = $"{hitsLeft} Hit{plural} Left";
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
