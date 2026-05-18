using System;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Status overlay rendered above the splash fade during the long
    /// auth → presence-lobby → Relay-session → vessel-spawn chain.
    ///
    /// Surfaces:
    ///   • One line of status text driven by SOAP events.
    ///   • A retry button that becomes visible after the auto-retry loop in
    ///     <see cref="Core.AuthenticationSceneController"/> exhausts its attempts.
    ///   • A reconnect banner when <see cref="HostConnectionDataSO.OnHostConnectionLost"/>
    ///     fires from the background refresh loop.
    ///
    /// Lifetime: DontDestroyOnLoad, mounted on the splash canvas in the
    /// Authentication scene. Persists through the Menu_Main load so a mid-session
    /// disconnect re-uses the same surface.
    ///
    /// Single-writer: this component owns its own visibility. Other systems
    /// communicate via the public <see cref="SetStatus"/>, <see cref="Show"/>,
    /// <see cref="Hide"/>, and <see cref="ShowRetry"/> methods or via the
    /// subscribed SOAP events.
    /// </summary>
    public class BootStatusPanel : MonoBehaviour
    {
        public static BootStatusPanel Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button retryButton;

        [Header("SOAP")]
        [SerializeField] private AuthenticationDataVariable authData;
        [SerializeField] private HostConnectionDataSO connectionData;
        [Inject] private GameDataSO _gameData;

        [Header("Labels")]
        [SerializeField] private string labelConnecting        = "Connecting…";
        [SerializeField] private string labelJoiningLobby      = "Joining lobby…";
        [SerializeField] private string labelCreatingSession   = "Creating session…";
        [SerializeField] private string labelHostReady         = "Host ready…";
        [SerializeField] private string labelVesselReady       = "Vessel ready";
        [SerializeField] private string labelConnectionLost    = "Connection lost. Tap retry.";

        private Action _retryAction;
        private bool   _hostReadyReached;

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            HideRetryButton();
            SetStatus(labelConnecting);
            Show();
        }

        void OnEnable()
        {
            var signedInEvent = authData?.Value?.OnSignedIn;
            if (signedInEvent != null)
                signedInEvent.OnRaised += HandleSignedIn;

            if (connectionData != null)
            {
                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised += HandleHostConnectionEstablished;
                if (connectionData.OnHostConnectionLost != null)
                    connectionData.OnHostConnectionLost.OnRaised += HandleHostConnectionLost;
            }

            if (retryButton != null)
                retryButton.onClick.AddListener(HandleRetryClicked);
        }

        void OnDisable()
        {
            var signedInEvent = authData?.Value?.OnSignedIn;
            if (signedInEvent != null)
                signedInEvent.OnRaised -= HandleSignedIn;

            if (connectionData != null)
            {
                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised -= HandleHostConnectionEstablished;
                if (connectionData.OnHostConnectionLost != null)
                    connectionData.OnHostConnectionLost.OnRaised -= HandleHostConnectionLost;
            }

            if (retryButton != null)
                retryButton.onClick.RemoveListener(HandleRetryClicked);

            UnsubscribeFromClientReady();
        }

        void Start()
        {
            // GameDataSO arrives via [Inject] which is populated between Awake and Start.
            SubscribeToClientReady();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public void SetStatus(string text)
        {
            if (statusText != null && !string.IsNullOrEmpty(text))
                statusText.text = text;
        }

        public void Show()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            HideRetryButton();
        }

        /// <summary>
        /// Surface the retry button with a label and a callback. Used by the
        /// AuthenticationSceneController when its auto-retry loop is exhausted.
        /// </summary>
        public void ShowRetry(string statusLabel, Action onRetry)
        {
            _retryAction = onRetry;
            if (!string.IsNullOrEmpty(statusLabel))
                SetStatus(statusLabel);
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(true);
                retryButton.interactable = true;
            }
            Show();
        }

        public void HideRetryButton()
        {
            _retryAction = null;
            if (retryButton != null)
                retryButton.gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOAP event handlers
        // ─────────────────────────────────────────────────────────────────────

        private void HandleSignedIn() => SetStatus(labelJoiningLobby);

        private void HandleHostConnectionEstablished()
        {
            // OnHostConnectionEstablished fires twice: at lobby join (NM not
            // listening) and after Relay session create (NM listening). The
            // first transition shows "Creating session…", the second shows
            // "Host ready" until OnClientReady hides the panel.
            if (!_hostReadyReached &&
                Unity.Netcode.NetworkManager.Singleton is { IsListening: true })
            {
                _hostReadyReached = true;
                SetStatus(labelHostReady);
            }
            else
            {
                SetStatus(labelCreatingSession);
            }
        }

        private void HandleHostConnectionLost()
        {
            // Background-loop disconnect: surface the retry path and re-show
            // the panel if a previous Hide() had taken it down.
            _hostReadyReached = false;
            ShowRetry(labelConnectionLost, RetryReconnectAsync);
        }

        private void HandleClientReady()
        {
            SetStatus(labelVesselReady);
            Hide();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Retry plumbing
        // ─────────────────────────────────────────────────────────────────────

        private void HandleRetryClicked()
        {
            if (retryButton != null)
                retryButton.interactable = false;

            var action = _retryAction;
            _retryAction = null;

            if (action == null)
                action = RetryReconnectAsync;

            try { action(); }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[BootStatusPanel] Retry callback threw: {e.Message}");
                if (retryButton != null)
                    retryButton.interactable = true;
            }
        }

        private void RetryReconnectAsync()
        {
            SetStatus(labelCreatingSession);
            RunRetryAsync().Forget();
        }

        private async UniTaskVoid RunRetryAsync()
        {
            try
            {
                var hcs = HostConnectionService.Instance;
                if (hcs == null)
                {
                    CSDebug.LogWarning("[BootStatusPanel] No HostConnectionService instance to retry against.");
                    return;
                }
                await hcs.RetryCreateOwnPartySessionAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException) { /* scene destroyed */ }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[BootStatusPanel] Retry failed: {e.Message}");
                ShowRetry(labelConnectionLost, RetryReconnectAsync);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // OnClientReady subscription
        // ─────────────────────────────────────────────────────────────────────

        private bool _clientReadySubscribed;

        private void SubscribeToClientReady()
        {
            if (_clientReadySubscribed || _gameData?.OnClientReady == null) return;
            _gameData.OnClientReady.OnRaised += HandleClientReady;
            _clientReadySubscribed = true;
        }

        private void UnsubscribeFromClientReady()
        {
            if (!_clientReadySubscribed || _gameData?.OnClientReady == null) return;
            _gameData.OnClientReady.OnRaised -= HandleClientReady;
            _clientReadySubscribed = false;
        }
    }
}
