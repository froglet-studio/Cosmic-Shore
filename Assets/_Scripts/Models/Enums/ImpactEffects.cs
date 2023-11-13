// Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
// Always assign a static numeric value to your enum types

/*
 * List of all effects that can be activated on impact when objects collide
 */
public enum ImpactEffects
{
    FillCharge = 1,
    DrainAmmo = 2,
    GainOneThirdMaxAmmo = 3,
    Boost = 5,
    AreaOfEffectExplosion = 6,
    ResetAggression = 7,
    IncrementLevel = 8,
    ReduceSpeed = 10,
    PlayHaptics = 11,

    DrainHalfAmmo = 1,
    DebuffSpeed = 2,
    OnlyBuffSpeed = 5,
    ChangeBoost = 6,
    DecrementLevel = 8,
    ChangeAmmo = 10,
    DeactivateTrailBlock = 3,
    ActivateTrailBlock = 4,
    Steal = 7,
    Attach = 9,
}

/*
{
    HapticEffectProperties
        int HapticId

    
    // FEEDBACK
    PlayHaptics(HapticEffectProperties),
    PlayHaptics,
    ResetAggression,

    // RESOURCES   
    type, quanity, unit
    charge, 100, percent of current

    ResourceEffectProperties
        enum ResourceType
        float Amount
        enum AllocationType

    AdjustResource(ResourceEffectProperties)

    FillCharge,
    FillOneThirdMaxAmmo,
    DrainAmmo,
    DrainHalfAmmo,
    ChargeAmmo,
    ChargeBoost,

    // Buffs
    OnlyBuffSpeed,
    IncrementLevel,
    Boost,    

    // Debuffs
    DebuffSpeed,
    ReduceSpeed,
    DecrementLevel,
    TrailSpawnerCooldown,
    SpinAround,
    Knockback,
    Stun,

    // Blocks
    DeactivateTrailBlock,
    ActivateTrailBlock,
    StealTrailBlock,
    Attach,
    Grow,
    Shrink,
    Shield,

    // Ship
    TriggerAreaEffect,
}
*/