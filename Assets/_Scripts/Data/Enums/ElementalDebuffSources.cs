using System;

namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.

    /// <summary>
    /// WHAT applied an elemental debuff — the source class carried by every negative
    /// <c>ResourceSystem.ApplyElementalEffect</c> call, and the scope a debuff-immunity grant is
    /// held against.
    ///
    /// <para>Elementals are the platform's single buff/debuff system, so "immune to elemental
    /// debuffs" stayed ONE gate on the negative branch of <c>ApplyElementalEffect</c>. What this
    /// adds is that the gate can now be asked a narrower question than "immune to everything?":
    /// an ability that wards a pilot against DANGER PRISMS is a different promise from one that
    /// wards them against an opposing pilot's weapon, and a single bool could not tell the two
    /// apart. The Dolphin's Time-5 "Drift Ward" is the case that forced it — as an unscoped grant
    /// it also cancelled the Dolphin crystal blast, which is the entire scoring event of The
    /// Bends, a mode in which every pilot is a Dolphin.</para>
    ///
    /// <para><b>A debuff names exactly one class; a grant holds a MASK.</b> The debuff is blocked
    /// iff the two overlap. A call that names no class lands in <see cref="Other"/>, so it is
    /// blocked only by a grant that covers everything — which is why adding a source class here
    /// can never silently widen an existing narrow ward.</para>
    ///
    /// <para><see cref="All"/> is <c>~0</c> rather than the OR of the members below, deliberately:
    /// it is serialized on prefabs, and an "everything" grant authored today must keep covering a
    /// class added tomorrow.</para>
    /// </summary>
    [Flags]
    public enum ElementalDebuffSources
    {
        /// <summary>No source — an empty grant wards nothing.</summary>
        None = 0,

        /// <summary>Ramming a dangerous prism (<c>VesselElementalDebuffByDangerPrismEffectSO</c>).
        /// Environment/terrain punishment: friendly-fire by design, your own trail included.</summary>
        DangerPrism = 1,

        /// <summary>Being caught in an area blast
        /// (<c>VesselElementalDebuffByExplosionEffectSO</c>) — the Dolphin's crystal cone, and any
        /// future blast that strips levels rather than hull. A weapon another pilot aimed at
        /// you.</summary>
        Explosion = 2,

        /// <summary>A vessel-on-vessel contact debuff — today the joust overtake
        /// (<c>VesselOvertakeBySkimmerEffectSO</c>, which buffs allies and debuffs
        /// opponents).</summary>
        VesselContact = 4,

        /// <summary>The default bucket for a debuff that names no class. Blocked only by a grant
        /// that covers everything.</summary>
        Other = 8,

        /// <summary>Every source, including classes added after a grant was authored.</summary>
        All = ~0,
    }
}
