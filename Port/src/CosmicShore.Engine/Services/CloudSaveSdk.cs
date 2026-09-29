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
        public string Key { get; set; }
        public IDeserializable Value { get; }
        public string WriteLock { get; set; } = string.Empty;
        public System.DateTime? Modified { get; set; }
        public Item(IDeserializable value) => Value = value;
    }

    /// <summary>Per-player key/value data operations (original contract: Unity.Services.CloudSave.Internal.IPlayerDataService).</summary>
    public interface IPlayerDataApi
    {
        Task<Dictionary<string, Item>> LoadAsync(HashSet<string> keys);
        Task SaveAsync(Dictionary<string, object> data);

        // Default members keep hand-written fakes compiling; the local service overrides them.
        Task<Dictionary<string, Item>> LoadAsync(HashSet<string> keys, Models.Data.Player.LoadOptions options) => LoadAsync(keys);
        Task<Dictionary<string, string>> SaveAsync(IDictionary<string, object> data, Models.Data.Player.SaveOptions options)
        {
            var copy = new Dictionary<string, object>(data);
            return SaveAsync(copy).ContinueWith(_ => new Dictionary<string, string>());
        }
        Task DeleteAsync(string key, Models.Data.Player.DeleteOptions options = null) => Task.CompletedTask;
        Task DeleteAllAsync(Models.Data.Player.DeleteAllOptions options = null) => Task.CompletedTask;
        Task<List<Models.ItemKey>> ListAllKeysAsync(Models.Data.Player.ListAllKeysOptions options = null)
            => Task.FromResult(new List<Models.ItemKey>());
        Task<List<Models.EntityData>> QueryAsync(Models.Query query, Models.Data.Player.QueryOptions options = null)
            => Task.FromResult(new List<Models.EntityData>());
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

        public Task<Dictionary<string, string>> SaveAsync(IDictionary<string, object> data, Models.Data.Player.SaveOptions options)
        {
            var writeLocks = new Dictionary<string, string>();
            foreach (var kv in data)
            {
                _store[kv.Key] = JsonSerializer.Serialize(kv.Value, CloudSaveJson.Options);
                writeLocks[kv.Key] = System.Guid.NewGuid().ToString("N");
            }
            return Task.FromResult(writeLocks);
        }

        public Task DeleteAsync(string key, Models.Data.Player.DeleteOptions options = null)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task DeleteAllAsync(Models.Data.Player.DeleteAllOptions options = null)
        {
            _store.Clear();
            return Task.CompletedTask;
        }

        public Task<List<Models.ItemKey>> ListAllKeysAsync(Models.Data.Player.ListAllKeysOptions options = null)
        {
            var keys = new List<Models.ItemKey>();
            foreach (var k in _store.Keys) keys.Add(new Models.ItemKey(k, string.Empty, System.DateTime.UtcNow));
            return Task.FromResult(keys);
        }

        /// <summary>Only the local player exists offline: a query can match only this player's own data.</summary>
        public Task<List<Models.EntityData>> QueryAsync(Models.Query query, Models.Data.Player.QueryOptions options = null)
        {
            var results = new List<Models.EntityData>();
            if (query?.Fields == null) return Task.FromResult(results);
            foreach (var f in query.Fields)
            {
                if (!_store.TryGetValue(f.Key, out var json)) return Task.FromResult(results);
                var stored = JsonSerializer.Deserialize<object>(json, CloudSaveJson.Options)?.ToString();
                bool eq = string.Equals(stored, f.Value?.ToString(), System.StringComparison.Ordinal);
                if ((f.Op == Models.FieldFilter.OpOptions.EQ) != eq) return Task.FromResult(results);
            }
            var data = new List<Item>();
            foreach (var k in query.ReturnKeys ?? new HashSet<string>())
                if (_store.TryGetValue(k, out var j)) data.Add(new Item(new JsonDeserializable(j)) { Key = k });
            results.Add(new Models.EntityData(AuthenticationService.Instance?.PlayerId ?? "local", data));
            return Task.FromResult(results);
        }
    }
}
