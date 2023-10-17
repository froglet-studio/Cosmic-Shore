public struct Squad
{
    //public ShipTypes SquadLeaderClass;
    //public Element SquadLeaderElement;
    public SO_Pilot SquadLeader;
    public SO_Pilot RogueOne;
    public SO_Pilot RogueTwo;

    public Squad(SO_Pilot leader, SO_Pilot rogueOne,  SO_Pilot rogueTwo)
    {
        SquadLeader = leader;
        RogueOne = rogueOne;
        RogueTwo = rogueTwo;
    }
}