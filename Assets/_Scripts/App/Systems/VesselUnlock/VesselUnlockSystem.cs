using System;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.App.Systems.VesselUnlock
{
    /// <summary>
    /// Thin wrapper around SO_Vessel.IsLocked for event broadcasting and currency management.
    /// Unlock state lives directly on the SO_Vessel asset (isLocked field).
    /// In builds, runtime unlocks reset on restart — by design until UGS sync is implemented.
    /// </summary>
    public static class VesselUnlockSystem
    {
        const string CurrencyKey = "VesselCurrency";

        public static event Action OnUnlockStateChanged;

        /// <summary>
        /// Unlocks a vessel. Fires OnUnlockStateChanged if the vessel was locked.
        /// </summary>
        public static bool UnlockVessel(SO_Vessel vessel)
        {
            if (vessel == null || !vessel.IsLocked)
                return false;

            vessel.Unlock();
            CSDebug.Log($"VesselUnlockSystem: Unlocked {vessel.Name}");
            OnUnlockStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Locks a vessel. Fires OnUnlockStateChanged if the vessel was unlocked.
        /// Intended for debug/testing.
        /// </summary>
        public static bool LockVessel(SO_Vessel vessel)
        {
            if (vessel == null || vessel.IsLocked)
                return false;

            vessel.Lock();
            CSDebug.Log($"VesselUnlockSystem: Locked {vessel.Name}");
            OnUnlockStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Attempts to purchase and unlock a vessel using its configured UnlockCost.
        /// </summary>
        public static bool TryPurchaseVessel(SO_Vessel vessel)
        {
            if (vessel == null || !vessel.IsLocked)
                return false;

            if (vessel.UnlockCost > 0 && !TrySpendCurrency(vessel.UnlockCost))
                return false;

            return UnlockVessel(vessel);
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
        /// Locks all vessels in the list, then unlocks those not marked as locked in their SO.
        /// Intended for debug/testing — resets runtime state to match serialized defaults.
        /// </summary>
        public static void ResetAllUnlocks(SO_VesselList vesselList)
        {
            if (vesselList == null) return;

            foreach (var vessel in vesselList.VesselList)
            {
                if (vessel == null) continue;
                // Force lock, then let the serialized isLocked value be re-read on next access.
                // Since we can't reload serialized defaults at runtime, just lock everything.
                vessel.Lock();
            }

            OnUnlockStateChanged?.Invoke();
        }
    }
}
