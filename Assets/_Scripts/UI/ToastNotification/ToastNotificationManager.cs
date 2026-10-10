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
    ///
    /// Toast visuals come from the authored prefab: the serialized reference wins,
    /// then Resources/ToastNotificationItem (so the runtime-auto-created manager
    /// still uses the authored prefab), and only as a last resort a plain
    /// code-built item.
    /// </summary>
    public sealed class ToastNotificationManager : SingletonPersistent<ToastNotificationManager>
    {
        private const string PrefabResourcePath = "ToastNotificationItem";

        [Header("Configuration")]
        [SerializeField] private ToastNotificationSettingsSO settings;

        [Header("Event Channel")]
        [Tooltip("SOAP event channel for decoupled toast requests. Optional - you can also call Show() directly.")]
        [SerializeField] private ToastNotificationChannel channel;

        [Header("Toast Prefab")]
        [Tooltip("Prefab for individual toast items. If null, Resources/ToastNotificationItem is used, " +
                 "then a plain default built at runtime.")]
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
                toastPrefab = ResolvePrefab();
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
        /// Runtime wiring for the auto-created manager (see ToastNotificationAPI).
        /// Rebinds the channel subscription when the channel changes.
        /// </summary>
        public void Configure(ToastNotificationSettingsSO newSettings, ToastNotificationChannel newChannel)
        {
            if (newSettings != null)
                settings = newSettings;

            if (newChannel != null && newChannel != channel)
            {
                if (channel && isActiveAndEnabled) channel.OnRaised -= Show;
                channel = newChannel;
                if (isActiveAndEnabled) channel.OnRaised += Show;
            }
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

            // Scene transitions can destroy the previous container along with any
            // toasts parented under it - drop the dead references before counting.
            _activeToasts.RemoveAll(item => item == null);

            if (_activeToasts.Count >= settings.maxVisible)
            {
                if (_pendingQueue.Count >= settings.maxQueue)
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

        private ToastNotificationItem GetOrCreateItem()
        {
            // Pooled items live under the container, so a destroyed container
            // leaves destroyed entries behind - skip them.
            while (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled == null) continue;

                pooled.transform.SetParent(container, false);
                return pooled;
            }

            var item = Instantiate(toastPrefab, container);
            item.gameObject.SetActive(false);
            item.OnDismissed += HandleDismissed;
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

        #region Prefab Resolution

        private ToastNotificationItem ResolvePrefab()
        {
            var loaded = Resources.Load<ToastNotificationItem>(PrefabResourcePath);
            if (loaded != null) return loaded;

            CSDebug.LogWarning(
                "[ToastNotificationManager] No toast prefab assigned and none found at " +
                $"Resources/{PrefabResourcePath}. Falling back to a code-built default.");
            return CreateDefaultTemplate();
        }

        private ToastNotificationItem CreateDefaultTemplate()
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
            tmp.raycastTarget = false;

            var item = go.AddComponent<ToastNotificationItem>();
            item.BindMessageText(tmp);

            go.transform.SetParent(transform, false);
            return item;
        }

        #endregion
    }
}
