using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Pilots
{
    /// <summary>
    /// Test helpers for the Hangar's PILOTS and ARTIFACTS tabs (Docs/Pilots/CALLSIGNS.md, Docs/Pilots/ARTIFACTS.md): empty every vessel's
    /// artifact slots or every pilot seat. Every pilot and artifact is free (Shombith, 2026-10-26), so nothing is granted. They write the device store (<see cref="PilotLoadoutStore"/>, PlayerPrefs) and no asset, so this is a
    /// READER-side tool: it records nothing in the tool ledger.
    /// </summary>
    public static class PilotDevTools
    {
        [MenuItem("FrogletTools/Pilots/Clear Every Artifact Slot (dev)", false, 301)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 1,
            Description = "Empties every vessel's artifact slots (every artifact is free, so there is nothing to grant).")]
        public static void ClearSlots()
        {
            foreach (VesselClassType v in System.Enum.GetValues(typeof(VesselClassType)))
                PlayerPrefs.DeleteKey($"artifacts.slots.{(int)v}");
            PlayerPrefs.Save();
            Debug.Log("[Pilots] Emptied every artifact slot.");
        }

        [MenuItem("FrogletTools/Pilots/Empty Every Pilot Seat (dev)", false, 302)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 1,
            Description = "Empties every vessel's pilot seat (an empty seat has no sight on LB).")]
        public static void EmptySeats()
        {
            foreach (VesselClassType v in System.Enum.GetValues(typeof(VesselClassType)))
                if (v != VesselClassType.Any && v != VesselClassType.Random) PilotLoadoutStore.SetPilot(v, string.Empty);
            Debug.Log("[Pilots] Emptied every pilot seat.");
        }
    }
}
