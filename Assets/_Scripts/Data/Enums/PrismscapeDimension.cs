namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types

    /// <summary>
    /// The dimensionality of a prismscape - the shape class of the structure a prism belongs
    /// to. Prisms/Prismscapes are a platform fundamental, and trails have always been "the
    /// 1-dimensional case"; this names the whole ladder so systems can speak about it.
    ///
    /// The values ARE the dimension, which is why they are pinned 0-3:
    ///
    ///   0 Singleton - a lone prism: a planted block, a one-prism remnant.
    ///   1 Trail     - a ribbon: the player wake, any <c>Trail</c>-backed lay. Ridden by
    ///                 sliding ALONG it (TrailFollower).
    ///   2 Surface   - a shell: gyroid and Schwarz-P flora, walls, membrane-like builds.
    ///                 Ridden by rolling ACROSS it (BlockscapeFollower).
    ///   3 Volume    - a solid: dense lattices, filled structures. A rider stays on its
    ///                 boundary, which is locally a Surface - the distinction matters to
    ///                 queries and spawning, not to riding.
    /// </summary>
    public enum PrismscapeDimension
    {
        Singleton = 0,
        Trail = 1,
        Surface = 2,
        Volume = 3,
    }
}
