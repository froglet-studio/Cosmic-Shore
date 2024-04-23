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

        //public SO_Guide SquadLeader;
        //public SO_Guide RogueOne;
        //public SO_Guide RogueTwo;

        public Squad(SO_Guide leader, SO_Guide rogueOne, SO_Guide rogueTwo)
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