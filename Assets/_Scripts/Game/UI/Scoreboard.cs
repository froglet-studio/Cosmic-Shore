using System.Collections.Generic;
using CosmicShore.Soap;
using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    /// <summary>
    /// Simplified Scoreboard - ONLY displays final scores, NO animations.
    /// </summary>
    public class Scoreboard : MonoBehaviour
    {
        [FormerlySerializedAs("miniGameData")]
        [SerializeField] protected GameDataSO gameData;

        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        [SerializeField] Transform scoreboardPanel;

        [Header("Banner")]
        [SerializeField] Image BannerImage;
        [SerializeField] TMP_Text BannerText;
        [SerializeField] Color SinglePlayerBannerColor;
        [SerializeField] Color JadeTeamBannerColor;
        [SerializeField] Color RubyTeamBannerColor;
        [SerializeField] Color GoldTeamBannerColor;
        [SerializeField] Color BlueTeamBannerColor;

        [Header("Single Player View")]
        [SerializeField] Transform SingleplayerView;
        [SerializeField] TMP_Text SinglePlayerScoreTextField;
        [SerializeField] TMP_Text SinglePlayerHighscoreTextField;

        [Header("Multiplayer View")]
        [SerializeField] Transform MultiplayerView;
        [SerializeField] List<TMP_Text> PlayerNameTextFields;
        [SerializeField] List<TMP_Text> PlayerScoreTextFields;

        void Awake()
        {
            HideScoreboard();
        }

        void OnEnable()
        {
            if (gameData != null && gameData.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised += ShowScoreboard;

            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised += HideScoreboard;
        }

        void OnDisable()
        {
            if (gameData != null && gameData.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised -= ShowScoreboard;

            if (OnResetForReplay != null)
                OnResetForReplay.OnRaised -= HideScoreboard;
        }

        /// <summary>
        /// Called by OnShowGameEndScreen event after cinematics are complete.
        /// Simply displays the scoreboard with no animations.
        /// </summary>
        void ShowScoreboard()
        {
            if (gameData.IsMultiplayerMode)
                ShowMultiplayerView();
            else
                ShowSinglePlayerView();

            if (scoreboardPanel)
                scoreboardPanel.gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides all scoreboard UI elements.
        /// Called when resetting for replay.
        /// </summary>
        void HideScoreboard()
        {
            if (scoreboardPanel) 
                scoreboardPanel.gameObject.SetActive(false);
            if (MultiplayerView) 
                MultiplayerView.gameObject.SetActive(false);
            if (SingleplayerView) 
                SingleplayerView.gameObject.SetActive(false);
        }

        protected virtual void ShowSinglePlayerView()
        {
            // Determine if local player won
            bool won = gameData.IsLocalDomainWinner(out DomainStats localDomainStats);

            // Set banner
            var bannerText = won ? "VICTORY" : "DEFEAT";
            if (BannerImage) 
                BannerImage.color = SinglePlayerBannerColor;
            if (BannerText) 
                BannerText.text = bannerText;

            // Display score
            var playerScore = (int)localDomainStats.Score;
            if (SinglePlayerScoreTextField) 
                SinglePlayerScoreTextField.text = playerScore.ToString();
            if (SinglePlayerHighscoreTextField) 
                SinglePlayerHighscoreTextField.text = playerScore.ToString();

            // Show single player view, hide multiplayer
            if (MultiplayerView) 
                MultiplayerView.gameObject.SetActive(false);
            if (SingleplayerView) 
                SingleplayerView.gameObject.SetActive(true);
        }

        protected virtual void ShowMultiplayerView()
        {
            // Get winning domain
            bool isLocalWinner = gameData.IsLocalDomainWinner(out DomainStats winnerStats);
            var winningDomain = winnerStats.Domain;

            // Set banner based on winning team
            SetBannerForDomain(winningDomain);

            // Display all player scores (sorted by winner first)
            DisplayPlayerScores();

            // Show multiplayer view, hide single player
            if (SingleplayerView) 
                SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) 
                MultiplayerView.gameObject.SetActive(true);
        }

        void SetBannerForDomain(Domains domain)
        {
            switch (domain)
            {
                case Domains.Jade:
                    if (BannerImage) BannerImage.color = JadeTeamBannerColor;
                    if (BannerText) BannerText.text = "JADE VICTORY";
                    break;
                    
                case Domains.Ruby:
                    if (BannerImage) BannerImage.color = RubyTeamBannerColor;
                    if (BannerText) BannerText.text = "RUBY VICTORY";
                    break;
                    
                case Domains.Gold:
                    if (BannerImage) BannerImage.color = GoldTeamBannerColor;
                    if (BannerText) BannerText.text = "GOLD VICTORY";
                    break;
                    
                case Domains.Blue:
                    if (BannerImage) BannerImage.color = BlueTeamBannerColor;
                    if (BannerText) BannerText.text = "BLUE VICTORY";
                    break;
                    
                default:
                    Debug.LogWarning($"Domain {domain} does not have banner configuration.");
                    break;
            }
        }

        void DisplayPlayerScores()
        {
            var playerScores = gameData.RoundStatsList;

            // Fill in player scores
            for (var i = 0; i < playerScores.Count && i < PlayerNameTextFields.Count; i++)
            {
                var playerScore = playerScores[i];
                
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScore.Name;
                    
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = ((int)playerScore.Score).ToString();
            }

            // Clear remaining slots if there are more UI elements than players
            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i]) 
                    PlayerNameTextFields[i].text = "";
                    
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i]) 
                    PlayerScoreTextFields[i].text = "";
            }
        }
        public void OnPlayAgainButtonPressed()
        {
            gameData.ResetForReplay();
        }
    }
}