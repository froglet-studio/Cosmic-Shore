namespace CosmicShore.Data
{
    /// <summary>
    /// The three fauna types that compose a worm colony (Docs/ECOSYSTEM.md §23).
    /// EVERY role is a lifeform carrying its own elemental heart — a worm is a
    /// POPULATION, not a creature with body parts (§23.3) — and a role decides
    /// only the member's threat surface and its death consequence: Head and Tail
    /// carry danger prisms, while a Body segment is soft tissue whose death SPLITS
    /// the population in two. A role is FIXED at birth: a colony that loses an end
    /// GROWS a real replacement on its host cell's next fauna production cycle
    /// (WormFauna.TickProduction, §23.9), never by hardening a body segment.
    /// </summary>
    public enum WormSegmentRole
    {
        Body = 0,
        Head = 1,
        Tail = 2,
    }
}
