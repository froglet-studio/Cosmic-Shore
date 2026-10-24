
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
        Goby = 18,   // the Goby & Pistol crew vessel (two subvessels, one hull class): .claude/skills/duo-vessel; moved off 16 (BrittleStar)
        Wrasse = 19,   // a cleaner wrasse and its host: the Sparrow hull with the Kite & Tail navigator seat (two players, one hull): Controller/Vessel/Crew/KITE_NAVIGATOR.md
        Orca = 20,     // the 4-seat vessel: one helm and three gunner seats on one hull, every empty seat an AI: Controller/Vessel/Crew/ORCA_CREW.md
        Starling = 21,    // the passive swarm card vessel: the swarm roams on its own, the pilot turns the view and picks cards: Controller/Vessel/R_VesselActions/STARLING.md
        Jacks = 22,    // the Jacks: two jacks that herd real fauna, one player: thumbs run them on the board, or fly them as the hull (two-thumb flight). Was "Marlin and Jacks"; the name Marlin is kept for a swordsman: Controller/Vessel/R_VesselActions/JACKS.md
        Honeyguide = 23,   // the Honeyguide and Ratel: two seats on one hull (its own model on the Sparrow's airframe) with the Ratel's Dynamo seat: Controller/Vessel/Crew/HONEYGUIDE.md
    }
}
