using CosmicShore.Core;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Panel for sending friend requests by player name.
    /// Provides a text input field and send button.
    /// </summary>
    public class AddFriendPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text feedbackText;

        [Header("SOAP Data")]
        [SerializeField] private FriendsDataSO friendsData;

        [Inject] private FriendsServiceFacade friendsService;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            sendButton?.onClick.AddListener(OnSendPressed);
            closeButton?.onClick.AddListener(Hide);
            nameInputField?.onValueChanged.AddListener(OnInputChanged);
        }

        void OnEnable()
        {
            ClearState();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────────────────────────────

        private void OnInputChanged(string value)
        {
            if (sendButton != null)
                sendButton.interactable = !string.IsNullOrWhiteSpace(value);

            if (feedbackText != null)
                feedbackText.text = "";
        }

        private async void OnSendPressed()
        {
            string playerName = nameInputField?.text?.Trim();
            if (string.IsNullOrEmpty(playerName)) return;

            if (friendsService == null)
            {
                SetFeedback("Friends service not available.", false);
                return;
            }

            if (sendButton != null)
                sendButton.interactable = false;

            try
            {
                await friendsService.SendFriendRequestByNameAsync(playerName);
                SetFeedback($"Request sent to '{playerName}'!", true);
                if (nameInputField != null)
                    nameInputField.text = "";
            }
            catch (Unity.Services.Friends.FriendsServiceException e)
            {
                SetFeedback($"Could not send request: {e.Reason}", false);
            }
            catch (System.Exception e)
            {
                SetFeedback($"Error: {e.Message}", false);
            }
            finally
            {
                if (sendButton != null)
                    sendButton.interactable = !string.IsNullOrWhiteSpace(nameInputField?.text);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void ClearState()
        {
            if (nameInputField != null)
                nameInputField.text = "";

            if (feedbackText != null)
                feedbackText.text = "";

            if (sendButton != null)
                sendButton.interactable = false;
        }

        private void SetFeedback(string message, bool success)
        {
            if (feedbackText == null) return;
            feedbackText.text = message;
            feedbackText.color = success ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);
        }
    }
}
