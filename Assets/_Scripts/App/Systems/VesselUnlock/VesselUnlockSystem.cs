using System;
using UnityEngine;
using CosmicShore.App.Profile;
using CosmicShore.Utility;

namespace CosmicShore.App.Systems.VesselUnlock
{
    /// <summary>
    /// Thin wrapper around SO_Vessel.IsLocked for event broadcasting and currency management.
    /// Unlock state lives directly on the SO_Vessel asset (isLocked field).
    /// Crystal currency is persisted via PlayerDataService (UGS cloud save).
    /// </summary>
    public static class VesselUnlockSystem
    {
        public static event Action OnUnlockStateChanged;

        public static bool UnlockVessel(SO_Vessel vessel)
        {
            if (vessel == null || !vessel.IsLocked)
                return false;

            vessel.Unlock();
            CSDebug.Log($"VesselUnlockSystem: Unlocked {vessel.Name}");
            OnUnlockStateChanged?.Invoke();
            return true;
        }

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
        /// Attempts to purchase and unlock a vessel by spending crystals via PlayerDataService.
        /// </summary>
        public static bool TryPurchaseVessel(SO_Vessel vessel)
        {
            if (vessel == null || !vessel.IsLocked)
                return false;

            if (vessel.UnlockCost > 0)
            {
                var service = PlayerDataService.Instance;
                if (service == null || !service.TrySpendCrystals(vessel.UnlockCost))
                    return false;
            }

            return UnlockVessel(vessel);
        }

        /// <summary>
        /// Gets the current crystal balance from PlayerDataService.
        /// </summary>
        public static int GetCurrencyBalance()
        {
            var service = PlayerDataService.Instance;
            return service != null ? service.GetCrystalBalance() : 0;
        }

        public static void ResetAllUnlocks(SO_VesselList vesselList)
        {
            if (vesselList == null) return;

            foreach (var vessel in vesselList.VesselList)
            {
                if (vessel == null) continue;
                vessel.Lock();
            }

            OnUnlockStateChanged?.Invoke();
        }
    }
}
