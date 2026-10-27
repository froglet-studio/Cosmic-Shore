using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>How a squad the player joined stands, read from its owner's public record.</summary>
    public enum SquadJoinState
    {
        /// <summary>The owner has seated the player.</summary>
        Seated = 0,
        /// <summary>The player claimed the seat; the owner has not read the claim yet (they seat it when they next open the Ark).</summary>
        Pending = 1,
        /// <summary>The owner gave the seat to someone else or closed it.</summary>
        SeatLost = 2,
        /// <summary>The owner deleted the squad.</summary>
        Disbanded = 3,
        /// <summary>The owner's record could not be read (offline); the last copy is shown.</summary>
        Unknown = 4,
    }

    /// <summary>Why a join was refused.</summary>
    public enum SquadJoinRefusal
    {
        None = 0,
        /// <summary>The player is already in <see cref="SquadBook.MaxSquads"/> squads.</summary>
        BookFull = 1,
        /// <summary>The player is already in that squad.</summary>
        AlreadyIn = 2,
        /// <summary>The squad has no seat held open.</summary>
        NoOpenSeat = 3,
        /// <summary>The player made that squad.</summary>
        OwnSquad = 4,
    }

    /// <summary>
    /// The rules of making and joining squads (the Ark's Squads tab), pure so they run in an edit-mode test. Each player
    /// writes only their own <see cref="SquadPublicRecord"/>; these functions turn the records a player can read into
    /// the state of their own <see cref="SquadBook"/>.
    /// </summary>
    public static class SquadMembership
    {
        /// <summary>The first rogue seat held open, or -1.</summary>
        public static int OpenSeat(SavedSquad squad)
        {
            if (squad == null) return -1;
            for (int i = 1; i < SavedSquad.SlotCount; i++)
                if (squad.GetSlot(i).IsOpen()) return i;
            return -1;
        }

        /// <summary>A deep copy (slots are values; the squad is not).</summary>
        public static SavedSquad Copy(SavedSquad s) => s == null ? null : new SavedSquad
        {
            Id = s.Id, Name = s.Name, OwnerId = s.OwnerId, OwnerName = s.OwnerName, MySeat = s.MySeat, Domain = s.Domain,
            Leader = s.Leader, RogueOne = s.RogueOne, RogueTwo = s.RogueTwo,
        };

        /// <summary>
        /// The record the player publishes: copies of the squads they made, and a claim for each squad they joined
        /// (their seat and their four picks in it).
        /// </summary>
        public static SquadPublicRecord BuildPublicRecord(SquadBook book, string playerName)
        {
            var record = new SquadPublicRecord { PlayerName = playerName };
            if (book?.Squads == null) return record;
            foreach (var s in book.Squads)
            {
                if (s == null) continue;
                if (!s.IsJoined())
                {
                    record.Squads.Add(Copy(s));
                    continue;
                }
                record.Claims.Add(new SquadClaim
                {
                    OwnerId = s.OwnerId, SquadId = s.Id, Seat = s.MySeat, Loadout = s.GetSlot(s.MySeat),
                });
            }
            return record;
        }

        /// <summary>
        /// The owner's side: seats the claims friends published on <paramref name="mine"/>, refreshes the picks and
        /// names of friends already seated, and opens again the seat of a friend whose record no longer claims it (they
        /// left). <paramref name="records"/> are the records the owner could read this time, in the order claims win (a
        /// friend whose record could not be read keeps their seat). A claim on a seat the owner made an AI seat is not
        /// seated. True when anything changed.
        /// </summary>
        public static bool ReconcileOwned(SavedSquad mine, string myId,
            IReadOnlyList<(string playerId, SquadPublicRecord record)> records)
        {
            if (mine == null || mine.IsJoined() || records == null) return false;
            bool changed = false;

            for (int seat = 1; seat < SavedSquad.SlotCount; seat++)
            {
                var slot = mine.GetSlot(seat);
                if (slot.IsPlayer())
                {
                    if (!TryFind(records, slot.MemberId, out var record)) continue;   // unread: keep them
                    var claim = record.FindClaim(myId, mine.Id);
                    var next = claim != null && claim.Seat == seat
                        ? slot.WithLoadout(claim.Loadout).WithMember(slot.MemberId, NameOr(record, slot.MemberName))
                        : slot.WithSeat(SquadSeat.Open);
                    changed |= Set(mine, seat, slot, next);
                }
                else if (slot.IsOpen())
                {
                    foreach (var (playerId, record) in records)
                    {
                        if (record == null || string.IsNullOrEmpty(playerId) || playerId == myId) continue;
                        if (HoldsAnotherSeat(mine, playerId, seat)) continue;
                        var claim = record.FindClaim(myId, mine.Id);
                        if (claim == null || claim.Seat != seat) continue;
                        changed |= Set(mine, seat, slot, slot.WithLoadout(claim.Loadout).WithMember(playerId, record.PlayerName));
                        break;
                    }
                }
            }
            return changed;
        }

        /// <summary>
        /// The joiner's side: refreshes <paramref name="joined"/> from its owner's record (<paramref name="ownerRecord"/>
        /// null when it could not be read) and says how the player's seat stands. The player's own picks always win in
        /// their seat: the owner's copy of them can lag a publish behind.
        /// </summary>
        public static SquadJoinState RefreshJoined(SavedSquad joined, string myId, SquadPublicRecord ownerRecord)
        {
            if (joined == null || !joined.IsJoined()) throw new ArgumentException("not a joined squad", nameof(joined));
            if (ownerRecord == null) return SquadJoinState.Unknown;

            var source = ownerRecord.FindSquad(joined.Id);
            if (source == null) return SquadJoinState.Disbanded;

            joined.Name = source.Name;
            joined.Domain = source.Domain;
            if (!string.IsNullOrEmpty(ownerRecord.PlayerName)) joined.OwnerName = ownerRecord.PlayerName;
            for (int i = 0; i < SavedSquad.SlotCount; i++)
                if (i != joined.MySeat) joined.SetSlot(i, source.GetSlot(i));

            var ownerSeat = source.GetSlot(joined.MySeat);
            if (ownerSeat.IsPlayer() && ownerSeat.MemberId == myId) return SquadJoinState.Seated;
            if (ownerSeat.IsOpen()) return SquadJoinState.Pending;
            // The seat went to someone else, or the owner closed it: show what the owner now has there.
            joined.SetSlot(joined.MySeat, ownerSeat);
            return SquadJoinState.SeatLost;
        }

        /// <summary>
        /// Joins <paramref name="ownerSquad"/> (read from <paramref name="ownerId"/>'s record) in its first open seat with
        /// <paramref name="myLoadout"/>, adding the copy to <paramref name="book"/>. The owner seats the player when they
        /// next read the player's record.
        /// </summary>
        public static SquadJoinRefusal TryJoin(SquadBook book, string myId, string myName, string ownerId, string ownerName,
            SavedSquad ownerSquad, SquadSlot myLoadout, out SavedSquad joined)
        {
            joined = null;
            if (book == null) throw new ArgumentNullException(nameof(book));
            if (ownerSquad == null) throw new ArgumentNullException(nameof(ownerSquad));
            if (string.IsNullOrEmpty(ownerId) || ownerId == myId) return SquadJoinRefusal.OwnSquad;
            if (Contains(book, ownerId, ownerSquad.Id)) return SquadJoinRefusal.AlreadyIn;
            if (!book.CanAdd()) return SquadJoinRefusal.BookFull;
            int seat = OpenSeat(ownerSquad);
            if (seat < 0) return SquadJoinRefusal.NoOpenSeat;

            joined = Copy(ownerSquad);
            joined.OwnerId = ownerId;
            joined.OwnerName = ownerName;
            joined.MySeat = seat;
            joined.SetSlot(seat, ownerSquad.GetSlot(seat).WithLoadout(myLoadout).WithMember(myId, myName));
            book.Add(joined);
            return SquadJoinRefusal.None;
        }

        /// <summary>True when the player is in <paramref name="ownerId"/>'s squad <paramref name="squadId"/>.</summary>
        public static bool Contains(SquadBook book, string ownerId, string squadId)
        {
            if (book?.Squads == null) return false;
            foreach (var s in book.Squads)
                if (s != null && s.IsJoined() && s.OwnerId == ownerId && s.Id == squadId) return true;
            return false;
        }

        static bool HoldsAnotherSeat(SavedSquad squad, string playerId, int exceptSeat)
        {
            for (int i = 1; i < SavedSquad.SlotCount; i++)
            {
                if (i == exceptSeat) continue;
                var s = squad.GetSlot(i);
                if (s.IsPlayer() && s.MemberId == playerId) return true;
            }
            return false;
        }

        static bool TryFind(IReadOnlyList<(string playerId, SquadPublicRecord record)> records, string id, out SquadPublicRecord found)
        {
            foreach (var (playerId, record) in records)
            {
                if (playerId != id || record == null) continue;
                found = record;
                return true;
            }
            found = null;
            return false;
        }

        static string NameOr(SquadPublicRecord record, string fallback) =>
            string.IsNullOrEmpty(record.PlayerName) ? fallback : record.PlayerName;

        static bool Set(SavedSquad squad, int seat, SquadSlot before, SquadSlot after)
        {
            if (before.Equals(after)) return false;
            squad.SetSlot(seat, after);
            return true;
        }
    }
}
