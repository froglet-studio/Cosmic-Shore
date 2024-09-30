// Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
// Always assign a static numeric value to your enum types
public enum TrailBlockImpactEffects
{
    PlayHaptics = 0,
    DrainHalfAmmo = 1,
    DebuffSpeed = 2,
    DeactivateTrailBlock = 3,
    ActivateTrailBlock = 4,
    OnlyBuffSpeed = 5,
    GainResourceByVolume = 6,
    Steal = 7,
    DecrementLevel = 8,
    Attach = 9,
    GainResource = 10,
    Shield = 11,
    Stop = 12,
    Fire = 13,
    Bounce = 14,
    Explode = 15,
    FX =16,
    FeelDanger = 17,
}

/*
public enum ImpactEffects
{
    FillCharge = 1,
    DrainAmmo = 2,
    GainOneThirdMaxAmmo = 3,
    Score = 4,
    Boost = 5,
    AreaOfEffectExplosion = 6,
    ResetAggression = 7,
    IncrementLevel = 8,
    PlayFakeCrystalHaptics = 9,
    ReduceSpeed = 10,
    PlayHaptics = 11,

    PlayHaptics = 0,
    DrainHalfAmmo = 1,
    DebuffSpeed = 2,
    DeactivateTrailBlock = 3,
    ActivateTrailBlock = 4,
    OnlyBuffSpeed = 5,
    ChangeBoost = 6,
    Steal = 7,
    DecrementLevel = 8,
    Attach = 9,
    ChangeAmmo = 10
}
*/
// scan, sort, filter, reorganize as this list grows