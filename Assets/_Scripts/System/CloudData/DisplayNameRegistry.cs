using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;

// Query/FieldFilter/EntityData live in Unity.Services.CloudSave.Models; the options types live in
// Unity.Services.CloudSave.Models.Data.Player. That namespace is aliased per-type rather than
// imported wholesale because its SaveOptions collides with the deprecated
// Unity.Services.CloudSave.SaveOptions (CS0104).
using PlayerQueryOptions = Unity.Services.CloudSave.Models.Data.Player.QueryOptions;
using PlayerSaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;
using PublicWriteAccessClassOptions = Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions;

namespace CosmicShore.Core
{
    /// <summary>Outcome of a display-name availability check.</summary>
    public enum DisplayNameAvailability
    {
        /// <summary>No other player has claimed this name.</summary>
        Available = 0,

        /// <summary>Another player already holds this name.</summary>
        Taken = 1,

        /// <summary>The check could not run (offline, not signed in, or the Cloud Save index is not configured). Policy for this case lives in <c>DisplayNameValidationConfigSO.BlockWhenUniquenessUnknown</c>.</summary>
        Unknown = 2,
    }

    /// <summary>
    /// Global display-name registry over UGS Cloud Save PUBLIC player data. Each player
    /// publishes their own normalized name under <see cref="PublicNameKey"/>; a claim
    /// queries that key across all players to reject duplicates.
    ///
    /// DASHBOARD REQUIREMENT: querying needs a Cloud Save index on the Public access
    /// class for the field "display_name_norm" (Unity Cloud dashboard → Cloud Save →
    /// Indexes → Create index → Player data / Public). Until the index exists, checks
    /// return <see cref="DisplayNameAvailability.Unknown"/> with a warning log.
    ///
    /// The check-then-claim pair is not atomic — two players claiming the same name in
    /// the same instant can both pass. Closing that race needs a Cloud Code reservation
    /// script; for now the window is a few hundred milliseconds and accepted.
    /// </summary>
    public static class DisplayNameRegistry
    {
        /// <summary>Public Cloud Save key holding each player's normalized display name.</summary>
        public const string PublicNameKey = "display_name_norm";

        static bool ServicesReady
        {
            get
            {
                try
                {
                    return UnityServices.State == ServicesInitializationState.Initialized &&
                           AuthenticationService.Instance != null &&
                           AuthenticationService.Instance.IsSignedIn;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Checks whether another player already claimed <paramref name="normalizedName"/>
        /// (produced by <see cref="DisplayNameValidator.NormalizeForUniqueness"/>).
        /// The caller's own claim never counts against them.
        /// </summary>
        public static async UniTask<DisplayNameAvailability> CheckAvailabilityAsync(string normalizedName)
        {
            if (string.IsNullOrEmpty(normalizedName) || !ServicesReady)
                return DisplayNameAvailability.Unknown;

            try
            {
                var query = new Query(
                    new List<FieldFilter>
                    {
                        new FieldFilter(PublicNameKey, normalizedName, FieldFilter.OpOptions.EQ, true),
                    },
                    new HashSet<string> { PublicNameKey });

                var results = await CloudSaveService.Instance.Data.Player
                    .QueryAsync(query, new PlayerQueryOptions())
                    .AsMainThread();

                string ownPlayerId = AuthenticationService.Instance.PlayerId;
                foreach (var entity in results)
                {
                    if (!string.Equals(entity.Id, ownPlayerId, StringComparison.Ordinal))
                        return DisplayNameAvailability.Taken;
                }

                return DisplayNameAvailability.Available;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning(
                    $"[DisplayNameRegistry] Availability check for '{normalizedName}' failed: {e.Message}. " +
                    $"If this persists online, verify the Cloud Save index on '{PublicNameKey}' " +
                    "(dashboard → Cloud Save → Indexes, Player data / Public access class).");
                return DisplayNameAvailability.Unknown;
            }
        }

        /// <summary>
        /// Publishes the local player's normalized name to the public registry — called on
        /// every successful name change, and once per session so accounts created before
        /// this feature backfill into the index over time.
        /// </summary>
        public static async UniTask<bool> PublishOwnNameAsync(string normalizedName)
        {
            if (string.IsNullOrEmpty(normalizedName) || !ServicesReady)
                return false;

            try
            {
                var data = new Dictionary<string, object> { { PublicNameKey, normalizedName } };
                await CloudSaveService.Instance.Data.Player
                    .SaveAsync(data, new PlayerSaveOptions(new PublicWriteAccessClassOptions()))
                    .AsMainThread();
                return true;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[DisplayNameRegistry] Publishing own name failed: {e.Message}");
                return false;
            }
        }
    }
}
