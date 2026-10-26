using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Pilots
{
    /// <summary>
    /// Test helpers for the Hangar's PILOTS and ARTIFACTS tabs (Docs/Pilots/PILOTS_AND_ARTIFACTS.md). Finding an artifact is
    /// not built yet, so on a fresh profile every artifact is undiscovered; these give them all, take them back, or empty
    /// every pilot seat. They write the device store (<see cref="PilotLoadoutStore"/>, PlayerPrefs) and no asset, so this is a
    /// READER-side tool: it records nothing in the tool ledger.
    /// </summary>
    public static class PilotDevTools
    {
        const string Catalog = "Assets/_SO_Assets/Pilots/PilotArtifactCatalog.asset";

        static PilotArtifactCatalogSO Load()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PilotArtifactCatalogSO>(Catalog);
            if (!catalog) Debug.LogError($"[Pilots] No catalog at {Catalog}: run python3 Tools/Build/author_pilot_assets.py");
            return catalog;
        }

        [MenuItem("FrogletTools/Pilots/Grant Every Artifact (dev)", false, 300)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 2,
            Description = "Gives every artifact at a random rank, so the Hangar's ARTIFACTS tab can be tested.")]
        public static void GrantAll()
        {
            var catalog = Load();
            if (!catalog) return;
            var rng = new System.Random(1234);
            foreach (var a in catalog.Artifacts)
                if (a) PilotLoadoutStore.Grant(a.Id, rng.Next(1, ArtifactSO.RankCount + 1));
            Debug.Log($"[Pilots] Granted {catalog.Artifacts.Count} artifacts.");
        }

        [MenuItem("FrogletTools/Pilots/Forget Every Artifact (dev)", false, 301)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 1,
            Description = "Takes back every found artifact and empties every slot.")]
        public static void ForgetAll()
        {
            var catalog = Load();
            if (!catalog) return;
            foreach (var a in catalog.Artifacts)
                if (a) PilotLoadoutStore.Forget(a.Id);
            foreach (VesselClassType v in System.Enum.GetValues(typeof(VesselClassType)))
                PlayerPrefs.DeleteKey($"artifacts.slots.{(int)v}");
            PlayerPrefs.Save();
            Debug.Log("[Pilots] Forgot every artifact.");
        }

        [MenuItem("FrogletTools/Pilots/Empty Every Pilot Seat (dev)", false, 302)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 1,
            Description = "Empties every vessel's pilot seat (each flies its base kit).")]
        public static void EmptySeats()
        {
            foreach (VesselClassType v in System.Enum.GetValues(typeof(VesselClassType)))
                if (v != VesselClassType.Any && v != VesselClassType.Random) PilotLoadoutStore.SetPilot(v, string.Empty);
            Debug.Log("[Pilots] Emptied every pilot seat.");
        }
    }
}
