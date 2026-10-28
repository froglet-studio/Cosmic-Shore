using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// A pilot: a pair of eyes (Docs/Pilots/CALLSIGNS.md). Each pilot carries ONE callsign, <see cref="Vision"/>, and
    /// holding LB on a hull it flies shows that one kind of thing while the rest of the world is suppressed
    /// (<c>PilotSight</c>). A pilot touches nothing but photons: it changes no ability, no number and no outcome. The
    /// kit is the vessel's, how strongly it works is the elements', and what everyone fights over is the artifacts'.
    ///
    /// <para><b>2026-10-27 (Garrett): built on Shombith's roster, with what conflicted thrown out.</b> The roster's
    /// names, portraits and two-per-vessel seats stay; the ability each pilot rewrote, its tuning asset (the Tomahawk
    /// throw), the elemental boost and the default "base kit" pilot are gone, because a pilot is no longer part of the
    /// kit. An empty seat is a pilot with no sight. Authored from <c>Docs/Pilots/pilots_and_artifacts.json</c> by
    /// <c>Tools/Build/author_pilot_assets.py</c>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Pilot", menuName = "ScriptableObjects/Pilots/Pilot")]
    public class PilotSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id saved in squad records. Never rename once players have saved it; " +
                 "lowercase with dots, e.g. pilot.rhino.tomahawk.")]
        [SerializeField] string id;

        [Tooltip("Name shown on the squad screen. Printable ASCII only: the UI font has no other glyphs.")]
        [SerializeField] string displayName;

        [Tooltip("Who the pilot is, in one or two plain sentences. Empty shows nothing.")]
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
        [Tooltip("Which hulls this pilot can fly. The roster seats two pilots per vessel class.")]
        [SerializeField] VesselRequirement requirement = new();

        [Header("Sight")]
        [Tooltip("The callsign this pilot carries: what holding LB lights (Docs/Pilots/CALLSIGNS.md §2). One per pilot.")]
        [SerializeField] PilotVision vision = PilotVision.None;

        [Header("AI")]
        [Tooltip("How an AI flying this pilot leans on its sight (CALLSIGNS.md §4.4). An AI reads the world directly; " +
                 "this is doctrine, never a buff.")]
        [TextArea(2, 5)]
        [SerializeField] string aiBehaviorDescription;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public string Flavor => flavor;
        public Sprite Portrait => portrait;
        public bool PortraitIsStandIn => portraitIsStandIn;
        public Sprite Icon => icon;
        public VesselRequirement Requirement => requirement;
        public PilotVision Vision => vision;
        public string AIBehaviorDescription => aiBehaviorDescription;
    }
}
