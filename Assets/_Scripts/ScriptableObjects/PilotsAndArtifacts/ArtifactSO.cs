using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// An artifact: a genuine upgrade found in the HyperSea, carried optionally and at risk
    /// (Docs/PILOTS_AND_ARTIFACTS.md §3).
    ///
    /// <para><b>The fifth knob.</b> The element system exposes four parameters per vessel, one per
    /// ability. An artifact reaches one more, ideally a parameter no element on that vessel scales;
    /// one that touches a knob the vessel's element map already owns is a duplicate and should not be
    /// authored. Artifacts stay quantitative so they can never hand out what a pilot does; a
    /// qualitative artifact needs an explicit decision.</para>
    ///
    /// <para><b>Boundary (§3.3, proposed).</b> It changes the carrying vessel's own parameter, is fixed
    /// for the match, and never buffs or debuffs another vessel, moves petals, or reaches the
    /// environment.</para>
    ///
    /// <para>Which parameter it changes, and by how much, binds in the match-start slice of the
    /// milestone; slice 1 is identity, compatibility and the at-risk flag the squad screen shows.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Artifact", menuName = "ScriptableObjects/Pilots/Artifact")]
    public class ArtifactSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id saved in squad records. Never rename once players have saved it; " +
                 "lowercase with dots, e.g. artifact.sparrow.afterburner-acceleration.")]
        [SerializeField] string id;

        [Tooltip("Name shown on the squad screen. Printable ASCII only: the UI font has no other glyphs.")]
        [SerializeField] string displayName;

        [Tooltip("The one parameter it changes and in which direction, in plain words.")]
        [TextArea(2, 5)]
        [SerializeField] string description;

        [Tooltip("Where it was found and what it was; the reason it is worth the risk.")]
        [TextArea(1, 4)]
        [SerializeField] string lore;

        [SerializeField] Sprite icon;

        [Header("Compatibility")]
        [Tooltip("Which hulls can carry it. Bind a vessel class when the parameter is one hull's own; " +
                 "name keywords when several hulls share it.")]
        [SerializeField] VesselRequirement requirement = new();

        [Header("Risk")]
        [Tooltip("Carrying it into the HyperSea risks losing it. The squad screen flags it; the loss " +
                 "itself is milestone 2.")]
        [SerializeField] bool atRisk = true;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public string Lore => lore;
        public Sprite Icon => icon;
        public VesselRequirement Requirement => requirement;
        public bool AtRisk => atRisk;
    }
}
