using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace CosmicShore.Soap
{
    /// <summary>
    /// Listener components registered on one channel. The list keeps registration order
    /// (raises walk it newest first); the set makes register / unregister O(1), which
    /// matters at prism scale, where every prism carries a listener on shared channels.
    /// </summary>
    internal sealed class SoapListenerSet<TListener> where TListener : Object
    {
        readonly List<TListener> _items = new();
        readonly HashSet<TListener> _lookup = new();

        public List<TListener> Items => _items;

        public void Add(TListener listener)
        {
            if (_lookup.Add(listener)) _items.Add(listener);
        }

        public void Remove(TListener listener)
        {
            if (_lookup.Remove(listener)) _items.Remove(listener);
        }

        public void Clear()
        {
            _items.Clear();
            _lookup.Clear();
        }
    }

    /// <summary>
    /// Remembers which Unity objects own a channel's C# subscribers, so a test or tool can
    /// ask "who listens here?" without raising anything (see GetAllObjects).
    /// </summary>
    internal static class SoapSubscribers
    {
        public static void Track(List<Object> owners, Delegate handler)
        {
            if (handler?.Target is Object owner && owner != null && !owners.Contains(owner))
                owners.Add(owner);
        }

        public static void Untrack(List<Object> owners, Delegate handler)
        {
            if (handler?.Target is Object owner)
                owners.Remove(owner);
        }

        public static List<Object> All<TListener>(List<TListener> listeners, List<Object> owners)
            where TListener : Object
        {
            var all = new List<Object>(listeners.Count + owners.Count);
            foreach (var listener in listeners) all.Add(listener);
            all.AddRange(owners);
            return all;
        }
    }
}
