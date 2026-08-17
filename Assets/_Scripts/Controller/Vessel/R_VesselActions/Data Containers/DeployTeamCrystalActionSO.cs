using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Config for the Dolphin's crystal seeding. The ability is <b>PASSIVE</b>: it takes no input
    /// at all. A cooldown runs continuously, and every time it completes the Dolphin seeds a TEAM
    /// crystal at a random point in the containing cell's <b>cytoplasm</b> — the shell between the
    /// nucleus and the membrane — and the cooldown immediately restarts.
    ///
    /// What gets planted is a TEAM crystal — only the pilot's domain can collect it, the same rule
    /// Skim Race's track crystals run on.
    ///
    /// Element → parameter: CHARGE owns this ability. Its multiplier divides the recharge, and its
    /// level-5 upgrade raises how many crystals each cycle plants to <see cref="UpgradedSeedsPerCycle"/>.
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
        [Tooltip("Seconds between seedings at the RESTING charge level.")]
        [SerializeField] private float cooldown = 30f;
        [Tooltip("Absolute floor on the recharge in seconds, so the ability can never become free.")]
        [SerializeField, Min(0.1f)] private float minCooldown = 4f;
        [Tooltip("Crystals seeded per cycle once Charge's level-5 upgrade is active. Below the " +
                 "upgrade the yield is always 1.")]
        [SerializeField, Min(1)] private int upgradedSeedsPerCycle = 2;

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

        [Header("Elemental (Charge)")]
        [Tooltip("CHARGE -> cooldown: multiplier on Cooldown at Charge level 10 (1 at the resting " +
                 "level, extrapolating into the deficit band so debuffed Charge LENGTHENS the " +
                 "recharge). Authored HERE rather than through the map's generic multiplier, which " +
                 "ChargeBoostActionExecutor already consumes for the boost peak - reading both " +
                 "would double-dip one element into two parameters.")]
        [SerializeField] private float cooldownMultiplierAtFullCharge = 0.5f;
        [Tooltip("Floor for the Charge cooldown multiplier so overcharge can never zero the recharge.")]
        [SerializeField] private float minCooldownMultiplier = 0.35f;

        public float Cooldown => cooldown;
        public float MinCooldown => minCooldown;
        public int UpgradedSeedsPerCycle => upgradedSeedsPerCycle;
        public float BandInnerFraction => Mathf.Min(bandInnerFraction, bandOuterFraction);
        public float BandOuterFraction => Mathf.Max(bandInnerFraction, bandOuterFraction);
        public float CellessSeedRadius => cellessSeedRadius;
        public int MaxLiveSeeded => maxLiveSeeded;
        public float CooldownMultiplierAtFullCharge => cooldownMultiplierAtFullCharge;
        public float MinCooldownMultiplier => Mathf.Max(0.01f, minCooldownMultiplier);

        // Passive: the ability is bound to no input, so neither hook ever fires. They stay
        // overridden (and empty) rather than absent so the asset remains a legal ShipActionSO and
        // can still be dropped into a binding list for debugging without doing anything surprising.
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    }
}
