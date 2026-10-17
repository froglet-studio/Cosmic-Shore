
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
        Butterfly = 13,
        Thresher = 14,
        Stoat = 15,
        BrittleStar = 16,
        // 17 is the Angler (cece/zealous-feynman-po8k8d); 18 is left for the Goby (cece/sharp-tesla-j0kd7z), which must move off 16
        Kite = 19,   // the Sparrow with the Kite & Tail navigator seat (two players, one hull): Controller/Vessel/Crew/KITE_NAVIGATOR.md
    }
}
