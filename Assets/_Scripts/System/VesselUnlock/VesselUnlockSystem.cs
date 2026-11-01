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
            // IsLockedByEntitlement, never IsLocked: this GRANTS ownership, and IsLocked reads
            // false for everything while the master developer unlock is on (the default until
            // the FTUE is designed), which would make every grant a silent no-op.
            if (vessel == null || !vessel.IsLockedByEntitlement)
                return false;

            vessel.Unlock();
            PersistUnlockToCloud(vessel.Name, unlocked: true);
            CSDebug.LogVerbose(CSLogChannel.CloudData, $"[VesselUnlock] Unlocked {vessel.Name}");
            OnUnlockStateChanged?.Invoke();
            return true;
        }

        public static bool LockVessel(SO_Vessel vessel)
        {
            // Entitlement, not playability - see UnlockVessel.
            if (vessel == null || vessel.IsLockedByEntitlement)
                return false;

            vessel.Lock();
            PersistUnlockToCloud(vessel.Name, unlocked: false);
            CSDebug.LogVerbose(CSLogChannel.CloudData, $"[VesselUnlock] Locked {vessel.Name}");
            OnUnlockStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Attempts to purchase and unlock a vessel by spending crystals via PlayerDataService.
        /// </summary>
        public static bool TryPurchaseVessel(SO_Vessel vessel)
        {
            // Entitlement, not playability: a purchase must not be refused (nor the crystals
            // spent) on the strength of a developer gate - see UnlockVessel.
            if (vessel == null || !vessel.IsLockedByEntitlement)
                return false;

            if (vessel.UnlockCost > 0 || ServerEconomyGate.Enabled)
            {
                // The crystal wallet always persists, but the UNLOCK only persists while the
                // progression backend gate is open (PersistUnlockToCloud returns early, and
                // ApplyStarterUnlocks re-locks the hull on the next launch). Spending here with
                // the gate closed took the crystals for good and gave the vessel for one session.
                if (vessel.UnlockCost > 0 && !ProgressionBackendGate.CloudEnabled)
                {
                    CSDebug.LogWarning($"[VesselUnlockSystem] Purchase of {vessel.Name} refused: the progression backend gate is closed, so the unlock could not be saved but the crystals would be.");
                    return false;
                }

                // With the server wallet on, the server sells the vessel and records it (fair play T3); the local unlock
                // below is the display, and the server's answer confirms or undoes it (ApplyServerOwnership).
                var service = PlayerDataService.Instance;
                if (service == null || !service.TryBuyVessel(vessel.Name, vessel.UnlockCost))
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
            // Skipped entirely while the progression backend gate is closed (local-only mode).
            var ds = ProgressionBackendGate.CloudEnabled ? UGSDataService.Instance : null;
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

        /// <summary>
        /// The server's list of bought vessels is the truth (fair play T3; <c>economy.js</c>): every shipped vessel it
        /// lists, or that every account owns from the start, is unlocked; any other is locked again, which undoes an
        /// unlock written into a save by hand. Editor debug unlocks (the Froglet Toolbox) are undone too while the
        /// server wallet is on. A no-op until the vessel list is loaded.
        /// </summary>
        public static void ApplyServerOwnership(IReadOnlyCollection<string> boughtVesselNames)
        {
            var list = UGSDataService.Instance != null ? UGSDataService.Instance.VesselList : null;
            if (list == null || boughtVesselNames == null) return;
            var bought = new HashSet<string>(boughtVesselNames);
            foreach (var vessel in list.VesselList)
            {
                if (vessel == null || string.IsNullOrWhiteSpace(vessel.Name)) continue;
                bool owned = vessel.OwnedFromStart || bought.Contains(vessel.Name);
                if (owned && vessel.IsLockedByEntitlement) UnlockVessel(vessel);
                else if (!owned && !vessel.IsLockedByEntitlement) LockVessel(vessel);
            }
        }

        /// <summary>The vessels this client records as bought (not starters): what an account brings across once.</summary>
        public static List<string> LocallyBoughtVesselNames()
        {
            var names = new List<string>();
            var list = UGSDataService.Instance != null ? UGSDataService.Instance.VesselList : null;
            if (list == null) return names;
            foreach (var vessel in list.VesselList)
                if (vessel != null && !vessel.OwnedFromStart && !vessel.IsLockedByEntitlement && !string.IsNullOrWhiteSpace(vessel.Name))
                    names.Add(vessel.Name);
            return names;
        }

        static void PersistUnlockToCloud(string vesselName, bool unlocked)
        {
            if (!ProgressionBackendGate.CloudEnabled) return;

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
