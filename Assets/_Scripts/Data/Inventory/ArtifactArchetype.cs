using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// What a unique artifact is rolled FROM: one authored payload transformation (an <c>ArtifactSO</c>: Javelin,
    /// Aegis, Hydra...), described in plain data so the pure generator never touches an asset
    /// (Docs/Pilots/INVENTORY.md §2.1). <c>ArtifactSO.ToArchetype()</c> builds one.
    /// </summary>
    public sealed class ArtifactArchetype
    {
        /// <summary>The template id every instance's id starts with, e.g. <c>artifact.javelin</c>.</summary>
        public string Id;
        /// <summary>The kind's name ("Javelin"), the noun of every instance's name.</summary>
        public string DisplayName;
        /// <summary>The tracks the kind authors, and what each raises as the player reads it ("line width"). An instance
        /// is attuned to ONE of these elements.</summary>
        public readonly List<(Element element, string aspect)> Tracks = new();

        public ArtifactArchetype() { }

        public ArtifactArchetype(string id, string displayName, params (Element element, string aspect)[] tracks)
        {
            Id = id;
            DisplayName = displayName;
            if (tracks != null) Tracks.AddRange(tracks);
        }

        /// <summary>What <paramref name="element"/>'s track raises, or empty.</summary>
        public string AspectOf(Element element)
        {
            foreach (var t in Tracks)
                if (t.element == element) return t.aspect ?? string.Empty;
            return string.Empty;
        }

        /// <summary>The elements an instance can be attuned to, in track order (Mass, Space, Time, Charge only).</summary>
        public List<Element> AttunableElements()
        {
            var list = new List<Element>();
            foreach (var e in ArtifactLevels.TrackElements)
                foreach (var t in Tracks)
                    if (t.element == e && !list.Contains(e)) list.Add(e);
            return list;
        }
    }
}
