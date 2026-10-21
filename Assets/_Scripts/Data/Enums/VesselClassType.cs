
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
        Angler = 17,
        // 18 is left for the Goby (cece/sharp-tesla-j0kd7z), which must move off 16
        Wrasse = 19,   // a cleaner wrasse and its host: the Sparrow hull with the Kite & Tail navigator seat (two players, one hull): Controller/Vessel/Crew/KITE_NAVIGATOR.md
        // 20 is the Orca's and 21 is claimed twice in flight (the Honeyguide and the Starlings), so both are left free here
        Marlin = 22,   // Marlin and Jacks: the Rhino hull and two thumb-run jacks that herd real fauna: Controller/Vessel/R_VesselActions/MARLIN_JACKS.md
    }
}
