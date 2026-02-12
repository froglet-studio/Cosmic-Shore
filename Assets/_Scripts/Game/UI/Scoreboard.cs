using CosmicShore.Game.Arcade;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using Obvious.Soap;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace CosmicShore.Game.UI
{
    public class Scoreboard : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        // [Fix] SerializeField Reference (Assign in Inspector!)
        [Header("References")]
        [SerializeField] private MultiplayerMiniGameControllerBase multiplayerController;

        [Header("UI Containers")]
        [SerializeField] Transform scoreboardPanel;
        [SerializeField] Transform statsContainer; 
        [SerializeField] StatRowUI statRowPrefab;   

        [Header("Banner")]
        [SerializeField] Image BannerImage;
        [SerializeField] protected TMP_Text BannerText;
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
            
            // Robust event subscription
            var resetEvent = OnResetForReplay != null ? OnResetForReplay : gameData?.OnResetForReplay;
            if (resetEvent != null) resetEvent.OnRaised += HideScoreboard;
        }

        void OnDisable()
        {
            if (gameData != null && gameData.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised -= ShowScoreboard;
            
            var resetEvent = OnResetForReplay != null ? OnResetForReplay : gameData?.OnResetForReplay;
            if (resetEvent != null) resetEvent.OnRaised -= HideScoreboard;
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
                        modeEnum, gameData.SelectedIntensity.Value, playerScore);
                }
                SinglePlayerHighscoreTextField.text = ((int)finalHighScore).ToString();
            }

            if (MultiplayerView) MultiplayerView.gameObject.SetActive(false);
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(true);
        }

        protected virtual void ShowMultiplayerView()
        {
            // Now that DomainStats are calculated in the Controller, this will return the correct Winner
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
        
        public void OnPlayAgainButtonPressed()
        {
            if (UGSStatsManager.Instance != null) 
                UGSStatsManager.Instance.TrackPlayAgain();

            if (gameData.IsMultiplayerMode)
            {
                if (multiplayerController != null) 
                {
                    multiplayerController.RequestReplay();
                }
                else
                {
                    Debug.LogError("[Scoreboard] Multiplayer Controller Reference Missing! Assign in Inspector.");
                }
            }
            else
            {
                gameData.ResetForReplay();
            }
        }
    }
}