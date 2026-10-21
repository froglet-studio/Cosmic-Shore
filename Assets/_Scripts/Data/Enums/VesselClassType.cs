
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
        Orca = 20,     // the 4-seat vessel: one helm and three gunner seats on one hull, every empty seat an AI: Controller/Vessel/Crew/ORCA_CREW.md
        Starling = 21,    // the passive swarm card vessel: the swarm roams on its own, the pilot turns the view and picks cards: Controller/Vessel/R_VesselActions/STARLING.md
        Marlin = 22,   // Marlin and Jacks: a marlin on the Rhino's flight rig and two thumb-run jacks that herd real fauna: Controller/Vessel/R_VesselActions/MARLIN_JACKS.md
    }
}
