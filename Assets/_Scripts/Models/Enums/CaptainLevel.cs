namespace CosmicShore.Integrations.Enums
{
    /// <summary>
    /// Enum that tracks the product ContentTypes of upgrades defined in Playfab.
    /// Using this, we can see the highest upgrade level purchased
    /// The same enum is associated with each locally saved instance of a captain in case the player is playing offline.
    /// </summary>
    public enum CaptainLevel
    {
        // Keep upgrade 0 in case we need it
        Upgrade0 = 0,
        Upgrade1 = 1,
        Upgrade2 = 2,
        Upgrade3 = 3,
        Upgrade4 = 4,
        Upgrade5 = 5
    }
}