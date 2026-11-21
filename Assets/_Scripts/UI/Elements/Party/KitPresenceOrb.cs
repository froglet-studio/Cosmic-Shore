using System;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The presence orb (Docs/MENU_REDESIGN_B.md §45.4, rev 6 friends option A): the FRIENDS tab is gone, and in its
    /// place a small orb in the bottom-left corner shows who is with you at a glance - you at the centre, your party
    /// bonded to you on the inner ring in their domain colours, and one dot on the slowly turning orbit for each player
    /// online. Pressing it opens the party panel (the <see cref="FriendsDrawer"/>'s handle button is the orb's).
    ///
    /// <para>It reads, never decides: the seating, the domain colours and the online count all come from the party
    /// panel's <see cref="ArcadeLobbyList"/>, the same synced roster the panel draws, and the orb repaints only when
    /// that raises <see cref="ArcadeLobbyList.PresenceChanged"/>. The rings, the orbit and the bonds animate in the
    /// <c>CosmicShore/UI/Presence</c> shader, so the orb costs nothing per frame.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Presence Orb")]
    public class KitPresenceOrb : MonoBehaviour
    {
        /// <summary>One party member's place on the orb's inner ring.</summary>
        [Serializable]
        public struct MemberSeat
        {
            [Tooltip("The seat's group: shown while a member sits here.")]
            public CanvasGroup group;
            [Tooltip("The member's avatar.")]
            public Image avatar;
            [Tooltip("The octagon behind the avatar, painted the member's domain colour (neutral blue while it is not known).")]
            public Image ring;
        }

        private static readonly int OnlineId = Shader.PropertyToID("_Online");
        private static readonly int[] BondIds =
            { Shader.PropertyToID("_Bond0"), Shader.PropertyToID("_Bond1"), Shader.PropertyToID("_Bond2") };

        [Header("Source")]
        [Tooltip("The party panel whose seating and online list the orb shows. Left empty, the orb finds the one inside " +
                 "its Friends Drawer (the menu holds exactly one).")]
        [SerializeField] private ArcadeLobbyList party;

        [Header("Orb")]
        [Tooltip("The Image drawn with CosmicShore/UI/Presence: glow, rings, the orbit with its online dots and the " +
                 "bonds. The orb draws it through its own copy of that material, written only when something changes.")]
        [SerializeField] private Graphic orb;
        [Tooltip("Most dots the orbit shows. More players than this online still reads in the count under the orb.")]
        [SerializeField, Min(0)] private int maxDots = 8;

        [Header("You")]
        [SerializeField] private Image centreAvatar;
        [Tooltip("The octagon behind your avatar, painted your domain colour (neutral blue while it is not known).")]
        [SerializeField] private Image centreRing;

        [Header("Party")]
        [Tooltip("The member seats on the inner ring, in the order of the orb material's bond angles (_BondAngles x, y, z).")]
        [SerializeField] private MemberSeat[] members = new MemberSeat[3];

        [Header("Count")]
        [SerializeField] private KitText countText;
        [Tooltip("Under the orb while other players are online. {count} is how many (you not counted). Empty shows nothing.")]
        [SerializeField] private string onlineTemplate = "{count} online";
        [Tooltip("Under the orb while nobody else is online. Empty shows nothing.")]
        [SerializeField] private string aloneText = "Friends";

        [Header("Theme")]
        [SerializeField] private UIThemeSO theme;

        private Material _material;
        private int _shownCount = -1;

        private void Awake()
        {
            if (!party)
            {
                var drawer = GetComponentInParent<FriendsDrawer>(true);
                if (drawer) party = drawer.GetComponentInChildren<ArcadeLobbyList>(true);
            }

            // UI graphics cannot take a MaterialPropertyBlock, so the orb owns one copy of its material: the online
            // count and the bond colours are written to it on change, which never rebuilds the canvas.
            if (orb && orb.material)
            {
                _material = new Material(orb.material) { name = $"{orb.material.name} (presence)" };
                orb.material = _material;
            }
        }

        private void OnEnable()
        {
            if (party) party.PresenceChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (party) party.PresenceChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (_material) Destroy(_material);
        }

        private void Refresh()
        {
            // A domain not known yet is the neutral blue: an element's colour would claim a meaning (elements carry no
            // colour in the UI, Docs/UI_COLOR_RULES.md).
            Color unknown = UITheme.Resolve(theme, UIColorToken.NeutralLightest);
            int online = party ? party.RemoteOnlineCount : 0;
            if (_material) _material.SetFloat(OnlineId, Mathf.Min(online, maxDots));
            if (countText && online != _shownCount)
            {
                _shownCount = online;
                countText.SetText(online > 0 ? onlineTemplate.Replace("{count}", online.ToString()) : aloneText);
            }

            // You at the centre; everyone else in the synced seating order on the inner ring.
            int seat = 0;
            int seats = party ? party.DrawnSeatCount : 0;
            for (int i = 0; i < seats; i++)
            {
                if (!party.TryGetSeat(i, out var avatar, out var domain, out bool isLocal)) continue;
                if (isLocal)
                {
                    if (centreAvatar) centreAvatar.sprite = avatar;
                    if (centreRing) centreRing.color = domain ?? unknown;
                    continue;
                }
                if (members == null || seat >= members.Length) continue;
                ShowMember(seat++, avatar, domain ?? unknown);
            }

            for (int i = seat; members != null && i < members.Length; i++)
                HideMember(i);
        }

        private void ShowMember(int index, Sprite avatar, Color colour)
        {
            var m = members[index];
            KitVisibility.SetShown(m.group, true);
            if (m.avatar) m.avatar.sprite = avatar;
            if (m.ring) m.ring.color = colour;
            if (_material && index < BondIds.Length) _material.SetColor(BondIds[index], colour);
        }

        private void HideMember(int index)
        {
            KitVisibility.SetShown(members[index].group, false);
            if (_material && index < BondIds.Length) _material.SetColor(BondIds[index], Color.clear);
        }
    }
}
