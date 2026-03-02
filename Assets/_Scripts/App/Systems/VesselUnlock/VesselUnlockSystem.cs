using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.App.Systems.VesselUnlock
{
    /// <summary>
    /// Manages vessel unlock state. Persists to PlayerPrefs.
    /// Vessels marked UnlockedByDefault in their SO_Ship are unlocked on first run.
    /// </summary>
    public static class VesselUnlockSystem
    {
        const string UnlockKeyPrefix = "VesselUnlocked_";
        const string InitializedKey = "VesselUnlockSystem_Initialized";
        const string CurrencyKey = "VesselCurrency";

        static readonly HashSet<VesselClassType> _unlockedCache = new();
        static bool _initialized;

        public static event Action OnUnlockStateChanged;

        /// <summary>
        /// Initializes the system using the given ship list to seed default unlocks.
        /// Safe to call multiple times — only runs once per app session.
        /// </summary>
        public static void Initialize(SO_ShipList shipList = null)
        {
            if (_initialized) return;

            _unlockedCache.Clear();

            // On first-ever run, seed defaults from SO_Ship.UnlockedByDefault
            if (!PlayerPrefs.HasKey(InitializedKey) && shipList != null)
            {
                foreach (var ship in shipList.ShipList)
                {
                    if (ship == null) continue;
                    if (ship.UnlockedByDefault)
                        PlayerPrefs.SetInt(GetKey(ship.Class), 1);
                }
                PlayerPrefs.SetInt(InitializedKey, 1);
                PlayerPrefs.Save();
            }

            // Load all vessel unlock states from PlayerPrefs
            foreach (VesselClassType vesselClass in Enum.GetValues(typeof(VesselClassType)))
            {
                if (vesselClass == VesselClassType.Any || vesselClass == VesselClassType.Random)
                    continue;

                if (PlayerPrefs.GetInt(GetKey(vesselClass), 0) == 1)
                    _unlockedCache.Add(vesselClass);
            }

            _initialized = true;
        }

        /// <summary>
        /// Returns true if the given vessel class is unlocked.
        /// </summary>
        public static bool IsUnlocked(VesselClassType vesselClass)
        {
            Initialize();
            return _unlockedCache.Contains(vesselClass);
        }

        /// <summary>
        /// Returns true if the given SO_Ship is unlocked.
        /// </summary>
        public static bool IsUnlocked(SO_Ship ship)
        {
            if (ship == null) return false;
            return IsUnlocked(ship.Class);
        }

        /// <summary>
        /// Unlocks a vessel class. Returns true if the vessel was newly unlocked.
        /// </summary>
        public static bool UnlockVessel(VesselClassType vesselClass)
        {
            Initialize();

            if (_unlockedCache.Contains(vesselClass))
                return false;

            _unlockedCache.Add(vesselClass);
            PlayerPrefs.SetInt(GetKey(vesselClass), 1);
            PlayerPrefs.Save();

            CSDebug.Log($"VesselUnlockSystem: Unlocked {vesselClass}");
            OnUnlockStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Gets the current currency balance.
        /// </summary>
        public static int GetCurrencyBalance()
        {
            return PlayerPrefs.GetInt(CurrencyKey, 0);
        }

        /// <summary>
        /// Adds currency. Returns new balance.
        /// </summary>
        public static int AddCurrency(int amount)
        {
            var balance = GetCurrencyBalance() + amount;
            PlayerPrefs.SetInt(CurrencyKey, balance);
            PlayerPrefs.Save();
            return balance;
        }

        /// <summary>
        /// Attempts to spend currency. Returns true if successful.
        /// </summary>
        public static bool TrySpendCurrency(int amount)
        {
            var balance = GetCurrencyBalance();
            if (balance < amount) return false;

            PlayerPrefs.SetInt(CurrencyKey, balance - amount);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Attempts to purchase and unlock a vessel using its configured cost.
        /// </summary>
        public static bool TryPurchaseVessel(SO_Ship ship)
        {
            if (ship == null) return false;
            return TryPurchaseVessel(ship.Class, ship.UnlockCost);
        }

        /// <summary>
        /// Attempts to purchase and unlock a vessel. Returns true if successful.
        /// </summary>
        public static bool TryPurchaseVessel(VesselClassType vesselClass, int cost)
        {
            Initialize();

            if (_unlockedCache.Contains(vesselClass))
                return false;

            if (cost > 0 && !TrySpendCurrency(cost))
                return false;

            return UnlockVessel(vesselClass);
        }

        static string GetKey(VesselClassType vesselClass)
        {
            return $"{UnlockKeyPrefix}{(int)vesselClass}";
        }

        /// <summary>
        /// Resets unlock state and re-seeds from the given ship list. Only for testing/debug.
        /// </summary>
        public static void ResetAllUnlocks(SO_ShipList shipList = null)
        {
            PlayerPrefs.DeleteKey(InitializedKey);
            foreach (VesselClassType vesselClass in Enum.GetValues(typeof(VesselClassType)))
            {
                if (vesselClass == VesselClassType.Any || vesselClass == VesselClassType.Random)
                    continue;
                PlayerPrefs.DeleteKey(GetKey(vesselClass));
            }

            _unlockedCache.Clear();
            _initialized = false;
            PlayerPrefs.Save();

            Initialize(shipList);
            OnUnlockStateChanged?.Invoke();
        }
    }
}
