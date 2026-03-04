using System.Collections.Generic;
using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CosmicShore.Utility;

namespace CosmicShore.App.UI.ToastNotification
{
    /// <summary>
    /// Persistent singleton that manages toast notification lifecycle.
    /// Spawns toasts inside an assigned UI container (RectTransform with RectMask2D).
    /// New toasts appear at the bottom; older ones slide upward and out.
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

        [Header("Container")]
        [Tooltip("RectTransform inside the UI hierarchy where toasts are spawned. " +
                 "Should have a RectMask2D so toasts clip when they slide out the top.")]
        [SerializeField] private RectTransform container;

        // Runtime
        private readonly List<ToastNotificationItem> _activeToasts = new();
        private readonly Queue<string> _pendingQueue = new();
        private readonly Stack<ToastNotificationItem> _pool = new();

        /// <summary>
        /// Assign the container at runtime (used by auto-creation in ToastNotificationAPI).
        /// </summary>
        public RectTransform Container
        {
            get => container;
            set => container = value;
        }

        public override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

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

            if (container == null)
            {
                CSDebug.LogWarning("[ToastNotificationManager] No container assigned. Toast dropped.");
                return;
            }

            if (string.IsNullOrWhiteSpace(message)) return;

            if (_activeToasts.Count >= settings.maxVisible)
            {
                if (_pendingQueue.Count < settings.maxQueue)
                {
                    _pendingQueue.Enqueue(message);
                    return;
                }

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

            // Push all existing toasts up by one slot
            RepositionAll();

            // New toast enters at the bottom slot (index = count - 1)
            var showPos = CalculatePosition(_activeToasts.Count - 1);
            item.Show(message, showPos, settings);
        }

        /// <summary>
        /// Calculates position for a given slot index.
        /// Index 0 = topmost slot (oldest), highest index = bottommost (newest).
        /// Positions are anchored from the bottom of the container upward.
        /// </summary>
        private Vector2 CalculatePosition(int index)
        {
            float itemHeight = GetItemHeight();
            int count = _activeToasts.Count;

            // Stack from the bottom of the container. Slot 0 = top, slot (count-1) = bottom.
            float y = (count - 1 - index) * (itemHeight + settings.stackSpacing);
            float x = settings.leftMargin;
            return new Vector2(x, y);
        }

        private void RepositionAll()
        {
            for (int i = 0; i < _activeToasts.Count - 1; i++)
            {
                var pos = CalculatePosition(i);
                _activeToasts[i].AnimateToY(pos.y);
            }
        }

        private float GetItemHeight()
        {
            if (toastPrefab != null)
            {
                var rt = toastPrefab.GetComponent<RectTransform>();
                if (rt != null && rt.rect.height > 0)
                    return rt.rect.height;
            }
            return 80f;
        }

        #region Pool

        private ToastNotificationItem GetOrCreateItem()
        {
            ToastNotificationItem item;

            if (_pool.Count > 0)
            {
                item = _pool.Pop();
                item.transform.SetParent(container, false);
            }
            else
            {
                item = Instantiate(toastPrefab, container);
                item.OnDismissed += HandleDismissed;
            }

            return item;
        }

        private void HandleDismissed(ToastNotificationItem item)
        {
            _activeToasts.Remove(item);
            _pool.Push(item);

            // Reposition remaining toasts — they slide down to fill the gap
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

        #region Default Prefab (Runtime Fallback)

        private ToastNotificationItem CreateDefaultPrefab()
        {
            var go = new GameObject("ToastItem_Default", typeof(RectTransform));
            go.SetActive(false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 80f);

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

            var item = go.AddComponent<ToastNotificationItem>();
            SetMessageText(item, tmp);

            go.transform.SetParent(transform, false);
            return item;
        }

        private static void SetMessageText(ToastNotificationItem item, TMP_Text text)
        {
            var field = typeof(ToastNotificationItem).GetField("messageText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(item, text);
        }

        #endregion
    }
}
