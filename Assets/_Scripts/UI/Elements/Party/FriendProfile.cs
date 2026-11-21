using System;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// What a pilot's profile popup shows (Rev 7, Docs/MENU_REDESIGN_B.md §47.3): who they are and where they are, read
    /// from the row or seat that already draws them, so the popup never asks the party layer a second time.
    /// </summary>
    public readonly struct FriendProfile
    {
        public readonly string PlayerId;
        public readonly string DisplayName;
        public readonly Sprite Avatar;
        /// <summary>The ring round the avatar: the pilot's domain colour when the seat knows it.</summary>
        public readonly Color? DomainColour;
        public readonly OnlineInfoEntry.Status Status;
        public readonly int PartyMemberCount;
        public readonly int PartyMaxSlots;
        public readonly string MatchName;
        /// <summary>INVITE would send now.</summary>
        public readonly bool CanInvite;
        /// <summary>Our invite to them is out (INVITE reads "Invited", inert).</summary>
        public readonly bool InvitePending;
        public readonly OnlineInfoEntry.JoinMode Join;

        public FriendProfile(string playerId, string displayName, Sprite avatar, Color? domainColour,
                             OnlineInfoEntry.Status status, int partyMemberCount, int partyMaxSlots, string matchName,
                             bool canInvite, bool invitePending, OnlineInfoEntry.JoinMode join)
        {
            PlayerId = playerId;
            DisplayName = displayName;
            Avatar = avatar;
            DomainColour = domainColour;
            Status = status;
            PartyMemberCount = partyMemberCount;
            PartyMaxSlots = partyMaxSlots;
            MatchName = matchName;
            CanInvite = canInvite;
            InvitePending = invitePending;
            Join = join;
        }
    }

    /// <summary>A row or seat a <see cref="FriendProfilePopup"/> can open on.</summary>
    public interface IFriendProfileSource
    {
        /// <summary>The pilot shown here, or false when there is none to show (an open seat, your own seat).</summary>
        bool TryGetProfile(out FriendProfile profile);

        /// <summary>Raised when what <see cref="TryGetProfile"/> answers changes, so an open popup follows it.</summary>
        event Action ProfileChanged;

        /// <summary>The popup's INVITE: the same press as the source's own Invite.</summary>
        void RequestInvite();

        /// <summary>The popup's JOIN / SPECTATE: the same press as the source's own join control.</summary>
        void RequestJoin();
    }
}
