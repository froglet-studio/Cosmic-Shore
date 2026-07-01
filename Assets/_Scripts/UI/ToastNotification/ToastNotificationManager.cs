using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CosmicShore.UI
{
    /// <summary>
    /// Persistent singleton that manages toast notification lifecycle. It is fully
    /// self-contained: it boots itself before the first scene loads, loads its settings
    /// and SOAP channel from <c>Resources</c>, and owns a <see cref="DontDestroyOnLoad"/>
    /// screen-space-overlay <see cref="Canvas"/> so toasts render in EVERY scene
    /// (menu, gameplay, loading) — not just a scene that happens to ship a container.
    ///
    /// <para>Call it from anywhere via <see cref="ToastNotificationAPI.Show(string)"/>,
    /// by raising the <see cref="ToastNotificationChannel"/> SOAP event, or directly via
    /// <see cref="Show(string)"/>.</para>
    ///
    /// <para>If a <see cref="container"/> is explicitly assigned in the inspector it is
    /// used verbatim (its VerticalLayoutGroup / RectMask2D own positioning); otherwise the
    /// manager builds its own overlay canvas + vertically-stacked container from the
    /// margins in <see cref="ToastNotificationSettingsSO"/>. New toasts are added as the
    /// last sibling; older toasts shift via layout.</para>
    /// </summary>
    public sealed class ToastNotificationManager : SingletonPersistent<ToastNotificationManager>
    {
        private const string SettingsResourcePath = "ToastNotificationSettings";
        private const string ChannelResourcePath = "Channels/ToastNotificationChannel";
        private const string ContainerName = "ToastNotificationContainer";
        private const int OverlaySortingOrder = 32000; // above gameplay HUDs / menus

        [Header("Configuration")]
        [Tooltip("Optional — auto-loaded from Resources/ToastNotificationSettings when unset.")]
        [SerializeField] private ToastNotificationSettingsSO settings;

        [Header("Event Channel")]
        [Tooltip("SOAP event channel for decoupled toast requests. Auto-loaded from " +
                 "Resources/Channels/ToastNotificationChannel when unset. You can also call Show() directly.")]
        [SerializeField] private ToastNotificationChannel channel;

        [Header("Toast Prefab")]
        [Tooltip("Prefab for individual toast items. If null, a default one is created at runtime.")]
        [SerializeField] private ToastNotificationItem toastPrefab;

        [Header("Container")]
        [Tooltip("Optional RectTransform to spawn toasts into. Leave empty to let the manager own " +
                 "a persistent overlay canvas that renders in every scene.")]
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

        /// <summary>
        /// Guarantees a live manager exists before the first scene loads, so any later
        /// <see cref="Show(string)"/> call (from any scene) has somewhere to render.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => EnsureInstance();

        /// <summary>
        /// Returns the singleton, creating a fully-configured instance on the fly if one
        /// does not yet exist. Main-thread only (it may instantiate GameObjects).
        /// </summary>
        public static ToastNotificationManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(ToastNotificationManager));
            go.AddComponent<ToastNotificationManager>(); // Awake wires config + DontDestroyOnLoad
            return Instance;
        }

        public override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            if (settings == null)
                settings = Resources.Load<ToastNotificationSettingsSO>(SettingsResourcePath);
            if (channel == null)
                channel = Resources.Load<ToastNotificationChannel>(ChannelResourcePath);
            if (toastPrefab == null)
                toastPrefab = CreateDefaultPrefab();

            EnsureRenderTarget();
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
            if (string.IsNullOrWhiteSpace(message)) return;

            if (settings == null)
            {
                CSDebug.LogWarning("[ToastNotificationManager] No settings assigned. Toast dropped.");
                return;
            }

            EnsureRenderTarget();
            if (container == null)
            {
                CSDebug.LogWarning("[ToastNotificationManager] No render target. Toast dropped.");
                return;
            }

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

        #region Render Target

        /// <summary>
        /// Ensures <see cref="container"/> points at a live, visible RectTransform. If nothing
        /// was assigned in the inspector, builds a persistent screen-space-overlay canvas owned
        /// by this manager so toasts appear in every scene.
        /// </summary>
        private void EnsureRenderTarget()
        {
            if (container != null) return;

            var canvasGO = new GameObject("ToastOverlayCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var containerGO = new GameObject(ContainerName,
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(CanvasGroup));
            var rt = containerGO.GetComponent<RectTransform>();
            rt.SetParent(canvasGO.transform, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(settings != null ? settings.leftMargin : 24f,
                                              -(settings != null ? settings.topMargin : 120f));
            rt.sizeDelta = new Vector2(560f, 0f);

            var layout = containerGO.GetComponent<VerticalLayoutGroup>();
            layout.spacing = settings != null ? settings.stackSpacing : 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;   // items keep their own height
            layout.childForceExpandHeight = false;

            container = rt;
        }

        #endregion

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

            if (_pendingQueue.Count > 0 && _activeToasts.Count < settings.maxVisible)
                SpawnToast(_pendingQueue.Dequeue());
        }

        #endregion

        #region Default Prefab (Runtime Fallback)

        private ToastNotificationItem CreateDefaultPrefab()
        {
            var go = new GameObject("ToastItem_Default", typeof(RectTransform));
            go.SetActive(false);

            var rootRT = go.GetComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(560f, 72f); // real height so the layout can place it

            go.AddComponent<CanvasGroup>().alpha = 0f;

            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(go.transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            bgGO.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

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
            tmp.raycastTarget = false;

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
