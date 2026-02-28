using System;
using UnityEngine;
using System.Linq;

namespace CosmicShore.UI
{
    public class HexRaceScoreboard : Scoreboard
    {
        protected override void ShowMultiplayerView()
        {
            // Sort ascending (golf rules): winner's finish time < 10000 comes first,
            // then losers ordered by crystals left (fewer left = higher placement).
            if (gameData.RoundStatsList is { Count: > 0 })
                gameData.RoundStatsList.Sort((a, b) => a.Score.CompareTo(b.Score));

            if (gameData.DomainStatsList is { Count: > 0 })
                SetBannerForDomain(gameData.DomainStatsList[0].Domain);
            else if (gameData.RoundStatsList is { Count: > 0 })
                SetBannerForDomain(gameData.RoundStatsList[0].Domain);
            else if (BannerText) BannerText.text = "GAME OVER";

            base.DisplayPlayerScores();
            FormatMultiplayerTimeOrCrystals();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        void FormatMultiplayerTimeOrCrystals()
        {
            var playerScores = gameData.RoundStatsList;

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (!PlayerScoreTextFields[i]) continue;

                float score = playerScores[i].Score;

                if (score < 10000f)
                {
                    TimeSpan t = TimeSpan.FromSeconds(score);
                    PlayerScoreTextFields[i].text = $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
                }
                else
                {
                    int crystalsLeft = Mathf.Max(0, (int)(score - 10000f));
                    PlayerScoreTextFields[i].text = $"{crystalsLeft} Crystals Left";
                }
            }
        }
    }
}