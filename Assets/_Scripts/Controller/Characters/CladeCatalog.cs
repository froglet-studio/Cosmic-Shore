using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The set of clade assets a genome can name. Built from an explicit list (tests, the editor
    /// window) or from <c>Resources/Characters/Clades</c> (<see cref="FromResources"/>) — drop a
    /// seventh asset in that folder and it is in the catalog with no code change.
    /// </summary>
    public sealed class CladeCatalog
    {
        public const string ResourcesFolder = "Characters/Clades";

        readonly List<CladeSO> _clades = new();
        readonly Dictionary<string, CladeSO> _byKey = new(StringComparer.Ordinal);

        public CladeCatalog(IEnumerable<CladeSO> clades)
        {
            foreach (var c in clades)
            {
                if (c == null || string.IsNullOrEmpty(c.Key)) continue;
                if (_byKey.ContainsKey(c.Key))
                    throw new InvalidOperationException($"CladeCatalog: two clade assets carry the key '{c.Key}'.");
                _byKey.Add(c.Key, c);
                _clades.Add(c);
            }
            _clades.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        }

        public static CladeCatalog FromResources() => new CladeCatalog(Resources.LoadAll<CladeSO>(ResourcesFolder));

        public IReadOnlyList<CladeSO> All => _clades;

        /// <summary>The non-human clades, in key order (deterministic for the roller).</summary>
        public List<CladeSO> Animals()
        {
            var list = new List<CladeSO>();
            foreach (var c in _clades) if (!c.IsHuman) list.Add(c);
            return list;
        }

        public CladeSO Human
        {
            get
            {
                foreach (var c in _clades) if (c.IsHuman) return c;
                return null;
            }
        }

        public bool TryGet(string key, out CladeSO clade) => _byKey.TryGetValue(key ?? string.Empty, out clade);

        public CladeSO Require(string key)
        {
            if (TryGet(key, out var c)) return c;
            throw new InvalidOperationException($"CladeCatalog: no clade with key '{key}'. Known: {string.Join(", ", _byKey.Keys)}.");
        }
    }
}
