namespace CosmicShore.Data
{
    /// <summary>
    /// The gameplay "state" a spawned prism is themed with, orthogonal to its <see cref="Domains"/>
    /// colour. Used by the environment spawners and the freestyle microscene conveyor to lay
    /// variety beyond plain prisms - hazards to weave, tougher accents, immovable landmarks.
    ///
    /// Numeric values are explicit to prevent Unity serialization drift (CLAUDE.md ▸ Code Style).
    /// Collider-budget note: ALL FOUR kinds ride the same LOD-cullable <c>BoxCollider</c>, so a
    /// kind costs nothing in colliders. A shield swaps the MESH and the mass, never the collider -
    /// <c>shieldMeshCollider.enabled = true</c> appears nowhere in the project (four sites, all
    /// <c>= false</c>), so the octahedron and the stella are look-only and shape-precise shielded
    /// contact runs on the spatial-index SHELL tier instead (Docs/SPATIAL_INDEX.md). What
    /// <see cref="Shielded"/> and <see cref="SuperShielded"/> DO cost is ecological: armoured mass
    /// is never food and leaves the cell's fauna targeting grids, so it persists (CLAUDE.md ▸
    /// "CHARGE armours its mass"). This comment used to claim an always-on convex
    /// <c>MeshCollider</c>; that was false, and it was once cited as a branch-blocking go/no-go
    /// gate (see <see cref="CosmicShore.Gameplay.SpawnableBreakwater"/>, which records the
    /// measurement) - verify a gate before you pay for it. Guarded by
    /// <c>Tools/Build/check_shield_collider_claims.py</c>.
    /// </summary>
    public enum PrismKind
    {
        Plain = 0,
        Danger = 1,
        Shielded = 2,
        SuperShielded = 3,
    }
}
