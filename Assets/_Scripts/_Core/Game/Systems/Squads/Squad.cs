public struct Squad
{
    //public ShipTypes SquadLeaderClass;
    //public Element SquadLeaderElement;
    public SO_Vessel SquadLeader;
    public SO_Vessel RogueOne;
    public SO_Vessel RogueTwo;

    public Squad(SO_Vessel leader, SO_Vessel rogueOne,  SO_Vessel rogueTwo)
    {
        SquadLeader = leader;
        RogueOne = rogueOne;
        RogueTwo = rogueTwo;
    }
}