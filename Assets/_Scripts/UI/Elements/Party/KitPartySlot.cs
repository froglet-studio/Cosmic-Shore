using System;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One seat of the style-B party panel (Docs/MENU_REDESIGN_B.md, item 3): the same seating,
    /// kick and add behaviour as <see cref="FriendInfoSlot"/>, drawn in the kit.
    ///
    /// <para>A seat is two layers on top of each other. The OCCUPIED layer is the framed row - the
    /// avatar in an octagon ring painted the pilot's domain colour, the name, the role line ("You /
    /// Host", "Pilot") and, for the host looking at a remote seat, the kick button. The EMPTY layer is
    /// the slot's add button, "Open slot - invite a friend below". Exactly one shows, through
    /// CanvasGroups; nothing here is SetActive.</para>
    ///
    /// <para>A remote member's seat is also a profile source (rev 7): hovering its avatar opens
    /// <see cref="FriendProfilePopup"/>.</para>
    /// </summary>
    public class KitPartySlot : FriendInfoSlot, IFriendProfileSource
    {
        [Header("Style-B Layers")]
        [Tooltip("The filled seat: frame, avatar ring, name, role and kick button.")]
        [SerializeField] private CanvasGroup occupiedGroup;
        [Tooltip("The open seat: this slot's add button.")]
        [SerializeField] private CanvasGroup emptyGroup;
        [Tooltip("The kick button's own group, shown only on a remote seat the local host may kick.")]
        [SerializeField] private CanvasGroup kickGroup;

        [Header("Seat")]
        [Tooltip("\"You / Host\", \"You\", \"Host\" or \"Pilot\" (Label role).")]
        [SerializeField] private KitText roleText;
        [Tooltip("The octagon behind the avatar. The party panel pushes the seated pilot's domain colour " +
                 "(SO_ColorSet) every tick; Surface Dark while that domain is not known yet.")]
        [SerializeField] private Image domainRing;
        [Tooltip("The chrome theme, for the ring's unknown-domain colour.")]
        [SerializeField] private UIThemeSO theme;

        // The seated pilot's domain colour, for the profile popup's ring.
        private Color? _domain;

        /// <summary>Raised when the seat's pilot or their domain colour changes (the profile popup follows it).</summary>
        public event Action ProfileChanged;

        /// <summary>
        /// A remote member's seat as a profile: already in your party, so the popup offers nothing to press. Your own
        /// seat and an open seat have none.
        /// </summary>
        public bool TryGetProfile(out FriendProfile profile)
        {
            profile = new FriendProfile(PlayerId, DisplayNameText ? DisplayNameText.text : string.Empty,
                                        AvatarIcon ? AvatarIcon.sprite : null, _domain, OnlineInfoEntry.Status.InYourParty,
                                        0, 0, null, false, false, OnlineInfoEntry.JoinMode.Hidden);
            return IsOccupied && !IsLocalPlayer;
        }

        public void RequestInvite() { }

        public void RequestJoin() { }

        // The ring replaces the legacy slot's generated halo, so none is built.
        protected override void EnsureDomainGlow() { }

        public override void SetDomainGlow(Color? domainColour)
        {
            if (domainRing)
                domainRing.color = domainColour ?? UITheme.Resolve(theme, UIColorToken.SurfaceDark);
            if (_domain == domainColour) return;
            _domain = domainColour;
            ProfileChanged?.Invoke();
        }

        public override void SetRole(bool isLocal, bool isHost)
        {
            // Called after every seat assignment: the seat's pilot may have changed.
            ProfileChanged?.Invoke();
            if (!roleText) return;
            roleText.SetText(isLocal ? (isHost ? "You / Host" : "You") : (isHost ? "Host" : "Pilot"));
            roleText.SetColorToken(isLocal ? UIColorToken.TextLight : UIColorToken.NeutralLightest);
        }

        protected override void SetPartShown(Component part, bool shown)
        {
            if (part == AddButton)
            {
                KitVisibility.SetShown(emptyGroup, shown);
                KitVisibility.SetShown(occupiedGroup, !shown);
                ProfileChanged?.Invoke();
            }
            else if (part == KickButton)
            {
                KitVisibility.SetShown(kickGroup, shown);
            }
            // The avatar and the name live in the occupied layer, which shows and hides them together.
        }
    }
}
