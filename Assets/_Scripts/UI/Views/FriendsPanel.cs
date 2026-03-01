using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Main friends panel with tabbed navigation: Friends List, Requests, Add Friend.
    /// Reads from <see cref="FriendsDataSO"/> (SOAP) and delegates actions to
    /// <see cref="FriendsServiceFacade"/> and <see cref="HostConnectionService"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FriendsPanel : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("SOAP Data")]
        [SerializeField] private FriendsDataSO friendsData;
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("Tab Buttons")]
        [SerializeField] private Button friendsTabButton;
        [SerializeField] private Button requestsTabButton;
        [SerializeField] private Button addFriendTabButton;

        [Header("Tab Content Panels")]
        [SerializeField] private GameObject friendsListContent;
        [SerializeField] private GameObject requestsListContent;
        [SerializeField] private AddFriendPanel addFriendContent;

        [Header("Friends List")]
        [SerializeField] private GameObject friendEntryPrefab;
        [SerializeField] private Transform friendsContainer;
        [SerializeField] private GameObject friendsEmptyState;

        [Header("Requests List")]
        [SerializeField] private GameObject friendRequestEntryPrefab;
        [SerializeField] private Transform requestsContainer;
        [SerializeField] private GameObject requestsEmptyState;

        [Header("Header")]
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text requestsBadge;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button refreshButton;

        [Inject] private FriendsServiceFacade friendsService;

        // ─────────────────────────────────────────────────────────────────────
        // Internal State
        // ─────────────────────────────────────────────────────────────────────

        private readonly List<FriendEntryView> _friendEntries = new();
        private readonly List<FriendRequestEntryView> _requestEntries = new();
        private int _activeTab; // 0=friends, 1=requests, 2=addFriend
        private CanvasGroup _canvasGroup;
        private CanvasGroup _friendsListCG;
        private CanvasGroup _requestsListCG;
        private CanvasGroup _addFriendCG;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            friendsTabButton?.onClick.AddListener(() => SwitchTab(0));
            requestsTabButton?.onClick.AddListener(() => SwitchTab(1));
            addFriendTabButton?.onClick.AddListener(() => SwitchTab(2));
            closeButton?.onClick.AddListener(Hide);
            refreshButton?.onClick.AddListener(OnRefreshPressed);
        }

        void OnEnable()
        {
            if (friendsData != null)
            {
                if (friendsData.Friends != null)
                {
                    friendsData.Friends.OnItemAdded += OnFriendAdded;
                    friendsData.Friends.OnItemRemoved += OnFriendRemoved;
                    friendsData.Friends.OnCleared += OnFriendsCleared;
                }

                if (friendsData.IncomingRequests != null)
                {
                    friendsData.IncomingRequests.OnItemAdded += OnRequestAdded;
                    friendsData.IncomingRequests.OnItemRemoved += OnRequestRemoved;
                    friendsData.IncomingRequests.OnCleared += OnRequestsCleared;
                }

                if (friendsData.OutgoingRequests != null)
                {
                    friendsData.OutgoingRequests.OnCleared += OnRequestsCleared;
                }
            }
        }

        void OnDisable()
        {
            if (friendsData != null)
            {
                if (friendsData.Friends != null)
                {
                    friendsData.Friends.OnItemAdded -= OnFriendAdded;
                    friendsData.Friends.OnItemRemoved -= OnFriendRemoved;
                    friendsData.Friends.OnCleared -= OnFriendsCleared;
                }

                if (friendsData.IncomingRequests != null)
                {
                    friendsData.IncomingRequests.OnItemAdded -= OnRequestAdded;
                    friendsData.IncomingRequests.OnItemRemoved -= OnRequestRemoved;
                    friendsData.IncomingRequests.OnCleared -= OnRequestsCleared;
                }

                if (friendsData.OutgoingRequests != null)
                {
                    friendsData.OutgoingRequests.OnCleared -= OnRequestsCleared;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Show()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            SetCanvasGroupVisible(true);
            SwitchTab(0);
            RebuildFriendsList();
            RebuildRequestsList();
            UpdateBadge();
        }

        public void Hide()
        {
            SetCanvasGroupVisible(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tab Navigation
        // ─────────────────────────────────────────────────────────────────────

        private void SwitchTab(int tabIndex)
        {
            _activeTab = tabIndex;

            EnsureTabCanvasGroups();
            SetTabCanvasGroupVisible(_friendsListCG, tabIndex == 0);
            SetTabCanvasGroupVisible(_requestsListCG, tabIndex == 1);
            SetTabCanvasGroupVisible(_addFriendCG, tabIndex == 2);

            SetTabSelected(friendsTabButton, tabIndex == 0);
            SetTabSelected(requestsTabButton, tabIndex == 1);
            SetTabSelected(addFriendTabButton, tabIndex == 2);

            if (headerText != null)
            {
                headerText.text = tabIndex switch
                {
                    0 => "Friends",
                    1 => "Requests",
                    2 => "Add Friend",
                    _ => "Friends"
                };
            }
        }

        private void EnsureTabCanvasGroups()
        {
            if (_friendsListCG == null && friendsListContent != null)
            {
                if (!friendsListContent.TryGetComponent(out _friendsListCG))
                    _friendsListCG = friendsListContent.AddComponent<CanvasGroup>();
            }
            if (_requestsListCG == null && requestsListContent != null)
            {
                if (!requestsListContent.TryGetComponent(out _requestsListCG))
                    _requestsListCG = requestsListContent.AddComponent<CanvasGroup>();
            }
            if (_addFriendCG == null && addFriendContent != null)
            {
                if (!addFriendContent.TryGetComponent(out _addFriendCG))
                    _addFriendCG = addFriendContent.gameObject.AddComponent<CanvasGroup>();
            }
        }

        private static void SetTabCanvasGroupVisible(CanvasGroup cg, bool visible)
        {
            if (cg == null) return;
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable = visible;
        }

        private static void SetTabSelected(Button button, bool selected)
        {
            if (button == null) return;

            var colors = button.colors;
            colors.normalColor = selected ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 1f, 1f, 0.1f);
            button.colors = colors;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Friends List
        // ─────────────────────────────────────────────────────────────────────

        private void RebuildFriendsList()
        {
            ClearFriendEntries();

            if (friendsData?.Friends == null || friendsData.Friends.Count == 0)
            {
                friendsEmptyState?.SetActive(true);
                return;
            }

            friendsEmptyState?.SetActive(false);

            foreach (var friend in friendsData.Friends)
                SpawnFriendEntry(friend);
        }

        private void SpawnFriendEntry(FriendData data)
        {
            if (friendEntryPrefab == null || friendsContainer == null) return;

            var go = Instantiate(friendEntryPrefab, friendsContainer);
            var entry = go.GetComponent<FriendEntryView>();
            if (entry == null) return;

            entry.Populate(data, OnInviteFriendToParty, OnRemoveFriend);
            _friendEntries.Add(entry);
        }

        private void ClearFriendEntries()
        {
            foreach (var entry in _friendEntries)
                Destroy(entry.gameObject);
            _friendEntries.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Requests List
        // ─────────────────────────────────────────────────────────────────────

        private void RebuildRequestsList()
        {
            ClearRequestEntries();

            bool hasRequests = false;

            // Incoming requests
            if (friendsData?.IncomingRequests != null)
            {
                foreach (var req in friendsData.IncomingRequests)
                {
                    SpawnIncomingRequestEntry(req);
                    hasRequests = true;
                }
            }

            // Outgoing requests
            if (friendsData?.OutgoingRequests != null)
            {
                foreach (var req in friendsData.OutgoingRequests)
                {
                    SpawnOutgoingRequestEntry(req);
                    hasRequests = true;
                }
            }

            requestsEmptyState?.SetActive(!hasRequests);
        }

        private void SpawnIncomingRequestEntry(FriendData data)
        {
            if (friendRequestEntryPrefab == null || requestsContainer == null) return;

            var go = Instantiate(friendRequestEntryPrefab, requestsContainer);
            var entry = go.GetComponent<FriendRequestEntryView>();
            if (entry == null) return;

            entry.PopulateIncoming(data, OnAcceptRequest, OnDeclineRequest);
            _requestEntries.Add(entry);
        }

        private void SpawnOutgoingRequestEntry(FriendData data)
        {
            if (friendRequestEntryPrefab == null || requestsContainer == null) return;

            var go = Instantiate(friendRequestEntryPrefab, requestsContainer);
            var entry = go.GetComponent<FriendRequestEntryView>();
            if (entry == null) return;

            entry.PopulateOutgoing(data, OnCancelRequest);
            _requestEntries.Add(entry);
        }

        private void ClearRequestEntries()
        {
            foreach (var entry in _requestEntries)
                Destroy(entry.gameObject);
            _requestEntries.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOAP Callbacks
        // ─────────────────────────────────────────────────────────────────────

        private void OnFriendAdded(FriendData _) => RebuildFriendsList();
        private void OnFriendRemoved(FriendData _) => RebuildFriendsList();
        private void OnFriendsCleared() => RebuildFriendsList();
        private void OnRequestAdded(FriendData _) { RebuildRequestsList(); UpdateBadge(); }
        private void OnRequestRemoved(FriendData _) { RebuildRequestsList(); UpdateBadge(); }
        private void OnRequestsCleared() { RebuildRequestsList(); UpdateBadge(); }

        // ─────────────────────────────────────────────────────────────────────
        // Action Handlers
        // ─────────────────────────────────────────────────────────────────────

        private async void OnInviteFriendToParty(FriendData friend)
        {
            // Send a party invite via the existing presence lobby invite system
            if (HostConnectionService.Instance == null) return;
            await HostConnectionService.Instance.SendInviteAsync(friend.PlayerId);
        }

        private async void OnRemoveFriend(FriendData friend)
        {
            if (friendsService == null) return;

            try
            {
                await friendsService.RemoveFriendAsync(friend.PlayerId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FriendsPanel] Remove friend error: {e.Message}");
            }
        }

        private async void OnAcceptRequest(FriendData request)
        {
            if (friendsService == null) return;

            try
            {
                await friendsService.AcceptFriendRequestAsync(request.PlayerId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FriendsPanel] Accept request error: {e.Message}");
            }
        }

        private async void OnDeclineRequest(FriendData request)
        {
            if (friendsService == null) return;

            try
            {
                await friendsService.DeclineFriendRequestAsync(request.PlayerId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FriendsPanel] Decline request error: {e.Message}");
            }
        }

        private async void OnCancelRequest(FriendData request)
        {
            if (friendsService == null) return;

            try
            {
                await friendsService.CancelFriendRequestAsync(request.PlayerId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FriendsPanel] Cancel request error: {e.Message}");
            }
        }

        private async void OnRefreshPressed()
        {
            if (friendsService == null) return;

            if (refreshButton != null)
                refreshButton.interactable = false;

            try
            {
                await friendsService.RefreshAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FriendsPanel] Refresh error: {e.Message}");
            }
            finally
            {
                if (refreshButton != null)
                    refreshButton.interactable = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateBadge()
        {
            if (requestsBadge == null) return;

            int count = friendsData?.IncomingRequestCount ?? 0;
            requestsBadge.text = count > 0 ? count.ToString() : "";
            requestsBadge.gameObject.SetActive(count > 0);
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
