using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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
    /// Inbound channel — <see cref="ScriptableEventBootStatusRequest"/> —
    /// drives <see cref="Apply"/>: writes <c>statusText.text</c>, toggles
    /// retry button visibility.
    ///
    /// Outbound channel — <see cref="ScriptableEventNoParam"/> retry-requested —
    /// raised when the user taps the retry button. Listeners (e.g.
    /// <c>HostConnectionService</c>) decide what to do.
    ///
    /// Zero-wire fallback: this component has been lost from the splash canvas
    /// once already via a script-GUID break (see B10 in
    /// Docs/PartySystem/BUGS.md), leaving the authored retry button
    /// orphaned-active on every splash. To make that state unrepresentable,
    /// missing UI references are auto-found among the splash canvas's children
    /// (it contains exactly one status TMP_Text and one retry Button), missing
    /// event channels are supplied at runtime by
    /// <c>BootStatusBroadcaster.EnsureStatusPanelWired</c> via
    /// <see cref="EnsureWired"/>, and the retry button is forced hidden as
    /// soon as the panel binds — whatever its authored active state. Retry
    /// only ever appears via an explicit <see cref="BootStatusMode.Retry"/>
    /// request.
    ///
    /// Visibility of the surface as a whole is owned by the splash
    /// <c>CanvasGroup</c> on the parent Bootstrap canvas (managed by
    /// <c>SceneTransitionManager</c>). This panel does not touch it — when
    /// the splash fades, text + button fade with it. That is intentional:
    /// no status text is shown while the splash is hidden.
    /// </summary>
    public class BootStatusPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button retryButton;

        [Header("SOAP")]
        [Tooltip("Inbound: any system raises BootStatusRequest to drive the surface.")]
        [SerializeField] private ScriptableEventBootStatusRequest inboundRequestEvent;

        [Tooltip("Outbound: raised when the user taps the retry button.")]
        [SerializeField] private ScriptableEventNoParam outboundRetryEvent;

        bool _inboundSubscribed;
        bool _buttonHooked;
        bool _initialStateApplied;

        void OnEnable() => Bind();

        void OnDisable()
        {
            if (_inboundSubscribed && inboundRequestEvent != null)
            {
                inboundRequestEvent.OnRaised -= HandleRequest;
                _inboundSubscribed = false;
            }

            if (_buttonHooked && retryButton != null)
            {
                retryButton.onClick.RemoveListener(HandleRetryClicked);
                _buttonHooked = false;
            }

            _initialStateApplied = false;
        }

        /// <summary>
        /// Fills any missing references and (re)binds. Called by
        /// <c>BootStatusBroadcaster</c>'s runtime self-heal (Awake and Start —
        /// the outbound channel's owner may not exist yet at Awake). Safe to
        /// call repeatedly; a no-op for anything already wired in the
        /// inspector.
        /// </summary>
        public void EnsureWired(ScriptableEventBootStatusRequest inbound, ScriptableEventNoParam outboundRetry)
        {
            if (inboundRequestEvent == null) inboundRequestEvent = inbound;
            if (outboundRetryEvent == null) outboundRetryEvent = outboundRetry;

            if (isActiveAndEnabled)
                Bind();
        }

        /// <summary>
        /// Idempotent: auto-finds missing UI references, subscribes the inbound
        /// channel and the button click once each, and asserts the hidden
        /// initial state once per enable-cycle.
        /// </summary>
        void Bind()
        {
            if (statusText == null) statusText = GetComponentInChildren<TMP_Text>(true);
            if (retryButton == null) retryButton = GetComponentInChildren<Button>(true);

            if (!_inboundSubscribed && inboundRequestEvent != null)
            {
                inboundRequestEvent.OnRaised += HandleRequest;
                _inboundSubscribed = true;
            }

            if (!_buttonHooked && retryButton != null)
            {
                retryButton.onClick.AddListener(HandleRetryClicked);
                _buttonHooked = true;
            }

            if (!_initialStateApplied)
            {
                Apply(new BootStatusRequest(BootStatusMode.Hide));
                _initialStateApplied = true;
            }
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
                    Debug.Log($"[BootStatusPanel] Retry surface shown — \"{req.Text}\"");
                retryButton.gameObject.SetActive(wantRetry);
                retryButton.interactable = wantRetry;
            }
        }

        private void HandleRetryClicked()
        {
            if (retryButton != null)
                retryButton.interactable = false;

            if (outboundRetryEvent == null)
            {
                Debug.LogError("[BootStatusPanel] Retry tapped but outboundRetryEvent is not wired — " +
                               "no recovery will run. Check BootStatusBroadcaster.EnsureStatusPanelWired " +
                               "and HostConnectionService.bootStatusRetryRequestedEvent.");
                return;
            }

            outboundRetryEvent.Raise();
        }
    }
}
