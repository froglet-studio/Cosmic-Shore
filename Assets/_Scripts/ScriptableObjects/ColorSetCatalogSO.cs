using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The colour sets a player can pick in the Profile (Docs/MENU_REDESIGN_B.md §122-123; Garrett's sets,
    /// Docs/VesselModels/colour_sets.json). Each entry names one <see cref="SO_ColorSet"/> and the words the
    /// Profile shows for it. The game's palette is still the one slot on <c>ThemeManagerDataContainer.asset</c>;
    /// <c>ThemeManager</c> puts the player's pick into that slot and repaints, so everything that reads the slot
    /// follows. Written by <c>Tools/Build/author_colour_set_catalog.py</c> (<c>--check</c> is the drift gate).
    /// </summary>
    [CreateAssetMenu(fileName = "ColorSetCatalog", menuName = "ScriptableObjects/ColorSetCatalog")]
    public class ColorSetCatalogSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Stable id the player's pick is saved under. Never rename one that has shipped.")]
            public string id;
            [Tooltip("The palette this entry applies.")]
            public SO_ColorSet colorSet;
            [Tooltip("The set's name on its card, the preview and the loading screen. Empty shows nothing.")]
            public string displayName;
            [Tooltip("One line under the name. Empty shows nothing.")]
            [TextArea] public string line;
            [Tooltip("Whether a player may pick it. Every set is unlocked for now (Shombith, 2026-11-05); quests " +
                     "will give sets once they are planned.")]
            public bool unlocked = true;
            [Tooltip("Jade, Ruby, Gold, Blue: each domain's rim over its face, as the Profile names them (\"Mint over " +
                     "Bronze\"). Empty shows nothing for that domain.")]
            public string[] pigments = Array.Empty<string>();
            [Tooltip("The vision this set is made for (colour_sets.json rule 5). Off: a set picked for its looks. Any " +
                     "other value: Settings > General > Colorblind mode wears this set when the player picks that mode, " +
                     "and the mode's option shows this entry's name.")]
            public ColorblindModeSetting vision = ColorblindModeSetting.Off;
        }

        [Header("Sets, in card order")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public int Count => entries?.Length ?? 0;

        /// <summary>The pigment words for domain <paramref name="domainIndex"/> (Jade 0 .. Blue 3), or empty.</summary>
        public static string Pigment(Entry entry, int domainIndex) =>
            entry?.pigments != null && domainIndex >= 0 && domainIndex < entry.pigments.Length
                ? entry.pigments[domainIndex] ?? string.Empty : string.Empty;

        public Entry Get(int index) => entries != null && index >= 0 && index < entries.Length ? entries[index] : null;

        /// <summary>The entry that applies <paramref name="set"/>, or -1. A question, never an error.</summary>
        public int IndexOf(SO_ColorSet set)
        {
            if (!set || entries == null) return -1;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].colorSet == set) return i;
            return -1;
        }

        /// <summary>
        /// The first entry made for <paramref name="vision"/>, or -1 (always -1 for <see cref="ColorblindModeSetting.Off"/>,
        /// which no set is made for). A question, never an error.
        /// </summary>
        public int IndexOfVision(ColorblindModeSetting vision)
        {
            if (vision == ColorblindModeSetting.Off || entries == null) return -1;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].vision == vision) return i;
            return -1;
        }

        /// <summary>The entry saved under <paramref name="id"/>, or -1. A question, never an error.</summary>
        public int IndexOfId(string id)
        {
            if (string.IsNullOrEmpty(id) || entries == null) return -1;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].id == id) return i;
            return -1;
        }
    }

    /// <summary>
    /// The player's picked colour set, kept on this device. A colour set changes how this player sees the game
    /// and nothing anyone else sees, so it is local (PlayerPrefs), like the other per-device view settings.
    /// </summary>
    public static class ColorSetPreference
    {
        const string Key = "CosmicShore.ColorSet";

        public static string Load() => PlayerPrefs.GetString(Key, string.Empty);

        public static void Save(string id)
        {
            if (string.IsNullOrEmpty(id)) PlayerPrefs.DeleteKey(Key);
            else PlayerPrefs.SetString(Key, id);
            PlayerPrefs.Save();
        }
    }
}
