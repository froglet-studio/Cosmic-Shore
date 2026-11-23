using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number behind the Scarab's NUCLEUS SEEDING ability (design: R_VesselActions/SCARAB.md
    /// §4.6). One asset at <c>Resources/ScarabNucleusFieldConfig</c>, read by
    /// <see cref="ScarabNucleusSeeder"/> and <see cref="ScarabNucleusField"/> — the ability is a
    /// platform behaviour of the vessel, so its tuning lives in ONE place rather than on a mode.
    /// </summary>
    [CreateAssetMenu(fileName = "ScarabNucleusFieldConfig",
                     menuName = "ScriptableObjects/Vessel/Scarab Nucleus Field Config")]
    public class ScarabNucleusFieldConfigSO : ScriptableObject
    {
        [Header("Seeding")]
        [Tooltip("Seconds between one Scarab planting one ball in the nucleus. Passive — no input, " +
                 "no meter, the Dolphin crystal-seeding shape.")]
        [SerializeField, Min(0.5f)] float seedIntervalSeconds = 14f;
        public float SeedIntervalSeconds => seedIntervalSeconds;

        [Tooltip("Most embedded (not yet knocked loose) balls one DOMAIN may have studding the " +
                 "nucleus at once. The clock PAUSES at the cap rather than culling anything — not " +
                 "creating mass is allowed, aging it out is not.")]
        [SerializeField, Min(1)] int maxEmbeddedPerDomain = 3;
        public int MaxEmbeddedPerDomain => maxEmbeddedPerDomain;

        [Tooltip("How far the ball sinks into the nucleus surface, as a fraction of its own radius. " +
                 "0 = sitting on the surface, 1 = fully swallowed. ~0.4 reads as embedded.")]
        [SerializeField, Range(0f, 1f)] float embedSinkFraction = 0.4f;
        public float EmbedSinkFraction => embedSinkFraction;

        [Tooltip("The ball prefab the seeder mints. Leave EMPTY to disable seeding entirely — an " +
                 "unwired slot is a visible TODO, never a borrowed prefab.")]
        [SerializeField] AstroLeagueBall ballPrefab;
        public AstroLeagueBall BallPrefab => ballPrefab;

        [Header("Nucleus overload")]
        [Tooltip("How many of ONE SCARAB's balls (counted by domain) may be knocked INTO the nucleus " +
                 "before the next one overloads it. One more of that domain's balls detonates " +
                 "instead of entering, so this many can be banked safely per Scarab.")]
        [SerializeField, Min(1)] int nucleusEntryLimit = 5;
        public int NucleusEntryLimit => nucleusEntryLimit;

        [Tooltip("Explosion radius of the overload detonation, as a multiple of each ball's own " +
                 "radius.")]
        [SerializeField, Min(0.1f)] float detonationRadiusScale = 6f;
        public float DetonationRadiusScale => detonationRadiusScale;

        [Tooltip("ON: the overload detonates EVERY live ball (the authored spectacle). OFF: only the " +
                 "balls banked inside the nucleus. A dial for playtest, not a design fork.")]
        [SerializeField] bool detonateAllLiveBalls = true;
        public bool DetonateAllLiveBalls => detonateAllLiveBalls;

        // NO CYTOPLASM FIELD. A ball knocked outward is held inside the membrane and bounced off the
        // nucleus from outside by the BALL itself (AstroLeagueBall.ResolveNucleusBoundary), which
        // applies in every cell a ball can reach rather than only to one this ability released — so
        // that dial lives with its siblings on AstroLeagueSettingsSO (`cytoplasmOuterFraction`,
        // beside outsideNucleusDragMultiplier/Falloff). Do not re-add it here: two assets holding
        // one number is two numbers.
    }
}
