using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The shipped avatar illustrations, as genomes: one JSON per profile icon under
    /// <c>Resources/Characters/AvatarPresets</c> (authored by <c>author_character_assets.py</c>'s
    /// PRESETS table), each the nearest point in the generator's space to that picture. They are
    /// ordinary genomes — load one into the inspector and every slider still works.
    /// </summary>
    public static class AvatarPresets
    {
        public const string ResourcesFolder = "Characters/AvatarPresets";

        public struct Preset
        {
            public string Name;      // the JSON file's name, e.g. "Avatar_02_ProfileIcon02"
            public string IconName;  // the profile icon sprite it recreates, e.g. "ProfileIcon02"
            public CharacterGenome Genome;
        }

        public static List<Preset> FromResources()
        {
            var list = new List<Preset>();
            var assets = Resources.LoadAll<TextAsset>(ResourcesFolder);
            foreach (var a in assets)
            {
                if (a == null || !CharacterGenome.TryFromJson(a.text, out var g)) continue;
                list.Add(new Preset { Name = a.name, IconName = IconNameOf(a.name), Genome = g });
            }
            list.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
            return list;
        }

        /// <summary>"Avatar_02_ProfileIcon02" → "ProfileIcon02".</summary>
        public static string IconNameOf(string presetName)
        {
            int i = presetName.IndexOf("_ProfileIcon", System.StringComparison.Ordinal);
            return i < 0 ? string.Empty : presetName.Substring(i + 1);
        }
    }
}
