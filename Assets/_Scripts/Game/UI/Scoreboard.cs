using System;
using CosmicShore.Game.Arcade;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.SOAP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class Scoreboard : MonoBehaviour
    {
        [Header("Banner")] [SerializeField] Image BannerImage;
        [SerializeField] public TMP_Text BannerText;
        [SerializeField] Color SinglePlayerBannerColor;
        [SerializeField] Color JadeTeamBannerColor;
        [SerializeField] Color RubyTeamBannerColor;
        [SerializeField] Color GoldTeamBannerColor;

        [Header("Single Player")] [SerializeField]
        Transform SingleplayerView;

        [SerializeField] TMP_Text SinglePlayerScoreTextField;
        [SerializeField] TMP_Text SinglePlayerHighscoreTextField;

        [Header("Multi Player")] [SerializeField]
        Transform MultiplayerView;

        [SerializeField] List<TMP_Text> PlayerNameTextFields;
        [SerializeField] List<TMP_Text> PlayerScoreTextFields;

        // TODO - Use MiniGameDataVariable instead
        // ScoreTracker scoreTracker;
        [SerializeField]
        MiniGameDataSO miniGameData; 

        void Awake()
        {
            // scoreTracker = FindAnyObjectByType<ScoreTracker>();
            MultiplayerView.gameObject.SetActive(false);
            SingleplayerView.gameObject.SetActive(false);
        }

        public void ShowMultiplayerView()
        {
            // Set banner for winning player
            // TODO - Take winner data from MiniGameDataSO
            var winningTeam = Teams.Jade; // miniGameData.GetWinnerScoreData().Team;

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
                case Teams.Blue:
                case Teams.Unassigned:
                case Teams.None:
                default:
                    Debug.LogWarning($"{winningTeam} does not have assigned banner image color and banner text preset.");
                    break;
            }

            // Populate scores
            var playerScores = miniGameData.RoundStatsList;

            // Populate rows with player scores
            for (var i=0; i<playerScores.Count; i++)
            {
                var playerScore = playerScores[i];
                PlayerNameTextFields[i].text = playerScore.Name;
                PlayerScoreTextFields[i].text = ((int) playerScore.Score).ToString();
            }

            // Hide unused rows
            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                PlayerNameTextFields[i].text = "";
                PlayerScoreTextFields[i].text = "";
            }

            // Show the jam
            SingleplayerView.gameObject.SetActive(false);
            MultiplayerView.gameObject.SetActive(true);
        }

        public void ShowSinglePlayerView(bool defeat=false)
        {
            // Setup Banner
            BannerImage.color = SinglePlayerBannerColor;
            if (defeat)
                BannerText.text = "DEFEAT";
            else
                BannerText.text = "RUN RESULTS";

            // Populate this run's score
            var playerScore = Mathf.Max(miniGameData.RoundStatsList[0].Score, 0);
            SinglePlayerScoreTextField.text = ((int)playerScore).ToString();

            // TODO: pull actual high score
            // Populate high score
            SinglePlayerHighscoreTextField.text = ((int) playerScore).ToString();

            // Show the jam
            MultiplayerView.gameObject.SetActive(false);
            SingleplayerView.gameObject.SetActive(true);
        }
    }
}