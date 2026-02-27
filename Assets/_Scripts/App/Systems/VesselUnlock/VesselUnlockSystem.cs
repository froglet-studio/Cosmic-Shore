using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.App.Systems.VesselUnlock
{
    /// <summary>
    /// Manages vessel unlock state. Persists to PlayerPrefs.
    /// Only Squirrel is unlocked by default.
    /// </summary>
    public static class VesselUnlockSystem
    {
        const string UnlockKeyPrefix = "VesselUnlocked_";
        const string CurrencyKey = "VesselCurrency";

        static readonly HashSet<VesselClassType> _unlockedCache = new();
        static bool _initialized;

        public static event Action OnUnlockStateChanged;

        /// <summary>
        /// Ensures the system is initialized. Safe to call multiple times.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _unlockedCache.Clear();

            // Always unlock Squirrel by default
            if (!PlayerPrefs.HasKey(GetKey(VesselClassType.Squirrel)))
                PlayerPrefs.SetInt(GetKey(VesselClassType.Squirrel), 1);

            // Load all vessel unlock states from PlayerPrefs
            foreach (VesselClassType vesselClass in Enum.GetValues(typeof(VesselClassType)))
            {
                if (vesselClass == VesselClassType.Any || vesselClass == VesselClassType.Random)
                    continue;

                if (PlayerPrefs.GetInt(GetKey(vesselClass), 0) == 1)
                    _unlockedCache.Add(vesselClass);
            }

            PlayerPrefs.Save();
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
        /// Attempts to purchase and unlock a vessel. Returns true if successful.
        /// </summary>
        public static bool TryPurchaseVessel(VesselClassType vesselClass, int cost)
        {
            Initialize();

            if (_unlockedCache.Contains(vesselClass))
                return false;

            if (!TrySpendCurrency(cost))
                return false;

            return UnlockVessel(vesselClass);
        }

        static string GetKey(VesselClassType vesselClass)
        {
            return $"{UnlockKeyPrefix}{(int)vesselClass}";
        }

        /// <summary>
        /// Resets unlock state. Only for testing/debug.
        /// </summary>
        public static void ResetAllUnlocks()
        {
            foreach (VesselClassType vesselClass in Enum.GetValues(typeof(VesselClassType)))
            {
                if (vesselClass == VesselClassType.Any || vesselClass == VesselClassType.Random)
                    continue;
                PlayerPrefs.DeleteKey(GetKey(vesselClass));
            }

            _unlockedCache.Clear();
            _initialized = false;
            PlayerPrefs.Save();

            Initialize();
            OnUnlockStateChanged?.Invoke();
        }
    }
}
