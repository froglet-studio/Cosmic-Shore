using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The player's pilots and artifacts (Docs/Pilots/PILOTS_AND_ARTIFACTS.md): which pilot sits in each vessel's seat, which
    /// artifacts the player has found and at what rank, and which ones are slotted on each vessel. Ids are the
    /// <c>PilotSO.Id</c> / <c>ArtifactSO.Id</c> strings (pilot.rhino.tomahawk), which players save and nobody renames.
    ///
    /// <para>Saved on the DEVICE (PlayerPrefs) for now. The player's profile is where it belongs, beside the Hangar's element
    /// levels (<c>HangarUpgradeStore</c>), and moves there once the fair-play branch's profile changes land
    /// (claude/vigilant-archimedes-euoz8z rewrites <c>PlayerDataService</c>; anything on the backend goes through Shombith).
    /// An empty seat flies the vessel's base kit. Nothing in a match reads any of this yet.</para>
    /// </summary>
    public static class PilotLoadoutStore
    {
        /// <summary>A seat changed: the vessel and the pilot id now in it (empty = the base kit).</summary>
        public static event Action<VesselClassType, string> PilotChanged;
        /// <summary>A vessel's slotted artifacts, or the found ranks, changed.</summary>
        public static event Action<VesselClassType> ArtifactsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PilotChanged = null;
            ArtifactsChanged = null;
        }

        const char Separator = ',';
        static string SeatKey(VesselClassType vessel) => $"pilots.seat.{(int)vessel}";
        static string SlotsKey(VesselClassType vessel) => $"artifacts.slots.{(int)vessel}";
        static string FoundKey(string artifactId) => $"artifacts.found.{artifactId}";

        // ---- the seat ----

        public static string GetPilot(VesselClassType vessel) => PlayerPrefs.GetString(SeatKey(vessel), string.Empty);

        public static void SetPilot(VesselClassType vessel, string pilotId)
        {
            pilotId ??= string.Empty;
            if (pilotId.Length == 0) PlayerPrefs.DeleteKey(SeatKey(vessel));
            else PlayerPrefs.SetString(SeatKey(vessel), pilotId);
            PlayerPrefs.Save();
            PilotChanged?.Invoke(vessel, pilotId);
        }

        // ---- found artifacts ----

        /// <summary>Every artifact is free (Shombith, 2026-10-26: "all of these can be unlocked for free not buy it").</summary>
        public const bool EverythingIsFree = true;

        /// <summary>The rank the player holds this artifact at, 1 to 3; 0 = not held. Everything is free, so every
        /// artifact is held at the top rank unless a record says otherwise (a drop system would write one).</summary>
        public static int FoundRank(string artifactId)
        {
            if (string.IsNullOrEmpty(artifactId)) return 0;
            int saved = Mathf.Clamp(PlayerPrefs.GetInt(FoundKey(artifactId), 0), 0, 3);
            return EverythingIsFree ? 3 : saved;
        }

        /// <summary>Records a find. A rank only goes up; a lower find of one already held changes nothing.</summary>
        public static void Grant(string artifactId, int rank)
        {
            if (string.IsNullOrEmpty(artifactId)) return;
            rank = Mathf.Clamp(rank, 1, 3);
            if (rank <= FoundRank(artifactId)) return;
            PlayerPrefs.SetInt(FoundKey(artifactId), rank);
            PlayerPrefs.Save();
            ArtifactsChanged?.Invoke(VesselClassType.Any);
        }

        /// <summary>Forgets a find (dev tooling; the theft of milestone 2 would land here too).</summary>
        public static void Forget(string artifactId)
        {
            if (string.IsNullOrEmpty(artifactId)) return;
            PlayerPrefs.DeleteKey(FoundKey(artifactId));
            PlayerPrefs.Save();
            ArtifactsChanged?.Invoke(VesselClassType.Any);
        }

        // ---- slots ----

        /// <summary>The vessel's slotted artifact ids, in slot order, into <paramref name="into"/> (cleared first).</summary>
        public static void GetSlotted(VesselClassType vessel, List<string> into)
        {
            into.Clear();
            Split(PlayerPrefs.GetString(SlotsKey(vessel), string.Empty), into);
        }

        /// <summary>Slots the artifact if there is room and it is found, or takes it out if it is slotted. True when it
        /// ends up slotted.</summary>
        public static bool ToggleSlot(VesselClassType vessel, string artifactId, int slotCount)
        {
            var slotted = new List<string>();
            GetSlotted(vessel, slotted);
            bool canSlot = FoundRank(artifactId) > 0;
            bool now = Toggle(slotted, artifactId, slotCount, canSlot);
            PlayerPrefs.SetString(SlotsKey(vessel), string.Join(Separator.ToString(), slotted));
            PlayerPrefs.Save();
            ArtifactsChanged?.Invoke(vessel);
            return now;
        }

        /// <summary>The slot rule, pure: a slotted id comes out; an unslotted one goes in when it may and a slot is free.
        /// True when the id ends up slotted.</summary>
        public static bool Toggle(List<string> slotted, string id, int slotCount, bool canSlot)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (slotted.Remove(id)) return false;
            if (!canSlot || slotted.Count >= slotCount) return false;
            slotted.Add(id);
            return true;
        }

        static void Split(string saved, List<string> into)
        {
            if (string.IsNullOrEmpty(saved)) return;
            foreach (var part in saved.Split(Separator))
                if (part.Length > 0 && !into.Contains(part)) into.Add(part);
        }
    }
}
