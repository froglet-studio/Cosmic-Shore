namespace CosmicShore.UI
{
    /// <summary>
    /// What a menu entry currently does when pressed — the one availability vocabulary the whole
    /// app shell speaks. Read by <see cref="MenuAvailabilityView"/>, which is the one place these
    /// states are turned into pixels and into a response.
    ///
    /// <para><b>Availability is a state, not a missing entry</b> (<c>Docs/HomeHub/ARCHITECTURE.md</c>
    /// §2). An entry that is simply not drawn tells the player the game has three things in it, and
    /// the day it ships they have to re-learn the screen. Both unfinished states therefore stay on
    /// screen; they differ in what they promise.</para>
    ///
    /// <para>The values are deliberately <b>explicit and stable</b>: this enum was lifted out of
    /// <c>MenuHubButton.HubAvailability</c>, whose serialized fields are stored as these integers,
    /// so renumbering would silently re-state every authored entry.</para>
    /// </summary>
    public enum MenuAvailability
    {
        /// <summary>Does its job — opens its modal, navigates to its screen, selects its tab.</summary>
        Available = 0,

        /// <summary>
        /// Exists but is closed off: <i>this exists and you cannot open it yet</i>. Stays pressable
        /// on purpose — the press is how the player is TOLD, so it refuses with a sting and a reason.
        /// </summary>
        Locked = 1,

        /// <summary>
        /// Not built: <i>this is not a thing yet</i>. Reads as inert and does not respond, because
        /// it has nothing to say.
        /// </summary>
        Unavailable = 2,
    }
}
