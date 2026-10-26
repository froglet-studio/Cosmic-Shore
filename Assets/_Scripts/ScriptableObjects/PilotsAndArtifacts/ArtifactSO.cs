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
    ///
    /// <para><b>2026-10-26 (Shombith, Docs/Pilots/PILOTS_AND_ARTIFACTS.md): three ranks, three slots.</b> An artifact
    /// is found at rank I, II or III; <see cref="Stat"/> names the number it moves and <see cref="Apply"/> moves the
    /// stat's BASE value by the rank's value (the elements still scale on top in the match). A vessel has
    /// <see cref="PilotArtifactCatalogSO.SlotsPerVessel"/> slots. A hull artifact binds its vessel class; a fleet
    /// artifact leaves the class on Any.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Artifact", menuName = "ScriptableObjects/Pilots/Artifact")]
    public class ArtifactSO : ScriptableObject
    {
        public const int RankCount = 3;

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

        [Header("Value (2026-10-26)")]
        [Tooltip("The number this artifact moves: the key vessel code reads its value by.")]
        [SerializeField] ArtifactStat stat;
        [Tooltip("What each rank reads as on screen (\"+1\", \"+10%\", \"5 pellets\"). Printable ASCII.")]
        [SerializeField] string[] rankLabels = new string[RankCount];
        [Tooltip("Each rank's value, applied to the stat's base value by Kind.")]
        [SerializeField] float[] rankValues = new float[RankCount];
        [SerializeField] ArtifactValueKind kind = ArtifactValueKind.Add;

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
        public ArtifactStat Stat => stat;
        public ArtifactValueKind Kind => kind;

        /// <summary>True for a fleet artifact: any hull may carry it (its requirement names no class).</summary>
        public bool IsFleet => requirement == null || requirement.Vessel == VesselClassType.Any;

        /// <summary>Rank 1 to 3; 0 (not found) and anything else reads empty.</summary>
        public string RankLabel(int rank) =>
            rank >= 1 && rank <= RankCount && rankLabels != null && rank <= rankLabels.Length ? rankLabels[rank - 1] : string.Empty;

        public float RankValue(int rank) =>
            rank >= 1 && rank <= RankCount && rankValues != null && rank <= rankValues.Length ? rankValues[rank - 1] : 0f;

        /// <summary>The stat's value with this artifact at <paramref name="rank"/>; rank 0 (not found) leaves the base.</summary>
        public float Apply(float baseValue, int rank)
        {
            if (rank < 1 || rank > RankCount) return baseValue;
            float v = RankValue(rank);
            return kind switch
            {
                ArtifactValueKind.Add => baseValue + v,
                ArtifactValueKind.Percent => baseValue * (1f + v / 100f),
                ArtifactValueKind.Multiply => baseValue * v,
                ArtifactValueKind.Set => v,
                _ => baseValue,
            };
        }
    }
}
