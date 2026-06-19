using Cysharp.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Bridges incoming party invites to the generic popup system: an incoming invite surfaces as a
    /// small, non-modal, bottom-left popup with Accept / Decline, in parallel with the
    /// FriendsListPanel Requests row. Latest-wins — a newer invite instantly replaces the current
    /// popup (shared <see cref="replaceKey"/>). Auto-hides after a few seconds without declining
    /// (the invite lives on in the panel). Resolving the invite from EITHER surface dismisses the
    /// popup (via <see cref="HostConnectionDataSO.OnInviteResolved"/>).
    ///
    /// Lives on a Menu_Main UI GameObject. Reads SOAP only — no direct HostConnectionService ref
    /// beyond the singleton PartyInviteController used to route accept/decline.
    /// </summary>
    public class InvitePopupController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private HostConnectionDataSO connectionData;
        [SerializeField] private PopupChannel popupChannel;

        [Header("Copy")]
        [SerializeField] private string title = "PARTY INVITE";
        [SerializeField] private string acceptLabel = "Accept";
        [SerializeField] private string declineLabel = "Decline";
        [Tooltip("{0} = inviter display name.")]
        [SerializeField] private string messageFormat = "{0} invited you to their party";

        [Header("Behaviour")]
        [Tooltip("Seconds the bottom-left popup stays before auto-hiding. Hiding does NOT decline — " +
                 "the invite remains in the FriendsListPanel Requests list.")]
        [SerializeField] private float autoHideSeconds = 3f;
        [Tooltip("Shared key so a newer invite replaces the current popup (latest-wins) rather than stacking.")]
        [SerializeField] private string replaceKey = "party_invite";

        private void OnEnable()
        {
            if (connectionData == null) return;
            if (connectionData.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised += HandleInviteReceived;
            if (connectionData.OnInviteResolved != null)
                connectionData.OnInviteResolved.OnRaised += HandleInviteResolved;
        }

        private void OnDisable()
        {
            if (connectionData == null) return;
            if (connectionData.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised -= HandleInviteReceived;
            if (connectionData.OnInviteResolved != null)
                connectionData.OnInviteResolved.OnRaised -= HandleInviteResolved;
        }

        private void HandleInviteReceived(PartyInviteData invite)
        {
            if (popupChannel == null) return;

            string message = string.Format(messageFormat,
                string.IsNullOrEmpty(invite.HostDisplayName) ? "A player" : invite.HostDisplayName);

            popupChannel.Show(new PopupRequest(
                title: title,
                message: message,
                primaryLabel: acceptLabel,
                secondaryLabel: declineLabel,
                modal: false,
                placement: PopupPlacement.BottomLeft,
                autoHideSeconds: autoHideSeconds,
                replaceKey: replaceKey,
                onResult: result => OnPopupResult(result, invite)));
        }

        private void OnPopupResult(PopupResult result, PartyInviteData invite)
        {
            var controller = PartyInviteController.Instance;
            if (controller == null) return;

            switch (result)
            {
                case PopupResult.Primary:
                    controller.AcceptInviteAsync(invite).Forget();
                    break;
                case PopupResult.Secondary:
                    controller.DeclineInviteAsync().Forget();
                    break;
                // PopupResult.Dismissed (auto-hide timeout): intentionally do nothing — the popup is
                // just a transient teaser; the invite stays in the FriendsListPanel Requests list for
                // its full lifetime, and the host's pending state is cleared by its own timeout.
            }
        }

        // An invite was resolved on ANY surface (this popup, the FriendsListPanel row, or a leave) —
        // silently dismiss the popup so both surfaces clear together. If the popup already closed via
        // its own button, this is a harmless no-op.
        private void HandleInviteResolved() => popupChannel?.Dismiss(replaceKey);
    }
}
