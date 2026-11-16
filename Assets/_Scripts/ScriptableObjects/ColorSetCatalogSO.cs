using System;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The vision a colour set is made for (Docs/VesselModels/colour_sets.json, rule 5). Saved in the catalog as an int:
    /// never renumber.
    /// </summary>
    public enum ColourVision
    {
        None = 0,          // a set picked for its looks
        Protanopia = 1,    // no red cones (protanomaly is the milder form)
        Deuteranopia = 2,  // no green cones (deuteranomaly, the commonest)
        Tritanopia = 3,    // no blue cones
        Achromatopsia = 4, // no colour at all: lightness only
        LowVision = 5,     // reduced contrast sensitivity, cataract
        Contrast = 6,      // typical vision, the domains as far apart as the rules allow
    }

    /// <summary>
    /// The three kinds of set the Profile's list shows apart (Rev 112 D, Docs/MENU_REDESIGN_B.md §187). Read from an
    /// entry, never saved.
    /// </summary>
    public enum ColorSetKind
    {
        Starter = 0,     // yours from the start (price 0, picked for its looks)
        ColorBlind = 1,  // made for a vision: Color Blind supported, always free
        Premium = 2,     // unlocked with crystals
    }

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
                     "will give sets once they are planned. A set made for a vision is always free, whatever this " +
                     "says: it is accessibility (Shombith, 2026-11-09; IsPickable).")]
            public bool unlocked = true;
            [Tooltip("Jade, Ruby, Gold, Blue: each domain's rim over its face, as the Profile names them (\"Mint over " +
                     "Bronze\"). Empty shows nothing for that domain.")]
            public string[] pigments = Array.Empty<string>();
            [Tooltip("The vision this set is made for (colour_sets.json rule 5). None: a set picked for its looks. " +
                     "Settings > General > Colour sets opens the Profile's rail on the first set made for a vision.")]
            public ColourVision vision = ColourVision.None;
            [Tooltip("Crystals to unlock it in the Profile's list (Rev 112 D): above 0 the set is Premium, 0 it is " +
                     "yours from the start. A set made for a vision is Color Blind supported and always free, " +
                     "whatever this says.")]
            [Min(0)] public int price;
        }

        [Header("Sets, in card order")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public int Count => entries?.Length ?? 0;

        /// <summary>
        /// Whether a player may wear <paramref name="entry"/>: it names a palette and is unlocked, or it is made for a
        /// vision (colour blindness, low vision, Contrast), which is always free because it is accessibility
        /// (Shombith, 2026-11-09). A question, never an error.
        /// </summary>
        public static bool IsPickable(Entry entry) =>
            entry != null && entry.colorSet && (entry.unlocked || entry.vision != ColourVision.None || IsPurchased(entry));

        /// <summary>
        /// Answers whether the player bought the set with this id. Set by <c>PlayerDataService</c> (the player's
        /// unlocked rewards, <see cref="RewardId"/>); unset, nothing is bought. A question, never an error.
        /// </summary>
        public static Func<string, bool> Purchased;

        /// <summary>The reward id a bought set is kept under in the player's profile.</summary>
        public static string RewardId(string setId) => "colorset:" + setId;

        static bool IsPurchased(Entry entry) => !string.IsNullOrEmpty(entry.id) && Purchased != null && Purchased(entry.id);

        /// <summary>Which of the list's three kinds <paramref name="entry"/> is (Rev 112 D).</summary>
        public static ColorSetKind KindOf(Entry entry) =>
            entry == null ? ColorSetKind.Starter
            : entry.vision != ColourVision.None ? ColorSetKind.ColorBlind
            : entry.price > 0 ? ColorSetKind.Premium
            : ColorSetKind.Starter;

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
        /// The first entry made for <paramref name="vision"/>, or -1 (always -1 for <see cref="ColourVision.None"/>, which
        /// no set is made for). A question, never an error.
        /// </summary>
        public int IndexOfVision(ColourVision vision)
        {
            if (vision == ColourVision.None || entries == null) return -1;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].vision == vision) return i;
            return -1;
        }

        /// <summary>The first unlocked entry made for any vision, or -1: where Settings' breadcrumb opens the rail.</summary>
        public int IndexOfFirstVisionSet()
        {
            if (entries == null) return -1;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].vision != ColourVision.None && IsPickable(entries[i]))
                    return i;
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
