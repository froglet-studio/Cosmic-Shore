namespace CosmicShore.Data
{
    /// <summary>
    /// The gameplay "state" a spawned prism is themed with, orthogonal to its <see cref="Domains"/>
    /// colour. Used by the environment spawners and the freestyle microscene conveyor to lay
    /// variety beyond plain prisms - hazards to weave, tougher accents, immovable landmarks.
    ///
    /// Numeric values are explicit to prevent Unity serialization drift (CLAUDE.md ▸ Code Style).
    /// Collider-budget note: <see cref="Plain"/> and <see cref="Danger"/> ride a LOD-cullable
    /// <c>BoxCollider</c>; <see cref="Shielded"/> and <see cref="SuperShielded"/> swap to an
    /// always-on convex <c>MeshCollider</c> that collider-LOD cannot reclaim - keep them rare.
    /// </summary>
    public enum PrismKind
    {
        Plain = 0,
        Danger = 1,
        Shielded = 2,
        SuperShielded = 3,
    }
}
