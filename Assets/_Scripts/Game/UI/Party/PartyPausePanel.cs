using System.Collections.Generic;
using CosmicShore.Game.Arcade.Party;
using CosmicShore.Soap;
using CosmicShore.Utility;
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
            // Start hidden — controller will call ForceShow after OnNetworkSpawn
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
        /// Called at the start of the party (and again when all players are known).
        /// </summary>
        public void Initialize(int totalRounds, IReadOnlyList<PartyPlayerState> players)
        {
            foreach (var tab in _roundTabs)
                if (tab) Destroy(tab.gameObject);
            _roundTabs.Clear();

            for (int i = 0; i < totalRounds; i++)
            {
                if (!roundTabPrefab || !roundTabContainer) break;

                var tab = Instantiate(roundTabPrefab, roundTabContainer);
                tab.Initialize(i);
                tab.SetPlayerNames(players);
                _roundTabs.Add(tab);
            }

            _activeRoundIndex = 0;
            SetActiveRound(0);

            // Ready button disabled at start
            SetReadyButtonInteractable(false);
            if (readyButtonText) readyButtonText.text = "WAITING...";

            CSDebug.Log($"[PartyPanel] Initialized: {totalRounds} rounds, {players.Count} players");
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

        public void ForceShow()
        {
            _isVisible = false;
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

                case PartyPhase.MiniGameReady:
                    SetReadyButtonInteractable(true);
                    if (readyButtonText) readyButtonText.text = "READY";
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
                    SetReadyButtonInteractable(true);
                    if (readyButtonText) readyButtonText.text = "NEXT";
                    break;
            }

            CSDebug.Log($"[PartyPanel] Phase → {phase}, button interactable={readyButton && readyButton.interactable}");
        }

        #endregion

        #region Game State Text

        public void SetGameStateText(string text)
        {
            if (gameStateText) gameStateText.text = text;

            if (_activeRoundIndex >= 0 && _activeRoundIndex < _roundTabs.Count)
                _roundTabs[_activeRoundIndex].SetGameStateText(text);
        }

        #endregion

        #region Player Events

        public void OnPlayerJoined(string playerName, Domains domain, bool isAI)
        {
            // Tabs are refreshed when the controller re-initializes via ReinitializePanelWithPlayers
        }

        public void OnPlayerLeft(string playerName)
        {
        }

        /// <summary>
        /// Called when a player readies up. Updates the active round tab's ready count.
        /// </summary>
        public void OnPlayerReadyChanged(string playerName, bool isReady, int readyCount, int totalCount)
        {
            if (_activeRoundIndex >= 0 && _activeRoundIndex < _roundTabs.Count)
                _roundTabs[_activeRoundIndex].SetReadyCount(readyCount, totalCount);
        }

        /// <summary>
        /// Called when ready states are reset between phases (e.g., 1st ready → 2nd ready).
        /// Resets the ready count display on the active round tab.
        /// </summary>
        public void OnReadyStatesReset()
        {
            if (_activeRoundIndex >= 0 && _activeRoundIndex < _roundTabs.Count)
                _roundTabs[_activeRoundIndex].ResetReadyCount();
        }

        #endregion

        #region Round Updates

        public void UpdateRoundResult(int roundIndex, PartyRoundResult result)
        {
            if (roundIndex < 0 || roundIndex >= _roundTabs.Count) return;

            _roundTabs[roundIndex].SetRoundResult(result);

            int nextRound = roundIndex + 1;
            _activeRoundIndex = nextRound;

            for (int i = 0; i < _roundTabs.Count; i++)
                _roundTabs[i].SetActive(i == nextRound);

            ScrollToRound(roundIndex);
        }

        public void SetActiveRound(int roundIndex)
        {
            _activeRoundIndex = roundIndex;
            for (int i = 0; i < _roundTabs.Count; i++)
                _roundTabs[i].SetActive(i == roundIndex);

            ScrollToRound(roundIndex);
        }

        #endregion

        #region Auto-Scroll

        void ScrollToActiveRound() => ScrollToRound(_activeRoundIndex);

        void ScrollToRound(int roundIndex)
        {
            if (!scrollRect || _roundTabs.Count <= 1) return;
            if (roundIndex < 0 || roundIndex >= _roundTabs.Count) return;

            var tab = _roundTabs[roundIndex];
            if (!tab) return;

            Canvas.ForceUpdateCanvases();

            var contentRect = scrollRect.content;
            var viewportRect = scrollRect.viewport ?? (RectTransform)scrollRect.transform;
            if (!contentRect) return;

            var tabRect = (RectTransform)tab.transform;
            float contentHeight = contentRect.rect.height;
            float viewportHeight = viewportRect.rect.height;
            if (contentHeight <= viewportHeight) return;

            float tabLocalY = contentRect.InverseTransformPoint(tabRect.position).y;
            float contentTopY = contentRect.rect.yMax;
            float distanceFromTop = contentTopY - tabLocalY;

            float targetScroll = (distanceFromTop - viewportHeight * 0.5f) / (contentHeight - viewportHeight);
            targetScroll = Mathf.Clamp01(1f - targetScroll);

            scrollRect.verticalNormalizedPosition = targetScroll;
        }

        #endregion

        #region Final Results

        public void OnFinalResults(IReadOnlyList<PartyPlayerState> sortedPlayers)
        {
            ForceShow();
        }

        #endregion

        #region Button Handlers

        void OnReadyClicked()
        {
            CSDebug.Log($"[PartyPanel] Ready clicked. Phase={_currentPhase}, hasController={partyController != null}");

            if (_currentPhase == PartyPhase.FinalResults)
            {
                SetReadyButtonInteractable(false);
                Hide();
                gameData.InvokeShowGameEndScreen();
                return;
            }

            if (_currentPhase != PartyPhase.WaitingForReady &&
                _currentPhase != PartyPhase.RoundResults &&
                _currentPhase != PartyPhase.MiniGameReady)
            {
                CSDebug.Log($"[PartyPanel] Ready clicked in non-ready phase {_currentPhase}, ignoring.");
                return;
            }

            SetReadyButtonInteractable(false);
            if (readyButtonText) readyButtonText.text = "READY!";

            if (partyController)
            {
                partyController.OnLocalPlayerReady();
            }
            else
            {
                CSDebug.LogWarning("[PartyPanel] partyController is null! Cannot send ready.");
            }
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
