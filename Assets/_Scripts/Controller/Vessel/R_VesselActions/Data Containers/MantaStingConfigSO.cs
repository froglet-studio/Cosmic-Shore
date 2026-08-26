using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for the Manta's STING — the buttonless bomb weapon (skim to charge, graze to
    /// plant, crystal to cash in; see MANTA_STING_KABLOOM.md). One shared asset wired
    /// DIRECTLY on <see cref="MantaStingActionExecutor"/>: Sting is bound to no input event,
    /// so the lazy <c>CollectBoundActions</c> sweep can never resolve an action SO for it —
    /// the config rides a serialized field on the executor, the Dolphin crystal-seeding shape.
    ///
    /// CHARGE owns the bomb bay quantitatively (capacity + skim-charge rate — the 07/2026 kit
    /// resolution of the class doc's "debuff duration" conflict), SPACE owns every bloom's
    /// radius, and the two level-5 gates (Contagion on Charge, No Friendly Fire on Space) are
    /// read per use through <c>IsUpgradeActive</c>, never a raw level.
    /// </summary>
    [CreateAssetMenu(fileName = "MantaStingConfig",
        menuName = "ScriptableObjects/Vessel Actions/Manta Sting Config")]
    public class MantaStingConfigSO : ScriptableObject
    {
        [Header("Bomb bay (CHARGE — capacity & charge rate)")]
        [Tooltip("Bombs the bay holds at the RESTING Charge level. The spec's 3-charge base " +
                 "(02/2026) with the 5-bomb ceiling (07/2026) reached through Charge overcharge.")]
        [SerializeField, Min(1)] int baseCapacity = 3;

        [Tooltip("Extra capacity per integer Charge level (rounded). At +0.2/level the bay " +
                 "reaches 5 at Charge 15 and bottoms out at the floor below.")]
        [SerializeField] float capacityPerChargeLevel = 0.2f;

        [SerializeField, Min(1)] int minCapacity = 1;
        [SerializeField, Min(1)] int maxCapacity = 5;

        [Tooltip("Bomb charge gained per unique prism/lifeform/vessel skim tick, in BOMBS. " +
                 "0.34 arms roughly one bomb per three skims at resting Charge.")]
        [SerializeField, Min(0.01f)] float chargePerSkim = 0.34f;

        [Tooltip("CHARGE → skim-charge rate: multiplier on chargePerSkim at Charge level 10 " +
                 "(1x at the resting level; the deficit band starves the bay).")]
        [SerializeField, Min(1f)] float chargeRateAtFullCharge = 2f;

        [Tooltip("Floor for the Charge rate multiplier so a deficit can never zero the bay.")]
        [SerializeField, Range(0.05f, 1f)] float minChargeRateMultiplier = 0.25f;

        [Tooltip("Seconds between charge ticks paid by the SAME prism — the skimmer re-enters " +
                 "a prism it is sliding along, and one graze must not milk one prism dry.")]
        [SerializeField, Min(0f)] float perPrismChargeCooldown = 1.5f;

        [Header("Planting")]
        [Tooltip("Fuse on a planted bomb, seconds. The 07/2026 spec band is 20-30; Bloomrush " +
                 "overrides this per intensity through MantaBombRules.FuseSecondsOverride.")]
        [SerializeField, Min(1f)] float fuseSeconds = 25f;

        [Tooltip("How much FASTER than the target the Manta must be moving to plant on a " +
                 "VESSEL (u/s). 0 = any graze plants — the accessibility default; raise it to " +
                 "make vessel planting a true joust.")]
        [SerializeField, Min(0f)] float plantSpeedMargin = 0f;

        [Tooltip("Seconds after planting during which the carrier's prism contacts cannot " +
                 "knock the bomb off — the graze that planted it is usually still touching " +
                 "things.")]
        [SerializeField, Min(0f)] float knockOffGraceSeconds = 1f;

        [Tooltip("Seconds a carrier's own FRESH trail is ignored by the knock-off test, " +
                 "measured from each prism's TimeCreated. A vessel is always touching the " +
                 "ribbon it is laying; scraping a bomb off must take deliberate geometry, not " +
                 "flying straight. Mirrors SelfTrailContactConfig's owner+age shape.")]
        [SerializeField, Min(0f)] float ownFreshTrailGraceSeconds = 6f;

        [Header("Blooms (SPACE — radius; the crystal pays more than the fuse)")]
        [Tooltip("The bloom prefab family. Sphere blast(s); spawned at the carrier's position.")]
        [SerializeField] AOEExplosion[] aoePrefabs;

        [Tooltip("Optional material override for bomb blooms. Empty = the vessel's authored " +
                 "AOE material.")]
        [SerializeField] Material bloomMaterial;

        [Tooltip("Blast MaxScale (sphere diameter) when a bomb's FUSE runs out — the small, " +
                 "consolation bloom. Beating the fuse with a crystal pays kabloomBlastScale.")]
        [SerializeField, Min(1f)] float fuseBlastScale = 70f;

        [Tooltip("Blast MaxScale when a CRYSTAL detonates the bomb (Kabloom) — the medium " +
                 "bloom, deliberately bigger than the fuse's so cashing in always beats " +
                 "timing out.")]
        [SerializeField, Min(1f)] float kabloomBlastScale = 140f;

        [Tooltip("The Kabloom's own extra DOMAINED blast at the Manta's position, MaxScale.")]
        [SerializeField, Min(0f)] float kabloomSelfBlastScale = 140f;

        [Tooltip("SPACE → bloom radius: multiplier on every bomb blast scale at Space level " +
                 "10 (1x at the resting level).")]
        [SerializeField, Min(1f)] float blastScaleAtFullSpace = 1.6f;

        [Tooltip("Floor for the Space radius multiplier so a deficit can never collapse a " +
                 "bloom to nothing.")]
        [SerializeField, Range(0.05f, 1f)] float minBlastScaleMultiplier = 0.5f;

        [Header("Contagion (CHARGE level 5)")]
        [Tooltip("With the Charge upgrade, anything caught in a bomb's detonation is itself " +
                 "bombed, free. This is the radius FRACTION of the blast scale used for the " +
                 "catch test (a blast's reach is MaxScale/2; 1 = the full blast sphere).")]
        [SerializeField, Range(0.1f, 1.5f)] float contagionRadiusFraction = 1f;

        public int BaseCapacity => baseCapacity;
        public float CapacityPerChargeLevel => capacityPerChargeLevel;
        public int MinCapacity => minCapacity;
        public int MaxCapacity => maxCapacity;
        public float ChargePerSkim => chargePerSkim;
        public float ChargeRateAtFullCharge => chargeRateAtFullCharge;
        public float MinChargeRateMultiplier => minChargeRateMultiplier;
        public float PerPrismChargeCooldown => perPrismChargeCooldown;
        public float FuseSeconds => fuseSeconds;
        public float PlantSpeedMargin => plantSpeedMargin;
        public float KnockOffGraceSeconds => knockOffGraceSeconds;
        public float OwnFreshTrailGraceSeconds => ownFreshTrailGraceSeconds;
        public AOEExplosion[] AoePrefabs => aoePrefabs;
        public Material BloomMaterial => bloomMaterial;
        public float FuseBlastScale => fuseBlastScale;
        public float KabloomBlastScale => kabloomBlastScale;
        public float KabloomSelfBlastScale => kabloomSelfBlastScale;
        public float BlastScaleAtFullSpace => blastScaleAtFullSpace;
        public float MinBlastScaleMultiplier => minBlastScaleMultiplier;
        public float ContagionRadiusFraction => contagionRadiusFraction;

        /// <summary>Bomb capacity at an integer Charge level ([-5, 15]).</summary>
        public int CapacityForChargeLevel(int chargeLevel) =>
            Mathf.Clamp(Mathf.RoundToInt(baseCapacity + chargeLevel * capacityPerChargeLevel),
                        minCapacity, maxCapacity);

        /// <summary>The skim-charge gained by one paid skim tick, Charge-scaled, in bombs.</summary>
        public float ChargePerSkimFor(IVesselStatus status) =>
            chargePerSkim * ElementalScaling.Multiplier(status, Element.Charge,
                chargeRateAtFullCharge, minChargeRateMultiplier);

        /// <summary>SPACE → the live bloom-scale multiplier for this vessel.</summary>
        public float BlastScaleMultiplierFor(IVesselStatus status) =>
            ElementalScaling.Multiplier(status, Element.Space,
                blastScaleAtFullSpace, minBlastScaleMultiplier);
    }

    /// <summary>
    /// Mode-level dials for the Manta bomb system. A minigame that wants a different fuse
    /// (Bloomrush's per-intensity 30/25/20/20 ladder) sets the override at round start and
    /// clears it on teardown; the executor reads it at PLANT time so live fuses keep the
    /// length they were planted with. Static by design — the same shape as the AI's mode
    /// hooks — with the mandatory domain-reload reset.
    /// </summary>
    public static class MantaBombRules
    {
        public static float? FuseSecondsOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => FuseSecondsOverride = null;
    }
}
