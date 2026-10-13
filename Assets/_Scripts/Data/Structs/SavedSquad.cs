using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// A named squad of three: the player plus two friends, two AI, or a mix. Saving one is saving a
    /// deck; before a game the player picks which to play (Docs/PILOTS_AND_ARTIFACTS.md §1).
    /// Held in a <see cref="SquadBook"/>.
    /// </summary>
    [Serializable]
    public class SavedSquad
    {
        public const int SlotCount = 3;

        public string Name;
        public SquadSlot Leader;
        public SquadSlot RogueOne;
        public SquadSlot RogueTwo;

        public SavedSquad() { }

        public SavedSquad(string name, SquadSlot leader, SquadSlot rogueOne, SquadSlot rogueTwo)
        {
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

        /// <summary>A copy under a new name, for "save as" and for starting a new squad from this one.</summary>
        public SavedSquad Clone(string name) => new SavedSquad(name, Leader, RogueOne, RogueTwo);
    }
}
