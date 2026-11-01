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

        [Header("One verb (Rev 40 E, Docs/MENU_REDESIGN_B.md section 107)")]
        [Tooltip("Show at most ONE button: Cancel while our invite is out, else Join / Spectate, else Invite. The " +
                 "three share one fixed slot, so a row can never draw two buttons over each other (2026-10-31: " +
                 "INVITE and SPECTATE overlapped). The pending invite shows Cancel, not a dimmed Invited.")]
        [SerializeField] private bool oneVerbPerRow;

        bool _wantInvite, _wantJoin, _wantCancel;

        protected override void SetPartShown(Component part, bool shown)
        {
            if (part == InviteButton) _wantInvite = shown;
            else if (part == JoinButton) _wantJoin = shown;
            else if (part == CancelButton) _wantCancel = shown;
            else return;
            ApplyParts();
        }

        void ApplyParts()
        {
            var (invite, join, cancel) = VisibleParts(oneVerbPerRow, _wantInvite, _wantJoin, _wantCancel,
                                                      preferInvite: CurrentStatus == Status.Online);
            KitVisibility.SetShown(inviteGroup, invite);
            KitVisibility.SetShown(joinGroup, join);
            KitVisibility.SetShown(cancelGroup, cancel);
        }

        /// <summary>
        /// Which buttons show, from the ones the row asked for. With one verb a row shows a single button: Cancel wins,
        /// then Join / Spectate, then Invite - except for a pilot alone in the menu (<paramref name="preferInvite"/>),
        /// whose row says Invite while Join stays on offer in their profile card. Pure, so it is tested without a row.
        /// </summary>
        internal static (bool invite, bool join, bool cancel) VisibleParts(bool oneVerb, bool invite, bool join, bool cancel,
                                                                           bool preferInvite = false)
        {
            if (!oneVerb) return (invite, join, cancel);
            if (cancel) return (false, false, true);
            if (invite && preferInvite) return (true, false, false);
            if (join) return (false, true, false);
            return (invite, false, false);
        }

        protected override void DrawLabel(Status status, bool pending, string text, Color color)
        {
            if (!statusText) return;
            statusText.SetText(text);
            // Colour roles (Docs/UI_COLOR_RULES.md): online and free to join is good news, lime; in a match is a neutral
            // fact, blue; full or pending is quiet. No element colours.
            statusText.SetColorToken(pending ? UIColorToken.NeutralLightest : status switch
            {
                Status.InMatch => UIColorToken.ControlActive,
                Status.LobbyFull => UIColorToken.NeutralLightest,
                _ => UIColorToken.Cta,
            });
        }

        protected override void DrawJoinButton(bool spectate, bool live)
        {
            // Not pressable reads as the kit's Inactive look through the button's own interactable flag.
            if (JoinButton is KitButton button)
                button.Variant = spectate ? KitButtonVariant.Default : KitButtonVariant.Play;   // joining is the call to action; watching is neutral
            if (joinLabel) joinLabel.SetText(spectate ? spectateWord : joinWord);
        }

        // The kit row has no tinted background: the status line carries the state.
        protected override void ApplyRowTint(Color c) { }

        protected override void OnInvitePendingChanged(bool pending)
        {
            // With one verb the pending row shows only its Cancel (ApplyParts); otherwise the Invite stays, dimmed.
            if (pending && InviteButton && !oneVerbPerRow)
            {
                _wantInvite = true;
                ApplyParts();
                InviteButton.interactable = false;
            }
            if (inviteLabel) inviteLabel.SetText(pending ? invitedWord : inviteWord);
        }
    }
}
