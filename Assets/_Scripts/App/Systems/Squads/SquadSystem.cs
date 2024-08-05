using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.App.Systems.Squads
{
    public static class SquadSystem
    {
        public static List<SO_Captain> CaptainList;
        public static SO_Captain DefaultLeader;
        public static SO_Captain DefaultRogueOne;
        public static SO_Captain DefaultRogueTwo;
        static Squad Squad;

        const string SquadSaveFileName = "squad.data";

        public static void Init()
        {
            Squad = DataAccessor.Load<Squad>(SquadSaveFileName);

            if (Squad.Equals(default(Squad)))
            {
                Squad = new Squad(DefaultLeader, DefaultRogueOne, DefaultRogueTwo);
                DataAccessor.Save(SquadSaveFileName, Squad);
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
            DataAccessor.Save(SquadSaveFileName, Squad);
        }

        public static SO_Captain SquadLeader
        {
            get 
            {
                if (Squad.Equals(default(Squad)))
                    Init();

                return CaptainList.Where(x => x.PrimaryElement == Squad.SquadLeaderElement && x.Ship.Class == Squad.SquadLeaderClass).FirstOrDefault(); 
            }
        }

        public static SO_Captain RogueOne
        {
            get
            {
                if (Squad.Equals(default(Squad)))
                    Init();

                return CaptainList.Where(x => x.PrimaryElement == Squad.RogueOneElement && x.Ship.Class == Squad.RogueOneClass).FirstOrDefault(); 
            }
        }

        public static SO_Captain RogueTwo
        {
            get
            {
                if (Squad.Equals(default(Squad)))
                    Init();

                return CaptainList.Where(x => x.PrimaryElement == Squad.RogueTwoElement && x.Ship.Class == Squad.RogueTwoClass).FirstOrDefault(); 
            }
        }

        public static void SetSquadLeader(ShipTypes shipClass, Element element)
        {
            Squad.SquadLeaderElement = element;
            Squad.SquadLeaderClass = shipClass;
        }

        public static void SetSquadLeader(SO_Captain captain)
        {
            Squad.SquadLeaderElement = captain.PrimaryElement;
            Squad.SquadLeaderClass = captain.Ship.Class;
        }

        public static void SetRogueOne(ShipTypes shipClass, Element element)
        {
            Squad.RogueOneElement = element;
            Squad.RogueOneClass = shipClass;
        }

        public static void SetRogueOne(SO_Captain captain)
        {
            Squad.RogueOneElement = captain.PrimaryElement;
            Squad.RogueOneClass = captain.Ship.Class;
        }

        public static void SetRogueTwo(ShipTypes shipClass, Element element)
        {
            Squad.RogueTwoElement = element;
            Squad.RogueTwoClass = shipClass;
        }

        public static void SetRogueTwo(SO_Captain captain)
        {
            Squad.RogueTwoElement = captain.PrimaryElement;
            Squad.RogueTwoClass = captain.Ship.Class;
        }
    }
}