using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        TMP_Text _title, _body, _reconnectLabel, _dismissLabel;

        // Section 103: the same window asks to rejoin a match this player dropped out of, or says its host left.
        enum Mode { Connection = 0, Rejoin = 1, HostLeft = 2 }
        Mode _mode;
        PartyPlayerData _rejoinVia;
        float _nextRejoinCheck;
        float _presenceOnlineFor;
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
            SetMode(Mode.Connection);
            Show(_config.TitleConnectionLost, _config.BodyConnectionLost, offerReconnect: true);
        }

        void HandleFound()
        {
            if (_targetAlpha <= 0f || _mode != Mode.Connection) return;      // never surfaced; nothing to resolve
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
            SetLive(true);
            // A pad reaches RECONNECT as a submit (A); B is the dismiss button's own press.
            var focus = _reconnectButton != null && _reconnectButton.gameObject.activeSelf ? _reconnectButton
                      : _dismissButton != null && _dismissButton.gameObject.activeSelf ? _dismissButton : null;
            if (focus != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(focus.gameObject);
        }

        void Hide()
        {
            _targetAlpha = 0f;
            SetLive(false);
        }

        // Interactable gates the window's B press (ControllerButtonPress reads the group) as well as raycasts.
        void SetLive(bool live)
        {
            if (_group == null) return;
            _group.blocksRaycasts = live;
            _group.interactable = live;
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
            CheckRejoin();
            if (_group == null) return;
            var step = _config.FadeSeconds <= 0f
                ? 1f
                : Time.unscaledDeltaTime / _config.FadeSeconds;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, step);
        }

        void OnDismiss()
        {
            if (_mode != Mode.Connection)
            {
                MatchRejoin.Forget();          // NOT NOW / BACK TO HOME: the match is not offered again
                SetMode(Mode.Connection);
                Hide();
                return;
            }
            _dismissed = true;
            Hide();
        }

        async void OnReconnect()
        {
            if (_mode == Mode.Rejoin)
            {
                Rejoin();
                return;
            }
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
        // ---- rejoin (section 103) --------------------------------------------------------

        /// <summary>
        /// On the menu, while a match is remembered (<see cref="MatchRejoin"/>): when a party mate still advertises that
        /// match, offer to rejoin it; when nobody has for a while, give it up - saying "The host left" after a drop, since
        /// the host is a player and its match ends with it.
        /// </summary>
        void CheckRejoin()
        {
            if (Time.unscaledTime < _nextRejoinCheck) return;
            _nextRejoinCheck = Time.unscaledTime + _config.RejoinCheckSeconds;
            if (_targetAlpha > 0f || _launching || _busy) return;
            if (!MatchRejoin.TryLoad(out var record)) { _presenceOnlineFor = 0f; return; }

            var hcs = HostConnectionService.Instance;
            if (hcs == null || PartyInviteController.Instance == null || _gameData == null || _gameData.IsOfflineSession) return;
            // Not while the bounce that brought us here is still running: a Rejoin pressed then was ignored
            // ("Already transitioning", 2026-10-30). The offer comes on the first check after it ends.
            if (PartyInviteController.Instance.IsTransitioning) return;
            // Already in that match (this client never left it, or it just rejoined).
            if (_gameData.ActiveSession != null && _gameData.ActiveSession.Id == record.sessionId) return;

            var online = hcs.ConnectionData != null ? hcs.ConnectionData.OnlinePlayers : null;
            if (online != null)
            {
                foreach (var member in online)
                {
                    if (member.PartySessionId != record.sessionId || !member.IsInMatch) continue;
                    _rejoinVia = member;
                    ShowRejoin(record);
                    return;
                }
            }

            _presenceOnlineFor += _config.RejoinCheckSeconds;
            if (_presenceOnlineFor < _config.RejoinGiveUpSeconds) return;
            _presenceOnlineFor = 0f;
            if (record.Dropped)
            {
                SetMode(Mode.HostLeft);
                Show(_config.TitleHostLeft, _config.BodyHostLeft, offerReconnect: false);
                if (_dismissButton != null) _dismissButton.gameObject.SetActive(true);
                Select(_dismissButton);
            }
            MatchRejoin.Forget();
        }

        void ShowRejoin(MatchRejoin.Record record)
        {
            string mode = ModeName(record.Mode);
            bool recent = record.Dropped &&
                          (DateTime.UtcNow - new DateTime(record.droppedAtUtcTicks, DateTimeKind.Utc)).TotalSeconds <= _config.DroppedOutRecentSeconds;
            SetMode(Mode.Rejoin);
            _dismissed = false;
            Show(TemplateText.Fill(recent ? _config.TitleDroppedOut : _config.TitleRejoin, "mode", mode),
                 TemplateText.Fill(recent ? _config.BodyDroppedOut : _config.BodyRejoin, "mode", mode),
                 offerReconnect: true);
            if (_reconnectButton != null) _reconnectButton.gameObject.SetActive(true);
            Select(_reconnectButton);
        }

        void Rejoin()
        {
            var controller = PartyInviteController.Instance;
            string ugsId = Unity.Services.Authentication.AuthenticationService.Instance?.PlayerId;
            SetMode(Mode.Connection);
            Hide();
            if (controller == null || string.IsNullOrEmpty(ugsId))
            {
                MatchRejoin.Forget();
                return;
            }
            controller.RejoinMatchAsync(_rejoinVia, ugsId).Forget();
        }

        void SetMode(Mode mode)
        {
            _mode = mode;
            if (_reconnectLabel != null)
                _reconnectLabel.text = mode == Mode.Rejoin ? _config.RejoinLabel : _config.ReconnectLabel;
            if (_dismissLabel != null)
                _dismissLabel.text = mode switch
                {
                    Mode.Rejoin => _config.NotNowLabel,
                    Mode.HostLeft => _config.BackHomeLabel,
                    _ => _config.DismissLabel,
                };
        }

        static void Select(Button button)
        {
            if (button != null && button.gameObject.activeSelf && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        // A GameModes member's words, "SkimRace" -> "Skim Race" (the cards' authored titles live in each scene's list).
        static string ModeName(GameModes mode) =>
            System.Text.RegularExpressions.Regex.Replace(mode.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

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
            SetLive(false);

            if (_config.WindowPrefab != null)
            {
                BuildWindow(canvasGo.transform);
                return;
            }

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
                                       _config.SecondaryButtonColor, new Vector2(150f, 56f), out _dismissLabel);
            _dismissButton.onClick.AddListener(OnDismiss);
        }

        // The style-B window: layout from the prefab, every word from the config.
        void BuildWindow(Transform canvas)
        {
            var view = Instantiate(_config.WindowPrefab, canvas, false);
            Stretch((RectTransform)view.transform);
            if (view.Group != null)
            {
                // The window's group carries the fade, so the B press (which reads it) is live only while shown.
                _group.alpha = 1f;
                _group.blocksRaycasts = _group.interactable = true;
                _group = view.Group;
                _group.alpha = 0f;
                SetLive(false);
            }
            _title = view.Title;
            _body = view.Body;
            _reconnectButton = view.ReconnectButton;
            _reconnectLabel = view.ReconnectLabel;
            _dismissButton = view.DismissButton;
            if (_reconnectLabel != null) _reconnectLabel.text = _config.ReconnectLabel;
            _dismissLabel = view.DismissLabel;
            if (_dismissLabel != null) _dismissLabel.text = _config.DismissLabel;
            if (_reconnectButton != null) _reconnectButton.onClick.AddListener(OnReconnect);
            if (_dismissButton != null) _dismissButton.onClick.AddListener(OnDismiss);
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
