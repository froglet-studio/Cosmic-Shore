using System;

namespace CosmicShore.App.Systems.Squads
{
    [Serializable]
    public struct Squad
    {
        public ShipTypes SquadLeaderClass;
        public Element SquadLeaderElement;
        public ShipTypes RogueOneClass;
        public Element RogueOneElement;
        public ShipTypes RogueTwoClass;
        public Element RogueTwoElement;

        public Squad(SO_Captain leader, SO_Captain rogueOne, SO_Captain rogueTwo)
        {
            SquadLeaderClass = leader.Ship.Class;
            SquadLeaderElement = leader.PrimaryElement;
            RogueOneClass = rogueOne.Ship.Class;
            RogueOneElement = rogueOne.PrimaryElement;
            RogueTwoClass = rogueTwo.Ship.Class;
            RogueTwoElement = rogueTwo.PrimaryElement;
        }
    }
}