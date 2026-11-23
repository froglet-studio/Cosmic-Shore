using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;

// The options types live in Unity.Services.CloudSave.Models.Data.Player and are aliased per type, because that
// namespace's members collide with the deprecated top-level ones (CS0104) - DisplayNameRegistry's convention.
using PlayerLoadOptions = Unity.Services.CloudSave.Models.Data.Player.LoadOptions;
using PlayerSaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;
using PublicReadAccessClassOptions = Unity.Services.CloudSave.Models.Data.Player.PublicReadAccessClassOptions;
using PublicWriteAccessClassOptions = Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions;

namespace CosmicShore.Core
{
    /// <summary>
    /// The squads friends can see (the Ark's Squads tab): each player's <see cref="SquadPublicRecord"/> in UGS Cloud Save
    /// PUBLIC player data under <see cref="PublicKey"/>. Reading needs only the player id (no Cloud Save index, unlike
    /// <see cref="DisplayNameRegistry"/>'s query); only the owner writes their own record. The rules that turn the
    /// records into a squad book are <see cref="SquadMembership"/>'s; this class only moves records.
    ///
    /// <para>The record is stored as a JSON string written with Newtonsoft and read back the same way, so the enum and
    /// struct fields round-trip exactly as the private SQUAD_DATA record does.</para>
    /// </summary>
    public static class SquadDirectory
    {
        public const string PublicKey = UGSKeys.SquadsPublic;

        /// <summary>True when Cloud Save can be called (services up and signed in).</summary>
        public static bool IsReady
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

        /// <summary>The signed-in player's id, or empty.</summary>
        public static string LocalPlayerId => IsReady ? AuthenticationService.Instance.PlayerId : string.Empty;

        /// <summary>Publishes the player's record. False when it could not be written (offline: the next change retries).</summary>
        public static async UniTask<bool> PublishAsync(SquadPublicRecord record)
        {
            if (record == null || !IsReady) return false;
            try
            {
                var data = new Dictionary<string, object> { { PublicKey, JsonConvert.SerializeObject(record) } };
                await CloudSaveService.Instance.Data.Player
                    .SaveAsync(data, new PlayerSaveOptions(new PublicWriteAccessClassOptions()))
                    .AsMainThread();
                return true;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[SquadDirectory] Publishing the squad record failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads <paramref name="playerId"/>'s record. Null when it could not be read (offline, or the read failed);
        /// an EMPTY record when the player has never published one (they have no squads).
        /// </summary>
        public static async UniTask<SquadPublicRecord> LoadAsync(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || !IsReady) return null;
            try
            {
                var result = await CloudSaveService.Instance.Data.Player
                    .LoadAsync(new HashSet<string> { PublicKey },
                        new PlayerLoadOptions(new PublicReadAccessClassOptions(playerId)))
                    .AsMainThread();
                if (!result.TryGetValue(PublicKey, out var item)) return new SquadPublicRecord();
                var json = item.Value.GetAs<string>();
                var record = string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<SquadPublicRecord>(json);
                if (record == null) return new SquadPublicRecord();
                record.Squads ??= new List<SavedSquad>();
                record.Claims ??= new List<SquadClaim>();
                return record;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[SquadDirectory] Reading {playerId}'s squad record failed: {e.Message}");
                return null;
            }
        }
    }
}
