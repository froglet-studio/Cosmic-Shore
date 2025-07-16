using System;

namespace CosmicShore.App.Systems.Squads
{
    [Serializable]
    public struct Squad
    {
        public ShipClassType SquadLeaderClass;
        public Element SquadLeaderElement;
        public ShipClassType RogueOneClass;
        public Element RogueOneElement;
        public ShipClassType RogueTwoClass;
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