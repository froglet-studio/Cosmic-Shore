using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One site as the texture painter sees it: its UV, its UV radius, its direction.</summary>
    public struct Landmark
    {
        public string Name;
        public Vector2 Uv;
        public Vector2 UvRadius;   // the site's ring radius in u and in v
        public Vector3 Dir;
        public bool Mirrored;
    }

    /// <summary>
    /// Every site of the head projected into texture space, so the painter can put a lash line
    /// on the lids and a nostril under the nose without knowing what shape the head took.
    /// </summary>
    public sealed class FaceLandmarks
    {
        readonly Dictionary<string, Landmark> _byName = new();
        public IHeadSurface Surface;

        public void Add(Landmark l) => _byName[l.Name] = l;
        public bool TryGet(string name, out Landmark l) => _byName.TryGetValue(name, out l);
        public IEnumerable<Landmark> All => _byName.Values;

        public Landmark Get(string name) => _byName.TryGetValue(name, out var l) ? l : default;
        public bool Has(string name) => _byName.ContainsKey(name);
    }
}
