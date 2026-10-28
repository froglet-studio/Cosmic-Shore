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
    /// track does is authored here (<see cref="ArtifactTrack"/>); the levels are the player's, raised by spending
    /// crystals (<c>PilotLoadoutStore</c>, device-local until the profile carries them).</para>
    ///
    /// <para><b>2026-10-27 (Garrett):</b> replaces the 2026-10-26 "one stat in three ranks" artifact on Shombith's
    /// branch, whose 45 vessel-bound artifacts were thrown out. The id is still what squad records save.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Artifact", menuName = "ScriptableObjects/Pilots/Artifact")]
    public class ArtifactSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id saved in squad records. Never rename once players have saved it; " +
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
        public static bool Twisted(Element element, in ArtifactLevels levels) => levels.Of(element) >= ArtifactTrack.TwistLevel;
    }

    /// <summary>
    /// One element's track on an artifact (Docs/Pilots/ARTIFACTS.md §4): what the element raises, from its level-0 value
    /// to its level-10 value, what each level costs in that element's crystals, and the twist level 5 adds.
    /// </summary>
    [Serializable]
    public class ArtifactTrack
    {
        public const int MaxLevel = 10;
        public const int TwistLevel = 5;

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

        /// <summary>The value at <paramref name="level"/> (clamped to 0..10).</summary>
        public float Value(int level) => Mathf.Lerp(atZero, atTen, Mathf.Clamp(level, 0, MaxLevel) / (float)MaxLevel);

        /// <summary>Crystals to raise the track from <paramref name="level"/> to the next; 0 at the top.</summary>
        public int CostFrom(int level) => level < 0 || level >= MaxLevel ? 0 : baseCost + costStep * level;
    }

    /// <summary>A player's four track levels on one artifact.</summary>
    public readonly struct ArtifactLevels
    {
        public readonly int Mass;
        public readonly int Space;
        public readonly int Time;
        public readonly int Charge;

        public ArtifactLevels(int mass, int space, int time, int charge)
        {
            Mass = Mathf.Clamp(mass, 0, ArtifactTrack.MaxLevel);
            Space = Mathf.Clamp(space, 0, ArtifactTrack.MaxLevel);
            Time = Mathf.Clamp(time, 0, ArtifactTrack.MaxLevel);
            Charge = Mathf.Clamp(charge, 0, ArtifactTrack.MaxLevel);
        }

        public int Of(Element element) => element switch
        {
            Element.Mass => Mass,
            Element.Space => Space,
            Element.Time => Time,
            Element.Charge => Charge,
            _ => 0,
        };
    }
}
