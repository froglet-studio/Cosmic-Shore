using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CosmicShore.UI
{
    /// <summary>
    /// Persistent singleton that manages toast notification lifecycle.
    /// Spawns toasts inside an assigned UI container. The container's own layout
    /// (VerticalLayoutGroup, ContentSizeFitter, RectMask2D, etc.) controls
    /// positioning and clipping - this script never touches anchors, size, or position.
    /// New toasts are added as the last sibling; older toasts shift upward via layout.
    /// </summary>
    public sealed class ToastNotificationManager : SingletonPersistent<ToastNotificationManager>
    {
        [Header("Configuration")]
        [SerializeField] private ToastNotificationSettingsSO settings;

        [Header("Event Channel")]
        [Tooltip("SOAP event channel for decoupled toast requests. Optional - you can also call Show() directly.")]
        [SerializeField] private ToastNotificationChannel channel;

        [Header("Toast Prefab")]
        [Tooltip("Prefab for individual toast items. If null, a default one is created at runtime.")]
        [SerializeField] private ToastNotificationItem toastPrefab;

        [Header("Container")]
        [Tooltip("RectTransform inside the UI hierarchy where toasts are spawned. " +
                 "Layout is entirely controlled by the container (VerticalLayoutGroup + RectMask2D).")]
        [SerializeField] private RectTransform container;

        // Runtime
        private readonly List<ToastNotificationItem> _activeToasts = new();
        private readonly Queue<string> _pendingQueue = new();
        private readonly Stack<ToastNotificationItem> _pool = new();

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

            ForgetToastsDestroyedWithTheirScene();

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

            // Place as last child so container layout puts it at the bottom
            item.transform.SetAsLastSibling();
            item.Show(message, settings);
        }

        #region Pool

        /// <summary>
        /// This manager outlives every scene, but its toast items are children of a container that
        /// lives IN a scene (Menu_Main's ToastNotificationContainer), so a scene change destroys them
        /// without a dismissal: a toast on screen at the time stayed in <see cref="_activeToasts"/>
        /// for good (three of them and every later toast queued forever), and a pooled one threw
        /// MissingReferenceException the next time it was popped. A failed party join or a lost host
        /// reloads Menu_Main and then shows its notice here, so any toast earlier in the session was
        /// enough to lose that notice. Drops the dead references before a toast is placed.
        /// </summary>
        private void ForgetToastsDestroyedWithTheirScene()
        {
            _activeToasts.RemoveAll(item => !item);

            bool anyDeadInPool = false;
            foreach (var item in _pool)
                if (!item) { anyDeadInPool = true; break; }
            if (!anyDeadInPool) return;

            var live = new List<ToastNotificationItem>(_pool.Count);
            foreach (var item in _pool)
                if (item) live.Add(item);
            _pool.Clear();
            for (int i = live.Count - 1; i >= 0; i--)
                _pool.Push(live[i]);
        }

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

            if (_pendingQueue.Count > 0 && _activeToasts.Count < settings.maxVisible)
                SpawnToast(_pendingQueue.Dequeue());
        }

        #endregion

        #region Default Prefab (Runtime Fallback)

        private ToastNotificationItem CreateDefaultPrefab()
        {
            var go = new GameObject("ToastItem_Default", typeof(RectTransform));
            go.SetActive(false);

            go.AddComponent<CanvasGroup>().alpha = 0f;

            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(go.transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            bgGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

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
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            var item = go.AddComponent<ToastNotificationItem>();
            var field = typeof(ToastNotificationItem).GetField("messageText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(item, tmp);

            go.transform.SetParent(transform, false);
            return item;
        }

        #endregion
    }
}
