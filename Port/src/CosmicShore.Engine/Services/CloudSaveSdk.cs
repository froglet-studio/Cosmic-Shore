// ─────────────────────────────────────────────────────────────────────────────
// CloudSaveSdk.cs — engine placeholder surface for the UGS Cloud Save SDK
// (original contract: Unity.Services.CloudSave — CloudSaveService.Instance.
// Data.Player.LoadAsync / SaveAsync, Models.Item with an IDeserializable
// Value). Grown per the MultiplayerSdk / Friends-SDK precedent so
// UGSCloudSaveProvider and the CloudData repository family port FULLY LIVE.
//
// The default <see cref="CloudSaveService.Instance"/> is a
// <see cref="LocalCloudSaveService"/>: honest single-process semantics — an
// in-memory per-key store that serializes on save and deserializes on load
// (a REAL JSON round-trip, so non-serializable payloads and dictionary
// round-tripping behave like the wire, not like a reference cache). Nothing
// pre-exists on a fresh process; saves persist for the process lifetime.
// Tests swap fakes into the settable Instance; the real SDK binding replaces
// the local service at the services phase.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Services
{
    /// <summary>
    /// Shared JSON options for cloud-save payloads. <c>IncludeFields</c> is
    /// load-bearing: the cloud data models are Unity-style [Serializable]
    /// classes with public FIELDS, which System.Text.Json ignores by default.
    /// </summary>
    public static class CloudSaveJson
    {
        public static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
    }

    /// <summary>A stored value that deserializes on demand (original contract: Unity.Services.CloudSave.Internal.IDeserializable).</summary>
    public interface IDeserializable
    {
        T GetAs<T>();
    }

    /// <summary>An <see cref="IDeserializable"/> over a serialized JSON payload.</summary>
    public sealed class JsonDeserializable : IDeserializable
    {
        readonly string _json;
        public JsonDeserializable(string json) => _json = json;
        public T GetAs<T>() => JsonSerializer.Deserialize<T>(_json, CloudSaveJson.Options);
    }

    /// <summary>One loaded key's payload (original contract: Unity.Services.CloudSave.Models.Item).</summary>
    public class Item
    {
        public IDeserializable Value { get; }
        public Item(IDeserializable value) => Value = value;
    }

    /// <summary>Per-player key/value data operations (original contract: Unity.Services.CloudSave.Internal.IPlayerDataService).</summary>
    public interface IPlayerDataApi
    {
        Task<Dictionary<string, Item>> LoadAsync(HashSet<string> keys);
        Task SaveAsync(Dictionary<string, object> data);
    }

    /// <summary>Original contract: Unity.Services.CloudSave.Internal.IDataService (the <c>Data.Player</c> hop).</summary>
    public interface ICloudSaveDataApi
    {
        IPlayerDataApi Player { get; }
    }

    /// <summary>The service surface the game consumes (original contract: Unity.Services.CloudSave.ICloudSaveService).</summary>
    public interface ICloudSaveService
    {
        ICloudSaveDataApi Data { get; }
    }

    /// <summary>
    /// Static access point (original contract: Unity.Services.CloudSave.CloudSaveService).
    /// Defaults to the in-process <see cref="LocalCloudSaveService"/>; tests swap fakes in
    /// and call <see cref="Reset"/> in teardown.
    /// </summary>
    public static class CloudSaveService
    {
        public static ICloudSaveService Instance { get; set; } = new LocalCloudSaveService();

        /// <summary>Restore the local default with an empty store (test isolation helper).</summary>
        public static void Reset() => Instance = new LocalCloudSaveService();
    }

    /// <summary>
    /// The single-process cloud store: saves serialize immediately (like the wire),
    /// loads return only keys that were actually saved this process, and a fresh
    /// service starts empty — honest local semantics for the CloudData repositories.
    /// </summary>
    public sealed class LocalCloudSaveService : ICloudSaveService, ICloudSaveDataApi, IPlayerDataApi
    {
        readonly Dictionary<string, string> _store = new();

        public ICloudSaveDataApi Data => this;
        public IPlayerDataApi Player => this;

        public Task<Dictionary<string, Item>> LoadAsync(HashSet<string> keys)
        {
            var result = new Dictionary<string, Item>();
            foreach (var key in keys)
                if (_store.TryGetValue(key, out var json))
                    result[key] = new Item(new JsonDeserializable(json));
            return Task.FromResult(result);
        }

        public Task SaveAsync(Dictionary<string, object> data)
        {
            foreach (var kv in data)
                _store[kv.Key] = JsonSerializer.Serialize(kv.Value, CloudSaveJson.Options);
            return Task.CompletedTask;
        }
    }
}
