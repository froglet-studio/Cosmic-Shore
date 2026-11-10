using CosmicShore.Data;
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

            s_pending = request;
            KeepFlownHull(gameData);
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
        /// The pilot sails in the hull they were flying: the voyage scene spawns the vessel from the player's
        /// <c>NetDefaultVesselType</c>, and the menu writes its own attract hull there. No swap on arrival (a seat's hull
        /// swapped in mid-hand-off once left the player in a Manta they could not fly, 2026-10-31).
        /// </summary>
        static void KeepFlownHull(GameDataSO gameData)
        {
            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel == null || vessel is Object uo && !uo || vessel.VesselStatus == null) return;
            if (gameData.LocalPlayer is not Player player || !player.IsSpawned || !player.IsOwner) return;
            var hull = vessel.VesselStatus.VesselType;
            if (VesselRequirement.IsConcrete(hull) && player.NetDefaultVesselType.Value != hull)
                player.NetDefaultVesselType.Value = hull;
        }

        /// <summary>The loaded <see cref="GameDataSO"/> (one per app), for callers that are not injected.</summary>
        public static GameDataSO ResolveGameData()
        {
            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            return all is { Length: > 0 } ? all[0] : null;
        }
    }
}
