using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// What the WHAT'S NEW window says (Frogma Rev 121 C, picked by Shombith on 2026-11-21, Docs/MENU_REDESIGN_B.md
    /// section 216): the window's own words and one entry per update, newest first. Every word on the window is a field
    /// here (CLAUDE.md, the words rule); an empty field shows nothing.
    ///
    /// <para>There is exactly one asset, at <c>Assets/Resources/WhatsNew.asset</c>, loaded with no scene wiring, as the
    /// credits manifest is. The next update is an asset edit: add its entry at the top and give it a new
    /// <see cref="Entry.Id"/>. A new id is what makes the window open once by itself after the update
    /// (<see cref="CosmicShore.UI.WhatsNewRules.ShouldOpenByItself"/>).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "WhatsNew", menuName = "ScriptableObjects/UI/What's New")]
    public class WhatsNewSO : ScriptableObject
    {
        /// <summary>Where <see cref="Load"/> looks.</summary>
        public const string ResourcePath = "WhatsNew";

        /// <summary>What an entry's button does. Explicit values: they are serialized.</summary>
        public enum Action
        {
            None = 0,
            OpenVoyages = 1,
        }

        [Serializable]
        public class Feature
        {
            [Tooltip("The feature's name, one or two words (\"Take a job\").")]
            public string Title;

            [Tooltip("One sentence on what the player can now do.")]
            [TextArea(1, 3)]
            public string Line;
        }

        [Serializable]
        public class Entry
        {
            [Tooltip("Never shown. The update's id (\"0.3\"); a new id at the top of the list opens the window once " +
                     "by itself on the next visit to the menu. Never change the id of an entry players have seen.")]
            public string Id;

            [Tooltip("The update's label, shown over its name (\"Update 0.3\").")]
            public string Version;

            [Tooltip("The update's name (\"Voyages\").")]
            public string Name;

            [Tooltip("When it shipped, shown beside the version (\"November 2026\").")]
            public string Date;

            [Tooltip("One sentence under the name.")]
            [TextArea(1, 3)]
            public string Line;

            [Tooltip("Optional. The picture across the top of the entry. Empty shows the HyperSea.")]
            public Sprite Art;

            [Tooltip("Up to three features, shown side by side under \"In this update\".")]
            public List<Feature> Features = new();

            [Tooltip("Smaller changes and fixes, one line each, under \"Also in this update\".")]
            [TextArea(1, 2)]
            public List<string> Also = new();

            [Tooltip("What the entry's button does. Shown on the newest entry only; None shows no button.")]
            public Action Button = Action.None;

            [Tooltip("The button's words (\"Set sail\").")]
            public string ButtonLabel;
        }

        [Header("The window's words (empty shows nothing)")]
        [Tooltip("Over the title.")]
        [SerializeField] string eyebrow = "Cosmic Shore";
        [SerializeField] string title = "What's new";
        [Tooltip("The tag beside the newest update in the list.")]
        [SerializeField] string newTag = "New";
        [SerializeField] string featuresHeading = "In this update";
        [SerializeField] string alsoHeading = "Also in this update";
        [Tooltip("{version} and {date} are filled from the entry.")]
        [SerializeField] string versionLine = "{version}  -  {date}";
        [SerializeField] string closeLabel = "Close";
        [SerializeField] string closeKey = "B";
        [SerializeField] string buttonKey = "A";

        [Header("Updates, newest first")]
        [SerializeField] List<Entry> updates = new();

        public string Eyebrow => eyebrow;
        public string Title => title;
        public string NewTag => newTag;
        public string FeaturesHeading => featuresHeading;
        public string AlsoHeading => alsoHeading;
        public string VersionLine => versionLine;
        public string CloseLabel => closeLabel;
        public string CloseKey => closeKey;
        public string ButtonKey => buttonKey;
        public IReadOnlyList<Entry> Updates => updates;

        /// <summary>The newest update, or null when there is none.</summary>
        public Entry Newest => updates is { Count: > 0 } ? updates[0] : null;

        static WhatsNewSO s_Loaded;

        public static WhatsNewSO Load()
        {
            if (!s_Loaded) s_Loaded = Resources.Load<WhatsNewSO>(ResourcePath);
            return s_Loaded;
        }
    }
}
