
namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types

    // TODO - Add namespace
    [System.Serializable]
    public enum VesselClassType
    {
        Any = -1,
        Random = 0,
        Manta = 1,
        Dolphin = 2,
        Rhino = 3,
        Urchin = 4,
        Grizzly = 5,
        Squirrel = 6,
        Serpent = 7,
        Termite = 8,
        Falcon = 9,
        Shrike = 10,
        Sparrow = 11,
        Scarab = 12,

        /// <summary>
        /// The two-thumb tether flyer: both triggers fire lateral beams that plant their own
        /// anchor prism and become a swing line. Id 13 is deliberately the SAME id and name the
        /// superseded claude/spider-momentum-physics branch allocated for this vessel concept, so
        /// the two can never collide on a merge and nothing has to be renumbered if anything from
        /// that branch is ever revived.
        /// </summary>
        Gibbon = 13,
    }
}
