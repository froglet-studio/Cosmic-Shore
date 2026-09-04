
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
        // Player-facing name is "Gibbon" (a brachiating web-slinger). This
        // member's ToString() drives every player-visible surface — the vessel
        // card, the vessel-changer toy label, and telemetry — so it reads
        // "Gibbon". Internal identifiers keep the "Spider" working name
        // (Spider.prefab, SpiderVesselHUD*, PrismType.Spider, the
        // SO_Class_Spider / SpiderCameraSettings asset filenames, and
        // SwingingVesselTransformer) — same code-name/display-name split as
        // Maelstrom/Tournament. 13, not 12: upstream claimed 12 for the Scarab
        // while this vessel lived on a branch; the prefab + SO_Class carry 13.
        Gibbon = 13,
    }
}
