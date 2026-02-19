// Scoreboard.cs — rematch panels auto-dismiss after 2s (except received panel)
using CosmicShore.Game.Arcade;
using CosmicShore.Game.Analytics;
using CosmicShore.Soap;
using Obvious.Soap;
using System.Collections;
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
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        [Header("References")]
        [SerializeField] private MultiplayerMiniGameControllerBase multiplayerController;

        [SerializeField] private GameObject endGameObject;

        [Header("UI Containers")]
        [SerializeField] Transform scoreboardPanel;
        [SerializeField] Transform statsContainer;
        [SerializeField] StatRowUI statRowPrefab;

        [Header("Banner")]
        [SerializeField] Image BannerImage;
        [SerializeField] protected TMP_Text BannerText;
        [SerializeField] Color SinglePlayerBannerColor = new Color(0.2f, 0.6f, 0.9f);
        [SerializeField] Color JadeTeamBannerColor    = new Color(0.0f, 0.8f, 0.4f);
        [SerializeField] Color RubyTeamBannerColor    = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] Color GoldTeamBannerColor    = new Color(1.0f, 0.8f, 0.0f);
        [SerializeField] Color BlueTeamBannerColor    = new Color(0.2f, 0.4f, 0.9f);

        [Header("Single Player View")]
        [SerializeField] protected Transform SingleplayerView;
        [SerializeField] TMP_Text SinglePlayerScoreTextField;
        [SerializeField] TMP_Text SinglePlayerHighscoreTextField;

        [Header("Multiplayer View")]
        [SerializeField] protected Transform MultiplayerView;
        [SerializeField] protected List<TMP_Text> PlayerNameTextFields;
        [SerializeField] protected List<TMP_Text> PlayerScoreTextFields;

        [Header("Multiplayer Rematch")]
        [Tooltip("Shown to the player who SENT the request — auto-dismisses after 2s if no response")]
        [SerializeField] private GameObject rematchInvitedPanel;

        [Tooltip("Shown to the player who RECEIVED the request — stays until Yes/No pressed")]
        [SerializeField] private GameObject rematchReceivedPanel;
        [SerializeField] private TMP_Text rematchReceivedText;

        [Tooltip("Shown to the requester when DENIED — auto-dismisses after 2s")]
        [SerializeField] private GameObject rematchDeniedPanel;
        [SerializeField] private TMP_Text rematchDeniedText;

        [Tooltip("Play Again button — hidden while rematch request is pending")]
        [SerializeField] private GameObject playAgainButton;

        [Tooltip("Seconds before invited/denied panels auto-dismiss")]
        [SerializeField] private float rematchPanelAutoDismissSeconds = 2f;

        #endregion

        #region Private Fields

        private ScoreboardStatsProvider statsProvider;
        private Coroutine _invitedAutoDismiss;
        private Coroutine _deniedAutoDismiss;

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            statsProvider = GetComponent<ScoreboardStatsProvider>();
            if (!statsProvider)
                Debug.LogWarning("[Scoreboard] No ScoreboardStatsProvider found.");
            HideScoreboard();
        }

        void OnEnable()
        {
            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised += ShowScoreboard;

            var resetEvent = OnResetForReplay ?? gameData?.OnResetForReplay;
            if (resetEvent != null) resetEvent.OnRaised += HideScoreboard;
        }

        void OnDisable()
        {
            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised -= ShowScoreboard;

            var resetEvent = OnResetForReplay ?? gameData?.OnResetForReplay;
            if (resetEvent != null) resetEvent.OnRaised -= HideScoreboard;
        }

        #endregion

        #region Core

        void ShowScoreboard()
        {
            if (!gameData) { Debug.LogError("[Scoreboard] GameData is null!"); return; }

            HideAllRematchPanels();

            if (gameData.IsMultiplayerMode) ShowMultiplayerView();
            else ShowSinglePlayerView();

            PopulateDynamicStats();

            if (scoreboardPanel) scoreboardPanel.gameObject.SetActive(true);
        }

        void HideScoreboard()
        {
            if (scoreboardPanel) scoreboardPanel.gameObject.SetActive(false);
            if(endGameObject) endGameObject.SetActive(false);
            HideAllRematchPanels();
        }

        void HideAllRematchPanels()
        {
            StopAutoDismiss(ref _invitedAutoDismiss);
            StopAutoDismiss(ref _deniedAutoDismiss);

            if (rematchInvitedPanel)  rematchInvitedPanel.SetActive(false);
            if (rematchReceivedPanel) rematchReceivedPanel.SetActive(false);
            if (rematchDeniedPanel)   rematchDeniedPanel.SetActive(false);
            if (playAgainButton)      playAgainButton.SetActive(true);
        }

        void StopAutoDismiss(ref Coroutine coroutine)
        {
            if (coroutine == null) return;
            StopCoroutine(coroutine);
            coroutine = null;
        }

        IEnumerator AutoDismissPanel(GameObject panel, float delay, Action onDismiss = null)
        {
            yield return new WaitForSeconds(delay);
            if (panel) panel.SetActive(false);
            onDismiss?.Invoke();
        }

        #endregion

        #region Single Player View

        protected virtual void ShowSinglePlayerView()
        {
            bool won = gameData.IsLocalDomainWinner(out DomainStats localDomainStats);
            if (BannerImage) BannerImage.color = SinglePlayerBannerColor;
            if (BannerText)  BannerText.text   = won ? "VICTORY" : "DEFEAT";

            int playerScore = (int)localDomainStats.Score;
            if (SinglePlayerScoreTextField)
                SinglePlayerScoreTextField.text = playerScore.ToString();

            if (SinglePlayerHighscoreTextField)
            {
                float highScore = playerScore;
                if (UGSStatsManager.Instance &&
                    Enum.TryParse(gameData.GameMode.ToString(), out GameModes modeEnum))
                {
                    highScore = UGSStatsManager.Instance.GetEvaluatedHighScore(
                        modeEnum, gameData.SelectedIntensity.Value, playerScore);
                }
                SinglePlayerHighscoreTextField.text = ((int)highScore).ToString();
            }

            if (MultiplayerView)  MultiplayerView.gameObject.SetActive(false);
            if (SingleplayerView) SingleplayerView.gameObject.SetActive(true);
        }

        #endregion

        #region Multiplayer View

        protected virtual void ShowMultiplayerView()
        {
            gameData.IsLocalDomainWinner(out DomainStats winnerStats);
            SetBannerForDomain(winnerStats.Domain);
            DisplayPlayerScores();

            if (SingleplayerView) SingleplayerView.gameObject.SetActive(false);
            if (MultiplayerView)  MultiplayerView.gameObject.SetActive(true);
        }

        protected virtual void SetBannerForDomain(Domains domain)
        {
            switch (domain)
            {
                case Domains.Jade:
                    if (BannerImage) BannerImage.color = JadeTeamBannerColor;
                    if (BannerText)  BannerText.text   = "JADE VICTORY"; break;
                case Domains.Ruby:
                    if (BannerImage) BannerImage.color = RubyTeamBannerColor;
                    if (BannerText)  BannerText.text   = "RUBY VICTORY"; break;
                case Domains.Gold:
                    if (BannerImage) BannerImage.color = GoldTeamBannerColor;
                    if (BannerText)  BannerText.text   = "GOLD VICTORY"; break;
                case Domains.Blue:
                    if (BannerImage) BannerImage.color = BlueTeamBannerColor;
                    if (BannerText)  BannerText.text   = "BLUE VICTORY"; break;
                default:
                    if (BannerText) BannerText.text = "GAME OVER"; break;
            }
        }

        protected virtual void DisplayPlayerScores()
        {
            var playerScores = gameData.RoundStatsList;

            for (int i = 0; i < playerScores.Count && i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i])
                    PlayerNameTextFields[i].text = playerScores[i].Name;
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = ((int)playerScores[i].Score).ToString();
            }

            for (int i = playerScores.Count; i < PlayerNameTextFields.Count; i++)
            {
                if (PlayerNameTextFields[i]) PlayerNameTextFields[i].text = "";
                if (i < PlayerScoreTextFields.Count && PlayerScoreTextFields[i])
                    PlayerScoreTextFields[i].text = "";
            }
        }

        #endregion

        #region Dynamic Stats

        void PopulateDynamicStats()
        {
            if (statsContainer)
                foreach (Transform child in statsContainer)
                    Destroy(child.gameObject);

            if (!statsProvider || !statsContainer || !statRowPrefab) return;

            foreach (var stat in statsProvider.GetStats())
            {
                var row = Instantiate(statRowPrefab, statsContainer);
                row.Initialize(stat.Label, stat.Value, stat.Icon);
            }
        }

        #endregion

        #region Play Again / Rematch

        public void OnPlayAgainButtonPressed()
        {
            if (UGSStatsManager.Instance != null)
                UGSStatsManager.Instance.TrackPlayAgain();

            if (gameData.IsMultiplayerMode)
            {
                if (multiplayerController == null)
                {
                    Debug.LogError("[Scoreboard] multiplayerController not assigned!");
                    return;
                }

                if (playAgainButton)     playAgainButton.SetActive(false);
                if (rematchInvitedPanel) rematchInvitedPanel.SetActive(true);

                // Invited panel auto-dismisses after 2s if opponent doesn't respond
                // Restores play again button so local player isn't stuck waiting
                StopAutoDismiss(ref _invitedAutoDismiss);
                _invitedAutoDismiss = StartCoroutine(AutoDismissPanel(
                    rematchInvitedPanel,
                    rematchPanelAutoDismissSeconds,
                    onDismiss: () => { if (playAgainButton) playAgainButton.SetActive(true); }
                ));

                multiplayerController.RequestRematch(gameData.LocalPlayer.Name);
            }
            else
            {
                gameData.ResetForReplay();
            }
        }

        /// <summary>
        /// Called by MultiplayerMiniGameControllerBase when the OPPONENT requests a rematch.
        /// Received panel stays until the player responds — no auto-dismiss.
        /// </summary>
        public void ShowRematchRequest(string requesterName)
        {
            if (rematchReceivedText)
                rematchReceivedText.text = $"{requesterName} wants a rematch!";

            if (rematchReceivedPanel) rematchReceivedPanel.SetActive(true);
            if (playAgainButton)      playAgainButton.SetActive(false);
            // No auto-dismiss — player must actively accept or decline
        }

        /// <summary>
        /// Bound to YES button inside rematchReceivedPanel.
        /// </summary>
        public void OnAcceptRematch()
        {
            HideAllRematchPanels();
            multiplayerController?.RequestReplay();
        }

        /// <summary>
        /// Bound to NO button inside rematchReceivedPanel.
        /// </summary>
        public void OnDeclineRematch()
        {
            if (rematchReceivedPanel) rematchReceivedPanel.SetActive(false);
            if (playAgainButton)      playAgainButton.SetActive(true);
            multiplayerController?.NotifyRematchDeclined(gameData.LocalPlayer.Name);
        }

        /// <summary>
        /// Called by MultiplayerMiniGameControllerBase when the OPPONENT declined our request.
        /// Denied panel auto-dismisses after 2s, then restores play again button.
        /// </summary>
        public void ShowRematchDeclined(string declinerName)
        {
            StopAutoDismiss(ref _invitedAutoDismiss); // cancel invited panel if still running
            if (rematchInvitedPanel) rematchInvitedPanel.SetActive(false);

            if (rematchDeniedText)
                rematchDeniedText.text = $"{declinerName} declined the rematch.";

            if (rematchDeniedPanel) rematchDeniedPanel.SetActive(true);

            StopAutoDismiss(ref _deniedAutoDismiss);
            _deniedAutoDismiss = StartCoroutine(AutoDismissPanel(
                rematchDeniedPanel,
                rematchPanelAutoDismissSeconds,
                onDismiss: () => { if (playAgainButton) playAgainButton.SetActive(true); }
            ));
        }

        #endregion
    }
}