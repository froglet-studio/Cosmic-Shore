using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Brief toast notification shown when a new friend is added
    /// (i.e., someone accepted your request or you accepted theirs).
    /// Subscribes to <see cref="FriendsDataSO.OnFriendAdded"/> (SOAP event).
    /// Auto-dismisses after a configurable duration.
    /// </summary>
    public class FriendAddedNotificationUI : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private FriendsDataSO friendsData;

        [Header("UI References")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button dismissButton;

        [Header("Auto-Dismiss")]
        [SerializeField] private float autoDismissSeconds = 5f;

        private float _dismissTimer;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            dismissButton?.onClick.AddListener(Hide);
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (friendsData?.OnFriendAdded != null)
                friendsData.OnFriendAdded.OnRaised += ShowNotification;
        }

        void OnDisable()
        {
            if (friendsData?.OnFriendAdded != null)
                friendsData.OnFriendAdded.OnRaised -= ShowNotification;
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

        private void ShowNotification(FriendData friend)
        {
            _dismissTimer = 0f;

            if (messageText != null)
                messageText.text = $"{friend.DisplayName} is now your friend!";

            gameObject.SetActive(true);
        }

        private void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
