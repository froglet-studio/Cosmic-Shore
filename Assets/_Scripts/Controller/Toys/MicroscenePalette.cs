using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for how a microscene is <b>themed</b> - domain distribution, prism-kind accents, scale
    /// mood, and crystal mix - kept separate from the recipe GEOMETRY. The weights are deliberately
    /// lopsided toward coherence: most scenes come out one domain and all-plain (like today), with
    /// richer variety appearing as occasional spice, so the field reads "infinitely fresh" without
    /// becoming multicoloured chaotic confetti. Authored on the conveyor toy definition (config
    /// separation); a null palette falls back to <see cref="Default"/>.
    /// </summary>
    [System.Serializable]
    public sealed class MicroscenePalette
    {
        [Header("Domain scheme weights (Mono dominates → coherent)")]
        [Tooltip("Whole scene one domain - the most common, coherent look.")]
        public float MonoWeight = 6f;
        [Tooltip("Contiguous structure runs, each its own domain.")]
        public float BandedWeight = 2.2f;
        [Tooltip("Mostly one domain with sparse accents of another.")]
        public float AccentWeight = 1.6f;
        [Tooltip("Mostly one domain with occasional neutral Blue veins.")]
        public float NeutralVeinWeight = 1.1f;
        [Range(0f, 1f), Tooltip("Accented scheme: per-prism chance to take the accent domain.")]
        public float AccentChance = 0.22f;
        [Range(0f, 1f), Tooltip("Neutral-veined scheme: per-prism chance to be neutral Blue.")]
        public float BlueVeinChance = 0.14f;

        [Header("Prism-kind scheme weights (AllPlain dominates → mostly plain)")]
        public float AllPlainWeight = 10f;
        [Tooltip("A few danger prisms - risk/reward (Squirrel danger-skim boost).")]
        public float DangerAccentWeight = 2.6f;
        [Tooltip("A few tougher shielded accents (always-on convex MeshCollider - kept rare).")]
        public float ShieldAccentWeight = 1.1f;
        [Tooltip("A rare supershielded landmark (always-on convex MeshCollider - kept very rare).")]
        public float LandmarkWeight = 0.5f;
        [Min(0)] public int MaxDanger = 8;
        [Min(0), Tooltip("Collider-budget cap on shielded prisms per scene.")]
        public int MaxShielded = 3;
        [Min(0), Tooltip("Collider-budget cap on supershielded prisms per scene.")]
        public int MaxSuperShielded = 1;

        [Header("Scale mood (grand vs. delicate scenes)")]
        [Range(0f, 1f)] public float ScaleMoodChance = 0.45f;
        [Min(0.1f)] public float ScaleMoodMin = 0.7f;
        [Min(0.1f)] public float ScaleMoodMax = 1.6f;

        [Header("Crystal mix")]
        [Range(0f, 1f), Tooltip("Per crystal point, chance it's the omni jackpot instead of an elemental skim.")]
        public float OmniCrystalChance = 0.16f;

        /// <summary>The playable domains a scene may colour from - set live by the conveyor each draw
        /// (never authored/snapshotted: the Domain Changer toy can re-pick mid-freestyle).</summary>
        [System.NonSerialized] public Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        public static readonly MicroscenePalette Default = new();
    }
}
