namespace CosmicShore.Data
{
    /// <summary>
    /// Who flies a squad slot (Docs/MENU_REDESIGN_B.md, the Ark's Squads tab). The leader's slot is always the squad's
    /// maker; each rogue slot is an AI wingmate, a seat held open for a friend to join, or a friend who joined it.
    ///
    /// <para>Values are fixed and never reordered: they serialize into the saved squads (SQUAD_DATA) and the public
    /// squad record friends read (SQUADS_PUBLIC). <see cref="Ai"/> is 0 so a slot saved before seats existed reads as an
    /// AI wingmate, which is what it was.</para>
    /// </summary>
    public enum SquadSeat
    {
        /// <summary>An AI wingmate flies it; its owner picks the vessel, element, pilot and artifact.</summary>
        Ai = 0,
        /// <summary>Held for a friend: the first friend who joins takes it. An AI flies it until then.</summary>
        Open = 1,
        /// <summary>A player holds it (the leader, or a friend who joined); they pick their own four.</summary>
        Player = 2,
    }
}
