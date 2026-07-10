// PORT Deviation — type-preserving SHELL of the captain economy manager
// (original: Assets/_Scripts/System/Playfab/Economy/CaptainManager.cs, 202 lines:
// captain unlock/upgrade state over the legacy PlayFab catalog + CaptainManager
// UI feeds — meta-economy phase concerns). The type exists so AppManager's
// RegisterManagerSingleton<CaptainManager> binding compiles; the Store unit grew
// the captain-lookup surface StoreScreen + CatalogManager consume — upstream's
// captainData.AllCaptains dictionary (seeded from an SO_CaptainList in Start)
// backs GetCaptainByName, and EncounterCaptain marks the model (the upstream
// XpHandler.EncounterCaptain PlayFab persistence is a deviation — inert there
// too). The full port arrives with the meta-economy phase. Precedent: AudioSystem
// shell (Deviation #11).
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public class CaptainManager : SingletonPersistent<CaptainManager>
    {
        readonly Dictionary<string, Captain> AllCaptains = new();

        /// <summary>Seed the captain roster (upstream: Start builds this from the serialized SO_CaptainList).</summary>
        internal void LoadLocalCaptains(IEnumerable<Captain> captains)
        {
            AllCaptains.Clear();
            foreach (var captain in captains)
                AllCaptains[captain.Name] = captain;
        }

        public Captain GetCaptainByName(string name)
        {
            return AllCaptains.FirstOrDefault(x => x.Value.Name == name).Value;
        }

        public SO_Captain GetCaptainSOByName(string name)
        {
            // Upstream reads the serialized SO_CaptainList; the shell reads the
            // same SO through the seeded models.
            return GetCaptainByName(name)?.SO_Captain;
        }

        public Captain GetCaptainFromUpgrade(VirtualItem upgrade)
        {
            foreach (var captain in AllCaptains.Values)
            {
                if (upgrade.Tags.Contains(captain.Vessel.Class.ToString()) && upgrade.Tags.Contains(captain.PrimaryElement.ToString()))
                    return captain;
            }
            return null;
        }

        public void EncounterCaptain(string captainName)
        {
            // PORT Deviation (Store unit, PlayFab SDK): upstream routed through
            // XpHandler.EncounterCaptain (PlayFab persistence, inert there); the
            // local model flip is the surviving effect.
            var captain = GetCaptainByName(captainName);
            if (captain != null) captain.Encountered = true;
        }
    }
}
