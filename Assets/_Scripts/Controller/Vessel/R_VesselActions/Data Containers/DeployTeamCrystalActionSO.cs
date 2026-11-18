using CosmicShore.Data;
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
        [Tooltip("MASS -> yield: how many crystals one seeding plants and how many may be alive at once, as a " +
                 "multiplier on 1 and on maxLiveSeeded. x1 at rest -> x2 at Mass 10 (floor x0.5), so a heavy Dolphin " +
                 "seeds two crystals a cycle and keeps twice the field. The recharge is fixed: making it come back SOONER " +
                 "is Time's job, not Mass's (2026-11-18, the element-placeholder pass; it was a Mass cooldown before).")]
        [SerializeField] ElementalFloat yieldScale = ElementalFloat.Multiplier(1f, 2f, Element.Mass, 0.5f);

        public float Cooldown => cooldown;
        public float MinCooldown => minCooldown;
        public float BandInnerFraction => Mathf.Min(bandInnerFraction, bandOuterFraction);
        public float BandOuterFraction => Mathf.Max(bandInnerFraction, bandOuterFraction);
        public float CellessSeedRadius => cellessSeedRadius;
        public int MaxLiveSeeded => maxLiveSeeded;
        /// <summary>The Mass yield multiplier at this vessel's live Mass level.</summary>
        public float YieldFor(IVesselStatus status) => Mathf.Max(0f, yieldScale.EvaluateLive(status));
        /// <summary>Crystals one seeding plants (at least one).</summary>
        public int SeedsPerCycleFor(IVesselStatus status) => Mathf.Max(1, Mathf.RoundToInt(YieldFor(status)));
        /// <summary>The live cap on this Dolphin's seeded crystals (0 = uncapped).</summary>
        public int MaxLiveSeededFor(IVesselStatus status) =>
            maxLiveSeeded <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(maxLiveSeeded * YieldFor(status)));

        // Passive: the ability is bound to no input, so neither hook ever fires. They stay
        // overridden (and empty) rather than absent so the asset remains a legal ShipActionSO and
        // can still be dropped into a binding list for debugging without doing anything surprising.
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    }
}
