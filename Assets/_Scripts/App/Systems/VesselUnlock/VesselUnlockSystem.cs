using System;
using CosmicShore.App.Profile;
using CosmicShore.App.Systems.CloudData;
using CosmicShore.Utility;

namespace CosmicShore.App.Systems.VesselUnlock
{
    /// <summary>
    /// Manages vessel lock/unlock state with cloud persistence via HangarRepository.
    /// Unlock state is applied to SO_Vessel assets at runtime and persisted to UGS Cloud Save
    /// so unlocks survive app restarts and roam across devices.
    /// Crystal currency is managed via PlayerDataService.
    /// </summary>
    public static class VesselUnlockSystem
    {
        public static event Action OnUnlockStateChanged;

        public static bool UnlockVessel(SO_Vessel vessel)
        {
            if (vessel == null || !vessel.IsLocked)
                return false;

            vessel.Unlock();
            PersistUnlockToCloud(vessel.Name, unlocked: true);
            CSDebug.Log($"VesselUnlockSystem: Unlocked {vessel.Name}");
            OnUnlockStateChanged?.Invoke();
            return true;
        }

        public static bool LockVessel(SO_Vessel vessel)
        {
            if (vessel == null || vessel.IsLocked)
                return false;

            vessel.Lock();
            PersistUnlockToCloud(vessel.Name, unlocked: false);
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

            // Clear cloud data
            var ds = UGSDataService.Instance;
            if (ds?.HangarRepo != null)
            {
                ds.HangarRepo.Data.UnlockedVessels.Clear();
                ds.HangarRepo.Data.VesselPreferences.Clear();
                ds.HangarRepo.MarkDirty();
            }

            OnUnlockStateChanged?.Invoke();
        }

        static void PersistUnlockToCloud(string vesselName, bool unlocked)
        {
            var ds = UGSDataService.Instance;
            if (ds?.HangarRepo == null) return;

            if (unlocked)
                ds.HangarRepo.Data.UnlockVessel(vesselName);
            else
                ds.HangarRepo.Data.LockVessel(vesselName);

            ds.HangarRepo.MarkDirty();
        }
    }
}
