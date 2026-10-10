using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The Vessel Studio's database in Amoebius: the claude.ai artifact's <c>db</c> (collections of JSON documents:
    /// <c>decisions</c>, <c>jobs</c>, <c>requests</c>, <c>data/users/&lt;id&gt;</c>) kept as one JSON file per collection
    /// under the data dir (<c>studio/db/</c>). Same shapes, same query semantics as the pages use them: add, doc get /
    /// set / update / delete, and orderBy(field, dir).limit(n), where (as in Firestore) a document without the ordering
    /// field is left out. Local to this computer: not shared with the artifact's store (/vessel-studio D33).
    /// </summary>
    public sealed class StudioStore
    {
        public const int MaxDocBytes = 256 * 1024;
        public const int MaxDocsPerCollection = 5000;
        public const int MaxLimit = 1000;

        static readonly Regex PathRe = new(@"^[A-Za-z0-9_-]{1,64}(/[A-Za-z0-9_.@:-]{1,96}){0,5}$", RegexOptions.CultureInvariant);
        static readonly Regex IdRe = new(@"^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant);

        readonly string _dir;
        readonly object _lock = new();
        readonly Dictionary<string, Dictionary<string, JsonObject>> _cache = new();
        readonly Dictionary<string, long> _version = new();

        /// <summary>A document was added (the job runner listens for <c>jobs</c>).</summary>
        public event Action<string, string, JsonObject>? Added;

        public StudioStore(string dir) { _dir = dir; Directory.CreateDirectory(dir); }

        public static bool ValidPath(string? p) => p != null && PathRe.IsMatch(p) && !p.Split('/').Any(s => s is "." or ".." || s.StartsWith('.'));
        public static bool ValidId(string? id) => id != null && IdRe.IsMatch(id);

        public static string NewId()
        {
            const string A = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Span<byte> b = stackalloc byte[20];
            RandomNumberGenerator.Fill(b);
            var c = new char[20];
            for (int i = 0; i < 20; i++) c[i] = A[b[i] % A.Length];
            return new string(c);
        }

        public string Add(string path, JsonObject data)
        {
            Check(path, null, data);
            string id;
            lock (_lock)
            {
                var coll = Load(path);
                if (coll.Count >= MaxDocsPerCollection) throw new StoreException("resource_exhausted", $"{path} is full ({MaxDocsPerCollection} documents)");
                do id = NewId(); while (coll.ContainsKey(id));
                coll[id] = (JsonObject)data.DeepClone();
                Save(path);
            }
            try { Added?.Invoke(path, id, (JsonObject)data.DeepClone()); } catch { /* a listener never fails the write */ }
            return id;
        }

        public JsonObject? Get(string path, string id)
        {
            Check(path, id, null);
            lock (_lock) return Load(path).TryGetValue(id, out var d) ? (JsonObject)d.DeepClone() : null;
        }

        public void Set(string path, string id, JsonObject data)
        {
            Check(path, id, data);
            lock (_lock)
            {
                var coll = Load(path);
                if (!coll.ContainsKey(id) && coll.Count >= MaxDocsPerCollection) throw new StoreException("resource_exhausted", $"{path} is full");
                coll[id] = (JsonObject)data.DeepClone();
                Save(path);
            }
        }

        /// <summary>Firestore's update: merges the top-level fields into an existing document; a missing one is not_found.</summary>
        public void Update(string path, string id, JsonObject patch)
        {
            Check(path, id, patch);
            lock (_lock)
            {
                var coll = Load(path);
                if (!coll.TryGetValue(id, out var doc)) throw new StoreException("not_found", $"no document {path}/{id}");
                var merged = (JsonObject)doc.DeepClone();
                foreach (var kv in patch) merged[kv.Key] = kv.Value?.DeepClone();
                if (merged.ToJsonString().Length > MaxDocBytes) throw new StoreException("invalid_argument", "document too large");
                coll[id] = merged;
                Save(path);
            }
        }

        public bool Delete(string path, string id)
        {
            Check(path, id, null);
            lock (_lock)
            {
                bool had = Load(path).Remove(id);
                if (had) Save(path);
                return had;
            }
        }

        /// <summary>orderBy(field, dir).limit(n): documents without the field are left out, as Firestore does; no field = id order.</summary>
        public List<(string Id, JsonObject Data)> List(string path, string? orderBy, bool desc, int limit)
        {
            Check(path, null, null);
            if (orderBy != null && !Regex.IsMatch(orderBy, @"^[A-Za-z0-9_]{1,64}$")) throw new StoreException("invalid_argument", "bad orderBy field");
            limit = limit <= 0 ? MaxLimit : Math.Min(limit, MaxLimit);
            lock (_lock)
            {
                IEnumerable<KeyValuePair<string, JsonObject>> docs = Load(path);
                if (orderBy == null) docs = docs.OrderBy(kv => kv.Key, StringComparer.Ordinal);
                else
                {
                    docs = docs.Where(kv => kv.Value.ContainsKey(orderBy) && kv.Value[orderBy] != null);
                    var cmp = Comparer<JsonNode?>.Create(CompareValues);
                    docs = desc ? docs.OrderByDescending(kv => kv.Value[orderBy], cmp).ThenByDescending(kv => kv.Key, StringComparer.Ordinal)
                                : docs.OrderBy(kv => kv.Value[orderBy], cmp).ThenBy(kv => kv.Key, StringComparer.Ordinal);
                }
                return docs.Take(limit).Select(kv => (kv.Key, (JsonObject)kv.Value.DeepClone())).ToList();
            }
        }

        /// <summary>Bumps on every write of the collection.</summary>
        public long Version(string path) { lock (_lock) return _version.TryGetValue(path, out var v) ? v : 0; }

        // Firestore's order across types: booleans, numbers, strings, then the rest by their JSON.
        static int Rank(JsonNode? n) => n switch
        {
            JsonValue v when v.TryGetValue<bool>(out _) => 1,
            JsonValue v when v.TryGetValue<double>(out _) => 2,
            JsonValue v when v.TryGetValue<string>(out _) => 3,
            _ => 4,
        };

        static int CompareValues(JsonNode? a, JsonNode? b)
        {
            int ra = Rank(a), rb = Rank(b);
            if (ra != rb) return ra.CompareTo(rb);
            return ra switch
            {
                1 => a!.GetValue<bool>().CompareTo(b!.GetValue<bool>()),
                2 => a!.GetValue<double>().CompareTo(b!.GetValue<double>()),
                3 => string.CompareOrdinal(a!.GetValue<string>(), b!.GetValue<string>()),
                _ => string.CompareOrdinal(a?.ToJsonString(), b?.ToJsonString()),
            };
        }

        static void Check(string path, string? id, JsonObject? data)
        {
            if (!ValidPath(path)) throw new StoreException("invalid_argument", "bad collection path");
            if (id != null && !ValidId(id)) throw new StoreException("invalid_argument", "bad document id");
            if (data != null && data.ToJsonString().Length > MaxDocBytes) throw new StoreException("invalid_argument", "document too large");
        }

        string FileOf(string path) => Path.Combine(new[] { _dir }.Concat(path.Split('/')).ToArray()) + ".json";

        Dictionary<string, JsonObject> Load(string path)
        {
            if (_cache.TryGetValue(path, out var c)) return c;
            c = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            var f = FileOf(path);
            if (File.Exists(f))
            {
                try
                {
                    if (JsonNode.Parse(File.ReadAllText(f)) is JsonObject o)
                        foreach (var kv in o)
                            if (kv.Value is JsonObject d && ValidId(kv.Key)) c[kv.Key] = (JsonObject)d.DeepClone();
                }
                catch (JsonException) { File.Copy(f, f + ".corrupt", overwrite: true); /* start empty, keep the bad file to look at */ }
            }
            _cache[path] = c;
            return c;
        }

        void Save(string path)
        {
            var f = FileOf(path);
            Directory.CreateDirectory(Path.GetDirectoryName(f)!);
            var o = new JsonObject();
            foreach (var kv in _cache[path]) o[kv.Key] = kv.Value.DeepClone();
            var tmp = f + ".tmp";
            File.WriteAllText(tmp, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, f, overwrite: true);
            _version[path] = (_version.TryGetValue(path, out var v) ? v : 0) + 1;
        }
    }

    /// <summary>A store refusal with the artifact db's error code (invalid_argument, not_found, resource_exhausted ...).</summary>
    public sealed class StoreException : Exception
    {
        public string Code { get; }
        public StoreException(string code, string message) : base(message) { Code = code; }
    }
}
