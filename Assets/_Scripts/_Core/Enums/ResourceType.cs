// Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
// Always assign a static numeric value to your enum types

namespace StarWriter.Core
{
    public enum ResourceType
    {
        Charge = 0,
        Ammunition = 1,
        Boost = 2,
        Level = 3,
        Mass = 4,
        SpaceTime = 5,
    }
}