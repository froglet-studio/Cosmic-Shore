using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using UnityEngine;

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
            PopulateTeamScorecards();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        /// <summary>
        /// Override team scorecard population with HexRace-specific time/crystal formatting.
        /// </summary>
        protected override void PopulateTeamScorecards()
        {
            // Use the race-specific FormatScore for team scorecards
            base.PopulateTeamScorecards();
        }

        void FormatMultiplayerTimeOrCrystals()
        {
            var playerScores = gameData.RoundStatsList;

            for (var i = 0; i < playerScores.Count && i < PlayerScoreTextFields.Count; i++)
            {
                if (!PlayerScoreTextFields[i]) continue;
                PlayerScoreTextFields[i].text = FormatRaceScore(playerScores[i].Score);
            }
        }

        static string FormatRaceScore(float score)
        {
            if (score < 10000f)
            {
                TimeSpan t = TimeSpan.FromSeconds(score);
                return $"{t.Minutes:D2}:{t.Seconds:D2}:{t.Milliseconds / 10:D2}";
            }

            int crystalsLeft = Mathf.Max(0, (int)(score - 10000f));
            return $"{crystalsLeft} Crystals Left";
        }
    }
}
