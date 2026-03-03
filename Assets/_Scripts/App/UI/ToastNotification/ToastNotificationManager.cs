using System.Collections.Generic;
using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CosmicShore.Utility;

namespace CosmicShore.App.UI.ToastNotification
{
    /// <summary>
    /// Persistent singleton that manages the toast notification canvas and item lifecycle.
    /// Creates its own overlay Canvas so toasts render on top of everything, across all scenes.
    /// </summary>
    public sealed class ToastNotificationManager : SingletonPersistent<ToastNotificationManager>
    {
        [Header("Configuration")]
        [SerializeField] private ToastNotificationSettingsSO settings;

        [Header("Event Channel")]
        [Tooltip("SOAP event channel for decoupled toast requests. Optional — you can also call Show() directly.")]
        [SerializeField] private ToastNotificationChannel channel;

        [Header("Toast Prefab")]
        [Tooltip("Prefab for individual toast items. If null, a default one is created at runtime.")]
        [SerializeField] private ToastNotificationItem toastPrefab;

        // Runtime
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private readonly List<ToastNotificationItem> _activeToasts = new();
        private readonly Queue<string> _pendingQueue = new();
        private readonly Stack<ToastNotificationItem> _pool = new();

        public override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            EnsureCanvas();

            if (toastPrefab == null)
                toastPrefab = CreateDefaultPrefab();
        }

        private void OnEnable()
        {
            if (channel) channel.OnRaised += Show;
        }

        private void OnDisable()
        {
            if (channel) channel.OnRaised -= Show;
        }

        /// <summary>
        /// Show a toast notification with the given message.
        /// </summary>
        public void Show(string message)
        {
            if (settings == null)
            {
                CSDebug.LogWarning("[ToastNotificationManager] No settings assigned.");
                return;
            }

            if (string.IsNullOrWhiteSpace(message)) return;

            // If at capacity, either queue or evict the oldest
            if (_activeToasts.Count >= settings.maxVisible)
            {
                if (_pendingQueue.Count < settings.maxQueue)
                {
                    _pendingQueue.Enqueue(message);
                    return;
                }

                // Queue is full — drop the oldest queued message and enqueue new one
                _pendingQueue.Dequeue();
                _pendingQueue.Enqueue(message);
                return;
            }

            SpawnToast(message);
        }

        private void SpawnToast(string message)
        {
            var item = GetOrCreateItem();
            _activeToasts.Add(item);

            var showPos = CalculatePosition(_activeToasts.Count - 1);
            item.Show(message, showPos, settings);
        }

        private Vector2 CalculatePosition(int index)
        {
            // Anchor: top-left. Y goes downward from top.
            float x = settings.leftMargin;
            float y = -(settings.topMargin + index * (GetItemHeight() + settings.stackSpacing));
            return new Vector2(x, y);
        }

        private float GetItemHeight()
        {
            if (toastPrefab != null)
            {
                var rt = toastPrefab.GetComponent<RectTransform>();
                if (rt != null && rt.rect.height > 0)
                    return rt.rect.height;
            }
            return 80f; // fallback
        }

        #region Pool

        private ToastNotificationItem GetOrCreateItem()
        {
            ToastNotificationItem item;

            if (_pool.Count > 0)
            {
                item = _pool.Pop();
            }
            else
            {
                item = Instantiate(toastPrefab, _canvasRect);
                item.OnDismissed += HandleDismissed;
            }

            return item;
        }

        private void HandleDismissed(ToastNotificationItem item)
        {
            _activeToasts.Remove(item);
            _pool.Push(item);

            // Reposition remaining toasts
            for (int i = 0; i < _activeToasts.Count; i++)
            {
                var pos = CalculatePosition(i);
                _activeToasts[i].AnimateToY(pos.y);
            }

            // Drain queue
            if (_pendingQueue.Count > 0 && _activeToasts.Count < settings.maxVisible)
            {
                SpawnToast(_pendingQueue.Dequeue());
            }
        }

        #endregion

        #region Canvas Setup

        private void EnsureCanvas()
        {
            if (_canvas != null) return;

            var canvasGO = new GameObject("ToastNotificationCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999; // on top of everything

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            _canvasRect = canvasGO.GetComponent<RectTransform>();
        }

        #endregion

        #region Default Prefab (Runtime Fallback)

        private ToastNotificationItem CreateDefaultPrefab()
        {
            // Root
            var go = new GameObject("ToastItem_Default", typeof(RectTransform));
            go.SetActive(false); // prefab template, stays inactive

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(500f, 80f);

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // Background image
            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(go.transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            // Text
            var textGO = new GameObject("MessageText", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(16f, 8f);
            textRT.offsetMax = new Vector2(-16f, -8f);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            // Add ToastNotificationItem component and wire references via reflection-free approach
            var item = go.AddComponent<ToastNotificationItem>();

            // We need to set the serialized field. Since it's private [SerializeField], we use a helper.
            SetMessageText(item, tmp);

            go.transform.SetParent(transform, false);
            return item;
        }

        /// <summary>
        /// Sets the private messageText field on a ToastNotificationItem via reflection.
        /// Only used for the runtime-generated fallback prefab.
        /// </summary>
        private static void SetMessageText(ToastNotificationItem item, TMP_Text text)
        {
            var field = typeof(ToastNotificationItem).GetField("messageText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(item, text);
        }

        #endregion
    }
}
