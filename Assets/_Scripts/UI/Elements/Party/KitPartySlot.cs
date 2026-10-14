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
    /// </summary>
    public class KitPartySlot : FriendInfoSlot
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

        // The ring replaces the legacy slot's generated halo, so none is built.
        protected override void EnsureDomainGlow() { }

        public override void SetDomainGlow(Color? domainColour)
        {
            if (domainRing)
                domainRing.color = domainColour ?? UITheme.Resolve(theme, UIColorToken.SurfaceDark);
        }

        public override void SetRole(bool isLocal, bool isHost)
        {
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
            }
            else if (part == KickButton)
            {
                KitVisibility.SetShown(kickGroup, shown);
            }
            // The avatar and the name live in the occupied layer, which shows and hides them together.
        }
    }
}
