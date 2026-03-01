using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drives the Party Area on the left side of the Arcade Panel.
    /// Renders 4 <see cref="PartySlotView"/> slots sourced from
    /// <see cref="HostConnectionDataSO"/>. Slot 0 is always the local player.
    /// Empty slots show a "+" button that opens the <see cref="OnlinePlayersPanel"/>.
    ///
    /// All sub-panels (OnlinePlayersPanel, FriendsPanel, PartyInviteNotificationPanel)
    /// are children of the PartyArea GameObject in the hierarchy.
    ///
    /// Button OnClick wiring (inspector or code):
    ///   - Each PartySlotView "+" addButton → <see cref="OnAddSlotPressed"/> (via Initialize callback)
    ///   - Friends button → <see cref="OnFriendsPressed"/>
    ///   - Refresh button → <see cref="OnRefreshPressed"/>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PartyArcadeView : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;
        [SerializeField] private FriendsDataSO friendsData;

        [Header("Slots (4 pre-placed in hierarchy)")]
        [Tooltip("Pre-placed slot views (index 0 = local player, 1-3 = remote/empty).")]
        [SerializeField] private List<PartySlotView> partySlots;

        [Header("Sub-Panels (children of PartyArea)")]
        [SerializeField] private OnlinePlayersPanel onlinePlayersPanel;
        [SerializeField] private FriendsPanel friendsPanel;
        [SerializeField] private PartyInviteNotificationPanel inviteNotificationPanel;

        [Header("Buttons")]
        [SerializeField] private Button friendsButton;
        [SerializeField] private Button refreshButton;

        [Header("Status Display")]
        [SerializeField] private TMP_Text friendsRequestBadge;
        [SerializeField] private TMP_Text partyStatusText;

        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Inject] private PlayerDataService playerDataService;

        private CanvasGroup _canvasGroup;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            foreach (var slot in partySlots)
                slot.Initialize(OnAddSlotPressed);

            friendsButton?.onClick.AddListener(OnFriendsPressed);
            refreshButton?.onClick.AddListener(OnRefreshPressed);
        }

        void Start()
        {
            if (connectionData != null)
            {
                if (connectionData.PartyMembers != null)
                {
                    connectionData.PartyMembers.OnItemAdded += OnPartyMemberAdded;
                    connectionData.PartyMembers.OnItemRemoved += OnPartyMemberRemoved;
                    connectionData.PartyMembers.OnCleared += OnPartyCleared;
                }

                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised += RefreshAllSlots;

                if (connectionData.OnPartyJoinCompleted != null)
                    connectionData.OnPartyJoinCompleted.OnRaised += RefreshAllSlots;
            }

            if (friendsData?.IncomingRequests != null)
                friendsData.IncomingRequests.OnItemCountChanged += UpdateFriendsBadge;

            if (playerDataService != null)
                playerDataService.OnProfileChanged += OnLocalProfileChanged;

            RefreshAllSlots();
            UpdateFriendsBadge();
            UpdatePartyStatus();
        }

        void OnDisable()
        {
            if (connectionData != null)
            {
                if (connectionData.PartyMembers != null)
                {
                    connectionData.PartyMembers.OnItemAdded -= OnPartyMemberAdded;
                    connectionData.PartyMembers.OnItemRemoved -= OnPartyMemberRemoved;
                    connectionData.PartyMembers.OnCleared -= OnPartyCleared;
                }

                if (connectionData.OnHostConnectionEstablished != null)
                    connectionData.OnHostConnectionEstablished.OnRaised -= RefreshAllSlots;

                if (connectionData.OnPartyJoinCompleted != null)
                    connectionData.OnPartyJoinCompleted.OnRaised -= RefreshAllSlots;
            }

            if (friendsData?.IncomingRequests != null)
                friendsData.IncomingRequests.OnItemCountChanged -= UpdateFriendsBadge;

            if (playerDataService != null)
                playerDataService.OnProfileChanged -= OnLocalProfileChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public — Slot Refresh
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Full rebuild of all 4 slot visuals from <see cref="HostConnectionDataSO.PartyMembers"/>.
        /// Slot 0 = local player, slots 1-3 = remote party members or empty "+" buttons.
        /// </summary>
        public void RefreshAllSlots()
        {
            RefreshLocalPlayerSlot();

            int memberIndex = 0;
            for (int slotIndex = 1; slotIndex < partySlots.Count; slotIndex++)
            {
                var slot = partySlots[slotIndex];

                PartyPlayerData? remoteMember = null;
                while (memberIndex < (connectionData?.PartyMembers?.Count ?? 0))
                {
                    var candidate = connectionData.PartyMembers[memberIndex];
                    memberIndex++;

                    if (candidate.PlayerId == connectionData?.LocalPlayerId)
                        continue;

                    remoteMember = candidate;
                    break;
                }

                if (remoteMember.HasValue)
                    slot.SetPlayer(remoteMember.Value);
                else
                    slot.ClearSlot();
            }

            UpdatePartyStatus();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public — Button Handlers (wire via OnClick in inspector)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called when a "+" button is pressed on any empty slot.
        /// Transitions to Relay host if needed, then opens the online players panel.
        /// Wired automatically via <see cref="PartySlotView.Initialize"/> in Awake.
        /// Can also be wired manually via inspector OnClick on each slot's addButton.
        /// </summary>
        public async void OnAddSlotPressed()
        {
            if (connectionData != null && !connectionData.HasOpenSlots)
            {
                Debug.Log("[PartyArcadeView] No open party slots.");
                return;
            }

            // Transition to Relay host before opening the invite panel
            // so invited clients can actually connect.
            var controller = PartyInviteController.Instance;
            if (controller != null && connectionData is { IsHost: true })
            {
                await controller.TransitionToPartyHostAsync();
            }

            onlinePlayersPanel?.Show();
        }

        /// <summary>
        /// Opens the <see cref="FriendsPanel"/>.
        /// Wire the Friends button OnClick to this method.
        /// </summary>
        public void OnFriendsPressed()
        {
            friendsPanel?.Show();
        }

        /// <summary>
        /// Refreshes all party data from SOAP sources.
        /// Wire the Refresh button OnClick to this method.
        /// </summary>
        public void OnRefreshPressed()
        {
            RefreshAllSlots();
            UpdateFriendsBadge();
        }

        /// <summary>
        /// Shows the PartyArea and all its child sub-panels.
        /// </summary>
        public void Show()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            SetCanvasGroupVisible(true);
        }

        /// <summary>
        /// Hides the PartyArea and all its child sub-panels.
        /// </summary>
        public void Hide()
        {
            onlinePlayersPanel?.Hide();
            friendsPanel?.Hide();
            SetCanvasGroupVisible(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOAP Callbacks
        // ─────────────────────────────────────────────────────────────────────

        private void OnPartyMemberAdded(PartyPlayerData member)
        {
            RefreshAllSlots();
        }

        private void OnPartyMemberRemoved(PartyPlayerData member)
        {
            RefreshAllSlots();
        }

        private void OnPartyCleared()
        {
            foreach (var slot in partySlots)
                slot.ClearSlot();
            UpdatePartyStatus();
        }

        private void OnLocalProfileChanged(PlayerProfileData profileData)
        {
            RefreshLocalPlayerSlot();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Display Updates
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateFriendsBadge()
        {
            if (friendsRequestBadge == null) return;

            int count = friendsData?.IncomingRequestCount ?? 0;
            friendsRequestBadge.text = count > 0 ? count.ToString() : "";
            friendsRequestBadge.gameObject.SetActive(count > 0);
        }

        private void UpdatePartyStatus()
        {
            if (partyStatusText == null) return;

            int memberCount = connectionData?.PartyMembers?.Count ?? 0;
            int maxSlots = partySlots.Count;

            if (memberCount <= 1)
                partyStatusText.text = "Invite players to your party";
            else
                partyStatusText.text = $"Party: {memberCount}/{maxSlots}";
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshLocalPlayerSlot()
        {
            if (partySlots.Count == 0) return;

            var slot0 = partySlots[0];

            if (connectionData != null && !string.IsNullOrEmpty(connectionData.LocalPlayerId))
            {
                slot0.SetPlayer(connectionData.LocalPlayerData);
            }
            else if (playerDataService?.CurrentProfile != null)
            {
                var profile = playerDataService.CurrentProfile;
                slot0.SetPlayer(new PartyPlayerData(
                    profile.userId, profile.displayName, profile.avatarId));
            }
        }

        private void SetCanvasGroupVisible(bool visible)
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
    }
}
