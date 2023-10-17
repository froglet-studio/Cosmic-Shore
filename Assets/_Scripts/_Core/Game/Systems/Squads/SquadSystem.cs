using System.Collections.Generic;
using System.Linq;

public static class SquadSystem
{
    public static List<SO_Pilot> PilotList;
    public static SO_Pilot DefaultLeader;
    public static SO_Pilot DefaultRogueOne;
    public static SO_Pilot DefaultRogueTwo;
    static Squad Squad;

    const string SquadSaveFileName = "squad.data";

    public static void Init()
    {
        var dataAccessor = new DataAccessor(SquadSaveFileName);
        Squad = dataAccessor.Load<Squad>();

        if (Squad.Equals(default(Squad)))
        {
            Squad = new Squad(DefaultLeader, DefaultRogueOne, DefaultRogueTwo);
            dataAccessor.Save(Squad);
        }
    }

    public static Squad GetSquad()
    {
        if (Squad.Equals(default(Squad)))
            Init();

        return Squad;
    }

    public static void SaveSquad()
    {
        var dataAccessor = new DataAccessor(SquadSaveFileName);
        dataAccessor.Save(Squad);
    }

    public static void SetSquadLeader(ShipTypes shipClass, Element element)
    {
        Squad.SquadLeader = PilotList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
    }

    public static void SetSquadLeader(SO_Pilot pilot)
    {
        Squad.SquadLeader = pilot;
    }

    public static void SetRogueOne(ShipTypes shipClass, Element element)
    {
        Squad.RogueOne = PilotList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
    }

    public static void SetRogueOne(SO_Pilot pilot)
    {
        Squad.RogueOne = pilot;
    }

    public static void SetRogueTwo(ShipTypes shipClass, Element element)
    {
        Squad.RogueTwo= PilotList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
    }

    public static void SetRogueTwo(SO_Pilot pilot)
    {
        Squad.RogueTwo= pilot;
    }
}