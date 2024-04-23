using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.App.Systems.Squads
{
    public static class SquadSystem
    {
        public static List<SO_Guide> GuideList;
        public static SO_Guide DefaultLeader;
        public static SO_Guide DefaultRogueOne;
        public static SO_Guide DefaultRogueTwo;
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

        public static SO_Guide SquadLeader
        {
            get { return GuideList.Where(x => x.PrimaryElement == Squad.SquadLeaderElement && x.Ship.Class == Squad.SquadLeaderClass).FirstOrDefault(); }
        }

        public static SO_Guide RogueOne
        {
            get { return GuideList.Where(x => x.PrimaryElement == Squad.RogueOneElement && x.Ship.Class == Squad.RogueOneClass).FirstOrDefault(); }
        }

        public static SO_Guide RogueTwo
        {
            get { return GuideList.Where(x => x.PrimaryElement == Squad.RogueTwoElement && x.Ship.Class == Squad.RogueTwoClass).FirstOrDefault(); }
        }

        public static void SetSquadLeader(ShipTypes shipClass, Element element)
        {
            Squad.SquadLeaderElement = element;
            Squad.SquadLeaderClass = shipClass;
            //Squad.SquadLeader = GuideList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
        }

        public static void SetSquadLeader(SO_Guide guide)
        {
            Squad.SquadLeaderElement = guide.PrimaryElement;
            Squad.SquadLeaderClass = guide.Ship.Class;
        }

        public static void SetRogueOne(ShipTypes shipClass, Element element)
        {
            Squad.RogueOneElement = element;
            Squad.RogueOneClass = shipClass;
            //Squad.RogueOne = GuideList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
        }

        public static void SetRogueOne(SO_Guide guide)
        {
            Squad.RogueOneElement = guide.PrimaryElement;
            Squad.RogueOneClass = guide.Ship.Class;
        }

        public static void SetRogueTwo(ShipTypes shipClass, Element element)
        {
            Squad.RogueTwoElement = element;
            Squad.RogueTwoClass = shipClass;
            //Squad.RogueTwo= GuideList.Where(x => x.PrimaryElement == element && x.Ship.Class == shipClass).FirstOrDefault();
        }

        public static void SetRogueTwo(SO_Guide guide)
        {
            Squad.RogueTwoElement = guide.PrimaryElement;
            Squad.RogueTwoClass = guide.Ship.Class;
        }
    }
}