using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Displays popup messages, reusing a pool of <see cref="PopupPanel"/> prefabs.
    /// Two surfaces:
    ///  • Legacy static API <see cref="ShowPopupPanel"/> — single-button info popups (stacked, offset).
    ///  • SOAP <see cref="PopupChannel"/> — generic 1/2-button popups (placement, modal, auto-hide,
    ///    latest-wins via ReplaceKey). This is the single subscriber/presenter for the channel.
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        private const float OFFSET = 30;
        private const float MAX_OFFSET = 200;

        [SerializeField]
        private GameObject _popupPanelPrefab;

        [SerializeField]
        private NotificationUI _notificationUI;

        [Header("SOAP")]
        [Tooltip("Popup request/dismiss channel. Raise requests on this from anywhere; this manager presents them.")]
        [SerializeField]
        private PopupChannel _channel;

        private List<PopupPanel> _popupPanels = new List<PopupPanel>();

        private static PopupManager _instance;

        private void Awake()
        {
            if (_instance != null)
            {
                CSDebug.LogWarning("PopupManager Invalid State, instance already exists");
                return;
            }

            _instance = this;            // since DontdestroyOnLoad only works on root objects, we need to make sure the canvas is a root object
        }

        private void OnEnable()
        {
            if (_channel != null)
            {
                _channel.OnPopupRequested += HandleRequest;
                _channel.OnDismissRequested += HandleDismiss;
            }
        }

        private void OnDisable()
        {
            if (_channel != null)
            {
                _channel.OnPopupRequested -= HandleRequest;
                _channel.OnDismissRequested -= HandleDismiss;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── SOAP presenter ────────────────────────────────────────────────────

        private void HandleRequest(PopupRequest request)
        {
            // Latest-wins: a request carrying a ReplaceKey reuses the panel already showing that
            // key (so a newer invite instantly takes over the same slot instead of stacking).
            PopupPanel panel = null;
            if (!string.IsNullOrEmpty(request.ReplaceKey))
                panel = _popupPanels.Find(p => p != null && p.IsDisplaying && p.ReplaceKey == request.ReplaceKey);

            if (panel == null)
                panel = AcquireForRequest();

            if (panel == null)
            {
                CSDebug.LogError("[PopupManager] No popup panel prefab assigned — cannot present popup.");
                return;
            }

            panel.Setup(request);
        }

        private void HandleDismiss(string replaceKey)
        {
            if (string.IsNullOrEmpty(replaceKey)) return;
            var panel = _popupPanels.Find(p => p != null && p.IsDisplaying && p.ReplaceKey == replaceKey);
            if (panel != null) panel.HideSilently();
        }

        /// <summary>Pool a panel for a SOAP request and reset its root to fill the parent so the
        /// card's own placement (centre / bottom-left) is the only thing positioning it.</summary>
        private PopupPanel AcquireForRequest()
        {
            var panel = _popupPanels.Find(p => p != null && !p.gameObject.activeSelf);
            if (panel == null)
            {
                if (_popupPanelPrefab == null) return null;
                panel = Instantiate(_popupPanelPrefab, transform).GetComponent<PopupPanel>();
                _popupPanels.Add(panel);
            }

            if (panel != null && panel.transform is RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            return panel;
        }

        // ── Legacy static API (unchanged) ─────────────────────────────────────

        /// <summary>
        /// Displays a popup panel message with the specified title and main text
        /// </summary>
        public static PopupPanel ShowPopupPanel(string titleText, string mainText, bool closeableByUser = true)
        {
            if (_instance != null)
            {
                return _instance.DisplayPopupPanel(titleText, mainText, closeableByUser);
            }

            CSDebug.LogError($"No popuppanel instance found. Cannot display message: {titleText}: {mainText}");
            return null;
        }

        /// <summary>
        /// Used to display a status message notification at the top of the screen for a short duration
        /// </summary>
        public static void DisplayStatus(string status, int duration)
        {
            _instance._notificationUI.DisplayStatus(status, duration);
        }

        private PopupPanel DisplayPopupPanel(string titleText, string mainText, bool closeableByUser)
        {
            PopupPanel panel = GetNextAvailablePopupPanel();

            if (panel != null)
            {
                panel.SetupPopupPanel(titleText, mainText, closeableByUser);
            }

            return panel;
        }

        private PopupPanel GetNextAvailablePopupPanel()
        {
            PopupPanel panel = _popupPanels.Find(p => !p.gameObject.activeSelf);

            if (panel == null)
            {
                panel = Instantiate(_popupPanelPrefab, transform).GetComponent<PopupPanel>();
                panel.gameObject.transform.position += new Vector3(1, -1) * (OFFSET * _popupPanels.Count % MAX_OFFSET);
                _popupPanels.Add(panel);
            }

            return panel;
        }
    }
}
