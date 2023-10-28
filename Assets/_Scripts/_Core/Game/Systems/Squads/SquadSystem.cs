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

    public static Squad LoadSquad()
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

    public static SO_Vessel SquadLeader
    {
        get { return VesselList.Where(x => x.PrimaryElement == Squad.SquadLeaderElement && x.Ship.Class == Squad.SquadLeaderClass).FirstOrDefault(); }
    }

    public static SO_Vessel RogueOne
    {
        get { return VesselList.Where(x => x.PrimaryElement == Squad.RogueOneElement && x.Ship.Class == Squad.RogueOneClass).FirstOrDefault(); }
    }

    public static SO_Vessel RogueTwo
    {
        get { return VesselList.Where(x => x.PrimaryElement == Squad.RogueTwoElement && x.Ship.Class == Squad.RogueTwoClass).FirstOrDefault(); }
    }

    public static void SetSquadLeader(ShipTypes shipClass, Element element)
    {
        Squad.SquadLeaderElement = element;
        Squad.SquadLeaderClass = shipClass;
        //Squad.SquadLeader = VesselList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
    }

    public static void SetSquadLeader(SO_Vessel vessel)
    {
        Squad.SquadLeaderElement = vessel.PrimaryElement;
        Squad.SquadLeaderClass = vessel.Ship.Class;
    }

    public static void SetRogueOne(ShipTypes shipClass, Element element)
    {
        Squad.RogueOneElement = element;
        Squad.RogueOneClass = shipClass;
        //Squad.RogueOne = VesselList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
    }

    public static void SetRogueOne(SO_Vessel vessel)
    {
        Squad.RogueOneElement = vessel.PrimaryElement;
        Squad.RogueOneClass = vessel.Ship.Class;
    }

    public static void SetRogueTwo(ShipTypes shipClass, Element element)
    {
        Squad.RogueTwoElement = element;
        Squad.RogueTwoClass = shipClass;
        //Squad.RogueTwo= VesselList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
    }

    public static void SetRogueTwo(SO_Vessel vessel)
    {
        Squad.RogueTwoElement = vessel.PrimaryElement;
        Squad.RogueTwoClass = vessel.Ship.Class;
    }
}