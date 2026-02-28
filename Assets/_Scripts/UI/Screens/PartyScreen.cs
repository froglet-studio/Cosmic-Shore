using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Main Party screen in the Menu_Main scene.
    /// Implements <see cref="IScreen"/> so <see cref="ScreenSwitcher"/> can
    /// notify it on enter/exit transitions.
    ///
    /// Composes the existing party components:
    ///   - <see cref="PartyAreaPanel"/> (3-slot party display)
    ///   - <see cref="OnlinePlayersPanel"/> (online player list modal)
    ///   - <see cref="FriendsPanel"/> (friends management modal)
    ///   - <see cref="PartyInviteNotificationPanel"/> (incoming invite banner)
    ///
    /// Also displays a party status summary and provides buttons to open
    /// the online players and friends panels.
    /// </summary>
    public class PartyScreen : MonoBehaviour, IScreen
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;
        [SerializeField] private FriendsDataSO friendsData;

        [Header("Party Slots")]
        [Tooltip("The reusable PartyAreaPanel showing local player + remote member slots.")]
        [SerializeField] private PartyAreaPanel partyAreaPanel;

        [Header("Sub-Panels")]
        [SerializeField] private OnlinePlayersPanel onlinePlayersPanel;
        [SerializeField] private FriendsPanel friendsPanel;
        [SerializeField] private PartyInviteNotificationPanel inviteNotificationPanel;

        [Header("Buttons")]
        [SerializeField] private Button findPlayersButton;
        [SerializeField] private Button friendsButton;
        [SerializeField] private Button refreshButton;

        [Header("Status Display")]
        [SerializeField] private TMP_Text partyStatusText;
        [SerializeField] private TMP_Text onlineCountText;
        [SerializeField] private TMP_Text friendsRequestBadge;

        [Header("Connection State")]
        [SerializeField] private GameObject connectedIndicator;
        [SerializeField] private GameObject disconnectedIndicator;

        [Inject] private FriendsServiceFacade friendsService;

        private bool _viewLoaded;

        // ─────────────────────────────────────────────────────────────────────
        // IScreen
        // ─────────────────────────────────────────────────────────────────────

        public void OnScreenEnter()
        {
            LoadView();
        }

        public void OnScreenExit()
        {
            // Hide sub-panels when navigating away
            onlinePlayersPanel?.Hide();
            friendsPanel?.Hide();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            findPlayersButton?.onClick.AddListener(OnFindPlayersPressed);
            friendsButton?.onClick.AddListener(OnFriendsPressed);
            refreshButton?.onClick.AddListener(OnRefreshPressed);
        }

        void Start()
        {
            SubscribeToEvents();
        }

        void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        // ─────────────────────────────────────────────────────────────────────
        // View Loading
        // ─────────────────────────────────────────────────────────────────────

        private void LoadView()
        {
            RefreshStatusDisplay();
            RefreshOnlineCount();
            RefreshFriendsBadge();
            RefreshConnectionIndicator();
            _viewLoaded = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Button Handlers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the <see cref="OnlinePlayersPanel"/> to browse and invite online players.
        /// </summary>
        public void OnFindPlayersPressed()
        {
            onlinePlayersPanel?.Show();
        }

        /// <summary>
        /// Opens the <see cref="FriendsPanel"/> for managing friends and requests.
        /// </summary>
        public void OnFriendsPressed()
        {
            friendsPanel?.Show();
        }

        /// <summary>
        /// Refreshes the friends list from the service and updates the UI.
        /// </summary>
        public void OnRefreshPressed()
        {
            if (friendsService == null) return;

            if (refreshButton != null)
                refreshButton.interactable = false;

            RefreshAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOAP Event Subscriptions
        // ─────────────────────────────────────────────────────────────────────

        private void SubscribeToEvents()
        {
            if (connectionData != null)
            {
                if (connectionData.PartyMembers != null)
                    connectionData.PartyMembers.OnItemCountChanged += OnPartyMemberCountChanged;

                if (connectionData.OnlinePlayers != null)
                    connectionData.OnlinePlayers.OnItemCountChanged += OnOnlinePlayerCountChanged;

                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised += OnConnectionChanged;

                if (connectionData.OnHostConnectionLost != null)
                    connectionData.OnHostConnectionLost.OnRaised += OnConnectionChanged;
            }

            if (friendsData?.IncomingRequests != null)
                friendsData.IncomingRequests.OnItemCountChanged += OnFriendRequestCountChanged;
        }

        private void UnsubscribeFromEvents()
        {
            if (connectionData != null)
            {
                if (connectionData.PartyMembers != null)
                    connectionData.PartyMembers.OnItemCountChanged -= OnPartyMemberCountChanged;

                if (connectionData.OnlinePlayers != null)
                    connectionData.OnlinePlayers.OnItemCountChanged -= OnOnlinePlayerCountChanged;

                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised -= OnConnectionChanged;

                if (connectionData.OnHostConnectionLost != null)
                    connectionData.OnHostConnectionLost.OnRaised -= OnConnectionChanged;
            }

            if (friendsData?.IncomingRequests != null)
                friendsData.IncomingRequests.OnItemCountChanged -= OnFriendRequestCountChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOAP Callbacks
        // ─────────────────────────────────────────────────────────────────────

        private void OnPartyMemberCountChanged()
        {
            RefreshStatusDisplay();
        }

        private void OnOnlinePlayerCountChanged()
        {
            RefreshOnlineCount();
        }

        private void OnConnectionChanged()
        {
            RefreshConnectionIndicator();
            RefreshStatusDisplay();
            RefreshOnlineCount();
        }

        private void OnFriendRequestCountChanged()
        {
            RefreshFriendsBadge();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Display Updates
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshStatusDisplay()
        {
            if (partyStatusText == null) return;

            int memberCount = connectionData?.PartyMembers?.Count ?? 0;
            int maxSlots = connectionData?.MaxPartySlots ?? 4;

            if (memberCount <= 1)
                partyStatusText.text = "Solo - Invite players to form a party";
            else
                partyStatusText.text = $"Party: {memberCount}/{maxSlots} players";
        }

        private void RefreshOnlineCount()
        {
            if (onlineCountText == null) return;

            int count = connectionData?.OnlinePlayers?.Count ?? 0;
            onlineCountText.text = count > 0 ? $"{count} Online" : "No players online";
        }

        private void RefreshFriendsBadge()
        {
            if (friendsRequestBadge == null) return;

            int count = friendsData?.IncomingRequestCount ?? 0;
            friendsRequestBadge.text = count > 0 ? count.ToString() : "";
            friendsRequestBadge.gameObject.SetActive(count > 0);
        }

        private void RefreshConnectionIndicator()
        {
            bool connected = connectionData != null && connectionData.IsConnected;

            if (connectedIndicator != null)
                connectedIndicator.SetActive(connected);

            if (disconnectedIndicator != null)
                disconnectedIndicator.SetActive(!connected);

            // Disable find players button if not connected
            if (findPlayersButton != null)
                findPlayersButton.interactable = connected;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private async void RefreshAsync()
        {
            try
            {
                await friendsService.RefreshAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PartyScreen] Refresh error: {e.Message}");
            }
            finally
            {
                if (refreshButton != null)
                    refreshButton.interactable = true;
            }
        }
    }
}
