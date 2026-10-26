using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// A pilot: the qualitative half of a squad mate's kit (Docs/PILOTS_AND_ARTIFACTS.md §2).
    ///
    /// <para><b>A vessel always has a pilot.</b> What used to read as a hull's base characteristics is
    /// the kit its DEFAULT pilot gives it, so every vessel class has exactly one pilot with
    /// <see cref="IsDefault"/> set and <see cref="Requirement"/> bound to that class. Any other pilot
    /// swaps part of that kit for a different one: a side-grade, never an upgrade. A candidate that
    /// only adds and gives nothing up is an artifact in disguise.</para>
    ///
    /// <para><b>Balance lives in the elements.</b> A pilot carries no quantitative change of its own.
    /// When one plays weaker than another, give it an <see cref="ElementalBoost"/>, which is extra
    /// starting level and so stays inside the element system.</para>
    ///
    /// <para><b>2026-10-26 (Shombith, Docs/Pilots/PILOTS_AND_ARTIFACTS.md):</b> pilots are not tied to the elements, so
    /// every authored pilot leaves its boost at none; the field stays as the balance lever should one be needed. Each
    /// vessel has two pilots besides its default, each rewriting one ability (<see cref="Ability"/>, described by
    /// <see cref="Description"/>), authored from <c>Docs/Pilots/pilots_and_artifacts.json</c> by
    /// <c>Tools/Build/author_pilot_assets.py</c>.</para>
    ///
    /// <para>The kit swap itself (which action and executor config the pilot replaces) is the next
    /// slice of the milestone and is not on this asset yet; slice 1 is identity, compatibility and
    /// balance. Successor to <c>SO_Captain</c> (§5.1), which this does not reuse: a captain was one
    /// asset per vessel AND element, while a pilot is per vessel and the element is the squad slot's
    /// own choice.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Pilot", menuName = "ScriptableObjects/Pilots/Pilot")]
    public class PilotSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id saved in squad records. Never rename once players have saved it; " +
                 "lowercase with dots, e.g. pilot.manta.default.")]
        [SerializeField] string id;

        [Tooltip("Name shown on the squad screen. Printable ASCII only: the UI font has no other glyphs.")]
        [SerializeField] string displayName;

        [Tooltip("The ability the pilot rewrites, as the player reads it (\"Sword - special move\"). Empty on a default " +
                 "pilot: it IS the base kit.")]
        [SerializeField] string ability;

        [Tooltip("What the pilot changes, in one or two plain sentences. Name what it gives up.")]
        [TextArea(2, 5)]
        [SerializeField] string description;

        [Tooltip("In-world flavour line.")]
        [TextArea(1, 3)]
        [SerializeField] string flavor;

        [SerializeField] Sprite portrait;
        [Tooltip("The portrait is a spare standing in until the artist paints this pilot (Shombith, 2026-10-26: not kept for good).")]
        [SerializeField] bool portraitIsStandIn;
        [SerializeField] Sprite icon;

        [Header("Compatibility")]
        [Tooltip("Which hulls this pilot can fly. A default pilot binds its one vessel class; a " +
                 "generalised pilot leaves the class on Any and names keywords.")]
        [SerializeField] VesselRequirement requirement = new();

        [Tooltip("This pilot is its vessel's base kit. Exactly one per vessel class, bound to that class.")]
        [SerializeField] bool isDefault;

        [Header("Balance")]
        [Tooltip("Extra starting petals in one element: the lever for a pilot whose swap plays " +
                 "weaker than another's. Usually none.")]
        [SerializeField] ElementalBoost elementalBoost;

        [Header("AI")]
        [Tooltip("How an AI squad mate flying this pilot should behave. Carried over from SO_Captain.")]
        [TextArea(2, 5)]
        [SerializeField] string aiBehaviorDescription;

        public string Id => id;
        public string DisplayName => displayName;
        public string Ability => ability;
        public string Description => description;
        public string Flavor => flavor;
        public Sprite Portrait => portrait;
        public bool PortraitIsStandIn => portraitIsStandIn;
        public Sprite Icon => icon;
        public VesselRequirement Requirement => requirement;
        public bool IsDefault => isDefault;
        public ElementalBoost ElementalBoost => elementalBoost;
        public string AIBehaviorDescription => aiBehaviorDescription;

        /// <summary>True when this is the base-kit pilot of <paramref name="vesselClass"/>.</summary>
        public bool IsDefaultFor(VesselClassType vesselClass) =>
            isDefault && requirement != null && requirement.Vessel == vesselClass;
    }
}
