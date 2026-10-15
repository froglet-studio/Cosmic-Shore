using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One online pilot in the style-B party panel's list (Docs/MENU_REDESIGN_B.md, item 3): the
    /// same invite, cancel, kick, join and spectate behaviour as <see cref="OnlineInfoEntry"/>,
    /// drawn in the kit.
    ///
    /// <para>The status line takes the state's colour - CTA while the pilot can be invited or
    /// joined, Purchase while they are in a match, Neutral while their party is full or our invite
    /// is out. The buttons are kit buttons that leave the row's layout when hidden: Invite (Default),
    /// Join (CTA) or Spectate (Purchase) on the one join control, and the ✕ that retracts an invite or
    /// kicks a member. While our invite is out the Invite button stays in place, inactive, reading
    /// "Invited".</para>
    /// </summary>
    public class KitOnlineRow : OnlineInfoEntry
    {
        [Header("Style-B")]
        [Tooltip("The status line's KitText (on the same object as Label Text).")]
        [SerializeField] private KitText statusText;
        [Tooltip("The Invite button's label, which reads \"Invited\" while our invite is out.")]
        [SerializeField] private KitText inviteLabel;
        [Tooltip("The join button's label: joinWord or spectateWord.")]
        [SerializeField] private KitText joinLabel;

        [Header("Words (empty = blank label)")]
        [SerializeField] private string joinWord = "Join";
        [SerializeField] private string spectateWord = "Spectate";
        [SerializeField] private string inviteWord = "Invite";
        [SerializeField] private string invitedWord = "Invited";
        [SerializeField] private CanvasGroup inviteGroup;
        [SerializeField] private CanvasGroup joinGroup;
        [SerializeField] private CanvasGroup cancelGroup;

        protected override void SetPartShown(Component part, bool shown)
        {
            if (part == InviteButton) KitVisibility.SetShown(inviteGroup, shown);
            else if (part == JoinButton) KitVisibility.SetShown(joinGroup, shown);
            else if (part == CancelButton) KitVisibility.SetShown(cancelGroup, shown);
        }

        protected override void DrawLabel(Status status, bool pending, string text, Color color)
        {
            if (!statusText) return;
            statusText.SetText(text);
            statusText.SetColorToken(pending ? UIColorToken.NeutralLightest : status switch
            {
                Status.InMatch => UIColorToken.Purchase,
                Status.LobbyFull => UIColorToken.NeutralLightest,
                Status.InYourParty => UIColorToken.ControlActive,
                _ => UIColorToken.Cta,
            });
        }

        protected override void DrawJoinButton(bool spectate, bool live)
        {
            // Not pressable reads as the kit's Inactive look through the button's own interactable flag.
            if (JoinButton is KitButton button)
                button.Variant = spectate ? KitButtonVariant.Purchase : KitButtonVariant.Cta;
            if (joinLabel) joinLabel.SetText(spectate ? spectateWord : joinWord);
        }

        // The kit row has no tinted background: the status line carries the state.
        protected override void ApplyRowTint(Color c) { }

        protected override void OnInvitePendingChanged(bool pending)
        {
            if (pending && InviteButton)
            {
                KitVisibility.SetShown(inviteGroup, true);
                InviteButton.interactable = false;
            }
            if (inviteLabel) inviteLabel.SetText(pending ? invitedWord : inviteWord);
        }
    }
}
