using System;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The disconnect notice: the one surface that tells a player their connection went away, in
    /// whatever scene they were in, and offers them the reconnect that already exists.
    ///
    /// <para><b>Why it builds its own canvas.</b> Before this, losing the connection surfaced as a
    /// toast — and <c>ToastService</c> is a scene-bound MonoBehaviour that subscribes in
    /// <c>OnEnable</c>, so it is destroyed and recreated by a scene load and is <b>absent entirely
    /// in a game scene</b> (<c>PartyInviteController.BounceToSoloMenuAsync</c> documents raising its
    /// notice only AFTER recovery for exactly that reason). A notice that can be dropped by the
    /// event it is reporting on is not a notice. This lives on a <c>DontDestroyOnLoad</c> root with
    /// its own canvas, so it outlives the scene reload that a disconnect triggers.</para>
    ///
    /// <para><b>What it does not do.</b> It changes no network or session state. The existing
    /// recovery paths are good and keep their ownership: a transport failure still bounces to a
    /// working solo menu (<c>PartyInviteController.HandleHostLossAsync</c>), and a boot with no
    /// network still falls back to the offline host. This only makes the event legible and puts
    /// <see cref="ReconnectService"/> — which already re-runs the boot chain in place — one tap
    /// away instead of behind a button that is in no scene.</para>
    ///
    /// <para><b>Why it defers during a launch.</b> <c>BootStatusBroadcaster</c> deliberately
    /// suppresses connection-lost while the launch splash is opaque, because those flows own their
    /// own recovery and "tap retry" would be misleading there. This honours the same window rather
    /// than second-guessing it (<see cref="DisconnectNoticeConfigSO.SuppressDuringLaunch"/>).</para>
    /// </summary>
    public class DisconnectNotice : MonoBehaviour
    {
        const string ConfigResourcePath = "DisconnectNoticeConfig";

        static DisconnectNotice _instance;

        DisconnectNoticeConfigSO _config;
        NetworkMonitorDataVariable _networkData;
        GameDataSO _gameData;
        ReconnectService _reconnect;

        CanvasGroup _group;
        TMP_Text _title, _body, _reconnectLabel;
        Button _reconnectButton, _dismissButton;
        bool _subscribed, _launching, _dismissed, _busy;
        float _targetAlpha;

        /// <summary>
        /// Creates the notice on its own persistent root. Called once from <c>AppManager.Start</c>,
        /// after DI has resolved, so the services arrive as arguments rather than through
        /// <c>[Inject]</c> — a runtime-created object is not injected unless somebody injects it,
        /// which is the trap <c>Docs/UI_ARCHITECTURE_AUDIT.md</c> records against persistent
        /// listener lists.
        /// </summary>
        public static DisconnectNotice Install(NetworkMonitorDataVariable networkData,
                                               GameDataSO gameData,
                                               ReconnectService reconnect)
        {
            if (_instance != null) return _instance;

            var config = Resources.Load<DisconnectNoticeConfigSO>(ConfigResourcePath);
            if (config == null)
            {
                CSDebug.LogWarning($"[DisconnectNotice] No config at Resources/{ConfigResourcePath} " +
                                   "- the disconnect notice will not be shown.");
                return null;
            }

            var host = new GameObject(nameof(DisconnectNotice));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<DisconnectNotice>();
            _instance._config = config;
            _instance._networkData = networkData;
            _instance._gameData = gameData;
            _instance._reconnect = reconnect;
            _instance.BuildUI();
            _instance.Subscribe();
            return _instance;
        }

        void OnDestroy()
        {
            Unsubscribe();
            if (_instance == this) _instance = null;
        }

        // ---- wiring -----------------------------------------------------------------------

        void Subscribe()
        {
            if (_subscribed) return;
            var data = _networkData != null ? _networkData.Value : null;
            if (data != null)
            {
                if (data.OnNetworkLost != null) data.OnNetworkLost.OnRaised += HandleLost;
                if (data.OnNetworkFound != null) data.OnNetworkFound.OnRaised += HandleFound;
            }
            if (_gameData != null)
            {
                if (_gameData.OnLaunchGame != null) _gameData.OnLaunchGame.OnRaised += HandleLaunch;
                if (_gameData.OnClientReady != null) _gameData.OnClientReady.OnRaised += HandleReady;
            }
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            var data = _networkData != null ? _networkData.Value : null;
            if (data != null)
            {
                if (data.OnNetworkLost != null) data.OnNetworkLost.OnRaised -= HandleLost;
                if (data.OnNetworkFound != null) data.OnNetworkFound.OnRaised -= HandleFound;
            }
            if (_gameData != null)
            {
                if (_gameData.OnLaunchGame != null) _gameData.OnLaunchGame.OnRaised -= HandleLaunch;
                if (_gameData.OnClientReady != null) _gameData.OnClientReady.OnRaised -= HandleReady;
            }
            _subscribed = false;
        }

        void HandleLaunch() => _launching = true;
        void HandleReady() => _launching = false;

        void HandleLost()
        {
            if (_config.SuppressDuringLaunch && _launching) return;
            _dismissed = false;
            Show(_config.TitleConnectionLost, _config.BodyConnectionLost, offerReconnect: true);
        }

        void HandleFound()
        {
            if (_targetAlpha <= 0f) return;      // never surfaced; nothing to resolve
            Show(_config.TitleConnectionRestored, string.Empty, offerReconnect: false);
            HideAfterAsync(_config.RestoredDwellSeconds).Forget();
        }

        async UniTaskVoid HideAfterAsync(float seconds)
        {
            var until = Time.unscaledTime + Mathf.Max(0f, seconds);
            while (Time.unscaledTime < until && _targetAlpha > 0f)
                await UniTask.Yield(PlayerLoopTiming.Update);
            Hide();
        }

        // ---- presentation -----------------------------------------------------------------

        void Show(string title, string body, bool offerReconnect)
        {
            if (_dismissed && offerReconnect) return;
            if (_title != null) _title.text = title;
            if (_body != null)
            {
                _body.text = body;
                _body.gameObject.SetActive(!string.IsNullOrEmpty(body));
            }
            if (_reconnectButton != null)
                _reconnectButton.gameObject.SetActive(offerReconnect && CanReconnect);
            if (_dismissButton != null) _dismissButton.gameObject.SetActive(offerReconnect);
            _targetAlpha = 1f;
            if (_group != null) _group.blocksRaycasts = true;
        }

        void Hide()
        {
            _targetAlpha = 0f;
            if (_group != null) _group.blocksRaycasts = false;
        }

        bool CanReconnect
        {
            get
            {
                try { return _reconnect != null && _reconnect.CanReconnect; }
                catch (Exception e)
                {
                    CSDebug.LogWarning($"[DisconnectNotice] CanReconnect threw: {e.Message}");
                    return false;
                }
            }
        }

        void Update()
        {
            if (_group == null) return;
            var step = _config.FadeSeconds <= 0f
                ? 1f
                : Time.unscaledDeltaTime / _config.FadeSeconds;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, step);
        }

        void OnDismiss()
        {
            _dismissed = true;
            Hide();
        }

        async void OnReconnect()
        {
            if (_busy || _reconnect == null) return;
            _busy = true;
            if (_reconnectButton != null) _reconnectButton.interactable = false;
            if (_reconnectLabel != null) _reconnectLabel.text = _config.ReconnectingLabel;
            try
            {
                // ReconnectAsync falls back to offline rather than stranding the player, so a
                // failed attempt simply leaves the notice up and the control available again.
                await _reconnect.ReconnectAsync();
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[DisconnectNotice] Reconnect threw: {e.Message}");
            }
            finally
            {
                _busy = false;
                if (_reconnectLabel != null) _reconnectLabel.text = _config.ReconnectLabel;
                if (_reconnectButton != null) _reconnectButton.interactable = true;
                if (_gameData != null && !_gameData.IsOfflineSession) Hide();
            }
        }
        // ---- construction -----------------------------------------------------------------

        void BuildUI()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler),
                                          typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _config.SortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            var scrim = NewRect("Scrim", canvasGo.transform);
            Stretch(scrim);
            AddImage(scrim, _config.ScrimColor);

            var panel = NewRect("Panel", scrim);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = _config.PanelSize;
            AddImage(panel, _config.PanelColor);

            _title = AddText(panel, "Title", 44, FontStyles.Bold, _config.TitleColor,
                             TextAlignmentOptions.Center,
                             new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -34f),
                             new Vector2(-60f, 64f));
            _body = AddText(panel, "Body", 26, FontStyles.Normal, _config.BodyColor,
                            TextAlignmentOptions.Top,
                            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -118f),
                            new Vector2(-96f, 110f));

            _reconnectButton = AddButton(panel, "ReconnectButton", _config.ReconnectLabel,
                                         _config.PrimaryButtonColor, new Vector2(-150f, 56f),
                                         out _reconnectLabel);
            _reconnectButton.onClick.AddListener(OnReconnect);

            _dismissButton = AddButton(panel, "DismissButton", _config.DismissLabel,
                                       _config.SecondaryButtonColor, new Vector2(150f, 56f), out _);
            _dismissButton.onClick.AddListener(OnDismiss);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
        }

        static TMP_Text AddText(RectTransform parent, string name, float size, FontStyles style,
                                Color color, TextAlignmentOptions align, Vector2 anchorMin,
                                Vector2 anchorMax, Vector2 position, Vector2 sizeDelta)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        static Button AddButton(RectTransform parent, string name, string label, Color color,
                                Vector2 position, out TMP_Text labelText)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(280f, 72f);
            AddImage(rect, color);

            labelText = AddText(rect, "Label", 28, FontStyles.Bold, Color.white,
                                TextAlignmentOptions.Center, Vector2.zero, Vector2.one,
                                Vector2.zero, Vector2.zero);
            var labelRect = (RectTransform)labelText.transform;
            Stretch(labelRect);
            labelRect.pivot = new Vector2(0.5f, 0.5f);

            return rect.gameObject.AddComponent<Button>();
        }
    }
}
