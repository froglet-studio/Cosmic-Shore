namespace CosmicShore.Data
{
    /// <summary>
    /// A pilot's CALLSIGN: the one thing in the world they see when LB is held
    /// (Docs/Pilots/CALLSIGNS.md §2). Fixed values, in the fundamentals list's order (the # column
    /// of §2), so Unity's serialization never drifts. The shader reads ENSIGN, MANGER and CITADEL by
    /// number (PrismDestructionSight.hlsl, PILOT_VISION_*), so those three must never move.
    /// </summary>
    public enum PilotVision
    {
        None = 0,
        Ensign = 1,     // Domain: your domain's mass
        Manger = 2,     // Mass: the cluster the fauna are heading for
        Anvil = 3,      // Mass crystals
        Volt = 4,       // Charge crystals
        Zodiac = 5,     // Space crystals
        Sundial = 6,    // Time crystals
        Opal = 7,       // Omni crystals
        Rampart = 8,    // shielded prisms
        Citadel = 9,    // super-shielded prisms
        Hornet = 10,    // danger prisms
        Ivy = 11,       // plants
        Talon = 12,     // predators
        Doe = 13,       // herbivores
        Wingman = 14,   // vessels on your side
        Bandit = 15,    // opposing vessels
        Needle = 16,    // switches: gates, Scarab switches, goals
        Galleon = 17,   // Arks, and the payload aboard
    }
}
