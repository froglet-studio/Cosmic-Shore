using System.Collections.Generic;
using CosmicShore.App.UI.Modals;
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
        [SerializeField] GameObject panelRoot;
        [SerializeField] ModalWindowManager modalWindowManager;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Game State")]
        [SerializeField] TMP_Text gameStateText;

        [Header("Round Tabs")]
        [SerializeField] Transform roundTabContainer;
        [SerializeField] PartyRoundTab roundTabPrefab;

        [Header("Buttons")]
        [SerializeField] Button readyButton;
        [SerializeField] TMP_Text readyButtonText;
        [SerializeField] Button quitButton;
        [SerializeField] Button settingsButton;

        [Header("Final Results")]
        [SerializeField] GameObject finalResultsContainer;
        [SerializeField] List<TMP_Text> finalPlayerNameTexts = new();
        [SerializeField] List<TMP_Text> finalPlayerWinsTexts = new();
        [SerializeField] TMP_Text partyWinnerText;

        readonly List<PartyRoundTab> _roundTabs = new();
        PartyPhase _currentPhase;
        bool _isVisible;

        #region Unity Lifecycle

        void Awake()
        {
            if (finalResultsContainer) finalResultsContainer.SetActive(false);
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

            if (finalResultsContainer)
                finalResultsContainer.SetActive(false);

            SetReadyButtonInteractable(false);
        }

        #endregion

        #region Show / Hide

        public void Show()
        {
            if (_isVisible) return;
            _isVisible = true;

            if (panelRoot) panelRoot.SetActive(true);

            if (canvasGroup)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (modalWindowManager)
                modalWindowManager.ModalWindowIn();
        }

        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false;

            if (modalWindowManager)
                modalWindowManager.ModalWindowOut();

            if (canvasGroup)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (panelRoot) panelRoot.SetActive(false);
        }

        /// <summary>
        /// Forces the panel visible for all players (e.g., during countdown).
        /// </summary>
        public void ForceShow()
        {
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
                    break;

                case PartyPhase.FinalResults:
                    SetReadyButtonInteractable(false);
                    if (readyButtonText) readyButtonText.text = "PARTY OVER";
                    if (finalResultsContainer) finalResultsContainer.SetActive(true);
                    ForceShow();
                    break;
            }
        }

        #endregion

        #region Game State Text

        public void SetGameStateText(string text)
        {
            if (gameStateText) gameStateText.text = text;
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
            int activeRound = GetActiveRoundIndex();
            if (activeRound >= 0 && activeRound < _roundTabs.Count)
                _roundTabs[activeRound].SetPlayerReady(playerName, isReady);
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
            for (int i = 0; i < _roundTabs.Count; i++)
                _roundTabs[i].SetActive(i == nextRound);
        }

        /// <summary>
        /// Sets the active round tab (the one currently being played).
        /// </summary>
        public void SetActiveRound(int roundIndex)
        {
            for (int i = 0; i < _roundTabs.Count; i++)
                _roundTabs[i].SetActive(i == roundIndex);
        }

        #endregion

        #region Final Results

        public void OnFinalResults(IReadOnlyList<PartyPlayerState> sortedPlayers)
        {
            if (finalResultsContainer) finalResultsContainer.SetActive(true);

            for (int i = 0; i < finalPlayerNameTexts.Count; i++)
            {
                if (i < sortedPlayers.Count)
                {
                    if (finalPlayerNameTexts[i])
                        finalPlayerNameTexts[i].text = sortedPlayers[i].PlayerName;
                    if (i < finalPlayerWinsTexts.Count && finalPlayerWinsTexts[i])
                        finalPlayerWinsTexts[i].text = $"{sortedPlayers[i].GamesWon} wins";
                }
                else
                {
                    if (finalPlayerNameTexts[i])
                        finalPlayerNameTexts[i].text = "";
                    if (i < finalPlayerWinsTexts.Count && finalPlayerWinsTexts[i])
                        finalPlayerWinsTexts[i].text = "";
                }
            }

            if (partyWinnerText && sortedPlayers.Count > 0)
                partyWinnerText.text = $"{sortedPlayers[0].PlayerName} wins the party!";

            ForceShow();
        }

        #endregion

        #region Button Handlers

        void OnReadyClicked()
        {
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

        #region Helpers

        int GetActiveRoundIndex()
        {
            if (!partyController) return 0;

            // The active round is the first non-completed tab
            for (int i = 0; i < _roundTabs.Count; i++)
            {
                if (i < partyController.RoundResults.Count && !partyController.RoundResults[i].IsCompleted)
                    return i;
            }
            return _roundTabs.Count - 1;
        }

        #endregion
    }
}
