using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// What a player publishes so friends can see and join their squads (the Ark's Squads tab): the squads they made,
    /// and the seats they hold in friends' squads. Saved as UGS Cloud Save PUBLIC player data under
    /// <c>SquadDirectory.PublicKey</c>, which any signed-in player can read by player id, and only its owner can write.
    ///
    /// <para>There is no server in the middle. A friend joins by writing a <see cref="SquadClaim"/> into THEIR record;
    /// the owner, reading their friends' records, seats the first claim on each open seat
    /// (<see cref="SquadMembership.ReconcileOwned"/>) and publishes the result in THEIR record, which the joiner then
    /// reads back (<see cref="SquadMembership.RefreshJoined"/>). Each player only ever writes their own record.</para>
    /// </summary>
    [Serializable]
    public class SquadPublicRecord
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        /// <summary>The publisher's display name.</summary>
        public string PlayerName;
        /// <summary>The squads the publisher made (each with <c>OwnerId</c> empty and seat 0 theirs).</summary>
        public List<SavedSquad> Squads = new();
        /// <summary>The seats the publisher holds in squads friends made.</summary>
        public List<SquadClaim> Claims = new();

        /// <summary>The squad with this id among the ones the publisher made, or null.</summary>
        public SavedSquad FindSquad(string squadId)
        {
            if (Squads == null || string.IsNullOrEmpty(squadId)) return null;
            foreach (var s in Squads)
                if (s != null && s.Id == squadId) return s;
            return null;
        }

        /// <summary>The publisher's claim on <paramref name="ownerId"/>'s squad <paramref name="squadId"/>, or null.</summary>
        public SquadClaim FindClaim(string ownerId, string squadId)
        {
            if (Claims == null) return null;
            foreach (var c in Claims)
                if (c != null && c.OwnerId == ownerId && c.SquadId == squadId) return c;
            return null;
        }
    }

    /// <summary>A seat a player holds in a friend's squad, and the four picks they fly it with.</summary>
    [Serializable]
    public class SquadClaim
    {
        /// <summary>The UGS player id of the friend who made the squad.</summary>
        public string OwnerId;
        public string SquadId;
        /// <summary>1 (Rogue One) or 2 (Rogue Two).</summary>
        public int Seat;
        /// <summary>The claimant's vessel, element, pilot and artifact for the seat (who flies it is not read).</summary>
        public SquadSlot Loadout;
    }
}
