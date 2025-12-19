using System.Collections.Generic;
using CosmicShore.Soap;
using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class Scoreboard : MonoBehaviour
    {
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        protected GameDataSO gameData; 
        
        [SerializeField]
        private ScriptableEventNoParam OnResetForReplay; 
        
        [SerializeField]
        Transform gameOverPanel;
        
        [Header("Banner")] 
        [SerializeField] Image BannerImage;
        
        [SerializeField] TMP_Text BannerText;
        [SerializeField] Color SinglePlayerBannerColor;
        [SerializeField] Color JadeTeamBannerColor;
        [SerializeField] Color RubyTeamBannerColor;
        [SerializeField] Color GoldTeamBannerColor;

        [Header("Single Player")] 
        [SerializeField]
        Transform SingleplayerView;

        [SerializeField] TMP_Text SinglePlayerScoreTextField;
        [SerializeField] TMP_Text SinglePlayerHighscoreTextField;

        [Header("Multi Player")] [SerializeField]
        Transform MultiplayerView;

        [SerializeField] List<TMP_Text> PlayerNameTextFields;
        [SerializeField] List<TMP_Text> PlayerScoreTextFields;

        // TODO - Use MiniGameDataVariable instead
        // ScoreTracker scoreTracker;
        

        void Awake()
        {
            // scoreTracker = FindAnyObjectByType<ScoreTracker>();
            ResetForReplay();
        }

        private void OnEnable()
        {
            gameData.OnWinnerCalculated += ShowSinglePlayerView;
            OnResetForReplay.OnRaised += ResetForReplay;
        }

        private void OnDisable()
        {
            gameData.OnWinnerCalculated -= ShowSinglePlayerView;
            OnResetForReplay.OnRaised -= ResetForReplay;
        }

        private void ResetForReplay()
        {
            gameOverPanel.gameObject.SetActive(false);
            MultiplayerView.gameObject.SetActive(false);
            SingleplayerView.gameObject.SetActive(false);
        }

        protected virtual bool TryGetWinner(out IRoundStats roundStats, out bool localIsWinner) =>
            gameData.TryGetWinner(out roundStats, out localIsWinner);
        
        protected virtual void ShowSinglePlayerView()
        {
            if (!TryGetWinner(out  IRoundStats roundStats, out bool localIsWinner))
                return;
                
            // Setup Banner
            BannerImage.color = SinglePlayerBannerColor;
            
            if (!localIsWinner)
                BannerText.text = "DEFEAT";
            else
                BannerText.text = "WON";

            // Populate this run's Score
            var playerScore = roundStats.Score; // Mathf.Max(roundStats.Score, 0);
            SinglePlayerScoreTextField.text = ((int)playerScore).ToString();

            // TODO: pull actual high Score
            // Populate high Score
            SinglePlayerHighscoreTextField.text = ((int) playerScore).ToString();

            // Show the jam
            MultiplayerView.gameObject.SetActive(false);
            SingleplayerView.gameObject.SetActive(true);
            gameOverPanel.gameObject.SetActive(true);
        }
        
        public void ShowMultiplayerView()
        {
            // Set banner for winning player
            // TODO - Take winner data from MiniGameDataSO
            var winningTeam = Domains.Jade; // miniGameData.GetWinnerScoreData().Team;

            switch (winningTeam)
            {
                case Domains.Jade:
                    BannerImage.color = JadeTeamBannerColor;
                    BannerText.text = "JADE VICTORY";
                    break;
                case Domains.Ruby:
                    BannerImage.color = RubyTeamBannerColor;
                    BannerText.text = "RUBY VICTORY";
                    break;
                case Domains.Gold:
                    BannerImage.color = GoldTeamBannerColor;
                    BannerText.text = "GOLD VICTORY";
                    break;
                case Domains.Blue:
                case Domains.Unassigned:
                case Domains.None:
                default:
                    Debug.LogWarning($"{winningTeam} does not have assigned banner image color and banner text preset.");
                    break;
            }

            // Populate scores
            var playerScores = gameData.RoundStatsList;

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
            gameOverPanel.gameObject.SetActive(true);
        }
        
        /*public void ShowSinglePlayerView(bool defeat=false)
        {
            // Setup Banner
            BannerImage.color = SinglePlayerBannerColor;
            if (defeat)
                BannerText.text = "DEFEAT";
            else
                BannerText.text = "RUN RESULTS";

            // Populate this run's Score
            var playerScore = Mathf.Max(miniGameData.RoundStatsList[0].Score, 0);
            SinglePlayerScoreTextField.text = ((int)playerScore).ToString();

            // TODO: pull actual high Score
            // Populate high Score
            SinglePlayerHighscoreTextField.text = ((int) playerScore).ToString();

            // Show the jam
            MultiplayerView.gameObject.SetActive(false);
            SingleplayerView.gameObject.SetActive(true);
        }*/
    }
}