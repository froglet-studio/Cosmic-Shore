using System.Collections.Generic;
using System.Linq;

public static class SquadSystem
{
    public static List<SO_Vessel> VesselList;
    public static SO_Vessel DefaultLeader;
    public static SO_Vessel DefaultRogueOne;
    public static SO_Vessel DefaultRogueTwo;
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
        Squad.SquadLeader = VesselList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
    }

    public static void SetSquadLeader(SO_Vessel vessel)
    {
        Squad.SquadLeader = vessel;
    }

    public static void SetRogueOne(ShipTypes shipClass, Element element)
    {
        Squad.RogueOne = VesselList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
    }

    public static void SetRogueOne(SO_Vessel vessel)
    {
        Squad.RogueOne = vessel;
    }

    public static void SetRogueTwo(ShipTypes shipClass, Element element)
    {
        Squad.RogueTwo= VesselList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
    }

    public static void SetRogueTwo(SO_Vessel vessel)
    {
        Squad.RogueTwo= vessel;
    }
}