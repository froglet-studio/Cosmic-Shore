using System;
using System.Collections.Generic;
using CosmicShore.UI;
using CosmicShore.Core;
using CosmicShore.Utility;

namespace CosmicShore.Core
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
                if (service == null || !service.TrySpendCrystals(vessel.UnlockCost, "vessel_unlock"))
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

            // A reset returns the player to a FRESH ACCOUNT, not to a locked-out one: the
            // starter vessel is not an unlock, so it survives.
            var starters = new HashSet<string>();
            foreach (var vessel in vesselList.VesselList)
            {
                if (vessel == null) continue;

                if (vessel.OwnedFromStart)
                {
                    if (!string.IsNullOrWhiteSpace(vessel.Name))
                        starters.Add(vessel.Name);
                    continue;
                }

                vessel.Lock();
            }

            // Clear ownership only. Lifetime per-vessel stats live in the same record now
            // and are TELEMETRY, not entitlement - a debug unlock reset must not wipe them.
            var ds = UGSDataService.Instance;
            if (ds?.HangarRepo != null)
            {
                foreach (var name in new List<string>(ds.HangarRepo.Data.UnlockedVesselNames()))
                    if (!starters.Contains(name))
                        ds.HangarRepo.Data.LockVessel(name);

                ds.HangarRepo.Data.SelectedVessel = "";
                ds.HangarRepo.MarkDirty();

                // Re-seed starters and re-default SelectedVessel in one pass.
                ds.SyncHangarToVessels();
            }

            OnUnlockStateChanged?.Invoke();
        }

        static void PersistUnlockToCloud(string vesselName, bool unlocked)
        {
            var ds = UGSDataService.Instance;
            if (ds?.HangarRepo == null) return;

            if (string.IsNullOrWhiteSpace(vesselName))
            {
                // SO_Vessel.Name is authored data and at least one asset ships blank. Persisting
                // it is what put an empty string in the old flat UnlockedVessels list.
                CSDebug.LogWarning("[VesselUnlockSystem] Refusing to persist unlock state for a vessel with a blank Name. Fix the SO_Vessel asset.");
                return;
            }

            if (unlocked)
                ds.HangarRepo.Data.UnlockVessel(vesselName, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            else
                ds.HangarRepo.Data.LockVessel(vesselName);

            ds.HangarRepo.MarkDirty();
        }
    }
}
