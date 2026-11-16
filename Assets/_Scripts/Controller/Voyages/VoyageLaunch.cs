using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>What a voyage sets out to do: a job off the board, or the Ark escort.</summary>
    public enum VoyageLaunchKind
    {
        Job,
        Escort,
    }

    /// <summary>
    /// One voyage the app shell asked for: the job (or the escort's difficulty), the squad, and whether the AI flies the
    /// local seat too (the escort sim). Carried from the menu into <c>Voyage.unity</c> by <see cref="VoyageLaunch"/>.
    /// </summary>
    public sealed class VoyageLaunchRequest
    {
        public VoyageLaunchKind Kind;

        /// <summary>The job to sail (<see cref="VoyageLaunchKind.Job"/>).</summary>
        public VoyageJob Job;

        /// <summary>The escort's difficulty, 1-9 (<see cref="VoyageLaunchKind.Escort"/>).</summary>
        public int Difficulty;

        /// <summary>The squad that sails; null is the player's active squad.</summary>
        public SavedSquad Squad;

        /// <summary>True when the AI flies the local seat as well (the escort sim, never a player path).</summary>
        public bool AiLocal;

        public static VoyageLaunchRequest ForJob(VoyageJob job) =>
            new() { Kind = VoyageLaunchKind.Job, Job = job };

        public static VoyageLaunchRequest ForEscort(int difficulty, SavedSquad squad = null, bool aiLocal = false) =>
            new() { Kind = VoyageLaunchKind.Escort, Difficulty = difficulty, Squad = squad, AiLocal = aiLocal };

        public override string ToString() => Kind == VoyageLaunchKind.Escort
            ? $"escort d{Difficulty}{(AiLocal ? " (AI local)" : string.Empty)}"
            : $"job {Job?.Title ?? "(none)"}";
    }

    /// <summary>
    /// Voyages have their own scene (Garrett, 2026-11-01: freestyle stays a toybox simulation, and a voyage has high stakes
    /// and a specific place at each start and stop). <see cref="Begin"/> is the one way in: it holds the request and loads
    /// <see cref="SceneName"/> through the launch path every game uses (<see cref="GameDataSO.InvokeGameLaunch"/>, so the
    /// host loads it over Netcode and a party follows). In the scene, <see cref="VoyageScene"/> takes the request
    /// (<see cref="Take"/>) and sails it, and returns to the menu when the voyage is over.
    /// </summary>
    public static class VoyageLaunch
    {
        /// <summary>The voyage scene's name; it must be in the build list.</summary>
        public const string SceneName = "Voyage";

        /// <summary>The voyage scene's asset path.</summary>
        public const string ScenePath = "Assets/_Scenes/Voyage.unity";

        static VoyageLaunchRequest s_pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_pending = null;

        /// <summary>The voyage asked for and not yet taken by the scene, or null.</summary>
        public static VoyageLaunchRequest Pending => s_pending;

        /// <summary>
        /// Load the voyage scene to sail <paramref name="request"/>. False (with a warning) when there is nothing to sail
        /// or no game data to launch with. A party client cannot start a voyage: the host's scene is the party's.
        /// </summary>
        public static bool Begin(VoyageLaunchRequest request, GameDataSO gameData = null)
        {
            if (request == null || request.Kind == VoyageLaunchKind.Job && request.Job == null) return false;
            // Milestone 1 locks the crew's jobs but the listed few (VoyageToyDefinitionSO.IsJobOpen); every way in asks here.
            if (request.Kind == VoyageLaunchKind.Job && !VoyageToyDefinitionSO.JobSails(request.Job))
            {
                CSDebug.LogWarning($"[Voyage] {request.Job.TemplateId} is locked for now - only the open voyages sail.");
                return false;
            }
            gameData ??= ResolveGameData();
            if (!gameData)
            {
                CSDebug.LogWarning("[Voyage] No GameDataSO - the voyage scene cannot load.");
                return false;
            }
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer)
            {
                CSDebug.LogWarning("[Voyage] Only the host sets sail; a party follows the host into the voyage.");
                return false;
            }

            // The squad that sails is fixed here, as the Voyage window shows it, and the scene sails exactly this one.
            request.Squad ??= VoyageRun.ActiveSquad(gameData);
            s_pending = request;
            SeatLocalHull(gameData, request.Squad);
            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[Voyage] Set sail: {request} - loading {SceneName}.");
            gameData.SceneName = SceneName;
            gameData.InvokeGameLaunch();
            return true;
        }

        /// <summary>The request the scene sails, once (later calls return null).</summary>
        public static VoyageLaunchRequest Take()
        {
            var request = s_pending;
            s_pending = null;
            return request;
        }

        /// <summary>
        /// The pilot sails in the vessel their seat names (the Voyage window's seat picker, the squad's card): the voyage
        /// scene spawns the local vessel from the player's <c>NetDefaultVesselType</c>, so the seat's hull is written there
        /// BEFORE the scene loads and the pilot spawns in it at the berth. Nothing is swapped on arrival (a seat's hull
        /// swapped in mid-hand-off once left the player in a Manta they could not fly, 2026-10-31). The seat's pilot and
        /// artifact go to that hull's loadout, as the Ark's Take into does. A seat with no hull, or a hull with no vessel
        /// prefab, keeps the hull the pilot was flying.
        /// </summary>
        static void SeatLocalHull(GameDataSO gameData, SavedSquad squad)
        {
            if (gameData.LocalPlayer is not Player player || !player.IsSpawned || !player.IsOwner) return;
            var seat = SeatHull(squad);
            var hull = HullThatSails(gameData, squad);
            if (hull != seat)
            {
                if (VesselRequirement.IsConcrete(seat))
                    CSDebug.LogWarning($"[Voyage] The seat names {seat}, which has no vessel prefab - the pilot sails the hull they flew.");
                if (!VesselRequirement.IsConcrete(hull)) return;
            }
            else
            {
                var slot = squad.GetSlot(LocalSeat(squad));
                if (!slot.UsesDefaultPilot() && PilotLoadoutStore.TryGetPilot(slot.PilotId, out _))
                    PilotLoadoutStore.SetPilot(hull, slot.PilotId);
                PilotLoadoutStore.SetSlottedArtifact(hull, slot.ArtifactId);
            }
            if (player.NetDefaultVesselType.Value != hull) player.NetDefaultVesselType.Value = hull;
            if (gameData.selectedVesselClass) gameData.selectedVesselClass.Value = hull;
        }

        /// <summary>The seat the local pilot flies in <paramref name="squad"/>, as the voyage deploys it
        /// (<c>VoyageRun.Deploy</c>).</summary>
        public static int LocalSeat(SavedSquad squad) =>
            squad == null ? 0 : Mathf.Clamp(squad.MySeat, 0, SavedSquad.SlotCount - 1);

        /// <summary>The hull the local pilot's seat names, or the default (no class) when there is no squad.</summary>
        public static VesselClassType SeatHull(SavedSquad squad) =>
            squad == null ? default : squad.GetSlot(LocalSeat(squad)).Vessel;

        /// <summary>
        /// The hull the local pilot spawns in when <paramref name="squad"/> sails: their seat's, or the hull they were
        /// flying when the seat's has no vessel prefab. The Voyage window's "You sail as" reads this, so it names what
        /// <see cref="Begin"/> spawns.
        /// </summary>
        public static VesselClassType HullThatSails(GameDataSO gameData, SavedSquad squad)
        {
            var hull = SeatHull(squad);
            return CanSpawn(hull) ? hull : FlownHull(gameData);
        }

        static VesselClassType FlownHull(GameDataSO gameData)
        {
            if (!gameData) return default;
            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel == null || vessel is Object uo && !uo || vessel.VesselStatus == null) return default;
            return vessel.VesselStatus.VesselType;
        }

        static bool CanSpawn(VesselClassType hull)
        {
            if (!VesselRequirement.IsConcrete(hull)) return false;
            var initializer = Object.FindFirstObjectByType<MenuServerPlayerVesselInitializer>();
            var prefabs = initializer ? initializer.VesselPrefabContainer : null;
            return !prefabs || prefabs.TryGetShipPrefab(hull, out _, reportMissing: false);
        }

        /// <summary>The loaded <see cref="GameDataSO"/> (one per app), for callers that are not injected.</summary>
        public static GameDataSO ResolveGameData()
        {
            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            return all is { Length: > 0 } ? all[0] : null;
        }
    }
}
