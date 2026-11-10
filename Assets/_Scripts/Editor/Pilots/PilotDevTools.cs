using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Pilots
{
    /// <summary>
    /// Test helpers for the Hangar's PILOTS and ARTIFACTS tabs and the Ark's ARTIFACTS tab (Docs/Pilots/INVENTORY.md,
    /// Docs/Pilots/CALLSIGNS.md, Docs/Pilots/ARTIFACTS.md): empty every vessel's artifact slot or pilot seat, credit
    /// crystals, and empty what every held artifact was fed.
    ///
    /// <para><b>2026-11-07 (Garrett): every artifact is unique and accepts ONE element</b> without limit, with
    /// diminishing returns (<see cref="ArtifactRecord"/>, <see cref="ArtifactCurve"/>). There are no four track levels to
    /// zero any more, so the old "reset every track" became "reset every held artifact's feed": each artifact goes back
    /// to the level it was rolled at (<see cref="PilotLoadoutStore.ResetFed"/>, no refund).</para>
    ///
    /// <para>Everything goes through <see cref="PilotLoadoutStore"/>, so it writes the account inventory (the cloud
    /// record once signed in, otherwise this machine's snapshot of it) and no asset: this is a READER-side tool and
    /// records nothing in the tool ledger.</para>
    /// </summary>
    public static class PilotDevTools
    {
        [MenuItem("FrogletTools/Pilots/Clear Every Artifact Slot (dev)", false, 301)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 1,
            Description = "Empties every vessel's artifact slot in the account inventory (the artifacts stay in the hoard).")]
        public static void ClearSlots()
        {
            foreach (VesselClassType v in System.Enum.GetValues(typeof(VesselClassType)))
                if (v != VesselClassType.Any && v != VesselClassType.Random) PilotLoadoutStore.SetSlottedArtifact(v, string.Empty);
            Debug.Log("[Pilots] Emptied every artifact slot.");
        }

        [MenuItem("FrogletTools/Pilots/Grant 20 Crystals Of Every Element (dev)", false, 303)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 1,
            Description = "Credits 20 Mass, Space, Time, Charge and Omni crystals to the signed-in profile, to try the Ark's ARTIFACTS tab. Play mode only.")]
        public static void GrantCrystals()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Pilots] Enter Play mode first: crystals live on the loaded profile."); return; }
            foreach (var e in new[] { Element.Mass, Element.Space, Element.Time, Element.Charge, Element.Omni })
                PilotLoadoutStore.AddCrystals(e, 20, "dev grant");
        }

        [MenuItem("FrogletTools/Pilots/Reset Every Artifact Feed (dev)", false, 304)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 1,
            Description = "Empties the crystals fed to every held artifact, so each one is back at the level it was rolled at (no refund).")]
        public static void ResetFed()
        {
            var hoard = new List<ArtifactRecord>();
            PilotLoadoutStore.GetHoard(hoard);
            foreach (var a in hoard)
                PilotLoadoutStore.ResetFed(a.Id);
            Debug.Log($"[Pilots] Emptied what {hoard.Count} held artifact(s) were fed.");
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
