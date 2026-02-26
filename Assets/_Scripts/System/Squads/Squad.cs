using CosmicShore.ScriptableObjects;
using System;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    [Serializable]
    public struct Squad
    {
        public VesselClassType SquadLeaderClass;
        public Element SquadLeaderElement;
        public VesselClassType RogueOneClass;
        public Element RogueOneElement;
        public VesselClassType RogueTwoClass;
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