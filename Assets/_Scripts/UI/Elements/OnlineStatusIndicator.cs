using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The menu's online/offline lamp AND its toggle: green while the session is online, grey
    /// while it is offline, and tapping it asks the player whether to switch.
    ///
    /// <para>
    /// One control for both states rather than two buttons, because the question is always the
    /// same one - "which mode am I in, and do I want the other?" - and the answer is the colour.
    /// The confirmation is routed through a shared <see cref="ConfirmQuestionBar"/>: switching
    /// mode re-runs the boot chain, so it is never something to trigger on a mis-tap.
    /// </para>
    ///
    /// <para>
    /// Colours come from the shared <see cref="ElementalBarsConfigSO"/> ladder (lime = live,
    /// grey = inert) rather than local literals, so the lamp speaks the same colour language as
    /// the element flowers: grey already means "not in use" everywhere else in this UI.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class OnlineStatusIndicator : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField, Tooltip("Image tinted by connection state. Defaults to this object's own Image.")]
        Image lamp;

        [SerializeField, Tooltip("Optional label reading ONLINE / OFFLINE.")]
        TMPro.TMP_Text statusLabel;

        [Header("State sprites")]
        [SerializeField, Tooltip("Lamp sprite while ONLINE (filled). Optional - the lamp falls " +
                                 "back to tint-only when both sprites are empty.")]
        Sprite onlineSprite;

        [SerializeField, Tooltip("Lamp sprite while OFFLINE (hollow). Optional.")]
        Sprite offlineSprite;

        [SerializeField] string onlineText = "ONLINE";
        [SerializeField] string offlineText = "OFFLINE";

        [Header("Confirmation")]
        [SerializeField, Tooltip("The question bar this lamp drives. Required - without it the lamp is display-only.")]
        ConfirmQuestionBar questionBar;

        [SerializeField] string goOfflineQuestion = "GO OFFLINE?";
        [SerializeField] string goOnlineQuestion = "GO ONLINE?";

        [Header("Feel")]
        [SerializeField, Tooltip("Seconds for the colour crossfade when the state changes.")]
        float colorFadeSeconds = 0.35f;

        [SerializeField, Tooltip("Seconds per pulse while a mode switch is in flight. 0 disables the pulse.")]
        float busyPulseSeconds = 0.6f;

        [Inject] GameDataSO _gameData;
        [Inject] ReconnectService _reconnect;

        Button _button;
        Tween _colorTween;
        Tween _pulseTween;
        bool _subscribed;
        bool _lastKnownOffline;
        bool _hasAppliedOnce;

        ElementalBarsConfigSO _palette;

        bool IsOffline => _gameData != null && _gameData.IsOfflineSession;

        Color OnlineColor => _palette != null ? _palette.limeColor : new Color(0.59f, 0.92f, 0.16f, 1f);
        Color OfflineColor => _palette != null ? _palette.greyColor : new Color(0.51f, 0.51f, 0.54f, 1f);

        void Awake()
        {
            _button = GetComponent<Button>();
            if (lamp == null) lamp = GetComponent<Image>();

            // Shared palette, same asset the element flowers read. Optional - falls back to the
            // ladder's authored values so the lamp is never colourless.
            _palette = Resources.Load<ElementalBarsConfigSO>("ElementalBarsConfig");
        }

        void OnEnable()
        {
            if (_button != null) _button.onClick.AddListener(HandleClick);
            Apply(instant: true);
            TrySubscribe();
        }

        void Start()
        {
            // [Inject] lands between Awake and Start, so OnEnable's first pass on a
            // scene-loaded object can read a null GameDataSO as "online".
            Apply(instant: true);
            TrySubscribe();
        }

        void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);

            if (_subscribed && _reconnect != null)
            {
                _reconnect.OnReconnectingChanged -= HandleBusyChanged;
                _subscribed = false;
            }

            _colorTween?.Kill();
            _pulseTween?.Kill();
            _colorTween = null;
            _pulseTween = null;

            // Leave the lamp at rest - a pooled/toggled UI must not come back mid-pulse.
            if (lamp != null) lamp.transform.localScale = Vector3.one;
        }

        void Update()
        {
            // The session flag is plain shared state with no change event of its own (it is
            // written by OfflineModeService during a scene transition, when no listener is
            // guaranteed alive). One bool compare per frame on a single menu widget is far
            // cheaper than the machinery an event would need to survive that transition.
            if (_hasAppliedOnce && IsOffline == _lastKnownOffline) return;
            Apply(instant: false);
        }

        void TrySubscribe()
        {
            if (_subscribed || _reconnect == null) return;
            _reconnect.OnReconnectingChanged += HandleBusyChanged;
            _subscribed = true;
        }

        void HandleBusyChanged(bool busy)
        {
            if (_button != null) _button.interactable = !busy;

            _pulseTween?.Kill();
            _pulseTween = null;

            if (busy && busyPulseSeconds > 0f && lamp != null)
            {
                _pulseTween = lamp.transform
                    .DOScale(1.15f, busyPulseSeconds * 0.5f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true)          // menu tweens must survive a paused timescale
                    .SetLink(gameObject);
            }
            else if (lamp != null)
            {
                lamp.transform.localScale = Vector3.one;
            }
        }

        void Apply(bool instant)
        {
            bool offline = IsOffline;
            _lastKnownOffline = offline;
            _hasAppliedOnce = true;

            var target = offline ? OfflineColor : OnlineColor;
            var sprite = offline ? offlineSprite : onlineSprite;

            if (lamp != null)
            {
                _colorTween?.Kill();

                if (instant || colorFadeSeconds <= 0f)
                {
                    lamp.color = target;
                    ApplySprite(sprite);
                }
                else
                {
                    // Swap the sprite at the MIDPOINT of the colour crossfade rather than at
                    // either end. Filled↔hollow is a shape change, and a shape that changes
                    // while the colour is still travelling reads as one motion; changed at an
                    // end it reads as two, with a visible hitch.
                    _colorTween = DOTween.Sequence()
                        .Append(lamp.DOColor(target, colorFadeSeconds).SetEase(Ease.InOutQuad))
                        .InsertCallback(colorFadeSeconds * 0.5f, () => ApplySprite(sprite))
                        .SetUpdate(true)
                        .SetLink(gameObject);
                }
            }

            if (statusLabel != null)
                statusLabel.text = offline ? offlineText : onlineText;
        }

        /// <summary>
        /// Assigns a state sprite, tolerating an unwired pair. With neither sprite authored the
        /// lamp keeps whatever it was given in the scene and conveys state by tint alone - still
        /// correct, just without the filled/hollow shape cue.
        /// </summary>
        void ApplySprite(Sprite sprite)
        {
            if (lamp == null || sprite == null) return;
            lamp.sprite = sprite;
        }

        void HandleClick()
        {
            if (_reconnect == null)
            {
                CSDebug.LogWarning("[OnlineStatusIndicator] No ReconnectService - is there a ContainerScope in this scene?");
                return;
            }

            if (_reconnect.IsReconnecting) return;

            bool offline = IsOffline;

            if (questionBar == null)
            {
                // Display-only wiring: act with no confirmation rather than doing nothing
                // silently, but say so - a missing bar is a wiring bug, not a mode.
                CSDebug.LogWarning("[OnlineStatusIndicator] No ConfirmQuestionBar wired - switching without confirmation.");
                Switch(offline);
                return;
            }

            questionBar.Ask(offline ? goOnlineQuestion : goOfflineQuestion, () => Switch(offline));
        }

        void Switch(bool currentlyOffline)
        {
            if (currentlyOffline)
                _reconnect.ReconnectAsync().Forget();
            else
                _reconnect.GoOfflineAsync().Forget();
        }
    }
}
