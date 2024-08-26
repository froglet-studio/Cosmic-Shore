using CosmicShore.Game.Arcade;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class Scoreboard : MonoBehaviour
    {
        [Header("Banner")]
        [SerializeField] Image BannerImage;
        [SerializeField] TMP_Text BannerText;
        [SerializeField] Color SinglePlayerBannerColor;
        [SerializeField] Color JadeTeamBannerColor;
        [SerializeField] Color RubyTeamBannerColor;
        [SerializeField] Color GoldTeamBannerColor;

        [Header("Single Player")]
        [SerializeField] Transform SingleplayerView;
        [SerializeField] TMP_Text SinglePlayerScoreTextField;
        [SerializeField] TMP_Text SinglePlayerHighscoreTextField;

        [Header("Multi Player")]
        [SerializeField] Transform MultiplayerView;
        [SerializeField] List<TMP_Text> PlayerNameTextFields;
        [SerializeField] List<TMP_Text> PlayerScoreTextFields;

        ScoreTracker scoreTracker;

        void Start()
        {
            scoreTracker = FindAnyObjectByType<ScoreTracker>();

            MultiplayerView.gameObject.SetActive(false);
            SingleplayerView.gameObject.SetActive(false);
        }

        public void ShowMultiplayerView()
        {
            // Set banner for winning player
            var winningTeam = scoreTracker.GetWinningTeam();

            switch (winningTeam)
            {
                case Teams.Jade:
                    BannerImage.color = JadeTeamBannerColor;
                    BannerText.text = "JADE VICTORY";
                    break;
                case Teams.Ruby:
                    BannerImage.color = RubyTeamBannerColor;
                    BannerText.text = "RUBY VICTORY";
                    break;
                case Teams.Gold:
                    BannerImage.color = GoldTeamBannerColor;
                    BannerText.text = "GOLD VICTORY";
                    break;
            }

            // Populate scores
            var playerScores = scoreTracker.playerScores.ToList();
            if (scoreTracker.GolfRules)
                playerScores.Sort((score1, score2) => score1.Value.CompareTo(score2.Value));
            else
                playerScores.Sort((score1, score2) => score2.Value.CompareTo(score1.Value));

            // Populate rows with player scores
            for (var i=0; i<playerScores.Count; i++)
            {
                var playerScore = playerScores[i];
                PlayerNameTextFields[i].text = playerScore.Key;
                PlayerScoreTextFields[i].text = ((int) playerScore.Value).ToString();
            }

            // Hide unused rows
            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                PlayerNameTextFields[i].text = "";
                PlayerScoreTextFields[i].text = "";
            }

            // Show the jam
            MultiplayerView.gameObject.SetActive(true);
        }

        public void ShowSingleplayerView()
        {
            // Setup Banner
            BannerImage.color = Color.white;
            BannerText.text = "RUN RESULTS";

            // Populate this run's score
            var playerScore = scoreTracker.playerScores.First();
            SinglePlayerScoreTextField.text = ((int)playerScore.Value).ToString();

            // TODO: pull actual high score
            // Populate high score
            SinglePlayerHighscoreTextField.text = ((int) playerScore.Value).ToString();

            // Show the jam
            SingleplayerView.gameObject.SetActive(true);
        }
    }
}