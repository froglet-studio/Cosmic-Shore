using CosmicShore.Core;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The main-menu "Retry connection" control for an OFFLINE session: one tap re-runs the
    /// boot chain in place (<see cref="ReconnectService"/>) so the player can come back online
    /// without restarting the app.
    ///
    /// <para>
    /// Shows itself only while a reconnect is worth offering
    /// (<see cref="ReconnectService.CanReconnect"/>), disables itself for the duration of an
    /// attempt, and - because a failed retry falls back to offline rather than stranding the
    /// player - simply becomes available again if the network is still down.
    /// </para>
    ///
    /// <para>
    /// Wire it next to the offline notice on an <see cref="OfflineUIGate"/>'s
    /// offline-only list, so the notice and this button appear and disappear together.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ReconnectButton : MonoBehaviour
    {
        [Header("Optional")]
        [SerializeField, Tooltip("Label swapped to the busy text while an attempt is in flight. Optional.")]
        TMP_Text label;

        [SerializeField, Tooltip("Label text at rest.")]
        string idleText = "Retry Connection";

        [SerializeField, Tooltip("Label text while reconnecting.")]
        string busyText = "Reconnecting…";

        [SerializeField, Tooltip("Hide the whole GameObject when a reconnect is not applicable (i.e. already online). " +
                                 "Leave off when an OfflineUIGate already owns this object's visibility.")]
        bool hideWhenOnline = true;

        [Inject] ReconnectService _reconnect;

        Button _button;
        bool _subscribed;

        void Awake() => _button = GetComponent<Button>();

        void OnEnable()
        {
            if (_button != null)
                _button.onClick.AddListener(HandleClick);

            Refresh();
            TrySubscribe();
        }

        void Start()
        {
            // [Inject] resolves between Awake and Start - OnEnable's first pass may have run
            // with a null service.
            Refresh();
            TrySubscribe();
        }

        void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClick);

            if (_subscribed && _reconnect != null)
            {
                _reconnect.OnReconnectingChanged -= HandleReconnectingChanged;
                _subscribed = false;
            }
        }

        void TrySubscribe()
        {
            if (_subscribed || _reconnect == null) return;
            _reconnect.OnReconnectingChanged += HandleReconnectingChanged;
            _subscribed = true;
        }

        void HandleReconnectingChanged(bool _) => Refresh();

        void Refresh()
        {
            if (_reconnect == null)
            {
                // No service (scene without a ContainerScope): fail closed - a retry button
                // that cannot retry is worse than no button.
                if (hideWhenOnline) gameObject.SetActive(false);
                return;
            }

            bool busy = _reconnect.IsReconnecting;

            if (hideWhenOnline && !busy && !_reconnect.CanReconnect)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_button != null)
                _button.interactable = !busy;

            if (label != null)
                label.text = busy ? busyText : idleText;
        }

        void HandleClick()
        {
            if (_reconnect == null || _reconnect.IsReconnecting) return;

            CSDebug.Log("[ReconnectButton] Retry connection tapped.");
            _reconnect.ReconnectAsync().Forget();
        }
    }
}
