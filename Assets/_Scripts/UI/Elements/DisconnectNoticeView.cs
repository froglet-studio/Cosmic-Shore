using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The style-B disconnect notice (Docs/MENU_REDESIGN_B.md section 100.2): the boot Retry window's look - a
    /// slivered window with an eyebrow, a title and a line - with RECONNECT (A, lime) and CONTINUE OFFLINE (B).
    /// Authored by <c>Tools/Build/author_disconnect_notice.py</c>; <see cref="DisconnectNotice"/> instantiates it
    /// from <c>DisconnectNoticeConfigSO.WindowPrefab</c> on its persistent canvas and fills every word from the
    /// config, so the prefab carries layout only.
    /// </summary>
    public class DisconnectNoticeView : MonoBehaviour
    {
        [Tooltip("The window's own group: DisconnectNotice fades it and gates it (interactable), and the dismiss " +
                 "button's B press reads it.")]
        [SerializeField] CanvasGroup group;

        [Header("Words (filled from DisconnectNoticeConfigSO)")]
        [Tooltip("The window's heading: Connection Lost, or Back Online.")]
        [SerializeField] TMP_Text title;
        [Tooltip("The line under the heading. Hidden when the state has none (Back Online).")]
        [SerializeField] TMP_Text body;

        [Header("Controls")]
        [Tooltip("Re-runs the boot chain (ReconnectService). Selected as the notice opens, so A presses it.")]
        [SerializeField] Button reconnectButton;
        [Tooltip("RECONNECT's label; it reads the config's Reconnecting label while an attempt is in flight.")]
        [SerializeField] TMP_Text reconnectLabel;
        [Tooltip("Leaves the notice and carries on offline (B).")]
        [SerializeField] Button dismissButton;
        [Tooltip("CONTINUE OFFLINE's label.")]
        [SerializeField] TMP_Text dismissLabel;

        public CanvasGroup Group => group;
        public TMP_Text Title => title;
        public TMP_Text Body => body;
        public Button ReconnectButton => reconnectButton;
        public TMP_Text ReconnectLabel => reconnectLabel;
        public Button DismissButton => dismissButton;
        public TMP_Text DismissLabel => dismissLabel;
    }
}
