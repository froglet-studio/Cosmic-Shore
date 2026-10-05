using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Services.Models
{
    /// <summary>A query filter on an indexed key (original: Unity.Services.CloudSave.Models.FieldFilter).</summary>
    public class FieldFilter
    {
        public enum OpOptions { EQ, NE, LT, LE, GT, GE }
        public string Key { get; }
        public object Value { get; }
        public OpOptions Op { get; }
        public bool Asc { get; }
        public FieldFilter(string key, object value, OpOptions op, bool asc) { Key = key; Value = value; Op = op; Asc = asc; }
    }

    /// <summary>An index query (original: Unity.Services.CloudSave.Models.Query).</summary>
    public class Query
    {
        public List<FieldFilter> Fields { get; }
        public HashSet<string> ReturnKeys { get; }
        public int Offset { get; }
        public int Limit { get; }
        public Query(List<FieldFilter> fields, HashSet<string> returnKeys = null, int offset = 0, int limit = 0)
        { Fields = fields; ReturnKeys = returnKeys; Offset = offset; Limit = limit; }
    }

    /// <summary>One matched entity (original: Unity.Services.CloudSave.Models.EntityData).</summary>
    public class EntityData
    {
        public string Id { get; }
        public List<Item> Data { get; }
        public EntityData(string id, List<Item> data) { Id = id; Data = data; }
    }

    public class ItemKey
    {
        public string Key { get; }
        public string WriteLock { get; }
        public DateTime Modified { get; }
        public ItemKey(string key, string writeLock, DateTime modified) { Key = key; WriteLock = writeLock; Modified = modified; }
    }
}

namespace CosmicShore.Engine.Services.Models.Data.Player
{
    public abstract class AccessClassOptions { }
    public class DefaultWriteAccessClassOptions : AccessClassOptions { }
    public class PublicWriteAccessClassOptions : AccessClassOptions { }
    public class ProtectedWriteAccessClassOptions : AccessClassOptions { }
    public class DefaultReadAccessClassOptions : AccessClassOptions { }
    public class PublicReadAccessClassOptions : AccessClassOptions { public PublicReadAccessClassOptions(string playerId = null) { PlayerId = playerId; } public string PlayerId { get; } }

    public class SaveOptions
    {
        public AccessClassOptions AccessClassOptions { get; }
        public SaveOptions(AccessClassOptions accessClassOptions = null) { AccessClassOptions = accessClassOptions ?? new DefaultWriteAccessClassOptions(); }
    }

    public class LoadOptions
    {
        public AccessClassOptions AccessClassOptions { get; }
        public LoadOptions(AccessClassOptions accessClassOptions = null) { AccessClassOptions = accessClassOptions; }
    }

    public class DeleteOptions
    {
        public string WriteLock { get; set; }
        public AccessClassOptions AccessClassOptions { get; }
        public DeleteOptions(AccessClassOptions accessClassOptions = null) { AccessClassOptions = accessClassOptions; }
    }

    public class DeleteAllOptions { public DeleteAllOptions(AccessClassOptions a = null) { } }
    public class ListAllKeysOptions { public ListAllKeysOptions(AccessClassOptions a = null) { } }
    public class QueryOptions { public QueryOptions(AccessClassOptions a = null) { } }
}
