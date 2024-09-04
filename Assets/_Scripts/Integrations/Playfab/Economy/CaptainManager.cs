using CosmicShore.App.Systems.Xp;
using CosmicShore.Models;
using CosmicShore.Utility.Singleton;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// TODO: Renamespace - not using playfab directly here
namespace CosmicShore.Integrations.PlayFab.Economy
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
                    Debug.LogError($"UpgradeXPRequirements.GetRequirementByLevel - level out of range: {nextLevel}");
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
        public HashSet<SO_Ship> UnlockedShips = new();

        void OnEnable()
        {
            XpHandler.OnCaptainDataLoaded += LoadCaptainsData;

            CatalogManager.OnLoadInventory += LoadCaptainsData;
            CatalogManager.OnInventoryChange += LoadCaptainsData;
        }

        void OnDisable()
        {
            XpHandler.OnCaptainDataLoaded -= LoadCaptainsData;

            CatalogManager.OnLoadInventory += LoadCaptainsData;
            CatalogManager.OnInventoryChange -= LoadCaptainsData;
        }

        public void LoadCaptainsData()
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
                XpHandler.EncounteredCaptainsData.ContainsKey(captain.Ship.Class) &&
                XpHandler.EncounteredCaptainsData[captain.Ship.Class].Contains(captain.PrimaryElement);

            if (captain.Encountered)
                captainData.EncounteredCaptains[captain.Name] = captain;

            // check for unlocked
            captain.Unlocked = CatalogManager.Inventory.ContainsCaptain(captain.Name);
            if (captain.Unlocked)
            {
                UnlockedShips.Add(captain.Ship);
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

            Debug.Log($"LoadCaptainData - {captain.Name}, Level:{captain.Level}, XP:{captain.XP}, Unlocked:{captain.Unlocked}, Encountered:{captain.Encountered}");
        }

        public void IssueXP(string captainName, int amount)
        {
            Debug.Log($"CaptainManager.IssueXP {captainName}, {amount}");
            IssueXP(GetCaptainByName(captainName), amount);
        }

        public void IssueXP(Captain captain, int amount)
        {
            captain.XP += amount;
            captainData.AllCaptains[captain.SO_Captain.Name].XP += amount;

            // Save to Playfab
            Debug.Log($"CaptainManager.IssueXP {captain.Name}, {amount}");
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
                if (upgrade.Tags.Contains(captain.Ship.Class.ToString()) && upgrade.Tags.Contains(captain.PrimaryElement.ToString()))
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
            return CatalogManager.Inventory.captainUpgrades.Where(x => x.Tags.Contains(captain.Ship.Class.ToString()) && x.Tags.Contains(captain.PrimaryElement.ToString())).Count();
        }
    }
}