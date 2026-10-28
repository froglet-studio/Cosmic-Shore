using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// A named squad of three: the player plus two friends, two AI, or a mix. Saving one is saving a
    /// deck; before a game the player picks which to play (Docs/PILOTS_AND_ARTIFACTS.md §1).
    /// Held in a <see cref="SquadBook"/>.
    ///
    /// <para>A squad is either one the player MADE (<see cref="OwnerId"/> empty; they are its leader) or one they JOINED
    /// (<see cref="OwnerId"/> names the friend who made it, and <see cref="MySeat"/> is the rogue seat they hold). A
    /// joined squad is a copy of the owner's, refreshed from the owner's public squad record (<c>SquadDirectory</c>)
    /// whenever the Ark's Squads tab opens; <see cref="SquadMembership"/> holds the rules.</para>
    /// </summary>
    [Serializable]
    public class SavedSquad
    {
        public const int SlotCount = 3;

        /// <summary>Stable id the owner mints; a joined copy carries the owner's. Empty is repaired on load.</summary>
        public string Id;
        public string Name;
        /// <summary>The UGS player id of the friend who made this squad; empty for a squad the player made.</summary>
        public string OwnerId;
        /// <summary>The owner's display name when the squad was last read.</summary>
        public string OwnerName;
        /// <summary>The seat the player holds: 0 (the leader) in a squad they made, 1 or 2 in one they joined.</summary>
        public int MySeat;
        /// <summary>
        /// The squad's domain (its team colour: Jade, Ruby or Gold). The owner sets it; a joined copy reads it from the owner.
        /// A record saved before squads had one reads as Jade.
        /// </summary>
        public Domains Domain = Domains.Jade;
        public SquadSlot Leader;
        public SquadSlot RogueOne;
        public SquadSlot RogueTwo;

        public SavedSquad() { }

        public SavedSquad(string name, SquadSlot leader, SquadSlot rogueOne, SquadSlot rogueTwo)
        {
            Id = NewId();
            Name = name;
            Leader = leader;
            RogueOne = rogueOne;
            RogueTwo = rogueTwo;
        }

        /// <summary>Slot 0 is the leader, 1 is Rogue One, 2 is Rogue Two.</summary>
        public SquadSlot GetSlot(int index) => index switch
        {
            0 => Leader,
            1 => RogueOne,
            2 => RogueTwo,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "A squad has three slots: 0, 1 and 2."),
        };

        /// <summary>Slot 0 is the leader, 1 is Rogue One, 2 is Rogue Two.</summary>
        public void SetSlot(int index, SquadSlot slot)
        {
            switch (index)
            {
                case 0: Leader = slot; break;
                case 1: RogueOne = slot; break;
                case 2: RogueTwo = slot; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index), index, "A squad has three slots: 0, 1 and 2.");
            }
        }

        /// <summary>
        /// Each vessel has its own pilot and its own artifact (Garrett, 2026-10-27). The pilot and artifact now on
        /// <paramref name="seat"/> leave any other slot that carries the same one: a vessel always has a pilot, so the
        /// other slot takes <paramref name="previousPilotId"/> (the pilot <paramref name="seat"/> had); an artifact slot
        /// may be empty, so the other slot's artifact is cleared. True when another slot changed.
        /// </summary>
        public bool KeepCardsOwn(int seat, string previousPilotId)
        {
            var mine = GetSlot(seat);
            bool changed = false;
            for (int i = 0; i < SlotCount; i++)
            {
                if (i == seat) continue;
                var other = GetSlot(i);
                bool clash = false;
                if (!string.IsNullOrEmpty(mine.PilotId) && other.PilotId == mine.PilotId)
                {
                    other.PilotId = previousPilotId == mine.PilotId ? null : previousPilotId;
                    clash = true;
                }
                if (!string.IsNullOrEmpty(mine.ArtifactId) && other.ArtifactId == mine.ArtifactId) { other.ArtifactId = null; clash = true; }
                if (!clash) continue;
                SetSlot(i, other);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Every vessel needs a pilot (Garrett, 2026-10-27): each slot with no pilot, or with one another slot already
        /// flies, takes the first of <paramref name="pilotIds"/> no slot flies yet. True when a slot changed.
        /// </summary>
        public bool FillPilots(IReadOnlyList<string> pilotIds)
        {
            if (pilotIds == null) return false;
            bool changed = false;
            for (int i = 0; i < SlotCount; i++)
            {
                var slot = GetSlot(i);
                bool taken = false;
                for (int j = 0; j < i; j++) taken |= GetSlot(j).PilotId == slot.PilotId;
                if (!string.IsNullOrEmpty(slot.PilotId) && !taken) continue;
                foreach (var id in pilotIds)
                {
                    if (string.IsNullOrEmpty(id) || Flies(id)) continue;
                    slot.PilotId = id;
                    SetSlot(i, slot);
                    changed = true;
                    break;
                }
            }
            return changed;
        }

        bool Flies(string pilotId) =>
            Leader.PilotId == pilotId || RogueOne.PilotId == pilotId || RogueTwo.PilotId == pilotId;

        /// <summary>True when a friend made this squad and the player joined it.</summary>
        public bool IsJoined() => !string.IsNullOrEmpty(OwnerId);

        /// <summary>
        /// A copy under a new name and a new id, for "save as" and for starting a new squad from this one. The copy is
        /// the player's own: whoever held its rogue seats is not carried over (an open seat stays open).
        /// </summary>
        public SavedSquad Clone(string name)
        {
            static SquadSlot Unheld(SquadSlot s) => s.IsPlayer() ? s.WithSeat(SquadSeat.Open) : s;
            return new SavedSquad(name, Leader, Unheld(RogueOne), Unheld(RogueTwo)) { Domain = Domain };
        }

        /// <summary>A fresh squad id: 32 hex characters, unique enough for a player's handful of squads.</summary>
        public static string NewId() => Guid.NewGuid().ToString("N");
    }
}
