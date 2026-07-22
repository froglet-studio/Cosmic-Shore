using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Pure view for the boot/loading status surface. Renders status text +
    /// retry button as children of the Bootstrap splash canvas.
    ///
    /// Single-Responsibility: this component knows nothing about auth, party,
    /// or scene flow. Its only inputs and outputs are SOAP event channels.
    ///
    /// Inbound channel - <see cref="ScriptableEventBootStatusRequest"/> -
    /// drives <see cref="Apply"/>: writes <c>statusText.text</c>, toggles
    /// retry button visibility.
    ///
    /// Outbound channel - <see cref="ScriptableEventNoParam"/> retry-requested -
    /// raised when the user taps the retry button. Listeners (e.g.
    /// <c>HostConnectionService</c>) decide what to do.
    ///
    /// All references are inspector-wired in Bootstrap.unity - there is no
    /// runtime discovery or repair. This component's scene wiring was lost
    /// once via a script-GUID break, leaving the authored retry button
    /// orphaned-active on every splash (B10 in Docs/PartySystem/BUGS.md), so
    /// missing wiring now fails loud via <see cref="ReportMissingWiring"/>
    /// instead of silently degrading.
    ///
    /// Visibility of the surface as a whole is owned by the splash
    /// <c>CanvasGroup</c> on the parent Bootstrap canvas (managed by
    /// <c>SceneTransitionManager</c>). This panel does not touch it - when
    /// the splash fades, text + button fade with it. That is intentional:
    /// no status text is shown while the splash is hidden.
    ///
    /// The loader icon heartbeats (strong beat + weak beat + rest) the whole
    /// time the panel is enabled, occasionally shifting tint and randomly
    /// swapping between the authored icon sprites. Purely decorative - it
    /// never reacts to <see cref="BootStatusRequest"/>.
    /// </summary>
    public class BootStatusPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button retryButton;

        [Header("Heartbeat Icon")]
        [Tooltip("Loader icon that pulses like a heartbeat while the splash is visible.")]
        [SerializeField] private Image heartbeatIcon;
        [Tooltip("Sprite pool the icon randomly swaps between (expects 4).")]
        [SerializeField] private Sprite[] iconSprites;
        [Tooltip("Tint palette the icon occasionally shifts through. Include the rest color so the icon sometimes returns to it.")]
        [SerializeField] private Color[] iconColors =
        {
            Color.white,
            new Color(0.25f, 0.95f, 1f),  // cyan
            new Color(1f, 0.30f, 0.85f),  // magenta
            new Color(0.65f, 0.45f, 1f),  // violet
        };

        [Header("Heartbeat Tuning")]
        [Tooltip("Peak scale multiplier of the first (strong) beat.")]
        [SerializeField, Min(1f)] private float beatScale = 1.18f;
        [Tooltip("Peak scale multiplier of the second (weak) beat.")]
        [SerializeField, Min(1f)] private float secondBeatScale = 1.09f;
        [Tooltip("Seconds for each grow/shrink step of a beat.")]
        [SerializeField, Min(0.02f)] private float beatStepDuration = 0.12f;
        [Tooltip("Seconds of rest between heartbeat cycles.")]
        [SerializeField, Min(0f)] private float beatRestInterval = 0.55f;
        [Tooltip("Seconds between random icon swaps (applied at the next beat so the swap never pops mid-pulse).")]
        [SerializeField, Min(0.1f)] private float iconSwapInterval = 2f;
        [Tooltip("Chance per heartbeat cycle that the icon shifts to a random palette color.")]
        [SerializeField, Range(0f, 1f)] private float colorChangeChance = 0.35f;
        [Tooltip("Seconds for a color shift to blend in.")]
        [SerializeField, Min(0.02f)] private float colorFadeDuration = 0.3f;

        [Header("SOAP")]
        [Tooltip("Inbound: any system raises BootStatusRequest to drive the surface.")]
        [SerializeField] private ScriptableEventBootStatusRequest inboundRequestEvent;

        [Tooltip("Outbound: raised when the user taps the retry button.")]
        [SerializeField] private ScriptableEventNoParam outboundRetryEvent;

        private Sequence _heartbeat;
        private Tween _colorTween;
        private Vector3 _iconRestScale = Vector3.one;
        private Color _iconRestColor = Color.white;
        private float _lastSwapTime;

        void OnEnable()
        {
            ReportMissingWiring();

            if (inboundRequestEvent != null)
                inboundRequestEvent.OnRaised += HandleRequest;

            if (retryButton != null)
                retryButton.onClick.AddListener(HandleRetryClicked);

            Apply(new BootStatusRequest(BootStatusMode.Hide));

            StartHeartbeat();
        }

        void OnDisable()
        {
            if (inboundRequestEvent != null)
                inboundRequestEvent.OnRaised -= HandleRequest;

            if (retryButton != null)
                retryButton.onClick.RemoveListener(HandleRetryClicked);

            StopHeartbeat();
        }

        /// <summary>
        /// Fail loud on missing inspector wiring (project policy): an unwired
        /// reference means the boot/retry surface silently stops working - see
        /// B10 in Docs/PartySystem/BUGS.md for the orphaned-retry-button
        /// incident this guards against.
        /// </summary>
        void ReportMissingWiring()
        {
            if (statusText == null)
                Debug.LogError("[BootStatusPanel] statusText is not wired in the inspector.", this);
            if (retryButton == null)
                Debug.LogError("[BootStatusPanel] retryButton is not wired in the inspector.", this);
            if (inboundRequestEvent == null)
                Debug.LogError("[BootStatusPanel] inboundRequestEvent is not wired in the inspector.", this);
            if (outboundRetryEvent == null)
                Debug.LogError("[BootStatusPanel] outboundRetryEvent is not wired in the inspector.", this);
            if (heartbeatIcon == null)
                Debug.LogError("[BootStatusPanel] heartbeatIcon is not wired in the inspector.", this);
            if (iconSprites == null || iconSprites.Length == 0)
                Debug.LogError("[BootStatusPanel] iconSprites is empty - the heartbeat icon has nothing to swap between.", this);
        }

        private void HandleRequest(BootStatusRequest req) => Apply(req);

        private void Apply(BootStatusRequest req)
        {
            if (statusText != null)
                statusText.text = req.Text ?? string.Empty;

            if (retryButton != null)
            {
                bool wantRetry = req.Mode == BootStatusMode.Retry;
                if (wantRetry)
                    Debug.Log($"[BootStatusPanel] Retry surface shown - \"{req.Text}\"");
                retryButton.gameObject.SetActive(wantRetry);
                retryButton.interactable = wantRetry;
            }
        }

        private void HandleRetryClicked()
        {
            if (retryButton != null)
                retryButton.interactable = false;

            outboundRetryEvent.Raise();
        }

        // Unscaled time throughout: the splash lives across scene transitions
        // where timeScale can be anything, including 0.
        private void StartHeartbeat()
        {
            if (heartbeatIcon == null)
                return;

            _iconRestScale = heartbeatIcon.rectTransform.localScale;
            _iconRestColor = heartbeatIcon.color;
            _lastSwapTime = Time.unscaledTime;

            // Swap/recolor via OnStepComplete (fires at the end of every loop,
            // i.e. during the rest between beats) - a callback appended at
            // position 0 of a looped Sequence does not re-fire reliably.
            RectTransform rect = heartbeatIcon.rectTransform;
            _heartbeat = DOTween.Sequence()
                .Append(rect.DOScale(_iconRestScale * beatScale, beatStepDuration).SetEase(Ease.OutQuad))
                .Append(rect.DOScale(_iconRestScale, beatStepDuration).SetEase(Ease.InOutSine))
                .Append(rect.DOScale(_iconRestScale * secondBeatScale, beatStepDuration).SetEase(Ease.OutQuad))
                .Append(rect.DOScale(_iconRestScale, beatStepDuration).SetEase(Ease.InOutSine))
                .AppendInterval(beatRestInterval)
                .OnStepComplete(OnBeatCycleComplete)
                .SetLoops(-1)
                .SetUpdate(true)
                .SetLink(heartbeatIcon.gameObject);
        }

        private void StopHeartbeat()
        {
            _heartbeat?.Kill();
            _heartbeat = null;
            _colorTween?.Kill();
            _colorTween = null;

            if (heartbeatIcon != null)
            {
                heartbeatIcon.rectTransform.localScale = _iconRestScale;
                heartbeatIcon.color = _iconRestColor;
            }
        }

        private void OnBeatCycleComplete()
        {
            TrySwapIcon();
            TryShiftColor();
        }

        private void TrySwapIcon()
        {
            if (iconSprites == null || iconSprites.Length < 2)
                return;
            if (Time.unscaledTime - _lastSwapTime < iconSwapInterval)
                return;

            _lastSwapTime = Time.unscaledTime;

            // Exclude the current sprite so a swap is always visible.
            int currentIndex = System.Array.IndexOf(iconSprites, heartbeatIcon.sprite);
            int nextIndex = Random.Range(0, iconSprites.Length);
            if (nextIndex == currentIndex)
                nextIndex = (nextIndex + 1) % iconSprites.Length;

            heartbeatIcon.sprite = iconSprites[nextIndex];
        }

        private void TryShiftColor()
        {
            if (iconColors is not { Length: > 0 } || Random.value > colorChangeChance)
                return;

            Color target = iconColors[Random.Range(0, iconColors.Length)];
            target.a = _iconRestColor.a;

            _colorTween?.Kill();
            _colorTween = heartbeatIcon.DOColor(target, colorFadeDuration)
                .SetUpdate(true)
                .SetLink(heartbeatIcon.gameObject);
        }
    }
}
