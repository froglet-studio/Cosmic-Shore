using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// What each arcade mode's CONTROLS section shows besides the derived ability rows — edited in
    /// ONE asset (<c>Resources/ModeControlsLibrary</c>) instead of per panel, per scene, or per
    /// prefab.
    ///
    /// <para><b>Why this exists:</b> the flight rows ("Left stick — steer") were authored on the
    /// panel itself, so every card showed them whether or not they said anything worth the space —
    /// and there was nowhere to say "this mode's section should open with THIS". The section's
    /// derived half stays derived (the vessel's <see cref="ScriptableObjects.ElementalAbilityMapSO"/>
    /// is the authority on abilities and nothing here can contradict it); this asset owns only the
    /// AUTHORED half: which extra rows a mode wants, if any.</para>
    ///
    /// <para><b>The default is NO extra rows.</b> A mode's designated abilities and their controls
    /// are the section; the stick primer earned its place on no card, so a mode has to ask for it.
    /// Add a mode entry to give one card bespoke rows (a mode-specific manoeuvre, a reminder that
    /// the ball only converts on a juke), or put rows in <see cref="DefaultRows"/> to give them to
    /// every card that has no entry of its own.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ModeControlsLibrary",
                     menuName = "ScriptableObjects/UI/Mode Controls Library")]
    public class ModeControlsLibrarySO : ScriptableObject
    {
        /// <summary>Where the panel loads this from.</summary>
        public const string ResourcePath = "ModeControlsLibrary";

        /// <summary>One mode's authored section content.</summary>
        [Serializable]
        public class ModeEntry
        {
            [Tooltip("The mode this entry configures.")]
            public GameModes Mode;

            [Tooltip("Rows shown ABOVE the derived ability rows, in this order. Empty means the " +
                     "abilities alone - which is the default for every mode without an entry too.")]
            public List<VesselControlsPanel.FlightControl> Rows = new();

            [Tooltip("Untick to suppress the derived ability rows for this mode - for a mode " +
                     "whose vessel abilities genuinely do not apply. Leave on everywhere else; " +
                     "the abilities are the point of the section.")]
            public bool ShowAbilityRows = true;

            [Tooltip("Show ONLY these elements' abilities on this card. Empty = all four, which " +
                     "is the right default: they are the vessel's abilities and the vessel is " +
                     "what you fly.\n\n" +
                     "This exists because a hull is shared across modes - Skim Race and Joust are " +
                     "both the Squirrel, so without a filter the two cards say exactly the same " +
                     "thing about two very different games.")]
            public List<Element> Abilities = new();

            [Tooltip("Describe THIS hull's abilities, whatever the card lists. Any = use the " +
                     "card's own vessel.\n\n" +
                     "For a card that lists SEVERAL vessels (Scurry, Brood Rush, Freestyle): with " +
                     "no single hull to describe those cards showed no abilities at all, which is " +
                     "worse than describing one of them. Name the hull the mode is really about.")]
            public VesselClassType Vessel = VesselClassType.Any;
        }

        /// <summary>What a scoring metric LOOKS like - the crystal, the joust, the goal.</summary>
        [Serializable]
        public class MetricIcon
        {
            public ScoringMetric Metric;
            public Sprite Icon;
        }

        [Tooltip("One icon per scoring metric, shared by every surface that shows an objective - " +
                 "the launch panel's objective box and its micro toast. A metric without an icon " +
                 "draws text alone, which is honest rather than broken.")]
        public List<MetricIcon> ObjectiveIcons = new();

        /// <summary>The icon for a metric, or null when none is authored.</summary>
        public Sprite IconForMetric(ScoringMetric metric)
        {
            if (ObjectiveIcons == null) return null;
            foreach (var entry in ObjectiveIcons)
                if (entry != null && entry.Metric == metric) return entry.Icon;
            return null;
        }

        [Tooltip("Rows for a mode that has NO entry below. Ships EMPTY on purpose: the section " +
                 "then shows the vessel's designated abilities and nothing else.")]
        public List<VesselControlsPanel.FlightControl> DefaultRows = new();

        [Tooltip("Per-mode overrides. A mode appears at most once; the first entry wins.")]
        public List<ModeEntry> Entries = new();

        /// <summary>This mode's entry, or null when it has none (use <see cref="DefaultRows"/>).</summary>
        public ModeEntry EntryFor(GameModes mode)
        {
            if (Entries == null) return null;
            foreach (var entry in Entries)
                if (entry != null && entry.Mode == mode)
                    return entry;
            return null;
        }

        /// <summary>The authored rows for a mode: its own entry's, else the defaults.</summary>
        public List<VesselControlsPanel.FlightControl> RowsFor(GameModes mode)
            => EntryFor(mode)?.Rows ?? DefaultRows;

        /// <summary>Whether the derived ability rows draw for this mode. Default yes.</summary>
        public bool AbilityRowsFor(GameModes mode)
            => EntryFor(mode)?.ShowAbilityRows ?? true;

        /// <summary>Which elements' abilities this mode shows. Null or empty means all four.</summary>
        public List<Element> AbilitiesFor(GameModes mode) => EntryFor(mode)?.Abilities;

        /// <summary>The hull this mode describes, or <see cref="VesselClassType.Any"/> to use the card's.</summary>
        public VesselClassType VesselFor(GameModes mode)
            => EntryFor(mode)?.Vessel ?? VesselClassType.Any;
    }
}
