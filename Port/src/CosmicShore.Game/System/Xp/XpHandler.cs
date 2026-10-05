// Ported verbatim from Assets/_Scripts/System/Xp/XpHandler.cs (CaptainManager unit
// 2026-07-10), replacing the struct-only extraction (XpHandler.Structs.cs, absorbed —
// upstream declares XpData in this file). Mechanical substitutions: UnityEngine →
// CosmicShore.Engine. The LOCAL lanes are live: the ClassXpData /
// EncounteredCaptainsData dictionaries, IssueXP's per-element accumulation,
// EncounterCaptain's dedupe + add, GetCaptainXP's reads, and the
// OnCaptainDataLoaded fan-out. The PlayFab persistence + pull-processing lanes
// (PlayerDataController.GetPlayerData / UpdatePlayerData sends over
// Newtonsoft-serialized payloads; OnLoadCaptainXpData + the GetUserDataResult
// converters) are carried as commented source — upstream they are inert too
// ("[PLAYFAB DISABLED]" posture: the send callbacks never fire).
using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
// PORT Deviation (PlayFab arc — restore with the persistence lanes below): using Newtonsoft.Json;
// PORT Deviation (PlayFab arc — restore with the persistence lanes below): using PlayFab.ClientModels;
using CosmicShore.Engine;
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
        /// Captain Xp key for querying data from PlayFab data storage, not used for PlayFab API calls now.
        /// </summary>
        private const string ClassXpKey = "ClassXP";

        /// <summary>
        /// Encountered Captains key for querying data from PlayFab data storage, not used for PlayFab API calls now.
        /// </summary>
        private const string EncounteredCaptainsKey = "EncounteredCaptains";

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

        /// <summary>
        /// A wrapper to get player data for now.
        /// </summary>
        public static void LoadCaptainXpData()
        {
            if (ClassXpData == null)
            {
                // For now we don't pass any keys, pull all player data and query locally.
                // PORT Deviation (PlayFab arc): PlayerDataController.Instance.GetPlayerData();
                // — the PlayFab pull that would answer through OnLoadCaptainXpData below.
                // Inert upstream ("[PLAYFAB DISABLED]"); rigs seed the dictionaries directly.
            }
        }

        public static void IssueXP(Captain captain, int amount)
        {
            CSDebug.Log($"XPHandler.IssueXP {captain.Name}, {amount}");

            if (!ClassXpData.ContainsKey(captain.Vessel.Class))
                ClassXpData.Add(captain.Vessel.Class, new XpData (0, 0, 0, 0));

            var xpData = ClassXpData[captain.Vessel.Class];
            xpData.Space += captain.PrimaryElement == Element.Space ? amount : 0;
            xpData.Time += captain.PrimaryElement == Element.Time ? amount : 0;
            xpData.Mass += captain.PrimaryElement == Element.Mass ? amount : 0;
            xpData.Charge += captain.PrimaryElement == Element.Charge ? amount : 0;
            ClassXpData[captain.Vessel.Class] = xpData;

            // TODO: Security - Move to cloud script and store in internal data
            // PORT Deviation (PlayFab arc — the persistence send; its OnCaptainDataLoaded
            // callback never fires upstream with PlayFab inert):
            // var dataContent = new Dictionary<string, string>
            // {
            //     { ClassXpKey, JsonConvert.SerializeObject(ClassXpData) }
            // };
            //
            // PlayerDataController.Instance.UpdatePlayerData(dataContent, OnCaptainDataLoaded);
            //
            // CSDebug.Log($"IssueXP Success - {JsonConvert.SerializeObject(ClassXpData)}");
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

            // TODO: Security && Portability - Move to cloud script and store in internal data
            // PORT Deviation (PlayFab arc — the persistence send; see IssueXP):
            // var dataContent = new Dictionary<string, string>
            // {
            //     { EncounteredCaptainsKey, JsonConvert.SerializeObject(EncounteredCaptainsData) }
            // };
            //
            // PlayerDataController.Instance.UpdatePlayerData(dataContent, OnCaptainDataLoaded);
            //
            // CSDebug.Log($"Encounter Captain Success - {JsonConvert.SerializeObject(EncounteredCaptainsData)}");
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

        // PORT Deviation (PlayFab arc — the pull-processing seam: GetUserDataResult +
        // the Newtonsoft converters arrive with the PlayFab surface; carried as source):
        //
        // /// <summary>
        // /// Process user data result upon pulling player data, convert the result to Class Xp Data, and log them in the console.
        // /// </summary>
        // /// <param name="result">Query result for player data</param>
        // public static void OnLoadCaptainXpData(GetUserDataResult result)
        // {
        //     ClassXpData = ConvertResultToCaptainXpData(result);
        //     EncounteredCaptainsData = ConvertResultToEncounteredCaptainData(result);
        //
        //     foreach (var key in ClassXpData.Keys)
        //         CSDebug.Log($"OnLoadCaptainXpData - ClassXpData.ShipClassXpData.Keys: {key}");
        //
        //     CSDebug.Log($"OnLoadCaptainXpData - Custom Data: {result.CustomData}");
        //
        //     OnCaptainDataLoaded?.Invoke();
        // }
        //
        // static Dictionary<VesselClassType, XpData> ConvertResultToCaptainXpData(GetUserDataResult result) { ... }
        // static Dictionary<VesselClassType, List<Element>> ConvertResultToEncounteredCaptainData(GetUserDataResult result) { ... }
    }
}
