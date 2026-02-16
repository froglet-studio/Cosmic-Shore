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
        #region Serialized Fields
        
        [Header("Data")]
        [Tooltip("Main game data container")]
        [SerializeField] protected GameDataSO gameData;
        
        [Tooltip("Event triggered when replay is requested (optional override)")]
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        [Header("References")]
        [Tooltip("Required for multiplayer replay functionality")]
        [SerializeField] private MultiplayerMiniGameControllerBase multiplayerController;

        [Header("UI Containers")]
        [Tooltip("Root panel that contains the entire scoreboard")]
        [SerializeField] Transform scoreboardPanel;
        
        [Tooltip("Container where dynamic stat rows will be instantiated")]
        [SerializeField] Transform statsContainer;
        
        [Tooltip("Prefab for individual stat rows (Label + Value + Icon)")]
        [SerializeField] StatRowUI statRowPrefab;

        [Header("Banner")]
        [Tooltip("Banner background image (colored based on winner)")]
        [SerializeField] Image BannerImage;
        
        [Tooltip("Banner text (shows VICTORY/DEFEAT or team name)")]
        [SerializeField] protected TMP_Text BannerText;
        
        [SerializeField] Color SinglePlayerBannerColor = new Color(0.2f, 0.6f, 0.9f);
        [SerializeField] Color JadeTeamBannerColor = new Color(0.0f, 0.8f, 0.4f);
        [SerializeField] Color RubyTeamBannerColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] Color GoldTeamBannerColor = new Color(1.0f, 0.8f, 0.0f);
        [SerializeField] Color BlueTeamBannerColor = new Color(0.2f, 0.4f, 0.9f);

        [Header("Single Player View")]
        [Tooltip("Root container for single-player scoreboard elements")]
        [SerializeField] protected Transform SingleplayerView;
        
        [Tooltip("Displays the player's final score")]
        [SerializeField] TMP_Text SinglePlayerScoreTextField;
        
        [Tooltip("Displays the player's high score (current or new)")]
        [SerializeField] TMP_Text SinglePlayerHighscoreTextField;

        [Header("Multiplayer View")]
        [Tooltip("Root container for multiplayer scoreboard elements")]
        [SerializeField] protected Transform MultiplayerView;
        
        [Tooltip("Text fields for player names (one per player slot)")]
        [SerializeField] protected List<TMP_Text> PlayerNameTextFields;
        
        [Tooltip("Text fields for player scores (one per player slot)")]
        [SerializeField] protected List<TMP_Text> PlayerScoreTextFields;
        
        #endregion

        #region Private Fields

        private ScoreboardStatsProvider statsProvider;
        
        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            // Get the stats provider component (UniversalStatsProvider, HexRaceStatsProvider, etc.)
            statsProvider = GetComponent<ScoreboardStatsProvider>();
            
            if (!statsProvider)
            {
                Debug.LogWarning("[Scoreboard] No ScoreboardStatsProvider found. Stats will not be displayed.");
            }
            
            HideScoreboard();
        }

        void OnEnable()
        {
            // Subscribe to show scoreboard event
            if (gameData != null && gameData.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised += ShowScoreboard;
            
            // Subscribe to reset event (prefer local override if set, otherwise use gameData)
            var resetEvent = OnResetForReplay != null ? OnResetForReplay : gameData?.OnResetForReplay;
            if (resetEvent != null) 
                resetEvent.OnRaised += HideScoreboard;
        }

        void OnDisable()
        {
            // Unsubscribe from events
            if (gameData != null && gameData.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised -= ShowScoreboard;
            
            var resetEvent = OnResetForReplay != null ? OnResetForReplay : gameData?.OnResetForReplay;
            if (resetEvent != null) 
                resetEvent.OnRaised -= HideScoreboard;
        }
        
        #endregion

        #region Core Scoreboard Logic

        void ShowScoreboard()
        {
            if (!gameData)
            {
                Debug.LogError("[Scoreboard] GameData is null! Cannot show scoreboard.");
                return;
            }
            
            // Show appropriate view based on game mode
            if (gameData.IsMultiplayerMode) 
                ShowMultiplayerView();
            else 
                ShowSinglePlayerView();

            // Populate dynamic stats (crystals collected, drift time, etc.)
            PopulateDynamicStats();
            
            // Make scoreboard visible
            if (scoreboardPanel) 
                scoreboardPanel.gameObject.SetActive(true);
        }

        void HideScoreboard()
        {
            if (scoreboardPanel) 
                scoreboardPanel.gameObject.SetActive(false);
        }

        #endregion

        #region Single Player View

        protected virtual void ShowSinglePlayerView()
        {
            // Determine if player won and get their stats
            bool won = gameData.IsLocalDomainWinner(out DomainStats localDomainStats);
            var bannerText = won ? "VICTORY" : "DEFEAT";
            
            // Set banner appearance
            if (BannerImage) 
                BannerImage.color = SinglePlayerBannerColor;
            if (BannerText) 
                BannerText.text = bannerText;

            // Display player score
            var playerScore = (int)localDomainStats.Score;
            if (SinglePlayerScoreTextField) 
                SinglePlayerScoreTextField.text = playerScore.ToString();

            // Display high score (either existing or new)
            if (SinglePlayerHighscoreTextField)
            {
                float finalHighScore = playerScore;
                
                // Check with UGS for actual high score
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

            // Toggle view visibility
            if (MultiplayerView) 
                MultiplayerView.gameObject.SetActive(false);
            if (SingleplayerView) 
                SingleplayerView.gameObject.SetActive(true);
        }

        #endregion

        #region Multiplayer View
        
        protected virtual void ShowMultiplayerView()
        {
            // Determine winner and set banner
            bool isLocalWinner = gameData.IsLocalDomainWinner(out DomainStats winnerStats);
            SetBannerForDomain(winnerStats.Domain);
            
            // Display player scores
            DisplayPlayerScores();

            // Toggle view visibility
            if (SingleplayerView) 
                SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView) 
                MultiplayerView.gameObject.SetActive(true);
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
            
            // Fill player name and score fields
            for (var i = 0; i < playerScores.Count && i < PlayerNameTextFields.Count; i++)
            {
                var playerScore = playerScores[i];
                
                // Set player name
                if (PlayerNameTextFields[i]) 
                    PlayerNameTextFields[i].text = playerScore.Name;
                
                // Set player score (as integer by default)
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = ((int)playerScore.Score).ToString();
            }
            
            // Clear unused slots
            for (var i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i]) 
                    PlayerNameTextFields[i].text = "";
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i]) 
                    PlayerScoreTextFields[i].text = "";
            }
        }

        #endregion

        #region Dynamic Stats
        
        void PopulateDynamicStats()
        {
            // Clear existing stat rows
            if (statsContainer)
            {
                foreach (Transform child in statsContainer)
                    Destroy(child.gameObject);
            }
            
            // Validate dependencies
            if (!statsProvider || !statsContainer || !statRowPrefab)
            {
                if (!statsProvider)
                    Debug.LogWarning("[Scoreboard] No stats provider attached. Skipping stats display.");
                return;
            }

            // Get stats from provider and instantiate rows
            var stats = statsProvider.GetStats();
            foreach (var stat in stats)
            {
                var row = Instantiate(statRowPrefab, statsContainer);
                row.Initialize(stat.Label, stat.Value, stat.Icon);
            }
        }

        #endregion

        #region Play Again Button

        /// <summary>
        /// Called when the "Play Again" button is pressed.
        /// Handles replay for both single-player and multiplayer modes.
        /// </summary>
        public void OnPlayAgainButtonPressed()
        {
            // Track analytics
            if (UGSStatsManager.Instance != null) 
                UGSStatsManager.Instance.TrackPlayAgain();

            if (gameData.IsMultiplayerMode)
            {
                // Multiplayer: Request replay through controller
                if (multiplayerController != null) 
                {
                    multiplayerController.RequestReplay();
                }
                else
                {
                    Debug.LogError("[Scoreboard] Multiplayer Controller reference missing! Assign in Inspector.");
                }
            }
            else
            {
                // Single-player: Reset directly through game data
                gameData.ResetForReplay();
            }
        }

        #endregion
    }
}