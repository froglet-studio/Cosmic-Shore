using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Game;
using UnityEngine;

public static class ActionInstanceFactory
{
    public static T CreatePerVesselInstance<T>(T asset, IVessel vessel) where T : ShipActionSO
    {
        if (!asset) return null;
        var cache = new Dictionary<ShipActionSO, ShipActionSO>(RefEq.Instance);
        return (T)CloneRecursively(asset, vessel, cache);
    }

    public static void DestroyInstances(IEnumerable<ShipActionSO> instances)
    {
        if (instances == null) return;
        foreach (var so in instances)
            if (so) UnityEngine.Object.Destroy(so);
    }

    // ---------- internals ----------
    private static ShipActionSO CloneRecursively(ShipActionSO source, IVessel vessel, Dictionary<ShipActionSO, ShipActionSO> cache)
    {
        if (!source) return null;
        if (cache.TryGetValue(source, out var cached)) return cached;

        var clone = UnityEngine.Object.Instantiate(source);
        clone.name = $"{source.name} (Runtime {vessel?.VesselStatus?.PlayerName})";
        cache[source] = clone;

        ReplaceNestedActions(clone, vessel, cache);
        clone.Initialize(vessel);
        return clone;
    }

    private static void ReplaceNestedActions(ShipActionSO owner, IVessel vessel, Dictionary<ShipActionSO, ShipActionSO> cache)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var f in owner.GetType().GetFields(flags))
        {
            var isUnitySerialized = f.IsPublic || Attribute.IsDefined(f, typeof(SerializeField));
            if (!isUnitySerialized) continue;

            var ft = f.FieldType;

            if (typeof(ShipActionSO).IsAssignableFrom(ft))
            {
                var cur = f.GetValue(owner) as ShipActionSO;
                if (!cur) continue;
                f.SetValue(owner, CloneRecursively(cur, vessel, cache));
                continue;
            }

            if (!typeof(IList).IsAssignableFrom(ft) || !ft.IsGenericType) continue;
            var elem = ft.GetGenericArguments()[0];
            if (!typeof(ShipActionSO).IsAssignableFrom(elem)) continue;

            var list = f.GetValue(owner) as IList;
            if (list == null || list.Count == 0) continue;

            var newList = (IList)Activator.CreateInstance(ft);
            foreach (var t in list)
            {
                var child = t as ShipActionSO;
                newList.Add(child ? CloneRecursively(child, vessel, cache) : null);
            }
            f.SetValue(owner, newList);
        }
    }

    private sealed class RefEq : IEqualityComparer<ShipActionSO>
    {
        public static readonly RefEq Instance = new();
        public bool Equals(ShipActionSO x, ShipActionSO y) => ReferenceEquals(x, y);
        public int GetHashCode(ShipActionSO obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}