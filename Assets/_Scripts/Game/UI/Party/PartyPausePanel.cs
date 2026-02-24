using System.Collections.Generic;
using CosmicShore.Game.Arcade.Party;
using CosmicShore.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI.Party
{
    /// <summary>
    /// Main UI controller for the Party Game pause/scoreboard panel.
    /// Manages round tabs, ready/quit buttons, and overall party state display.
    /// Shown instead of the normal pause panel during party mode.
    /// </summary>
    public class PartyPausePanel : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] PartyGameConfigSO config;
        [SerializeField] PartyGameController partyController;

        [Header("Panel")]
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Game State")]
        [SerializeField] TMP_Text gameStateText;

        [Header("Round Tabs")]
        [SerializeField] Transform roundTabContainer;
        [SerializeField] PartyRoundTab roundTabPrefab;
        [SerializeField] ScrollRect scrollRect;

        [Header("Buttons")]
        [SerializeField] Button readyButton;
        [SerializeField] TMP_Text readyButtonText;
        [SerializeField] Button quitButton;

        readonly List<PartyRoundTab> _roundTabs = new();
        PartyPhase _currentPhase;
        bool _isVisible;
        int _activeRoundIndex;

        #region Unity Lifecycle

        void Awake()
        {
            // Ensure initial state is hidden
            _isVisible = true; // So Hide() doesn't early-return
            Hide();
        }

        void OnEnable()
        {
            if (readyButton)
                readyButton.onClick.AddListener(OnReadyClicked);
            if (quitButton)
                quitButton.onClick.AddListener(OnQuitClicked);
        }

        void OnDisable()
        {
            if (readyButton)
                readyButton.onClick.RemoveListener(OnReadyClicked);
            if (quitButton)
                quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Called once at the start of the party to create round tabs.
        /// </summary>
        public void Initialize(int totalRounds, IReadOnlyList<PartyPlayerState> players)
        {
            // Clear existing tabs
            foreach (var tab in _roundTabs)
                if (tab) Destroy(tab.gameObject);
            _roundTabs.Clear();

            // Create round tabs
            for (int i = 0; i < totalRounds; i++)
            {
                if (!roundTabPrefab || !roundTabContainer) break;

                var tab = Instantiate(roundTabPrefab, roundTabContainer);
                tab.Initialize(i);
                tab.SetPlayerNames(players);
                _roundTabs.Add(tab);
            }

            // Highlight the first round as active
            _activeRoundIndex = 0;
            SetActiveRound(0);

            SetReadyButtonInteractable(false);
        }

        #endregion

        #region Show / Hide

        public void Show()
        {
            if (_isVisible) return;
            _isVisible = true;

            if (canvasGroup)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            gameObject.SetActive(true);

            // Auto-scroll to the current active round
            ScrollToActiveRound();
        }

        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;

            if (canvasGroup)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Forces the panel visible regardless of current state.
        /// </summary>
        public void ForceShow()
        {
            _isVisible = false; // Reset so Show() doesn't early-return
            Show();
        }

        public bool IsVisible => _isVisible;

        #endregion

        #region Phase Updates

        public void OnPhaseChanged(PartyPhase phase)
        {
            _currentPhase = phase;

            switch (phase)
            {
                case PartyPhase.Lobby:
                    SetReadyButtonInteractable(false);
                    if (readyButtonText) readyButtonText.text = "WAITING...";
                    break;

                case PartyPhase.WaitingForReady:
                    SetReadyButtonInteractable(true);
                    if (readyButtonText) readyButtonText.text = "READY";
                    break;

                case PartyPhase.Randomizing:
                    SetReadyButtonInteractable(false);
                    if (readyButtonText) readyButtonText.text = "RANDOMIZING...";
                    break;

                case PartyPhase.Countdown:
                    SetReadyButtonInteractable(false);
                    if (readyButtonText) readyButtonText.text = "GET READY!";
                    break;

                case PartyPhase.Playing:
                    SetReadyButtonInteractable(false);
                    if (readyButtonText) readyButtonText.text = "IN GAME";
                    break;

                case PartyPhase.RoundResults:
                    SetReadyButtonInteractable(true);
                    if (readyButtonText) readyButtonText.text = "READY";
                    ScrollToActiveRound();
                    break;

                case PartyPhase.FinalResults:
                    // Button state is set here but the panel is shown by
                    // PartyEndGameHandler after the cinematic finishes
                    SetReadyButtonInteractable(true);
                    if (readyButtonText) readyButtonText.text = "NEXT";
                    break;
            }
        }

        #endregion

        #region Game State Text

        public void SetGameStateText(string text)
        {
            if (gameStateText) gameStateText.text = text;

            // Also update the active round tab's game state text
            if (_activeRoundIndex >= 0 && _activeRoundIndex < _roundTabs.Count)
                _roundTabs[_activeRoundIndex].SetGameStateText(text);
        }

        #endregion

        #region Player Events

        public void OnPlayerJoined(string playerName, Domains domain, bool isAI)
        {
            // Update round tabs with new player list — tabs will be refreshed
            // when the controller provides the full player list
        }

        public void OnPlayerLeft(string playerName)
        {
            // Could show a notification that the player was replaced by AI
        }

        public void OnPlayerReadyChanged(string playerName, bool isReady)
        {
            // Update the current active round tab's ready indicator
            if (_activeRoundIndex >= 0 && _activeRoundIndex < _roundTabs.Count)
                _roundTabs[_activeRoundIndex].SetPlayerReady(playerName, isReady);
        }

        #endregion

        #region Round Updates

        /// <summary>
        /// Called when a round result is synced from the server.
        /// </summary>
        public void UpdateRoundResult(int roundIndex, PartyRoundResult result)
        {
            if (roundIndex < 0 || roundIndex >= _roundTabs.Count) return;

            _roundTabs[roundIndex].SetRoundResult(result);

            // Highlight the next round as active
            int nextRound = roundIndex + 1;
            _activeRoundIndex = nextRound;

            for (int i = 0; i < _roundTabs.Count; i++)
                _roundTabs[i].SetActive(i == nextRound);

            // Auto-scroll to the completed round (so user can see the result)
            ScrollToRound(roundIndex);
        }

        /// <summary>
        /// Sets the active round tab (the one currently being played).
        /// </summary>
        public void SetActiveRound(int roundIndex)
        {
            _activeRoundIndex = roundIndex;
            for (int i = 0; i < _roundTabs.Count; i++)
                _roundTabs[i].SetActive(i == roundIndex);

            ScrollToRound(roundIndex);
        }

        #endregion

        #region Auto-Scroll

        /// <summary>
        /// Scrolls the scroll view to bring the active round tab into view.
        /// </summary>
        void ScrollToActiveRound()
        {
            ScrollToRound(_activeRoundIndex);
        }

        /// <summary>
        /// Scrolls the scroll view so the specified round tab is visible.
        /// Uses normalized scroll position based on the tab's index within the list.
        /// </summary>
        void ScrollToRound(int roundIndex)
        {
            if (!scrollRect || _roundTabs.Count <= 1) return;
            if (roundIndex < 0 || roundIndex >= _roundTabs.Count) return;

            var tab = _roundTabs[roundIndex];
            if (!tab) return;

            // Force layout rebuild so RectTransform positions are current
            Canvas.ForceUpdateCanvases();

            var contentRect = scrollRect.content;
            var viewportRect = scrollRect.viewport ?? (RectTransform)scrollRect.transform;

            if (!contentRect) return;

            // Calculate the position of the tab within the content
            var tabRect = (RectTransform)tab.transform;
            float contentHeight = contentRect.rect.height;
            float viewportHeight = viewportRect.rect.height;

            if (contentHeight <= viewportHeight) return; // No scrolling needed

            // Get the tab's position relative to the content top
            float tabLocalY = contentRect.InverseTransformPoint(tabRect.position).y;
            float contentTopY = contentRect.rect.yMax;
            float distanceFromTop = contentTopY - tabLocalY;

            // Center the tab in the viewport
            float targetScroll = (distanceFromTop - viewportHeight * 0.5f) / (contentHeight - viewportHeight);
            targetScroll = Mathf.Clamp01(1f - targetScroll); // ScrollRect vertical: 1 = top, 0 = bottom

            scrollRect.verticalNormalizedPosition = targetScroll;
        }

        #endregion

        #region Final Results

        /// <summary>
        /// Called when final results are synced. Updates round tabs but does not
        /// display final standings inline — the PartyScoreboard handles that
        /// when the player clicks "Next".
        /// </summary>
        public void OnFinalResults(IReadOnlyList<PartyPlayerState> sortedPlayers)
        {
            ForceShow();
        }

        #endregion

        #region Button Handlers

        void OnReadyClicked()
        {
            if (_currentPhase == PartyPhase.FinalResults)
            {
                // "Next" button pressed after last round — show the final scoreboard
                SetReadyButtonInteractable(false);
                Hide();
                gameData.InvokeShowGameEndScreen();
                return;
            }

            if (_currentPhase != PartyPhase.WaitingForReady &&
                _currentPhase != PartyPhase.RoundResults)
                return;

            // Disable button after click to prevent double-tap
            SetReadyButtonInteractable(false);
            if (readyButtonText) readyButtonText.text = "READY!";

            // Notify the controller
            if (partyController)
                partyController.OnLocalPlayerReady();
        }

        void OnQuitClicked()
        {
            if (partyController)
                partyController.OnQuitParty();
        }

        void SetReadyButtonInteractable(bool interactable)
        {
            if (readyButton) readyButton.interactable = interactable;
        }

        #endregion

    }
}
