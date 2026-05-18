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

        void OnEnable()
        {
            if (inboundRequestEvent != null)
                inboundRequestEvent.OnRaised += HandleRequest;

            if (retryButton != null)
                retryButton.onClick.AddListener(HandleRetryClicked);

            Apply(new BootStatusRequest(BootStatusMode.Hide));
        }

        void OnDisable()
        {
            if (inboundRequestEvent != null)
                inboundRequestEvent.OnRaised -= HandleRequest;

            if (retryButton != null)
                retryButton.onClick.RemoveListener(HandleRetryClicked);
        }

        private void HandleRequest(BootStatusRequest req) => Apply(req);

        private void Apply(BootStatusRequest req)
        {
            if (statusText != null)
                statusText.text = req.Text ?? string.Empty;

            if (retryButton != null)
            {
                bool wantRetry = req.Mode == BootStatusMode.Retry;
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
    }
}
