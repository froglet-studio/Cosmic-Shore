using CosmicShore.ScriptableObjects;
using CosmicShore.Core;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Core
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
            // CaptainList must be set externally (e.g. by PortSquadView) before use.
            Squad = DataAccessor.Load<Squad>(SquadSaveFileName);

            if (!Squad.Equals(default(Squad)))
                return;

            // No saved squad: seed one from the default captains - but only once something has
            // supplied all three. Captains were removed from vessels, so nothing assigns them today
            // (PortSquadView's roster is empty) and a fresh profile has NO squad: it stays default,
            // nothing is written, and the getters below resolve to null - the empty slot every
            // consumer already draws (SquadMemberCard, FactionMissionModal). Building one from null
            // captains threw out of Squad's constructor on every fresh-profile entry to Menu_Main.
            if (DefaultLeader == null || DefaultRogueOne == null || DefaultRogueTwo == null)
                return;

            Squad = new Squad(DefaultLeader, DefaultRogueOne, DefaultRogueTwo);
            DataAccessor.Save(SquadSaveFileName, Squad);
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

                // CaptainList is set externally (e.g. by PortSquadView) — a view whose
                // Start() runs before that assignment must get null (the same contract as
                // FirstOrDefault finding no match), not an ArgumentNullException.
                if (CaptainList == null) return null;

                return CaptainList.Where(x => x.PrimaryElement == Squad.SquadLeaderElement && x.Vessel.Class == Squad.SquadLeaderClass).FirstOrDefault();
            }
        }

        public static SO_Captain RogueOne
        {
            get
            {
                if (Squad.Equals(default(Squad)))
                    Init();

                if (CaptainList == null) return null;

                return CaptainList.Where(x => x.PrimaryElement == Squad.RogueOneElement && x.Vessel.Class == Squad.RogueOneClass).FirstOrDefault(); 
            }
        }

        public static SO_Captain RogueTwo
        {
            get
            {
                if (Squad.Equals(default(Squad)))
                    Init();

                if (CaptainList == null) return null;

                return CaptainList.Where(x => x.PrimaryElement == Squad.RogueTwoElement && x.Vessel.Class == Squad.RogueTwoClass).FirstOrDefault(); 
            }
        }

        public static void SetSquadLeader(VesselClassType vesselClass, Element element)
        {
            Squad.SquadLeaderElement = element;
            Squad.SquadLeaderClass = vesselClass;
        }

        public static void SetSquadLeader(SO_Captain captain)
        {
            Squad.SquadLeaderElement = captain.PrimaryElement;
            Squad.SquadLeaderClass = captain.Vessel.Class;
        }

        public static void SetRogueOne(VesselClassType vesselClass, Element element)
        {
            Squad.RogueOneElement = element;
            Squad.RogueOneClass = vesselClass;
        }

        public static void SetRogueOne(SO_Captain captain)
        {
            Squad.RogueOneElement = captain.PrimaryElement;
            Squad.RogueOneClass = captain.Vessel.Class;
        }

        public static void SetRogueTwo(VesselClassType vesselClass, Element element)
        {
            Squad.RogueTwoElement = element;
            Squad.RogueTwoClass = vesselClass;
        }

        public static void SetRogueTwo(SO_Captain captain)
        {
            Squad.RogueTwoElement = captain.PrimaryElement;
            Squad.RogueTwoClass = captain.Vessel.Class;
        }
    }
}