// Ported verbatim from Assets/_Scripts/System/Economy/CaptainManager.cs
// (CaptainManager unit 2026-07-10; path follows upstream's move into
// System/Economy/). Mechanical substitutions only: UnityEngine → CosmicShore.Engine.
// Upstream posture kept faithfully: OnEnable wires nothing (captain management is
// disabled upstream, pending its rebuild on UGS), so LoadCaptainsData runs only when
// a caller drives it. All the data lanes are LIVE: LoadCaptainsData building the
// roster from the serialized SO_CaptainList, per-captain XP/Encountered/Unlocked/Level
// derivation over the live XpHandler dictionaries + CatalogManager.Inventory, IssueXP
// accumulation, the query surface, and the UpgradeXPRequirements ladder. Upstream's
// CSDebug.LogVerbose(CSLogChannel.CloudData) traces in IssueXP are omitted (the port's
// CSDebug has no log channels; the channel is off by default upstream). One port-only
// rig seam is retained from the shell era (documented at the member): the menushell +
// tests author their captain roster through it.
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Engine;

namespace CosmicShore.Core
{
    [System.Serializable]
    class CaptainData
    {
        /// <summary>
        /// Dictionary mapping captain name to captain for all encountered, but not yet unlocked captains
        /// </summary>
        public Dictionary<string, Captain> EncounteredCaptains = new Dictionary<string, Captain>();
        /// <summary>
        /// Dictionary mapping captain name to captain for all unlocked captains
        /// </summary>
        public Dictionary<string, Captain> UnlockedCaptains = new Dictionary<string, Captain>();
        /// <summary>
        /// Dictionary mapping captain name to captain for all unlocked captains
        /// </summary>
        public Dictionary<string, Captain> AllCaptains = new Dictionary<string, Captain>();
    }

    static class UpgradeXPRequirements
    {
        public const int LevelTwo = 100;
        public const int LevelThree = 200;
        public const int LevelFour = 300;
        public const int LevelFive = 400;

        public static int GetRequirementByLevel(int nextLevel)
        {
            switch (nextLevel)
            {
                case 1: return LevelTwo;    // Not yet unlocked captain
                case 2: return LevelTwo;
                case 3: return LevelThree;
                case 4: return LevelFour;
                case 5: return LevelFive;
                default:
                    CSDebug.LogError($"UpgradeXPRequirements.GetRequirementByLevel - level out of range: {nextLevel}");
                    return LevelTwo;
            }
        }
    }

    public  class CaptainManager : SingletonPersistent<CaptainManager>
    {
        public static event Action OnLoadCaptainData;
        public static bool CaptainDataLoaded { get; private set; }
        [SerializeField] SO_CaptainList AllCaptains;
        CaptainData captainData;

        // TODO: Move to Hangar
        public HashSet<SO_Vessel> UnlockedShips = new();

        void OnEnable()
        {
            // Disabled upstream: captain management will be rebuilt on UGS. Pending removal.
        }

        void OnDisable()
        {
            // Teardown only. These are no-ops while OnEnable is disabled, but they must stay
            // detaches: OnLoadInventory used to be a '+=' here, which subscribed this handler
            // to a STATIC event on disable and leaked it for the rest of the process.
            XpHandler.OnCaptainDataLoaded -= LoadCaptainsData;

            CatalogManager.OnLoadInventory -= LoadCaptainsData;
            CatalogManager.OnInventoryChange -= LoadCaptainsData;
        }

        void LoadCaptainsData()
        {
            captainData = new CaptainData();
            foreach (var so_Captain in AllCaptains.CaptainList)
            {
                var captain = new Captain(so_Captain);
                LoadCaptainData(captain, false);
            }

            CaptainDataLoaded = true;
            OnLoadCaptainData?.Invoke();
        }

        public void LoadCaptainData(Captain captain, bool invokeCallback=true)
        {
            // Set XP
            captain.XP = XpHandler.GetCaptainXP(captain);

            // Check for Encountered
            captain.Encountered =
                XpHandler.EncounteredCaptainsData.ContainsKey(captain.Vessel.Class) &&
                XpHandler.EncounteredCaptainsData[captain.Vessel.Class].Contains(captain.PrimaryElement);

            if (captain.Encountered)
                captainData.EncounteredCaptains[captain.Name] = captain;

            // check for unlocked
            captain.Unlocked = CatalogManager.Inventory.ContainsCaptain(captain.Name);
            if (captain.Unlocked)
            {
                UnlockedShips.Add(captain.Vessel);
                captainData.UnlockedCaptains[captain.Name] = captain;
            }

            captainData.AllCaptains[captain.Name] = captain;

            // Set Level
            if (!captain.Encountered || !captain.Unlocked)
                captain.Level = 0;
            else
                captain.Level = 1 + GetCaptainUpgradeCount(captain);

            if (invokeCallback)
                OnLoadCaptainData?.Invoke();

        }

