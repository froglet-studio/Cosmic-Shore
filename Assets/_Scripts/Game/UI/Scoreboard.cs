using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using Obvious.Soap;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class Scoreboard : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        [Header("UI Containers")]
        [SerializeField] Transform scoreboardPanel;
        [SerializeField] Transform statsContainer; 
        [SerializeField] StatRowUI statRowPrefab;   

        [Header("Banner")]
        [SerializeField] Image BannerImage;
        [SerializeField] TMP_Text BannerText;
        [SerializeField] Color SinglePlayerBannerColor;
        [SerializeField] Color JadeTeamBannerColor;
        [SerializeField] Color RubyTeamBannerColor;
        [SerializeField] Color GoldTeamBannerColor;
        [SerializeField] Color BlueTeamBannerColor;

        [Header("Single Player View")]
        [SerializeField] protected Transform SingleplayerView;
        [SerializeField] TMP_Text SinglePlayerScoreTextField;
        [SerializeField] TMP_Text SinglePlayerHighscoreTextField;

        [Header("Multiplayer View")]
        [SerializeField] protected Transform MultiplayerView;
        [SerializeField] protected List<TMP_Text> PlayerNameTextFields;
        [SerializeField] protected List<TMP_Text> PlayerScoreTextFields;

        private ScoreboardStatsProvider statsProvider;

        void Awake()
        {
            statsProvider = GetComponent<ScoreboardStatsProvider>();
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

        void ShowScoreboard()
        {
            if (gameData.IsMultiplayerMode) ShowMultiplayerView();
            else ShowSinglePlayerView();

            PopulateDynamicStats();
            if (scoreboardPanel) scoreboardPanel.gameObject.SetActive(true);
        }

        void PopulateDynamicStats()
        {
            if (statsContainer) foreach (Transform child in statsContainer) Destroy(child.gameObject);
            if (!statsProvider || !statsContainer || !statRowPrefab) return;

            var stats = statsProvider.GetStats();
            foreach (var stat in stats)
            {
                var row = Instantiate(statRowPrefab, statsContainer);
                row.Initialize(stat.Label, stat.Value, stat.Icon);
            }
        }

        void HideScoreboard()
        {
            if (scoreboardPanel) scoreboardPanel.gameObject.SetActive(false);
        }
        
        protected virtual void ShowSinglePlayerView()
        {
            bool won = gameData.IsLocalDomainWinner(out DomainStats localDomainStats);
            var bannerText = won ? "VICTORY" : "DEFEAT";
            
            if (BannerImage) BannerImage.color = SinglePlayerBannerColor;
            if (BannerText) BannerText.text = bannerText;

            var playerScore = (int)localDomainStats.Score;

            if (SinglePlayerScoreTextField) 
                SinglePlayerScoreTextField.text = playerScore.ToString();

            if (SinglePlayerHighscoreTextField)
            {
                float finalHighScore = playerScore;
                
                if (UGSStatsManager.Instance && Enum.TryParse(gameData.GameMode.ToString(), out GameModes modeEnum))
                {
                    finalHighScore = UGSStatsManager.Instance.GetEvaluatedHighScore(
                        modeEnum, 
                        gameData.SelectedIntensity.Value, 
                        playerScore
                    );
                }
                
                SinglePlayerHighscoreTextField.text = ((int)finalHighScore).ToString();
            }

            if (MultiplayerView) MultiplayerView.gameObject.SetActive(false);
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(true);
        }

        protected virtual void ShowMultiplayerView()
        {
            bool isLocalWinner = gameData.IsLocalDomainWinner(out DomainStats winnerStats);
            SetBannerForDomain(winnerStats.Domain);
            DisplayPlayerScores();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) MultiplayerView.gameObject.SetActive(true);
        }

        protected virtual void SetBannerForDomain(Domains domain)
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
                    if (BannerText) BannerText.text = "GAME OVER";
                    break;
            }
        }

        protected virtual void DisplayPlayerScores()
        {
            var playerScores = gameData.RoundStatsList;
            for (var i = 0; i < playerScores.Count && i < PlayerNameTextFields.Count; i++)
            {
                var playerScore = playerScores[i];
                if (PlayerNameTextFields[i]) PlayerNameTextFields[i].text = playerScore.Name;
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = ((int)playerScore.Score).ToString();
            }
            
            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i]) PlayerNameTextFields[i].text = "";
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i]) PlayerScoreTextFields[i].text = "";
            }
        }
        
        /// <summary>
        /// Called when Play Again button is pressed.
        /// In multiplayer, only the server should trigger the reset to keep all clients in sync.
        /// </summary>
        public void OnPlayAgainButtonPressed()
        {
            if (UGSStatsManager.Instance != null) 
                UGSStatsManager.Instance.TrackPlayAgain();
            
            // In multiplayer, only server initiates replay
            if (gameData.IsMultiplayerMode)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    gameData.ResetForReplay();
                }
                // Clients do nothing - they'll receive the reset via ClientRpc
            }
            else
            {
                // Single player can reset directly
                gameData.ResetForReplay();
            }
        }
    }
}