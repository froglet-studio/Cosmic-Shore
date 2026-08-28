using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for how a microscene is <b>themed</b> - domain distribution, prism-kind accents, scale
    /// moods, and crystal mix - kept separate from the recipe GEOMETRY. Painting is structural, not
    /// confetti: every scheme keys off the plan's substructure metadata (gate #, strand #,
    /// t-along-path) or spatial coordinates (flight-z, angle, side), so colour and kind read as
    /// deliberate features of the construction - alternating gates, a gradient running up a helix,
    /// danger on the tips, an armoured frame. Weights are balanced so most scenes carry SOME
    /// structural colour/kind play while pure-mono / all-plain scenes still occur as breathing room.
    /// Authored on the conveyor toy definition (config separation); a null palette falls back to
    /// <see cref="Default"/>.
    /// </summary>
    [System.Serializable]
    public sealed class MicroscenePalette
    {
        [Header("Domain scheme weights (structural colour)")]
        [Tooltip("Whole scene one domain - the coherent breathing-room look.")]
        public float MonoWeight = 2.5f;
        [Tooltip("Each substructure its own domain - alternating gates, per-strand ribbons (rainbow runs).")]
        public float BandedWeight = 2.5f;
        [Tooltip("Domain bands along the flight axis - the scene shifts colour as you fly through it.")]
        public float GradientWeight = 1.8f;
        [Tooltip("Mostly one domain with sparse accents of another.")]
        public float AccentWeight = 1.5f;
        [Tooltip("Domain by angle around the flight axis - pinwheel sectors (great on hoops/turbines/rosettes).")]
        public float RadialWeight = 1.5f;
        [Tooltip("Domains alternating point-by-point along each substructure - candy-stripe hoops and ribbons.")]
        public float StripeWeight = 1.3f;
        [Tooltip("Port/starboard split - one domain each side of the flight line.")]
        public float MirrorWeight = 1.2f;
        [Tooltip("Mostly one domain with occasional neutral Blue veins.")]
        public float NeutralVeinWeight = 1.1f;
        [Range(0f, 1f), Tooltip("Accented scheme: per-prism chance to take the accent domain.")]
        public float AccentChance = 0.22f;
        [Range(0f, 1f), Tooltip("Neutral-veined scheme: per-prism chance to be neutral Blue.")]
        public float BlueVeinChance = 0.14f;

        [Header("Prism-kind scheme weights (danger/shield as palette tools)")]
        public float AllPlainWeight = 5f;
        [Tooltip("A few danger prisms sprinkled - loose risk/reward (Squirrel danger-skim boost).")]
        public float DangerAccentWeight = 2f;
        [Tooltip("One whole substructure hot - a gate of fire to thread or skim deliberately.")]
        public float DangerStructureWeight = 1.7f;
        [Tooltip("Danger on the far tips of multi-point structures - hot spire/arm/blade ends.")]
        public float DangerTipsWeight = 1.7f;
        [Tooltip("A few tougher shielded accents (always-on convex MeshCollider - kept rare).")]
        public float ShieldAccentWeight = 1.2f;
        [Tooltip("Shielded ribs along one substructure - an armoured frame (capped by Max Shielded).")]
        public float ShieldFrameWeight = 1.2f;
        [Tooltip("A supershielded keystone guarding the crystal, with a shielded entourage (very rare).")]
        public float LandmarkWeight = 0.9f;
        [Min(0), Tooltip("Cap on danger prisms per scene (box collider - no extra collider cost).")]
        public int MaxDanger = 16;
        [Min(0), Tooltip("Collider-budget cap on shielded prisms per scene.")]
        public int MaxShielded = 3;
        [Min(0), Tooltip("Collider-budget cap on supershielded prisms per scene.")]
        public int MaxSuperShielded = 1;

        [Header("Scale moods (grand / delicate / elongated / tapered scenes)")]
        [Range(0f, 1f), Tooltip("Chance a scene rolls a uniform size mood (grand vs. delicate).")]
        public float ScaleMoodChance = 0.6f;
        [Min(0.1f)] public float ScaleMoodMin = 0.6f;
        [Min(0.1f)] public float ScaleMoodMax = 1.8f;
        [Range(0f, 1f), Tooltip("Chance a scene stretches/squashes every prism along its own longest " +
                                "axis - wiry elongated scenes vs. chunky squat ones.")]
        public float StretchMoodChance = 0.5f;
        [Min(0.1f)] public float StretchMin = 0.65f;
        [Min(0.1f)] public float StretchMax = 1.9f;
        [Range(0f, 1f), Tooltip("Chance substructures taper along their own path - trunks thick at the " +
                                "root thinning to the tip (<1) or flaring outward (>1).")]
        public float TaperChance = 0.45f;
        [Min(0.05f)] public float TaperTipMin = 0.5f;
        [Min(0.05f)] public float TaperTipMax = 1.35f;

        [Header("Crystal mix")]
        [Range(0f, 1f), Tooltip("Per crystal point, chance it's the omni jackpot instead of an elemental skim.")]
        public float OmniCrystalChance = 0.16f;

        /// <summary>The playable domains a scene may colour from - set live by the conveyor each draw
        /// (never authored/snapshotted). The conveyor feeds the full playable triad: belt prisms are
        /// environment mass, not player property, so the palette is never limited to the domains
        /// present in the session.</summary>
        [System.NonSerialized] public Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        public static readonly MicroscenePalette Default = new();
    }
}