        public void IssueXP(string captainName, int amount)
        {
            IssueXP(GetCaptainByName(captainName), amount);
        }

        public void IssueXP(Captain captain, int amount)
        {
            captain.XP += amount;
            captainData.AllCaptains[captain.SO_Captain.Name].XP += amount;

            // Persisted by XpHandler below. XpHandler holds the tables in memory and nothing
            // writes them to a server today.
            XpHandler.IssueXP(captain, amount);
        }

        public bool IsCaptainEncountered(string  captainName)
        {
            return GetEncounteredCaptains().Where(x => x.Name == captainName).Any();
        }

        public Captain GetCaptainByName(string name)
        {
            return captainData.AllCaptains.FirstOrDefault(x => x.Value.Name == name).Value;
        }

        public SO_Captain GetCaptainSOByName(string name)
        {
            return AllCaptains.CaptainList.FirstOrDefault(x => x.Name == name);
        }

        public Captain GetCaptainFromUpgrade(VirtualItem upgrade)
        {
            foreach (var captain in captainData.AllCaptains.Values)
            {
                if (upgrade.Tags.Contains(captain.Vessel.Class.ToString()) && upgrade.Tags.Contains(captain.PrimaryElement.ToString()))
                    return captain;
            }
            return null;
        }

        public List<Captain> GetEncounteredCaptains()
        {
            return captainData.EncounteredCaptains.Values.ToList();
        }
        public List<Captain> GetUnlockedCaptains()
        {
            return captainData.UnlockedCaptains.Values.ToList();
        }
        public List<Captain> GetAllCaptains()
        {
            return captainData.AllCaptains.Values.ToList();
        }
        public List<SO_Captain> GetAllSOCaptains()
        {
            return AllCaptains.CaptainList;
        }

        public int GetCaptainUpgradeXPRequirement(Captain captain)
        {
            return UpgradeXPRequirements.GetRequirementByLevel(captain.Level+1);
        }

        public void EncounterCaptain(string captainName)
        {
            XpHandler.EncounterCaptain(GetCaptainByName(captainName));
        }

        int GetCaptainUpgradeCount(Captain captain)
        {
            return CatalogManager.Inventory.captainUpgrades.Where(x => x.Tags.Contains(captain.Vessel.Class.ToString()) && x.Tags.Contains(captain.PrimaryElement.ToString())).Count();
        }

        // ── Port-only rig seam (retained from the Store-unit shell; documented) ──

        /// <summary>
        /// Seeds the roster for harness worlds the way the inspector + a loaded
        /// save would: authors the serialized SO_CaptainList from the models'
        /// SO_Captains (so the verbatim GetCaptainSOByName / GetAllSOCaptains
        /// lanes are live), lands the XpHandler tables through upstream's own
        /// XpHandler.LoadCaptainXpData (seeded empty when absent, so the live
        /// EncounterCaptain / IssueXP lanes work), and adopts the caller-authored
        /// models VERBATIM into captainData (fixture Unlocked/Encountered states
        /// preserved — the menushell Store rig owns its captains' economy state).
        /// </summary>
        internal void LoadLocalCaptains(IEnumerable<Captain> captains)
        {
            XpHandler.LoadCaptainXpData();

            captainData = new CaptainData();
            var soList = ScriptableObject.CreateInstance<SO_CaptainList>();
            soList.CaptainList = new List<SO_Captain>();
            foreach (var captain in captains)
            {
                captainData.AllCaptains[captain.Name] = captain;
                if (captain.Encountered) captainData.EncounteredCaptains[captain.Name] = captain;
                if (captain.Unlocked) captainData.UnlockedCaptains[captain.Name] = captain;
                if (captain.SO_Captain != null) soList.CaptainList.Add(captain.SO_Captain);
            }
            AllCaptains = soList;
            CaptainDataLoaded = true;
        }
    }
}
