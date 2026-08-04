namespace CosmicShore.Data
{
    /// <summary>
    /// The three fauna types that compose a worm colony (Docs/ECOSYSTEM.md §23).
    /// A segment's role decides its threat surface and its death consequence:
    /// Head and Tail carry danger prisms and an elemental heart (they are the
    /// colony's capital lifeforms — killing one drops its crystal); a Body
    /// segment is vulnerable connective tissue whose death SPLITS the worm in
    /// two. Roles are not fixed for life: when an end dies, the neighboring
    /// Body segment DIFFERENTIATES into the missing role (see
    /// WormFauna.TickDifferentiation).
    /// </summary>
    public enum WormSegmentRole
    {
        Body = 0,
        Head = 1,
        Tail = 2,
    }
}
