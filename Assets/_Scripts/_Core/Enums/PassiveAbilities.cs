// Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
// Always assign a static numeric value to your enum types
public enum ShipControlOverrides
{
    TurnSpeed = 0,
    BlockThief = 1,
    BlockScout = 2,
    CloseCam = 3,
    FarCam = 4,
    SecondMode = 5,
    SpeedBasedTurning = 6,
    DensityBasedBlockSize = 7,
}