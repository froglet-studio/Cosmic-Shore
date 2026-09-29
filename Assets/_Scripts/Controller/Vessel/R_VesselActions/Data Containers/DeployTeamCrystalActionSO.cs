using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Config for the Dolphin's crystal seeding. The ability is <b>PASSIVE</b>: it takes no input
    /// at all. A cooldown runs continuously, and every time it completes the Dolphin seeds a crystal
    /// at a random point in the containing cell's <b>cytoplasm</b> — the shell between the nucleus
    /// and the membrane — and the cooldown immediately restarts.
    ///
    /// Element → parameter: <b>MASS</b> owns this ability (2026-08-17). Its multiplier divides the
    /// recharge, and its level-5 upgrade changes WHAT gets planted: an un-upgraded Dolphin seeds an
    /// ordinary <b>omni</b> crystal that anyone can fly into, and Mass 5 promotes the seed to a
    /// <b>team</b> crystal only the pilot's own domain can collect. That is the ability's whole risk
    /// curve — until the upgrade lands, your ammunition is standing in open space for a rival to
    /// take, and the seeding rate is as much a liability as a supply.
    ///
    /// <para>The gate is structural rather than conventional: <c>TeamCrystal.prefab</c> drops the
    /// base <see cref="OmniCrystalImpactor"/> in favour of a <see cref="TeamCrystalImpactor"/>,
    /// whose <c>IsDomainMatching</c> rejects every vessel outside the crystal's domain in the impact
    /// chain itself — so the upgrade is a prefab swap, not a rule the seeding has to remember to
    /// enforce.</para>
    ///
    /// <para><b>This is not the omni-crystal respawn volume.</b> `CrystalManager.GetAnchorlessSpawnRadius`
    /// is LOCKED to the nucleus (CLAUDE.md ▸ Rampage §27.3) because the nucleus is the visible marker
    /// of "the middle" that every mode teaches players to contest. That rule governs the CELL's own
    /// respawning crystal. This is a vessel ABILITY planting its own team-locked crystal, and it
    /// deliberately seeds OUTSIDE the nucleus: the nucleus interior is the territorial claim, so
    /// dropping ability crystals into it would make the sanctuary the place to farm.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "DeployTeamCrystalAction", menuName = "ScriptableObjects/Vessel Actions/Deploy Team Crystal")]
    public class DeployTeamCrystalActionSO : ShipActionSO
    {
        [Header("Cooldown")]
        [Tooltip("Seconds between seedings at the RESTING mass level.")]
        [SerializeField] private float cooldown = 30f;
        [Tooltip("Absolute floor on the recharge in seconds, so the ability can never become free.")]
        [SerializeField, Min(0.1f)] private float minCooldown = 4f;

        [Header("Placement — the cytoplasm")]
        [Tooltip("Inner edge of the seeding band, as a fraction of the way from the NUCLEUS surface " +
                 "to the membrane. 0 = right at the nucleus. The band is clamped outside the nucleus " +
                 "in code regardless, because nucleus mass is the cell's territorial claim.")]
        [SerializeField, Range(0f, 1f)] private float bandInnerFraction = 0.1f;
        [Tooltip("Outer edge of the seeding band, as a fraction of nucleus surface -> membrane. " +
                 "1 = right at the membrane; pulled in a little so crystals do not seed inside the " +
                 "membrane wall itself.")]
        [SerializeField, Range(0f, 1f)] private float bandOuterFraction = 0.9f;
        [Tooltip("Radius used when the Dolphin is in open space with no cell to measure (freestyle " +
                 "transit, tool scenes). Seeds in a ball of this radius around the vessel instead.")]
        [SerializeField, Min(1f)] private float cellessSeedRadius = 600f;

        [Header("Population")]
        [Tooltip("How many of THIS Dolphin's seeded crystals may be alive at once. At the cap the " +
                 "seed clock PAUSES - it never culls a planted crystal. Not creating mass is " +
                 "allowed; aging it out is not (CLAUDE.md - Mass is conserved). 0 = uncapped.")]
        [SerializeField, Min(0)] private int maxLiveSeeded = 8;

        [Header("Elemental (Mass)")]
        [Tooltip("MASS -> cooldown: multiplier on Cooldown at Mass level 10 (1 at the resting " +
                 "level, extrapolating into the deficit band so debuffed Mass LENGTHENS the " +
                 "recharge). Authored HERE rather than through the map's generic multiplier, so the " +
                 "recharge is driven by exactly one number that lives beside the ability it tunes.")]
        [FormerlySerializedAs("cooldownMultiplierAtFullCharge")]
        [SerializeField] private float cooldownMultiplierAtFullMass = 0.5f;
        [Tooltip("Floor for the Mass cooldown multiplier so overcharge can never zero the recharge.")]
        [SerializeField] private float minCooldownMultiplier = 0.35f;

        public float Cooldown => cooldown;
        public float MinCooldown => minCooldown;
        public float BandInnerFraction => Mathf.Min(bandInnerFraction, bandOuterFraction);
        public float BandOuterFraction => Mathf.Max(bandInnerFraction, bandOuterFraction);
        public float CellessSeedRadius => cellessSeedRadius;
        public int MaxLiveSeeded => maxLiveSeeded;
        public float CooldownMultiplierAtFullMass => cooldownMultiplierAtFullMass;
        public float MinCooldownMultiplier => Mathf.Max(0.01f, minCooldownMultiplier);

        // Passive: the ability is bound to no input, so neither hook ever fires. They stay
        // overridden (and empty) rather than absent so the asset remains a legal ShipActionSO and
        // can still be dropped into a binding list for debugging without doing anything surprising.
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    }
}
