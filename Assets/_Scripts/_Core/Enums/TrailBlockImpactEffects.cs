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
    ChangeBoost = 6,
    Steal = 7,
    DecrementLevel = 8,
    Attach = 9,
    ChangeAmmo = 10
}