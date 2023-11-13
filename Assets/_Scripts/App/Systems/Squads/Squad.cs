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

        //public SO_Vessel SquadLeader;
        //public SO_Vessel RogueOne;
        //public SO_Vessel RogueTwo;

        public Squad(SO_Vessel leader, SO_Vessel rogueOne, SO_Vessel rogueTwo)
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