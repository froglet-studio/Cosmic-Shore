using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// An artifact: one PAYLOAD TRANSFORMATION (Docs/Pilots/ARTIFACTS.md §3). The squad slot's artifact loads every
    /// payload that slot's pilot takes from the Ark, and <see cref="Transform"/> decides what the loaded payload does
    /// when RB releases it: how it is delivered, what shape its effect takes, what it does to the mass it reaches.
    ///
    /// <para><b>No vessel class appears in an artifact.</b> A Sparrow and a Butterfly loaded with the same artifact
    /// fire the same thing. <see cref="Requirement"/> stays only for the rare artifact that leans on a shared keyword
    /// (Wake on <c>BasePrismProduction</c>); its vessel is always Any, and the catalog's validation fails otherwise.</para>
    ///
    /// <para><b>Four tracks, one per element</b> (§4): Mass is how much, Space how far, Time how long, Charge how hard.
    /// Each runs 0 to 10 and adds one qualitative twist at 5, as every vessel ability does with its elements. What each
    /// track does is authored here (<see cref="ArtifactTrack"/>).</para>
    ///
    /// <para><b>2026-11-07 (Garrett): this asset is a KIND, not an artifact.</b> Every artifact a player holds is unique
    /// (<see cref="ArtifactRecord"/>, Docs/Pilots/INVENTORY.md §2): rolled from its own seed on top of a kind, attuned to
    /// ONE of the kind's tracks, fed that one element without limit with diminishing returns. Its id is this asset's id
    /// plus '#' and its seed (<see cref="UniqueId"/>), so <see cref="PilotArtifactCatalogSO.TryGetArtifact"/> still finds
    /// the kind from any artifact's id.</para>
    ///
    /// <para><b>2026-10-27 (Garrett):</b> replaces the 2026-10-26 "one stat in three ranks" artifact on Shombith's
    /// branch, whose 45 vessel-bound artifacts were thrown out.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Artifact", menuName = "ScriptableObjects/Pilots/Artifact")]
    public class ArtifactSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable kind id: every unique artifact's id starts with it. Never rename once players hold one; " +
                 "lowercase with dots, e.g. artifact.javelin.")]
        [SerializeField] string id;

        [Tooltip("Name shown on the squad screen. Printable ASCII only: the UI font has no other glyphs.")]
        [SerializeField] string displayName;

        [Tooltip("What a payload loaded with it does, in one plain sentence.")]
        [TextArea(2, 5)]
        [SerializeField] string description;

        [Tooltip("Where it was found and what it was; the reason it is worth the risk.")]
        [TextArea(1, 4)]
        [SerializeField] string lore;

        [SerializeField] Sprite icon;

        [Header("Compatibility")]
        [Tooltip("Leave the vessel on Any: no vessel class appears in an artifact. Name a shared keyword only when the " +
                 "artifact leans on one (Docs/Pilots/ARTIFACTS.md §3).")]
        [SerializeField] VesselRequirement requirement = new();

        [Header("The payload")]
        [Tooltip("What a loaded payload does on release.")]
        [SerializeField] PayloadTransformSO payloadTransform;

        [Tooltip("One track per element: Mass, Space, Time and Charge, each 0 to 10 with a twist at 5.")]
        [SerializeField] ArtifactTrack[] tracks = Array.Empty<ArtifactTrack>();

        [Header("Getting it")]
        [Tooltip("Locked until found: the player holds it only once one has been found on a voyage and docked home " +
                 "(Garrett, 2026-10-28). Off: free, like every other artifact.")]
        [SerializeField] bool foundOnVoyages;

        [Header("Audio (shipped empty: an unwired slot is a visible TODO)")]
        [Tooltip("Plays when a payload is taken loaded with this artifact.")]
        [SerializeField] EventReference loadSound;
        [Tooltip("Plays when a payload loaded with this artifact is released.")]
        [SerializeField] EventReference releaseSound;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public string Lore => lore;
        public Sprite Icon => icon;
        public VesselRequirement Requirement => requirement;
        public PayloadTransformSO Transform => payloadTransform;
        public ArtifactTrack[] Tracks => tracks ?? Array.Empty<ArtifactTrack>();
        public bool FoundOnVoyages => foundOnVoyages;
        public EventReference LoadSound => loadSound;
        public EventReference ReleaseSound => releaseSound;

        /// <summary>The track that <paramref name="element"/> raises, or null.</summary>
        public ArtifactTrack Track(Element element)
        {
            var all = Tracks;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].element == element) return all[i];
            return null;
        }

        /// <summary>The value <paramref name="element"/>'s track gives at the levels in <paramref name="levels"/>; 0
        /// when the artifact has no such track.</summary>
        public float Value(Element element, in ArtifactLevels levels) =>
            Track(element) is { } track ? track.Value(levels.Of(element)) : 0f;

        /// <summary>True once <paramref name="element"/>'s track has reached its twist.</summary>
        public static bool Twisted(Element element, in ArtifactLevels levels) => levels.Twisted(element);

        /// <summary>
        /// This kind as the pure generator reads it (Docs/Pilots/INVENTORY.md §2.1): its id, name and the tracks a
        /// unique artifact of this kind can be attuned to. Each unique artifact (<see cref="ArtifactRecord"/>) is rolled
        /// on top of it and accepts the crystals of ONE of these tracks.
        /// </summary>
        public ArtifactArchetype ToArchetype()
        {
            var kind = new ArtifactArchetype { Id = id, DisplayName = displayName };
            foreach (var t in Tracks)
                if (t != null && ArtifactLevels.IsTrackElement(t.element)) kind.Tracks.Add((t.element, t.label));
            return kind;
        }
    }

    /// <summary>
    /// One element's track on an artifact KIND (Docs/Pilots/ARTIFACTS.md §4): what the element raises, from its level-0
    /// value to its level-10 value, and the twist level 5 adds. Since 2026-11-07 a unique artifact of the kind is attuned
    /// to ONE track and feeds it without limit along <see cref="ArtifactCurve"/>; the other three stay at the levels it
    /// was rolled with (Docs/Pilots/INVENTORY.md §2). <see cref="baseCost"/> and <see cref="costStep"/> priced the old
    /// whole-level raises and are kept only so the authored data still reads.
    /// </summary>
    [Serializable]
    public class ArtifactTrack
    {
        public const int MaxLevel = (int)ArtifactLevels.MaxLevel;
        public const int TwistLevel = (int)ArtifactLevels.TwistLevel;

        [Tooltip("The crystal that raises this track: Mass, Space, Time or Charge.")]
        public Element element;

        [Tooltip("What the track raises, as the player reads it (\"line width\").")]
        public string label;

        [Tooltip("The value at level 0.")]
        public float atZero;

        [Tooltip("The value at level 10. The value rises evenly between.")]
        public float atTen;

        [Tooltip("Crystals of this element the first level costs.")]
        [Min(1)] public int baseCost = 1;

        [Tooltip("Crystals each further level costs on top of the one before.")]
        [Min(0)] public int costStep;

        [Tooltip("What level 5 adds, as the player reads it.")]
        public string twistLine;

        /// <summary>The value at <paramref name="level"/> (clamped to 0..10; levels are continuous since 2026-11-07).</summary>
        public float Value(float level) => Mathf.Lerp(atZero, atTen, Mathf.Clamp(level, 0f, MaxLevel) / MaxLevel);

        /// <summary>LEGACY: what the old whole-level raise from <paramref name="level"/> cost; 0 at the top. Read only by
        /// the one-time import of a device's old track levels (<c>AccountInventoryStore</c>).</summary>
        public int CostFrom(int level) => level < 0 || level >= MaxLevel ? 0 : baseCost + costStep * level;
    }
}
