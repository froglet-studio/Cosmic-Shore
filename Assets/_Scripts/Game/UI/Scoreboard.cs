using System.Collections.Generic;
using CosmicShore.Game.UI; 
using CosmicShore.Soap;
using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
// [Visual Note] Add Namespace for StatsManager
using CosmicShore.Game.Analytics; 

namespace CosmicShore.Game
{
    public class Scoreboard : MonoBehaviour
    {
        // ... [Keep Headers and Variables exactly as you had them] ...
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
        [SerializeField] Transform SingleplayerView;
        [SerializeField] TMP_Text SinglePlayerScoreTextField;
        [SerializeField] TMP_Text SinglePlayerHighscoreTextField;

        [Header("Multiplayer View")]
        [SerializeField] Transform MultiplayerView;
        [SerializeField] List<TMP_Text> PlayerNameTextFields;
        [SerializeField] List<TMP_Text> PlayerScoreTextFields;

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
            if (gameData.IsMultiplayerMode)
                ShowMultiplayerView();
            else
                ShowSinglePlayerView();

            PopulateDynamicStats();

            if (scoreboardPanel)
                scoreboardPanel.gameObject.SetActive(true);
        }

        void PopulateDynamicStats()
        {
            if (statsContainer)
            {
                foreach (Transform child in statsContainer)
                    Destroy(child.gameObject);
            }

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
            
            // [Visual Note] Update the Static Text Fields
            if (SinglePlayerScoreTextField) 
                SinglePlayerScoreTextField.text = playerScore.ToString();
            
            // Fetch High Score from StatsManager for static display
            if (SinglePlayerHighscoreTextField)
            {
                int highScore = playerScore; // Default to current if manager missing
                if (UGSStatsManager.Instance != null)
                {
                    // If current score is higher than cached high score, we display current
                    int cachedHigh = UGSStatsManager.Instance.GetHighScoreForCurrentMode();
                    highScore = Mathf.Max(playerScore, cachedHigh);
                }
                SinglePlayerHighscoreTextField.text = highScore.ToString();
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

        void SetBannerForDomain(Domains domain)
        {
            // ... [Keep switch case exactly as provided] ...
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
                case Domains.None:
                case Domains.Unassigned:
                default:
                    Debug.LogWarning($"Domain {domain} does not have banner configuration.");
                    break;
            }
        }

        void DisplayPlayerScores()
        {
            // ... [Keep display logic exactly as provided] ...
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
            {
                UGSStatsManager.Instance.TrackPlayAgain();
            }

            gameData.ResetForReplay();
        }
    }
}