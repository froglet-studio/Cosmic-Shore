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
    /// Toast notification for incoming friend requests.
    /// Subscribes to <see cref="FriendsDataSO.OnFriendRequestReceived"/> (SOAP event)
    /// and displays a popup with accept/decline actions.
    /// </summary>
    public class FriendRequestNotificationUI : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private FriendsDataSO friendsData;

        [Header("UI References")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private Button dismissButton;

        [Header("Auto-Dismiss")]
        [Tooltip("Seconds before the notification auto-hides. 0 = no auto-dismiss.")]
        [SerializeField] private float autoDismissSeconds = 10f;

        [Inject] private FriendsServiceFacade friendsService;

        private FriendData _pendingRequest;
        private float _dismissTimer;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            acceptButton?.onClick.AddListener(OnAccept);
            declineButton?.onClick.AddListener(OnDecline);
            dismissButton?.onClick.AddListener(Hide);
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (friendsData?.OnFriendRequestReceived != null)
                friendsData.OnFriendRequestReceived.OnRaised += ShowNotification;
        }

        void OnDisable()
        {
            if (friendsData?.OnFriendRequestReceived != null)
                friendsData.OnFriendRequestReceived.OnRaised -= ShowNotification;
        }

        void Update()
        {
            if (autoDismissSeconds <= 0f) return;

            _dismissTimer += Time.unscaledDeltaTime;
            if (_dismissTimer >= autoDismissSeconds)
                Hide();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Show / Hide
        // ─────────────────────────────────────────────────────────────────────

        private void ShowNotification(FriendData request)
        {
            _pendingRequest = request;
            _dismissTimer = 0f;

            if (messageText != null)
                messageText.text = $"{request.DisplayName} sent you a friend request!";

            SetButtonsInteractable(true);
            gameObject.SetActive(true);
        }

        private void Hide()
        {
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Button Handlers
        // ─────────────────────────────────────────────────────────────────────

        private async void OnAccept()
        {
            SetButtonsInteractable(false);

            if (friendsService != null)
            {
                try
                {
                    await friendsService.AcceptFriendRequestAsync(_pendingRequest.PlayerId);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[FriendRequestNotificationUI] Accept error: {e.Message}");
                }
            }

            Hide();
        }

        private async void OnDecline()
        {
            SetButtonsInteractable(false);

            if (friendsService != null)
            {
                try
                {
                    await friendsService.DeclineFriendRequestAsync(_pendingRequest.PlayerId);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[FriendRequestNotificationUI] Decline error: {e.Message}");
                }
            }

            Hide();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void SetButtonsInteractable(bool interactable)
        {
            if (acceptButton != null) acceptButton.interactable = interactable;
            if (declineButton != null) declineButton.interactable = interactable;
        }
    }
}
