using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
namespace CosmicShore.Core
{
    /// <summary>
    /// Captain Xp Data
    /// Contains Captain class elements - Space, Time, Charge, Mass
    /// </summary>
    [System.Serializable]
    public struct XpData
    {
        public int Space;
        public int Time;
        public int Charge;
        public int Mass;

        public XpData(int space, int time, int mass, int charge)
        {
            Space = space;
            Time = time;
            Mass = mass;
            Charge = charge;
        }
    }

    public class XpHandler
    {

        /// <summary>
        /// Delegate invoked when captain data (xp, encountered) Loaded
        /// </summary>
        public static Action OnCaptainDataLoaded;

        /// <summary>
        /// Class Xp Data
        /// Used for storing Captain Xp Data for each Vessel type.
        /// </summary>
        public static Dictionary<VesselClassType, XpData> ClassXpData;

        /// <summary>
        /// Encountered Captain Data
        /// Used for storing Encountered Captains for each Vessel type.
        /// </summary>
        public static Dictionary<VesselClassType, List<Element>> EncounteredCaptainsData;

        // The ClassXpData == null guard is the only refetch trigger — a stale dict means
        // session 2 never reloads XP. The bare delegate is publicly assignable and never cleared.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ClassXpData = null;
            EncounteredCaptainsData = null;
            OnCaptainDataLoaded = null;
        }

        /// <summary>
        /// Was a wrapper around PlayFab's player-data fetch. Captain progression is owned by UGS
        /// CloudSave now (<c>CaptainProgressCloudData</c>, whose own docstring says it "replaces
        /// the disabled PlayFab CaptainManager + XpHandler system"), so there is nothing to pull
        /// here and the in-memory tables below are the whole of this type's state.
        /// </summary>
        public static void LoadCaptainXpData()
        {
            ClassXpData ??= new();
            EncounteredCaptainsData ??= new();
            OnCaptainDataLoaded?.Invoke();
        }

        public static void IssueXP(Captain captain, int amount)
        {
            CSDebug.LogVerbose(CSLogChannel.CloudData, $"[XpHandler] IssueXP - captain={captain.Name}, {amount}");

            if (!ClassXpData.ContainsKey(captain.Vessel.Class))
                ClassXpData.Add(captain.Vessel.Class, new XpData (0, 0, 0, 0));

            var xpData = ClassXpData[captain.Vessel.Class];
            xpData.Space += captain.PrimaryElement == Element.Space ? amount : 0;
            xpData.Time += captain.PrimaryElement == Element.Time ? amount : 0;
            xpData.Mass += captain.PrimaryElement == Element.Mass ? amount : 0;
            xpData.Charge += captain.PrimaryElement == Element.Charge ? amount : 0;
            ClassXpData[captain.Vessel.Class] = xpData;

            OnCaptainDataLoaded?.Invoke();
        }

        public static void EncounterCaptain(Captain captain)
        {
            if (EncounteredCaptainsData.ContainsKey(captain.Vessel.Class))
            {
                if (EncounteredCaptainsData[captain.Vessel.Class].Contains(captain.PrimaryElement)){ return; }

                EncounteredCaptainsData[captain.Vessel.Class].Add(captain.PrimaryElement);
            }
            else
            {
                EncounteredCaptainsData[captain.Vessel.Class] = new() { captain.PrimaryElement };
            }

            OnCaptainDataLoaded?.Invoke();
        }


        public static int GetCaptainXP(Captain captain)
        {
            if (!ClassXpData.ContainsKey(captain.Vessel.Class))
                return 0;

            switch (captain.PrimaryElement) {
                case Element.Space: return ClassXpData[captain.Vessel.Class].Space;
                case Element.Time: return ClassXpData[captain.Vessel.Class].Time;
                case Element.Mass: return ClassXpData[captain.Vessel.Class].Mass;
                case Element.Charge: return ClassXpData[captain.Vessel.Class].Charge;
            }

            return 0;
        }
        
        // Three PlayFab parsers lived here — OnLoadCaptainXpData(GetUserDataResult) and the two
        // ConvertResultTo* helpers that turned a PlayFab user-data blob into the tables above.
        // Their only caller was PlayerDataController, whose prefab is in no scene, so they were
        // unreachable. Removed with PlayFab; persistence belongs to UGS CloudSave.
    }
}
